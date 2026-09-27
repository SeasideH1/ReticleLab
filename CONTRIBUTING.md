# Contributing

Start with a small, reproducible issue. Include Windows/Game Bar/app versions, DPI, selected widget, input mode and reproduction steps. Remove account names, Steam identifiers, local paths and tokens from logs. Do not upload LocalState or an entire diagnostics archive without reviewing it.

## Local checks

```powershell
.\native\scripts\build.ps1 -NoRestore  # omit NoRestore on first build
node --test tests/model.test.cjs tests/overlays.test.cjs
python tests/check_assets.py
python native/scripts/setup_gsi_smoke.py
python native/scripts/gsi_latency_smoke.py
python native/scripts/public_audit.py --staged
```

Use `http_smoke.py` against the new build's Bridge DLL. It creates fixtures on loopback port 29843 and terminates only its own child. It does not send synthetic input or use a real game session. Keep the test port free.

Keep C# visual objects on their owning dispatcher. Preserve unknown data, physical-pixel offsets, read-only self GSI boundaries and explicit input opt-out. New native API calls require a documented purpose and an updated compiled-boundary audit. Do not introduce process inspection, input automation, captured game content or borrowed assets without an appropriate license.

Report exactly which automated and interactive checks ran. A passing build is not visual acceptance, a source scan is not a VAC guarantee, and a target frame rate is not a benchmark. Do not claim performance gains without comparable frame-time/CPU/GPU evidence.

No personal signing material, generated cfg/token, diagnostic logs, screenshots from private sessions, package caches or build directories belong in a pull request. Git ignores reduce mistakes; review `git diff --cached` and run the public audit before submitting.

For a source checkout with no initial commit, `native/scripts/Commit-Public-Source.cmd` stages the explicit public project paths, checks whitespace and scans the Git index before creating a local MIT-source commit as `Reticle Lab Contributors <contributors@reticle-lab.invalid>`. It does not add a remote or push. Review the staged diff; never use it to publish private state.
