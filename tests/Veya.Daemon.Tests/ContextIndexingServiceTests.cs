using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Veya.Daemon;
using Veya.Shared.Context;
using Veya.Shared.Inference;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;
using Xunit;

namespace Veya.Daemon.Tests;

public class ContextIndexingServiceTests
{
    private sealed class NullAuditLog : IAuditLog
    {
        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class GrantAllGate : IPermissionGate
    {
        public Task<bool> CheckAsync(PermissionSource source, string requester, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class LengthEmbeddingBackend : IEmbeddingBackend
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(texts.Select(t => new[] { (float)t.Length, 0f, 0f }).ToList());
    }

    private sealed class ListSource(PermissionSource source, params string[] texts) : IContextSource
    {
        public PermissionSource Source { get; } = source;

        public async IAsyncEnumerable<ContextChunk> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < texts.Length; i++)
            {
                yield return new ContextChunk($"{Source}#{i}", Source, $"origin/{i}", texts[i]);
                await Task.Yield();
            }
        }
    }

    private sealed class ThrowingSource : IContextSource
    {
        public PermissionSource Source => PermissionSource.Files;

        public async IAsyncEnumerable<ContextChunk> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new IOException("disk on fire");
#pragma warning disable CS0162 // Unreachable: yield makes this an iterator.
            yield break;
#pragma warning restore CS0162
        }
    }

    private static ContextIndexer Indexer(IContextStore store) =>
        new(new LengthEmbeddingBackend(), store, new GrantAllGate(), new NullAuditLog());

    [Fact]
    public async Task RunAsync_IndexesEachSource()
    {
        await using var store = new SqliteContextStore("Data Source=:memory:");
        var service = new ContextIndexingService(
            [new ListSource(PermissionSource.Files, "alpha", "beta")],
            Indexer(store),
            NullLogger<ContextIndexingService>.Instance);

        await service.RunAsync(CancellationToken.None);

        var matches = await store.SearchAsync([5f, 0f, 0f], k: 10, new HashSet<PermissionSource> { PermissionSource.Files });
        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public async Task RunAsync_ContinuesAfterASourceThrows()
    {
        await using var store = new SqliteContextStore("Data Source=:memory:");
        var service = new ContextIndexingService(
            [new ThrowingSource(), new ListSource(PermissionSource.PersonalIndex, "gamma")],
            Indexer(store),
            NullLogger<ContextIndexingService>.Instance);

        // Must not throw despite the first source failing.
        await service.RunAsync(CancellationToken.None);

        var matches = await store.SearchAsync([5f, 0f, 0f], k: 10, new HashSet<PermissionSource> { PermissionSource.PersonalIndex });
        Assert.Single(matches);
    }
}
