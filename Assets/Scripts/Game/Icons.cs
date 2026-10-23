using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snake3D
{
    /// <summary>介面小圖示：程式用距離場畫出來的白底透明圖（顏色由 Image.color 決定），不依賴任何外部圖檔或字型符號。</summary>
    public static class Icons
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            Sprite sp;
            if (cache.TryGetValue(name, out sp)) return sp;
            Func<float, float, float> sdf = Shape(name);
            const int S = 96;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float nx = (x + 0.5f) / S * 2f - 1f, ny = (y + 0.5f) / S * 2f - 1f;
                    float d = sdf(nx, ny);
                    px[y * S + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d * (S / 2f)));
                }
            tex.SetPixels(px); tex.Apply(false, true);
            sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            cache[name] = sp;
            return sp;
        }

        static float Seg(float x, float y, float ax, float ay, float bx, float by, float w)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            float cx = px - dx * t, cy = py - dy * t;
            return Mathf.Sqrt(cx * cx + cy * cy) - w;
        }

        static float Box(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x - cx) - (hw - r), qy = Mathf.Abs(y - cy) - (hh - r);
            return Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        static Func<float, float, float> Shape(string name)
        {
            switch (name)
            {
                case "close":
                    return (x, y) => Mathf.Min(Seg(x, y, -0.5f, -0.5f, 0.5f, 0.5f, 0.12f), Seg(x, y, -0.5f, 0.5f, 0.5f, -0.5f, 0.12f));
                case "plus":
                    return (x, y) => Mathf.Min(Seg(x, y, -0.55f, 0, 0.55f, 0, 0.12f), Seg(x, y, 0, -0.55f, 0, 0.55f, 0.12f));
                case "minus":
                    return (x, y) => Seg(x, y, -0.55f, 0, 0.55f, 0, 0.12f);
                case "back":
                    return (x, y) => Mathf.Min(Seg(x, y, 0.25f, 0.6f, -0.3f, 0, 0.13f), Seg(x, y, -0.3f, 0, 0.25f, -0.6f, 0.13f));
                case "pause":
                    return (x, y) => Mathf.Min(Box(x, y, -0.27f, 0, 0.16f, 0.55f, 0.08f), Box(x, y, 0.27f, 0, 0.16f, 0.55f, 0.08f));
                case "down":
                    return (x, y) => Mathf.Min(Seg(x, y, -0.55f, 0.2f, 0, -0.35f, 0.12f), Seg(x, y, 0, -0.35f, 0.55f, 0.2f, 0.12f));
                case "chat":
                    return (x, y) => Mathf.Min(Box(x, y, 0, 0.12f, 0.75f, 0.52f, 0.25f), Seg(x, y, -0.3f, -0.4f, -0.5f, -0.78f, 0.1f));
                case "info":
                    return (x, y) => Mathf.Min(Seg(x, y, 0, -0.1f, 0, -0.55f, 0.11f), Mathf.Sqrt(x * x + (y - 0.5f) * (y - 0.5f)) - 0.13f);
                case "gear":
                    return (x, y) =>
                    {
                        float r = Mathf.Sqrt(x * x + y * y);
                        float a = Mathf.Atan2(y, x);
                        float seg = Mathf.PI / 4f;
                        float m = Mathf.Abs(Mathf.Repeat(a + seg / 2f, seg) - seg / 2f);
                        float outer = m < 0.2f ? 0.82f : 0.64f;
                        return Mathf.Max(r - outer, 0.26f - r);
                    };
                case "globe":
                    return (x, y) =>
                    {
                        float r = Mathf.Sqrt(x * x + y * y);
                        float ring = Mathf.Abs(r - 0.7f) - 0.07f;
                        float ell = Mathf.Abs((Mathf.Sqrt(x * x / (0.34f * 0.34f) + y * y / (0.7f * 0.7f)) - 1f) * 0.34f) - 0.06f;
                        float eq = Mathf.Max(Mathf.Abs(y) - 0.06f, Mathf.Abs(x) - 0.7f);
                        float lat1 = Mathf.Max(Mathf.Abs(y - 0.38f) - 0.05f, Mathf.Abs(x) - 0.6f);
                        float lat2 = Mathf.Max(Mathf.Abs(y + 0.38f) - 0.05f, Mathf.Abs(x) - 0.6f);
                        return Mathf.Min(Mathf.Min(ring, ell), Mathf.Min(eq, Mathf.Min(lat1, lat2)));
                    };
                case "flash":
                    return (x, y) => Mathf.Min(Seg(x, y, 0.15f, 0.7f, -0.35f, -0.05f, 0.1f), Mathf.Min(Seg(x, y, -0.35f, -0.05f, 0.2f, 0.0f, 0.1f), Seg(x, y, 0.2f, 0.0f, -0.15f, -0.7f, 0.1f)));
                default:
                    return (x, y) => Mathf.Sqrt(x * x + y * y) - 0.5f;
            }
        }
    }
}
