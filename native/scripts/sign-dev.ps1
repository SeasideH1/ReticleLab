param([Parameter(Mandatory=$true)][string]$Package, [string]$Jsign)
$ErrorActionPreference = 'Stop'
$nativeRoot = Split-Path $PSScriptRoot -Parent
$inputPackage = (Resolve-Path -LiteralPath $Package).Path
$keyDir = Join-Path $nativeRoot '.signing'
$pfxPath = Join-Path $keyDir 'ReticleLab.Local.pfx'
$passwordPath = Join-Path $keyDir 'password.dpapi'
$certificatePath = Join-Path $keyDir 'ReticleLab.Local.cer'
$signtool = Join-Path $nativeRoot '.packages/microsoft.windows.sdk.buildtools/10.0.26100.6584/bin/10.0.26100.0/x64/signtool.exe'
if ($Jsign) {
    $Jsign = (Resolve-Path -LiteralPath $Jsign).Path
    if ((Get-FileHash -LiteralPath $Jsign -Algorithm SHA256).Hash -ne '602A51C3545A6DC4FB99BD2EA7152B26D1345916D0C93DDFBD5936CB735AF91C') { throw 'Unexpected Jsign 7.5 binary hash.' }
} elseif (!(Test-Path -LiteralPath $signtool)) { throw 'Build the native package first to restore the Windows SDK signing tools.' }

$zip = [IO.Compression.ZipFile]::OpenRead($inputPackage)
try {
    $entry = $zip.GetEntry('AppxManifest.xml')
    if (!$entry) { throw 'Package manifest missing.' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $publisher = $manifest.Package.Identity.Publisher
} finally { $zip.Dispose() }
if ($publisher -ne 'CN=ReticleLab.Local') { throw "Unexpected Publisher: $publisher" }

New-Item -ItemType Directory -Force $keyDir | Out-Null
if (!(Test-Path -LiteralPath $pfxPath)) {
    $rsa = [Security.Cryptography.RSA]::Create(3072)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new($publisher, $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
        $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new([Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $true))
        $oids = [Security.Cryptography.OidCollection]::new()
        [void]$oids.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
        $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($oids, $true))
        $cert = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddYears(1))
        try {
            $password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
            $protected = [Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($password), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
            [IO.File]::WriteAllBytes($passwordPath, $protected)
            [IO.File]::WriteAllBytes($pfxPath, $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password))
            [IO.File]::WriteAllBytes($certificatePath, $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
        } finally { $cert.Dispose() }
    } finally { $rsa.Dispose() }
}
$clearBytes = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($passwordPath), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
$password = [Text.Encoding]::UTF8.GetString($clearBytes)
[Array]::Clear($clearBytes, 0, $clearBytes.Length)
$outDir = Join-Path ([IO.Path]::GetDirectoryName($inputPackage)) ('signed-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $outDir | Out-Null
$pendingPath = Join-Path $outDir 'ReticleLab-signing-pending.msix'
$version = ([version]$manifest.Package.Identity.Version).ToString(3)
$signedPath = Join-Path $outDir "ReticleLab-$version-x64-dev-signed.msix"
Copy-Item -LiteralPath $inputPackage -Destination $pendingPath
try {
    if ($Jsign) {
        $env:RETICLE_SIGN_PASSWORD = $password
        & java -jar $Jsign --keystore $pfxPath --storetype PKCS12 --storepass env:RETICLE_SIGN_PASSWORD --alg SHA-256 $pendingPath
    } else {
        # SignTool requires the PFX password as a process argument; never log it.
        & $signtool sign /fd SHA256 /a /f $pfxPath /p $password $pendingPath
    }
    if ($LASTEXITCODE -ne 0) { throw "Package signing failed, exit code $LASTEXITCODE" }
} finally { $password = $null; Remove-Item Env:\RETICLE_SIGN_PASSWORD -ErrorAction SilentlyContinue }
& (Join-Path $PSScriptRoot 'verify-dev-signature.ps1') -Package $pendingPath -Certificate $certificatePath
Move-Item -LiteralPath $pendingPath -Destination $signedPath
Copy-Item -LiteralPath $certificatePath -Destination (Join-Path $outDir 'ReticleLab.Local.cer')
$publicCert = [Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificateFromFile($certificatePath)
try {
    [ordered]@{
        SignedPackage = [IO.Path]::GetFileName($signedPath)
        SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $signedPath).Hash
        Subject = $publicCert.Subject
        Thumbprint = $publicCert.Thumbprint
        Expires = $publicCert.NotAfter.ToUniversalTime().ToString('O')
        TrustImported = $false
        Timestamped = $false
        SigningTool = $(if ($Jsign) { 'Jsign 7.5' } else { 'Windows SDK SignTool' })
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outDir 'signing-info.json') -Encoding utf8
} finally { $publicCert.Dispose() }
Write-Output "Development signing complete: $outDir"
