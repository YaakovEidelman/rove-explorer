#!/usr/bin/env bash
# Print whether the live Rove instance is running, and its window info.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
source scripts/live/_common.sh

if [ -f "$STATE_FILE" ] && source "$STATE_FILE" && kill -0 "$PID" 2>/dev/null; then
    echo "Running: pid=$PID addr=$ADDR geom=$GEOM"
else
    echo "Not running."
fi
