using System;
using System.Collections;
using UnityEngine;

namespace ZombiePile
{
    /// The flow: menu -> (new zombie card) -> level -> win (stars, coins, x2 ad) or overrun (coins kept) -> armory
    /// -> next level. Campaign progress is saved, so a loss only replays that level. Endless mode is separate:
    /// waves forever, best wave recorded. Coins from every kill go straight into the savings.
    public class Game : MonoBehaviour
    {
        public static Game I;
        public const string Version = "v0.6";

        public bool Playing { get; private set; }
        public bool Endless { get; private set; }
        public int LevelNumber { get; private set; }
        public float WallHp { get; private set; }
        public float WallMax { get; private set; }
        public int RunCoins { get; private set; }
        public int RunKills { get; private set; }

        // power-ups from supply crates
        public static float RateBoost = 1f, DamageBoost = 1f;
        float rateT, damageT;

        float comboT, attractT;
        int combo;
        bool climbWarned, attract;

        void Awake() { I = this; }

        void Start()
        {
            Arena.Build();
            gameObject.AddComponent<Level>();
            Shooter.BuildBoth();
            Hud.Build();
            Screens.Build();
            PlatformSDK.Init(() => PlatformSDK.LoadingStop());
            ShowMenu();
        }

        public void Delay(float seconds, Action a) { StartCoroutine(DelayCo(seconds, a)); }
        IEnumerator DelayCo(float s, Action a) { yield return new WaitForSeconds(s); a(); }

        // ------------------------------------------------------------------ flow
        public void ShowMenu()
        {
            Playing = false;
            Level.I.Stop();
            Time.timeScale = 1f;
            Zombie.KillAll();
            Hud.I.Show(false);
            CameraRig.I.SetView(0, true);
            Screens.I.Menu();
            // the horde shambling up the street behind the menu (topped up as they climb over)
            attract = true;
            for (int i = 0; i < 22; i++) AttractZombie(6f + i * 1.9f);
        }

        /// Campaign level n (shows the NEW ZOMBIE / BOSS card first when there is something new).
        public void PlayLevel(int n)
        {
            var intro = ZType.NewAt(n);
            if (intro != null && (Save.Data.seen & (1 << intro.id)) == 0)
            {
                Save.Data.seen |= 1 << intro.id;
                Save.Write();
                Screens.I.Intro(intro, () => Begin(n, false));
                return;
            }
            Begin(n, false);
        }

        public void PlayEndless() { Begin(1, true); }

        void AttractZombie(float z)
        {
            Zombie.Spawn(UnityEngine.Random.value < 0.2f ? ZType.Walker2 : ZType.Walker, new Vector3(UnityEngine.Random.Range(-3.2f, 3.2f), 0f, z), 1f, 0.6f);
        }

        void Begin(int n, bool endless)
        {
            attract = false;
            Screens.I.HideAll();
            Zombie.KillAll();
            Fx.I.ClearStains();
            Arena.I.ResetDrums();
            foreach (var c in FindObjectsByType<Crate>(FindObjectsSortMode.None)) Destroy(c.gameObject);
            Endless = endless;
            LevelNumber = n;
            WallMax = Econ.WallMax(Econ.Lvl(Up.WallHp));
            WallHp = WallMax;
            RunCoins = 0; RunKills = 0; combo = 0;
            RateBoost = DamageBoost = 1f; rateT = damageT = 0f;
            climbWarned = false;
            Shooter.Wall.RefreshWeapon();
            Shooter.Wall.ResetCooldowns();
            Time.timeScale = 1f;
            CameraRig.I.SetView(0, true);
            Hud.I.Show(true);
            Hud.I.SetLevel(endless ? "WAVE " + n : "LEVEL " + n);
            Hud.I.SetWall(1f);
            Hud.I.Banner(endless ? "WAVE " + n : "LEVEL " + n, endless ? "ENDLESS MODE" : "Hold the wall!", 2f);
            SoundBank.I.Play(SoundBank.I.horn, 0.7f);
            Save.Data.plays++;
            Level.I.Begin(n, endless);
            Playing = true;
            PlatformSDK.GameplayStart();
        }

