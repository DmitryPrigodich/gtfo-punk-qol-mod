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
/// ARCHIVE button on the rundown screen (a copy of the CREDITS button): opens a terminal-like screen with the
/// terminal logs of the expeditions you've completed in the current rundown — read them again without replaying.
/// Mouse (the game cursor): click a line to open it, right click / Backspace — back, wheel — scroll, Esc — close.
/// Keyboard: ↑/↓, Enter. Client-side, reads only the game data and what the rundown screen shows.
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
        public readonly List<LogFile> Logs = new();
        public bool Open => Done || _showAll.Value;
    }

    private static readonly List<Exp> _exps = new();
    private static readonly Regex Tags = new("<[^>]*>");

    /// <summary>The expeditions as the rundown screen shows them (its icons know the tier, the index and whether it's finished).</summary>
    private static void Build(CM_PageRundown_New page)
    {
        _exps.Clear();
        var icons = page?.m_expIconsAll;
        if (icons == null || icons.Count == 0) { ModInfo.Log.LogWarning("[Archive] no expeditions on the rundown screen"); return; }
        var seen = new HashSet<string>();
        for (int i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            var d = icon?.DataBlock;
            if (d == null) continue;
            string code = Tags.Replace(icon.ShortName ?? "", "").Trim();
            if (code.Length == 0) code = (d.Descriptive?.Prefix ?? "") + icon.Tier.ToString().Replace("Tier", "") + (icon.ExpIndex + 1);
            if (!seen.Add(code)) continue;
            var status = icon.Status;
            var exp = new Exp
            {
                Code = code,
                Name = Tags.Replace(icon.PublicName ?? d.Descriptive?.PublicName ?? "?", "").Trim(),
                Done = status == eExpeditionIconStatus.PlayedAndFinished || status == eExpeditionIconStatus.TierLockedFinishedAnyway,
            };
            AddLayout(exp, d.LevelLayoutData, "MAIN");
            if (_otherLayers.Value)
            {
                if (d.SecondaryLayerEnabled) AddLayout(exp, d.SecondaryLayout, "SECONDARY");
                if (d.ThirdLayerEnabled) AddLayout(exp, d.ThirdLayout, "OVERLOAD");
            }
            _exps.Add(exp);
        }
        ModInfo.Log.LogInfo($"[Archive] rundown screen: {_exps.Count} expeditions, {_exps.Count(e => e.Done)} completed, {_exps.Where(e => e.Open).Sum(e => e.Logs.Count)} logs open");
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
        BlockPage(true);
    }

    // ---------- audio logs ----------

    private static CellSoundPlayer _audio;
    private static bool _playing;

    private static uint CurrentAudio()
    {
        if (_level != 2 || _exp >= _exps.Count || _log >= _exps[_exp].Logs.Count) return 0;
        return _exps[_exp].Logs[_log].AudioId;
    }

    private static void ToggleAudio()
    {
        uint id = CurrentAudio();
        if (id == 0) return;
        if (_playing) { StopAudio(); return; }
        try
        {
            _audio ??= new CellSoundPlayer();
            _audio.Post(id, true);
            _playing = true;
            ModInfo.Log.LogInfo($"[Archive] playing the audio log (event {id})");
        }
        catch (Exception e) { ModInfo.Log.LogWarning($"[Archive] audio log {id}: {e.Message}"); }
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

    /// <summary>While the archive is open the rundown screen's buttons under it can't be hit (their colliders are off), except ARCHIVE.</summary>
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

    private static void Back()
    {
        StopAudio();
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
            StopAudio();
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
        if (_level == 2 && Input.GetKeyDown(KeyCode.P)) { ToggleAudio(); return; }
        if (Input.GetMouseButtonDown(0))
        {
            if (!L.Panel.Contains(_cursor)) return; // the ARCHIVE button itself toggles; elsewhere — ignore
            if (_level == 2 && CurrentAudio() != 0 && _cursor.y < L.Body.y && _cursor.y > L.Panel.y) { ToggleAudio(); return; } // the header line
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
        Text(x, top, CurrentAudio() != 0 ? w * 0.6f : w, L.Line, "> " + path, Amber);
        if (CurrentAudio() != 0)
            Text(x + w * 0.62f, top, w * 0.38f, L.Line, _playing ? Lang.T("■ AUDIO PLAYING (P — stop)", "■ ИДЁТ АУДИО (P — стоп)") : Lang.T("▶ AUDIO LOG (P — play)", "▶ АУДИОЛОГ (P — играть)"), _playing ? Amber : Ink);
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
            ? (CurrentAudio() != 0 ? Lang.T("P / CLICK HEADER audio    ", "P / КЛИК ПО ЗАГОЛОВКУ аудио    ") : "")
              + Lang.T("WHEEL / ↑↓ scroll    RMB / BACKSPACE back    ESC close", "КОЛЕСО / ↑↓ прокрутка    ПКМ / BACKSPACE назад    ESC закрыть")
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
