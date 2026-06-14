# ADR-0004: Local inference backend — Ollama

- Status: accepted
- Date: 2026-06-12

## Context

Milestone 2 (docs/roadmap.md) calls for a `LocalBackend` behind
`IInferenceBackend`, alongside `ClaudeBackend` (ADR-0001's stack, implemented in
Milestone 1). The roadmap names two candidates: **Ollama** and **LLamaSharp**.

- **Ollama** is a separate service the user installs (apt package or install
  script), running on `localhost:11434` with a JSON/HTTP API
  (`/api/chat`) that includes OpenAI-style tool calling. It manages model
  downloads and runtime itself.
- **LLamaSharp** is a .NET binding to llama.cpp, shipped as NuGet packages with
  large platform- and accelerator-specific native runtime assets (CPU/CUDA/
  ROCm/Vulkan). Veya would load and run model weights in-process.

## Decision

**`OllamaBackend : IInferenceBackend`**, talking to a locally-running Ollama
over HTTP (`POST /api/chat`, non-streaming), in `Veya.Shared.Inference`.

Reasons:

- It is a thin HTTP client, matching the shape `ClaudeBackend` already
  established (request/response mapping, audit logging, `HttpClient`
  injectable for tests) — no native bindings, no bundled runtime assets, no new
  build-time complexity.
- Ollama already handles model management (pulling, quantization, GPU/CPU
  placement); Veya does not need to reimplement any of that.
- LLamaSharp's per-accelerator native packages would significantly complicate
  packaging and CI (hard rule: tests must not assume a desktop session, and
  should stay lightweight/headless). Bundling a model runtime in-process is a
  bigger commitment than an MVP local backend needs.
- Cost accepted: the user must separately install and run Ollama. Veya detects
  it being unreachable the same way it detects a missing Claude API key — via
  `BackendUnavailableException`, surfaced as a friendly error.

LLamaSharp remains an option for a later "fully self-contained, no external
service" mode; this decision does not preclude it, just defers it.

## Consequences

- `OllamaBackend` maps `InferenceRequest`/`InferenceResponse` to Ollama's
  `/api/chat` JSON shape. Ollama's `tool_calls` entries carry no id, so
  `OllamaBackend` synthesizes a `ToolUseBlock.Id` per call and recovers the
  tool name for outgoing `tool` role messages from the conversation history.
- `OllamaOptions` (base URL, model name) configures the backend; defaults match
  a stock local Ollama install (`http://localhost:11434`).
- Local requests are audit-logged as `local.request` (mirroring
  `cloud.request`'s backend/model/token/duration fields) for observability —
  but, per docs/security.md, this is **not** cloud usage: nothing leaves the
  machine, so no `CloudUsage` signal fires for Ollama calls.
- Tests fake the HTTP transport (`HttpMessageHandler`), matching
  `ClaudeBackendTests` — no real Ollama install required in CI.
- Out of scope here: the `ModelRouter` policy that picks `ClaudeBackend` vs
  `OllamaBackend` per request, and the `CloudUsage`/`ActiveBackend` D-Bus
  surface (docs/dbus-interfaces.md) — tracked as a follow-up issue.
