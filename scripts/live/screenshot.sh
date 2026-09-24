#!/usr/bin/env bash
# Screenshot the live Rove window. Usage: scripts/live/screenshot.sh [name]
# Prints the saved path (under /tmp/rove-live/) on stdout.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
source scripts/live/_common.sh
require_running

NAME="${1:-shot}"
OUT="$STATE_DIR/${NAME}.png"
grim -g "$GEOM" "$OUT"
echo "$OUT"
