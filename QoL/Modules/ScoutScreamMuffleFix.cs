using AK;
using GtfoPunk.Core;
using HarmonyLib;
using UnityEngine;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// После крика скаута игра приглушает звук, и иногда он так и остаётся приглушённым.
/// Сбрасываем приглушение через 15 с после крика, а также при выходе в лобби/меню
/// (там же сбрасываем приглушение от меню и карты).
/// </summary>
public class ScoutScreamMuffleFix : ModuleBase
{
    public override string Id => "ScoutScreamMuffleFix";
    public override string Description => "Звук не остаётся приглушённым после крика скаута (и после выхода в лобби).";
    public override ModuleSide Side => ModuleSide.Client;
    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[] { KnownMods.QoLFix };

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
