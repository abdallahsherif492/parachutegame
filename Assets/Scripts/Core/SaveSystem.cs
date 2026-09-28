using System;
using UnityEngine;

namespace SkyDrop
{
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int coins;
        public int unlocked;              // highest playable level index
        public int current;               // level shown on the menu
        public int[] stars = new int[Levels.Count];
        public int[] upgrades = new int[Economy.UpgradeCount];
        public int skin;
        public bool[] ownedSkins = new bool[Economy.SkinCount];
        public bool tutorialDone;
        public string lastDailyClaim = "";
        public int dailyStreak;           // number of days already claimed in the current streak
        public string lastDailyJump = "";
        public float bestMultiplier;
        public int totalJumps;
        public int perfectLandings;
        public bool muted;
    }

    public static class SaveSystem
    {
        const string Key = "skydrop_save_v1";
        public static SaveData Data { get; private set; }

        public static void Load()
        {
            SaveData d = null;
            string json = PlatformSDK.LoadString(Key);
            if (!string.IsNullOrEmpty(json))
            {
                try { d = JsonUtility.FromJson<SaveData>(json); }
                catch (Exception e) { Debug.LogWarning("Save corrupted, starting fresh: " + e.Message); }
            }
            Data = Sanitize(d ?? new SaveData());
        }

        static SaveData Sanitize(SaveData d)
        {
            if (d.stars == null || d.stars.Length != Levels.Count)
            {
                var s = new int[Levels.Count];
                if (d.stars != null) Array.Copy(d.stars, s, Mathf.Min(s.Length, d.stars.Length));
                d.stars = s;
            }
            if (d.upgrades == null || d.upgrades.Length != Economy.UpgradeCount)
            {
                var u = new int[Economy.UpgradeCount];
                if (d.upgrades != null) Array.Copy(d.upgrades, u, Mathf.Min(u.Length, d.upgrades.Length));
                d.upgrades = u;
            }
            if (d.ownedSkins == null || d.ownedSkins.Length != Economy.SkinCount)
            {
                var o = new bool[Economy.SkinCount];
                if (d.ownedSkins != null) Array.Copy(d.ownedSkins, o, Mathf.Min(o.Length, d.ownedSkins.Length));
                d.ownedSkins = o;
            }
            d.ownedSkins[0] = true;
            d.unlocked = Mathf.Clamp(d.unlocked, 0, Levels.Count - 1);
            d.current = Mathf.Clamp(d.current, 0, d.unlocked);
            d.skin = Mathf.Clamp(d.skin, 0, Economy.SkinCount - 1);
            if (!d.ownedSkins[d.skin]) d.skin = 0;
            if (d.lastDailyClaim == null) d.lastDailyClaim = "";
            if (d.lastDailyJump == null) d.lastDailyJump = "";
            return d;
        }

        public static void Save()
        {
            if (Data == null) return;
            PlatformSDK.SaveString(Key, JsonUtility.ToJson(Data));
        }

        public static string Today { get { return DateTime.Now.ToString("yyyy-MM-dd"); } }
        public static string Yesterday { get { return DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"); } }
    }
}
