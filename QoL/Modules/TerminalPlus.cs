using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GtfoPunk.Core;
using HarmonyLib;
using LevelGeneration;
using SNetwork;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Терминал «как Linux». Всё локальное: другие игроки видят обычные команды.
///  - Алиасы: ls/l/dir → LIST, cat/less → READ, clear → CLS, p → PING, q → QUERY, man → HELP, uv/uc/rs/rv… (как в BashTerm).
///  - Сокращения объектов: «p am 69» → PING AMMOPACK_69, «q med 12» → QUERY MEDIPACK_12.
///  - Пайпы: «ls | grep ammo», «ls | grep -v locker» — фильтрует вывод только у тебя.
///  - История: !! (последняя), !5 (по номеру), !pi (последняя на «pi»), history — список.
///  - Горячие клавиши: Ctrl+L — очистить экран, Ctrl+C / Ctrl+U — стереть строку, Ctrl+V — вставить,
///    Ctrl+R — поиск по истории (повторное нажатие — следующее совпадение).
///  - Мелочи: whoami, echo, uname, uptime.
/// Стрелки ↑/↓ (история) и Tab (дополнение) работают штатно.
/// </summary>
public class TerminalPlus : ModuleBase
{
    public override string Id => "TerminalPlus";
    public override string Description => "Терминал «как Linux»: алиасы (ls, cat, clear, p, q…), | grep, !!, history, Ctrl+L/C/U/V/R.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { "io.takina.gtfo.BashTerm", "BashTerm", "Drummerx04.TerminalCompletionPlugin", "TerminalCompletion", "BetterTerminal" };

    private static BepInEx.Configuration.ConfigEntry<bool> _aliases, _expand;

    protected override void OnBindConfig()
    {
        _aliases = Bind("Aliases", true, "Алиасы команд (ls, cat, clear, p, q, man, uv, uc, rs, rv…).");
        _expand = Bind("ObjectExpansion", true, "Сокращения объектов в PING/QUERY: «p am 69» → PING AMMOPACK_69.");
    }

    // ---------- данные ----------