        public void LevelCleared()
        {
            if (!Playing) return;
            if (Endless)
            {
                // next wave after a short breather; the wall gets a little patch-up
                int reward = 10 + 5 * LevelNumber;
                Hud.I.Banner("WAVE " + LevelNumber + " CLEARED!", "+" + reward + " coins", 2f);
                SoundBank.I.Play(SoundBank.I.chime, 0.8f);
                AddCoins(reward, Arena.TowerSpot);
                WallHp = Mathf.Min(WallMax, WallHp + WallMax * 0.15f);
                Hud.I.SetWall(WallHp / WallMax);
                int next = LevelNumber + 1;
                Delay(3f, () =>
                {
                    if (!Playing || !Endless) return;
                    LevelNumber = next;
                    Hud.I.SetLevel("WAVE " + next);
                    Hud.I.Banner("WAVE " + next, ZType.IsBossLevel(next) ? "Boss wave!" : "", 1.6f);
                    Level.I.Begin(next, true);
                });
                return;
            }
            Playing = false;
            PlatformSDK.GameplayStop();
            PlatformSDK.HappyTime();
            float k = WallHp / WallMax;
            int stars = k >= 0.9f ? 3 : k >= 0.5f ? 2 : 1;
            int bonus = Mathf.RoundToInt(Econ.LevelBonus(LevelNumber) * Econ.CoinMult);
            Save.Data.coins += bonus;
            RunCoins += bonus;
            Save.SetStars(LevelNumber, stars);
            if (LevelNumber >= Save.Data.level) Save.Data.level = LevelNumber + 1;
            Save.Write();
            SoundBank.I.Play(SoundBank.I.fanfare, 0.9f);
            StartCoroutine(SlowMo());
            int n = LevelNumber;
            Delay(1.3f, () => Screens.I.Complete(n, stars, RunKills, RunCoins));
        }

        IEnumerator SlowMo()
        {
            Time.timeScale = 0.35f;
            yield return new WaitForSecondsRealtime(0.9f);
            Time.timeScale = 1f;
        }

        void Lose()
        {
            if (!Playing) return;
            Playing = false;
            Level.I.Stop();
            PlatformSDK.GameplayStop();
            SoundBank.I.Play(SoundBank.I.lose, 0.9f);
            bool best = false;
            if (Endless && LevelNumber > Save.Data.bestWave) { Save.Data.bestWave = LevelNumber; best = true; }
            Save.Write();
            int n = LevelNumber, coins = RunCoins;
            if (Endless) Delay(1.2f, () => Screens.I.EndlessOver(n, Save.Data.bestWave, coins, best));
            else Delay(1.2f, () => Screens.I.Failed(n, coins));
        }

        /// x2 coins for watching an ad on the results screen.
        public void DoubleCoins(Action done)
        {
            int extra = RunCoins;
            PlatformSDK.Rewarded(() => AudioListener.pause = true,
                () => { AudioListener.pause = false; Save.Data.coins += extra; RunCoins += extra; Save.Write(); SoundBank.I.Play(SoundBank.I.buy, 0.8f); if (done != null) done(); },
                () => { AudioListener.pause = false; });
        }

        /// Between levels: an ad break every 3 levels (the SDK decides whether one shows).
        public void AfterBreak(int finished, Action next)
        {
            if (finished % 3 == 0) PlatformSDK.Midgame(() => AudioListener.pause = true, () => { AudioListener.pause = false; next(); });
            else next();
        }

        // ------------------------------------------------------------------ events
        public void OnKill(Zombie z, Vector3 at, bool headshot)
        {
            if (Level.I != null) Level.I.OnResolved(z);
            if (!Playing) return;
            RunKills++;
            Save.Data.totalKills++;
            int coins = Mathf.Max(1, Mathf.RoundToInt(z.Type.coins * (1f + 0.08f * (LevelNumber - 1)) * Econ.CoinMult));
            AddCoins(coins, at);
            combo = comboT > 0f ? combo + 1 : 1;
            comboT = 0.8f;
            if (z.IsBoss) { Hud.I.Popup(at + Vector3.up, "BOSS DOWN!", new Color(1f, 0.4f, 0.3f), 1.8f); CameraRig.I.Shake(1f); }
            else if (z.IsBrute) Hud.I.Popup(at, "BRUTE DOWN!", new Color(1f, 0.45f, 0.3f), 1.4f);
            else if (z.Airborne) Hud.I.Popup(at, "AIR SHOT!", new Color(0.55f, 0.9f, 1f), 1.2f);
            else if (headshot) Hud.I.Popup(at, "HEADSHOT!", new Color(1f, 0.85f, 0.25f), 1.1f);
            if (combo >= 3)
            {
                string c = combo >= 10 ? "MASSACRE x" + combo : combo >= 6 ? "RAMPAGE x" + combo : "COMBO x" + combo;
                Hud.I.Popup(at + Vector3.up * 1.2f, c, new Color(0.6f, 1f, 0.4f), 1f + Mathf.Min(combo, 12) * 0.04f);
            }
        }

