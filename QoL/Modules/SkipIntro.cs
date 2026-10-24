using CellMenu;

namespace GtfoPunk.QoL.Modules;

/// <summary>Skips the intro at game start (presses "Inject" right away).</summary>
public class SkipIntro : ModuleBase
{
    public override string Id => "SkipIntro";
    public override string Description => "Skip the intro at game start.";
    public override ModuleSide Side => ModuleSide.Client;

    public override IReadOnlyList<string> ReplacedBy => new[]
    {
        "com.Shadsterwolf.SkipIntro", KnownMods.SkipIntro, KnownMods.QoLFix,
    };

    [HarmonyPatch(typeof(MainMenuGuiLayer), nameof(MainMenuGuiLayer.Setup))]
    private static class Patch_MainMenuSetup
    {
        private static void Postfix(MainMenuGuiLayer __instance)
        {
            CM_PageIntro intro = __instance.PageIntro;
            if (intro != null) intro.EXT_PressInject(1);
        }
    }
}
