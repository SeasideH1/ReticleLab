param([Parameter(Mandatory=$true)][string]$Package, [Parameter(Mandatory=$true)][string]$Certificate)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security.Cryptography.Pkcs
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package).Path)
try {
    $entry = $zip.GetEntry('AppxSignature.p7x')
    if (!$entry) { throw 'Package has no signature.' }
    $stream = $entry.Open()
    $buffer = [IO.MemoryStream]::new()
    try { $stream.CopyTo($buffer); $bytes = $buffer.ToArray() } finally { $stream.Dispose(); $buffer.Dispose() }
} finally { $zip.Dispose() }
if ([Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne 'PKCX') { throw 'Invalid signature header.' }
$cms = [Security.Cryptography.Pkcs.SignedCms]::new()
$cms.Decode([byte[]]$bytes[4..($bytes.Length-1)])
# Verify cryptography without installing or trusting the self-signed root.
$cms.CheckSignature($true)
$cert = [Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificateFromFile((Resolve-Path -LiteralPath $Certificate).Path)
try {
    if ($cms.SignerInfos.Count -ne 1 -or $cms.SignerInfos[0].Certificate.Thumbprint -ne $cert.Thumbprint) { throw 'Signer mismatch.' }
    Write-Output "PASS: CMS cryptographic signature and signer certificate match ($($cert.Thumbprint)). This does not establish OS trust."
} finally { $cert.Dispose() }
