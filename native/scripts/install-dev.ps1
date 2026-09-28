#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$PackageDirectory,
    [switch]$CheckOnly,
    [switch]$ValidateOnly,
    [switch]$TrustOnly,
    [switch]$UpdateOnly,
    [switch]$AllowCertificateRenewal,
    [switch]$AcceptComponentTerms
)
$ErrorActionPreference = 'Stop'
# Launchers such as Python or PowerShell 7 can inherit a module path without
# Windows PowerShell's modules. Restore that path for this process only.
$windowsModules = Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/Modules'
$env:PSModulePath = $windowsModules + ';' + $env:PSModulePath
if (!$PackageDirectory) {
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'ReticleLab-0.2.12-x64-dev-signed.msix')) { $PackageDirectory = $PSScriptRoot }
    elseif ($UpdateOnly) { $PackageDirectory = Join-Path $PSScriptRoot '../artifacts/releases/ReticleLab-0.2.12-update-x64' }
    else { $PackageDirectory = Join-Path $PSScriptRoot '../artifacts/releases/ReticleLab-0.2.12-windows-x64' }
}
$logStarted = $false
$started = Get-Date
$exitCode = 1
$expectedThumbprint = '15F24B4316D19A662750CC286ADEA0C724087581'

function Read-PackageManifest([string]$Path) {
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $zip.GetEntry('AppxManifest.xml')
        if (!$entry) { throw "Manifest missing: $Path" }
        $reader = New-Object IO.StreamReader($entry.Open())
        try { return [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $zip.Dispose() }
}
function Assert-Hash([string]$Path, [string]$Hash) {
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Hash) { throw "File hash mismatch: $Path. Restore the original release files." }
}
function Test-TrustedCertificate {
    return Test-Path "Cert:\LocalMachine\TrustedPeople\$expectedThumbprint"
}
function Confirm-ComponentTerms {
    Write-Host 'Review LICENSE and LICENSES. Project code is MIT; components retain their own terms.'
    Write-Host 'Enter INSTALL then press Enter to continue. Enter Q to cancel. An empty answer does not accept.'
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        $answer = Read-Host 'INSTALL / Q'
        if ($null -eq $answer) { return $false }
        switch ($answer.Trim().ToUpperInvariant()) {
            'INSTALL' { return $true }
            'Q' { return $false }
            'CANCEL' { return $false }
        }
        Write-Host 'No selection recognized. Type INSTALL to continue, or Q to cancel.' -ForegroundColor Yellow
    }
    return $false
}
function Write-FailureDetails($Failure) {
    $message = ($Failure | Format-List * -Force | Out-String) + $Failure.Exception.ToString()
    Write-Host $message -ForegroundColor Red
    $guidPattern = '(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
    foreach ($id in @([regex]::Matches($message, $guidPattern) | ForEach-Object Value | Select-Object -Unique)) {
        try { Get-AppPackageLog -ActivityID $id -ErrorAction Stop | Format-List * | Out-String | Write-Host } catch { Write-Host "Activity log unavailable: $id" }
    }
    try {
        Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-AppXDeploymentServer/Operational'; StartTime=$started} -MaxEvents 40 -ErrorAction Stop |
            Where-Object { $_.Message -match 'ReticleLab|VCLibs' } |
            Select-Object TimeCreated, Id, LevelDisplayName, Message | Format-List | Out-String | Write-Host
    } catch { Write-Host 'No accessible matching deployment events.' }
    $hints = @{
        '800B0109'='Certificate is not trusted. Check LocalMachine/TrustedPeople and system date.'
        '80073CF3'='Dependency/conflict error. See the framework identity and version in the deployment log.'
        '80073D02'='Windows could not release target-package files after requesting shutdown. See the deployment log for remaining owners, then retry.'
        '80073D06'='A newer version is installed. This script will not downgrade or remove it.'
        '80073CFF'='Windows sideload policy blocked installation. Review Settings > For developers or contact your administrator.'
        '80070005'='Access denied. Review deployment log and system policy; do not disable security software.'
        '80080204'='Manifest validation failed. Preserve this log for a package fix.'
        '800704C7'='The elevation request was cancelled. Run again and approve the certificate import if desired.'
    }
    foreach ($code in $hints.Keys) { if ($message -match $code) { Write-Host "$code : $($hints[$code])" -ForegroundColor Yellow } }
}

