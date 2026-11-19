using System.Collections.Generic;
using UnityEngine;

namespace Banjjak
{
    public static partial class MeshKit
    {
        // ------------------------------------------------------------------ 2D outlines
        public static List<Vector2> Arc(float cx, float cy, float r, float a0, float a1, int segs)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= segs; i++)
            {
                float a = Mathf.Lerp(a0, a1, (float)i / segs);
                pts.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            return pts;
        }

        static List<Vector2> Clean(IList<Vector2> pts)
        {
            var o = new List<Vector2>();
            foreach (var p in pts) if (o.Count == 0 || (p - o[o.Count - 1]).sqrMagnitude > 1e-10f) o.Add(p);
            if (o.Count > 2 && (o[0] - o[o.Count - 1]).sqrMagnitude < 1e-10f) o.RemoveAt(o.Count - 1);
            return o;
        }

        static float Area(List<Vector2> p)
        {
            float a = 0;
            for (int i = 0; i < p.Count; i++) { var q = p[i]; var r = p[(i + 1) % p.Count]; a += q.x * r.y - r.x * q.y; }
            return a / 2;
        }

        /// <summary>Ear-clipping triangulation of a simple polygon (returns index triples, CCW output).</summary>
        public static List<int> Triangulate(List<Vector2> p)
        {
            var res = new List<int>();
            var idx = new List<int>();
            bool ccw = Area(p) > 0;
            for (int i = 0; i < p.Count; i++) idx.Add(ccw ? i : p.Count - 1 - i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (cross <= 1e-9f) continue;
                    bool inside = false;
                    foreach (int k in idx)
                    {
                        if (k == ia || k == ib || k == ic) continue;
                        if (InTri(p[k], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    res.Add(ia); res.Add(ib); res.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { res.Add(idx[0]); res.Add(idx[1]); res.Add(idx[2]); }
            return res;
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        public static List<Vector2> QuadBezier(Vector2 p0, Vector2 c, Vector2 p1, int n = 8)
        {
            var pts = new List<Vector2>();
            for (int i = 1; i <= n; i++)
            {
                float t = (float)i / n, u = 1 - t;
                pts.Add(u * u * p0 + 2 * u * t * c + t * t * p1);
            }
            return pts;
        }

        /// <summary>Disc facing +Z with 0..1 decal UVs.</summary>
        public static MeshData DiscZ(float r, int seg = 32)
        {
            var md = new MeshData();
            int c = md.Vert(Vector3.zero, Vector3.forward, new Vector2(.5f, .5f));
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2 * i / seg;
                var p = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
                md.Vert(p, Vector3.forward, new Vector2(.5f - p.x / (2 * r), .5f + p.y / (2 * r)));
                if (i > 0) md.Tri(c, c + i, c + i + 1);
            }
            return md;
        }

        /// <summary>Extrude an XY outline from z = 0 to z = depth (flat-shaded sides, UV in metres).
        /// With twoMaterials, caps are submesh 0 and sides submesh 1.</summary>
        public static MeshData Extrude(IList<Vector2> outline, float depth, bool twoMaterials)
        {
            var md = Extrude(outline, depth);
            if (!twoMaterials) return md;
            var p = Clean(outline);
            int capTris = (Triangulate(p).Count / 3) * 2;
            md.Sub = new List<int>();
            for (int i = 0; i < md.T.Count / 3; i++) md.Sub.Add(i < capTris ? 0 : 1);
            return md;
        }

        public static MeshData Extrude(IList<Vector2> outline, float depth)
        {
            var p = Clean(outline);
            var md = new MeshData();
            var tris = Triangulate(p);
            bool ccw = Area(p) > 0;
            for (int cap = 0; cap < 2; cap++)
            {
                float z = cap == 0 ? 0 : depth;
                var n = cap == 0 ? Vector3.back : Vector3.forward;
                int b = md.Count;
                foreach (var q in p) md.Vert(new Vector3(q.x, q.y, z), n, q);
                for (int i = 0; i < tris.Count; i += 3) md.Tri(b + tris[i], b + tris[i + 1], b + tris[i + 2]);
            }
            float u = 0;
            for (int i = 0; i < p.Count; i++)
            {
                var a = p[i]; var c = p[(i + 1) % p.Count];
                var e = c - a; float len = e.magnitude;
                var n2 = ccw ? new Vector2(e.y, -e.x).normalized : new Vector2(-e.y, e.x).normalized;
                var n = new Vector3(n2.x, n2.y, 0);
                int b = md.Count;
                md.Vert(new Vector3(a.x, a.y, 0), n, new Vector2(u, 0));
                md.Vert(new Vector3(c.x, c.y, 0), n, new Vector2(u + len, 0));
                md.Vert(new Vector3(c.x, c.y, depth), n, new Vector2(u + len, depth));
                md.Vert(new Vector3(a.x, a.y, depth), n, new Vector2(u, depth));
                md.Quad(b, b + 1, b + 2, b + 3);
                u += len;
            }
            return md;
        }

        /// <summary>Flat two-sided polygon in the XY plane.</summary>
        public static MeshData Polygon(IList<Vector2> outline) => Extrude(outline, .004f);

        /// <summary>Plane W x H in the XY plane (normal +Z) with rectangular holes [x0,y0,x1,y1] measured from the bottom-left.</summary>
        public static MeshData Panel(float W, float H, IList<float[]> holes)
        {
            var md = new MeshData();
            foreach (var r in RectsMinus(W, H, holes))
            {
                float x0 = r[0] - W / 2, x1 = r[2] - W / 2, y0 = r[1] - H / 2, y1 = r[3] - H / 2;
                int a = md.Vert(new Vector3(x0, y0, 0), Vector3.forward, new Vector2(r[0], r[1]));
                int b = md.Vert(new Vector3(x1, y0, 0), Vector3.forward, new Vector2(r[2], r[1]));
                int c = md.Vert(new Vector3(x1, y1, 0), Vector3.forward, new Vector2(r[2], r[3]));
                int d = md.Vert(new Vector3(x0, y1, 0), Vector3.forward, new Vector2(r[0], r[3]));
                md.Quad(a, b, c, d);
            }
            return md;
        }

        public static List<float[]> RectsMinus(float W, float H, IList<float[]> holes)
        {
            var xs = new SortedSet<float> { 0, W };
            foreach (var h in holes) { xs.Add(Mathf.Clamp(h[0], 0, W)); xs.Add(Mathf.Clamp(h[2], 0, W)); }
            var xl = new List<float>(xs);
            var outR = new List<float[]>();
            for (int i = 0; i < xl.Count - 1; i++)
            {
                float xa = xl[i], xb = xl[i + 1];
                if (xb - xa < 1e-4f) continue;
                float xm = (xa + xb) / 2;
                var cov = new List<float[]>();
                foreach (var h in holes) if (h[0] <= xm && h[2] >= xm) cov.Add(h);
                cov.Sort((p, q) => p[1].CompareTo(q[1]));
                float cur = 0;
                foreach (var h in cov) { if (h[1] > cur + 1e-4f) outR.Add(new[] { xa, cur, xb, h[1] }); cur = Mathf.Max(cur, h[3]); }
                if (cur < H - 1e-4f) outR.Add(new[] { xa, cur, xb, H });
            }
            return outR;
        }

        /// <summary>Recompute smooth normals from triangle winding.</summary>
        public static MeshData RecalcNormals(MeshData md)
        {
            var acc = new Vector3[md.Count];
            for (int i = 0; i < md.T.Count; i += 3)
            {
                int a = md.T[i], b = md.T[i + 1], c = md.T[i + 2];
                var n = Vector3.Cross(md.V[b] - md.V[a], md.V[c] - md.V[a]);
                acc[a] += n; acc[b] += n; acc[c] += n;
            }
            for (int i = 0; i < md.Count; i++) md.N[i] = acc[i].sqrMagnitude > 1e-12f ? acc[i].normalized : Vector3.up;
            return md;
        }

        /// <summary>Sweep a closed 2D profile (x = side, y = up) along a smooth path.</summary>
        public static MeshData Sweep(Vector2[] profile, Vector3[] path, int segs, Vector3 side)
        {
            var md = new MeshData();
            int np = profile.Length + 1;
            float L = 0;
            var prev = CurvePoint(path, 0);
            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                var p = CurvePoint(path, t);
                L += Vector3.Distance(p, prev); prev = p;
                var tan = (CurvePoint(path, Mathf.Min(1, t + .01f)) - CurvePoint(path, Mathf.Max(0, t - .01f))).normalized;
                var up = Vector3.Cross(side, tan).normalized;
                var sd = Vector3.Cross(tan, up).normalized;
                float pl = 0;
                for (int k = 0; k < np; k++)
                {
                    var q = profile[k % profile.Length];
                    if (k > 0) pl += Vector2.Distance(q, profile[k - 1]);
                    md.Vert(p + sd * q.x + up * q.y, up, new Vector2(pl, L));
                }
            }
            for (int i = 0; i < segs; i++)
                for (int k = 0; k < np - 1; k++)
                {
                    int a = i * np + k, b = a + 1, c = a + np, d = c + 1;
                    md.Tri(a, b, d); md.Tri(a, d, c);
                }
            return RecalcNormals(md);
        }

        /// <summary>Quarter-pipe transition surface: width w, radius r, rising towards -Z.</summary>
        public static MeshData Ramp(float w, float r, int su = 8, int sv = 20)
        {
            var md = new MeshData();
            for (int j = 0; j <= sv; j++)
            {
                float v = (float)j / sv, a = v * Mathf.PI / 2;
                for (int i = 0; i <= su; i++)
                {
                    float u = (float)i / su;
                    var n = new Vector3(0, Mathf.Cos(a), Mathf.Sin(a));
                    md.Vert(new Vector3((u - .5f) * w, r * (1 - Mathf.Cos(a)), -r * Mathf.Sin(a)), n, new Vector2(u * w, v * r * Mathf.PI / 2));
                }
            }
            for (int j = 0; j < sv; j++)
                for (int i = 0; i < su; i++)
                {
                    int a = j * (su + 1) + i, b = a + 1, c = a + su + 1, d = c + 1;
                    md.Tri(a, c, b); md.Tri(b, c, d);
                }
            return md;
        }

        public static MeshData Cone(float r, float h, int seg = 32) => Cylinder(.001f, r, h, seg);
    }
}
