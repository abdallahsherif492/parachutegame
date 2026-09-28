using System;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiePile
{
    /// Full-screen pages (Tools/ui-design/mock2.html): menu, armory (upgrades with levels, weapons),
    /// level complete, overrun, endless results and the NEW ZOMBIE card. Each page is rebuilt when shown.
    public class Screens : MonoBehaviour
    {
        public static Screens I;
        RectTransform root;
        GameObject page;
        Text coinText;
        int armoryTab;

        static readonly Vector2 BL = new Vector2(0f, 0f), BR = new Vector2(1f, 0f), C = new Vector2(0.5f, 0.5f), TL = new Vector2(0f, 1f);
        static readonly Color Orange = new Color(0.91f, 0.45f, 0.17f), Blue = new Color(0.18f, 0.53f, 0.91f), Green = new Color(0.31f, 0.6f, 0.07f);

        public static void Build()
        {
            var go = new GameObject("Screens");
            I = go.AddComponent<Screens>();
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            I.root = (RectTransform)go.transform;
        }

        void Update()
        {
            if (coinText != null && Save.Data != null) coinText.text = Save.Data.coins.ToString("N0");
        }

        public void HideAll()
        {
            if (page != null) Destroy(page);
            page = null;
            coinText = null;
        }

        Transform NewPage(string name, Color tint)
        {
            HideAll();
            var img = UIKit.Image(root, name, tint, null, true);
            UIKit.Stretch(img.rectTransform);
            page = img.gameObject;
            return page.transform;
        }

        static Text L(Transform p, string s, bool bangers, int size, Color c, float x, float y, float w, TextAnchor a = TextAnchor.MiddleCenter, bool stroke = true)
        { return Hud.Label(p, s, bangers, size, c, C, x, y, w, a, stroke); }

        static Image P(Transform p, string sprite, float x, float y, float w, float h, Color? c = null)
        { return Hud.Pic(p, sprite, c ?? Color.white, C, x, y, w, h); }

        static Button B(Transform p, string sprite, string label, int size, float x, float y, float w, float h, Action onClick, Vector2? anchor = null)
        {
            var b = UIKit.SpriteButton(p, sprite, label, size, () => { SoundBank.I.Play(SoundBank.I.click); if (onClick != null) onClick(); });
            UIKit.At((RectTransform)b.transform, anchor ?? C, x, y, w, h);
            return b;
        }

        static void Ribbon(Transform p, float x, float y, float w, string label, int size)
        {
            P(p, "ribbon", x, y, w, 100);
            L(p, label, true, size, Color.white, x, y + 10 + (62 - size) * 0.5f, w);
        }

        void Coins(Transform p, float y = 14)
        {
            RectTransform panel, icon;
            coinText = Hud.CoinPanel(p, 1084, y, out panel, out icon);
        }

        static Transform Group(Transform parent, string name)
        {
            var r = UIKit.Node(name, parent);
            UIKit.Stretch(r);
            return r;
        }

        // ------------------------------------------------------------------ menu
        public void Menu()
        {
            var p = NewPage("Menu", new Color(0.04f, 0.02f, 0.06f, 0.4f));
            var f = UIKit.Image(p, "Fade", new Color(0.04f, 0.02f, 0.06f, 0.85f), UIKit.Fade);
            UIKit.Stretch(f.rectTransform); f.rectTransform.anchorMax = new Vector2(1f, 0.6f);
            Hud.Vignette(p, 1f);
            P(p, "logo", 300, 0, 680, 332).gameObject.AddComponent<Pulse>().amount = 0.015f;
            Coins(p);
            int lvl = Save.Data.level;
            var play = B(p, "btn_green", "", 0, 440, 342, 400, 134, () => Game.I.PlayLevel(Save.Data.level));
            play.gameObject.AddComponent<Pulse>().amount = 0.03f;
            var pr = (RectTransform)play.transform;
            Hud.In(UIKit.Title(pr, "PLAY", 76, Color.white).rectTransform, new Vector2(400, 134), 0, 14, 400, 76);
            Hud.In(UIKit.Text(pr, "LEVEL " + lvl, 22, Color.white).rectTransform, new Vector2(400, 134), 0, 91, 400, 22);

            var endless = B(p, "btn_blue", "", 0, 440, 492, 194, 92, () => Game.I.PlayEndless());
            var er = (RectTransform)endless.transform;
            Hud.In(UIKit.Pic(er, "ic_infinity", Color.white).rectTransform, new Vector2(194, 92), 12, 14, 64, 48);
            Hud.In(UIKit.Title(er, "ENDLESS", 34, Color.white, TextAnchor.MiddleLeft).rectTransform, new Vector2(194, 92), 76, 12, 116, 34);
            Hud.In(UIKit.Text(er, Save.Data.bestWave > 0 ? "BEST: WAVE " + Save.Data.bestWave : "NO LIMITS", 16, Color.white, TextAnchor.MiddleLeft).rectTransform, new Vector2(194, 92), 76, 52, 116, 18);

            var arm = B(p, "btn_gold", "", 0, 646, 492, 194, 92, () => Armory(0));
            var ar = (RectTransform)arm.transform;
            Hud.In(UIKit.Pic(ar, "ic_upgrade", Color.white).rectTransform, new Vector2(194, 92), 10, 8, 64, 64);
            Hud.In(UIKit.Title(ar, "ARMORY", 38, Color.white, TextAnchor.MiddleLeft).rectTransform, new Vector2(194, 92), 76, 22, 116, 38);

            P(p, "panel", 540, 604, 200, 52);
            P(p, "star_on", 552, 608, 44, 44);
            L(p, Save.TotalStars + " STARS", false, 24, UIKit.Gold, 600, 618, 130, TextAnchor.MiddleLeft);

            Image muteIcon = null;
            var mute = B(p, "round_dark", "", 0, 1172, 612, 88, 88, () =>
            {
                Save.Data.muted = !Save.Data.muted; Save.Write(); SoundBank.ApplyMute();
                muteIcon.sprite = UIKit.Spr(Save.Data.muted ? "ic_mute" : "ic_sound");
            }, BR);
            muteIcon = UIKit.Pic(mute.transform, Save.Data.muted ? "ic_mute" : "ic_sound", Color.white);
            Hud.In(muteIcon.rectTransform, new Vector2(88, 88), 18, 12, 52, 52);
            Hud.Label(p, "Zombie Pile " + Game.Version + "   " + Kit.Status + "   |   models: Quaternius (CC0)   |   icons: game-icons.net by Lorc & Delapouite (CC BY 3.0)",
                false, 14, new Color(1f, 1f, 1f, 0.5f), BL, 16, 698, 1100, TextAnchor.MiddleLeft, false);
        }

        // ------------------------------------------------------------------ armory
        public void Armory(int tab)
        {
            armoryTab = tab;
            var p = NewPage("Armory", new Color(0.03f, 0.01f, 0.05f, 0.86f));
            Ribbon(p, 390, 12, 500, "ARMORY", 62);
            var back = B(p, "round_dark", "", 0, 22, 18, 84, 84, () => Game.I.ShowMenu(), TL);
            Hud.In(UIKit.Pic(back.transform, "ic_back", Color.white).rectTransform, new Vector2(84, 84), 14, 10, 56, 56);
            Coins(p, 20);

            string[] names = { "SHAUN", "LIS", "GEAR" };
            string[] icons = { "por_shaun", "por_lis", "ic_shield" };
            for (int i = 0; i < 3; i++)
            {
                int t = i;
                var b = B(p, tab == i ? "btn_gold" : "btn_dark", "", 0, 250 + i * 270, 124, 250, 76, () => Armory(t));
                var br = (RectTransform)b.transform;
                Hud.In(UIKit.Pic(br, icons[i], Color.white).rectTransform, new Vector2(250, 76), 14, 6, 58, 58);
                Hud.In(UIKit.Title(br, names[i], 40, Color.white, TextAnchor.MiddleLeft).rectTransform, new Vector2(250, 76), 80, 14, 160, 40);
            }

            if (tab == 0)
            {
                for (int i = 0; i < Econ.Weapons.Length; i++) WeaponCard(p, 170 + i * 320, 216, Econ.Weapons[i]);
                UpCard(p, 170, 404, Up.GunDamage, Orange);
                UpCard(p, 490, 404, Up.GunRate, Orange);
                UpCard(p, 810, 404, Up.GunBullets, Orange);
            }
            else if (tab == 1)
            {
                SniperCard(p, 490, 216);
                UpCard(p, 170, 404, Up.SniperDamage, Blue);
                UpCard(p, 490, 404, Up.SniperRate, Blue);
                UpCard(p, 810, 404, Up.SniperPierce, Blue);
            }
            else
            {
                UpCard(p, 170, 216, Up.BarrelPower, Blue);
                UpCard(p, 490, 216, Up.BarrelReload, Blue);
                UpCard(p, 810, 216, Up.Airstrike, Blue);
                UpCard(p, 170, 414, Up.WallHp, Green);
                UpCard(p, 490, 414, Up.CoinBonus, Green);
                UpCard(p, 810, 414, Up.Repair, Green);
            }
            B(p, "btn_green", "FIGHT! LEVEL " + Save.Data.level, 54, 430, 612, 420, 96, () => Game.I.PlayLevel(Save.Data.level));
        }

        void UpCard(Transform parent, float x, float y, Up u, Color color)
        {
            var d = Econ.Def(u);
            int lvl = Econ.Lvl(u);
            var g = Group(parent, "Up " + d.name);
            P(g, "panel", x, y, 300, 186);
            P(g, "fill", x + 8, y + 8, 284, 48, color);
            L(g, d.name, true, 32, Color.white, x + 18, y + 16, 200, TextAnchor.MiddleLeft);
            P(g, "panel", x + 212, y + 14, 74, 36);
            L(g, "LV " + lvl, false, 20, Color.white, x + 212, y + 22, 74);
            var circle = UIKit.Image(g, "IconBg", new Color(0f, 0f, 0f, 0.3f), UIKit.Circle);
            UIKit.At(circle.rectTransform, C, x + 16, y + 66, 74, 74);
            P(g, d.icon, x + 21, y + 71, 64, 64);
            bool maxed = Econ.Maxed(u);
            string stat = maxed ? d.show(lvl) : d.show(lvl) + " <color=#a8f04a>» " + d.show(lvl + 1) + "</color>";
            L(g, stat, true, 30, Color.white, x + 100, y + 72, 190, TextAnchor.MiddleLeft);

            if (maxed) { L(g, "MAX", true, 38, UIKit.Gold, x + 100, y + 124, 186); return; }
            if (!Econ.Unlocked(u))
            {
                P(g, "ic_lock", x + 112, y + 118, 44, 44);
                L(g, "LEVEL " + d.unlockLevel, false, 26, new Color(0.8f, 0.76f, 0.86f), x + 150, y + 128, 136);
                return;
            }
            int cost = Econ.Cost(u);
            bool can = Econ.CanBuy(u);
            var buy = B(g, can ? "btn_green" : "btn_dark", "", 0, x + 100, y + 112, 186, 62, () =>
            {
                if (Econ.Buy(u)) { SoundBank.I.Play(SoundBank.I.buy, 0.9f); Armory(armoryTab); }
                else SoundBank.I.Play(SoundBank.I.click, 0.6f, 0.6f);
            });
            var br = (RectTransform)buy.transform;
            Hud.In(UIKit.Pic(br, "coin", Color.white).rectTransform, new Vector2(186, 62), 12, 8, 38, 38);
            Hud.In(UIKit.Title(br, cost.ToString("N0"), 34, can ? Color.white : new Color(1f, 0.55f, 0.48f)).rectTransform, new Vector2(186, 62), 50, 8, 124, 38);
        }

        void WeaponCard(Transform parent, float x, float y, WeaponDef w)
        {
            var g = Group(parent, "Weapon " + w.name);
            bool owned = Econ.Owns(w.id), equipped = owned && Save.Data.weapon == w.id, locked = !owned && Save.Data.level < w.unlockLevel;
            if (equipped) { var glow = P(g, "glow", x - 30, y - 30, 360, 230, new Color(0.66f, 0.94f, 0.29f, 0.5f)); glow.preserveAspect = false; }
            P(g, "panel", x, y, 300, 170);
            P(g, w.image, x + 30, y + 6, 240, 110, locked ? new Color(0.2f, 0.2f, 0.25f) : Color.white);
            if (locked) P(g, "ic_lock", x + 118, y + 22, 64, 64);
            L(g, w.name, true, 30, Color.white, x + 16, y + 118, 140, TextAnchor.MiddleLeft);
            if (equipped) L(g, "EQUIPPED", false, 22, new Color(0.66f, 0.94f, 0.29f), x + 150, y + 126, 134, TextAnchor.MiddleRight);
            else if (owned) B(g, "btn_blue", "EQUIP", 30, x + 164, y + 110, 122, 54, () => { Save.Data.weapon = w.id; Save.Write(); Shooter.Wall.RefreshWeapon(); Armory(0); });
            else if (locked) L(g, "LEVEL " + w.unlockLevel, false, 22, new Color(0.8f, 0.76f, 0.86f), x + 150, y + 126, 134, TextAnchor.MiddleRight);
            else
            {
                bool can = Save.Data.coins >= w.price;
                var b = B(g, can ? "btn_green" : "btn_dark", "", 0, x + 150, y + 108, 136, 56, () =>
                {
                    if (Econ.BuyWeapon(w.id)) { SoundBank.I.Play(SoundBank.I.buy, 0.9f); Shooter.Wall.RefreshWeapon(); Armory(0); }
                });
                var br = (RectTransform)b.transform;
                Hud.In(UIKit.Pic(br, "coin", Color.white).rectTransform, new Vector2(136, 56), 10, 8, 36, 36);
                Hud.In(UIKit.Title(br, w.price.ToString("N0"), 32, can ? Color.white : new Color(1f, 0.55f, 0.48f)).rectTransform, new Vector2(136, 56), 46, 8, 84, 36);
            }
        }

        void SniperCard(Transform parent, float x, float y)
        {
            var g = Group(parent, "Sniper");
            P(g, "panel", x, y, 300, 170);
            P(g, "w_rifle", x + 30, y + 6, 240, 110);
            L(g, "SNIPER", true, 30, Color.white, x + 16, y + 118, 140, TextAnchor.MiddleLeft);
            L(g, "ON THE TOWER", false, 20, new Color(0.56f, 0.85f, 1f), x + 130, y + 126, 154, TextAnchor.MiddleRight);
        }

        // ------------------------------------------------------------------ results
        public void Complete(int level, int stars, int kills, int coins)
        {
            var p = NewPage("Complete", new Color(0.03f, 0.01f, 0.05f, 0.72f));
            Hud.Vignette(p, 1f);
            Ribbon(p, 300, 30, 680, "LEVEL " + level + " COMPLETE!", 56);
            for (int i = 0; i < 3; i++)
            {
                float size = i == 1 ? 130 : 110, x = i == 0 ? 472 : i == 1 ? 580 : 708, y = i == 1 ? 128 : 150;
                var s = P(p, i < stars ? "star_on" : "star_off", x, y, size, size);
                if (i < stars) { s.transform.localScale = Vector3.zero; s.gameObject.AddComponent<PopIn>().delay = 0.25f + i * 0.25f; }
            }
            P(p, "panel", 420, 286, 440, 176);
            P(p, "ic_skull", 446, 300, 60, 60); L(p, "KILLS", false, 34, Color.white, 520, 312, 150, TextAnchor.MiddleLeft); L(p, kills.ToString(), true, 52, Color.white, 700, 306, 136, TextAnchor.MiddleRight);
            P(p, "coin", 446, 378, 64, 64); L(p, "COINS", false, 34, Color.white, 520, 392, 150, TextAnchor.MiddleLeft);
            var earned = L(p, "+" + coins, true, 60, UIKit.Gold, 660, 380, 176, TextAnchor.MiddleRight);
            Coins(p);
            Button dbl = null;
            dbl = B(p, "btn_gold", "", 0, 380, 494, 250, 112, () =>
            {
                Game.I.DoubleCoins(() => { earned.text = "+" + Game.I.RunCoins; if (dbl != null) dbl.gameObject.SetActive(false); });
            });
            var dr = (RectTransform)dbl.transform;
            Hud.In(UIKit.Pic(dr, "ic_video", Color.white).rectTransform, new Vector2(250, 112), 16, 22, 60, 60);
            Hud.In(UIKit.Title(dr, "×2", 64, Color.white).rectTransform, new Vector2(250, 112), 80, 16, 150, 64);
            Hud.In(UIKit.Text(dr, "WATCH AD", 18, Color.white).rectTransform, new Vector2(250, 112), 80, 76, 150, 18);
            B(p, "btn_green", "CONTINUE", 50, 650, 494, 250, 112, () => Game.I.AfterBreak(level, () => Armory(0)));
        }

        public void Failed(int level, int coins)
        {
            var p = NewPage("Failed", new Color(0.2f, 0f, 0f, 0.8f));
            Hud.Vignette(p, 1f);
            L(p, "OVERRUN!", true, 150, new Color(1f, 0.29f, 0.21f), 0, 30, 1280).GetComponent<Stroke>().width = 12f;
            P(p, "panel", 420, 214, 440, 180);
            P(p, "zhead", 446, 228, 60, 60); L(p, "LEVEL", false, 34, Color.white, 520, 240, 150, TextAnchor.MiddleLeft); L(p, level.ToString(), true, 52, Color.white, 700, 234, 136, TextAnchor.MiddleRight);
            P(p, "coin", 446, 300, 64, 64); L(p, "COINS", false, 34, Color.white, 520, 314, 150, TextAnchor.MiddleLeft); L(p, "+" + coins, true, 60, UIKit.Gold, 660, 302, 176, TextAnchor.MiddleRight);
            L(p, "Your coins are kept. Upgrade and try again!", false, 26, Color.white, 0, 412, 1280);
            Coins(p);
            var arm = B(p, "btn_gold", "", 0, 380, 470, 250, 112, () => Armory(0));
            var ar = (RectTransform)arm.transform;
            Hud.In(UIKit.Pic(ar, "ic_upgrade", Color.white).rectTransform, new Vector2(250, 112), 16, 16, 64, 64);
            Hud.In(UIKit.Title(ar, "ARMORY", 46, Color.white).rectTransform, new Vector2(250, 112), 82, 26, 160, 46);
            B(p, "btn_green", "RETRY", 60, 650, 470, 250, 112, () => Game.I.PlayLevel(level));
        }

        public void EndlessOver(int wave, int best, int coins, bool newBest)
        {
            var p = NewPage("EndlessOver", new Color(0.2f, 0f, 0f, 0.8f));
            Hud.Vignette(p, 1f);
            L(p, "OVERRUN!", true, 150, new Color(1f, 0.29f, 0.21f), 0, 30, 1280).GetComponent<Stroke>().width = 12f;
            P(p, "panel", 420, 214, 440, 230);
            P(p, "ic_infinity", 442, 234, 72, 54); L(p, "WAVE", false, 34, Color.white, 520, 240, 150, TextAnchor.MiddleLeft); L(p, wave.ToString(), true, 52, UIKit.Gold, 700, 234, 136, TextAnchor.MiddleRight);
            P(p, "ic_trophy", 446, 300, 60, 60);
            L(p, newBest ? "NEW BEST!" : "BEST", newBest ? true : false, newBest ? 44 : 34, newBest ? UIKit.Gold : Color.white, 520, newBest ? 308 : 312, 190, TextAnchor.MiddleLeft);
            if (!newBest) L(p, best.ToString(), true, 52, Color.white, 700, 306, 136, TextAnchor.MiddleRight);
            P(p, "coin", 446, 370, 64, 64); L(p, "COINS", false, 34, Color.white, 520, 384, 150, TextAnchor.MiddleLeft); L(p, "+" + coins, true, 60, UIKit.Gold, 660, 372, 176, TextAnchor.MiddleRight);
            Coins(p);
            var arm = B(p, "btn_gold", "", 0, 380, 478, 250, 112, () => Armory(0));
            var ar = (RectTransform)arm.transform;
            Hud.In(UIKit.Pic(ar, "ic_upgrade", Color.white).rectTransform, new Vector2(250, 112), 16, 16, 64, 64);
            Hud.In(UIKit.Title(ar, "ARMORY", 46, Color.white).rectTransform, new Vector2(250, 112), 82, 26, 160, 46);
            B(p, "btn_blue", "RETRY", 60, 650, 478, 250, 112, () => Game.I.PlayEndless());
            var back = B(p, "round_dark", "", 0, 22, 18, 84, 84, () => Game.I.ShowMenu(), TL);
            Hud.In(UIKit.Pic(back.transform, "ic_back", Color.white).rectTransform, new Vector2(84, 84), 14, 10, 56, 56);
        }

        // ------------------------------------------------------------------ new zombie card
        public void Intro(ZType t, Action done)
        {
            var p = NewPage("Intro", new Color(0.03f, 0.01f, 0.05f, 0.8f));
            bool boss = t.ability == Ability.Boss;
            Ribbon(p, 340, 22, 600, boss ? "BOSS LEVEL!" : "NEW ZOMBIE!", 62);
            P(p, "panel", 390, 134, 500, 450);
            var glow = P(p, "glow", 440, 130, 400, 330, new Color(t.text.r, t.text.g, t.text.b, 0.6f));
            glow.preserveAspect = false;
            var pic = P(p, t.portrait, 470, 140, 340, 300);
            pic.gameObject.AddComponent<Pulse>().amount = 0.02f;
            L(p, t.name, true, 70, t.text, 390, 440, 500);
            var d = L(p, t.desc, false, 28, Color.white, 400, 516, 480);
            d.lineSpacing = 1.05f;
            B(p, "btn_green", "GOT IT!", 56, 490, 604, 300, 96, () => { HideAll(); if (done != null) done(); });
            SoundBank.I.Play(boss ? SoundBank.I.horn : SoundBank.I.chime, 0.8f);
        }
    }

    /// Scales in with a little overshoot after a delay (stars on the results screen).
    public class PopIn : MonoBehaviour
    {
        public float delay;
        float t;
        bool played;
        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01((t - delay) / 0.3f);
            if (k > 0f && !played) { played = true; SoundBank.I.Play(SoundBank.I.coin, 0.6f, 0.8f + delay); }
            float s = k < 1f ? Mathf.Sin(k * Mathf.PI * 0.5f) * (1f + 0.3f * Mathf.Sin(k * Mathf.PI)) : 1f;
            transform.localScale = Vector3.one * s;
        }
    }
}
