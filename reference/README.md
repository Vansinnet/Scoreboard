# Local Reference Material

This ignored tree stores machine-local, versioned evidence used to inspect Valheim and modding-library APIs.

```text
reference/
  assemblies/<steam-build-id>/
  decompiled/<steam-build-id>/
  game-data/<steam-build-id>/
  upstream/<library>/<version-or-commit>/
  inventory/<steam-build-id>-<timestamp>.json
```

Run `scripts/Update-ReferenceInventory.ps1` to record source paths, versions, and SHA-256 hashes. Commit only a sanitized reviewed baseline under `docs/evidence/`.

Use original assemblies for compilation and decompiled C# only for investigation. Never commit or release game DLLs, decompiled game code, exported assets, local inventories, or proprietary third-party runtime files.
