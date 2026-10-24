using Il2CppInterop.Runtime.InteropTypes;
using LevelGeneration;
using Localization;
using Player;
using SNetwork;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Holding a resource pack or consumable and looking into an open locker/box with a free slot —
/// hold "Use" (E) and the item goes back. Uses the stock "place item" action,
/// so it's synced for everyone.
/// </summary>
public class ReturnToLocker : ModuleBase
{
    public override string Id => "ReturnToLocker";
    public override string Description => "Put a resource pack or consumable back into an open locker/box (hold E).";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.DropItem, KnownMods.QoLFix };

    private static ConfigEntry<float> _holdTime;

    protected override void OnBindConfig()
    {
        _holdTime = Bind("HoldSeconds", 0.4f, "How many seconds to hold E to put the item back.");
    }

    private const float Reach = 1.8f;
    private const float SlotOccupiedRadius = 0.25f;

    private const float SearchInterval = 0.1f; // the container search (raycasts, slot checks) runs at 10 Hz, not every frame

    private static bool _active;          // our prompt is shown right now
    private static float _holdProgress;
    private static bool _waitRelease;     // after a successful action, wait for E to be released
    private static Transform _target;
    private static LG_ResourceContainer_Storage _storage;
    private static float _nextSearch;
    private static bool _found;
    private static ItemEquippable _foundItem;
    private static InventorySlot _foundSlot;

    protected override void OnEnable()
    {
        FrameTicker.OnUpdate += Tick;
        FrameTicker.OnLevelExit += Reset;
    }

    private static void Reset()
    {
        if (_active) HidePrompt();
        _active = false; _holdProgress = 0; _target = null; _storage = null; _found = false; _foundItem = null;
    }

    private static void Tick()
    {
        if (Time.time >= _nextSearch)
        {
            _nextSearch = Time.time + SearchInterval;
            _found = TryFindTarget(out _foundItem, out _foundSlot);
        }
        // Things that can change between searches: focus (map, menu) and the item in hand.
        if (_found && (FocusStateManager.CurrentState != eFocusState.FPS
                       || GameUtil.LocalAgent?.Inventory?.WieldedItem?.Pointer != _foundItem?.Pointer)) _found = false;
        var item = _foundItem;
        var slot = _foundSlot;
        if (!_found)
        {
            if (_active) { HidePrompt(); _active = false; }
            _holdProgress = 0;
            if (_waitRelease && !InputMapper.GetButtonKeyMouseGamepad(InputAction.Use, eFocusState.FPS)) _waitRelease = false;
            return;
        }

        _active = true;
        var gui = GuiManager.InteractionLayer;
        gui.InteractPromptVisible = true;
        gui.SetInteractPrompt(string.Format(Text.Get(864u), item.PublicName),
            string.Format(Text.Get(827u), InputMapper.GetBindingName(InputAction.Use)));

        bool held = InputMapper.GetButtonKeyMouseGamepad(InputAction.Use, eFocusState.FPS);
        if (_waitRelease) { if (!held) _waitRelease = false; gui.SetTimer(0); return; }

        if (!held) { _holdProgress = 0; gui.SetTimer(0); return; }

        _holdProgress += Time.deltaTime;
        float need = Mathf.Max(0.05f, _holdTime.Value);
        gui.SetTimer(Mathf.Clamp01(_holdProgress / need));
        if (_holdProgress >= need)
        {
            Place(item, slot);
            _holdProgress = 0;
            _waitRelease = true;
        }
    }

    private static bool TryFindTarget(out ItemEquippable item, out InventorySlot slot)
    {
        item = null; slot = InventorySlot.None; _target = null; _storage = null;
        if (!GameUtil.InLevel || FocusStateManager.CurrentState != eFocusState.FPS) return false;

        var agent = GameUtil.LocalAgent;
        var inv = agent?.Inventory;
        if (inv == null) return false;
        slot = inv.WieldedSlot;
        if (slot != InventorySlot.ResourcePack && slot != InventorySlot.Consumable) return false;
        item = inv.WieldedItem;
        if (item == null || !item.AllowPlayerInteraction) return false;

        var cam = agent.FPSCamera;
        if (cam == null) return false;

        // Find the nearest container on the view ray.
        var hits = GameUtil.RaycastAllSorted(cam.Position, cam.Forward, Reach, ~0, QueryTriggerInteraction.Collide);
        LG_WeakResourceContainer container = null;
        Vector3 hitPoint = default;
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            var c = h.collider.GetComponentInParent<LG_WeakResourceContainer>();
            if (c != null) { container = c; hitPoint = h.point; break; }
            // Look past triggers, players and lying items; anything else (a wall) stops us.
            if (h.collider.isTrigger || h.collider.GetComponentInParent<LG_PickupItem_Sync>() != null
                || h.collider.GetComponentInParent<PlayerAgent>() != null) continue;
            break;
        }
        if (container == null) return false;

        var gfx = container.m_graphics == null ? null : ((Il2CppObjectBase)container.m_graphics).TryCast<LG_WeakResourceContainer_Graphics>();
        if (gfx == null || gfx.m_status != eResourceContainerStatus.Open) return false;
        var storage = container.m_storage == null ? null : ((Il2CppObjectBase)container.m_storage).TryCast<LG_ResourceContainer_Storage>();
        if (storage == null || storage.m_storageSlots == null) return false;

        // The free slot closest to the look point.
        Transform bestT = null;
        float bestD = float.MaxValue;
        var slots = storage.m_storageSlots;
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s == null) continue;
            var t = slot == InventorySlot.ResourcePack ? s.ResourcePack : s.Consumable;
            if (t == null || IsOccupied(s)) continue;
            float d = (t.position - hitPoint).sqrMagnitude;
            if (d < bestD) { bestD = d; bestT = t; }
        }
        if (bestT == null) return false;

        _target = bestT;
        _storage = storage;
        return true;
    }

    private static bool IsOccupied(StorageSlot s) =>
        Near(s.ResourcePack) || Near(s.Consumable) || Near(s.CommoditySmall) || Near(s.CommodityMedium) || Near(s.CommodityLarge) || Near(s.Keycard);

    private static bool Near(Transform t) => t != null && PickupRegistry.AnyPlacedNear(t.position, SlotOccupiedRadius);

    private static void Place(ItemEquippable item, InventorySlot slot)
    {
        if (_target == null || _storage == null) return;
        _found = false; // search again after placing
        if (!PlayerBackpackManager.TryGetItemInLevelFromItemData(item.Get_pItemData(), out Item lvlItem) || lvlItem == null) return;
        var inLevel = ((Il2CppObjectBase)lvlItem).TryCast<ItemInLevel>();
        var sync = inLevel?.GetSyncComponent();
        if (sync == null) return;

        var custom = sync.GetCustomData();
        var ammo = PlayerBackpackManager.LocalBackpack?.AmmoStorage?.GetInventorySlotAmmo(slot);
        if (ammo != null) custom.ammo = ammo.AmmoInPack;

        sync.AttemptPickupInteraction(ePickupItemInteractionType.Place, SNet.LocalPlayer, custom,
            _target.position, _target.rotation, _storage.m_core?.SpawnNode, true, false);
    }

    private static void HidePrompt()
    {
        var gui = GuiManager.InteractionLayer;
        gui.SetTimer(0);
        gui.InteractPromptVisible = false;
    }

    // While our prompt is shown, stock interactions must not grab E.
    [HarmonyPatch(typeof(PlayerInteraction), nameof(PlayerInteraction.UpdateWorldInteractions))]
    private static class Patch_WorldInteractions
    {
        private static bool Prefix() => !_active;
    }

    [HarmonyPatch(typeof(Gear.ResourcePackFirstPerson), nameof(Gear.ResourcePackFirstPerson.UpdateInteraction))]
    private static class Patch_PackInteraction
    {
        private static bool Prefix() => !_active;
    }
}