    private static readonly Dictionary<string, string> CmdAlias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ls"] = "LIST", ["l"] = "LIST", ["dir"] = "LIST", ["lsu"] = "LIST U", ["lu"] = "LIST U",
        ["cat"] = "READ", ["less"] = "READ", ["more"] = "READ",
        ["clear"] = "CLS", ["p"] = "PING", ["q"] = "QUERY", ["man"] = "HELP",
        ["uc"] = "UPLINK_CONNECT", ["uv"] = "UPLINK_VERIFY",
        ["rs"] = "REACTOR_STARTUP", ["rsd"] = "REACTOR_SHUTDOWN", ["rv"] = "REACTOR_VERIFY",
        ["quit"] = "EXIT", ["logout"] = "EXIT",
    };

    private static readonly Dictionary<string, string> ObjExact = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nhsu"] = "NEONATE_HSU", ["diss"] = "DISINFECTION_STATION",
    };

    // Порядок важен: длинные префиксы раньше коротких.
    private static readonly (string pre, string full)[] ObjPrefix =
    {
        ("turb", "FOG_TURBINE"), ("bulk", "BULKHEAD_KEY"), ("data", "DATA_CUBE"), ("lock", "LOCKER"),
        ("cell", "CELL"), ("med", "MEDIPACK"), ("dis", "DISINFECT_PACK"), ("his", "HISEC_CARGO"),
        ("nfr", "NFRAME"), ("gen", "GENERATOR"), ("sec", "SEC_DOOR"), ("key", "KEY"),
        ("am", "AMMOPACK"), ("to", "TOOL_REFILL"), ("bk", "BULKHEAD_KEY"), ("bd", "BULKHEAD_DC"),
        ("dc", "DATA_CUBE"), ("sd", "SEC_DOOR"),
    };

    private class TermState
    {
        public readonly List<string> History = new();
        public string GrepPattern;
        public bool GrepInvert;
        public float GrepUntil;
        public string SearchQuery;
        public int SearchIndex = -1;
        public string LastSearchResult;
    }

    private static readonly Dictionary<uint, TermState> _states = new();
    private static float _levelStart;

    private static TermState StateOf(LG_ComputerTerminal t)
    {
        if (!_states.TryGetValue(t.SyncID, out var s)) _states[t.SyncID] = s = new TermState();
        return s;
    }

    protected override void OnEnable()
    {
        FrameTicker.OnLevelExit += () => _states.Clear();
        FrameTicker.OnUpdate += () =>
        {
            if (!GameUtil.InLevel) _levelStart = 0f;
            else if (_levelStart == 0f) _levelStart = Time.time;
        };
    }

    private static void Print(LG_ComputerTerminal t, string line) =>
        t.m_command.AddOutput(TerminalLineType.Normal, line, 0f, TerminalSoundType.LineTypeDefault, TerminalSoundType.LineTypeDefault);

    private static void SetLine(LG_ComputerTerminal t, string line)
    {
        t.m_currentLine = line ?? "";
        t.m_caretBlinkOffsetFromEnd = 0;
    }

    // ---------- горячие клавиши ----------

    [HarmonyPatch(typeof(LG_TERM_PlayerInteracting), nameof(LG_TERM_PlayerInteracting.Update))]
    private static class Patch_Update
    {
        private static void Prefix(LG_TERM_PlayerInteracting __instance)
        {
            var t = __instance.m_terminal;
            if (t == null) return;
            if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) return;
            var st = StateOf(t);

            if (Input.GetKeyDown(KeyCode.L)) t.m_command.ClearOutputQueueAndScreenBuffer();
            else if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.U)) { SetLine(t, ""); st.SearchQuery = null; }
            else if (Input.GetKeyDown(KeyCode.V)) Paste(t);
            else if (Input.GetKeyDown(KeyCode.R)) ReverseSearch(t, st);
        }
    }

    private static void Paste(LG_ComputerTerminal t)
    {
        string clip = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(clip)) return;
        clip = clip.Split('\n', '\r')[0].ToUpperInvariant();
        var sb = new StringBuilder();
        foreach (char c in clip)
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || " _-*?!.|".IndexOf(c) >= 0) sb.Append(c);
        string line = t.m_currentLine ?? "";
        int pos = Mathf.Clamp(line.Length - t.m_caretBlinkOffsetFromEnd, 0, line.Length);
        t.m_currentLine = line.Insert(pos, sb.ToString());
    }

    private static void ReverseSearch(LG_ComputerTerminal t, TermState st)
    {
        string cur = t.m_currentLine ?? "";
        if (st.SearchQuery == null || cur != st.LastSearchResult) { st.SearchQuery = cur; st.SearchIndex = st.History.Count; }
        for (int i = st.SearchIndex - 1; i >= 0; i--)
        {
            if (st.History[i].IndexOf(st.SearchQuery, StringComparison.OrdinalIgnoreCase) < 0) continue;
            st.SearchIndex = i;
            st.LastSearchResult = st.History[i];
            SetLine(t, st.History[i]);
            return;
        }
        st.SearchIndex = st.History.Count; // по кругу
    }

    // ---------- Enter: история, пайпы, локальные команды ----------

    [HarmonyPatch(typeof(LG_TERM_PlayerInteracting), nameof(LG_TERM_PlayerInteracting.OnReturn))]
    [HarmonyPriority(Priority.High)]
    private static class Patch_OnReturn
    {
        private static bool Prefix(LG_TERM_PlayerInteracting __instance)
        {
            var t = __instance.m_terminal;
            if (t == null) return true;
            var st = StateOf(t);
            st.GrepPattern = null;
            st.SearchQuery = null;

            string line = (t.m_currentLine ?? "").Trim();
            if (line.Length == 0) return true;

            // История: !!, !N, !prefix
            if (line.StartsWith("!"))
            {
                string found = ExpandHistory(st, line.Substring(1));
                if (found == null) { Print(t, "> " + line); Print(t, Lang.T("event not found", "нет такой команды в истории")); SetLine(t, ""); return false; }
                line = found;
            }

            st.History.Add(line);
            if (st.History.Count > 200) st.History.RemoveAt(0);

            // Пайп: «команда | grep X» / «| grep -v X»
            string cmd = line;
            int bar = line.IndexOf('|');
            if (bar >= 0)
            {
                cmd = line.Substring(0, bar).Trim();
                var m = Regex.Match(line.Substring(bar + 1), @"^\s*grep\s+(-v\s+)?(.+)$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    st.GrepInvert = m.Groups[1].Success;
                    st.GrepPattern = m.Groups[2].Value.Trim();
                    st.GrepUntil = Time.time + 20f;
                }
            }

            if (TryLocalCommand(t, st, cmd)) { SetLine(t, ""); return false; }

            t.m_currentLine = cmd;
            return true;
        }
    }

    private static string ExpandHistory(TermState st, string arg)
    {
        if (st.History.Count == 0) return null;
        if (arg == "!") return st.History[^1];
        if (int.TryParse(arg, out int n)) return n >= 1 && n <= st.History.Count ? st.History[n - 1] : null;
        for (int i = st.History.Count - 1; i >= 0; i--)
            if (st.History[i].StartsWith(arg, StringComparison.OrdinalIgnoreCase)) return st.History[i];
        return null;
    }

    private static bool TryLocalCommand(LG_ComputerTerminal t, TermState st, string cmd)
    {
        var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string head = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";
        string rest = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : "";
        switch (head)
        {
            case "history":
            case "hist":
                Print(t, "> " + cmd);
                int from = Math.Max(0, st.History.Count - 20);
                for (int i = from; i < st.History.Count; i++) Print(t, $"{i + 1,4}  {st.History[i]}");
                return true;
            case "whoami":
                Print(t, "> " + cmd);
                Print(t, SNet.HasLocalPlayer ? SNet.LocalPlayer.NickName : "prisoner");
                return true;
            case "echo":
                Print(t, "> " + cmd);
                Print(t, rest);
                return true;
            case "uname":
                Print(t, "> " + cmd);
                Print(t, $"WARDEN-OS 4.20 tty{t.SyncID} (GtfoPunk QoL {ModInfo.Version})");
                return true;
            case "uptime":
                Print(t, "> " + cmd);
                float s = _levelStart > 0 ? Time.time - _levelStart : 0f;
                Print(t, $"up {(int)(s / 60):00}:{(int)(s % 60):00}");
                return true;
            default:
                return false;
        }
    }

    // ---------- алиасы и сокращения (до разбора команды игрой) ----------

    [HarmonyPatch(typeof(LG_ComputerTerminalCommandInterpreter), nameof(LG_ComputerTerminalCommandInterpreter.EvaluateInput))]
    private static class Patch_Evaluate
    {
        private static void Prefix(ref string inputString)
        {
            if (string.IsNullOrWhiteSpace(inputString)) return;
            var parts = inputString.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 0) return;

            if (_aliases.Value && CmdAlias.TryGetValue(parts[0], out var full))
            {
                parts.RemoveAt(0);
                parts.InsertRange(0, full.Split(' '));
            }

            string c = parts[0].ToUpperInvariant();
            if (_expand.Value && (c == "PING" || c == "QUERY") && parts.Count >= 2)
            {
                var args = parts.Skip(1).ToList();
                var flags = args.Where(a => a.StartsWith("-")).ToList();
                var words = args.Where(a => !a.StartsWith("-")).ToList();
                if (words.Count >= 1)
                {
                    words[0] = ExpandObj(words[0]);
                    string obj = string.Join("_", words);
                    parts = new List<string> { c, obj };
                    parts.AddRange(flags);
                }
            }

            inputString = string.Join(" ", parts).ToUpperInvariant();
        }
    }

    private static string ExpandObj(string w)
    {
        if (ObjExact.TryGetValue(w, out var e)) return e;
        if (w.Contains('_')) return w; // уже полное имя
        foreach (var (pre, full) in ObjPrefix)
            if (w.StartsWith(pre, StringComparison.OrdinalIgnoreCase) && full.StartsWith(w, StringComparison.OrdinalIgnoreCase) || w.Equals(pre, StringComparison.OrdinalIgnoreCase))
                return full;
        foreach (var (pre, full) in ObjPrefix)
            if (w.StartsWith(pre, StringComparison.OrdinalIgnoreCase)) return full;
        return w;
    }

    // ---------- grep: фильтр вывода ----------

    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);

    private static bool Keep(LG_ComputerTerminalCommandInterpreter interp, string line)
    {
        var t = interp?.m_terminal;
        if (t == null || !_states.TryGetValue(t.SyncID, out var st) || st.GrepPattern == null) return true;
        if (Time.time > st.GrepUntil) { st.GrepPattern = null; return true; }
        if (string.IsNullOrWhiteSpace(line)) return false;
        bool match = Tags.Replace(line, "").IndexOf(st.GrepPattern, StringComparison.OrdinalIgnoreCase) >= 0;
        return st.GrepInvert ? !match : match;
    }

    [HarmonyPatch(typeof(LG_ComputerTerminalCommandInterpreter), nameof(LG_ComputerTerminalCommandInterpreter.AddOutput),
        new[] { typeof(TerminalLineType), typeof(string), typeof(float), typeof(TerminalSoundType), typeof(TerminalSoundType) })]
    private static class Patch_AddOutputTyped
    {
        private static bool Prefix(LG_ComputerTerminalCommandInterpreter __instance, string line) => Keep(__instance, line);
    }

    [HarmonyPatch(typeof(LG_ComputerTerminalCommandInterpreter), nameof(LG_ComputerTerminalCommandInterpreter.AddOutput),
        new[] { typeof(string), typeof(bool) })]
    private static class Patch_AddOutputPlain
    {
        private static bool Prefix(LG_ComputerTerminalCommandInterpreter __instance, string originalLine) => Keep(__instance, originalLine);
    }
}
