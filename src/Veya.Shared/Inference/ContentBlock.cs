using System.Text.Json;

namespace Veya.Shared.Inference;

/// <summary>One piece of a <see cref="ChatMessage"/>'s content.</summary>
public abstract record ContentBlock;

/// <summary>Plain text content.</summary>
public sealed record TextBlock(string Text) : ContentBlock;

/// <summary>A model-issued request to call a tool.</summary>
public sealed record ToolUseBlock(string Id, string Name, JsonElement Input) : ContentBlock;

/// <summary>The result of executing a <see cref="ToolUseBlock"/>, sent back to the model.</summary>
public sealed record ToolResultBlock(string ToolUseId, string Content, bool IsError = false) : ContentBlock;
