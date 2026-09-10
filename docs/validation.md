# Validation

`scripts/Validate.ps1` is the canonical validation command. It performs:

1. Machine-local path and sanitized reference-baseline verification.
2. Steam build, Unity, BepInEx, Harmony, and assembly SHA-256 checks.
3. Locked NuGet restore.
4. Release compilation with warnings treated as errors.
5. Automated aggregation, retry, identity, limit, and persistence checks.
6. Version consistency across the project, plugin metadata, and README.
7. Repository policy checks for local paths, binaries, archives, secrets, and forbidden runtime dependencies.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Validate.ps1
```

Passing validation means the source compiles against the recorded local baseline and automated checks pass. It does not establish in-game behavior. Manual coverage is recorded separately under `docs/manual-testing.md` and in the generated release record.

Any failed required check blocks packaging and publication. Do not replace it with a weaker check without documenting and reviewing a baseline update.
