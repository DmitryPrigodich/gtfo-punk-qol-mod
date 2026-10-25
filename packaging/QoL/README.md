# GtfoPunk QoL

Client-side quality-of-life features and bug fixes for GTFO.

- **No host required.** Everything works on your side only.
- **No balance changes.** Safe to use with randoms in matchmaking.
- **Does not require GTFO-API.**
- **Every feature has its own toggle** in `BepInEx/config/AmDDRed.GtfoPunk.QoL.cfg`.
- In-game text language: `[General] TextLanguage = auto` — as the game's language (or `en` / `ru`).
- **Plays nice with other mods:** if you already have a mod that does the same thing (QoLFix, PingEverything, PacksHelper, DropItem, ResourceHelper, DMRReloadFix…), the overlapping feature turns itself off. The startup log shows a summary of what is on, off or skipped.

Gameplay-changing features live in a separate package: **GtfoPunk My Game My Rules**.

## Features

### Interface and convenience
| Feature | What it does |
|---|---|
| SkipIntro | Skips the startup intro. |
| PingDisplay | Ping next to player names in the lobby, your ping to the host in the corner during a mission. |
| PingAnything | If the game finds nothing to ping: pings sleeping enemies nearby (≤12 m) or any world point. |
| LockerItemPing | Pings the item inside an open locker/box instead of the whole container. |
| ResourceAutoMark | Packs and consumables seen by you or teammates get a marker with name and charges (local only). |
| ReturnToLocker | Hold E while looking into an open locker/box to put your pack or consumable back. |
| AllApparel | All apparel available in the APPAREL menu. **Off by default** (experimental: the menu came out empty). |
| PackControls | E — apply to whoever you look at, otherwise yourself; LMB — yourself; RMB — teammate. |
| PackHelper | While holding a resource pack, teammates' health/ammo is shown above them. |
| ResourceReceivedSound | Plays a sound when a teammate gives you ammo or heals you. |
| SprintCancelReload | Sprinting cancels a reload. |
| AutoReload | Reloads automatically when the magazine runs dry. |
| LobbyUnready | Lets you un-ready in the lobby (non-host). |
| InstantReady | Lobby Ready on a single click instead of press-and-hold (option InstantDrop for the host's drop button). |
| SteamProfileLink | Click a player's name in the lobby to open their Steam profile. |
| RecentlyPlayedWith | Adds lobby players to Steam's "Recently played with". |
| ElevatorVolume | Quieter sound effects during the elevator ride (50% by default). |
| ElevatorSkip | Skips the drop-in sequence. |
| BackgroundFpsLimit | Caps FPS to 30 while the game is not focused. |
| ScreenLiquidFilter | Hides blood/goo/etc. on screen, per category. **Off by default.** |
| NoiseRemoval | Removes the blue-noise grain overlay. **Off by default.** |
| WeaponStats | Weapon stats in the loadout: damage, mag, reserve, fire rate, reload, multipliers, spread, hits-to-kill. |
| TerminalPlus | Linux-style terminal: `ls`, `cat`, `clear`, `p am 69`, `ls \| grep ammo`, `!!`, `history`, Ctrl+L/C/U/V/R; Tab after `READ` completes the log file name. |
| TerminalArchive | ARCHIVE button on the rundown screen (next to CREDITS): read the terminal logs of the expeditions you've completed. |
| PreciseHud | Health with decimals and HP, tool charges, teammates' health/infection on the map. |
| HudToggle | F9 hides the whole HUD (screenshots). |
| ExpeditionStats | Results screen: time, downs, kills/damage per player (when hosting), team kills, your packs. |

### Bug fixes
| Fix | What it fixes |
|---|---|
| KeepFlashlight | Flashlight no longer turns off when you drop or deploy an item. |
| DmrReloadFix | Magazine no longer under-fills on reload (DMR with one round left). |
| WeaponAnimFix | Reload animation no longer carries over to the next weapon. |
| BioTrackerFixes | Bio tracker doesn't eat tool refills; old scan tags don't linger on screen. |
| ScoutScreamMuffleFix | Sound no longer stays muffled after a scout scream. |
| DoorFoamFix | C-foam no longer passes through door cracks. |

## Credits
Several features re-implement ideas from community mods (rewritten from scratch for the current game version): QoLFix (notpeelz), PingEverything / ResourceHelper / PacksHelper (Localia), DropItem (Extra), DMRReloadFix (randomuserhi), AutoReloadPlus (Dinorush), WeaponStatShower (DacreDOLOLO), BashTerm (food), TerminalCompletion (OneOfTheLiving), PreciseUI (Andocas), HUDToggler (ValenTeam). Thanks to their authors.

---

## По-русски

Удобства и багфиксы для GTFO. Всё работает только у тебя: хост не нужен, баланс не меняется, можно играть с рандомами. Каждая фича включается и выключается в `BepInEx/config/AmDDRed.GtfoPunk.QoL.cfg`. Если у тебя уже стоит мод с такой же функцией, наша часть сама выключится. Что включено, что нет и почему — видно в логе при запуске.

Геймплейные изменения — в отдельном пакете **GtfoPunk My Game My Rules**.
