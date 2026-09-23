STATE_DIR="/tmp/rove-live"
STATE_FILE="$STATE_DIR/state.env"

require_running() {
    if [ ! -f "$STATE_FILE" ]; then
        echo "Rove isn't running. Run scripts/live/start.sh first." >&2
        exit 1
    fi
    source "$STATE_FILE"
    if ! kill -0 "$PID" 2>/dev/null; then
        echo "Tracked pid $PID is dead. Run scripts/live/start.sh again." >&2
        rm -f "$STATE_FILE"
        exit 1
    fi
}
