using System;
using GameData;
using Gear;
using Player;
using SNetwork;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// A bot's saved gear may point at a weapon or tool that no longer exists — e.g. a sentry or the silenced pistol of
/// GtfoPunk My Game My Rules after that mod is removed. The game then fails to build it: the bot keeps resetting and
/// carries an empty slot. Such gear is swapped for stock gear of the same slot (the burst sentry for a tool).
/// </summary>
public class BotGearGuard : ModuleBase
{
    public override string Id => "BotGearGuard";
    public override string Description => "Bots whose saved gear no longer exists (a removed mod's weapon or sentry) get stock gear instead of an empty slot.";
    public override ModuleSide Side => ModuleSide.Client;

    /// <summary>Slots of the gear the Rules mod adds (category IDs never change).</summary>
    private static InventorySlot SlotOfMissing(uint cat) => cat switch
    {
        4200 => InventorySlot.GearSpecial,                // DRED SILENZIO (silenced pistol)
        >= 4201 and <= 4208 => InventorySlot.GearClass,   // DRED GARAGE sentries
        _ => InventorySlot.None,
    };

    [HarmonyPatch(typeof(PlayerBackpackManager), nameof(PlayerBackpackManager.EquipBotGear), new[] { typeof(SNet_Player), typeof(GearIDRange) })]
    private static class Patch_Equip
    {
        private static void Prefix(ref GearIDRange gearSetup)
        {
            try
            {
                if (gearSetup == null) return;
                uint cat = gearSetup.GetCompID(eGearComponent.Category);
                if (cat == 0 || GameDataBlockBase<GearCategoryDataBlock>.HasBlock(cat)) return;
                var slot = SlotOfMissing(cat);
                var stock = slot == InventorySlot.None ? null : FindStock(slot);
                if (stock == null) { ModInfo.Log.LogWarning($"[BotGearGuard] bot gear with missing category {cat}: no replacement found"); return; }
                ModInfo.Log.LogWarning($"[BotGearGuard] bot gear with missing category {cat} ({slot}) replaced by '{stock.name}'");
                gearSetup = new GearIDRange(stock.GearJSON);
            }
            catch (Exception e) { ModInfo.Log.LogWarning($"[BotGearGuard] {e.Message}"); }
        }
    }

    /// <summary>Stock gear for the slot: the burst sentry for tools, otherwise the first gear of that slot.</summary>
    private static PlayerOfflineGearDataBlock FindStock(InventorySlot slot)
    {
        PlayerOfflineGearDataBlock first = null;
        foreach (var b in GameDataBlockBase<PlayerOfflineGearDataBlock>.GetAllBlocks())
        {
            if (b == null || string.IsNullOrEmpty(b.GearJSON) || b.persistentID >= 4200 && b.persistentID < 10000) continue;
            try
            {
                var range = new GearIDRange(b.GearJSON);
                uint cat = range.GetCompID(eGearComponent.Category);
                var c = GameDataBlockBase<GearCategoryDataBlock>.GetBlock(cat);
                var item = c == null ? null : GameDataBlockBase<ItemDataBlock>.GetBlock(c.BaseItem);
                if (item == null || item.inventorySlot != slot) continue;
                if (slot == InventorySlot.GearClass && cat == 12 && (b.name ?? "").IndexOf("Burst", StringComparison.OrdinalIgnoreCase) >= 0) return b;
                first ??= b;
            }
            catch { }
        }
        return first;
    }
}
