using System.Globalization;
using Player;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// More precise HUD:
///  - health with tenths and in HP units ("73.4% · 18/25");
///  - for the tool (sentry, mine deployer, foam, mines) — how many charges are left, not just percent;
///  - on the map — teammates' health and infection.
/// </summary>
public class PreciseHud : ModuleBase
{
    public override string Id => "PreciseHud";
    public override string Description => "Exact numbers on the HUD: health with tenths and in HP, tool charges, teammates' health/infection on the map.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { "Andocas.PreciseUI", "PreciseUI" };

    private static ConfigEntry<bool> _health, _tool, _map;

    protected override void OnBindConfig()
    {
        _health = Bind("Health", true, "Health with tenths and in HP units.");
        _tool = Bind("ToolCharges", true, "How many charges the tool has left.");
        _map = Bind("TeamOnMap", true, "Teammates' health and infection on the map screen.");
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [HarmonyPatch(typeof(PUI_LocalPlayerStatus), nameof(PUI_LocalPlayerStatus.UpdateHealth))]
    private static class Patch_Health
    {
        private static void Postfix(PUI_LocalPlayerStatus __instance, float health)
        {
            if (!_health.Value || __instance.m_healthText == null) return;
            string text = (health * 100f).ToString("0.0", Inv) + "%";
            var dmg = GameUtil.LocalAgent?.Damage;
            if (dmg != null && dmg.HealthMax > 0f)
                text += $" <size=75%>{Mathf.CeilToInt(dmg.Health)}/{Mathf.RoundToInt(dmg.HealthMax)}</size>";
            var t = __instance.m_healthText;
            t.text = text;
            t.enableWordWrapping = false;
            t.enableAutoSizing = true;
            t.fontSizeMin = 0f;
            t.fontSizeMax = 22f;
        }
    }

    [HarmonyPatch(typeof(PUI_Inventory), nameof(PUI_Inventory.SetSlotAmmo))]
    private static class Patch_SlotAmmo
    {
        private static void Postfix(PUI_Inventory __instance, InventorySlot slot, int inPackAbs, float inPackRel)
        {
            if (!_tool.Value || slot != InventorySlot.GearClass) return;
            if (__instance.m_owner == null || !__instance.m_owner.IsLocal) return;
            if (!__instance.m_inventorySlots.ContainsKey(slot)) return;
            var ui = __instance.m_inventorySlots[slot];
            string text = $"{inPackAbs} <size=70%>({(inPackRel * 100f).ToString("0", Inv)}%)</size>";
            if (ui.m_selected_ammoTotal != null) ui.m_selected_ammoTotal.text = text;
            if (ui.m_slim_ammoTotal != null) ui.m_slim_ammoTotal.text = text;
        }
    }

    [HarmonyPatch(typeof(PUI_Inventory), nameof(PUI_Inventory.SetHeader))]
    private static class Patch_MapHeader
    {
        private static void Prefix(PUI_Inventory __instance, ref string header)
        {
            if (!_map.Value || __instance.m_owner == null) return;
            var agent = GameUtil.AgentOf(__instance.m_owner);
            if (agent == null || agent.Damage == null) return;
            float hp = Mathf.Clamp01(agent.Damage.GetHealthRel());
            float inf = agent.Damage.Infection;
            header = $"{agent.PlayerName} <color=orange>({(hp * 100f).ToString("0", Inv)}%)</color>";
            if (inf > 0f) header += $"<color=#00FFA8> ({(inf * 100f).ToString("0", Inv)}%)</color>";
        }
    }
}
