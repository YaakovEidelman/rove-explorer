#!/usr/bin/env bash
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

args=(test Rove.slnx -p:RuntimeIdentifier=linux-x64 --nologo -v q --logger "console;verbosity=minimal")
if [[ $# -gt 0 ]]; then
  filter=""
  for name in "$@"; do
    filter+="${filter:+|}FullyQualifiedName~$name"
  done
  args+=(--filter "$filter")
fi

log="$(mktemp)"
trap 'rm -f "$log"' EXIT

dotnet "${args[@]}" >"$log" 2>&1
status=$?

grep -E "^\s*(Passed|Failed)!" "$log" || echo "No tests ran."
if [[ $status -ne 0 ]]; then
  grep -E "error [A-Z]+[0-9]+|^\s+Failed |Error Message|Assert\.|Expected:|Actual:|^\s+at Rove" "$log" \
    | awk '!seen[$0]++' | head -60
fi
exit $status
