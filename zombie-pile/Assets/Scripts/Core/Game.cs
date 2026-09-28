using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// Run flow: menu -> waves (the horde piles against the wall) -> pick 1 of 3 upgrades -> next wave ... until the wall falls.
    public class Game : MonoBehaviour
    {
        public static Game I;
        public const string Version = "v0.3";

        public bool Playing { get; private set; }
        public int Wave { get; private set; }
        public int Kills { get; private set; }
        public float WallHp { get; private set; }
        public float WallMax { get; private set; }

        int toSpawn, brutesLeft, spawnedThisWave, waveTotal;
        float spawnT, spawnEvery, zHp, zSpeed, comboT;
        int combo;
        bool waveOver;
        const int MaxAlive = 85;

        class Upgrade { public string title, desc; public Color color; public Action apply; public Func<bool> allowed; }
        List<Upgrade> pool;

        void Awake() { I = this; }

        void Start()
        {
            Arena.Build();
            Gunner.Build();
            Hud.Build();
            BuildUpgrades();
            PlatformSDK.Init(() => PlatformSDK.LoadingStop());
            Hud.I.ShowMenu(Save.Data.bestWave);
            // zombies shambling toward the wall behind the menu
            for (int i = 0; i < 14; i++)
                Zombie.Spawn(new Vector3(UnityEngine.Random.Range(-4f, 4f), 1f, 6f + i * 1.4f), 1f, 3.5f, false);
        }

        public void Delay(float seconds, Action a) { StartCoroutine(DelayCo(seconds, a)); }
        IEnumerator DelayCo(float s, Action a) { yield return new WaitForSeconds(s); a(); }

        // ------------------------------------------------------------------ run
        public void StartRun()
        {
            foreach (var z in FindObjectsByType<Zombie>(FindObjectsSortMode.None)) Destroy(z.gameObject);
            Zombie.Alive.Clear();
            var g = Gunner.I;
            g.fireRate = 10f; g.damage = 1f; g.headMult = 2.5f; g.bullets = 1; g.pierce = 0;
            g.barrelCooldown = 5f; g.barrelRadius = 3.6f; g.cluster = false;
            WallMax = WallHp = 100f;
            Kills = 0;
            Wave = 0;
            Save.Data.runs++;
            Hud.I.HideMenu();
            Hud.I.SetWall(1f);
            NextWave();
        }

        void NextWave()
        {
            Wave++;
            int n = Wave;
            // hordes, not handfuls: dozens of fast, fragile zombies
            waveTotal = toSpawn = 36 + 16 * (n - 1);
            brutesLeft = n % 3 == 0 ? n / 3 : 0;
            zHp = 1f + 0.45f * (n - 1);
            zSpeed = Mathf.Min(6.5f, 4.4f + 0.2f * n);
            spawnEvery = Mathf.Max(0.25f, 0.55f - 0.03f * n);
            spawnT = 1.2f;
            spawnedThisWave = 0;
            waveOver = false;
            Playing = true;
            SoundBank.I.Play(SoundBank.I.horn, 0.7f);
            Hud.I.Banner("WAVE " + n, brutesLeft > 0 ? "BRUTES INCOMING!" : "Don't let them climb the wall!");
            Hud.I.SetWave(n);
            PlatformSDK.GameplayStart();
        }

        void Update()
        {
            comboT -= Time.deltaTime;
            if (comboT <= 0f) combo = 0;
            if (!Playing) return;

            spawnT -= Time.deltaTime;
            if (toSpawn > 0 && spawnT <= 0f && Zombie.Alive.Count < MaxAlive)
            {
                spawnT = spawnEvery * UnityEngine.Random.Range(0.6f, 1.4f);
                int burst = UnityEngine.Random.Range(3, 7);   // packs of runners
                for (int i = 0; i < burst && toSpawn > 0; i++) SpawnOne();
            }
            Hud.I.SetLeft(toSpawn + Zombie.Alive.Count);
            if (!waveOver && toSpawn == 0 && Zombie.Alive.Count == 0)
            {
                waveOver = true;
                Playing = false;
                PlatformSDK.GameplayStop();
                if (Wave > Save.Data.bestWave) { Save.Data.bestWave = Wave; }
                Save.Write();
                Hud.I.Banner("WAVE " + Wave + " CLEARED!", "Pick an upgrade");
                SoundBank.I.Play(SoundBank.I.chime, 0.8f);
                PlatformSDK.HappyTime();
                Delay(1.4f, OfferUpgrades);
            }
        }

        void SpawnOne()
        {
            toSpawn--;
            spawnedThisWave++;
            bool brute = brutesLeft > 0 && spawnedThisWave > waveTotal * 0.4f && UnityEngine.Random.value < 0.25f;
            if (brute) brutesLeft--;
            if (toSpawn == 0 && brutesLeft > 0) { brute = true; brutesLeft--; toSpawn += brutesLeft; brutesLeft = 0; }
            var pos = new Vector3(UnityEngine.Random.Range(-Arena.HalfWidth + 0.8f, Arena.HalfWidth - 0.8f), brute ? 2f : 1.1f,
                Arena.SpawnZ + UnityEngine.Random.Range(-3f, 3f));
            Zombie.Spawn(pos, zHp, zSpeed, brute);
        }

        public void OnKill(Zombie z, bool headshot, Vector3 at)
        {
            Kills++;
            Save.Data.totalKills++;
            combo = comboT > 0f ? combo + 1 : 1;
            comboT = 0.7f;
            Hud.I.SetKills(Kills);
            if (z.IsBrute) Hud.I.Popup(at, "BRUTE DOWN!", new Color(1f, 0.45f, 0.3f), 1.5f);
            else if (headshot) Hud.I.Popup(at, "HEADSHOT!", new Color(1f, 0.9f, 0.3f), 1.1f);
            if (combo >= 3)
            {
                string c = combo >= 8 ? "MASSACRE x" + combo : combo >= 5 ? "MULTI KILL x" + combo : combo == 3 ? "TRIPLE!" : "QUAD!";
                Hud.I.Popup(at + Vector3.up * 1.2f, c, new Color(0.5f, 1f, 0.45f), 1.2f + Mathf.Min(combo, 10) * 0.05f);
            }
        }

        public void OnBreach(Zombie z)
        {
            if (WallHp <= 0f) return;
            WallHp -= z.IsBrute ? 30f : 12f;
            SoundBank.I.Play(SoundBank.I.breach, 0.9f);
            CameraRig.I.Shake(0.7f);
            Hud.I.FlashDamage();
            Hud.I.Popup(new Vector3(z.transform.position.x, Arena.WallHeight + 1.5f, 0f), "OVER THE WALL!", new Color(1f, 0.3f, 0.25f), 1.3f);
            Hud.I.SetWall(Mathf.Max(0f, WallHp / WallMax));
            if (WallHp <= 0f) GameOver();
        }

        void GameOver()
        {
            Playing = false;
            toSpawn = 0;
            PlatformSDK.GameplayStop();
            SoundBank.I.Play(SoundBank.I.lose, 0.9f);
            bool best = Wave > Save.Data.bestWave;
            if (best) Save.Data.bestWave = Wave;
            Save.Write();
            Hud.I.ShowGameOver(Wave, Kills, Save.Data.bestWave, best);
        }

        public void Retry()
        {
            // an ad break between runs (the SDK decides whether one actually shows)
            PlatformSDK.Midgame(() => AudioListener.pause = true, () => { AudioListener.pause = false; StartRun(); });
        }

        // ------------------------------------------------------------------ upgrades
        void BuildUpgrades()
        {
            var g = Gunner.I;
            pool = new List<Upgrade>
            {
                new Upgrade { title = "RAPID FIRE", desc = "+30% fire rate", color = new Color(1f, 0.6f, 0.2f), apply = () => g.fireRate *= 1.3f },
                new Upgrade { title = "HOLLOW POINTS", desc = "+35% damage", color = new Color(0.9f, 0.3f, 0.3f), apply = () => g.damage *= 1.35f },
                new Upgrade { title = "SPLIT SHOT", desc = "+1 bullet per shot", color = new Color(0.3f, 0.6f, 1f), apply = () => g.bullets++, allowed = () => g.bullets < 5 },
                new Upgrade { title = "PIERCING", desc = "Bullets go through +1 zombie", color = new Color(0.6f, 0.4f, 1f), apply = () => g.pierce++, allowed = () => g.pierce < 4 },
                new Upgrade { title = "HEADHUNTER", desc = "Headshots x1.5 damage", color = new Color(1f, 0.85f, 0.2f), apply = () => g.headMult *= 1.5f },
                new Upgrade { title = "BIG BOOM", desc = "Barrels blast 30% wider", color = new Color(1f, 0.4f, 0.1f), apply = () => g.barrelRadius *= 1.3f },
                new Upgrade { title = "QUICK THROW", desc = "Barrels recharge 30% faster", color = new Color(0.25f, 0.8f, 0.5f), apply = () => g.barrelCooldown *= 0.7f, allowed = () => g.barrelCooldown > 1.5f },
                new Upgrade { title = "CLUSTER BOMBS", desc = "Barrels split into 3 more blasts", color = new Color(0.95f, 0.35f, 0.55f), apply = () => g.cluster = true, allowed = () => !g.cluster },
                new Upgrade { title = "REPAIR", desc = "Fix the wall: +40 HP", color = new Color(0.55f, 0.6f, 0.65f), apply = () => { WallHp = Mathf.Min(WallMax, WallHp + 40f); Hud.I.SetWall(WallHp / WallMax); }, allowed = () => WallHp < WallMax - 10f },
                new Upgrade { title = "SANDBAGS", desc = "+25 max wall HP (and fill it)", color = new Color(0.8f, 0.7f, 0.45f), apply = () => { WallMax += 25f; WallHp = WallMax; Hud.I.SetWall(1f); } },
            };
        }

        void OfferUpgrades()
        {
            if (WallHp <= 0f) return;
            var options = new List<Upgrade>();
            var candidates = pool.FindAll(u => u.allowed == null || u.allowed());
            while (options.Count < 3 && candidates.Count > 0)
            {
                int i = UnityEngine.Random.Range(0, candidates.Count);
                options.Add(candidates[i]);
                candidates.RemoveAt(i);
            }
            var titles = new string[options.Count];
            var descs = new string[options.Count];
            var colors = new Color[options.Count];
            for (int i = 0; i < options.Count; i++) { titles[i] = options[i].title; descs[i] = options[i].desc; colors[i] = options[i].color; }
            Hud.I.ShowUpgrades(titles, descs, colors, pickIdx =>
            {
                options[pickIdx].apply();
                SoundBank.I.Play(SoundBank.I.chime, 0.7f, 1.2f);
                // clear the bodies, short breather, next wave (with an ad break every 3 waves)
                foreach (var z in FindObjectsByType<Zombie>(FindObjectsSortMode.None)) if (!z.IsAlive) Destroy(z.gameObject);
                if (Wave % 3 == 0) PlatformSDK.Midgame(() => AudioListener.pause = true, () => { AudioListener.pause = false; Delay(0.6f, NextWave); });
                else Delay(0.6f, NextWave);
            });
        }
    }
}
