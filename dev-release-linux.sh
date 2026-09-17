#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

# Builds and tests the current working tree, then uploads a linux-x64 build
# to the "dev" prerelease on GitHub. Run this locally instead of pushing to
# main (dev-build.yml is manual-trigger only now). See dev-release-win.ps1
# for the Windows half — run that separately on a Windows machine.

version="0.2.$(date +%s)"
sha="$(git rev-parse --short HEAD)"
dirty=""
[[ -n "$(git status --porcelain)" ]] && dirty=" (dirty working tree)"

echo "Testing..."
dotnet test Rove.slnx -c Release --nologo

echo "Publishing rove $version..."
rm -rf out/linux stage rove-linux-x64.tar.gz
dotnet publish src/Rove.UI/Rove.UI.csproj \
  -c Release -r linux-x64 \
  -p:Version="$version" \
  -p:DebugType=none \
  -o out/linux

dotnet publish src/Rove.Portal/Rove.Portal.csproj \
  -c Release -r linux-x64 \
  -p:Version="$version" \
  -p:DebugType=none \
  -o out/linux

rm -f out/linux/*.pdb out/linux/*.dbg
mkdir -p stage/rove
cp out/linux/* stage/rove/
chmod +x stage/rove/Rove stage/rove/rove-portal
tar -czf rove-linux-x64.tar.gz -C stage rove

if ! gh release view dev >/dev/null 2>&1; then
  gh release create dev \
    --prerelease \
    --title "Dev build (latest main)" \
    --notes "Manual dev builds. Unpack and run Rove — see docs/installing.md."
fi

gh release upload dev rove-linux-x64.tar.gz --clobber

echo "Uploaded rove-linux-x64.tar.gz $version @ $sha$dirty to the dev release."
