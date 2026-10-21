using GtfoPunk.Core;
using HarmonyLib;
using Player;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Баг расчёта перезарядки: при некоторых остатках в магазине (заметнее всего у DMR, когда остался
/// один патрон) игра набивает магазин не до конца. Повторный расчёт от результата первого доливает
/// недостающее; если магазин уже полный, второй вызов ничего не меняет.
/// </summary>
public class DmrReloadFix : ModuleBase
{
    public override string Id => "DmrReloadFix";
    public override string Description => "Фикс недозарядки магазина (баг DMR при одном оставшемся патроне).";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { "DMRReloadFix" };

    private static bool _inner;

    [HarmonyPatch(typeof(PlayerAmmoStorage), nameof(PlayerAmmoStorage.GetClipBulletsFromPack))]
    private static class Patch_GetClipBulletsFromPack
    {
        private static bool Prefix(PlayerAmmoStorage __instance, ref int __result, int currentClip, AmmoType ammoType)
        {
            if (_inner) return true; // наш собственный вложенный вызов — пусть идёт в оригинал
            _inner = true;
            try
            {
                int first = __instance.GetClipBulletsFromPack(currentClip, ammoType);
                __result = __instance.GetClipBulletsFromPack(first, ammoType);
            }
            finally { _inner = false; }
            return false;
        }
    }
}
