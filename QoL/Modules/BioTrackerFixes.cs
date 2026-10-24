using Enemies;
using Gear;
using Il2CppInterop.Runtime.InteropTypes;
using Player;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Two bio-tracker bugs:
///  1) a player with a bio-tracker "needs" tool refill, and a teammate wastes a tool refill on them;
///  2) after several scans enemy markers stay stuck on the screen.
/// </summary>
public class BioTrackerFixes : ModuleBase
{
    public override string Id => "BioTrackerFixes";
    public override string Description => "The bio-tracker doesn't eat tool refills, and old scanner markers don't stay on screen.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static bool HasBioTracker(PlayerAgent agent)
    {
        if (agent == null || !PlayerBackpackManager.TryGetBackpack(agent.Owner, out PlayerBackpack bp) || bp == null) return false;
        if (!bp.TryGetBackpackItem(InventorySlot.GearClass, out BackpackItem item) || item == null) return false;
        var inst = item.Instance;
        if (inst != null && ((Il2CppObjectBase)inst).TryCast<EnemyScanner>() != null) return true;
        return item.Name == "Enemy Scanner";
    }

    [HarmonyPatch(typeof(PlayerAgent), nameof(PlayerAgent.NeedToolAmmo))]
    private static class Patch_NeedToolAmmo
    {
        private static bool Prefix(PlayerAgent __instance, ref bool __result)
        {
            if (!HasBioTracker(__instance)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(EnemyAgent), nameof(EnemyAgent.SyncPlaceNavMarkerTag))]
    private static class Patch_Tag
    {
        private static void Prefix(EnemyAgent __instance) => __instance.RemoveNavMarker();
    }
}
