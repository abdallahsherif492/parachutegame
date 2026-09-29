using System;
using UnityEngine;

namespace ZombiePile
{
    /// Upgrade tracks bought in the Armory (the order is the save format: only append).
    public enum Up { GunDamage, GunRate, GunBullets, SniperDamage, SniperRate, SniperPierce, BarrelPower, BarrelReload, Airstrike, WallHp, CoinBonus, Repair }

    public class UpgradeDef
    {
        public Up id;
        public string name, icon;
        public int max;
        public float baseCost, growth;
        public int unlockLevel;                 // campaign level needed before it can be bought
        public Func<int, string> show;          // stat text at a given upgrade level
    }

    public class WeaponDef
    {
        public int id;
        public string name, model, image;
        public float rate, damage, spread, headMult;
        public int bullets, pierce;
        public int price, unlockLevel;
        public float kick;                      // knockback on hit
        public Color tint = Color.white;        // colour of the gun model (new guns reuse a kit model)
    }

    /// Numbers of the game in one place: upgrades, weapons, coin rewards. Damage and HP use x10 units
    /// (a basic zombie has 30 HP, an SMG bullet does 10) so the floating numbers read like an arcade game.
    public static class Econ
    {
        public static readonly UpgradeDef[] Ups =
        {
            new UpgradeDef { id = Up.GunDamage, name = "FIREPOWER", icon = "ic_damage", max = 20, baseCost = 40, growth = 1.34f, show = l => Mathf.RoundToInt(GunDamageMult(l) * Weapon.damage).ToString() },
            new UpgradeDef { id = Up.GunRate, name = "FIRE RATE", icon = "ic_firerate", max = 12, baseCost = 50, growth = 1.38f, show = l => (Weapon.rate * GunRateMult(l)).ToString("0.#") + "/s" },
            new UpgradeDef { id = Up.GunBullets, name = "MULTI SHOT", icon = "ic_split", max = 3, baseCost = 350, growth = 2.3f, unlockLevel = 3, show = l => (Weapon.bullets + l).ToString() },
            new UpgradeDef { id = Up.SniperDamage, name = "FIREPOWER", icon = "ic_damage", max = 20, baseCost = 45, growth = 1.34f, show = l => Mathf.RoundToInt(Sniper.damage * SniperDamageMult(l)).ToString() },
            new UpgradeDef { id = Up.SniperRate, name = "FIRE RATE", icon = "ic_firerate", max = 12, baseCost = 55, growth = 1.38f, show = l => (Sniper.rate * SniperRateMult(l)).ToString("0.#") + "/s" },
            new UpgradeDef { id = Up.SniperPierce, name = "PIERCE", icon = "ic_pierce", max = 4, baseCost = 160, growth = 1.9f, unlockLevel = 2, show = l => "x" + (Sniper.pierce + l + 1) },
            new UpgradeDef { id = Up.BarrelPower, name = "BARREL BOOM", icon = "barrel3d", max = 15, baseCost = 60, growth = 1.36f, show = l => Mathf.RoundToInt(BarrelDamage(l)).ToString() },
            new UpgradeDef { id = Up.BarrelReload, name = "BARREL RELOAD", icon = "ic_quick", max = 10, baseCost = 60, growth = 1.42f, show = l => BarrelCooldown(l).ToString("0.0") + "s" },
            new UpgradeDef { id = Up.Airstrike, name = "AIRSTRIKE", icon = "ic_airstrike", max = 8, baseCost = 400, growth = 1.45f, unlockLevel = 5, show = l => l == 0 ? "LOCKED" : AirstrikeCooldown(l).ToString("0") + "s" },
            new UpgradeDef { id = Up.WallHp, name = "WALL ARMOR", icon = "ic_wall", max = 15, baseCost = 50, growth = 1.33f, show = l => WallMax(l).ToString("0") },
            new UpgradeDef { id = Up.CoinBonus, name = "COIN BONUS", icon = "ic_magnet", max = 10, baseCost = 100, growth = 1.45f, show = l => "+" + (l * 10) + "%" },
            new UpgradeDef { id = Up.Repair, name = "FIELD REPAIR", icon = "ic_repair", max = 8, baseCost = 180, growth = 1.5f, unlockLevel = 4, show = l => "+" + RepairRate(l).ToString("0.#") + "/s" },
        };
        public static int UpCount { get { return Ups.Length; } }

