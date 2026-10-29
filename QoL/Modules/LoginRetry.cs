using System;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Now and then the game hangs on a black screen at start: it asks Steam for a login ticket (PlayFab login) and Steam
/// never answers — the log stops at "PlayFabManager.OnSteamInitialized", the menu never shows, a restart helps.
/// This asks Steam again: no ticket answer for a few seconds after the request → the ticket is requested anew, a few
/// times. Seen in the player's logs in 2024–2026, with and without mods.
/// </summary>
public class LoginRetry : ModuleBase
{
    public override string Id => "LoginRetry";
    public override string Description => "Black screen at start (Steam doesn't answer the login): asks Steam for the login ticket again.";
    public override ModuleSide Side => ModuleSide.Client;

    private static ConfigEntry<float> _wait;
    private static ConfigEntry<int> _tries;

    protected override void OnBindConfig()
    {
        _wait = Bind("WaitSeconds", 6f, "Seconds without Steam's answer before asking again.");
        _tries = Bind("Tries", 5, "How many times to ask again.");
    }

    protected override void OnEnable() => FrameTicker.OnUpdate += Tick;

    private static float _askedAt = -1f;
    private static bool _answered;
    private static int _tried;

    // OnSteamInitialized asks for the ticket itself (the inner call may not pass through a patch), so both start the wait.
    [HarmonyPatch(typeof(PlayFabManager), nameof(PlayFabManager.OnSteamInitialized))]
    private static class Patch_SteamReady
    {
        private static void Postfix() { _askedAt = Time.realtimeSinceStartup; }
    }

    [HarmonyPatch(typeof(PlayFabManager), nameof(PlayFabManager.GetSteamAuthTicket))]
    private static class Patch_Ask
    {
        private static void Postfix() { _askedAt = Time.realtimeSinceStartup; }
    }

    [HarmonyPatch(typeof(PlayFabManager), nameof(PlayFabManager.OnGetAuthSessionTicketResponse))]
    private static class Patch_Answer
    {
        private static void Prefix()
        {
            _answered = true;
            if (_tried > 0) ModInfo.Log.LogInfo($"[LoginRetry] Steam answered after {_tried} more request(s)");
        }
    }

    private static void Tick()
    {
        if (_answered || _askedAt < 0f || _tried >= _tries.Value) return;
        if (Time.realtimeSinceStartup - _askedAt < _wait.Value) return;
        try
        {
            if (PlayFabManager.LoggedIn) { _answered = true; return; }
            var pf = PlayFabManager.Current;
            if (pf == null) return;
            _tried++;
            ModInfo.Log.LogWarning($"[LoginRetry] no answer from Steam for {_wait.Value:0} s — asking for the login ticket again ({_tried}/{_tries.Value})");
            pf.GetSteamAuthTicket(); // the postfix restarts the wait
        }
        catch (Exception e) { _tried = int.MaxValue; ModInfo.Log.LogWarning($"[LoginRetry] {e.Message}"); }
    }
}
