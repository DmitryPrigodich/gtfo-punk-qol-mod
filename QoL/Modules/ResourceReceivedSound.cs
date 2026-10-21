using AK;
using Gear;
using GtfoPunk.Core;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Player;
using SNetwork;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Когда тиммейт даёт тебе патроны или лечит, у тебя играет звук применения пачки.
/// В ванилле звук слышит только тот, кто применяет, поэтому получение легко пропустить.
/// Если пачку на себя применил ты сам, лишний звук не играет.
/// </summary>
public class ResourceReceivedSound : ModuleBase
{
    public override string Id => "ResourceReceivedSound";
    public override string Description => "Звук, когда тиммейт дал тебе патроны или подлечил.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static BepInEx.Configuration.ConfigEntry<float> _minHeal;

    protected override void OnBindConfig()
    {
        _minHeal = Bind("MinHealPercent", 5f,
            "Минимальный прирост здоровья (в % от максимума), на который играет звук. Отсекает естественную регенерацию.");
    }

    /// <summary>До этого момента считаем, что ресурс пришёл от нашей же пачки (применили на себя).</summary>
    private static float _selfApplyUntil;

    private static bool SelfApplyActive => Time.time < _selfApplyUntil;

    private static void PlayLocal(uint soundEvent)
    {
        var agent = GameUtil.LocalAgent;
        agent?.Sound?.Post(soundEvent, true);
    }

    // Запоминаем, что мы применяем пачку на себя: ответ (особенно у клиента) придёт с задержкой по сети.
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

            if (!GameUtil.InLevel || SelfApplyActive || before <= 0f || max <= 0f) return; // лежим/поднимают — не наш случай
            if ((after - before) / max * 100f >= _minHeal.Value)
                PlayLocal(EVENTS.MEDPACK_APPLY);
        }
    }
}
