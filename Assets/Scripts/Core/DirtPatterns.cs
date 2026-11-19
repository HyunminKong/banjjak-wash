using UnityEngine;

namespace Banjjak
{
    /// <summary>
    /// "base+over+over" dirt recipe for a component, e.g. "mud+leaves+moss".
    /// </summary>
    public class DirtSpec
    {
        public int Base;
        public int[] Over;
        public string Key;

        public static DirtSpec Parse(string s)
        {
            var parts = s.Split('+');
            var spec = new DirtSpec { Key = s, Base = Dirt.Index(parts[0]), Over = new int[parts.Length - 1] };
            for (int i = 1; i < parts.Length; i++) spec.Over[i - 1] = Dirt.Index(parts[i]);
            return spec;
        }
    }

    /// <summary>Procedural placement of each dirt type over a surface (ported from the web game).</summary>
    public static class DirtPatterns
    {
        const float TAU = Mathf.PI * 2f;
        static float VN(float x, float y, int s) => Noise.VNoise(x, y, s);
        static float F(float x, float y, int s) => Noise.Fbm(x, y, s);
        static float H(int x, int y, int s) => Noise.Hash2(x, y, s);

        static float Spots(float a, float b, float cs, float prob, int s, float rmin, float rmax, float aspect = 1)
        {
            int cx = Mathf.FloorToInt(a / cs), cy = Mathf.FloorToInt(b / cs);
            float best = 0;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox, gy = cy + oy;
                    if (H(gx, gy, s + 2) > prob) continue;
                    float sx = (gx + H(gx, gy, s)) * cs, sy = (gy + H(gx, gy, s + 1)) * cs;
                    float rad = rmin + (rmax - rmin) * H(gx, gy, s + 3);
                    float ang = H(gx, gy, s + 4) * TAU, dx = a - sx, dy = b - sy, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                    float u = (dx * ca + dy * sa) / rad, v = (-dx * sa + dy * ca) / (rad * aspect), dd = u * u + v * v;
                    if (dd < 1) best = Mathf.Max(best, 1 - dd * .3f);
                }
            return best;
        }

        public static float Eval(int type, float a, float b, float v, bool vert, bool up, int s)
        {
            switch (Dirt.All[type].Key)
            {
                case "dust": return .62f + .5f * (F(a * .9f + s, b * .9f - s, s) - .5f) + .2f * (VN(a * 4 + s, b * 4, s + 3) - .5f) + (vert ? .12f * (1 - v) : .1f);
                case "pollen": { float n = F(a * 1.3f + s, b * 1.3f, s + 2); return up ? .55f + .5f * (n - .5f) : .22f + .3f * (n - .5f); }
                case "mud": return .42f + 1.1f * (F(a * .9f + s, b * .9f - s, s) - .38f) + .25f * (VN(a * 4, b * 4, s + 3) - .5f) + (vert ? .55f * (1 - v) * (1 - v) - .1f : 0);
                case "leaves": { float k = Spots(a, b, .38f, .32f, s + 40, .05f, .09f, .45f); return k > 0 ? .82f + .1f * k : 0; }
                case "bird": { float k = Spots(a, b, .5f, vert ? .2f : .35f, s + 50, .05f, .12f, vert ? 1.8f : 1); return k > 0 ? .9f : 0; }
                case "grime": return .62f + .5f * (VN(a * 7 + s, b * .7f, s + 5) - .5f) + .3f * (F(a * .9f, b * .9f, s) - .5f) + (vert ? .18f * (1 - v) : 0);
                case "oil": { float o = F(a * .45f + s, b * .45f, s + 4); return o > .58f ? .75f + (o - .58f) * 2 : o > .52f ? .5f : 0; }
                case "tire":
                    {
                        if (!up) return 0;
                        float t = b - 1.4f * Mathf.Sin(a * .17f + s) - (s % 5), per = 6.5f, tt = ((t % per) + per) % per;
                        float d = Mathf.Min(Mathf.Abs(tt - 1.4f), Mathf.Abs(tt - 3.0f));
                        return d < .11f ? .72f + .25f * VN(a * 3, b * 3, s) : 0;
                    }
                case "soot": return .3f + .7f * (vert ? v * v : .45f) + .35f * (F(a * .8f + s, b * .8f, s + 1) - .5f);
                case "mildew":
                    {
                        float m = Mathf.Max(1 - v, v);
                        float cl = F(a * .5f + s, b * .5f, s + 4) + (vert ? m * m * .35f : 0), sp = VN(a * 16 + s, b * 16, s + 5);
                        return cl > .55f && sp > .5f ? .82f : cl > .63f ? .42f : 0;
                    }
                case "algae": return .3f + 1.2f * (F(a * .7f + s, b * .7f, s + 6) - .42f) + (vert ? .65f * (1 - v) * (1 - v) : .1f);
                case "moss": return .3f + 1.4f * (F(a * .9f + s, b * .9f - s, s) - .4f) + .3f * (VN(a * 4, b * 4, s + 3) - .5f) + (vert ? .5f * (1 - v) * (1 - v) : .05f);
                case "rust": return .45f + (F(a * .9f + s, b * .9f, s) - .45f) + .35f * (VN(a * 4, b * 4, s + 3) - .5f) + (vert ? .3f * (1 - v) + .35f * Mathf.Max(0, VN(a * 9 + s, b * .6f, s + 8) - .4f) : 0);
                case "lime": return .25f + .9f * (vert ? Mathf.Pow(1 - v, 1.4f) : .5f) + .3f * (F(a * 1.1f + s, b * 1.1f, s + 7) - .5f) + .15f * (VN(a * 6, b * 6, s + 9) - .5f);
                case "gum": return up && Spots(a, b, .3f, .17f, s + 60, .014f, .03f) > 0 ? .95f : 0;
                case "paint":
                    {
                        float p = Mathf.Sin(a * 2.4f + F(a * .6f, b * .6f, s) * 7), q = Mathf.Sin(b * 2.8f + F(a * .5f + 3, b * .5f, s + 1) * 8);
                        float line = Mathf.Max(1 - Mathf.Abs(p) * 4, 1 - Mathf.Abs(q) * 4), blob = F(a * .35f + s, b * .35f, s + 9);
                        return (line > 0 && blob > .4f) ? .8f + .2f * line : (blob > .64f ? .9f : 0);
                    }
            }
            return 0;
        }

        /// <summary>Returns (amount, type) for a world point with world normal.</summary>
        public static (float amt, byte type) Gen(DirtSpec spec, Vector3 pw, Vector3 nw, float v, int s)
        {
            float ay = Mathf.Abs(nw.y), ax = Mathf.Abs(nw.x), az = Mathf.Abs(nw.z);
            float a, b;
            if (ay > .7f) { a = pw.x; b = pw.z; }
            else if (ax > az) { a = pw.z; b = pw.y; }
            else { a = pw.x; b = pw.y; }
            bool vert = ay < .5f, up = nw.y > .5f;
            float amt = Eval(spec.Base, a, b, v, vert, up, s);
            int ty = spec.Base;
            foreach (int o in spec.Over)
            {
                float x = Eval(o, a, b, v, vert, up, s + 31);
                if (x > .5f && x >= amt * .7f) { amt = Mathf.Max(x, amt); ty = o; }
            }
            if (amt < .14f) return (0, (byte)ty);
            return (Mathf.Min(1, amt), (byte)ty);
        }
    }
}
