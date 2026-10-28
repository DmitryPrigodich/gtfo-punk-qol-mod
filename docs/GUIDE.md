# GtfoPunk QoL — guide

Client-side quality of life and bug fixes for GTFO (BepInEx 6, IL2CPP). [Русская версия](GUIDE.ru.md).

- **Client-side:** nothing has to be installed by the host or the others; no balance changes — safe with randoms.
- Settings: `BepInEx/config/AmDDRed.GtfoPunk.QoL.cfg`. Every feature is a section with `Enabled` (on by default unless
  noted). Keys that are no longer used are removed from the file at start.
- `[General] RespectOtherMods = true`: a feature turns itself off if a known mod with the same feature is installed
  (e.g. QoLFix, SkipIntro) — no double patches.
- In-game text: `[General] TextLanguage = auto` (as the game's language), `en` or `ru`.
- Pairs with **GtfoPunk My Game My Rules** (gameplay, everyone needs it) but doesn't require it.

## Menus and lobby

| Feature | What it does | Settings |
|---|---|---|
| SkipIntro | Skips the intro at game start. | — |
| MenuMusic | The main-menu music keeps playing on the rundown screen and in the lobby (the game silences it there; with SkipIntro there was practically no menu music at all). Silenced when the drop starts. | — |
| TerminalArchive | **ARCHIVE** button next to CREDITS on the rundown screen: a terminal with the logs of the expeditions you've completed (every rundown you've opened). | `ShowUncompleted`, `SecondaryLayers`, `ButtonPlacement`, `FontScale` |
| AllApparel | Every apparel item (helmets, torsos, legs, backpacks, palettes) in the APPAREL menu. Local only — nothing is sent anywhere. | — |
| InstantReady | Ready on a single click instead of holding. | `InstantDrop` (host's drop button too; off) |
| LobbyUnready | Unready if you pressed Ready by accident (not for the host). | — |
| SteamProfileLink | Click a player's name in the lobby — their Steam profile opens. | — |
| RecentlyPlayedWith | Lobby players go to Steam's "Recently played with". | — |
| PingDisplay | Players' ping in the lobby, your ping to the host in a corner during a mission. | `ShowInLobby`, `ShowInHud`, `UpdateInterval` |
| WeaponStats | Weapon stats in the loadout description (damage, magazine, rate, hits to kill). | `ShowHitsToKill` |
| BotGearGuard | A bot whose saved gear no longer exists (a removed mod's weapon or sentry) gets stock gear instead of an empty slot. | — |

### Archive terminal

Opens with HELP and the list of logs. Commands:
- `LIST` — logs of all completed expeditions; `LIST R4` — one rundown; `LIST R4A1` — one expedition.
  A table: CODE, NAME, LOG ID, AUDIO.
- `READ <id>` — show a log (the start of the ID is enough; Tab completes it).
- `PLAY <id>` — play an audio log; `STOP` — stop it.
- `CLS` — clear, `HELP`, `EXIT` (or Esc).

Completions come from the game's own progression and are remembered in `BepInEx/config/GtfoPunk.Archive.txt`, so a
rundown you've seen once stays in the archive.

## Elevator

| Feature | What it does | Settings |
|---|---|---|
| ElevatorSkip | Skips the drop-in scene (seats, unbuckling). | — |
| ElevatorVolume | Quieter effects during the ride. | `Volume` (0.5) |

## In the level

| Feature | What it does | Settings |
|---|---|---|
| AutoReload | Reloads when the magazine runs dry. | `Delay`, `WhileRunning` |
| SprintCancelReload | Sprint cancels a reload (no rounds added, as with a normal cancel). | — |
| PackControls | Pack: E — on target or yourself, LMB — yourself, RMB — the teammate you look at. | — |
| PackHelper | While you hold a pack, teammates' health and ammo are shown above them. | — |
| ReturnToLocker | Hold E to put a pack or consumable back into an open locker/box. | `HoldSeconds` |
| ResourceAutoMark | Markers on resources and consumables you or teammates found (only you see them). | `DiscoverRadius`, `ShowDistance`, `MarkConsumables` |
| ResourceReceivedSound | A sound when a teammate gives you ammo or heals you. | `MinHealPercent` |
| PingAnything | Ping sleeping enemies nearby and any world point if the game found no target. | `PingSleepingEnemies`, `SleepingEnemyRange`, `PingWorldPoint` |
| LockerItemPing | Pings the item inside an open locker/box instead of the container. | — |
| PreciseHud | Health with tenths and in HP, tool charges, teammates' health/infection on the map. | `Health`, `ToolCharges`, `TeamOnMap` |
| ExpeditionStats | Results screen: time, downs, kills and damage (exact when you host), your packs. | — |
| HudToggle | F9 hides the whole HUD (screenshots). | `Key` |
| TerminalPlus | Linux-like terminal: aliases (ls, cat, clear, p, q…), `| grep`, `!!`, history, Ctrl+L/C/U/V/R, Tab after READ. | `Aliases`, `ObjectExpansion` |
| BackgroundFpsLimit | Limits FPS when the game is out of focus (not with V-Sync). | `MaxFps` |

## Fixes

| Feature | What it fixes |
|---|---|
| BioTrackerFixes | The bio-tracker doesn't eat tool refills; old scanner markers don't stay on screen. |
| DmrReloadFix | The magazine not filling completely (DMR with one round left). |
| DoorFoamFix | C-foam flying through the gaps of closed doors. |
| KeepFlashlight | The flashlight turning off when you drop or place an item. |
| WeaponAnimFix | The reload animation carrying over to another weapon on a quick switch. |
| ScoutScreamMuffleFix | Sound staying muffled after a scout scream (also reset when leaving to the lobby). |

## Off by default

| Feature | What it does |
|---|---|
| ScreenLiquidFilter | Removes liquids on screen by category: `HideEnemyBlood`, `HidePlayerBlood`, `HideGoo`, `HideWater`, `HideDisinfection`, `HideInfection`. |
| NoiseRemoval | Removes the blue grain over the image (dark areas may get darker). |

## Troubleshooting

- Logs: `BepInEx/LogOutput.log`; QoL lines start with the feature name (`[Archive]`, `[MenuMusic]` …).
- A feature that "does nothing": check its `Enabled` and whether another mod replaced it (the log says
  "already provided by …").
