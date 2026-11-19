using UnityEngine;

namespace Banjjak
{
    /// <summary>Procedural, tileable albedo + normal textures generated at startup.</summary>
    public static partial class TexGen
    {
        public struct Pair { public Texture2D Albedo, Normal; }

        static float Fbm(float x, float y, int s, int period, int oct = 4) => Noise.FbmT(x, y, s, period, oct);
        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        static Texture2D NewTex(int n, bool linear, string name)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            return t;
        }

        public static Texture2D Finish(Texture2D t, Color32[] px)
        {
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        /// <summary>Normal map (RG = tangent XY) from a height field, wrap-around.</summary>
        public static Texture2D NormalFromHeight(float[] h, int n, float strength, string name)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int xl = (x - 1 + n) % n, xr = (x + 1) % n, yd = (y - 1 + n) % n, yu = (y + 1) % n;
                    float dx = (h[y * n + xr] - h[y * n + xl]) * strength;
                    float dy = (h[yu * n + x] - h[yd * n + x]) * strength;
                    var v = new Vector3(-dx, -dy, 1).normalized;
                    px[y * n + x] = new Color32((byte)((v.x * .5f + .5f) * 255), (byte)((v.y * .5f + .5f) * 255), (byte)((v.z * .5f + .5f) * 255), 255);
                }
            return Finish(NewTex(n, true, name + "_n"), px);
        }

        static Color32 C(Color c) => (Color32)new Color(Clamp01(c.r), Clamp01(c.g), Clamp01(c.b), 1);

        // ---------------------------------------------------------------- dirt noise
        public static Texture2D DirtNoise(int n = 256)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float r = Fbm(u * 8, v * 8, 11, 8, 4);
                    float g = Fbm(u * 16, v * 16, 23, 16, 3);
                    float b = Fbm(u * 4, v * 4, 37, 4, 4);
                    float a = Fbm(u * 32, v * 32, 51, 32, 2);
                    px[y * n + x] = new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), (byte)(a * 255));
                }
            return Finish(NewTex(n, true, "dirtNoise"), px);
        }

        // ---------------------------------------------------------------- paving slabs (2 m tile)
        public static Pair Pavers(int n = 1024)
        {
            const float tile = 2f;
            var px = new Color32[n * n];
            var h = new float[n * n];
            // rows of slabs with random widths summing to the tile width
            int rows = 4;
            var rowEdges = new float[rows][];
            var rng = new Rng(77u);
            for (int r = 0; r < rows; r++)
            {
                var edges = new System.Collections.Generic.List<float> { 0 };
                float x = 0;
                while (x < tile - .01f)
                {
                    float w = new[] { .4f, .5f, .6f, .7f }[rng.Int(4)];
                    if (tile - (x + w) < .3f) w = tile - x;
                    x += w; edges.Add(x);
                }
                float shift = rng.Range(0, tile);
                for (int i = 0; i < edges.Count; i++) edges[i] = edges[i] + shift;
                rowEdges[r] = edges.ToArray();
            }
            float grout = .007f;
            for (int py = 0; py < n; py++)
            {
                float v = (py + .5f) / n * tile;
                int row = Mathf.Min(rows - 1, (int)(v / (tile / rows)));
                float rv = v - row * tile / rows;
                float dyEdge = Mathf.Min(rv, tile / rows - rv);
                var edges = rowEdges[row];
                for (int pxi = 0; pxi < n; pxi++)
                {
                    float u = (pxi + .5f) / n * tile;
                    // find slab
                    float uu = u;
                    while (uu < edges[0]) uu += tile;
                    while (uu >= edges[edges.Length - 1]) uu -= tile;
                    int si = 0;
                    for (int i = 0; i < edges.Length - 1; i++) if (uu >= edges[i] && uu < edges[i + 1]) { si = i; break; }
                    float dxEdge = Mathf.Min(uu - edges[si], edges[si + 1] - uu);
                    float d = Mathf.Min(dxEdge, dyEdge);
                    float id = Noise.Hash2(row, si, 5);
                    float fu = (float)pxi / n, fv = (float)py / n;
                    float big = Fbm(fu * 6, fv * 6, 3, 6, 4), fine = Fbm(fu * 64, fv * 64, 9, 64, 2);
                    float speck = Noise.Hash2(pxi, py, 17);
                    Color baseC = Color.Lerp(new Color(.74f, .69f, .6f), new Color(.62f, .6f, .56f), id);
                    baseC = Color.Lerp(baseC, new Color(.8f, .74f, .62f), Noise.Hash2(row, si, 9) * .4f);
                    var col = baseC * (.86f + .22f * big + .1f * (fine - .5f));
                    if (speck > .985f) col *= .82f;
                    float bevel = Mathf.SmoothStep(0, 1, Clamp01((d - grout) / .012f));
                    float height = bevel * (.85f + .15f * big) + .04f * fine;
                    if (d < grout)
                    {
                        col = new Color(.42f, .4f, .37f) * (.85f + .3f * fine);
                        height = .05f * fine;
                    }
                    else col *= .82f + .18f * bevel;
                    px[py * n + pxi] = C(col);
                    h[py * n + pxi] = height;
                }
            }
            var a = Finish(NewTex(n, false, "pavers"), px);
            return new Pair { Albedo = a, Normal = NormalFromHeight(h, n, 6f, "pavers") };
        }

        // ---------------------------------------------------------------- bricks (1 m tile)
        public static Pair Brick(int n = 1024)
        {
            var px = new Color32[n * n];
            var h = new float[n * n];
            int rows = 14, cols = 4;
            float mortar = .009f, rowH = 1f / rows, colW = 1f / cols;
            for (int py = 0; py < n; py++)
            {
                float v = (py + .5f) / n;
                int row = Mathf.Min(rows - 1, (int)(v / rowH));
                float rv = v - row * rowH;
                for (int pxi = 0; pxi < n; pxi++)
                {
                    float u = (pxi + .5f) / n + (row % 2 == 1 ? colW / 2 : 0);
                    u -= Mathf.Floor(u);
                    int col = Mathf.Min(cols - 1, (int)(u / colW));
                    float ru = u - col * colW;
                    float d = Mathf.Min(Mathf.Min(rv, rowH - rv), Mathf.Min(ru, colW - ru));
                    float fu = (float)pxi / n, fv = (float)py / n;
                    float id = Noise.Hash2(row, col, 3), id2 = Noise.Hash2(row, col, 8);
                    float fine = Fbm(fu * 96, fv * 96, 5, 96, 2), mid = Fbm(fu * 12, fv * 12, 7, 12, 3);
                    Color c = Color.Lerp(new Color(.55f, .25f, .18f), new Color(.66f, .36f, .25f), id);
                    c = Color.Lerp(c, new Color(.42f, .22f, .17f), id2 * .5f);
                    float edge = Clamp01((d - mortar) / .01f);
                    c *= .78f + .25f * mid + .12f * (fine - .5f);
                    c *= .85f + .15f * edge;
                    float height = Mathf.SmoothStep(0, 1, edge) * (.9f + .1f * mid) + .06f * fine;
                    if (d < mortar)
                    {
                        c = new Color(.66f, .63f, .58f) * (.85f + .25f * fine);
                        height = .15f + .05f * fine;
                    }
                    px[py * n + pxi] = C(c);
                    h[py * n + pxi] = height;
                }
            }
            return new Pair { Albedo = Finish(NewTex(n, false, "brick"), px), Normal = NormalFromHeight(h, n, 7f, "brick") };
        }

        // ---------------------------------------------------------------- wood grain (1 m tile, grain along U)
        public static Pair Wood(int n = 512, Color light = default, Color dark = default, int seed = 1)
        {
            if (light == default) light = new Color(.72f, .52f, .33f);
            if (dark == default) dark = new Color(.48f, .31f, .18f);
            var px = new Color32[n * n];
            var h = new float[n * n];
            for (int py = 0; py < n; py++)
                for (int pxi = 0; pxi < n; pxi++)
                {
                    float u = (float)pxi / n, v = (float)py / n;
                    float warp = Fbm(u * 2, v * 8, seed, 2, 3);
                    float rings = Mathf.Sin((v * 40 + warp * 6) * Mathf.PI * 2) * .5f + .5f;
                    float streak = Fbm(u * 4, v * 64, seed + 5, 4, 3);
                    float t = Mathf.Pow(rings, 3) * .6f + streak * .5f;
                    var c = Color.Lerp(light, dark, Clamp01(t));
                    c *= .9f + .15f * Fbm(u * 8, v * 8, seed + 9, 8, 3);
                    px[py * n + pxi] = C(c);
                    h[py * n + pxi] = rings * .3f + streak * .5f;
                }
            return new Pair { Albedo = Finish(NewTex(n, false, "wood" + seed), px), Normal = NormalFromHeight(h, n, 3f, "wood" + seed) };
        }

        // ---------------------------------------------------------------- generic fine noise (tint per material)
        public static Pair Fine(int n, string name, float amp, float freq, int seed, float bump)
        {
            var px = new Color32[n * n];
            var h = new float[n * n];
            int f = Mathf.Max(1, Mathf.RoundToInt(freq));
            for (int py = 0; py < n; py++)
                for (int pxi = 0; pxi < n; pxi++)
                {
                    float u = (float)pxi / n, v = (float)py / n;
                    float a = Fbm(u * f, v * f, seed, f, 4), b = Fbm(u * f * 8, v * f * 8, seed + 3, f * 8, 2);
                    float g = 1 - amp + amp * (a * .7f + b * .3f);
                    px[py * n + pxi] = C(new Color(g, g, g));
                    h[py * n + pxi] = a * .4f + b * .6f;
                }
            return new Pair { Albedo = Finish(NewTex(n, false, name), px), Normal = NormalFromHeight(h, n, bump, name) };
        }

        // ---------------------------------------------------------------- lawn (4 m tile)
        public static Pair Grass(int n = 1024)
        {
            var px = new Color32[n * n];
            var h = new float[n * n];
            for (int py = 0; py < n; py++)
                for (int pxi = 0; pxi < n; pxi++)
                {
                    float u = (float)pxi / n, v = (float)py / n;
                    float patch = Fbm(u * 4, v * 4, 3, 4, 4);
                    float blades = Fbm(u * 160, v * 40, 8, 160, 2);
                    float speck = Noise.Hash2(pxi, py, 21);
                    var c = Color.Lerp(new Color(.24f, .38f, .13f), new Color(.36f, .48f, .17f), patch);
                    c = Color.Lerp(c, new Color(.42f, .45f, .22f), Mathf.SmoothStep(.62f, .8f, patch) * .6f);
                    c *= .78f + .35f * blades;
                    if (speck > .992f) c = Color.Lerp(c, new Color(.36f, .3f, .2f), .7f);
                    px[py * n + pxi] = C(c);
                    h[py * n + pxi] = blades;
                }
            return new Pair { Albedo = Finish(NewTex(n, false, "grass"), px), Normal = NormalFromHeight(h, n, 2.5f, "grass") };
        }

        // ---------------------------------------------------------------- stucco / plaster (2 m tile)
        public static Pair Plaster(int n = 512)
        {
            var px = new Color32[n * n];
            var h = new float[n * n];
            for (int py = 0; py < n; py++)
                for (int pxi = 0; pxi < n; pxi++)
                {
                    float u = (float)pxi / n, v = (float)py / n;
                    float a = Fbm(u * 6, v * 6, 4, 6, 4), b = Fbm(u * 96, v * 96, 6, 96, 2);
                    float g = .9f + .07f * a + .05f * (b - .5f);
                    px[py * n + pxi] = C(new Color(g, g * .99f, g * .97f));
                    h[py * n + pxi] = b * .7f + a * .3f;
                }
            return new Pair { Albedo = Finish(NewTex(n, false, "plaster"), px), Normal = NormalFromHeight(h, n, 2.5f, "plaster") };
        }

        // ---------------------------------------------------------------- roof shingles (2 m tile)
        public static Pair Roof(int n = 512)
        {
            var px = new Color32[n * n];
            var h = new float[n * n];
            int rows = 10, cols = 6;
            for (int py = 0; py < n; py++)
                for (int pxi = 0; pxi < n; pxi++)
                {
                    float u = (float)pxi / n, v = (float)py / n;
                    int row = Mathf.Min(rows - 1, (int)(v * rows));
                    float rv = v * rows - row;
                    float uu = u * cols + (row % 2) * .5f;
                    int col = Mathf.FloorToInt(uu);
                    float ru = uu - col;
                    float id = Noise.Hash2(row, ((col % cols) + cols) % cols, 4);
                    float gap = Mathf.Min(ru, 1 - ru);
                    float g = (.75f + .3f * id) * (.6f + .4f * rv) * (gap < .03f ? .55f : 1f);
                    g *= .9f + .15f * Fbm(u * 64, v * 64, 2, 64, 2);
                    px[py * n + pxi] = C(new Color(g, g, g));
                    h[py * n + pxi] = rv * (gap < .03f ? .3f : 1f);
                }
            return new Pair { Albedo = Finish(NewTex(n, false, "roof"), px), Normal = NormalFromHeight(h, n, 5f, "roof") };
        }

        // ---------------------------------------------------------------- bark (1 m tile, ridges along V)
        public static Pair Bark(int n = 512)
        {
            var px = new Color32[n * n];
            var h = new float[n * n];
            for (int py = 0; py < n; py++)
                for (int pxi = 0; pxi < n; pxi++)
                {
                    float u = (float)pxi / n, v = (float)py / n;
                    float r = Fbm(u * 24, v * 3, 5, 24, 4);
                    float ridge = 1 - Mathf.Abs(r * 2 - 1);
                    var c = Color.Lerp(new Color(.18f, .14f, .1f), new Color(.38f, .31f, .23f), ridge);
                    c *= .85f + .2f * Fbm(u * 8, v * 8, 9, 8, 3);
                    px[py * n + pxi] = C(c);
                    h[py * n + pxi] = ridge;
                }
            return new Pair { Albedo = Finish(NewTex(n, false, "bark"), px), Normal = NormalFromHeight(h, n, 6f, "bark") };
        }

        // ---------------------------------------------------------------- leaf atlas with alpha
        public static Texture2D Leaves(int n = 512, int count = 16, int seed = 3)
        {
            var px = new Color32[n * n];
            var rng = new Rng((uint)seed);
            var acc = new float[n * n * 4];
            for (int i = 0; i < count; i++)
            {
                float cx = rng.Range(.18f, .82f) * n, cy = rng.Range(.18f, .82f) * n;
                float len = rng.Range(.16f, .26f) * n, wid = len * rng.Range(.32f, .45f);
                float ang = rng.Range(0, Mathf.PI * 2);
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                var baseC = Color.Lerp(new Color(.2f, .36f, .12f), new Color(.38f, .52f, .18f), rng.Next());
                int x0 = Mathf.Max(0, (int)(cx - len)), x1 = Mathf.Min(n - 1, (int)(cx + len));
                int y0 = Mathf.Max(0, (int)(cy - len)), y1 = Mathf.Min(n - 1, (int)(cy + len));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float a = (dx * ca + dy * sa) / len;      // along leaf -1..1
                        float b = (-dx * sa + dy * ca) / wid;     // across
                        if (a < -1 || a > 1) continue;
                        float halfW = Mathf.Sin((a + 1) * .5f * Mathf.PI);
                        halfW = Mathf.Pow(halfW, .8f) * (a > 0 ? 1 - a * .35f : 1);
                        float e = Mathf.Abs(b) - halfW;
                        if (e > .06f) continue;
                        float alpha = Clamp01((.06f - e) / .12f);
                        float vein = Mathf.Abs(b) < .06f ? .78f : 1f;
                        float side = .9f + .2f * Mathf.Sign(b) * .5f;
                        var c = baseC * vein * side * (.9f + .2f * (1 - Mathf.Abs(a)));
                        int k = (y * n + x) * 4;
                        float inv = 1 - alpha;
                        acc[k] = acc[k] * inv + c.r * alpha;
                        acc[k + 1] = acc[k + 1] * inv + c.g * alpha;
                        acc[k + 2] = acc[k + 2] * inv + c.b * alpha;
                        acc[k + 3] = Mathf.Max(acc[k + 3], alpha);
                    }
            }
            for (int k = 0; k < n * n; k++)
            {
                float a = acc[k * 4 + 3];
                // un-premultiply so mip edges keep leaf colour
                float r = a > 0 ? acc[k * 4] : .25f, g = a > 0 ? acc[k * 4 + 1] : .38f, b = a > 0 ? acc[k * 4 + 2] : .14f;
                px[k] = new Color32((byte)(Clamp01(r) * 255), (byte)(Clamp01(g) * 255), (byte)(Clamp01(b) * 255), (byte)(a * 255));
            }
            var t = NewTex(n, false, "leaves");
            t.wrapMode = TextureWrapMode.Clamp;
            return Finish(t, px);
        }

        /// <summary>Soft round particle sprite.</summary>
        public static Texture2D SoftDot(int n = 64)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + .5f) / n * 2 - 1, dy = (y + .5f) / n * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Clamp01(1 - d);
                    a = a * a * (3 - 2 * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            var t = NewTex(n, false, "dot");
            t.wrapMode = TextureWrapMode.Clamp;
            return Finish(t, px);
        }

        public static Texture2D Solid(Color c)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = c;
            t.SetPixels32(px); t.Apply();
            return t;
        }
    }
}
