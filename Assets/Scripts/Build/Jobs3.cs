using UnityEngine;

namespace Banjjak
{
    /// <summary>The two extra-large jobs made for co-op: a harbour quay and a school sports ground.</summary>
    public static class Jobs3
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector2 P(float x, float z) => new Vector2(x, z);
        static MeshData Box(float w, float h, float d, float r = .015f) => MeshKit.RoundedBox(w, h, d, r);
        const float R2D = Mathf.Rad2Deg;
        static readonly MeshOpts OBox = new MeshOpts { Occ = Occ.Box };
        static readonly MeshOpts NoRay = new MeshOpts { NoRay = true };

        // ============================================================ harbour quay
        public static void BuildHarbor(CleanJob job)
        {
            Jobs.GroundGrid(job, "부두 바닥", 0, 1.5f, 40, 25, 5, 4, Mats.Concrete, "oil+tire+grime+mud");

            // the canal along the front edge, with a stone quay wall and mooring posts
            var dk = job.DecoObj("canal");
            // the lawn plane covers y = 0 everywhere, so the canal is a brim-full strip of water just above it
            dk.Deco(MeshKit.Plane(70, 7.5f, 8, 2), Mats.Water, V(0, .03f, -15.1f), default, new MeshOpts { NoShadow = true, NoRay = true });
            job.Paved.Add(Rect.MinMaxRect(-36, -19, 36, -11.2f));
            job.NoLitter.Add(new Bounds(V(0, 0, -15.1f), V(72, 4, 8)));
            var q = job.Object("안벽", V(0, 0, -11.15f), 0, "algae+lime+bird");
            q.Mesh(q.Comp("안벽 연석"), Box(40, .32f, .5f, .04f), Mats.StoneSmooth, V(0, .16f, 0), default, OBox);
            q.Volume(V(80, 3, .3f), V(0, 1.5f, -.4f));   // nobody falls into the canal
            Props2.Bollards(job, "계류 기둥", new[] { P(-16, -10.55f), P(-8, -10.55f), P(0, -10.55f), P(8, -10.55f), P(16, -10.55f) }, "rust+bird");

            // fishing boat moored in the canal
            var b = job.Object("어선", V(5, 0, -15.3f), 0, "algae+lime+bird");
            var hull = MeshKit.Extrude(new[] { P(-3.6f, .2f), P(3.2f, .2f), P(3.9f, .7f), P(4.1f, 1.25f), P(-3.8f, 1.25f), P(-3.9f, .7f) }, 2.6f);
            b.Mesh(b.Comp("선체"), hull.Xf(0, 0, 1.3f), Mats.Solid(G.Hex(0xe9eef2), .4f), V(0, -.25f, 0), default, OBox);
            b.Mesh(b.Comp("갑판", "bird+oil+dust"), Box(7.6f, .08f, 2.5f, .02f), Mats.Deck, V(0, .98f, 0));
            b.Mesh(b.Comp("조타실", "bird+grime"), MeshKit.RoundedBox(2.0f, 1.5f, 1.9f, .1f, 3, new[] { 4 }), Mats.Faces(Mats.White, Mats.White, Mats.White, Mats.White, Mats.WindowGlass, Mats.White), V(-1.4f, 1.77f, 0), default, OBox);
            b.Mesh(b.Comp("돛대", "rust+bird"), MeshKit.Merge(MeshKit.Cylinder(.06f, .07f, 3.2f, 12).Xf(1.6f, 2.6f, 0), MeshKit.Cylinder(.03f, .03f, 2.2f, 8).Xf(1.6f, 3.4f, 0, 90, 0, 0)), Mats.Metal, Vector3.zero);

            // shipping containers at the back, two of them stacked
            var cc = new[] { G.Hex(0xb8432f), G.Hex(0x2f6fb0), G.Hex(0x3d8a4a), G.Hex(0xd9822b) };
            var spots = new[] { (-15.5f, 0f, 9.2f), (-15.5f, 2.62f, 9.2f), (-8.8f, 0f, 9.8f), (14.5f, 0f, 8.6f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var (x, y, z) = spots[i];
                var o = job.Object($"컨테이너 {i + 1}", V(x, y, z), 0, "rust+grime+bird");
                var m = Mats.Solid(cc[i], .35f, .25f);
                o.Mesh(o.Comp("철판"), MeshKit.RoundedBox(6.06f, 2.59f, 2.44f, .03f, 2), m, V(0, 1.3f, 0), default, OBox);
                o.Deco(Box(.06f, 2.4f, 2.3f), Mats.DarkMetal, V(3.04f, 1.3f, 0));
                if (y == 0) o.Volume(V(6.06f, 2.6f, 2.44f), V(0, 1.3f, 0));
            }
            for (int i = 0; i < 5; i++)
            {
                var o = job.Object($"나무 상자 {i + 1}", V(8.6f + (i % 3) * 1.05f, 0, 9.3f + (i / 3) * 1.05f), (i * 13) % 20, "mud+dust+bird");
                o.Mesh(o.Comp("상자"), Box(.95f, .95f, .95f, .03f), Mats.Wood, V(0, .475f, 0), default, OBox);
                o.Volume(V(.95f, .95f, .95f), V(0, .475f, 0));
            }

            // fish market stall under an awning
            var fm = job.Object("어시장 매대", V(5.5f, 0, 6.5f), 0, "lime+grime+bird");
            fm.Mesh(fm.Comp("매대"), Box(4.2f, .9f, 1.2f, .05f), Mats.StoneSmooth, V(0, .45f, 0), default, OBox);
            fm.Volume(V(4.2f, .9f, 1.2f), V(0, .45f, 0));
            var poles = new MeshData();
            foreach (float sx in new[] { -2.2f, 2.2f }) foreach (float sz in new[] { -1f, 1f }) poles.Add(MeshKit.Cylinder(.05f, .05f, 2.5f, 10).Xf(sx, 1.25f, sz));
            fm.Mesh(fm.Comp("천막 기둥", "rust+grime"), poles, Mats.Metal, Vector3.zero);
            fm.Mesh(fm.Comp("천막", "bird+dust+mildew"), Box(5f, .06f, 2.6f, .02f), Mats.Solid(G.Hex(0x3a7bd5), .3f), V(0, 2.53f, 0), V(-6, 0, 0));

            // warehouse wall with roller doors
            var w = job.Object("창고", V(0, 0, 13.6f), 0, "grime+mildew+bird");
            w.Mesh(w.Comp("창고 외벽"), MeshKit.Quad(30, 6), Mats.Siding, V(0, 3, 0), V(0, 180, 0));
            w.Deco(MeshKit.Quad(30, 6), Mats.Siding, V(0, 3, .02f), default, NoRay);
            w.Volume(V(30, 6, .4f), V(0, 3, .2f));
            foreach (float x in new[] { -7.5f, 0f, 7.5f })
                w.Mesh(w.Comp($"셔터 {(x < 0 ? 1 : x == 0 ? 2 : 3)}", "grime+rust"), MeshKit.RoundedBox(4f, 3.8f, .08f, .02f, 2, new[] { 5 }), Mats.Faces(Mats.Grey, Mats.Grey, Mats.Grey, Mats.Grey, Mats.Grey, Mats.Garage), V(x, 1.9f, -.05f), default, OBox);
            w.Mesh(w.Comp("간판", "grime+bird"), Box(8, 1.1f, .15f, .04f), Mats.Solid(G.Hex(0x1e4f8a), .4f), V(0, 4.9f, -.1f), default, OBox);
            job.Paved.Add(Rect.MinMaxRect(-15.5f, 13, 15.5f, 14.4f));

            for (int i = 0; i < 4; i++) Props2.LampPost(job, $"가로등 {i + 1}", -14 + i * 9.3f, -9.4f);
            Props.ParkBench(job, "벤치 (왼쪽)", -11, -8.6f, 180, "bird+dust+gum");
            Props.ParkBench(job, "벤치 (오른쪽)", 11, -8.6f, 180, "bird+dust+gum");
            Props2.RoundBin(job, -6, -8.8f); Props2.RoundBin(job, 6, -8.8f);
            Props2.Dumpster(job, 18.5f, 3, -90);
            Props2.Hatchback(job, "손님 차", 13, -1.5f, 25, Mats.CarRed);
            var d = job.DecoObj();
            foreach (var t in new[] { P(-21, 9), P(21, 11) }) Props.Tree(d, t.x, t.y, 1.1f, true);
        }

        // ============================================================ school sports ground
        public static void BuildSchool(CleanJob job)
        {
            Jobs.GroundGrid(job, "운동장 바닥", 0, 2, 44, 28, 6, 4, (r, c) => (r == 0 || r == 3 || c == 0 || c == 5) ? Mats.RubberRed : Mats.RubberGreen, "mud+gum+leaves+dust");

            // school building front with two rows of windows, a door and the name board
            var sc = job.Object("학교 건물", V(0, 0, -14.5f), 0, "grime+mildew+bird");
            sc.Mesh(sc.Comp("1층 외벽"), MeshKit.Quad(34, 3.6f), Mats.StuccoWarm, V(0, 1.8f, 0));
            sc.Mesh(sc.Comp("2층 외벽", "grime+mildew+bird"), MeshKit.Quad(34, 3.6f), Mats.StuccoWarm, V(0, 5.4f, 0));
            sc.Volume(V(34, 7.2f, 6), V(0, 3.6f, -3.05f));
            sc.Deco(Box(34.4f, .3f, .4f, .04f), Mats.WhiteTrim, V(0, 7.25f, .1f));
            job.Paved.Add(Rect.MinMaxRect(-17.5f, -21, 17.5f, -11.9f));
            var g1 = sc.Comp("1층 창문", "dust+grime"); var g2 = sc.Comp("2층 창문", "dust+bird");
            var frames = new MeshData();
            foreach (float y in new[] { 1.9f, 5.5f })
                for (int i = 0; i < 8; i++)
                {
                    float x = -14 + i * 4f;
                    if (y < 3 && Mathf.Abs(x) < 2.5f) continue;   // the door goes there
                    var gm = sc.Mesh(y < 3 ? g1 : g2, MeshKit.Quad(2.4f, 1.6f), Mats.WindowGlass, V(x, y, .045f));
                    sc.OccFor(gm, V(2.42f, 1.62f, .01f), V(x, y, 0));
                    frames.Add(Box(2.56f, .08f, .08f).Xf(x, y + .82f, .06f)); frames.Add(Box(2.56f, .08f, .08f).Xf(x, y - .82f, .06f));
                    frames.Add(Box(.08f, 1.64f, .08f).Xf(x - 1.24f, y, .06f)); frames.Add(Box(.08f, 1.64f, .08f).Xf(x + 1.24f, y, .06f));
                    frames.Add(Box(2.7f, .07f, .2f, .02f).Xf(x, y - .9f, .1f));   // sill
                }
            sc.Mesh(sc.Comp("창틀", "grime+mildew"), frames, Mats.WhiteTrim, Vector3.zero);
            sc.Mesh(sc.Comp("현관문", "grime+dust"), MeshKit.RoundedBox(3.0f, 2.5f, .08f, .02f, 2, new[] { 4 }), Mats.Faces(Mats.Alu, Mats.Alu, Mats.Alu, Mats.Alu, Mats.WindowGlass, Mats.Alu), V(0, 1.25f, .06f), default, OBox);
            sc.Mesh(sc.Comp("학교 이름판", "grime+bird"), Box(6, .9f, .12f, .03f), Mats.Solid(G.Hex(0x2f6b45), .4f), V(0, 3.15f, .08f), default, OBox);

            // basketball hoops at both ends of the court
            foreach (int s in new[] { -1, 1 })
            {
                var h = job.Object($"농구 골대 {(s < 0 ? 1 : 2)}", V(s * 18.5f, 0, 2), s < 0 ? 90 : -90, "rust+bird+grime");
                h.Mesh(h.Comp("기둥"), MeshKit.Merge(MeshKit.Cylinder(.09f, .1f, 3.2f, 14).Xf(0, 1.6f, -.6f), MeshKit.Cylinder(.06f, .06f, .9f, 10).Xf(0, 3.05f, -.2f, 90, 0, 0)), Mats.Blue, Vector3.zero);
                h.Volume(V(.25f, 3.2f, .25f), V(0, 1.6f, -.6f));
                h.Mesh(h.Comp("백보드", "dust+bird"), Box(1.8f, 1.1f, .06f, .02f), Mats.White, V(0, 3.35f, .25f), default, OBox);
                var rim = new Vector3[17];
                for (int i = 0; i <= 16; i++) { float a = i * Mathf.PI * 2 / 16; rim[i] = V(Mathf.Cos(a) * .23f, 3.05f, .55f + Mathf.Sin(a) * .23f); }
                h.Mesh(h.Comp("림", "rust"), MeshKit.Tube(rim, .018f, 32, 8, false), Mats.Orange, Vector3.zero);
            }

            // football goals at the long sides
            foreach (int s in new[] { -1, 1 })
            {
                var g = job.Object($"축구 골대 {(s < 0 ? 1 : 2)}", V(0, 0, s < 0 ? -9.5f : 13.5f), s < 0 ? 0 : 180, "rust+mud+leaves");
                var fr = MeshKit.Merge(MeshKit.LineTube(V(-2.5f, 0, 0), V(-2.5f, 2.1f, 0), .06f), MeshKit.LineTube(V(2.5f, 0, 0), V(2.5f, 2.1f, 0), .06f),
                    MeshKit.LineTube(V(-2.5f, 2.1f, 0), V(2.5f, 2.1f, 0), .06f), MeshKit.LineTube(V(-2.5f, 0, -1.4f), V(2.5f, 0, -1.4f), .04f),
                    MeshKit.LineTube(V(-2.5f, 2.1f, 0), V(-2.5f, 0, -1.4f), .04f), MeshKit.LineTube(V(2.5f, 2.1f, 0), V(2.5f, 0, -1.4f), .04f));
                g.Mesh(g.Comp("골대 틀"), fr, Mats.White, Vector3.zero);
            }

            // concrete stands along the right side
            var st = job.Object("스탠드", V(23.2f, 0, 2), -90, "gum+dust+bird");
            var steps = new MeshData(); var seats = new MeshData();
            for (int i = 0; i < 3; i++)
            {
                steps.Add(Box(16, .45f * (i + 1), .9f, .03f).Xf(0, .225f * (i + 1), -i * .9f));
                seats.Add(Box(15.6f, .06f, .4f, .02f).Xf(0, .45f * (i + 1) + .03f, -i * .9f + .15f));
                st.Volume(V(16, .45f * (i + 1), .9f), V(0, .225f * (i + 1), -i * .9f));
            }
            st.Mesh(st.Comp("계단식 좌석"), steps, Mats.Concrete, Vector3.zero, default, OBox);
            st.Mesh(st.Comp("앉는 판", "gum+bird+dust"), seats, Mats.Seat, Vector3.zero);
            job.Paved.Add(Rect.MinMaxRect(21.5f, -7, 26, 11));

            // pull-up bars and a drinking-water trough on the left
            var pb = job.Object("철봉", V(-23, 0, -4), 90, "rust+grime");
            var bars = new MeshData();
            for (int i = 0; i < 4; i++) bars.Add(MeshKit.Cylinder(.05f, .05f, 2.2f, 10).Xf(-1.8f + i * 1.2f, 1.1f, 0));
            for (int i = 0; i < 3; i++) bars.Add(MeshKit.LineTube(V(-1.8f + i * 1.2f, 1.3f + i * .35f, 0), V(-.6f + i * 1.2f, 1.3f + i * .35f, 0), .025f));
            pb.Mesh(pb.Comp("철봉 틀"), bars, Mats.Metal, Vector3.zero);
            var wt = job.Object("수돗가", V(-23, 0, 6), 90, "lime+algae+mud");
            wt.Mesh(wt.Comp("세면대"), Box(3.6f, .85f, .8f, .05f), Mats.StoneSmooth, V(0, .425f, 0), default, OBox);
            wt.Volume(V(3.6f, .85f, .8f), V(0, .425f, 0));
            var taps = new MeshData();
            for (int i = 0; i < 4; i++) taps.Add(MeshKit.Tube(new[] { V(-1.35f + i * .9f, .85f, -.3f), V(-1.35f + i * .9f, 1.05f, -.3f), V(-1.35f + i * .9f, 1.05f, -.15f) }, .02f, 10, 8));
            wt.Mesh(wt.Comp("수도꼭지", "lime+rust"), taps, Mats.Steel, Vector3.zero);

            for (int i = 0; i < 4; i++) Props2.LampPost(job, $"가로등 {i + 1}", i < 2 ? -20 : 20, i % 2 == 0 ? -11 : 15);
            Props.ParkBench(job, "벤치 1", -12, 16.6f, 180, "gum+bird+dust");
            Props.ParkBench(job, "벤치 2", -4, 16.6f, 180, "gum+bird+dust");
            Props.ParkBench(job, "벤치 3", 4, 16.6f, 180, "gum+bird+dust");
            Props.ParkBench(job, "벤치 4", 12, 16.6f, 180, "gum+bird+dust");
            Props2.RoundBin(job, -16, 16.2f); Props2.RoundBin(job, 16, 16.2f);
            var d = job.DecoObj();
            foreach (var t in new[] { P(-24, 17), P(24, 17), P(-24, -12), P(24, -12) }) Props.Tree(d, t.x, t.y, 1.15f, true);
        }
    }
}
