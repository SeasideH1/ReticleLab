param([string]$ConfigPath)
$ErrorActionPreference='Stop'
try {
    if (!$ConfigPath) { . (Join-Path $PSScriptRoot 'SteamPaths.ps1'); $ConfigPath=Join-Path (Find-Cs2ConfigDirectory '') 'gamestate_integration_reticlelab.cfg' }
    $target=(Resolve-Path -LiteralPath $ConfigPath).Path
    if([IO.Path]::GetFileName($target) -ne 'gamestate_integration_reticlelab.cfg') {throw 'Only the Reticle GSI configuration can be updated.'}
    $contents=[IO.File]::ReadAllText($target)
    if($contents -notmatch '"uri"\s+"http://127\.0\.0\.1:29841/gsi"') {throw 'Unexpected receiver URI. No changes made.'}
    $updated=$contents
    foreach($setting in @(@('buffer','0.0'),@('throttle','0.03'))) {
        $pattern='(?m)^(\s*"'+$setting[0]+'"\s+)"[0-9.]+"'
        if([regex]::Matches($updated,$pattern).Count -ne 1) {throw "Expected one $($setting[0]) setting. No changes made."}
        $updated=[regex]::Replace($updated,$pattern,('${1}"'+$setting[1]+'"'))
    }
    if($updated -eq $contents) {Write-Host 'Already using the low-latency settings. Restart CS2 if it was running during the previous update.';exit 0}
    $backup=$target+'.'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'.bak'
    Copy-Item -LiteralPath $target -Destination $backup -ErrorAction Stop
    [IO.File]::WriteAllText($target,$updated,[Text.UTF8Encoding]::new($false))
    Write-Host 'Updated buffer=0.0, throttle=0.03. URI and token were preserved.'
    Write-Host "Backup: $backup"
    Write-Host 'Fully exit and restart CS2 for the new settings to take effect.'
    exit 0
} catch {Write-Error $_;exit 1}
