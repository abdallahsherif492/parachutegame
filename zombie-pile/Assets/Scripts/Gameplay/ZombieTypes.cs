using UnityEngine;

namespace ZombiePile
{
    public enum Ability { None, Leap, Explode, Cone, Smash, Boss }

    /// One kind of zombie. HP is in x10 units (see Econ); 'step' is how much the pile grows when it clings on.
    public class ZType
    {
        public int id;
        public string name, model, portrait, desc;
        public Color tint = Color.white, text = Color.white;
        public float scale = 1f, hp = 30f, speed = 1f, step = 0.48f;
        public float neck = 0.82f, headY = 1.13f, headZ = 0.16f, headR = 0.3f;   // measured on the kit's run cycle
        public float headUp = 0.28f;              // head sphere centre above the head bone (measured)
        public float chip = 0.4f;                 // damage per second to the wall while clinging to it
        public int coins = 2, firstLevel = 1;
        public float weight = 1f;                 // how often it shows up once unlocked
        public float wallDamage = 8f;             // when it climbs over the wall
        public Ability ability;
        public bool hasHead = true;
        public string runClip = "run_arms";

        public static readonly ZType Walker = new ZType
        {
            id = 0, name = "WALKER", model = "Zombie_Basic", portrait = "z_walker", desc = "The horde. Lots of them.",
            hp = 40f, speed = 1f, coins = 2, chip = 0.45f, text = new Color(0.6f, 0.95f, 0.3f)
        };
        public static readonly ZType Walker2 = new ZType
        {
            id = 0, name = "WALKER", model = "Zombie_Arm", portrait = "z_walker", desc = "The horde. Lots of them.",
            hp = 46f, speed = 0.95f, coins = 2, chip = 0.45f, neck = 1.0f, headY = 1.38f, headZ = 0.01f, headR = 0.28f, headUp = 0.27f, step = 0.55f
        };
        public static readonly ZType Sprinter = new ZType
        {
            id = 1, name = "SPRINTER", model = "Zombie_Ribcage", portrait = "z_sprinter", desc = "Just legs. Very fast legs.",
            scale = 1.3f, hp = 26f, speed = 1.75f, coins = 3, chip = 0.3f, step = 0.35f, neck = 0.8f, hasHead = false, runClip = "run",
            firstLevel = 2, weight = 0.35f, wallDamage = 5f, text = new Color(1f, 0.82f, 0.45f)
        };
        public static readonly ZType Brute = new ZType
        {
            id = 2, name = "BRUTE", model = "Zombie_Chubby", portrait = "z_brute", desc = "Tough and heavy. Pounds on the wall.",
            scale = 1.5f, hp = 340f, speed = 0.55f, coins = 12, chip = 1.2f, step = 0.95f, neck = 1.3f, headY = 1.56f, headZ = 0.08f, headR = 0.26f, headUp = 0.17f,
            firstLevel = 3, weight = 0.07f, wallDamage = 20f, ability = Ability.Smash, text = new Color(0.72f, 0.62f, 1f)
        };
        public static readonly ZType Cone = new ZType
        {
            id = 3, name = "CONEHEAD", model = "Zombie_Basic", portrait = "z_cone", desc = "The cone soaks up bullets. Shoot it off!",
            scale = 1.05f, hp = 40f, speed = 0.9f, coins = 5, chip = 0.5f, firstLevel = 4, weight = 0.25f, ability = Ability.Cone,
            text = new Color(1f, 0.55f, 0.2f)
        };
        public static readonly ZType Leaper = new ZType
        {
            id = 4, name = "LEAPER", model = "Zombie_Arm", portrait = "z_leaper", desc = "Jumps straight onto the pile.\nShoot it before it lands!",
            scale = 0.95f, hp = 46f, speed = 1.2f, coins = 5, chip = 0.45f, neck = 1.0f, headY = 1.38f, headZ = 0.01f, headR = 0.28f, headUp = 0.27f, step = 0.5f,
            tint = new Color(1.6f, 0.6f, 0.5f), firstLevel = 6, weight = 0.18f, ability = Ability.Leap, text = new Color(1f, 0.48f, 0.29f)
        };
        public static readonly ZType Boomer = new ZType
        {
            id = 5, name = "BOOMER", model = "Zombie_Chubby", portrait = "z_boomer", desc = "Pops when it dies and takes\nits friends with it. Use that!",
            scale = 1.1f, hp = 95f, speed = 0.8f, coins = 6, chip = 0.9f, step = 0.7f, neck = 1.3f, headY = 1.56f, headZ = 0.08f, headR = 0.25f, headUp = 0.17f,
            tint = new Color(0.9f, 2f, 0.6f), firstLevel = 8, weight = 0.12f, wallDamage = 25f, ability = Ability.Explode,
            text = new Color(0.55f, 1f, 0.45f)
        };
        public static readonly ZType Boss = new ZType
        {
            id = 6, name = "THE TANK", model = "Zombie_Chubby", portrait = "z_boss", desc = "Smashes the wall and lets the\nhorde climb over it. Bring it down!",
            scale = 2.4f, hp = 2800f, speed = 0.42f, coins = 90, chip = 0.0f, step = 2.2f, neck = 1.3f, headY = 1.56f, headZ = 0.08f, headR = 0.24f, headUp = 0.17f,
            tint = new Color(1.35f, 0.4f, 0.35f), firstLevel = 5, weight = 0f, wallDamage = 40f, ability = Ability.Boss,
            text = new Color(1f, 0.35f, 0.3f)
        };

        /// Network codes: the index in this table.
        public static readonly ZType[] Table = { Walker, Walker2, Sprinter, Brute, Cone, Leaper, Boomer, Boss };
        public static int NetCode(ZType t) { int i = System.Array.IndexOf(Table, t); return i < 0 ? 0 : i; }
        public static ZType FromNet(int code) { return Table[Mathf.Clamp(code, 0, Table.Length - 1)]; }

        public static readonly ZType[] Specials = { Sprinter, Brute, Cone, Leaper, Boomer };
        public static readonly ZType[] Introduced = { Sprinter, Brute, Cone, Boss, Leaper, Boomer };

        /// HP multiplier for campaign level (or endless wave) 'level'.
        public static float HpScale(int level) { return 1f + 0.16f * (level - 1) + 0.004f * (level - 1) * (level - 1); }
        public static float SpeedScale(int level) { return Mathf.Min(1.35f, 1f + 0.02f * (level - 1)); }

        /// A random zombie for this level, weighted by what is unlocked.
        public static ZType Pick(int level, System.Random rnd)
        {
            float total = 1f;
            foreach (var t in Specials) if (level >= t.firstLevel) total += t.weight;
            float r = (float)rnd.NextDouble() * total;
            if (r < 1f) return rnd.NextDouble() < 0.6 ? Walker : Walker2;
            r -= 1f;
            foreach (var t in Specials)
            {
                if (level < t.firstLevel) continue;
                if (r < t.weight) return t;
                r -= t.weight;
            }
            return Walker;
        }

        /// The type introduced at this level (shown on the NEW ZOMBIE card), if any.
        public static ZType NewAt(int level)
        {
            foreach (var t in Introduced) if (t.firstLevel == level) return t;
            return null;
        }

        public static bool IsBossLevel(int level) { return level % 5 == 0; }
    }
}
