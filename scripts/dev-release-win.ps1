#!/usr/bin/env pwsh
param([switch]$Install)
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$version = "0.2.$([int]($now / 86400)).$([int](($now % 86400) / 60))"
$sha = (git rev-parse --short HEAD).Trim()
$dirty = ""
if (git status --porcelain) { $dirty = " (dirty working tree)" }

Write-Host "Testing..."
dotnet test tests/Rove.Core.Tests/Rove.Core.Tests.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test tests/Rove.UI.Tests/Rove.UI.Tests.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Publishing rove $version..."
Remove-Item out/win, stage, rove-win-x64.zip -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish src/Rove.UI/Rove.UI.csproj `
  -c Release -r win-x64 `
  -p:Version=$version `
  -p:DebugType=none `
  -o out/win
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Remove-Item out/win/*.pdb -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path stage/Rove -Force | Out-Null
Copy-Item out/win/* stage/Rove/
Compress-Archive -Path stage/Rove -DestinationPath rove-win-x64.zip

gh release view dev *> $null
if ($LASTEXITCODE -ne 0) {
  gh release create dev `
    --prerelease `
    --title "Dev build (latest main)" `
    --notes "Manual dev builds. Unpack and run Rove -- see docs/installing.md."
}

gh release upload dev rove-win-x64.zip --clobber

Write-Host "Uploaded rove-win-x64.zip $version @ $sha$dirty to the dev release."

if ($Install) {
  Write-Host "Installing the build just published..."
  & stage/Rove/Rove.exe --install
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
