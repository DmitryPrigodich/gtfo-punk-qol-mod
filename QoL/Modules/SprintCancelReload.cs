using GtfoPunk.Core;
using HarmonyLib;
using Player;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Нажал спринт во время перезарядки — перезарядка отменяется и ты бежишь.
/// Патроны в магазине остаются как были (перезарядка не засчитывается).
/// </summary>
public class SprintCancelReload : ModuleBase
{
    public override string Id => "SprintCancelReload";
    public override string Description => "Спринт отменяет перезарядку (патроны не засчитываются, как при обычной отмене).";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static void Check(PlayerAgent owner, bool crouching)
    {
        if (owner == null || !owner.IsLocallyOwned) return;

        // RunInput меняет RunToggleLock как побочный эффект — возвращаем как было.
        bool toggleLock = PlayerLocomotion.RunToggleLock;
        bool wantsRun;
        try { wantsRun = PlayerLocomotion.RunInput(owner, crouching); }
        finally { PlayerLocomotion.RunToggleLock = toggleLock; }
        if (!wantsRun) return;

        if (!owner.Locomotion.InputIsForwardEnoughForRun()) return;
        if (crouching && !owner.PlayerCharacterController.ColliderCanStand()) return;

        var holder = owner.FPItemHolder;
        if (holder == null || !holder.ItemIsBusy) return;
        var item = holder.WieldedItem;
        if (item == null || !item.IsReloading) return;

        item.IsReloading = false;
        item.StopAnimationSequenceCoroutine();

        var parts = item.GearPartHolder;
        if (parts != null)
        {
            parts.FrontPartAnimator?.Rebind();
            parts.ReceiverPartAnimator?.Rebind();
            parts.StockPartAnimator?.Rebind();
        }
        owner.AnimatorArms?.Rebind();
        owner.Inventory?.PlayAnimationsForWieldedItem();
    }

    [HarmonyPatch(typeof(PLOC_Stand), nameof(PLOC_Stand.Update))]
    private static class Patch_Stand
    {
        private static void Prefix(PLOC_Stand __instance) => Check(__instance.m_owner, false);
    }

    [HarmonyPatch(typeof(PLOC_Crouch), nameof(PLOC_Crouch.Update))]
    private static class Patch_Crouch
    {
        private static void Prefix(PLOC_Crouch __instance) => Check(__instance.m_owner, true);
    }
}
