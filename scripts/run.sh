#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
dotnet run --project src/Rove.UI/Rove.UI.csproj -p:RuntimeIdentifier=linux-x64 "$@"
