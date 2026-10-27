using System;
using System.Linq;
using System.Text.RegularExpressions;
using CellMenu;
using GameData;
using GtfoPunk.QoL.Core;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// ARCHIVE button on the rundown screen (a copy of the CREDITS button): opens a terminal with the terminal logs of the
/// expeditions you've completed in the current rundown — read (and listen to) them again without replaying.
/// Typed like the game's terminal: LIST [R4 | R4A1] (code, name, log ID, audio), READ id (the start is enough, Tab
/// completes), PLAY / STOP (audio logs), CLS, HELP, EXIT. ↑/↓ — history, PgUp/PgDn / wheel — scroll, Esc — close.
/// Covers every rundown you've opened on the rundown screen (finished expeditions are remembered in a file).
/// Client-side, reads only the game data and what the rundown screen shows.
/// </summary>
public class TerminalArchive : ModuleBase
{
    public override string Id => "TerminalArchive";
    public override string Description => "ARCHIVE button on the rundown screen: read the terminal logs of the expeditions you've completed.";
    public override ModuleSide Side => ModuleSide.Client;

    private static ConfigEntry<bool> _showAll, _otherLayers;
    private static ConfigEntry<string> _placement;
    private static ConfigEntry<float> _fontScale;

    protected override void OnBindConfig()
    {
        _showAll = Bind("ShowUncompleted", false, "Also open the logs of expeditions you haven't completed (spoilers!).");
        _otherLayers = Bind("SecondaryLayers", true, "Also list the logs of the secondary / overload layers of a completed expedition.");
        _placement = Bind("ButtonPlacement", "Auto", "Where the ARCHIVE button goes relative to CREDITS: Auto, Right, Left, Above, Below.");
        _fontScale = Bind("FontScale", 1f, "Text size on the archive screen.");
    }

    protected override void OnEnable()
    {
        FrameTicker.OnUpdate += Tick;
    }

    // ---------- data ----------

    private class LogFile { public string Name, Text, Layer; public bool Audio; public uint AudioId; }

    private class Exp
    {
        public string Code, Name;
        public bool Done;
        public uint Rundown; public int Tier, Index;
        public readonly List<LogFile> Logs = new();
        public bool Open => Done || _showAll.Value;
    }

    private static readonly List<Exp> _exps = new();
    private static readonly Regex Tags = new("<[^>]*>");
    private static readonly Regex FullCode = new(@"^R\d+[A-E]\d", RegexOptions.IgnoreCase);

    // ---------- completed expeditions of every rundown (remembered as you browse the rundowns) ----------

    /// <summary>
    /// The game shows "finished" only for the rundown on screen, so what you've seen finished is remembered in
    /// BepInEx/config/GtfoPunk.Archive.txt: rundown block, tier, index, code, name. The archive lists every
    /// remembered rundown — open a rundown once to add it.
    /// </summary>
    private class Done { public uint Rundown; public int Tier, Index; public string Code, Name; }

