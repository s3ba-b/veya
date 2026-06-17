using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Veya.Shared.Permissions;

namespace Veya.Shared.Context;

/// <summary>
/// <see cref="IContextStore"/> over SQLite with the <c>sqlite-vec</c> extension
/// (ADR-0009). Chunk metadata lives in a plain table; embeddings live in a
/// <c>vec0</c> virtual table whose <c>source</c> metadata column lets the KNN
/// search itself filter to permitted sources, so a revoked source never
/// surfaces (defence in depth behind the query-time permission gate).
/// </summary>
/// <remarks>
/// One connection is held open for the store's lifetime (a single-user daemon),
/// guarded by a semaphore since <see cref="SqliteConnection"/> is not safe for
/// concurrent use. The <c>vec0</c> table is created lazily on the first upsert,
/// when the embedding dimension becomes known (ADR-0009: dimension is read from
/// the embedding, not hard-coded).
/// </remarks>
public sealed class SqliteContextStore : IContextStore, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int? _dimension;
    private bool _initialized;

    /// <param name="connectionString">
    /// A <c>Microsoft.Data.Sqlite</c> connection string, e.g.
    /// <c>"Data Source=/path/to/context.db"</c> or <c>"Data Source=:memory:"</c>
    /// (tests). The connection is opened immediately and the <c>vec0</c>
    /// extension loaded.
    /// </param>
    public SqliteContextStore(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        SqliteVecExtension.Load(_connection);
    }

    public async Task UpsertAsync(ContextChunk chunk, float[] embedding, CancellationToken cancellationToken = default)
    {
        if (embedding.Length == 0)
        {
            throw new ArgumentException("Embedding must not be empty.", nameof(embedding));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureInitialized(embedding.Length);
            if (embedding.Length != _dimension)
            {
                throw new ArgumentException(
                    $"Embedding dimension {embedding.Length} does not match the index dimension {_dimension}.", nameof(embedding));
            }

            var source = chunk.Source.ToString();

            // Reuse the rowid for an existing (source, id) so re-ingest replaces.
            long? existingRowId = null;
            await using (var find = _connection.CreateCommand())
            {
                find.CommandText = "SELECT rowid FROM chunks WHERE source = $source AND id = $id";
                find.Parameters.AddWithValue("$source", source);
                find.Parameters.AddWithValue("$id", chunk.Id);
                var result = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (result is long row)
                {
                    existingRowId = row;
                }
            }

            await using var transaction = (SqliteTransaction)await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            long rowId;
            if (existingRowId is long existing)
            {
                rowId = existing;
                await using var update = _connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = "UPDATE chunks SET origin = $origin, text = $text, ingested_at = $ingestedAt WHERE rowid = $rowid";
                update.Parameters.AddWithValue("$origin", chunk.Origin);
                update.Parameters.AddWithValue("$text", chunk.Text);
                update.Parameters.AddWithValue("$ingestedAt", DateTimeOffset.UtcNow.ToString("O"));
                update.Parameters.AddWithValue("$rowid", rowId);
                await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                await using var deleteVec = _connection.CreateCommand();
                deleteVec.Transaction = transaction;
                deleteVec.CommandText = "DELETE FROM vec_chunks WHERE rowid = $rowid";
                deleteVec.Parameters.AddWithValue("$rowid", rowId);
                await deleteVec.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await using var insert = _connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText =
                    "INSERT INTO chunks (source, id, origin, text, ingested_at) VALUES ($source, $id, $origin, $text, $ingestedAt); SELECT last_insert_rowid();";
                insert.Parameters.AddWithValue("$source", source);
                insert.Parameters.AddWithValue("$id", chunk.Id);
                insert.Parameters.AddWithValue("$origin", chunk.Origin);
                insert.Parameters.AddWithValue("$text", chunk.Text);
                insert.Parameters.AddWithValue("$ingestedAt", DateTimeOffset.UtcNow.ToString("O"));
                rowId = (long)(await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            }

            await using (var insertVec = _connection.CreateCommand())
            {
                insertVec.Transaction = transaction;
                insertVec.CommandText = "INSERT INTO vec_chunks (rowid, source, embedding) VALUES ($rowid, $source, $embedding)";
                insertVec.Parameters.AddWithValue("$rowid", rowId);
                insertVec.Parameters.AddWithValue("$source", source);
                insertVec.Parameters.AddWithValue("$embedding", ToBlob(embedding));
                await insertVec.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ContextMatch>> SearchAsync(
        float[] queryEmbedding,
        int k,
        IReadOnlySet<PermissionSource> allowedSources,
        CancellationToken cancellationToken = default)
    {
        if (k <= 0 || allowedSources.Count == 0)
        {
            return [];
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // No upsert yet means no vec table and nothing to match.
            if (!_initialized || _dimension is null)
            {
                return [];
            }

            if (queryEmbedding.Length != _dimension)
            {
                throw new ArgumentException(
                    $"Query embedding dimension {queryEmbedding.Length} does not match the index dimension {_dimension}.", nameof(queryEmbedding));
            }

            var blob = ToBlob(queryEmbedding);
            var matches = new List<ContextMatch>();

            // One KNN per allowed source: vec0 metadata equality filters inside
            // the search, so each source's recall is correct; merge and take the
            // globally nearest k.
            foreach (var source in allowedSources)
            {
                await using var command = _connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT c.source, c.id, c.origin, c.text, v.distance
                    FROM vec_chunks v
                    JOIN chunks c ON c.rowid = v.rowid
                    WHERE v.embedding MATCH $query AND k = $k AND v.source = $source
                    ORDER BY v.distance
                    """;
                command.Parameters.AddWithValue("$query", blob);
                command.Parameters.AddWithValue("$k", k);
                command.Parameters.AddWithValue("$source", source.ToString());

                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var chunk = new ContextChunk(
                        Id: reader.GetString(1),
                        Source: Enum.Parse<PermissionSource>(reader.GetString(0)),
                        Origin: reader.GetString(2),
                        Text: reader.GetString(3));
                    matches.Add(new ContextMatch(chunk, reader.GetDouble(4)));
                }
            }

            return matches.OrderBy(match => match.Distance).Take(k).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteBySourceAsync(PermissionSource source, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                return;
            }

            var sourceName = source.ToString();
            await using var transaction = (SqliteTransaction)await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            await using (var deleteVec = _connection.CreateCommand())
            {
                deleteVec.Transaction = transaction;
                deleteVec.CommandText = "DELETE FROM vec_chunks WHERE source = $source";
                deleteVec.Parameters.AddWithValue("$source", sourceName);
                await deleteVec.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var deleteChunks = _connection.CreateCommand())
            {
                deleteChunks.Transaction = transaction;
                deleteChunks.CommandText = "DELETE FROM chunks WHERE source = $source";
                deleteChunks.Parameters.AddWithValue("$source", sourceName);
                await deleteChunks.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private void EnsureInitialized(int dimension)
    {
        if (_initialized)
        {
            return;
        }

        using (var chunks = _connection.CreateCommand())
        {
            chunks.CommandText =
                """
                CREATE TABLE IF NOT EXISTS chunks (
                    rowid INTEGER PRIMARY KEY,
                    source TEXT NOT NULL,
                    id TEXT NOT NULL,
                    origin TEXT NOT NULL,
                    text TEXT NOT NULL,
                    ingested_at TEXT NOT NULL,
                    UNIQUE(source, id)
                )
                """;
            chunks.ExecuteNonQuery();
        }

        using (var vec = _connection.CreateCommand())
        {
            vec.CommandText = $"CREATE VIRTUAL TABLE IF NOT EXISTS vec_chunks USING vec0(source TEXT, embedding float[{dimension}])";
            vec.ExecuteNonQuery();
        }

        _dimension = dimension;
        _initialized = true;
    }

    private static byte[] ToBlob(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        MemoryMarshal.AsBytes(vector.AsSpan()).CopyTo(bytes);
        return bytes;
    }
}
