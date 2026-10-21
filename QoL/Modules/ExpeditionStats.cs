using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Agents;
using CellMenu;
using Gear;
using GtfoPunk.Core;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Player;
using SNetwork;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Expedition stats on the results screen (success or failure):
///  - time, how many times each player went down (visible to everyone);
///  - kills and damage per player — counted exactly only when you are the host
///    (the host processes damage to enemies); on a client — the team's total kills;
///  - how many packs you used.
/// Sends nothing over the network.
/// </summary>
public class ExpeditionStats : ModuleBase
{
    public override string Id => "ExpeditionStats";
    public override string Description => "Expedition stats on the results screen: time, downs, kills and damage (exact when you host), your packs.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { "kain.gtfo.advanced_scoreboard", "Kain_Advanced_Scoreboard" };

    private class PlayerStat
    {
        public string Name;
        public int Kills, Downs;
        public float Damage;
        public bool WasAlive = true;
    }

    private static readonly Dictionary<ulong, PlayerStat> _players = new();
    private static int _teamKills, _otherKills;
    private static bool _hostCounted;
    private static float _start, _end;
    private static readonly Dictionary<eResourceContainerSpawnType, int> _packs = new();
    private static float _nextTick;

    protected override void OnEnable() => FrameTicker.OnUpdate += Tick;

    private static void Reset()
    {
        _players.Clear(); _packs.Clear();
        _teamKills = _otherKills = 0; _hostCounted = false;
        _start = 0f; _end = 0f;
    }

    private static PlayerStat Stat(SNet_Player p)
    {
        if (p == null) return null;
        if (!_players.TryGetValue(p.Lookup, out var s)) _players[p.Lookup] = s = new PlayerStat { Name = p.NickName };
        return s;
    }