    private static readonly Dictionary<string, Done> _done = new();
    private static bool _doneLoaded;
    private static string DonePath => System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "GtfoPunk.Archive.txt");

    private static void LoadDone()
    {
        if (_doneLoaded) return;
        _doneLoaded = true;
        try
        {
            if (!System.IO.File.Exists(DonePath)) return;
            foreach (var line in System.IO.File.ReadAllLines(DonePath))
            {
                var f = line.Split('\t');
                if (f.Length < 5 || !uint.TryParse(f[0], out var r) || !int.TryParse(f[1], out var t) || !int.TryParse(f[2], out var i)) continue;
                _done[$"{r}:{t}:{i}"] = new Done { Rundown = r, Tier = t, Index = i, Code = f[3], Name = f[4] };
            }
        }
        catch (Exception e) { ModInfo.Log.LogWarning($"[Archive] {DonePath}: {e.Message}"); }
    }

    private static void SaveDone()
    {
        try { System.IO.File.WriteAllLines(DonePath, _done.Values.OrderBy(d => d.Rundown).ThenBy(d => d.Tier).ThenBy(d => d.Index).Select(d => $"{d.Rundown}\t{d.Tier}\t{d.Index}\t{d.Code}\t{d.Name}")); }
        catch (Exception e) { ModInfo.Log.LogWarning($"[Archive] {DonePath}: {e.Message}"); }
    }

    /// <summary>Remembers the finished expeditions of the rundown on screen; returns the screen's other expeditions.</summary>
    private static List<(Exp, ExpeditionInTierData)> ReadScreen(CM_PageRundown_New page)
    {
        var rest = new List<(Exp, ExpeditionInTierData)>();
        var rd = page?.m_currentRundownData;
        if (rd == null) return rest;
        var seen = new HashSet<IntPtr>();
        bool changed = false;
        foreach (var list in new[] { page.m_expIconsAll, page.m_expIconsTier1, page.m_expIconsTier2, page.m_expIconsTier3, page.m_expIconsTier4, page.m_expIconsTier5, page.m_expIconsTierExt })
        {
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
            {
                var icon = list[i];
                var d = icon?.DataBlock;
                if (d == null || !seen.Add(d.Pointer)) continue;
                string shortName = Tags.Replace(icon.ShortName ?? "", "").Trim();
                string code = FullCode.IsMatch(shortName) ? shortName : (d.Descriptive?.Prefix ?? "") + icon.Tier.ToString().Replace("Tier", "") + (icon.ExpIndex + 1);
                string name = Tags.Replace(icon.PublicName ?? d.Descriptive?.PublicName ?? "?", "").Trim();
                var st = icon.Status;
                if (st == eExpeditionIconStatus.PlayedAndFinished || st == eExpeditionIconStatus.TierLockedFinishedAnyway)
                {
                    string key = $"{rd.persistentID}:{(int)icon.Tier}:{icon.ExpIndex}";
                    if (!_done.ContainsKey(key)) { _done[key] = new Done { Rundown = rd.persistentID, Tier = (int)icon.Tier, Index = icon.ExpIndex, Code = code, Name = name }; changed = true; }
                }
                else rest.Add((new Exp { Code = code, Name = name, Rundown = rd.persistentID, Tier = (int)icon.Tier, Index = icon.ExpIndex }, d));
            }
        }
        if (changed) SaveDone();
        return rest;
    }

    private static ExpeditionInTierData ExpData(uint rundown, int tier, int index)
    {
        var rd = GameDataBlockBase<RundownDataBlock>.GetBlock(rundown);
        if (rd == null) return null;
        var list = tier switch { 1 => rd.TierA, 2 => rd.TierB, 3 => rd.TierC, 4 => rd.TierD, 5 => rd.TierE, _ => null };
        return list != null && index >= 0 && index < list.Count ? list[index] : null;
    }

    private static void Build(CM_PageRundown_New page)
    {
        _exps.Clear();
        LoadDone();
        var rest = ReadScreen(page);
        foreach (var d in _done.Values.OrderBy(d => d.Rundown).ThenBy(d => d.Tier).ThenBy(d => d.Index))
        {
            var data = ExpData(d.Rundown, d.Tier, d.Index);
            if (data == null) continue;
            var exp = new Exp { Code = d.Code, Name = d.Name, Done = true, Rundown = d.Rundown, Tier = d.Tier, Index = d.Index };
            AddLogs(exp, data);
            _exps.Add(exp);
        }
        if (_showAll.Value) foreach (var (exp, data) in rest) { AddLogs(exp, data); _exps.Add(exp); }
        ModInfo.Log.LogInfo($"[Archive] completed expeditions remembered: {_done.Count} in {_done.Values.Select(d => d.Rundown).Distinct().Count()} rundowns; "
            + $"logs open: {_exps.Where(e => e.Open).Sum(e => e.Logs.Count)}; " + string.Join(", ", _exps.Select(e => $"{e.Code}:{e.Logs.Count}")));
    }

    private static void AddLogs(Exp exp, ExpeditionInTierData d)
    {
        AddLayout(exp, d.LevelLayoutData, "MAIN");
        if (!_otherLayers.Value) return;
        if (d.SecondaryLayerEnabled) AddLayout(exp, d.SecondaryLayout, "SECONDARY");
        if (d.ThirdLayerEnabled) AddLayout(exp, d.ThirdLayout, "OVERLOAD");
    }

    private static void AddLayout(Exp exp, uint layoutId, string layer)
    {
        if (layoutId == 0) return;
        var layout = GameDataBlockBase<LevelLayoutDataBlock>.GetBlock(layoutId);
        if (layout?.Zones == null) return;
        foreach (var z in layout.Zones)
        {
            if (z == null) continue;
            if (z.TerminalPlacements != null)
                foreach (var t in z.TerminalPlacements) if (t?.LocalLogFiles != null) foreach (var f in t.LocalLogFiles) AddLog(exp, f, layer);
            if (z.SpecificTerminalSpawnDatas != null)
                foreach (var t in z.SpecificTerminalSpawnDatas) if (t?.LocalLogFiles != null) foreach (var f in t.LocalLogFiles) AddLog(exp, f, layer);
        }
    }

    private static void AddLog(Exp exp, TerminalLogFileData f, string layer)
    {
        if (f == null || string.IsNullOrWhiteSpace(f.FileName)) return;
        if (exp.Logs.Any(l => string.Equals(l.Name, f.FileName, StringComparison.OrdinalIgnoreCase))) return;
        string text = null;
        try { var c = f.FileContent; text = c?.ToString(); if (string.IsNullOrWhiteSpace(text)) text = c?.UntranslatedText; } catch { }
        text = Tags.Replace(text ?? "", "").Replace("\r", "");
        exp.Logs.Add(new LogFile { Name = f.FileName.Trim(), Text = string.IsNullOrWhiteSpace(text) ? Lang.T("(no text)", "(без текста)") : text, Layer = layer, Audio = f.AttachedAudioFile != 0, AudioId = f.AttachedAudioFile });
    }

    // ---------- the button ----------

    private static GameObject _btn;
    private static CM_Item _btnItem;
    private static CM_PageRundown_New _btnPage;
    private static float _nextTry;
    private static bool _open;
    private static int _openedFrame;

    private static CM_PageRundown_New Page()
    {
        try
        {
            var page = MainMenuGuiLayer.Current?.PageRundownNew;
            return page != null && page.m_isActive ? page : null;
        }
        catch { return null; }
    }

    private static float _readySince = -1f, _nextSync;

    private static void Tick()
    {
        var page = Page();
        if (page == null) { if (_open) Close(); _readySince = -1f; return; }
        EnsureButton(page);
        if (_btn != null && Time.time >= _nextSync)
        {
            _nextSync = Time.time + 0.5f;
            // Shown only while CREDITS is (the game hides it during the intro and the rundown reveal).
            var src = page.m_creditsButton;
            bool show = src != null && src.gameObject.activeSelf;
            if (_btn.activeSelf != show) _btn.SetActive(show);
            if (show && _btnItem != null && _btnItem.m_texts != null && _btnItem.m_texts.Length > 0)
            {
                string label = Lang.T("ARCHIVE", "АРХИВ");
                var t = _btnItem.m_texts[0];
                if (t != null && t.text != label) t.SetText(label);
            }
        }
        if (_open) Input_(page);
    }

    /// <summary>The rundown screen is fully up: CREDITS visible and the expedition icons placed, for a couple of seconds.</summary>
    private static bool Ready(CM_PageRundown_New page)
    {
        bool now = false;
        try
        {
            var src = page.m_creditsButton;
            now = page.m_dataIsSetup && src != null && src.gameObject.activeInHierarchy
                && page.m_expIconsAll != null && page.m_expIconsAll.Count > 0
                && LocalBox(src.gameObject, src.transform.parent).size != Vector3.zero;
        }
        catch { }
        if (!now) { _readySince = -1f; return false; }
        if (_readySince < 0f) _readySince = Time.time;
        return Time.time - _readySince >= 2f;
    }

    private static void EnsureButton(CM_PageRundown_New page)
    {
        if (_btn != null && _btnPage == page) return;
        if (Time.time < _nextTry) return;
        _nextTry = Time.time + 0.5f;
        if (!Ready(page)) return;
        var src = page.m_creditsButton;
        if (_btn != null) UnityEngine.Object.Destroy(_btn);
        var parent = src.transform.parent;
        _btn = UnityEngine.Object.Instantiate(src.gameObject, parent, false);
        _btn.name = "GtfoPunk_ArchiveButton";
        _btnItem = _btn.GetComponent<CM_Item>();
        if (_btnItem == null) { UnityEngine.Object.Destroy(_btn); _btn = null; return; }
        try { _btnItem.SetupCMItem(); } catch { }
        _btnItem.SetOnBtnPressCallback(DelegateSupport.ConvertDelegate<Il2CppSystem.Action<int>>(new Action<int>(_ => Toggle())));
        Place(src.gameObject, parent, page);
        _btnPage = page;
        ModInfo.Log.LogInfo($"[Archive] ARCHIVE button made (copy of '{src.name}')");
    }

    /// <summary>Next to CREDITS: the first side (or the configured one) that doesn't cover another button.</summary>
    private static void Place(GameObject src, Transform parent, CM_PageRundown_New page)
    {
        var a = LocalBox(src, parent);
        if (a.size == Vector3.zero) return;
        var others = new List<Bounds>();
        foreach (var it in page.GetComponentsInChildren<CM_Item>(false))
            if (it != null && it.gameObject != _btn && it.gameObject != src) { var b = LocalBox(it.gameObject, parent); if (b.size != Vector3.zero) others.Add(b); }
        float gx = a.size.y * 0.4f, gy = a.size.y * 0.3f;
        var sides = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase)
        {
            ["Above"] = new(0f, a.size.y + gy, 0f), ["Below"] = new(0f, -(a.size.y + gy), 0f),
            ["Right"] = new(a.size.x + gx, 0f, 0f), ["Left"] = new(-(a.size.x + gx), 0f, 0f),
        };
        Vector3 shift = sides["Above"];
        if (sides.TryGetValue(_placement.Value?.Trim() ?? "", out var forced)) shift = forced;
        else
            foreach (var s in sides.Values)
            {
                var b = new Bounds(a.center + s, a.size * 0.95f);
                if (!others.Any(o => o.Intersects(b))) { shift = s; break; }
            }
        _btn.transform.localPosition = src.transform.localPosition + shift;
    }

    private static Bounds LocalBox(GameObject go, Transform parent)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var r in go.GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled) continue;
            foreach (var corner in new[] { r.bounds.min, r.bounds.max })
            {
                var p = parent.InverseTransformPoint(corner);
                p.z = 0f;
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    // ---------- the terminal ----------

    private static void Toggle() { if (_open) Close(); else OpenArchive(); }

    private static readonly List<(string Text, Color Color)> _out = new();
    private static readonly List<string> _history = new();
    private static string _input = "";
    private static int _histPos, _scroll; // _scroll: lines up from the bottom

    private static void OpenArchive()
    {
        Build(_btnPage ?? Page());
        _open = true;
        _openedFrame = Time.frameCount;
        _out.Clear(); _input = ""; _scroll = 0; _lastTab = null;
        Print(Lang.T("WARDEN ARCHIVE TERMINAL — logs of the expeditions you've completed", "АРХИВНЫЙ ТЕРМИНАЛ СМОТРИТЕЛЯ — логи пройденных экспедиций"), Amber);
        Print("", Ink);
        Run("HELP", false);
        Print("", Ink);
        Run("LIST", false);
        Overlay.Ensure();
        Overlay.OnDraw -= Draw;
        Overlay.OnDraw += Draw;
        BlockPage(true);
    }

    private static void Print(string text, Color c)
    {
        foreach (var line in (text ?? "").Split('\n')) _out.Add((line, c));
        while (_out.Count > 3000) _out.RemoveAt(0);
        _scroll = 0;
        _wrapped = null;
    }

    private static IEnumerable<(Exp Exp, LogFile Log)> Files(string filter)
    {
        foreach (var e in _exps.Where(e => e.Open))
            if (filter == null || e.Code.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                foreach (var l in e.Logs) yield return (e, l);
    }

    /// <summary>A log by its ID, or by the start of it when that's unique.</summary>
    private static (Exp, LogFile) FindFile(string id)
    {
        id = id.Trim();
        var all = Files(null).ToList();
        var hit = all.FirstOrDefault(f => f.Log.Name.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (hit.Log != null) return hit;
        var pre = all.Where(f => f.Log.Name.StartsWith(id, StringComparison.OrdinalIgnoreCase)).GroupBy(f => f.Log.Name.ToUpperInvariant()).ToList();
        return pre.Count == 1 ? pre[0].First() : (null, null);
    }

    private static void Run(string line, bool echo = true)
    {
        line = (line ?? "").Trim();
        if (echo) Print("> " + line, Amber);
        if (line.Length == 0) return;
        if (echo && (_history.Count == 0 || _history[^1] != line)) _history.Add(line);
        _histPos = _history.Count;
        var parts = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        string cmd = parts[0].ToUpperInvariant(), arg = parts.Length > 1 ? parts[1].Trim() : "";
        switch (cmd)
        {
            case "HELP": case "MAN":
                Print(Lang.T(
                    "LIST — logs of all completed expeditions; LIST R4 — of a rundown; LIST R4A1 — of one\nREAD id — show a log (the start of the ID is enough; Tab completes)\nPLAY id — play an audio log; STOP — stop it\nCLS — clear the screen; EXIT or Esc — close\n↑/↓ — command history, PgUp/PgDn or the wheel — scroll\nA rundown gets here once you've opened it on the rundown screen.",
                    "LIST — логи всех пройденных экспедиций; LIST R4 — рандауна; LIST R4A1 — одной\nREAD id — показать лог (хватит начала ID; Tab дописывает)\nPLAY id — включить аудиолог; STOP — выключить\nCLS — очистить экран; EXIT или Esc — закрыть\n↑/↓ — история команд, PgUp/PgDn или колесо — прокрутка\nРандаун попадает сюда, когда ты открыл его на экране рандаунов."), Ink);
                break;
            case "LIST": case "LS": case "DIR": case "LOGS":
            {
                string filter = arg.Length > 0 ? arg.ToUpperInvariant() : null;
                var files = Files(filter).ToList();
                if (files.Count == 0)
                {
                    Print(filter == null ? Lang.T("No completed expeditions with logs yet (open a rundown on the rundown screen to add it).", "Пока нет пройденных экспедиций с логами (открой рандаун на экране рандаунов, чтобы он добавился).")
                                         : Lang.T($"Nothing for {filter}.", $"Для {filter} ничего нет."), Dim);
                    break;
                }
                Print(Lang.T("CODE\tNAME\tLOG ID\tAUDIO", "КОД\tНАЗВАНИЕ\tID ЛОГА\tАУДИО"), Dim);
                foreach (var (e, l) in files)
                    Print($"{e.Code}\t{e.Name.ToUpperInvariant()}\t{l.Name}\t{(l.Audio ? Lang.T("yes", "да") : "")}", e.Done ? Ink : Dim);
                break;
            }
            case "READ": case "CAT": case "LESS": case "OPEN":
            {
                if (arg.Length == 0) { Print(Lang.T("READ which log? LIST shows them.", "READ какой лог? Список — LIST."), Dim); break; }
                var (e, l) = FindFile(arg);
                if (l == null) { Print(Lang.T($"No such log (or several start like that): {arg}", $"Нет такого лога (или их несколько): {arg}"), Dim); break; }
                Print($"——— {e.Code} {e.Name.ToUpperInvariant()} / {l.Name} ———", Amber);
                Print(l.Text, Ink);
                if (l.Audio) Print(Lang.T($"[audio log — PLAY {l.Name}]", $"[аудиолог — PLAY {l.Name}]"), Dim);
                Print("", Ink);
                break;
            }
            case "PLAY":
            {
                var (e, l) = FindFile(arg);
                if (l == null || !l.Audio) { Print(Lang.T("No such audio log.", "Нет такого аудиолога."), Dim); break; }
                PlayAudio(l.AudioId, l.Name);
                break;
            }
            case "STOP": StopAudio(); Print(Lang.T("Stopped.", "Остановлено."), Dim); break;
            case "CLS": case "CLEAR": _out.Clear(); _wrapped = null; break;
            case "EXIT": case "QUIT": case "LOGOFF": Close(); break;
            default: Print(Lang.T($"Unknown command: {cmd}. HELP — the list.", $"Неизвестная команда: {cmd}. HELP — список."), Dim); break;
        }
    }

    private static string _lastTab;

    /// <summary>Tab after READ / PLAY: completes the log ID (the common part if there are several; the choices once).</summary>
    private static void Complete()
    {
        var m = Regex.Match(_input, @"^\s*(READ|CAT|LESS|OPEN|PLAY)\s+(\S*)$", RegexOptions.IgnoreCase);
        if (!m.Success) return;
        string pre = m.Groups[2].Value;
        var names = Files(null).Select(f => f.Log.Name).Where(n => n.StartsWith(pre, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0) return;
        string common = names[0];
        foreach (var n in names.Skip(1))
        {
            int k = 0;
            while (k < common.Length && k < n.Length && char.ToUpperInvariant(common[k]) == char.ToUpperInvariant(n[k])) k++;
            common = common.Substring(0, k);
        }
        if (names.Count > 1 && common.Length <= pre.Length)
        {
            if (_lastTab != _input) Print(string.Join("   ", names), Dim); // the choices once, not on every Tab
            _lastTab = _input;
            return;
        }
        _input = _input.Substring(0, m.Groups[2].Index) + common;
        _lastTab = null;
    }

    // ---------- audio logs ----------

    private static CellSoundPlayer _audio;
    private static bool _playing;

    private static void PlayAudio(uint id, string name)
    {
        StopAudio();
        try
        {
            _audio ??= new CellSoundPlayer();
            _audio.Post(id, true);
            _playing = true;
            Print(Lang.T($"Playing {name}… (STOP — stop)", $"Играет {name}… (STOP — стоп)"), Dim);
            ModInfo.Log.LogInfo($"[Archive] playing the audio log {name} (event {id})");
        }
        catch (Exception e) { Print(Lang.T("Can't play it here.", "Здесь не воспроизводится."), Dim); ModInfo.Log.LogWarning($"[Archive] audio log {id}: {e.Message}"); }
    }

    private static void StopAudio()
    {
        if (!_playing) return;
        _playing = false;
        try { _audio?.Stop(); } catch { }
    }

    private static void Close()
    {
        StopAudio();
        _open = false;
        Overlay.OnDraw -= Draw;
        BlockPage(false);
    }

    private static readonly List<Collider> _off3d = new();
    private static readonly List<Collider2D> _off2d = new();

    /// <summary>While the archive is open the rundown screen's buttons can't be hit (their colliders are off), except ARCHIVE.</summary>
    private static void BlockPage(bool block)
    {
        try
        {
            if (!block)
            {
                foreach (var c in _off3d) if (c != null) c.enabled = true;
                foreach (var c in _off2d) if (c != null) c.enabled = true;
                _off3d.Clear(); _off2d.Clear();
                return;
            }
            var page = _btnPage ?? Page();
            if (page == null) return;
            foreach (var c in page.GetComponentsInChildren<Collider>(false))
                if (c != null && c.enabled && (_btn == null || !c.transform.IsChildOf(_btn.transform))) { c.enabled = false; _off3d.Add(c); }
            foreach (var c in page.GetComponentsInChildren<Collider2D>(false))
                if (c != null && c.enabled && (_btn == null || !c.transform.IsChildOf(_btn.transform))) { c.enabled = false; _off2d.Add(c); }
            ModInfo.Log.LogInfo($"[Archive] rundown screen buttons paused: {_off3d.Count + _off2d.Count}");
        }
        catch (Exception e) { ModInfo.Log.LogWarning($"[Archive] pause buttons: {e.Message}"); }
    }

    // ---------- keyboard ----------

    private static void Input_(CM_PageRundown_New page)
    {
        if (Time.frameCount == _openedFrame) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        var L = GetLayout();
        int pageLines = Math.Max(1, L.Rows - 2);
        if (Input.GetKeyDown(KeyCode.PageUp)) _scroll += pageLines;
        if (Input.GetKeyDown(KeyCode.PageDown)) _scroll -= pageLines;
        float wheel = Input.mouseScrollDelta.y;
        if (Math.Abs(wheel) > 0.01f) _scroll += (int)Math.Round(wheel * 3f);
        if (Input.GetKeyDown(KeyCode.UpArrow) && _history.Count > 0) { _histPos = Math.Max(0, _histPos - 1); _input = _history[_histPos]; }
        if (Input.GetKeyDown(KeyCode.DownArrow) && _history.Count > 0) { _histPos = Math.Min(_history.Count, _histPos + 1); _input = _histPos < _history.Count ? _history[_histPos] : ""; }
        if (Input.GetKeyDown(KeyCode.Tab)) Complete();
        foreach (char ch in Input.inputString)
        {
            if (ch == '\b') { if (_input.Length > 0) _input = _input.Substring(0, _input.Length - 1); }
            else if (ch == '\n' || ch == '\r') { string line = _input; _input = ""; Run(line); if (!_open) return; }
            else if (ch == '\t') { }
            else if (!char.IsControl(ch) && _input.Length < 120) _input += ch;
        }
    }

    // ---------- drawing ----------

    private struct Layout { public Rect Panel, Body; public float Line; public int Rows, Font; }

    private static Layout GetLayout()
    {
        float W = Screen.width, H = Screen.height;
        float h = H * 0.8f, w = Math.Min(W * 0.92f, h * 1.4f);
        var panel = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h);
        int font = Math.Max(10, (int)(H * 0.019f * Mathf.Clamp(_fontScale.Value, 0.5f, 2.5f)));
        float line = font * 1.4f;
        var body = new Rect(panel.x + font * 1.5f, panel.y + line, panel.width - font * 3f, panel.height - line * 3.2f);
        return new Layout { Panel = panel, Body = body, Line = line, Rows = Math.Max(1, (int)(body.height / line)), Font = font };
    }

    private static Texture2D _white;
    private static GUIStyle _style;

    /// <summary>A console font from the system (Consolas, Lucida Console, Courier New); the default one if none.</summary>
    private static Font ConsoleFont(int size)
    {
        Font f = null;
        try { f = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Lucida Console", "Courier New" }, size); }
        catch (Exception e) { ModInfo.Log.LogWarning($"[Archive] console font: {e.Message}"); }
        ModInfo.Log.LogInfo($"[Archive] font: {(f != null ? f.name : "default")}");
        return f;
    }

    private static void Rect_(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, _white); }

    private static void Frame(Rect r, Color c, float t = 1f)
    {
        Rect_(new Rect(r.x, r.y, r.width, t), c); Rect_(new Rect(r.x, r.yMax - t, r.width, t), c);
        Rect_(new Rect(r.x, r.y, t, r.height), c); Rect_(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    private static readonly Color Ink = new(0.8f, 0.92f, 0.95f, 1f), Dim = new(0.5f, 0.6f, 0.64f, 1f), Amber = new(1f, 0.72f, 0.3f, 1f);

    private static void Text(float x, float y, float w, float h, string s, Color c) { GUI.color = c; GUI.Label(new Rect(x, y, w, h), s, _style); }

    private static List<(string, Color)> _wrapped;
    private static float _wrapWidth;
    private static int _wrapFont;

    private static void Draw()
    {
        if (!_open) return;
        if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
        var L = GetLayout();
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip, richText = false };
            var f = ConsoleFont(L.Font);
            if (f != null) _style.font = f;
        }
        _style.fontSize = L.Font;
        var old = GUI.color;

        Rect_(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
        Rect_(L.Panel, new Color(0.02f, 0.04f, 0.05f, 0.97f));
        Frame(L.Panel, new Color(Ink.r, Ink.g, Ink.b, 0.5f), 2f);

        if (_wrapped == null || _wrapWidth != L.Body.width || _wrapFont != L.Font)
        {
            _wrapped = new List<(string, Color)>();
            foreach (var (t, c) in _out)
                if (t.IndexOf('\t') >= 0) _wrapped.Add((t, c)); // table rows aren't wrapped
                else foreach (var w in Wrap(t, L.Body.width)) _wrapped.Add((w, c));
            _wrapWidth = L.Body.width; _wrapFont = L.Font;
        }
        int rows = L.Rows;
        int maxScroll = Math.Max(0, _wrapped.Count - rows);
        _scroll = Mathf.Clamp(_scroll, 0, maxScroll);
        int first = Math.Max(0, _wrapped.Count - rows - _scroll);
        for (int i = 0; i < rows && first + i < _wrapped.Count; i++)
        {
            var (t, c) = _wrapped[first + i];
            float y = L.Body.y + i * L.Line;
            if (t.IndexOf('\t') < 0) { Text(L.Body.x, y, L.Body.width, L.Line, t, c); continue; }
            // A table row: columns at fixed places.
            var cols = t.Split('\t');
            float[] at = { 0f, 0.09f, 0.42f, 0.8f };
            for (int k = 0; k < cols.Length && k < at.Length; k++)
            {
                float x0 = L.Body.x + L.Body.width * at[k], x1 = k + 1 < at.Length ? L.Body.x + L.Body.width * at[k + 1] : L.Body.xMax;
                Text(x0, y, x1 - x0 - 6f, L.Line, cols[k], c);
            }
        }
        if (_wrapped.Count > rows)
        {
            var track = new Rect(L.Panel.xMax - L.Font * 0.8f, L.Body.y, 3f, rows * L.Line);
            Rect_(track, new Color(Ink.r, Ink.g, Ink.b, 0.15f));
            float k = (float)rows / _wrapped.Count, pos = 1f - (float)_scroll / Math.Max(1, maxScroll);
            Rect_(new Rect(track.x - 1f, track.y + (track.height - track.height * k) * pos, 5f, track.height * k), new Color(Ink.r, Ink.g, Ink.b, 0.6f));
        }

        // The prompt.
        float py = L.Panel.yMax - L.Line * 1.8f;
        Rect_(new Rect(L.Body.x - 4f, py - 3f, L.Body.width + 8f, 1f), new Color(Ink.r, Ink.g, Ink.b, 0.3f));
        bool blink = ((int)(Time.unscaledTime * 2f) & 1) == 0;
        Text(L.Body.x, py, L.Body.width, L.Line, "\\\\ROOT\\ARCHIVE> " + _input + (blink ? "_" : ""), Amber);
        if (_scroll > 0) Text(L.Body.x + L.Body.width * 0.7f, py, L.Body.width * 0.3f, L.Line, Lang.T("↓ PgDn — back to the end", "↓ PgDn — к концу"), Dim);
        GUI.color = old;
    }

    /// <summary>Word wrap by the measured width.</summary>
    private static List<string> Wrap(string text, float width)
    {
        var res = new List<string>();
        if (string.IsNullOrEmpty(text)) { res.Add(""); return res; }
        var line = "";
        foreach (var word in text.Split(' '))
        {
            string next = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && _style.CalcSize(new GUIContent(next)).x > width) { res.Add(line); line = word; }
            else line = next;
        }
        res.Add(line);
        return res;
    }
}
