# Release Process

## Preconditions
- The release is explicitly requested.
- `scripts/Validate.ps1` passes.
- Version is consistent in project/plugin/user documentation.
- Engine-facing contracts and reference evidence are current.
- Runtime coverage and remaining risks are stated accurately.
- The Git worktree is clean and `origin/main` equals local `HEAD` before GitHub publication.

## Build The Installation Archive
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Release.ps1
```

The script rebuilds from authoritative source and produces:

```text
artifacts/<version>/
  Scoreboard-<version>.zip
  Scoreboard-<version>.manifest.sha256
  Scoreboard-<version>.zip.sha256
  release-record.md
```

The ZIP has exactly one `Scoreboard/` root containing only:
- `Scoreboard.dll`
- `README.md`
- `LICENSE`

The wrapper inspects archive paths and hashes the actual extracted archive bytes before completion.

## Publish GitHub Release
Runtime-pending builds must be prereleases:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Publish-GitHubRelease.ps1 -Version 0.1.0
```

The publisher revalidates/rebuilds, verifies a clean pushed commit, rejects an existing tag, and uploads the ZIP, payload manifest, ZIP checksum, and release record. GitHub's generated source archives are repository snapshots and are not installation packages.

Do not mark a release stable/latest until the listen-server and dedicated-server acceptance checklist has documented Runtime-verified evidence.
