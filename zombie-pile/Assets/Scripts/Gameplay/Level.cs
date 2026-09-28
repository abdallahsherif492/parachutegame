using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// Runs one campaign level (or one endless wave) as a script of beats with a rhythm:
    /// a trickle to warm up, a HORDE surge, a breather with a supply drop, then the final push (and the boss
    /// every 5th level). Reports progress to the HUD and tells the game when everything is dead.
    public class Level : MonoBehaviour
    {
        public static Level I;

        class Beat { public string banner, sub; public int count; public float interval, delay; public int packMin = 1, packMax = 2; public bool boss, crate; }

        public int Number { get; private set; }
        public bool Endless { get; private set; }
        public int Total { get; private set; }
        public int Resolved { get; private set; }
        public bool Running { get; private set; }
        public Zombie Boss { get; private set; }

        readonly List<Beat> beats = new List<Beat>();
        int beat, spawnedInBeat;
        float timer;
        bool bannerShown;
        System.Random rnd;
        const int MaxAlive = 110;

        void Awake() { I = this; }

        /// n: campaign level, or the wave number in endless mode.
        public void Begin(int n, bool endless)
        {
            Number = n;
            Endless = endless;
            rnd = new System.Random(endless ? System.Environment.TickCount : 1000 + n * 7919);
            beats.Clear();
            int total = endless ? Mathf.Min(150, 16 + 8 * n) : Mathf.Min(140, 14 + 6 * n);
            bool boss = ZType.IsBossLevel(n);
            int a = Mathf.RoundToInt(total * 0.3f), b = Mathf.RoundToInt(total * 0.45f), c = total - a - b;
            beats.Add(new Beat { count = a, interval = 1f, delay = 1.2f, packMin = 1, packMax = 2 });
            beats.Add(new Beat { banner = "HORDE INCOMING!", sub = "Here they come!", count = b, interval = 0.38f, delay = 2f, packMin = 2, packMax = 4 });
            beats.Add(new Beat { count = 0, delay = 4.5f, crate = n >= 2 || endless });
            beats.Add(new Beat { banner = boss ? "BOSS INCOMING!" : "FINAL WAVE!", sub = boss ? ZType.Boss.name : "Hold the wall!", count = c, interval = 0.5f, delay = 2f, packMin = 2, packMax = 3, boss = boss });
            Total = total + (boss ? 1 : 0);
            Resolved = 0;
            beat = 0; spawnedInBeat = 0; timer = 0f; bannerShown = false;
            Boss = null;
            Running = true;
        }

        public void Stop() { Running = false; }

        /// A zombie was killed or climbed over the wall.
        public void OnResolved(Zombie z)
        {
            if (!Running) return;
            Resolved++;
            if (z == Boss) Boss = null;
        }

        float HpScale { get { return ZType.HpScale(Endless ? Mathf.Max(1, Mathf.RoundToInt(Number * 1.2f)) : Number); } }
        float SpeedScale { get { return ZType.SpeedScale(Number); } }
        int TypeLevel { get { return Endless ? Number + 1 : Number; } }

        void Update()
        {
            if (!Running || Game.I == null || !Game.I.Playing) return;
            if (Hud.I != null) Hud.I.SetProgress(Resolved, Total);
            if (beat < beats.Count)
            {
                var bt = beats[beat];
                timer += Time.deltaTime;
                if (timer < bt.delay) return;
                if (!bannerShown)
                {
                    bannerShown = true;
                    if (bt.banner != null && Hud.I != null) { Hud.I.Banner(bt.banner, bt.sub, 2f); SoundBank.I.Play(SoundBank.I.horn, 0.7f); }
                    if (bt.crate) Crate.Drop(new Vector3((float)(rnd.NextDouble() * 4.0 - 2.0), 0f, 9f + (float)rnd.NextDouble() * 8f));
                    if (bt.boss) Boss = Spawn(ZType.Boss, 0f);
                }
                if (spawnedInBeat >= bt.count) { NextBeat(); return; }
                if (timer < bt.delay + bt.interval || Zombie.Alive.Count >= MaxAlive) return;
                timer = bt.delay;
                int pack = rnd.Next(bt.packMin, bt.packMax + 1);
                for (int i = 0; i < pack && spawnedInBeat < bt.count; i++)
                {
                    Spawn(ZType.Pick(TypeLevel, rnd), (float)(rnd.NextDouble() * 2.0 - 1.0) * (Arena.HalfWidth - 0.7f));
                    spawnedInBeat++;
                }
                return;
            }
            // everything is out: the level ends when the street is clear (and the last climbers are over)
            if (Zombie.Alive.Count > 0) { timer = 0f; return; }
            timer += Time.deltaTime;
            if (Resolved >= Total || timer > 2.5f) { Running = false; Game.I.LevelCleared(); }
        }

        void NextBeat()
        {
            beat++;
            if (beat >= beats.Count) { timer = 0f; return; }
            spawnedInBeat = 0;
            timer = 0f;
            bannerShown = false;
        }

        Zombie Spawn(ZType t, float x)
        {
            var pos = new Vector3(x, 0f, Arena.SpawnZ + (float)rnd.NextDouble() * 6f);
            return Zombie.Spawn(t, pos, HpScale, SpeedScale);
        }
    }
}
