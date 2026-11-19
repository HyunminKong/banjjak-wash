using UnityEngine;

namespace Banjjak
{
    /// <summary>Deterministic hash / value noise, identical in spirit to the web version.</summary>
    public static class Noise
    {
        public static float Hash2(int x, int y, int s)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 1442695041;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                h ^= (int)((uint)h >> 16);
                return (uint)h / 4294967296f;
            }
        }

        public static float VNoise(float x, float y, int s, int period = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            int x1 = xi + 1, y1 = yi + 1;
            if (period > 0)
            {
                xi = ((xi % period) + period) % period;
                yi = ((yi % period) + period) % period;
                x1 = ((x1 % period) + period) % period;
                y1 = ((y1 % period) + period) % period;
            }
            float a = Hash2(xi, yi, s), b = Hash2(x1, yi, s), c = Hash2(xi, y1, s), d = Hash2(x1, y1, s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        public static float Fbm(float x, float y, int s) =>
            0.5f * VNoise(x, y, s) + 0.3f * VNoise(x * 2.1f, y * 2.1f, s + 7) + 0.2f * VNoise(x * 4.3f, y * 4.3f, s + 13);

        /// <summary>Tileable fbm over a period (for textures). x,y in lattice units of the base octave.</summary>
        public static float FbmT(float x, float y, int s, int period, int octaves = 4, float gain = 0.5f)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            int p = period;
            float fx = x, fy = y;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * VNoise(fx, fy, s + o * 17, p);
                norm += amp;
                amp *= gain;
                fx *= 2; fy *= 2; p *= 2;
            }
            return sum / norm;
        }

        public static uint HashStr(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in s) { h ^= ch; h *= 16777619; }
                return h;
            }
        }
    }

    /// <summary>mulberry32 port.</summary>
    public class Rng
    {
        uint a;
        public Rng(uint seed) { a = seed; }
        public Rng(string seed) { a = Noise.HashStr(seed); }

        public float Next()
        {
            unchecked
            {
                a += 0x6D2B79F5;
                uint t = a;
                t = (t ^ (t >> 15)) * (1 | t);
                t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296f;
            }
        }

        public float Range(float lo, float hi) => lo + (hi - lo) * Next();
        public int Int(int n) => Mathf.Min(n - 1, (int)(Next() * n));
    }
}
