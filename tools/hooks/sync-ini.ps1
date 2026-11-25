# Claude Code UserPromptSubmit hook: if the repo's EucMod.ini differs from the copy the game
# reads, rebuild/deploy the script (.\build.ps1 -SkipModel) and report back.
# Silent (no output) when nothing changed, so it costs nothing on normal prompts.
param(
    [string]$GtaDir = 'C:\Program Files\Rockstar Games\Grand Theft Auto V Legacy'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot)
$src = Join-Path $root 'src\EucMod\EucMod.ini'
$dst = Join-Path $GtaDir 'scripts\EucMod.ini'

function Out-Json($message) {
    $obj = @{
        systemMessage      = $message
        hookSpecificOutput = @{ hookEventName = 'UserPromptSubmit'; additionalContext = "[sync-ini hook] $message" }
    }
    $obj | ConvertTo-Json -Compress
}

if (-not (Test-Path $src)) { exit 0 }
if ((Test-Path $dst) -and ((Get-FileHash $src).Hash -eq (Get-FileHash $dst).Hash)) { exit 0 }

$log = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'build.ps1') -SkipModel -GtaDir $GtaDir 2>&1 | Out-String
$ok = (Test-Path $dst) -and ((Get-FileHash $src).Hash -eq (Get-FileHash $dst).Hash)
if ($ok) {
    Out-Json 'EucMod.ini changed - rebuilt and deployed to the game. Press Insert in-game to reload.'
} else {
    $tail = ($log -split "`r?`n" | Where-Object { $_ -match 'error|rror|Exception' } | Select-Object -First 3) -join ' | '
    Out-Json "EucMod.ini changed but deploy FAILED: $tail"
}
exit 0
