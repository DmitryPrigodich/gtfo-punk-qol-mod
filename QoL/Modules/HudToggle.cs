using System;
using GtfoPunk.Core;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Клавиша, которая прячет весь HUD (для скриншотов и видео) и возвращает обратно.
/// Настройки игры не трогает — просто скрывает слои интерфейса до повторного нажатия или выхода из уровня.
/// </summary>
public class HudToggle : ModuleBase
{
    public override string Id => "HudToggle";
    public override string Description => "Клавиша, которая прячет весь HUD для скриншотов (по умолчанию F9).";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { "HUDToggler" };

    private static BepInEx.Configuration.ConfigEntry<KeyCode> _key;
    private static bool _hidden;

    protected override void OnBindConfig()
    {
        _key = Bind("Key", KeyCode.F9, "Клавиша скрытия HUD.");
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
