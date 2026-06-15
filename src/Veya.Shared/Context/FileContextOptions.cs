namespace Veya.Shared.Context;

/// <summary>
/// Configures <see cref="FileContextSource"/> (ADR-0010). Bound from the
/// <c>Context:Files</c> configuration section. With no <see cref="Roots"/>
/// nothing is indexed (default-deny in spirit: the source is inert until the
/// user points it somewhere).
/// </summary>
public sealed class FileContextOptions
{
    /// <summary>Directories indexed recursively.</summary>
    public IList<string> Roots { get; set; } = [];

    /// <summary>File extensions to index (case-insensitive, leading dot).</summary>
    public IList<string> Extensions { get; set; } = [".txt", ".md"];

    /// <summary>Files larger than this are skipped.</summary>
    public long MaxFileBytes { get; set; } = 1024 * 1024;

    /// <summary>Maximum characters per chunk (paragraph-aware, see <see cref="TextChunker"/>).</summary>
    public int MaxChunkChars { get; set; } = 1000;
}
