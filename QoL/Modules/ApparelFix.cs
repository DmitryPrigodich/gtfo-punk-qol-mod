using System;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Diagnostics for the empty APPAREL menu: logs how many apparel items the menu got after each update.
/// (0.7.5 also filtered the server's list before the game read it — reading that array crashed the game at start.)
/// </summary>
public class ApparelFix : ModuleBase
{
    public override string Id => "ApparelFix";
    public override string Description => "Logs how many apparel items the APPAREL menu got (diagnostics for the empty menu).";
    public override ModuleSide Side => ModuleSide.Client;

    [HarmonyPatch(typeof(VanityItemInventory), nameof(VanityItemInventory.UpdateItems))]
    private static class Patch_Update
    {
        private static void Postfix(VanityItemInventory __instance)
        {
            try { ModInfo.Log.LogInfo($"[ApparelFix] APPAREL menu items: {__instance.m_allItems?.Count ?? -1}"); } catch { }
        }
    }

    /// <summary>When the apparel popup is filled: how many cards it got for that clothes type.</summary>
    [HarmonyPatch(typeof(CellMenu.CM_PlayerLobbyBar), nameof(CellMenu.CM_PlayerLobbyBar.UpdateClothesSelectionPoup))]
    private static class Patch_Popup
    {
        private static void Postfix(CellMenu.CM_PlayerLobbyBar __instance, ClothesType type)
        {
            try
            {
                int ofType = 0;
                var inv = PersistentInventoryManager.Current?.m_vanityItemsInventory?.m_allItems;
                if (inv != null) foreach (var it in inv) if (it != null && it.type == type) ofType++;
                var win = __instance.m_popupScrollWindow;
                int cards = win != null ? win.GetComponentsInChildren<CellMenu.CM_Item>(true).Length : -1;
                ModInfo.Log.LogInfo($"[ApparelFix] apparel popup {type}: inventory has {ofType}, popup items {cards}, visible {__instance.m_popupVisible}");
            }
            catch (Exception e) { ModInfo.Log.LogWarning($"[ApparelFix] popup: {e.Message}"); }
        }
    }
}
