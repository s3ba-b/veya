#!/usr/bin/env bash
# Canonical build + test + format check. CI runs exactly this script;
# if it passes locally, CI should pass.
set -euo pipefail

cd "$(dirname "$0")/.."

# Pre-scaffolding: stay green while the solution doesn't exist yet.
shopt -s nullglob
solutions=(*.sln *.slnx)
shopt -u nullglob
if [[ ${#solutions[@]} -eq 0 ]]; then
    echo "verify: no solution file found yet (pre-scaffolding phase) — nothing to build. OK."
    exit 0
fi

sln="${solutions[0]}"
echo "verify: using solution $sln"

echo "==> dotnet restore"
dotnet restore "$sln"

echo "==> license scan (dependency licenses)"
"$(dirname "$0")/license-scan.sh" "$sln"

echo "==> dotnet format (check only)"
dotnet format "$sln" --verify-no-changes --no-restore

echo "==> dotnet build"
dotnet build "$sln" --no-restore --configuration Release -warnaserror

echo "==> dotnet test"
dotnet test "$sln" --no-build --configuration Release

echo "verify: OK"
