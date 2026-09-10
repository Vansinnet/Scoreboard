# Valheim Mod Engineering Instructions

This workspace is for creating, validating, and releasing C# mods for Valheim. English is the primary and required language for documentation, source comments, configuration descriptions, logs, UI text, repository metadata, and release notes.

## Default Workflow
1. Start in the target project under `src/<ModName>/` and read its behavior path.
2. Classify the change as trivial, normal, or runtime-sensitive.
3. For runtime-sensitive work, verify exact signatures, call sites, lifecycle, authority, ownership, and cleanup in versioned local assemblies before editing.
4. Make the smallest correct change and preserve existing style.
5. Run `scripts/Validate.ps1` for the complete affected release scope.
6. Record static results, runtime coverage, and untested contexts separately.
7. Release only on explicit request and only through `scripts/Release.ps1` and `scripts/Publish-GitHubRelease.ps1`.

Ask a question only when missing information changes the implementation or would require a risky guess.

## Risk Levels
- **Trivial:** wording, formatting, known constants. Validate the changed surface.
- **Normal:** settings, local behavior, or an established project pattern. Read the entry point and directly related code; run focused tests.
- **Runtime-sensitive:** Harmony patches, networking, persistence, UI/input, Unity lifecycle, assets, client/server behavior, or saved data. Verify implementation and authority from current assemblies, document the contract, and test relevant failure and cleanup paths.

## Source Authority
1. Target mod source under `src/<ModName>/` for intended behavior and style.
2. Versioned local Valheim assemblies for game implementation and exact signatures.
3. Installed, version-matched BepInEx/Harmony assemblies and upstream source/docs.
4. Unity documentation matching the installed engine family.
5. Jötunn source/docs when the project actually depends on Jötunn.
6. Reference mods only as integration examples, never as API authority.
7. General online guides only when authoritative sources are insufficient.

Never invent APIs, overloads, prefab names, authority, or lifecycle. Decompiled implementation is evidence, not a stable API promise.

## Contracts And Evidence
- Record engine-facing findings in `docs/contracts.md` with assembly, exact signature, build/hash evidence, lifecycle, authority, status, and remaining uncertainty.
- Use these statuses: `Source-verified`, `Automated`, `Runtime-verified`, and `Runtime-pending`.
- Keep sanitized reference hashes in `docs/evidence/`; keep game DLLs, full local inventories, decompiled code, and exported assets under ignored `reference/` paths.
- Re-check affected contracts after a Valheim, BepInEx, Harmony, Jötunn, or Unity update.

## Code And Runtime Rules
- Give every plugin a stable unique GUID and one canonical semantic version.
- Use BepInEx configuration and logging. Avoid hot-path allocations and log spam.
- Prefer narrow Harmony prefix/postfix patches. Verify the exact target and interaction with other patches; use transpilers only when necessary.
- Use only the mod's Harmony ID and unregister only owned patches/events/RPC handlers.
- Respect Unity object lifetime, Unity null semantics, and main-thread requirements.
- Make registration idempotent where lifecycle can repeat.
- Explicitly define client, host, and dedicated-server behavior. Never assume a dedicated server has a local player, HUD, camera, input, or graphics resources.
- Validate RPC sender authority, payload bounds, rate limits, protocol compatibility, and reconnect cleanup.
- Treat client-reported statistics as untrusted unless the server independently derives them.
- Version persisted schemas and protocols. Fail closed on unknown formats; document migrations.

## Workspace Ownership
- Source, tests, scripts, and documentation in this repository are authoritative.
- `config/paths.local.props` is machine-local and must not be committed. Use `config/paths.example.props` as the template.
- `reference/`, `bin/`, `obj/`, `artifacts/`, deployed copies, logs, backups, and ZIP files are never development sources.
- Do not commit or release Valheim, Unity, BepInEx, Harmony, Jötunn, or other third-party runtime DLLs.
- Build must not deploy or launch the game. Deployment is explicit and targets a configured test profile.

## Validation And Completion
- `scripts/Validate.ps1` is the canonical validation entry point.
- Locked restore, Release build, zero compiler warnings, automated tests, version consistency, reference-baseline checks, and repository/payload policy must pass.
- A successful build does not verify Harmony targets at runtime, RPC delivery, persistence under process failure, UI rendering, or multiplayer behavior.
- Follow `docs/manual-testing.md` for host and dedicated-server acceptance. Never claim in-game coverage that was not observed.
- Before completion, confirm intended files only, documented contracts, no temporary probes, exact validation results, and the smallest remaining runtime test.

## Release Boundary
- Release only after an explicit user request.
- Build archives only from the authoritative repository source with `scripts/Release.ps1`; never ZIP `bin/`, a deployed copy, or a previous artifact manually.
- Inspect every archive entry. The install ZIP must contain one `Scoreboard/` root and only the allowlisted runtime DLL and user documentation/license.
- Record source commit, version, game/reference baseline, validation, runtime coverage, archive manifest, archive SHA-256, and remaining risks.
- `scripts/Publish-GitHubRelease.ps1` must require a clean worktree and `origin/main` equal to local `HEAD`.
- Runtime-pending builds may only be GitHub prereleases and must not be marked latest/stable.
