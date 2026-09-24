#!/usr/bin/env bash
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

log="$(mktemp)"
trap 'rm -f "$log"' EXIT

status=0
for project in src/Rove.UI/Rove.UI.csproj src/Rove.Portal/Rove.Portal.csproj; do
  dotnet build "$project" -p:RuntimeIdentifier=linux-x64 --nologo -v q >>"$log" 2>&1 || status=1
done

grep -E "(error|warning) [A-Z]+[0-9]+" "$log" | sed 's|/[^ ]*/rove-explorer/||g' | awk '!seen[$0]++' | head -40
if [[ $status -eq 0 ]]; then
  echo "Build succeeded."
fi
exit $status
