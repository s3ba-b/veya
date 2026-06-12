namespace Sage.Daemon;

/// <summary>
/// Selects an inference backend and answers a single-turn prompt
/// (docs/architecture.md, "Model router").
/// </summary>
public interface IModelRouter
{
    public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default);
}
