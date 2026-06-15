using Veya.Shared.Context;
using Xunit;

namespace Veya.Shared.Tests.Context;

public class TextChunkerTests
{
    [Fact]
    public void Chunk_ReturnsSingleChunk_WhenUnderCap()
    {
        var chunks = TextChunker.Chunk("one paragraph", maxChunkChars: 100);

        Assert.Equal(["one paragraph"], chunks);
    }

    [Fact]
    public void Chunk_PacksParagraphsUpToCap()
    {
        // "aaaa" (4) + "\n\n" (2) + "bbbb" (4) = 10, fits in 10; "cccc" forces a new chunk.
        var text = "aaaa\n\nbbbb\n\ncccc";

        var chunks = TextChunker.Chunk(text, maxChunkChars: 10);

        Assert.Equal(["aaaa\n\nbbbb", "cccc"], chunks);
    }

    [Fact]
    public void Chunk_HardSplitsParagraphLongerThanCap()
    {
        var chunks = TextChunker.Chunk(new string('x', 25), maxChunkChars: 10);

        Assert.Equal(3, chunks.Count);
        Assert.Equal(new string('x', 10), chunks[0]);
        Assert.Equal(new string('x', 10), chunks[1]);
        Assert.Equal(new string('x', 5), chunks[2]);
    }

    [Fact]
    public void Chunk_NormalizesCrLfAndSkipsBlankParagraphs()
    {
        var chunks = TextChunker.Chunk("a\r\n\r\n\r\n\r\nb", maxChunkChars: 100);

        Assert.Equal(["a\n\nb"], chunks);
    }

    [Fact]
    public void Chunk_IsDeterministic()
    {
        const string text = "alpha\n\nbeta gamma\n\ndelta";

        Assert.Equal(TextChunker.Chunk(text, 12), TextChunker.Chunk(text, 12));
    }

    [Fact]
    public void Chunk_Throws_OnNonPositiveCap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextChunker.Chunk("x", 0));
    }
}
