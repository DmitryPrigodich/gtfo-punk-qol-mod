using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GtfoPunk.Core;
using HarmonyLib;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Removes "liquids on the glass": blood, goo, disinfection, water, infection sweat — by category.
/// Affects only your screen. The module is off by default (it's part of the atmosphere).
/// </summary>
public class ScreenLiquidFilter : ModuleBase
{
    public override string Id => "ScreenLiquidFilter";
    public override string Description => "Remove liquid effects on screen (by the categories below). Off by default.";
    public override ModuleSide Side => ModuleSide.Client;
    public override bool EnabledByDefault => false;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static BepInEx.Configuration.ConfigEntry<bool> _enemyBlood, _playerBlood, _goo, _water, _disinfection, _infection;

    protected override void OnBindConfig()
    {
        _enemyBlood = Bind("HideEnemyBlood", true, "Remove enemy blood splatter.");
        _playerBlood = Bind("HidePlayerBlood", true, "Remove blood when you get hurt.");
        _goo = Bind("HideGoo", true, "Remove goo (spitters, shooters, anemones).");
        _water = Bind("HideWater", false, "Remove water drops and rain (including in the elevator).");
        _disinfection = Bind("HideDisinfection", false, "Remove disinfection splashes.");
        _infection = Bind("HideInfection", false, "Remove infection \"sweat\" on the glass.");
    }

    private static bool ShouldHide(ScreenLiquidSettingName s)
    {
        string n = s.ToString();
        if (n.StartsWith("enemyBlood")) return _enemyBlood.Value;
        if (n.StartsWith("playerBlood")) return _playerBlood.Value;
        if (n is "spitterJizz" or "shooterGoo" or "anemoneGoo" || n.EndsWith("Goo")) return _goo.Value;
        if (n is "elevatorRain" || n.StartsWith("water")) return _water.Value;
        if (n.StartsWith("disinfection")) return _disinfection.Value;
        if (n.StartsWith("infection")) return _infection.Value;
        return false;
    }

    [HarmonyPatch]
    private static class Patch_Apply
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            typeof(ScreenLiquidManager).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "Apply" or "TryApply" or "DirectApply"
                            && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(ScreenLiquidSettingName));

        private static bool Prefix(ScreenLiquidSettingName setting, ref bool __result)
        {
            if (!ShouldHide(setting)) return true;
            __result = false;
            return false;
        }
    }
}

/// <summary>
/// Removes the blue noise ("grain") over the image. The picture is cleaner, but dark areas may get noticeably darker.
/// Off by default.
/// </summary>
public class NoiseRemoval : ModuleBase
{
    public override string Id => "NoiseRemoval";
    public override string Description => "Remove the blue noise (grain) over the image. Dark areas may get darker. Off by default.";
    public override ModuleSide Side => ModuleSide.Client;
    public override bool EnabledByDefault => false;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static Texture _empty;

    [HarmonyPatch(typeof(PE_BlueNoise), nameof(PE_BlueNoise.Update))]
    private static class Patch_Update
    {
        private static bool Prefix()
        {
            if (PE_BlueNoise.s_computeShader == null) return false;
            if (_empty == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                tex.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 0.5f)); // neutral value instead of noise
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                _empty = tex;
            }
            Shader.SetGlobalTexture("_PE_BlueNoise", _empty);
            PE_BlueNoise.s_computeShader.SetTexture(0, "_PE_BlueNoise", _empty);
            return false;
        }
    }
}
