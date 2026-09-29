using System;
using UnityEngine;

namespace ZombiePile
{
    /// Things the player does that missions and the tutorial count.
    public enum Ev { Kill, Head, Barrel, Air, Crate, Toy, Clear, Star3, Coins, Combo, Boss, Switch, Shot }

    /// Counters for this session, and the one place gameplay reports events to.
    public static class Stats
    {
        public static int Shots, Kills, Heads, Barrels, Switches, Toys;

        public static void Add(Ev e, int n = 1)
        {
            switch (e)
            {
                case Ev.Shot: Shots += n; return;             // shots only feed the tutorial
                case Ev.Kill: Kills += n; break;
                case Ev.Head: Heads += n; break;
                case Ev.Barrel: Barrels += n; break;
                case Ev.Switch: Switches += n; break;
                case Ev.Toy: Toys += n; break;
            }
            Missions.OnEvent(e, n);
        }
    }

    public class MissionDef
    {
        public string text;         // {0} is the target
        public Ev ev;
        public int baseTarget, minLevel;
        public float reward;        // multiplier of the level's coin reward
        public bool needsAirstrike;
    }

    /// Three missions at a time: small goals that pay coins and steer the player toward everything the game
    /// has (headshots, barrels, switching, crates, stars...). A finished mission is replaced by a new one.
    public static class Missions
    {
        public static readonly MissionDef[] Defs =
        {
            new MissionDef { text = "KILL {0} ZOMBIES", ev = Ev.Kill, baseTarget = 30, reward = 0.7f },
            new MissionDef { text = "GET {0} HEADSHOTS", ev = Ev.Head, baseTarget = 12, reward = 0.8f },
            new MissionDef { text = "THROW {0} BARRELS", ev = Ev.Barrel, baseTarget = 4, reward = 0.6f },
            new MissionDef { text = "CLEAR {0} LEVELS", ev = Ev.Clear, baseTarget = 2, reward = 1.3f },
            new MissionDef { text = "EARN 3 STARS ON {0} LEVEL", ev = Ev.Star3, baseTarget = 1, reward = 1.2f, minLevel = 2 },
            new MissionDef { text = "OPEN {0} SUPPLY CRATES", ev = Ev.Crate, baseTarget = 2, reward = 0.9f, minLevel = 2 },
            new MissionDef { text = "SWITCH SHOOTER {0} TIMES", ev = Ev.Switch, baseTarget = 5, reward = 0.5f },
            new MissionDef { text = "COLLECT {0} COINS", ev = Ev.Coins, baseTarget = 250, reward = 0.9f },
            new MissionDef { text = "LET YOUR BUDDIES THROW {0} THINGS", ev = Ev.Toy, baseTarget = 6, reward = 0.7f },
            new MissionDef { text = "TAKE DOWN {0} BOSS", ev = Ev.Boss, baseTarget = 1, reward = 2f, minLevel = 5 },
            new MissionDef { text = "CALL {0} AIRSTRIKES", ev = Ev.Air, baseTarget = 2, reward = 1f, minLevel = 5, needsAirstrike = true },
            new MissionDef { text = "GET {0} COMBOS OF 5", ev = Ev.Combo, baseTarget = 3, reward = 0.9f, minLevel = 2 },
        };

        public const int Slots = 3;
        /// Set when a mission was finished; the HUD / screens show the message.
        public static event Action<string, int> Completed;

        static int Level { get { return Mathf.Max(1, Save.Data.level); } }

        public static void Ensure()
        {
            var d = Save.Data;
            if (d.mSlot == null || d.mSlot.Length != Slots) d.mSlot = new int[Slots] { -1, -1, -1 };
            if (d.mProg == null || d.mProg.Length != Slots) d.mProg = new int[Slots];
            for (int i = 0; i < Slots; i++)
                if (d.mSlot[i] < 0 || d.mSlot[i] >= Defs.Length || !Eligible(d.mSlot[i], i)) Refill(i);
        }

        static bool Eligible(int def, int except)
        {
            var m = Defs[def];
            if (Level < m.minLevel) return false;
            if (m.needsAirstrike && Econ.Lvl(Up.Airstrike) <= 0) return false;
            for (int i = 0; i < Slots; i++) if (i != except && Save.Data.mSlot[i] == def) return false;
            return true;
        }

        static void Refill(int slot)
        {
            var pool = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Defs.Length; i++) if (Eligible(i, slot)) pool.Add(i);
            Save.Data.mSlot[slot] = pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : 0;
            Save.Data.mProg[slot] = 0;
        }

        public static int Target(int slot)
        {
            var m = Defs[Save.Data.mSlot[slot]];
            float k = 1f + 0.06f * (Level - 1);
            int t = Mathf.Max(1, Mathf.RoundToInt(m.baseTarget * (m.baseTarget <= 3 ? 1f + 0.03f * (Level - 1) : k)));
            return m.baseTarget >= 100 ? Mathf.RoundToInt(t / 25f) * 25 : t;
        }

        public static int Reward(int slot)
        {
            var m = Defs[Save.Data.mSlot[slot]];
            return Mathf.Max(20, Mathf.RoundToInt(m.reward * (40f + 14f * Level) / 5f) * 5);
        }

        public static string Text(int slot) { return string.Format(Defs[Save.Data.mSlot[slot]].text, Target(slot)); }
        public static int Progress(int slot) { return Mathf.Min(Save.Data.mProg[slot], Target(slot)); }

        /// The mission closest to done (for one-line hints).
        public static int Closest()
        {
            int best = 0; float bk = -1f;
            for (int i = 0; i < Slots; i++)
            {
                float k = (float)Progress(i) / Target(i);
                if (k > bk) { bk = k; best = i; }
            }
            return best;
        }

        public static void OnEvent(Ev e, int n)
        {
            if (Save.Data == null || Save.Data.mSlot == null || Save.Data.mSlot.Length != Slots) return;
            for (int i = 0; i < Slots; i++)
            {
                if (Defs[Save.Data.mSlot[i]].ev != e) continue;
                Save.Data.mProg[i] += n;
                if (Save.Data.mProg[i] >= Target(i)) Finish(i);
            }
        }

        static void Finish(int slot)
        {
            string text = Text(slot);
            int reward = Reward(slot);
            Save.Data.coins += reward;
            Save.Data.mDone++;
            Refill(slot);
            Save.Write();
            SoundBank.I.Play(SoundBank.I.chime, 0.8f);
            if (Completed != null) Completed(text, reward);
        }
    }

    /// One reward a day, bigger for every day in a row (up to 7).
    public static class DailyReward
    {
        static bool shownThisRun;

        public static int Today { get { return (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalDays; } }

        /// True when today's reward has not been taken and the player has not been asked yet this run.
        public static bool Due { get { return !shownThisRun && Save.Data != null && Save.Data.lastDay != Today; } }

        public static int StreakToday
        {
            get
            {
                int d = Save.Data.lastDay;
                return d == Today - 1 ? Mathf.Min(7, Save.Data.streak + 1) : 1;
            }
        }

        public static int RewardFor(int day) { return day * 50 + 10 * Mathf.Max(1, Save.Data.level); }

        public static void Asked() { shownThisRun = true; }

        public static int Claim(bool doubled)
        {
            int day = StreakToday;
            int coins = RewardFor(day) * (doubled ? 2 : 1);
            Save.Data.streak = day;
            Save.Data.lastDay = Today;
            Save.Data.coins += coins;
            Save.Write();
            return coins;
        }
    }
}
