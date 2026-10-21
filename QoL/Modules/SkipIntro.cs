using CellMenu;
using GtfoPunk.Core;
using HarmonyLib;

namespace GtfoPunk.QoL.Modules;

/// <summary>Пропускает заставку при запуске игры (сразу жмёт «Inject»).</summary>
public class SkipIntro : ModuleBase
{
    public override string Id => "SkipIntro";
    public override string Description => "Пропускать интро-заставку при запуске игры.";
    public override ModuleSide Side => ModuleSide.Client;

    public override System.Collections.Generic.IReadOnlyList<string> ReplacedBy => new[]
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
