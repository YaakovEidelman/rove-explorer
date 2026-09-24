#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

install_local=false
for arg in "$@"; do
  [[ "$arg" == "--install" ]] && install_local=true
done

now="$(date +%s)"
version="0.2.$((now / 86400)).$(((now % 86400) / 60))"
sha="$(git rev-parse --short HEAD)"
dirty=""
[[ -n "$(git status --porcelain)" ]] && dirty=" (dirty working tree)"

echo "Testing..."
dotnet test Rove.slnx -c Release --nologo -p:RuntimeIdentifier=linux-x64

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

if $install_local; then
  echo "Installing the build just published..."
  stage/rove/Rove --install
fi
