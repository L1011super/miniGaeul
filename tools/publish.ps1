$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

dotnet restore
dotnet build -c Release --no-restore
dotnet test tests/GaeulDesktopPet.Tests/GaeulDesktopPet.Tests.csproj -c Release

$out = "dist/GaeulDesktopPet-win-x64"
if (Test-Path $out) { Remove-Item -Recurse -Force $out }

dotnet publish src/GaeulDesktopPet/GaeulDesktopPet.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o $out

$assetRoot = Join-Path $out "Assets/Sprites/01_gaeul_kitsch"
if (-not (Test-Path $assetRoot)) { throw "Runtime animation directory is missing" }
$pngCount = (Get-ChildItem -LiteralPath $assetRoot -Recurse -File -Filter *.png).Count
if ($pngCount -ne 127) { throw "Expected 127 runtime PNG frames, found $pngCount" }

$zip = "dist/GaeulDesktopPet-win-x64.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path "$out/*" -DestinationPath $zip -Force
Write-Host "Published $out and $zip"
