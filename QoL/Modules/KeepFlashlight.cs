using GtfoPunk.Core;
using HarmonyLib;
using Player;
using SNetwork;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Баг игры: если выбросить предмет или поставить его (турель, мина и т.п.), фонарик выключается.
/// Запоминаем состояние до действия и восстанавливаем после.
/// </summary>
public class KeepFlashlight : ModuleBase
{
    public override string Id => "KeepFlashlight";
    public override string Description => "Фонарик не выключается, когда выбрасываешь или ставишь предмет.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static bool? _saved;

    private static void Save(SNet_Player player)
    {
        _saved = null;
        if (!GameUtil.IsLocal(player)) return;
        var agent = GameUtil.AgentOf(player);
        if (agent?.Inventory != null) _saved = agent.Inventory.FlashlightEnabled;
    }

    private static void Restore(SNet_Player player)
    {
        if (_saved == null || !GameUtil.IsLocal(player)) return;
        var inv = GameUtil.AgentOf(player)?.Inventory;
        if (inv != null && inv.FlashlightEnabled != _saved.Value)
            inv.ReceiveSetFlashlightStatus(_saved.Value, false);
        _saved = null;
    }

    [HarmonyPatch(typeof(PlayerBackpackManager), nameof(PlayerBackpackManager.RemoveItem),
        new[] { typeof(SNet_Player), typeof(pItemData) })]
    private static class Patch_RemoveItem
    {
        private static void Prefix(SNet_Player fromPlayer) => Save(fromPlayer);
        private static void Postfix(SNet_Player fromPlayer) => Restore(fromPlayer);
    }

    [HarmonyPatch(typeof(PlayerBackpack), nameof(PlayerBackpack.SetDeployed))]
    private static class Patch_SetDeployed
    {
        private static void Prefix(PlayerBackpack __instance) => Save(__instance.Owner);
        private static void Postfix(PlayerBackpack __instance) => Restore(__instance.Owner);
    }
}
