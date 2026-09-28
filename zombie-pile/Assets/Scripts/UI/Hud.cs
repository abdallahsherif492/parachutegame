using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombiePile
{
    /// All UI, built from code: HUD, menu, upgrade cards, game over, floating popups.
    public class Hud : MonoBehaviour
    {
        public static Hud I;

        RectTransform root;
        Text waveText, leftText, killText, wallPct, bannerText, bannerSub, bestText, overStats, overTitle;
        Image wallFill, barrelFill, barrelFace, barrelGlow, damage;
        RectTransform crosshair;
        RectTransform[] ticks = new RectTransform[4];
        Button barrelBtn, muteBtn;
        GameObject hud, menu, upgrades, over, bestPill;
        RectTransform cardRow, killBox;
        float bannerT, damageT, killPunch, wallShown = 1f, wallTarget = 1f, spread;
        Color bannerColor = UIKit.Gold;

        class Pop { public Text t; public Vector3 world; public float life; public float size; }
        readonly List<Pop> pops = new List<Pop>();
        int nextPop;

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

            // top shade so the numbers read on any background
            var topShade = UIKit.Image(ht, "TopShade", new Color(0f, 0f, 0f, 0.45f), UIKit.Fade);
            topShade.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);
            UIKit.Place(topShade.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000, 150));
            topShade.rectTransform.pivot = new Vector2(0.5f, 0f);

            // wave (top left)
            var wavePanel = UIKit.Panel(ht, "WavePanel", UIKit.Dark);
            UIKit.Place(wavePanel.rectTransform, new Vector2(0, 1), new Vector2(16, -14), new Vector2(250, 96));
            waveText = UIKit.Title(wavePanel.transform, "WAVE 1", 60, UIKit.Gold, TextAnchor.UpperLeft);
            UIKit.Inset(waveText.rectTransform, 18, 34, 10, 2);
            leftText = UIKit.Text(wavePanel.transform, "0 ZOMBIES LEFT", 22, UIKit.Lime, TextAnchor.LowerLeft);
            UIKit.Inset(leftText.rectTransform, 20, 12, 10, 50);

            // kills (top right)
            var killPanel = UIKit.Panel(ht, "KillPanel", UIKit.Dark);
            UIKit.Place(killPanel.rectTransform, new Vector2(1, 1), new Vector2(-16, -14), new Vector2(210, 80));
            killBox = killPanel.rectTransform;
            var skull = UIKit.Skull(killPanel.transform, 50);
            UIKit.Place(skull, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(50, 50));
            killText = UIKit.Title(killPanel.transform, "0", 60, Color.white, TextAnchor.MiddleRight);
            UIKit.Inset(killText.rectTransform, 70, 0, 18, 0);

            // wall health (top centre)
            var wallPanel = UIKit.Panel(ht, "WallPanel", UIKit.Dark);
            UIKit.Place(wallPanel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(470, 64));
            var wl = UIKit.Title(wallPanel.transform, "WALL", 40, Color.white, TextAnchor.MiddleLeft);
            UIKit.Inset(wl.rectTransform, 18, 0, 0, 0);
            var track = UIKit.Image(wallPanel.transform, "Track", new Color(0f, 0f, 0f, 0.6f), UIKit.Round);
            UIKit.Inset(track.rectTransform, 110, 14, 16, 14);
            wallFill = UIKit.Image(track.transform, "Fill", UIKit.Green, UIKit.Round);
            UIKit.Inset(wallFill.rectTransform, 3, 3, 3, 3);
            wallFill.rectTransform.pivot = new Vector2(0, 0.5f);
            var wg = UIKit.Image(wallFill.transform, "Gloss", new Color(1f, 1f, 1f, 0.28f), UIKit.Round);
            UIKit.Inset(wg.rectTransform, 4, 17, 4, 3);
            wallPct = UIKit.Text(track.transform, "100%", 24, Color.white);
            UIKit.Stretch(wallPct.rectTransform);

            // barrel (bottom right)
            barrelBtn = UIKit.Button(ht, "", UIKit.Red, new Vector2(170, 170), () => Gunner.I.ThrowBarrel());
            UIKit.Place((RectTransform)barrelBtn.transform, new Vector2(1, 0), new Vector2(-28, 28), new Vector2(170, 170));
            ((Image)barrelBtn.targetGraphic).sprite = UIKit.Circle;
            ((Image)barrelBtn.targetGraphic).type = Image.Type.Simple;
            barrelFace = UIKit.Face(barrelBtn);
            barrelFace.sprite = UIKit.Circle;
            barrelFace.type = Image.Type.Simple;
            var bgloss = barrelFace.transform.Find("Gloss");
            if (bgloss != null) Destroy(bgloss.gameObject);
            var icon = UIKit.BarrelIcon(barrelFace.transform, 84);
            UIKit.Place(icon, new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(84, 84));
            var bl = UIKit.Title(barrelFace.transform, "BARREL", 34, Color.white);
            UIKit.Place(bl.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(170, 36));
            if (!Application.isMobilePlatform)
            {
                var key = UIKit.Panel(barrelBtn.transform, "Key", UIKit.Dark);
                UIKit.Place(key.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 40), new Vector2(110, 34));
                var kt = UIKit.Text(key.transform, "SPACE", 20, Color.white, TextAnchor.MiddleCenter, false);
                UIKit.Stretch(kt.rectTransform);
            }
            barrelFill = UIKit.Image(barrelFace.transform, "Cooldown", new Color(0f, 0f, 0f, 0.6f), UIKit.Circle);
            UIKit.Stretch(barrelFill.rectTransform);
            barrelFill.type = Image.Type.Filled;
            barrelFill.fillMethod = Image.FillMethod.Radial360;
            barrelFill.fillOrigin = (int)Image.Origin360.Top;
            barrelFill.fillClockwise = false;
            barrelGlow = UIKit.Image(barrelBtn.transform, "Glow", UIKit.Gold, UIKit.Ring);
            UIKit.Place(barrelGlow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 200));

            var hint = UIKit.Text(ht, Application.isMobilePlatform ? "HOLD ANYWHERE TO SHOOT" : "HOLD THE MOUSE TO SHOOT   ·   SPACE / RIGHT-CLICK = BARREL", 24, Color.white);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(900, 30));
            hint.gameObject.AddComponent<FadeAfter>().delay = 8f;

            // crosshair: four ticks and a dot, opening up while firing
            crosshair = UIKit.Node("Crosshair", root);
            crosshair.sizeDelta = new Vector2(60, 60);
            for (int i = 0; i < 4; i++)
            {
                var tk = UIKit.Image(crosshair, "Tick", Color.white, UIKit.Round);
                tk.rectTransform.sizeDelta = i % 2 == 0 ? new Vector2(6, 18) : new Vector2(18, 6);
                var o = tk.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.7f); o.effectDistance = new Vector2(1.5f, -1.5f);
                ticks[i] = tk.rectTransform;
            }
            var dot = UIKit.Image(crosshair, "Dot", new Color(1f, 0.25f, 0.2f), UIKit.Circle);
            dot.rectTransform.sizeDelta = new Vector2(8, 8);

            bannerText = UIKit.Title(root, "", 110, UIKit.Gold);
            UIKit.Place(bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1200, 120));
            bannerSub = UIKit.Text(root, "", 34, Color.white);
            UIKit.Place(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 78), new Vector2(1200, 50));

            for (int i = 0; i < 16; i++)
            {
                var t = UIKit.Title(root, "", 44, Color.white);
                t.rectTransform.sizeDelta = new Vector2(600, 60);
                t.gameObject.SetActive(false);
                pops.Add(new Pop { t = t });
            }

            // ---------------- menu
            menu = Overlay("Menu", new Color(0f, 0f, 0f, 0.2f), new Color(0.02f, 0.01f, 0.03f, 0.9f));
            var mt = menu.transform;
            var logo = UIKit.Node("Logo", mt);
            UIKit.Place(logo, new Vector2(0.5f, 0.5f), new Vector2(0, 185), new Vector2(900, 200));
            logo.localRotation = Quaternion.Euler(0, 0, 3f);
            var title = UIKit.Title(logo, "ZOMBIE PILE", 170, UIKit.Lime);
            UIKit.Stretch(title.rectTransform);
            var stamp = UIKit.Image(logo, "Stamp", UIKit.Red, UIKit.Round);
            UIKit.Place(stamp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330, -90), new Vector2(250, 70));
            stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, -12f);
            var so = stamp.gameObject.AddComponent<Outline>(); so.effectColor = Color.white; so.effectDistance = new Vector2(4f, -4f);
            var st = UIKit.Title(stamp.transform, "NOT FAKE!", 54, Color.white);
            UIKit.Stretch(st.rectTransform);
            stamp.gameObject.AddComponent<Pulse>();
            var sub = UIKit.Text(mt, "THE GAME FROM THE ADS. FOR REAL.", 34, Color.white);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1100, 50));
            var sub2 = UIKit.Text(mt, "Shoot the pile down before it climbs the wall!", 26, new Color(1f, 1f, 1f, 0.8f));
            UIKit.Place(sub2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(1100, 40));
            var play = UIKit.Button(mt, "PLAY", UIKit.Green, new Vector2(400, 124), () => { SoundBank.I.Play(SoundBank.I.click); Game.I.StartRun(); }, 72);
            UIKit.Place((RectTransform)play.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(400, 124));
            play.gameObject.AddComponent<Pulse>().amount = 0.04f;
            var bp = UIKit.Panel(mt, "Best", UIKit.Dark);
            UIKit.Place(bp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -238), new Vector2(300, 52));
            bestPill = bp.gameObject;
            bestText = UIKit.Text(bp.transform, "", 30, UIKit.Gold);
            UIKit.Stretch(bestText.rectTransform);
            muteBtn = UIKit.Button(mt, Save.Data.muted ? "SOUND: OFF" : "SOUND: ON", UIKit.Gray, new Vector2(210, 64), () =>
            {
                Save.Data.muted = !Save.Data.muted; Save.Write(); SoundBank.ApplyMute();
                UIKit.SetLabel(muteBtn, Save.Data.muted ? "SOUND: OFF" : "SOUND: ON");
            }, 28);
            UIKit.Place((RectTransform)muteBtn.transform, new Vector2(1, 0), new Vector2(-24, 24), new Vector2(210, 64));

            var ver = UIKit.Text(mt, "Zombie Pile " + Game.Version + " · " + Kit.Status, 16, new Color(1f, 1f, 1f, 0.45f), TextAnchor.LowerLeft, false);
            UIKit.Place(ver.rectTransform, new Vector2(0, 0), new Vector2(16, 12), new Vector2(1000, 26));

            // ---------------- upgrades
            upgrades = Overlay("Upgrades", new Color(0f, 0f, 0f, 0.5f), new Color(0.02f, 0.01f, 0.03f, 0.85f));
            var ut = UIKit.Title(upgrades.transform, "CHOOSE AN UPGRADE", 80, UIKit.Gold);
            UIKit.Place(ut.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1200, 90));
            cardRow = UIKit.Node("Cards", upgrades.transform);
            UIKit.Place(cardRow, new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1100, 380));
            upgrades.SetActive(false);

            // ---------------- game over
            over = Overlay("Over", new Color(0.25f, 0.02f, 0.02f, 0.45f), new Color(0.05f, 0f, 0f, 0.92f));
            overTitle = UIKit.Title(over.transform, "OVERRUN!", 170, new Color(1f, 0.28f, 0.22f));
            UIKit.Place(overTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 175), new Vector2(1200, 180));
            overTitle.rectTransform.localRotation = Quaternion.Euler(0, 0, -3f);
            var sp = UIKit.Panel(over.transform, "Stats", UIKit.Dark);
            UIKit.Place(sp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(560, 170));
            overStats = UIKit.Text(sp.transform, "", 38, Color.white);
            UIKit.Stretch(overStats.rectTransform);
            var retry = UIKit.Button(over.transform, "TRY AGAIN", UIKit.Green, new Vector2(420, 120), () => { over.SetActive(false); Game.I.Retry(); }, 60);
            UIKit.Place((RectTransform)retry.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(420, 120));
            retry.gameObject.AddComponent<Pulse>().amount = 0.04f;
            over.SetActive(false);
            hud.SetActive(false);
        }

        /// Full-screen overlay: a flat tint plus a dark fade rising from the bottom.
        GameObject Overlay(string name, Color tint, Color bottom)
        {
            var img = UIKit.Image(root, name, tint, null, true);
            UIKit.Stretch(img.rectTransform);
            var f = UIKit.Image(img.transform, "Fade", bottom, UIKit.Fade);
            UIKit.Stretch(f.rectTransform);
            f.rectTransform.anchorMax = new Vector2(1f, 0.75f);
            return img.gameObject;
        }

        // ------------------------------------------------------------------ API
        public void ShowMenu(int best)
        {
            menu.SetActive(true); hud.SetActive(false);
            bestText.text = best > 0 ? "BEST: WAVE " + best : "";
            bestPill.SetActive(best > 0);
        }

        public void HideMenu() { menu.SetActive(false); hud.SetActive(true); }
        public void SetWave(int n) { waveText.text = "WAVE " + n; }
        public void SetLeft(int n) { leftText.text = n + (n == 1 ? " ZOMBIE LEFT" : " ZOMBIES LEFT"); }
        public void SetKills(int n) { if (killText.text != n.ToString()) killPunch = 1f; killText.text = n.ToString(); }
        public void SetWall(float k) { wallTarget = k; }
        public void FlashDamage() { damageT = 1f; }

        public void Banner(string title, string sub)
        {
            bannerText.text = title; bannerSub.text = sub; bannerT = 2.2f;
            bannerColor = title.Contains("CLEARED") ? UIKit.Lime : UIKit.Gold;
            bannerText.transform.localScale = Vector3.one * 1.4f;
        }

        public void Popup(Vector3 world, string text, Color c, float size)
        {
            var p = pops[nextPop];
            nextPop = (nextPop + 1) % pops.Count;
            p.world = world; p.life = 1f; p.size = size;
            p.t.text = text; p.t.color = c;
            p.t.gameObject.SetActive(true);
        }

        public void ShowUpgrades(string[] titles, string[] descs, Color[] colors, Action<int> pick)
        {
            for (int i = cardRow.childCount - 1; i >= 0; i--) Destroy(cardRow.GetChild(i).gameObject);
            bool portrait = Screen.height > Screen.width;
            for (int i = 0; i < titles.Length; i++)
            {
                int idx = i;
                var size = portrait ? new Vector2(640, 160) : new Vector2(330, 350);
                var card = UIKit.Button(cardRow, "", colors[i], size, () =>
                {
                    upgrades.SetActive(false);
                    pick(idx);
                });
                var r = (RectTransform)card.transform;
                if (portrait) UIKit.Place(r, new Vector2(0.5f, 0.5f), new Vector2(0, 180 - i * 180), size);
                else UIKit.Place(r, new Vector2(0.5f, 0.5f), new Vector2((i - (titles.Length - 1) * 0.5f) * 365, 0), size);
                var face = UIKit.Face(card).transform;
                var t = UIKit.Title(face, titles[i], portrait ? 54 : 50, Color.white);
                UIKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, portrait ? -12 : -26), new Vector2(size.x - 20, 70));
                var box = UIKit.Image(face, "Desc", new Color(0f, 0f, 0f, 0.28f), UIKit.Round);
                if (portrait) UIKit.Inset(box.rectTransform, 16, 14, 16, 84);
                else UIKit.Inset(box.rectTransform, 16, 70, 16, 120);
                var d = UIKit.Text(box.transform, descs[i], 26, Color.white);
                d.horizontalOverflow = HorizontalWrapMode.Wrap;
                UIKit.Inset(d.rectTransform, 12, 6, 12, 6);
                if (!portrait)
                {
                    var tap = UIKit.Text(face, "TAP TO PICK", 22, new Color(1f, 1f, 1f, 0.85f));
                    UIKit.Place(tap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(300, 30));
                }
            }
            upgrades.SetActive(true);
        }

        public void ShowGameOver(int wave, int kills, int best, bool newBest)
        {
            over.SetActive(true);
            overTitle.text = "OVERRUN!";
            overStats.text = "YOU HELD UNTIL WAVE " + wave + "\n" + kills + " ZOMBIES DOWN\n" + (newBest ? "<color=#FFD133>NEW BEST!</color>" : "<color=#FFD133>BEST: WAVE " + best + "</color>");
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            // crosshair follows the mouse on desktop
            bool touch = Input.touchCount > 0 || Application.isMobilePlatform;
            crosshair.gameObject.SetActive(hud.activeSelf && !touch && Game.I != null && Game.I.Playing);
            if (crosshair.gameObject.activeSelf)
            {
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Input.mousePosition, null, out local);
                crosshair.anchoredPosition = local;
                spread = Mathf.Lerp(spread, Gunner.I.Firing ? 1f : 0f, 1f - Mathf.Exp(-dt * 14f));
                float g = 11f + spread * 9f;
                ticks[0].anchoredPosition = new Vector2(0, g + 9); ticks[2].anchoredPosition = new Vector2(0, -g - 9);
                ticks[1].anchoredPosition = new Vector2(g + 9, 0); ticks[3].anchoredPosition = new Vector2(-g - 9, 0);
                Cursor.visible = false;
            }
            else Cursor.visible = true;

            if (Gunner.I != null)
            {
                float ready = Gunner.I.BarrelReady;
                barrelFill.fillAmount = 1f - ready;
                bool on = ready >= 1f;
                barrelFace.color = on ? UIKit.Red : UIKit.Darker(UIKit.Red, 0.35f);
                barrelFace.transform.localScale = Vector3.one * (on ? 1f + Mathf.Sin(Time.time * 6f) * 0.03f : 0.96f);
                barrelGlow.gameObject.SetActive(on);
                if (on)
                {
                    float k = Mathf.Repeat(Time.time * 1.2f, 1f);
                    barrelGlow.rectTransform.localScale = Vector3.one * (0.85f + k * 0.35f);
                    barrelGlow.color = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 1f - k);
                }
            }
            wallShown = Mathf.MoveTowards(wallShown, wallTarget, dt * 0.8f);
            wallFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.03f, wallShown), 1f);
            wallFill.color = wallShown > 0.5f ? Color.Lerp(UIKit.Gold, UIKit.Green, (wallShown - 0.5f) * 2f)
                                              : Color.Lerp(UIKit.Red, UIKit.Gold, wallShown * 2f);
            wallPct.text = Mathf.CeilToInt(wallShown * 100f) + "%";

            killPunch = Mathf.Max(0f, killPunch - dt * 6f);
            killBox.localScale = Vector3.one * (1f + killPunch * 0.08f);

            damageT = Mathf.Max(0f, damageT - dt * 2.5f);
            damage.color = new Color(0.9f, 0.1f, 0.05f, damageT * 0.6f);

            bannerT -= dt;
            float ba = Mathf.Clamp01(bannerT / 0.5f);
            bannerText.color = new Color(bannerColor.r, bannerColor.g, bannerColor.b, ba);
            bannerSub.color = new Color(1f, 1f, 1f, ba);
            bannerText.transform.localScale = Vector3.Lerp(bannerText.transform.localScale, Vector3.one, 1f - Mathf.Exp(-dt * 10f));

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
            if (text != null) { var c = text.color; c.a = Mathf.Clamp01(1f - (t - delay)) * 0.8f; text.color = c; }
        }
    }
}
