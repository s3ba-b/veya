namespace Veya.Shared.Context;

/// <summary>
/// Deterministic, paragraph-aware text chunking for the file context source
/// (ADR-0010). Splits on blank lines into paragraphs, packs consecutive
/// paragraphs into chunks up to a character cap, and hard-splits any single
/// paragraph that exceeds the cap. Deterministic so re-runs produce identical
/// chunk boundaries (and therefore stable chunk ids).
/// </summary>
public static class TextChunker
{
    public static IReadOnlyList<string> Chunk(string text, int maxChunkChars)
    {
        if (maxChunkChars <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChunkChars), "Chunk size must be positive.");
        }

        var chunks = new List<string>();
        var current = new List<string>();
        var currentLength = 0;

        void Flush()
        {
            if (current.Count > 0)
            {
                chunks.Add(string.Join("\n\n", current));
                current.Clear();
                currentLength = 0;
            }
        }

        foreach (var paragraph in SplitParagraphs(text))
        {
            if (paragraph.Length > maxChunkChars)
            {
                Flush();
                foreach (var piece in HardSplit(paragraph, maxChunkChars))
                {
                    chunks.Add(piece);
                }

                continue;
            }

            // +2 for the "\n\n" join between paragraphs already in the chunk.
            var added = current.Count == 0 ? paragraph.Length : paragraph.Length + 2;
            if (currentLength + added > maxChunkChars)
            {
                Flush();
            }

            current.Add(paragraph);
            currentLength += current.Count == 1 ? paragraph.Length : paragraph.Length + 2;
        }

        Flush();
        return chunks;
    }

    private static IEnumerable<string> SplitParagraphs(string text)
    {
        foreach (var block in text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = block.Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    private static IEnumerable<string> HardSplit(string paragraph, int maxChunkChars)
    {
        for (var offset = 0; offset < paragraph.Length; offset += maxChunkChars)
        {
            yield return paragraph.Substring(offset, Math.Min(maxChunkChars, paragraph.Length - offset));
        }
    }
}
