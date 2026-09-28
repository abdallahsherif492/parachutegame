using System;
using UnityEngine;

namespace SkyDrop
{
    public enum GState { Boot, Menu, Freefall, Canopy, Landed, Crashed, Results }

    /// Game flow + rules: jump, rings, coins, hazards, parachute timing, landing and rewards.
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public GState State { get; private set; }

        Transform worldRoot;
        Level level;
        Jumper jumper;
        Transform shadow;       // landing marker: where you'd touch down right now
        Material shadowMat;
        LevelConfig cfg;
        CameraRig cam;
        GameUI ui;

        // current run
        int runCoins, combo, ringsHit;
        float multiplier = 1f;
        int lockedZone = Zones.Max.Length - 1;
        float stateTime;
        Vector3 prevPos;
        Hazard pendingNearMiss;
        float nearMissTimer;
        ResultInfo lastResult;
        bool doubled;

        // tutorial (first ever jump)
        bool tutorial;
        int tutStep;
        float tutTimer;
        bool tutFreeze;

        // slow motion + ads
        float slowScale = 1f, slowTimer;
        float focus = 1f;   // automatic bullet-time close to the ground
        bool paused;
        int runsSinceAd;
        float lastAdTime = -1000f;

        static bool Mobile { get { return Application.isMobilePlatform; } }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            worldRoot = new GameObject("World").transform;
            cam = CameraRig.I;
        }

        void Start()
        {
            State = GState.Boot;
            PlatformSDK.Init(OnPlatformReady);
        }

        void OnPlatformReady()
        {
            SaveSystem.Load();
            SoundBank.ApplyMute();
            ui = GameUI.Create();
            if (!SaveSystem.Data.tutorialDone)
            {
                // First visit: no menu at all, straight into the action.
                LoadLevel(0, false);
                Jump();
            }
            else LoadLevel(SaveSystem.Data.current, false);
            PlatformSDK.LoadingStop();
        }

        // =============================================================================== flow

        void LoadLevel(int index, bool daily)
        {
            if (level != null) level.Destroy();
            if (Fx.I != null) Fx.I.Clear();
            Time.timeScale = 1f;
            slowTimer = 0f;
            tutFreeze = false;
            focus = 1f;
            cfg = daily ? Levels.MakeDaily(SaveSystem.Data.unlocked) : Levels.Make(index);
            level = LevelBuilder.Build(cfg, worldRoot);
            SpawnJumper();
            if (shadowMat == null) shadowMat = Mat.UniqueClear(new Color(0f, 0f, 0f, 0.4f), 0.2f);
            shadow = Shapes.Make("Shadow", MeshGen.Disc(20), shadowMat, level.root.transform, Vector3.zero, new Vector3(2.2f, 1f, 2.2f)).transform;
            shadow.gameObject.SetActive(false);
            cam.SetMode(CamMode.Menu, true);
            State = GState.Menu;
            lockedZone = Zones.Max.Length - 1;
            runCoins = 0;
            combo = 0;
            ringsHit = 0;
            multiplier = 1f;
            pendingNearMiss = null;
            doubled = false;
            InputReader.ResetState();
            ui.HideResults();
            ui.ShowHud(false, null);
            ui.ShowMenu(true, cfg);
        }

        void SpawnJumper()
        {
            if (jumper != null) Destroy(jumper.gameObject);
            jumper = Jumper.Create(level.root.transform, level.spawn, Economy.Skins[SaveSystem.Data.skin]);
            jumper.SetShieldVisible(false);
            cam.target = jumper.transform;
        }

        public void RefreshJumperSkin()
        {
            if (State == GState.Menu) SpawnJumper();
        }

        void Jump()
        {
            if (State != GState.Menu) return;
            ui.ShowMenu(false, null);
            ui.ShowHud(true, cfg);
            jumper.BeginFall();
            jumper.SetShieldVisible(jumper.shields > 0);
            State = GState.Freefall;
            stateTime = 0f;
            cam.SetMode(CamMode.Freefall);
            ui.ShowBanner(cfg);
            prevPos = jumper.transform.position;
            tutorial = !SaveSystem.Data.tutorialDone && !cfg.daily && cfg.index == 0;
            tutStep = 0;
            tutTimer = 0f;
            SaveSystem.Data.totalJumps++;
            SoundBank.I.Play(SoundBank.I.whoosh, 1f, 0.8f);
            PlatformSDK.GameplayStart();
        }

        public void OnTapJump()
        {
            if (State == GState.Menu && !ui.ModalOpen) Jump();
        }

        public void SelectLevel(int index)
        {
            SaveSystem.Data.current = index;
            SaveSystem.Save();
            LoadLevel(index, false);
        }

        public void StartDailyJump()
        {
            if (State != GState.Menu) return;
            LoadLevel(0, true);
            Jump();
        }

        // =============================================================================== update

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            UpdateTimeScale(udt);
            if (level == null || paused) return;
            if (jumper != null) InputReader.PlayerScreen = cam.cam.WorldToScreenPoint(jumper.transform.position);
            InputReader.Update();
            float dt = Time.deltaTime;
            level.Tick(dt);
            stateTime += udt;

            switch (State)
            {
                case GState.Menu:
                    jumper.Animate(0, Vector2.zero, level.time, dt);
                    if (InputReader.ConsumeOpen() && !ui.ModalOpen) Jump();
                    break;
                case GState.Freefall: FreefallUpdate(dt); break;
                case GState.Canopy: CanopyUpdate(dt); break;
                case GState.Landed:
                case GState.Crashed:
                    if (State == GState.Landed) jumper.Animate(3, Vector2.zero, level.time, dt);
                    InputReader.ConsumeOpen();
                    if (stateTime > 2.2f) ShowResults();
                    break;
                case GState.Results:
                    if (InputReader.ConsumeOpen() && !ui.ModalOpen)
                    {
                        if (lastResult.hasNext) OnNext();
                        else OnRetry();
                    }
                    break;
            }

            if (level.plane != null)
            {
                if (State != GState.Menu) level.plane.position += Vector3.right * 45f * dt;
                var prop = level.plane.Find("Prop");
                if (prop != null) prop.Rotate(2000f * dt, 0f, 0f, Space.Self);
            }

            bool falling = State == GState.Freefall;
            bool canopy = State == GState.Canopy;
            if (falling || canopy)
            {
                Vector3 pos = jumper.transform.position;
                float alt = level.AltitudeAbovePad(pos);
                float margin = RiskMargin(alt);
                ui.UpdateHud(new HudInfo
                {
                    coins = runCoins,
                    altitude = alt,
                    margin = margin,
                    freefall = falling,
                    canopy = canopy,
                    canOpen = margin < Zones.Ceiling,
                    shields = jumper.shields,
                    wind = level.wind * Economy.WindFactor,
                    rings = ringsHit,
                    ringsTotal = level.rings.Count,
                    lockedZone = lockedZone,
                    opened = canopy,
                    ringsNeeded = cfg.ringsNeeded,
                    riskZone = cfg.riskZone
                });
                UpdateShadow(pos, alt);
            }
            // Fog opens up with altitude so the ground and target stay readable from high up.
            SkyEnv.SetFog(60f, Mathf.Max(650f, cam.transform.position.y * 1.25f + 350f));
            float speed01 = falling ? Mathf.Clamp01(jumper.vFall / (Jumper.Terminal + 10f)) : 0f;
            cam.speed01 = speed01;
            SoundBank.I.SetWind(falling ? speed01 : canopy ? 0.12f : 0f, 0.8f + speed01 * 0.5f);
            if (Fx.I != null) Fx.I.UpdateStreaks(jumper != null ? jumper.transform.position : Vector3.zero, falling ? jumper.vFall : 0f, falling ? speed01 : 0f, udt);
        }

        void FreefallUpdate(float dt)
        {
            Vector2 steer = InputReader.Steer;
            prevPos = jumper.transform.position;
            jumper.TickFreefall(steer, dt);
            Vector3 pos = jumper.transform.position;

            CheckRings(prevPos, pos);
            CheckCoins(pos, dt);
            CheckHazards(pos, false, dt);
            if (State != GState.Freefall) return;

            float alt = level.AltitudeAbovePad(pos);
            float margin = RiskMargin(alt);
            bool canOpen = margin < Zones.Ceiling;
            focus = margin < 50f && margin > -3f ? Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(margin / 50f)) : 1f;
            if (InputReader.ConsumeOpen())
            {
                if (canOpen)
                {
                    Deploy(margin);
                    return;
                }
                ui.ScreenPopup("TOO HIGH - WAIT!", Color.white, 40, -40f);
            }

            if (level.InsideRoof(pos) && prevPos.y < level.target.top - 0.3f)
            {
                Crash("WALL!", "You hit the side of the building.");
                return;
            }
            bool onTarget;
            float surface = level.SurfaceHeight(pos, out onTarget);
            if (pos.y <= surface)
            {
                SetY(surface);
                Crash("SPLAT!", "You never opened the parachute!");
                return;
            }

            Tutorial(margin);
            jumper.Animate(1, steer, level.time, dt);
            jumper.SetTrail(jumper.vFall > Jumper.Terminal - 4f, combo >= 5 ? new Color(1f, 0.6f, 0.15f, 0.8f) : new Color(1f, 1f, 1f, 0.45f));
        }

        void CanopyUpdate(float dt)
        {
            Vector2 steer = InputReader.Steer;
            InputReader.ConsumeOpen();
            prevPos = jumper.transform.position;
            float alt = level.AltitudeAbovePad(prevPos);
            jumper.TickCanopy(steer, level.wind, alt, dt);
            Vector3 pos = jumper.transform.position;

            CheckCoins(pos, dt);
            CheckHazards(pos, true, dt);
            if (State != GState.Canopy) return;

            if (level.InsideRoof(pos) && prevPos.y < level.target.top - 0.3f)
            {
                Crash("WALL!", "You flew into the side of the building.");
                return;
            }
            bool onTarget;
            float surface = level.SurfaceHeight(pos, out onTarget);
            if (pos.y <= surface)
            {
                SetY(surface);
                Touchdown(onTarget);
                return;
            }
            if (tutorial) ui.SetHint("Steer onto the TARGET - land in the middle!");
            jumper.Animate(2, steer, level.time, dt);
        }

        /// Meters above the lowest safe opening point, normalized by the Quick Chute upgrade.
        float RiskMargin(float altitude)
        {
            return (altitude - jumper.HMin) / Economy.ZoneScale;
        }

        /// A dark disc on the ground right below the jumper, so you can see exactly where you'll land.
        void UpdateShadow(Vector3 pos, float alt)
        {
            bool onTarget;
            float surface = level.SurfaceHeight(pos, out onTarget);
            bool show = alt < 260f;
            if (shadow.gameObject.activeSelf != show) shadow.gameObject.SetActive(show);
            if (!show) return;
            shadow.position = new Vector3(pos.x, surface + 0.14f, pos.z);
            float k = Mathf.Clamp01(alt / 260f);
            shadow.localScale = Vector3.one * Mathf.Lerp(1.6f, 4f, k);
            shadowMat.SetColor("_Color", onTarget ? new Color(0.1f, 0.35f, 0.1f, Mathf.Lerp(0.6f, 0.25f, k)) : new Color(0f, 0f, 0f, Mathf.Lerp(0.55f, 0.2f, k)));
        }

        void SetY(float y)
        {
            var p = jumper.transform.position;
            p.y = y;
            jumper.transform.position = p;
        }

        // =============================================================================== parachute

        void Deploy(float margin)
        {
            int z = Zones.Of(margin);
            multiplier = Zones.Mult[z];
            lockedZone = z;
            focus = 1f;
            jumper.StartDeploy();
            jumper.SetTrail(false, Color.white);
            State = GState.Canopy;
            tutFreeze = false;
            ui.SetHint(null);
            ui.SetOpenAttention(false);
            cam.SetMode(CamMode.Canopy);
            cam.Kick(1f);
            SoundBank.I.Play(SoundBank.I.pop);
            ui.ScreenPopup(Zones.MultText(multiplier) + "  " + Zones.Name[z] + (z <= 2 ? "!" : ""), Zones.Col[z], z <= 2 ? 70 : 50, 150f);
            if (z <= 2)
            {
                SlowMo(0.3f, 0.6f);
                cam.Shake(0.4f);
                ui.Flash(Zones.Col[z], 0.35f);
            }
        }

        void Touchdown(bool onTarget)
        {
            float impact = jumper.vFall;
            if (impact > Jumper.SafeLanding)
            {
                Crash("TOO LATE!", "Opened too low - hit the ground at " + Mathf.RoundToInt(impact * 3.6f) + " km/h");
                return;
            }
            Vector3 pos = jumper.transform.position;
            float dist = level.target.HorizontalDistance(pos);
            float r = level.target.radius;
            int stars = !onTarget ? 0 : dist <= r * 0.25f ? 3 : dist <= r * 0.6f ? 2 : 1;   // landing precision
            if (shadow != null) shadow.gameObject.SetActive(false);

            State = GState.Landed;
            stateTime = 0f;
            focus = 1f;
            cam.SetMode(CamMode.Landed);
            jumper.hVel = Vector3.zero;
            jumper.vFall = 0f;
            jumper.Land();
            if (onTarget) jumper.transform.SetParent(level.target.tr, true);   // ride moving targets
            ui.SetHint(null);

            string title, sub;
            Color col;
            bool water = cfg.water && !onTarget && !level.InsideRoof(pos);
            if (stars == 3)
            {
                title = "PERFECT LANDING!";
                sub = "Bullseye! " + dist.ToString("0.0") + " m from the center";
                col = UIKit.Gold;
                SoundBank.I.Play(SoundBank.I.perfect);
                Fx.I.Confetti(pos);
                PlatformSDK.HappyTime();
            }
            else if (stars == 2)
            {
                title = "GREAT LANDING!";
                sub = dist.ToString("0.0") + " m from the center - so close to perfect";
                col = UIKit.Green;
                SoundBank.I.Play(SoundBank.I.perfect, 0.7f, 0.9f);
                Fx.I.Confetti(pos);
            }
            else if (stars == 1)
            {
                title = "GOOD LANDING";
                sub = "Land closer to the center for a bigger bonus";
                col = new Color(0.55f, 0.8f, 1f);
                SoundBank.I.Play(SoundBank.I.land);
            }
            else
            {
                float miss = dist - r;
                title = water ? "SPLASH!" : "MISSED THE TARGET";
                sub = miss < 4f ? "So close! Only " + miss.ToString("0.0") + " m away" : "Steer onto the target while under the canopy";
                col = UIKit.Orange;
                SoundBank.I.Play(water ? SoundBank.I.splash : SoundBank.I.land);
                if (water) Fx.I.Burst(pos, new Color(0.7f, 0.9f, 1f), 30, 9f, 0.5f, 1f, true);
            }
            Fx.I.Burst(pos, new Color(0.85f, 0.8f, 0.7f), 14, 5f, 0.5f, 0.7f, false);
            cam.Shake(0.2f);
            lastResult = BuildResult(false, onTarget, stars, title, sub, col);
        }

        // =============================================================================== checks

        void CheckRings(Vector3 prev, Vector3 pos)
        {
            foreach (var r in level.rings)
            {
                if (r.done || !(prev.y > r.pos.y && pos.y <= r.pos.y)) continue;
                float t = (prev.y - r.pos.y) / Mathf.Max(0.0001f, prev.y - pos.y);
                Vector3 cross = Vector3.Lerp(prev, pos, t);
                float d = new Vector2(cross.x - r.pos.x, cross.z - r.pos.z).magnitude;
                r.done = true;
                if (d <= r.radius + 0.3f)
                {
                    combo++;
                    ringsHit++;
                    bool perfect = d < r.radius * 0.35f;
                    int gain = 1 + combo + (perfect ? 2 : 0);
                    runCoins += gain;
                    jumper.Boost(7f);
                    SoundBank.I.Play(SoundBank.I.ring, 0.9f, 1f + Mathf.Min(combo, 10) * 0.05f);
                    Fx.I.Burst(r.pos, new Color(1f, 0.85f, 0.2f), 16, 10f, 0.4f, 0.6f, false);
                    Fx.I.PopAway(r.tr);
                    ui.Popup(r.pos, "+" + gain, UIKit.Gold, 44);
                    ui.Combo(combo, perfect);
                    ui.CoinPunch();
                    cam.Kick(0.5f);
                }
                else
                {
                    combo = 0;
                    r.tr.GetComponent<MeshRenderer>().sharedMaterial = Mat.Lit(new Color(0.5f, 0.5f, 0.55f));
                    ui.Popup(r.pos, "MISSED", new Color(1f, 1f, 1f, 0.7f), 30);
                }
            }
        }

        void CheckCoins(Vector3 pos, float dt)
        {
            float radius = Economy.MagnetRadius;
            float pull = Economy.Level(UpgradeId.Magnet) > 0 ? radius * 2.2f : 0f;
            foreach (var c in level.coins)
            {
                if (c.collected) continue;
                Vector3 cp = c.tr.position;
                float d = Vector3.Distance(cp, pos);
                if (!c.pulled && d < pull) c.pulled = true;
                if (c.pulled)
                {
                    c.tr.position = Vector3.MoveTowards(cp, pos, (60f + jumper.vFall) * dt);
                    d = Vector3.Distance(c.tr.position, pos);
                }
                if (d < radius)
                {
                    c.collected = true;
                    c.tr.gameObject.SetActive(false);
                    runCoins++;
                    SoundBank.I.Coin();
                    ui.CoinPunch();
                    Fx.I.Burst(cp, UIKit.Gold, 5, 5f, 0.25f, 0.4f, false);
                }
            }
        }

        void CheckHazards(Vector3 pos, bool underCanopy, float dt)
        {
            foreach (var h in level.hazards)
            {
                float d = h.Distance(pos) - Jumper.Radius;
                if (d < 0f)
                {
                    if (!h.hit)
                    {
                        HitHazard(h, pos, underCanopy);
                        if (State == GState.Crashed) return;
                    }
                }
                else if (!h.nearMissDone && d < 2.2f && (h.kind == HazardKind.Lethal || h.label == "Balloon!"))
                {
                    h.nearMissDone = true;
                    pendingNearMiss = h;
                    nearMissTimer = 0.25f;
                }
            }
            if (pendingNearMiss != null)
            {
                nearMissTimer -= dt;
                if (pendingNearMiss.hit) pendingNearMiss = null;
                else if (nearMissTimer <= 0f)
                {
                    pendingNearMiss = null;
                    runCoins += 3;
                    ui.Popup(pos + Vector3.up, "CLOSE CALL! +3", new Color(0.4f, 1f, 0.9f), 40);
                    SoundBank.I.Play(SoundBank.I.whoosh, 1f, 1.3f);
                    SlowMo(0.4f, 0.35f);
                    cam.Kick(0.6f);
                }
            }
            if (underCanopy)
            {
                foreach (var g in level.groundHazards)
                {
                    if (g.Distance(pos) - Jumper.Radius < 0f)
                    {
                        Crash("CRASH!", "You landed on a " + Noun(g.label) + ".");
                        return;
                    }
                }
            }
        }

        static string Noun(string label)
        {
            return label.Replace("!", "").ToLower();
        }

        void HitHazard(Hazard h, Vector3 pos, bool underCanopy)
        {
            h.hit = true;
            Vector3 away = pos - h.Center;
            if (jumper.shields > 0)
            {
                jumper.shields--;
                jumper.SetShieldVisible(jumper.shields > 0);
                SoundBank.I.Play(SoundBank.I.shield);
                Fx.I.Burst(pos, new Color(0.4f, 1f, 0.85f), 20, 12f, 0.3f, 0.6f, false);
                ui.Popup(pos, "SHIELD SAVED YOU!", new Color(0.4f, 1f, 0.85f), 38);
                cam.Shake(0.3f);
                away.y = 0f;
                jumper.hVel = away.normalized * 8f;
                return;
            }
            if (h.kind == HazardKind.Lethal)
            {
                Crash("CRASHED!", "You hit a " + Noun(h.label) + ".");
                return;
            }
            jumper.Tumble(away, underCanopy ? 5f : 12f);
            combo = 0;
            cam.Shake(0.5f);
            SoundBank.I.Play(SoundBank.I.thud, 0.5f, 1.5f);
            bool storm = h.kind == HazardKind.Storm;
            Fx.I.Burst(pos, storm ? new Color(1f, 0.95f, 0.4f) : new Color(0.9f, 0.9f, 0.9f), 14, 8f, 0.3f, 0.6f, false);
            ui.Popup(pos, storm ? "ZAP!" : "OUCH!", new Color(1f, 0.35f, 0.3f), 48);
            if (storm) ui.Flash(new Color(1f, 1f, 0.8f), 0.6f);
        }

        void Crash(string title, string sub)
        {
            if (State == GState.Crashed || State == GState.Landed) return;
            State = GState.Crashed;
            stateTime = 0f;
            tutFreeze = false;
            focus = 1f;
            ui.SetHint(null);
            Vector3 pos = jumper.transform.position;
            bool onTarget;
            float ground = level.SurfaceHeight(pos, out onTarget);
            jumper.Explode(level.root.transform, ground, jumper.hVel + Vector3.down * jumper.vFall);
            cam.SetMode(CamMode.Crash);
            cam.Shake(1f);
            SoundBank.I.Play(SoundBank.I.thud);
            Fx.I.Burst(pos, new Color(0.9f, 0.85f, 0.75f), 24, 12f, 0.5f, 0.8f, true);
            ui.Flash(new Color(1f, 0.2f, 0.2f), 0.45f);
            SlowMo(0.25f, 0.5f);
            if (shadow != null) shadow.gameObject.SetActive(false);
            lastResult = BuildResult(true, false, 0, title, sub, new Color(1f, 0.3f, 0.3f));
        }

        // =============================================================================== results

        /// precision: 3 bullseye, 2 inner ring, 1 outer ring, 0 off target.
        /// Stars come from the level goals: land on target / enough rings / risky opening.
        ResultInfo BuildResult(bool crashed, bool onTarget, int precision, string title, string sub, Color col)
        {
            var d = SaveSystem.Data;
            bool cleared = !crashed && onTarget;
            bool ringsGoal = ringsHit >= cfg.ringsNeeded;
            bool riskGoal = !crashed && lockedZone <= cfg.riskZone && multiplier > 1f;
            int stars = cleared ? 1 + (ringsGoal ? 1 : 0) + (riskGoal ? 1 : 0) : 0;
            int landingBonus = precision == 3 ? 40 : precision == 2 ? 20 : precision == 1 ? 10 : 0;
            if (precision > 0) landingBonus += cfg.index * 2;
            if (cleared && !ringsGoal && !riskGoal) sub = "Landed! Hit the other goals for more stars";
            float mult = cleared ? multiplier : 1f;
            int total = Mathf.RoundToInt((runCoins + landingBonus) * mult);
            if (cfg.daily) total *= 2;

            var r = new ResultInfo
            {
                title = title,
                subtitle = sub,
                titleColor = col,
                stars = stars,
                airCoins = runCoins,
                landingBonus = landingBonus,
                multiplier = multiplier,
                cleared = cleared,
                crashed = crashed,
                daily = cfg.daily,
                canDouble = true,
            };
            r.goals[0] = cleared;
            r.goals[1] = ringsGoal;
            r.goals[2] = riskGoal;
            r.goalText[0] = LevelConfig.LandGoal;
            r.goalText[1] = "Rings  " + ringsHit + " / " + cfg.ringsNeeded;
            r.goalText[2] = "Open at " + Zones.MultText(Zones.Mult[cfg.riskZone]) + "+" + (multiplier > 1f || !crashed ? "   (you: " + Zones.MultText(multiplier) + ")" : "");

            if (cleared && multiplier > d.bestMultiplier)
            {
                r.newRecord = d.bestMultiplier > 0f;
                d.bestMultiplier = multiplier;
            }
            if (cfg.daily)
            {
                if (cleared && Economy.DailyJumpAvailable)
                {
                    d.lastDailyJump = SaveSystem.Today;
                    r.dailyBonus = 150;
                    total += 150;
                }
            }
            else
            {
                d.stars[cfg.index] = Mathf.Max(d.stars[cfg.index], stars);
                if (cleared && cfg.index + 1 < Levels.Count)
                {
                    if ((cfg.index + 1) % Levels.PerWorld == 0 && d.unlocked <= cfg.index)
                        r.subtitle = "NEW WORLD UNLOCKED: " + Levels.Themes[(cfg.index + 1) / Levels.PerWorld].name.ToUpper() + "!";
                    d.unlocked = Mathf.Max(d.unlocked, cfg.index + 1);
                    d.current = cfg.index + 1;
                    r.hasNext = true;
                }
            }
            if (precision == 3) d.perfectLandings++;
            d.tutorialDone = true;
            d.coins += total;
            r.total = total;
            SaveSystem.Save();
            return r;
        }

        void ShowResults()
        {
            State = GState.Results;
            PlatformSDK.GameplayStop();
            ui.ShowResults(lastResult);
            SoundBank.I.Play(lastResult.cleared ? SoundBank.I.buy : SoundBank.I.fail, 0.8f);
        }

        public void OnNext()
        {
            if (State != GState.Results) return;
            MaybeAd(() =>
            {
                LoadLevel(SaveSystem.Data.current, false);
                Jump();
            });
        }

        public void OnRetry()
        {
            if (State != GState.Results) return;
            bool daily = cfg.daily;
            int index = cfg.index;
            MaybeAd(() =>
            {
                LoadLevel(index, daily);
                Jump();
            });
        }

        public void OnMenu()
        {
            if (State != GState.Results) return;
            LoadLevel(SaveSystem.Data.current, false);
        }

        public void OnDoubleReward()
        {
            if (State != GState.Results || doubled || lastResult == null) return;
            PlatformSDK.Rewarded(OnAdStart, () =>
            {
                OnAdEnd();
                doubled = true;
                SaveSystem.Data.coins += lastResult.total;
                SaveSystem.Save();
                SoundBank.I.Play(SoundBank.I.perfect);
                ui.UpdateResultTotal(lastResult.total * 2, true);
            }, () =>
            {
                OnAdEnd();
                ui.ScreenPopup("No ad available right now", Color.white, 36, -200f);
            });
        }

        /// Midgame ad at a natural break (between runs), rate limited.
        void MaybeAd(Action then)
        {
            runsSinceAd++;
            if (runsSinceAd >= 3 && Time.realtimeSinceStartup - lastAdTime > 150f)
            {
                runsSinceAd = 0;
                lastAdTime = Time.realtimeSinceStartup;
                PlatformSDK.Midgame(OnAdStart, () =>
                {
                    OnAdEnd();
                    then();
                });
            }
            else then();
        }

        void OnAdStart()
        {
            paused = true;
            AudioListener.pause = true;
            Time.timeScale = 0f;
        }

        void OnAdEnd()
        {
            paused = false;
            AudioListener.pause = false;
            Time.timeScale = 1f;
        }

        // =============================================================================== time + tutorial

        void SlowMo(float scale, float duration)
        {
            slowScale = scale;
            slowTimer = duration;
            Time.timeScale = scale;
        }

        void UpdateTimeScale(float udt)
        {
            if (paused)
            {
                Time.timeScale = 0f;
                return;
            }
            if (tutFreeze)
            {
                Time.timeScale = Mathf.MoveTowards(Time.timeScale, 0.03f, udt * 6f);
                return;
            }
            if (slowTimer > 0f)
            {
                slowTimer -= udt;
                Time.timeScale = Mathf.MoveTowards(Time.timeScale, Mathf.Min(slowScale, focus), udt * 12f);
            }
            else Time.timeScale = Mathf.MoveTowards(Time.timeScale, focus, udt * (focus < Time.timeScale ? 6f : 3f));
        }

        void Tutorial(float margin)
        {
            if (!tutorial) return;
            tutTimer += Time.unscaledDeltaTime;
            switch (tutStep)
            {
                case 0:
                    ui.SetHint(Mobile ? "SLIDE your finger to steer" : "Move the MOUSE to steer (or WASD)");
                    if ((InputReader.UsedSteer && tutTimer > 1.5f) || tutTimer > 5f) { tutStep = 1; tutTimer = 0f; }
                    break;
                case 1:
                    ui.SetHint("Fly through " + cfg.ringsNeeded + " RINGS - that's a star!");
                    if ((ringsHit > 0 && tutTimer > 1f) || tutTimer > 6f) { tutStep = 2; tutTimer = 0f; }
                    break;
                case 2:
                    ui.SetHint("Get above the TARGET. Open LOW = BIG bonus + star!");
                    if (margin < 40f)
                    {
                        tutStep = 3;
                        tutFreeze = true;
                        ui.SetOpenAttention(true);
                        SoundBank.I.Play(SoundBank.I.beep, 0.8f, 1.5f);
                    }
                    break;
                case 3:
                    ui.SetHint(Mobile ? "TAP  OPEN  NOW!" : "PRESS SPACE TO OPEN NOW!");
                    break;
            }
        }
    }
}
