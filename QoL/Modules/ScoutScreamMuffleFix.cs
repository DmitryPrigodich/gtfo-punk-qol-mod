using AK;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// After a scout scream the game muffles the sound, and sometimes it stays muffled.
/// Reset the muffle 15 s after the scream, and also when leaving to the lobby/menu
/// (also reset the menu and map muffles there).
/// </summary>
public class ScoutScreamMuffleFix : ModuleBase
{
    public override string Id => "ScoutScreamMuffleFix";
    public override string Description => "Sound doesn't stay muffled after a scout scream (and after leaving to the lobby).";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

    private const float ResetDelay = 15f;
    private static float _resetAt = -1f;

    protected override void OnEnable() => FrameTicker.OnUpdate += Tick;

    private static void Tick()
    {
        if (_resetAt < 0f || Time.time < _resetAt) return;
        _resetAt = -1f;
        CellSound.SetGlobalRTPCValue(GAME_PARAMETERS.SCOUT_SCREAM_DUCKING, 0f);
    }

    [HarmonyPatch(typeof(EnemyVoice), nameof(EnemyVoice.PlayVoiceEvent))]
    private static class Patch_Voice
    {
        private static void Postfix(uint ID)
        {
            if (ID == EVENTS.SCOUT_DETECT_SCREAM) _resetAt = Time.time + ResetDelay;
        }
    }

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.ChangeState))]
    private static class Patch_State
    {
        private static void Postfix(eGameStateName nextState)
        {
            if (nextState is eGameStateName.NoLobby or eGameStateName.Lobby or eGameStateName.Generating)
            {
                CellSound.SetGlobalRTPCValue(GAME_PARAMETERS.SCOUT_SCREAM_DUCKING, 0f);
                CellSound.SetGlobalRTPCValue(GAME_PARAMETERS.GAME_MENU_MUFFLE, 0f);
                CellSound.SetGlobalRTPCValue(GAME_PARAMETERS.MINIMAP_MUFFLE, 0f);
                _resetAt = -1f;
            }
        }
    }
}
