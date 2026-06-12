using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Sage.Shared.Safety;

namespace Sage.Shared.Inference;

/// <summary>
/// <see cref="IInferenceBackend"/> backed by the Anthropic Claude API
/// (docs/architecture.md, "Model router"). Maps <see cref="InferenceRequest"/>
/// to the Anthropic SDK's messages-create call and writes one
/// <c>cloud.request</c> audit event per call, with no message content.
/// </summary>
public sealed class ClaudeBackend : IInferenceBackend
{
    private const long DefaultMaxTokens = 4096;

    private readonly IApiKeyProvider _apiKeyProvider;
    private readonly IAuditLog _auditLog;
    private readonly string _model;
    private readonly HttpClient? _httpClient;

    /// <param name="apiKeyProvider">Supplies the Anthropic API key.</param>
    /// <param name="auditLog">Receives one <c>cloud.request</c> event per call.</param>
    /// <param name="model">The Claude model name, e.g. <c>"claude-sonnet-4-6"</c>.</param>
    /// <param name="httpClient">
    /// Optional transport override for the underlying <see cref="AnthropicClient"/>,
    /// used by tests to avoid real network access.
    /// </param>
    public ClaudeBackend(IApiKeyProvider apiKeyProvider, IAuditLog auditLog, string model, HttpClient? httpClient = null)
    {
        _apiKeyProvider = apiKeyProvider;
        _auditLog = auditLog;
        _model = model;
        _httpClient = httpClient;
    }

    public async Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = await _apiKeyProvider.GetApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new BackendUnavailableException("No Anthropic API key is configured. Set the ANTHROPIC_API_KEY environment variable.");
        }

        var options = new ClientOptions { ApiKey = apiKey };
        if (_httpClient is not null)
        {
            options.HttpClient = _httpClient;
        }

        var client = new AnthropicClient(options);
        var parameters = ToMessageCreateParams(request, _model);

        var stopwatch = Stopwatch.StartNew();
        var message = await client.Messages.Create(parameters, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        await _auditLog.WriteAsync(
            AuditEvent.CloudRequest("claude", _model, (int)message.Usage.InputTokens, (int)message.Usage.OutputTokens, stopwatch.Elapsed),
            cancellationToken).ConfigureAwait(false);

        return ToInferenceResponse(message);
    }

    private static MessageCreateParams ToMessageCreateParams(InferenceRequest request, string model)
    {
        var parameters = new MessageCreateParams
        {
            Model = model,
            MaxTokens = DefaultMaxTokens,
            Messages = request.Messages.Select(ToMessageParam).ToList(),
            Tools = request.Tools.Select(ToToolUnion).ToList(),
        };

        if (request.SystemPrompt is not null)
        {
            parameters = new MessageCreateParams(parameters) { System = request.SystemPrompt };
        }

        return parameters;
    }

    private static MessageParam ToMessageParam(ChatMessage message) =>
        new()
        {
            Role = message.Role == ChatRole.User ? Role.User : Role.Assistant,
            Content = message.Content.Select(ToContentBlockParam).ToList(),
        };

    private static ContentBlockParam ToContentBlockParam(ContentBlock block) => block switch
    {
        TextBlock text => new TextBlockParam(text.Text),
        ToolUseBlock toolUse => new ToolUseBlockParam
        {
            ID = toolUse.Id,
            Name = toolUse.Name,
            Input = ToJsonDictionary(toolUse.Input),
        },
        ToolResultBlock toolResult => new ToolResultBlockParam(toolResult.ToolUseId)
        {
            Content = toolResult.Content,
            IsError = toolResult.IsError,
        },
        _ => throw new NotSupportedException($"Unsupported content block type: {block.GetType().Name}"),
    };

    private static ToolUnion ToToolUnion(ToolDefinition tool) =>
        new Tool
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = new InputSchema(ToJsonDictionary(tool.InputSchema)),
        };

    private static InferenceResponse ToInferenceResponse(Message message)
    {
        var blocks = new List<ContentBlock>();
        foreach (var block in message.Content)
        {
            if (block.TryPickText(out var text))
            {
                blocks.Add(new TextBlock(text.Text));
            }
            else if (block.TryPickToolUse(out var toolUse))
            {
                blocks.Add(new ToolUseBlock(toolUse.ID, toolUse.Name, ToJsonElement(toolUse.Input)));
            }
        }

        return new InferenceResponse(
            new ChatMessage(ChatRole.Assistant, blocks),
            (string)message.StopReason!,
            (int)message.Usage.InputTokens,
            (int)message.Usage.OutputTokens);
    }

    private static IReadOnlyDictionary<string, JsonElement> ToJsonDictionary(JsonElement element)
    {
        var dictionary = new Dictionary<string, JsonElement>();
        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = property.Value.Clone();
        }

        return dictionary;
    }

    private static JsonElement ToJsonElement(IReadOnlyDictionary<string, JsonElement> dictionary)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in dictionary)
            {
                writer.WritePropertyName(key);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        stream.Position = 0;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }
}
