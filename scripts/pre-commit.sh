#!/usr/bin/env bash
# Run before every commit: build, then the full test suite. Stops at the
# first failure so you're not staring at a wall of cascading errors.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

scripts/build.sh
scripts/test.sh
