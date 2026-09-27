<div align="center">

# Reticle Lab

**Your crosshair. Your layout. Your data stays local.**

A Windows Xbox Game Bar overlay with pixel-aware crosshairs, independent kill feedback, live self statistics, and configurable keyboard/mouse visuals.

[简体中文](README.zh-CN.md) · [Install](#install) · [Build](#build-from-source) · [Data boundaries](#what-cs2-can-and-cannot-tell-us) · [Safety review](docs/ANTI-CHEAT-REVIEW.md)

![Reticle Lab interface illustration](docs/preview.svg)

**Windows x64 · Game Bar · C# / .NET 10 · MIT project code · Development release 0.2.10**

[0.2.10 changes](docs/RELEASE-0.2.10.md): smoother mouse trails, reliable folder-launch requests, saved layouts and captured appearance defaults. [Public source / local commit](docs/PUBLIC-SOURCE.md).

</div>

> Development software. Not affiliated with Microsoft, Valve, or Murbong. No VAC approval or ban-free guarantee. A target of 240 FPS is a setting, not a measured performance claim.

## Four widgets, one workspace

| Widget | What you can tune |
| --- | --- |
| **Crosshair** | Cross, ring, center dot, color, physical-pixel size, anti-aliasing, one-pixel nudges and live preview. |
| **Kill feedback** | Separate rows per kill, quartic easing, independent position, color and timing. The number is a labeled **GSI update damage delta**, never guessed target damage. |
| **Live stats** | K/D/A, round kills, headshot-kill ratio, available damage and money. Per-item labels, sizes, visibility and snapping. |
| **Keyboard & mouse** | Per-key A/B display modes, editable bindings, mouse-button highlighting and auto-fitting motion trails within a fixed panel. |

### Small details that matter

- Crosshair size and arrow-key movement default to **physical pixels**, including scaled Windows desktops.
- Each Game Bar window owns its own UI dispatcher and visual cache.
- Retained visuals, reusable feedback objects and 12 batched trail polylines reduce repeated object creation.
- A configurable **1–240 FPS target** coalesces updates. Idle animation timers stop; data sampling stays independent.
- Mouse detection uses documented, mouse-only **Windows Raw Input**, with a switch and a visible-widget lease.
- All project runtime state stays on the current computer. No account, cloud service, telemetry endpoint or uploaded gameplay data is implemented.

## Install

Use the `ReticleLab-0.2.10-windows-x64.zip` development bundle supplied with this source checkout or a future release attachment. This repository does not claim that a GitHub Release has already been published.

1. Extract the **whole** ZIP to a writable folder. Do not run the installer from inside the ZIP.
2. Run `Install-ReticleLab.cmd` under the Windows account that will use the widgets. The script validates package hashes, signatures, publisher and dependencies. Windows asks for administrator approval **only when the development certificate needs to be trusted**.
3. Ensure Xbox Game Bar is installed, then press **Win + G**, open the Reticle widgets and pin them. Updates request shutdown of this package's existing widgets/helper and preserve settings.
4. Optional CS2 integration: open Reticle settings → **GSI** → start/check receiver. Run `Setup-GSI.cmd` from the bundle, then restart CS2. It finds Steam libraries and installs the **new computer's own** generated configuration, backing up an existing Reticle config.

If Steam detection fails:

```powershell
.\Setup-GSI.cmd -Cs2Directory "<CS2 installation directory>"
```

The app is self-contained: **no separate .NET runtime, Visual Studio or CS2 installation is needed for the crosshair alone**. Windows 10 build 19041+ / Windows 11 x64 and Xbox Game Bar are required. The ZIP includes the x64 Visual C++ framework dependency and public development certificate. ARM64 and non-Windows systems are not validated.

This is an **installable bundle**, not a zero-install portable executable. The certificate is self-signed and expires; Windows policies can block sideloading. Never disable security software or anti-cheat to install it. See [installation details](docs/INSTALL.md).

## What CS2 can and cannot tell us

The receiver accepts authenticated GSI only on `127.0.0.1:29841`. It requires the local player identity to match the provider and quarantines spectated-player state.

| Requested feedback | Implemented meaning |
| --- | --- |
| Kill | Change in reliable self kill counters; initial connection and reconnection do not replay history. |
| Two kills in one update | Two separate rows. Crosshair effects queue for separate playback; this queue is presentation timing. |
| Headshot kill | Confirmed only when the available counters permit attribution. Mixed aggregate attribution stays unknown. |
| Weapon | **Currently held** weapon fallback; not a claim about which weapon caused the kill. |
| Damage number | Difference between consecutive valid `round_totaldmg` samples, labeled **更新增量**. Split rows from one update carry the same update total; do not sum those rows. |
| Missing damage / reset / reconnect | **—**, not a fabricated zero. A valid unchanged counter gives **0**. |
| Body/head hit, hit accuracy, per-victim damage | Not reliably available from the self GSI used here. Hit buttons are explicitly **demos**. |
| Buy menu | Not detected. Optional dimming follows the entire freeze phase. |

Do not mistake the offline HTML prototype's simulated events for native GSI capabilities.

## Architecture

```mermaid
flowchart LR
    CS2[CS2 self GSI] -->|Authenticated localhost POST| Bridge[ReticleBridge]
    Mouse[Windows mouse Raw Input] -->|Optional visible-widget lease| Bridge
    Bridge -->|Atomic local snapshots| Store[Package LocalState]
    Store --> Crosshair[Crosshair widget]
    Store --> Feed[Kill feed widget]
    Store --> Stats[Stats widget]
    Store --> Input[Input widget]
    GameBar[Xbox Game Bar hotkey API] --> Input
```

The bridge is a declared full-trust desktop helper; it is not a kernel driver or a game module. The native app is an independent implementation inspired by crosshair utilities. No Murbong source is redistributed.

## Build from source

Requirements: Windows x64, .NET SDK 10, PowerShell 7 for development signing, Windows PowerShell 5.1 for the installer tests, Python 3 and Node.js for auxiliary checks. NuGet restore requires network access. Licensed Noto fonts and original app icons are included; a CS2 install is not required to build.

```powershell
.
ative\scriptsuild.ps1
# The output prints the new artifact directory.
python native/scripts/audit.py --package "<unsigned.msix>"
python native/scripts/http_smoke.py "<artifact directory>/package/Bridge/ReticleBridge.dll"
node --test tests/model.test.cjs tests/overlays.test.cjs
python tests/check_assets.py
```

`build.ps1` runs core smoke tests, publishes both processes, audits the compiled entry point/native API boundary and validates the MSIX. Build tools and SDK metadata are restored from pinned NuGet dependencies.

For local signing and distribution, follow [the signing guide](native/DEVELOPMENT-SIGNING.md). **Do not publish `.signing`, PFX files, runtime configs, logs or LocalState.** A fork must use its own signing identity and update installer pins; it cannot reuse the private key for these release packages.

## Validation and limits

The source includes **132 core checks**, authenticated HTTP receiver tests, real mouse registration/lease and sharing-recovery checks, package integrity checks, installer negative tests and public-export scanning. See [validation](native/VALIDATION.md) for the actual executed scope.

Game Bar appearance, real in-game input, multi-monitor centering, long-session stability and achieved frame rate still require interactive acceptance. We do not label compilation as a successful game test. The design uses documented APIs, but that is **not a VAC certification or tournament permission**. Read the [anti-cheat review](docs/ANTI-CHEAT-REVIEW.md) before use in a restricted environment.

## Contribute

Bug reports with reproducible steps and sanitized diagnostics are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md). Keep unknown values unknown; do not add memory reads, injection, input automation or inferred enemy information.

## License and credits

Project-authored code and original illustrations: **[MIT](LICENSE)**. Noto fonts: **SIL OFL 1.1**. Microsoft SDK/runtime components keep their own licenses; they are not relicensed under MIT. See [third-party notices](native/THIRD-PARTY.md).

Names of games and platforms identify compatibility only. The preview above is an original illustration using sample values, not a verified game screenshot.
