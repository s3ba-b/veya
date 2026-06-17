#!/usr/bin/env bash
# Fetches the local Whisper speech-to-text model used by voice input
# (ADR-0015). Not run automatically by setup-dev.sh or at daemon startup —
# this is a deliberate, explicit network fetch, not a silent one.
#
# Default model: ggml-base.bin, the multilingual "base" model (~140MB) —
# not "base.en", since an English-only model would silently fail non-English
# speech. Override with: ./scripts/download-whisper-model.sh <model-name>
# (e.g. "small" for better accuracy at ~460MB, "tiny" for a faster ~75MB model).
set -euo pipefail

MODEL_NAME="${1:-base}"
MODEL_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/veya/models"
MODEL_PATH="$MODEL_DIR/ggml-${MODEL_NAME}.bin"
MODEL_URL="https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-${MODEL_NAME}.bin"

if [[ -f "$MODEL_PATH" ]]; then
    echo "Already present: $MODEL_PATH"
    exit 0
fi

mkdir -p "$MODEL_DIR"
echo "Fetching $MODEL_URL"
echo "  -> $MODEL_PATH"
curl -fL --progress-bar "$MODEL_URL" -o "$MODEL_PATH.partial"
mv "$MODEL_PATH.partial" "$MODEL_PATH"

echo "Done. Set Voice:WhisperModelPath if you used a model name other than 'base'."
