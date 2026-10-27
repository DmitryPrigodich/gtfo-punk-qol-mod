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
}
