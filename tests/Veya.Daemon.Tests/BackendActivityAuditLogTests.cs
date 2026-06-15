using Veya.Shared;
using Veya.Shared.Safety;
using Xunit;

namespace Veya.Daemon.Tests;

public class BackendActivityAuditLogTests
{
    private sealed class RecordingAuditLog : IAuditLog
    {
        public List<AuditEvent> Written { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Written.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private static AuditEvent Cloud(string backend = "mistral", string model = "mistral-large", int input = 12, int output = 34) =>
        AuditEvent.CloudRequest(backend, model, input, output, TimeSpan.FromMilliseconds(5));

    private static AuditEvent Local(string backend = "ollama") =>
        AuditEvent.LocalRequest(backend, "llama3", 1, 2, TimeSpan.FromMilliseconds(5));

    [Fact]
    public async Task WriteAsync_ForwardsEveryEventToInnerLog()
    {
        var inner = new RecordingAuditLog();
        var log = new BackendActivityAuditLog(inner);

        var permissionEvent = AuditEvent.PermissionDecision("clipboard", "ClipboardTool", granted: true);
        await log.WriteAsync(permissionEvent);

        Assert.Single(inner.Written);
        Assert.Same(permissionEvent, inner.Written[0]);
    }

    [Fact]
    public async Task WriteAsync_OnCloudRequest_RaisesCloudRequestedWithEventFields()
    {
        var log = new BackendActivityAuditLog(new RecordingAuditLog());
        CloudUsageInfo? received = null;
        log.CloudRequested += info => received = info;

        await log.WriteAsync(Cloud(backend: "claude", model: "claude-sonnet-4-6", input: 100, output: 200));

        Assert.NotNull(received);
        Assert.Equal("claude", received!.Value.Backend);
        Assert.Equal("claude-sonnet-4-6", received.Value.Model);
        Assert.Equal(100u, received.Value.InputTokens);
        Assert.Equal(200u, received.Value.OutputTokens);
    }

    [Fact]
    public async Task WriteAsync_OnLocalRequest_DoesNotRaiseCloudRequested()
    {
        var log = new BackendActivityAuditLog(new RecordingAuditLog());
        var raised = false;
        log.CloudRequested += _ => raised = true;

        await log.WriteAsync(Local());

        Assert.False(raised);
    }

    [Fact]
    public void ActiveBackend_DefaultsToLocalFirstBackend()
    {
        var log = new BackendActivityAuditLog(new RecordingAuditLog());

        Assert.Equal("ollama", log.ActiveBackend);
    }

    [Fact]
    public async Task ActiveBackend_TracksMostRecentlyServedBackend()
    {
        var log = new BackendActivityAuditLog(new RecordingAuditLog());

        await log.WriteAsync(Cloud(backend: "mistral"));
        Assert.Equal("mistral", log.ActiveBackend);

        await log.WriteAsync(Local(backend: "ollama"));
        Assert.Equal("ollama", log.ActiveBackend);
    }
}
