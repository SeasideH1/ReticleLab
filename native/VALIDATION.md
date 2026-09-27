# Release validation

Public summary for **0.2.9**, reviewed 2026-09-28. Private machine logs, paths and gameplay snapshots are not part of this repository.

| Check | Scope / result |
| --- | --- |
| Core smoke | 111 checks passed: self GSI counters, unknown fields, update-damage attribution, reset/reconnect, multi-kill delivery, frame pacing, shared-file recovery and adaptive trail bounds. |
| UI dispatcher model | Two independent dispatcher-context smoke threads; compiled MTA/startup/static-render subscription checks passed. No claim of real Game Bar visual acceptance. |
| Compiled native boundary | Published first-party assemblies matched 14 explicitly reviewed package/window/mouse P/Invokes. |
| HTTP receiver | Real spawned helper on a test loopback port; authentication, dedup, reset, labeled damage, payload limits and observer isolation. |
| Mouse infrastructure | Real Windows Raw Input registration, switch/lease lifecycle and file-sharing recovery; no synthesized game input. |
| HTML prototype | 29 Node tests and local-asset/ID checks; public illustration frames replace private reference screenshots. |
| GSI setup | Real Windows PowerShell 5.1 fixture copy with spaces in paths, backup, idempotence, foreign URI/extra command rejection. |
| Packaging | MakePRI/MakeAppx, SDK WinMD/registration, signature/payload and API audits. |
| Export hygiene | Public-candidate and nested-archive scans; staged-index scanning is provided in the final commit script for private state, keys, user paths, identifiers and known local token; explicit distribution allowlist. Pattern scanning is not proof against every possible secret. |
| Installer | Valid bundle and wrong hash/missing dependency/bad certificate tests; relocation checked without installation or certificate trust changes. |

The 0.2.9 checks add previous-round preservation, freeze shopping, early/late map round changes, GSI previous-value damage fallback and missing-damage diagnostics. The actual HTTP receiver persists these fields across round transitions. Seven PowerShell 5.1 prompt cases cover whitespace/case, retry, cancellation and missing input without changing certificate trust or installing an app.

The earlier 0.2.7 public-source copy compiled with zero warnings/errors and passed extracted-path validation. The 0.2.9 source/package are rebuilt and audited separately; see its local release evidence for hashes. GitHub Actions configuration is included for future repository runs; no successful hosted CI run is claimed before publishing.

## Not yet validated

- Installation on a second physical PC or clean Windows VM.
- Native Game Bar visual appearance, multiple monitors/DPI and real CS2 mouse interactions after this build.
- Sustained real-game stability, achieved 240 FPS, CPU/GPU/frame-time comparisons.
- Approval by Valve, VAC, FACEIT, tournament operators or other anti-cheat systems.

No claim of per-hit/body/head data support has been added. Unknown damage remains unknown; the visible number is a GSI update delta, not target damage. See the [anti-cheat review](../docs/ANTI-CHEAT-REVIEW.md).

## 0.2.10 update validation

132 core checks pass, including bounded mouse history and interpolation, correlated folder-launch results while the receiver lock is held, exact whole-valued damage parsing, captured appearance defaults and per-field configuration merging. Widget compilation passes with no warnings or errors. The folder-command tests substitute the Explorer launch action; actual packaged Game Bar-to-Explorer activation and host window-position restoration still need native UI acceptance. No claim of missing game-side damage availability is made.