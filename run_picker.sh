#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

out_file="$(mktemp)"
trap 'rm -f "$out_file"' EXIT

dotnet run --project src/Rove.UI/Rove.UI.csproj -p:RuntimeIdentifier=linux-x64 -- \
    --picker --start-dir "${HOME}" --out "$out_file" "$@"

if [[ -s "$out_file" ]]; then
    echo "Selected:"
    cat "$out_file"
else
    echo "No selection (cancelled)."
fi
