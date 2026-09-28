# Development signing and release assembly

## Build and sign your own development package

```powershell
.\native\scripts\build.ps1
.\native\scripts\sign-dev.ps1 -Package "<unsigned.msix>"
```

Use PowerShell 7/.NET 10 for signing. The default signer is the restored Windows SDK SignTool. An optional `-Jsign <jsign-7.5.jar>` path is hash-checked and requires Java. Jsign is a build tool and is not redistributed in the app or source.

`-KeyDirectory <directory>` selects a separate key directory. If an existing desktop-user key is present under `native/.signing/desktop-user`, it is preferred by default. Run signing under the Windows account that created that key: DPAPI passwords cannot be decrypted merely by switching to another administrator account. Selecting a separate directory preserves the previous key files.

The 0.2.11 updater can trust its pinned replacement certificate through an explicit `-AllowCertificateRenewal` option used by `Update-ReticleLab.cmd`. Package, signature and certificate checks precede the Windows elevation prompt; installation continues under the original user.

On first signing, the script creates a local RSA development certificate under `native/.signing`. The PFX password is random and stored with current-Windows-user DPAPI protection. **Never commit/copy this directory to another computer or a public release.** A fork must make its own certificate and installer pins; it cannot reproduce this publisher's signature without the private key.

The published `.cer` contains only the public certificate. Its subject/thumbprint and the MSIX/dependency hashes are pinned in `install-dev.ps1`. After producing a new release, update those pins and `installer_smoke.py` to the intended release before bundling. Do not disable hash/signature checks for convenience.

SignTool requires a private-key password process argument; the script does not print it. Jsign takes it through a temporary environment variable. Signing is local-only. Packages are not timestamped; the certificate's expiry applies. This is not Microsoft Store certification or a trusted public code-signing identity.

## Produce a transferable bundle

```powershell
python native/scripts/make_distribution.py --signed-dir "<signed output directory>" --dependency "<Microsoft.VCLibs.x64.14.00.appx>"
python native/scripts/public_audit.py --archive "<distribution ZIP>"
python native/scripts/installer_smoke.py
```

The assembler copies an explicit allowlist: signed app, public certificate, Microsoft dependency, install/GSI/diagnostic scripts, licenses and public documentation. It does not copy logs, signing tools, private keys, token/config, settings, diagnostics, reference screenshots or upstream research. It produces per-file SHA-256 checksums and an external ZIP checksum.

A signed payload comparison verifies that signing changed only signature/content-type entries. Metadata import auditing applies to first-party assemblies; Microsoft runtime/SDK internals remain a separate dependency trust boundary. A clean source build and a relocated ValidateOnly test do not substitute for installing on a second PC.
