using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GtfoPunk.Core;
using HarmonyLib;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Убирает «жидкости на стекле»: кровь, слизь, дезинфекцию, воду, пот инфекции — по категориям.
/// Влияет только на твой экран. По умолчанию модуль выключен (это часть атмосферы).
/// </summary>
public class ScreenLiquidFilter : ModuleBase
{
    public override string Id => "ScreenLiquidFilter";
    public override string Description => "Убирать эффекты жидкостей на экране (по категориям ниже). По умолчанию выключено.";
    public override ModuleSide Side => ModuleSide.Client;
    public override bool EnabledByDefault => false;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static BepInEx.Configuration.ConfigEntry<bool> _enemyBlood, _playerBlood, _goo, _water, _disinfection, _infection;

    protected override void OnBindConfig()
    {
        _enemyBlood = Bind("HideEnemyBlood", true, "Убрать брызги крови врагов.");
        _playerBlood = Bind("HidePlayerBlood", true, "Убрать кровь, когда ранят тебя.");
        _goo = Bind("HideGoo", true, "Убрать слизь (спиттеры, шутеры, анемоны).");
        _water = Bind("HideWater", false, "Убрать капли воды и дождь (в том числе в лифте).");
        _disinfection = Bind("HideDisinfection", false, "Убрать брызги дезинфекции.");
        _infection = Bind("HideInfection", false, "Убрать «пот» инфекции на стекле.");
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
/// Убирает синий шум («зерно») поверх картинки. Изображение чище, но тёмные места могут стать заметно темнее.
/// По умолчанию выключено.
/// </summary>
public class NoiseRemoval : ModuleBase
{
    public override string Id => "NoiseRemoval";
    public override string Description => "Убрать синий шум (зерно) поверх картинки. Тёмные места могут стать темнее. По умолчанию выключено.";
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
                tex.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 0.5f)); // нейтральное значение вместо шума
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
