using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Banjjak
{
    public class Comp
    {
        public string Name;
        public JobObject Obj;
        public DirtSpec Spec;
        public readonly List<DirtSurface> Surfs = new List<DirtSurface>();
        public int Total, Remaining;
        public bool Done;
        public float Progress => Total <= 0 ? 1f : 1f - (float)Remaining / Total;
    }

    public enum Occ { None, Box, Cyl, Sphere }

    public class MeshOpts
    {
        public bool Ground;
        public bool Double;
        public bool NoShadow;
        public bool NoRay;          // no collider at all
        public bool SprayOnly;      // spray hits it, player walks through
        public float Cell;
        public Occ Occ;
        public float OccR, OccY0, OccY1;
        public bool GroundOnly;
        public float Scale = 1;
        public static readonly MeshOpts Default = new MeshOpts();
    }

    public class Occluder
    {
        public int Owner;
        public bool GroundOnly;
        public Bounds Bounds;
        public Occ Type;
        public Vector3 C;
        public float R, Y0, Y1;

        public bool Contains(Vector3 p)
        {
            switch (Type)
            {
                case Occ.Box: return Bounds.Contains(p);
                case Occ.Cyl: return p.y >= Y0 && p.y <= Y1 && (p.x - C.x) * (p.x - C.x) + (p.z - C.z) * (p.z - C.z) < R * R;
                default: return (p - C).sqrMagnitude < R * R;
            }
        }
    }

    public class JobObject
    {
        public CleanJob Job;
        public string Name;
        public string Spec;
        public Transform T;
        public bool HighOK;
        public readonly List<Comp> Comps = new List<Comp>();
        public bool Done => Comps.TrueForAll(c => c.Done);

        public Comp Comp(string name, string spec = null)
        {
            var c = new Comp { Name = name, Obj = this, Spec = DirtSpec.Parse(spec ?? Spec) };
            Comps.Add(c);
            return c;
        }

        public GameObject Mesh(Comp comp, Mesh mesh, Material mat, Vector3 pos, Vector3 euler = default, MeshOpts o = null)
            => Mesh(comp, mesh, new[] { mat }, pos, euler, o);

        public GameObject Mesh(Comp comp, MeshData md, Material mat, Vector3 pos, Vector3 euler = default, MeshOpts o = null)
            => Mesh(comp, md.ToMesh(comp != null ? comp.Name : "deco"), new[] { mat }, pos, euler, o);

        public GameObject Mesh(Comp comp, MeshData md, Material[] mats, Vector3 pos, Vector3 euler = default, MeshOpts o = null)
            => Mesh(comp, md.ToMesh(comp != null ? comp.Name : "deco"), mats, pos, euler, o);

        public GameObject Mesh(Comp comp, Mesh mesh, Material[] mats, Vector3 pos, Vector3 euler = default, MeshOpts o = null)
        {
            o = o ?? MeshOpts.Default;
            var go = new GameObject(comp != null ? comp.Name : "deco");
            go.transform.SetParent(T, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            if (o.Scale != 1) go.transform.localScale = Vector3.one * o.Scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var use = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++) use[i] = o.Double ? Mats.DoubleSided(mats[i]) : mats[i];
            mr.sharedMaterials = use;
            mr.shadowCastingMode = o.NoShadow ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            if (!o.NoRay)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                if (o.SprayOnly) go.layer = Layers.SprayOnly;
            }
            if (comp != null)
            {
                var s = go.AddComponent<DirtSurface>();
                s.Setup(Job, comp, o.Ground, o.Cell > 0 ? o.Cell : (o.Ground ? G.GroundCell : G.Cell), Job.Surfs.Count);
                s.Index = Job.Surfs.Count;
                comp.Surfs.Add(s);
                Job.Surfs.Add(s);
            }
            if (o.Occ != Occ.None)
                Job.PendingOcc.Add((go, o.Occ, o.OccR, o.OccY0, o.OccY1, o.GroundOnly));
            return go;
        }

        public GameObject Deco(MeshData md, Material mat, Vector3 pos, Vector3 euler = default, MeshOpts o = null)
            => Mesh(null, md.ToMesh("deco"), new[] { mat }, pos, euler, o);

        public GameObject Deco(Mesh mesh, Material mat, Vector3 pos, Vector3 euler = default, MeshOpts o = null)
            => Mesh(null, mesh, new[] { mat }, pos, euler, o);

        /// <summary>Invisible collision box for the player only.</summary>
        public void Volume(Vector3 size, Vector3 pos, Vector3 euler = default, bool occ = false, bool groundOnly = false)
        {
            var go = new GameObject("volume");
            go.transform.SetParent(T, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            go.layer = Layers.PlayerOnly;
            if (occ) Job.PendingOcc.Add((go, Occ.Box, 0, 0, 0, groundOnly));
        }

        public void OccBox(Vector3 size, Vector3 pos, bool groundOnly = false)
        {
            var go = new GameObject("occ");
            go.transform.SetParent(T, false);
            go.transform.localPosition = pos;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size; bc.enabled = false;
            Job.PendingOcc.Add((go, Occ.Box, 0, 0, 0, groundOnly));
        }

        /// <summary>Occluder box that hides voxels of every surface except <paramref name="owner"/>.</summary>
        public void OccFor(GameObject owner, Vector3 size, Vector3 pos)
        {
            var go = new GameObject("occFor");
            go.transform.SetParent(T, false);
            go.transform.localPosition = pos;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size; bc.enabled = false;
            Job.OccOwner[go] = owner.GetInstanceID();
            Job.PendingOcc.Add((go, Occ.Box, 0, 0, 0, false));
        }

        /// <summary>Vertical cylinder occluder (local x/z, heights relative to the object).</summary>
        public void OccCyl(float r, float y0, float y1, float x, float z, bool groundOnly = false)
        {
            var go = new GameObject("occCyl");
            go.transform.SetParent(T, false);
            go.transform.localPosition = new Vector3(x, 0, z);
            Job.PendingOcc.Add((go, Occ.Cyl, r, y0, y1, groundOnly));
        }
    }

    /// <summary>A cleaning job: objects, components and their dirt surfaces, plus progress.</summary>
    public class CleanJob
    {
        public string Id, Name, Desc;
        public float Reward;
        public Vector3 Spawn;
        public float SpawnYaw;
        public Rect Bounds;
        public Transform Root;
        public Rng Rng;
        public readonly List<JobObject> Objects = new List<JobObject>();
        public readonly List<DirtSurface> Surfs = new List<DirtSurface>();
        public readonly List<(GameObject go, Occ type, float r, float y0, float y1, bool groundOnly)> PendingOcc = new List<(GameObject, Occ, float, float, float, bool)>();
        public readonly Dictionary<GameObject, int> OccOwner = new Dictionary<GameObject, int>();
        public readonly List<Rect> Paved = new List<Rect>();   // no grass blades here (x/z rectangles)
        public bool FreeLift;
        public float Mul = 1;                                   // reward multiplier (re-done jobs pay half)
        public readonly List<Vector3> Bins = new List<Vector3>();
        public readonly List<Bounds> NoLitter = new List<Bounds>();
        public readonly List<LitterZone> LitterZones = new List<LitterZone>();
        public GrassField Grass;
        public WeedField Weeds;
        public TrashField Trash;
        public readonly HashSet<DirtSurface> Wet = new HashSet<DirtSurface>();
        public readonly HashSet<DirtSurface> Foamy = new HashSet<DirtSurface>();
        public int Total, Remaining;
        public float Elapsed, Earned;
        public bool Completed, Changed;
        public float PayPerCell;

        public event Action<Comp, int> CompDone;
        public event Action<JobObject, int> ObjectDone;
        public event Action<int> JobDone;

        public float Progress => Total <= 0 ? 1f : 1f - (float)Remaining / Total;

        public CleanJob(string id, string name, float reward)
        {
            Id = id; Name = name; Reward = reward;
            Rng = new Rng(id);
            Root = new GameObject("Job_" + id).transform;
        }

        public JobObject Object(string name, Vector3 pos, float rotY = 0, string spec = "dust")
        {
            var o = new JobObject { Job = this, Name = name, Spec = spec };
            o.T = new GameObject(name).transform;
            o.T.SetParent(Root, false);
            o.T.localPosition = pos;
            o.T.localEulerAngles = new Vector3(0, rotY, 0);
            Objects.Add(o);
            return o;
        }

        public JobObject DecoObj(string name = "deco")
        {
            var o = new JobObject { Job = this, Name = name, Spec = "dust" };
            o.T = new GameObject(name).transform;
            o.T.SetParent(Root, false);
            return o;
        }

        /// <summary>Build occluders, sample every surface, compute totals.</summary>
        public void Finish()
        {
            Physics.SyncTransforms();
            var occ = new List<Occluder>();
            foreach (var p in PendingOcc)
            {
                int owner = p.go.GetInstanceID();
                if (p.type == Occ.Box)
                {
                    Bounds b;
                    var r = p.go.GetComponent<Renderer>();
                    if (r != null) b = r.bounds;
                    else
                    {
                        var bc = p.go.GetComponent<BoxCollider>();
                        b = TransformBounds(p.go.transform, new Bounds(bc.center, bc.size));
                    }
                    b.Expand(.04f);
                    int own = OccOwner.TryGetValue(p.go, out var ov) ? ov : (r != null ? owner : -1);
                    occ.Add(new Occluder { Owner = own, GroundOnly = p.groundOnly, Bounds = b, Type = Occ.Box });
                }
                else
                {
                    var c = p.go.transform.position;
                    float rr = p.r + .02f;
                    if (p.type == Occ.Cyl)
                    {
                        float y0 = c.y + p.y0 - .02f, y1 = c.y + p.y1 + .02f;
                        occ.Add(new Occluder { Owner = owner, GroundOnly = p.groundOnly, Type = Occ.Cyl, C = c, R = rr, Y0 = y0, Y1 = y1, Bounds = new Bounds(new Vector3(c.x, (y0 + y1) / 2, c.z), new Vector3(rr * 2, y1 - y0, rr * 2)) });
                    }
                    else
                    {
                        var rd = p.go.GetComponent<Renderer>();
                        var cc = rd != null ? rd.bounds.center : c;
                        occ.Add(new Occluder { Owner = owner, Type = Occ.Sphere, C = cc, R = rr, Bounds = new Bounds(cc, Vector3.one * rr * 2) });
                    }
                }
            }
            foreach (var s in Surfs) s.Init(occ, s.Comp.Obj.HighOK);
            if (UseReachBake) ApplyReachBake();
            ComputeTotals();
            PayPerCell = Total > 0 ? Reward * Mul * .4f / Total : 0;
        }

        /// <summary>Off only while ReachCheck bakes, so it sees every cell.</summary>
        public static bool UseReachBake = true;

        /// <summary>
        /// Leave out the dirt cells no spray line can reach from anywhere the player can stand: pressed against a wall,
        /// shut in under a ramp or a deck, the back of a door. Worked out ahead of time by ReachCheck (the automated
        /// test with -bakereach) and shipped as Resources/Reach/&lt;job id&gt;.bytes, so loading a job costs nothing extra.
        /// </summary>
        void ApplyReachBake()
        {
            var ta = Resources.Load<TextAsset>("Reach/" + Id);
            if (ta == null) return;
            using (var r = new System.IO.BinaryReader(new System.IO.MemoryStream(ta.bytes)))
            {
                if (r.ReadInt32() != ReachCheck.BakeMagic) return;
                int n = r.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    int cellsN = r.ReadInt32(), dirtCells = r.ReadInt32(), cnt = r.ReadInt32();
                    var idx = new int[cnt];
                    for (int j = 0; j < cnt; j++) idx[j] = r.ReadInt32();
                    if (i >= Surfs.Count) continue;
                    var s = Surfs[i];
                    // geometry changed since the bake: skip rather than hide the wrong cells
                    if (s.N != cellsN || Mathf.Abs(s.DirtCellCount() - dirtCells) > dirtCells * .01f + 2)
                    {
                        Debug.LogWarning($"[Banjjak] reach bake out of date for {Id} / {s.Comp.Obj.Name} / {s.Comp.Name}");
                        continue;
                    }
                    if (cnt > 0) s.HideCells(idx);
                }
            }
        }

        static Bounds TransformBounds(Transform t, Bounds local)
        {
            var c = t.TransformPoint(local.center);
            var e = local.extents;
            var ax = t.TransformVector(new Vector3(e.x, 0, 0));
            var ay = t.TransformVector(new Vector3(0, e.y, 0));
            var az = t.TransformVector(new Vector3(0, 0, e.z));
            var ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            return new Bounds(c, ext * 2);
        }

        public void ComputeTotals()
        {
            Total = 0; Remaining = 0;
            foreach (var o in Objects)
                foreach (var c in o.Comps)
                {
                    c.Total = 0; c.Remaining = 0;
                    foreach (var s in c.Surfs) { c.Total += s.Total; c.Remaining += s.Done ? 0 : s.Remaining; }
                    c.Done = c.Surfs.TrueForAll(s => s.Done);
                    if (c.Done) c.Remaining = 0;
                    Total += c.Total; Remaining += c.Remaining;
                }
        }

        public void MarkWet(DirtSurface s) => Wet.Add(s);
        public void MarkFoamy(DirtSurface s) => Foamy.Add(s);

        public void OnCleaned(DirtSurface s, int cleaned)
        {
            s.Remaining -= cleaned;
            s.Comp.Remaining -= cleaned;
            Remaining -= cleaned;
            Changed = true;
            if (!s.Done && s.Remaining <= s.Allow) FinishSurf(s);
        }

        public void FinishSurf(DirtSurface s)
        {
            int left = s.Remaining;
            s.Finish();
            s.Comp.Remaining -= left;
            Remaining -= left;
            Changed = true;
            var c = s.Comp;
            if (!c.Done && c.Surfs.TrueForAll(x => x.Done)) FinishComp(c);
        }

        void FinishComp(Comp c)
        {
            c.Done = true; c.Remaining = 0;
            int pay = Mathf.Max(3, Mathf.RoundToInt(c.Total * PayPerCell));
            Earned += pay;
            CompDone?.Invoke(c, pay);
            if (c.Obj.Done) ObjectDone?.Invoke(c.Obj, pay);
            CompleteIfAllDone();
        }

        /// <summary>Per-frame bookkeeping: drying, foam soak, texture uploads.</summary>
        float tickT;
        static readonly List<DirtSurface> tmp = new List<DirtSurface>();
        public void Tick(float dt)
        {
            if (!Completed) Elapsed += dt;
            tickT += dt;
            if (tickT >= .1f)
            {
                tickT = 0;
                tmp.Clear(); tmp.AddRange(Wet);
                foreach (var s in tmp) if (!s.TickWet()) Wet.Remove(s);
                tmp.Clear(); tmp.AddRange(Foamy);
                foreach (var s in tmp) if (!s.TickFoam()) Foamy.Remove(s);
            }
        }

        public void UploadDirty()
        {
            foreach (var s in Surfs) if (s.TexDirty) s.Upload();
        }

        // ---------- progress save: 4-bit dirt per counted voxel ----------
        // Stored either locally (desktop: gzip file, browser: PlayerPrefs/IndexedDB because GZipStream needs a
        // native library WebGL lacks) or in the player's cloud account (GameManager decides).
        static bool Web => Application.platform == RuntimePlatform.WebGLPlayer;
        static string SavePathFor(string id) => Path.Combine(Application.persistentDataPath, "job-" + id + ".bin");
        static string PrefKeyFor(string id) => "banjjak-job-" + id;

        public byte[] Serialize()
        {
            byte[] raw;
                using (var ms = new MemoryStream())
                {
                    using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
                    {
                        w.Write(8); // format
                        w.Write(Surfs.Count);
                        w.Write(Elapsed);
                        foreach (var s in Surfs)
                        {
                            w.Write(s.N);
                            w.Write(s.Done);
                            int nib = 0, cur = 0;
                            var bytes = new List<byte>(s.N / 4);
                            for (int k = 0; k < s.N; k++)
                            {
                                if (s.Surf[k] != 2) continue;
                                int q = Mathf.Clamp(Mathf.CeilToInt(s.DirtAmt[k] * 15f - .001f), 0, 15);
                                if (nib == 0) { cur = q; nib = 1; } else { bytes.Add((byte)(cur | (q << 4))); nib = 0; }
                            }
                            if (nib == 1) bytes.Add((byte)cur);
                            w.Write(bytes.Count);
                            w.Write(bytes.ToArray());
                        }
                        WriteBits(w, Grass != null ? Grass.CutState() : new bool[0]);
                        WriteBits(w, Weeds != null ? Weeds.Pulled.ToArray() : new bool[0]);
                        var picked = new bool[Trash != null ? Trash.Items.Count : 0];
                        for (int i = 0; i < picked.Length; i++) picked[i] = Trash.Items[i].Picked;
                        WriteBits(w, picked);
                    }
                    raw = ms.ToArray();
                }
            return raw;
        }

        public void SaveLocal(byte[] raw)
        {
            try
            {
                if (Web) { PlayerPrefs.SetString(PrefKeyFor(Id), Convert.ToBase64String(raw)); PlayerPrefs.Save(); }
                else
                {
                    using (var ms = new MemoryStream())
                    {
                        using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Fastest, true)) gz.Write(raw, 0, raw.Length);
                        File.WriteAllBytes(SavePathFor(Id), ms.ToArray());
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Banjjak] save failed: " + e.Message); }
        }

        public static byte[] ReadLocal(string id)
        {
            try
            {
                if (Web)
                {
                    var s = PlayerPrefs.GetString(PrefKeyFor(id), "");
                    return string.IsNullOrEmpty(s) ? null : Convert.FromBase64String(s);
                }
                if (!File.Exists(SavePathFor(id))) return null;
                using (var fs = File.OpenRead(SavePathFor(id)))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                using (var ms = new MemoryStream())
                {
                    gz.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception e) { Debug.LogWarning("[Banjjak] read failed: " + e.Message); return null; }
        }

        public static void ClearLocal(string id)
        {
            if (Web) { PlayerPrefs.DeleteKey(PrefKeyFor(id)); PlayerPrefs.Save(); return; }
            try { if (File.Exists(SavePathFor(id))) File.Delete(SavePathFor(id)); } catch { }
        }

        /// <summary>Apply saved progress bytes to this freshly built job.</summary>
        public bool Apply(byte[] raw)
        {
            if (raw == null) return false;
            try
            {
                using (var r = new BinaryReader(new MemoryStream(raw)))
                {
                    int ver = r.ReadInt32();
                    if (ver != 7 && ver != 8) return false;
                    if (r.ReadInt32() != Surfs.Count) return false;
                    float elapsed = r.ReadSingle();
                    var all = new List<(bool done, byte[] b)>();
                    foreach (var s in Surfs)
                    {
                        if (r.ReadInt32() != s.N) return false;
                        bool done = r.ReadBoolean();
                        int len = r.ReadInt32();
                        all.Add((done, r.ReadBytes(len)));
                    }
                    loadDone.Clear();
                    newlyDone.Clear();
                    for (int i = 0; i < Surfs.Count; i++)
                    {
                        var s = Surfs[i];
                        var b = all[i].b;
                        int idx = 0;
                        for (int k = 0; k < s.N; k++)
                        {
                            if (s.Surf[k] != 2) continue;
                            int bi = idx >> 1;
                            int q = bi < b.Length ? ((idx & 1) == 0 ? b[bi] & 15 : b[bi] >> 4) : 15;
                            idx++;
                            float saved = q / 15f;
                            if (saved < s.DirtAmt[k]) s.DirtAmt[k] = saved < G.CleanT * .5f ? 0 : saved;
                        }
                        // neighbours of counted voxels follow them so edges stay consistent
                        s.Dilate();
                        s.RecountFromDirt();
                        if (all[i].done || s.Remaining <= s.Allow)
                        {
                            // finished only now (e.g. a game update left out cells nobody can reach): settle it after loading
                            if (!all[i].done) newlyDone.Add(s);
                            s.Finish();
                        }
                    }
                    Elapsed = elapsed;
                    if (ver >= 8)
                    {
                        var grass = ReadBits(r); var weeds = ReadBits(r); var trash = ReadBits(r);
                        if (Grass != null) Grass.ApplyCut(grass);
                        if (Weeds != null) for (int i = 0; i < Mathf.Min(weeds.Length, Weeds.N); i++) if (weeds[i]) Weeds.Pull(i);
                        if (Trash != null) for (int i = 0; i < Mathf.Min(trash.Length, Trash.N); i++) if (trash[i]) Trash.Pick(i);
                    }
                }
                ComputeTotals();
                foreach (var o in Objects) foreach (var c in o.Comps) if (c.Done) c.Remaining = 0;
                foreach (var s in newlyDone) if (s.Comp.Done && !loadDone.Contains(s.Comp)) loadDone.Add(s.Comp);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[Banjjak] load failed: " + e.Message); return false; }
        }

        readonly List<Comp> loadDone = new List<Comp>();
        readonly List<DirtSurface> newlyDone = new List<DirtSurface>();

        /// <summary>
        /// After a save is applied (and the events are hooked up): pay for parts that only finished on loading, and
        /// finish the job if nothing is left. Without this a job could sit at 100% with nothing to clean and never
        /// unlock the next one.
        /// </summary>
        public void SettleAfterLoad(bool payParts = true)
        {
            if (payParts) foreach (var c in loadDone)
            {
                int pay = Mathf.Max(3, Mathf.RoundToInt(c.Total * PayPerCell));
                Earned += pay;
                CompDone?.Invoke(c, pay);
                if (c.Obj.Done) ObjectDone?.Invoke(c.Obj, pay);
            }
            loadDone.Clear();
            CompleteIfAllDone();
        }

        void CompleteIfAllDone()
        {
            if (Completed || !Objects.TrueForAll(o => o.Done)) return;
            Completed = true;
            int bonus = Mathf.RoundToInt(Reward * Mul * .6f);
            Earned += bonus;
            JobDone?.Invoke(bonus);
        }

        static void WriteBits(BinaryWriter w, bool[] bits)
        {
            w.Write(bits.Length);
            var b = new byte[(bits.Length + 7) / 8];
            for (int i = 0; i < bits.Length; i++) if (bits[i]) b[i >> 3] |= (byte)(1 << (i & 7));
            w.Write(b);
        }

        static bool[] ReadBits(BinaryReader r)
        {
            int n = r.ReadInt32();
            var b = r.ReadBytes((n + 7) / 8);
            var bits = new bool[n];
            for (int i = 0; i < n; i++) bits[i] = (b[i >> 3] & (1 << (i & 7))) != 0;
            return bits;
        }
    }
}
