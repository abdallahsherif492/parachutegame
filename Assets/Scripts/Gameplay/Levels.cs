using UnityEngine;

namespace SkyDrop
{
    public class WorldTheme
    {
        public string name;
        public Color sky, ground, ground2, light, ambientSky, ambientGround;
        public Color targetA, targetB, prop1, prop2, prop3;
        public bool night;
    }

    /// All numbers that define one jump. Generated deterministically from the level index.
    public class LevelConfig
    {
        public int index, world, local, seed;
        public bool daily;
        public float height;          // drop altitude above the landing pad
        public float targetRadius;    // landing pad radius
        public float targetDistance;  // horizontal distance from drop point to pad
        public float targetTop;       // pad surface height above the ground
        public int rings;
        public float ringRadius;
        public float lateral;         // how much the ring line snakes around
        public int birdFlocks, balloons, drones, helis, storms, towers;
        public float wind;
        public bool movingTarget, water, city;
        public float targetSpeed;

        // The 3 goals of the level (one star each). Landing on the target is always goal #1.
        public int ringsNeeded;
        public int riskZone;          // open the chute in this zone or riskier (Zones index)

        public string RingGoal { get { return "Fly through " + ringsNeeded + " rings"; } }
        public string RiskGoal { get { return "Open the chute at " + Zones.MultText(Zones.Mult[riskZone]) + " or more"; } }
        public const string LandGoal = "Land on the target";

        public WorldTheme Theme { get { return Levels.Themes[world]; } }

        public string Title
        {
            get { return daily ? "DAILY JUMP" : "LEVEL " + (index + 1); }
        }
    }

    public static class Levels
    {
        public const int PerWorld = 10;
        public const int Worlds = 5;
        public const int Count = PerWorld * Worlds;

        public static readonly WorldTheme[] Themes =
        {
            new WorldTheme
            {
                name = "Green Valley", sky = C(0.56f, 0.8f, 0.97f), ground = C(0.47f, 0.76f, 0.36f), ground2 = C(0.4f, 0.68f, 0.3f),
                light = C(1f, 0.96f, 0.88f), ambientSky = C(0.55f, 0.62f, 0.72f), ambientGround = C(0.35f, 0.4f, 0.3f),
                targetA = C(1f, 1f, 1f), targetB = C(0.95f, 0.25f, 0.25f), prop1 = C(0.2f, 0.52f, 0.25f), prop2 = C(0.5f, 0.33f, 0.2f), prop3 = C(0.95f, 0.9f, 0.8f)
            },
            new WorldTheme
            {
                name = "Pyramid Desert", sky = C(0.98f, 0.84f, 0.64f), ground = C(0.93f, 0.78f, 0.5f), ground2 = C(0.88f, 0.7f, 0.43f),
                light = C(1f, 0.93f, 0.8f), ambientSky = C(0.65f, 0.58f, 0.5f), ambientGround = C(0.5f, 0.4f, 0.28f),
                targetA = C(1f, 1f, 1f), targetB = C(0.1f, 0.35f, 0.7f), prop1 = C(0.86f, 0.7f, 0.42f), prop2 = C(0.3f, 0.6f, 0.3f), prop3 = C(0.55f, 0.38f, 0.2f)
            },
            new WorldTheme
            {
                name = "Tropical Islands", sky = C(0.5f, 0.84f, 0.98f), ground = C(0.1f, 0.58f, 0.82f), ground2 = C(0.95f, 0.88f, 0.62f),
                light = C(1f, 0.97f, 0.9f), ambientSky = C(0.5f, 0.65f, 0.75f), ambientGround = C(0.2f, 0.4f, 0.45f),
                targetA = C(1f, 1f, 1f), targetB = C(1f, 0.55f, 0.1f), prop1 = C(0.2f, 0.62f, 0.3f), prop2 = C(0.55f, 0.38f, 0.22f), prop3 = C(0.95f, 0.95f, 0.95f)
            },
            new WorldTheme
            {
                name = "Snow Peaks", sky = C(0.78f, 0.87f, 0.96f), ground = C(0.93f, 0.95f, 0.98f), ground2 = C(0.82f, 0.87f, 0.93f),
                light = C(1f, 0.98f, 0.95f), ambientSky = C(0.62f, 0.68f, 0.8f), ambientGround = C(0.5f, 0.55f, 0.62f),
                targetA = C(0.15f, 0.15f, 0.2f), targetB = C(0.9f, 0.15f, 0.3f), prop1 = C(0.15f, 0.4f, 0.3f), prop2 = C(0.5f, 0.52f, 0.58f), prop3 = C(0.98f, 0.98f, 1f)
            },
            new WorldTheme
            {
                name = "Neon City", sky = C(0.08f, 0.07f, 0.2f), ground = C(0.12f, 0.12f, 0.17f), ground2 = C(0.2f, 0.2f, 0.26f),
                light = C(0.55f, 0.55f, 0.8f), ambientSky = C(0.35f, 0.3f, 0.55f), ambientGround = C(0.15f, 0.12f, 0.2f),
                targetA = C(0.1f, 1f, 0.95f), targetB = C(1f, 0.15f, 0.7f), prop1 = C(0.2f, 0.2f, 0.28f), prop2 = C(1f, 0.2f, 0.75f), prop3 = C(0.1f, 0.95f, 1f),
                night = true
            },
        };

