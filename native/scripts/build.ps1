param([string]$NuGetConfig = '', [switch]$NoPackage, [switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$nativeRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $nativeRoot '.dotnet'
$env:NUGET_PACKAGES = Join-Path $nativeRoot '.packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $nativeRoot '.httpcache'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
function RunDotnet([string[]]$Arguments) { & dotnet @Arguments; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $Arguments" } }
$restoreArgs = @('-p:NuGetAudit=false')
if ($NuGetConfig) { $restoreArgs += @('--configfile', $NuGetConfig) }
foreach ($project in $(if ($NoRestore) { @() } else { @('Widget','Bridge','Smoke') })) {
    RunDotnet (@('restore', (Join-Path $nativeRoot "$project/$project.csproj")) + $restoreArgs)
}
RunDotnet @('run','--project',(Join-Path $nativeRoot 'Smoke/Smoke.csproj'),'--no-restore')
if ($NoPackage) { RunDotnet @('build',(Join-Path $nativeRoot 'Widget/Widget.csproj'),'--no-restore'); return }
if (!(Test-Path (Join-Path $nativeRoot 'Widget/Fonts/notosanssc-regular.ttf'))) { throw 'Run prepare_assets.py with the installed CS2 panorama/fonts directory first.' }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $nativeRoot 'Widget/Package.appxmanifest') -Encoding utf8
$version = ([version]$manifest.Package.Identity.Version).ToString(3)
$packageName = "ReticleLab-$version-x64-unsigned.msix"
if ($NoRestore) { $restoreArgs = @('--no-restore') }
# Each build stages into a new directory; no deletion of earlier artifacts or user data.
$buildId = Get-Date -Format 'yyyyMMdd-HHmmss'
$outDir = Join-Path $nativeRoot "artifacts/$buildId"
$packageDir = Join-Path $outDir 'package'
New-Item -ItemType Directory -Force $packageDir | Out-Null
RunDotnet (@('publish',(Join-Path $nativeRoot 'Widget/Widget.csproj'),'-c','Release','-r','win-x64','--self-contained','true','-o',$packageDir) + $restoreArgs)
RunDotnet @('run','--project',(Join-Path $nativeRoot 'Smoke/Smoke.csproj'),'--no-restore','--','--entrypoint',(Join-Path $packageDir 'ReticleWidget.dll'))
RunDotnet (@('publish',(Join-Path $nativeRoot 'Bridge/Bridge.csproj'),'-c','Release','-r','win-x64','--self-contained','true','-o',(Join-Path $packageDir 'Bridge')) + $restoreArgs)
RunDotnet @('run','--project',(Join-Path $nativeRoot 'Smoke/Smoke.csproj'),'--no-restore','--','--native-boundary',$packageDir)
# Only new staging output is touched; historical builds and source symbols remain local.
foreach ($symbol in @(Get-ChildItem -LiteralPath $packageDir -Filter '*.pdb' -File -Recurse)) {
    Remove-Item -LiteralPath $symbol.FullName
}
Copy-Item -LiteralPath (Join-Path $nativeRoot 'Widget/Package.appxmanifest') -Destination (Join-Path $packageDir 'AppxManifest.xml')
Copy-Item -LiteralPath (Join-Path $nativeRoot 'Widget/Assets') -Destination $packageDir -Recurse -Force
Copy-Item -LiteralPath (Join-Path $nativeRoot 'Widget/Fonts') -Destination $packageDir -Recurse -Force
Copy-Item -LiteralPath (Join-Path $nativeRoot 'THIRD-PARTY.md') -Destination $packageDir
$sdk = Join-Path $env:NUGET_PACKAGES 'microsoft.gaming.xboxgamebar/7.3.2607010'
Copy-Item -LiteralPath (Join-Path $sdk 'runtimes/win10-x64/native/Microsoft.Gaming.XboxGameBar.dll') -Destination $packageDir
Copy-Item -LiteralPath (Join-Path $sdk 'runtimes/win10-x64/native/Microsoft.Gaming.XboxGameBar.pri') -Destination $packageDir
Copy-Item -LiteralPath (Join-Path $sdk 'license.txt') -Destination (Join-Path $packageDir 'GAMEBAR-LICENSE.txt')
RunDotnet @('run','--project',(Join-Path $nativeRoot 'Smoke/Smoke.csproj'),'--no-restore','--','--prepare-gamebar',$packageDir,$sdk)
New-Item -ItemType Directory -Force (Join-Path $packageDir 'GameBar') | Out-Null
Set-Content -LiteralPath (Join-Path $packageDir 'GameBar/readme.txt') -Value 'Reticle Lab widgets' -Encoding utf8
$toolsDir = Join-Path $env:NUGET_PACKAGES 'microsoft.windows.sdk.buildtools/10.0.26100.6584/bin/10.0.26100.0/x64'
& (Join-Path $toolsDir 'makepri.exe') createconfig /cf (Join-Path $outDir 'priconfig.xml') /dq zh-CN /o
if ($LASTEXITCODE -ne 0) { throw 'makepri createconfig failed' }
& (Join-Path $toolsDir 'makepri.exe') new /pr $packageDir /cf (Join-Path $outDir 'priconfig.xml') /of (Join-Path $packageDir 'resources.pri') /o
if ($LASTEXITCODE -ne 0) { throw 'makepri new failed' }
& (Join-Path $toolsDir 'makeappx.exe') pack /d $packageDir /p (Join-Path $outDir $packageName) /o
if ($LASTEXITCODE -ne 0) { throw 'makeappx validation failed' }
Get-FileHash -Algorithm SHA256 (Join-Path $outDir $packageName) | Format-List
Write-Output "Unsigned development package: $outDir"
