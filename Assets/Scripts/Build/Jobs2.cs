using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    /// <summary>The seven larger jobs, ported from the web game.</summary>
    public static class Jobs2
    {
        const float R2D = Mathf.Rad2Deg;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector2 P(float x, float y) => new Vector2(x, y);
        static MeshOpts Occ(Banjjak.Occ o) => new MeshOpts { Occ = o };
        static readonly MeshOpts OBox = new MeshOpts { Occ = Banjjak.Occ.Box };
        static readonly MeshOpts Dbl = new MeshOpts { Double = true };
        static readonly MeshOpts NoRay = new MeshOpts { NoRay = true };
        static MeshData Box(float w, float h, float d, float r = .015f) => MeshKit.RoundedBox(w, h, d, r);

        // ============================================================ camper
        public static void BuildCamper(CleanJob job)
        {
            Jobs.GroundGrid(job, "진입로", 0, 0, 10, 7, 2, 1, Mats.Concrete, "mud+oil+leaves");
            var v = job.Object("캠핑카", V(0, 0, -.6f), 0, "grime+bird");
            v.HighOK = true;
            v.Mesh(v.Comp("차체"), MeshKit.RoundedBox(4.0f, 2.05f, 2.1f, .2f, 3, new[] { 0, 1, 4, 5 }),
                Mats.Faces(Mats.CamperFront, Mats.CamperBack, Mats.White, Mats.DarkUnder, Mats.CamperSide, Mats.CamperSide), V(-.5f, 1.825f, 0), default, OBox);
            v.Mesh(v.Comp("보닛"), MeshKit.RoundedBox(1.0f, 1.0f, 2.0f, .16f, 3, new[] { 0, 4, 5 }),
                Mats.Faces(Mats.HoodFront, Mats.White, Mats.White, Mats.DarkUnder, Mats.HoodSide, Mats.HoodSide), V(2.0f, 1.3f, 0), default, OBox);
            var bump = v.Comp("범퍼", "mud+grime");
            v.Mesh(bump, MeshKit.Merge(Box(.16f, .26f, 2.14f, .07f).Xf(2.58f, .72f, 0), Box(.16f, .26f, 2.14f, .07f).Xf(-2.58f, .72f, 0)), Mats.Metal, Vector3.zero);
            var wc = v.Comp("휠", "mud+grime");
            foreach (float wx in new[] { 1.9f, -1.7f })
                foreach (int zs in new[] { 1, -1 })
                {
                    v.Deco(MeshKit.Lathe(new float[,] { { .26f, -.12f }, { .36f, -.13f }, { .4f, -.08f }, { .41f, 0 }, { .4f, .08f }, { .36f, .13f }, { .26f, .12f } }, 28).Xf(0, 0, 0, 90), Mats.Rubber, V(wx, .41f, zs * .86f));
                    v.Mesh(wc, MeshKit.DiscZ(.27f), Mats.Hubcap, V(wx, .41f, zs * .995f), V(0, zs > 0 ? 0 : 180, 0));
                }
            var mc = v.Comp("사이드미러", "dust+bird");
            foreach (int zs in new[] { 1, -1 })
            {
                v.Deco(MeshKit.LineTube(V(1.5f, 2.05f, zs * 1.04f), V(1.55f, 2.1f, zs * 1.18f), .015f), Mats.Rubber, Vector3.zero);
                v.Mesh(mc, Box(.06f, .28f, .16f, .03f), Mats.BlackMetal, V(1.56f, 2.0f, zs * 1.24f));
            }
            v.Mesh(v.Comp("지붕 에어컨", "dust+bird"), MeshKit.RoundedBox(.9f, .28f, .75f, .09f, 3), Mats.White, V(-1.2f, 2.99f, 0), default, OBox);
            v.Deco(Box(4.7f, .3f, 1.4f, .05f), Mats.DarkUnder, V(0, .62f, 0));
            v.Volume(V(5.3f, .8f, 2.1f), V(-.05f, .4f, 0), default, true, true);

            var sc = job.DecoObj("steps");
            sc.Deco(Box(3.2f, .08f, .9f, .02f), Mats.Diamond, V(-.8f, 1.56f, 1.95f));
            sc.Volume(V(3.2f, 1.6f, .9f), V(-.8f, .8f, 1.95f), default, true, true);
            foreach (var p in new[] { P(-2.35f, 1.55f), P(-2.35f, 2.35f), P(.75f, 1.55f), P(.75f, 2.35f) })
            {
                float ph = p.y > 2 ? 2.6f : 1.56f;   // outer posts also carry the handrail
                sc.Deco(MeshKit.Cylinder(.03f, .03f, ph, 10), Mats.Alu, V(p.x, ph / 2, p.y));
            }
            sc.Volume(V(3.1f, 1.0f, .06f), V(-.8f, 2.1f, 2.37f));
            foreach (var s in new[] { P(1.2f, 1.0f), P(.8f, 1.4f), P(.4f, 1.8f) })
            {
                sc.Deco(Box(.4f, .06f, .9f, .02f), Mats.Diamond, V(s.y, s.x - .03f, 1.95f));
                sc.Volume(V(.4f, s.x, .9f), V(s.y, s.x / 2, 1.95f), default, true, true);
            }
            sc.Deco(MeshKit.LineTube(V(-2.35f, 2.6f, 2.37f), V(.75f, 2.6f, 2.37f), .025f), Mats.Alu, Vector3.zero);
            Props.WheelieBin(job, 5.6f, -3.0f, .3f * R2D);
            Props2.Mailbox(job, -6.4f, 3.4f, .4f * R2D);
            var d = job.DecoObj();
            Props.Bush(d, -7, -4.5f, 1.2f); Props.Bush(d, 7.2f, 4.8f, 1.2f);
            Props.Tree(d, -7.4f, 7.2f, 1.1f, true, 0);
        }

        // ============================================================ playground
        public static void BuildPark(CleanJob job)
        {
            Jobs.GroundGrid(job, "고무 바닥", 0, 0, 12, 12, 2, 2, (r, c) => (r + c) % 2 == 1 ? Mats.RubberGreen : Mats.RubberRed, "mud+leaves+gum");
            var s = job.Object("미끄럼틀", V(-3.2f, 0, -2.4f), 0, "grime+leaves");
            var pg = new MeshData();
            foreach (float sx in new[] { -.6f, .6f })
                foreach (float sz in new[] { -.6f, .6f })
                {
                    pg.Add(MeshKit.Cylinder(.06f, .06f, 2.5f, 16).Xf(sx, 1.25f, sz));
                    pg.Add(MeshKit.Sphere(.075f, 12, 8).Xf(sx, 2.5f, sz));
                    s.OccCyl(.065f, 0, 2.5f, sx, sz);
                }
            s.Mesh(s.Comp("기둥", "grime+rust"), pg, Mats.RedMetal, Vector3.zero);
            s.Mesh(s.Comp("발판", "mud+leaves"), Box(1.36f, .08f, 1.36f, .025f), Mats.Yellow, V(0, 1.5f, 0), default, OBox);
            var rl = new MeshData();
            foreach (float sz in new[] { .6f, -.6f })
            {
                rl.Add(MeshKit.LineTube(V(-.6f, 2.15f, sz), V(.6f, 2.15f, sz), .025f));
                rl.Add(MeshKit.LineTube(V(-.6f, 1.85f, sz), V(.6f, 1.85f, sz), .02f));
            }
            s.Mesh(s.Comp("난간", "grime"), rl, Mats.Yellow, Vector3.zero);
            s.Deco(MeshKit.Cone(1.05f, .55f, 32), Mats.RedMetal, V(0, 2.8f, 0), default, NoRay);
            var prof = new[] { P(-.36f, .14f), P(-.36f, 0), P(-.33f, -.04f), P(.33f, -.04f), P(.36f, 0), P(.36f, .14f), P(.32f, .14f), P(.32f, .02f), P(-.32f, .02f), P(-.32f, .14f) };
            s.Mesh(s.Comp("슬라이드", "grime+leaves"), MeshKit.Sweep(prof, new[] { V(.66f, 1.52f, 0), V(1.3f, 1.25f, 0), V(2.2f, .6f, 0), V(2.9f, .33f, 0), V(3.4f, .3f, 0) }, 48, Vector3.forward), Mats.SlideBlue, Vector3.zero, default, Dbl);
            var lad = new MeshData();
            foreach (float sz in new[] { -.3f, .3f }) lad.Add(MeshKit.LineTube(V(-.85f, 0, sz), V(-.68f, 1.5f, sz), .025f));
            foreach (float y in new[] { .35f, .7f, 1.05f }) lad.Add(MeshKit.LineTube(V(-.85f + y * .11f, y, -.3f), V(-.85f + y * .11f, y, .3f), .02f));
            s.Mesh(s.Comp("사다리", "rust+grime"), lad, Mats.Metal, Vector3.zero);
            s.Volume(V(1.4f, 1.54f, 1.4f), V(0, .77f, 0));
            s.Volume(V(1.3f, 1.5f, .8f), V(1.3f, .75f, 0));
            s.Volume(V(1.2f, .75f, .8f), V(2.6f, .375f, 0));
            s.OccBox(V(2.8f, 1.6f, .8f), V(2.0f, .8f, 0), true);

            var w = job.Object("그네", V(3.2f, 0, -2.8f), 0, "grime+rust");
            var lg = new MeshData();
            foreach (float ex in new[] { -1.5f, 1.5f })
                foreach (int ez in new[] { 1, -1 })
                    lg.Add(MeshKit.Tube(new[] { V(ex, 0, ez * .8f), V(ex, 2.3f, 0) }, .055f, 4, 12));
            w.Mesh(w.Comp("기둥"), lg, Mats.RedMetal, Vector3.zero);
            w.Mesh(w.Comp("가로대"), MeshKit.Tube(new[] { V(-1.65f, 2.3f, 0), V(1.65f, 2.3f, 0) }, .065f, 4, 14), Mats.RedMetal, Vector3.zero, default, OBox);
            foreach (var (sx, nm) in new[] { (-.7f, "그네 1"), (.7f, "그네 2") })
            {
                foreach (float cx in new[] { -.22f, .22f }) w.Deco(MeshKit.Cylinder(.01f, .01f, 1.75f, 6), Mats.Alu, V(sx + cx, 1.425f, 0), default, NoRay);
                w.Mesh(w.Comp(nm, "grime+leaves"), MeshKit.Sweep(new[] { P(-.1f, .02f), P(.1f, .02f), P(.1f, -.02f), P(-.1f, -.02f) }, new[] { V(sx - .25f, .58f, 0), V(sx, .5f, 0), V(sx + .25f, .58f, 0) }, 16, Vector3.forward), Mats.Seat, Vector3.zero, default, Dbl);
            }
            var dm = job.Object("돔 놀이기구", V(3.4f, 0, 3.2f), 0, "moss+algae");
            dm.Mesh(dm.Comp("돔"), MeshKit.Sphere(1.1f, 48, 16, Mathf.PI / 2), Mats.Orange, Vector3.zero, default, new MeshOpts { Occ = Banjjak.Occ.Sphere, OccR = 1.1f, Double = true });
            dm.Volume(V(1.7f, 1.0f, 1.7f), V(0, .5f, 0));
            var sb = job.Object("모래 놀이터", V(-8.3f, 0, 2.5f), 0, "moss+mildew");
            var logs = new MeshData();
            foreach (float zz in new[] { -1.15f, 1.15f }) logs.Add(MeshKit.Cylinder(.12f, .12f, 2.5f, 16).Xf(0, .12f, zz, 0, 0, 90));
            foreach (float xx in new[] { -1.15f, 1.15f }) logs.Add(MeshKit.Cylinder(.12f, .12f, 2.1f, 16).Xf(xx, .12f, 0, 90));
            sb.Mesh(sb.Comp("통나무 테두리"), logs, Mats.Wood, Vector3.zero);
            sb.Deco(MeshKit.Plane(2.1f, 2.1f), Mats.Sand, V(0, .025f, 0), default, new MeshOpts { NoShadow = true });
            job.Paved.Add(Rect.MinMaxRect(-9.7f, 1.1f, -6.9f, 3.9f));
            Props.ParkBench(job, "벤치 (왼쪽)", -2.6f, 7.6f, 180, "moss+bird+leaves");
            Props.ParkBench(job, "벤치 (오른쪽)", 2.6f, 7.6f, 180, "moss+bird+leaves");
            Props.WheelieBin(job, 7.6f, 6.6f, -.5f * R2D);
            var d = job.DecoObj();
            Props.Bush(d, -9.5f, -7.5f, 1.2f); Props.Bush(d, 8.5f, -7.8f, 1.2f); Props.Bush(d, -9.6f, 8.5f, 1.2f);
            Props.Tree(d, 8.4f, 8.6f, 1, true, 2);
        }

        // ============================================================ house
        public static void BuildHouse(CleanJob job)
        {
            Jobs.GroundGrid(job, "차고 앞 진입로", 3.7f, 4, 4, 12, 1, 3, Mats.Concrete, "oil+tire+leaves");
            Jobs.GroundGrid(job, "현관 앞 길", -1.8f, 5.55f, 1.6f, 8.9f, 1, 2, Mats.Pavers, "mud+moss+leaves");
            job.Paved.Add(Rect.MinMaxRect(-6.3f, -11.7f, 6.3f, -1.7f));
            job.Paved.Add(Rect.MinMaxRect(-4.3f, -2.1f, .7f, 1.2f));
            var h = job.Object("집 외벽", Vector3.zero, 0, "grime+mildew");
            h.Mesh(h.Comp("1층 외벽"), MeshKit.Quad(12, 3), Mats.Siding, V(0, 1.5f, -2));
            h.Mesh(h.Comp("2층 외벽", "grime+mildew+bird"), MeshKit.Quad(12, 3), Mats.Siding, V(0, 4.5f, -2));
            h.Mesh(h.Comp("왼쪽 벽"), MeshKit.Quad(9.5f, 6), Mats.Siding, V(-6, 3, -6.75f), V(0, -90, 0));
            h.Mesh(h.Comp("오른쪽 벽"), MeshKit.Quad(9.5f, 6), Mats.Siding, V(6, 3, -6.75f), V(0, 90, 0));
            h.Volume(V(12, 6, 9.5f), V(0, 3, -6.75f));
            h.Deco(MeshKit.Quad(12, 6), Mats.Siding, V(0, 3, -11.5f), V(0, 180, 0), NoRay);
            var trim = Mats.WhiteTrim;
            h.Deco(Box(12.2f, .14f, .08f, .02f), trim, V(0, 3, -1.96f)); h.OccBox(V(12.2f, .14f, .08f), V(0, 3, -1.96f));
            foreach (float sx in new[] { -6f, 6f }) { h.Deco(Box(.16f, 6, .16f, .03f), trim, V(sx, 3, -2)); h.OccBox(V(.16f, 6, .16f), V(sx, 3, -2)); }
            h.Deco(Box(12.6f, .24f, .2f, .04f), trim, V(0, 6.04f, -1.92f)); h.OccBox(V(12.6f, .24f, .2f), V(0, 6.04f, -1.92f));
            var gut = Mats.Solid(G.Hex(0xd6d7d5), .6f, .4f);
            h.Deco(MeshKit.Cylinder(.08f, .08f, 12.8f, 16, true).Xf(0, 0, 0, 0, 0, 90), gut, V(0, 5.93f, -1.74f), default, Dbl);
            foreach (float sx in new[] { -5.8f, 5.8f })
            {
                h.Deco(MeshKit.Tube(new[] { V(sx, 5.9f, -1.74f), V(sx, 5.75f, -1.86f), V(sx, .15f, -1.88f), V(sx + (sx < 0 ? .15f : -.15f), .05f, -1.75f) }, .05f, 24), gut, Vector3.zero);
                h.OccBox(V(.12f, 6, .14f), V(sx, 3, -1.88f));
            }
            var roof = MeshKit.Extrude(new[] { P(-5.35f, 0), P(5.35f, 0), P(0, 2.7f) }, 12.8f, true).Xf(0, 0, -6.4f);
            h.Mesh(null, roof, new[] { Mats.Solid(G.Hex(0xb6c4cb), .2f), Mats.RoofSlate }, V(0, 6.1f, -6.75f), V(0, 90, 0));
            h.Deco(Box(.9f, 1.8f, .9f, .04f), Mats.Brick, V(3.6f, 7.8f, -8), default, NoRay);

            var win1 = h.Comp("1층 창문", "dust+grime"); var win2 = h.Comp("2층 창문", "dust+bird"); var frames = h.Comp("창틀", "grime+mildew");
            var frameParts = new MeshData(); var frameOcc = new List<(Vector3 size, Vector3 pos)>();
            void WindowAt(Comp c, float x, float y)
            {
                var gm = h.Mesh(c, MeshKit.Quad(1.2f, 1.4f), Mats.WindowGlass, V(x, y, -1.955f));
                h.OccFor(gm, V(1.22f, 1.42f, .01f), V(x, y, -2.0f));
                var f = new[] { (1.34f, .08f, 0f, .66f), (1.34f, .08f, 0f, -.66f), (.08f, 1.4f, .63f, 0f), (.08f, 1.4f, -.63f, 0f), (.05f, 1.32f, 0f, 0f), (1.24f, .05f, 0f, 0f) };
                foreach (var (fw, fh, fx, fy) in f) { frameParts.Add(Box(fw, fh, .07f, .012f).Xf(x + fx, y + fy, -1.945f)); frameOcc.Add((V(fw, fh, .07f), V(x + fx, y + fy, -1.945f))); }
                frameParts.Add(Box(1.46f, .06f, .16f, .02f).Xf(x, y - .74f, -1.92f)); frameOcc.Add((V(1.46f, .06f, .16f), V(x, y - .74f, -1.92f)));
            }
            WindowAt(win1, -4.6f, 1.6f); WindowAt(win1, .6f, 1.6f);
            foreach (float x in new[] { -4.5f, -1.8f, .9f, 3.7f }) WindowAt(win2, x, 4.5f);
            var fm = h.Mesh(frames, frameParts, Mats.WhiteTrim, Vector3.zero);
            foreach (var fo in frameOcc) h.OccFor(fm, fo.size, fo.pos);
            h.Mesh(h.Comp("현관문", "grime+dust"), MeshKit.RoundedBox(1.0f, 2.15f, .06f, .02f, 2, new[] { 4 }), Mats.Faces(trim, trim, trim, trim, Mats.DoorM, trim), V(-1.8f, 1.53f, -1.97f), default, OBox);
            h.Deco(MeshKit.Sphere(.035f, 12, 8), Mats.Solid(G.Hex(0xd9b247), .7f, .9f), V(-1.45f, 1.55f, -1.92f));
            h.Mesh(h.Comp("차고 문 (낙서)", "paint"), MeshKit.RoundedBox(3.0f, 2.4f, .08f, .02f, 2, new[] { 4 }), Mats.Faces(trim, trim, trim, trim, Mats.Garage, trim), V(3.7f, 1.2f, -1.97f), default, OBox);
            h.Deco(Box(3.3f, .14f, .16f, .03f), trim, V(3.7f, 2.47f, -1.94f)); h.OccBox(V(3.3f, .14f, .16f), V(3.7f, 2.47f, -1.94f));

            var p = job.Object("현관 데크", Vector3.zero, 0, "mud+leaves");
            var boards = new MeshData();
            for (int i = 0; i < 17; i++) boards.Add(Box(4.8f, .04f, .13f, .012f).Xf(-1.8f, .43f, -1.93f + i * .14f));
            p.Mesh(p.Comp("데크 바닥"), boards, Mats.Deck, Vector3.zero, default, OBox);
            p.Mesh(p.Comp("데크 옆판", "mud+mildew"), MeshKit.Merge(Box(4.86f, .4f, .04f, .01f).Xf(-1.8f, .2f, .4f), Box(.04f, .4f, 2.4f, .01f).Xf(-4.21f, .2f, -.8f), Box(.04f, .4f, 2.4f, .01f).Xf(.61f, .2f, -.8f)), Mats.Wood, Vector3.zero);
            p.Volume(V(4.8f, .45f, 2.4f), V(-1.8f, .225f, -.8f)); p.OccBox(V(4.74f, .38f, 2.34f), V(-1.8f, .19f, -.81f));
            p.Mesh(p.Comp("계단", "mud+leaves"), MeshKit.Merge(Box(1.6f, .06f, .36f, .015f).Xf(-1.8f, .27f, .58f), Box(1.6f, .06f, .36f, .015f).Xf(-1.8f, .12f, .94f)), Mats.Deck, Vector3.zero);
            p.Volume(V(1.6f, .3f, .36f), V(-1.8f, .15f, .58f)); p.Volume(V(1.6f, .15f, .36f), V(-1.8f, .075f, .94f));
            p.OccBox(V(1.56f, .2f, .32f), V(-1.8f, .1f, .58f)); p.OccBox(V(1.56f, .05f, .32f), V(-1.8f, .025f, .94f));
            var rp = new MeshData();
            foreach (var q in new[] { P(-4.15f, .35f), P(-2.65f, .35f), P(-.95f, .35f), P(.55f, .35f), P(-4.15f, -1.9f), P(.55f, -1.9f) }) rp.Add(Box(.09f, 1.0f, .09f, .02f).Xf(q.x, .95f, q.y));
            foreach (float y in new[] { .85f, 1.4f })
            {
                rp.Add(Box(1.5f, .05f, .07f).Xf(-3.4f, y, .35f)); rp.Add(Box(1.7f, .05f, .07f).Xf(-.2f, y, .35f));
                foreach (float x in new[] { -4.15f, .55f }) rp.Add(Box(.07f, .05f, 2.25f).Xf(x, y, -.775f));
            }
            for (int i = 0; i < 6; i++) { rp.Add(MeshKit.Cylinder(.016f, .016f, .53f, 8).Xf(-3.95f + i * .23f, 1.12f, .35f)); rp.Add(MeshKit.Cylinder(.016f, .016f, .53f, 8).Xf(-.75f + i * .25f, 1.12f, .35f)); }
            p.Mesh(p.Comp("난간", "grime+mildew"), rp, Mats.WhiteTrim, Vector3.zero);
            p.Volume(V(1.5f, 1.0f, .1f), V(-3.4f, .95f, .35f)); p.Volume(V(1.7f, 1.0f, .1f), V(-.2f, .95f, .35f));

            var ac = job.Object("실외기", V(-6.32f, 0, -4.2f), 0, "rust+grime");
            ac.Mesh(ac.Comp("본체"), MeshKit.RoundedBox(.6f, .8f, 1.0f, .05f, 2, new[] { 1 }), Mats.Faces(Mats.DarkMetal, Mats.AcUnit, Mats.DarkMetal, Mats.DarkMetal, Mats.DarkMetal, Mats.DarkMetal), V(0, .45f, 0), default, OBox);
            var pl = job.Object("화분", Vector3.zero, 0, "mud+lime");
            Props.Pot(pl, pl.Comp("화분 1"), -3.3f, .95f); Props.Pot(pl, pl.Comp("화분 2"), -.3f, .95f);
            Props.ParkBench(job, "정원 벤치", -8.5f, 4, 90, "moss+bird+pollen");
            Props.WheelieBin(job, 6.5f, -1.2f, 0);
            Props2.Mailbox(job, -.6f, 10.0f, 0);
            Props2.Fence(job, "울타리 (왼쪽)", -12, -2.7f, 10.8f);
            Props2.Fence(job, "울타리 (가운데)", -.9f, 1.6f, 10.8f);
            Props2.Fence(job, "울타리 (오른쪽)", 5.8f, 12, 10.8f);
            var d = job.DecoObj();
            Props.Tree(d, 9, 5, 1.1f, true, 0); Props.Tree(d, -9.5f, 8.5f, 1, true, 2);
            foreach (var b in new[] { P(-5.2f, -1.4f), P(-3.0f, -1.0f), P(1.4f, -1.3f), P(-7.2f, -1.6f), P(8.4f, -1.5f) }) Props.Bush(d, b.x, b.y, .9f);
        }

        // ============================================================ skate park
        public static void BuildSkate(CleanJob job)
        {
            Jobs.GroundGrid(job, "콘크리트 바닥", 0, 0, 26, 22, 3, 3, Mats.Skate, "grime+gum+leaves");
            Props2.QuarterPipe(job, "쿼터파이프 (뒤)", -6, -7.4f, 0, 6, 1.6f);
            Props2.QuarterPipe(job, "쿼터파이프 (오른쪽)", 9.2f, 0, -90, 6, 1.6f);
            var fb = job.Object("펀박스", V(-1, 0, 1.2f), 0, "grime+gum");
            fb.Mesh(fb.Comp("상판과 옆면"), Box(4, .6f, 2.4f, .04f), Mats.Skate, V(0, .3f, 0), default, OBox);
            float L = Mathf.Sqrt(1.8f * 1.8f + .6f * .6f), a = Mathf.Atan2(.6f, 1.8f);
            var tri = Mats.Solid(G.Hex(0xb3afa6), .1f);
            var rc = fb.Comp("경사로", "tire+grime");
            foreach (int s in new[] { 1, -1 })
            {
                fb.Mesh(rc, MeshKit.Plane(L, 2.4f, 4, 4), Mats.Skate, V(s * 2.9f, .3f, 0), V(0, 0, -s * a * R2D));
                var t = new[] { P(s * 2, 0), P(s * 3.8f, 0), P(s * 2, .6f) };
                foreach (float zz in new[] { -1.2f, 1.2f }) fb.Deco(MeshKit.Polygon(t), tri, V(0, 0, zz));
                fb.Volume(V(1.8f, .3f, 2.4f), V(s * 2.9f, .15f, 0));
            }
            fb.Volume(V(4, .6f, 2.4f), V(0, .3f, 0));
            fb.OccBox(V(7.6f, .6f, 2.4f), V(0, .3f, 0), true);
            fb.Mesh(fb.Comp("레일", "rust"), MeshKit.Merge(MeshKit.LineTube(V(-1.5f, .92f, 0), V(1.5f, .92f, 0), .04f, 12), MeshKit.Cylinder(.025f, .025f, .3f, 10).Xf(-1.3f, .76f, 0), MeshKit.Cylinder(.025f, .025f, .3f, 10).Xf(1.3f, .76f, 0)), Mats.Metal, Vector3.zero, default, OBox);
            var gr = job.Object("긴 레일", V(-4, 0, 6.5f), 0, "rust");
            var grm = new MeshData();
            grm.Add(MeshKit.LineTube(V(-3, .5f, 0), V(3, .5f, 0), .045f, 12));
            foreach (float px in new[] { -2.6f, 0, 2.6f }) grm.Add(Box(.06f, .48f, .06f).Xf(px, .24f, 0));
            gr.Mesh(gr.Comp("레일"), grm, Mats.Metal, Vector3.zero);
            var st = job.Object("계단 세트", V(6, 0, -5.5f), 0, "grime+gum");
            var prof = new[] { P(0, 0), P(0, .2f), P(-.35f, .2f), P(-.35f, .4f), P(-.7f, .4f), P(-.7f, .6f), P(-1.05f, .6f), P(-1.05f, .8f), P(-3.05f, .8f), P(-3.05f, 0) };
            st.Mesh(st.Comp("계단과 플랫폼"), MeshKit.Extrude(prof, 3).Xf(0, 0, -1.5f), Mats.Skate, Vector3.zero, V(0, -90, 0), OBox);
            foreach (var q in new[] { P(.2f, 0), P(.4f, -.35f), P(.6f, -.7f) }) st.Volume(V(3, q.x, .35f), V(0, q.x / 2, q.y - .175f));
            st.Volume(V(3, .8f, 2), V(0, .4f, -2.05f));
            var hr = new MeshData();
            hr.Add(MeshKit.Tube(new[] { V(1.6f, 1.05f, .1f), V(1.6f, 1.65f, -1.05f), V(1.6f, 1.65f, -2.2f), V(1.6f, 1.65f, -3.0f) }, .03f, 30));
            foreach (var q in new[] { P(.1f, 1.05f), P(-1.05f, 1.65f), P(-3.0f, 1.65f) }) hr.Add(MeshKit.Cylinder(.025f, .025f, q.y, 10).Xf(1.6f, q.y / 2, q.x));
            st.Mesh(st.Comp("핸드레일", "rust"), hr, Mats.Metal, Vector3.zero);
            var gw = job.Object("낙서 벽", V(2.5f, 0, 9.6f), 0, "paint");
            gw.Mesh(gw.Comp("낙서 벽면"), Box(9, 2.2f, .3f, .06f), Mats.Skate, V(0, 1.1f, 0), default, OBox);
            Props.ParkBench(job, "벤치 (위)", -11.6f, 3, 90, "dust+gum+bird");
            Props.ParkBench(job, "벤치 (아래)", -11.6f, 6.5f, 90, "dust+gum+bird");
            Props.WheelieBin(job, -11.8f, 9.6f, 90);
            var d = job.DecoObj();
            Props.Tree(d, 11.5f, 9.5f, 1, true, 1); Props.Tree(d, -11.5f, -9.5f, 1, true, 0);
        }

        // ============================================================ fountain plaza
        public static void BuildPlaza(CleanJob job)
        {
            Jobs.GroundGrid(job, "광장 바닥", 0, 0, 28, 24, 4, 4, Mats.Pavers, "dust+bird+gum+leaves");
            var f = job.Object("분수대", Vector3.zero, 0, "lime+algae");
            f.HighOK = true;
            f.Mesh(f.Comp("수반"), MeshKit.Lathe(new float[,] { { 3.55f, 0 }, { 3.55f, .08f }, { 3.45f, .12f }, { 3.42f, .45f }, { 3.5f, .5f }, { 3.5f, .6f }, { 3.38f, .64f }, { 3.05f, .64f }, { 3.0f, .6f }, { 3.0f, .42f } }, 72), Mats.StoneSmooth, Vector3.zero, default, Dbl);
            f.OccCyl(3.57f, -1, .2f, 0, 0, true);
            f.Volume(V(7.1f, .64f, 7.1f), V(0, .32f, 0));
            f.Deco(MeshKit.Disc(3.0f, 64), Mats.Water, V(0, .42f, 0), default, new MeshOpts { NoShadow = true });
            f.Mesh(f.Comp("중앙 기둥", "lime+moss"), MeshKit.Lathe(new float[,] { { .42f, .42f }, { .44f, .5f }, { .33f, .58f }, { .25f, .8f }, { .31f, 1.0f }, { .24f, 1.18f }, { .36f, 1.28f }, { .4f, 1.33f } }, 40), Mats.StoneSmooth, Vector3.zero, default,
                new MeshOpts { Occ = Banjjak.Occ.Cyl, OccR = .42f, OccY0 = .3f, OccY1 = 1.34f, Double = true });
            f.Mesh(f.Comp("윗 수반", "lime+algae"), MeshKit.Lathe(new float[,] { { .4f, 1.33f }, { .62f, 1.38f }, { 1.0f, 1.46f }, { 1.3f, 1.56f }, { 1.38f, 1.62f }, { 1.38f, 1.68f }, { 1.3f, 1.7f }, { 1.15f, 1.66f }, { 1.1f, 1.6f } }, 56), Mats.StoneSmooth, Vector3.zero, default, Dbl);
            f.Deco(MeshKit.Disc(1.1f, 48), Mats.Water, V(0, 1.6f, 0), default, new MeshOpts { NoShadow = true });
            f.Mesh(f.Comp("장식 구슬", "bird+lime"), MeshKit.Merge(MeshKit.Lathe(new float[,] { { .1f, 1.6f }, { .14f, 1.68f }, { .08f, 1.82f }, { .12f, 1.9f } }, 24), MeshKit.Sphere(.22f, 28, 16).Xf(0, 2.1f, 0)), Mats.StoneSmooth, Vector3.zero, default, Dbl);
            Props2.FountainWater(f.T, V(0, 2.34f, 0), true);
            Props2.FountainWater(f.T, V(0, 1.66f, 0), false);
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI / 6 + i * Mathf.PI / 3, x = Mathf.Cos(a) * 6.8f, z = Mathf.Sin(a) * 6.8f;
                Props.ParkBench(job, $"벤치 {i + 1}", x, z, Mathf.Atan2(-x, -z) * R2D, "bird+dust+pollen");
            }
            var lamps = new[] { P(-9.5f, -8.5f), P(9.5f, -8.5f), P(-9.5f, 8.5f), P(9.5f, 8.5f) };
            for (int i = 0; i < lamps.Length; i++) Props2.LampPost(job, $"가로등 {i + 1}", lamps[i].x, lamps[i].y);
            var beds = new[] { P(-11.5f, -2), P(11.5f, -2), P(-11.5f, 3.5f), P(11.5f, 3.5f) };
            for (int i = 0; i < beds.Length; i++)
            {
                var o = job.Object($"화단 {i + 1}", V(beds[i].x, 0, beds[i].y), 0, "moss+mildew");
                o.Mesh(o.Comp("돌 화단"), Box(1.7f, .7f, 1.7f, .06f), Mats.StoneSmooth, V(0, .35f, 0), default, OBox);
                o.Deco(MeshKit.Plane(1.56f, 1.56f), Mats.Soil, V(0, .705f, 0), default, NoRay);
                o.OccBox(V(1.56f, .04f, 1.56f), V(0, .7f, 0));
                Props.Tree(o, 0, 0, .55f, false, i % 3);
            }
            var pg = job.Object("퍼걸러", V(0, 0, -9.6f), 0, "moss+mildew");
            var posts = new MeshData();
            foreach (float sx in new[] { -2f, 2f }) foreach (float sz in new[] { -1f, 1f }) posts.Add(Box(.2f, 2.6f, .2f, .03f).Xf(sx, 1.3f, sz));
            var pm = pg.Mesh(pg.Comp("기둥"), posts, Mats.Wood, Vector3.zero);
            foreach (float sx in new[] { -2f, 2f }) foreach (float sz in new[] { -1f, 1f }) { pg.OccFor(pm, V(.2f, 2.6f, .2f), V(sx, 1.3f, sz)); pg.Volume(V(.2f, 2.6f, .2f), V(sx, 1.3f, sz)); }
            var beams = MeshKit.Merge(Box(4.8f, .22f, .16f, .03f).Xf(0, 2.71f, -1), Box(4.8f, .22f, .16f, .03f).Xf(0, 2.71f, 1));
            var bm = pg.Mesh(pg.Comp("보"), beams, Mats.Wood, Vector3.zero);
            foreach (float sz in new[] { -1f, 1f }) pg.OccFor(bm, V(4.8f, .22f, .16f), V(0, 2.71f, sz));
            var raf = new MeshData();
            for (int i = 0; i < 6; i++) raf.Add(Box(.1f, .14f, 2.9f, .02f).Xf(-2.2f + i * .88f, 2.89f, 0));
            pg.Mesh(pg.Comp("서까래"), raf, Mats.Wood, Vector3.zero);
            Props.ParkBench(job, "퍼걸러 벤치", 0, -10.1f, 0, "bird+moss");
            Props2.RoundBin(job, 13, 7.5f); Props2.RoundBin(job, -13, -7.5f);
            Props2.Bollards(job, "볼라드", new[] { P(-4, 11.4f), P(-2, 11.4f), P(0, 11.4f), P(2, 11.4f), P(4, 11.4f) });
        }

        // ============================================================ gas station
        public static void BuildStation(CleanJob job)
        {
            Jobs.GroundGrid(job, "주유소 바닥", 0, 0, 38, 30, 4, 4, Mats.Asphalt, "oil+tire+gum+leaves");
            var c = job.Object("캐노피", V(0, 0, -.5f), 0, "grime+soot");
            var pil = new MeshData();
            foreach (float x in new[] { -5.5f, 5.5f }) foreach (float z in new[] { -3.2f, 3.2f }) pil.Add(Box(.5f, 5.0f, .5f, .08f).Xf(x, 2.5f, z));
            var cpm = c.Mesh(c.Comp("기둥"), pil, Mats.WhiteTrim, Vector3.zero);
            foreach (float x in new[] { -5.5f, 5.5f }) foreach (float z in new[] { -3.2f, 3.2f }) { c.Volume(V(.5f, 5, .5f), V(x, 2.5f, z)); c.OccFor(cpm, V(.5f, 5, .5f), V(x, 2.5f, z)); }
            c.Mesh(c.Comp("지붕 테두리와 천장", "grime+bird"), MeshKit.RoundedBox(14, .7f, 9, .1f, 2, new[] { 0, 1, 3, 4, 5 }),
                Mats.Faces(Mats.Fascia, Mats.Fascia, Mats.White, Mats.CanopyUnder, Mats.Fascia, Mats.Fascia), V(0, 5.35f, 0));
            foreach (float x in new[] { -2.6f, 2.6f })
            {
                var o = job.Object($"주유 섬 {(x < 0 ? 1 : 2)}", V(x, 0, -.5f), 0, "oil+grime");
                o.Mesh(o.Comp("받침대"), Box(1.2f, .2f, 4.6f, .06f), Mats.Concrete, V(0, .1f, 0), default, OBox);
                var pc = o.Comp("주유기", "grime+oil+dust");
                foreach (float z in new[] { -1.2f, 1.2f })
                {
                    o.Mesh(pc, MeshKit.RoundedBox(.8f, 1.8f, .5f, .06f, 2, new[] { 4, 5 }), Mats.Faces(Mats.White, Mats.White, Mats.White, Mats.DarkUnder, Mats.Pump, Mats.Pump), V(0, 1.1f, z), V(0, 90, 0), OBox);
                    o.Deco(MeshKit.Tube(new[] { V(.28f, 1.4f, z + .22f), V(.5f, 1.0f, z + .3f), V(.42f, .5f, z + .25f), V(.28f, .9f, z + .26f) }, .02f, 20), Mats.Rubber, Vector3.zero);
                }
                o.Volume(V(1.2f, 1.9f, 4.6f), V(0, .95f, 0));
                Props2.Bollards(job, $"볼라드 {(x < 0 ? 1 : 2)}", new[] { P(x, -3.2f), P(x, 2.2f) });
            }
            var s = job.Object("편의점", Vector3.zero, 0, "grime+mildew");
            s.Mesh(s.Comp("외벽"), MeshKit.Quad(16, 4.8f), Mats.Stucco, V(0, 2.4f, -9));
            s.Mesh(s.Comp("왼쪽 벽"), MeshKit.Quad(6, 4.8f), Mats.Stucco, V(-8, 2.4f, -12), V(0, -90, 0));
            s.Mesh(s.Comp("오른쪽 벽"), MeshKit.Quad(6, 4.8f), Mats.Stucco, V(8, 2.4f, -12), V(0, 90, 0));
            s.Volume(V(16, 4.8f, 6), V(0, 2.4f, -12));
            s.Deco(MeshKit.Quad(16, 4.8f), Mats.Stucco, V(0, 2.4f, -15), V(0, 180, 0), NoRay);
            s.Deco(Box(16.3f, .3f, 6.3f, .05f), Mats.Solid(G.Hex(0xd6d0c4), .2f), V(0, 4.95f, -12));
            job.Paved.Add(Rect.MinMaxRect(-8.3f, -15.3f, 8.3f, -8.7f));
            var gl = s.Comp("유리창", "dust+grime+bird"); var fr = s.Comp("창틀", "grime");
            var frames = new MeshData(); var focc = new List<(Vector3, Vector3)>();
            foreach (var q in new[] { P(-5.4f, 3.2f), P(-1.9f, 3.2f), P(1.9f, 1.8f), P(5.4f, 3.2f) })
            {
                float x = q.x, w = q.y;
                var gm = s.Mesh(gl, MeshKit.Quad(w, 2.5f), Mats.WindowGlass, V(x, 1.55f, -8.955f));
                s.OccFor(gm, V(w + .02f, 2.52f, .01f), V(x, 1.55f, -9.0f));
                foreach (var (fw, fh, fx, fy) in new[] { (w + .1f, .09f, x, 2.83f), (w + .1f, .09f, x, .27f), (.09f, 2.6f, x - w / 2, 1.55f), (.09f, 2.6f, x + w / 2, 1.55f) })
                {
                    frames.Add(Box(fw, fh, .08f, .015f).Xf(fx, fy, -8.945f));
                    focc.Add((V(fw, fh, .08f), V(fx, fy, -8.945f)));
                }
            }
            var sfm = s.Mesh(fr, frames, Mats.DarkMetal, Vector3.zero);
            foreach (var (sz, ps) in focc) s.OccFor(sfm, sz, ps);
            s.Mesh(s.Comp("간판", "grime+bird"), MeshKit.RoundedBox(15, .9f, .2f, .05f, 2, new[] { 4 }), Mats.Faces(Mats.White, Mats.White, Mats.White, Mats.White, Mats.StoreSign, Mats.White), V(0, 3.85f, -8.9f), default, OBox);
            var vm = job.Object("자판기", V(7.2f, 0, -8.4f), 0, "grime+dust");
            vm.Mesh(vm.Comp("자판기"), MeshKit.RoundedBox(1.0f, 1.9f, .8f, .05f, 2, new[] { 4 }), Mats.Faces(Mats.White, Mats.White, Mats.White, Mats.DarkUnder, Mats.Vending, Mats.White), V(0, .95f, 0), default, OBox);
            Props.ParkBench(job, "편의점 앞 벤치", -6.5f, -7.6f, 0, "gum+bird+dust");
            Props2.RoundBin(job, -4.6f, -7.9f); Props2.RoundBin(job, 4.6f, -7.9f);
            Props2.Hatchback(job, "손님 차", 11, 5, .35f * R2D, Mats.CarRed);
            var ws = job.Object("공기·물 충전기", V(-14, 0, 6), 90, "grime+rust");
            ws.Mesh(ws.Comp("충전기"), Box(.6f, 1.3f, .45f, .06f), Mats.RedMetal, V(0, .65f, 0), default, OBox);
            ws.Deco(MeshKit.Tube(new[] { V(.25f, 1.0f, .2f), V(.6f, .5f, .3f), V(.4f, .2f, .4f), V(.1f, .7f, .26f) }, .018f, 20), Mats.Rubber, Vector3.zero);
            var stops = job.Object("주차 스토퍼", Vector3.zero, 0, "oil+grime");
            var sm = new MeshData();
            foreach (float x in new[] { 12f, 14.5f, 17f }) sm.Add(Box(.2f, .12f, 1.8f, .04f).Xf(x, .06f, -7));
            stops.Mesh(stops.Comp("스토퍼"), sm, Mats.Concrete, Vector3.zero);
            var d = job.DecoObj();
            foreach (var t in new[] { P(-17.2f, -13), P(17, -13.5f), P(-17.5f, 12.8f), P(16.6f, 13) }) Props.Tree(d, t.x, t.y, 1.1f, true);
            Props.Bush(d, -12, -8.6f, 1); Props.Bush(d, 12, -8.6f, 1);
        }

        // ============================================================ three-storey building
        class Face
        {
            public bool AxisX;
            public Vector2 OutP, InP;
            public float OutRY, InRY, OutW, InW, Mid;
            public System.Func<float, float> OutU, InU;
            public string Side;
        }

        public static void BuildTower(CleanJob job)
        {
            const float X0 = -9, X1 = 9, Z0 = -15, Z1 = -3, T = .25f, FH = 3.5f;
            const int NF = 3;
            Jobs.GroundGrid(job, "앞 광장", 0, 2.5f, 24, 11, 3, 2, Mats.Pavers, "gum+leaves+bird");
            Jobs.GroundGrid(job, "주차장", 16.5f, -4.5f, 9, 21, 1, 3, Mats.Asphalt, "oil+tire+gum");
            Jobs.GroundGrid(job, "뒷골목", 0, -17, 24, 4, 3, 1, Mats.Concrete, "grime+oil+leaves");
            job.Paved.Add(Rect.MinMaxRect(X0 - .3f, Z0 - .3f, X1 + .3f, Z1 + .3f));
            job.NoLitter.Add(new Bounds(new Vector3(0, 10, -9), new Vector3(X1 - X0 + .8f, 40, Z1 - Z0 + .8f)));
            for (int f = 0; f < NF; f++)
                job.LitterZones.Add(new LitterZone { X0 = -8.3f, X1 = 8.3f, Z0 = -14.3f, Z1 = -3.7f, Y = f * FH + .02f, N = 14, Skip = new[] { Rect.MinMaxRect(5.2f, -15, 9, -10.2f) } });
            (float c, float w, float s, float h) Win(float c, float w = 2) => (c, w, .9f, 2.5f);
            var sides = new Dictionary<string, List<(float c, float w, float s, float h)>>
            {
                { "front", new List<(float, float, float, float)> { Win(-6.5f), Win(-3.25f), Win(3.25f), Win(6.5f) } },
                { "back", new List<(float, float, float, float)> { Win(-6), Win(-2), Win(2) } },
                { "left", new List<(float, float, float, float)> { Win(-6, 1.8f), Win(-12, 1.8f) } },
                { "right", new List<(float, float, float, float)> { Win(-6, 1.8f), Win(-12, 1.8f) } },
            };
            var faces = new[]
            {
                new Face { Side = "front", AxisX = true, OutP = P(0, Z1), OutRY = 0, OutW = 18, OutU = c => c + 9, InP = P(0, Z1 - T), InRY = 180, InW = 17.5f, InU = c => 8.75f - c, Mid = Z1 - T / 2 },
                new Face { Side = "back", AxisX = true, OutP = P(0, Z0), OutRY = 180, OutW = 18, OutU = c => 9 - c, InP = P(0, Z0 + T), InRY = 0, InW = 17.5f, InU = c => c + 8.75f, Mid = Z0 + T / 2 },
                new Face { Side = "right", AxisX = false, OutP = P(X1, -9), OutRY = 90, OutW = 12, OutU = c => -3 - c, InP = P(X1 - T, -9), InRY = -90, InW = 11.5f, InU = c => c + 14.75f, Mid = X1 - T / 2 },
                new Face { Side = "left", AxisX = false, OutP = P(X0, -9), OutRY = -90, OutW = 12, OutU = c => c + 15, InP = P(X0 + T, -9), InRY = 90, InW = 11.5f, InU = c => -3.25f - c, Mid = X0 + T / 2 },
            };
            string[] floorSpec = { "mud+gum+leaves", "dust+gum+grime", "oil+tire+dust" };
            Material[] floorMat = { Mats.Tile, Mats.Vinyl, Mats.Concrete };
            var hole = new[] { 14.25f, 7.25f, 17.5f, 11.5f };
            for (int f = 0; f < NF; f++)
            {
                float y0 = f * FH;
                var o = job.Object($"{f + 1}층", Vector3.zero, 0, "grime+mildew");
                var outC = o.Comp("외벽", "grime+mildew+bird"); var inC = o.Comp("실내 벽"); var glC = o.Comp("유리창", "dust+grime+bird"); var flC = o.Comp("바닥", floorSpec[f]);
                var outG = new MeshData(); var inG = new MeshData(); var glG = new MeshData(); var frG = new MeshData();
                foreach (var fc in faces)
                {
                    var ops = new List<(float c, float w, float s, float h)>(sides[fc.Side]);
                    if (fc.Side == "front") ops.Add(f == 0 ? (0f, 2.4f, 0f, 2.7f) : Win(0));
                    var outHoles = new List<float[]>(); var inHoles = new List<float[]>();
                    foreach (var op in ops)
                    {
                        outHoles.Add(new[] { fc.OutU(op.c) - op.w / 2, op.s, fc.OutU(op.c) + op.w / 2, op.h });
                        inHoles.Add(new[] { fc.InU(op.c) - op.w / 2, op.s, fc.InU(op.c) + op.w / 2, op.h });
                    }
                    outG.Add(MeshKit.Panel(fc.OutW, FH, outHoles).Xf(fc.OutP.x, y0 + FH / 2, fc.OutP.y, 0, fc.OutRY, 0));
                    inG.Add(MeshKit.Panel(fc.InW, 3.2f, inHoles).Xf(fc.InP.x, y0 + 1.6f, fc.InP.y, 0, fc.InRY, 0));
                    foreach (var op in ops)
                    {
                        float cy = y0 + (op.s + op.h) / 2, hh = op.h - op.s;
                        if (op.s > 0)
                            glG.Add(fc.AxisX ? MeshKit.Quad(op.w - .12f, hh - .12f).Xf(op.c, cy, fc.Mid) : MeshKit.Quad(op.w - .12f, hh - .12f).Xf(fc.Mid, cy, op.c, 0, 90, 0));
                        var parts = new List<(float a, float b, float off, float yy)> { (.06f, hh, -op.w / 2 + .03f, cy), (.06f, hh, op.w / 2 - .03f, cy), (op.w, .06f, 0, y0 + op.h - .03f) };
                        if (op.s > 0) parts.Add((op.w, .06f, 0, y0 + op.s + .03f));
                        foreach (var (a, b, ofs, yy) in parts)
                            frG.Add(fc.AxisX ? Box(a, b, T + .02f, .01f).Xf(op.c + ofs, yy, fc.Mid) : Box(T + .02f, b, a, .01f).Xf(fc.Mid, yy, op.c + ofs));
                    }
                }
                o.Mesh(outC, outG, f == 0 ? Mats.Brick : Mats.StuccoWarm, Vector3.zero);
                o.Mesh(inC, inG, Mats.Plaster, Vector3.zero);
                o.Mesh(glC, glG, Mats.WindowGlass, Vector3.zero, default, Dbl);
                o.Deco(frG, Mats.Alu, Vector3.zero);
                var holes = f > 0 ? new List<float[]> { hole } : new List<float[]>();
                o.Mesh(flC, MeshKit.Panel(17.5f, 11.5f, holes), floorMat[f], V(0, y0 + .015f, -9), V(-90, 0, 0), new MeshOpts { Ground = true, Cell = G.GroundCell, NoShadow = true });
                foreach (var r in MeshKit.RectsMinus(17.5f, 11.5f, holes))
                {
                    float w = r[2] - r[0], dd = r[3] - r[1];
                    o.Volume(V(w, .3f, dd), V(r[0] + w / 2 - 8.75f, y0 - .15f, -3.25f - (r[1] + dd / 2)));
                }
                var chole = f < NF - 1 ? new List<float[]> { new[] { 14.25f, 0f, 17.5f, 4.25f } } : new List<float[]>();
                o.Deco(MeshKit.Panel(17.5f, 11.5f, chole), Mats.Ceiling, V(0, y0 + 3.2f, -9), V(90, 0, 0), new MeshOpts { NoShadow = true });
                foreach (var q in new[] { P(-5, -6), P(-5, -12), P(0, -6), P(0, -12), P(4, -6) })
                    o.Deco(Box(1.2f, .04f, .6f, .015f), Mats.PanelLight, V(q.x, y0 + 3.18f, q.y), default, new MeshOpts { NoRay = true, NoShadow = true });
                Props2.PointLight(o.T, V(-4, y0 + 2.9f, -8));
                Props2.PointLight(o.T, V(2, y0 + 2.9f, -10));
                if (f == 0)
                {
                    o.Volume(V(7.8f, FH, T), V(-5.1f, y0 + FH / 2, Z1 - T / 2));
                    o.Volume(V(7.8f, FH, T), V(5.1f, y0 + FH / 2, Z1 - T / 2));
                    o.Volume(V(2.4f, FH - 2.7f, T), V(0, y0 + 2.7f + (FH - 2.7f) / 2, Z1 - T / 2));
                }
                else o.Volume(V(18, FH, T), V(0, y0 + FH / 2, Z1 - T / 2));
                o.Volume(V(18, FH, T), V(0, y0 + FH / 2, Z0 + T / 2));
                o.Volume(V(T, FH, 12), V(X0 + T / 2, y0 + FH / 2, -9));
                o.Volume(V(T, FH, 12), V(X1 - T / 2, y0 + FH / 2, -9));
            }
            var top = job.DecoObj("roof");
            top.Deco(Box(18.3f, .4f, 12.3f, .03f), Mats.Solid(G.Hex(0xc9c1b4), .1f), V(0, NF * FH + .2f, -9));
            top.Volume(V(18.3f, .4f, 12.3f), V(0, NF * FH + .2f, -9));
            top.Deco(Box(18.4f, .2f, 12.4f, .03f), Mats.Solid(G.Hex(0xe8e2d6), .15f), V(0, NF * FH + .5f, -9));
            var sign = job.Object("간판", Vector3.zero, 0, "grime+bird");
            sign.Mesh(sign.Comp("간판"), MeshKit.RoundedBox(6, .5f, .16f, .04f, 2, new[] { 4 }), Mats.Faces(Mats.White, Mats.White, Mats.White, Mats.White, Mats.TowerSign, Mats.White), V(0, 3.1f, Z1 + .08f), default, OBox);

            // stairwell: two flights + landing per storey
            var st = job.Object("계단실", Vector3.zero, 0, "mud+gum");
            const float R = .175f, TR = .28f;
            const int N = 10;
            MeshData Flight()
            {
                var s = new List<Vector2> { P(0, 0) };
                for (int i = 0; i < N; i++) { s.Add(P(i * TR, (i + 1) * R)); s.Add(P((i + 1) * TR, (i + 1) * R)); }
                s.Add(P(N * TR, N * R - .3f)); s.Add(P(.45f, 0));
                return MeshKit.Extrude(s, 1.3f);
            }
            var rails = new MeshData();
            for (int f = 0; f < NF - 1; f++)
            {
                float y0 = f * FH;
                st.Mesh(st.Comp($"{f + 1}→{f + 2}층 계단"), MeshKit.Merge(Flight().Xf(5.6f, y0, -10.5f, 0, 90, 0), Flight().Xf(8.4f, y0 + 1.75f, -13.3f, 0, -90, 0), Box(3.0f, .3f, 1.45f, .02f).Xf(7.0f, y0 + 1.6f, -14.025f)), Mats.StoneSmooth, Vector3.zero, default, Dbl);
                for (int i = 0; i < N; i++)
                {
                    st.Volume(V(1.3f, .3f, TR), V(6.25f, y0 + (i + 1) * R - .15f, -10.5f - (i + .5f) * TR));
                    st.Volume(V(1.3f, .3f, TR), V(7.75f, y0 + 1.75f + (i + 1) * R - .15f, -13.3f + (i + .5f) * TR));
                }
                st.Volume(V(3.0f, .3f, 1.45f), V(7.0f, y0 + 1.6f, -14.025f));
                rails.Add(MeshKit.LineTube(V(5.55f, y0 + 1.05f, -10.5f), V(5.55f, y0 + 2.8f, -13.3f), .022f));
                rails.Add(MeshKit.LineTube(V(8.45f, y0 + 2.8f, -13.3f), V(8.45f, y0 + 4.55f, -10.5f), .022f));
                rails.Add(MeshKit.LineTube(V(5.55f, y0 + 2.8f, -13.3f), V(5.55f, y0 + 2.8f, -14.7f), .022f, 8));
                rails.Add(MeshKit.LineTube(V(5.55f, y0 + 2.8f, -14.7f), V(8.45f, y0 + 2.8f, -14.7f), .022f, 8));
            }
            for (int f = 1; f < NF; f++)
            {
                float y = f * FH;
                rails.Add(MeshKit.LineTube(V(5.45f, y + 1.0f, -10.45f), V(5.45f, y + 1.0f, -14.7f), .025f));
                foreach (float z in new[] { -10.5f, -12.5f, -14.6f }) rails.Add(MeshKit.Cylinder(.02f, .02f, 1.0f, 8).Xf(5.45f, y + .5f, z));
                st.Volume(V(.1f, 1.0f, 4.3f), V(5.45f, y + .5f, -12.6f));
            }
            st.Mesh(st.Comp("계단 난간", "grime+rust"), rails, Mats.Metal, Vector3.zero);

            // 1F cafe
            var cafe = job.Object("1층 카페", Vector3.zero, 0, "grime+dust");
            cafe.Mesh(cafe.Comp("카운터"), MeshKit.Merge(Box(3.6f, 1.0f, .65f, .04f).Xf(-6.6f, .5f, -13.6f), Box(3.8f, .05f, .8f, .02f).Xf(-6.6f, 1.03f, -13.6f)), Mats.DeskTop, Vector3.zero, default, OBox);
            cafe.Volume(V(3.8f, 1.05f, .8f), V(-6.6f, .525f, -13.6f));
            var tbl = new MeshData();
            var chair = Props2.Chair();
            foreach (var q in new[] { P(-3, -7), P(.5f, -7.5f), P(-3, -10.8f), P(.8f, -11.2f), P(3.5f, -6) })
            {
                tbl.Add(MeshKit.Lathe(new float[,] { { .22f, 0 }, { .24f, .03f }, { .05f, .06f }, { .04f, .72f }, { .36f, .74f }, { .38f, .76f }, { .001f, .76f } }, 28).Xf(q.x, 0, q.y));
                cafe.Volume(V(.76f, .76f, .76f), V(q.x, .38f, q.y));
                for (int k = 0; k < 2; k++)
                {
                    float a = k * Mathf.PI + .4f;
                    cafe.Deco(chair, Mats.ChairWood, V(q.x + Mathf.Sin(a) * .65f, 0, q.y + Mathf.Cos(a) * .65f), V(0, (a + Mathf.PI) * R2D, 0));
                }
            }
            cafe.Mesh(cafe.Comp("테이블", "grime+gum"), tbl, Mats.DeskTop, Vector3.zero, default, Dbl);
            Props2.TrashStation(job, 4.6f, -4.0f, 0);
            Props.Pot(cafe, cafe.Comp("화분", "mud+lime"), -8.2f, -4.0f, .9f);

            // 2F office
            float y1 = FH;
            var off = job.Object("2층 사무실", Vector3.zero, 0, "dust+grime");
            var dk = new MeshData();
            foreach (float x in new[] { -6.5f, -3.5f, -.5f })
                foreach (float z in new[] { -6.2f, -9.6f })
                {
                    dk.Add(Box(1.6f, .04f, .8f).Xf(x, y1 + .74f, z));
                    dk.Add(Box(.04f, .72f, .7f, .01f).Xf(x - .76f, y1 + .36f, z));
                    dk.Add(Box(.04f, .72f, .7f, .01f).Xf(x + .76f, y1 + .36f, z));
                    off.Volume(V(1.6f, .76f, .8f), V(x, y1 + .38f, z));
                    off.Deco(chair, Mats.ChairDark, V(x, y1, z + .75f), V(0, 180, 0));
                }
            off.Mesh(off.Comp("책상"), dk, Mats.DeskTop, Vector3.zero);
            off.Mesh(off.Comp("화이트보드", "paint"), Box(2.6f, 1.2f, .04f, .01f), Mats.Whiteboard, V(-4, y1 + 1.6f, -14.7f), default, OBox);
            foreach (float z in new[] { -5f, -7f, -9f })
            {
                off.Deco(Box(.5f, 1.3f, .9f, .02f), Mats.Cabinet, V(-8.45f, y1 + .65f, z));
                off.Volume(V(.5f, 1.3f, .9f), V(-8.45f, y1 + .65f, z), default, true, true);
            }
            Props2.TrashStation(job, 4.6f, -4.2f, 0, y1);

            // 3F warehouse
            float y2 = FH * 2;
            var wh = job.Object("3층 창고", Vector3.zero, 0, "dust+rust");
            var sh = new MeshData(); var boxes = new MeshData();
            foreach (float x in new[] { -7f, -3.5f, 0f })
            {
                foreach (float z in new[] { -5.5f, -13.2f }) foreach (float dx in new[] { -1.1f, 1.1f }) sh.Add(Box(.06f, 2.2f, .06f, .01f).Xf(x + dx, y2 + 1.1f, z));
                foreach (float yy in new[] { .15f, .9f, 1.65f, 2.18f }) sh.Add(Box(2.3f, .04f, 7.8f, .01f).Xf(x, y2 + yy, -9.35f));
                wh.Volume(V(2.3f, 2.2f, 7.8f), V(x, y2 + 1.1f, -9.35f));
                wh.OccBox(V(2.3f, .1f, 7.8f), V(x, y2 + .05f, -9.35f), true);
                for (int k = 0; k < 6; k++) boxes.Add(Box(.5f, .35f, .45f, .01f).Xf(x + (k % 2 == 1 ? .5f : -.5f), y2 + .35f + (k / 2) * .75f, -6.5f - k * 1.1f));
            }
            wh.Mesh(wh.Comp("선반"), sh, Mats.Shelf, Vector3.zero);
            wh.Deco(boxes, Mats.Cardboard, Vector3.zero);
            Props2.TrashStation(job, 4.6f, -4.2f, 0, y2);

            Props2.Dumpster(job, -6, -17.6f, 0); Props2.Dumpster(job, 6, -17.6f, 0);
            Props.ParkBench(job, "광장 벤치 1", -6, 5.5f, 180, "bird+gum+dust");
            Props.ParkBench(job, "광장 벤치 2", 6, 5.5f, 180, "bird+gum+dust");
            Props2.LampPost(job, "가로등 1", -11, 7); Props2.LampPost(job, "가로등 2", 11, 7);
            Props2.RoundBin(job, -9.5f, 0); Props2.RoundBin(job, 9.5f, 0);
            Props2.Hatchback(job, "주차된 차", 16.5f, 0, (Mathf.PI / 2 + .1f) * R2D, Mats.CarRed);
            Props2.Bollards(job, "볼라드", new[] { P(-3, 7.7f), P(-1, 7.7f), P(1, 7.7f), P(3, 7.7f) });
            var dd2 = job.DecoObj();
            foreach (var t in new[] { P(-15, 10), P(15, 12), P(-19, -12) }) Props.Tree(dd2, t.x, t.y, 1.1f, true);
            Props.Bush(dd2, -11, -2, 1); Props.Bush(dd2, 11, -2, 1);
            foreach (var o in job.Objects) o.HighOK = true;
        }
    }
}
