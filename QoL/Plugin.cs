using BepInEx;
using BepInEx.Unity.IL2CPP;
using GtfoPunk.Core;

namespace GtfoPunk.QoL;

/// <summary>
/// GtfoPunk QoL — conveniences and bug fixes. Everything works locally, no host needed, no balance changes.
/// Safe to play with randoms.
/// </summary>
[BepInPlugin(GUID, NAME, VERSION)]
public class Plugin : BasePlugin
{
    public const string GUID = "AmDDRed.GtfoPunk.QoL";
    public const string NAME = "GtfoPunk QoL";
    public const string VERSION = "0.7.11";

    public override void Load()
    {
        ModInfo.Start(GUID, NAME, VERSION, Config, Log);
        Watermark.Register($"GPUNK QOL v{VERSION}");
    }
}
