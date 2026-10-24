using System;
using System.Globalization;
using System.Text;
using CellMenu;
using GameData;
using Gear;
using Player;

namespace GtfoPunk.QoL.Modules;

/// <summary>
/// In the loadout a stats block is added to the weapon description: damage, magazine, reserve, rate of fire,
/// reload, multipliers, spread, penetration — and how many hits typical sleepers take.
/// Data comes from the game tables, so it works for modded rundowns too.
/// </summary>
public class WeaponStats : ModuleBase
{
    public override string Id => "WeaponStats";
    public override string Description => "Weapon stats in the description when picking in the loadout (damage, magazine, rate, hits to kill, etc.).";
    public override ModuleSide Side => ModuleSide.Client;
    public override IReadOnlyList<string> ReplacedBy => new[] { "WeaponStatShower", "DescriptiveWeaponStatShower" };

    private static ConfigEntry<bool> _hitsToKill;

    protected override void OnBindConfig()
    {
        _hitsToKill = Bind("ShowHitsToKill", true, "Show how many hits typical sleepers take (head / body).");
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(float v, int digits = 1) => Math.Round(v, digits).ToString(Inv);

    // HP and head damage multiplier of typical enemies (vanilla values; armored — the head is armored).
    private static readonly (string name, float hp, float head, bool armoredHead)[] Sleepers =
    {
        ("Striker", 20f, 3f, false),
        ("Shooter", 30f, 5f, false),
        ("Scout", 42f, 3f, false),
        ("Big Striker", 120f, 1.5f, false),
        ("Big Shooter", 150f, 2f, false),
        ("Charger", 30f, 1f, true),
    };

    private const string Hdr = "<color=#ff4fd8>";
    private const string Dim = "<color=#9aa0a6>";
    private const string End = "</color>";

    [HarmonyPatch(typeof(CM_InventorySlotItem), nameof(CM_InventorySlotItem.LoadData))]
    private static class Patch_LoadData
    {
        private static void Postfix(CM_InventorySlotItem __instance, GearIDRange idRange, bool detailedInfo)
        {
            if (__instance == null || idRange == null || !detailedInfo) return;
            string stats = Build(idRange);
            if (!string.IsNullOrEmpty(stats))
                __instance.GearDescription = (__instance.GearDescription ?? "") + "\n\n" + stats;
        }
    }

    private static string Build(GearIDRange idRange)
    {
        uint catId = idRange.GetCompID(eGearComponent.Category);
        var cat = GameDataBlockBase<GearCategoryDataBlock>.GetBlock(catId);
        if (cat == null) return null;
        var item = GameDataBlockBase<ItemDataBlock>.GetBlock(cat.BaseItem);
        if (item == null) return null;

        if (item.inventorySlot == InventorySlot.GearMelee)
        {
            var melee = GameDataBlockBase<MeleeArchetypeDataBlock>.GetBlock(GearBuilder.GetMeleeArchetypeID(cat));
            return melee == null ? null : BuildMelee(melee);
        }

        var fireMode = (eWeaponFireMode)idRange.GetCompID(eGearComponent.FireMode);
        bool sentry = catId == 12;
        var arch = sentry
            ? SentryGunInstance_Firing_Bullets.GetArchetypeDataForFireMode(fireMode)
            : GameDataBlockBase<ArchetypeDataBlock>.GetBlock(GearBuilder.GetArchetypeID(cat, fireMode));
        return arch == null ? null : BuildGun(arch, item, fireMode, sentry);
    }

    private static string BuildGun(ArchetypeDataBlock a, ItemDataBlock item, eWeaponFireMode mode, bool sentry)
    {
        var sb = new StringBuilder();
        int pellets = Math.Max(1, a.ShotgunBulletCount);
        float shotDmg = a.Damage * pellets;

        sb.Append(Hdr).Append(Lang.T("STATS", "СТАТЫ")).Append(End).Append('\n');
        sb.Append(Lang.T("Damage ", "Урон ")).Append(F(a.Damage, 2));
        if (a.ShotgunBulletCount > 0) sb.Append(" ×").Append(a.ShotgunBulletCount).Append(Dim).Append(" (=").Append(F(shotDmg)).Append(')').Append(End);
        sb.Append(Lang.T("   Rate ", "   Темп ")).Append(RateOfFire(a, mode)).Append(Lang.T("/s", "/с"));
        sb.Append('\n');

        if (!sentry)
            sb.Append(Lang.T("Mag ", "Магазин ")).Append(a.DefaultClipSize).Append(Lang.T("   Reload ", "   Перезарядка ")).Append(F(a.DefaultReloadTime, 2)).Append(Lang.T("s", "с"));
        int total = TotalAmmo(a, item, sentry);
        if (total > 0) sb.Append(sentry ? Lang.T("Reserve ", "Запас ") : Lang.T("   Reserve ", "   Запас ")).Append(total);
        sb.Append('\n');

        sb.Append(Lang.T("Precision ×", "В голову ×")).Append(F(a.PrecisionDamageMulti, 2))
          .Append(Lang.T("   Stagger ×", "   Стаггер ×")).Append(F(a.StaggerDamageMulti, 2))
          .Append(Lang.T("   Falloff from ", "   Спад урона с ")).Append((int)a.DamageFalloff.x).Append(Lang.T("m", "м"));
        sb.Append('\n');

        if (a.ShotgunBulletCount == 0)
            sb.Append(Lang.T("Spread: hip ", "Разброс: от бедра ")).Append(F(a.HipFireSpread)).Append(Lang.T(" / ADS ", " / в прицеле ")).Append(F(a.AimSpread));
        else
            sb.Append(Lang.T("Cone ", "Конус дроби ")).Append(a.ShotgunConeSize).Append(Lang.T(" / spread ", " / разлёт ")).Append(a.ShotgunBulletSpread);
        if (a.PiercingBullets) sb.Append(Lang.T("   Pierce ", "   Пробитие ")).Append(a.PiercingDamageCountLimit);
        sb.Append('\n');

        if (_hitsToKill.Value && !sentry)
        {
            sb.Append(Dim).Append(Lang.T("Hits to kill (head / body):", "Попаданий до убийства (голова / тело):")).Append(End).Append('\n');
            int i = 0;
            foreach (var s in Sleepers)
            {
                float headDmg = s.armoredHead ? shotDmg : shotDmg * a.PrecisionDamageMulti * s.head;
                int head = Hits(s.hp, headDmg), body = Hits(s.hp, shotDmg);
                sb.Append(s.name).Append(' ').Append(head).Append('/').Append(body);
                sb.Append(++i % 3 == 0 ? "\n" : "   ");
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildMelee(MeleeArchetypeDataBlock m)
    {
        var sb = new StringBuilder();
        sb.Append(Hdr).Append(Lang.T("STATS", "СТАТЫ")).Append(End).Append('\n');
        sb.Append(Lang.T("Light ", "Удар ")).Append(F(m.LightAttackDamage)).Append(Lang.T("   Charged ", "   Заряженный ")).Append(F(m.ChargedAttackDamage)).Append('\n');
        sb.Append(Lang.T("Precision ×", "В голову ×")).Append(F(m.LightPrecisionMulti, 2)).Append(" / ×").Append(F(m.ChargedPrecisionMulti, 2))
          .Append(Lang.T("   Stagger ×", "   Стаггер ×")).Append(F(m.LightStaggerMulti, 2)).Append(" / ×").Append(F(m.ChargedStaggerMulti, 2)).Append('\n');
        sb.Append(Lang.T("Sleeper ×", "По спящим ×")).Append(F(m.ChargedSleeperMulti, 2))
          .Append(Lang.T("   Backstab ×", "   В спину ×")).Append(F(m.ChargedBackstabberMulti, 2))
          .Append(Lang.T("   Range ", "   Дальность ")).Append(F(m.CameraDamageRayLength, 2)).Append(Lang.T("m", "м"));
        if (m.CanHitMultipleEnemies) sb.Append(Lang.T("   Multi-hit", "   Бьёт нескольких"));
        if (_hitsToKill.Value)
        {
            float charged = m.ChargedAttackDamage * m.ChargedSleeperMulti;
            sb.Append('\n').Append(Dim).Append(Lang.T("Charged on sleeper (head / body):", "Заряженным по спящему (голова / тело):")).Append(End).Append('\n');
            int i = 0;
            foreach (var s in Sleepers)
            {
                float head = s.armoredHead ? charged : charged * m.ChargedPrecisionMulti * s.head;
                sb.Append(s.name).Append(' ').Append(Hits(s.hp, head)).Append('/').Append(Hits(s.hp, charged));
                sb.Append(++i % 3 == 0 ? "\n" : "   ");
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static int Hits(float hp, float dmg) => dmg <= 0f ? 0 : (int)Math.Ceiling(hp / dmg - 0.0001f);

    private static string RateOfFire(ArchetypeDataBlock a, eWeaponFireMode mode)
    {
        float v;
        switch (mode)
        {
            case eWeaponFireMode.Burst:
            case eWeaponFireMode.SentryGunBurst:
                float cycle = a.BurstDelay + a.SpecialChargetupTime + a.ShotDelay * (a.BurstShotCount - 1);
                v = cycle > 0 ? a.BurstShotCount / cycle : 0;
                break;
            case eWeaponFireMode.Semi:
            case eWeaponFireMode.SentryGunShotgunSemi:
                v = 1f / Math.Max(0.0001f, a.ShotDelay + a.SpecialChargetupTime);
                break;
            default:
                v = 1f / Math.Max(0.0001f, a.ShotDelay);
                break;
        }
        return F(v);
    }

    private static int TotalAmmo(ArchetypeDataBlock a, ItemDataBlock item, bool sentry)
    {
        var pd = GameDataBlockBase<PlayerDataBlock>.GetBlock(1u);
        if (pd == null) return 0;
        int max = PlayerAmmoStorage.GetAmmoTypeFromSlot(item.inventorySlot) switch
        {
            AmmoType.Standard => pd.AmmoStandardMaxCap,
            AmmoType.Special => pd.AmmoSpecialMaxCap,
            AmmoType.Class => pd.AmmoClassMaxCap,
            _ => -1,
        };
        if (max <= 0) return 0;
        float cost = a.CostOfBullet;
        if (sentry)
        {
            cost *= item.ClassAmmoCostFactor;
            if (a.ShotgunBulletCount > 0) cost *= a.ShotgunBulletCount;
            return cost > 0 ? (int)(max / cost) : 0;
        }
        return (cost > 0 ? (int)(max / cost) : 0) + a.DefaultClipSize;
    }
}
