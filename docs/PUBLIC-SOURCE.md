# Public source and local commit

Extract the source ZIP completely. It includes MIT-licensed project source, bundled-font notices, build tools, sanitized factory appearance defaults, README files and commit scripts. It excludes Git history, private signing keys, runtime GSI tokens/config, personal state, logs and build caches.

Install Git and Python 3, then run `Commit-Source.cmd` from the source directory. The wrapper invokes `native/scripts/Commit-Public-Source.ps1`, which:

1. Initializes a local `main` repository if needed.
2. Stages the explicit public source paths, respecting the repository ignore rules.
3. Checks whitespace and scans the staged bytes for known private-state/credential patterns.
4. Creates a local commit as **Reticle Lab Contributors <contributors@reticle-lab.invalid>** using the version in the package manifest.

It does not change your global Git identity, add a remote or push to GitHub. A failed audit stops before commit; review the staged files before retrying. Existing Git hooks are not disabled. The source ZIP already includes all commit tools; the separate commit-tools ZIP provides the same files for convenience. Extract that tools ZIP into the source root, never an unrelated project directory.

The released installer is development-signed. Private signing material is deliberately absent from the source; source builds must use the builder's own signing identity before installation.
