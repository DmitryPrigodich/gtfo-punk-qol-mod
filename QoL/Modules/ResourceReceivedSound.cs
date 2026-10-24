using AK;
using Gear;
using Il2CppInterop.Runtime.InteropTypes;
using Player;
using SNetwork;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// When a teammate gives you ammo or heals you, you hear the pack-use sound.
/// In vanilla only the one applying hears it, so it's easy to miss receiving.
/// If you applied the pack to yourself, no extra sound plays.
/// </summary>
public class ResourceReceivedSound : ModuleBase
{
    public override string Id => "ResourceReceivedSound";
    public override string Description => "A sound when a teammate gave you ammo or healed you.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static ConfigEntry<float> _minHeal;

    protected override void OnBindConfig()
    {
        _minHeal = Bind("MinHealPercent", 5f,
            "Minimum health gain (in % of max) that plays the sound. Filters out natural regeneration.");
    }

    /// <summary>Until this moment we assume the resource came from our own pack (applied to self).</summary>
    private static float _selfApplyUntil;

    private static bool SelfApplyActive => Time.time < _selfApplyUntil;

    private static void PlayLocal(uint soundEvent)
    {
        var agent = GameUtil.LocalAgent;
        agent?.Sound?.Post(soundEvent, true);
    }

    // Remember that we're applying a pack to ourselves: the response (especially on a client) arrives with network delay.
    [HarmonyPatch(typeof(ResourcePackFirstPerson), nameof(ResourcePackFirstPerson.ApplyPack))]
    private static class Patch_ApplyPack
    {
        private static void Prefix(ResourcePackFirstPerson __instance)
        {
            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned) return;
            var receiver = __instance.m_actionReceiver;
            if (receiver == null) return;
            var target = ((Il2CppObjectBase)receiver).TryCast<PlayerAgent>();
            if (target != null && target.IsLocallyOwned)
                _selfApplyUntil = Time.time + 2f;
        }
    }

    [HarmonyPatch(typeof(PlayerBackpackManager), nameof(PlayerBackpackManager.ReceiveAmmoGive))]
    private static class Patch_ReceiveAmmoGive
    {
        private static void Postfix(pAmmoGive data)
        {
            if (!GameUtil.InLevel || SelfApplyActive) return;
            if (!data.targetPlayer.TryGetPlayer(out SNet_Player target) || !GameUtil.IsLocal(target)) return;
            PlayLocal(EVENTS.AMMOPACK_APPLY);
        }
    }

    [HarmonyPatch(typeof(Dam_PlayerDamageLocal), nameof(Dam_PlayerDamageLocal.ReceiveSetHealth))]
    private static class Patch_ReceiveSetHealth
    {
        private static void Prefix(Dam_PlayerDamageLocal __instance, pSetHealthData data)
        {
            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned) return;

            float max = __instance.HealthMax;
            float before = __instance.Health;
            float after = data.health.Get(max);

            if (!GameUtil.InLevel || SelfApplyActive || before <= 0f || max <= 0f) return; // downed/being revived — not our case
            if ((after - before) / max * 100f >= _minHeal.Value)
                PlayLocal(EVENTS.MEDPACK_APPLY);
        }
    }
}
