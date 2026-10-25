using System;
using System.Linq;
using System.Text.RegularExpressions;
using CellMenu;
using DropServer;
using GameData;
using GtfoPunk.QoL.Core;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// ARCHIVE button on the rundown screen (a copy of the CREDITS button): opens a terminal-like screen with the
/// terminal logs of the expeditions you've completed in the current rundown — read them again without replaying.
/// Mouse (the game cursor): click a line to open it, right click / Backspace — back, wheel — scroll, Esc — close.
/// Keyboard: ↑/↓, Enter. Client-side, reads only the game data and your own progression.
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
        _otherLayers = Bind("SecondaryLayersEnteredOnly", true, "Logs of the secondary / overload layers only once you've entered that layer.");
        _placement = Bind("ButtonPlacement", "Auto", "Where the ARCHIVE button goes relative to CREDITS: Auto, Right, Left, Above, Below.");
        _fontScale = Bind("FontScale", 1f, "Text size on the archive screen.");
    }

    protected override void OnEnable()
    {
        FrameTicker.OnUpdate += Tick;
    }

    // ---------- data ----------

    private class LogFile { public string Name, Text, Layer; public bool Audio; }

    private class Exp
    {
        public string Code, Name;
        public bool Done;
        public readonly List<LogFile> Logs = new();
        public bool Open => Done || _showAll.Value;
    }

    private static readonly List<Exp> _exps = new();
    private static readonly Regex Tags = new("<[^>]*>");

    private static void Build(CM_PageRundown_New page)
    {
        _exps.Clear();
        var rd = page?.m_currentRundownData;
        if (rd == null)
        {
            try
            {
                var key = RundownManager.ActiveRundownKey ?? "";
                var m = Regex.Match(key, @"(\d+)\s*$");
                if (m.Success) rd = GameDataBlockBase<RundownDataBlock>.GetBlock(uint.Parse(m.Groups[1].Value));
            }
            catch { }
        }
        if (rd == null) { ModInfo.Log.LogWarning("[Archive] no rundown data"); return; }
        RundownProgression prog = null;
        try { prog = RundownManager.RundownProgression; } catch { }

        AddTier(rd.TierA, eRundownTier.TierA, "A", prog);
        AddTier(rd.TierB, eRundownTier.TierB, "B", prog);
        AddTier(rd.TierC, eRundownTier.TierC, "C", prog);
        AddTier(rd.TierD, eRundownTier.TierD, "D", prog);
        AddTier(rd.TierE, eRundownTier.TierE, "E", prog);
        ModInfo.Log.LogInfo($"[Archive] rundown '{rd.name}': {_exps.Count} expeditions, {_exps.Count(e => e.Done)} completed, "
            + $"{_exps.Where(e => e.Open).Sum(e => e.Logs.Count)} logs open (progression {(prog != null ? "read" : "not available")})");
    }

    private static void AddTier(Il2CppSystem.Collections.Generic.List<ExpeditionInTierData> list, eRundownTier tier, string letter, RundownProgression prog)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (d == null || !d.Enabled) continue;
            var desc = d.Descriptive;
            var exp = new Exp
            {
                Code = (desc?.Prefix ?? "") + letter + (desc != null && desc.SkipExpNumberInName ? "" : (i + 1).ToString()),
                Name = desc?.PublicName ?? "?",
            };
            LayerProgressionState main = LayerProgressionState.Undiscovered, second = main, third = main;
            try
            {
                string key = RundownManager.GetRundownProgressionExpeditionKey(tier, i);
                if (prog?.Expeditions != null && key != null && prog.Expeditions.ContainsKey(key))
                {
                    var e = prog.Expeditions[key];
                    var layers = e.Layers;
                    main = layers.Main.State; second = layers.Secondary.State; third = layers.Third.State;
                    exp.Done = main == LayerProgressionState.Completed || layers.Main.CompletionCount > 0;
                }
            }
            catch (Exception ex) { ModInfo.Log.LogWarning($"[Archive] progression of {exp.Code}: {ex.Message}"); }

            AddLayout(exp, d.LevelLayoutData, "MAIN");
            bool all = _showAll.Value || !_otherLayers.Value;
            if (d.SecondaryLayerEnabled && (all || second >= LayerProgressionState.Entered)) AddLayout(exp, d.SecondaryLayout, "SECONDARY");
            if (d.ThirdLayerEnabled && (all || third >= LayerProgressionState.Entered)) AddLayout(exp, d.ThirdLayout, "OVERLOAD");
            _exps.Add(exp);
        }
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
        exp.Logs.Add(new LogFile { Name = f.FileName.Trim(), Text = string.IsNullOrWhiteSpace(text) ? Lang.T("(no text)", "(без текста)") : text, Layer = layer, Audio = f.AttachedAudioFile != 0 });
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

    private static void Tick()
    {
        var page = Page();
        if (page == null) { if (_open) Close(); return; }
        EnsureButton(page);
        if (_btnItem != null && _btnItem.m_texts != null && _btnItem.m_texts.Length > 0)
        {
            string label = Lang.T("ARCHIVE", "АРХИВ");
            var t = _btnItem.m_texts[0];
            if (t != null && t.text != label) t.SetText(label);
        }
        if (_open) Input_(page);
    }

    private static void EnsureButton(CM_PageRundown_New page)
    {
        if (_btn != null && _btnPage == page) return;
        if (Time.time < _nextTry) return;
        _nextTry = Time.time + 3f;
        var src = page.m_creditsButton;
        if (src == null) { ModInfo.Log.LogWarning("[Archive] CREDITS button not found"); return; }
        if (_btn != null) UnityEngine.Object.Destroy(_btn);
        var parent = src.transform.parent;
        _btn = UnityEngine.Object.Instantiate(src.gameObject, parent, false);
        _btn.name = "GtfoPunk_ArchiveButton";
        _btn.SetActive(true);
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

    /// <summary>While the archive is open, the game's own buttons under it don't react to clicks.</summary>
    [HarmonyPatch(typeof(CM_Item), nameof(CM_Item.OnBtnPress))]
    private static class Patch_Press
    {
        private static bool Prefix(CM_Item __instance) => !_open || (__instance != null && __instance.gameObject == _btn);
    }

    // ---------- the screen: 0 — expeditions, 1 — logs of one, 2 — a log ----------

    private static int _level, _exp, _log, _sel, _scroll, _hover = -1;
    private static List<string> _lines = new();
    private static float _wrapWidth;
    private static int _wrapFont;

    private static void Toggle() { if (_open) Close(); else OpenArchive(); }

    private static void OpenArchive()
    {
        Build(_btnPage ?? Page());
        _open = true;
        _openedFrame = Time.frameCount;
        _level = 0; _sel = 0; _scroll = 0;
        Overlay.Ensure();
        Overlay.OnDraw -= Draw;
        Overlay.OnDraw += Draw;
    }

    private static void Close()
    {
        _open = false;
        Overlay.OnDraw -= Draw;
    }

    private static void Back()
    {
        if (_level == 0) { Close(); return; }
        _level--;
        _sel = _level == 0 ? _exp : _log;
        _scroll = Math.Max(0, _sel - 3);
    }

    private static int RowCount() => _level switch
    {
        0 => _exps.Count,
        1 => _exp < _exps.Count ? _exps[_exp].Logs.Count : 0,
        _ => _lines.Count,
    };

    private static void Enter(int row)
    {
        if (_level == 0)
        {
            if (row < 0 || row >= _exps.Count || !_exps[row].Open || _exps[row].Logs.Count == 0) return;
            _exp = row; _level = 1; _sel = 0; _scroll = 0;
        }
        else if (_level == 1)
        {
            if (row < 0 || row >= _exps[_exp].Logs.Count) return;
            _log = row; _level = 2; _sel = 0; _scroll = 0; _wrapWidth = -1f;
        }
    }

    private struct Layout { public Rect Panel, Body; public float Line; public int Rows, Font; }

    private static Layout GetLayout()
    {
        float W = Screen.width, H = Screen.height;
        float h = H * 0.78f, w = Math.Min(W * 0.92f, h * 1.35f);
        var panel = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h);
        int font = Math.Max(10, (int)(H * 0.019f * Mathf.Clamp(_fontScale.Value, 0.5f, 2.5f)));
        float line = font * 1.45f;
        var body = new Rect(panel.x + font * 1.5f, panel.y + line * 2.6f, panel.width - font * 3f, panel.height - line * 4.6f);
        return new Layout { Panel = panel, Body = body, Line = line, Rows = Math.Max(1, (int)(body.height / line)), Font = font };
    }

    private static Vector2 Cursor(CM_PageRundown_New page)
    {
        try
        {
            var cam = CM_PageBase.Camera;
            if (cam != null)
            {
                var sp = cam.WorldToScreenPoint(page.CursorWorldPosition);
                return new Vector2(sp.x, Screen.height - sp.y);
            }
        }
        catch { }
        var m = Input.mousePosition;
        return new Vector2(m.x, Screen.height - m.y);
    }

    private static Vector2 _cursor;

    private static void Input_(CM_PageRundown_New page)
    {
        var L = GetLayout();
        _cursor = Cursor(page);
        int count = RowCount();
        _hover = -1;
        if (_level < 2 && L.Body.Contains(_cursor))
        {
            int r = _scroll + (int)((_cursor.y - L.Body.y) / L.Line);
            if (r < count && r - _scroll < L.Rows) _hover = r;
        }
        if (Time.frameCount == _openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetMouseButtonDown(1)) { Back(); return; }
        if (Input.GetMouseButtonDown(0))
        {
            if (!L.Panel.Contains(_cursor)) return; // the ARCHIVE button itself toggles; elsewhere — ignore
            if (_hover >= 0) { _sel = _hover; Enter(_hover); }
            return;
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { Enter(_sel); return; }

        int step = 0;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) step = 1;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) step = -1;
        if (Input.GetKeyDown(KeyCode.PageDown)) step = L.Rows - 1;
        if (Input.GetKeyDown(KeyCode.PageUp)) step = -(L.Rows - 1);
        float wheel = Input.mouseScrollDelta.y;
        int maxScroll = Math.Max(0, count - L.Rows);
        if (_level == 2)
        {
            _scroll = Mathf.Clamp(_scroll + step - (int)Math.Round(wheel * 3f), 0, maxScroll);
            return;
        }
        if (step != 0) { _sel = Mathf.Clamp(_sel + step, 0, Math.Max(0, count - 1)); if (_sel < _scroll) _scroll = _sel; if (_sel >= _scroll + L.Rows) _scroll = _sel - L.Rows + 1; }
        if (Math.Abs(wheel) > 0.01f) _scroll = Mathf.Clamp(_scroll - (int)Math.Round(wheel * 2f), 0, maxScroll);
    }

    // ---------- drawing ----------

    private static Texture2D _white;
    private static GUIStyle _style;
    private static Font _mono;
    private static bool _monoTried;

    private static Font Mono(int size)
    {
        if (_monoTried) return _mono;
        _monoTried = true;
        try { _mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Lucida Console", "Courier New" }, size); } catch { }
        ModInfo.Log.LogInfo($"[Archive] font: {(_mono != null ? _mono.name : "default")}");
        return _mono;
    }

    private static void Rect_(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, _white); }

    private static void Frame(Rect r, Color c, float t = 1f)
    {
        Rect_(new Rect(r.x, r.y, r.width, t), c); Rect_(new Rect(r.x, r.yMax - t, r.width, t), c);
        Rect_(new Rect(r.x, r.y, t, r.height), c); Rect_(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    private static readonly Color Ink = new(0.8f, 0.92f, 0.95f, 1f), Dim = new(0.5f, 0.6f, 0.64f, 1f), Amber = new(1f, 0.72f, 0.3f, 1f);

    private static void Text(float x, float y, float w, float h, string s, Color c) { GUI.color = c; GUI.Label(new Rect(x, y, w, h), s, _style); }

    private static void Draw()
    {
        if (!_open) return;
        if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
        var L = GetLayout();
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip, richText = false };
            var f = Mono(L.Font);
            if (f != null) _style.font = f;
        }
        _style.fontSize = L.Font;
        var old = GUI.color;

        Rect_(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
        Rect_(L.Panel, new Color(0.02f, 0.04f, 0.05f, 0.97f));
        Frame(L.Panel, new Color(Ink.r, Ink.g, Ink.b, 0.5f), 2f);

        float x = L.Body.x, w = L.Body.width;
        float top = L.Panel.y + L.Line * 0.6f;
        string path = "ARCHIVE://" + (_level >= 1 && _exp < _exps.Count ? _exps[_exp].Code : "")
            + (_level == 2 && _exp < _exps.Count && _log < _exps[_exp].Logs.Count ? "/" + _exps[_exp].Logs[_log].Name : "");
        Text(x, top, w, L.Line, "> " + path, Amber);
        Rect_(new Rect(x, top + L.Line * 1.25f, w, 1f), new Color(Ink.r, Ink.g, Ink.b, 0.3f));

        int count = RowCount();
        if (_level == 0) DrawExpeditions(L);
        else if (_level == 1) DrawLogs(L);
        else DrawLog(L);

        // Scroll position.
        if (count > L.Rows)
        {
            var track = new Rect(L.Panel.xMax - L.Font * 0.8f, L.Body.y, 3f, L.Rows * L.Line);
            Rect_(track, new Color(Ink.r, Ink.g, Ink.b, 0.15f));
            float k = (float)L.Rows / count, pos = (float)_scroll / Math.Max(1, count - L.Rows);
            Rect_(new Rect(track.x - 1f, track.y + (track.height - track.height * k) * pos, 5f, track.height * k), new Color(Ink.r, Ink.g, Ink.b, 0.6f));
        }

        string hint = _level == 2
            ? Lang.T("WHEEL / ↑↓ scroll    RMB / BACKSPACE back    ESC close", "КОЛЕСО / ↑↓ прокрутка    ПКМ / BACKSPACE назад    ESC закрыть")
            : Lang.T("CLICK / ENTER open    RMB / BACKSPACE back    ESC close", "КЛИК / ENTER открыть    ПКМ / BACKSPACE назад    ESC закрыть");
        Text(x, L.Panel.yMax - L.Line * 1.5f, w, L.Line, hint, Dim);

        // The game's cursor is under the panel — a small marker where it is.
        Rect_(new Rect(_cursor.x - 6f, _cursor.y - 1f, 13f, 2f), Amber);
        Rect_(new Rect(_cursor.x - 1f, _cursor.y - 6f, 2f, 13f), Amber);
        GUI.color = old;
    }

    private static void Row(Layout L, int i, out float y, out bool visible)
    {
        int r = i - _scroll;
        visible = r >= 0 && r < L.Rows;
        y = L.Body.y + r * L.Line;
        if (!visible) return;
        if (i == _hover || (i == _sel && _hover < 0)) Rect_(new Rect(L.Body.x - 4f, y, L.Body.width + 8f, L.Line), new Color(Ink.r, Ink.g, Ink.b, 0.12f));
    }

    private static void DrawExpeditions(Layout L)
    {
        if (_exps.Count == 0) { Text(L.Body.x, L.Body.y, L.Body.width, L.Line, Lang.T("NO DATA", "НЕТ ДАННЫХ"), Dim); return; }
        float codeW = L.Font * 5f;
        for (int i = 0; i < _exps.Count; i++)
        {
            Row(L, i, out float y, out bool vis);
            if (!vis) continue;
            var e = _exps[i];
            Text(L.Body.x, y, codeW, L.Line, e.Code, e.Open ? Amber : Dim);
            if (!e.Open)
            {
                Text(L.Body.x + codeW, y, L.Body.width - codeW, L.Line, Lang.T("—— NOT COMPLETED ——", "—— НЕ ПРОЙДЕНА ——"), Dim);
                continue;
            }
            Text(L.Body.x + codeW, y, L.Body.width * 0.6f, L.Line, e.Name.ToUpperInvariant() + (e.Done ? "" : Lang.T("  (not completed)", "  (не пройдена)")), Ink);
            string n = e.Logs.Count == 0 ? Lang.T("no logs", "нет логов") : Lang.T($"{e.Logs.Count} logs", $"логов: {e.Logs.Count}");
            Text(L.Body.x + L.Body.width * 0.75f, y, L.Body.width * 0.25f, L.Line, n, e.Logs.Count == 0 ? Dim : Ink);
        }
    }

    private static void DrawLogs(Layout L)
    {
        var e = _exps[_exp];
        for (int i = 0; i < e.Logs.Count; i++)
        {
            Row(L, i, out float y, out bool vis);
            if (!vis) continue;
            var g = e.Logs[i];
            Text(L.Body.x, y, L.Font * 8f, L.Line, g.Layer, Dim);
            Text(L.Body.x + L.Font * 8f, y, L.Body.width - L.Font * 8f, L.Line, g.Name + (g.Audio ? Lang.T("   [AUDIO]", "   [АУДИО]") : ""), Ink);
        }
    }

    private static void DrawLog(Layout L)
    {
        var g = _exps[_exp].Logs[_log];
        if (_wrapWidth != L.Body.width || _wrapFont != L.Font) { _lines = Wrap(g.Text, L.Body.width); _wrapWidth = L.Body.width; _wrapFont = L.Font; }
        for (int i = _scroll; i < _lines.Count && i - _scroll < L.Rows; i++)
            Text(L.Body.x, L.Body.y + (i - _scroll) * L.Line, L.Body.width, L.Line, _lines[i], Ink);
    }

    /// <summary>Word wrap by the measured width (done once per log and screen size).</summary>
    private static List<string> Wrap(string text, float width)
    {
        var res = new List<string>();
        foreach (var para in text.Split('\n'))
        {
            if (para.Trim().Length == 0) { res.Add(""); continue; }
            var line = "";
            foreach (var word in para.Split(' '))
            {
                string next = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && _style.CalcSize(new GUIContent(next)).x > width) { res.Add(line); line = word; }
                else line = next;
            }
            res.Add(line);
        }
        while (res.Count > 0 && res[^1].Length == 0) res.RemoveAt(res.Count - 1);
        return res;
    }
}
