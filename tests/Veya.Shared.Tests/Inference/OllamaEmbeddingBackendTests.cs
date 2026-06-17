using System.Net;
using System.Net.Http;
using System.Text.Json;
using Veya.Shared.Inference;
using Veya.Shared.Safety;
using Veya.TestSupport;
using Xunit;

namespace Veya.Shared.Tests.Inference;

public class OllamaEmbeddingBackendTests
{
    private static OllamaEmbeddingBackend CreateBackend(HttpMessageHandler handler, IAuditLog? auditLog = null, string model = "nomic-embed-text") =>
        new(new HttpClient(handler), auditLog ?? new RecordingAuditLog(), new OllamaOptions { EmbeddingModel = model });

    [Fact]
    public async Task EmbedAsync_ReturnsVectorsAndWritesLocalRequestAuditEvent()
    {
        const string responseJson = """
        {
          "model": "nomic-embed-text",
          "embeddings": [[0.1, 0.2, 0.3], [0.4, 0.5, 0.6]],
          "prompt_eval_count": 9
        }
        """;

        var auditLog = new RecordingAuditLog();
        var backend = CreateBackend(new CapturingHttpMessageHandler(responseJson), auditLog);

        var vectors = await backend.EmbedAsync(["hello", "world"]);

        Assert.Equal(2, vectors.Count);
        Assert.Equal([0.1f, 0.2f, 0.3f], vectors[0]);
        Assert.Equal([0.4f, 0.5f, 0.6f], vectors[1]);

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal("local.request", auditEvent.EventType);
        Assert.Equal("ollama", auditEvent.Fields["backend"]);
        Assert.Equal("nomic-embed-text", auditEvent.Fields["model"]);
        Assert.Equal(9, auditEvent.Fields["inputTokens"]);
        Assert.Equal(0, auditEvent.Fields["outputTokens"]);
    }

    [Fact]
    public async Task EmbedAsync_SendsModelAndInputsToEmbedEndpoint()
    {
        const string responseJson = """
        {"embeddings": [[1.0]], "prompt_eval_count": 1}
        """;

        var handler = new CapturingHttpMessageHandler(responseJson);
        var backend = CreateBackend(handler, model: "mxbai-embed-large");

        await backend.EmbedAsync(["one input"]);

        Assert.EndsWith("/api/embed", handler.LastRequest!.RequestUri!.ToString());

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal("mxbai-embed-large", root.GetProperty("model").GetString());
        Assert.Equal("one input", root.GetProperty("input")[0].GetString());
    }

    [Fact]
    public async Task EmbedAsync_ReturnsEmptyAndDoesNotCallBackend_WhenNoTexts()
    {
        var handler = new ThrowingHttpMessageHandler();
        var auditLog = new RecordingAuditLog();
        var backend = CreateBackend(handler, auditLog);

        var vectors = await backend.EmbedAsync([]);

        Assert.Empty(vectors);
        Assert.Empty(auditLog.Events);
    }

    [Fact]
    public async Task EmbedAsync_ThrowsBackendUnavailable_WhenOllamaUnreachable()
    {
        var backend = CreateBackend(new ThrowingHttpMessageHandler());

        await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.EmbedAsync(["hi"]));
    }

    [Fact]
    public async Task EmbedAsync_ThrowsBackendUnavailable_OnErrorStatusCode()
    {
        var backend = CreateBackend(new CapturingHttpMessageHandler("model not found", HttpStatusCode.NotFound));

        var ex = await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.EmbedAsync(["hi"]));
        Assert.Contains("404", ex.Message);
    }

    [Fact]
    public async Task EmbedAsync_ThrowsBackendUnavailable_OnCountMismatch()
    {
        const string responseJson = """
        {"embeddings": [[0.1, 0.2]], "prompt_eval_count": 4}
        """;

        var backend = CreateBackend(new CapturingHttpMessageHandler(responseJson));

        await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.EmbedAsync(["a", "b"]));
    }
}
