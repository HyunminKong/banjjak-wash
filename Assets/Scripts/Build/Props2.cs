using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    /// <summary>Street furniture, vehicles and skate features for the larger jobs.</summary>
    public static class Props2
    {
        const float R2D = Mathf.Rad2Deg;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector2 P(float x, float y) => new Vector2(x, y);

        public static JobObject Mailbox(CleanJob job, float x, float z, float rotDeg)
        {
            var o = job.Object("우편함", V(x, 0, z), rotDeg, "dust+rust");
            var sh = new List<Vector2> { P(-.11f, 0), P(.11f, 0), P(.11f, .13f) };
            sh.AddRange(MeshKit.Arc(0, .13f, .11f, 0, Mathf.PI, 12));
            var g = MeshKit.Extrude(sh, .46f).Xf(0, 0, -.23f);
            o.Mesh(o.Comp("우편함"), g, Mats.Blue, V(0, 1.08f, 0), default, new MeshOpts { Occ = Occ.Box });
            o.Mesh(o.Comp("기둥", "moss+mildew"), MeshKit.RoundedBox(.09f, 1.08f, .09f, .015f), Mats.Wood, V(0, .54f, 0));
            o.Deco(MeshKit.RoundedBox(.02f, .14f, .05f, .006f), Mats.Taillight, V(.125f, 1.24f, .1f));
            return o;
        }

        public static JobObject LampPost(CleanJob job, string name, float x, float z)
        {
            var o = job.Object(name, V(x, 0, z), 0, "rust+grime");
            o.Mesh(o.Comp("기둥"), MeshKit.Lathe(new float[,] { { .2f, 0 }, { .2f, .12f }, { .15f, .18f }, { .1f, .32f }, { .07f, .42f }, { .06f, 2.8f }, { .08f, 2.85f }, { .05f, 2.95f } }, 24), Mats.BlackMetal, Vector3.zero, default,
                new MeshOpts { Occ = Occ.Cyl, OccR = .2f, OccY0 = 0, OccY1 = 2.95f });
            o.Mesh(o.Comp("등", "grime+bird"), MeshKit.Lathe(new float[,] { { .05f, 2.95f }, { .17f, 3.05f }, { .19f, 3.4f }, { .24f, 3.45f }, { .1f, 3.6f }, { .03f, 3.75f } }, 8), Mats.Lantern, Vector3.zero);
            o.Deco(MeshKit.Cylinder(.15f, .15f, .32f, 8), Mats.LampGlow, V(0, 3.22f, 0), default, new MeshOpts { NoRay = true, NoShadow = true });
            return o;
        }

        public static JobObject RoundBin(CleanJob job, float x, float z)
        {
            var o = job.Object("거리 쓰레기통", V(x, 0, z), 0, "grime+gum");
            job.Bins.Add(V(x, 0, z));
            o.Mesh(o.Comp("통"), MeshKit.Lathe(new float[,] { { .24f, 0 }, { .26f, .04f }, { .26f, .82f }, { .28f, .86f }, { .27f, .9f }, { .24f, .88f } }, 28), Mats.BinGreen, Vector3.zero, default,
                new MeshOpts { Double = true, Occ = Occ.Cyl, OccR = .26f, OccY0 = 0, OccY1 = .9f });
            o.Deco(MeshKit.Disc(.24f), Mats.Solid(new Color(.1f, .1f, .1f), .05f), V(0, .7f, 0), default, new MeshOpts { NoRay = true });
            return o;
        }

        public static JobObject Dumpster(CleanJob job, float x, float z, float rotDeg, float y = 0)
        {
            var o = job.Object("대형 수거함", V(x, 0, z), rotDeg, "grime+rust");
            job.Bins.Add(V(x, y, z));
            o.Mesh(o.Comp("수거함"), MeshKit.RoundedBox(1.9f, 1.15f, 1.1f, .06f, 2), Mats.BinGreen, V(0, y + .62f, 0), default, new MeshOpts { Occ = Occ.Box });
            o.Deco(MeshKit.RoundedBox(1.96f, .06f, 1.18f, .02f), Mats.BinLid, V(0, y + 1.23f, 0), V(-.08f * R2D, 0, 0));
            foreach (var p in new[] { P(-.8f, -.45f), P(.8f, -.45f), P(-.8f, .45f), P(.8f, .45f) })
                o.Deco(MeshKit.Cylinder(.06f, .06f, .05f, 12), Mats.Rubber, V(p.x, y + .06f, p.y), V(0, 0, 90));
            return o;
        }

        public static JobObject TrashStation(CleanJob job, float x, float z, float rotDeg, float y = 0)
        {
            var o = job.Object("실내 쓰레기통", V(x, 0, z), rotDeg, "grime");
            job.Bins.Add(V(x, y, z));
            o.Mesh(o.Comp("통"), MeshKit.Lathe(new float[,] { { .17f, 0 }, { .19f, .03f }, { .2f, .62f }, { .21f, .66f }, { .18f, .66f } }, 24), Mats.Grey, V(0, y, 0), default,
                new MeshOpts { Double = true, Occ = Occ.Cyl, OccR = .2f, OccY0 = 0, OccY1 = .66f });
            return o;
        }

        public static JobObject Bollards(CleanJob job, string name, Vector2[] pts, string spec = "grime+tire")
        {
            var o = job.Object(name, Vector3.zero, 0, spec);
            var c = o.Comp("볼라드");
            var md = new MeshData();
            foreach (var p in pts) md.Add(MeshKit.Lathe(new float[,] { { .11f, 0 }, { .11f, .78f }, { .09f, .86f }, { .02f, .9f } }, 20).Xf(p.x, 0, p.y));
            o.Mesh(c, md, Mats.Bollard, Vector3.zero, default, new MeshOpts { Double = true });
            foreach (var p in pts) { o.OccCyl(.12f, -1, .1f, p.x, p.y, true); o.Volume(V(.24f, .9f, .24f), V(p.x, .45f, p.y)); }
            return o;
        }

        public static JobObject Hatchback(CleanJob job, string name, float x, float z, float rotDeg, Material paint)
        {
            var o = job.Object(name, V(x, 0, z), rotDeg, "dust+bird+mud");
            var b = new List<Vector2> { P(-2.0f, .3f), P(-1.62f, .3f) };
            b.AddRange(MeshKit.Arc(-1.25f, .3f, .37f, Mathf.PI, 0, 14));
            b.Add(P(.88f, .3f));
            b.AddRange(MeshKit.Arc(1.25f, .3f, .37f, Mathf.PI, 0, 14));
            b.Add(P(2.02f, .3f));
            b.AddRange(MeshKit.QuadBezier(P(2.02f, .3f), P(2.1f, .32f), P(2.1f, .5f)));
            b.Add(P(2.05f, .76f));
            b.AddRange(MeshKit.QuadBezier(P(2.05f, .76f), P(2.0f, .88f), P(1.7f, .9f)));
            b.Add(P(.55f, .98f)); b.Add(P(-1.75f, .98f));
            b.AddRange(MeshKit.QuadBezier(P(-1.75f, .98f), P(-2.02f, .95f), P(-2.05f, .75f)));
            b.Add(P(-2.05f, .45f));
            b.AddRange(MeshKit.QuadBezier(P(-2.05f, .45f), P(-2.05f, .3f), P(-2.0f, .3f)));
            o.Mesh(o.Comp("차체"), MeshKit.Extrude(b, 1.6f).Xf(0, 0, -.8f), paint, Vector3.zero, default, new MeshOpts { Occ = Occ.Box });
            var cab = new List<Vector2> { P(.5f, 0), P(-.05f, .43f), P(-1.42f, .43f), P(-1.82f, 0) };
            o.Mesh(o.Comp("유리", "dust+bird"), MeshKit.Extrude(cab, 1.38f).Xf(0, .98f, -.69f), Mats.CarGlass, Vector3.zero);
            o.Mesh(o.Comp("지붕", "bird+dust+leaves"), MeshKit.RoundedBox(1.42f, .05f, 1.36f, .02f), paint, V(-.73f, 1.43f, 0));
            var wc = o.Comp("휠", "mud+grime");
            foreach (float wx in new[] { -1.25f, 1.25f })
                foreach (int s in new[] { 1, -1 })
                {
                    o.Deco(MeshKit.Lathe(new float[,] { { .2f, -.1f }, { .3f, -.11f }, { .33f, -.07f }, { .34f, 0 }, { .33f, .07f }, { .3f, .11f }, { .2f, .1f } }, 28).Xf(0, 0, 0, 90), Mats.Rubber, V(wx, .34f, s * .82f));
                    o.Mesh(wc, MeshKit.DiscZ(.2f), Mats.Hubcap, V(wx, .34f, s * .935f), V(0, s > 0 ? 0 : 180, 0));
                }
            foreach (int s in new[] { 1, -1 })
            {
                o.Deco(MeshKit.RoundedBox(.06f, .1f, .28f, .03f), Mats.Headlight, V(2.1f, .65f, s * .55f), default, new MeshOpts { NoRay = true });
                o.Deco(MeshKit.RoundedBox(.05f, .1f, .3f, .03f), Mats.Taillight, V(-2.07f, .72f, s * .55f), default, new MeshOpts { NoRay = true });
            }
            o.Volume(V(4.3f, 1.5f, 1.8f), V(0, .75f, 0), default, true, true);
            return o;
        }

        public static MeshData Chair()
        {
            var md = new MeshData();
            md.Add(MeshKit.RoundedBox(.42f, .05f, .42f, .02f).Xf(0, .45f, 0));
            md.Add(MeshKit.RoundedBox(.42f, .4f, .04f, .02f).Xf(0, .7f, -.2f, -.1f * R2D));
            foreach (var p in new[] { P(-.18f, -.18f), P(.18f, -.18f), P(-.18f, .18f), P(.18f, .18f) })
                md.Add(MeshKit.Cylinder(.015f, .015f, .45f, 8).Xf(p.x, .225f, p.y));
            return md;
        }

        public static JobObject QuarterPipe(CleanJob job, string name, float x, float z, float rotDeg, float w, float r)
        {
            var o = job.Object(name, V(x, 0, z), rotDeg, "grime+gum");
            o.Mesh(o.Comp("경사면", "grime+leaves"), MeshKit.Ramp(w, r), Mats.Skate, Vector3.zero, default, new MeshOpts { Double = true });
            o.Mesh(o.Comp("데크", "dust+gum"), MeshKit.RoundedBox(w, r, 1.2f, .03f), Mats.Skate, V(0, r / 2, -r - .6f), default, new MeshOpts { Occ = Occ.Box });
            o.Mesh(o.Comp("코핑", "rust"), MeshKit.LineTube(V(-w / 2, r + .01f, -r), V(w / 2, r + .01f, -r), .045f, 12), Mats.Metal, Vector3.zero, default, new MeshOpts { Occ = Occ.Box });
            var sh = new List<Vector2> { P(0, 0) };
            for (int i = 1; i <= 16; i++) { float a = i / 16f * Mathf.PI / 2; sh.Add(P(-r * Mathf.Sin(a), r * (1 - Mathf.Cos(a)))); }
            sh.Add(P(-r, 0));
            var side = Mats.Solid(G.Hex(0xb3afa6), .1f);
            foreach (float sx in new[] { -w / 2, w / 2 }) o.Deco(MeshKit.Polygon(sh), side, V(sx, 0, 0), V(0, -90, 0));
            var am = Mats.Solid(G.Hex(0x8c8a85), .1f);
            foreach (var p in new[] { P(.4f, 1.2f), P(.8f, .8f), P(1.2f, .4f) })
            {
                float hh = p.x, px = w / 2 + .2f + p.y - .4f + .2f;
                o.Deco(MeshKit.RoundedBox(.4f, hh, 1.2f, .02f), am, V(px, hh / 2, -r - .6f));
                o.Volume(V(.4f, hh, 1.2f), V(px, hh / 2, -r - .6f), default, true);
            }
            o.Volume(V(w, r, r + 1.2f), V(0, r / 2, -(r + 1.2f) / 2), default, true, true);
            return o;
        }

        public static void Fence(CleanJob job, string name, float xa, float xb, float zLine)
        {
            var o = job.Object(name, Vector3.zero, 0, "moss+mildew");
            var c = o.Comp("울타리판"); var pc = o.Comp("기둥", "moss+bird");
            int n = Mathf.Max(1, Mathf.RoundToInt((xb - xa) / 2.2f));
            float seg = (xb - xa) / n;
            var pk = new List<Vector2> { P(-.045f, 0), P(.045f, 0), P(.045f, .82f), P(0, .9f), P(-.045f, .82f) };
            var picket = MeshKit.Extrude(pk, .02f);
            var parts = new MeshData(); var posts = new MeshData();
            for (int i = 0; i <= n; i++)
            {
                posts.Add(MeshKit.RoundedBox(.1f, 1.15f, .1f, .02f).Xf(xa + i * seg, .575f, zLine));
                posts.Add(MeshKit.Sphere(.06f, 10, 6).Xf(xa + i * seg, 1.18f, zLine));
            }
            for (int i = 0; i < n; i++)
            {
                float x0 = xa + i * seg + .06f, x1 = xa + (i + 1) * seg - .06f;
                int cnt = Mathf.FloorToInt((x1 - x0) / .13f);
                for (int k = 0; k < cnt; k++) parts.Add(picket, Matrix4x4.Translate(V(x0 + .065f + k * .13f, .12f, zLine + .03f)));
                parts.Add(MeshKit.RoundedBox(x1 - x0, .07f, .03f, .01f).Xf((x0 + x1) / 2, .35f, zLine));
                parts.Add(MeshKit.RoundedBox(x1 - x0, .07f, .03f, .01f).Xf((x0 + x1) / 2, .78f, zLine));
            }
            o.Mesh(c, parts, Mats.WhiteTrim, Vector3.zero);
            o.Mesh(pc, posts, Mats.WhiteTrim, Vector3.zero);
        }

        /// <summary>Looping water particles for fountains (jet straight up, or a ring dripping over a basin).</summary>
        public static void FountainWater(Transform parent, Vector3 pos, bool jet)
        {
            var go = new GameObject(jet ? "jet" : "ring");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = jet ? .9f : .55f;
            main.startSpeed = jet ? 3.4f : .6f;
            main.startSize = jet ? .05f : .04f;
            main.gravityModifier = 1f;
            main.maxParticles = 1500;
            main.startColor = new Color(.86f, .94f, 1f, .5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = jet ? 260 : 320;
            var sh = ps.shape;
            if (jet) { sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6; sh.radius = .02f; go.transform.localEulerAngles = V(-90, 0, 0); }
            else { sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.3f; sh.radiusThickness = 0; go.transform.localEulerAngles = V(-90, 0, 0); sh.arcMode = ParticleSystemShapeMultiModeValue.Random; }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Particle;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = .03f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        public static void PointLight(Transform parent, Vector3 pos, float range = 9, float intensity = 1.6f)
        {
            var go = new GameObject("lamp");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = G.Hex(0xfff0d8);
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }
    }
}
