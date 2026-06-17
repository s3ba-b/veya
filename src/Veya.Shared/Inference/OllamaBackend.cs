using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Veya.Shared.Inference;

/// <summary>
/// <see cref="IInferenceBackend"/> backed by a local Ollama server
/// (ADR-0004, docs/architecture.md "Model router"). Maps
/// <see cref="InferenceRequest"/> to Ollama's <c>/api/chat</c> JSON. Audit
/// logging is layered on by <see cref="AuditingInferenceBackend"/>, not by this
/// backend; nothing here leaves the machine.
/// </summary>
public sealed class OllamaBackend : IInferenceBackend
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    /// <param name="httpClient">Transport used to call the Ollama HTTP API. Tests supply a fake handler.</param>
    /// <param name="options">Base URL and model name (ADR-0004).</param>
    public OllamaBackend(HttpClient httpClient, OllamaOptions options)
    {
        _httpClient = httpClient;
        _baseUrl = options.BaseUrl.TrimEnd('/');
        _model = options.Model;
    }

    public async Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default)
    {
        var payload = ToOllamaRequest(request, _model);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/chat", payload, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new BackendUnavailableException(
                $"Veya can't reach the local model backend (Ollama) at {_baseUrl}. Make sure Ollama is running.", ex);
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new BackendUnavailableException($"Ollama returned {(int)httpResponse.StatusCode}: {body}");
        }

        var ollamaResponse = await httpResponse.Content.ReadFromJsonAsync<OllamaResponse>(SerializerOptions, cancellationToken)
            .ConfigureAwait(false) ?? throw new BackendUnavailableException("Ollama returned an empty response.");

        return ToInferenceResponse(ollamaResponse);
    }

    private static OllamaRequest ToOllamaRequest(InferenceRequest request, string model)
    {
        var messages = new List<OllamaMessage>();
        if (request.SystemPrompt is not null)
        {
            messages.Add(new OllamaMessage("system", request.SystemPrompt));
        }

        // Ollama's tool_calls carry no id, so we track id -> tool name from
        // each assistant turn to recover it for the matching `tool` message.
        var toolNamesById = new Dictionary<string, string>();
        foreach (var message in request.Messages)
        {
            AppendMessages(message, toolNamesById, messages);
        }

        return new OllamaRequest(
            model,
            messages,
            request.Tools.Count > 0 ? request.Tools.Select(ToOllamaTool).ToList() : null,
            Stream: false);
    }

    private static void AppendMessages(ChatMessage message, Dictionary<string, string> toolNamesById, List<OllamaMessage> messages)
    {
        if (message.Role == ChatRole.Assistant)
        {
            var text = string.Concat(message.Content.OfType<TextBlock>().Select(block => block.Text));
            var toolCalls = new List<OllamaToolCall>();
            foreach (var toolUse in message.Content.OfType<ToolUseBlock>())
            {
                toolNamesById[toolUse.Id] = toolUse.Name;
                toolCalls.Add(new OllamaToolCall(new OllamaFunctionCall(toolUse.Name, toolUse.Input)));
            }

            messages.Add(new OllamaMessage("assistant", text, toolCalls.Count > 0 ? toolCalls : null));
            return;
        }

        var textBlocks = message.Content.OfType<TextBlock>().ToList();
        if (textBlocks.Count > 0)
        {
            messages.Add(new OllamaMessage("user", string.Concat(textBlocks.Select(block => block.Text))));
        }

        foreach (var result in message.Content.OfType<ToolResultBlock>())
        {
            toolNamesById.TryGetValue(result.ToolUseId, out var toolName);
            messages.Add(new OllamaMessage("tool", result.Content, ToolName: toolName));
        }
    }

    private static OllamaTool ToOllamaTool(ToolDefinition tool) =>
        new("function", new OllamaFunctionDef(tool.Name, tool.Description, tool.InputSchema));

    private static InferenceResponse ToInferenceResponse(OllamaResponse response)
    {
        var blocks = new List<ContentBlock>();
        if (!string.IsNullOrEmpty(response.Message.Content))
        {
            blocks.Add(new TextBlock(response.Message.Content));
        }

        var toolCalls = response.Message.ToolCalls ?? [];
        foreach (var toolCall in toolCalls)
        {
            // Synthesize an id: Ollama's tool_calls don't carry one (ADR-0004).
            blocks.Add(new ToolUseBlock(Guid.NewGuid().ToString(), toolCall.Function.Name, toolCall.Function.Arguments));
        }

        var stopReason = toolCalls.Count > 0 ? "tool_use" : "end_turn";

        return new InferenceResponse(
            new ChatMessage(ChatRole.Assistant, blocks),
            stopReason,
            response.PromptEvalCount,
            response.EvalCount);
    }

    private sealed record OllamaRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] List<OllamaMessage> Messages,
        [property: JsonPropertyName("tools")] List<OllamaTool>? Tools,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content = "",
        [property: JsonPropertyName("tool_calls")] List<OllamaToolCall>? ToolCalls = null,
        [property: JsonPropertyName("tool_name")] string? ToolName = null);

    private sealed record OllamaToolCall(
        [property: JsonPropertyName("function")] OllamaFunctionCall Function);

    private sealed record OllamaFunctionCall(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("arguments")] JsonElement Arguments);

    private sealed record OllamaTool(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] OllamaFunctionDef Function);

    private sealed record OllamaFunctionDef(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("parameters")] JsonElement Parameters);

    private sealed record OllamaResponse(
        [property: JsonPropertyName("message")] OllamaMessage Message,
        [property: JsonPropertyName("prompt_eval_count")] int PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int EvalCount);
}
