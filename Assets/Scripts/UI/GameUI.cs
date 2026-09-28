using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SkyDrop
{
    public struct HudInfo
    {
        public int coins;
        public float altitude;   // above the pad
        public float margin;     // altitude minus what the chute needs
        public bool freefall, canopy, canOpen;
        public int shields;
        public Vector3 wind;
        public int rings, ringsTotal;
        public int lockedZone;   // multiplier zone locked in when the chute opened
    }

    public class ResultInfo
    {
        public string title, subtitle;
        public Color titleColor;
        public int stars, airCoins, landingBonus, total;
        public float multiplier;
        public bool cleared, newRecord, canDouble, daily, crashed, hasNext;
        public int dailyBonus;
    }

    /// Every screen of the game, built in code: HUD, menu, shop panels and results.
    public class GameUI : MonoBehaviour
    {
        public static GameUI I;

        RectTransform root, hud, menu, modal, results, popupLayer;
        Image flash;

        // HUD
        Text coinText, levelText, multText, multSub, comboText, hintText, altText, windText, ringText;
        Image hintBg, altMarker;
        RectTransform altBar, windArrow, windGroup, multGroup, shieldRow, joyBase, joyKnob, coinPill, openHolder;
        Button openButton;
        Text openLabel, openSub;
        Image openImg;
        float comboTimer, coinPunch;
        int lastZone = -1;
        readonly List<Image> shieldIcons = new List<Image>();

        // Menu
        Text menuTitle, menuSub, menuCoins, bestText;
        GameObject upgradeBadge, dailyBadge, dailyJumpBadge;
        Button muteButton;

        // Results
        Text resTitle, resSub, resRows, resValues, resTotal, resRecord;
        Image[] resStars = new Image[3];
        Button resDouble, resRetry, resNext, resUpgrade;
        ResultInfo current;
        float shownTotal, resTime;

        // Popups
        class PopupItem
        {
            public Text text;
            public float life;
            public Vector2 vel;
        }
        readonly List<PopupItem> popups = new List<PopupItem>();

        public bool ModalOpen { get { return modal.gameObject.activeSelf; } }
        public bool ResultsOpen { get { return results.gameObject.activeSelf; } }

        public static GameUI Create()
        {
            var go = new GameObject("UI");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            var ui = go.AddComponent<GameUI>();
            ui.Build();
            return ui;
        }

        void Build()
        {
            I = this;
            root = (RectTransform)transform;
            hud = UIKit.Stretch(UIKit.Node("HUD", root));
            menu = UIKit.Stretch(UIKit.Node("Menu", root));
            results = UIKit.Stretch(UIKit.Node("Results", root));
            modal = UIKit.Stretch(UIKit.Node("Modal", root));
            popupLayer = UIKit.Stretch(UIKit.Node("Popups", root));
            flash = UIKit.Image(root, "Flash", new Color(1f, 1f, 1f, 0f));
            UIKit.Stretch(flash.rectTransform);
            BuildHud();
            BuildMenu();
            BuildResults();
            modal.gameObject.SetActive(false);
            results.gameObject.SetActive(false);
            hud.gameObject.SetActive(false);
            for (int i = 0; i < 18; i++)
            {
                var t = UIKit.Text(popupLayer, "", 40, Color.white);
                t.rectTransform.sizeDelta = new Vector2(600f, 80f);
                t.gameObject.SetActive(false);
                popups.Add(new PopupItem { text = t });
            }
        }

        // =============================================================================== HUD

        void BuildHud()
        {
            coinPill = CoinPill(hud, new Vector2(0f, 1f), new Vector2(20f, -20f), out coinText);

            shieldRow = UIKit.Place(UIKit.Node("Shields", hud), new Vector2(0f, 1f), new Vector2(26f, -100f), new Vector2(200f, 40f));
            for (int i = 0; i < 3; i++)
            {
                var s = UIKit.Image(shieldRow, "Shield", new Color(0.4f, 1f, 0.85f), UIKit.Circle);
                UIKit.Place(s.rectTransform, new Vector2(0f, 0.5f), new Vector2(i * 44f, 0f), new Vector2(36f, 36f));
                shieldIcons.Add(s);
            }

            levelText = UIKit.Text(hud, "LEVEL 1", 30, Color.white);
            UIKit.Place(levelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(500f, 44f));
            ringText = UIKit.Text(hud, "", 22, new Color(1f, 1f, 1f, 0.85f));
            UIKit.Place(ringText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(400f, 30f));

            multGroup = UIKit.Place(UIKit.Node("Mult", hud), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(600f, 150f));
            multText = UIKit.Text(multGroup, "", 92, Color.white);
            UIKit.Place(multText.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(600f, 100f));
            multSub = UIKit.Text(multGroup, "", 32, Color.white);
            UIKit.Place(multSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(600f, 44f));

            windGroup = UIKit.Place(UIKit.Node("Wind", hud), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(200f, 90f));
            windText = UIKit.Text(windGroup, "WIND", 24, Color.white);
            UIKit.Place(windText.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(200f, 30f));
            windArrow = UIKit.Place(UIKit.Node("Arrow", windGroup), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(60f, 60f));
            windArrow.pivot = new Vector2(0.5f, 0.5f);
            var shaft = UIKit.Image(windArrow, "Shaft", Color.white, UIKit.Round);
            UIKit.Place(shaft.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-6f, 0f), new Vector2(44f, 10f));
            var head = UIKit.Image(windArrow, "Head", Color.white);
            UIKit.Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(20f, 20f));
            head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // Altimeter with the risk bands.
            altBar = UIKit.Place(UIKit.Node("Altimeter", hud), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(36f, 440f));
            float H = 440f;
            for (int i = 0; i < Zones.Max.Length; i++)
            {
                float from = i == 0 ? Zones.BarMin : Zones.Max[i - 1];
                float to = Mathf.Min(Zones.Max[i], Zones.BarMax);
                float y0 = Mathf.InverseLerp(Zones.BarMin, Zones.BarMax, from) * H;
                float y1 = Mathf.InverseLerp(Zones.BarMin, Zones.BarMax, to) * H;
                var band = UIKit.Image(altBar, "Band", Zones.Col[i]);
                band.rectTransform.anchorMin = new Vector2(0f, 0f);
                band.rectTransform.anchorMax = new Vector2(1f, 0f);
                band.rectTransform.pivot = new Vector2(0.5f, 0f);
                band.rectTransform.anchoredPosition = new Vector2(0f, y0);
                band.rectTransform.sizeDelta = new Vector2(0f, y1 - y0);
                var lbl = UIKit.Text(altBar, Zones.MultText(Zones.Mult[i]), 20, Zones.Col[i], TextAnchor.MiddleRight);
                lbl.rectTransform.anchorMin = lbl.rectTransform.anchorMax = new Vector2(0f, 0f);
                lbl.rectTransform.pivot = new Vector2(1f, 0.5f);
                lbl.rectTransform.anchoredPosition = new Vector2(-6f, (y0 + y1) * 0.5f);
                lbl.rectTransform.sizeDelta = new Vector2(70f, 24f);
            }
            float ceilY = Mathf.InverseLerp(Zones.BarMin, Zones.BarMax, Zones.Ceiling) * H;
            var ceil = UIKit.Image(altBar, "Ceiling", Color.white);
            ceil.rectTransform.anchorMin = ceil.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            ceil.rectTransform.anchoredPosition = new Vector2(0f, ceilY);
            ceil.rectTransform.sizeDelta = new Vector2(50f, 3f);
            altMarker = UIKit.Image(altBar, "Marker", Color.white, UIKit.Round);
            altMarker.rectTransform.anchorMin = altMarker.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            altMarker.rectTransform.sizeDelta = new Vector2(64f, 12f);
            altText = UIKit.Text(altMarker.transform, "", 26, Color.white, TextAnchor.MiddleRight);
            altText.rectTransform.anchorMin = altText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            altText.rectTransform.pivot = new Vector2(1f, 0.5f);
            altText.rectTransform.anchoredPosition = new Vector2(-50f, 0f);
            altText.rectTransform.sizeDelta = new Vector2(140f, 30f);

            comboText = UIKit.Text(hud, "", 46, UIKit.Gold);
            UIKit.Place(comboText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(600f, 60f));

            hintBg = UIKit.Image(hud, "HintBg", UIKit.Dark, UIKit.Round);
            UIKit.Place(hintBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(700f, 70f));
            hintText = UIKit.Text(hintBg.transform, "", 32, Color.white);
            UIKit.Stretch(hintText.rectTransform);
            hintBg.gameObject.SetActive(false);

            openHolder = UIKit.Place(UIKit.Node("OpenHolder", hud), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(320f, 130f));
            openButton = UIKit.Button(openHolder, "", UIKit.Gray, new Vector2(320f, 130f), () => InputReader.QueueOpen());
            UIKit.Place((RectTransform)openButton.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 130f));
            openImg = openButton.GetComponent<Image>();
            openLabel = UIKit.Text(openButton.transform, "OPEN", 56, Color.white);
            UIKit.Place(openLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(320f, 70f));
            openSub = UIKit.Text(openButton.transform, Application.isMobilePlatform ? "PARACHUTE" : "PARACHUTE  [SPACE]", 20, Color.white);
            UIKit.Place(openSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(320f, 30f));
            openHolder.gameObject.AddComponent<Pulse>().enabled = false;

            joyBase = UIKit.Image(hud, "JoyBase", new Color(1f, 1f, 1f, 0.12f), UIKit.Circle).rectTransform;
            joyBase.sizeDelta = new Vector2(190f, 190f);
            joyKnob = UIKit.Image(hud, "JoyKnob", new Color(1f, 1f, 1f, 0.35f), UIKit.Circle).rectTransform;
            joyKnob.sizeDelta = new Vector2(84f, 84f);
            joyBase.anchorMin = joyBase.anchorMax = joyKnob.anchorMin = joyKnob.anchorMax = Vector2.zero;
        }

        RectTransform CoinPill(Transform parent, Vector2 anchor, Vector2 pos, out Text text)
        {
            var pill = UIKit.Image(parent, "CoinPill", UIKit.Dark, UIKit.Round);
            UIKit.Place(pill.rectTransform, anchor, pos, new Vector2(230f, 66f));
            var icon = UIKit.Image(pill.transform, "Coin", UIKit.Gold, UIKit.Circle);
            UIKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(46f, 46f));
            var inner = UIKit.Image(icon.transform, "Inner", new Color(1f, 0.93f, 0.5f), UIKit.Circle);
            UIKit.Place(inner.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            text = UIKit.Text(pill.transform, "0", 36, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(150f, 50f));
            return pill.rectTransform;
        }

        public void ShowHud(bool on, LevelConfig cfg)
        {
            hud.gameObject.SetActive(on);
            if (on && cfg != null)
            {
                levelText.text = cfg.Title + "  -  " + cfg.Theme.name.ToUpper();
                lastZone = -1;
                comboText.text = "";
                SetHint(null);
            }
        }

        public void UpdateHud(HudInfo h)
        {
            float dt = Time.unscaledDeltaTime;
            coinText.text = h.coins.ToString();
            coinPunch = Mathf.MoveTowards(coinPunch, 0f, dt * 4f);
            coinPill.localScale = Vector3.one * (1f + coinPunch * 0.25f);
            ringText.text = h.ringsTotal > 0 && h.freefall ? "RINGS " + h.rings + "/" + h.ringsTotal : "";

            for (int i = 0; i < shieldIcons.Count; i++) shieldIcons[i].gameObject.SetActive(i < h.shields);

            altBar.gameObject.SetActive(h.freefall);
            openHolder.gameObject.SetActive(h.freefall);
            multGroup.gameObject.SetActive(h.freefall || h.canopy);
            if (h.canopy)
            {
                int lz = Mathf.Clamp(h.lockedZone, 0, Zones.Max.Length - 1);
                multText.text = Zones.MultText(Zones.Mult[lz]);
                multText.color = Zones.Col[lz];
                multSub.text = "LAND ON THE TARGET TO CASH IN!";
                multSub.color = Color.white;
                multGroup.localScale = Vector3.Lerp(multGroup.localScale, Vector3.one * 0.75f, 1f - Mathf.Exp(-dt * 6f));
            }

            if (h.freefall)
            {
                float y = Mathf.InverseLerp(Zones.BarMin, Zones.BarMax, h.margin) * 440f;
                altMarker.rectTransform.anchoredPosition = new Vector2(0f, y);
                altText.text = Mathf.Max(0, Mathf.RoundToInt(h.altitude)) + "m";
                int z = Zones.Of(h.margin);
                if (h.canOpen)
                {
                    multText.text = Zones.MultText(Zones.Mult[z]);
                    multText.color = Zones.Col[z];
                    multSub.text = Zones.Name[z] + (z <= 2 ? "!" : "");
                    multSub.color = Zones.Col[z];
                    if (z != lastZone)
                    {
                        multGroup.localScale = Vector3.one * 1.35f;
                        if (lastZone >= 0 && z < lastZone && SoundBank.I != null)
                            SoundBank.I.Play(SoundBank.I.beep, 0.6f, 1f + (6 - z) * 0.12f);
                        lastZone = z;
                    }
                    openImg.color = Zones.Col[z];
                    openLabel.text = "OPEN";
                    openSub.text = Application.isMobilePlatform ? "PARACHUTE" : "PARACHUTE  [SPACE]";
                    openHolder.GetComponent<Pulse>().enabled = z <= 3;
                }
                else
                {
                    multText.text = Mathf.RoundToInt(h.altitude) + "m";
                    multText.color = Color.white;
                    multSub.text = "Open lower = bigger bonus";
                    multSub.color = new Color(1f, 1f, 1f, 0.8f);
                    openImg.color = UIKit.Gray;
                    openLabel.text = "WAIT";
                    openSub.text = "TOO HIGH TO OPEN";
                    openHolder.GetComponent<Pulse>().enabled = false;
                    lastZone = -1;
                }
                multGroup.localScale = Vector3.Lerp(multGroup.localScale, Vector3.one, 1f - Mathf.Exp(-dt * 10f));
            }

            bool showWind = h.wind.sqrMagnitude > 0.25f && (h.canopy || (h.freefall && h.canOpen));
            windGroup.gameObject.SetActive(showWind);
            if (showWind)
            {
                // Camera always looks along +Z, so world X/Z map to screen right/up.
                float ang = Mathf.Atan2(h.wind.z, h.wind.x) * Mathf.Rad2Deg;
                windArrow.localRotation = Quaternion.Euler(0f, 0f, ang);
                windText.text = "WIND " + h.wind.magnitude.ToString("0") + " m/s";
            }

            comboTimer -= dt;
            if (comboTimer <= 0f) comboText.text = "";
            comboText.transform.localScale = Vector3.Lerp(comboText.transform.localScale, Vector3.one, 1f - Mathf.Exp(-dt * 10f));

            bool joy = InputReader.Dragging;
            joyBase.gameObject.SetActive(joy);
            joyKnob.gameObject.SetActive(joy);
            if (joy)
            {
                float s = root.rect.width / Mathf.Max(1f, Screen.width);
                joyBase.anchoredPosition = InputReader.DragOrigin * s;
                Vector2 d = Vector2.ClampMagnitude(InputReader.DragPos - InputReader.DragOrigin, InputReader.JoystickRadius);
                joyKnob.anchoredPosition = (InputReader.DragOrigin + d) * s;
            }
        }

        public void CoinPunch() { coinPunch = 1f; }

        public void Combo(int combo, bool perfect)
        {
            if (combo < 2 && !perfect) return;
            comboText.text = (perfect ? "PERFECT!  " : "") + (combo >= 2 ? "COMBO x" + combo : "");
            comboText.color = combo >= 5 ? new Color(1f, 0.45f, 0.2f) : UIKit.Gold;
            comboText.transform.localScale = Vector3.one * 1.5f;
            comboTimer = 1.4f;
        }

        public void SetHint(string text)
        {
            bool on = !string.IsNullOrEmpty(text);
            hintBg.gameObject.SetActive(on);
            if (!on) return;
            hintText.text = text;
            hintBg.rectTransform.sizeDelta = new Vector2(hintText.preferredWidth + 70f, 74f);
        }

        public void SetOpenAttention(bool on)
        {
            var p = openHolder.GetComponent<Pulse>();
            p.amount = on ? 0.12f : 0.06f;
            if (on) p.enabled = true;
        }

        // =============================================================================== popups / flash

        public void Popup(Vector3 world, string text, Color c, int size = 40)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupLayer, sp, null, out local);
            SpawnPopup(local, text, c, size);
        }

        public void ScreenPopup(string text, Color c, int size, float y)
        {
            SpawnPopup(new Vector2(0f, y), text, c, size);
        }

        void SpawnPopup(Vector2 local, string text, Color c, int size)
        {
            PopupItem best = popups[0];
            foreach (var p in popups)
                if (p.life <= 0f) { best = p; break; }
                else if (p.life < best.life) best = p;
            best.text.gameObject.SetActive(true);
            best.text.text = text;
            best.text.color = c;
            best.text.fontSize = size;
            best.text.rectTransform.anchorMin = best.text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            best.text.rectTransform.anchoredPosition = local;
            best.text.transform.localScale = Vector3.one * 1.6f;
            best.life = 1.1f;
            best.vel = new Vector2(Random.Range(-20f, 20f), 110f);
        }

        public void Flash(Color c, float alpha = 0.6f)
        {
            c.a = alpha;
            flash.color = c;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var p in popups)
            {
                if (p.life <= 0f) continue;
                p.life -= dt;
                p.text.rectTransform.anchoredPosition += p.vel * dt;
                p.text.transform.localScale = Vector3.Lerp(p.text.transform.localScale, Vector3.one, 1f - Mathf.Exp(-dt * 12f));
                var c = p.text.color;
                c.a = Mathf.Clamp01(p.life * 3f);
                p.text.color = c;
                if (p.life <= 0f) p.text.gameObject.SetActive(false);
            }
            var fc = flash.color;
            if (fc.a > 0f)
            {
                fc.a = Mathf.MoveTowards(fc.a, 0f, dt * 2.5f);
                flash.color = fc;
            }
            if (results.gameObject.activeSelf) AnimateResults(dt);
        }

        // =============================================================================== menu

        void BuildMenu()
        {
            var tap = UIKit.Image(menu, "TapArea", new Color(0f, 0f, 0f, 0f), null, true);
            UIKit.Stretch(tap.rectTransform);
            tap.gameObject.AddComponent<Button>().onClick.AddListener(() => GameManager.I.OnTapJump());

            menuTitle = UIKit.Text(menu, "SKY DROP", 104, UIKit.Gold);
            UIKit.Place(menuTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 120f));
            menuTitle.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);
            menuSub = UIKit.Text(menu, "", 36, Color.white);
            UIKit.Place(menuSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 50f));

            var tapHolder = UIKit.Place(UIKit.Node("TapHolder", menu), new Vector2(0.5f, 0f), new Vector2(0f, 175f), new Vector2(600f, 80f));
            var tapText = UIKit.Text(tapHolder, Application.isMobilePlatform ? "TAP TO JUMP!" : "CLICK / SPACE TO JUMP!", 54, Color.white);
            UIKit.Stretch(tapText.rectTransform);
            tapHolder.gameObject.AddComponent<Pulse>();

            var bar = UIKit.Place(UIKit.Node("Buttons", menu), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1100f, 110f));
            string[] labels = { "UPGRADES", "SKINS", "LEVELS", "DAILY GIFT", "DAILY JUMP" };
            Color[] colors = { UIKit.Orange, UIKit.Purple, UIKit.Blue, UIKit.Green, new Color(0.95f, 0.3f, 0.45f) };
            UnityEngine.Events.UnityAction[] actions =
            {
                OpenUpgrades, OpenSkins, OpenLevels, OpenDaily, () => GameManager.I.StartDailyJump()
            };
            for (int i = 0; i < labels.Length; i++)
            {
                var b = UIKit.Button(bar, labels[i], colors[i], new Vector2(205f, 100f), actions[i], 28);
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2((i - 2) * 218f, 0f), new Vector2(205f, 100f));
                var badge = Badge(b.transform);
                if (i == 0) upgradeBadge = badge;
                else if (i == 3) dailyBadge = badge;
                else if (i == 4) dailyJumpBadge = badge;
                else badge.SetActive(false);
            }

            Text menuCoinText;
            CoinPill(menu, new Vector2(1f, 1f), new Vector2(-20f, -20f), out menuCoinText);
            menuCoins = menuCoinText;
            muteButton = UIKit.Button(menu, "SOUND", UIKit.Dark, new Vector2(150f, 66f), ToggleMute, 24);
            UIKit.Place((RectTransform)muteButton.transform, new Vector2(1f, 1f), new Vector2(-265f, -20f), new Vector2(150f, 66f));
            bestText = UIKit.Text(menu, "", 26, Color.white, TextAnchor.MiddleLeft);
            UIKit.Place(bestText.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(400f, 60f));
        }

        GameObject Badge(Transform parent)
        {
            var b = UIKit.Image(parent, "Badge", new Color(1f, 0.2f, 0.25f), UIKit.Circle);
            UIKit.Place(b.rectTransform, new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(40f, 40f));
            var t = UIKit.Text(b.transform, "!", 30, Color.white);
            UIKit.Stretch(t.rectTransform);
            b.gameObject.AddComponent<Pulse>().amount = 0.12f;
            return b.gameObject;
        }

        public void ShowMenu(bool on, LevelConfig cfg)
        {
            menu.gameObject.SetActive(on);
            if (!on) return;
            if (cfg != null) menuSub.text = cfg.Title + "  -  " + cfg.Theme.name.ToUpper();
            RefreshMenu();
        }

        public void RefreshMenu()
        {
            var d = SaveSystem.Data;
            menuCoins.text = d.coins.ToString();
            upgradeBadge.SetActive(Economy.AnyAffordable());
            dailyBadge.SetActive(Economy.DailyAvailable);
            dailyJumpBadge.SetActive(Economy.DailyJumpAvailable);
            UIKit.SetLabel(muteButton, d.muted ? "MUTED" : "SOUND");
            bestText.text = d.bestMultiplier > 0f ? "BEST RISK: " + Zones.MultText(d.bestMultiplier) + "\nPERFECT LANDINGS: " + d.perfectLandings : "";
        }

        void ToggleMute()
        {
            SaveSystem.Data.muted = !SaveSystem.Data.muted;
            SaveSystem.Save();
            SoundBank.ApplyMute();
            RefreshMenu();
        }

        // =============================================================================== modal panels

        RectTransform OpenModal(string title, Vector2 size)
        {
            for (int i = modal.childCount - 1; i >= 0; i--) Destroy(modal.GetChild(i).gameObject);
            modal.gameObject.SetActive(true);
            var dim = UIKit.Image(modal, "Dim", new Color(0f, 0f, 0f, 0.6f), null, true);
            UIKit.Stretch(dim.rectTransform);
            var panel = UIKit.Image(modal, "Panel", new Color(0.12f, 0.13f, 0.22f, 0.97f), UIKit.Round, true);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var t = UIKit.Text(panel.transform, title, 50, UIKit.Gold);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(size.x, 60f));
            var close = UIKit.Button(panel.transform, "X", new Color(0.9f, 0.25f, 0.3f), new Vector2(70f, 70f), CloseModal, 36);
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(70f, 70f));
            return panel.rectTransform;
        }

        public void CloseModal()
        {
            modal.gameObject.SetActive(false);
            if (menu.gameObject.activeSelf) RefreshMenu();
            if (results.gameObject.activeSelf) RefreshResultButtons();
        }

        public void OpenUpgrades()
        {
            var p = OpenModal("UPGRADES", new Vector2(1060f, 640f));
            Text coins;
            CoinPill(p, new Vector2(0f, 1f), new Vector2(20f, -16f), out coins);
            coins.text = SaveSystem.Data.coins.ToString();
            for (int i = 0; i < Economy.UpgradeCount; i++)
            {
                var id = (UpgradeId)i;
                var def = Economy.Upgrades[i];
                float y = 175f - i * 100f;
                var row = UIKit.Image(p, "Row", new Color(1f, 1f, 1f, 0.06f), UIKit.Round);
                UIKit.Place(row.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, y - 40f), new Vector2(1000f, 88f));
                var icon = UIKit.Image(row.transform, "Icon", def.color, UIKit.Round);
                UIKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(66f, 66f));
                var lvl = UIKit.Text(icon.transform, Economy.Level(id).ToString(), 34, Color.white);
                UIKit.Stretch(lvl.rectTransform);
                var name = UIKit.Text(row.transform, def.name, 32, Color.white, TextAnchor.UpperLeft);
                UIKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(95f, 22f), new Vector2(330f, 40f));
                var desc = UIKit.Text(row.transform, def.desc, 18, new Color(0.8f, 0.85f, 1f), TextAnchor.UpperLeft, false);
                UIKit.Place(desc.rectTransform, new Vector2(0f, 0.5f), new Vector2(95f, -14f), new Vector2(330f, 40f));
                for (int k = 0; k < def.maxLevel; k++)
                {
                    var pip = UIKit.Image(row.transform, "Pip", k < Economy.Level(id) ? def.color : new Color(1f, 1f, 1f, 0.15f), UIKit.Round);
                    UIKit.Place(pip.rectTransform, new Vector2(0f, 0.5f), new Vector2(450f + k * 30f * (10f / def.maxLevel), 0f),
                        new Vector2(def.maxLevel <= 3 ? 80f : 24f, 30f));
                }
                bool maxed = Economy.IsMaxed(id);
                bool afford = Economy.CanAfford(id);
                var buy = UIKit.Button(row.transform, maxed ? "MAX" : "BUY  " + Economy.Cost(id), afford ? UIKit.Green : UIKit.Gray,
                    new Vector2(210f, 70f), () =>
                    {
                        if (Economy.Buy(id))
                        {
                            SoundBank.I.Play(SoundBank.I.buy);
                            OpenUpgrades();
                        }
                        else SoundBank.I.Play(SoundBank.I.fail, 0.6f);
                    }, 28);
                UIKit.Place((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(210f, 70f));
                buy.interactable = !maxed;
            }
        }

        public void OpenSkins()
        {
            var p = OpenModal("SKINS", new Vector2(1000f, 640f));
            Text coins;
            CoinPill(p, new Vector2(0f, 1f), new Vector2(20f, -16f), out coins);
            coins.text = SaveSystem.Data.coins.ToString();
            var d = SaveSystem.Data;
            for (int i = 0; i < Economy.SkinCount; i++)
            {
                int idx = i;
                var s = Economy.Skins[i];
                float x = (i % 4 - 1.5f) * 235f;
                float y = i < 4 ? 95f : -170f;
                var card = UIKit.Image(p, "Card", new Color(1f, 1f, 1f, d.skin == i ? 0.2f : 0.07f), UIKit.Round);
                UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(220f, 245f));
                // preview: canopy stripes over a suit block
                for (int k = 0; k < 5; k++)
                {
                    var stripe = UIKit.Image(card.transform, "Canopy", k % 2 == 0 ? s.canopyA : s.canopyB);
                    UIKit.Place(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2((k - 2) * 28f, -18f - Mathf.Abs(k - 2) * 6f), new Vector2(28f, 26f));
                }
                var body = UIKit.Image(card.transform, "Suit", s.suit, UIKit.Round);
                UIKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(50f, 60f));
                var helm = UIKit.Image(card.transform, "Helmet", s.helmet, UIKit.Circle);
                UIKit.Place(helm.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(34f, 34f));
                var stripe2 = UIKit.Image(card.transform, "Suit2", s.suit2);
                UIKit.Place(stripe2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(50f, 10f));
                var name = UIKit.Text(card.transform, s.name, 26, Color.white);
                UIKit.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(220f, 34f));
                bool owned = d.ownedSkins[i];
                string label = d.skin == i ? "EQUIPPED" : owned ? "EQUIP" : s.cost.ToString();
                Color col = d.skin == i ? UIKit.Gray : owned ? UIKit.Blue : (d.coins >= s.cost ? UIKit.Green : UIKit.Gray);
                var b = UIKit.Button(card.transform, label, col, new Vector2(190f, 58f), () =>
                {
                    var data = SaveSystem.Data;
                    if (!data.ownedSkins[idx])
                    {
                        if (data.coins < Economy.Skins[idx].cost)
                        {
                            SoundBank.I.Play(SoundBank.I.fail, 0.6f);
                            return;
                        }
                        data.coins -= Economy.Skins[idx].cost;
                        data.ownedSkins[idx] = true;
                        SoundBank.I.Play(SoundBank.I.buy);
                    }
                    data.skin = idx;
                    SaveSystem.Save();
                    GameManager.I.RefreshJumperSkin();
                    OpenSkins();
                }, 26);
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(190f, 58f));
            }
        }

        int levelsPage = -1;

        public void OpenLevels()
        {
            if (levelsPage < 0) levelsPage = SaveSystem.Data.current / Levels.PerWorld;
            levelsPage = Mathf.Clamp(levelsPage, 0, Levels.Worlds - 1);
            var th = Levels.Themes[levelsPage];
            var p = OpenModal(th.name.ToUpper(), new Vector2(960f, 600f));
            var sub = UIKit.Text(p, "WORLD " + (levelsPage + 1) + "   -   STARS " + Levels.WorldStars(levelsPage) + "/" + (Levels.PerWorld * 3), 26, Color.white);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(800f, 36f));
            var prev = UIKit.Button(p, "<", UIKit.Blue, new Vector2(80f, 100f), () => { levelsPage--; OpenLevels(); }, 50);
            UIKit.Place((RectTransform)prev.transform, new Vector2(0f, 0.5f), new Vector2(16f, -20f), new Vector2(80f, 100f));
            prev.interactable = levelsPage > 0;
            var next = UIKit.Button(p, ">", UIKit.Blue, new Vector2(80f, 100f), () => { levelsPage++; OpenLevels(); }, 50);
            UIKit.Place((RectTransform)next.transform, new Vector2(1f, 0.5f), new Vector2(-16f, -20f), new Vector2(80f, 100f));
            next.interactable = levelsPage < Levels.Worlds - 1;

            for (int i = 0; i < Levels.PerWorld; i++)
            {
                int idx = levelsPage * Levels.PerWorld + i;
                bool unlocked = Levels.IsUnlocked(idx);
                float x = (i % 5 - 2f) * 150f;
                float y = i < 5 ? 60f : -110f;
                var b = UIKit.Button(p, (idx + 1).ToString(), unlocked ? (idx == SaveSystem.Data.current ? UIKit.Orange : UIKit.Blue) : new Color(0.25f, 0.27f, 0.35f),
                    new Vector2(130f, 140f), () =>
                    {
                        if (!Levels.IsUnlocked(idx)) { SoundBank.I.Play(SoundBank.I.fail, 0.6f); return; }
                        CloseModal();
                        GameManager.I.SelectLevel(idx);
                    }, 50);
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(130f, 140f));
                var label = b.GetComponentInChildren<Text>();
                label.rectTransform.offsetMin = new Vector2(0f, 30f);
                if (!unlocked) label.color = new Color(1f, 1f, 1f, 0.3f);
                int stars = SaveSystem.Data.stars[idx];
                for (int s = 0; s < 3; s++)
                {
                    var st = UIKit.Image(b.transform, "Star", s < stars ? UIKit.Gold : new Color(0f, 0f, 0f, 0.3f), UIKit.Star);
                    UIKit.Place(st.rectTransform, new Vector2(0.5f, 0f), new Vector2((s - 1) * 36f, 14f), new Vector2(34f, 34f));
                }
            }
        }

        public void OpenDaily()
        {
            var p = OpenModal("DAILY GIFT", new Vector2(1060f, 560f));
            int today = Economy.DailyIndex;
            bool available = Economy.DailyAvailable;
            int claimedUpTo = available ? today : SaveSystem.Data.dailyStreak;
            for (int i = 0; i < Economy.DailyRewards.Length; i++)
            {
                bool claimed = i < claimedUpTo;
                bool isToday = available && i == today;
                var tile = UIKit.Image(p, "Day", isToday ? UIKit.Gold : claimed ? UIKit.Green : new Color(1f, 1f, 1f, 0.08f), UIKit.Round);
                UIKit.Place(tile.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 3f) * 142f, 60f), new Vector2(130f, 170f));
                var dt = UIKit.Text(tile.transform, "DAY " + (i + 1), 24, Color.white);
                UIKit.Place(dt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(130f, 30f));
                var coin = UIKit.Image(tile.transform, "Coin", UIKit.Gold, UIKit.Circle);
                UIKit.Place(coin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(i == 6 ? 64f : 48f, i == 6 ? 64f : 48f));
                var amt = UIKit.Text(tile.transform, claimed ? "OK" : "+" + Economy.DailyRewards[i], 28, Color.white);
                UIKit.Place(amt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(130f, 34f));
                if (isToday) tile.gameObject.AddComponent<Pulse>().amount = 0.05f;
            }
            if (available)
            {
                var claim = UIKit.Button(p, "CLAIM!", UIKit.Green, new Vector2(320f, 96f), () =>
                {
                    int amount = Economy.ClaimDaily();
                    SoundBank.I.Play(SoundBank.I.perfect);
                    ScreenPopup("+" + amount + " COINS", UIKit.Gold, 64, 0f);
                    OpenDaily();
                }, 44);
                UIKit.Place((RectTransform)claim.transform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(320f, 96f));
            }
            else
            {
                var t = UIKit.Text(p, "Come back tomorrow for DAY " + (SaveSystem.Data.dailyStreak % Economy.DailyRewards.Length + 1) + "!\nMiss a day and the streak resets.", 30, Color.white);
                UIKit.Place(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(900f, 90f));
            }
        }

        // =============================================================================== results

        void BuildResults()
        {
            var dim = UIKit.Image(results, "Dim", new Color(0f, 0f, 0f, 0.45f), null, true);
            UIKit.Stretch(dim.rectTransform);
            var p = UIKit.Image(results, "Panel", new Color(0.12f, 0.13f, 0.22f, 0.96f), UIKit.Round, true).rectTransform;
            UIKit.Place(p, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 640f));

            resTitle = UIKit.Text(p, "", 72, Color.white);
            UIKit.Place(resTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(860f, 84f));
            resSub = UIKit.Text(p, "", 28, new Color(0.85f, 0.9f, 1f));
            UIKit.Place(resSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(860f, 36f));
            for (int i = 0; i < 3; i++)
            {
                resStars[i] = UIKit.Image(p, "Star", Color.white, UIKit.Star);
                UIKit.Place(resStars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 110f, i == 1 ? -150f : -165f), new Vector2(i == 1 ? 110f : 90f, i == 1 ? 110f : 90f));
            }
            resRows = UIKit.Text(p, "", 30, Color.white, TextAnchor.UpperLeft, false);
            UIKit.Place(resRows.rectTransform, new Vector2(0.5f, 1f), new Vector2(-120f, -285f), new Vector2(420f, 150f));
            resValues = UIKit.Text(p, "", 30, Color.white, TextAnchor.UpperRight, false);
            UIKit.Place(resValues.rectTransform, new Vector2(0.5f, 1f), new Vector2(150f, -285f), new Vector2(240f, 150f));
            resRows.lineSpacing = 1.15f;
            resValues.lineSpacing = 1.15f;
            resTotal = UIKit.Text(p, "", 58, UIKit.Gold);
            UIKit.Place(resTotal.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(800f, 70f));
            resRecord = UIKit.Text(p, "", 28, new Color(1f, 0.45f, 0.6f));
            UIKit.Place(resRecord.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -268f), new Vector2(800f, 30f));

            resDouble = UIKit.Button(p, "x2 COINS  (AD)", UIKit.Purple, new Vector2(270f, 96f), () => GameManager.I.OnDoubleReward(), 28);
            UIKit.Place((RectTransform)resDouble.transform, new Vector2(0.5f, 0f), new Vector2(-280f, 30f), new Vector2(270f, 96f));
            resRetry = UIKit.Button(p, "RETRY", UIKit.Blue, new Vector2(240f, 96f), () => GameManager.I.OnRetry(), 36);
            UIKit.Place((RectTransform)resRetry.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(240f, 96f));
            resNext = UIKit.Button(p, "NEXT", UIKit.Green, new Vector2(270f, 96f), () => GameManager.I.OnNext(), 40);
            UIKit.Place((RectTransform)resNext.transform, new Vector2(0.5f, 0f), new Vector2(280f, 30f), new Vector2(270f, 96f));
            resUpgrade = UIKit.Button(p, "UPGRADE!", UIKit.Orange, new Vector2(190f, 64f), OpenUpgrades, 26);
            UIKit.Place((RectTransform)resUpgrade.transform, new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(190f, 64f));
            Badge(resUpgrade.transform);
            var menuBtn = UIKit.Button(p, "MENU", UIKit.Gray, new Vector2(150f, 64f), () => GameManager.I.OnMenu(), 26);
            UIKit.Place((RectTransform)menuBtn.transform, new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(150f, 64f));
        }

        public void ShowResults(ResultInfo r)
        {
            current = r;
            results.gameObject.SetActive(true);
            hud.gameObject.SetActive(false);
            resTitle.text = r.title;
            resTitle.color = r.titleColor;
            resSub.text = r.subtitle;
            string rows = "Coins collected\n";
            string vals = r.airCoins + "\n";
            if (!r.crashed && r.landingBonus > 0) { rows += "Landing bonus\n"; vals += "+" + r.landingBonus + "\n"; }
            if (r.multiplier > 1f && r.cleared) { rows += "Risk multiplier\n"; vals += Zones.MultText(r.multiplier) + "\n"; }
            if (r.daily) { rows += "Daily jump bonus\n"; vals += "x2\n"; }
            if (r.dailyBonus > 0) { rows += "First daily clear\n"; vals += "+" + r.dailyBonus + "\n"; }
            resRows.text = rows;
            resValues.text = vals;
            resRecord.text = r.newRecord ? "NEW RISK RECORD!" : "";
            shownTotal = 0f;
            resTime = 0f;
            for (int i = 0; i < 3; i++)
            {
                resStars[i].color = new Color(0f, 0f, 0f, 0.35f);
                resStars[i].transform.localScale = Vector3.one;
            }
            RefreshResultButtons();
        }

        public void UpdateResultTotal(int total, bool doubled)
        {
            current.total = total;
            current.canDouble = !doubled && current.canDouble;
            if (doubled) resRows.text += "Bonus ad reward\n";
            if (doubled) resValues.text += "x2\n";
            RefreshResultButtons();
        }

        void RefreshResultButtons()
        {
            if (current == null) return;
            resDouble.gameObject.SetActive(current.canDouble && current.total > 0);
            resNext.gameObject.SetActive(current.hasNext);
            UIKit.SetLabel(resRetry, current.cleared ? "REPLAY" : "RETRY");
            ((RectTransform)resRetry.transform).anchoredPosition = new Vector2(current.hasNext ? 0f : 140f, 30f);
            if (!resDouble.gameObject.activeSelf && !resNext.gameObject.activeSelf)
                ((RectTransform)resRetry.transform).anchoredPosition = new Vector2(0f, 30f);
            resUpgrade.gameObject.SetActive(Economy.AnyAffordable());
        }

        public void HideResults() { results.gameObject.SetActive(false); }

        void AnimateResults(float dt)
        {
            resTime += dt;
            for (int i = 0; i < 3; i++)
            {
                float t = resTime - 0.35f - i * 0.25f;
                if (i < current.stars && t > 0f)
                {
                    if (resStars[i].color != UIKit.Gold)
                    {
                        resStars[i].color = UIKit.Gold;
                        resStars[i].transform.localScale = Vector3.one * 1.8f;
                        SoundBank.I.Play(SoundBank.I.coin, 0.9f, 1f + i * 0.2f);
                    }
                    resStars[i].transform.localScale = Vector3.Lerp(resStars[i].transform.localScale, Vector3.one, 1f - Mathf.Exp(-dt * 10f));
                }
            }
            if (resTime > 0.4f) shownTotal = Mathf.MoveTowards(shownTotal, current.total, Mathf.Max(current.total, 30) * dt * 1.3f);
            resTotal.text = "+" + Mathf.RoundToInt(shownTotal) + " COINS";
        }
    }
}
