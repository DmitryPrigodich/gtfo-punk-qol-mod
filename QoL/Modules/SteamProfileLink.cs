using System.Collections.Generic;
using CellMenu;
using GtfoPunk.Core;
using HarmonyLib;
using SNetwork;
using Steamworks;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>В лобби клик по нику игрока открывает его профиль Steam (в оверлее Steam, если он включён).</summary>
public class SteamProfileLink : ModuleBase
{
    public override string Id => "SteamProfileLink";
    public override string Description => "Клик по нику игрока в лобби открывает его профиль Steam.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    // Коллайдеры, которые мы повесили на ники: по ним определяем попадание курсора.
    private static readonly Dictionary<int, CM_PlayerLobbyBar> _colliders = new();

    protected override void OnEnable() => FrameTicker.OnUpdate += SyncColliders;

    private static float _next;

    /// <summary>Держим коллайдер ника по размеру текста (ник меняется, плюс к нему дописывается пинг).</summary>
    private static void SyncColliders()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.5f;
        var bars = CM_PlayerLobbyBar.instances;
        if (bars == null) return;
        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            var text = bar?.m_nickText;
            if (text == null) continue;
            var col = text.gameObject.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = text.gameObject.AddComponent<BoxCollider2D>();
                _colliders[col.GetInstanceID()] = bar;
            }
            float w = text.GetRenderedWidth(true), h = text.GetRenderedHeight(true);
            col.size = new Vector2(w, h);
            col.offset = new Vector2(w / 2f, 0f);
        }
    }

    [HarmonyPatch(typeof(CM_PageBase), nameof(CM_PageBase.UpdateButtonPress))]
    private static class Patch_ButtonPress
    {
        private static void Postfix(CM_PageBase __instance)
        {
            if (!Input.GetMouseButtonUp(0)) return;
            if (__instance.TryCast<CM_PageLoadout>() == null) return;
            var canvas = __instance.m_guiLayer?.GuiLayerBase?.m_cellUICanvas;
            if (canvas == null || __instance.m_cursor == null) return;
            if (!canvas.Raycast(__instance.m_cursor.WorldPos, out RaycastHit2D hit) || hit.collider == null) return;
            if (!_colliders.TryGetValue(hit.collider.GetInstanceID(), out var bar) || bar == null) return;

            SNet_Player player = bar.m_player;
            if (player == null || player.IsBot) return;
            string url = $"https://steamcommunity.com/profiles/{player.Lookup}";
            ModInfo.Log.LogInfo($"[SteamProfileLink] {player.NickName}: {url}");
            if (SteamUtils.IsOverlayEnabled()) SteamFriends.ActivateGameOverlayToWebPage(url);
            else Application.OpenURL(url);
        }
    }
}
