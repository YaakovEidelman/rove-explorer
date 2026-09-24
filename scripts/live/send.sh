#!/usr/bin/env bash
# Focus the live Rove window and forward all args to wtype.
# Examples:
#   scripts/live/send.sh "hello world"      # types text
#   scripts/live/send.sh -k space           # taps Space
#   scripts/live/send.sh -k Return
#   scripts/live/send.sh -k BackSpace
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
source scripts/live/_common.sh
require_running

ACTIVE=$(hyprctl activewindow -j | python3 -c "import json,sys; print(json.load(sys.stdin).get('address',''))")
if [ "$ACTIVE" != "$ADDR" ]; then
    echo "Warning: Rove window ($ADDR) isn't focused (active: $ACTIVE)." \
        "Click it once, then re-run." >&2
fi
wtype "$@"
sleep 0.15
