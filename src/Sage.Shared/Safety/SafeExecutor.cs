using System.Diagnostics;
using System.Text;

namespace Sage.Shared.Safety;

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

    public async Task<ExecResult> RunAsync(ExecRequest request, CancellationToken cancellationToken = default)
    {
        if (!allowlist.TryGetValue(request.Binary, out var spec) || !spec.ArgumentsAllowed(request.Arguments))
        {
            await auditLog.WriteAsync(AuditEvent.ToolExecDenied(request.Tool, request.Binary, request.Arguments), cancellationToken);
            throw new CommandNotAllowedException(request.Binary);
        }

        var result = await RunProcessAsync(spec.Path, request.Arguments, cancellationToken);

        await auditLog.WriteAsync(AuditEvent.ToolExecAllowed(request.Tool, request.Binary, request.Arguments, result), cancellationToken);
        return result;
    }

    private async Task<ExecResult> RunProcessAsync(string path, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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

            await process.WaitForExitAsync(CancellationToken.None);

            if (!timedOut)
            {
                throw;
            }
        }

        stopwatch.Stop();

        return new ExecResult(
            process.ExitCode,
            stdout.ToString(),
            stderr.ToString(),
            stopwatch.Elapsed,
            timedOut,
            stdout.Truncated,
            stderr.Truncated);
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
