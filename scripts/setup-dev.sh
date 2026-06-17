#!/usr/bin/env bash
# One-time dev environment setup. Uses sudo only for apt installs, and tells
# you so before doing it.
#
# Profiles (single source of truth for Veya's native dependencies):
#   full (default) — full Ubuntu dev box: .NET SDK, GTK4/libadwaita -dev libs,
#                    D-Bus, OCR, audio/TTS, d-feet. Needed to run Veya end to end.
#   ci             — headless build + test only: the GTK4/libadwaita *runtime*
#                    libs that Veya.Overlay's Gir.Core bindings resolve at
#                    runtime (ADR-0002); no display server. Assumes the .NET SDK
#                    is already provided (CI's setup-dotnet action, or the
#                    devcontainer base image). Distro-portable (Ubuntu/Debian).
set -euo pipefail

PROFILE="${1:-full}"
case "$PROFILE" in
    full|ci) ;;
    *) echo "usage: $(basename "$0") [full|ci]" >&2; exit 2 ;;
esac

echo "Veya dev setup (profile: $PROFILE) — this will use sudo for 'apt-get install' below."

# ---- Native runtime libraries (every profile) ----
# Gir.Core resolves these .so files at runtime during unit tests; building and
# headless testing need them but no display server is required (ADR-0002).
sudo apt-get update
sudo apt-get install -y libgtk-4-1 libadwaita-1-0

if [[ "$PROFILE" == "ci" ]]; then
    echo "ci profile: .NET SDK is expected from the environment (setup-dotnet / devcontainer image)."
    dotnet --version 2>/dev/null || echo "warning: dotnet not found on PATH"
    echo "Done. Build and test with: ./scripts/verify.sh"
    exit 0
fi

# ---- Everything below is full Ubuntu dev-box setup ----
ARCH=$(dpkg --print-architecture)
echo "Detected: $(lsb_release -ds 2>/dev/null || echo Ubuntu) ${ARCH}"

# ---- .NET SDK (>= 10.0; RollForward=Major in Directory.Build.props allows later majors) ----
NEED_DOTNET=true
if command -v dotnet &>/dev/null; then
    DOTNET_MAJOR=$(dotnet --version | cut -d. -f1)
    if [[ "$DOTNET_MAJOR" -ge 10 ]]; then
        echo ".NET SDK $(dotnet --version) already installed — skipping."
        NEED_DOTNET=false
    fi
fi

if [[ "$NEED_DOTNET" == true ]]; then
    if [[ "$ARCH" == "arm64" ]]; then
        # Ubuntu does not ship dotnet-sdk-10.0 for arm64; add Microsoft's apt feed.
        # We use the 24.04 channel — the latest known-good arm64 feed; it is
        # forward-compatible with newer Ubuntu releases.
        echo "ARM64: adding Microsoft package feed (24.04 channel)..."
        curl -fsSL https://packages.microsoft.com/keys/microsoft.asc \
            | sudo gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg
        echo "deb [arch=arm64 signed-by=/usr/share/keyrings/microsoft-prod.gpg] \
https://packages.microsoft.com/ubuntu/24.04/prod noble main" \
            | sudo tee /etc/apt/sources.list.d/microsoft-prod.list
        sudo apt-get update
    fi
    sudo apt-get install -y dotnet-sdk-10.0
fi

# ---- Dev libraries + tooling ----
sudo apt-get install -y \
    libgtk-4-dev \
    libadwaita-1-dev \
    dbus-x11 \
    tesseract-ocr \
    alsa-utils \
    espeak-ng

# ---- d-feet (optional D-Bus GUI debugger) ----
# Not available in arm64 apt repos. Install manually if needed:
#   sudo snap install d-feet
if [[ "$ARCH" == "arm64" ]]; then
    echo "d-feet: skipping on arm64 (not in apt repos)."
    echo "  To install manually: sudo snap install d-feet"
else
    sudo apt-get install -y d-feet
fi

echo
echo "Versions:"
dotnet --version

echo
echo "Done. Build and test with: ./scripts/verify.sh"
echo "Voice input (ADR-0015) needs a local Whisper model — fetch it with:"
echo "  ./scripts/download-whisper-model.sh"
