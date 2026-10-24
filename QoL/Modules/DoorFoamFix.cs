using LevelGeneration;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// C-foam slips through a closed weak door if you aim at the gaps.
/// Slightly enlarge the door panel colliders vertically so there are no gaps.
/// </summary>
public class DoorFoamFix : ModuleBase
{
    public override string Id => "DoorFoamFix";
    public override string Description => "C-foam doesn't fly through the gaps of closed doors.";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

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
