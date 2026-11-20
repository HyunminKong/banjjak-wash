using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Banjjak.Net
{
    /// <summary>
    /// Minimal Supabase Realtime client (Phoenix channels, serializer 1.0.0 = JSON objects) for one channel at a time:
    /// join with presence, heartbeat, broadcast send/receive, presence join/leave tracking.
    /// Every broadcast counts once plus once per receiver against the project's message quota, so callers batch.
    /// </summary>
    public class Realtime
    {
        public string Topic { get; private set; }
        public bool Joined { get; private set; }
        public string Error { get; private set; }
        public bool Closed => sock == null || sock.Status == NetSocket.State.Closed;

        /// <summary>(event, payload) for each broadcast from someone else.</summary>
        public event Action<string, Dictionary<string, object>> OnBroadcast;
        /// <summary>(key, meta) when someone's presence appears or updates, (key, null) when they leave.</summary>
        public event Action<string, Dictionary<string, object>> OnPresence;

        NetSocket sock;
        int refN = 1;
        string joinRef;
        float heartbeatT;
        string presenceKey, presenceJson;
        public readonly Dictionary<string, Dictionary<string, object>> Present = new Dictionary<string, Dictionary<string, object>>();

        public void Connect(string channel, string myKey, string myPresenceJson)
        {
            Close();
            Error = null;
            Topic = "realtime:" + channel;
            presenceKey = myKey;
            presenceJson = myPresenceJson;
            var url = CloudConfig.Url.Replace("https://", "wss://").TrimEnd('/') + "/realtime/v1/websocket?apikey=" + CloudConfig.Key + "&vsn=1.0.0";
            sock = new NetSocket(url);
            joinSent = false;
        }

        bool joinSent;

        public void Close()
        {
            if (sock != null)
            {
                if (Joined) Send(Topic, "phx_leave", "{}");
                sock.Close();
            }
            sock = null; Joined = false; joinSent = false;
            Present.Clear();
        }

        string NextRef() => (refN++).ToString();

        void Send(string topic, string ev, string payloadJson, string jref = null, string msgRef = null)
        {
            if (sock == null) return;
            var sb = new StringBuilder(64 + payloadJson.Length);
            sb.Append("{\"topic\":").Append(MiniJson.Quote(topic))
              .Append(",\"event\":").Append(MiniJson.Quote(ev))
              .Append(",\"payload\":").Append(payloadJson)
              .Append(",\"ref\":").Append(MiniJson.Quote(msgRef ?? NextRef()));
            if (jref != null) sb.Append(",\"join_ref\":").Append(MiniJson.Quote(jref));
            sb.Append('}');
            sock.Send(sb.ToString());
        }

        public void Broadcast(string ev, string payloadJson)
        {
            if (!Joined) return;
            Send(Topic, "broadcast", "{\"type\":\"broadcast\",\"event\":" + MiniJson.Quote(ev) + ",\"payload\":" + payloadJson + "}", joinRef);
        }

        public void Track(string presenceJsonNow)
        {
            presenceJson = presenceJsonNow;
            if (Joined) Send(Topic, "presence", "{\"type\":\"presence\",\"event\":\"track\",\"payload\":" + presenceJson + "}", joinRef);
        }

        /// <summary>Call every frame.</summary>
        public void Tick(float dt)
        {
            if (sock == null) return;
            if (sock.Status == NetSocket.State.Closed) { if (Error == null) Error = "connection closed"; Joined = false; return; }
            if (sock.Status != NetSocket.State.Open) return;
            if (!joinSent)
            {
                joinSent = true;
                joinRef = NextRef();
                var cfg = "{\"config\":{\"broadcast\":{\"self\":false,\"ack\":false},\"presence\":{\"key\":" + MiniJson.Quote(presenceKey) + ",\"enabled\":true},\"private\":false}}";
                // the reply to the join carries this ref, so the join's ref and join_ref are the same number
                Send(Topic, "phx_join", cfg, joinRef, joinRef);
            }
            heartbeatT += dt;
            if (heartbeatT > 20) { heartbeatT = 0; Send("phoenix", "heartbeat", "{}"); }
            // a handler may close this channel (leaving the room), so re-check the socket every time
            int guard = 0;
            while (guard++ < 200 && sock != null && sock.TryRecv(out var raw)) Handle(raw);
        }

        void Handle(string raw)
        {
            var msg = MiniJson.Parse(raw) as Dictionary<string, object>;
            if (msg == null) return;
            string ev = MiniJson.Str(msg, "event"), topic = MiniJson.Str(msg, "topic");
            var payload = MiniJson.Obj(msg, "payload");
            if (topic != Topic) return;
            switch (ev)
            {
                case "phx_reply":
                    if (!Joined && MiniJson.Str(msg, "ref") == joinRef)
                    {
                        if (MiniJson.Str(payload, "status") == "ok")
                        {
                            Joined = true;
                            Track(presenceJson);
                        }
                        else
                        {
                            var resp = MiniJson.Obj(payload, "response");
                            Error = "join failed: " + (MiniJson.Str(resp, "reason") ?? "unknown");
                            Debug.LogWarning("[Banjjak] realtime " + Error);
                        }
                    }
                    break;
                case "phx_error":
                case "phx_close":
                    Joined = false;
                    Error = ev;
                    break;
                case "broadcast":
                    {
                        var inner = MiniJson.Obj(payload, "payload");
                        OnBroadcast?.Invoke(MiniJson.Str(payload, "event"), inner);
                        break;
                    }
                case "presence_state":
                    if (payload != null)
                        foreach (var kv in payload) PresenceJoin(kv.Key, kv.Value);
                    break;
                case "presence_diff":
                    {
                        var joins = MiniJson.Obj(payload, "joins");
                        var leaves = MiniJson.Obj(payload, "leaves");
                        if (leaves != null) foreach (var kv in leaves) if (joins == null || !joins.ContainsKey(kv.Key)) { Present.Remove(kv.Key); OnPresence?.Invoke(kv.Key, null); }
                        if (joins != null) foreach (var kv in joins) PresenceJoin(kv.Key, kv.Value);
                        break;
                    }
            }
        }

        void PresenceJoin(string key, object entry)
        {
            var metas = MiniJson.Arr(entry, "metas");
            if (metas == null || metas.Count == 0) return;
            var meta = metas[metas.Count - 1] as Dictionary<string, object>;
            if (meta == null) return;
            Present[key] = meta;
            OnPresence?.Invoke(key, meta);
        }
    }
}
