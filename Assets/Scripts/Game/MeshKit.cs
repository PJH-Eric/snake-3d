using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snake3D
{
    /// <summary>程序建模：不依賴任何外部模型檔，用程式產生球、圓柱、圓錐、圓盤、圓環、星星。</summary>
    public static class MeshKit
    {
        static Mesh Build(List<Vector3> v, List<Vector2> uv, List<int> t, string name, bool flat = false)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>半徑 0.5 的球。</summary>
        public static Mesh Sphere(int lat = 10, int lon = 16)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= lat; i++)
            {
                float a = Mathf.PI * i / lat, y = Mathf.Cos(a) * 0.5f, r = Mathf.Sin(a) * 0.5f;
                for (int j = 0; j <= lon; j++)
                {
                    float b = Mathf.PI * 2 * j / lon;
                    v.Add(new Vector3(Mathf.Cos(b) * r, y, Mathf.Sin(b) * r)); uv.Add(new Vector2((float)j / lon, 1f - (float)i / lat));
                }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = i * (lon + 1) + j, b = a + lon + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(a + 1); t.Add(b + 1); t.Add(b);
                }
            // 面向外：上面的順序在 Unity（順時針為正面）下是朝外
            var m = Build(v, uv, t, "Sphere");
            return m;
        }

        /// <summary>半徑 0.5、高 1 的圓柱（中心在原點）。</summary>
        public static Mesh Cylinder(int seg = 12)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int j = 0; j <= seg; j++)
            {
                float b = Mathf.PI * 2 * j / seg, x = Mathf.Cos(b) * 0.5f, z = Mathf.Sin(b) * 0.5f;
                v.Add(new Vector3(x, -0.5f, z)); uv.Add(new Vector2((float)j / seg, 0));
                v.Add(new Vector3(x, 0.5f, z)); uv.Add(new Vector2((float)j / seg, 1));
            }
            for (int j = 0; j < seg; j++)
            {
                int a = j * 2;
                t.Add(a); t.Add(a + 1); t.Add(a + 2);
                t.Add(a + 1); t.Add(a + 3); t.Add(a + 2);
            }
            int topC = v.Count; v.Add(new Vector3(0, 0.5f, 0)); uv.Add(new Vector2(0.5f, 0.5f));
            int topStart = v.Count;
            for (int j = 0; j <= seg; j++) { float b = Mathf.PI * 2 * j / seg; v.Add(new Vector3(Mathf.Cos(b) * 0.5f, 0.5f, Mathf.Sin(b) * 0.5f)); uv.Add(new Vector2(0.5f, 0.5f)); }
            for (int j = 0; j < seg; j++) { t.Add(topC); t.Add(topStart + j + 1); t.Add(topStart + j); }
            return Build(v, uv, t, "Cylinder");
        }

        /// <summary>底半徑 0.5、高 1 的圓錐（底在 y=-0.5，尖端在 y=0.5）。</summary>
        public static Mesh Cone(int seg = 12)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int j = 0; j <= seg; j++)
            {
                float b = Mathf.PI * 2 * j / seg;
                v.Add(new Vector3(Mathf.Cos(b) * 0.5f, -0.5f, Mathf.Sin(b) * 0.5f)); uv.Add(new Vector2((float)j / seg, 0));
                v.Add(new Vector3(0, 0.5f, 0)); uv.Add(new Vector2((float)j / seg, 1));
            }
            for (int j = 0; j < seg; j++) { int a = j * 2; t.Add(a); t.Add(a + 1); t.Add(a + 2); }
            int c = v.Count; v.Add(new Vector3(0, -0.5f, 0)); uv.Add(new Vector2(0.5f, 0.5f));
            int st = v.Count;
            for (int j = 0; j <= seg; j++) { float b = Mathf.PI * 2 * j / seg; v.Add(new Vector3(Mathf.Cos(b) * 0.5f, -0.5f, Mathf.Sin(b) * 0.5f)); uv.Add(new Vector2(0.5f, 0.5f)); }
            for (int j = 0; j < seg; j++) { t.Add(c); t.Add(st + j); t.Add(st + j + 1); }
            return Build(v, uv, t, "Cone");
        }

        /// <summary>朝上的圓盤（y=0），uv 以 tile 為一個貼圖週期。</summary>
        public static Mesh Disc(float radius, int seg, float tile)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            v.Add(Vector3.zero); uv.Add(Vector2.zero);
            for (int j = 0; j <= seg; j++)
            {
                float b = Mathf.PI * 2 * j / seg, x = Mathf.Cos(b) * radius, z = Mathf.Sin(b) * radius;
                v.Add(new Vector3(x, 0, z)); uv.Add(new Vector2(x / tile, z / tile));
            }
            for (int j = 0; j < seg; j++) { t.Add(0); t.Add(j + 2); t.Add(j + 1); }
            return Build(v, uv, t, "Disc");
        }

        /// <summary>水平放置的圓環（大半徑 R、管半徑 r）。</summary>
        public static Mesh Torus(float R, float r, int segR = 72, int segr = 8)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= segR; i++)
            {
                float a = Mathf.PI * 2 * i / segR, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int j = 0; j <= segr; j++)
                {
                    float b = Mathf.PI * 2 * j / segr, cb = Mathf.Cos(b), sb = Mathf.Sin(b);
                    v.Add(new Vector3((R + r * cb) * ca, r * sb, (R + r * cb) * sa)); uv.Add(new Vector2((float)i / segR, (float)j / segr));
                }
            }
            for (int i = 0; i < segR; i++)
                for (int j = 0; j < segr; j++)
                {
                    int a = i * (segr + 1) + j, b = a + segr + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(a + 1); t.Add(b + 1); t.Add(b);
                }
            return Build(v, uv, t, "Torus");
        }

        /// <summary>五角星（站立在 XY 平面，朝 -Z 的面為正面；厚度 0.3，外徑 0.5）。</summary>
        public static Mesh Star()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            int n = 10; float h = 0.15f;
            var ring = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI / 2 + Mathf.PI * 2 * i / n, rr = i % 2 == 0 ? 0.5f : 0.22f;
                ring[i] = new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr);
            }
            // 前後兩面（各自獨立頂點，法線才會平整）
            int front = v.Count; v.Add(new Vector3(0, 0, -h)); uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < n; i++) { v.Add(new Vector3(ring[i].x, ring[i].y, -h)); uv.Add(new Vector2(0.5f, 0.5f)); }
            for (int i = 0; i < n; i++) { t.Add(front); t.Add(front + 1 + (i + 1) % n); t.Add(front + 1 + i); }
            int back = v.Count; v.Add(new Vector3(0, 0, h)); uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < n; i++) { v.Add(new Vector3(ring[i].x, ring[i].y, h)); uv.Add(new Vector2(0.5f, 0.5f)); }
            for (int i = 0; i < n; i++) { t.Add(back); t.Add(back + 1 + i); t.Add(back + 1 + (i + 1) % n); }
            // 側面
            for (int i = 0; i < n; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % n];
                int s = v.Count;
                v.Add(new Vector3(a.x, a.y, -h)); v.Add(new Vector3(b.x, b.y, -h)); v.Add(new Vector3(a.x, a.y, h)); v.Add(new Vector3(b.x, b.y, h));
                for (int k = 0; k < 4; k++) uv.Add(new Vector2(0.5f, 0.5f));
                t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s + 1); t.Add(s + 2); t.Add(s + 3);
            }
            return Build(v, uv, t, "Star");
        }

        public static Mesh Combine(List<CombineInstance> parts, string name)
        {
            var m = new Mesh { name = name };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.CombineMeshes(parts.ToArray(), true, true);
            m.RecalculateBounds();
            return m;
        }
    }
}
