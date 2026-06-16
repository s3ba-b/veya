#!/usr/bin/env bash
# Install the Veya GNOME Shell extension for local development.
# Requires GNOME Shell 45+ (Ubuntu 24.04 or later).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
EXT_SRC="$SCRIPT_DIR/../src/gnome-shell-extension"
EXT_UUID="veya@veya-project.org"
EXT_DIR="$HOME/.local/share/gnome-shell/extensions/$EXT_UUID"

echo "Installing Veya extension to $EXT_DIR"
rm -rf "$EXT_DIR"
mkdir -p "$EXT_DIR"

cp "$EXT_SRC/metadata.json" "$EXT_DIR/"
cp "$EXT_SRC/extension.js"  "$EXT_DIR/"
cp "$EXT_SRC/stylesheet.css" "$EXT_DIR/"

# Compile and install the GSettings schema
mkdir -p "$EXT_DIR/schemas"
cp "$EXT_SRC/schemas/org.gnome.shell.extensions.veya.gschema.xml" "$EXT_DIR/schemas/"
glib-compile-schemas "$EXT_DIR/schemas/"

echo "Done. Enable with:"
echo "  gnome-extensions enable $EXT_UUID"
echo "Then reload GNOME Shell (Alt+F2 → 'r' on X11, or log out/in on Wayland)."
echo ""
echo "To change the summon shortcut (default: Super+Shift+V):"
echo "  gsettings set org.gnome.shell.extensions.veya summon-shortcut \"['<Super><Shift>v']\""
