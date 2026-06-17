using Veya.Shared.Inference;

namespace Veya.Daemon.Tests;

/// <summary>Test double for <see cref="IModelRouter"/>: lets a test script the reply (or throw) per prompt.</summary>
internal sealed class FakeModelRouter(Func<string, Task<string>> respond) : IModelRouter
{
    public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default) => respond(prompt);
}
