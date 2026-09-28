using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombiePile
{
    /// The in-game HUD (Tools/ui-design/mock2.html, "hudA"/"hudB"/"boss"): wall health, level progress, boss bar,
    /// coins (with coins flying in from every kill), the shooter switch, the barrel and airstrike buttons,
    /// crosshair, damage numbers, popups and banners, and health bars over wounded zombies.
    public class Hud : MonoBehaviour
    {
        public static Hud I;
        public RectTransform Root { get; private set; }

        static readonly Vector2 TL = new Vector2(0f, 1f), TR = new Vector2(1f, 1f);
        static readonly Vector2 BL = new Vector2(0f, 0f), BC = new Vector2(0.5f, 0f), BR = new Vector2(1f, 0f);

        GameObject hud, banner, bossBox, warn, boostBox, airBox;
        RectTransform crosshair, barBox, coinIcon;
        Text levelText, progText, wallPct, coinText, bannerText, bannerSub, boostText, switchName;
        Image wallFill, progFill, bossFill, boostFill, barrelFill, barrelFace, barrelGlow, airFill, airFace, damage, hitmark, portrait;
        float bannerT, bannerIn, damageT, hitT, wallShown = 1f, wallTarget = 1f, progShown, progTarget, spread, coinPunch;
        int coinsInFlight, coinsShown = -1;
        Color bannerColor = Color.white;

        class Pop { public Text t; public Vector3 world; public float life, max, size, rise; }
        readonly List<Pop> pops = new List<Pop>();
        readonly List<Pop> numbers = new List<Pop>();
        int nextPop, nextNum;

        class Flyer { public RectTransform r; public Vector2 from, ctrl; public float t, dur; public int value; }
        readonly List<Flyer> flyers = new List<Flyer>();

        class Bar { public RectTransform r; public Image fill; }
        readonly List<Bar> bars = new List<Bar>();

        public static void Build()
        {
            var go = new GameObject("HUD");
            go.AddComponent<Hud>().Make();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        // ------------------------------------------------------------------ builders (mock-up coordinates)
        internal static Text Label(Transform p, string s, bool bangers, int size, Color c, Vector2 anchor, float x, float y, float w, TextAnchor align = TextAnchor.MiddleCenter, bool stroke = true)
        {
            var t = bangers ? UIKit.Title(p, s, size, c, align) : UIKit.Text(p, s, size, c, align, stroke);
            UIKit.At(t.rectTransform, anchor, x, y, w, size);
            return t;
        }

        internal static Image Pic(Transform p, string sprite, Color c, Vector2 anchor, float x, float y, float w, float h)
        {
            var img = UIKit.Pic(p, sprite, c);
            UIKit.At(img.rectTransform, anchor, x, y, w, h);
            return img;
        }

        /// A bar fill inside a track: stretches from the left, its width set through anchorMax.x.
        internal static Image FillIn(Image track, Color c, float pad)
        {
            var f = UIKit.Pic(track.transform, "fill", c);
            UIKit.Inset(f.rectTransform, pad, pad, pad, pad);
            return f;
        }

        /// A child placed with mock-up coordinates relative to its parent's top-left corner.
        internal static RectTransform In(RectTransform r, Vector2 parentSize, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = new Vector2(x + w * 0.5f - parentSize.x * 0.5f, -(y + h * 0.5f - parentSize.y * 0.5f));
            return r;
        }

        internal static void Vignette(Transform p, float alpha)
        {
            var v = UIKit.Pic(p, "vignette", new Color(1f, 1f, 1f, alpha));
            v.preserveAspect = false;
            UIKit.Stretch(v.rectTransform);
        }

        /// The coin counter panel (also used by the menu and armory screens).
        internal static Text CoinPanel(Transform p, float x, float y, out RectTransform panel, out RectTransform icon)
        {
            panel = Pic(p, "panel", Color.white, TR, x, y, 184, 62).rectTransform;
            icon = Pic(p, "coin", Color.white, TR, x + 8, y + 7, 48, 48).rectTransform;
            return Label(p, "0", true, 42, UIKit.Gold, TR, x + 60, y + 10, 112, TextAnchor.MiddleRight);
        }

        void Make()
        {
            I = this;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            Root = (RectTransform)transform;

            damage = UIKit.Image(Root, "Damage", new Color(0.9f, 0.1f, 0.05f, 0f), UIKit.Fade);
            UIKit.Stretch(damage.rectTransform);
            damage.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);   // red creeps in from the top (the wall)

            hud = UIKit.Node("HUD", Root).gameObject;
            UIKit.Stretch((RectTransform)hud.transform);
            var ht = hud.transform;
            Vignette(ht, 0.55f);
            barBox = UIKit.Node("HealthBars", ht);
            UIKit.Stretch(barBox);

            // everything sits in the top-left corner so the street (the middle of the screen) stays clear:
            // level + progress, wall health, power-up timer, boss bar, announcements
            Pic(ht, "panel", Color.white, TL, 12, 10, 300, 66);
            levelText = Label(ht, "LEVEL 1", true, 34, UIKit.Gold, TL, 26, 14, 170, TextAnchor.MiddleLeft);
            progText = Label(ht, "0 / 0", false, 20, Color.white, TL, 180, 20, 118, TextAnchor.MiddleRight);
            progFill = FillIn(Pic(ht, "track", Color.white, TL, 22, 48, 280, 20), new Color(1f, 0.69f, 0.13f), 3f);

            Pic(ht, "panel", Color.white, TL, 12, 80, 300, 56);
            Pic(ht, "ic_wall", Color.white, TL, 20, 84, 48, 48);
            wallFill = FillIn(Pic(ht, "track", Color.white, TL, 74, 94, 228, 28), UIKit.Green, 4f);
            wallPct = Label(ht, "100%", false, 20, Color.white, TL, 74, 98, 228);

            boostBox = UIKit.Node("Boost", ht).gameObject;
            UIKit.Stretch((RectTransform)boostBox.transform);
            Pic(boostBox.transform, "panel", Color.white, TL, 12, 140, 240, 46);
            boostText = Label(boostBox.transform, "", true, 24, UIKit.Gold, TL, 24, 144, 200, TextAnchor.MiddleLeft);
            boostFill = FillIn(Pic(boostBox.transform, "track", Color.white, TL, 22, 168, 220, 12), UIKit.Gold, 2f);
            boostBox.SetActive(false);

            bossBox = UIKit.Node("Boss", ht).gameObject;
            UIKit.Stretch((RectTransform)bossBox.transform);
            Pic(bossBox.transform, "panel", Color.white, TL, 12, 190, 300, 58);
            Pic(bossBox.transform, "ic_crown", Color.white, TL, 18, 194, 48, 48);
            Label(bossBox.transform, ZType.Boss.name, true, 22, new Color(1f, 0.42f, 0.32f), TL, 72, 194, 220, TextAnchor.MiddleLeft);
            bossFill = FillIn(Pic(bossBox.transform, "track", Color.white, TL, 72, 220, 230, 20), UIKit.Red, 3f);
            bossBox.SetActive(false);

            // coins (top right, compact)
            RectTransform coinPanel;
            coinText = CoinPanel(ht, 1084, 10, out coinPanel, out coinIcon);

            // barrel button (bottom right)
            var barrel = UIKit.SpriteButton(ht, "round_red", "", 0, () => Shooter.Wall.ThrowBarrel());
            var bt = (RectTransform)barrel.transform;
            UIKit.At(bt, BR, 1082, 522, 176, 176);
            barrelFace = (Image)barrel.targetGraphic;
            var bsz = new Vector2(176, 176);
            In(UIKit.Pic(bt, "barrel3d", Color.white).rectTransform, bsz, 28, 4, 120, 120);
            In(UIKit.Title(bt, "BARREL", 34, Color.white).rectTransform, bsz, 0, 118, 176, 34);
            barrelFill = Cooldown(bt, bsz, 164);
            barrelGlow = UIKit.Image(bt, "Glow", UIKit.Gold, UIKit.Ring);
            In(barrelGlow.rectTransform, bsz, -14, -14, 204, 204);
            Key(bt, bsz, "SPACE", 46, -36, 84);

            // airstrike button (once bought)
            var air = UIKit.SpriteButton(ht, "round_gold", "", 0, () => Shooter.Wall.CallAirstrike());
            var at = (RectTransform)air.transform;
            UIKit.At(at, BR, 946, 574, 124, 124);
            airBox = air.gameObject;
            airFace = (Image)air.targetGraphic;
            var asz = new Vector2(124, 124);
            In(UIKit.Pic(at, "ic_airstrike", Color.white).rectTransform, asz, 30, 12, 64, 64);
            In(UIKit.Title(at, "AIRSTRIKE", 22, Color.white).rectTransform, asz, 0, 80, 124, 22);
            airFill = Cooldown(at, asz, 114);
            Key(at, asz, "F", 37, -34, 50);

            // switch shooter (bottom left): the other shooter's portrait
            var sw = UIKit.SpriteButton(ht, "por_lis", "", 0, () => CameraRig.I.Toggle());
            var st = (RectTransform)sw.transform;
            UIKit.At(st, BL, 24, 548, 132, 132);
            portrait = (Image)sw.targetGraphic;
            var ssz = new Vector2(132, 132);
            In(UIKit.Pic(st, "round_dark", Color.white).rectTransform, ssz, 88, -8, 58, 58);
            In(UIKit.Pic(st, "ic_switch", Color.white).rectTransform, ssz, 96, 0, 42, 42);
            switchName = UIKit.Title(st, "LIS", 32, Color.white);
            In(switchName.rectTransform, ssz, 0, 122, 132, 32);
            Key(st, ssz, "TAB", 26, -40, 80);

            // "they're climbing" warning next to it
            warn = UIKit.Node("Warn", ht).gameObject;
            UIKit.Stretch((RectTransform)warn.transform);
            var wp = Pic(warn.transform, "panel", new Color(1f, 0.75f, 0.75f), BL, 170, 590, 300, 64);
            wp.gameObject.AddComponent<Pulse>().amount = 0.05f;
            Label(warn.transform, "CLIMBERS! SWITCH TO LIS", false, 22, new Color(1f, 0.42f, 0.32f), BL, 176, 610, 288);
            warn.SetActive(false);

            var hint = Label(ht, Application.isMobilePlatform ? "HOLD TO SHOOT   |   TAP THE PORTRAIT TO SWITCH" :
                "HOLD MOUSE: SHOOT   |   TAB: SWITCH SHOOTER   |   SPACE: BARREL", false, 21, Color.white, BC, 0, 690, 1280);
            hint.gameObject.AddComponent<FadeAfter>().delay = 10f;

            crosshair = UIKit.Pic(Root, "crosshair", Color.white).rectTransform;
            crosshair.sizeDelta = new Vector2(72, 72);
            hitmark = UIKit.Pic(crosshair, "hitmark", new Color(1f, 1f, 1f, 0f));
            hitmark.rectTransform.sizeDelta = new Vector2(52, 52);

            // announcements: a small ribbon that slides in on the left, away from the street
            banner = UIKit.Node("Banner", Root).gameObject;
            UIKit.Stretch((RectTransform)banner.transform);
            var bn = banner.transform;
            Pic(bn, "ribbon", Color.white, TL, 0, 256, 440, 74);
            bannerText = Label(bn, "", true, 40, Color.white, TL, 60, 267, 320);
            bannerSub = Label(bn, "", false, 20, UIKit.Gold, TL, 16, 334, 420);
            banner.SetActive(false);

            for (int i = 0; i < 16; i++) pops.Add(MakePop(34));
            for (int i = 0; i < 40; i++) numbers.Add(MakePop(34));
            for (int i = 0; i < 40; i++)
            {
                var c = UIKit.Pic(Root, "coin", Color.white).rectTransform;
                c.sizeDelta = new Vector2(34, 34);
                c.gameObject.SetActive(false);
                flyers.Add(new Flyer { r = c });
            }
            hud.SetActive(false);
        }

        Pop MakePop(int size)
        {
            var t = UIKit.Title(Root, "", size, Color.white);
            t.rectTransform.sizeDelta = new Vector2(600, size * 1.3f);
            t.gameObject.SetActive(false);
            return new Pop { t = t };
        }

        static Image Cooldown(RectTransform parent, Vector2 psz, float size)
        {
            var f = UIKit.Image(parent, "Cooldown", new Color(0.05f, 0.02f, 0.08f, 0.62f), UIKit.Circle);
            In(f.rectTransform, psz, (psz.x - size) * 0.5f, (psz.y - size) * 0.5f - 4f, size, size);
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Radial360;
            f.fillOrigin = (int)Image.Origin360.Top;
            f.fillClockwise = false;
            return f;
        }

        static void Key(RectTransform parent, Vector2 psz, string label, float x, float y, float w)
        {
            if (Application.isMobilePlatform) return;
            var key = UIKit.Pic(parent, "keycap", Color.white);
            In(key.rectTransform, psz, x, y, w, 38);
            var kt = UIKit.Text(key.transform, label, 18, new Color(0.17f, 0.11f, 0.23f), TextAnchor.MiddleCenter, false);
            UIKit.Inset(kt.rectTransform, 0, 5, 0, 0);
        }

        // ------------------------------------------------------------------ API
        public void Show(bool on)
        {
            hud.SetActive(on);
            if (!on) { banner.SetActive(false); foreach (var b in bars) b.r.gameObject.SetActive(false); }
            coinsShown = -1;
        }

        public void SetLevel(string s) { levelText.text = s; progShown = progTarget = 0f; }
        public void SetWall(float k) { wallTarget = k; }
        public void FlashDamage() { damageT = 1f; }

        public void SetProgress(int done, int total)
        {
            progTarget = total > 0 ? Mathf.Clamp01((float)done / total) : 0f;
            progText.text = done + " / " + total;
        }

        public void SetClimbWarning(bool on) { if (warn.activeSelf != on) warn.SetActive(on); }

        public void SetBoost(string label, float k)
        {
            bool on = label != null && hud.activeSelf;
            if (boostBox.activeSelf != on) boostBox.SetActive(on);
            if (!on) return;
            boostText.text = label;
            boostFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.05f, k), 1f);
        }

        public void Banner(string title, string sub, float duration)
        {
            bannerText.text = title; bannerSub.text = sub; bannerT = duration;
            bannerColor = title.Contains("CLEARED") ? UIKit.Lime : Color.white;
            banner.SetActive(true);
            bannerIn = 0f;
        }

        public void Popup(Vector3 world, string text, Color c, float size)
        {
            var p = pops[nextPop];
            nextPop = (nextPop + 1) % pops.Count;
            Start(p, world, text, c, size, 1f, 1.5f);
        }

        /// A floating damage number (gold and bigger for headshots).
        public void Damage(Vector3 world, int amount, bool crit)
        {
            var p = numbers[nextNum];
            nextNum = (nextNum + 1) % numbers.Count;
            Start(p, world + Random.insideUnitSphere * 0.25f, crit ? "CRIT " + amount : amount.ToString(), crit ? UIKit.Gold : Color.white, crit ? 1.15f : 0.8f, 0.7f, 1.1f);
        }

        void Start(Pop p, Vector3 world, string text, Color c, float size, float life, float rise)
        {
            p.world = world; p.life = p.max = life; p.size = size; p.rise = rise;
            p.t.text = text; p.t.color = c;
            p.t.gameObject.SetActive(true);
            p.t.transform.SetAsLastSibling();
        }

        public void HitMark(bool kill)
        {
            hitT = 0.12f;
            hitmark.color = kill ? new Color(1f, 0.35f, 0.25f, 1f) : Color.white;
        }

        /// Coins pop out of a kill and fly into the counter; the counter ticks up as they land.
        public void CoinBurst(Vector3 world, int amount)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var sp = cam.WorldToScreenPoint(world);
            Vector2 from;
            if (sp.z < 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, sp, null, out from)) from = Vector2.zero;
            int n = Mathf.Clamp(amount, 1, 6);
            int each = amount / n, rest = amount - each * n;
            for (int i = 0; i < n; i++)
            {
                Flyer f = null;
                foreach (var x in flyers) if (!x.r.gameObject.activeSelf) { f = x; break; }
                if (f == null) break;   // all coins busy: the counter just jumps
                f.value = each + (i == 0 ? rest : 0);
                f.from = from + Random.insideUnitCircle * 26f;
                f.ctrl = f.from + new Vector2(Random.Range(-120f, 120f), Random.Range(60f, 160f));
                f.t = -i * 0.05f;
                f.dur = Random.Range(0.55f, 0.8f);
                f.r.anchoredPosition = f.from;
                f.r.localScale = Vector3.zero;
                f.r.gameObject.SetActive(true);
                coinsInFlight += f.value;
            }
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool playing = Game.I != null && Game.I.Playing;
            var cam = Camera.main;

            // crosshair follows the mouse on desktop and opens up while firing
            bool touch = Input.touchCount > 0 || Application.isMobilePlatform;
            crosshair.gameObject.SetActive(hud.activeSelf && !touch && playing);
            if (crosshair.gameObject.activeSelf)
            {
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, Input.mousePosition, null, out local);
                crosshair.anchoredPosition = local;
                var sh = Shooter.Active;
                spread = Mathf.Lerp(spread, sh != null && sh.Firing ? 1f : 0f, 1f - Mathf.Exp(-dt * 14f));
                crosshair.localScale = Vector3.one * (1f + spread * 0.25f);
                Cursor.visible = false;
            }
            else Cursor.visible = true;
            hitT -= dt;
            var hc = hitmark.color; hc.a = Mathf.Clamp01(hitT / 0.12f); hitmark.color = hc;

            if (Shooter.Wall != null)
            {
                float ready = Shooter.Wall.BarrelReady;
                bool on = ready >= 1f;
                barrelFill.fillAmount = 1f - ready;
                barrelFace.color = on ? Color.white : new Color(0.75f, 0.7f, 0.72f);
                barrelGlow.gameObject.SetActive(on);
                if (on)
                {
                    float k = Mathf.Repeat(Time.unscaledTime * 1.2f, 1f);
                    barrelGlow.rectTransform.localScale = Vector3.one * (0.9f + k * 0.3f);
                    barrelGlow.color = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 1f - k);
                }
                bool hasAir = Econ.Lvl(Up.Airstrike) > 0;
                if (airBox.activeSelf != hasAir) airBox.SetActive(hasAir);
                if (hasAir)
                {
                    float ar = Shooter.Wall.AirReady;
                    airFill.fillAmount = 1f - ar;
                    airFace.color = ar >= 1f ? Color.white : new Color(0.75f, 0.7f, 0.72f);
                }
            }
            // the switch button shows the other shooter
            bool onTower = CameraRig.I != null && CameraRig.I.View == 1;
            var por = UIKit.Spr(onTower ? "por_shaun" : "por_lis");
            if (portrait.sprite != por) { portrait.sprite = por; switchName.text = onTower ? "SHAUN" : "LIS"; }

            wallShown = Mathf.MoveTowards(wallShown, wallTarget, dt * 0.8f);
            wallFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.04f, wallShown), 1f);
            wallFill.color = wallShown > 0.5f ? Color.Lerp(UIKit.Gold, UIKit.Green, (wallShown - 0.5f) * 2f) : Color.Lerp(UIKit.Red, UIKit.Gold, wallShown * 2f);
            wallPct.text = Mathf.CeilToInt(wallShown * 100f) + "%";
            progShown = Mathf.MoveTowards(progShown, progTarget, dt * 1.5f);
            progFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.04f, progShown), 1f);

            // boss health
            var boss = Level.I != null ? Level.I.Boss : null;
            bool showBoss = hud.activeSelf && boss != null && boss.IsAlive;
            if (bossBox.activeSelf != showBoss) bossBox.SetActive(showBoss);
            if (showBoss) bossFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.03f, boss.Hp / boss.MaxHp), 1f);

            // coins: the counter lags behind the savings by what is still flying in
            int target = (Save.Data != null ? Save.Data.coins : 0) - coinsInFlight;
            if (coinsShown < 0) coinsShown = target;
            if (coinsShown != target) coinsShown = target;
            coinText.text = coinsShown.ToString("N0");
            coinPunch = Mathf.Max(0f, coinPunch - dt * 5f);
            coinIcon.localScale = Vector3.one * (1f + coinPunch * 0.25f);
            Vector2 coinTo = Root.InverseTransformPoint(coinIcon.position);
            foreach (var f in flyers)
            {
                if (!f.r.gameObject.activeSelf) continue;
                f.t += dt;
                if (f.t < 0f) continue;
                float k = Mathf.Clamp01(f.t / f.dur);
                float e = k * k;
                var p = (1f - e) * (1f - e) * f.from + 2f * (1f - e) * e * f.ctrl + e * e * coinTo;
                f.r.anchoredPosition = p;
                f.r.localScale = Vector3.one * Mathf.Min(1f, f.t * 8f) * (1f - 0.35f * e);
                if (k >= 1f)
                {
                    f.r.gameObject.SetActive(false);
                    coinsInFlight = Mathf.Max(0, coinsInFlight - f.value);
                    coinPunch = 1f;
                    SoundBank.I.Play(SoundBank.I.coin, 0.25f, Random.Range(0.95f, 1.1f));
                }
            }

            damageT = Mathf.Max(0f, damageT - dt * 2.5f);
            damage.color = new Color(0.9f, 0.1f, 0.05f, damageT * 0.6f);

            if (banner.activeSelf)
            {
                bannerT -= dt;
                float ba = Mathf.Clamp01(bannerT / 0.4f);
                foreach (var g in banner.GetComponentsInChildren<Graphic>())
                {
                    var c = g == bannerText ? bannerColor : g == bannerSub ? UIKit.Gold : Color.white;
                    c.a = ba;
                    g.color = c;
                }
                bannerIn = Mathf.Min(1f, bannerIn + dt * 5f);
                float slide = 1f - (1f - bannerIn) * (1f - bannerIn);
                ((RectTransform)banner.transform).anchoredPosition = new Vector2(-460f * (1f - slide), 0f);
                if (bannerT <= 0f) banner.SetActive(false);
            }

            if (cam != null)
            {
                Float(pops, cam, dt);
                Float(numbers, cam, dt);
                HealthBars(cam);
            }
        }

        void Float(List<Pop> list, Camera cam, float dt)
        {
            foreach (var p in list)
            {
                if (p.life <= 0f) continue;
                p.life -= dt;
                if (p.life <= 0f) { p.t.gameObject.SetActive(false); continue; }
                float k = 1f - p.life / p.max;
                var sp = cam.WorldToScreenPoint(p.world + Vector3.up * k * p.rise);
                if (sp.z < 0f) { p.t.gameObject.SetActive(false); p.life = 0f; continue; }
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, sp, null, out local);
                p.t.rectTransform.anchoredPosition = local;
                float pop = k < 0.15f ? 1f + (0.15f - k) * 3f : 1f;
                p.t.transform.localScale = Vector3.one * p.size * pop;
                var c = p.t.color; c.a = Mathf.Clamp01((1f - k) * 2.5f); p.t.color = c;
            }
        }

        /// Small health bars over wounded zombies (full-health ones stay clean).
        void HealthBars(Camera cam)
        {
            int used = 0;
            if (hud.activeSelf)
            {
                foreach (var z in Zombie.Alive)
                {
                    if (z == null || z.Hp >= z.MaxHp || z.Head == null || z.IsBoss) continue;
                    var sp = cam.WorldToScreenPoint(z.Head.position + Vector3.up * (z.IsBrute ? 0.75f : 0.5f));
                    if (sp.z < 0f) continue;
                    if (used == bars.Count)
                    {
                        var track = UIKit.Pic(barBox, "track", Color.white);
                        var fill = UIKit.Pic(track.transform, "fill", Color.white);
                        UIKit.Inset(fill.rectTransform, 2.5f, 2.5f, 2.5f, 2.5f);
                        bars.Add(new Bar { r = track.rectTransform, fill = fill });
                    }
                    var b = bars[used++];
                    if (!b.r.gameObject.activeSelf) b.r.gameObject.SetActive(true);
                    Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(barBox, sp, null, out local);
                    b.r.anchoredPosition = local;
                    b.r.sizeDelta = z.IsBrute ? new Vector2(90, 16) : new Vector2(46, 12);
                    float k = Mathf.Clamp01(z.Hp / z.MaxHp);
                    b.fill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.08f, k), 1f);
                    b.fill.color = k > 0.5f ? Color.Lerp(UIKit.Gold, UIKit.Lime, (k - 0.5f) * 2f) : Color.Lerp(UIKit.Red, UIKit.Gold, k * 2f);
                }
            }
            for (int i = used; i < bars.Count; i++) if (bars[i].r.gameObject.activeSelf) bars[i].r.gameObject.SetActive(false);
        }
    }

    /// Fades a hint out after a while.
    public class FadeAfter : MonoBehaviour
    {
        public float delay = 6f;
        float t;
        Text text;
        void Awake() { text = GetComponent<Text>(); }
        void OnEnable() { t = 0f; }
        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (text != null) { var c = text.color; c.a = Mathf.Clamp01(1f - (t - delay)); text.color = c; }
        }
    }
}