        public void OnBreach(Zombie z)
        {
            if (Level.I != null) Level.I.OnResolved(z);
            if (!Playing) return;
            Damage(z.Type.wallDamage);
            SoundBank.I.Play(SoundBank.I.breach, 0.9f);
            CameraRig.I.Shake(0.6f);
            Hud.I.Popup(new Vector3(z.transform.position.x, Arena.WallHeight + 1.2f, Arena.WallFront), "OVER THE WALL!", new Color(1f, 0.3f, 0.25f), 1.3f);
        }

        /// Brutes and the boss pounding on the wall.
        public void WallHit(float dmg, Vector3 at, bool big)
        {
            if (!Playing) return;
            Damage(dmg);
            SoundBank.I.Play(SoundBank.I.smash, big ? 0.9f : 0.5f, UnityEngine.Random.Range(0.9f, 1.1f));
            CameraRig.I.Shake(big ? 0.7f : 0.3f);
            Fx.I.Dust(at, big ? 1.6f : 0.8f);
        }

        void Damage(float d)
        {
            WallHp -= d;
            Hud.I.FlashDamage();
            Hud.I.SetWall(Mathf.Max(0f, WallHp / WallMax));
            if (WallHp <= 0f) Lose();
        }

        public void AddCoins(int amount, Vector3 at)
        {
            if (amount <= 0) return;
            Save.Data.coins += amount;
            RunCoins += amount;
            Hud.I.CoinBurst(at, amount);
        }

        public void GivePowerUp(PowerUp p)
        {
            switch (p)
            {
                case PowerUp.RapidFire: RateBoost = 2f; rateT = 8f; Hud.I.Banner("RAPID FIRE!", "8 seconds", 1.4f); break;
                case PowerUp.DoubleDamage: DamageBoost = 2f; damageT = 8f; Hud.I.Banner("DOUBLE DAMAGE!", "8 seconds", 1.4f); break;
                case PowerUp.Reload: Shooter.Wall.ResetCooldowns(); Hud.I.Banner("RELOADED!", "Barrel and airstrike ready", 1.4f); break;
                case PowerUp.Repair:
                    WallHp = Mathf.Min(WallMax, WallHp + WallMax * 0.25f);
                    Hud.I.SetWall(WallHp / WallMax);
                    Hud.I.Banner("WALL REPAIRED!", "+25%", 1.4f);
                    break;
            }
            SoundBank.I.Play(SoundBank.I.buy, 0.8f);
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Time.deltaTime;
            comboT -= dt;
            if (rateT > 0f) { rateT -= dt; if (rateT <= 0f) RateBoost = 1f; }
            if (damageT > 0f) { damageT -= dt; if (damageT <= 0f) DamageBoost = 1f; }
            Hud.I.SetBoost(rateT > 0f ? "RAPID FIRE" : damageT > 0f ? "DOUBLE DMG" : null, Mathf.Max(rateT, damageT) / 8f);
            if (attract && !Playing)
            {
                attractT -= dt;
                if (attractT <= 0f && Zombie.Alive.Count < 22) { attractT = 1.2f; AttractZombie(Arena.SpawnZ); }
            }
            if (!Playing) return;

            // field repair (armory upgrade)
            float rep = Econ.RepairRate(Econ.Lvl(Up.Repair));
            if (rep > 0f && WallHp < WallMax) { WallHp = Mathf.Min(WallMax, WallHp + rep * dt); Hud.I.SetWall(WallHp / WallMax); }

            // switch shooters, throw a barrel, call an airstrike
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Q)) CameraRig.I.Toggle();
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(1)) Shooter.Wall.ThrowBarrel();
            if (Input.GetKeyDown(KeyCode.F)) Shooter.Wall.CallAirstrike();

            // zombies high on the wall while the player looks from the wall: point at the tower view
            bool danger = Pile.MaxHeight > Arena.WallHeight * 0.45f;
            Hud.I.SetClimbWarning(danger && CameraRig.I.View == 0);
            if (danger && !climbWarned && !Save.Data.switchTip)
            {
                climbWarned = true;
                Save.Data.switchTip = true;
                Hud.I.Banner("THEY'RE CLIMBING!", "Press TAB to switch to Lis on the tower", 3f);
            }
        }
    }
}
