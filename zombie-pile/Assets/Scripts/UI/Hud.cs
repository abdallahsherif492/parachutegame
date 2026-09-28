using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombiePile
{
    /// All UI, built from code with the rendered sprites in Resources/UI. Every position comes from the
    /// 1280x720 design mock-up (Tools/ui-design/mock.html) through UIKit.At, so the game matches the design.
    public class Hud : MonoBehaviour
    {
        public static Hud I;

        static readonly Vector2 TL = new Vector2(0f, 1f), TC = new Vector2(0.5f, 1f), TR = new Vector2(1f, 1f);
        static readonly Vector2 BL = new Vector2(0f, 0f), BC = new Vector2(0.5f, 0f), BR = new Vector2(1f, 0f), C = new Vector2(0.5f, 0.5f);

        RectTransform root, cardRow, crosshair, barBox;
        Text waveText, leftText, killText, wallPct, bannerText, bannerSub, bestText, overWave, overKills, overBest, overBestLabel;
        Image wallFill, waveFill, barrelFill, barrelGlow, barrelFace, damage, muteIcon;
        Button barrelBtn;
        GameObject hud, menu, upgrades, over, bestPill, banner;
        float bannerT, damageT, killPunch, wallShown = 1f, wallTarget = 1f, waveShown, waveTarget, spread;
        Color bannerColor = Color.white;

        class Pop { public Text t; public Vector3 world; public float life; public float size; }
        readonly List<Pop> pops = new List<Pop>();
        int nextPop;

        class Bar { public RectTransform r; public Image fill; }
        readonly List<Bar> bars = new List<Bar>();

        public static void Build()
        {
            var go = new GameObject("UI");
            go.AddComponent<Hud>().Make();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        // ------------------------------------------------------------------ small builders
        static Text Label(Transform p, string s, bool bangers, int size, Color c, Vector2 anchor, float x, float y, float w, TextAnchor align = TextAnchor.MiddleCenter, bool stroke = true)
        {
            var t = bangers ? UIKit.Title(p, s, size, c, align) : UIKit.Text(p, s, size, c, align, stroke);
            UIKit.At(t.rectTransform, anchor, x, y, w, size);
            return t;
        }

        static Image Pic(Transform p, string sprite, Color c, Vector2 anchor, float x, float y, float w, float h)
        {
            var img = UIKit.Pic(p, sprite, c);
            UIKit.At(img.rectTransform, anchor, x, y, w, h);
            return img;
        }

        /// A bar fill inside a track: stretches from the left, its width set through anchorMax.x.
        static Image FillIn(Image track, Color c, float pad)
        {
            var f = UIKit.Pic(track.transform, "fill", c);
            UIKit.Inset(f.rectTransform, pad, pad, pad, pad);
            return f;
        }

        /// A child placed with mock-up coordinates relative to its parent's top-left corner.
        static RectTransform In(RectTransform r, Vector2 parentSize, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = new Vector2(x + w * 0.5f - parentSize.x * 0.5f, -(y + h * 0.5f - parentSize.y * 0.5f));
            return r;
        }

        GameObject Overlay(string name, Color tint)
        {
            var img = UIKit.Image(root, name, tint, null, true);
            UIKit.Stretch(img.rectTransform);
            return img.gameObject;
        }

        static void Vignette(Transform p, float alpha)
        {
            var v = UIKit.Pic(p, "vignette", new Color(1f, 1f, 1f, alpha));
            v.preserveAspect = false;
            UIKit.Stretch(v.rectTransform);
        }

        // ------------------------------------------------------------------ build
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
            root = (RectTransform)transform;

            damage = UIKit.Image(root, "Damage", new Color(0.9f, 0.1f, 0.05f, 0f), UIKit.Fade);
            UIKit.Stretch(damage.rectTransform);
            damage.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);   // red creeps in from the top (the wall)

            // ---------------- HUD
            hud = UIKit.Node("HUD", root).gameObject;
            UIKit.Stretch((RectTransform)hud.transform);
            var ht = hud.transform;
            Vignette(ht, 0.55f);
            barBox = UIKit.Node("HealthBars", ht);
            UIKit.Stretch(barBox);

            // wall health (top left)
            Pic(ht, "panel", Color.white, TL, 16, 14, 330, 80);
            Pic(ht, "ic_wall", Color.white, TL, 26, 20, 68, 68);
            Label(ht, "WALL", true, 30, Color.white, TL, 104, 22, 120, TextAnchor.MiddleLeft);
            var wallTrack = Pic(ht, "track", Color.white, TL, 102, 52, 230, 32);
            wallFill = FillIn(wallTrack, UIKit.Green, 4f);
            wallPct = Label(ht, "100%", false, 22, Color.white, TL, 102, 56, 230);

            // wave + progress (top centre)
            Pic(ht, "panel", Color.white, TC, 430, 14, 420, 98);
            waveText = Label(ht, "WAVE 1", true, 48, UIKit.Gold, TC, 430, 22, 420);
            var waveTrack = Pic(ht, "track", Color.white, TC, 452, 70, 330, 30);
            waveFill = FillIn(waveTrack, UIKit.Lime, 4f);
            leftText = Label(ht, "0 LEFT", false, 21, Color.white, TC, 452, 74, 330);
            Pic(ht, "zhead", Color.white, TC, 770, 46, 76, 76);

            // kills (top right)
            Pic(ht, "panel", Color.white, TR, 1054, 14, 210, 80);
            Pic(ht, "ic_skull", Color.white, TR, 1064, 20, 68, 68);
            killText = Label(ht, "0", true, 60, Color.white, TR, 1130, 22, 120, TextAnchor.MiddleRight);

            // barrel button (bottom right): its parts are children so they squash with the press
            barrelBtn = UIKit.SpriteButton(ht, "round_red", "", 0, () => Gunner.I.ThrowBarrel());
            var bt = (RectTransform)barrelBtn.transform;
            UIKit.At(bt, BR, 1082, 522, 176, 176);
            var bsz = new Vector2(176, 176);
            barrelFace = (Image)barrelBtn.targetGraphic;
            In(UIKit.Pic(bt, "barrel3d", Color.white).rectTransform, bsz, 28, 4, 120, 120);
            In(UIKit.Title(bt, "BARREL", 34, Color.white).rectTransform, bsz, 0, 118, 176, 34);
            barrelFill = UIKit.Image(bt, "Cooldown", new Color(0.05f, 0.02f, 0.08f, 0.62f), UIKit.Circle);
            In(barrelFill.rectTransform, bsz, 6, 4, 164, 164);
            barrelFill.type = Image.Type.Filled;
            barrelFill.fillMethod = Image.FillMethod.Radial360;
            barrelFill.fillOrigin = (int)Image.Origin360.Top;
            barrelFill.fillClockwise = false;
            barrelGlow = UIKit.Image(bt, "Glow", UIKit.Gold, UIKit.Ring);
            In(barrelGlow.rectTransform, bsz, -14, -14, 204, 204);
            if (!Application.isMobilePlatform)
            {
                var key = UIKit.Pic(bt, "keycap", Color.white);
                In(key.rectTransform, bsz, 46, -36, 84, 38);
                var kt = UIKit.Text(key.transform, "SPACE", 18, new Color(0.17f, 0.11f, 0.23f), TextAnchor.MiddleCenter, false);
                UIKit.Inset(kt.rectTransform, 0, 5, 0, 0);
            }

            var hint = Label(ht, Application.isMobilePlatform ? "HOLD ANYWHERE TO SHOOT" : "HOLD THE MOUSE TO SHOOT   |   SPACE / RIGHT-CLICK = BARREL", false, 22, Color.white, BC, 0, 684, 1280);
            hint.gameObject.AddComponent<FadeAfter>().delay = 8f;

            crosshair = UIKit.Pic(root, "crosshair", Color.white).rectTransform;
            crosshair.sizeDelta = new Vector2(72, 72);

            // wave banner on a ribbon
            banner = UIKit.Node("Banner", root).gameObject;
            UIKit.Stretch((RectTransform)banner.transform);
            Pic(banner.transform, "ribbon", Color.white, C, 370, 170, 540, 100);
            bannerText = Label(banner.transform, "", true, 76, Color.white, C, 370, 174, 540);
            bannerSub = Label(banner.transform, "", true, 40, UIKit.Gold, C, 170, 286, 940);
            banner.SetActive(false);

            for (int i = 0; i < 16; i++)
            {
                var t = UIKit.Title(root, "", 44, Color.white);
                t.rectTransform.sizeDelta = new Vector2(600, 60);
                t.gameObject.SetActive(false);
                pops.Add(new Pop { t = t });
            }

            // ---------------- menu
            menu = Overlay("Menu", new Color(0.04f, 0.02f, 0.06f, 0.4f));
            var mt = menu.transform;
            var mf = UIKit.Image(mt, "Fade", new Color(0.04f, 0.02f, 0.06f, 0.85f), UIKit.Fade);
            UIKit.Stretch(mf.rectTransform);
            mf.rectTransform.anchorMax = new Vector2(1f, 0.6f);
            Vignette(mt, 1f);
            Pic(mt, "logo", Color.white, C, 230, 10, 820, 400).gameObject.AddComponent<Pulse>().amount = 0.015f;
            Label(mt, "THE GAME FROM THE ADS. FOR REAL.", false, 34, Color.white, C, 0, 408, 1280);
            var play = UIKit.SpriteButton(mt, "btn_green", "PLAY", 88, () => { SoundBank.I.Play(SoundBank.I.click); Game.I.StartRun(); });
            UIKit.At((RectTransform)play.transform, C, 460, 462, 360, 124);
            play.gameObject.AddComponent<Pulse>().amount = 0.035f;
            bestPill = UIKit.Node("Best", mt).gameObject;
            UIKit.Stretch((RectTransform)bestPill.transform);
            Pic(bestPill.transform, "panel", Color.white, C, 500, 606, 280, 60);
            Pic(bestPill.transform, "ic_trophy", Color.white, C, 514, 610, 52, 52);
            bestText = Label(bestPill.transform, "", false, 30, UIKit.Gold, C, 570, 620, 200, TextAnchor.MiddleLeft);
            var mute = UIKit.SpriteButton(mt, "round_dark", "", 0, () =>
            {
                Save.Data.muted = !Save.Data.muted; Save.Write(); SoundBank.ApplyMute();
                muteIcon.sprite = UIKit.Spr(Save.Data.muted ? "ic_mute" : "ic_sound");
            });
            UIKit.At((RectTransform)mute.transform, BR, 1172, 612, 88, 88);
            muteIcon = UIKit.Pic(mute.transform, Save.Data.muted ? "ic_mute" : "ic_sound", Color.white);
            In(muteIcon.rectTransform, new Vector2(88, 88), 18, 12, 52, 52);
            Label(mt, "Zombie Pile " + Game.Version + "   " + Kit.Status + "   |   models: Quaternius (CC0)   |   icons: game-icons.net by Lorc & Delapouite (CC BY 3.0)", false, 14,
                new Color(1f, 1f, 1f, 0.5f), BL, 16, 698, 1100, TextAnchor.MiddleLeft, false);

            // ---------------- upgrades
            upgrades = Overlay("Upgrades", new Color(0.03f, 0.01f, 0.05f, 0.72f));
            Pic(upgrades.transform, "ribbon", Color.white, C, 310, 34, 660, 100);
            Label(upgrades.transform, "CHOOSE AN UPGRADE", true, 62, Color.white, C, 310, 44, 660);
            cardRow = UIKit.Node("Cards", upgrades.transform);
            UIKit.At(cardRow, C, 150, 170, 980, 380);
            upgrades.SetActive(false);

            // ---------------- game over
            over = Overlay("Over", new Color(0.18f, 0f, 0f, 0.7f));
            var ot = over.transform;
            Vignette(ot, 1f);
            Label(ot, "OVERRUN!", true, 150, new Color(1f, 0.29f, 0.21f), C, 0, 40, 1280).GetComponent<Stroke>().width = 12f;
            Pic(ot, "panel", Color.white, C, 420, 228, 440, 230);
            Pic(ot, "zhead", Color.white, C, 446, 244, 60, 60);
            Label(ot, "WAVE", false, 34, Color.white, C, 520, 256, 150, TextAnchor.MiddleLeft);
            overWave = Label(ot, "1", true, 52, UIKit.Gold, C, 700, 250, 136, TextAnchor.MiddleRight);
            Pic(ot, "ic_skull", Color.white, C, 446, 314, 60, 60);
            Label(ot, "KILLS", false, 34, Color.white, C, 520, 326, 150, TextAnchor.MiddleLeft);
            overKills = Label(ot, "0", true, 52, Color.white, C, 700, 320, 136, TextAnchor.MiddleRight);
            Pic(ot, "ic_trophy", Color.white, C, 446, 384, 60, 60);
            overBestLabel = Label(ot, "BEST", false, 34, Color.white, C, 520, 396, 150, TextAnchor.MiddleLeft);
            overBest = Label(ot, "", true, 48, UIKit.Gold, C, 520, 390, 316, TextAnchor.MiddleRight);
            var retry = UIKit.SpriteButton(ot, "btn_green", "TRY AGAIN", 72, () => { over.SetActive(false); Game.I.Retry(); });
            UIKit.At((RectTransform)retry.transform, C, 440, 490, 400, 124);
            retry.gameObject.AddComponent<Pulse>().amount = 0.035f;
            over.SetActive(false);
            hud.SetActive(false);
        }

        // ------------------------------------------------------------------ API
        public void ShowMenu(int best)
        {
            menu.SetActive(true); hud.SetActive(false);
            bestText.text = "BEST: WAVE " + best;
            bestPill.SetActive(best > 0);
        }

        public void HideMenu() { menu.SetActive(false); hud.SetActive(true); }
        public void SetWave(int n) { waveText.text = "WAVE " + n; waveShown = waveTarget = 0f; }
        public void SetLeft(int n, int total) { leftText.text = n + " LEFT"; waveTarget = total > 0 ? 1f - (float)n / total : 0f; }
        public void SetKills(int n) { if (killText.text != n.ToString()) killPunch = 1f; killText.text = n.ToString(); }
        public void SetWall(float k) { wallTarget = k; }
        public void FlashDamage() { damageT = 1f; }

        public void Banner(string title, string sub)
        {
            bannerText.text = title; bannerSub.text = sub; bannerT = 2.2f;
            bannerColor = title.Contains("CLEARED") ? UIKit.Lime : Color.white;
            banner.SetActive(true);
            banner.transform.localScale = Vector3.one * 1.3f;
        }

        public void Popup(Vector3 world, string text, Color c, float size)
        {
            var p = pops[nextPop];
            nextPop = (nextPop + 1) % pops.Count;
            p.world = world; p.life = 1f; p.size = size;
            p.t.text = text; p.t.color = c;
            p.t.gameObject.SetActive(true);
        }

        public void ShowUpgrades(string[] titles, string[] descs, Color[] colors, string[] icons, Action<int> pick)
        {
            for (int i = cardRow.childCount - 1; i >= 0; i--) Destroy(cardRow.GetChild(i).gameObject);
            // the row is 980 wide: on a narrow (portrait) screen it shrinks to fit
            cardRow.localScale = Vector3.one * Mathf.Min(1f, (root.rect.width - 32f) / 980f);
            var rowSize = new Vector2(980, 380);
            var cardSize = new Vector2(300, 380);
            for (int i = 0; i < titles.Length; i++)
            {
                int idx = i;
                var card = UIKit.SpriteButton(cardRow, "card", "", 0, () => { upgrades.SetActive(false); pick(idx); });
                ((Image)card.targetGraphic).color = colors[i];
                var r = In((RectTransform)card.transform, rowSize, 20 + i * 330 + (3 - titles.Length) * 165, 0, 300, 380);
                In(UIKit.Image(r, "Badge", new Color(0f, 0f, 0f, 0.25f), UIKit.Circle).rectTransform, cardSize, 90, 28, 120, 120);
                if (!string.IsNullOrEmpty(icons[i])) In(UIKit.Pic(r, icons[i], Color.white).rectTransform, cardSize, 95, 33, 110, 110);
                In(UIKit.Title(r, titles[i], 44, Color.white).rectTransform, cardSize, 0, 162, 300, 44);
                In(UIKit.Image(r, "Plate", new Color(0f, 0f, 0f, 0.28f), UIKit.Round).rectTransform, cardSize, 22, 222, 256, 86);
                var d = UIKit.Text(r, descs[i], 25, Color.white);
                d.horizontalOverflow = HorizontalWrapMode.Wrap;
                d.GetComponent<Stroke>().width = 3f;
                In(d.rectTransform, cardSize, 30, 226, 240, 78);
                In(UIKit.Text(r, "TAP TO PICK", 20, new Color(1f, 1f, 1f, 0.9f)).rectTransform, cardSize, 0, 326, 300, 20);
            }
            upgrades.SetActive(true);
        }

        public void ShowGameOver(int wave, int kills, int best, bool newBest)
        {
            over.SetActive(true);
            overWave.text = wave.ToString();
            overKills.text = kills.ToString();
            overBestLabel.gameObject.SetActive(!newBest);
            overBest.text = newBest ? "NEW BEST!" : best.ToString();
            overBest.alignment = newBest ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool playing = Game.I != null && Game.I.Playing;

            // crosshair follows the mouse on desktop and opens up while firing
            bool touch = Input.touchCount > 0 || Application.isMobilePlatform;
            crosshair.gameObject.SetActive(hud.activeSelf && !touch && playing);
            if (crosshair.gameObject.activeSelf)
            {
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Input.mousePosition, null, out local);
                crosshair.anchoredPosition = local;
                spread = Mathf.Lerp(spread, Gunner.I.Firing ? 1f : 0f, 1f - Mathf.Exp(-dt * 14f));
                crosshair.localScale = Vector3.one * (1f + spread * 0.25f);
                Cursor.visible = false;
            }
            else Cursor.visible = true;

            if (Gunner.I != null)
            {
                float ready = Gunner.I.BarrelReady;
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
            }

            wallShown = Mathf.MoveTowards(wallShown, wallTarget, dt * 0.8f);
            wallFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.04f, wallShown), 1f);
            wallFill.color = wallShown > 0.5f ? Color.Lerp(UIKit.Gold, UIKit.Green, (wallShown - 0.5f) * 2f) : Color.Lerp(UIKit.Red, UIKit.Gold, wallShown * 2f);
            wallPct.text = Mathf.CeilToInt(wallShown * 100f) + "%";
            waveShown = Mathf.MoveTowards(waveShown, waveTarget, dt * 1.5f);
            waveFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.04f, waveShown), 1f);

            killPunch = Mathf.Max(0f, killPunch - dt * 6f);
            killText.transform.localScale = Vector3.one * (1f + killPunch * 0.18f);

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
                banner.transform.localScale = Vector3.Lerp(banner.transform.localScale, Vector3.one, 1f - Mathf.Exp(-dt * 12f));
                if (bannerT <= 0f) banner.SetActive(false);
            }

            var cam = Camera.main;
            foreach (var p in pops)
            {
                if (p.life <= 0f) continue;
                p.life -= dt;
                if (p.life <= 0f) { p.t.gameObject.SetActive(false); continue; }
                var sp = cam.WorldToScreenPoint(p.world + Vector3.up * (1f - p.life) * 1.5f);
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out local);
                p.t.rectTransform.anchoredPosition = local;
                float pop = p.life > 0.85f ? 1f + (p.life - 0.85f) * 3f : 1f;
                p.t.transform.localScale = Vector3.one * p.size * pop;
                var c = p.t.color; c.a = Mathf.Clamp01(p.life * 2f); p.t.color = c;
            }

            HealthBars(cam);
        }

        /// Small health bars over wounded zombies (full-health ones stay clean).
        void HealthBars(Camera cam)
        {
            int used = 0;
            if (hud.activeSelf && cam != null)
            {
                foreach (var z in Zombie.Alive)
                {
                    if (z == null || z.Hp >= z.MaxHp || z.Head == null) continue;
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
