using System;
using System.Collections.Generic;
using CellMenu;
using GtfoPunk.Core;
using HarmonyLib;
using SNetwork;
using Steamworks;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Shows ping:
///  - in the lobby next to each player's name;
///  - in a mission next to the watermark in the screen corner (your ping to the host).
/// Ping comes from the real Steam connection, or from the game's estimate if there is none.
/// </summary>
public class PingDisplay : ModuleBase
{
    public override string Id => "PingDisplay";
    public override string Description => "Players' ping in the lobby and your ping to the host in the screen corner during a mission.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix, "ModList" };

    private static BepInEx.Configuration.ConfigEntry<bool> _inLobby;
    private static BepInEx.Configuration.ConfigEntry<bool> _inHud;
    private static BepInEx.Configuration.ConfigEntry<float> _interval;

    protected override void OnBindConfig()
    {
        _inLobby = Bind("ShowInLobby", true, "Show ping next to names in the lobby.");
        _inHud = Bind("ShowInHud", true, "Show your ping to the host in the screen corner during a mission.");
        _interval = Bind("UpdateInterval", 0.5f, "How often to update, in seconds.");
    }

    private const int Host = -1, Unknown = -2;

    // ---------- getting the ping ----------

    private static bool _steamApiBroken;

    /// <summary>RTT to the player in ms; Host = they are the host (or we are the host); Unknown = we don't know.</summary>
    private static int GetPing(SNet_Player player)
    {
        if (player == null) return Unknown;
        if (player.IsMaster) return Host;
        if (player.IsLocal)
        {
            // Our ping only makes sense to the host.
            if (!SNet.HasMaster || SNet.Master == null) return Unknown;
            if (SNet.Master.IsLocal) return Host;
            return GetPing(SNet.Master);
        }
        if (player.IsBot) return Unknown;

        // We're a client and this is another client: no direct connection, nothing to measure.
        if (!SNet.IsMaster && !player.IsMaster) return Unknown;

        int rtt = SteamRtt(player.Lookup);
        if (rtt > 0) return rtt;
        return player.Ping > 0 ? player.Ping : Unknown;
    }

    private static int SteamRtt(ulong steamId)
    {
        if (_steamApiBroken) return -1;
        try
        {
            var id = new SteamNetworkingIdentity();
            id.SetSteamID64(steamId);
            var state = SteamNetworkingMessages.GetSessionConnectionInfo(ref id, out _, out var status);
            if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected && status != null)
                return status.m_nPing;
        }
        catch (Exception e)
        {
            _steamApiBroken = true; // log once and live on the game's estimate from then on
            ModInfo.Log.LogWarning($"[PingDisplay] Steam API unavailable, using the game's estimate: {e.Message}");
        }
        return -1;
    }

    private static string Format(int ping) => ping switch
    {
        Host => "<color=#9fd0ff>HOST</color>",
        Unknown => "",
        < 80 => $"<color=#7dff7d>{ping} ms</color>",
        < 150 => $"<color=#ffd75e>{ping} ms</color>",
        _ => $"<color=#ff6b6b>{ping} ms</color>",
    };

    // ---------- drawing ----------

    private static float _nextUpdate;
    private static PUI_Watermark _watermark;
    private static string _watermarkBase;

    protected override void OnEnable() => FrameTicker.OnUpdate += Tick;

    private static void Tick()
    {
        if (Time.unscaledTime < _nextUpdate) return;
        _nextUpdate = Time.unscaledTime + Mathf.Max(0.1f, _interval.Value);
        if (_inLobby.Value) UpdateLobby();
        if (_inHud.Value) UpdateHud();
    }

    private static void UpdateLobby()
    {
        var bars = CM_PlayerLobbyBar.instances;
        if (bars == null) return;
        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            if (bar == null || bar.m_nickText == null) continue;
            var player = bar.m_player;
            if (player == null || !bar.HasPlayer()) continue;

            string ping = Format(GetPing(player));
            string text = string.IsNullOrEmpty(ping)
                ? player.NickName
                : $"{player.NickName} <size=70%>{ping}</size>";
            if (bar.m_nickText.text != text) bar.m_nickText.SetText(text);
        }
    }

    private static void UpdateHud()
    {
        if (_watermark == null || _watermark.m_watermarkText == null) return;
        bool show = GameUtil.InLevel && SNet.HasMaster;
        string ping = show ? Format(GetPing(SNet.LocalPlayer)) : "";
        string text = string.IsNullOrEmpty(ping) ? _watermarkBase : $"{_watermarkBase}  <size=90%>PING {ping}</size>";
        if (text != null && _watermark.m_watermarkText.text != text)
            _watermark.m_watermarkText.SetText(text);
    }

    // Remember the "clean" watermark text every time the game rewrites it.
    [HarmonyPatch(typeof(PUI_Watermark), nameof(PUI_Watermark.UpdateWatermark))]
    private static class Patch_Watermark
    {
        private static void Postfix(PUI_Watermark __instance)
        {
            _watermark = __instance;
            _watermarkBase = __instance.m_watermarkText != null ? __instance.m_watermarkText.text : null;
        }
    }
}
