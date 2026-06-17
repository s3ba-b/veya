using Veya.Shared.Inference;
using Veya.Shared.Safety;
using Veya.TestSupport;
using Xunit;

namespace Veya.Shared.Tests.Inference;

public class AuditingInferenceBackendTests
{
    private sealed class StubBackend(InferenceResponse response) : IInferenceBackend
    {
        public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }

    private sealed class ThrowingBackend(Exception exception) : IInferenceBackend
    {
        public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<InferenceResponse>(exception);
    }

    private static readonly InferenceRequest Request = new(
        SystemPrompt: null,
        Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])],
        Tools: []);

    private static InferenceResponse Reply(int inputTokens, int outputTokens) =>
        new(new ChatMessage(ChatRole.Assistant, [new TextBlock("ok")]), "end_turn", inputTokens, outputTokens);

    [Fact]
    public async Task CompleteAsync_WritesCloudRequestForRemoteBackend()
    {
        var auditLog = new RecordingAuditLog();
        var backend = new AuditingInferenceBackend(
            new StubBackend(Reply(12, 7)), auditLog, "mistral", "mistral-large-latest", isLocal: false);

        var response = await backend.CompleteAsync(Request);

        Assert.Equal(12, response.InputTokens);
        Assert.Equal(7, response.OutputTokens);

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal("cloud.request", auditEvent.EventType);
        var allowedKeys = new[] { "backend", "model", "inputTokens", "outputTokens", "durationMs" };
        Assert.Equal(allowedKeys.Length, auditEvent.Fields.Count);
        Assert.Equal("mistral", auditEvent.Fields["backend"]);
        Assert.Equal("mistral-large-latest", auditEvent.Fields["model"]);
        Assert.Equal(12, auditEvent.Fields["inputTokens"]);
        Assert.Equal(7, auditEvent.Fields["outputTokens"]);
    }

    [Fact]
    public async Task CompleteAsync_WritesLocalRequestForLocalBackend()
    {
        var auditLog = new RecordingAuditLog();
        var backend = new AuditingInferenceBackend(
            new StubBackend(Reply(20, 8)), auditLog, "ollama", "llama3.1", isLocal: true);

        await backend.CompleteAsync(Request);

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal("local.request", auditEvent.EventType);
        Assert.Equal("ollama", auditEvent.Fields["backend"]);
        Assert.Equal("llama3.1", auditEvent.Fields["model"]);
        Assert.Equal(20, auditEvent.Fields["inputTokens"]);
        Assert.Equal(8, auditEvent.Fields["outputTokens"]);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotWriteAuditEvent_WhenInnerThrows()
    {
        var auditLog = new RecordingAuditLog();
        var backend = new AuditingInferenceBackend(
            new ThrowingBackend(new BackendUnavailableException("down")), auditLog, "ollama", "llama3.1", isLocal: true);

        await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.CompleteAsync(Request));

        Assert.Empty(auditLog.Events);
    }
}