    // Downs and time — for all players, on every client.
    private static void Tick()
    {
        if (!GameUtil.InLevel) return;
        if (_start == 0f) _start = Time.time;
        _end = Time.time;
        if (Time.time < _nextTick) return;
        _nextTick = Time.time + 0.25f;
        var list = PlayerManager.PlayerAgentsInLevel;
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            var s = Stat(a?.Owner);
            if (s == null) continue;
            bool alive = a.Alive;
            if (s.WasAlive && !alive) s.Downs++;
            s.WasAlive = alive;
        }
    }

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.ChangeState))]
    private static class Patch_State
    {
        private static void Postfix(eGameStateName nextState)
        {
            if (nextState == eGameStateName.Generating) Reset(); // new drop
        }
    }

    // ---------- damage and kills (exact on the host) ----------

    private static void OnDamage(Dam_EnemyDamageBase dam, pAgent source, float before, bool hasSource)
    {
        if (!SNet.IsMaster || dam == null) return;
        _hostCounted = true;
        float after = dam.Health;
        float delta = Mathf.Max(0f, before - Mathf.Max(0f, after));
        bool killed = before > 0f && after <= 0f;
        PlayerStat s = null;
        if (hasSource && source.TryGet(out Agent agent) && agent != null)
            s = Stat(((Il2CppObjectBase)agent).TryCast<PlayerAgent>()?.Owner);
        if (s != null) { s.Damage += delta; if (killed) s.Kills++; }
        else if (killed) _otherKills++;
        if (killed) _teamKills++;
    }

    [HarmonyPatch(typeof(Dam_EnemyDamageBase), nameof(Dam_EnemyDamageBase.ReceiveBulletDamage))]
    private static class Patch_Bullet
    {
        private static void Prefix(Dam_EnemyDamageBase __instance, out float __state) => __state = __instance.Health;
        private static void Postfix(Dam_EnemyDamageBase __instance, pBulletDamageData data, float __state) => OnDamage(__instance, data.source, __state, true);
    }

    [HarmonyPatch(typeof(Dam_EnemyDamageBase), nameof(Dam_EnemyDamageBase.ReceiveMeleeDamage))]
    private static class Patch_Melee
    {
        private static void Prefix(Dam_EnemyDamageBase __instance, out float __state) => __state = __instance.Health;
        private static void Postfix(Dam_EnemyDamageBase __instance, pFullDamageData data, float __state) => OnDamage(__instance, data.source, __state, true);
    }

    [HarmonyPatch(typeof(Dam_EnemyDamageBase), nameof(Dam_EnemyDamageBase.ReceiveExplosionDamage))]
    private static class Patch_Explosion
    {
        private static void Prefix(Dam_EnemyDamageBase __instance, out float __state) => __state = __instance.Health;
        private static void Postfix(Dam_EnemyDamageBase __instance, float __state) => OnDamage(__instance, default, __state, false);
    }

    // On a client: total kills from the host's health sync.
    [HarmonyPatch(typeof(Dam_EnemyDamageBase), nameof(Dam_EnemyDamageBase.ReceiveSetHealth))]
    private static class Patch_SetHealth
    {
        private static void Prefix(Dam_EnemyDamageBase __instance, out float __state) => __state = __instance.Health;
        private static void Postfix(Dam_EnemyDamageBase __instance, float __state)
        {
            if (SNet.IsMaster) return;
            if (__state > 0f && __instance.Health <= 0f) _teamKills++;
        }
    }

    // ---------- own packs ----------

    [HarmonyPatch(typeof(ResourcePackFirstPerson), nameof(ResourcePackFirstPerson.ApplyPack))]
    private static class Patch_ApplyPack
    {
        private static void Postfix(ResourcePackFirstPerson __instance)
        {
            var owner = __instance.Owner;
            if (owner == null || !owner.IsLocallyOwned) return;
            _packs.TryGetValue(__instance.m_packType, out int n);
            _packs[__instance.m_packType] = n + 1;
        }
    }

    // ---------- output ----------

    private static string PackName(eResourceContainerSpawnType t) => t switch
    {
        eResourceContainerSpawnType.Health => Lang.T("medi", "аптечки"),
        eResourceContainerSpawnType.AmmoWeapon => Lang.T("ammo", "патроны"),
        eResourceContainerSpawnType.AmmoTool => Lang.T("tool", "инструмент"),
        eResourceContainerSpawnType.Disinfection => Lang.T("disinfect", "дезинфекция"),
        _ => t.ToString(),
    };

    private static string Build()
    {
        if (_start == 0f) return null;
        var sb = new StringBuilder();
        float dur = Mathf.Max(0f, _end - _start);
        sb.Append("<color=#ff4fd8>").Append(Lang.T("EXPEDITION STATS", "СТАТИСТИКА ЭКСПЕДИЦИИ")).Append("</color>  ")
          .Append($"{(int)(dur / 60)}:{(int)(dur % 60):00}").Append('\n');

        foreach (var s in _players.Values.OrderByDescending(p => p.Kills).ThenByDescending(p => p.Damage))
        {
            sb.Append(s.Name).Append(": ");
            if (_hostCounted) sb.Append(Lang.T("kills ", "убийств ")).Append(s.Kills).Append(Lang.T(", dmg ", ", урон ")).Append((int)s.Damage).Append(", ");
            sb.Append(Lang.T("downs ", "падений ")).Append(s.Downs).Append('\n');
        }

        sb.Append(Lang.T("Team kills: ", "Убийств командой: ")).Append(_teamKills);
        if (_hostCounted && _otherKills > 0) sb.Append(Lang.T($" (mines/other {_otherKills})", $" (мины/прочее {_otherKills})"));
        if (!_hostCounted) sb.Append(Lang.T("  (per-player kills: host only)", "  (по игрокам — только у хоста)"));
        sb.Append('\n');

        if (_packs.Count > 0)
            sb.Append(Lang.T("Your packs: ", "Твои пачки: "))
              .Append(string.Join(", ", _packs.Select(kv => $"{PackName(kv.Key)} {kv.Value}")));
        return sb.ToString().TrimEnd();
    }

    private static void Show(TMPro.TextMeshPro target)
    {
        string text = Build();
        if (text == null) return;
        ModInfo.Log.LogMessage("\n" + System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", ""));
        if (target == null) return;
        string old = target.text ?? "";
        int cut = old.IndexOf("\n\n<size=80%><color=#ff4fd8>", StringComparison.Ordinal);
        if (cut >= 0) old = old.Substring(0, cut);
        target.text = old + "\n\n<size=80%>" + text + "</size>";
    }

    [HarmonyPatch(typeof(CM_PageExpeditionSuccess), nameof(CM_PageExpeditionSuccess.OnEnable))]
    private static class Patch_Success
    {
        private static void Postfix(CM_PageExpeditionSuccess __instance) => Show(__instance.m_artifactInfo_text);
    }

    [HarmonyPatch(typeof(CM_PageExpeditionFail), nameof(CM_PageExpeditionFail.OnEnable))]
    private static class Patch_Fail
    {
        private static void Postfix(CM_PageExpeditionFail __instance) => Show(__instance.m_artifactInfo_text);
    }
}
