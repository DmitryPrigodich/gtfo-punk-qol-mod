using BepInEx;
using BepInEx.Unity.IL2CPP;
using GtfoPunk.Core;

namespace GtfoPunk.QoL;

/// <summary>
/// GtfoPunk QoL — удобства и багфиксы. Всё работает у себя, хост не нужен, баланс не трогает.
/// Безопасно для игры с рандомами.
/// </summary>
[BepInPlugin(GUID, NAME, VERSION)]
public class Plugin : BasePlugin
{
    public const string GUID = "AmDDRed.GtfoPunk.QoL";
    public const string NAME = "GtfoPunk QoL";
    public const string VERSION = "0.6.1";

    public override void Load() => ModInfo.Start(GUID, NAME, VERSION, Config, Log);
}
