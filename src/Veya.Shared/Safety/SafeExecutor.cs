using System.Diagnostics;
using System.Text;

namespace Veya.Shared.Safety;

/// <summary>
/// Default <see cref="ISafeExecutor"/>: runs an allowlisted binary as a
/// direct process (argv array, no shell), enforcing a hard timeout and
/// per-stream output caps, and audit-logging every decision.
/// </summary>
public sealed class SafeExecutor(
    IReadOnlyDictionary<string, CommandSpec> allowlist,
    IAuditLog auditLog,
    TimeSpan? timeout = null,
    int maxOutputBytes = 64 * 1024) : ISafeExecutor
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(5);

    // After a timeout we kill the process and reap it, but a detached descendant
    // that inherited a redirected pipe can keep WaitForExitAsync from completing
    // (it waits for output EOF). Never block on that — the hard timeout must hold.
    private static readonly TimeSpan ReapGrace = TimeSpan.FromSeconds(1);

    public async Task<ExecResult> RunAsync(ExecRequest request, CancellationToken cancellationToken = default)
    {
        if (!allowlist.TryGetValue(request.Binary, out var spec) || !spec.ArgumentsAllowed(request.Arguments))
        {
            await auditLog.WriteAsync(AuditEvent.ToolExecDenied(request.Tool, request.Binary, request.Arguments), cancellationToken);
            throw new CommandNotAllowedException(request.Binary);
        }

        var result = request.Detached
            ? await RunDetachedProcessAsync(spec.Path, request.Arguments, request.StandardInput, cancellationToken)
            : await RunProcessAsync(spec.Path, request.Arguments, request.StandardInput, cancellationToken);

        await auditLog.WriteAsync(AuditEvent.ToolExecAllowed(request.Tool, request.Binary, request.Arguments, result), cancellationToken);
        return result;
    }

    private async Task<ExecResult> RunProcessAsync(string path, IReadOnlyList<string> arguments, string? standardInput, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new BoundedBuffer(maxOutputBytes);
        var stderr = new BoundedBuffer(maxOutputBytes);
        process.OutputDataReceived += (_, e) => stdout.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => stderr.AppendLine(e.Data);

        var stopwatch = Stopwatch.StartNew();
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }

        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = timeoutCts.IsCancellationRequested;
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited.
            }

            // Bounded reap. A detached descendant holding a redirected pipe can
            // keep this from ever completing, so cap it — the timeout is hard.
            using var reapCts = new CancellationTokenSource(ReapGrace);
            try
            {
                await process.WaitForExitAsync(reapCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Gave up reaping; proceed with whatever output was captured.
            }

            if (!timedOut)
            {
                throw;
            }
        }

        stopwatch.Stop();

        return new ExecResult(
            TryGetExitCode(process),
            stdout.ToString(),
            stderr.ToString(),
            stopwatch.Elapsed,
            timedOut,
            stdout.Truncated,
            stderr.Truncated);
    }

    /// <summary>
    /// Runs a fire-and-forget command (<see cref="ExecRequest.Detached"/>):
    /// stdout/stderr are left uncaptured so a persistent helper cannot hold a
    /// pipe open, and we wait only a bounded time for the foreground process to
    /// exit. Any survivor (e.g. the <c>wl-copy</c> daemon serving the clipboard)
    /// is intentionally left running.
    /// </summary>
    private async Task<ExecResult> RunDetachedProcessAsync(string path, IReadOnlyList<string> arguments, string? standardInput, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = false,
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        var stopwatch = Stopwatch.StartNew();
        process.Start();

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }

        // No output is captured, so WaitForExitAsync waits only for process exit
        // (no reader EOF coupling). Bound it anyway: a well-behaved helper forks
        // and the foreground process exits promptly.
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var exited = true;
        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Foreground process is still running past the grace window; leave it.
            exited = false;
        }

        stopwatch.Stop();

        return new ExecResult(
            exited ? TryGetExitCode(process) : 0,
            string.Empty,
            string.Empty,
            stopwatch.Elapsed,
            TimedOut: false,
            StdoutTruncated: false,
            StderrTruncated: false);
    }

    private static int TryGetExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            // Process has not exited (e.g. a survivor we declined to wait on).
            return -1;
        }
    }

    /// <summary>Accumulates output lines up to a byte budget, flagging truncation.</summary>
    private sealed class BoundedBuffer(int maxBytes)
    {
        private readonly StringBuilder _builder = new();
        private int _bytes;

        public bool Truncated { get; private set; }

        public void AppendLine(string? line)
        {
            if (line is null || Truncated)
            {
                return;
            }

            var lineBytes = Encoding.UTF8.GetByteCount(line) + 1;
            if (_bytes + lineBytes > maxBytes)
            {
                Truncated = true;
                return;
            }

            _builder.AppendLine(line);
            _bytes += lineBytes;
        }

        public override string ToString() => _builder.ToString();
    }
}
