using CellMenu;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// The lobby "Ready" button reacts to a single click instead of press-and-hold.
/// The host's drop button can be made instant too (off by default — it starts the expedition for everyone).
/// Purely local UI: other players see the normal ready state.
/// </summary>
public class InstantReady : ModuleBase
{
    public override string Id => "InstantReady";
    public override string Description => "Lobby Ready on a single click instead of holding the mouse button.";
    public override ModuleSide Side => ModuleSide.Client;

    private static ConfigEntry<bool> _drop;

    protected override void OnBindConfig()
    {
        _drop = Bind("InstantDrop", false, "Also make the host's drop/start button instant (it starts the expedition for everyone, so it's off by default).");
    }

    private static void MakeInstant(CM_TimedButton btn)
    {
        if (btn == null || btn.m_clickType == eTimedButtonClickType.Instant) return;
        btn.m_clickType = eTimedButtonClickType.Instant;
        btn.m_holdButtonDuration = 0f;
    }

    [HarmonyPatch(typeof(CM_PageLoadout), nameof(CM_PageLoadout.UpdateReadyState))]
    private static class Patch_UpdateReadyState
    {
        private static void Postfix(CM_PageLoadout __instance)
        {
            MakeInstant(__instance.m_readyButton);
            if (_drop.Value) MakeInstant(__instance.m_dropButton);
        }
    }
}
