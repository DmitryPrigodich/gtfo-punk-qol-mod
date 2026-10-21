using GtfoPunk.Core;
using HarmonyLib;
using SNetwork;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Пробный модуль: при высадке в уровень пишет в локальный чат статус мода.
/// Нужен, чтобы убедиться, что патчи и игровое API работают. Сообщение видишь только ты.
/// </summary>
public class StatusInChat : ModuleBase
{
    public override string Id => "StatusInChat";
    public override string Description => "Пишет в твой чат статус мода при высадке в уровень (видишь только ты).";
    public override ModuleSide Side => ModuleSide.Client;
    public override bool EnabledByDefault => false; // тестовый модуль, своё отработал

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.DoChangeState))]
    private static class Patch_DoChangeState
    {
        private static void Postfix(eGameStateName nextState)
        {
            if (nextState != eGameStateName.InLevel || !SNet.HasLocalPlayer) return;
            PlayerChatManager.PostChatMessageLocaly(SNet.LocalPlayer,
                $"<color=#ff4fd8>[{ModInfo.Name} {ModInfo.Version}]</color> {ModuleManager.ShortStatus()}");
        }
    }
}
