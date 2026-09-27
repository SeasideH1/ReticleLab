# Anti-cheat boundary review

Review date: 2026-09-28 · release line: 0.2.7 · scope: this first-party source and its compiled assemblies, not Valve's detector or every dependency implementation.

## Conclusion

**No game-process opening, game-memory reading/writing, process injection, Windows hooks or input synthesis was found in the reviewed first-party code and native import inventory.** The implementation uses Game Bar UI, self GSI and an optional Windows mouse-input receiver.

**This is not a claim of VAC approval, zero ban risk, FACEIT/ESEA support or tournament permission.** No public Valve statement approving this specific app was found, and no VAC-protected match was used as a validation experiment. Do not weaken Trusted Mode, add `-allow_third_party_software`, disable anti-cheat or inject a workaround to make an overlay appear.

## Evidence and limits

| Surface | Verified implementation | Remaining boundary |
| --- | --- | --- |
| Rendering | Declared Xbox Game Bar widgets using normal XAML transparency/opacity; per-window dispatchers. | Game Bar hosting does not itself grant permission under every game's rules. |
| Game data | IPv4 loopback-only HTTP; token comparison; self/provider identity check; selected fields only; spectating quarantined. | A process running as the same Windows user can access local files/token. GSI does not prove per-hit or per-target attribution. |
| Mouse | `RegisterRawInputDevices` Page 1 / Usage 2 / INPUTSINK to this helper's message-only HWND; unregister on switch-off/lease expiry. | Receives mouse activity outside CS2 while enabled and visible. It does not intercept input; anti-cheat acceptance is not certified. |
| Keyboard | Normal focused UI events and optional Game Bar hotkey watcher for configured keys. | Not a raw global keyboard recorder. |
| Full trust | The declared helper hosts localhost HTTP and owns its own window/message loop. | It is a desktop process, not an AppContainer-only app. No claim that restricted privileges alone enforce all boundaries. |
| Game files | Optional setup copies only a generated `gamestate_integration_reticlelab.cfg`, with backup; timing helper changes two GSI values. | No game binary/DLL modification, launch-option changes or console command injection is performed. |
| Installer | Pinned hashes, signer/publisher and dependency checks; only target-package shutdown. Public certificate import is explicit. | Self-signed development distribution is not a Microsoft Store approval. |

## Compiled native API inventory

`NativeBoundaryAudit` reads metadata from the published `ReticleWidget.dll`, `Core.dll`, `Bridge/ReticleBridge.dll` and `Bridge/Core.dll`; it does not load them into a game. Only the helper has direct P/Invokes:

- `kernel32`: `GetCurrentPackageFamilyName`, `GetModuleHandleW`.
- `user32`: `RegisterClassW`, `UnregisterClassW`, `CreateWindowExW`, `RegisterRawInputDevices`, `GetRawInputData`, `GetMessageW`, `TranslateMessage`, `DispatchMessageW`, `DefWindowProcW`, `PostMessageW`, `DestroyWindow`, `PostQuitMessage`.

`PostMessageW` targets the helper's own window for shutdown, not game input. `GetModuleHandleW(null)` obtains this helper's module, not the game. `Marshal.AllocHGlobal` allocates a local raw-input buffer; it is not remote process memory access.

The audit rejects imports outside this reviewed set. The source audit also rejects process/memory/injection/input-synthesis APIs and confines raw-input registration to the mouse receiver. These checks cannot detect every possible malicious technique and are not a full formal proof. Runtime and Game Bar dependencies are not fully re-audited or reimplemented here.

## What Valve and Microsoft actually say

- Valve describes VAC as a cheat-detection system and treats unauthorized advantage-providing modifications as cheats: [VAC FAQ](https://help.steampowered.com/en/faqs/view/571A-97DA-70E9-FF74). The published policy does not whitelist this project.
- Valve's [CS2 Trusted Mode FAQ](https://help.steampowered.com/en/faqs/view/09A0-4879-4353-EF95) discusses restrictions on third-party process interaction and targeted process tampering. Avoiding those mechanisms is a design choice, not a grant of permission.
- The [Steam Subscriber Agreement](https://store.steampowered.com/subscriber_agreement/) covers cheats and unauthorized third-party software. Server, tournament and other anti-cheat rules remain separate.
- Microsoft documents [transparent Game Bar widgets](https://learn.microsoft.com/en-us/xbox/game-bar/guide/transparency), [click-through](https://learn.microsoft.com/en-us/xbox/game-bar/guide/click-through) and [Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input). Those APIs are documented; that does not mean Valve approves every use of them.

Valve Support pages were available through search excerpts during this review; opening them returned a minimal page. No private VAC internals or unpublished whitelist was accessible. Microsoft's Raw Input and transparency documentation was directly readable.

## Practical release decision

This build is suitable for **source review and development evaluation**, not a universal anti-cheat compatibility assertion. A crosshair overlay can still fall under a competition's prohibited-assistance rules. Where policies restrict overlays/input visualizers, leave the plugin disabled and obtain the platform/organizer's permission rather than changing anti-cheat settings.

Never describe this app as “VAC safe”, “undetected” or “ban-proof”. Re-run the source, compiled-boundary, dependency and export checks after any code change. A previous scan does not cover a modified build.
