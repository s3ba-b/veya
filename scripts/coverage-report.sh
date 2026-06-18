#!/usr/bin/env bash
# Generates a human-readable coverage report from the cobertura output that
# `dotnet test --collect:"XPlat Code Coverage"` (run via verify.sh) produces.
# Requires the local dotnet tool manifest: `dotnet tool restore` once, then
# this script. Run verify.sh first if ./coverage has no reports yet.
set -euo pipefail

cd "$(dirname "$0")/.."

if ! compgen -G "coverage/*/coverage.cobertura.xml" > /dev/null; then
    echo "coverage-report: no coverage data in ./coverage — run ./scripts/verify.sh first." >&2
    exit 1
fi

dotnet tool restore
dotnet tool run reportgenerator \
    -reports:"coverage/*/coverage.cobertura.xml" \
    -targetdir:"coverage/report" \
    -reporttypes:"TextSummary;Html"

echo
cat coverage/report/Summary.txt
echo
echo "Full HTML report: coverage/report/index.html"
