#!/usr/bin/env bash
# Stop the Rove instance started by scripts/live/start.sh.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
source scripts/live/_common.sh

if [ ! -f "$STATE_FILE" ]; then
    echo "Not running."
    exit 0
fi
source "$STATE_FILE"
kill "$PID" 2>/dev/null || true
rm -f "$STATE_FILE"
echo "Stopped pid=$PID"
