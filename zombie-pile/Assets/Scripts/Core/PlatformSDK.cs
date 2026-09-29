using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZombiePile
{
    /// <summary>
    /// The CrazyGames SDK (v3, HTML5) through Assets/Plugins/WebGL/ZPCrazyGames.jslib: no Unity package needed.
    /// In a WebGL build the jslib loads the SDK script itself. Everywhere else (the Editor, a build hosted
    /// somewhere that is not CrazyGames, no network) the game keeps working the same way: progress goes to
    /// PlayerPrefs and ads resolve at once (the rewarded one grants its reward).
    /// Progress is also written to the player's CrazyGames account, and the newer copy wins on start.
    /// </summary>
    public static class PlatformSDK
    {
        public static bool Ready { get; private set; }
        /// True when the CrazyGames SDK is up (ads, account save and invite links are real).
        public static bool Live { get { return state == 2; } }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ZP_Sdk_Init();
        [DllImport("__Internal")] static extern int ZP_Sdk_State();
        [DllImport("__Internal")] static extern void ZP_Sdk_Game(string name);
        [DllImport("__Internal")] static extern void ZP_Sdk_RequestAd(string type);
        [DllImport("__Internal")] static extern int ZP_Sdk_AdEvent();
        [DllImport("__Internal")] static extern IntPtr ZP_Sdk_GetItem(string key);
        [DllImport("__Internal")] static extern void ZP_Sdk_SetItem(string key, string value);
        [DllImport("__Internal")] static extern void ZP_Sdk_ShowInvite(string code);
        [DllImport("__Internal")] static extern void ZP_Sdk_HideInvite();
        [DllImport("__Internal")] static extern IntPtr ZP_Sdk_InviteParam();
        [DllImport("__Internal")] static extern void ZP_Sdk_Free(IntPtr p);

        static string Take(IntPtr p)
        {
            if (p == IntPtr.Zero) return "";
            string s = Marshal.PtrToStringUTF8(p);
            ZP_Sdk_Free(p);
            return s ?? "";
        }
#endif

        static int state;                 // 0 idle, 1 loading, 2 live, 3 not available
        static bool playing, inviteShown;
        static Action onReady;
        static Driver driver;

        // ------------------------------------------------------------------ start
        public static void Init(Action ready)
        {
            onReady = ready;
#if UNITY_WEBGL && !UNITY_EDITOR
            var go = new GameObject("PlatformSDK");
            UnityEngine.Object.DontDestroyOnLoad(go);
            driver = go.AddComponent<Driver>();
            ZP_Sdk_Init();
            state = 1;
#else
            Finish(3);
#endif
        }

        static void Finish(int newState)
        {
            state = newState;
            Ready = true;
            if (state == 2) SyncSave();
            var cb = onReady; onReady = null;
            if (cb != null) cb();
        }

        // ------------------------------------------------------------------ game state
        public static void LoadingStop() { Call("loadingStop"); }

        public static void GameplayStart() { if (playing) return; playing = true; Call("gameplayStart"); }

        public static void GameplayStop() { if (!playing) return; playing = false; Call("gameplayStop"); }

        /// A celebration (level won): the platform may use it to time promotions.
        public static void HappyTime() { Call("happytime"); }

        static void Call(string name)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (state == 2) ZP_Sdk_Game(name);
#endif
        }

        // ------------------------------------------------------------------ ads
        /// Midgame (interstitial) ad at a natural break. onDone always fires.
        public static void Midgame(Action onStart, Action onDone) { Ad(false, onStart, onDone, onDone); }

        /// Rewarded ad. onReward only fires when the ad was fully watched.
        public static void Rewarded(Action onStart, Action onReward, Action onFail) { Ad(true, onStart, onReward, onFail); }

        static void Ad(bool rewarded, Action start, Action ok, Action fail)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (state == 2 && driver != null && !driver.Busy)
            {
                driver.Run(rewarded, start, ok, fail);
                ZP_Sdk_RequestAd(rewarded ? "rewarded" : "midgame");
                return;
            }
#endif
            // no SDK: nothing to watch. Midgame just continues; the rewarded ad pays out (testing, other websites).
            if (rewarded) { if (ok != null) ok(); }
            else if (ok != null) ok();
        }

        // ------------------------------------------------------------------ progress
        public static string LoadString(string key)
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        public static void SaveString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
#if UNITY_WEBGL && !UNITY_EDITOR
            if (state == 2) ZP_Sdk_SetItem(key, value);
#endif
        }

        /// The account copy may be newer (another device): the newer one wins.
        static void SyncSave()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string remote = Take(ZP_Sdk_GetItem(Save.Key));
            if (remote.Length > 0 && Save.MergeRemote(remote)) Save.Write();
            else if (Save.Data != null) ZP_Sdk_SetItem(Save.Key, JsonUtility.ToJson(Save.Data));
#endif
        }

        // ------------------------------------------------------------------ co-op invite links
        /// Shows CrazyGames' invite button for this room (friends who open the link land in the room).
        public static void ShowInvite(string code)
        {
            if (inviteShown) return;
            inviteShown = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (state == 2) ZP_Sdk_ShowInvite(code);
#endif
        }

        public static void HideInvite()
        {
            if (!inviteShown) return;
            inviteShown = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (state == 2) ZP_Sdk_HideInvite();
#endif
        }

        /// The room code from an invite link this page was opened with ("" when there is none).
        public static string InviteRoom()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (state == 2) return Take(ZP_Sdk_InviteParam()).Trim().ToUpperInvariant();
#endif
            return "";
        }

        // ------------------------------------------------------------------ polling (the jslib never calls C#)
        class Driver : MonoBehaviour
        {
            bool rewarded, busy, started;
            float waited;
            Action start, ok, fail;
            public bool Busy { get { return busy; } }

            public void Run(bool isRewarded, Action onStart, Action onOk, Action onFail)
            {
                rewarded = isRewarded; start = onStart; ok = onOk; fail = onFail;
                busy = true; started = false; waited = 0f;
            }

            void Update()
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (!Ready)
                {
                    int st = ZP_Sdk_State();
                    if (st == 2 || st == 3) Finish(st);
                    return;
                }
                if (!busy) return;
                waited += Time.unscaledDeltaTime;
                int e;
                while ((e = ZP_Sdk_AdEvent()) != 0)
                {
                    if (e == 1) { started = true; if (start != null) start(); }
                    else { End(e == 2); return; }
                }
                // an ad that never reports back must not freeze the game
                if (!started && waited > 12f) End(false);
#endif
            }

            void End(bool finished)
            {
                busy = false;
                var a = finished ? ok : fail;
                ok = fail = start = null;
                if (a != null) a();
            }
        }
    }
}
