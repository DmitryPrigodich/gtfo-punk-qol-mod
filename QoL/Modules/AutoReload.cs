using Gear;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Auto reload: the magazine runs dry — the weapon reloads by itself (after a short pause).
/// Doesn't reload while sprinting or while holding E (hacking, picking up, etc.).
/// </summary>
public class AutoReload : ModuleBase
{
    public override string Id => "AutoReload";
    public override string Description => "Auto reload when the magazine runs dry.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { "Dinorush.AutoReloadPlus", "AutoReloadPlus", "AutoReload" };

    private static ConfigEntry<float> _delay;
    private static ConfigEntry<bool> _whileRunning;

    protected override void OnBindConfig()
    {
        _delay = Bind("Delay", 0.25f, "Pause (s) after the last round before reloading.");
        _whileRunning = Bind("WhileRunning", false, "Reload while sprinting too.");
    }

    private static float _emptySince = -1f;

    protected override void OnEnable() => FrameTicker.OnUpdate += Tick;

    private static void Tick()
    {
        var agent = GameUtil.LocalAgent;
        if (!GameUtil.InLevel || agent == null || !agent.Alive) { _emptySince = -1f; return; }
        var item = agent.Inventory?.WieldedItem;
        var gun = item == null ? null : ((Il2CppObjectBase)item).TryCast<BulletWeapon>();
        if (gun == null || gun.IsReloading || gun.GetCurrentClip() > 0) { _emptySince = -1f; return; }

        if (_emptySince < 0f) { _emptySince = Time.time; return; }
        if (Time.time - _emptySince < _delay.Value) return;
        if (!_whileRunning.Value && agent.Locomotion != null && agent.Locomotion.IsRunning) return;
        // Don't interfere with holding E (lock hacking, pickup, terminal).
        var interact = agent.Interaction?.m_bestSelectedInteract;
        var timed = interact == null ? null : ((Il2CppObjectBase)interact).TryCast<Interact_Timed>();
        if (timed != null && timed.TimerIsActive) return;

        var inv = gun.m_inventory;
        if (inv == null || !inv.CanReloadCurrent()) return;
        inv.TriggerReload();
        _emptySince = -1f;
    }
}
