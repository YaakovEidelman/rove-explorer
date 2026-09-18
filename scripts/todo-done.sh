#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

if [[ $# -ne 1 ]]; then
  echo "Usage: scripts/todo-done.sh <text the item starts with>" >&2
  exit 1
fi

python3 - "$1" <<'PY'
import re
import subprocess
import sys

start = sys.argv[1]


def finish(text):
    lines = text.split("\n")
    hits = [i for i, l in enumerate(lines) if re.match(r"\d+\. ", l) and re.sub(r"^\d+\. ", "", l).startswith(start)]
    if len(hits) != 1:
        sys.exit(f"expected one item starting with {start!r}, found {len(hits)}")
    at = hits[0]
    del lines[at]
    for i in range(at, len(lines)):
        if lines[i].startswith("#"):
            break
        m = re.match(r"(\d+)\. (.*)", lines[i])
        if m:
            lines[i] = f"{int(m.group(1)) - 1}. {m.group(2)}"
    return "\n".join(lines)


working = finish(open("TODO.md").read())
open("TODO.md", "w").write(working)

head = subprocess.run(["git", "show", "HEAD:TODO.md"], capture_output=True, text=True, check=True).stdout
blob = subprocess.run(
    ["git", "hash-object", "-w", "--stdin"], input=finish(head), capture_output=True, text=True, check=True
).stdout.strip()
subprocess.run(["git", "update-index", "--cacheinfo", f"100644,{blob},TODO.md"], check=True)
print("Removed from TODO.md and staged only that removal.")
PY
