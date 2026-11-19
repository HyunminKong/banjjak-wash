using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Banjjak
{
    /// <summary>CPU-side mesh under construction. Generators fill it; Add() merges with a transform.</summary>
    public class MeshData
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public List<Color> Col;
        public readonly List<int> T = new List<int>();
        public List<int> Sub;   // optional submesh index per triangle (multi-material meshes)

        public int Count => V.Count;

        public int Vert(Vector3 p, Vector3 n, Vector2 uv)
        {
            V.Add(p); N.Add(n); UV.Add(uv);
            if (Col != null) Col.Add(Color.white);
            return V.Count - 1;
        }

        public void Tri(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }
        public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

        public MeshData Add(MeshData o) => Add(o, Matrix4x4.identity);

        public MeshData Add(MeshData o, Matrix4x4 m)
        {
            int baseIdx = V.Count;
            var nm = m.inverse.transpose;
            bool wantCol = Col != null || o.Col != null;
            if (wantCol && Col == null) { Col = new List<Color>(V.Count); for (int i = 0; i < V.Count; i++) Col.Add(Color.white); }
            for (int i = 0; i < o.V.Count; i++)
            {
                V.Add(m.MultiplyPoint3x4(o.V[i]));
                N.Add(nm.MultiplyVector(o.N[i]).normalized);
                UV.Add(o.UV[i]);
                if (wantCol) Col.Add(o.Col != null ? o.Col[i] : Color.white);
            }
            bool flip = m.determinant < 0;
            if (o.Sub != null && Sub == null) { Sub = new List<int>(); for (int i = 0; i < T.Count / 3; i++) Sub.Add(0); }
            for (int i = 0; i < o.T.Count; i += 3)
            {
                if (flip) { T.Add(o.T[i] + baseIdx); T.Add(o.T[i + 2] + baseIdx); T.Add(o.T[i + 1] + baseIdx); }
                else { T.Add(o.T[i] + baseIdx); T.Add(o.T[i + 1] + baseIdx); T.Add(o.T[i + 2] + baseIdx); }
                if (Sub != null) Sub.Add(o.Sub != null ? o.Sub[i / 3] : 0);
            }
            return this;
        }

        /// <summary>Transform in place (position, euler degrees, uniform scale).</summary>
        public MeshData Xf(float x, float y, float z, float rx = 0, float ry = 0, float rz = 0, float s = 1)
        {
            var m = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(rx, ry, rz), Vector3.one * s);
            var nm = m.inverse.transpose;
            for (int i = 0; i < V.Count; i++)
            {
                V[i] = m.MultiplyPoint3x4(V[i]);
                N[i] = nm.MultiplyVector(N[i]).normalized;
            }
            return this;
        }

        public MeshData Scale(Vector3 s)
        {
            var m = Matrix4x4.Scale(s);
            var nm = m.inverse.transpose;
            for (int i = 0; i < V.Count; i++) { V[i] = m.MultiplyPoint3x4(V[i]); N[i] = nm.MultiplyVector(N[i]).normalized; }
            return this;
        }

        public MeshData Tint(Color c)
        {
            if (Col == null) { Col = new List<Color>(V.Count); for (int i = 0; i < V.Count; i++) Col.Add(c); }
            else for (int i = 0; i < Col.Count; i++) Col[i] = Col[i] * c;
            return this;
        }

        /// <summary>Make every triangle front-facing along its vertex normals (Unity: clockwise).</summary>
        public void FixWinding()
        {
            for (int i = 0; i < T.Count; i += 3)
            {
                int a = T[i], b = T[i + 1], c = T[i + 2];
                var g = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                if (g.sqrMagnitude < 1e-14f) continue;
                var an = N[a] + N[b] + N[c];
                if (Vector3.Dot(g, an) < 0) { T[i + 1] = c; T[i + 2] = b; }
            }
        }

        public Mesh ToMesh(string name = "mesh", bool fixWinding = true)
        {
            if (fixWinding) FixWinding();
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetNormals(N);
            m.SetUVs(0, UV);
            if (Col != null) m.SetColors(Col);
            if (Sub == null) m.SetTriangles(T, 0);
            else
            {
                int count = 0; foreach (int s in Sub) count = Mathf.Max(count, s + 1);
                m.subMeshCount = count;
                for (int s = 0; s < count; s++)
                {
                    var list = new List<int>();
                    for (int i = 0; i < Sub.Count; i++) if (Sub[i] == s) { list.Add(T[i * 3]); list.Add(T[i * 3 + 1]); list.Add(T[i * 3 + 2]); }
                    m.SetTriangles(list, s);
                }
            }
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    public static partial class MeshKit
    {
        const float TAU = Mathf.PI * 2f;

        public static MeshData Merge(params MeshData[] parts)
        {
            var d = new MeshData();
            foreach (var p in parts) d.Add(p);
            return d;
        }

        public static MeshData Merge(IEnumerable<MeshData> parts)
        {
            var d = new MeshData();
            foreach (var p in parts) d.Add(p);
            return d;
        }

        static List<float> Samples(float H, float r, int seg)
        {
            var s = new List<float> { -H };
            if (r > 1e-4f) for (int k = 1; k <= seg; k++) s.Add(-H + r * k / seg);
            for (int k = seg; k >= 0; k--)
            {
                float v = H - (r > 1e-4f ? r * k / seg : 0);
                if (v > s[s.Count - 1] + 1e-6f) s.Add(v);
            }
            return s;
        }

        /// <summary>
        /// Box with rounded edges; UVs in metres along each face. With decalFaces, those faces get 0..1 UVs,
        /// and every face becomes its own submesh (order +X, -X, +Y, -Y, +Z, -Z) for a 6-material array.
        /// </summary>
        public static MeshData RoundedBox(float w, float h, float d, float r = .015f, int seg = 2, int[] decalFaces = null, bool perFace = false)
        {
            float hx = w / 2, hy = h / 2, hz = d / 2;
            r = Mathf.Max(0, Mathf.Min(r, Mathf.Min(hx, Mathf.Min(hy, hz)) - 1e-4f));
            var md = new MeshData();
            float ix = hx - r, iy = hy - r, iz = hz - r;
            var faces = new (Vector3 n, Vector3 ua, Vector3 va, float A, float B, float C)[]
            {
                (Vector3.right, Vector3.back, Vector3.up, d, h, hx),
                (Vector3.left, Vector3.forward, Vector3.up, d, h, hx),
                (Vector3.up, Vector3.right, Vector3.back, w, d, hy),
                (Vector3.down, Vector3.right, Vector3.forward, w, d, hy),
                (Vector3.forward, Vector3.right, Vector3.up, w, h, hz),
                (Vector3.back, Vector3.left, Vector3.up, w, h, hz),
            };
            bool multi = perFace || decalFaces != null;
            if (multi) md.Sub = new List<int>();
            for (int fi = 0; fi < 6; fi++)
            {
                var f = faces[fi];
                bool dec = decalFaces != null && System.Array.IndexOf(decalFaces, fi) >= 0;
                var su = Samples(f.A / 2, r, seg);
                var sv = Samples(f.B / 2, r, seg);
                int baseIdx = md.Count;
                foreach (float b in sv)
                    foreach (float a in su)
                    {
                        var P = f.n * f.C + f.ua * a + f.va * b;
                        var Q = new Vector3(Mathf.Clamp(P.x, -ix, ix), Mathf.Clamp(P.y, -iy, iy), Mathf.Clamp(P.z, -iz, iz));
                        var D = P - Q;
                        float L = D.magnitude;
                        Vector3 nrm;
                        if (L > 1e-6f && r > 1e-4f) { D /= L; P = Q + D * r; nrm = D; }
                        else nrm = f.n;
                        md.Vert(P, nrm, dec ? new Vector2(.5f - a / f.A, b / f.B + .5f) : new Vector2(a + f.A / 2, b + f.B / 2));
                    }
                int cu = su.Count;
                for (int j = 0; j < sv.Count - 1; j++)
                    for (int i = 0; i < cu - 1; i++)
                    {
                        int a0 = baseIdx + j * cu + i, a1 = a0 + 1, b0 = a0 + cu, b1 = b0 + 1;
                        md.Tri(a0, a1, b1); md.Tri(a0, b1, b0);
                        if (multi) { md.Sub.Add(fi); md.Sub.Add(fi); }
                    }
            }
            return md;
        }

        public static MeshData Box(float w, float h, float d, float r = .015f) => RoundedBox(w, h, d, r, 2);

        /// <summary>Upward-facing plane centred on origin, UV in metres.</summary>
        public static MeshData Plane(float W, float D, int sx = 1, int sz = 1)
        {
            var md = new MeshData();
            for (int j = 0; j <= sz; j++)
                for (int i = 0; i <= sx; i++)
                {
                    float x = -W / 2 + W * i / sx, z = -D / 2 + D * j / sz;
                    md.Vert(new Vector3(x, 0, z), Vector3.up, new Vector2(x + W / 2, z + D / 2));
                }
            for (int j = 0; j < sz; j++)
                for (int i = 0; i < sx; i++)
                {
                    int a = j * (sx + 1) + i, b = a + 1, c = a + sx + 1, e = c + 1;
                    md.Tri(a, c, e); md.Tri(a, e, b);
                }
            return md;
        }

        /// <summary>Vertical quad facing +Z, centred.</summary>
        public static MeshData Quad(float W, float H)
        {
            var md = new MeshData();
            int a = md.Vert(new Vector3(-W / 2, -H / 2, 0), Vector3.forward, new Vector2(0, 0));
            int b = md.Vert(new Vector3(W / 2, -H / 2, 0), Vector3.forward, new Vector2(W, 0));
            int c = md.Vert(new Vector3(W / 2, H / 2, 0), Vector3.forward, new Vector2(W, H));
            int d = md.Vert(new Vector3(-W / 2, H / 2, 0), Vector3.forward, new Vector2(0, H));
            md.Quad(a, b, c, d);
            return md;
        }

        public static MeshData Disc(float r, int seg = 40)
        {
            var md = new MeshData();
            int c = md.Vert(Vector3.zero, Vector3.up, new Vector2(r, r));
            for (int i = 0; i <= seg; i++)
            {
                float a = TAU * i / seg;
                var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                md.Vert(p, Vector3.up, new Vector2(p.x + r, p.z + r));
                if (i > 0) md.Tri(c, c + i, c + i + 1);
            }
            return md;
        }

        public static MeshData Cylinder(float rt, float rb, float h, int seg = 24, bool open = false)
        {
            var md = new MeshData();
            float slope = (rb - rt) / h, C = Mathf.PI * (rt + rb);
            for (int j = 0; j <= 1; j++)
            {
                float y = -h / 2 + h * j, r = j == 0 ? rb : rt;
                for (int i = 0; i <= seg; i++)
                {
                    float a = TAU * i / seg, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    md.Vert(new Vector3(ca * r, y, sa * r), new Vector3(ca, slope, sa).normalized, new Vector2((float)i / seg * C, y + h / 2));
                }
            }
            for (int i = 0; i < seg; i++) { int a = i, b = i + 1, c = seg + 1 + i, d = c + 1; md.Tri(a, c, d); md.Tri(a, d, b); }
            if (!open)
            {
                for (int s = 0; s <= 1; s++)
                {
                    float y = s == 0 ? -h / 2 : h / 2, r = s == 0 ? rb : rt;
                    if (r < 1e-4f) continue;
                    var n = s == 0 ? Vector3.down : Vector3.up;
                    int c0 = md.Vert(new Vector3(0, y, 0), n, new Vector2(r, r));
                    for (int i = 0; i <= seg; i++)
                    {
                        float a = TAU * i / seg;
                        var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                        md.Vert(p, n, new Vector2(p.x + r, p.z + r));
                        if (i > 0) md.Tri(c0, c0 + i, c0 + i + 1);
                    }
                }
            }
            return md;
        }

        /// <summary>Surface of revolution around Y. pts = (radius, height) profile.</summary>
        public static MeshData Lathe(Vector2[] pts, int seg = 32)
        {
            var md = new MeshData();
            int np = pts.Length;
            float maxR = 0; foreach (var p in pts) maxR = Mathf.Max(maxR, p.x);
            float L = 0;
            var lens = new float[np];
            for (int i = 1; i < np; i++) { L += Vector2.Distance(pts[i], pts[i - 1]); lens[i] = L; }
            var pn = new Vector2[np];
            for (int i = 0; i < np; i++)
            {
                var t = pts[Mathf.Min(np - 1, i + 1)] - pts[Mathf.Max(0, i - 1)];
                pn[i] = new Vector2(t.y, -t.x).normalized;
            }
            for (int i = 0; i < np; i++)
                for (int j = 0; j <= seg; j++)
                {
                    float a = TAU * j / seg, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    md.Vert(new Vector3(pts[i].x * ca, pts[i].y, pts[i].x * sa),
                        new Vector3(pn[i].x * ca, pn[i].y, pn[i].x * sa).normalized,
                        new Vector2((float)j / seg * TAU * maxR, lens[i]));
                }
            for (int i = 0; i < np - 1; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j, b = a + 1, c = a + seg + 1, d = c + 1;
                    md.Tri(a, c, d); md.Tri(a, d, b);
                }
            return md;
        }

        public static MeshData Lathe(float[,] pts, int seg = 32, float s = 1)
        {
            var arr = new Vector2[pts.GetLength(0)];
            for (int i = 0; i < arr.Length; i++) arr[i] = new Vector2(pts[i, 0] * s, pts[i, 1] * s);
            return Lathe(arr, seg);
        }

        /// <summary>Sphere (or cap when thetaLength &lt; PI, measured from the top).</summary>
        public static MeshData Sphere(float r, int ws = 24, int hs = 16, float thetaLength = Mathf.PI)
        {
            var md = new MeshData();
            for (int j = 0; j <= hs; j++)
            {
                float th = thetaLength * j / hs;
                for (int i = 0; i <= ws; i++)
                {
                    float ph = TAU * i / ws;
                    var n = new Vector3(-Mathf.Cos(ph) * Mathf.Sin(th), Mathf.Cos(th), Mathf.Sin(ph) * Mathf.Sin(th));
                    md.Vert(n * r, n, new Vector2(ph * r, th * r));
                }
            }
            for (int j = 0; j < hs; j++)
                for (int i = 0; i < ws; i++)
                {
                    int a = j * (ws + 1) + i, b = a + 1, c = a + ws + 1, d = c + 1;
                    md.Tri(a, c, d); md.Tri(a, d, b);
                }
            return md;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
        }

        public static Vector3 CurvePoint(IList<Vector3> pts, float u)
        {
            int n = pts.Count - 1;
            float f = Mathf.Clamp01(u) * n;
            int i = Mathf.Min(n - 1, Mathf.FloorToInt(f));
            float t = f - i;
            var p0 = pts[Mathf.Max(0, i - 1)];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = pts[Mathf.Min(n, i + 2)];
            if (i == 0) p0 = p1 + (p1 - p2);
            if (i + 2 > n) p3 = p2 + (p2 - p1);
            return CatmullRom(p0, p1, p2, p3, t);
        }

        /// <summary>Tube along a smooth curve through pts, with rounded caps.</summary>
        public static MeshData Tube(IList<Vector3> pts, float r, int seg = 24, int rad = 10, bool caps = true)
        {
            var md = new MeshData();
            var P = new Vector3[seg + 1];
            for (int i = 0; i <= seg; i++) P[i] = pts.Count == 2 ? Vector3.Lerp(pts[0], pts[1], (float)i / seg) : CurvePoint(pts, (float)i / seg);
            var Tn = new Vector3[seg + 1];
            for (int i = 0; i <= seg; i++) Tn[i] = (P[Mathf.Min(seg, i + 1)] - P[Mathf.Max(0, i - 1)]).normalized;
            var up = Mathf.Abs(Tn[0].y) < .9f ? Vector3.up : Vector3.right;
            var Nn = Vector3.Cross(Tn[0], up).normalized;
            float len = 0;
            for (int i = 0; i <= seg; i++)
            {
                if (i > 0)
                {
                    len += Vector3.Distance(P[i], P[i - 1]);
                    var b = Vector3.Cross(Tn[i - 1], Tn[i]);
                    if (b.sqrMagnitude > 1e-10f)
                    {
                        float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(Tn[i - 1], Tn[i]), -1, 1)) * Mathf.Rad2Deg;
                        Nn = Quaternion.AngleAxis(ang, b.normalized) * Nn;
                    }
                }
                var B = Vector3.Cross(Tn[i], Nn).normalized;
                for (int k = 0; k <= rad; k++)
                {
                    float a = TAU * k / rad;
                    var n = (Nn * Mathf.Cos(a) + B * Mathf.Sin(a)).normalized;
                    md.Vert(P[i] + n * r, n, new Vector2(len, a * r));
                }
            }
            for (int i = 0; i < seg; i++)
                for (int k = 0; k < rad; k++)
                {
                    int a = i * (rad + 1) + k, b2 = a + 1, c = a + rad + 1, d = c + 1;
                    md.Tri(a, c, d); md.Tri(a, d, b2);
                }
            if (caps)
            {
                md.Add(Sphere(r, 10, 6).Xf(P[0].x, P[0].y, P[0].z));
                md.Add(Sphere(r, 10, 6).Xf(P[seg].x, P[seg].y, P[seg].z));
            }
            return md;
        }

        public static MeshData Tube(float r, int seg, params Vector3[] pts) => Tube(pts, r, seg);

        public static MeshData LineTube(Vector3 a, Vector3 b, float r, int rad = 10) => Tube(new[] { a, b }, r, 4, rad);

        /// <summary>Cluster of alpha leaf cards inside an ellipsoid. Normals point outward for soft shading.</summary>
        public static MeshData Foliage(float rx, float ry, float rz, int count, float card, Rng rng, float shadeMin = .62f)
        {
            var md = new MeshData { Col = new List<Color>() };
            for (int i = 0; i < count; i++)
            {
                Vector3 p;
                do { p = new Vector3(rng.Range(-1, 1), rng.Range(-1, 1), rng.Range(-1, 1)); } while (p.sqrMagnitude > 1);
                p = Vector3.Scale(p, new Vector3(rx, ry, rz));
                float s = card * rng.Range(.75f, 1.25f);
                var rot = Quaternion.Euler(rng.Range(0, 360), rng.Range(0, 360), rng.Range(0, 360));
                var outward = new Vector3(p.x / rx, (p.y + ry * .35f) / ry, p.z / rz).normalized;
                float shade = Mathf.Lerp(shadeMin, 1f, Mathf.Clamp01(.5f + .5f * outward.y + .3f * rng.Next()));
                var tint = new Color(shade * rng.Range(.92f, 1.05f), shade, shade * rng.Range(.85f, 1f), 0);
                int b = md.Count;
                var corners = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
                var uvs = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                for (int k = 0; k < 4; k++)
                {
                    md.V.Add(p + rot * (corners[k] * s));
                    md.N.Add(outward);
                    md.UV.Add(uvs[k]);
                    md.Col.Add(tint);
                }
                md.Tri(b, b + 1, b + 2); md.Tri(b, b + 2, b + 3);
            }
            return md;
        }

        /// <summary>Single grass blade (height 1, uv.y = height for the wind shader).</summary>
        public static void AddBlade(MeshData md, Vector3 basePos, float yaw, float h, float w, float lean, Color tint)
        {
            float[,] rows = { { 0, .5f }, { .35f, .42f }, { .7f, .26f }, { 1, 0 } };
            var rot = Quaternion.Euler(0, yaw, 0);
            int b = md.Count;
            for (int i = 0; i < 4; i++)
            {
                float y = rows[i, 0], ww = rows[i, 1] * w;
                float z = lean * y * y;
                var c = new Color(.2f + .3f * y, .36f + .32f * y, .12f + .14f * y, 0) * tint;
                c.a = 0;
                var n = rot * new Vector3(0, .6f, 1).normalized;
                md.V.Add(basePos + rot * new Vector3(-ww, y * h, z * h)); md.N.Add(n); md.UV.Add(new Vector2(0, y)); md.Col.Add(c);
                md.V.Add(basePos + rot * new Vector3(ww, y * h, z * h)); md.N.Add(n); md.UV.Add(new Vector2(1, y)); md.Col.Add(c);
                if (i > 0)
                {
                    int a = b + (i - 1) * 2;
                    md.Tri(a, a + 1, a + 3); md.Tri(a, a + 3, a + 2);
                }
            }
        }
    }
}
