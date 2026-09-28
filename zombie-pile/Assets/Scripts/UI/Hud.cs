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
        Text waveText, leftText, killText, bannerText, bannerSub, bestText, overStats, overTitle;
        Image wallFill, barrelFill, damage, crosshair;
        Button barrelBtn, muteBtn;
        GameObject hud, menu, upgrades, over;
        RectTransform cardRow;
        float bannerT, damageT, wallShown = 1f, wallTarget = 1f;

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

            damage = UIKit.Image(root, "Damage", new Color(0.9f, 0.1f, 0.05f, 0f));
            UIKit.Stretch(damage.rectTransform);

            // ---------------- HUD
            hud = UIKit.Node("HUD", root).gameObject;
            UIKit.Stretch((RectTransform)hud.transform);
            var ht = hud.transform;
            waveText = UIKit.Text(ht, "WAVE 1", 46, Color.white, TextAnchor.UpperLeft);
            UIKit.Place(waveText.rectTransform, new Vector2(0, 1), new Vector2(24, -14), new Vector2(360, 60));
            leftText = UIKit.Text(ht, "ZOMBIES 0", 26, new Color(0.75f, 1f, 0.55f), TextAnchor.UpperLeft);
            UIKit.Place(leftText.rectTransform, new Vector2(0, 1), new Vector2(26, -66), new Vector2(360, 36));
            killText = UIKit.Text(ht, "KILLS 0", 34, Color.white, TextAnchor.UpperRight);
            UIKit.Place(killText.rectTransform, new Vector2(1, 1), new Vector2(-24, -16), new Vector2(300, 50));

            var wallBack = UIKit.Image(ht, "WallBack", new Color(0f, 0f, 0f, 0.55f), UIKit.Round);
            UIKit.Place(wallBack.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(420, 34));
            wallFill = UIKit.Image(wallBack.transform, "WallFill", new Color(0.35f, 0.85f, 0.4f), UIKit.Round);
            wallFill.rectTransform.anchorMin = new Vector2(0, 0); wallFill.rectTransform.anchorMax = new Vector2(1, 1);
            wallFill.rectTransform.pivot = new Vector2(0, 0.5f);
            wallFill.rectTransform.offsetMin = new Vector2(4, 4); wallFill.rectTransform.offsetMax = new Vector2(-4, -4);
            var wl = UIKit.Text(wallBack.transform, "WALL", 22, Color.white);
            UIKit.Stretch(wl.rectTransform);

            barrelBtn = UIKit.Button(ht, "", new Color(0.85f, 0.25f, 0.18f), new Vector2(150, 150), () => Gunner.I.ThrowBarrel());
            ((Image)barrelBtn.targetGraphic).sprite = UIKit.Circle;
            var shade = barrelBtn.transform.Find("Shade");
            if (shade != null) Destroy(shade.gameObject);
            UIKit.Place((RectTransform)barrelBtn.transform, new Vector2(1, 0), new Vector2(-30, 30), new Vector2(150, 150));
            barrelFill = UIKit.Image(barrelBtn.transform, "Charge", new Color(0f, 0f, 0f, 0.55f), UIKit.Circle);
            UIKit.Stretch(barrelFill.rectTransform);
            barrelFill.type = Image.Type.Filled;
            barrelFill.fillMethod = Image.FillMethod.Radial360;
            barrelFill.fillClockwise = false;
            var bl = UIKit.Text(barrelBtn.transform, "BARREL", 28, Color.white);
            UIKit.Place(bl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(150, 40));
            var bk = UIKit.Text(barrelBtn.transform, Application.isMobilePlatform ? "" : "SPACE", 18, new Color(1f, 1f, 1f, 0.8f));
            UIKit.Place(bk.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -26), new Vector2(150, 30));

            var hint = UIKit.Text(ht, Application.isMobilePlatform ? "Hold anywhere to shoot" : "Hold the mouse to shoot · SPACE / right-click throws a barrel", 22, new Color(1f, 1f, 1f, 0.8f));
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(900, 30));
            hint.gameObject.AddComponent<FadeAfter>().delay = 8f;

            crosshair = UIKit.Image(root, "Crosshair", Color.white, UIKit.Circle);
            crosshair.rectTransform.sizeDelta = new Vector2(34, 34);
            var dot = UIKit.Image(crosshair.transform, "Dot", new Color(1f, 0.3f, 0.2f), UIKit.Circle);
            dot.rectTransform.sizeDelta = new Vector2(10, 10);
            var ring = crosshair.gameObject.AddComponent<Outline>();
            ring.effectColor = new Color(0, 0, 0, 0.6f);
            crosshair.color = new Color(1f, 1f, 1f, 0.25f);

            bannerText = UIKit.Text(root, "", 84, Color.white);
            UIKit.Place(bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1200, 110));
            bannerSub = UIKit.Text(root, "", 32, new Color(1f, 0.92f, 0.6f));
            UIKit.Place(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 85), new Vector2(1200, 50));

            for (int i = 0; i < 16; i++)
            {
                var t = UIKit.Text(root, "", 40, Color.white);
                t.rectTransform.sizeDelta = new Vector2(600, 60);
                t.gameObject.SetActive(false);
                pops.Add(new Pop { t = t });
            }

            // ---------------- menu
            menu = Panel("Menu", 0.35f);
            var mt = menu.transform;
            var title = UIKit.Text(mt, "ZOMBIE PILE", 120, new Color(0.62f, 0.95f, 0.4f));
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1200, 150));
            var stamp = UIKit.Image(mt, "Stamp", new Color(0.9f, 0.15f, 0.15f), UIKit.Round);
            UIKit.Place(stamp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330, 90), new Vector2(250, 64));
            stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, 8f);
            var st = UIKit.Text(stamp.transform, "NOT FAKE", 42, Color.white);
            UIKit.Stretch(st.rectTransform);
            stamp.gameObject.AddComponent<Pulse>();
            var sub = UIKit.Text(mt, "It's the game from the ads. For real.\nShoot the pile down before it climbs the wall!", 30, Color.white);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1100, 90));
            var play = UIKit.Button(mt, "PLAY", new Color(0.3f, 0.78f, 0.35f), new Vector2(360, 110), () => { SoundBank.I.Play(SoundBank.I.click); Game.I.StartRun(); }, 56);
            UIKit.Place((RectTransform)play.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -95), new Vector2(360, 110));
            play.gameObject.AddComponent<Pulse>();
            bestText = UIKit.Text(mt, "", 28, new Color(1f, 0.85f, 0.35f));
            UIKit.Place(bestText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(800, 40));
            muteBtn = UIKit.Button(mt, Save.Data.muted ? "SOUND OFF" : "SOUND ON", new Color(0.3f, 0.32f, 0.4f), new Vector2(220, 60), () =>
            {
                Save.Data.muted = !Save.Data.muted; Save.Write(); SoundBank.ApplyMute();
                UIKit.SetLabel(muteBtn, Save.Data.muted ? "SOUND OFF" : "SOUND ON");
            }, 26);
            UIKit.Place((RectTransform)muteBtn.transform, new Vector2(1, 0), new Vector2(-24, 24), new Vector2(220, 60));

            // ---------------- upgrades
            upgrades = Panel("Upgrades", 0.55f);
            var ut = UIKit.Text(upgrades.transform, "CHOOSE AN UPGRADE", 56, Color.white);
            UIKit.Place(ut.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1200, 80));
            cardRow = UIKit.Node("Cards", upgrades.transform);
            UIKit.Place(cardRow, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1100, 360));
            upgrades.SetActive(false);

            // ---------------- game over
            over = Panel("Over", 0.6f);
            overTitle = UIKit.Text(over.transform, "OVERRUN!", 110, new Color(1f, 0.35f, 0.3f));
            UIKit.Place(overTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1200, 140));
            overStats = UIKit.Text(over.transform, "", 36, Color.white);
            UIKit.Place(overStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1000, 140));
            var retry = UIKit.Button(over.transform, "TRY AGAIN", new Color(0.3f, 0.78f, 0.35f), new Vector2(380, 110), () => { over.SetActive(false); Game.I.Retry(); }, 50);
            UIKit.Place((RectTransform)retry.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(380, 110));
            retry.gameObject.AddComponent<Pulse>();
            over.SetActive(false);
            hud.SetActive(false);
        }

        GameObject Panel(string name, float alpha)
        {
            var img = UIKit.Image(root, name, new Color(0.05f, 0.04f, 0.08f, alpha), null, true);
            UIKit.Stretch(img.rectTransform);
            return img.gameObject;
        }

        // ------------------------------------------------------------------ API
        public void ShowMenu(int best)
        {
            menu.SetActive(true); hud.SetActive(false);
            bestText.text = best > 0 ? "BEST: WAVE " + best : "";
        }

        public void HideMenu() { menu.SetActive(false); hud.SetActive(true); }
        public void SetWave(int n) { waveText.text = "WAVE " + n; }
        public void SetLeft(int n) { leftText.text = "ZOMBIES " + n; }
        public void SetKills(int n) { killText.text = "KILLS " + n; }
        public void SetWall(float k) { wallTarget = k; }
        public void FlashDamage() { damageT = 1f; }

        public void Banner(string title, string sub)
        {
            bannerText.text = title; bannerSub.text = sub; bannerT = 2.2f;
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
                var card = UIKit.Button(cardRow, "", colors[i], portrait ? new Vector2(620, 150) : new Vector2(330, 330), () =>
                {
                    upgrades.SetActive(false);
                    pick(idx);
                });
                var r = (RectTransform)card.transform;
                if (portrait) UIKit.Place(r, new Vector2(0.5f, 0.5f), new Vector2(0, 170 - i * 170), new Vector2(620, 150));
                else UIKit.Place(r, new Vector2(0.5f, 0.5f), new Vector2((i - (titles.Length - 1) * 0.5f) * 360, 0), new Vector2(330, 330));
                var t = UIKit.Text(card.transform, titles[i], portrait ? 40 : 38, Color.white);
                UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, portrait ? 25 : 60), new Vector2(600, 60));
                var d = UIKit.Text(card.transform, descs[i], 26, new Color(1f, 1f, 1f, 0.92f));
                d.horizontalOverflow = HorizontalWrapMode.Wrap;
                UIKit.Place(d.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, portrait ? -30 : -30), new Vector2(portrait ? 560 : 290, 90));
            }
            upgrades.SetActive(true);
        }

        public void ShowGameOver(int wave, int kills, int best, bool newBest)
        {
            over.SetActive(true);
            overTitle.text = "OVERRUN!";
            overStats.text = "You held until WAVE " + wave + "\n" + kills + " zombies down\n" + (newBest ? "NEW BEST!" : "Best: wave " + best);
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
                crosshair.rectTransform.anchoredPosition = local;
                crosshair.color = Gunner.I.Firing ? new Color(1f, 0.9f, 0.5f, 0.9f) : new Color(1f, 1f, 1f, 0.6f);
                Cursor.visible = false;
            }
            else Cursor.visible = true;

            if (Gunner.I != null)
            {
                barrelFill.fillAmount = 1f - Gunner.I.BarrelReady;
                barrelBtn.transform.localScale = Vector3.one * (Gunner.I.BarrelReady >= 1f ? 1f + Mathf.Sin(Time.time * 6f) * 0.04f : 0.94f);
            }
            wallShown = Mathf.MoveTowards(wallShown, wallTarget, dt * 0.8f);
            wallFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, wallShown), 1f);
            wallFill.color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.35f, 0.85f, 0.4f), wallShown);

            damageT = Mathf.Max(0f, damageT - dt * 2.5f);
            damage.color = new Color(0.9f, 0.1f, 0.05f, damageT * 0.35f);

            bannerT -= dt;
            float ba = Mathf.Clamp01(bannerT / 0.5f);
            bannerText.color = new Color(1f, 1f, 1f, ba);
            bannerSub.color = new Color(1f, 0.92f, 0.6f, ba);
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
