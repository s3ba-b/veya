using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Veya.Shared.Inference;

/// <summary>
/// <see cref="IInferenceBackend"/> backed by Mistral's hosted API ("La
/// Plateforme", ADR-0008). Maps <see cref="InferenceRequest"/> to Mistral's
/// OpenAI-compatible <c>/v1/chat/completions</c> JSON. Audit logging is layered
/// on by <see cref="AuditingInferenceBackend"/>, not by this backend; like
/// <see cref="ClaudeBackend"/>, data leaves the machine, so it is audited as a
/// cloud (not local) event.
/// </summary>
public sealed class MistralBackend : IInferenceBackend
{
    private const int DefaultMaxTokens = 4096;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly IApiKeyProvider _apiKeyProvider;
    private readonly string _baseUrl;
    private readonly string _model;

    /// <param name="httpClient">Transport used to call the Mistral HTTP API. Tests supply a fake handler.</param>
    /// <param name="apiKeyProvider">Supplies the Mistral API key (<c>MISTRAL_API_KEY</c>).</param>
    /// <param name="options">Base URL and model name (ADR-0008).</param>
    public MistralBackend(HttpClient httpClient, IApiKeyProvider apiKeyProvider, MistralOptions options)
    {
        _httpClient = httpClient;
        _apiKeyProvider = apiKeyProvider;
        _baseUrl = options.BaseUrl.TrimEnd('/');
        _model = options.Model;
    }

    public async Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await _apiKeyProvider.GetApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new BackendUnavailableException("No Mistral API key is configured. Set the MISTRAL_API_KEY environment variable.");
        }

        var payload = ToMistralRequest(request, _model);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions")
        {
            Content = JsonContent.Create(payload, options: SerializerOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new BackendUnavailableException(
                $"Veya can't reach the Mistral API at {_baseUrl}. Check your network connection.", ex);
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new BackendUnavailableException($"Mistral returned {(int)httpResponse.StatusCode}: {body}");
        }

        var mistralResponse = await httpResponse.Content.ReadFromJsonAsync<MistralResponse>(SerializerOptions, cancellationToken)
            .ConfigureAwait(false) ?? throw new BackendUnavailableException("Mistral returned an empty response.");

        return ToInferenceResponse(mistralResponse);
    }

    private static MistralRequest ToMistralRequest(InferenceRequest request, string model)
    {
        var messages = new List<MistralMessage>();
        if (request.SystemPrompt is not null)
        {
            messages.Add(new MistralMessage("system", request.SystemPrompt));
        }

        foreach (var message in request.Messages)
        {
            AppendMessages(message, messages);
        }

        return new MistralRequest(
            model,
            messages,
            DefaultMaxTokens,
            request.Tools.Count > 0 ? request.Tools.Select(ToMistralTool).ToList() : null,
            request.Tools.Count > 0 ? "auto" : null);
    }

    private static void AppendMessages(ChatMessage message, List<MistralMessage> messages)
    {
        if (message.Role == ChatRole.Assistant)
        {
            var text = string.Concat(message.Content.OfType<TextBlock>().Select(block => block.Text));
            var toolCalls = message.Content.OfType<ToolUseBlock>()
                .Select(toolUse => new MistralToolCall(toolUse.Id, "function", new MistralFunctionCall(toolUse.Name, toolUse.Input.GetRawText())))
                .ToList();

            // Mistral wants null (not "") content when the turn is only tool calls.
            messages.Add(new MistralMessage(
                "assistant",
                text.Length > 0 ? text : null,
                toolCalls.Count > 0 ? toolCalls : null));
            return;
        }

        var textBlocks = message.Content.OfType<TextBlock>().ToList();
        if (textBlocks.Count > 0)
        {
            messages.Add(new MistralMessage("user", string.Concat(textBlocks.Select(block => block.Text))));
        }

        foreach (var result in message.Content.OfType<ToolResultBlock>())
        {
            messages.Add(new MistralMessage("tool", result.Content, ToolCallId: result.ToolUseId));
        }
    }

    private static MistralTool ToMistralTool(ToolDefinition tool) =>
        new("function", new MistralFunctionDef(tool.Name, tool.Description, tool.InputSchema));

    private static InferenceResponse ToInferenceResponse(MistralResponse response)
    {
        var choice = response.Choices.FirstOrDefault()
            ?? throw new BackendUnavailableException("Mistral returned no choices.");

        var blocks = new List<ContentBlock>();
        if (!string.IsNullOrEmpty(choice.Message.Content))
        {
            blocks.Add(new TextBlock(choice.Message.Content));
        }

        var toolCalls = choice.Message.ToolCalls ?? [];
        foreach (var toolCall in toolCalls)
        {
            blocks.Add(new ToolUseBlock(toolCall.Id, toolCall.Function.Name, ParseArguments(toolCall.Function.Arguments)));
        }

        // Mistral's finish_reason is "tool_calls" when it wants a tool; map it to
        // the same "tool_use" the rest of Veya (and ClaudeBackend) expects.
        var stopReason = choice.FinishReason == "tool_calls" ? "tool_use" : "end_turn";

        return new InferenceResponse(
            new ChatMessage(ChatRole.Assistant, blocks),
            stopReason,
            response.Usage.PromptTokens,
            response.Usage.CompletionTokens);
    }

    // Mistral returns tool-call arguments as a JSON string; older/other servers
    // may return an object. Handle both so callers always get an object element.
    private static JsonElement ParseArguments(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.String)
        {
            return arguments.Clone();
        }

        var raw = arguments.GetString();
        if (string.IsNullOrEmpty(raw))
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }

        using var document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    private sealed record MistralRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] List<MistralMessage> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("tools")] List<MistralTool>? Tools,
        [property: JsonPropertyName("tool_choice")] string? ToolChoice);

    private sealed record MistralMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content = null,
        [property: JsonPropertyName("tool_calls")] List<MistralToolCall>? ToolCalls = null,
        [property: JsonPropertyName("tool_call_id")] string? ToolCallId = null);

    private sealed record MistralToolCall(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] MistralFunctionCall Function);

    private sealed record MistralFunctionCall(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("arguments")] string Arguments);

    private sealed record MistralTool(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] MistralFunctionDef Function);

    private sealed record MistralFunctionDef(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("parameters")] JsonElement Parameters);

    private sealed record MistralResponse(
        [property: JsonPropertyName("choices")] List<MistralChoice> Choices,
        [property: JsonPropertyName("usage")] MistralUsage Usage);

    private sealed record MistralChoice(
        [property: JsonPropertyName("message")] MistralResponseMessage Message,
        [property: JsonPropertyName("finish_reason")] string FinishReason);

    private sealed record MistralResponseMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("tool_calls")] List<MistralResponseToolCall>? ToolCalls);

    private sealed record MistralResponseToolCall(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("function")] MistralResponseFunctionCall Function);

    private sealed record MistralResponseFunctionCall(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("arguments")] JsonElement Arguments);

    private sealed record MistralUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);
}
