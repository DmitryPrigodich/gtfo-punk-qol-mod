# Changelog

## 0.7.6
- **Archive is a terminal now** (the mouse-driven list was hard to control): type commands like on the game's terminals — LIST (expeditions, ✓ — completed), LOGS [R4A1] (log files), READ name (Tab completes), PLAY name / STOP (audio logs), CLS, HELP, EXIT. ↑/↓ — history, PgUp/PgDn or the wheel — scroll, Esc — close.
- ApparelFix: also logs how many cards the apparel popup got per clothes type (diagnostics for the empty APPAREL menu).

## 0.7.5
- New module **ApparelFix** (diagnostics): logs how many apparel items the APPAREL menu got. (The first build also filtered the server's apparel list — that crashed the game at start and was taken out.)
- Archive: audio logs play — P (or a click on the header line) plays / stops.
- Archive: expeditions come from all of the screen's icon lists and are told apart by their data, not by the short name (on R8 the list had one line).

## 0.7.4
- New module **TerminalArchive**: an ARCHIVE button on the rundown screen (a copy of CREDITS, next to it) opens a terminal-like screen with the terminal logs of the expeditions you've completed in this rundown (the screen's own "finished" marks; secondary / overload logs too — SecondaryLayers). Click a line to open it, right click / Backspace — back, wheel — scroll, Esc — close. ShowUncompleted opens the rest too (spoilers).
  The button appears only once the rundown screen is fully up (CREDITS shown, expeditions placed) and hides with CREDITS; while the archive is open the screen's own buttons are paused (no game method is patched for it).
- TerminalPlus: **Tab after READ** (or cat / less) completes the log file name from the files this terminal lists.
- AllApparel is **off by default**: with it on the APPAREL menu came out empty. The unlock itself is behind UnlockExperimental.
- The version line under the game's watermark: added once by a hook on the watermark update instead of a scene search every few seconds (a possible source of hitches).

## 0.7.3
- New module **AllApparel**: every apparel item (helmets, torsos, legs, backpacks, palettes) is available in the APPAREL menu.
- ReturnToLocker: looking at an item lying in the locker no longer shows "put back" — the item can be picked up (a syringe next to a pack couldn't be taken).

## 0.7.2
- The version shows under the game's watermark (bottom right): "GPUNK QOL v0.7.2".
- In-game texts: an old "ru" setting is switched back to auto once (expedition stats follow the game's language).

## 0.7.1
- In-game texts (expedition stats and the rest) follow the game's language by default: Russian when the game is in Russian ([General] TextLanguage = auto; en / ru to force one). The old Language key is moved over if it was changed.

## 0.7.0
- Removed **LoadoutPresets** (F5–F8 loadout presets) — little real use for the code it took. Its file `BepInEx/config/GtfoPunk.LoadoutPresets.json` can be deleted.
- Removed the **StatusInChat** test module (it was off by default).
- Code cleanup, no behaviour changes otherwise:
  - ReturnToLocker looks for the container 10 times a second instead of every frame while you hold a pack or consumable;
  - ResourceAutoMark checks distance first and does the expensive checks only for items near a player;
  - PingDisplay and SteamProfileLink no longer update lobby name tags while you're in a level;
  - the "UNREADY" button text is localized;
  - shared helpers (sorted raycasts), common usings, unused core code removed.

## 0.6.3
- New **InstantReady**: the lobby Ready button works on a single click instead of press-and-hold. Option InstantDrop makes the host's drop button instant too (off by default).
- **ElevatorSkip** is now on by default (existing configs keep their own value).

## 0.6.2
- Config descriptions and log messages are now in English (Russian stays in the in-game localization, `[General] Language = ru`). No behaviour changes; setting keys are unchanged, so existing configs keep their values.

## 0.6.1
- **AutoReload**: reloads automatically when the magazine is empty (not while sprinting or holding E). Turns itself off if AutoReloadPlus is installed.

## 0.6.0
- **WeaponStats**: weapon stats in the loadout description (damage, mag, reserve, fire rate, reload, multipliers, spread, pierce, hits-to-kill on common sleepers).
- **LoadoutPresets**: F5–F8 equip preset 1–4 in the lobby, Shift+F5–F8 save current loadout.
- **TerminalPlus**: Linux-style terminal — aliases (ls, cat, clear, p, q, man…), object shortcuts (`p am 69`), `| grep` / `| grep -v`, `!!` / `!N` / `!prefix`, `history`, Ctrl+L/C/U/V/R, `whoami`, `echo`, `uname`, `uptime`.
- **PreciseHud**: health with decimals and HP, tool charges, teammates' health/infection on the map.
- **HudToggle**: F9 hides the whole HUD for screenshots.
- **ExpeditionStats**: stats on the results screen (time, downs, kills and damage per player when you host, team kills, your packs).
- New `[General] Language` option (en/ru) for in-game text.

## 0.5.0
- First public package. Split from the private GtfoPunkMod build into **GtfoPunk QoL** (this package) and **GtfoPunk My Game My Rules** (later).
- New config file name: `AmDDRed.GtfoPunk.QoL.cfg`.

## 0.4.1 (private)
- RecentlyPlayedWith, ScreenLiquidFilter, NoiseRemoval. Test chat line off by default.

## 0.4.0 (private)
- PackControls, LobbyUnready, ElevatorSkip, ElevatorVolume, BackgroundFpsLimit, SteamProfileLink.
- Fixes: BioTracker, LockerItemPing, WeaponAnim, ScoutScreamMuffle, DoorFoam.

## 0.3.0 (private)
- DmrReloadFix, ReturnToLocker, ResourceAutoMark, PingAnything, PackHelper.

## 0.2.0 (private)
- KeepFlashlight, SprintCancelReload, ResourceReceivedSound, PingDisplay.

## 0.1.0 (private)
- Module framework, conflict detection, SkipIntro.
