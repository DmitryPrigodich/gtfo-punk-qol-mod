using Player;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// In vanilla, pinging an open locker/box always marks the container itself, even if you look
/// right at a pack inside. Here, if there's an item behind the container on the ray, the item gets pinged.
/// </summary>
public class LockerItemPing : ModuleBase
{
    public override string Id => "LockerItemPing";
    public override string Description => "Ping the item inside an open locker/box instead of the whole container.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private const float Range = 40f;
    private const float LookThrough = 1.2f; // how far past the container to look for an item

    private static bool IsContainerStyle(eNavMarkerStyle s) =>
        s == eNavMarkerStyle.PlayerPingResourceLocker || s == eNavMarkerStyle.PlayerPingResourceBox;

    [HarmonyPatch(typeof(LocalPlayerAgent), nameof(LocalPlayerAgent.UpdateGlobalInput))]
    [HarmonyPriority(Priority.High)]
    private static class Patch_GlobalInput
    {
        private static bool Prefix(LocalPlayerAgent __instance)
        {
            if (FocusStateManager.CurrentState != eFocusState.FPS) return true;
            if (!InputMapper.GetButtonDown.Invoke(InputAction.NavMarkerPing, __instance.InputFilter)) return true;
            if (GuiManager.PlayerMarkerIsVisibleAndInFocus(__instance)) return true;
            var cam = __instance.FPSCamera;
            if (cam == null) return true;

            var hits = GameUtil.RaycastAllSorted(cam.Position, cam.Forward, Range, LayerManager.MASK_PING_TARGET);
            float containerDist = -1f;
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                var target = h.collider.GetComponentInChildren<iPlayerPingTarget>();
                if (target == null) continue;
                bool container = IsContainerStyle(target.PingTargetStyle);
                if (containerDist < 0f)
                {
                    if (!container) return true; // the first hit isn't a container, the stock ping will handle it
                    containerDist = h.distance;
                    continue;
                }
                if (h.distance > containerDist + LookThrough) break;
                if (container) continue;

                // Found an item inside the container — ping it.
                __instance.m_pingTarget = target;
                __instance.m_pingPos = h.point;
                if (target != __instance.m_lastPingedTarget || Clock.Time > __instance.m_pingAgainTimer)
                {
                    __instance.TriggerMarkerPing(target, h.collider.gameObject, h.point);
                    __instance.m_pingAgainTimer = Clock.Time + 2f;
                    __instance.m_lastPingedTarget = target;
                }
                return false;
            }
            return true;
        }
    }
}