        public static readonly WeaponDef[] Weapons =
        {
            new WeaponDef { id = 0, name = "SMG", model = "SMG", image = "w_smg", rate = 10f, damage = 10f, spread = 1.1f, headMult = 2f, bullets = 1, pierce = 0, kick = 0.08f },
            new WeaponDef { id = 1, name = "RIFLE", model = "Rifle", image = "w_rifle", rate = 6f, damage = 24f, spread = 0.35f, headMult = 2.2f, bullets = 1, pierce = 1, price = 300, unlockLevel = 4, kick = 0.15f },
            new WeaponDef { id = 2, name = "SHOTGUN", model = "Shotgun", image = "w_shotgun", rate = 1.7f, damage = 16f, spread = 6.5f, headMult = 1.8f, bullets = 7, pierce = 0, price = 900, unlockLevel = 8, kick = 0.3f },
            new WeaponDef { id = 3, name = "MAGNUM", model = "Pistol", image = "w_magnum", rate = 2.4f, damage = 60f, spread = 0.05f, headMult = 3.2f, bullets = 1, pierce = 2, price = 700, unlockLevel = 6, kick = 0.4f, tint = new Color(1.5f, 1.2f, 0.6f) },
            new WeaponDef { id = 4, name = "AUTO-12", model = "Shotgun", image = "w_auto12", rate = 3.4f, damage = 11f, spread = 7.5f, headMult = 1.8f, bullets = 6, pierce = 0, price = 2400, unlockLevel = 11, kick = 0.22f, tint = new Color(0.6f, 1f, 0.65f) },
            new WeaponDef { id = 5, name = "MINIGUN", model = "SMG", image = "w_minigun", rate = 24f, damage = 7f, spread = 3.2f, headMult = 1.8f, bullets = 1, pierce = 0, price = 4500, unlockLevel = 14, kick = 0.04f, tint = new Color(0.55f, 0.7f, 1.15f) },
        };

        /// The shop lists guns in the order they unlock (ids stay as they are: they are the save format).
        public static readonly WeaponDef[] Shop = BuildShop();
        static WeaponDef[] BuildShop()
        {
            var l = new System.Collections.Generic.List<WeaponDef>(Weapons);
            l.Sort((a, b) => a.unlockLevel != b.unlockLevel ? a.unlockLevel.CompareTo(b.unlockLevel) : a.id.CompareTo(b.id));
            return l.ToArray();
        }

        /// The tower sniper's rifle.
        public static readonly WeaponDef Sniper = new WeaponDef { id = 10, name = "SNIPER", model = "Rifle", image = "w_rifle", rate = 1.8f, damage = 50f, spread = 0f, headMult = 3f, bullets = 1, pierce = 1, kick = 0.35f };

        public static WeaponDef Weapon { get { int w = Save.Data != null ? Save.Data.weapon : 0; return Weapons[Mathf.Clamp(w, 0, Weapons.Length - 1)]; } }

        public static int Lvl(Up u) { return Save.Data != null && (int)u < Save.Data.up.Length ? Save.Data.up[(int)u] : 0; }
        public static UpgradeDef Def(Up u) { return Ups[(int)u]; }
        public static bool Maxed(Up u) { return Lvl(u) >= Def(u).max; }
        public static bool Unlocked(Up u) { return Save.Data.level >= Def(u).unlockLevel; }

        public static int Cost(Up u)
        {
            var d = Def(u);
            float c = d.baseCost * Mathf.Pow(d.growth, Lvl(u));
            return Mathf.RoundToInt(c / 5f) * 5;
        }

        public static bool CanBuy(Up u) { return !Maxed(u) && Unlocked(u) && Save.Data.coins >= Cost(u); }

        public static bool Buy(Up u)
        {
            if (!CanBuy(u)) return false;
            Save.Data.coins -= Cost(u);
            Save.Data.up[(int)u]++;
            Save.Write();
            return true;
        }

        public static bool Owns(int weapon) { return (Save.Data.weapons & (1 << weapon)) != 0; }

        public static bool BuyWeapon(int weapon)
        {
            var w = Weapons[weapon];
            if (Owns(weapon) || Save.Data.level < w.unlockLevel || Save.Data.coins < w.price) return false;
            Save.Data.coins -= w.price;
            Save.Data.weapons |= 1 << weapon;
            Save.Data.weapon = weapon;
            Save.Write();
            return true;
        }

        // ------------------------------------------------------------------ stat formulas
        public static float GunDamageMult(int l) { return 1f + 0.18f * l; }
        public static float GunRateMult(int l) { return 1f + 0.09f * l; }
        public static float SniperDamageMult(int l) { return 1f + 0.22f * l; }
        public static float SniperRateMult(int l) { return 1f + 0.1f * l; }
        public static float BarrelDamage(int l) { return 90f * (1f + 0.25f * l); }
        public static float BarrelRadius(int l) { return 3.4f * (1f + 0.05f * l); }
        public static float BarrelCooldown(int l) { return 6f * Mathf.Pow(0.9f, l); }
        public static float AirstrikeCooldown(int l) { return l <= 0 ? 999f : 38f * Mathf.Pow(0.88f, l - 1); }
        public static float WallMax(int l) { return 100f + 20f * l; }
        public static float RepairRate(int l) { return 0.5f * l; }
        public static float CoinMult { get { return 1f + 0.1f * Lvl(Up.CoinBonus); } }

        /// Coins for clearing a campaign level (before the coin bonus).
        public static int LevelBonus(int level) { return 30 + 15 * level; }
    }
}
