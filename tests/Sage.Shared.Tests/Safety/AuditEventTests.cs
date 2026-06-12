using Sage.Shared.Safety;
using Xunit;

namespace Sage.Shared.Tests.Safety;

public class AuditEventTests
{
    [Fact]
    public void CloudRequest_HasExpectedFieldsAndType()
    {
        var auditEvent = AuditEvent.CloudRequest("claude", "claude-sonnet-4-6", inputTokens: 123, outputTokens: 45, duration: TimeSpan.FromMilliseconds(678));

        Assert.Equal("cloud.request", auditEvent.EventType);
        Assert.Equal("claude", auditEvent.Fields["backend"]);
        Assert.Equal("claude-sonnet-4-6", auditEvent.Fields["model"]);
        Assert.Equal(123, auditEvent.Fields["inputTokens"]);
        Assert.Equal(45, auditEvent.Fields["outputTokens"]);
        Assert.Equal(678d, auditEvent.Fields["durationMs"]);
    }

    [Fact]
    public void CloudRequest_DoesNotIncludeMessageContent()
    {
        var auditEvent = AuditEvent.CloudRequest("claude", "claude-sonnet-4-6", inputTokens: 1, outputTokens: 1, duration: TimeSpan.Zero);

        var allowedKeys = new[] { "backend", "model", "inputTokens", "outputTokens", "durationMs" };
        Assert.Equal(allowedKeys.Length, auditEvent.Fields.Count);
        foreach (var key in allowedKeys)
        {
            Assert.Contains(key, auditEvent.Fields.Keys);
        }
    }
}
