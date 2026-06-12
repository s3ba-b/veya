using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Sage.Shared.Inference;

namespace Sage.Daemon.Mcp;

/// <summary>
/// <see cref="IMcpToolGateway"/> backed by a real Sage.McpServer child
/// process, connected over stdio via the <c>ModelContextProtocol</c> client
/// SDK. The connection is established lazily on first use and reused for the
/// lifetime of the gateway; if the server can't be reached, tool discovery
/// returns an empty list and <see cref="Sage.Daemon.ModelRouter"/> falls back
/// to plain-text answers (no tool definitions are sent to the model).
/// </summary>
public sealed class McpToolGateway(IOptions<McpServerOptions> options, ILogger<McpToolGateway> logger)
    : IMcpToolGateway, IAsyncDisposable
{
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private McpClient? _client;
    private IReadOnlyList<ToolDefinition>? _tools;
    private IReadOnlyDictionary<string, McpClientTool> _toolsByName = new Dictionary<string, McpClientTool>();

    public async Task<IReadOnlyList<ToolDefinition>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        return _tools ?? [];
    }

    public async Task<string> ExecuteAsync(string toolName, JsonElement input, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        if (!_toolsByName.TryGetValue(toolName, out var tool))
        {
            throw new InvalidOperationException($"Tool '{toolName}' is not available from the MCP server.");
        }

        var result = await tool.CallAsync(ToArguments(input), cancellationToken: cancellationToken).ConfigureAwait(false);
        return ToResultText(result);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_tools is not null)
        {
            return;
        }

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tools is not null)
            {
                return;
            }

            var serverOptions = options.Value;
            var serverPath = serverOptions.ServerPath ?? Path.Combine(AppContext.BaseDirectory, "Sage.McpServer");

            try
            {
                var transport = new StdioClientTransport(new StdioClientTransportOptions
                {
                    Command = serverPath,
                    Arguments = serverOptions.Arguments.ToList(),
                });

                _client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken).ConfigureAwait(false);
                var tools = await _client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                _toolsByName = tools.ToDictionary(tool => tool.Name);
                _tools = tools.Select(tool => new ToolDefinition(tool.Name, tool.Description ?? string.Empty, tool.JsonSchema)).ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Sage.McpServer is unavailable at '{ServerPath}'; continuing without tools.", serverPath);
                _tools = [];
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private static IReadOnlyDictionary<string, object?> ToArguments(JsonElement input)
    {
        var arguments = new Dictionary<string, object?>();
        foreach (var property in input.EnumerateObject())
        {
            arguments[property.Name] = property.Value;
        }

        return arguments;
    }

    private static string ToResultText(CallToolResult result)
    {
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        return result.IsError == true ? $"Error: {text}" : text;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }
    }
}
