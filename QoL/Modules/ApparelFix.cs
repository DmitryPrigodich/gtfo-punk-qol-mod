using System;
using System.Linq;
using GameData;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using DsItem = DropServer.VanityItems.VanityItem;
using DsData = DropServer.VanityItems.VanityItemPlayerData;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// APPAREL menu came out empty: the drop server's list had an item the game data doesn't know (the log says
/// "Got vanity item with id … that does not exist in current game data"). Such items are left out before the game
/// builds the list, so the rest shows. Also logs how many apparel items the menu got.
/// </summary>
public class ApparelFix : ModuleBase
{
    public override string Id => "ApparelFix";
    public override string Description => "APPAREL menu isn't empty when the server lists an item unknown to the game data.";
    public override ModuleSide Side => ModuleSide.Client;

    [HarmonyPatch(typeof(VanityItemInventory), nameof(VanityItemInventory.UpdateItems))]
    private static class Patch_Update
    {
        private static void Prefix(DsData data)
        {
            try
            {
                var items = data?.Items;
                if (items == null) { ModInfo.Log.LogInfo("[ApparelFix] server apparel list: empty"); return; }
                var keep = new List<DsItem>();
                var dropped = new List<uint>();
                for (int i = 0; i < items.Length; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    if (GameDataBlockBase<VanityItemsTemplateDataBlock>.HasBlock(it.ItemId)) keep.Add(it);
                    else dropped.Add(it.ItemId);
                }
                if (dropped.Count > 0) data.Items = new Il2CppReferenceArray<DsItem>(keep.ToArray());
                ModInfo.Log.LogInfo($"[ApparelFix] server apparel list: {items.Length} items"
                    + (dropped.Count > 0 ? $", left out unknown to the game data: {string.Join(", ", dropped)}" : ""));
            }
            catch (Exception e) { ModInfo.Log.LogWarning($"[ApparelFix] {e.Message}"); }
        }

        private static void Postfix(VanityItemInventory __instance)
        {
            try { ModInfo.Log.LogInfo($"[ApparelFix] APPAREL menu items: {__instance.m_allItems?.Count ?? -1}"); } catch { }
        }
    }
}
