
namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Bug: switching weapons before an animation (e.g. reload) ends makes it "move over"
/// to the next item. Stop the sequence when holstering and when drawing an item.
/// </summary>
public class WeaponAnimFix : ModuleBase
{
    public override string Id => "WeaponAnimFix";
    public override string Description => "The reload animation doesn't carry over to another weapon on a quick switch.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static bool Local(ItemEquippable item)
    {
        var owner = item.Owner;
        return GameUtil.IsLocalHuman(owner);
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
