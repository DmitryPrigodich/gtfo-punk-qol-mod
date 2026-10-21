using System.Collections.Generic;
using GtfoPunk.Core;
using LevelGeneration;
using Player;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Пачки ресурсов и расходники, мимо которых прошёл ты или тиммейт (и видел их), получают метку
/// с названием и количеством зарядов. Метка пропадает, когда предмет подобрали.
/// Метки локальные: их видишь только ты, остальным ничего не отправляется.
/// </summary>
public class ResourceAutoMark : ModuleBase
{
    public override string Id => "ResourceAutoMark";
    public override string Description => "Авто-метки ресурсов и расходников, которые нашёл ты или тиммейты (видишь только ты).";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.ResourceHelper };

    private static BepInEx.Configuration.ConfigEntry<float> _discoverRadius;
    private static BepInEx.Configuration.ConfigEntry<float> _showDistance;
    private static BepInEx.Configuration.ConfigEntry<bool> _consumables;

    protected override void OnBindConfig()
    {
        _discoverRadius = Bind("DiscoverRadius", 2.5f, "На каком расстоянии от игрока предмет считается найденным (м).");
        _showDistance = Bind("ShowDistance", 30f, "Дальше этого расстояния метка скрывается (м).");
        _consumables = Bind("MarkConsumables", true, "Помечать и расходники (пена, мины, светящиеся палочки и т.п.), а не только пачки ресурсов.");
    }

    private class Mark
    {
        public LG_PickupItem_Sync Sync;
        public NavMarker Marker;
        public string Title;
        public bool Visible;
    }

    private static readonly Dictionary<int, Mark> _marks = new();
    private static float _nextScan, _nextRefresh;

    protected override void OnEnable()
    {
        FrameTicker.OnUpdate += Tick;
        FrameTicker.OnLevelExit += ClearAll;
    }

    private static void Tick()
    {
        if (!GameUtil.InLevel) return;
        float now = Time.time;
        if (now >= _nextScan) { _nextScan = now + 0.25f; Scan(); }
        if (now >= _nextRefresh) { _nextRefresh = now + 0.2f; Refresh(); }
    }

    private static bool Wanted(LG_PickupItem_Sync sync)
    {
        var slot = PickupRegistry.SlotOf(sync);
        return slot == InventorySlot.ResourcePack || (_consumables.Value && slot == InventorySlot.Consumable);
    }

    private static void Scan()
    {
        var players = PlayerManager.PlayerAgentsInLevel;
        if (players == null) return;
        float r = _discoverRadius.Value, r2 = r * r;

        foreach (var sync in PickupRegistry.Items)
        {
            if (_marks.ContainsKey(sync.GetInstanceID()) || !Wanted(sync) || !PickupRegistry.IsPlacedInLevel(sync)) continue;
            // Предмет в закрытом шкафчике/ящике недоступен для взаимодействия — его не «нашли».
            var interact = sync.GetComponentInChildren<Interact_Pickup_PickupItem>();
            if (interact == null || !interact.enabled || !interact.gameObject.activeInHierarchy) continue;
            Vector3 pos = sync.transform.position;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.Alive) continue;
                Vector3 eye = p.EyePosition;
                float d2 = (eye - pos).sqrMagnitude;
                if (d2 > r2) continue;
                // Рядом, и между игроком и предметом нет стены.
                if (d2 > 1.5f * 1.5f && Physics.Linecast(eye, pos, LayerManager.MASK_WORLD)) continue;
                Add(sync);
                break;
            }
        }
    }

    private static string MakeTitle(LG_PickupItem_Sync sync)
    {
        string name = sync.item.PublicName;
        if (PickupRegistry.SlotOf(sync) == InventorySlot.ResourcePack)
        {
            // У пачек ammo хранит заряды по 20 единиц.
            int uses = Mathf.FloorToInt(sync.GetCurrentState().custom.ammo / 20f);
            if (uses > 0) name += $" ×{uses}";
        }
        return name;
    }

    private static void Add(LG_PickupItem_Sync sync)
    {
        var marker = GuiManager.NavMarkerLayer.PrepareMarker(sync.gameObject);
        if (marker == null) return;
        marker.SetVisualStates(NavMarkerOption.CarryItemTitleDistance, NavMarkerOption.CarryItemTitleDistance,
            NavMarkerOption.Empty, NavMarkerOption.Empty);
        var m = new Mark { Sync = sync, Marker = marker, Title = MakeTitle(sync) };
        marker.SetTitle(m.Title);
        marker.SetColor(PickupRegistry.SlotOf(sync) == InventorySlot.ResourcePack
            ? new Color(0.95f, 0.85f, 0.45f) : new Color(0.75f, 0.75f, 0.75f));
        marker.SetVisible(false);
        _marks[sync.GetInstanceID()] = m;
    }

    private static readonly List<int> _toRemove = new();

    private static void Refresh()
    {
        var cam = GameUtil.LocalAgent?.FPSCamera;
        if (cam == null) return;
        float maxD2 = _showDistance.Value * _showDistance.Value;

        _toRemove.Clear();
        foreach (var kv in _marks)
        {
            var m = kv.Value;
            if (m.Sync == null || m.Marker == null || !PickupRegistry.IsPlacedInLevel(m.Sync))
            {
                _toRemove.Add(kv.Key);
                continue;
            }
            string title = MakeTitle(m.Sync);
            if (title != m.Title) { m.Title = title; m.Marker.SetTitle(title); }

            bool visible = (m.Sync.transform.position - cam.Position).sqrMagnitude <= maxD2;
            if (visible != m.Visible) { m.Visible = visible; m.Marker.SetVisible(visible); }
        }
        foreach (var id in _toRemove) Remove(id);
    }

    private static void Remove(int id)
    {
        if (!_marks.TryGetValue(id, out var m)) return;
        if (m.Marker != null) GuiManager.NavMarkerLayer.RemoveMarker(m.Marker);
        _marks.Remove(id);
    }

    private static void ClearAll()
    {
        foreach (var m in _marks.Values)
            if (m.Marker != null) GuiManager.NavMarkerLayer.RemoveMarker(m.Marker);
        _marks.Clear();
    }
}
