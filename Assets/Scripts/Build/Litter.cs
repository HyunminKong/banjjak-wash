using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    public class LitterZone
    {
        public float X0, X1, Z0, Z1, Y;
        public int N;
        public Rect[] Skip;
    }

    // ===================================================================== grass
    /// <summary>Lawn grass blades in chunk meshes that can be mown (vertex alpha = cut).</summary>
    public class GrassField
    {
        class Chunk { public Mesh Mesh; public Color[] Cols; public bool Dirty; }
        readonly List<Chunk> chunks = new List<Chunk>();
        readonly List<Vector2> pos = new List<Vector2>();
        readonly List<int> chunkOf = new List<int>();
        readonly List<int> vStart = new List<int>();
        readonly List<bool> cut = new List<bool>();
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        public int Lawn, CutCount;
        float per, acc;
        CleanJob job;

        static long Key(int gx, int gz) => ((long)(gx + 5000) << 20) | (long)(gz + 5000);

        public static GrassField Build(CleanJob job, Transform parent, Rect area, float density, int seed)
        {
            var g = new GrassField { job = job };
            Physics.SyncTransforms();
            var rng = new Rng((uint)seed);
            float chunk = 6f;
            int mask = (1 << Layers.Default) | (1 << Layers.PlayerOnly);
            var b = job.Bounds;
            for (float cx = area.xMin; cx < area.xMax; cx += chunk)
                for (float cz = area.yMin; cz < area.yMax; cz += chunk)
                {
                    var md = new MeshData { Col = new List<Color>() };
                    float w = Mathf.Min(chunk, area.xMax - cx), d = Mathf.Min(chunk, area.yMax - cz);
                    int n = Mathf.RoundToInt(w * d * density);
                    var center = new Vector3(cx + w / 2, 0, cz + d / 2);
                    int ci = g.chunks.Count;
                    var local = new List<int>();
                    for (int i = 0; i < n; i++)
                    {
                        float x = cx + rng.Next() * w, z = cz + rng.Next() * d;
                        bool skip = false;
                        foreach (var r in job.Paved) if (r.Contains(new Vector2(x, z))) { skip = true; break; }
                        if (skip) continue;
                        float patch = Noise.Fbm(x * .35f, z * .35f, 12);
                        if (rng.Next() > .45f + patch * .7f) continue;
                        if (Physics.CheckSphere(new Vector3(x, .12f, z), .06f, mask, QueryTriggerInteraction.Ignore)) continue;
                        float hgt = rng.Range(.12f, .26f) * (.7f + patch * .7f);
                        var tint = Color.Lerp(new Color(.9f, .95f, .85f), new Color(1.1f, 1.05f, .8f), rng.Next());
                        int vs = md.Count;
                        MeshKit.AddBlade(md, new Vector3(x, 0, z) - center, rng.Range(0, 360), hgt, rng.Range(.025f, .04f), rng.Range(.1f, .3f), tint);
                        int id = g.pos.Count;
                        g.pos.Add(new Vector2(x, z)); g.chunkOf.Add(ci); g.vStart.Add(vs); g.cut.Add(false);
                        if (b.Contains(new Vector2(x, z)))
                        {
                            g.Lawn++;
                            var k = Key(Mathf.FloorToInt(x), Mathf.FloorToInt(z));
                            if (!g.grid.TryGetValue(k, out var l)) g.grid[k] = l = new List<int>();
                            l.Add(id);
                        }
                    }
                    if (md.Count == 0) { g.chunks.Add(new Chunk()); continue; }
                    var go = Props.FoliageObj(parent, md, Mats.GrassBlades, center, "grass");
                    go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                    mesh.MarkDynamic();
                    g.chunks.Add(new Chunk { Mesh = mesh, Cols = md.Col.ToArray() });
                }
            g.per = job.Reward * .1f * job.Mul / Mathf.Max(1, g.Lawn);
            return g;
        }

        /// <summary>Mow blades within r of (x,z). Pays a small bonus. Returns the number cut.</summary>
        public int Cut(float x, float z, float r)
        {
            int n = 0;
            for (int gx = Mathf.FloorToInt(x - r); gx <= Mathf.FloorToInt(x + r); gx++)
                for (int gz = Mathf.FloorToInt(z - r); gz <= Mathf.FloorToInt(z + r); gz++)
                {
                    if (!grid.TryGetValue(Key(gx, gz), out var l)) continue;
                    foreach (int i in l)
                    {
                        if (cut[i]) continue;
                        var p = pos[i];
                        if ((p.x - x) * (p.x - x) + (p.y - z) * (p.y - z) >= r * r) continue;
                        cut[i] = true; n++;
                        if (NetHooks.Recording) NetHooks.Cuts.Add(i);
                        var c = chunks[chunkOf[i]];
                        for (int v = vStart[i]; v < vStart[i] + 8; v++) c.Cols[v].a = 1;
                        c.Dirty = true;
                    }
                }
            if (n > 0)
            {
                CutCount += n;
                job.Changed = true;
                acc += n * per;
                if (acc >= 1) { int add = Mathf.FloorToInt(acc); acc -= add; job.Earned += add; GameManager.I.AddMoney(add); }
            }
            return n;
        }

        public int UncutNear(float x, float z, float r)
        {
            int n = 0;
            for (int gx = Mathf.FloorToInt(x - r); gx <= Mathf.FloorToInt(x + r); gx++)
                for (int gz = Mathf.FloorToInt(z - r); gz <= Mathf.FloorToInt(z + r); gz++)
                {
                    if (!grid.TryGetValue(Key(gx, gz), out var l)) continue;
                    foreach (int i in l) if (!cut[i] && (pos[i] - new Vector2(x, z)).sqrMagnitude < r * r) n++;
                }
            return n;
        }

        public void Flush()
        {
            foreach (var c in chunks) if (c.Dirty && c.Mesh != null) { c.Mesh.SetColors(c.Cols); c.Dirty = false; }
        }

        /// <summary>Co-op: a blade someone else mowed (their mower pays them, not us).</summary>
        public void CutIdx(int i)
        {
            if (i < 0 || i >= cut.Count || cut[i]) return;
            cut[i] = true; CutCount++;
            var c = chunks[chunkOf[i]];
            for (int v = vStart[i]; v < vStart[i] + 8; v++) c.Cols[v].a = 1;
            c.Dirty = true;
        }

        public bool[] CutState() => cut.ToArray();
        public void ApplyCut(bool[] state)
        {
            for (int i = 0; i < Mathf.Min(state.Length, cut.Count); i++)
                if (state[i] && !cut[i])
                {
                    cut[i] = true; CutCount++;
                    var c = chunks[chunkOf[i]];
                    for (int v = vStart[i]; v < vStart[i] + 8; v++) c.Cols[v].a = 1;
                    c.Dirty = true;
                }
            Flush();
        }
    }

    // ===================================================================== weeds
    public class WeedField
    {
        public readonly List<(GameObject go, Vector3 p)> Items = new List<(GameObject, Vector3)>();
        public readonly List<bool> Pulled = new List<bool>();
        public int Count;
        public int N => Items.Count;

        static MeshData weedMesh;
        static MeshData Leaves()
        {
            if (weedMesh != null) return weedMesh;
            var md = new MeshData { Col = new List<Color>() };
            var uvs = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            for (int i = 0; i < 10; i++)
            {
                bool inner = i >= 7;
                float a = inner ? i * 120f : i / 7f * 360f + 17;
                float w = inner ? .07f : .1f, h = inner ? .17f : .22f, tilt = inner ? 20 : 62;
                var rot = Quaternion.Euler(0, a, 0) * Quaternion.Euler(tilt, 0, 0);
                var corners = new[] { new Vector3(-w / 2, 0, 0), new Vector3(w / 2, 0, 0), new Vector3(w / 2, h, 0), new Vector3(-w / 2, h, 0) };
                int b = md.Count;
                for (int k = 0; k < 4; k++) { md.V.Add(rot * corners[k] + Vector3.up * .01f); md.N.Add(Vector3.up); md.UV.Add(uvs[k]); md.Col.Add(new Color(.85f, 1f, .7f, 0)); }
                md.Tri(b, b + 1, b + 2); md.Tri(b, b + 2, b + 3);
            }
            return weedMesh = md;
        }

        public static WeedField Build(CleanJob job)
        {
            var wf = new WeedField();
            var b = job.Bounds;
            var rng = new Rng(job.Id + "w");
            float area = b.width * b.height;
            int want = Mathf.RoundToInt(Mathf.Clamp(area / 12, 18, 70));
            int mask = (1 << Layers.Default) | (1 << Layers.PlayerOnly);
            var root = new GameObject("weeds").transform;
            root.SetParent(job.Root, false);
            var flowerMat = Mats.Solid(G.Hex(0xffd23a), .4f);
            flowerMat.SetColor("_Emission", G.Hex(0x332200));
            var stem = MeshKit.Merge(MeshKit.Cylinder(.004f, .004f, .22f, 5).Xf(0, .11f, 0), MeshKit.Sphere(.028f, 10, 6).Xf(0, .23f, 0)).ToMesh("flower");
            var leafMesh = Leaves().ToMesh("weed", false);
            for (int i = 0; i < want * 12 && wf.Items.Count < want; i++)
            {
                float x = b.xMin + .5f + rng.Next() * (b.width - 1), z = b.yMin + .5f + rng.Next() * (b.height - 1);
                if (Physics.CheckBox(new Vector3(x, .3f, z), new Vector3(.25f, .25f, .25f), Quaternion.identity, mask, QueryTriggerInteraction.Ignore)) continue;
                bool blocked = false;
                foreach (var nl in job.NoLitter) if (x > nl.min.x && x < nl.max.x && z > nl.min.z && z < nl.max.z) { blocked = true; break; }
                if (blocked) continue;
                bool paved = false;
                foreach (var r in job.Paved) if (r.Contains(new Vector2(x, z))) { paved = true; break; }
                if (paved && rng.Next() < .55f) continue;
                float s = .8f + rng.Next() * .5f;
                var go = new GameObject("weed");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(x, .02f, z);
                go.transform.localEulerAngles = new Vector3(0, rng.Next() * 360, 0);
                go.transform.localScale = Vector3.one * s;
                go.AddComponent<MeshFilter>().sharedMesh = leafMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LeafLight;
                if (rng.Next() < .6f)
                {
                    var f = new GameObject("flower");
                    f.transform.SetParent(go.transform, false);
                    f.AddComponent<MeshFilter>().sharedMesh = stem;
                    f.AddComponent<MeshRenderer>().sharedMaterial = flowerMat;
                }
                wf.Items.Add((go, new Vector3(x, .02f, z)));
                wf.Pulled.Add(false);
            }
            return wf;
        }

        public bool Pull(int i)
        {
            if (i < 0 || i >= Items.Count || Pulled[i]) return false;
            Pulled[i] = true; Count++;
            if (NetHooks.Recording) NetHooks.Pulls.Add(i);
            Items[i].go.SetActive(false);
            return true;
        }
    }

    // ===================================================================== trash
    public class TrashField
    {
        public class Item { public GameObject Go; public Vector3 P; public bool Picked; }
        public readonly List<Item> Items = new List<Item>();
        public int Count;
        public int N => Items.Count;

        static List<(string name, Mesh mesh, Material[] mats, float rx, float lift)> kinds;

        static void InitKinds()
        {
            if (kinds != null) return;
            kinds = new List<(string, Mesh, Material[], float, float)>();
            Material M(uint c, float s, float metal = 0) => Mats.Solid(G.Hex(c), s, metal);
            var can = MeshKit.Lathe(new float[,] { { .001f, 0 }, { .031f, 0 }, { .033f, .006f }, { .033f, .112f }, { .027f, .122f }, { .001f, .122f } }, 16).ToMesh("can");
            kinds.Add(("캔", can, new[] { M(0xd23b2f, .7f, .65f), M(0x2a6fd6, .7f, .65f), M(0xc9ced3, .75f, .7f), M(0x2f9d58, .7f, .65f) }, 90, .033f));
            var bottle = MeshKit.Lathe(new float[,] { { .001f, 0 }, { .034f, 0 }, { .036f, .01f }, { .036f, .15f }, { .03f, .18f }, { .014f, .205f }, { .014f, .225f }, { .001f, .225f } }, 16).ToMesh("bottle");
            kinds.Add(("페트병", bottle, new[] { M(0xa8e0c8, .92f), M(0xd6ecf2, .92f) }, 90, .036f));
            var cup = MeshKit.Lathe(new float[,] { { .001f, 0 }, { .026f, 0 }, { .037f, .092f }, { .039f, .096f }, { .035f, .094f } }, 16).ToMesh("cup");
            kinds.Add(("종이컵", cup, new[] { Mats.DoubleSided(M(0xf4f1ea, .2f)), Mats.DoubleSided(M(0xc8a27a, .2f)) }, 83, .034f));
            var paper = MeshKit.Sphere(.05f, 10, 7);
            var r = new Rng(3u);
            for (int i = 0; i < paper.V.Count; i++) { var v = paper.V[i]; float k = .62f + .5f * r.Next(); paper.V[i] = new Vector3(v.x * k, v.y * k * .7f, v.z * k); }
            MeshKit.RecalcNormals(paper);
            kinds.Add(("구겨진 종이", paper.ToMesh("paper", false), new[] { M(0xf2efe6, .05f), M(0xe9dcc0, .05f) }, 0, .03f));
            var bag = MeshKit.Plane(.3f, .32f, 8, 8);
            for (int i = 0; i < bag.V.Count; i++)
            {
                var v = bag.V[i];
                bag.V[i] = new Vector3(v.x, .03f * Mathf.Sin(v.x * 31 + v.z * 17) + .025f * Mathf.Cos(v.x * 13 - v.z * 29) + .05f * (1 - Mathf.Sqrt(v.x * v.x + v.z * v.z) * 4), v.z);
            }
            MeshKit.RecalcNormals(bag);
            kinds.Add(("비닐봉지", bag.ToMesh("bag", false), new[] { Mats.DoubleSided(M(0xf5f5f5, .45f)) }, 0, .01f));
            kinds.Add(("과자 봉지", MeshKit.RoundedBox(.13f, .022f, .19f, .01f).ToMesh("snack"), new[] { M(0xf2a33a, .65f, .4f), M(0x7b3fd1, .65f, .4f), M(0xd23b2f, .65f, .4f) }, 0, .012f));
            kinds.Add(("택배 상자", MeshKit.RoundedBox(.3f, .2f, .24f, .008f).ToMesh("box"), new[] { Mats.Cardboard }, 0, .1f));
        }

        public static TrashField Build(CleanJob job)
        {
            InitKinds();
            var tf = new TrashField();
            var b = job.Bounds;
            var rng = new Rng(job.Id + "trash");
            float area = b.width * b.height;
            int mask = (1 << Layers.Default) | (1 << Layers.PlayerOnly);
            var root = new GameObject("trash").transform;
            root.SetParent(job.Root, false);
            // ground rectangles: every walkable paved surface not covered by a litter zone
            var rects = new List<Bounds>();
            foreach (var s in job.Surfs)
            {
                if (!s.IsGround) continue;
                var bb = s.GetComponent<Renderer>().bounds;
                bool inZone = false;
                foreach (var zn in job.LitterZones) if (Mathf.Abs(zn.Y - bb.max.y) < .1f && bb.min.x < zn.X1 && bb.max.x > zn.X0 && bb.min.z < zn.Z1 && bb.max.z > zn.Z0) inZone = true;
                if (!inZone) rects.Add(bb);
            }
            int floors = 0; foreach (var r in rects) if (r.max.y > .2f) floors++;
            int want = Mathf.RoundToInt(Mathf.Clamp(area / 18, 20, 70)) + floors * 7;
            bool Place(float x, float y, float z)
            {
                if (Physics.CheckBox(new Vector3(x, y + .27f, z), new Vector3(.2f, .23f, .2f), Quaternion.identity, mask, QueryTriggerInteraction.Ignore)) return false;
                var k = kinds[rng.Int(kinds.Count)];
                var go = new GameObject("trash_" + k.name);
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(x, y + k.lift, z);
                go.transform.localRotation = Quaternion.Euler(0, rng.Next() * 360, 0) * Quaternion.Euler(k.rx, 0, (rng.Next() - .5f) * 17);
                go.AddComponent<MeshFilter>().sharedMesh = k.mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = k.mats[rng.Int(k.mats.Length)];
                tf.Items.Add(new Item { Go = go, P = new Vector3(x, y, z) });
                return true;
            }
            for (int i = 0; i < want * 20 && tf.Items.Count < want; i++)
            {
                float x, z, y = 0;
                if (rng.Next() < .55f && rects.Count > 0)
                {
                    var r = rects[rng.Int(rects.Count)];
                    x = r.min.x + .3f + rng.Next() * Mathf.Max(.01f, r.size.x - .6f);
                    z = r.min.z + .3f + rng.Next() * Mathf.Max(.01f, r.size.z - .6f);
                    y = r.max.y;
                }
                else
                {
                    x = b.xMin + .6f + rng.Next() * (b.width - 1.2f);
                    z = b.yMin + .6f + rng.Next() * (b.height - 1.2f);
                    bool blocked = false;
                    foreach (var nl in job.NoLitter) if (x > nl.min.x && x < nl.max.x && z > nl.min.z && z < nl.max.z) { blocked = true; break; }
                    if (blocked) continue;
                }
                Place(x, y, z);
            }
            foreach (var zn in job.LitterZones)
            {
                int got = 0;
                for (int i = 0; i < zn.N * 30 && got < zn.N; i++)
                {
                    float x = zn.X0 + rng.Next() * (zn.X1 - zn.X0), z = zn.Z0 + rng.Next() * (zn.Z1 - zn.Z0);
                    bool skip = false;
                    if (zn.Skip != null) foreach (var s in zn.Skip) if (s.Contains(new Vector2(x, z))) skip = true;
                    if (skip) continue;
                    if (Place(x, zn.Y, z)) got++;
                }
            }
            return tf;
        }

        public bool Pick(int i)
        {
            if (i < 0 || i >= Items.Count) return false;
            var it = Items[i];
            if (it.Picked) return false;
            it.Picked = true; it.Go.SetActive(false); Count++;
            if (NetHooks.Recording) NetHooks.Picks.Add(i);
            return true;
        }

        public int NearestTo(Vector3 p, HashSet<int> ignore = null)
        {
            int best = -1; float bd = 1e9f;
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (it.Picked || Mathf.Abs(it.P.y - p.y) > .5f || (ignore != null && ignore.Contains(i))) continue;
                float d = (it.P.x - p.x) * (it.P.x - p.x) + (it.P.z - p.z) * (it.P.z - p.z);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }
    }
}
