using UnityEngine;

namespace Banjjak
{
    /// <summary>Simple 2D painter (canvas-style, y down) used for decals and patterned floors.</summary>
    public class Painter
    {
        public readonly int W, H;
        public readonly Color[] Px;
        public readonly float[] Height;
        Rng rng;

        public Painter(int w, int h, Color fill, int seed = 1)
        {
            W = w; H = h; Px = new Color[w * h]; Height = new float[w * h];
            for (int i = 0; i < Px.Length; i++) Px[i] = fill;
            rng = new Rng((uint)seed);
        }

        public static Color C(uint hex, float a = 1) => G.Hex(hex, a);

        void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || a <= 0) return;
            int k = y * W + x;
            Px[k] = Color.Lerp(Px[k], new Color(c.r, c.g, c.b, 1), Mathf.Clamp01(a * c.a));
        }

        public void Rect(float x, float y, float w, float h, Color c, float height = float.NaN)
        {
            for (int yy = Mathf.Max(0, (int)y); yy < Mathf.Min(H, (int)(y + h)); yy++)
                for (int xx = Mathf.Max(0, (int)x); xx < Mathf.Min(W, (int)(x + w)); xx++)
                {
                    Blend(xx, yy, c, 1);
                    if (!float.IsNaN(height)) Height[yy * W + xx] = height;
                }
        }

        public void RoundRect(float x, float y, float w, float h, float r, Color c, float height = float.NaN)
        {
            for (int yy = Mathf.Max(0, (int)y - 1); yy < Mathf.Min(H, (int)(y + h) + 1); yy++)
                for (int xx = Mathf.Max(0, (int)x - 1); xx < Mathf.Min(W, (int)(x + w) + 1); xx++)
                {
                    float px = xx + .5f, py = yy + .5f;
                    float cx = Mathf.Clamp(px, x + r, x + w - r), cy = Mathf.Clamp(py, y + r, y + h - r);
                    float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    float a = Mathf.Clamp01(r - d + .5f);
                    if (r <= 0) a = (px >= x && px <= x + w && py >= y && py <= y + h) ? 1 : 0;
                    Blend(xx, yy, c, a);
                    if (!float.IsNaN(height) && a > .5f) Height[yy * W + xx] = height;
                }
        }

        public void Ellipse(float cx, float cy, float rx, float ry, float ang, Color c, float height = float.NaN)
        {
            float R = Mathf.Max(rx, ry) + 1, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            for (int yy = Mathf.Max(0, (int)(cy - R)); yy < Mathf.Min(H, (int)(cy + R) + 1); yy++)
                for (int xx = Mathf.Max(0, (int)(cx - R)); xx < Mathf.Min(W, (int)(cx + R) + 1); xx++)
                {
                    float dx = xx + .5f - cx, dy = yy + .5f - cy;
                    float u = (dx * ca + dy * sa) / rx, v = (-dx * sa + dy * ca) / ry;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((1 - d) * Mathf.Min(rx, ry) + .5f);
                    Blend(xx, yy, c, a);
                    if (!float.IsNaN(height) && a > .5f) Height[yy * W + xx] = height;
                }
        }

        public void Circle(float cx, float cy, float r, Color c, float height = float.NaN) => Ellipse(cx, cy, r, r, 0, c, height);

        public void RingStroke(float cx, float cy, float r, float w, Color c)
        {
            float R = r + w;
            for (int yy = Mathf.Max(0, (int)(cy - R)); yy < Mathf.Min(H, (int)(cy + R) + 1); yy++)
                for (int xx = Mathf.Max(0, (int)(cx - R)); xx < Mathf.Min(W, (int)(cx + R) + 1); xx++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((xx + .5f - cx) * (xx + .5f - cx) + (yy + .5f - cy) * (yy + .5f - cy)) - r);
                    Blend(xx, yy, c, Mathf.Clamp01(w / 2 - d + .5f));
                }
        }

        /// <summary>Window glass with a sky-ish gradient and a soft diagonal sheen.</summary>
        public void Glass(float x, float y, float w, float h, float r, Color? frame = null)
        {
            if (frame.HasValue) RoundRect(x - 4, y - 4, w + 8, h + 8, r + 3, frame.Value);
            for (int yy = Mathf.Max(0, (int)y); yy < Mathf.Min(H, (int)(y + h)); yy++)
                for (int xx = Mathf.Max(0, (int)x); xx < Mathf.Min(W, (int)(x + w)); xx++)
                {
                    float px = xx + .5f, py = yy + .5f;
                    float cx = Mathf.Clamp(px, x + r, x + w - r), cy = Mathf.Clamp(py, y + r, y + h - r);
                    float a = Mathf.Clamp01(r - Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) + .5f);
                    float t = (py - y) / h;
                    var c = Color.Lerp(C(0x8fb0c4), C(0x2f4352), t);
                    float sheen = Mathf.Repeat((px - x) / w + t * .6f, 1f);
                    if (sheen > .18f && sheen < .3f) c = Color.Lerp(c, Color.white, .18f);
                    Blend(xx, yy, c, a);
                }
        }

        public void Grain(int count, float amp, float size = 2)
        {
            for (int i = 0; i < count; i++)
            {
                int x = (int)(rng.Next() * W), y = (int)(rng.Next() * H);
                float v = (rng.Next() - .5f) * 2 * amp;
                int s = Mathf.Max(1, (int)(rng.Next() * size + .5f));
                for (int yy = y; yy < Mathf.Min(H, y + s); yy++)
                    for (int xx = x; xx < Mathf.Min(W, x + s); xx++)
                    {
                        int k = yy * W + xx;
                        Px[k] = new Color(Px[k].r * (1 + v), Px[k].g * (1 + v), Px[k].b * (1 + v));
                        Height[k] += v;
                    }
            }
        }

        public void Noise(float amp, float freq, int seed)
        {
            int f = Mathf.Max(1, (int)freq);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = Banjjak.Noise.FbmT((float)x / W * f, (float)y / H * f, seed, f, 4) - .5f;
                    int k = y * W + x;
                    float m = 1 + n * 2 * amp;
                    Px[k] = new Color(Px[k].r * m, Px[k].g * m, Px[k].b * m);
                    Height[k] += n * .5f;
                }
        }

        public Texture2D Albedo(string name, bool clamp = true)
        {
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = Px[y * W + x];
                    px[(H - 1 - y) * W + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1);
                }
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = name, wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        public Texture2D Normal(string name, float strength)
        {
            if (W != H) return null;
            var flipped = new float[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) flipped[(H - 1 - y) * W + x] = Height[y * W + x];
            return TexGen.NormalFromHeight(flipped, W, strength, name);
        }

        public TexGen.Pair Pair(string name, float strength, bool clamp = false) =>
            new TexGen.Pair { Albedo = Albedo(name, clamp), Normal = Normal(name, strength) };
    }

    public static partial class TexGen
    {
        static readonly Color Body = Painter.C(0xf3f1ea), Teal = Painter.C(0x1d8a8f), Orange = Painter.C(0xf08a3c);

        // ------------------------------------------------------------------ patterned surfaces
        public static Pair Concrete()
        {
            var p = new Painter(512, 512, Painter.C(0xbcb9b2), 3);
            p.Noise(.12f, 4, 3);
            p.Grain(14000, .08f);
            p.Rect(0, 0, 512, 4, Painter.C(0x7f7b74), 0); p.Rect(0, 0, 4, 512, Painter.C(0x7f7b74), 0);
            return p.Pair("concrete", 3);
        }

        public static Pair Asphalt()
        {
            var p = new Painter(512, 512, Painter.C(0x56585b), 4);
            p.Noise(.15f, 4, 5);
            p.Grain(20000, .25f, 2.5f);
            return p.Pair("asphalt", 2.5f);
        }

        public static Pair Skate()
        {
            var p = new Painter(512, 512, Painter.C(0xb7b3ab), 5);
            p.Noise(.08f, 3, 7);
            p.Grain(6000, .05f);
            p.Rect(0, 0, 512, 3, Painter.C(0x8d8981), 0); p.Rect(0, 0, 3, 512, Painter.C(0x8d8981), 0);
            return p.Pair("skate", 1.5f);
        }

        public static Pair Siding()
        {
            var p = new Painter(512, 512, Color.white, 6);
            int bh = 512 / 8;
            for (int b = 0; b < 8; b++)
            {
                float v = .95f + Noise.Hash2(b, 1, 3) * .06f;
                for (int y = 0; y < bh; y++)
                {
                    float t = (float)y / bh;
                    float shade = t < .82f ? 1.06f - t * .1f : .96f - (t - .82f) * 1.5f;
                    var c = new Color(196 / 255f * v * shade, 209 / 255f * v * shade, 215 / 255f * v * shade);
                    p.Rect(0, b * bh + y, 512, 1, c, t);
                }
                p.Rect(0, b * bh + bh - 3, 512, 3, Painter.C(0x5e6a70), 0);
            }
            p.Grain(3000, .05f);
            return p.Pair("siding", 4);
        }

        public static Pair Tile()
        {
            var p = new Painter(512, 512, Painter.C(0xb9b5ad), 7);
            for (int ty = 0; ty < 4; ty++)
                for (int tx = 0; tx < 4; tx++)
                {
                    float v = .94f + Noise.Hash2(tx, ty, 4) * .06f;
                    p.RoundRect(tx * 128 + 3, ty * 128 + 3, 122, 122, 4, new Color(236 / 255f * v, 233 / 255f * v, 226 / 255f * v), 1);
                }
            p.Noise(.05f, 4, 9);
            p.Grain(3000, .04f);
            return p.Pair("tile", 2.5f);
        }

        public static Pair Vinyl()
        {
            var p = new Painter(512, 512, Painter.C(0xa7aaa8), 8);
            var r = new Rng(31u);
            for (int i = 0; i < 9000; i++)
            {
                float k = r.Next();
                var c = k < .4f ? new Color(1, 1, 1, .35f) : k < .8f ? new Color(.24f, .25f, .26f, .3f) : new Color(.47f, .55f, .59f, .35f);
                p.Rect(r.Next() * 512, r.Next() * 512, 2 + r.Next() * 3, 2 + r.Next() * 3, c);
            }
            p.Rect(0, 0, 512, 2, new Color(0, 0, 0, .25f), 0);
            return p.Pair("vinyl", .8f);
        }

        public static Pair Deck()
        {
            var p = new Painter(512, 512, Color.white, 9);
            int n = 6, pw = 512 / n;
            for (int k = 0; k < n; k++)
            {
                float v = .82f + Noise.Hash2(k, 2, 5) * .22f;
                p.Rect(0, k * pw, 512, pw, new Color(170 / 255f * v, 120 / 255f * v, 78 / 255f * v), 1);
                for (int l = 0; l < 14; l++) p.Rect(0, k * pw + Noise.Hash2(k, l, 6) * pw, 512, 1 + Noise.Hash2(k, l, 7), new Color(.27f, .16f, .07f, .05f + Noise.Hash2(k, l, 8) * .1f));
                p.Rect(0, k * pw, 512, 6, Painter.C(0x25180c), 0);
            }
            p.Grain(4000, .08f);
            return p.Pair("deck", 3);
        }

        public static Pair Diamond()
        {
            var p = new Painter(256, 256, Painter.C(0x9aa1a8), 10);
            for (int y = 0; y < 256; y += 32)
                for (int x = 0; x < 256; x += 32)
                {
                    int o = (y / 32) % 2 == 1 ? 16 : 0;
                    p.Ellipse(x + o + 8, y + 16, 10, 3, .7f, Painter.C(0xc4cacf), 1);
                }
            p.Grain(1500, .05f);
            return p.Pair("diamond", 4);
        }

        public static Pair Rubber()
        {
            var p = new Painter(256, 256, Painter.C(0xd6d6d6), 11);
            var r = new Rng(41u);
            for (int i = 0; i < 5000; i++) p.Rect(r.Next() * 256, r.Next() * 256, 2 + r.Next() * 2, 2 + r.Next() * 2, r.Next() < .5f ? Painter.C(0xf6f6f6) : Painter.C(0x9c9c9c), r.Next());
            p.Rect(0, 0, 256, 3, Painter.C(0x6d6d6d), 0); p.Rect(0, 0, 3, 256, Painter.C(0x6d6d6d), 0);
            return p.Pair("rubber", 1.5f);
        }

        // ------------------------------------------------------------------ decals (one face each)
        public static Texture2D CamperSide()
        {
            var p = new Painter(1024, 512, Body);
            p.Rect(0, 432, 1024, 80, Painter.C(0xd6d1c4)); p.Rect(0, 300, 1024, 34, Teal); p.Rect(0, 342, 1024, 10, Orange);
            p.Glass(90, 96, 260, 136, 24); p.Glass(410, 96, 220, 136, 24);
            p.RoundRect(756, 62, 184, 368, 18, Painter.C(0xc4beaf)); p.RoundRect(760, 66, 176, 360, 16, Body);
            p.Glass(786, 92, 124, 110, 14); p.RoundRect(784, 262, 42, 12, 6, Painter.C(0x3a3f44));
            p.Grain(4000, .03f);
            return p.Albedo("camperSide");
        }

        public static Texture2D CamperFront()
        {
            var p = new Painter(512, 512, Body);
            p.Glass(40, 40, 432, 196, 40); p.Rect(0, 250, 512, 8, Painter.C(0xc9c3b5));
            p.Grain(2000, .03f);
            return p.Albedo("camperFront");
        }

        public static Texture2D CamperBack()
        {
            var p = new Painter(512, 512, Body);
            p.Rect(0, 432, 512, 80, Painter.C(0xd6d1c4)); p.Rect(0, 300, 512, 34, Teal); p.Rect(0, 342, 512, 10, Orange);
            p.Glass(140, 80, 232, 120, 22);
            p.RoundRect(30, 290, 46, 96, 12, Painter.C(0xc4262a)); p.RoundRect(436, 290, 46, 96, 12, Painter.C(0xc4262a));
            p.Rect(193, 369, 126, 52, Painter.C(0x333333)); p.Rect(196, 372, 120, 46, Painter.C(0xfbfbf6));
            for (int i = 0; i < 6; i++) p.Rect(206 + i * 18, 384, 10, 22, Painter.C(0x333333));
            return p.Albedo("camperBack");
        }

        public static Texture2D HoodFront()
        {
            var p = new Painter(512, 256, Body);
            p.RoundRect(140, 66, 232, 96, 18, Painter.C(0x2a2f33));
            for (int i = 0; i < 5; i++) p.Rect(152, 78 + i * 17, 208, 6, Painter.C(0x4b5359));
            foreach (int x in new[] { 72, 440 }) { p.Circle(x, 112, 40, Painter.C(0x3a3f44)); p.Circle(x, 112, 31, Painter.C(0xfff4d0)); }
            p.Rect(0, 210, 512, 46, Painter.C(0xd6d1c4));
            return p.Albedo("hoodFront");
        }

        public static Texture2D HoodSide()
        {
            var p = new Painter(256, 256, Body);
            p.Rect(0, 43, 256, 34, Teal); p.Rect(0, 85, 256, 10, Orange); p.Rect(0, 210, 256, 46, Painter.C(0xd6d1c4));
            return p.Albedo("hoodSide");
        }

        public static Texture2D Hubcap()
        {
            var p = new Painter(256, 256, Painter.C(0x202020));
            for (int r = 127; r > 0; r -= 2) p.Circle(128 + (127 - r) * -.12f, 128 + (127 - r) * -.2f, r, Color.Lerp(Painter.C(0x8c949b), Painter.C(0xe4e8ec), 1 - r / 127f));
            for (int k = 0; k < 5; k++) { float a = k / 5f * Mathf.PI * 2; p.Ellipse(128 + Mathf.Cos(a) * 72, 128 + Mathf.Sin(a) * 72, 26, 16, a, Painter.C(0x2b3036)); }
            p.Circle(128, 128, 28, Painter.C(0xc3c9ce));
            return p.Albedo("hubcap");
        }

        public static Texture2D Door()
        {
            var p = new Painter(220, 440, Painter.C(0x8e2b26));
            for (int r = 0; r < 3; r++)
                for (int k = 0; k < 2; k++)
                {
                    float x = 26 + k * 90, y = 26 + r * 136, h = r == 0 ? 100 : 120;
                    p.RoundRect(x, y, 78, h, 4, new Color(0, 0, 0, .25f)); p.RoundRect(x + 6, y + 6, 66, h - 12, 3, Painter.C(0x9c332d));
                }
            return p.Albedo("door");
        }

        public static Texture2D Garage()
        {
            var p = new Painter(600, 480, Painter.C(0xecebe6));
            for (int r = 0; r < 4; r++)
            {
                float y = r * 120;
                p.Rect(0, y + 116, 600, 4, new Color(0, 0, 0, .25f));
                for (int k = 0; k < 4; k++)
                {
                    float x = 14 + k * 146;
                    if (r == 0) p.Glass(x + 8, y + 20, 122, 70, 6, Painter.C(0xcfcdc6));
                    else { p.RoundRect(x + 8, y + 18, 122, 82, 6, new Color(0, 0, 0, .08f)); p.RoundRect(x + 12, y + 22, 114, 74, 5, Painter.C(0xf2f1ec)); }
                }
            }
            return p.Albedo("garage");
        }

        public static Texture2D AcUnit()
        {
            var p = new Painter(400, 320, Painter.C(0xd4d7d8));
            p.Circle(250, 160, 118, Painter.C(0x3a3f43));
            for (int r = 20; r < 118; r += 14) p.RingStroke(250, 160, r, 4, Painter.C(0x8b9195));
            for (int y = 30; y < 290; y += 14) p.Rect(24, y, 80, 6, Painter.C(0x8b9195));
            return p.Albedo("acunit");
        }

        public static Texture2D Pump()
        {
            var p = new Painter(320, 720, Painter.C(0xf4f6f7));
            p.Rect(0, 0, 320, 96, Painter.C(0x1f86f0)); p.Rect(0, 96, 320, 14, Painter.C(0xff7a3d));
            p.RoundRect(40, 150, 240, 130, 14, Painter.C(0x20262b));
            for (int i = 0; i < 5; i++) p.Rect(118 + i * 30, 196, 20, 40, Painter.C(0x7cf29a));
            for (int r = 0; r < 3; r++) for (int k = 0; k < 3; k++) p.RoundRect(70 + k * 64, 310 + r * 52, 50, 40, 8, Painter.C(0xcfd5da));
            p.RoundRect(90, 480, 140, 190, 18, Painter.C(0x2a2f33)); p.RoundRect(110, 500, 100, 26, 6, Painter.C(0xffcc33));
            return p.Albedo("pump");
        }

        public static Texture2D StoreSign()
        {
            var p = new Painter(1500, 90, Painter.C(0x1b8a5a));
            // abstract lettering blocks (the sign reads as text from a distance)
            var r = new Rng(5u);
            float x = 60;
            for (int i = 0; i < 6; i++) { float w = 34 + r.Next() * 18; p.RoundRect(x, 22, w, 48, 6, Color.white); x += w + 14; }
            x = 520;
            for (int i = 0; i < 12; i++) { float w = 22 + r.Next() * 14; p.RoundRect(x, 32, w, 30, 4, new Color(1, 1, 1, .85f)); x += w + (i % 4 == 3 ? 30 : 10); }
            p.Circle(1400, 45, 26, Painter.C(0xffd34d));
            return p.Albedo("storeSign");
        }

        public static Texture2D Vending()
        {
            var p = new Painter(250, 475, Painter.C(0xd8342c));
            p.RoundRect(16, 18, 160, 330, 10, Painter.C(0xeef3f6));
            uint[] cols = { 0x2d7be0, 0xf2c12e, 0x19b5a5, 0xe23d8f, 0xff7a3d, 0x7b3fd1 };
            for (int r = 0; r < 5; r++)
                for (int k = 0; k < 4; k++)
                {
                    p.RoundRect(26 + k * 37, 30 + r * 62, 26, 48, 8, Painter.C(cols[(r * 4 + k) % cols.Length]));
                    p.Rect(30 + k * 37, 36 + r * 62, 5, 32, new Color(1, 1, 1, .4f));
                }
            p.RoundRect(190, 60, 40, 80, 6, Painter.C(0x2a2f33)); p.RoundRect(30, 380, 140, 60, 8, Painter.C(0x2a2f33));
            return p.Albedo("vending");
        }

        public static Texture2D Fascia()
        {
            var p = new Painter(1400, 70, Painter.C(0xf7f8f9));
            p.Rect(0, 30, 1400, 16, Painter.C(0x1f86f0)); p.Rect(0, 50, 1400, 7, Painter.C(0xff7a3d));
            return p.Albedo("fascia");
        }

        public static Texture2D CanopyUnder()
        {
            var p = new Painter(700, 450, Painter.C(0xd9dcde));
            for (int x = 0; x < 700; x += 50) p.Rect(x, 0, 2, 450, new Color(0, 0, 0, .12f));
            for (int r = 0; r < 3; r++) for (int k = 0; k < 4; k++) p.RoundRect(70 + k * 160, 70 + r * 120, 90, 50, 6, Painter.C(0xfffdf4));
            return p.Albedo("canopyUnder");
        }

        public static Texture2D TowerSign()
        {
            var p = new Painter(1200, 100, Painter.C(0x2a3440));
            var r = new Rng(9u);
            float x = 80;
            for (int i = 0; i < 5; i++) { float w = 50 + r.Next() * 20; p.RoundRect(x, 22, w, 56, 6, Painter.C(0xf6d36b)); x += w + 18; }
            x = 560;
            for (int i = 0; i < 10; i++) { float w = 26 + r.Next() * 14; p.RoundRect(x, 36, w, 30, 4, new Color(1, 1, 1, .9f)); x += w + (i % 3 == 2 ? 30 : 10); }
            return p.Albedo("towerSign");
        }
    }
}
