using Player;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Reload calculation bug: with some leftover rounds in the magazine (most noticeable on the DMR with
/// one round left) the game doesn't fill the magazine completely. Recalculating from the first result tops up
/// what's missing; if the magazine is already full, the second call changes nothing.
/// </summary>
public class DmrReloadFix : ModuleBase
{
    public override string Id => "DmrReloadFix";
    public override string Description => "Fix for the magazine not filling completely (DMR bug with one round left).";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { "DMRReloadFix" };

    private static bool _inner;

    [HarmonyPatch(typeof(PlayerAmmoStorage), nameof(PlayerAmmoStorage.GetClipBulletsFromPack))]
    private static class Patch_GetClipBulletsFromPack
    {
        private static bool Prefix(PlayerAmmoStorage __instance, ref int __result, int currentClip, AmmoType ammoType)
        {
            if (_inner) return true; // our own nested call — let it go to the original
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
