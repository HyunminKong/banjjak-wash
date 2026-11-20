using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Banjjak.Net
{
    /// <summary>
    /// A text WebSocket that works in the browser (through BanjjakWs.jslib) and in the desktop build
    /// (System.Net.WebSockets, used by the automated co-op test). Received messages are polled with TryRecv.
    /// </summary>
    public class NetSocket
    {
        public enum State { Connecting, Open, Closed }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int BjWsOpen(string url);
        [DllImport("__Internal")] static extern int BjWsState(int id);
        [DllImport("__Internal")] static extern int BjWsSend(int id, string msg);
        [DllImport("__Internal")] static extern string BjWsRecv(int id);
        [DllImport("__Internal")] static extern void BjWsClose(int id);
        [DllImport("__Internal")] static extern string BjPageQuery();
        int id;

        public NetSocket(string url) { id = BjWsOpen(url); }
        public State Status { get { int s = BjWsState(id); return s == 1 ? State.Open : s == 0 ? State.Connecting : State.Closed; } }
        public void Send(string msg) { if (Status == State.Open) BjWsSend(id, msg); }
        public bool TryRecv(out string msg) { msg = BjWsRecv(id); return msg != null; }
        public void Close() { BjWsClose(id); }
        public static string PageQuery() { try { return BjPageQuery() ?? ""; } catch { return ""; } }
#else
        readonly System.Net.WebSockets.ClientWebSocket ws = new System.Net.WebSockets.ClientWebSocket();
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        volatile State state = State.Connecting;

        public NetSocket(string url)
        {
            Run(url);
        }

        async void Run(string url)
        {
            try
            {
                await ws.ConnectAsync(new Uri(url), cts.Token);
                state = State.Open;
                Pump();
                var buf = new byte[64 * 1024];
                var sb = new StringBuilder();
                while (ws.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                    if (r.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) break;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                    if (r.EndOfMessage) { inbox.Enqueue(sb.ToString()); sb.Clear(); }
                }
            }
            catch (Exception e) { if (!cts.IsCancellationRequested) Debug.LogWarning("[Banjjak] socket: " + e.Message); }
            state = State.Closed;
        }

        // one sender at a time: ClientWebSocket allows a single outstanding SendAsync
        async void Pump()
        {
            while (!cts.IsCancellationRequested && ws.State == System.Net.WebSockets.WebSocketState.Open)
            {
                if (outbox.TryDequeue(out var m))
                {
                    try { await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(m)), System.Net.WebSockets.WebSocketMessageType.Text, true, cts.Token); }
                    catch { break; }
                }
                else await System.Threading.Tasks.Task.Delay(5);
            }
        }

        public State Status => state;
        public void Send(string msg) { if (state == State.Open) outbox.Enqueue(msg); }
        public bool TryRecv(out string msg) => inbox.TryDequeue(out msg);
        public void Close()
        {
            state = State.Closed;
            try { cts.Cancel(); ws.Abort(); } catch { }
        }
        public static string PageQuery() => "";
#endif
    }
}
