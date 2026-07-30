param(
    [string]$AppDir = (Resolve-Path (Join-Path $PSScriptRoot "..\release")).Path
)

$ErrorActionPreference = "Stop"
$packageName = "B41D0BF2-9C32-4CE0-A4C1-12AD47C95613"
$manifestPath = Join-Path $AppDir "AppxManifest.xml"

if (-not (Test-Path (Join-Path $AppDir "WPT.exe"))) {
    throw "WPT.exe not found in '$AppDir'. Publish WinUI first."
}

$existing = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
if ($existing) {
    Remove-AppxPackage -Package $existing.PackageFullName
}

Copy-Item (Join-Path $PSScriptRoot "..\WPT.WinUI\Package.appxmanifest") $manifestPath -Force
$manifest = (Get-Content $manifestPath -Raw).Replace('$targetnametoken$.exe', 'WPT.exe').Replace('$targetentrypoint$', 'Windows.FullTrustApplication')
Set-Content $manifestPath -Value $manifest -Encoding UTF8

$makePri = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter "makepri.exe" |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $makePri) {
    throw "makepri.exe not found. Install Windows SDK."
}

$configPath = Join-Path $AppDir "priconfig.xml"
@'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<resources targetOsVersion="10.0.17763.0" majorVersion="1">
  <index root="\" startIndexAt="Assets">
    <default>
      <qualifier name="Language" value="en-US" />
    </default>
    <indexer-config type="PRI" />
    <indexer-config type="RESFILES" qualifierDelimiter="." />
  </index>
</resources>
'@ | Set-Content $configPath -Encoding UTF8

& $makePri.FullName new /pr $AppDir /cf $configPath /of (Join-Path $AppDir "resources.pri") | Out-Null
Add-AppxPackage -Register -Path $manifestPath -ExternalLocation $AppDir -ForceApplicationShutdown
Write-Host "Registered sparse package for '$AppDir'"
