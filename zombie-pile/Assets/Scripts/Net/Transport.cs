using System;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace ZombiePile
{
    public enum LinkState { Idle, Connecting, Open, Closed, Failed }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// WebGL: the browser's WebSocket through Assets/Plugins/WebGL/ZPWebSocket.jslib.
    public class Transport
    {
        [DllImport("__Internal")] static extern void ZP_WS_Open(string url);
        [DllImport("__Internal")] static extern int ZP_WS_State();
        [DllImport("__Internal")] static extern void ZP_WS_Send(string msg);
        [DllImport("__Internal")] static extern int ZP_WS_Pending();
        [DllImport("__Internal")] static extern IntPtr ZP_WS_Pop();
        [DllImport("__Internal")] static extern void ZP_WS_Free(IntPtr p);
        [DllImport("__Internal")] static extern void ZP_WS_Close();

        public LinkState State
        {
            get
            {
                switch (ZP_WS_State())
                {
                    case 1: return LinkState.Connecting;
                    case 2: return LinkState.Open;
                    case 3: return LinkState.Closed;
                    case 4: return LinkState.Failed;
                    default: return LinkState.Idle;
                }
            }
        }

        public void Connect(string url) { ZP_WS_Open(url); }
        public void Send(string s) { ZP_WS_Send(s); }
        public void Close() { ZP_WS_Close(); }

        public bool TryReceive(out string s)
        {
            if (ZP_WS_Pending() > 0)
            {
                var p = ZP_WS_Pop();
                s = Marshal.PtrToStringUTF8(p);
                ZP_WS_Free(p);
                return true;
            }
            s = null;
            return false;
        }
    }
#else
    /// Editor and standalone builds: System.Net.WebSockets on background tasks, queues to the main thread.
    public class Transport
    {
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
        ClientWebSocket ws;
        CancellationTokenSource cts;
        volatile LinkState state = LinkState.Idle;

        public LinkState State { get { return state; } }

        public void Connect(string url)
        {
            Close();
            string m;
            while (inbox.TryDequeue(out m)) { }
            while (outbox.TryDequeue(out m)) { }
            state = LinkState.Connecting;
            cts = new CancellationTokenSource();
            var ct = cts.Token;
            var socket = new ClientWebSocket();
            ws = socket;
            Run(socket, url, ct);
        }

        public void Send(string s) { outbox.Enqueue(s); }
        public bool TryReceive(out string s) { return inbox.TryDequeue(out s); }

        public void Close()
        {
            if (cts != null) { cts.Cancel(); cts = null; }
            if (ws != null) { try { ws.Dispose(); } catch (Exception) { } ws = null; }
            state = LinkState.Idle;
        }

        async void Run(ClientWebSocket socket, string url, CancellationToken ct)
        {
            try
            {
                await socket.ConnectAsync(new Uri(url), ct);
                if (ct.IsCancellationRequested) return;
                state = LinkState.Open;
                var recv = ReceiveLoop(socket, ct);
                var send = SendLoop(socket, ct);
                await Task.WhenAny(recv, send);
                if (!ct.IsCancellationRequested) state = LinkState.Closed;
            }
            catch (Exception)
            {
                if (!ct.IsCancellationRequested) state = state == LinkState.Open ? LinkState.Closed : LinkState.Failed;
            }
        }

        async Task ReceiveLoop(ClientWebSocket socket, CancellationToken ct)
        {
            var buf = new byte[16384];
            var sb = new StringBuilder();
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var r = await socket.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                if (r.MessageType == WebSocketMessageType.Close) return;
                sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                if (r.EndOfMessage) { inbox.Enqueue(sb.ToString()); sb.Length = 0; }
            }
        }

        async Task SendLoop(ClientWebSocket socket, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                string m;
                if (outbox.TryDequeue(out m))
                {
                    var b = Encoding.UTF8.GetBytes(m);
                    await socket.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, ct);
                }
                else await Task.Delay(4, ct);
            }
        }
    }
#endif
}
