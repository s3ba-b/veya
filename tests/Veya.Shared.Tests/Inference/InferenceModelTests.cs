using System.Text.Json;
using Veya.Shared.Inference;
using Xunit;

namespace Veya.Shared.Tests.Inference;

public class InferenceModelTests
{
    private sealed class EchoBackend : IInferenceBackend
    {
        public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default)
        {
            var lastMessage = request.Messages[^1];
            var text = Assert.IsType<TextBlock>(lastMessage.Content[0]).Text;

            var response = new InferenceResponse(
                new ChatMessage(ChatRole.Assistant, [new TextBlock($"echo: {text}")]),
                StopReason: "end_turn",
                InputTokens: 1,
                OutputTokens: 1);

            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task CompleteAsync_RoundTripsThroughAFakeBackend()
    {
        IInferenceBackend backend = new EchoBackend();

        var request = new InferenceRequest(
            SystemPrompt: "You are Veya.",
            Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hello")])],
            Tools: []);

        var response = await backend.CompleteAsync(request);

        Assert.Equal("end_turn", response.StopReason);
        Assert.Equal(ChatRole.Assistant, response.Message.Role);
        Assert.Equal("echo: hello", Assert.IsType<TextBlock>(response.Message.Content[0]).Text);
    }

    [Fact]
    public void ToolDefinitionAndToolUseBlock_CarryJsonSchemaAndInput()
    {
        var schema = JsonDocument.Parse("""{"type":"object","properties":{"unit":{"type":"string"}}}""").RootElement;
        var tool = new ToolDefinition("get_service_status", "Reports a systemd unit's status.", schema);

        var input = JsonDocument.Parse("""{"unit":"ssh-agent.service"}""").RootElement;
        var toolUse = new ToolUseBlock("call_1", tool.Name, input);

        Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
        Assert.Equal("ssh-agent.service", toolUse.Input.GetProperty("unit").GetString());

        var result = new ToolResultBlock(toolUse.Id, "{\"activeState\":\"active\"}");
        Assert.False(result.IsError);
        Assert.Equal("call_1", result.ToolUseId);
    }
}
