using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    /// <summary>Reusable props, ported from the web game's builders.</summary>
    public static class Props
    {
        const float R2D = Mathf.Rad2Deg;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static MeshOpts Opts(Occ occ = Occ.None, bool dbl = false) => new MeshOpts { Occ = occ, Double = dbl };

        public static JobObject ParkBench(CleanJob job, string name, float x, float z, float rotDeg, string spec, string frameSpec = "rust+grime")
        {
            var o = job.Object(name, V(x, 0, z), rotDeg, spec);
            var seat = o.Comp("좌판"); var back = o.Comp("등받이"); var frame = o.Comp("프레임", frameSpec);
            var s = new MeshData();
            for (int i = 0; i < 4; i++) s.Add(MeshKit.RoundedBox(1.72f, .035f, .085f, .012f).Xf(0, .45f, .17f - i * .1f));
            o.Mesh(seat, s, Mats.Wood, Vector3.zero, default, Opts(Occ.Box));
            var b = new MeshData();
            for (int i = 0; i < 3; i++) b.Add(MeshKit.RoundedBox(1.72f, .085f, .03f, .012f).Xf(0, .6f + i * .105f, -.215f - i * .028f, -.26f * R2D));
            o.Mesh(back, b, Mats.Wood, Vector3.zero, default, Opts(Occ.Box));
            var sides = new MeshData();
            foreach (float sx in new[] { -.74f, .74f })
            {
                sides.Add(MeshKit.Tube(new[] { V(sx, 0, .25f), V(sx, .2f, .25f), V(sx, .425f, .22f), V(sx, .425f, 0), V(sx, .425f, -.17f), V(sx, .62f, -.23f), V(sx, .86f, -.29f) }, .02f, 48));
                sides.Add(MeshKit.Tube(new[] { V(sx, .42f, -.15f), V(sx, .2f, -.19f), V(sx, 0, -.22f) }, .02f, 16));
                sides.Add(MeshKit.Tube(new[] { V(sx, .43f, .21f), V(sx, .6f, .2f), V(sx, .65f, .1f), V(sx, .64f, -.12f) }, .018f, 24));
            }
            o.Mesh(frame, sides, Mats.BlackMetal, Vector3.zero);
            o.Volume(V(1.8f, .5f, .62f), V(0, .25f, -.02f));
            return o;
        }

        public static JobObject PicnicTable(CleanJob job, string name, float x, float z, float rotDeg, string spec)
        {
            var o = job.Object(name, V(x, 0, z), rotDeg, spec);
            var top = o.Comp("상판"); var seats = o.Comp("긴 의자"); var legs = o.Comp("다리", "grime+moss");
            var t = new MeshData();
            foreach (float zz in new[] { -.3f, -.15f, 0, .15f, .3f }) t.Add(MeshKit.RoundedBox(1.8f, .045f, .14f, .015f).Xf(0, .75f, zz));
            o.Mesh(top, t, Mats.Wood, Vector3.zero, default, Opts(Occ.Box));
            var se = new MeshData();
            foreach (float zz in new[] { -.68f, -.54f, .54f, .68f }) se.Add(MeshKit.RoundedBox(1.8f, .04f, .13f, .015f).Xf(0, .44f, zz));
            o.Mesh(seats, se, Mats.Wood, Vector3.zero, default, Opts(Occ.Box));
            var lg = new MeshData();
            foreach (float sx in new[] { -.7f, .7f })
            {
                foreach (int sg in new[] { 1, -1 }) lg.Add(MeshKit.RoundedBox(.06f, .9f, .1f, .015f).Xf(sx, .37f, sg * .33f, sg * -.54f * R2D));
                lg.Add(MeshKit.RoundedBox(.06f, .07f, 1.5f, .015f).Xf(sx, .38f, 0));
            }
            o.Mesh(legs, lg, Mats.DarkWood, Vector3.zero);
            o.Volume(V(1.8f, .78f, .8f), V(0, .39f, 0));
            o.Volume(V(1.8f, .46f, .3f), V(0, .23f, .61f));
            o.Volume(V(1.8f, .46f, .3f), V(0, .23f, -.61f));
            return o;
        }

        public static void Pot(JobObject o, Comp comp, float x, float z, float s = 1, bool plant = true)
        {
            var pts = new float[,] { { .25f, 0 }, { .29f, .04f }, { .35f, .5f }, { .4f, .52f }, { .41f, .58f }, { .38f, .6f }, { .35f, .58f }, { .34f, .53f } };
            o.Mesh(comp, MeshKit.Lathe(pts, 40, s), Mats.Clay, V(x, 0, z), default, new MeshOpts { Occ = Occ.Cyl, OccR = .36f * s, OccY0 = 0, OccY1 = .6f * s, Double = true });
            o.Deco(MeshKit.Disc(.34f * s), Mats.Soil, V(x, .53f * s, z), default, new MeshOpts { NoRay = true });
            if (plant) Bush(o, x, z, .75f * s, .45f * s);
        }

        public static JobObject GardenLamp(CleanJob job, string name, float x, float z)
        {
            var o = job.Object(name, V(x, 0, z), 0, "grime+rust");
            o.Mesh(o.Comp("기둥"), MeshKit.Lathe(new float[,] { { .07f, 0 }, { .075f, .05f }, { .045f, .12f }, { .035f, 1.4f }, { .05f, 1.45f }, { .045f, 1.5f } }, 20), Mats.BlackMetal, Vector3.zero, default,
                new MeshOpts { Occ = Occ.Cyl, OccR = .05f, OccY0 = 0, OccY1 = 1.5f });
            o.Mesh(o.Comp("등", "grime+bird"), MeshKit.Lathe(new float[,] { { .045f, 1.5f }, { .13f, 1.55f }, { .12f, 1.8f }, { .16f, 1.83f }, { .03f, 1.96f } }, 6), Mats.Lantern, Vector3.zero);
            o.Deco(MeshKit.Cylinder(.09f, .09f, .22f, 6), Mats.LampGlow, V(0, 1.67f, 0), default, new MeshOpts { NoRay = true, NoShadow = true });
            return o;
        }

        public static JobObject KettleGrill(CleanJob job, float x, float z)
        {
            var o = job.Object("바비큐 그릴", V(x, 0, z), 0, "soot+grime");
            var body = MeshKit.Merge(MeshKit.Sphere(.3f, 32, 16, Mathf.PI / 2).Xf(0, .76f, 0, 180), MeshKit.Sphere(.31f, 32, 12, Mathf.PI / 2).Xf(0, .79f, 0));
            o.Mesh(o.Comp("그릴 몸통"), body, Mats.BlackMetal, Vector3.zero, default, new MeshOpts { Occ = Occ.Sphere, OccR = .31f, Double = true });
            var legs = new MeshData();
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2, c = Mathf.Cos(a), s = Mathf.Sin(a);
                legs.Add(MeshKit.Tube(new[] { V(c * .2f, .55f, s * .2f), V(c * .3f, .25f, s * .3f), V(c * .36f, 0, s * .36f) }, .016f, 12));
            }
            o.Mesh(o.Comp("다리", "rust+grime"), legs, Mats.Metal, Vector3.zero);
            o.Deco(MeshKit.Tube(new[] { V(-.08f, 1.1f, 0), V(-.07f, 1.15f, 0), V(.07f, 1.15f, 0), V(.08f, 1.1f, 0) }, .015f, 10), Mats.Rubber, Vector3.zero);
            o.Volume(V(.64f, 1.1f, .64f), V(0, .55f, 0));
            return o;
        }

        public static JobObject BirdBath(CleanJob job, float x, float z)
        {
            var o = job.Object("새 물받이", V(x, 0, z), 0, "algae+lime+bird");
            o.Mesh(o.Comp("물받이"), MeshKit.Lathe(new float[,] { { .2f, 0 }, { .21f, .06f }, { .1f, .12f }, { .075f, .6f }, { .12f, .68f }, { .36f, .76f }, { .42f, .82f }, { .41f, .86f }, { .36f, .84f }, { .3f, .8f } }, 40),
                Mats.StoneSmooth, Vector3.zero, default, new MeshOpts { Double = true, Occ = Occ.Cyl, OccR = .1f, OccY0 = 0, OccY1 = .7f });
            o.Deco(MeshKit.Disc(.31f), Mats.Water, V(0, .8f, 0), default, new MeshOpts { NoShadow = true });
            return o;
        }

        public static JobObject WheelieBin(CleanJob job, float x, float z, float rotDeg)
        {
            var o = job.Object("대형 쓰레기통", V(x, 0, z), rotDeg, "grime");
            job.Bins.Add(V(x, 0, z));
            o.Mesh(o.Comp("몸통"), MeshKit.RoundedBox(.58f, .92f, .68f, .06f, 3), Mats.BinGreen, V(0, .48f, 0), default, Opts(Occ.Box));
            o.Mesh(o.Comp("뚜껑", "grime+bird"), MeshKit.RoundedBox(.64f, .06f, .76f, .03f), Mats.BinLid, V(0, .97f, .02f));
            o.Deco(MeshKit.Tube(new[] { V(-.24f, .9f, -.38f), V(-.24f, .94f, -.42f), V(.24f, .94f, -.42f), V(.24f, .9f, -.38f) }, .015f, 16), Mats.Rubber, Vector3.zero);
            foreach (float sx in new[] { -.26f, .26f }) o.Deco(MeshKit.Cylinder(.09f, .09f, .06f, 16), Mats.Rubber, V(sx, .09f, -.38f), V(0, 0, 90));
            return o;
        }

        public static void Bush(JobObject o, float x, float z, float s = 1, float y = 0)
        {
            var rng = new Rng($"{x},{z}");
            var f = MeshKit.Foliage(.6f * s, .45f * s, .6f * s, Mathf.RoundToInt(26 * s * s + 8), .45f * s, rng);
            FoliageObj(o.T, f, Mats.LeafDark, V(x, y + .4f * s, z), "bush");
            o.Deco(MeshKit.Sphere(.42f * s, 14, 10), Mats.Solid(new Color(.12f, .2f, .08f), .1f), V(x, y + .35f * s, z), default, new MeshOpts { SprayOnly = true });
        }

        public static GameObject FoliageObj(Transform parent, MeshData md, Material mat, Vector3 pos, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = md.ToMesh(name, false);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        /// <summary>Broadleaf tree: curved trunk, a few branches, leaf clusters at the tips.</summary>
        public static void Tree(JobObject o, float x, float z, float s = 1, bool solid = false, int variant = -1)
        {
            var rng = new Rng($"tree{x:F1},{z:F1}");
            if (variant < 0) variant = Mathf.Abs(Mathf.RoundToInt(x * 7 + z * 13)) % 3;
            float h = (2.6f + variant * .4f) * s;
            var trunk = new MeshData();
            var top = V(rng.Range(-.25f, .25f) * s, h, rng.Range(-.25f, .25f) * s);
            trunk.Add(MeshKit.Tube(new[] { V(0, -.1f, 0), V(.05f * s, h * .4f, 0), top }, .14f * s, 16, 12, false));
            trunk.Add(MeshKit.Cylinder(.2f * s, .26f * s, .3f * s, 12, true).Xf(0, .1f * s, 0));
            var tips = new List<Vector3> { top + V(0, .6f * s, 0) };
            int nb = 3 + variant;
            for (int i = 0; i < nb; i++)
            {
                float a = i / (float)nb * Mathf.PI * 2 + rng.Range(-.3f, .3f);
                float sy = h * rng.Range(.55f, .85f);
                var start = Vector3.Lerp(V(0, 0, 0), top, sy / h);
                var tip = start + V(Mathf.Cos(a) * 1.2f * s, rng.Range(.6f, 1.1f) * s, Mathf.Sin(a) * 1.2f * s);
                trunk.Add(MeshKit.Tube(new[] { start, Vector3.Lerp(start, tip, .5f) + V(0, .15f * s, 0), tip }, .06f * s, 10, 8));
                tips.Add(tip);
            }
            o.Deco(trunk, Mats.Bark, V(x, 0, z), V(0, (x + z) * R2D, 0), new MeshOpts { SprayOnly = true });
            var leaves = new MeshData { Col = new List<Color>() };
            var lr = new Rng($"leaf{x:F1},{z:F1}");
            foreach (var t in tips)
            {
                float r = rng.Range(.9f, 1.25f) * s;
                leaves.Add(MeshKit.Foliage(r, r * .75f, r, Mathf.RoundToInt(60 * s), .7f * s, lr), Matrix4x4.Translate(t));
            }
            var mats = new[] { Mats.Leaf, Mats.LeafDark, Mats.LeafLight };
            var f = FoliageObj(o.T, leaves, mats[Mathf.Abs(Mathf.RoundToInt(x + z)) % 3], V(x, 0, z), "tree");
            f.transform.localEulerAngles = V(0, (x + z) * R2D, 0);
            if (solid) o.Volume(V(.6f * s, 3 * s, .6f * s), V(x, 1.5f * s, z));
        }

        public static void Hedge(JobObject o, float w, float h, float d, float x, float z)
        {
            var rng = new Rng($"h{x},{z},{w}");
            o.Deco(MeshKit.RoundedBox(w, h, d, Mathf.Min(.3f, Mathf.Min(h / 2, d / 2))), Mats.Solid(new Color(.1f, .19f, .07f), .05f), V(x, h / 2, z), default, new MeshOpts { SprayOnly = true });
            int count = Mathf.Min(2600, Mathf.RoundToInt((w * h * 2 + d * h * 2 + w * d) * 6));
            var md = new MeshData { Col = new List<Color>() };
            float fw = w * h, fd = d * h, ft = w * d, tot = 2 * fw + 2 * fd + ft;
            var corners = new[] { V(-.5f, -.5f, 0), V(.5f, -.5f, 0), V(.5f, .5f, 0), V(-.5f, .5f, 0) };
            var uvs = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            for (int i = 0; i < count; i++)
            {
                float r = rng.Next() * tot, px, py, pz;
                if (r < 2 * fw) { px = (rng.Next() - .5f) * w; py = rng.Next() * h; pz = (r < fw ? 1 : -1) * d / 2; }
                else if (r < 2 * fw + 2 * fd) { px = (r < 2 * fw + fd ? 1 : -1) * w / 2; py = rng.Next() * h; pz = (rng.Next() - .5f) * d; }
                else { px = (rng.Next() - .5f) * w; py = h; pz = (rng.Next() - .5f) * d; }
                float s = .45f + rng.Next() * .3f;
                var rot = Quaternion.Euler(rng.Range(0, 180), rng.Range(0, 360), rng.Range(0, 180));
                var p = V(px, py, pz);
                var n = V(px / w, (py - h * .4f) / h, pz / d).normalized;
                float sh = .65f + rng.Next() * .35f;
                int b = md.Count;
                for (int k = 0; k < 4; k++) { md.V.Add(p + rot * (corners[k] * s)); md.N.Add(n); md.UV.Add(uvs[k]); md.Col.Add(new Color(sh, sh, sh, 0)); }
                md.Tri(b, b + 1, b + 2); md.Tri(b, b + 2, b + 3);
            }
            FoliageObj(o.T, md, Mats.LeafDark, V(x, 0, z), "hedge");
        }

        /// <summary>Simple background house: plaster walls, gable roof, windows, door.</summary>
        public static void House(JobObject o, float x, float z, float rotDeg, float W, float D, float H, Color wall, Color roof)
        {
            var root = new GameObject("house").transform;
            root.SetParent(o.T, false);
            root.localPosition = V(x, 0, z);
            root.localEulerAngles = V(0, rotDeg, 0);
            var wallM = new Material(Mats.Plaster); wallM.SetColor("_BaseColor", wall);
            var roofM = new Material(Mats.Roof); roofM.SetColor("_BaseColor", roof);
            Part(root, MeshKit.RoundedBox(W, H, D, .03f), wallM, V(0, H / 2, 0));
            // gable roof: two slabs + end triangles
            float rise = D * .38f, slope = Mathf.Atan2(rise, D / 2) * R2D, len = Mathf.Sqrt(rise * rise + D * D / 4) + .45f;
            Part(root, MeshKit.RoundedBox(W + .6f, .14f, len, .03f), roofM, V(0, H + rise / 2 + .05f, -D / 4 - .1f), V(-slope, 0, 0));
            Part(root, MeshKit.RoundedBox(W + .6f, .14f, len, .03f), roofM, V(0, H + rise / 2 + .05f, D / 4 + .1f), V(slope, 0, 0));
            var gable = new MeshData();
            foreach (float sx in new[] { -W / 2 + .01f, W / 2 - .01f })
            {
                var n = sx < 0 ? Vector3.left : Vector3.right;
                int a = gable.Vert(V(sx, H, -D / 2), n, new Vector2(0, 0));
                int b = gable.Vert(V(sx, H, D / 2), n, new Vector2(D, 0));
                int c = gable.Vert(V(sx, H + rise, 0), n, new Vector2(D / 2, rise));
                gable.Tri(a, b, c);
            }
            Part(root, gable, wallM, Vector3.zero);
            // windows on the long front (+Z) and back
            foreach (float side in new[] { 1f, -1f })
            {
                int nwin = Mathf.Max(2, Mathf.FloorToInt(W / 2.6f));
                for (int f = 0; f < 2; f++)
                    for (int i = 0; i < nwin; i++)
                    {
                        float wx = -W / 2 + W * (i + .5f) / nwin, wy = 1.4f + f * 2.6f;
                        if (f == 0 && side > 0 && i == nwin / 2) continue;
                        var fz = side * (D / 2 + .02f);
                        Part(root, MeshKit.RoundedBox(1.1f, 1.3f, .06f, .02f), Mats.WhiteTrim, V(wx, wy, fz));
                        Part(root, MeshKit.Quad(.94f, 1.14f), Mats.Glass, V(wx, wy, fz + side * .035f), V(0, side > 0 ? 0 : 180, 0));
                        Part(root, MeshKit.RoundedBox(.04f, 1.14f, .03f, .01f), Mats.WhiteTrim, V(wx, wy, fz + side * .05f));
                    }
                if (side > 0)
                {
                    Part(root, MeshKit.RoundedBox(1.2f, 2.3f, .08f, .02f), Mats.WhiteTrim, V(0, 1.15f, D / 2 + .02f));
                    Part(root, MeshKit.RoundedBox(1.0f, 2.15f, .06f, .02f), Mats.Solid(new Color(.35f, .2f, .14f), .35f), V(0, 1.08f, D / 2 + .05f));
                }
            }
        }

        static GameObject Part(Transform parent, MeshData md, Material m, Vector3 pos, Vector3 euler = default)
        {
            var go = new GameObject("part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.AddComponent<MeshFilter>().sharedMesh = md.ToMesh("part");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            return go;
        }

        /// <summary>Scattered grass blades in chunks, skipping excluded rectangles.</summary>
        public static void GrassField(Transform parent, Rect area, List<Rect> exclude, float density, int seed)
        {
            var rng = new Rng((uint)seed);
            float chunk = 6f;
            for (float cx = area.xMin; cx < area.xMax; cx += chunk)
                for (float cz = area.yMin; cz < area.yMax; cz += chunk)
                {
                    var md = new MeshData { Col = new List<Color>() };
                    float w = Mathf.Min(chunk, area.xMax - cx), d = Mathf.Min(chunk, area.yMax - cz);
                    int n = Mathf.RoundToInt(w * d * density);
                    var center = V(cx + w / 2, 0, cz + d / 2);
                    for (int i = 0; i < n; i++)
                    {
                        float x = cx + rng.Next() * w, z = cz + rng.Next() * d;
                        bool skip = false;
                        foreach (var r in exclude) if (r.Contains(new Vector2(x, z))) { skip = true; break; }
                        if (skip) continue;
                        float patch = Noise.Fbm(x * .35f, z * .35f, 12);
                        if (rng.Next() > .45f + patch * .7f) continue;
                        float hgt = rng.Range(.12f, .26f) * (.7f + patch * .7f);
                        var tint = Color.Lerp(new Color(.9f, .95f, .85f), new Color(1.1f, 1.05f, .8f), rng.Next());
                        MeshKit.AddBlade(md, V(x, 0, z) - center, rng.Range(0, 360), hgt, rng.Range(.025f, .04f), rng.Range(.1f, .3f), tint);
                    }
                    if (md.Count == 0) continue;
                    var go = FoliageObj(parent, md, Mats.GrassBlades, center, "grass");
                    go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
        }
    }
}
