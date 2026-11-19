using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    public static class Jobs
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>Paved ground split into cleanable zones.</summary>
        public static JobObject GroundGrid(CleanJob job, string name, float cx, float cz, float W, float D, int cols, int rows, Material mat, string spec)
            => GroundGrid(job, name, cx, cz, W, D, cols, rows, (r, c) => mat, spec);

        public static JobObject GroundGrid(CleanJob job, string name, float cx, float cz, float W, float D, int cols, int rows, System.Func<int, int, Material> matFor, string spec)
        {
            var o = job.Object(name, Vector3.zero, 0, spec);
            job.Paved.Add(Rect.MinMaxRect(cx - W / 2 - .15f, cz - D / 2 - .15f, cx + W / 2 + .15f, cz + D / 2 + .15f));
            var mat = (Material)null;
            float zw = W / cols, zd = D / rows;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    string label = cols == 1 && rows == 1 ? "바닥" : cols == 1 ? $"구역 {(char)('A' + r)}" : rows == 1 ? $"구역 {c + 1}" : $"구역 {(char)('A' + r)}{c + 1}";
                    var plane = MeshKit.Plane(zw, zd, 4, 4);
                    // world-anchored UVs so the slab pattern runs continuously across zones
                    float px = cx - W / 2 + zw * (c + .5f), pz = cz - D / 2 + zd * (r + .5f);
                    mat = matFor(r, c);
                    for (int i = 0; i < plane.UV.Count; i++) plane.UV[i] = new Vector2(plane.V[i].x + px, plane.V[i].z + pz);
                    o.Mesh(o.Comp(label), plane, mat, V(px, .015f, pz), default, new MeshOpts { Ground = true, NoShadow = true });
                }
            return o;
        }

        public static void BuildPatio(CleanJob job)
        {
            GroundGrid(job, "테라스 바닥", 0, 0, 8, 8, 2, 2, Mats.Stone, "mud+leaves+moss");

            var w = job.Object("벽돌 담장", V(0, 0, -4.75f), 0, "moss+mildew");
            var segs = new[] { w.Comp("왼쪽 벽"), w.Comp("가운데 벽"), w.Comp("오른쪽 벽") };
            var pil = w.Comp("기둥", "moss+bird");
            var cap = w.Comp("갓돌", "dust+bird+moss");
            float[] sx = { -2.8f, 0, 2.8f };
            for (int i = 0; i < 3; i++) w.Mesh(segs[i], MeshKit.RoundedBox(2.42f, 1.35f, .26f, .015f), Mats.Brick, V(sx[i], .675f, 0), default, new MeshOpts { Occ = Occ.Box });
            foreach (float x in new[] { -4.2f, -1.4f, 1.4f, 4.2f }) w.Mesh(pil, MeshKit.RoundedBox(.44f, 1.6f, .44f, .03f), Mats.Brick, V(x, .8f, 0), default, new MeshOpts { Occ = Occ.Box });
            var capMd = new MeshData();
            foreach (float x in new[] { -4.2f, -1.4f, 1.4f, 4.2f }) capMd.Add(MeshKit.RoundedBox(.54f, .08f, .54f, .025f).Xf(x, 1.64f, 0));
            foreach (float x in sx) capMd.Add(MeshKit.RoundedBox(2.4f, .06f, .34f, .02f).Xf(x, 1.38f, 0));
            w.Mesh(cap, capMd, Mats.StoneSmooth, Vector3.zero, default, new MeshOpts { Occ = Occ.Box });

            Props.ParkBench(job, "나무 벤치", -2.3f, -3.4f, 0, "dust+pollen+bird");
            Props.PicnicTable(job, "피크닉 테이블", 2.4f, -.6f, -.35f * Mathf.Rad2Deg, "dust+pollen+leaves");
            var pl = job.Object("화분", Vector3.zero, 0, "mud+lime");
            Props.Pot(pl, pl.Comp("화분 1"), -5.4f, 2.6f);
            Props.Pot(pl, pl.Comp("화분 2"), 5.4f, 2.6f);
            Props.GardenLamp(job, "정원 등", -5.4f, -2.2f);
            Props.KettleGrill(job, 5.3f, -2.6f);
            Props.BirdBath(job, -5.8f, 6.2f);
            Props.WheelieBin(job, 7.6f, 4.2f, -.4f * Mathf.Rad2Deg);
            var d = job.DecoObj();
            foreach (var p in new[] { V(-6.4f, 0, -3.6f), V(6.4f, 0, -4f), V(-7f, 0, .5f), V(7f, 0, 5.8f) }) Props.Bush(d, p.x, p.z, 1.1f);
            Props.Tree(d, 7.2f, 7.4f, 1, true, 1);

            job.Paved.Add(Rect.MinMaxRect(-4.6f, -5.2f, 4.6f, -4.3f));
        }

        /// <summary>Lawn, grass blades, hedges, trees and neighbouring houses around a job.</summary>
        public static void Scenery(CleanJob job)
        {
            var paved = job.Paved;
            var b = job.Bounds;
            var d = job.DecoObj("scenery");
            var ground = d.Deco(MeshKit.Plane(260, 260, 8, 8), Mats.Grass, V(0, 0, 0), default, new MeshOpts { NoShadow = true });
            ground.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float x0 = b.xMin, x1 = b.xMax, z0 = b.yMin, z1 = b.yMax;
            Props.Hedge(d, x1 - x0 + 3.4f, 1.2f, .9f, (x0 + x1) / 2, z0 - 1.2f);
            Props.Hedge(d, x1 - x0 + 3.4f, 1.2f, .9f, (x0 + x1) / 2, z1 + 1.2f);
            Props.Hedge(d, .9f, 1.2f, z1 - z0 + 1.5f, x0 - 1.2f, (z0 + z1) / 2);
            Props.Hedge(d, .9f, 1.2f, z1 - z0 + 1.5f, x1 + 1.2f, (z0 + z1) / 2);
            var rng = new Rng(job.Id + "t");
            for (int i = 0; i < 26; i++)
            {
                float a = rng.Next() * Mathf.PI * 2, r = 15 + rng.Next() * 26;
                float x = Mathf.Cos(a) * r * (1 + (x1 - x0) / 60), z = Mathf.Sin(a) * r * (1 + (z1 - z0) / 60);
                if (x > x0 - 4 && x < x1 + 4 && z > z0 - 4 && z < z1 + 4) continue;
                Props.Tree(d, x, z, .9f + rng.Next() * .5f);
            }
            var cols = new[] { (G.Hex(0xe8dfcf), G.Hex(0x7a3b2e)), (G.Hex(0xd6e2e6), G.Hex(0x3e4a57)), (G.Hex(0xf1e6c8), G.Hex(0x5a4a3e)), (G.Hex(0xe4d4cc), G.Hex(0x47536a)) };
            var spots = new[] { (x0 - 11, (z0 + z1) / 2, 90f), (x1 + 11, (z0 + z1) / 2 - 4, -90f), ((x0 + x1) / 2 - 8, z0 - 12, 0f), ((x0 + x1) / 2 + 9, z0 - 13, 0f) };
            for (int i = 0; i < spots.Length; i++)
                Props.House(d, spots[i].Item1, spots[i].Item2, spots[i].Item3, 9, 7, 5.5f, cols[i].Item1, cols[i].Item2);
            var lawn = Rect.MinMaxRect(x0 - .5f, z0 - .5f, x1 + .5f, z1 + .5f);
            job.Grass = GrassField.Build(job, d.T, lawn, 46, 7);
        }
    }
}
