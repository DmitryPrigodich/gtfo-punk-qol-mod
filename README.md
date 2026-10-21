# GtfoPunk QoL

Удобства и багфиксы для GTFO. Только у себя: хост не нужен, баланс не меняется, можно играть с рандомами.

Плагин для [BepInEx 6 IL2CPP](https://thunderstore.io/c/gtfo/p/BepInEx/BepInExPack_GTFO/) (GTFO). Описание фич для игроков: [packaging/QoL/README.md](packaging/QoL/README.md), история версий: [packaging/QoL/CHANGELOG.md](packaging/QoL/CHANGELOG.md).

## Установка (игрокам)
Zip из релизов → r2modman → *Settings → Import local mod*. Нужен BepInExPack_GTFO.

## Сборка
- .NET SDK 6+ (цель `net6.0`).
- Папка `lib/` со ссылками на игру (в репозиторий не входит — это файлы игры и BepInEx). Скопировать из профиля r2modman:
  - `BepInEx/core/*.dll` (0Harmony, BepInEx.Core, BepInEx.Unity.IL2CPP, BepInEx.Unity.Common, BepInEx.Preloader.Core, Il2CppInterop.*)
  - `BepInEx/interop/*.dll` (Modules-ASM, GameData-ASM, SNet_ASM, Il2Cppmscorlib, Il2CppSystem*, UnityEngine.*Module, Unity.TextMeshPro, Shards-ASM, CellSound-ASM, …)
  - `interop` появляется после первого запуска игры с BepInEx.
- `./build.sh` → `dist/GtfoPunk_QoL-<версия>.zip` (готовый пакет для r2modman/Thunderstore). Другой путь к библиотекам: `dotnet build QoL/GtfoPunk.QoL.csproj -c Release -p:GameLibs=/путь/к/lib`.

## Устройство
- `Shared/Core` — общее ядро GtfoPunk (одинаковое в обоих репозиториях): модули с отдельными переключателями в конфиге, пометка Client/Host/Everyone, автоотключение модуля, если стоит мод с той же функцией, язык en/ru.
- `QoL/Modules` — по файлу на фичу.
- `packaging/QoL` — manifest, README, CHANGELOG, иконка пакета.

Связанный репозиторий: gtfo-punk-rules-mod (геймплейные изменения).
