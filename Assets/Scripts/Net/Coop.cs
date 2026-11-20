using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Banjjak.Net
{
    /// <summary>
    /// Co-op rooms over Supabase Realtime (up to 4 players). The host's job is the room's job; guests rebuild it
    /// fresh and take a snapshot of the host's progress, then everyone streams what they change.
    ///
    /// Dirt only ever goes down, so each player sends "cell k of surface s is now at level q" and everyone keeps the
    /// smaller value: all screens converge regardless of message order or who joined when. Mowing, weeding and trash
    /// pickup are one-way too and travel the same way. Finished surfaces are exchanged every few seconds as a backstop.
    /// One broadcast every 0.25 s per player carries everything (each broadcast costs 1 + receivers messages of the
    /// project's realtime quota), and almost nothing is sent while a player stands still.
    /// </summary>
    public class Coop : MonoBehaviour
    {
        public static Coop I;
        public enum Role { None, Host, Guest }

        public Role Mode { get; private set; }
        public string Code { get; private set; }
        public string Status { get; private set; } = "";
        public bool Connected => rt != null && rt.Joined;
        public bool IsHost => Mode == Role.Host;
        public bool IsGuest => Mode == Role.Guest;
        public bool Active => Mode != Role.None;
        public const int MaxPlayers = 4;

        public string MyKey { get; private set; }
        string hostKey, fixedCode;
        Realtime rt;
        readonly Dictionary<string, Avatar> avatars = new Dictionary<string, Avatar>();
        // friends' robots and scissor lifts, shown only (they act on their owner's machine)
        readonly Dictionary<string, List<Robot>> ghostBots = new Dictionary<string, List<Robot>>();
        readonly Dictionary<string, Lift> ghostLifts = new Dictionary<string, Lift>();
        string lastBots = "", lastLift = "";
        float tickT, keepT, doneT, joinT, snapT;
        bool synced, waitingSnap;
        Vector3 lastFeet; float lastYaw, lastPitch; int lastTool = -1; bool lastSpray;
        readonly Dictionary<int, string> snapParts = new Dictionary<int, string>();
        int snapN;
        static string guestName;

        GameManager gm => GameManager.I;

        void Awake() { I = this; }

        // ------------------------------------------------------------------ who am I
        public string MyName()
        {
            if (gm.Acct.LoggedIn && !string.IsNullOrEmpty(gm.Acct.Username)) return gm.Acct.Username;
            if (guestName == null) guestName = "손님" + UnityEngine.Random.Range(100, 1000);
            return guestName;
        }

        public Skins.Look MyLook() => Skins.Look.Parse(gm.Save.Look);

        string PresenceJson() =>
            "{\"n\":" + MiniJson.Quote(MyName()) + ",\"h\":" + (IsHost ? 1 : 0) + ",\"s\":" + MiniJson.Quote(MyLook().ToString()) + "}";

        /// <summary>Names of everyone in the room, me first.</summary>
        public List<string> Names()
        {
            var l = new List<string> { MyName() + (IsHost ? " (방장)" : "") };
            if (rt != null)
                foreach (var kv in rt.Present)
                    if (kv.Key != MyKey) l.Add((MiniJson.Str(kv.Value, "n") ?? "친구") + (MiniJson.I(kv.Value, "h") == 1 ? " (방장)" : ""));
            return l;
        }

        // ------------------------------------------------------------------ rooms
        public void Host(string code = null)
        {
            if (!CloudConfig.Enabled) { gm.Toast("같이 하기를 쓸 수 없어요", "서버가 연결되지 않았어요", ToastKind.Info); return; }
            Leave(null, false);
            fixedCode = code;
            Mode = Role.Host;
            Code = code ?? UnityEngine.Random.Range(1000, 10000).ToString();
            hostKey = null;
            Connect();
            Status = "방을 만드는 중…";
        }

        public void Join(string code)
        {
            if (!CloudConfig.Enabled) { gm.Toast("같이 하기를 쓸 수 없어요", "서버가 연결되지 않았어요", ToastKind.Info); return; }
            code = (code ?? "").Trim();
            if (code.Length != 4 || !int.TryParse(code, out _)) { Status = "코드는 숫자 4자리예요"; return; }
            Leave(null, false);
            Mode = Role.Guest;
            Code = code;
            hostKey = null;
            synced = false;
            Connect();
            Status = "방을 찾는 중…";
        }

        void Connect()
        {
            MyKey = Guid.NewGuid().ToString("N").Substring(0, 10);
            rt = new Realtime();
            rt.OnBroadcast += OnBroadcast;
            rt.OnPresence += OnPresence;
            rt.Connect("bj-" + Code, MyKey, PresenceJson());
            joinT = 0; tickT = 0; keepT = 0; doneT = 0;
            BeginTracking();
        }

        /// <summary>Leave the room. Guests go back to their own saved progress of the current job.</summary>
        public void Leave(string reason = null, bool restore = true)
        {
            if (Mode == Role.None && rt == null) return;
            bool wasGuest = IsGuest;
            rt?.Close();
            rt = null;
            foreach (var a in avatars.Values) if (a != null) Destroy(a.gameObject);
            avatars.Clear();
            ClearGhosts(null);
            Mode = Role.None;
            NetHooks.Track = false;
            NetHooks.Clear();
            snapParts.Clear(); waitingSnap = false; synced = false;
            Status = reason ?? "";
            if (reason != null) gm.Toast(reason, "", ToastKind.Info);
            if (restore && wasGuest && gm.Job != null) gm.StartJob(gm.JobIndex);
            gm.Hud?.RefreshCoop();
        }

        /// <summary>The look changed in 꾸미기: tell the room.</summary>
        public void RefreshPresence() { if (rt != null && rt.Joined) rt.Track(PresenceJson()); }

        // ------------------------------------------------------------------ job changes
        /// <summary>Called by GameManager after every StartJob.</summary>
        public void OnJobStarted()
        {
            if (!Active) return;
            BeginTracking();
            foreach (var a in avatars.Values) if (a != null) a.gameObject.SetActive(false);
            ClearGhosts(null);
            lastBots = lastLift = "";
        }

        /// <summary>Remove one friend's (or, with null, everyone's) robots and lift.</summary>
        void ClearGhosts(string key)
        {
            foreach (var k in new List<string>(ghostBots.Keys))
                if (key == null || k == key) { foreach (var r in ghostBots[k]) if (r != null) Destroy(r.gameObject); ghostBots.Remove(k); }
            foreach (var k in new List<string>(ghostLifts.Keys))
                if (key == null || k == key) { if (ghostLifts[k] != null) Destroy(ghostLifts[k].gameObject); ghostLifts.Remove(k); }
        }

        /// <summary>"type:x:y:z:yaw;..." for my robots, as they are right now.</summary>
        static string BotsState()
        {
            var sb = new StringBuilder();
            var seen = new HashSet<Robot>();
            void Add(Robot r)
            {
                if (r == null || !seen.Add(r)) return;
                var p = r.transform.position;
                sb.Append(r.Type).Append(':').Append(MiniJson.Num(p.x)).Append(':').Append(MiniJson.Num(p.y)).Append(':').Append(MiniJson.Num(p.z))
                  .Append(':').Append(MiniJson.Num(r.transform.eulerAngles.y)).Append(';');
            }
            foreach (var r in Robots.Active) Add(r);
            Add(Robots.AutoMower);
            return sb.ToString();
        }

        static string LiftState()
        {
            var l = Lift.Active;
            if (l == null) return "";
            var p = l.transform.position;
            return MiniJson.Num(p.x) + "," + MiniJson.Num(p.y) + "," + MiniJson.Num(p.z) + "," + MiniJson.Num(l.transform.eulerAngles.y) + "," + MiniJson.Num(l.H);
        }

        void SyncGhostBots(string key, string state)
        {
            if (!ghostBots.TryGetValue(key, out var list)) ghostBots[key] = list = new List<Robot>();
            var items = string.IsNullOrEmpty(state) ? new string[0] : state.TrimEnd(';').Split(';');
            // rebuild when the set of robots changed, otherwise just move them
            bool same = items.Length == list.Count;
            for (int i = 0; same && i < items.Length; i++) if (list[i] == null || list[i].Type != items[i].Split(':')[0]) same = false;
            if (!same)
            {
                foreach (var r in list) if (r != null) Destroy(r.gameObject);
                list.Clear();
            }
            for (int i = 0; i < items.Length; i++)
            {
                var f = items[i].Split(':');
                if (f.Length < 5) continue;
                float.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
                float.TryParse(f[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
                float.TryParse(f[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
                float.TryParse(f[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float yaw);
                var p = new Vector3(x, y, z);
                if (!same)
                {
                    var go = new GameObject("FriendBot_" + f[0]);
                    var r = go.AddComponent<Robot>();
                    r.Ghost = true;
                    r.Init(f[0], p, yaw * Mathf.Deg2Rad);
                    go.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    list.Add(r);
                }
                list[i].SetGhostTarget(p, yaw);
            }
        }

        void SyncGhostLift(string key, string state)
        {
            ghostLifts.TryGetValue(key, out var l);
            if (string.IsNullOrEmpty(state)) { if (l != null) Destroy(l.gameObject); ghostLifts.Remove(key); return; }
            var f = state.Split(',');
            if (f.Length < 5) return;
            var v = new float[5];
            for (int i = 0; i < 5; i++) float.TryParse(f[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v[i]);
            if (l == null) ghostLifts[key] = l = Lift.Ghost();
            l.transform.SetPositionAndRotation(new Vector3(v[0], v[1], v[2]), Quaternion.Euler(0, v[3], 0));
            l.SetHeight(v[4]);
        }

        void BeginTracking()
        {
            NetHooks.Clear();
            NetHooks.Track = gm.Job != null;
            if (gm.Job != null) foreach (var s in gm.Job.Surfs) s.InitSent();
        }

        void FollowHost(string jobId, bool force)
        {
            int idx = JobDefs.IndexOf(jobId);
            if (idx < 0) return;
            if (force || gm.Job == null || gm.Job.Id != jobId)
            {
                // a clean build of the host's job, then the host's progress on top (never the guest's own save)
                gm.StartJob(idx, false, true);
                gm.Toast("방장의 현장으로 이동했어요", JobDefs.All[idx].Name, ToastKind.Info);
            }
            RequestSnapshot();
        }

        void RequestSnapshot()
        {
            if (rt == null || hostKey == null) return;
            waitingSnap = true; snapT = 0;
            snapParts.Clear(); snapN = 0;
            rt.Broadcast("sq", "{\"k\":" + MiniJson.Quote(MyKey) + ",\"to\":" + MiniJson.Quote(hostKey) + ",\"j\":" + MiniJson.Quote(gm.Job.Id) + "}");
        }

        void SendSnapshot(string to)
        {
            if (gm.Job == null) return;
            var b64 = Convert.ToBase64String(gm.Job.Serialize());
            const int part = 150000;
            int n = (b64.Length + part - 1) / part;
            for (int i = 0; i < n; i++)
            {
                var chunk = b64.Substring(i * part, Mathf.Min(part, b64.Length - i * part));
                rt.Broadcast("sp", "{\"to\":" + MiniJson.Quote(to) + ",\"j\":" + MiniJson.Quote(gm.Job.Id) + ",\"i\":" + i + ",\"n\":" + n + ",\"d\":\"" + chunk + "\"}");
            }
        }

        void OnSnapshotPart(Dictionary<string, object> p)
        {
            if (!waitingSnap || MiniJson.Str(p, "to") != MyKey || gm.Job == null || MiniJson.Str(p, "j") != gm.Job.Id) return;
            snapN = MiniJson.I(p, "n");
            snapParts[MiniJson.I(p, "i")] = MiniJson.Str(p, "d");
            if (snapParts.Count < snapN) return;
            var sb = new StringBuilder();
            for (int i = 0; i < snapN; i++) { if (!snapParts.TryGetValue(i, out var s)) return; sb.Append(s); }
            waitingSnap = false;
            snapParts.Clear();
            try
            {
                NetHooks.Remote = true;
                bool ok = gm.Job.Apply(Convert.FromBase64String(sb.ToString()));
                gm.Job.SettleAfterLoad(false);   // parts the others finished before I came don't pay me
                NetHooks.Remote = false;
                foreach (var s in gm.Job.Surfs) s.InitSent();
                if (ok) gm.Toast("방장의 진행도를 받았어요", $"진행도 {Mathf.FloorToInt(gm.Job.Progress * 100)}%", ToastKind.Info);
            }
            catch (Exception e) { NetHooks.Remote = false; Debug.LogWarning("[Banjjak] snapshot: " + e.Message); }
        }

        // ------------------------------------------------------------------ network in
        void OnPresence(string key, Dictionary<string, object> meta)
        {
            if (key == MyKey) return;
            if (meta == null)
            {
                if (avatars.TryGetValue(key, out var gone)) { if (gone != null) { gm.Toast($"{gone.DisplayName} 님이 나갔어요", "", ToastKind.Info); Destroy(gone.gameObject); } avatars.Remove(key); }
                ClearGhosts(key);
                if (key == hostKey && IsGuest) Leave("방장이 나가서 방이 닫혔어요");
                gm.Hud?.RefreshCoop();
                return;
            }
            bool isHost = MiniJson.I(meta, "h") == 1;
            if (isHost && IsHost && key != MyKey)
            {
                // someone else already hosts this code: pick another one
                if (fixedCode == null) { Host(); return; }
            }
            if (isHost) hostKey = key;
            var name = MiniJson.Str(meta, "n") ?? "친구";
            var look = Skins.Look.Parse(MiniJson.Str(meta, "s"));
            if (!avatars.TryGetValue(key, out var av) || av == null)
            {
                if (avatars.Count >= MaxPlayers - 1) return;
                av = Avatar.Create(key, name, look);
                av.gameObject.SetActive(false);   // shown once their first update says they're on my job
                avatars[key] = av;
                gm.Toast($"{name} 님이 들어왔어요", "", ToastKind.Info);
            }
            else av.SetLook(look);
            gm.Hud?.RefreshCoop();
        }

        void OnBroadcast(string ev, Dictionary<string, object> p)
        {
            if (p == null) return;
            switch (ev)
            {
                case "t": OnTick(p); break;
                case "sq": if (IsHost && MiniJson.Str(p, "to") == MyKey) SendSnapshot(MiniJson.Str(p, "k")); break;
                case "sp": OnSnapshotPart(p); break;
            }
        }

        void OnTick(Dictionary<string, object> p)
        {
            string k = MiniJson.Str(p, "k"), j = MiniJson.Str(p, "j");
            if (k == null || k == MyKey) return;
            if (IsGuest && k == hostKey && j != null && (!synced || gm.Job == null || gm.Job.Id != j)) { FollowHost(j, !synced); synced = true; }

            avatars.TryGetValue(k, out var av);
            bool sameJob = gm.Job != null && j == gm.Job.Id;
            if (av != null)
            {
                av.gameObject.SetActive(sameJob);
                if (sameJob)
                {
                    av.Push(new Vector3(MiniJson.F(p, "x"), MiniJson.F(p, "y"), MiniJson.F(p, "z")), MiniJson.F(p, "yw"), MiniJson.F(p, "pt"), MiniJson.I(p, "cr") == 1);
                    av.Tool = MiniJson.I(p, "tl"); av.Spraying = MiniJson.I(p, "sp") == 1; av.Deg = MiniJson.I(p, "dg");
                    av.Turbo = MiniJson.I(p, "tb") == 1; av.FanV = MiniJson.I(p, "fv") == 1; av.Agent = MiniJson.I(p, "ag");
                }
            }
            if (!sameJob) { ClearGhosts(k); return; }
            SyncGhostBots(k, MiniJson.Str(p, "rb"));
            SyncGhostLift(k, MiniJson.Str(p, "lf"));
            ApplyChanges(p);
        }

        void ApplyChanges(Dictionary<string, object> p)
        {
            var job = gm.Job;
            NetHooks.Remote = true;
            try
            {
                var c = MiniJson.Str(p, "c");
                if (!string.IsNullOrEmpty(c))
                {
                    var b = Convert.FromBase64String(c);
                    var hit = new HashSet<DirtSurface>();
                    for (int i = 0; i + 6 <= b.Length; i += 6)
                    {
                        int si = b[i] | b[i + 1] << 8;
                        if (si >= job.Surfs.Count) continue;
                        var s = job.Surfs[si];
                        s.ApplyRemote(b[i + 2] | b[i + 3] << 8 | b[i + 4] << 16, b[i + 5]);
                        hit.Add(s);
                    }
                    foreach (var s in hit) s.EndRemote();
                }
                var f = MiniJson.Str(p, "f");
                if (!string.IsNullOrEmpty(f))
                {
                    var b = Convert.FromBase64String(f);
                    for (int i = 0; i + 23 <= b.Length; i += 23)
                    {
                        int si = b[i] | b[i + 1] << 8;
                        if (si >= job.Surfs.Count) continue;
                        var pt = new Vector3(BitConverter.ToSingle(b, i + 2), BitConverter.ToSingle(b, i + 6), BitConverter.ToSingle(b, i + 10));
                        job.Surfs[si].StampFoam(pt, BitConverter.ToSingle(b, i + 14), BitConverter.ToSingle(b, i + 18), b[i + 22]);
                    }
                }
                var rs = MiniJson.Str(p, "rs");
                if (!string.IsNullOrEmpty(rs))
                {
                    var b = Convert.FromBase64String(rs);
                    for (int i = 0; i + 18 <= b.Length; i += 18)
                    {
                        int si = b[i] | b[i + 1] << 8;
                        if (si >= job.Surfs.Count) continue;
                        var pt = new Vector3(BitConverter.ToSingle(b, i + 2), BitConverter.ToSingle(b, i + 6), BitConverter.ToSingle(b, i + 10));
                        job.Surfs[si].RinseRemote(pt, BitConverter.ToSingle(b, i + 14));
                    }
                }
                var g = MiniJson.Str(p, "g");
                if (!string.IsNullOrEmpty(g) && job.Grass != null)
                {
                    var b = Convert.FromBase64String(g);
                    for (int i = 0; i + 3 <= b.Length; i += 3) job.Grass.CutIdx(b[i] | b[i + 1] << 8 | b[i + 2] << 16);
                }
                foreach (int i in Ints(MiniJson.Str(p, "w"))) job.Weeds?.Pull(i);
                foreach (int i in Ints(MiniJson.Str(p, "r"))) job.Trash?.Pick(i);
                var d = MiniJson.Str(p, "d");
                if (!string.IsNullOrEmpty(d))
                {
                    var b = Convert.FromBase64String(d);
                    for (int i = 0; i < job.Surfs.Count && (i >> 3) < b.Length; i++)
                        if ((b[i >> 3] & (1 << (i & 7))) != 0 && !job.Surfs[i].Done) job.FinishSurf(job.Surfs[i]);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Banjjak] co-op update: " + e.Message); }
            finally { NetHooks.Remote = false; }
        }

        static IEnumerable<int> Ints(string csv)
        {
            if (string.IsNullOrEmpty(csv)) yield break;
            foreach (var s in csv.Split(',')) if (int.TryParse(s, out int v)) yield return v;
        }

        // ------------------------------------------------------------------ network out
        void Update()
        {
            if (rt == null) return;
            float dt = Time.unscaledDeltaTime;
            rt.Tick(dt);
            if (rt == null) return;   // a message made us leave the room (the host left, or a new code was picked)
            joinT += dt;
            if (!rt.Joined)
            {
                if (rt.Error != null || joinT > 10) Leave(joinT > 10 && rt.Error == null ? "같이 하기 서버에 연결하지 못했어요" : "같이 하기 연결이 끊겼어요");
                return;
            }
            if (IsHost) Status = $"방 코드 {Code}";
            else if (hostKey == null) { Status = "방을 찾는 중…"; if (joinT > 6) { Leave("그 코드의 방을 찾지 못했어요"); return; } }
            else Status = waitingSnap ? "방장의 진행도를 받는 중…" : $"방 코드 {Code}";
            if (waitingSnap) { snapT += dt; if (snapT > 8) RequestSnapshot(); }

            tickT += dt; keepT += dt; doneT += dt;
            if (tickT < .25f || gm.Job == null || gm.Player == null) return;
            var pl = gm.Player;
            var feet = pl.transform.position;
            var w = gm.Washer;
            bool spray = w != null && w.Spraying;
            bool moved = (feet - lastFeet).sqrMagnitude > .0004f || Mathf.Abs(Mathf.DeltaAngle(pl.Yaw, lastYaw)) > 1 || Mathf.Abs(pl.Pitch - lastPitch) > 1
                         || spray != lastSpray || (w != null && w.Tool != lastTool);
            bool pending = NetHooks.Cuts.Count + NetHooks.Pulls.Count + NetHooks.Picks.Count + NetHooks.Foams.Count + NetHooks.Rinses.Count > 0 || AnyTouched();
            // my robots keep moving, and a lift going up or away has to show on the others' screens too
            string bots = BotsState(), lift = LiftState();
            bool gear = bots.Length > 0 || bots != lastBots || lift != lastLift;
            if (!moved && !pending && !spray && !gear && keepT < 2 && doneT < 3) return;
            tickT = 0; keepT = 0;
            lastFeet = feet; lastYaw = pl.Yaw; lastPitch = pl.Pitch; lastSpray = spray; lastTool = w != null ? w.Tool : 0;
            lastBots = bots; lastLift = lift;
            rt.Broadcast("t", BuildTick(pl, w, spray, bots, lift));
        }

        bool AnyTouched()
        {
            foreach (var s in gm.Job.Surfs) if (s.Touched != null && s.Touched.Count > 0) return true;
            return false;
        }

        string BuildTick(PlayerController pl, WasherTool w, bool spray, string bots, string lift)
        {
            var sb = new StringBuilder(256);
            var f = pl.transform.position;
            sb.Append("{\"k\":").Append(MiniJson.Quote(MyKey)).Append(",\"j\":").Append(MiniJson.Quote(gm.Job.Id))
              .Append(",\"x\":").Append(MiniJson.Num(f.x)).Append(",\"y\":").Append(MiniJson.Num(f.y)).Append(",\"z\":").Append(MiniJson.Num(f.z))
              .Append(",\"yw\":").Append(MiniJson.Num(pl.Yaw)).Append(",\"pt\":").Append(MiniJson.Num(pl.Pitch))
              .Append(",\"cr\":").Append(pl.Crouch ? 1 : 0)
              .Append(",\"tl\":").Append(w != null ? w.Tool : 0).Append(",\"sp\":").Append(spray ? 1 : 0)
              .Append(",\"dg\":").Append(w != null ? w.Deg : 0).Append(",\"tb\":").Append(w != null && w.Turbo ? 1 : 0)
              .Append(",\"fv\":").Append(w != null && w.FanVertical ? 1 : 0).Append(",\"ag\":").Append(gm.Save.Agent);

            // cleaned cells: (surface u16, cell u24, level u8), only levels lower than what was last sent
            var cells = new List<byte>();
            foreach (var s in gm.Job.Surfs)
            {
                if (s.Touched == null || s.Touched.Count == 0 || s.SentQ == null) continue;
                if (cells.Count > 90000) break;   // the rest goes in the next update
                foreach (int k in s.Touched)
                {
                    int q = DirtSurface.Q(s.DirtAmt[k]);
                    if (q >= s.SentQ[k]) continue;
                    s.SentQ[k] = (byte)q;
                    cells.Add((byte)s.Index); cells.Add((byte)(s.Index >> 8));
                    cells.Add((byte)k); cells.Add((byte)(k >> 8)); cells.Add((byte)(k >> 16));
                    cells.Add((byte)q);
                }
                s.ClearTouched();
            }
            if (cells.Count > 0) sb.Append(",\"c\":\"").Append(Convert.ToBase64String(cells.ToArray())).Append('"');

            if (NetHooks.Foams.Count > 0)
            {
                // nearby foam stamps on one surface merge into one, which keeps a long foam spray small on the wire
                var kept = new List<NetHooks.FoamStamp>();
                foreach (var fs in NetHooks.Foams)
                {
                    int m = kept.FindLastIndex(x => x.Surf == fs.Surf && (x.P - fs.P).sqrMagnitude < .01f);
                    if (m >= 0) { var x = kept[m]; x.Amount = Mathf.Min(1, x.Amount + fs.Amount); x.R = Mathf.Max(x.R, fs.R); kept[m] = x; }
                    else if (kept.Count < 120) kept.Add(fs);
                }
                var fb = new List<byte>(kept.Count * 23);
                foreach (var fs in kept)
                {
                    fb.Add((byte)fs.Surf); fb.Add((byte)(fs.Surf >> 8));
                    fb.AddRange(BitConverter.GetBytes(fs.P.x)); fb.AddRange(BitConverter.GetBytes(fs.P.y)); fb.AddRange(BitConverter.GetBytes(fs.P.z));
                    fb.AddRange(BitConverter.GetBytes(fs.R)); fb.AddRange(BitConverter.GetBytes(fs.Amount));
                    fb.Add((byte)fs.Agent);
                }
                sb.Append(",\"f\":\"").Append(Convert.ToBase64String(fb.ToArray())).Append('"');
            }
            if (NetHooks.Rinses.Count > 0)
            {
                // water over foam: (surface u16, point 3 x f32, radius f32), nearby ones merged
                var kept = new List<NetHooks.FoamStamp>();
                foreach (var fs in NetHooks.Rinses)
                {
                    int m = kept.FindLastIndex(x => x.Surf == fs.Surf && (x.P - fs.P).sqrMagnitude < .015f);
                    if (m >= 0) { var x = kept[m]; x.R = Mathf.Max(x.R, fs.R); kept[m] = x; }
                    else if (kept.Count < 160) kept.Add(fs);
                }
                var rb = new List<byte>(kept.Count * 18);
                foreach (var fs in kept)
                {
                    rb.Add((byte)fs.Surf); rb.Add((byte)(fs.Surf >> 8));
                    rb.AddRange(BitConverter.GetBytes(fs.P.x)); rb.AddRange(BitConverter.GetBytes(fs.P.y)); rb.AddRange(BitConverter.GetBytes(fs.P.z));
                    rb.AddRange(BitConverter.GetBytes(fs.R));
                }
                sb.Append(",\"rs\":\"").Append(Convert.ToBase64String(rb.ToArray())).Append('"');
            }
            if (bots.Length > 0) sb.Append(",\"rb\":").Append(MiniJson.Quote(bots));
            if (lift.Length > 0) sb.Append(",\"lf\":").Append(MiniJson.Quote(lift));
            if (NetHooks.Cuts.Count > 0)
            {
                var gb = new byte[NetHooks.Cuts.Count * 3];
                for (int i = 0; i < NetHooks.Cuts.Count; i++) { int v = NetHooks.Cuts[i]; gb[i * 3] = (byte)v; gb[i * 3 + 1] = (byte)(v >> 8); gb[i * 3 + 2] = (byte)(v >> 16); }
                sb.Append(",\"g\":\"").Append(Convert.ToBase64String(gb)).Append('"');
            }
            if (NetHooks.Pulls.Count > 0) sb.Append(",\"w\":\"").Append(string.Join(",", NetHooks.Pulls)).Append('"');
            if (NetHooks.Picks.Count > 0) sb.Append(",\"r\":\"").Append(string.Join(",", NetHooks.Picks)).Append('"');
            if (doneT >= 3)
            {
                doneT = 0;
                var surfs = gm.Job.Surfs;
                var db = new byte[(surfs.Count + 7) / 8];
                for (int i = 0; i < surfs.Count; i++) if (surfs[i].Done) db[i >> 3] |= (byte)(1 << (i & 7));
                sb.Append(",\"d\":\"").Append(Convert.ToBase64String(db)).Append('"');
            }
            NetHooks.Clear();
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Test / debug: a fingerprint of the dirt on every surface (equal on all screens once in sync).</summary>
        public static long Checksum(CleanJob job)
        {
            long sum = 0;
            foreach (var s in job.Surfs)
                for (int k = 0; k < s.N; k++)
                {
                    if (s.Surf[k] != 2) continue;
                    int q = DirtSurface.Q(s.DirtAmt[k]);
                    if (q > 2) sum += q * (long)(1 + (k % 7));   // levels 0-2 all count as clean
                }
            return sum;
        }

        public int AvatarCount => avatars.Count;
        public int GhostBotCount { get { int n = 0; foreach (var l in ghostBots.Values) n += l.Count; return n; } }
        public int GhostLiftCount => ghostLifts.Count;

        /// <summary>Test: how much foam lies on the job right now.</summary>
        public static float FoamSum(CleanJob job)
        {
            float s = 0;
            foreach (var d in job.Surfs) if (d.Foam != null) foreach (var f in d.Foam) s += f;
            return s;
        }
        public Avatar AnyAvatar() { foreach (var a in avatars.Values) if (a != null) return a; return null; }
    }
}
