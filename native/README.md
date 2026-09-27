# Native workspace

See the [project README](../README.md) and [中文说明](../README.zh-CN.md) for features and installation.

| Directory | Responsibility |
| --- | --- |
| `Core` | Self-state reducer, feedback cursor, geometry, frame pacing, bounded trails and atomic shared files. |
| `Widget` | Game Bar activation, one dispatcher per view, XAML widgets/settings and retained visuals. |
| `Bridge` | Authenticated loopback GSI receiver and optional mouse-only Raw Input. |
| `Smoke` | Core behavior, view context, metadata/package and native import checks. |
| `scripts` | Build, signing, installer, GSI setup, smoke tests and public-export checks. |

Four widget extensions plus one settings extension are declared in the MSIX; settings has six tabs. Project identity is `ReticleLab.GameBar`, publisher `CN=ReticleLab.Local`. A fork should choose its own identity and signing material before distributing.

Build with `scripts/build.ps1` on Windows using .NET SDK 10. Build outputs, caches and private keys are ignored. The public font provenance uses generic source descriptions rather than developer paths. `Directory.Build.props` remaps first-party source paths in compiler output; packaged PDBs are omitted.

The HTML at repository root is a separate offline visual prototype. It does not use native GSI or Raw Input and has deliberately simulated data. Native behavior and boundaries are documented in [validation](VALIDATION.md), [third-party notices](THIRD-PARTY.md), [signing](DEVELOPMENT-SIGNING.md) and the [anti-cheat review](../docs/ANTI-CHEAT-REVIEW.md).
