using AK;
using GtfoPunk.Core;
using HarmonyLib;

namespace GtfoPunk.QoL.Modules;

/// <summary>Пропуск сцены высадки из лифта. По умолчанию ВЫКЛЮЧЕН — это часть атмосферы.</summary>
public class ElevatorSkip : ModuleBase
{
    public override string Id => "ElevatorSkip";
    public override string Description => "Пропускать сцену высадки из лифта (сидения, отстёгивание). По умолчанию выключено.";
    public override ModuleSide Side => ModuleSide.Client;
    public override bool EnabledByDefault => false;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    [HarmonyPatch(typeof(ElevatorRide), nameof(ElevatorRide.StartPreReleaseSequence))]
    private static class Patch_PreRelease
    {
        private static void Postfix() => ElevatorRide.SkipPreReleaseSequence();
    }
}

/// <summary>Громкость звуковых эффектов во время поездки на лифте (от генерации уровня до высадки).</summary>
public class ElevatorVolume : ModuleBase
{
    public override string Id => "ElevatorVolume";
    public override string Description => "Тише звуки во время поездки на лифте. Громкость задаётся ниже.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static BepInEx.Configuration.ConfigEntry<float> _volume;
    private static bool _focused = true;
    private static bool _applied;

    protected override void OnBindConfig()
    {
        _volume = Bind("Volume", 0.5f, "Громкость эффектов в лифте: 1 = как в настройках, 0.5 = половина, 0 = тишина.");
    }

    private static bool InElevator(eGameStateName s) =>
        s is eGameStateName.Generating or eGameStateName.ReadyToStopElevatorRide
          or eGameStateName.StopElevatorRide or eGameStateName.ReadyToStartLevel;

    private static void Apply(eGameStateName state)
    {
        if (!_focused) return; // свёрнутой игрой управляет сама игра (mute in background)
        if (InElevator(state))
        {
            float sfx = CellSettingsManager.SettingsData.Audio.SFXVolume.Value;
            float basePercent = sfx <= 1.0001f ? sfx * 100f : sfx; // настройка может храниться как 0..1 или 0..100
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
