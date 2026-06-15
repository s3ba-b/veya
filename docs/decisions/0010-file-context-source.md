# ADR-0010: File context source — index user-approved text files

- Status: accepted
- Date: 2026-06-15

## Context

ADR-0009 landed the personal context index (embeddings + `sqlite-vec` store +
ingest/query pipeline) but shipped no concrete `IContextSource`. The index is
inert until something feeds it. Files are the obvious first source: a user points
Veya at a folder of notes/docs and asks questions grounded in them.

`PermissionSource.Files` already exists (ADR-0005). What this ADR decides is the
shape of the source, how files become chunks, when ingestion runs, and how
re-indexing avoids leaving stale rows behind.

## Decision

A `FileContextSource` in `Veya.Shared.Context`, plus a startup ingestion trigger
in the Daemon.

1. **`FileContextSource : IContextSource`**, `Source => PermissionSource.Files`.
   Walks the configured root directories recursively, and for each file that
   passes the filter, reads its text, chunks it, and yields one `ContextChunk`
   per chunk:
   - `Id` = `"{path}#{ordinal}"` — stable across runs for the same file/position,
     so re-ingesting upserts in place.
   - `Origin` = the absolute file path (tracing only, not security).
   - `Text` = the chunk text.

2. **Filtering — `FileContextOptions`** (bound from the `Context:Files` config
   section): `Roots` (directories to index), `Extensions` (default `.txt`,
   `.md`), `MaxFileBytes` (default 1 MiB; larger files skipped). Plain text only;
   binary/PDF/office formats are out of scope (own ADR later). Unreadable files
   (permissions, IO) are skipped, not fatal — one bad file must not abort a run.

3. **Chunking — deterministic, paragraph-aware.** Split on blank lines into
   paragraphs, then pack consecutive paragraphs into chunks up to a character cap
   (`MaxChunkChars`, default 1000). A single paragraph longer than the cap is
   hard-split. Deterministic so re-runs produce identical ids/positions.

4. **Re-index replaces the source — `ContextIndexer.IngestAsync(source,
   replaceExisting)`.** When `replaceExisting` is true and permission is granted,
   the indexer calls `IContextStore.DeleteBySourceAsync(source.Source)` before
   ingesting, so a file that shrank, was edited, or was deleted leaves no stale
   chunks. The delete happens only after the permission check passes (a denied
   run touches nothing). The existing parameterless overload keeps the additive
   behaviour for callers that want it.

5. **Ingestion trigger — `ContextIndexingService : IHostedService`** in the
   Daemon. On startup it runs `ContextIndexer.IngestAsync(source,
   replaceExisting: true)` over the registered `IContextSource`s, off the
   critical path (failures are logged, never crash the daemon). A richer trigger
   (file-watching, incremental, on-demand) is deferred.

6. **Retrieval includes files.** The Daemon's `ContextRetriever` candidate
   sources become `[PersonalIndex, Files]`, so file chunks are searchable —
   still gated per source at query time (a denied `Files` permission means file
   chunks never surface even though they are indexed).

## Consequences

- **Permission at ingest and query, unchanged from ADR-0009.** `FileContextSource`
  reads nothing on its own; `ContextIndexer` gates `Files` before reading, and
  `ContextRetriever` re-checks `Files` per query. Both still audit-log via the
  gate and the `context.ingest`/`context.query` events, which carry counts and
  timing but never file contents or paths beyond the source name.
- **Default-deny still holds.** With no `Context:Files:Roots` configured, or with
  `Permissions:Files` unset/false, nothing is indexed and nothing is retrieved.
- **Full re-index on every startup** is acceptable at current scale (a user's
  notes folder), keeps the store consistent with disk, and sidesteps
  change-tracking. If it becomes slow, incremental indexing is a follow-up — the
  `replaceExisting` seam does not preclude it.
- **`DeleteBySourceAsync` is now load-bearing for correctness**, not just for
  user-driven revocation; it already exists and is covered by store tests.
- Tests use temp directories and the in-memory store (hard rule 3): chunking
  determinism, filtering, re-index replacement, and the hosted service's
  permission-gated startup run. No desktop session involved.
- Out of scope (own ADRs/issues): non-text formats, file-watching/incremental
  updates, per-app scoping, and an on-demand "re-index now" D-Bus method.
