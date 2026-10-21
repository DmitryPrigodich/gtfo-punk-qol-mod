using AK;
using CellMenu;
using GtfoPunk.Core;
using HarmonyLib;
using SNetwork;

namespace GtfoPunk.QoL.Modules;

/// <summary>The lobby "Ready" button stays active after pressing — you can change your mind and unready.</summary>
public class LobbyUnready : ModuleBase
{
    public override string Id => "LobbyUnready";
    public override string Description => "You can unready in the lobby if you pressed Ready by accident (not for the host).";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    [HarmonyPatch(typeof(CM_PageLoadout), nameof(CM_PageLoadout.UpdateReadyState))]
    private static class Patch_UpdateReadyState
    {
        private static void Postfix(CM_PageLoadout __instance)
        {
            var btn = __instance.m_readyButton;
            if (btn == null) return;
            var mm = PlayfabMatchmakingManager.Current;
            if (mm != null && mm.IsMatchmakeInProgress) return;
            if (SNet.IsMaster || !GameStateManager.IsReady) return;

            btn.SetText("UNREADY");
            btn.SOUND_CLICK_HOLD_START = EVENTS.MENU_READY_PRESS_AND_HOLD;
            btn.SOUND_CLICK_HOLD_CANCEL = EVENTS.MENU_READY_PRESS_AND_HOLD_CANCEL;
            btn.SetButtonEnabled(true);
        }
    }
}