        static Color C(float r, float g, float b) { return new Color(r, g, b); }

        public static LevelConfig Make(int index)
        {
            index = Mathf.Clamp(index, 0, Count - 1);
            var c = new LevelConfig();
            c.index = index;
            c.world = index / PerWorld;
            c.local = index % PerWorld;
            c.seed = 1000 + index * 7919;

            float d = index / (float)(Count - 1);   // 0..1 over the whole game
            float l = c.local / (float)(PerWorld - 1); // 0..1 inside a world

            c.city = c.world == 4;
            c.water = c.world == 2;
            c.height = Mathf.Min(430f + index * 13f + l * 40f, 1100f);
            c.targetRadius = Mathf.Max(4f, Mathf.Lerp(9.5f, 5f, d) - l * 0.8f);
            c.targetDistance = Mathf.Lerp(25f, 110f, d) + l * 15f;
            c.targetTop = c.city ? 38f + c.local * 4f : (c.water ? 1.4f : 0.3f);

            // About one ring per second of free fall.
            c.rings = Mathf.Clamp(Mathf.RoundToInt((c.height - 200f) / 40f), 5, 24);
            c.ringRadius = Mathf.Lerp(6f, 3.8f, d);
            c.ringsNeeded = Mathf.Clamp(Mathf.CeilToInt(c.rings * Mathf.Lerp(0.6f, 0.85f, d)), 1, c.rings);
            c.riskZone = index < 4 ? 4 : index < 20 ? 3 : 2;   // x2 -> x3 -> x5
            c.lateral = Mathf.Lerp(8f, 38f, d);

            c.birdFlocks = index < 1 ? 0 : Mathf.RoundToInt(1 + d * 8f + l * 2f);
            c.balloons = index < 2 ? 0 : 1 + Mathf.RoundToInt(d * 5f + l);
            c.drones = index < 6 ? 0 : 1 + Mathf.RoundToInt(d * 5f);
            c.helis = index < 12 ? 0 : 1 + Mathf.RoundToInt(d * 4f);
            c.storms = c.world >= 3 ? 2 + c.local / 3 : 0;
            c.towers = c.city ? 16 + c.local * 2 : 0;

            c.wind = c.world == 0 ? l * 2f : Mathf.Lerp(1.5f, 5f, d) + (c.world == 3 ? 1.5f : 0f);
            c.movingTarget = (c.world == 1 && c.local >= 5) || (c.world == 2 && c.local >= 3) || (c.world == 3 && c.local >= 7);
            c.targetSpeed = c.movingTarget ? 2.5f + c.local * 0.35f : 0f;
            return c;
        }

        /// A daily challenge: same for everyone on a given date, a bit harder than the player's progress.
        public static LevelConfig MakeDaily(int unlocked)
        {
            var now = System.DateTime.Now;
            int dateSeed = now.Year * 1000 + now.DayOfYear;
            var rng = new System.Random(dateSeed);
            int baseIndex = Mathf.Clamp(unlocked + 2 + rng.Next(0, 4), 4, Count - 1);
            var c = Make(baseIndex);
            c.world = rng.Next(0, Mathf.Min(Worlds, unlocked / PerWorld + 2));
            c.city = c.world == 4;
            c.water = c.world == 2;
            c.targetTop = c.city ? 45f : (c.water ? 1.4f : 0.3f);
            if (!c.city) c.towers = 0; else c.towers = 20;
            if (c.world < 3) c.storms = 0;
            c.seed = dateSeed;
            c.daily = true;
            return c;
        }

        public static bool IsUnlocked(int index) { return index <= SaveSystem.Data.unlocked; }

        public static int WorldStars(int world)
        {
            int s = 0;
            for (int i = 0; i < PerWorld; i++) s += SaveSystem.Data.stars[world * PerWorld + i];
            return s;
        }
    }
}
