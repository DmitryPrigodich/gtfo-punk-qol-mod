using System;
using GameData;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Every apparel item (vanity: helmets, torsos, legs, backpacks, palettes) is available in the APPAREL menu, as if
/// already dropped. Cosmetic and local: it only adds the items to your inventory list; what you wear is shown to others
/// as usual (they have the same models in their game).
/// </summary>
public class AllApparel : ModuleBase
{
    public override string Id => "AllApparel";
    public override string Description => "All apparel (helmets, torsos, legs, backpacks, palettes) available in the APPAREL menu.";
    public override ModuleSide Side => ModuleSide.Client;

    protected override void OnEnable() => FrameTicker.OnUpdate += Tick;

    private static float _nextCheck;

    // The inventory is rebuilt from the drop server's data — add ours after each update.
    [HarmonyPatch(typeof(VanityItemInventory), nameof(VanityItemInventory.UpdateItems))]
    private static class Patch_Update
    {
        private static void Postfix(VanityItemInventory __instance) { try { AddAll(__instance); } catch (Exception e) { ModInfo.Log.LogWarning($"[AllApparel] {e.Message}"); } }
    }

    /// <summary>Also once in a while in the menus, in case the inventory was built before we were ready.</summary>
    private static void Tick()
    {
        if (Time() < _nextCheck || GameUtil.InLevel) return;
        _nextCheck = Time() + 5f;
        try
        {
            var inv = PersistentInventoryManager.Current?.m_vanityItemsInventory;
            if (inv != null) AddAll(inv);
        }
        catch { }
    }

    private static float Time() => UnityEngine.Time.realtimeSinceStartup;

    private static int _lastAdded = -1;

    private static void AddAll(VanityItemInventory inv)
    {
        if (inv.m_allItems == null) return;
        var have = new HashSet<uint>();
        foreach (var it in inv.m_allItems) if (it != null) have.Add(it.id);
        int added = 0;
        foreach (var t in GameDataBlockBase<VanityItemsTemplateDataBlock>.GetAllBlocks())
        {
            if (t == null || !t.internalEnabled || have.Contains(t.persistentID)) continue;
            var v = new VanityItem { publicName = t.publicName, type = t.type, prefab = t.prefab, id = t.persistentID, flags = VanityItemFlags.Touched | VanityItemFlags.Acknowledged };
            inv.m_allItems.Add(v);
            inv.m_backednItems?.Add(v);
            added++;
        }
        if (added > 0 || _lastAdded < 0)
        {
            _lastAdded = added;
            ModInfo.Log.LogInfo($"[AllApparel] apparel available: {inv.m_allItems.Count} (added {added})");
        }
    }
}
