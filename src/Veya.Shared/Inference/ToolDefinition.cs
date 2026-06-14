using System.Text.Json;

namespace Veya.Shared.Inference;

/// <summary>
/// A tool the model may call, described in the same shape MCP tool
/// definitions use (name, description, JSON Schema input).
/// </summary>
public sealed record ToolDefinition(string Name, string Description, JsonElement InputSchema);
