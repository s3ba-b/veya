using Veya.Shared.Context;
using Veya.Shared.Permissions;
using Xunit;

namespace Veya.Shared.Tests.Context;

public class FileContextSourceTests : IDisposable
{
    private readonly string _root;

    public FileContextSourceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "veya-file-ctx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private async Task<List<ContextChunk>> ReadAllAsync(FileContextOptions options)
    {
        var source = new FileContextSource(options);
        var chunks = new List<ContextChunk>();
        await foreach (var chunk in source.ReadAsync())
        {
            chunks.Add(chunk);
        }

        return chunks;
    }

    private FileContextOptions Options() => new() { Roots = [_root] };

    [Fact]
    public void Source_IsFilesPermission()
    {
        Assert.Equal(PermissionSource.Files, new FileContextSource(Options()).Source);
    }

    [Fact]
    public async Task ReadAsync_IndexesMatchingExtensionsAndSetsOriginAndId()
    {
        Write("note.md", "hello world");

        var chunks = await ReadAllAsync(Options());

        var chunk = Assert.Single(chunks);
        Assert.Equal(PermissionSource.Files, chunk.Source);
        Assert.Equal(Path.Combine(_root, "note.md"), chunk.Origin);
        Assert.Equal($"{Path.Combine(_root, "note.md")}#0", chunk.Id);
        Assert.Equal("hello world", chunk.Text);
    }

    [Fact]
    public async Task ReadAsync_SkipsNonMatchingExtensions()
    {
        Write("keep.txt", "indexed");
        Write("skip.log", "ignored");
        Write("skip.bin", "ignored");

        var chunks = await ReadAllAsync(Options());

        var chunk = Assert.Single(chunks);
        Assert.Equal("indexed", chunk.Text);
    }

    [Fact]
    public async Task ReadAsync_SkipsFilesOverMaxBytes()
    {
        Write("big.txt", new string('x', 100));

        var options = Options();
        options.MaxFileBytes = 10;

        Assert.Empty(await ReadAllAsync(options));
    }

    [Fact]
    public async Task ReadAsync_RecursesIntoSubdirectories()
    {
        Write(Path.Combine("sub", "deep", "note.txt"), "nested");

        var chunks = await ReadAllAsync(Options());

        Assert.Equal("nested", Assert.Single(chunks).Text);
    }

    [Fact]
    public async Task ReadAsync_EmitsOrdinalChunkIdsForLongFiles()
    {
        Write("long.txt", new string('a', 10) + "\n\n" + new string('b', 10));

        var options = Options();
        options.MaxChunkChars = 10;
        var chunks = await ReadAllAsync(options);

        var path = Path.Combine(_root, "long.txt");
        Assert.Equal(2, chunks.Count);
        Assert.Equal($"{path}#0", chunks[0].Id);
        Assert.Equal($"{path}#1", chunks[1].Id);
    }

    [Fact]
    public async Task ReadAsync_SkipsNonexistentRootWithoutThrowing()
    {
        var options = new FileContextOptions { Roots = [Path.Combine(_root, "does-not-exist")] };

        Assert.Empty(await ReadAllAsync(options));
    }
}
