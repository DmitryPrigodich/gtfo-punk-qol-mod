using System;
using GameData;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// Every apparel item (helmets, torsos, legs, backpacks, palettes) in the APPAREL menu, as if already dropped.
/// Local: the items are only added to your own inventory list, nothing is sent to the server; what you wear is
/// shown to others as usual (everyone has the same models).
/// Unlike the first try (0.7.3), the game's methods aren't hooked — that hook alone emptied the menu. Instead the list
/// is topped up in the menus every couple of seconds (the game rebuilds it from the server's data now and then).
/// </summary>
public class AllApparel : ModuleBase
{
    public override string Id => "AllApparel";
    public override string Description => "All apparel (helmets, torsos, legs, backpacks, palettes) available in the APPAREL menu. Local only.";
    public override ModuleSide Side => ModuleSide.Client;

    protected override void OnBindConfig() => ConfigMigration.DropOrphans(Config, Id); // UnlockExperimental (0.7.4)

    protected override void OnEnable() => FrameTicker.OnUpdate += Tick;

    private static float _next;
    private static int _logged = -1;
    private static bool _waitLogged;

    private static void Tick()
    {
        if (GameUtil.InLevel || UnityEngine.Time.realtimeSinceStartup < _next) return;
        _next = UnityEngine.Time.realtimeSinceStartup + 2f;
        try
        {
            var list = PersistentInventoryManager.Current?.m_vanityItemsInventory?.m_allItems;
            if (list == null || list.Count == 0) // wait for the game's own list (from the server)
            {
                if (!_waitLogged && UnityEngine.Time.realtimeSinceStartup > 90f) { _waitLogged = true; ModInfo.Log.LogInfo($"[AllApparel] no apparel list yet ({(list == null ? "not found" : "empty")})"); }
                return;
            }
            var have = new HashSet<uint>();
            for (int i = 0; i < list.Count; i++) { var it = list[i]; if (it != null) have.Add(it.id); }
            int own = have.Count, added = 0, total = 0, enabled = 0;
            foreach (var t in GameDataBlockBase<VanityItemsTemplateDataBlock>.GetAllBlocks())
            {
                if (t == null) continue;
                total++;
                if (!t.internalEnabled) continue;
                enabled++;
                if (have.Contains(t.persistentID)) continue;
                list.Add(new VanityItem
                {
                    publicName = t.publicName, type = t.type, prefab = t.prefab, id = t.persistentID,
                    flags = VanityItemFlags.Touched | VanityItemFlags.Acknowledged, // not marked as "new"
                });
                added++;
            }
            if (_logged != list.Count)
            {
                _logged = list.Count;
                ModInfo.Log.LogInfo($"[AllApparel] apparel in the game data: {total} ({enabled} enabled); yours {own} + added {added} = {list.Count} in the menu");
            }
        }
        catch (Exception e) { ModInfo.Log.LogWarning($"[AllApparel] {e.Message}"); _next += 30f; }
    }
}
