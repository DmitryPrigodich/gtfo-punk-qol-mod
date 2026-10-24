using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>When the game is out of focus (alt-tab), FPS is limited so the GPU doesn't heat up for nothing.</summary>
public class BackgroundFpsLimit : ModuleBase
{
    public override string Id => "BackgroundFpsLimit";
    public override string Description => "Limit FPS when the game is minimized/out of focus. Doesn't work with V-Sync on.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static ConfigEntry<int> _fps;
    private static bool _warned;

    protected override void OnBindConfig()
    {
        _fps = Bind("MaxFps", 30, "Max FPS when the game is out of focus.");
    }

    [HarmonyPatch(typeof(CellSettingsManager), nameof(CellSettingsManager.OnApplicationFocus))]
    private static class Patch_Focus
    {
        private static void Postfix(bool focus)
        {
            if (QualitySettings.vSyncCount != 0 && !_warned)
            {
                ModInfo.Log.LogWarning("[BackgroundFpsLimit] V-Sync is on — the FPS limit won't apply.");
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
