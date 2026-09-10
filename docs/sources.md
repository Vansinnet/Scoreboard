# Source Authority And Reference Material

## Priority
1. `src/Scoreboard/` for intended behavior and project style.
2. The current local `assembly_valheim.dll` and related Valheim assemblies for exact game implementation.
3. Installed BepInEx/Harmony assemblies and matching upstream source/documentation.
4. Unity 6000.0 documentation.
5. Jötunn documentation and source only if Jötunn becomes a dependency.
6. Reference mods only as integration examples.

## Local Runtime Material
The default managed assembly location is:

```text
D:\Steam\steamapps\common\Valheim\valheim_Data\Managed
```

Relevant current files include `assembly_valheim.dll`, `assembly_utils.dll`, UnityEngine modules, and `Newtonsoft.Json.dll`. BepInEx and Harmony are under `BepInEx/core/`.

Use metadata/decompilation tools to verify signatures, method bodies, call sites, authority, and lifecycle. Build against original assemblies, not decompiled C#. Keep DLLs and decompiled output under ignored `reference/` paths and never publish them.

Run `scripts/Update-ReferenceInventory.ps1` after a game/runtime update. It creates a full machine-local inventory. Update `docs/evidence/reference-baseline.json` only after reviewing compatibility.

## Upstream Sources
- BepInEx documentation: https://docs.bepinex.dev/
- Valheim BepInEx distribution: https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/
- Valheim BepInEx fork: https://github.com/AzumattDev/BepInEx
- HarmonyX: https://github.com/BepInEx/HarmonyX
- Unity 6 API: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/
- Jötunn API: https://valheim-modding.github.io/Jotunn/api/Jotunn.html
- Jötunn data: https://valheim-modding.github.io/Jotunn/data/intro.html
- Thunderstore package format: https://wiki.thunderstore.io/mods/creating-a-package

Online data may lag the installed build. Critical prefab names and runtime behavior require local verification.
