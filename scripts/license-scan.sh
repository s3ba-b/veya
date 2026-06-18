#!/usr/bin/env bash
# Dependency license scan — a precaution against pulling in third-party code
# whose license is incompatible with Veya's AGPL-3.0 distribution. We require
# dependencies to be permissive (MIT/BSD/Apache-2.0/etc.) so they impose no
# obligations of their own on top of the project's license; the scan rejects
# copyleft or unknown-licensed transitive dependencies.
#
# What it does: enumerates every restored NuGet package (direct + transitive)
# for the solution, reads each package's declared license from its .nuspec in
# the local NuGet cache, and fails if any license is not on the permissive
# allow-list below. Self-contained: no network, no external tools — it only
# reads what `dotnet restore` already placed on disk.
#
# What it does NOT do: internet-scale snippet/plagiarism detection. That needs
# an external corpus and is out of scope for a hermetic CI step. This catches
# the realistic regression — a new dependency that drags in a copyleft or
# unknown license — not verbatim copying of someone else's source.
#
# Acknowledged exceptions (packages whose license can't be auto-resolved but
# have been reviewed by a human) go in scripts/license-allowlist.txt, one
# package id per line.
set -euo pipefail

cd "$(dirname "$0")/.."

sln="${1:-Veya.sln}"
nuget_root="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
exceptions_file="scripts/license-allowlist.txt"

# SPDX expressions we accept without review. All permissive and compatible with
# the project's AGPL-3.0 license.
ALLOWED_SPDX=" MIT Apache-2.0 BSD-2-Clause BSD-3-Clause 0BSD ISC MS-PL MS-NET-Library Unlicense WTFPL Zlib "

# licenseUrl values that map to a known-good license (older packages predate
# SPDX expressions). aka.ms/deprecateLicenseUrl is a placeholder Microsoft uses
# when the real license lives in the <license> expression element.
url_to_spdx() {
    case "$1" in
        *licenses.nuget.org/*)          basename "$1" ;;
        *apache.org/licenses/LICENSE-2.0*) echo "Apache-2.0" ;;
        *opensource.org/licenses/MIT*)  echo "MIT" ;;
        *go.microsoft.com/fwlink/?LinkId=329770*) echo "MS-NET-Library" ;;
        *dotnet.microsoft.com/*license*|*microsoft.com/web/webpi/eula/*) echo "MS-NET-Library" ;;
        *) echo "" ;;
    esac
}

# Read the SPDX license for a restored package, or "" if undeterminable.
package_license() {
    local id="$1" ver="$2"
    local lid lver nuspec
    lid="$(echo "$id" | tr '[:upper:]' '[:lower:]')"
    lver="$(echo "$ver" | tr '[:upper:]' '[:lower:]')"
    nuspec="$nuget_root/$lid/$lver/$lid.nuspec"
    [[ -f "$nuspec" ]] || { echo ""; return; }

    # Prefer the SPDX expression form: <license type="expression">MIT</license>
    local expr
    expr="$(grep -oiE '<license[^>]*type="expression"[^>]*>[^<]+</license>' "$nuspec" \
            | sed -E 's/.*>([^<]+)<.*/\1/' | head -1)"
    if [[ -n "$expr" ]]; then echo "$expr"; return; fi

    # type="file" — license text is bundled; cannot evaluate automatically.
    if grep -qiE '<license[^>]*type="file"' "$nuspec"; then echo "FILE"; return; fi

    # Fall back to the legacy <licenseUrl>.
    local url
    url="$(grep -oiE '<licenseUrl>[^<]+</licenseUrl>' "$nuspec" \
           | sed -E 's/.*>([^<]+)<.*/\1/' | head -1)"
    [[ -n "$url" ]] && url_to_spdx "$url" || echo ""
}

is_allowed_spdx() { [[ "$ALLOWED_SPDX" == *" $1 "* ]]; }

is_excepted() {
    [[ -f "$exceptions_file" ]] || return 1
    grep -qxiF "$1" <(grep -vE '^\s*(#|$)' "$exceptions_file" | sed -E 's/\s+#.*//; s/\s+$//')
}

echo "license-scan: solution $sln (cache: $nuget_root)"

packages="$(dotnet list "$sln" package --include-transitive 2>/dev/null \
            | awk '/^[[:space:]]*>/ {print $2, $NF}' | sort -u)"

if [[ -z "$packages" ]]; then
    echo "license-scan: no packages resolved — did you restore? OK (nothing to scan)."
    exit 0
fi

fail=0
while read -r id ver; do
    [[ -z "$id" ]] && continue
    lic="$(package_license "$id" "$ver")"
    if [[ -n "$lic" && "$lic" != "FILE" ]] && is_allowed_spdx "$lic"; then
        continue
    fi
    if is_excepted "$id"; then
        echo "  ack   $id $ver — ${lic:-unresolved} (acknowledged in $exceptions_file)"
        continue
    fi
    fail=1
    echo "  FAIL  $id $ver — license '${lic:-unresolved}' not on allow-list"
done <<< "$packages"

if [[ "$fail" -ne 0 ]]; then
    echo
    echo "license-scan: one or more dependencies have a non-allow-listed license."
    echo "Review each, then either reconsider the dependency or, if its license is"
    echo "acceptable, add its package id to $exceptions_file with a comment."
    exit 1
fi

echo "license-scan: all dependencies carry allow-listed licenses. OK"
