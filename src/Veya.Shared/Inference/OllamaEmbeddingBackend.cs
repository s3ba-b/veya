using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Veya.Shared.Safety;

namespace Veya.Shared.Inference;

/// <summary>
/// <see cref="IEmbeddingBackend"/> backed by a local Ollama server (ADR-0009).
/// Calls Ollama's <c>/api/embed</c> (batch) endpoint and writes one
/// <c>local.request</c> audit event per call — like <see cref="OllamaBackend"/>,
/// nothing leaves the machine, so this never trips <c>CloudUsage</c>.
/// </summary>
public sealed class OllamaEmbeddingBackend : IEmbeddingBackend
{
    private readonly HttpClient _httpClient;
    private readonly IAuditLog _auditLog;
    private readonly string _baseUrl;
    private readonly string _model;

    /// <param name="httpClient">Transport used to call the Ollama HTTP API. Tests supply a fake handler.</param>
    /// <param name="auditLog">Receives one <c>local.request</c> event per call.</param>
    /// <param name="options">Base URL and embedding model name (ADR-0009).</param>
    public OllamaEmbeddingBackend(HttpClient httpClient, IAuditLog auditLog, OllamaOptions options)
    {
        _httpClient = httpClient;
        _auditLog = auditLog;
        _baseUrl = options.BaseUrl.TrimEnd('/');
        _model = options.EmbeddingModel;
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        var payload = new OllamaEmbedRequest(_model, texts);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/embed", payload, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new BackendUnavailableException(
                $"Veya can't reach the local embedding backend (Ollama) at {_baseUrl}. Make sure Ollama is running.", ex);
        }

        stopwatch.Stop();

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new BackendUnavailableException($"Ollama returned {(int)httpResponse.StatusCode}: {body}");
        }

        var response = await httpResponse.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken)
            .ConfigureAwait(false) ?? throw new BackendUnavailableException("Ollama returned an empty response.");

        if (response.Embeddings is null || response.Embeddings.Count != texts.Count)
        {
            throw new BackendUnavailableException(
                $"Ollama returned {response.Embeddings?.Count ?? 0} embeddings for {texts.Count} inputs.");
        }

        await _auditLog.WriteAsync(
            AuditEvent.LocalRequest("ollama", _model, response.PromptEvalCount, 0, stopwatch.Elapsed),
            cancellationToken).ConfigureAwait(false);

        return response.Embeddings;
    }

    private sealed record OllamaEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input);

    private sealed record OllamaEmbedResponse(
        [property: JsonPropertyName("embeddings")] List<float[]>? Embeddings,
        [property: JsonPropertyName("prompt_eval_count")] int PromptEvalCount);
}
