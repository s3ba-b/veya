using Sage.Shared.Inference;
using Xunit;

namespace Sage.Shared.Tests.Inference;

public class FallbackInferenceBackendTests
{
    private sealed class FakeBackend(Func<InferenceResponse> respond) : IInferenceBackend
    {
        public int CallCount { get; private set; }

        public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(respond());
        }
    }

    private sealed class ThrowingBackend(Exception exception) : IInferenceBackend
    {
        public int CallCount { get; private set; }

        public Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw exception;
        }
    }

    private static InferenceResponse Response(string text) =>
        new(new ChatMessage(ChatRole.Assistant, [new TextBlock(text)]), "end_turn", 1, 1);

    private static InferenceRequest Request() =>
        new(SystemPrompt: null, Messages: [new ChatMessage(ChatRole.User, [new TextBlock("hi")])], Tools: []);

    [Fact]
    public async Task CompleteAsync_UsesPrimaryWhenItSucceeds()
    {
        var primary = new FakeBackend(() => Response("from primary"));
        var secondary = new FakeBackend(() => Response("from secondary"));
        var backend = new FallbackInferenceBackend(primary, secondary);

        var response = await backend.CompleteAsync(Request());

        Assert.Equal("from primary", Assert.IsType<TextBlock>(response.Message.Content[0]).Text);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(0, secondary.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_FallsBackToSecondaryWhenPrimaryUnavailable()
    {
        var primary = new ThrowingBackend(new BackendUnavailableException("local backend down"));
        var secondary = new FakeBackend(() => Response("from secondary"));
        var backend = new FallbackInferenceBackend(primary, secondary);

        var response = await backend.CompleteAsync(Request());

        Assert.Equal("from secondary", Assert.IsType<TextBlock>(response.Message.Content[0]).Text);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, secondary.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_PropagatesSecondaryFailureWhenBothUnavailable()
    {
        var primary = new ThrowingBackend(new BackendUnavailableException("local backend down"));
        var secondary = new ThrowingBackend(new BackendUnavailableException("cloud backend down"));
        var backend = new FallbackInferenceBackend(primary, secondary);

        var ex = await Assert.ThrowsAsync<BackendUnavailableException>(() => backend.CompleteAsync(Request()));
        Assert.Equal("cloud backend down", ex.Message);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotFallBackOnOtherExceptions()
    {
        var primary = new ThrowingBackend(new InvalidOperationException("boom"));
        var secondary = new FakeBackend(() => Response("from secondary"));
        var backend = new FallbackInferenceBackend(primary, secondary);

        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.CompleteAsync(Request()));
        Assert.Equal(0, secondary.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotFallBackOnCancellation()
    {
        var primary = new ThrowingBackend(new OperationCanceledException());
        var secondary = new FakeBackend(() => Response("from secondary"));
        var backend = new FallbackInferenceBackend(primary, secondary);

        await Assert.ThrowsAsync<OperationCanceledException>(() => backend.CompleteAsync(Request()));
        Assert.Equal(0, secondary.CallCount);
    }
}
