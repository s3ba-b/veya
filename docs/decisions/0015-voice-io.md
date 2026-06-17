# ADR-0015: Voice I/O — local Whisper STT + espeak-ng TTS via `AskVoice`

- Status: accepted
- Date: 2026-06-17

## Context

Milestone 4 (docs/roadmap.md) lists "Voice input/output" as the last remaining
item — screen awareness (ADR-0013) and the GNOME shell extension (ADR-0014)
are done. We need a way for the user to ask Veya a question by speaking, and
to hear the answer, without sending audio or transcripts off the machine
(local-first privacy is a product pillar, not a feature).

Three questions needed answers:

1. **Where does capture/transcription/synthesis run?** Frontends are pure
   D-Bus clients with no system access (docs/dbus-interfaces.md,
   "Conventions"), so — like screen capture and clipboard writes — the actual
   audio I/O belongs in the Daemon, not the GNOME shell extension or Overlay.
2. **STT engine.** Options considered: cloud transcription (e.g. via the
   configured cloud backend) — rejected as the default for the same reason
   ADR-0013 rejected cloud OCR: it would send audio off the machine on every
   use, exactly what `CloudUsage` exists to make rare and visible, not the
   default path. A subprocess CLI (`whisper.cpp` built separately) — rejected
   for this slice: not packaged in Ubuntu's apt repos, so it would be the
   first MCP-style dependency that isn't a stock system utility. **Whisper.net**
   (NuGet binding to whisper.cpp, native runtime bundled via NuGet) — runs
   in-process, no subprocess, no apt dependency; only a model file needs
   fetching once.
3. **TTS engine.** `speech-dispatcher`'s `spd-say` was the original idea, but
   it takes text only as an argv positional — every other tool that handles
   potentially sensitive content (`ClipboardTool`'s `wl-copy`/`xclip`, ADR-0006)
   deliberately pipes it via stdin so it never lands in the `tool.exec` audit
   log's recorded `argv`. `espeak-ng` reads text from stdin when no text
   argument is given, is a smaller and more commonly preinstalled package, and
   keeps that invariant intact.

## Decision

**A new D-Bus method `AskVoice(in u maxDurationMs, out s transcript, out s
reply)`** on `org.veya.Veya1`, implemented by a Daemon-side `VoiceAskService`:

1. Check `PermissionSource.Microphone` via `IPermissionGate` (default-deny,
   ADR-0005). Denied → return `("", "<explanatory message>")`, nothing else
   happens — no recording is attempted.
2. **Capture**: `IAudioRecorder` (`AlsaAudioRecorder`) runs `arecord -q -t wav
   -f S16_LE -c 1 -r 16000 -d <seconds> <tmpfile>` through `ISafeExecutor`,
   bounded by `Voice:MaxRecordingMs` (default 15000ms). `arecord` talks to
   ALSA directly, which PulseAudio and PipeWire both provide a compatibility
   shim for on stock Ubuntu — no compositor/sound-server branching needed
   (unlike `ClipboardTool`'s Wayland/X11 split). Failure/timeout → `null`,
   handled as "couldn't hear anything".
3. **Transcribe**: `ISpeechToText` (`WhisperNetTranscriber`) loads a
   `WhisperFactory` once from `Voice:WhisperModelPath` (default
   `~/.local/share/veya/models/ggml-base.bin` — the **multilingual** base
   model, not `.en`, since English-only models would silently fail
   non-English speech) and transcribes the wav with language hint `"auto"`.
   A missing model file is a normal graceful-degradation path (mirrors a
   missing `tesseract` binary in ADR-0013), not a crash.
4. The temp wav is deleted immediately after transcription, success or
   failure — never persisted, never indexed.
5. A `voice.capture` audit event records success, transcript length, and
   duration — never the audio or the transcript text.
6. The transcript is run through the existing pipeline exactly like typed
   `Ask`: `reply = await modelRouter.AskAsync(transcript)`, with the same
   `BackendUnavailableException` → friendly-string mapping `Veya1Service.AskAsync`
   already uses.
7. **Speak**: best-effort `ITextToSpeech` (`EspeakTextToSpeech`) pipes the
   reply to `espeak-ng` via stdin (`Detached: true`, like `wl-copy` — the
   call returns once playback has started, not once it finishes). A `voice.speak`
   audit event records success, reply length, and duration — never the text.
   A TTS failure is logged but never fails the call: the text reply is already
   in hand.
8. Return `(transcript, reply)` so the caller can show what was heard
   alongside the answer.

**No separate permission gates the speaking step.** Speaking the reply aloud
isn't a new data source being read — it's the assistant's own
already-computed answer to a question the user just asked by voice, the audio
equivalent of showing the reply text in the overlay (which isn't gated
either).

**Scope of this ADR is the Daemon-side capability only.** A GNOME shell
extension mic button that calls `AskVoice` is a deliberate fast-follow, the
same sequencing this repo used for screen awareness (#63/#64 backend, then
#65/#66 UI).

## Consequences

- **Two new apt dependencies**: `alsa-utils` (`arecord`) and `espeak-ng`.
  Like `tesseract-ocr` (ADR-0013), not auto-installed (hard rule 2) — added to
  `scripts/setup-dev.sh` and documented in docs/running.md. A missing binary
  surfaces as a normal failed `tool.exec`, not a crash.
- **A model file Veya doesn't ship.** `ggml-base.bin` (~140MB) must be fetched
  once via the new `scripts/download-whisper-model.sh`, not automatically —
  avoids surprise network egress on first run, consistent with local-first
  being deliberate rather than silent.
- **New `PermissionSource.Microphone`** (default-deny) and two new audit event
  types, `voice.capture`/`voice.speak`, both content-free like every other
  capture event in this system.
- **`AskVoice` is the first D-Bus method that runs `ISafeExecutor` commands
  directly from the Daemon**, not via the McpServer subprocess. The Daemon
  gets its own `ISafeExecutor` instance (own allowlist: `arecord`,
  `espeak-ng`; own timeout, sized to cover `Voice:MaxRecordingMs`) — it does
  not share McpServer's allowlist or process, since McpServer's executor is
  scoped to a 5-second default timeout that a multi-second recording would
  exceed.
- **CI stays headless** (hard rule 3): `VoiceAskService`'s orchestration logic
  (permission check, capture/transcribe/speak sequencing, audit events, error
  mapping) is unit-tested against fakes for all five dependencies.
  `AlsaAudioRecorder` (real `arecord`), `WhisperNetTranscriber` (real model
  load), and `EspeakTextToSpeech` (real `espeak-ng`) are exercised manually,
  the same status as `PortalScreenshotClient` and `SessionBusNotificationSource`.
- **Deferred**: GNOME shell extension mic button and "Listening…" UI state
  (follow-up issue); Overlay wiring (later still); push-to-talk / early-stop
  recording (v1 is a fixed bounded duration, simplest viable version, same
  reasoning as ADR-0013 picking on-demand capture over continuous indexing);
  per-language voice selection for `espeak-ng` (currently default voice
  regardless of detected speech language).
