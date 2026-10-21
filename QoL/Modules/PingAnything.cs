using Enemies;
using GtfoPunk.Core;
using HarmonyLib;
using Player;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Extends the stock ping (mouse wheel): if the game found no target, you can ping
///  - a sleeping enemy nearby ("enemy" marker);
///  - any point in the world ("look here" marker).
/// Stock targets (items, doors, terminals) are still handled by the game.
/// Pings are sent the normal way — everyone sees them, even without the mod.
/// </summary>
public class PingAnything : ModuleBase
{
    public override string Id => "PingAnything";
    public override string Description => "Ping (mouse wheel) sleeping enemies nearby and any world point if the game found no target.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.PingEverything };

    private static BepInEx.Configuration.ConfigEntry<bool> _enemies;
    private static BepInEx.Configuration.ConfigEntry<float> _enemyRange;
    private static BepInEx.Configuration.ConfigEntry<bool> _world;

    protected override void OnBindConfig()
    {
        _enemies = Bind("PingSleepingEnemies", true, "Ping sleeping enemies.");
        _enemyRange = Bind("SleepingEnemyRange", 12f, "Max ping distance to a sleeping enemy (m).");
        _world = Bind("PingWorldPoint", true, "Ping any point (wall, floor) if there's no target.");
    }

    private const float VanillaRange = 40f;
    private static float _cooldownUntil;

    [HarmonyPatch(typeof(LocalPlayerAgent), nameof(LocalPlayerAgent.UpdateGlobalInput))]
    private static class Patch_GlobalInput
    {
        private static bool Prefix(LocalPlayerAgent __instance, bool __runOriginal)
        {
            if (!__runOriginal) return false; // the ping was already handled by another module (e.g. LockerItemPing)
            if (FocusStateManager.CurrentState != eFocusState.FPS) return true;
            if (!InputMapper.GetButtonDown.Invoke(InputAction.NavMarkerPing, __instance.InputFilter)) return true;
            if (GuiManager.PlayerMarkerIsVisibleAndInFocus(__instance)) return true; // removing your own marker — stock
            if (Time.time < _cooldownUntil) return true;

            var cam = __instance.FPSCamera;
            if (cam == null) return true;
            Vector3 from = cam.Position, dir = cam.Forward;

            // There's a stock target — let the game handle it.
            if (Physics.Raycast(from, dir, out var hit, VanillaRange, LayerManager.MASK_PING_TARGET, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInChildren<iPlayerPingTarget>() != null)
                return true;

            if (_enemies.Value && Physics.Raycast(from, dir, out hit, _enemyRange.Value, LayerManager.MASK_BULLETWEAPON_RAY, QueryTriggerInteraction.Ignore))
            {
                var enemy = hit.collider.GetComponentInParent<EnemyAgent>();
                if (enemy != null && enemy.Locomotion != null
                    && enemy.Locomotion.CurrentStateEnum == ES_StateEnum.Hibernate
                    && !enemy.name.Contains("Shadow"))
                {
                    Ping(__instance, hit, eNavMarkerStyle.PlayerPingEnemy);
                    return false;
                }
            }

            if (_world.Value && Physics.Raycast(from, dir, out hit, VanillaRange, LayerManager.MASK_DEFAULT, QueryTriggerInteraction.Ignore))
            {
                Ping(__instance, hit, eNavMarkerStyle.PlayerPingLookat);
                return false;
            }

            return true;
        }
    }

    private static void Ping(LocalPlayerAgent agent, RaycastHit hit, eNavMarkerStyle style)
    {
        var go = hit.collider.gameObject;
        bool added = false;
        var target = go.GetComponent<PlayerPingTarget>();
        if (target == null) { target = go.AddComponent<PlayerPingTarget>(); added = true; }
        var prevStyle = target.m_pingTargetStyle;
        target.m_pingTargetStyle = style;
        try
        {
            var iTarget = go.GetComponent<iPlayerPingTarget>();
            if (iTarget != null)
            {
                agent.m_pingTarget = iTarget;
                agent.m_pingPos = hit.point;
                agent.TriggerMarkerPing(iTarget, go, hit.point);
                agent.m_lastPingedTarget = iTarget;
                agent.m_pingAgainTimer = Clock.Time + 0.5f;
                _cooldownUntil = Time.time + 0.5f;
            }
        }
        finally
        {
            if (added) Object.Destroy(target);
            else target.m_pingTargetStyle = prevStyle;
        }
    }
}
