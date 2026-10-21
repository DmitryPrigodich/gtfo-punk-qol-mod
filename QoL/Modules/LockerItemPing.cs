using GtfoPunk.Core;
using HarmonyLib;
using Player;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// В ванилле пинг по открытому шкафчику/ящику всегда метит сам контейнер, даже если смотришь
/// прямо на пачку внутри. Здесь, если за контейнером на луче есть предмет, пингуется предмет.
/// </summary>
public class LockerItemPing : ModuleBase
{
    public override string Id => "LockerItemPing";
    public override string Description => "Пинг предмета внутри открытого шкафчика/ящика, а не всего контейнера.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private const float Range = 40f;
    private const float LookThrough = 1.2f; // насколько дальше контейнера ищем предмет

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

            var raw = Physics.RaycastAll(cam.Position, cam.Forward, Range, LayerManager.MASK_PING_TARGET, QueryTriggerInteraction.Ignore);
            var hits = new RaycastHit[raw.Length];
            for (int i = 0; i < raw.Length; i++) hits[i] = raw[i];
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            float containerDist = -1f;
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                var target = h.collider.GetComponentInChildren<iPlayerPingTarget>();
                if (target == null) continue;
                bool container = IsContainerStyle(target.PingTargetStyle);
                if (containerDist < 0f)
                {
                    if (!container) return true; // первая цель — не контейнер, штатный пинг справится
                    containerDist = h.distance;
                    continue;
                }
                if (h.distance > containerDist + LookThrough) break;
                if (container) continue;

                // Нашли предмет внутри контейнера — пингуем его.
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
