using System;
using UnityEngine;

namespace ZombiePile
{
    [Serializable]
    public class SaveData
    {
        public int bestWave;
        public int totalKills;
        public int runs;
        public bool muted;
    }

    public static class Save
    {
        const string Key = "zombiepile_save_v1";
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
        }

        public static void Write()
        {
            if (Data != null) PlatformSDK.SaveString(Key, JsonUtility.ToJson(Data));
        }
    }
}
