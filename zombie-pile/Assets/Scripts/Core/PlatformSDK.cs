using System;
using UnityEngine;
#if CRAZY_SDK
using CrazyGames;
#endif

namespace ZombiePile
{
    /// <summary>
    /// Thin wrapper around the CrazyGames SDK so the game runs with or without it.
    /// After importing the CrazyGames Unity SDK (v3), add the scripting define symbol
    /// CRAZY_SDK in Player Settings to switch these calls on.
    /// Without the SDK: progress goes to PlayerPrefs and ads resolve immediately.
    /// </summary>
    public static class PlatformSDK
    {
        public static bool Ready { get; private set; }

        public static void Init(Action onReady)
        {
#if CRAZY_SDK
            CrazySDK.Init(() =>
            {
                Ready = true;
                if (onReady != null) onReady();
            });
#else
            Ready = true;
            if (onReady != null) onReady();
#endif
        }

        public static void LoadingStop()
        {
#if CRAZY_SDK
            if (CrazySDK.IsAvailable) CrazySDK.Game.LoadingStop();
#endif
        }

        public static void GameplayStart()
        {
#if CRAZY_SDK
            if (CrazySDK.IsAvailable) CrazySDK.Game.GameplayStart();
#endif
        }

        public static void GameplayStop()
        {
#if CRAZY_SDK
            if (CrazySDK.IsAvailable) CrazySDK.Game.GameplayStop();
#endif
        }

        public static void HappyTime()
        {
#if CRAZY_SDK
            if (CrazySDK.IsAvailable) CrazySDK.Game.HappyTime();
#endif
        }

        /// Midgame (interstitial) ad at a natural break. onDone always fires.
        public static void Midgame(Action onStart, Action onDone)
        {
#if CRAZY_SDK
            if (CrazySDK.IsAvailable)
            {
                CrazySDK.Ad.RequestAd(CrazyAdType.Midgame,
                    () => { if (onStart != null) onStart(); },
                    error => { if (onDone != null) onDone(); },
                    () => { if (onDone != null) onDone(); });
                return;
            }
#endif
            if (onDone != null) onDone();
        }

        /// Rewarded ad. onReward only fires when the ad was fully watched.
        public static void Rewarded(Action onStart, Action onReward, Action onFail)
        {
#if CRAZY_SDK
            if (CrazySDK.IsAvailable)
            {
                CrazySDK.Ad.RequestAd(CrazyAdType.Rewarded,
                    () => { if (onStart != null) onStart(); },
                    error => { if (onFail != null) onFail(); },
                    () => { if (onReward != null) onReward(); });
                return;
            }
#endif
            if (onReward != null) onReward();
        }

        public static string LoadString(string key)
        {
#if CRAZY_SDK
            if (CrazySDK.IsAvailable)
                return CrazySDK.Data.HasKey(key) ? CrazySDK.Data.GetString(key) : null;
#endif
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        public static void SaveString(string key, string value)
        {
#if CRAZY_SDK
            if (CrazySDK.IsAvailable)
            {
                CrazySDK.Data.SetString(key, value);
                return;
            }
#endif
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }
    }
}
