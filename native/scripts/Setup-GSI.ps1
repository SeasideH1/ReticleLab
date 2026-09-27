#requires -Version 5.1
param([string]$Cs2Directory,[string]$SourceConfig)
$ErrorActionPreference='Stop'
try {
    . (Join-Path $PSScriptRoot 'SteamPaths.ps1')
    $destination=Join-Path (Find-Cs2ConfigDirectory $Cs2Directory) 'gamestate_integration_reticlelab.cfg'
    if (!$SourceConfig) {
        $app=Get-AppxPackage -Name ReticleLab.GameBar | Where-Object Publisher -eq 'CN=ReticleLab.Local' | Select-Object -First 1
        if (!$app) {throw 'Install Reticle Lab first.'}
        $SourceConfig=Join-Path $env:LOCALAPPDATA ("Packages/{0}/LocalState/gamestate_integration_reticlelab.cfg" -f $app.PackageFamilyName)
    }
    if (!(Test-Path -LiteralPath $SourceConfig)) {throw 'Open Reticle settings > GSI > Start / check receiver on THIS computer first, then run this script again.'}
    $text=[IO.File]::ReadAllText((Resolve-Path -LiteralPath $SourceConfig).Path)
    if ($text -notmatch '"uri"\s+"http://127\.0\.0\.1:29841/gsi"' -or $text -notmatch '"token"\s+"[A-Fa-f0-9]{64}"') {throw 'Invalid local Reticle configuration. No game files changed.'}
    # Only the known generated KeyValues fields are accepted; no game console commands.
    foreach ($line in ($text -split '\r?\n')) {
        if ($line -notmatch '^\s*(?:"Reticle Lab"|[{}]|"(?:uri|timeout|buffer|throttle|heartbeat|provider|map|round|player_id|player_state|player_match_stats|player_weapons)"\s+"[^"]+"|"auth"\s*\{\s*"token"\s+"[A-Fa-f0-9]{64}"\s*\}|"data"\s*\{)?\s*$') {throw 'Unexpected configuration content. No game files changed.'}
    }
    if (Test-Path -LiteralPath $destination) {
        if ([IO.File]::ReadAllText($destination) -eq $text) {Write-Host 'Already configured for this computer.';exit 0}
        Copy-Item -LiteralPath $destination -Destination ($destination+'.'+[guid]::NewGuid().ToString('N')+'.bak')
    }
    Copy-Item -LiteralPath $SourceConfig -Destination $destination -Force
    Write-Host 'Installed this computer''s own Reticle GSI config. Existing file backed up if needed. Restart CS2.'
    exit 0
} catch {Write-Error $_;exit 1}
