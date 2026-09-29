using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    public enum NetRole { Offline, Host, Client }

    [Serializable] public class LobbyPlayerJson { public int slot; public string name; public bool host; }
    [Serializable]
    public class CtlMsg
    {
        public string t, code, msg;
        public bool started, host;
        public int you, slot;
        public LobbyPlayerJson[] players;
    }

    /// Online co-op (2 or 3 players in one round). One player is the host: their game runs the whole
    /// simulation and streams what happens; the others send their aim and buttons and draw the host's game
    /// (zombies are puppets). The server (server/server.js) only matches players and relays messages.
    /// Slots: 0 = left tower, 1 = the wall (host), 2 = right tower; each human holds one, the AI covers the rest.
    public static class Net
    {
        /// The address of your fly.io app (see server/README): wss://YOUR-APP.fly.dev
        public const string ProdServer = "wss://zombie-pile.fly.dev";
        public const int Protocol = 1;

        public static string ServerUrl
        {
            get
            {
                string custom = PlayerPrefs.GetString("zp_server", "");
                if (!string.IsNullOrEmpty(custom)) return custom;
#if UNITY_EDITOR
                return "ws://localhost:8080";      // node server/server.js
#else
                return ProdServer;
#endif
            }
        }

        public static NetRole Role { get; private set; }
        public static bool IsHost { get { return Role == NetRole.Host; } }
        public static bool IsClient { get { return Role == NetRole.Client; } }
        public static bool IsOnline { get { return Role != NetRole.Offline; } }
        public static int LocalSlot = 1;
        /// Alone you can jump between the three shooters; in a room everybody stays where they are.
        public static bool CanSwitch { get { return Role == NetRole.Offline; } }

        public static NetHost Host;
        public static NetClient Client;

        // ------------------------------------------------------------------ lobby state (for the screens)
        public static string RoomCode = "";
        public static int YouSlot = 1;
        public static bool YouHost, Started;
        public static readonly List<LobbyPlayerJson> Players = new List<LobbyPlayerJson>();
        public static string Status = "";
        public static bool InRoom { get { return RoomCode.Length > 0; } }
        public static bool Connected { get { return link != null && link.State == LinkState.Open; } }

        /// The lobby changed (players came or went, or an error arrived).
        public static event Action LobbyChanged;
        /// The room started (everybody is in): go to the game.
        public static event Action<bool> RoomStarted;
        /// The room ended (the host left or the connection dropped).
        public static event Action<string> RoomClosed;

        static Transport link;
        static NetDriver driver;
        static string pending;          // the request to send once the connection is open
        static bool helloSent;
        static LinkState lastState;

        public static string PlayerName
        {
            get
            {
                if (string.IsNullOrEmpty(Save.Data.name)) { Save.Data.name = "PLAYER" + UnityEngine.Random.Range(100, 999); Save.Write(); }
                return Save.Data.name;
            }
        }

        // ------------------------------------------------------------------ requests from the screens
        public static void Quick() { Request("{\"t\":\"quick\"}"); }
        public static void Create() { Request("{\"t\":\"create\"}"); }
        public static void Join(string code) { Request("{\"t\":\"join\",\"code\":\"" + code.ToUpperInvariant() + "\"}"); }

        static void Request(string json)
        {
            EnsureDriver();
            Status = "Connecting...";
            pending = json.Substring(0, json.Length - 1) + ",\"name\":\"" + PlayerName + "\"}";
            if (link.State != LinkState.Open) { helloSent = false; link.Connect(ServerUrl); lastState = LinkState.Connecting; }
            else SendPending();
            if (LobbyChanged != null) LobbyChanged();
        }

        static void SendPending()
        {
            if (!helloSent) { link.Send("{\"t\":\"hello\",\"v\":" + Protocol + ",\"name\":\"" + PlayerName + "\"}"); helloSent = true; }
            if (pending != null) { link.Send(pending); pending = null; }
        }

        /// Host: everybody who is here plays now.
        public static void StartRoom() { if (Connected && YouHost && !Started) link.Send("{\"t\":\"start\"}"); }

        public static void Leave()
        {
            if (link != null && link.State == LinkState.Open) link.Send("{\"t\":\"leave\"}");
            Reset();
            if (link != null) link.Close();
        }

        /// Back to playing alone.
        public static void Reset()
        {
            Role = NetRole.Offline;
            RoomCode = ""; Started = false; YouHost = false; Players.Clear(); pending = null;
            LocalSlot = 1;
            PlatformSDK.HideInvite();
            if (Host != null) { UnityEngine.Object.Destroy(Host); Host = null; }
            if (Client != null) { UnityEngine.Object.Destroy(Client); Client = null; }
            Zombie.KilledHook = null;
            for (int i = 0; i < 3; i++) { var s = Shooter.Slots[i]; if (s != null) { s.Remote = null; if (s.Ctl == Control.Remote) s.Ctl = Control.AI; } }
            Shooter.SetLocal(1);
        }

        public static void Send(string msg) { if (link != null && link.State == LinkState.Open) link.Send(msg); }
        public static void PressBarrel() { if (Client != null) Client.PressBarrel(); }
        public static void PressAirstrike() { if (Client != null) Client.PressAirstrike(); }

        // ------------------------------------------------------------------ plumbing
        static void EnsureDriver()
        {
            if (link == null) link = new Transport();
            if (driver == null)
            {
                var go = new GameObject("Net");
                UnityEngine.Object.DontDestroyOnLoad(go);
                driver = go.AddComponent<NetDriver>();
            }
        }

        internal static void Pump()
        {
            if (link == null) return;
            var st = link.State;
            if (st != lastState)
            {
                var was = lastState;
                lastState = st;
                if (st == LinkState.Open) { SendPending(); }
                else if ((st == LinkState.Failed || st == LinkState.Closed) && (was == LinkState.Connecting || was == LinkState.Open))
                {
                    string why = st == LinkState.Failed ? "Could not reach the server." : "Connection lost.";
                    bool inRoom = InRoom || IsOnline;
                    Status = why;
                    Reset();
                    if (inRoom && RoomClosed != null) RoomClosed(why);
                    else if (LobbyChanged != null) LobbyChanged();
                }
            }
            string m;
            int guard = 0;
            while (guard++ < 400 && link.TryReceive(out m))
            {
                if (m.Length == 0) continue;
                if (m[0] == '{') Control_(m);
                else if (IsHost && Host != null) Host.OnMessage(m);
                else if (IsClient && Client != null) Client.OnMessage(m);
            }
        }

        static void Control_(string json)
        {
            CtlMsg c;
            try { c = JsonUtility.FromJson<CtlMsg>(json); } catch (Exception) { return; }
            if (c == null) return;
            switch (c.t)
            {
                case "hi": Status = ""; break;
                case "error": Status = c.msg ?? "Error"; if (LobbyChanged != null) LobbyChanged(); break;
                case "lobby":
                    RoomCode = c.code ?? ""; YouSlot = c.you; YouHost = c.host; Started = c.started; Status = "";
                    Players.Clear();
                    if (c.players != null) Players.AddRange(c.players);
                    if (YouHost && !Started && RoomCode.Length > 0) PlatformSDK.ShowInvite(RoomCode);
                    if (LobbyChanged != null) LobbyChanged();
                    break;
                case "started":
                    Started = true;
                    PlatformSDK.HideInvite();
                    Role = YouHost ? NetRole.Host : NetRole.Client;
                    LocalSlot = YouSlot;
                    if (YouHost) { Host = driver.gameObject.AddComponent<NetHost>(); }
                    else { Client = driver.gameObject.AddComponent<NetClient>(); }
                    if (RoomStarted != null) RoomStarted(YouHost);
                    break;
                case "left":
                    if (IsHost && Host != null) Host.PlayerLeft(c.slot);
                    break;
                case "closed":
                    Reset();
                    Status = "The host left the room.";
                    if (RoomClosed != null) RoomClosed(Status);
                    break;
            }
        }
    }

    /// Calls Net.Pump every frame.
    public class NetDriver : MonoBehaviour
    {
        void Update() { Net.Pump(); }
        void OnApplicationQuit() { Net.Leave(); }
    }
}
