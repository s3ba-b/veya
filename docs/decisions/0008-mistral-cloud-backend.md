# ADR-0008: Second cloud backend — Mistral (configurable cloud provider)

- Status: accepted
- Date: 2026-06-15

## Context

Milestone 1 (ADR-0001) chose the Claude API as the first cloud backend, and
ADR-0004 added `OllamaBackend` for local inference. The two are composed as
`FallbackInferenceBackend(local: Ollama, cloud: Claude)`.

The Claude API is metered and paid per token. During development — and for
users who have free or cheaper access to another provider — it is useful to
point the cloud tier at a different hosted model without touching the local
backend or the router. **Mistral's hosted API ("La Plateforme",
`api.mistral.ai`) has a free tier** and an OpenAI-compatible
`/v1/chat/completions` endpoint with tool calling, so it is a low-friction
second cloud backend.

This is the first time Veya has more than one cloud backend, so it also needs a
way to choose between them.

## Decision

1. **`MistralBackend : IInferenceBackend`** in `Veya.Shared.Inference`, a thin
   HTTP client for `POST {BaseUrl}/v1/chat/completions` (non-streaming),
   mirroring `OllamaBackend`'s shape (request/response mapping, audit logging,
   injectable `HttpClient` for tests).
2. **The cloud backend is config-selectable.** `InferenceOptions.CloudBackend`
   (bound from the `Inference` section, default `"mistral"`) selects `"mistral"`
   or `"claude"`; the chosen backend becomes the cloud tier of the existing
   `FallbackInferenceBackend(local: Ollama, cloud: <selected>)`. The local
   backend and `ModelRouter` are unchanged.

Reasons:

- Mistral's API is OpenAI-shaped, so the mapping is the same kind of thin HTTP
  translation `OllamaBackend` already does — no new NuGet dependency, no native
  assets.
- Selecting via config (not code) keeps Claude wired and available; switching is
  an environment-variable change (`Inference__CloudBackend=claude`), not a
  rebuild.

## Consequences

- **Mistral is a cloud backend: data leaves the machine.** `MistralBackend`
  therefore writes a `cloud.request` audit event (`backend="mistral"`), exactly
  like `ClaudeBackend` — the privacy pillar treats it identically to Claude, and
  the same user-visible cloud-usage surface applies. It is **not** a
  `local.request` (contrast `OllamaBackend`, ADR-0004).
- **API key:** resolved by `ConfigurationApiKeyProvider`, which reads
  `IConfiguration` first (key `Mistral:ApiKey`, or `Anthropic:ApiKey` for Claude)
  and falls back to `EnvironmentApiKeyProvider` (env var `MISTRAL_API_KEY` /
  `ANTHROPIC_API_KEY`). In **development** this lets the key come from
  `dotnet user-secrets` (the Daemon project has a `UserSecretsId`; secrets live
  in `~/.microsoft/usersecrets/<id>/secrets.json`, outside the repo, loaded only
  when `DOTNET_ENVIRONMENT=Development`). The installed systemd service runs in
  Production, where user-secrets are not loaded, so it uses the env var (and,
  later, libsecret/keyring — docs/security.md). Nothing is ever written to repo
  config. A missing key surfaces as `BackendUnavailableException`.
- **Mapping details:** Mistral expects tool-call `arguments` as a JSON **string**
  (not an object as Claude does), and it returns its own tool-call `id`s, so —
  unlike `OllamaBackend` — ids are preserved on round-trip rather than
  synthesized. `finish_reason: "tool_calls"` maps to Veya's `"tool_use"` stop
  reason. `tool` role messages carry `tool_call_id`.
- **`MistralOptions`** (base URL, model name) configures the backend; defaults
  are `https://api.mistral.ai` and `mistral-large-latest`.
- Tests fake the HTTP transport (`HttpMessageHandler`), matching
  `OllamaBackendTests`/`ClaudeBackendTests` — no real Mistral account in CI.
- Out of scope here (unchanged from ADR-0004): a richer `ModelRouter` policy and
  the `CloudUsage`/`ActiveBackend` D-Bus surface. This ADR does not supersede
  ADR-0001 or ADR-0004; it adds a sibling cloud backend and the selection knob.
