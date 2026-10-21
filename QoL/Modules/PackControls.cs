using Gear;
using GtfoPunk.Core;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Localization;
using Player;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Удобное управление пачкой ресурсов:
///  - зажать E — применить на того, на кого смотришь, а если ни на кого — на себя;
///  - зажать ЛКМ — на себя (как в ванилле);
///  - зажать ПКМ, глядя на тиммейта, — на него.
/// Логика выбора цели повторяет проверенную схему из PacksHelper (Localia).
/// </summary>
public class PackControls : ModuleBase
{
    public override string Id => "PackControls";
    public override string Description => "Пачка: E — на цель или на себя, ЛКМ — на себя, ПКМ — на тиммейта, на которого смотришь.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.PacksHelper };

    private const float GiveRange = 2.4f;

    // Флаги «окна» внутри одного вызова UpdateInteraction.
    private static bool _pickTarget, _forceStart, _forceCondition;

    private static bool Down(PlayerAgent a, InputAction act) => InputMapper.GetButtonDown.Invoke(act, a.InputFilter);
    private static bool Held(PlayerAgent a, InputAction act) => InputMapper.GetButton.Invoke(act, a.InputFilter);

    private static iResourcePackReceiver Self(PlayerAgent owner) => owner.GetComponentInChildren<iResourcePackReceiver>();

    private static iResourcePackReceiver Looked(PlayerAgent owner)
    {
        var cam = owner.FPSCamera;
        if (cam == null) return null;
        if (Physics.Raycast(cam.Position, cam.Forward, out var hit, GiveRange, LayerManager.MASK_GIVE_RESOURCE_PACK))
            return hit.collider.GetComponentInParent<iResourcePackReceiver>();
        return null;
    }

    [HarmonyPatch(typeof(ResourcePackFirstPerson), nameof(ResourcePackFirstPerson.UpdateInteraction))]
    private static class Patch_UpdateInteraction
    {
        [HarmonyPriority(Priority.High)]
        private static void Prefix(ResourcePackFirstPerson __instance)
        {
            if (__instance.m_interactApplyResource != null && !__instance.m_interactApplyResource.TimerIsActive)
            {
                __instance.m_lastActionReceiver = null;
                _pickTarget = true;
                _forceStart = false;
                _forceCondition = false;
            }
        }

        private static void Postfix() { _pickTarget = _forceStart = _forceCondition = false; }
    }

    [HarmonyPatch(typeof(ResourcePackFirstPerson), nameof(ResourcePackFirstPerson.UpdateInteractionActionName))]
    private static class Patch_ActionName
    {
        private static bool Prefix(ResourcePackFirstPerson __instance)
        {
            if (!_pickTarget) return true;
            _pickTarget = false;
            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned) return true;

            bool useDown = Down(owner, InputAction.Use);
            bool selfByFire = false;
            var looked = Looked(owner);
            iResourcePackReceiver receiver;
            if (useDown) receiver = looked ?? Self(owner);
            else if (Held(owner, InputAction.Fire)) { receiver = Self(owner); selfByFire = true; }
            else receiver = looked ?? Self(owner);
            if (receiver == null) return true;

            __instance.m_actionReceiver = receiver;
            var interact = __instance.m_interactApplyResource;
            interact.m_input = InputAction.Use;

            if (receiver.IsLocallyOwned)
            {
                __instance.UpdateInteractionActionName(Text.Get(856u)); // «на себя», штатный текст
                if (!useDown) interact.m_input = InputAction.Fire;
            }
            else
            {
                __instance.UpdateInteractionActionName(receiver.InteractionName);
                if (!useDown) interact.m_input = InputAction.Aim;
            }

            _forceStart = true;
            _forceCondition = selfByFire;
            return false;
        }
    }

    // Начинаем таймер при удержании кнопки, а не только в момент нажатия.
    [HarmonyPatch(typeof(Interact_Timed), nameof(Interact_Timed.PlayerCheckInput))]
    private static class Patch_CheckInput
    {
        private static bool Prefix(Interact_Timed __instance, PlayerAgent agent)
        {
            if (!_forceStart) return true;
            _forceStart = false;
            if (!__instance.InternalAllowInput) return true;
            if (__instance.m_localPlayerInteractInfo == null && Held(agent, __instance.InputAction))
            {
                __instance.OnInteractorStateChanged(agent, true);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Interact_ManualTimedWithCallback), nameof(Interact_ManualTimedWithCallback.ManualUpdateWithCondition))]
    private static class Patch_Condition
    {
        // Как в PacksHelper: при применении на себя по ЛКМ форсируем selectedOnIdle.
        private static void Prefix(ref bool selectedOnIdle)
        {
            if (!_forceCondition) return;
            _forceCondition = false;
            selectedOnIdle = true;
        }
    }
}
