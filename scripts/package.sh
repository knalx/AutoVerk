#!/usr/bin/env bash
# Builds the Thunderstore package from committed source: dist/AutoVerk-<version>.zip.
#
#   scripts/package.sh            # the current commit
#   scripts/package.sh v0.1.0     # a tag, branch or commit
#
# The commit is copied to a temporary folder and built there, so uncommitted changes never get into a
# release. Set VALHEIM_DIR if the game isn't in the default Steam folder (see src/AutoVerk/AutoVerk.csproj).
set -euo pipefail

ref="${1:-HEAD}"
root="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
commit="$(git -C "$root" rev-parse --short "$ref^{commit}")"

if [[ -n "$(git -C "$root" status --porcelain)" ]]; then
  echo "Note: uncommitted changes are not in the package:"
  git -C "$root" status --short | sed 's/^/  /'
  echo
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "Building $ref ($commit) in $work"
git -C "$root" archive "$commit" | tar -x -C "$work"

dotnet test "$work/tests/AutoVerk.Tests" --nologo -v quiet

valheim=()
[[ -n "${VALHEIM_DIR:-}" ]] && valheim=("-p:ValheimDir=$VALHEIM_DIR")
dotnet build "$work/src/AutoVerk" -c Release -t:Package --nologo -v quiet \
  "-p:PackageOutputDir=$root/dist/" ${valheim[@]+"${valheim[@]}"}

version="$(sed -n 's/.*"version_number": *"\([^"]*\)".*/\1/p' "$work/thunderstore/manifest.json")"
zip="$root/dist/AutoVerk-$version.zip"
echo
unzip -l "$zip"
echo "Packaged $commit as $zip"
