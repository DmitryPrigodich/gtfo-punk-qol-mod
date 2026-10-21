using GtfoPunk.Core;
using HarmonyLib;
using SNetwork;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Test module: on drop-in writes the mod status to the local chat.
/// Used to make sure patches and the game API work. Only you see the message.
/// </summary>
public class StatusInChat : ModuleBase
{
    public override string Id => "StatusInChat";
    public override string Description => "Writes the mod status to your chat on drop-in (only you see it).";
    public override ModuleSide Side => ModuleSide.Client;
    public override bool EnabledByDefault => false; // test module, done its job

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.DoChangeState))]
    private static class Patch_DoChangeState
    {
        private static void Postfix(eGameStateName nextState)
        {
            if (nextState != eGameStateName.InLevel || !SNet.HasLocalPlayer) return;
            PlayerChatManager.PostChatMessageLocaly(SNet.LocalPlayer,
                $"<color=#ff4fd8>[{ModInfo.Name} {ModInfo.Version}]</color> {ModuleManager.ShortStatus()}");
        }
    }
}
