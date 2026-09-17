#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

if [[ $# -ne 1 ]]; then
  echo "Usage: ./release.sh <version>   e.g. ./release.sh 1.0.0" >&2
  exit 1
fi

version="$1"
tag="v$version"

if [[ "$(git branch --show-current)" != "main" ]]; then
  echo "Not on main — switch to main before releasing." >&2
  exit 1
fi

if [[ -n "$(git status --porcelain)" ]]; then
  echo "Working tree isn't clean — commit or stash first." >&2
  exit 1
fi

git fetch origin main --quiet
if [[ "$(git rev-parse HEAD)" != "$(git rev-parse origin/main)" ]]; then
  echo "Local main isn't up to date with origin/main." >&2
  exit 1
fi

if git rev-parse "$tag" >/dev/null 2>&1; then
  echo "Tag $tag already exists." >&2
  exit 1
fi

git tag -a "$tag" -m "Rove $version"
git push origin "$tag"

echo "Pushed $tag — the release workflow will build and publish it now."
