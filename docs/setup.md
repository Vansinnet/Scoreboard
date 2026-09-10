# Development Setup

## Verified Local Baseline

Observed on 2026-09-10:

| Component | Value |
|---|---|
| Valheim installation | `D:\Steam\steamapps\common\Valheim` |
| Steam build ID | `25185596` |
| Unity | `6000.0.75f1` |
| BepInEx assembly | `5.4.23.3` |
| HarmonyX assembly | `2.9.0` |
| .NET SDK | `6.0.428` |

The sanitized reference hashes are committed in `docs/evidence/reference-baseline.json`. Local DLLs and full inventories are excluded from Git.

## Requirements
- A licensed local Valheim installation matching the reference baseline.
- The Valheim-specific BepInEx 5 distribution.
- .NET SDK `6.0.428`, locked by `global.json`.
- PowerShell 5.1 or newer.
- Git and GitHub CLI for publication.

Jötunn and Unity Editor are not required for Scoreboard. The plugin targets .NET Framework 4.7.2 while using the locked .NET SDK for compilation.

## Configuration
1. Copy `config/paths.example.props` to `config/paths.local.props`.
2. Set `ValheimInstall`, `ValheimManaged`, and `BepInExRoot`.
3. Optionally set `ModDeployPath` to an isolated test profile's `BepInEx/plugins/Scoreboard` directory.

`paths.local.props` is machine-local and ignored by Git.

## Commands
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Validate.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Deploy-Mod.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Release.ps1
```

Build and validation never deploy or launch Valheim. Deployment is explicit.
