using GtfoPunk.Core;
using HarmonyLib;
using Player;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// While you hold a resource pack, every teammate shows their status
/// (health and ammo — the stock "extra info" of the player marker), not only when you look at them.
/// So you see right away who needs it more.
/// </summary>
public class PackHelper : ModuleBase
{
    public override string Id => "PackHelper";
    public override string Description => "While you hold a resource pack, teammates' health and ammo are shown above them.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.PacksHelper };

    private static bool _holdingPack;
    private static float _next;

    protected override void OnEnable()
    {
        FrameTicker.OnUpdate += Tick;
        FrameTicker.OnLevelExit += () => _holdingPack = false;
    }

    private static void Tick()
    {
        if (Time.time < _next) return;
        _next = Time.time + 0.15f;

        bool holding = GameUtil.InLevel && GameUtil.LocalAgent?.Inventory?.WieldedSlot == InventorySlot.ResourcePack;
        if (holding == _holdingPack && !holding) return;

        bool changed = holding != _holdingPack;
        _holdingPack = holding;
        var players = PlayerManager.PlayerAgentsInLevel;
        if (players == null) return;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null || p.IsLocallyOwned || p.NavMarker == null) continue;
            if (holding && !p.NavMarker.ExtraInfoVisible) p.NavMarker.SetExtraInfoVisible(true);
            else if (!holding && changed) p.NavMarker.SetExtraInfoVisible(false);
        }
    }

    // While we hold a pack, the game must not hide teammates' extra info.
    [HarmonyPatch(typeof(PlaceNavMarkerOnGO), nameof(PlaceNavMarkerOnGO.SetExtraInfoVisible))]
    private static class Patch_SetExtraInfoVisible
    {
        private static bool Prefix(PlaceNavMarkerOnGO __instance, bool visible)
        {
            if (!_holdingPack || visible) return true;
            var owner = __instance.m_player;
            return owner == null || owner.IsLocallyOwned;
        }
    }
}
