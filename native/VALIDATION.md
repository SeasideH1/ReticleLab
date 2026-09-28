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

## 0.2.12 update validation

- Reproduced the final-kill ordinal reset with the old reducer. All 158 core checks pass after the fix, including 10 new round-ending ordering checks.
- Widget builds with zero warnings/errors. Release Widget/Bridge publishing, WinRT entrypoint, native API boundary, MakePRI/MakeAppx and unsigned-package structural checks pass.
- Both primary text fields and both captions share measured font baselines. The whole text group scales down together for short feed bars. Native in-game visual acceptance remains pending.
- Unsigned MSIX SHA256: `376663D3C04C0E183B665933BBCED7B8A7516A75436BFF027DF05F0DED2968BB`.
- The desktop-account signing script subsequently produced the signed MSIX. Its CMS signature and expected signer were reverified; all 552 original payload entries match the unsigned build. Signed MSIX SHA256: `7CADEA0724C37EBE20CAC90DECA9656DFF76A1517B1FC4A2C7DE2F17E9D02B66`.
- Full installer ZIP includes the runtime, pinned VCLibs dependency and GSI setup scripts. Extracted-path testing under Windows PowerShell 5.1 passes for a path with spaces, cancellation, corrupt package, missing dependency and invalid certificate. Nested public audit passes for 596 entries. No trust import or deployment was performed during validation; second-PC installation remains untested.
- Full installer ZIP SHA256: `421EA687994EC1E798E0358F57CD3D595E0C53F3DE8569E70BCFC70264170219`.

## 0.2.11 update validation

- 148 core checks and Widget/Bridge compilation passed; original native API boundary audits passed.
- New desktop-account development certificate signs the unchanged validated MSIX payload. Old key files were retained and hash-compared unchanged.
- Relocated update passes Windows PowerShell 5.1 ValidateOnly; corruption, missing certificate and substitution of the old public certificate are rejected.
- Nested archive public scan passes. The launcher also resolves the local runtime with PowerShell 7 removed from PATH.
- No certificate trust import or app deployment was performed during validation. Cursor behavior, certificate-renewal deployment and OBS recording remain interactive acceptance items.

## 0.2.10 update validation

132 core checks pass, including bounded mouse history and interpolation, correlated folder-launch results while the receiver lock is held, exact whole-valued damage parsing, captured appearance defaults and per-field configuration merging. Widget compilation passes with no warnings or errors. The folder-command tests substitute the Explorer launch action; actual packaged Game Bar-to-Explorer activation and host window-position restoration still need native UI acceptance. No claim of missing game-side damage availability is made.
