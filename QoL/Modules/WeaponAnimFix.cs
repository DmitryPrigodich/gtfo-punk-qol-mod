using GtfoPunk.Core;
using HarmonyLib;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Баг: если сменить оружие раньше конца анимации (например, перезарядки), она «переезжает»
/// на следующий предмет. Останавливаем последовательность при убирании и при взятии предмета.
/// </summary>
public class WeaponAnimFix : ModuleBase
{
    public override string Id => "WeaponAnimFix";
    public override string Description => "Анимация перезарядки не переносится на другое оружие при быстрой смене.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static bool Local(ItemEquippable item)
    {
        var owner = item.Owner;
        return owner != null && owner.IsLocallyOwned;
    }

    [HarmonyPatch(typeof(ItemEquippable), nameof(ItemEquippable.OnUnWield))]
    private static class Patch_UnWield
    {
        private static void Prefix(ItemEquippable __instance)
        {
            if (Local(__instance)) __instance.StopAnimationSequenceCoroutine();
        }
    }

    [HarmonyPatch(typeof(ItemEquippable), nameof(ItemEquippable.OnWield))]
    private static class Patch_Wield
    {
        private static void Prefix(ItemEquippable __instance)
        {
            if (Local(__instance)) __instance.StopAnimationSequenceCoroutine();
        }

        private static void Postfix(ItemEquippable __instance)
        {
            if (Local(__instance)) __instance.Owner.FPItemHolder?.OnIdleStart();
        }
    }
}
