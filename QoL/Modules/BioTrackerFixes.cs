using Enemies;
using Gear;
using GtfoPunk.Core;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Player;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Два бага биотрекера:
///  1) игрок с биотрекером «нуждается» в пополнении инструментов, и тиммейт тратит на него тул-рефилл впустую;
///  2) после нескольких сканов метки врагов остаются висеть на экране.
/// </summary>
public class BioTrackerFixes : ModuleBase
{
    public override string Id => "BioTrackerFixes";
    public override string Description => "Биотрекер не съедает тул-рефилл, и старые метки сканера не висят на экране.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

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
