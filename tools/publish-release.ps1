param(
    [switch]$RegisterSparsePackage
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$releaseDir = Join-Path $root "release"

Write-Host "Generating app icons..."
python (Join-Path $PSScriptRoot "generate_app_icon.py")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Publishing WinUI (WPT.exe, self-contained single-file)..."
dotnet publish (Join-Path $root "WPT.WinUI\WPT.WinUI.csproj") -c Release /p:PublishProfile=win-x64-single
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Publishing WPF (WPT WPF.exe, self-contained single-file)..."
dotnet publish (Join-Path $root "WPT.Wpf\WPT.Wpf.csproj") -c Release /p:PublishProfile=win-x64-single
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Release folder: $releaseDir"
Get-ChildItem $releaseDir -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $releaseDir -File | Sort-Object Name | ForEach-Object {
    $sizeMb = [math]::Round($_.Length / 1MB, 2)
    Write-Host ("  {0,-16} {1,8} MB" -f $_.Name, $sizeMb)
}

if ($RegisterSparsePackage) {
    & (Join-Path $PSScriptRoot "register-winui-sparse.ps1") -AppDir $releaseDir
}

Write-Host ""
Write-Host "Done."
