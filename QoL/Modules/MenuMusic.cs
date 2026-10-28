using System;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// The game plays music only on the start screen: as soon as the rundown screen opens (NoLobby) the music state
/// becomes Silence and stays silent in the lobby. With SkipIntro the start screen lasts a few seconds, so there is
/// practically no menu music at all. This keeps the main-menu music on the rundown screen and in the lobby, and
/// silences it when the drop starts (the level sets its own music from the elevator on).
/// </summary>
public class MenuMusic : ModuleBase
{
    public override string Id => "MenuMusic";
    public override string Description => "Main-menu music keeps playing on the rundown screen and in the lobby (the game mutes it there).";
    public override ModuleSide Side => ModuleSide.Client;

    private static bool _logged;

    /// <summary>Set in a prefix of the game-state change: the music is switched while the state is still changing.</summary>
    private static bool _inMenus;

    [HarmonyPatch(typeof(MusicMachine), nameof(MusicMachine.ChangeState))]
    private static class Patch_ChangeState
    {
        private static void Prefix(ref MUS_State state)
        {
            try
            {
                if (state != MUS_State.Silence || !_inMenus) return;
                state = MUS_State.MainMenu;
                if (!_logged) { _logged = true; ModInfo.Log.LogInfo("[MenuMusic] menu: Silence → MainMenu"); }
            }
            catch (Exception e) { ModInfo.Log.LogWarning($"[MenuMusic] {e.Message}"); }
        }
    }

    /// <summary>The game doesn't switch the music off itself when the drop starts (it only sets the elevator music
    /// once in the level), so silence it on Generating.</summary>
    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.ChangeState))]
    private static class Patch_GameState
    {
        private static void Prefix(eGameStateName nextState) => _inMenus = nextState is eGameStateName.NoLobby or eGameStateName.Lobby;

        private static void Postfix(eGameStateName nextState)
        {
            if (nextState != eGameStateName.Generating) return;
            try { if (MusicManager.CurrentState() == MUS_State.MainMenu) MusicManager.Machine?.ChangeState(MUS_State.Silence); }
            catch (Exception e) { ModInfo.Log.LogWarning($"[MenuMusic] {e.Message}"); }
        }
    }
}