try {
    $PackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
    $logDir = Join-Path $PackageDirectory 'install-logs'
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    $logPath = Join-Path $logDir ("install-{0}-{1}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $PID)
    Start-Transcript -LiteralPath $logPath | Out-Null
    $logStarted = $true
    Write-Host 'Reticle Lab - development package installer'
    Write-Host "Account: $([Security.Principal.WindowsIdentity]::GetCurrent().Name)"
    Write-Host "PowerShell: $($PSVersionTable.PSVersion); OS: $([Environment]::OSVersion.Version)"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    Add-Type -AssemblyName System.Security
    $package = Join-Path $PackageDirectory 'ReticleLab-0.2.12-x64-dev-signed.msix'
    $certificate = Join-Path $PackageDirectory 'ReticleLab.Local.cer'
    $dependency = Join-Path $PackageDirectory 'Dependencies/x64/Microsoft.VCLibs.x64.14.00.appx'
    Assert-Hash $package '7CADEA0724C37EBE20CAC90DECA9656DFF76A1517B1FC4A2C7DE2F17E9D02B66'
    if (!$UpdateOnly) { Assert-Hash $dependency '9C17B521F9D690A1F504DA5108ED6EEC5669EB3A8FD1331EEF43E40D84E74283' }
    $cert = New-Object Security.Cryptography.X509Certificates.X509Certificate2($certificate)
    if ($cert.Thumbprint -ne $expectedThumbprint -or $cert.Subject -ne 'CN=ReticleLab.Local' -or $cert.HasPrivateKey) { throw 'Unexpected public certificate.' }
    if ((Get-Date) -lt $cert.NotBefore -or (Get-Date) -gt $cert.NotAfter) { throw 'Certificate is outside its validity period. Check system date or rebuild/sign the package.' }
    $manifest = Read-PackageManifest $package
    if ($manifest.Package.Identity.Name -ne 'ReticleLab.GameBar' -or $manifest.Package.Identity.Publisher -ne $cert.Subject) { throw 'Package identity mismatch.' }
    $zip = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $signatureEntry = $zip.GetEntry('AppxSignature.p7x')
        if (!$signatureEntry) { throw 'Unsigned package.' }
        $stream = $signatureEntry.Open(); $buffer = New-Object IO.MemoryStream
        try { $stream.CopyTo($buffer); $bytes = $buffer.ToArray() } finally { $stream.Dispose(); $buffer.Dispose() }
    } finally { $zip.Dispose() }
    if ([Text.Encoding]::ASCII.GetString($bytes,0,4) -ne 'PKCX') { throw 'Invalid signature header.' }
    $cms = New-Object Security.Cryptography.Pkcs.SignedCms
    $cms.Decode([byte[]]$bytes[4..($bytes.Length-1)])
    $cms.CheckSignature($true)
    if ($cms.SignerInfos.Count -ne 1 -or $cms.SignerInfos[0].Certificate.Thumbprint -ne $expectedThumbprint) { throw 'Unexpected package signer.' }
    $frameworkIdentity = @($manifest.Package.Dependencies.PackageDependency | Where-Object { $_.Name -eq 'Microsoft.VCLibs.140.00' })[0]
    if (!$frameworkIdentity) { throw 'Package framework declaration missing.' }
    if (!$UpdateOnly) {
        $depManifest = Read-PackageManifest $dependency
        if ($depManifest.Package.Identity.Name -ne 'Microsoft.VCLibs.140.00' -or $depManifest.Package.Identity.ProcessorArchitecture -ne 'x64') { throw 'Wrong dependency package.' }
        $depSignature = Get-AuthenticodeSignature -LiteralPath $dependency
        if ($depSignature.Status -ne 'Valid' -or $depSignature.SignerCertificate.Subject -ne $depManifest.Package.Identity.Publisher) { throw "Microsoft framework signature validation failed: $($depSignature.StatusMessage)" }
    }
    Write-Host 'PASS: package hash, signature, publisher, certificate validity, and framework declaration.' -ForegroundColor Green
    if ($ValidateOnly) { $exitCode = 0; return }
    if (![Environment]::Is64BitProcess) { throw 'Use 64-bit Windows PowerShell via Install-ReticleLab.cmd.' }
    if ([Environment]::OSVersion.Version -lt [version]'10.0.19041.0') { throw 'Windows 10 build 19041 or later is required.' }
    if ($env:PROCESSOR_ARCHITECTURE -ne 'AMD64') { throw 'This development release targets Windows x64.' }

    if ($TrustOnly) {
        if ($UpdateOnly -and !$AllowCertificateRenewal) { throw 'Certificate renewal must be explicitly enabled for an update.' }
        if ($CheckOnly) { throw 'TrustOnly cannot be combined with CheckOnly.' }
        $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
        if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Certificate import requires elevation.' }
        if (!(Test-TrustedCertificate)) { Import-Certificate -FilePath $certificate -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null }
        if (!(Test-TrustedCertificate)) { throw 'Certificate trust import failed.' }
        Write-Host 'Certificate imported. No application installation in the elevated process.'
        $exitCode = 0; return
    }
    Import-Module Appx -ErrorAction Stop
    $installed = @(Get-AppxPackage -Name ReticleLab.GameBar)
    $framework = @(Get-AppxPackage -Name Microsoft.VCLibs.140.00 | Where-Object { $_.Architecture -in @('X64','Neutral') -and [version]$_.Version -ge [version]$frameworkIdentity.MinVersion -and $_.Status -eq 'Ok' -and $_.Publisher -eq $frameworkIdentity.Publisher })
    $gameBar = @(Get-AppxPackage -Name Microsoft.XboxGamingOverlay)
    Write-Host "Certificate trusted: $(Test-TrustedCertificate); suitable framework installed: $($framework.Count -gt 0); Xbox Game Bar installed: $($gameBar.Count -gt 0)"
    $installed | Select-Object Name, Version, Status, PackageFullName | Format-Table | Out-String | Write-Host
    if (!$gameBar.Count) { Write-Warning 'Xbox Game Bar is missing for this account. Install it from Microsoft Store (product 9NZKPSTSNW4P) to use widgets.' }
    if ($CheckOnly) {
        foreach ($app in $installed) {
            $state = Join-Path $env:LOCALAPPDATA ("Packages/{0}/LocalState" -f $app.PackageFamilyName)
            foreach ($name in @('last-startup.txt','startup-history.txt','last-error.txt','bridge-error.txt','bridge-status.json','mouse-state.json','mouse-error.txt','snapshot.json','feedback-Crosshair.txt','feedback-Feed.txt')) {
                $diagnostic = Join-Path $state $name
                if (Test-Path -LiteralPath $diagnostic) { Write-Host "--- $diagnostic"; Get-Content -LiteralPath $diagnostic -Encoding UTF8 | Out-String | Write-Host }
            }
        }
        try {
            Get-WinEvent -FilterHashtable @{LogName='Application';Id=1000,1026;StartTime=(Get-Date).AddHours(-6)} -MaxEvents 200 -ErrorAction Stop |
                Where-Object { $_.Message -match 'ReticleWidget.exe|ReticleBridge.exe' } | Select-Object -First 10 TimeCreated,Id,Message | Format-List | Out-String | Write-Host
        } catch { Write-Host 'No accessible recent Reticle crash events.' }
        Write-Host 'Diagnostics complete. No certificate or application changes made.'; $exitCode = 0; return
    }
    if ($installed | Where-Object { [version]$_.Version -gt [version]$manifest.Package.Identity.Version }) { throw '0x80073D06: A newer Reticle Lab version is already installed.' }
    if ($UpdateOnly) {
        if (!@($installed | Where-Object { $_.Publisher -eq $cert.Subject -and [version]$_.Version -ge [version]'0.2.7.0' -and $_.Status -eq 'Ok' }).Count) { throw 'Update requires an existing working Reticle Lab 0.2.7 or newer installation for this account. Use a full installer on a new PC.' }
        if (!$framework.Count) { throw 'Existing framework is missing. Repair with the full installer.' }
        if (!(Test-TrustedCertificate) -and !$AllowCertificateRenewal) { throw 'The new certificate is not trusted. Run Update-ReticleLab.cmd to enable certificate renewal with a Windows administrator prompt.' }
    }
    if (!$UpdateOnly -and !$AcceptComponentTerms) {
        if (!(Confirm-ComponentTerms)) {
            Write-Host 'Installation cancelled. No certificate or application changes were made. Run Install-ReticleLab.cmd to try again.'
            $exitCode = 2; return
        }
    }
    if (!(Test-TrustedCertificate)) {
        Write-Host 'Windows will request administrator approval to trust this development certificate. Installation then continues under the original account.'
        $powershell = Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
        $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -PackageDirectory "{1}" -TrustOnly' -f $PSCommandPath, $PackageDirectory
        if($UpdateOnly){$arguments += ' -UpdateOnly -AllowCertificateRenewal'}
        $child = Start-Process -FilePath $powershell -ArgumentList $arguments -Verb RunAs -WindowStyle Hidden -PassThru -Wait
        if ($child.ExitCode -ne 0 -or !(Test-TrustedCertificate)) { throw "Certificate import failed or was cancelled (exit $($child.ExitCode)). See install-logs." }
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $package
    if ($signature.Status -ne 'Valid') { throw "Windows package trust validation failed: $($signature.StatusMessage)" }
    $installArguments = @{Path=$package; ErrorAction='Stop'}
    if (!$framework.Count) { $installArguments.DependencyPath = @($dependency) }
    # Windows scopes shutdown to the verified target package, including its full-trust helper.
    # Do not use ForceApplicationShutdown, which would also close dependency-package apps.
    Write-Host 'Windows will close running Reticle widgets/settings and its GSI receiver for this package update.'
    Add-AppxPackage @installArguments -ForceTargetApplicationShutdown
    $result = @(Get-AppxPackage -Name ReticleLab.GameBar | Where-Object { $_.Publisher -eq $cert.Subject -and [version]$_.Version -eq [version]$manifest.Package.Identity.Version -and $_.Status -eq 'Ok' })
    if (!$result.Count) { throw 'Deployment returned but current-user registration could not be verified.' }
    $result | Select-Object Name, Version, Status, PackageFullName | Format-List | Out-String | Write-Host
    Write-Host 'SUCCESS: installed for the current account. Press Win+G and open Reticle widgets. Game Bar runtime acceptance is still required.' -ForegroundColor Green
    $exitCode = 0
} catch {
    Write-FailureDetails $_
} finally {
    if ($cert) { $cert.Dispose() }
    if ($logStarted) { Write-Host "Log: $logPath"; Stop-Transcript | Out-Null }
    exit $exitCode
}
