using System;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace GtfoPunk.QoL.Core;

/// <summary>Unity calls OnGUI on this component every frame; it forwards the repaint to <see cref="Overlay.OnDraw"/>.</summary>
public class QoLOverlayBehaviour : MonoBehaviour
{
    public QoLOverlayBehaviour(IntPtr ptr) : base(ptr) { }

    public void OnGUI() => Overlay.Draw();
}

/// <summary>
/// Screen-space drawing (immediate mode GUI, repaint only — input is read with Input in the modules' ticks).
/// Created on first use; survives scene changes.
/// </summary>
public static class Overlay
{
    public static event Action OnDraw;

    private static GameObject _host;
    private static bool _failed;

    public static void Ensure()
    {
        if (_host != null || _failed) return;
        try
        {
            if (!ClassInjector.IsTypeRegisteredInIl2Cpp<QoLOverlayBehaviour>()) ClassInjector.RegisterTypeInIl2Cpp<QoLOverlayBehaviour>();
            _host = new GameObject("GtfoPunk_QoL_Overlay");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.hideFlags = HideFlags.HideAndDontSave;
            _host.AddComponent<QoLOverlayBehaviour>();
            ModInfo.Log.LogInfo("[Overlay] screen overlay ready");
        }
        catch (Exception e) { _failed = true; ModInfo.Log.LogError($"[Overlay] can't create the screen overlay: {e}"); }
    }

    internal static void Draw()
    {
        var ev = Event.current;
        if (OnDraw == null || ev == null || ev.type != EventType.Repaint) return;
        foreach (Action h in OnDraw.GetInvocationList())
        {
            try { h(); }
            catch (Exception e) { ModInfo.Log.LogError($"[Overlay] {h.Method.DeclaringType?.Name}: {e.Message}"); OnDraw -= h; }
        }
    }
}
