using GtfoPunk.Core;
using HarmonyLib;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>Когда игра не в фокусе (alt-tab), FPS ограничивается, чтобы видеокарта не грелась впустую.</summary>
public class BackgroundFpsLimit : ModuleBase
{
    public override string Id => "BackgroundFpsLimit";
    public override string Description => "Ограничивать FPS, когда игра свёрнута/не в фокусе. Не работает при включённом V-Sync.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static BepInEx.Configuration.ConfigEntry<int> _fps;
    private static bool _warned;

    protected override void OnBindConfig()
    {
        _fps = Bind("MaxFps", 30, "Максимальный FPS, когда игра не в фокусе.");
    }

    [HarmonyPatch(typeof(CellSettingsManager), nameof(CellSettingsManager.OnApplicationFocus))]
    private static class Patch_Focus
    {
        private static void Postfix(bool focus)
        {
            if (QualitySettings.vSyncCount != 0 && !_warned)
            {
                ModInfo.Log.LogWarning("[BackgroundFpsLimit] Включён V-Sync — ограничение FPS не подействует.");
                _warned = true;
            }

            var target = CellSettingsManager.SettingsData.Video.TargetFramerate;
            if (!focus)
            {
                int user = target.Value;
                Application.targetFrameRate = Mathf.Clamp(_fps.Value, 1, user < 0 ? 999 : user);
            }
            else
            {
                target.ApplyValue();
            }
        }
    }
}
