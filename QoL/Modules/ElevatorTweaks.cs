using AK;
using GtfoPunk.Core;
using HarmonyLib;

namespace GtfoPunk.QoL.Modules;

/// <summary>Skip the elevator drop-in scene (seats, unbuckling) — you are in the level right away.</summary>
public class ElevatorSkip : ModuleBase
{
    public override string Id => "ElevatorSkip";
    public override string Description => "Skip the elevator drop-in scene (seats, unbuckling).";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    [HarmonyPatch(typeof(ElevatorRide), nameof(ElevatorRide.StartPreReleaseSequence))]
    private static class Patch_PreRelease
    {
        private static void Postfix() => ElevatorRide.SkipPreReleaseSequence();
    }
}

/// <summary>Sound effects volume during the elevator ride (from level generation to drop-in).</summary>
public class ElevatorVolume : ModuleBase
{
    public override string Id => "ElevatorVolume";
    public override string Description => "Quieter sounds during the elevator ride. Volume is set below.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static BepInEx.Configuration.ConfigEntry<float> _volume;
    private static bool _focused = true;
    private static bool _applied;

    protected override void OnBindConfig()
    {
        _volume = Bind("Volume", 0.5f, "Effects volume in the elevator: 1 = as in settings, 0.5 = half, 0 = silence.");
    }

    private static bool InElevator(eGameStateName s) =>
        s is eGameStateName.Generating or eGameStateName.ReadyToStopElevatorRide
          or eGameStateName.StopElevatorRide or eGameStateName.ReadyToStartLevel;

    private static void Apply(eGameStateName state)
    {
        if (!_focused) return; // the game handles a minimized window itself (mute in background)
        if (InElevator(state))
        {
            float sfx = CellSettingsManager.SettingsData.Audio.SFXVolume.Value;
            float basePercent = sfx <= 1.0001f ? sfx * 100f : sfx; // the setting may be stored as 0..1 or 0..100
            CellSound.SetGlobalRTPCValue(GAME_PARAMETERS.VOLUME_SETTING_SFX, basePercent * UnityEngine.Mathf.Clamp01(_volume.Value));
            _applied = true;
        }
        else if (_applied)
        {
            CellSettingsManager.SettingsData.Audio.ApplyAllValues();
            _applied = false;
        }
    }

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.ChangeState))]
    private static class Patch_ChangeState
    {
        private static void Postfix(eGameStateName nextState) => Apply(nextState);
    }

    [HarmonyPatch(typeof(CellSettingsManager), nameof(CellSettingsManager.OnApplicationFocus))]
    private static class Patch_Focus
    {
        private static void Postfix(bool focus)
        {
            _focused = focus;
            if (focus) Apply(GameStateManager.CurrentStateName);
        }
    }
}
