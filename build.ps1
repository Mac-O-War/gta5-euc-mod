# Full build: Blender model -> GTA assets -> dlc.rpf -> deploy, plus the C# script.
#   .\build.ps1              everything
#   .\build.ps1 -SkipModel   only the C# script
param(
    [switch]$SkipModel,
    [string]$GtaDir = 'C:\Program Files\Rockstar Games\Grand Theft Auto V Legacy',
    [string]$Blender = 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$env:BLENDER_USER_RESOURCES = Join-Path $root 'tools\blender_user'
$sitePackages = Join-Path $root 'tools\blender_user\extensions\.user\user_default\sollumz\lib\python3.11\site-packages'
$blenderPython = Join-Path (Split-Path $Blender) '4.3\python\bin\python.exe'

if (-not $SkipModel) {
    $export = Join-Path $root 'build\export'
    if (Test-Path $export) { Remove-Item -Recurse -Force $export }

    Write-Host '== Exporting model from Blender' -ForegroundColor Cyan
    & $Blender -b -P (Join-Path $root 'tools\blender\build_euc.py') -- `
        (Join-Path $root 'assets\blender\euc_offroad.blend') $export euc_offroad | Select-String 'TEXTURE|ARCHETYPE|WARNING|ERROR|Error|OUT '
    if (-not (Test-Path (Join-Path $export 'euc_offroad.ydr'))) { throw 'Model export failed' }

    Write-Host '== Building rider animations' -ForegroundColor Cyan
    $animDir = Join-Path $root 'build\anim'
    $rigBlend = Join-Path $root 'assets\game_extract\player_zero_rig.blend'
    if (-not (Test-Path $rigBlend)) { throw "Missing $rigBlend (player_zero rig imported via Sollumz; see README)" }
    & $Blender -b -P (Join-Path $root 'tools\blender\anim\build_euc_anims.py') -- $rigBlend $animDir | Select-String 'CLIP|EXPORT|Error|Traceback'
    $ycdconv = Join-Path $root 'tools\ycdconv\bin\Release\net7.0\ycdconv.dll'
    if (-not (Test-Path $ycdconv)) { dotnet build (Join-Path $root 'tools\ycdconv') -c Release --nologo -v q | Out-Null }
    dotnet $ycdconv (Join-Path $animDir 'euc_rider.ycd.xml') (Join-Path $export 'euc_rider.ycd')
    if ($LASTEXITCODE -ne 0) { throw 'Animation conversion failed' }

    Write-Host '== Packing dlc.rpf' -ForegroundColor Cyan
    $dlc = Join-Path $root 'build\dlc\eucmod\dlc.rpf'
    $env:PYTHONPATH = $sitePackages
    & $blenderPython (Join-Path $root 'tools\build_dlc.py') $export $dlc
    if ($LASTEXITCODE -ne 0) { throw 'DLC pack failed' }

    $modsPack = Join-Path $GtaDir 'mods\update\x64\dlcpacks\eucmod'
    if (Test-Path (Join-Path $GtaDir 'mods')) {
        New-Item -ItemType Directory -Force $modsPack | Out-Null
        try {
            Copy-Item -Force $dlc $modsPack
            Write-Host "Deployed dlc.rpf to $modsPack" -ForegroundColor Green
        } catch {
            Write-Warning "Could not copy dlc.rpf (is GTA V running? it locks the pack). Close the game and run .\build.ps1 again."
        }
    } else {
        Write-Warning "No mods folder in $GtaDir - set up OpenIV's mods folder first (see README). dlc.rpf is in build\dlc."
    }
}

Write-Host '== Building script' -ForegroundColor Cyan
dotnet build (Join-Path $root 'src\EucMod') -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Script build failed' }
