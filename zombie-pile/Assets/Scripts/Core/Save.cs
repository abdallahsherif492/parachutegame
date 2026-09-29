using System;
using UnityEngine;

namespace ZombiePile
{
    /// Everything that persists between sessions: coins, campaign progress, upgrade levels, weapons.
    [Serializable]
    public class SaveData
    {
        public int coins;
        public int level = 1;                  // next campaign level to play
        public int[] stars = new int[0];       // best stars per campaign level (index = level - 1)
        public int bestWave;                   // endless mode record
        public int[] up = new int[0];          // upgrade levels, indexed by Up
        public int weapons = 1;                // owned weapons (bit per Weapon id; the SMG is free)
        public int weapon;                     // equipped weapon for the wall gunner
        public int seen;                       // zombie types already introduced (bit per ZType id)
        public bool muted;
        public bool switchTip;                 // the "switch to the tower" tip was shown
        public string name;                    // shown to other players in co-op rooms
        public int totalKills, plays;
    }

    public static class Save
    {
        const string Key = "zombiepile_save_v2";
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
            Data = d ?? new SaveData();
            // arrays grow when new upgrades are added in later versions
            if (Data.up == null || Data.up.Length < Econ.UpCount)
            {
                var a = new int[Econ.UpCount];
                if (Data.up != null) Array.Copy(Data.up, a, Mathf.Min(Data.up.Length, a.Length));
                Data.up = a;
            }
            if (Data.stars == null) Data.stars = new int[0];
            if (Data.level < 1) Data.level = 1;
            Data.weapons |= 1;
        }

        public static void Write()
        {
            if (Data != null) PlatformSDK.SaveString(Key, JsonUtility.ToJson(Data));
        }

        public static int Stars(int level)
        {
            int i = level - 1;
            return Data != null && i >= 0 && i < Data.stars.Length ? Data.stars[i] : 0;
        }

        public static void SetStars(int level, int stars)
        {
            int i = level - 1;
            if (i < 0) return;
            if (Data.stars.Length <= i) Array.Resize(ref Data.stars, i + 1);
            Data.stars[i] = Mathf.Max(Data.stars[i], stars);
        }

        public static int TotalStars
        {
            get { int n = 0; if (Data != null) foreach (var s in Data.stars) n += s; return n; }
        }
    }
}
