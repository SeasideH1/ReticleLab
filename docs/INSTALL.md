# Install on another Windows computer

## Before starting

- Windows 10 x64 build 19041+ or Windows 11 x64. ARM64 is not validated.
- Xbox Game Bar installed for the account that will use the widgets. Microsoft Store product ID: `9NZKPSTSNW4P`.
- Extract the entire distribution ZIP to a writable folder. The package contains the app's .NET runtime and x64 VCLibs dependency; it does not include Game Bar itself.
- This is a self-signed development release. The public certificate is included; the private key is not. Check its fingerprint and validity in `signing-info.json` and verify `SHA256SUMS.txt` if transferring through an untrusted channel. A checksum shipped beside a file detects accidental changes but is not independent proof of origin.

## Install

Run `Install-ReticleLab.cmd`. Review the included project/component licenses and type `INSTALL` when asked; automation may explicitly pass `-AcceptComponentTerms` after accepting those terms. Validation checks the fixed MSIX/dependency hashes, CMS signer, certificate identity/validity and Microsoft dependency signature. The script requests trust of this development certificate in **LocalMachine/TrustedPeople**, with a Windows administrator prompt if needed. It does not add a root CA or disable security settings. App installation continues as the original user.

Installing updates requests shutdown of existing Reticle package processes and retains LocalState. New computers start with their own settings and token. No runtime state or token from the developer's computer is included.

The confirmation ignores case and surrounding spaces. Empty/invalid answers allow up to three attempts; `Q` cancels with exit code 2 and no deployment error stack. Cancellation does not import certificates or install packages. `Certificate trusted: False` before the trust prompt is normal on a new computer.

If system policy blocks sideloading, follow your administrator's policy. Do not disable security software. A newer installed package is not downgraded. Run `Diagnose-ReticleLab.cmd` for local diagnostics; review logs before sharing because paths and gameplay statistics can be private.

## CS2 setup on the destination computer

Open Reticle settings → GSI → start/check receiver. This generates a new random local token/config. Then run `Setup-GSI.cmd` from the extracted bundle. It discovers Steam library locations from registry/libraryfolders.vdf and backs up an existing Reticle config before copying the destination computer's generated file.

For a custom layout:

```powershell
.\Setup-GSI.cmd -Cs2Directory "<directory containing game/csgo/cfg>"
```

Fully restart CS2 after changing its GSI configuration. Never distribute `gsi-token.txt` or a real generated cfg with a release. The crosshair works without GSI; actual kill/stat feedback requires available, valid self-state. Body/head hit events remain unavailable.

`Update-GSI-Latency.cmd` is optional for older configurations. It backs up the Reticle file, preserves its endpoint/token and changes only buffer/throttle values. It does not change launch flags or other game configs.

## Uninstall

Uninstall Reticle Lab through Windows Installed Apps. An optional leftover `gamestate_integration_reticlelab.cfg` in your CS2 cfg directory can be removed manually; it is the only game-side config the setup script writes. Remove the development certificate from TrustedPeople only if you no longer use other packages signed by that same publisher certificate. Do not remove Microsoft's framework or root certificates.

The checked bundle is relocatable and has been validated from a separate path with spaces. A second physical PC/clean Windows VM installation has **not** been performed; missing Game Bar, corporate policy, certificate expiration and Store/dependency servicing can still affect installation.
