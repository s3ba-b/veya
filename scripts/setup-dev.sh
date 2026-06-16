#!/usr/bin/env bash
# One-time Ubuntu dev environment setup. Uses sudo only for apt installs,
# and tells you so before doing it.
set -euo pipefail

ARCH=$(dpkg --print-architecture)
UBUNTU_VERSION=$(lsb_release -rs)

echo "Veya dev setup — this will use sudo for 'apt-get install' below."
echo "Detected: Ubuntu ${UBUNTU_VERSION} ${ARCH}"

# ---- .NET SDK (>= 9.0; RollForward=Major in Directory.Build.props allows .NET 10+) ----
NEED_DOTNET=true
if command -v dotnet &>/dev/null; then
    DOTNET_MAJOR=$(dotnet --version | cut -d. -f1)
    if [[ "$DOTNET_MAJOR" -ge 9 ]]; then
        echo ".NET SDK $(dotnet --version) already installed — skipping."
        NEED_DOTNET=false
    fi
fi

if [[ "$NEED_DOTNET" == true ]]; then
    if [[ "$ARCH" == "arm64" ]]; then
        # Ubuntu does not ship dotnet-sdk-9.0 for arm64; add Microsoft's apt feed.
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
    sudo apt-get install -y dotnet-sdk-9.0
fi

# ---- Other apt packages ----
sudo apt-get update
sudo apt-get install -y \
    libgtk-4-dev \
    libadwaita-1-dev \
    dbus-x11 \
    tesseract-ocr

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
