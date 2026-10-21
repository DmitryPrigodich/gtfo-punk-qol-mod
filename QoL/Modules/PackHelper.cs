using GtfoPunk.Core;
using HarmonyLib;
using Player;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Пока у тебя в руках пачка ресурсов, над всеми тиммейтами показывается их состояние
/// (здоровье и патроны — штатная «доп. информация» метки игрока), а не только когда смотришь на них.
/// Так сразу видно, кому нужнее.
/// </summary>
public class PackHelper : ModuleBase
{
    public override string Id => "PackHelper";
    public override string Description => "Пока держишь пачку ресурсов, над тиммейтами видно их здоровье и патроны.";
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

    // Пока держим пачку, игра не должна прятать доп. информацию тиммейтов.
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
