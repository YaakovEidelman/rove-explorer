#!/usr/bin/env bash
# Launch Rove for manual/visual testing on the real Hyprland session (needs
# DISPLAY/WAYLAND_DISPLAY and hyprctl). Tracks pid/window address/geometry in
# /tmp/rove-live/state.env for the other scripts/live/*.sh scripts to use.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
source scripts/live/_common.sh

mkdir -p "$STATE_DIR"

if [ -f "$STATE_FILE" ] && source "$STATE_FILE" && kill -0 "$PID" 2>/dev/null; then
    echo "Already running: pid=$PID addr=$ADDR geom=$GEOM"
    exit 0
fi
rm -f "$STATE_FILE"

BIN="src/Rove.UI/bin/Debug/net10.0/linux-x64/Rove"
if [ ! -x "$BIN" ]; then
    echo "Building..."
    dotnet build src/Rove.UI/Rove.UI.csproj -p:RuntimeIdentifier=linux-x64 -v quiet
fi

ROVE_NO_INSTALL=1 "$BIN" >"$STATE_DIR/app.log" 2>&1 &
PID=$!
disown

ADDR=""
for _ in $(seq 1 100); do
    ADDR=$(hyprctl clients -j | python3 -c "
import json, sys
for c in json.load(sys.stdin):
    if c['pid'] == $PID and c['mapped']:
        print(c['address'])
        break
")
    [ -n "$ADDR" ] && break
    sleep 0.1
done

if [ -z "$ADDR" ]; then
    echo "Window never appeared. Check $STATE_DIR/app.log" >&2
    exit 1
fi

GEOM=$(hyprctl clients -j | python3 -c "
import json, sys
for c in json.load(sys.stdin):
    if c['address'] == '$ADDR':
        x, y = c['at']
        w, h = c['size']
        print(f'{x},{y} {w}x{h}')
        break
")

cat >"$STATE_FILE" <<EOF
PID=$PID
ADDR=$ADDR
GEOM="$GEOM"
EOF

echo "Started: pid=$PID addr=$ADDR geom=$GEOM"
