using UnityEngine;

namespace SkyDrop
{
    public enum UpgradeId { Chute = 0, Wingsuit = 1, Magnet = 2, Shield = 3, Canopy = 4 }

    public class UpgradeDef
    {
        public string name, desc;
        public int maxLevel, baseCost;
        public float costGrowth;
        public Color color;
    }

    public class SkinDef
    {
        public string name;
        public int cost;
        public Color suit, suit2, helmet, canopyA, canopyB;
    }

    /// Upgrade and skin tables plus all derived gameplay stats.
    public static class Economy
    {
        public const int UpgradeCount = 5;
        public const int SkinCount = 8;

        public static readonly UpgradeDef[] Upgrades =
        {
            new UpgradeDef { name = "Quick Chute", desc = "Opens faster: wider risk zones,\neasier big multipliers", maxLevel = 10, baseCost = 60, costGrowth = 1.55f, color = new Color(1f, 0.45f, 0.3f) },
            new UpgradeDef { name = "Wingsuit", desc = "Steer faster while falling", maxLevel = 10, baseCost = 50, costGrowth = 1.5f, color = new Color(0.3f, 0.7f, 1f) },
            new UpgradeDef { name = "Coin Magnet", desc = "Pull in coins from further away", maxLevel = 10, baseCost = 40, costGrowth = 1.5f, color = new Color(1f, 0.82f, 0.2f) },
            new UpgradeDef { name = "Shield", desc = "Survive hits from obstacles", maxLevel = 3, baseCost = 250, costGrowth = 2.4f, color = new Color(0.45f, 1f, 0.75f) },
            new UpgradeDef { name = "Canopy Control", desc = "Steer the parachute better,\nresist the wind", maxLevel = 10, baseCost = 55, costGrowth = 1.52f, color = new Color(0.8f, 0.5f, 1f) },
        };

        public static readonly SkinDef[] Skins =
        {
            Skin("Rookie", 0, "#E8573A", "#2B2D42", "#FFFFFF", "#FF5A5F", "#FFFFFF"),
            Skin("Ocean", 300, "#1E88E5", "#0D47A1", "#E3F2FD", "#29B6F6", "#FFFFFF"),
            Skin("Lime", 500, "#7CB342", "#33691E", "#F1F8E9", "#C0CA33", "#FFEB3B"),
            Skin("Pharaoh", 800, "#F4C430", "#1A237E", "#F4C430", "#1A237E", "#F4C430"),
            Skin("Ninja", 1200, "#212121", "#B71C1C", "#212121", "#212121", "#D32F2F"),
            Skin("Candy", 1600, "#F48FB1", "#CE93D8", "#FFFFFF", "#F06292", "#81D4FA"),
            Skin("Galaxy", 2500, "#4A148C", "#00E5FF", "#E1BEE7", "#7C4DFF", "#00E5FF"),
            Skin("Gold Legend", 4000, "#FFD54F", "#FF8F00", "#FFF8E1", "#FFC107", "#FFFFFF"),
        };

        static SkinDef Skin(string name, int cost, string suit, string suit2, string helmet, string a, string b)
        {
            return new SkinDef { name = name, cost = cost, suit = Hex(suit), suit2 = Hex(suit2), helmet = Hex(helmet), canopyA = Hex(a), canopyB = Hex(b) };
        }

        public static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        public static int Level(UpgradeId id) { return SaveSystem.Data.upgrades[(int)id]; }

        public static bool IsMaxed(UpgradeId id) { return Level(id) >= Upgrades[(int)id].maxLevel; }

        public static int Cost(UpgradeId id)
        {
            var d = Upgrades[(int)id];
            return Mathf.RoundToInt(d.baseCost * Mathf.Pow(d.costGrowth, Level(id)) / 5f) * 5;
        }

        public static bool CanAfford(UpgradeId id) { return !IsMaxed(id) && SaveSystem.Data.coins >= Cost(id); }

        public static bool AnyAffordable()
        {
            for (int i = 0; i < UpgradeCount; i++)
                if (CanAfford((UpgradeId)i)) return true;
            return false;
        }

        public static bool Buy(UpgradeId id)
        {
            if (!CanAfford(id)) return false;
            SaveSystem.Data.coins -= Cost(id);
            SaveSystem.Data.upgrades[(int)id]++;
            SaveSystem.Save();
            return true;
        }

        // ---- derived stats -------------------------------------------------------------

        /// Seconds for the canopy to fully open.
        public static float DeployTime { get { return Mathf.Lerp(1.0f, 0.5f, Level(UpgradeId.Chute) / 10f); } }

        /// Risk zones are this much taller (in meters) - makes big multipliers easier to hit.
        public static float ZoneScale { get { return 1f + Level(UpgradeId.Chute) * 0.06f; } }

        /// Max horizontal speed during free fall (m/s).
        public static float FreefallSteer { get { return 13f + Level(UpgradeId.Wingsuit) * 1.0f; } }

        /// Coin pickup radius (m).
        public static float MagnetRadius { get { return 1.8f + Level(UpgradeId.Magnet) * 0.55f; } }

        public static int Shields { get { return Level(UpgradeId.Shield); } }

        /// Horizontal steering speed under canopy (m/s).
        public static float CanopySteer { get { return 6.5f + Level(UpgradeId.Canopy) * 0.55f; } }

        /// Fraction of the wind that still pushes you under canopy.
        public static float WindFactor { get { return Mathf.Lerp(1f, 0.45f, Level(UpgradeId.Canopy) / 10f); } }

        // ---- daily rewards -------------------------------------------------------------

        public static readonly int[] DailyRewards = { 50, 80, 120, 160, 220, 300, 500 };

        public static bool DailyAvailable { get { return SaveSystem.Data.lastDailyClaim != SaveSystem.Today; } }

        /// Streak day index (0-6) the next claim will pay out.
        public static int DailyIndex
        {
            get
            {
                var d = SaveSystem.Data;
                bool continues = d.lastDailyClaim == SaveSystem.Yesterday;
                return continues ? d.dailyStreak % DailyRewards.Length : 0;
            }
        }

        public static int ClaimDaily()
        {
            if (!DailyAvailable) return 0;
            int idx = DailyIndex;
            var d = SaveSystem.Data;
            d.dailyStreak = idx + 1;
            d.lastDailyClaim = SaveSystem.Today;
            d.coins += DailyRewards[idx];
            SaveSystem.Save();
            return DailyRewards[idx];
        }

        public static bool DailyJumpAvailable { get { return SaveSystem.Data.lastDailyJump != SaveSystem.Today; } }
    }
}
