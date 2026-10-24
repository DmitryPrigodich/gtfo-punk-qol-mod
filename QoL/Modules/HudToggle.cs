using System;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// A key that hides the whole HUD (for screenshots and videos) and brings it back.
/// Doesn't touch game settings — just hides the UI layers until pressed again or the level is left.
/// </summary>
public class HudToggle : ModuleBase
{
    public override string Id => "HudToggle";
    public override string Description => "A key that hides the whole HUD for screenshots (F9 by default).";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { "HUDToggler" };

    private static ConfigEntry<KeyCode> _key;
    private static bool _hidden;

    protected override void OnBindConfig()
    {
        _key = Bind("Key", KeyCode.F9, "HUD toggle key.");
    }

    protected override void OnEnable()
    {
        FrameTicker.OnUpdate += Tick;
        FrameTicker.OnLevelExit += () => { if (_hidden) Apply(false); };
    }

    private static void Tick()
    {
        if (!GameUtil.InLevel || PlayerChatManager.InChatMode) return;
        if (FocusStateManager.CurrentState != eFocusState.FPS) return;
        if (Input.GetKeyDown(_key.Value)) Apply(!_hidden);
    }

    private static void Apply(bool hide)
    {
        _hidden = hide;
        Set(GuiManager.PlayerLayer, !hide);
        Set(GuiManager.CrosshairLayer, !hide);
        Set(GuiManager.NavMarkerLayer, !hide);
        Set(GuiManager.InteractionLayer, !hide);
        Set(GuiManager.WatermarkLayer, !hide);
    }

    private static void Set(GuiLayer layer, bool visible)
    {
        try { layer?.SetVisible(visible); }
        catch (Exception e) { ModInfo.Log.LogWarning($"[HudToggle] {layer?.GetType().Name}: {e.Message}"); }
    }
}
