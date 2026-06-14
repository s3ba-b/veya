#!/usr/bin/env bash
# One-time Ubuntu dev environment setup. Uses sudo only for apt installs,
# and tells you so before doing it.
set -euo pipefail

echo "Veya dev setup — this will use sudo for 'apt-get install' below."

APT_PACKAGES=(
    # .NET 9 SDK (Ubuntu 24.04+ feed; on older releases install via
    # https://learn.microsoft.com/dotnet/core/install/linux-ubuntu)
    dotnet-sdk-9.0
    # GTK4/libadwaita headers for the Overlay (later phase, harmless now)
    libgtk-4-dev
    libadwaita-1-dev
    # D-Bus tooling for poking at org.veya.Veya1 during development
    d-feet
    dbus-x11
)

sudo apt-get update
sudo apt-get install -y "${APT_PACKAGES[@]}"

echo
echo "Versions:"
dotnet --version

echo
echo "Done. Build and test with: ./scripts/verify.sh"
