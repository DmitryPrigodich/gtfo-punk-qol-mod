using Player;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Press sprint during a reload — the reload is cancelled and you run.
/// The magazine stays as it was (the reload doesn't count).
/// </summary>
public class SprintCancelReload : ModuleBase
{
    public override string Id => "SprintCancelReload";
    public override string Description => "Sprint cancels the reload (rounds aren't added, as with a normal cancel).";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private static void Check(PlayerAgent owner, bool crouching)
    {
        if (!GameUtil.IsLocalHuman(owner)) return; // not the host's bots: they'd stop reloading while you sprint

        // RunInput changes RunToggleLock as a side effect — restore it.
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
