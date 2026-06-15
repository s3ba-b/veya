namespace Veya.Daemon;

/// <summary>
/// Combines several <see cref="IContextProvider"/>s into one, so
/// <see cref="ModelRouter"/> keeps folding a single context block. Queries each
/// provider, concatenates the non-null blocks (blank line between), and returns
/// <c>null</c> if every provider returned nothing.
/// </summary>
public sealed class CompositeContextProvider(IReadOnlyList<IContextProvider> providers) : IContextProvider
{
    public async Task<string?> GetContextBlockAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var blocks = new List<string>();
        foreach (var provider in providers)
        {
            var block = await provider.GetContextBlockAsync(prompt, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(block))
            {
                blocks.Add(block);
            }
        }

        if (blocks.Count == 0)
        {
            return null;
        }

        return string.Join("\n\n", blocks);
    }
}
