# ADR-0009: Personal context index — SQLite + sqlite-vec, local-first embeddings

- Status: accepted
- Date: 2026-06-15

## Context

Milestone 3 (docs/roadmap.md) introduces a **personal context index**: a store
of user-approved content that retrieval can draw on to ground `Ask` answers.
docs/security.md states the product pillar plainly: "ingestion **and** query both
check permissions", defaults are deny, and decisions are audit-logged.
`PermissionSource.PersonalIndex` already exists (ADR-0005) but nothing reads or
writes it yet.

This is the first context source that is *persisted* and *embedded*, so it needs
three things the codebase does not have: a vector store, a way to produce
embeddings, and an ingestion path — each of which must sit behind the existing
permission gate and audit log rather than around them. Two questions are settled
up front:

1. **Where embeddings come from.** Local-first is the pillar, and ADR-0004
   already runs a local Ollama server, which exposes an embeddings endpoint
   (`/api/embeddings`). So embeddings default to local, mirroring the
   `IInferenceBackend` local-vs-cloud split rather than inventing a new policy.
2. **What stores the vectors.** SQLite is already the obvious local-first
   persistence choice; `sqlite-vec` is a small, embeddable extension that adds
   vector search to it without a separate service.

## Decision

A context layer in `Veya.Shared` (new folder `Veya.Shared.Context`), composed of
four pieces, each headless-testable (hard rule 3):

1. **`IEmbeddingBackend`** in `Veya.Shared.Inference` —
   `Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, ct)`.
   - **`OllamaEmbeddingBackend`** (default) calls Ollama `/api/embeddings`,
     writes a `local.request` audit event (it does not leave the machine), and
     throws `BackendUnavailableException` when Ollama is unreachable — same shape
     as `OllamaBackend`.
   - A cloud embedding backend is **deferred**: the interface leaves room for one
     (it would write `cloud.request` and trip `CloudUsage`, like the cloud
     inference backends), but Milestone 3 ships local-only embeddings. No
     `FallbackInferenceBackend` analogue is built until there is a cloud
     implementation to fall back to.
   - Embedding model name lives in `OllamaOptions` (e.g. `nomic-embed-text`);
     vector dimension is read from the first embedding, not hard-coded.
   - **When Ollama is unavailable, the index degrades; it does not break.** The
     backend throws `BackendUnavailableException` (as `OllamaBackend` does), and
     each of the two callers absorbs it:
     - **Ingestion** skips the chunk and leaves it to be re-indexed on a later
       run — nothing is persisted without its embedding.
     - **Query (`Ask`)** treats it as "no personal context": retrieval is skipped
       and `Ask` answers normally without the index. The personal index is an
       enhancement to an answer, never a precondition for getting one — Ollama
       being down must never stop `Ask` from responding.

2. **`IContextStore`** in `Veya.Shared.Context` — the vector store contract:
   `UpsertAsync(ContextChunk, ct)`, `SearchAsync(float[] queryEmbedding, int k, IReadOnlySet<PermissionSource> allowed, ct)`, `DeleteBySourceAsync(...)`.
   - **`SqliteContextStore`** implements it over `Microsoft.Data.Sqlite` with the
     `sqlite-vec` extension loaded. Schema: a `chunks` table (id, source, origin,
     text, ingested_at) joined to a `vec0` virtual table holding the embeddings.
   - **Permission enforced at query time inside the store**: `SearchAsync` filters
     to the `allowed` source set, so a revoked source cannot surface even if its
     rows are still present. The caller passes only sources the gate has approved
     (see below); the store's filter is defence in depth, not the primary gate.
   - DB path defaults under `$XDG_DATA_HOME/veya/context.db` (parallel to
     `AuditPaths`), injectable for tests (`:memory:` or a temp file).

3. **`IContextSource` + ingestion** — a source yields `ContextChunk`s tagged with
   their `PermissionSource`. Ingestion (`ContextIndexer`) for each chunk:
   `IPermissionGate.CheckAsync(source, "context.ingest", ct)` → if denied, skip
   (the gate has already logged `permission.decision`); if granted, embed via
   `IEmbeddingBackend` and `UpsertAsync`. Milestone 3 ships **no concrete
   sources** beyond a test/manual one — the file/notification sources come with
   their own ADRs later. This ADR lands the pipeline and contracts, not the
   sources.

4. **Retrieval wired into `Ask`** — before the model runs, the Daemon embeds the
   query and calls `SearchAsync` with the set of sources currently granted
   (each checked through `IPermissionGate` with requester `"context.query"`, so
   query-time decisions are audit-logged too). Retrieved chunks are injected as
   additional context. Nothing is retrieved from a source the user has not
   granted, and the grant is re-checked per query, not cached.

## Consequences

- **New NuGet + native asset.** `Microsoft.Data.Sqlite` plus the `sqlite-vec`
  package (`0.1.7-alpha.2.1`), which carries the `linux-x64` native `vec0.so`
  under `runtimes/`. **Resolved:** the package deploys the asset keyed by its own
  RID (`linux-x64`), not the running RID (`ubuntu.26.04-x64`), and
  `SqliteConnection.LoadExtension` appends the platform suffix itself, so
  `SqliteContextStore` resolves an explicit `runtimes/<os>-<arch>/native/vec0`
  path (no suffix) after the bare-name attempt. `System.Memory` arrives
  transitively with an unresolved (legacy `licenseUrl`) license and is
  acknowledged in `scripts/license-allowlist.txt`. `SqliteContextStoreTests` run
  against a real in-memory SQLite + sqlite-vec — the extension is exercised in
  CI, not mocked. The package is pre-1.0 (alpha); revisit the pin when a stable
  release lands.
- **No raw content in the audit log.** Consistent with `cloud.request`/
  `local.request` carrying no prompt text: ingestion and query audit events
  record source, requester, chunk counts, and timing — never the indexed text or
  the query. The new embedding calls reuse `local.request`.
- **Permission checked twice on purpose** — at ingestion (don't index what the
  user didn't approve) and at query (don't surface what was approved then
  revoked), with the store's own source filter as a third backstop. This is the
  pillar's "ingestion and query both check permissions" made concrete.
- **Embeddings are local-only for now**, so the personal index adds **no** new
  cloud egress and does not trip `CloudUsage`. If a cloud embedding backend is
  added later it must be config-selectable like the cloud inference backend
  (ADR-0008) and audit-logged as `cloud.request`.
- **`ContextChunk` / `ContextDocument`** models live in `Veya.Shared.Context`;
  `Veya.Shared` keeps its no-configuration-dependency stance (DB path and model
  name are passed in by the host, as `OllamaOptions` already is).
- docs/architecture.md gains a "Personal context index" subsection and a retrieval
  step in the data-flow narrative; docs/security.md's per-source-permissions
  section is updated to note the index is now a live source. This ADR does not
  supersede ADR-0004 or ADR-0005; it builds on both.
- **Deferred:** concrete context sources (files, notifications, screen) and their
  permission UX; a cloud embedding backend; chunking/summarisation strategy
  tuning; re-embedding on model change. Each gets its own issue.
