# Changelog

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
