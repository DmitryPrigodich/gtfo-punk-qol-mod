# GtfoPunk QoL

Quality-of-life features and bug fixes for GTFO. Client-side only: no host needed, no balance changes, safe with randoms.

A plugin for [BepInEx 6 IL2CPP](https://thunderstore.io/c/gtfo/p/BepInEx/BepInExPack_GTFO/) (GTFO). Feature description for players: [packaging/QoL/README.md](packaging/QoL/README.md), version history: [packaging/QoL/CHANGELOG.md](packaging/QoL/CHANGELOG.md).

## Install (players)
Zip from releases → r2modman → *Settings → Import local mod*. Requires BepInExPack_GTFO.

## Build
- .NET SDK 6+ (target `net6.0`).
- A `lib/` folder with game references (not in the repo — these are game and BepInEx files). Copy from an r2modman profile:
  - `BepInEx/core/*.dll` (0Harmony, BepInEx.Core, BepInEx.Unity.IL2CPP, BepInEx.Unity.Common, BepInEx.Preloader.Core, Il2CppInterop.*)
  - `BepInEx/interop/*.dll` (Modules-ASM, GameData-ASM, SNet_ASM, Il2Cppmscorlib, Il2CppSystem*, UnityEngine.*Module, Unity.TextMeshPro, Shards-ASM, CellSound-ASM, Wwise-ASM, …)
  - `interop` appears after the first game launch with BepInEx.
- `./build.sh` → `dist/GtfoPunk_QoL-<version>.zip` (ready package for r2modman/Thunderstore). Other library path: `dotnet build QoL/GtfoPunk.QoL.csproj -c Release -p:GameLibs=/path/to/lib`.

## Layout
- `Shared/Core` — the shared GtfoPunk core (identical in both repositories): modules with individual config toggles, Client/Host/Everyone marking, auto-disable when a mod with the same feature is installed, en/ru in-game language.
- `QoL/Modules` — one file per feature (closely related small features share a file: `ElevatorTweaks.cs`, `ScreenEffects.cs`).
- `QoL/GlobalUsings.cs` — usings every module needs (`GtfoPunk.Core`, `HarmonyLib`, `BepInEx.Configuration`, collections).
- `packaging/QoL` — manifest, README, CHANGELOG, package icon.

## Conventions
- A module = one class deriving from `ModuleBase`: Id (also the config section), Description, Side; Harmony patches as nested `[HarmonyPatch]` classes; per-frame work via `FrameTicker.OnUpdate` with its own throttle — no MonoBehaviours, no scene searches every frame.
- Config keys are never renamed silently: changing a default means a new key name (…2), so players' saved values aren't lost or misread.
- Code comments, config descriptions and log messages are in English. Russian lives only in localization (`Lang.T(en, ru)`) and in the Russian section of the player README.

Related repository: gtfo-punk-rules-mod (gameplay changes).

## License
MIT — see [LICENSE](LICENSE).
