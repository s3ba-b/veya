using System.Runtime.CompilerServices;
using Veya.Shared.Permissions;

namespace Veya.Shared.Context;

/// <summary>
/// An <see cref="IContextSource"/> over user-approved text files (ADR-0010).
/// Walks the configured roots, reads matching files, and yields one chunk per
/// <see cref="TextChunker"/> segment. Gated by <see cref="PermissionSource.Files"/>;
/// <see cref="ContextIndexer"/> checks that before this reads anything.
/// </summary>
/// <remarks>
/// Unreadable files (permissions, IO) are skipped rather than aborting the run,
/// so one bad file does not stop the rest from indexing.
/// </remarks>
public sealed class FileContextSource(FileContextOptions options) : IContextSource
{
    public PermissionSource Source => PermissionSource.Files;

    public async IAsyncEnumerable<ContextChunk> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var extensions = options.Extensions.Select(extension => extension.ToLowerInvariant()).ToHashSet();

        foreach (var root in options.Roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in EnumerateFiles(root, extensions))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var text = TryReadFile(path);
                if (text is null)
                {
                    continue;
                }

                var ordinal = 0;
                foreach (var chunkText in TextChunker.Chunk(text, options.MaxChunkChars))
                {
                    yield return new ContextChunk($"{path}#{ordinal}", PermissionSource.Files, path, chunkText);
                    ordinal++;
                    await Task.Yield();
                }
            }
        }
    }

    private IEnumerable<string> EnumerateFiles(string root, IReadOnlySet<string> extensions)
    {
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var path in entries)
        {
            if (!extensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            {
                continue;
            }

            long size;
            try
            {
                size = new FileInfo(path).Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (size <= options.MaxFileBytes)
            {
                yield return path;
            }
        }
    }

    private static string? TryReadFile(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
