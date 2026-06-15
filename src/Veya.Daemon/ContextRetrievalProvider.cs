using System.Text;
using Veya.Shared.Context;

namespace Veya.Daemon;

/// <summary>
/// <see cref="IContextProvider"/> backed by the personal context index
/// (ADR-0009): retrieves the most relevant chunks via <see cref="ContextRetriever"/>
/// — which enforces per-source permissions at query time and degrades to nothing
/// when the embedding backend is down — and formats them into a prompt block.
/// </summary>
public sealed class ContextRetrievalProvider(ContextRetriever retriever, int maxChunks = 5) : IContextProvider
{
    public async Task<string?> GetContextBlockAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var matches = await retriever.RetrieveAsync(prompt, maxChunks, cancellationToken).ConfigureAwait(false);
        if (matches.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder("Relevant context from the user's approved personal sources:\n");
        foreach (var match in matches)
        {
            builder.Append("- ").AppendLine(match.Chunk.Text);
        }

        return builder.ToString().TrimEnd();
    }
}
