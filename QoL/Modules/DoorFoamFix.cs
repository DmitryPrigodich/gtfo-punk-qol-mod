using GtfoPunk.Core;
using HarmonyLib;
using LevelGeneration;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Пена из C-Foam проскакивает сквозь закрытую слабую дверь, если целиться в щели.
/// Чуть увеличиваем коллайдеры полотна двери по высоте, чтобы щелей не было.
/// </summary>
public class DoorFoamFix : ModuleBase
{
    public override string Id => "DoorFoamFix";
    public override string Description => "Пена C-Foam не пролетает сквозь щели закрытых дверей.";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    [HarmonyPatch(typeof(LG_WeakDoor_Destruction), nameof(LG_WeakDoor_Destruction.Setup))]
    private static class Patch_Setup
    {
        private static void Postfix(LG_WeakDoor_Destruction __instance)
        {
            var blade = __instance.m_doorBladeSkinned;
            if (blade == null) return;
            foreach (var box in blade.GetComponentsInChildren<BoxCollider>())
            {
                var s = box.size;
                box.size = new Vector3(s.x, s.y * 1.1f, s.z);
            }
        }
    }
}
