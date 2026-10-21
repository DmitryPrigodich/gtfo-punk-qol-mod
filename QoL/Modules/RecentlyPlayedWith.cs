using GtfoPunk.Core;
using HarmonyLib;
using SNetwork;
using Steamworks;

namespace GtfoPunk.QoL.Modules;

/// <summary>Lobby players get added to Steam's "Recently played with" list — easier to find and befriend them later.</summary>
public class RecentlyPlayedWith : ModuleBase
{
    public override string Id => "RecentlyPlayedWith";
    public override string Description => "Add lobby players to Steam's 'Recently played with' list.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static void Mark(CSteamID id)
    {
        if (SNet.HasLocalPlayer && id.m_SteamID == SNet.LocalPlayer.Lookup) return;
        SteamFriends.SetPlayedWith(id);
    }

    [HarmonyPatch(typeof(SNet_Lobby_STEAM), nameof(SNet_Lobby_STEAM.PlayerJoined), new[] { typeof(SNet_Player), typeof(CSteamID) })]
    private static class Patch_Joined
    {
        private static void Postfix(CSteamID steamID) => Mark(steamID);
    }

    [HarmonyPatch(typeof(SNet_Lobby_STEAM), nameof(SNet_Lobby_STEAM.PlayerLeft), new[] { typeof(SNet_Player), typeof(CSteamID) })]
    private static class Patch_Left
    {
        private static void Postfix(CSteamID steamID) => Mark(steamID);
    }
}
