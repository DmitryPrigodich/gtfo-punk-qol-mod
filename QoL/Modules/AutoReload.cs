using System.Collections.Generic;
using Gear;
using GtfoPunk.Core;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Автоперезарядка: магазин опустел — оружие само перезаряжается (через небольшую паузу).
/// Не перезаряжает на бегу и во время удержания E (взлом, подбор и т. п.).
/// </summary>
public class AutoReload : ModuleBase
{
    public override string Id => "AutoReload";
    public override string Description => "Автоперезарядка, когда магазин опустел.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { "Dinorush.AutoReloadPlus", "AutoReloadPlus", "AutoReload" };

    private static BepInEx.Configuration.ConfigEntry<float> _delay;
    private static BepInEx.Configuration.ConfigEntry<bool> _whileRunning;

    protected override void OnBindConfig()
    {
        _delay = Bind("Delay", 0.25f, "Пауза (сек) после последнего патрона перед перезарядкой.");
        _whileRunning = Bind("WhileRunning", false, "Перезаряжать и на бегу.");
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
        // Не мешаем удержанию E (взлом замка, подбор, терминал).
        var interact = agent.Interaction?.m_bestSelectedInteract;
        var timed = interact == null ? null : ((Il2CppObjectBase)interact).TryCast<Interact_Timed>();
        if (timed != null && timed.TimerIsActive) return;

        var inv = gun.m_inventory;
        if (inv == null || !inv.CanReloadCurrent()) return;
        inv.TriggerReload();
        _emptySince = -1f;
    }
}
