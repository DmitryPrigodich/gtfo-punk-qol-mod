using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx;
using CellMenu;
using Gear;
using GtfoPunk.Core;
using Player;
using SNetwork;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Пресеты лоадаута: в лобби F5–F8 — надеть комплект 1–4, Shift+F5–F8 — сохранить текущий комплект.
/// Сохраняется основное, специальное, инструмент и милишка. Хранятся в BepInEx/config/GtfoPunk.LoadoutPresets.json.
/// </summary>
public class LoadoutPresets : ModuleBase
{
    public override string Id => "LoadoutPresets";
    public override string Description => "Пресеты лоадаута в лобби: F5–F8 — надеть комплект 1–4, Shift+F5–F8 — сохранить текущий.";
    public override ModuleSide Side => ModuleSide.Client;

    private static readonly InventorySlot[] Slots =
        { InventorySlot.GearStandard, InventorySlot.GearSpecial, InventorySlot.GearClass, InventorySlot.GearMelee };
    private static readonly KeyCode[] Keys = { KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8 };

    private static string FilePath => Path.Combine(Paths.ConfigPath, "GtfoPunk.LoadoutPresets.json");
    private static Dictionary<string, Dictionary<string, string>> _presets = new();

    protected override void OnEnable()
    {
        Load();
        FrameTicker.OnUpdate += Tick;
    }

    private static void Tick()
    {
        if (GameStateManager.CurrentStateName != eGameStateName.Lobby || PlayerChatManager.InChatMode) return;
        for (int i = 0; i < Keys.Length; i++)
        {
            if (!Input.GetKeyDown(Keys[i])) continue;
            bool save = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (save) SavePreset(i + 1); else ApplyPreset(i + 1);
        }
    }

    private static void Say(string msg)
    {
        ModInfo.Log.LogInfo("[LoadoutPresets] " + msg);
        if (SNet.HasLocalPlayer) PlayerChatManager.PostChatMessageLocaly(SNet.LocalPlayer, "<color=#ff4fd8>[GtfoPunk]</color> " + msg);
    }

    private static void SavePreset(int n)
    {
        var bp = PlayerBackpackManager.LocalBackpack;
        if (bp == null) { Say(Lang.T("No backpack yet.", "Рюкзак ещё не готов.")); return; }
        var preset = new Dictionary<string, string>();
        var names = new List<string>();
        foreach (var slot in Slots)
        {
            if (!bp.TryGetBackpackItem(slot, out BackpackItem item) || item?.GearIDRange == null) continue;
            string id = item.GearIDRange.PlayfabItemInstanceId;
            if (string.IsNullOrEmpty(id)) continue;
            preset[slot.ToString()] = id;
            names.Add(item.GearIDRange.PublicGearName);
        }
        _presets[n.ToString()] = preset;
        Store();
        Say(Lang.T($"Preset {n} saved: ", $"Пресет {n} сохранён: ") + string.Join(", ", names));
    }

    private static void ApplyPreset(int n)
    {
        if (!_presets.TryGetValue(n.ToString(), out var preset) || preset.Count == 0)
        {
            Say(Lang.T($"Preset {n} is empty. Shift+F{4 + n} to save.", $"Пресет {n} пуст. Shift+F{4 + n} — сохранить."));
            return;
        }
        int ok = 0, missing = 0;
        foreach (var slot in Slots)
        {
            if (!preset.TryGetValue(slot.ToString(), out var id)) continue;
            if (!GearManager.TryGetGear(id, out GearIDRange gear) || gear == null) { missing++; continue; }
            try
            {
                GearManager.RegisterGearInSlotAsEquipped(id, slot);
                PlayerBackpackManager.EquipLocalGear(gear);
                ok++;
            }
            catch (Exception e)
            {
                missing++;
                ModInfo.Log.LogWarning($"[LoadoutPresets] {slot}: {e.Message}");
            }
        }
        RefreshLobbyBar();
        Say(Lang.T($"Preset {n} equipped ({ok} items", $"Пресет {n} надет ({ok} предм.")
            + (missing > 0 ? Lang.T($", {missing} not available)", $", {missing} недоступно)") : ")"));
    }

    private static void RefreshLobbyBar()
    {
        var bars = CM_PlayerLobbyBar.instances;
        if (bars == null) return;
        for (int i = 0; i < bars.Count; i++)
        {
            var b = bars[i];
            if (b?.m_player != null && b.m_player.IsLocal) b.m_updateInventory = true;
        }
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                _presets = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception e) { ModInfo.Log.LogWarning("[LoadoutPresets] не удалось прочитать пресеты: " + e.Message); }
    }

    private static void Store()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(_presets, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception e) { ModInfo.Log.LogWarning("[LoadoutPresets] не удалось сохранить пресеты: " + e.Message); }
    }
}
