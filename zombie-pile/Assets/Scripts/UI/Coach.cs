using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// Teaches while playing. Level 1 walks the player through five lessons (shoot, headshots, the wall, the
    /// barrel, switching shooters), each one waiting for the player to actually do the thing; every later
    /// feature gets a one-time tip the first time it matters (crates, buddies, chewed wall, airstrike...).
    /// Mission rewards use the same card. Everything shows on the HUD's coach card at the bottom.
    public class Coach : MonoBehaviour
    {
        public static Coach I;

        public const int TipCrate = 0, TipSquad = 1, TipChip = 2, TipAir = 3, TipEndless = 4, TipCombo = 5;
        const int Steps = 5;

        class Item { public string title, body; public float dur; }
        readonly Queue<Item> queue = new Queue<Item>();
        Item current;
        float itemT;

        bool tutorial;
        int step;
        float stepT;
        int baseShots, baseHeads, baseBarrels, baseSwitches;
        string shownKey = "";

        public static void Build()
        {
            var go = new GameObject("Coach");
            go.AddComponent<Coach>();
        }

        void Awake()
        {
            I = this;
            Missions.Completed += OnMission;
        }

        void OnDestroy() { Missions.Completed -= OnMission; }

        static bool Mobile { get { return Application.isMobilePlatform; } }

        // ------------------------------------------------------------------ one-time tips
        /// Shows this tip once, ever (the flag is saved).
        public static void Tip(int id, string title, string body, float dur = 7f)
        {
            if (I == null || Save.Data == null || (Save.Data.tips & (1 << id)) != 0) return;
            Save.Data.tips |= 1 << id;
            Save.Write();
            I.queue.Enqueue(new Item { title = title, body = body, dur = dur });
        }

        void OnMission(string text, int reward)
        {
            queue.Enqueue(new Item { title = "MISSION COMPLETE!  +" + reward, body = text, dur = 4.5f });
            if (Game.I != null && !Game.I.Playing && Screens.I != null) Screens.I.Toast("MISSION COMPLETE!  +" + reward + " COINS");
        }

        // ------------------------------------------------------------------ the level 1 tutorial
        public void BeginLevel(int n, bool endless)
        {
            queue.Clear(); current = null; shownKey = "";
            tutorial = !endless && n <= 2 && Save.Data.tut < Steps;      // levels 1 and 2: it finishes even if level 1 is short
            if (tutorial) Enter(Mathf.Clamp(Save.Data.tut, 0, Steps - 1));
            else Hud.I.Highlight(0);
            if (endless) Tip(TipEndless, "ENDLESS MODE", "Waves never stop and get harder.\nReach your best wave!");
            if (Econ.Lvl(Up.Airstrike) > 0) Tip(TipAir, "AIRSTRIKE READY", Mobile ? "Tap AIRSTRIKE to carpet-bomb the street.\nAim at the crowd first." : "Press F to carpet-bomb the street.\nAim at the crowd first.");
        }

        public void SkipTutorial()
        {
            tutorial = false;
            Save.Data.tut = Steps;
            Save.Write();
            Hud.I.Highlight(0);
            shownKey = "";
        }

        void Enter(int s)
        {
            step = s; stepT = 0f;
            baseShots = Stats.Shots; baseHeads = Stats.Heads; baseBarrels = Stats.Barrels; baseSwitches = Stats.Switches;
            Save.Data.tut = s;
            Hud.I.Highlight(s == 2 ? 1 : s == 3 ? 2 : s == 4 ? 3 : 0);
            shownKey = "";
        }

        void Next()
        {
            if (step + 1 >= Steps)
            {
                SkipTutorial();
                queue.Enqueue(new Item { title = "NICE! YOU GOT IT", body = "Kill them all and keep the wall standing.\nSpend your coins in the ARMORY between levels.", dur = 7f });
                return;
            }
            Enter(step + 1);
        }

        bool StepDone()
        {
            switch (step)
            {
                case 0: return Stats.Shots - baseShots >= 10 && stepT > 3f;
                case 1: return (Stats.Heads - baseHeads >= 1 && stepT > 4f) || stepT > 30f;
                case 2: return stepT > 9f;
                case 3: return (Stats.Barrels - baseBarrels >= 1 && stepT > 3f) || stepT > 45f;
                default: return (Stats.Switches - baseSwitches >= 1 && stepT > 3f) || stepT > 50f;
            }
        }

        void StepText(out string title, out string body)
        {
            string n = (step + 1) + "/" + Steps + "  ";
            switch (step)
            {
                case 0:
                    title = n + "SHOOT!";
                    body = Mobile ? "Touch and hold on the zombies to fire.\nYour shooter aims where your finger is." : "Hold the mouse button on the zombies to fire.\nThe crosshair snaps to zombies near it.";
                    return;
                case 1:
                    title = n + "AIM FOR THE HEAD";
                    body = "Headshots do double damage (gold numbers).\nOne good hit on the head drops most zombies.";
                    return;
                case 2:
                    title = n + "PROTECT THE WALL";
                    body = "Zombies climb over each other. Every one that gets\nover hurts the wall (top left). At zero, the city falls.";
                    return;
                case 3:
                    title = n + "EXPLOSIVE BARREL";
                    body = Mobile ? "Tap BARREL (bottom right) to throw a barrel\nat the crowd. Bigger crowd, bigger boom!" : "Press SPACE (or right-click) to throw a barrel\nat the crowd. Bigger crowd, bigger boom!";
                    return;
                default:
                    title = n + "SWITCH SHOOTERS";
                    body = Mobile ? "Tap a portrait to jump to that shooter. The snipers\non the towers see the wall. Tap SHAUN to come back." : "Press 1 or 3 (or click a portrait) to jump to a tower\nsniper. Towers see the wall. Press 2 to come back.";
                    return;
            }
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            var hud = Hud.I;
            if (hud == null) return;
            bool playing = Game.I != null && Game.I.Playing;
            if (!playing) { current = null; queue.Clear(); if (hud.HintShown) hud.HideHint(); hud.Highlight(0); shownKey = ""; return; }
            float dt = Time.deltaTime;

            if (current == null && queue.Count > 0) { current = queue.Dequeue(); itemT = 0f; }
            if (current != null)
            {
                itemT += dt;
                Show("tip:" + current.title, current.title, current.body, false);
                if (itemT > current.dur) { current = null; shownKey = ""; }
                return;
            }
            if (!tutorial) { if (hud.HintShown) hud.HideHint(); shownKey = ""; return; }

            stepT += dt;
            if (StepDone()) { Next(); if (!tutorial) return; }
            string t, b;
            StepText(out t, out b);
            Show("step:" + step, t, b, true);
            hud.Highlight(step == 2 ? 1 : step == 3 ? 2 : step == 4 ? 3 : 0);
        }

        void Show(string key, string title, string body, bool skippable)
        {
            if (key == shownKey && Hud.I.HintShown) return;
            shownKey = key;
            Hud.I.ShowHint(title, body, skippable);
            if (!skippable) Hud.I.Highlight(0);
        }
    }
}
