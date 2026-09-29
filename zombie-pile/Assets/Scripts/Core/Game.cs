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
        public const string Version = "v0.8";

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

        float comboT, attractT, chipAcc, chipT;
        int combo;
        bool climbWarned, attract;

        void Awake() { I = this; }

        void Start()
        {
            Arena.Build();
            gameObject.AddComponent<Level>();
            Shooter.BuildAll();
            Hud.Build();
            Screens.Build();
            Net.RoomStarted += OnRoomStarted;
            Net.RoomClosed += OnRoomClosed;
            PlatformSDK.Init(OnPlatformReady);
            ShowMenu();
            booted = true;
        }

        bool booted;

        /// The CrazyGames SDK is up (after the menu is already showing): the account save may have been newer,
        /// and the page may have been opened from a friend's co-op invite link.
        void OnPlatformReady()
        {
            PlatformSDK.LoadingStop();
            if (!booted) return;
            if (!Playing && Screens.I.OnMenu) ShowMenu();
            string room = PlatformSDK.InviteRoom();
            if (room.Length == 5 && !Net.InRoom && !Playing) Net.Join(room);
        }

        void OnDestroy() { Net.RoomStarted -= OnRoomStarted; Net.RoomClosed -= OnRoomClosed; }

        // ------------------------------------------------------------------ co-op
        void OnRoomStarted(bool host)
        {
            attract = false;
            Zombie.KillAll();
            Screens.I.HideAll();
            if (host) PlayCoop();
            else Screens.I.Waiting("WAITING FOR THE HOST...");
        }

        void OnRoomClosed(string why)
        {
            Playing = false;
            Time.timeScale = 1f;
            Level.I.Stop();
            ShowMenu();
            Screens.I.Toast(why);
        }

        /// The host starts (or continues) the co-op run: everybody plays the host's current campaign level.
        public void PlayCoop() { Begin(Save.Data.level, false); }

        // ------------------------------------------------------------------ shown here and, from the host, on every screen
        public static void Say(string title, string sub, float dur)
        {
            if (Hud.I != null) Hud.I.Banner(title, sub, dur);
            if (Net.IsHost) Net.Host.Banner(title, sub, dur);
        }

        public static void Pop(Vector3 at, string text, Color c, float size)
        {
            if (Hud.I != null) Hud.I.Popup(at, text, c, size);
            if (Net.IsHost) Net.Host.Popup(at, text, c, size);
        }

        public static void Dmg(Vector3 at, int amount, bool crit)
        {
            if (Hud.I != null) Hud.I.Damage(at, amount, crit);
            if (Net.IsHost) Net.Host.Damage(at, amount, crit);
        }

        public void Delay(float seconds, Action a) { StartCoroutine(DelayCo(seconds, a)); }
        IEnumerator DelayCo(float s, Action a) { yield return new WaitForSeconds(s); a(); }

        // ------------------------------------------------------------------ flow
        public void ShowMenu()
        {
            Playing = false;
            Level.I.Stop();
            Time.timeScale = 1f;
            if (Net.IsOnline || Net.InRoom) Net.Leave();
            Zombie.KillAll();
            Hud.I.Show(false);
            CameraRig.I.SetView(1, true);
            Arena.I.SetTheme(Theme.ForLevel(Save.Data.level));
            Survivors.I.Reset();
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
            var theme = Theme.All[Theme.ForLevel(n)];
            Arena.I.SetTheme(Theme.ForLevel(n));
            Survivors.I.Reset();
            WallMax = Econ.WallMax(Econ.Lvl(Up.WallHp));
            WallHp = WallMax;
            RunCoins = 0; RunKills = 0; combo = 0; chipAcc = 0f;
            RateBoost = DamageBoost = 1f; rateT = damageT = 0f;
            climbWarned = false;
            Shooter.Wall.RefreshWeapon();
            Shooter.Wall.ResetCooldowns();
            Squad.ResetAll();
            Time.timeScale = 1f;
            Playing = false;
            Level.I.Stop();
            Hud.I.Show(false);
            Hud.I.SetLevel(endless ? "WAVE " + n : "LEVEL " + n);
            Hud.I.SetWall(1f);
            Save.Data.plays++;
            // establishing shot over the camp: what we are protecting, and where the horde comes from
            Hud.I.ShowStory(endless ? "WAVE " + n : "LEVEL " + n, theme.name, endless ? "Endless. They will not stop." : theme.line);
            SoundBank.I.Play(SoundBank.I.horn, 0.6f);
            if (Net.IsHost) Net.Host.LevelBegin(n, endless);
            CameraRig.I.PlayIntro(Net.IsOnline ? Net.LocalSlot : 1, () => StartPlaying(n, endless));
        }

        /// A co-op guest: the host began a level.
        public void ClientBegin(int n, bool endless) { Screens.I.HideAll(); Begin(n, endless); }

        /// A co-op guest: the host moved on to the next endless wave.
        public void ClientWave(int n)
        {
            LevelNumber = n;
            Arena.I.SetTheme(Theme.ForLevel(n));
            Hud.I.SetLevel("WAVE " + n);
            Hud.I.Banner("WAVE " + n, ZType.IsBossLevel(n) ? "Boss wave!" : "", 1.6f);
        }

        /// A co-op guest: the wall is standing at the end. The host worked out stars and the level bonus.
        public void ClientLevelClear(int stars, int kills, int bonus, int runCoins)
        {
            if (!Playing) return;
            Playing = false;
            Save.Data.coins += bonus;
            RunCoins += bonus;
            RunKills = kills;
            Save.Write();
            SoundBank.I.Play(SoundBank.I.fanfare, 0.9f);
            StartCoroutine(SlowMo());
            int n = LevelNumber, coins = RunCoins;
            Delay(1.3f, () => Screens.I.Complete(n, stars, kills, coins));
        }

        /// A co-op guest: the wall fell.
        public void ClientLose(int level, int coins, bool endless, int best, bool newBest)
        {
            if (!Playing) return;
            Playing = false;
            SoundBank.I.Play(SoundBank.I.lose, 0.9f);
            Hud.I.Show(false);
            CameraRig.I.ShowCamp();
            Hud.I.Banner("NEW HAVEN HAS FALLEN...", "", 3.5f);
            if (endless && newBest) Save.Data.bestWave = Mathf.Max(Save.Data.bestWave, best);
            Save.Write();
            int c = RunCoins;
            if (endless) Delay(3.6f, () => Screens.I.EndlessOver(level, Save.Data.bestWave, c, newBest));
            else Delay(3.6f, () => Screens.I.Failed(level, c));
        }

        /// A co-op guest: numbers from the host's snapshot.
        public void NetWall(float hp, float max, int kills, int coins)
        {
            WallHp = hp; WallMax = Mathf.Max(1f, max); RunKills = kills;
            Hud.I.SetWall(Mathf.Clamp01(hp / WallMax));
        }

        public void AddCoinsClient(int amount, Vector3 at)
        {
            if (amount <= 0) return;
            Save.Data.coins += amount;
            RunCoins += amount;
            Hud.I.CoinBurst(at, amount);
        }

        /// The swoop is over: the fight begins.
        public void StartPlaying(int n, bool endless)
        {
            Hud.I.HideStory();
            Hud.I.Show(true);
            Hud.I.Banner(endless ? "WAVE " + n : "LEVEL " + n, endless ? "ENDLESS MODE" : "Hold the wall!", 2f);
            if (!Net.IsClient) Level.I.Begin(n, endless);
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
                Say("WAVE " + LevelNumber + " CLEARED!", "+" + reward + " coins", 2f);
                SoundBank.I.Play(SoundBank.I.chime, 0.8f);
                AddCoins(reward, Arena.TowerSpot);
                WallHp = Mathf.Min(WallMax, WallHp + WallMax * 0.15f);
                Hud.I.SetWall(WallHp / WallMax);
                int next = LevelNumber + 1;
                Delay(3f, () =>
                {
                    if (!Playing || !Endless) return;
                    LevelNumber = next;
                    Arena.I.SetTheme(Theme.ForLevel(next));
                    Hud.I.SetLevel("WAVE " + next);
                    Say("WAVE " + next, ZType.IsBossLevel(next) ? "Boss wave!" : "", 1.6f);
                    if (Net.IsHost) Net.Host.WaveBegin(next);
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
            Survivors.I.SetMood(Survivors.Mood.Cheer);
            if (LevelNumber >= Save.Data.level) Save.Data.level = LevelNumber + 1;
            Save.Write();
            SoundBank.I.Play(SoundBank.I.fanfare, 0.9f);
            StartCoroutine(SlowMo());
            int n = LevelNumber;
            if (Net.IsHost) Net.Host.LevelClear(stars, RunKills, bonus, RunCoins);
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
            // the wall falls: the camera goes back over the camp and the horde pours in
            Hud.I.Show(false);
            CameraRig.I.ShowCamp();
            Zombie.OverrunAll();
            Survivors.I.Scatter();
            Hud.I.Banner("NEW HAVEN HAS FALLEN...", "", 3.5f);
            bool best = false;
            if (Endless && LevelNumber > Save.Data.bestWave) { Save.Data.bestWave = LevelNumber; best = true; }
            Save.Write();
            int n = LevelNumber, coins = RunCoins;
            if (Net.IsHost) Net.Host.Fail(n, coins, Endless, Save.Data.bestWave, best);
            if (Endless) Delay(3.6f, () => Screens.I.EndlessOver(n, Save.Data.bestWave, coins, best));
            else Delay(3.6f, () => Screens.I.Failed(n, coins));
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
            if (z.IsBoss) { Pop(at + Vector3.up, "BOSS DOWN!", new Color(1f, 0.4f, 0.3f), 1.8f); CameraRig.I.Shake(1f); }
            else if (z.IsBrute) Pop(at, "BRUTE DOWN!", new Color(1f, 0.45f, 0.3f), 1.4f);
            else if (z.Airborne) Pop(at, "AIR SHOT!", new Color(0.55f, 0.9f, 1f), 1.2f);
            else if (headshot) Pop(at, "HEADSHOT!", new Color(1f, 0.85f, 0.25f), 1.1f);
            if (combo >= 3)
            {
                string c = combo >= 10 ? "MASSACRE x" + combo : combo >= 6 ? "RAMPAGE x" + combo : "COMBO x" + combo;
                Pop(at + Vector3.up * 1.2f, c, new Color(0.6f, 1f, 0.4f), 1f + Mathf.Min(combo, 12) * 0.04f);
            }
        }

        public void OnBreach(Zombie z)
        {
            if (Level.I != null) Level.I.OnResolved(z);
            if (!Playing) return;
            Damage(z.Type.wallDamage);
            Survivors.I.Flinch();
            SoundBank.I.Play(SoundBank.I.breach, 0.9f);
            CameraRig.I.Shake(0.6f);
            Pop(new Vector3(z.transform.position.x, Arena.WallHeight + 1.2f, Arena.WallFront), "OVER THE WALL!", new Color(1f, 0.3f, 0.25f), 1.3f);
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

        /// Zombies clinging to the wall chew on it: collected here and applied in small steps.
        public void WallChip(float dmg, Vector3 at)
        {
            if (!Playing || dmg <= 0f) return;
            chipAcc += dmg * (1f + 0.06f * (LevelNumber - 1));
            chipAt = at;
        }
        Vector3 chipAt;

        void Damage(float d, bool flash = true)
        {
            WallHp -= d;
            if (flash) Hud.I.FlashDamage();
            Hud.I.SetWall(Mathf.Max(0f, WallHp / WallMax));
            float k = WallHp / WallMax;
            Survivors.I.SetMood(k < 0.35f ? Survivors.Mood.Panic : Survivors.Mood.Calm);
            if (WallHp <= 0f) Lose();
        }

        public void AddCoins(int amount, Vector3 at)
        {
            if (amount <= 0) return;
            Save.Data.coins += amount;
            RunCoins += amount;
            Hud.I.CoinBurst(at, amount);
            if (Net.IsHost) Net.Host.Coins(at, amount);
        }

        public void GivePowerUp(PowerUp p)
        {
            switch (p)
            {
                case PowerUp.RapidFire: RateBoost = 2f; rateT = 8f; Say("RAPID FIRE!", "8 seconds", 1.4f); break;
                case PowerUp.DoubleDamage: DamageBoost = 2f; damageT = 8f; Say("DOUBLE DAMAGE!", "8 seconds", 1.4f); break;
                case PowerUp.Reload: Shooter.Wall.ResetCooldowns(); Say("RELOADED!", "Barrel and airstrike ready", 1.4f); break;
                case PowerUp.Repair:
                    WallHp = Mathf.Min(WallMax, WallHp + WallMax * 0.25f);
                    Hud.I.SetWall(WallHp / WallMax);
                    Say("WALL REPAIRED!", "+25%", 1.4f);
                    break;
            }
            SoundBank.I.Play(SoundBank.I.buy, 0.8f);
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            if (Hud.I == null) return;      // Start failed earlier: the first error in the Console is the real one
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

            if (!Net.IsClient) HostUpdate(dt);
            Inputs();
        }

        /// Chip damage and field repair: the simulation, so only offline and on the host.
        void HostUpdate(float dt)
        {
            // wall chip damage: every half second, with dust and a thump so the wall visibly takes it
            chipT += dt;
            if (chipT >= 0.5f)
            {
                chipT = 0f;
                if (chipAcc > 0.05f)
                {
                    float d = chipAcc; chipAcc = 0f;
                    Damage(d, false);
                    Hud.I.ChipFlash(Mathf.Clamp01(d / 3f));
                    if (Net.IsHost) Net.Host.Chip(chipAt, d);
                    Fx.I.Dust(new Vector3(chipAt.x, Mathf.Min(chipAt.y, Arena.WallHeight - 1f), Arena.WallFront + 0.3f), 0.7f);
                    SoundBank.I.Play(SoundBank.I.smash, Mathf.Clamp(0.12f + d * 0.08f, 0.1f, 0.4f), UnityEngine.Random.Range(0.8f, 1.1f));
                    CameraRig.I.Shake(Mathf.Clamp(d * 0.04f, 0.02f, 0.25f));
                }
            }

            // field repair (armory upgrade)
            float rep = Econ.RepairRate(Econ.Lvl(Up.Repair));
            if (rep > 0f && WallHp < WallMax) { WallHp = Mathf.Min(WallMax, WallHp + rep * dt); Hud.I.SetWall(WallHp / WallMax); }

        }

        void Inputs()
        {
            // switch shooters, throw a barrel, call an airstrike
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Q)) CameraRig.I.Toggle();
            if (Input.GetKeyDown(KeyCode.Alpha1)) CameraRig.I.Go(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) CameraRig.I.Go(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) CameraRig.I.Go(2);
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(1)) Shooter.Wall.ThrowBarrel();
            if (Input.GetKeyDown(KeyCode.F)) Shooter.Wall.CallAirstrike();

            // zombies high on the wall while the player looks from the wall: point at the tower view
            bool danger = Zombie.HighestClimb > Arena.WallHeight * 0.45f;
            Hud.I.SetClimbWarning(danger && CameraRig.I.View == 1);
            if (danger && !climbWarned && !Save.Data.switchTip)
            {
                climbWarned = true;
                Save.Data.switchTip = true;
                Hud.I.Banner("THEY'RE CLIMBING!", "Switch to a tower (1 or 3): snipers see the wall", 3f);
            }
        }
    }
}
