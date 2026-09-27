#requires -Version 5.1
$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $projectRoot
$identityNames=@('GIT_AUTHOR_NAME','GIT_AUTHOR_EMAIL','GIT_COMMITTER_NAME','GIT_COMMITTER_EMAIL')
$previous=@{}
try {
    Get-Command git,python -ErrorAction Stop | Out-Null
    [xml]$manifest=Get-Content -LiteralPath 'native/Widget/Package.appxmanifest' -Encoding UTF8
    $version=([version]$manifest.Package.Identity.Version).ToString(3)
    foreach($name in $identityNames){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
    $env:GIT_AUTHOR_NAME='Reticle Lab Contributors';$env:GIT_COMMITTER_NAME='Reticle Lab Contributors'
    $env:GIT_AUTHOR_EMAIL='contributors@reticle-lab.invalid';$env:GIT_COMMITTER_EMAIL='contributors@reticle-lab.invalid'
    if (!(Test-Path -LiteralPath '.git')) {git init -b main;if($LASTEXITCODE -ne 0){throw 'Git initialization failed.'}}
    git -c core.excludesFile= add -- .gitignore .gitattributes .github Directory.Build.props NuGet.Config LICENSE README.md README.zh-CN.md CONTRIBUTING.md SECURITY.md Commit-Source.cmd docs assets app.js index.html model.js overlays.js styles.css native tests
    if($LASTEXITCODE -ne 0){throw 'Git staging failed. No commit created.'}
    git diff --cached --check
    if($LASTEXITCODE -ne 0){throw 'Whitespace check failed. No commit created.'}
    python native/scripts/public_audit.py --staged
    if($LASTEXITCODE -ne 0){throw 'Public index audit failed. No commit created.'}
    git -c commit.gpgsign=false commit -m "Prepare Reticle Lab $version public source and relocatable installer"
    if($LASTEXITCODE -ne 0){throw 'Git commit failed; review status. No push attempted.'}
    git log -1 --format='%h %an <%ae> %s'
    Write-Host 'Local commit complete. No remote was added and nothing was pushed.'
} finally {
    foreach($name in $identityNames){if($previous.ContainsKey($name)){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}}
    Pop-Location
}
