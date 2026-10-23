using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Snake3D
{
    /// <summary>
    /// 3D 舞台：圓形圍欄場地、動物蛇、果實、粒子、跟隨鏡頭。全部程式建模。
    /// 世界座標對應：規則核心的 (x, z) → Unity 的 (x, 0, -z)，這樣畫面右 = +x、畫面下 = +z，和網頁版一致。
    /// </summary>
    public sealed class World3D
    {
        const int MAX_BATCH = 1000;

        readonly GameObject root;
        readonly Camera cam;
        readonly Light sun;
        readonly ThemeDef theme;
        readonly double R;
        readonly string quality;
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        readonly Mesh sphere, star, cone;
        readonly Shader shader;

        sealed class SnakeVis
        {
            public int Slot; public GameObject Head; public Transform HeadT; public GameObject Tag; public TextMesh TagFront, TagBack;
            public GameObject Arrow; public string Animal; public bool Mine; public Vector3 LastPos; public bool Alive = true;
            public Material A, B; public Material Tail; public float TailScale;
        }
        readonly Dictionary<int, SnakeVis> snakes = new Dictionary<int, SnakeVis>();

        struct Puff { public Vector3 Pos, Vel; public float Life, Max, Size; public Color Col; }
        readonly List<Puff> puffs = new List<Puff>();

        readonly Matrix4x4[] buf = new Matrix4x4[MAX_BATCH];
        readonly Dictionary<Material, List<Matrix4x4>> groups = new Dictionary<Material, List<Matrix4x4>>();
        readonly Dictionary<Material, Mesh> groupMesh = new Dictionary<Material, Mesh>();

        Vector2 focus; bool hasFocus; float camH = -1; float shake; float clock;
        public Camera Cam { get { return cam; } }
        public float AspectSpan { get; private set; }
        /// <summary>相機實際佔用的像素大小（遊戲主區，不含左側資訊欄）；0 = 用整個螢幕。</summary>
        public float ViewW, ViewH;
        float shiftPx;

        public World3D(Transform parent, ViewState v, string quality, bool mineTag)
        {
            this.quality = quality ?? "normal";
            R = v.R; theme = Data.Theme(v.ThemeId);
            shader = Shader.Find("Snake3D/Lit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            root = new GameObject("World3D");
            root.transform.SetParent(parent, false);
            int q = this.quality == "low" ? 0 : 1;
            sphere = Own(MeshKit.Sphere(q == 0 ? 6 : 10, q == 0 ? 10 : 16));
            star = Own(MeshKit.Star());
            cone = Own(MeshKit.Cone(12));

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(root.transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = theme.Sky;
            cam.fieldOfView = 40f; cam.nearClipPlane = 0.5f; cam.farClipPlane = 300f; cam.depth = 0;
            cam.allowHDR = false; cam.allowMSAA = this.quality != "low";

            var lightGo = new GameObject("Sun");
            lightGo.transform.SetParent(root.transform, false);
            sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = theme.LightColor; sun.intensity = Mathf.Clamp(theme.LightIntensity * 1.05f, 0.5f, 1.5f);
            sun.transform.rotation = Quaternion.Euler(52f, 140f, 0f);
            sun.shadows = this.quality == "low" ? LightShadows.None : LightShadows.Soft;
            sun.shadowStrength = 0.6f; sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.4f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(theme.Hemi0, Color.white, 0.2f) * (theme.Night ? 0.9f : 1.0f);
            RenderSettings.ambientEquatorColor = Color.Lerp(theme.Hemi0, theme.Hemi1, 0.5f) * 0.9f;
            RenderSettings.ambientGroundColor = theme.Hemi1 * 0.8f;
            RenderSettings.fog = false;
            QualitySettings.shadowDistance = 55f;

            BuildStage(v);
            foreach (var p in v.Players) EnsureSnake(p, mineTag);
        }

        T Own<T>(T o) where T : UnityEngine.Object { owned.Add(o); return o; }

        Material Mat(Color c, float emis = 0f)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c) + "_" + emis.ToString("F2");
            Material m;
            if (mats.TryGetValue(key, out m)) return m;
            m = Own(new Material(shader));
            m.color = c;
            if (m.HasProperty("_Emis")) m.SetColor("_Emis", c * emis);
            m.enableInstancing = true;
            mats[key] = m;
            return m;
        }

        static Vector3 W(double x, double z, float y = 0f) { return new Vector3((float)x, y, (float)-z); }

        GameObject Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Quaternion? rot = null, bool shadow = true)
        {
            var go = new GameObject("part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            if (rot.HasValue) go.transform.localRotation = rot.Value;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return go;
        }

        // ---------- 舞台 ----------
        Texture2D FloorTexture()
        {
            const int S = 128;
            var tex = Own(new Texture2D(S, S, TextureFormat.RGBA32, true));
            tex.wrapMode = TextureWrapMode.Repeat; tex.filterMode = FilterMode.Bilinear;
            var px = new Color[S * S];
            var rnd = new System.Random((int)(R * 31) + 7);
            float p1 = (float)rnd.NextDouble() * 6.28f, p2 = (float)rnd.NextDouble() * 6.28f, p3 = (float)rnd.NextDouble() * 6.28f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S * Mathf.PI * 2, w = y / (float)S * Mathf.PI * 2;
                    float n = Mathf.Sin(u * 2 + p1) * Mathf.Sin(w * 2 + p2) * 0.5f + Mathf.Sin(u * 3 + w * 2 + p3) * 0.35f + Mathf.Sin(u * 5 - w * 4 + p1) * 0.15f;
                    float t = Mathf.SmoothStep(0, 1, n * 0.5f + 0.5f);
                    px[y * S + x] = Color.Lerp(theme.FloorA, theme.FloorB, t);
                }
            tex.SetPixels(px); tex.Apply(true, true);
            return tex;
        }

        void BuildStage(ViewState v)
        {
            float r = (float)R;
            // 大地與場地
            var outer = Part(root.transform, Own(MeshKit.Disc(r + 70f, 48, 20f)), Mat(theme.Outer), new Vector3(0, -0.06f, 0), Vector3.one, null, false);
            var floorMat = Own(new Material(shader)); floorMat.color = Color.white;
            if (floorMat.HasProperty("_MainTex")) floorMat.SetTexture("_MainTex", FloorTexture());
            floorMat.enableInstancing = false;
            Part(root.transform, Own(MeshKit.Disc(r + 0.4f, 96, 9f)), floorMat, Vector3.zero, Vector3.one, null, false);

            // 圍欄：柱子與柱帽合併成單一網格（一次繪製），再加兩圈欄杆
            var cyl = Own(MeshKit.Cylinder(10));
            int nPost = Mathf.RoundToInt(Mathf.PI * 2f * (r + 0.3f) / 1.7f);
            var posts = new List<CombineInstance>(); var caps = new List<CombineInstance>();
            for (int i = 0; i < nPost; i++)
            {
                float t = (float)i / nPost * Mathf.PI * 2f, x = Mathf.Cos(t) * (r + 0.35f), z = Mathf.Sin(t) * (r + 0.35f);
                posts.Add(new CombineInstance { mesh = cyl, transform = Matrix4x4.TRS(new Vector3(x, 0.55f, z), Quaternion.identity, new Vector3(0.34f, 1.1f, 0.34f)) });
                caps.Add(new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(new Vector3(x, 1.12f, z), Quaternion.identity, Vector3.one * 0.42f) });
            }
            Part(root.transform, Own(MeshKit.Combine(posts, "Posts")), Mat(theme.Wall), Vector3.zero, Vector3.one);
            Part(root.transform, Own(MeshKit.Combine(caps, "Caps")), Mat(theme.WallTop), Vector3.zero, Vector3.one);
            var rail = Own(MeshKit.Torus(r + 0.35f, 0.07f, 96, 6));
            Part(root.transform, rail, Mat(theme.WallTop), new Vector3(0, 0.45f, 0), Vector3.one, null, false);
            Part(root.transform, rail, Mat(theme.WallTop), new Vector3(0, 0.82f, 0), Vector3.one, null, false);

            // 石頭
            foreach (var k in v.Rocks)
            {
                var g = new GameObject("rock"); g.transform.SetParent(root.transform, false);
                g.transform.position = W(k.X, k.Z, 0);
                float d = (float)k.R * 2f;
                Part(g.transform, sphere, Mat(theme.Rock0), new Vector3(0, d * 0.32f, 0), new Vector3(d, d * 0.72f, d), Quaternion.Euler(0, (float)(k.X * 40), 0));
                Part(g.transform, sphere, Mat(theme.Rock1), new Vector3(d * 0.22f, d * 0.2f, d * 0.18f), new Vector3(d * 0.55f, d * 0.4f, d * 0.5f));
            }

            // 場外裝飾：一圈樹（圓錐＋球）
            var rnd = new System.Random(v.ThemeId * 7919 + (int)R);
            var trunks = new List<CombineInstance>(); var crowns = new List<CombineInstance>();
            int nDeco = this.quality == "low" ? 18 : 30;
            for (int i = 0; i < nDeco; i++)
            {
                float t = (float)i / nDeco * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.15f, dist = r + 3.5f + (float)rnd.NextDouble() * 5f;
                float s = 1.4f + (float)rnd.NextDouble() * 1.2f;
                var pos = new Vector3(Mathf.Cos(t) * dist, 0, Mathf.Sin(t) * dist);
                trunks.Add(new CombineInstance { mesh = cyl, transform = Matrix4x4.TRS(pos + Vector3.up * s * 0.45f, Quaternion.identity, new Vector3(0.35f * s, 0.9f * s, 0.35f * s)) });
                bool pointy = v.ThemeId == 3 || v.ThemeId == 4 || (v.ThemeId == 0 && i % 3 == 0);
                var crown = pointy ? cone : sphere;
                crowns.Add(new CombineInstance { mesh = crown, transform = Matrix4x4.TRS(pos + Vector3.up * s * (pointy ? 1.7f : 1.45f), Quaternion.identity, pointy ? new Vector3(1.5f * s, 2.2f * s, 1.5f * s) : Vector3.one * 1.8f * s) });
            }
            Part(root.transform, Own(MeshKit.Combine(trunks, "Trunks")), Mat(theme.Wall), Vector3.zero, Vector3.one);
            Color crownCol = v.ThemeId == 1 ? Data.Hex("#ff9fc4") : v.ThemeId == 3 ? Data.Hex("#e9f6ff") : v.ThemeId == 4 ? Data.Hex("#2f6b6f") : v.ThemeId == 2 ? Data.Hex("#5fcf9a") : Data.Hex("#5fbf62");
            Part(root.transform, Own(MeshKit.Combine(crowns, "Crowns")), Mat(crownCol), Vector3.zero, Vector3.one);
        }

        // ---------- 蛇（頭、名牌） ----------
        void EnsureSnake(ViewPlayer p, bool mineTag)
        {
            SnakeVis sn;
            if (snakes.TryGetValue(p.Slot, out sn)) return;
            var a = Data.Animal(p.Animal);
            sn = new SnakeVis { Slot = p.Slot, Animal = p.Animal, A = Mat(a.Seg0), B = Mat(a.Seg1), Tail = Mat(a.Tail), TailScale = a.TailScale };
            var head = new GameObject("head:" + p.Slot); head.transform.SetParent(root.transform, false);
            sn.Head = head; sn.HeadT = head.transform;
            var white = Mat(Color.white); var pupil = Mat(Data.Hex("#1d1630"));
            var body = Mat(a.Body); var dark = Mat(a.Dark);
            // 頭（單位：蛇的半徑 = 1，之後整體縮放）
            Part(head.transform, sphere, body, new Vector3(0, 0.05f, 0), new Vector3(2.4f, 2.2f, 2.4f));
            if (a.HasMuzzle) Part(head.transform, sphere, Mat(a.Muzzle), new Vector3(0, -0.28f, 0.85f), new Vector3(1.2f, 0.85f, 1.0f));
            if (a.HasBeak) Part(head.transform, cone, Mat(a.Beak), new Vector3(0, -0.12f, 1.28f), new Vector3(0.7f, 0.7f, 0.5f), Quaternion.Euler(90, 0, 0));
            else if (a.HasNose) Part(head.transform, sphere, Mat(a.Nose), new Vector3(0, -0.02f, 1.18f), new Vector3(0.36f, 0.28f, 0.3f));
            if (a.Patches)
                foreach (float sx in new[] { -1f, 1f }) Part(head.transform, sphere, Mat(a.EarCol), new Vector3(sx * 0.62f, 0.28f, 0.82f), new Vector3(0.6f, 0.78f, 0.3f), Quaternion.Euler(0, sx * 18f, sx * -20f));
            if (a.EyesTop)
            {
                foreach (float sx in new[] { -1f, 1f })
                {
                    Part(head.transform, sphere, body, new Vector3(sx * 0.62f, 0.95f, 0.35f), Vector3.one * 0.95f);
                    Part(head.transform, sphere, white, new Vector3(sx * 0.62f, 1.05f, 0.62f), Vector3.one * 0.62f, null, false);
                    Part(head.transform, sphere, pupil, new Vector3(sx * 0.62f, 1.06f, 0.88f), Vector3.one * 0.32f, null, false);
                }
            }
            else
            {
                foreach (float sx in new[] { -1f, 1f })
                {
                    Part(head.transform, sphere, white, new Vector3(sx * 0.58f, 0.4f, 0.92f), Vector3.one * 0.62f, null, false);
                    Part(head.transform, sphere, pupil, new Vector3(sx * 0.6f, 0.4f, 1.15f), Vector3.one * 0.34f, null, false);
                }
                Color ec = a.HasEarCol ? a.EarCol : a.Body;
                var earMat = Mat(ec);
                foreach (float sx in new[] { -1f, 1f })
                {
                    switch (a.Ear)
                    {
                        case "point":
                            Part(head.transform, cone, earMat, new Vector3(sx * 0.78f, 1.2f, 0.05f), new Vector3(0.8f, 1.0f, 0.5f), Quaternion.Euler(0, 0, sx * -18f));
                            if (a.HasEarIn) Part(head.transform, cone, Mat(a.EarIn), new Vector3(sx * 0.78f, 1.14f, 0.2f), new Vector3(0.5f, 0.7f, 0.3f), Quaternion.Euler(0, 0, sx * -18f), false);
                            break;
                        case "floppy":
                            Part(head.transform, sphere, earMat, new Vector3(sx * 1.2f, 0.35f, -0.05f), new Vector3(0.5f, 1.2f, 0.9f), Quaternion.Euler(0, 0, sx * 14f));
                            break;
                        case "long":
                            Part(head.transform, sphere, earMat, new Vector3(sx * 0.55f, 1.9f, -0.15f), new Vector3(0.62f, 1.9f, 0.5f), Quaternion.Euler(0, 0, sx * -8f));
                            if (a.HasEarIn) Part(head.transform, sphere, Mat(a.EarIn), new Vector3(sx * 0.55f, 1.88f, 0.05f), new Vector3(0.34f, 1.4f, 0.3f), Quaternion.Euler(0, 0, sx * -8f), false);
                            break;
                        case "round":
                            Part(head.transform, sphere, earMat, new Vector3(sx * 0.95f, 0.95f, -0.05f), Vector3.one * 0.82f);
                            if (a.HasEarIn && !a.HasEarCol) Part(head.transform, sphere, Mat(a.EarIn), new Vector3(sx * 0.95f, 0.95f, 0.18f), Vector3.one * 0.46f, null, false);
                            break;
                    }
                }
            }
            sn.LastPos = Vector3.zero;

            // 名牌（永遠面向鏡頭）
            var tag = new GameObject("tag:" + p.Slot); tag.transform.SetParent(root.transform, false);
            sn.Tag = tag;
            sn.TagBack = MakeText(tag.transform, Data.Ink, new Vector3(0.035f, -0.035f, 0.02f));
            sn.TagFront = MakeText(tag.transform, Data.SlotColor(p.Slot), Vector3.zero);
            SetTagText(sn, TagName(p, mineTag));
            // 自己頭上的小箭頭
            var arrow = new GameObject("arrow"); arrow.transform.SetParent(root.transform, false);
            Part(arrow.transform, cone, Mat(Data.SlotColor(p.Slot), 0.15f), Vector3.zero, new Vector3(0.7f, 0.9f, 0.7f), Quaternion.Euler(180, 0, 0), false);
            arrow.SetActive(false);
            sn.Arrow = arrow;
            snakes[p.Slot] = sn;
        }

        TextMesh MakeText(Transform parent, Color c, Vector3 off)
        {
            var go = new GameObject("t"); go.transform.SetParent(parent, false); go.transform.localPosition = off;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Ui.F; tm.fontSize = 64; tm.characterSize = 0.075f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = c; tm.fontStyle = FontStyle.Bold;
            var mr = go.GetComponent<MeshRenderer>();
            if (Ui.F != null) mr.sharedMaterial = Ui.F.material;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            return tm;
        }

        void SetTagText(SnakeVis sn, string s) { sn.TagFront.text = s; sn.TagBack.text = s; }

        public Func<ViewPlayer, string> NameOf;
        string TagName(ViewPlayer p, bool mine)
        {
            string n = NameOf != null ? NameOf(p) : p.Name;
            return n + (mine ? Loc.T("res.youTag") : "");
        }

        int selfSlotCached = -2;
        /// <summary>語言切換時重畫名牌。</summary>
        public void Relabel(ViewState v, int selfSlot)
        {
            foreach (var p in v.Players)
            {
                SnakeVis sn;
                if (snakes.TryGetValue(p.Slot, out sn)) SetTagText(sn, TagName(p, p.Slot == selfSlot));
            }
        }

        // ---------- 每幀繪製 ----------
        public void Draw(ViewState v, int selfSlot, double alpha, float dt, string zoomMode, int watchSlot, float safeTopPx, float safeBottomPx)
        {
            clock += dt;
            if (selfSlotCached != selfSlot)
            {
                selfSlotCached = selfSlot;
                foreach (var p in v.Players) { SnakeVis s0; if (snakes.TryGetValue(p.Slot, out s0)) { s0.Mine = p.Slot == selfSlot; SetTagText(s0, TagName(p, s0.Mine)); } }
            }
            foreach (var g in groups.Values) g.Clear();

            ViewPlayer me = v.BySlot(selfSlot), watch = v.BySlot(watchSlot);
            ViewPlayer target = me != null && me.Alive ? me : (watch != null && watch.Alive ? watch : null);
            shiftPx = (safeBottomPx - safeTopPx) * 0.5f;
            UpdateCamera(v, target, dt, zoomMode);

            float a = (float)Mathf.Clamp01((float)alpha);
            foreach (var p in v.Players)
            {
                SnakeVis sn; if (!snakes.TryGetValue(p.Slot, out sn)) continue;
                bool show = p.Alive && p.Pts != null && p.Pts.Length >= 2;
                sn.Head.SetActive(show); sn.Tag.SetActive(show);
                sn.Arrow.SetActive(show && p.Slot == selfSlot);
                if (!show) continue;
                DrawSnake(p, sn, a);
            }
            DrawFoods(v);
            UpdatePuffs(dt);
            Flush();
        }

        static float Lerp(double a, double b, float t) { return (float)(a + (b - a) * t); }

        Vector3 SegPos(double[] pts, double[] prev, int i, float a, float y)
        {
            double x = pts[i * 2], z = pts[i * 2 + 1];
            if (prev != null && prev.Length > i * 2 + 1) { x = prev[i * 2] + (x - prev[i * 2]) * a; z = prev[i * 2 + 1] + (z - prev[i * 2 + 1]) * a; }
            return W(x, z, y);
        }

        void DrawSnake(ViewPlayer p, SnakeVis sn, float a)
        {
            int n = p.Pts.Length / 2;
            float r = (float)p.R;
            var prev = p.PrevPts;
            // 頭
            Vector3 hp = SegPos(p.Pts, prev, 0, a, r * 1.05f);
            double ang = p.PrevAng != p.Ang ? p.PrevAng + Rules.WrapAng(p.Ang - p.PrevAng) * a : p.Ang;
            Vector3 fwd = new Vector3(Mathf.Cos((float)ang), 0, -Mathf.Sin((float)ang));
            sn.HeadT.position = hp; sn.HeadT.rotation = Quaternion.LookRotation(fwd, Vector3.up); sn.HeadT.localScale = Vector3.one * r;
            sn.LastPos = hp;
            // 身體：兩種顏色交替，用 GPU Instancing 一次畫
            var la = Group(sn.A, sphere); var lb = Group(sn.B, sphere);
            int taperN = Mathf.Min(8, Mathf.Max(1, n - 1));
            for (int i = 1; i < n; i++)
            {
                float k = Mathf.Clamp01((float)(n - 1 - i) / taperN);
                bool isTail = i == n - 1;
                float s = r * 2f * Mathf.Lerp(0.5f, 1f, k);
                if (isTail) s = r * 2f * 0.5f * sn.TailScale;
                var pos = SegPos(p.Pts, prev, i, a, s * 0.5f * 0.95f);
                var m = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(s, s * 0.95f, s));
                if (isTail) Group(sn.Tail, sphere).Add(m);
                else ((i & 1) == 0 ? la : lb).Add(m);
            }
            // 加速時尾巴冒小煙
            if (p.Boosting && n > 2 && UnityEngine.Random.value < 0.5f)
                AddPuff(SegPos(p.Pts, prev, Mathf.Min(n - 1, 3), a, r), Data.SlotColor(p.Slot), 0.5f, 1, 0.3f);
            // 名牌與箭頭
            float lift = r * 2.6f + 0.55f;
            sn.Tag.transform.position = hp + Vector3.up * lift;
            sn.Tag.transform.rotation = cam.transform.rotation;
            if (sn.Arrow.activeSelf)
            {
                sn.Arrow.transform.position = hp + Vector3.up * (lift + 0.95f + Mathf.Sin(clock * 6f) * 0.15f);
            }
        }

        List<Matrix4x4> Group(Material m, Mesh mesh)
        {
            List<Matrix4x4> l;
            if (!groups.TryGetValue(m, out l)) { l = new List<Matrix4x4>(); groups[m] = l; groupMesh[m] = mesh; }
            return l;
        }

        void DrawFoods(ViewState v)
        {
            var leaf = Mat(Data.FruitLeaf);
            foreach (var f in v.Foods)
            {
                float bob = Mathf.Sin(clock * 3f + f.Id * 1.7f) * 0.07f;
                if (f.Type == 0)
                {
                    int vi = Mathf.Clamp(f.V, 0, 2);
                    float d = 0.84f;
                    Group(Mat(Data.FruitBody[vi]), sphere).Add(Matrix4x4.TRS(W(f.X, f.Z, 0.42f + bob), Quaternion.identity, new Vector3(d, d * 0.92f, d)));
                    Group(leaf, sphere).Add(Matrix4x4.TRS(W(f.X, f.Z, 0.42f + bob) + new Vector3(0.1f, 0.4f, 0), Quaternion.Euler(0, 0, -25), new Vector3(0.34f, 0.12f, 0.2f)));
                }
                else if (f.Type == 1)
                {
                    if (f.Ttl > 0 && f.Ttl < 4 && ((int)(clock * 8f) & 1) == 0) continue;   // 快消失時閃爍
                    float spin = clock * 120f + f.Id * 40f;
                    Group(Mat(Data.Hex("#ffd23d"), 0.45f), star).Add(Matrix4x4.TRS(W(f.X, f.Z, 0.75f + bob * 1.5f), Quaternion.Euler(0, spin, 0), Vector3.one * 1.25f));
                }
                else
                {
                    if (f.Ttl > 0 && f.Ttl < 5 && ((int)(clock * 8f) & 1) == 0) continue;
                    float d = 0.62f + Mathf.Sin(clock * 5f + f.Id) * 0.05f;
                    Group(Mat(Data.SlotColor(f.V), 0.55f), sphere).Add(Matrix4x4.TRS(W(f.X, f.Z, 0.38f + bob), Quaternion.identity, Vector3.one * d));
                }
            }
        }

        // ---------- 粒子 ----------
        void AddPuff(Vector3 pos, Color c, float size, int count, float speed)
        {
            if (puffs.Count > 300) return;
            for (int i = 0; i < count; i++)
            {
                var dir = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0.3f, 1.4f), UnityEngine.Random.Range(-1f, 1f));
                puffs.Add(new Puff { Pos = pos, Vel = dir.normalized * speed * UnityEngine.Random.Range(2f, 6f), Life = 0, Max = UnityEngine.Random.Range(0.35f, 0.7f), Size = size * UnityEngine.Random.Range(0.6f, 1f), Col = c });
            }
        }

        void UpdatePuffs(float dt)
        {
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var p = puffs[i];
                p.Life += dt; p.Pos += p.Vel * dt; p.Vel.y -= 9f * dt; if (p.Pos.y < 0.05f) { p.Pos.y = 0.05f; p.Vel *= 0.4f; }
                if (p.Life >= p.Max) { puffs.RemoveAt(i); continue; }
                puffs[i] = p;
                float k = 1f - p.Life / p.Max;
                Group(Mat(p.Col, 0.3f), sphere).Add(Matrix4x4.TRS(p.Pos, Quaternion.identity, Vector3.one * p.Size * (0.4f + 0.6f * k)));
            }
        }

        public void Consume(List<GameEvent> events, int selfSlot, bool reduceMotion)
        {
            if (events == null) return;
            foreach (var e in events)
            {
                if (e.T == "eat")
                {
                    Color c = e.Type == 0 ? Data.FruitBody[Mathf.Clamp(e.V, 0, 2)] : e.Type == 1 ? Data.Hex("#ffd23d") : Data.SlotColor(e.V);
                    AddPuff(W(e.X, e.Z, 0.5f), c, 0.28f, reduceMotion ? 3 : 8, 0.55f);
                }
                else if (e.T == "die")
                {
                    AddPuff(W(e.X, e.Z, 0.8f), Data.SlotColor(e.Slot), 0.5f, reduceMotion ? 6 : 24, 1.2f);
                    if (e.Slot == selfSlot && !reduceMotion) shake = 0.6f;
                }
            }
        }

        // ---------- 繪製批次 ----------
        void Flush()
        {
            foreach (var kv in groups)
            {
                var list = kv.Value; if (list.Count == 0) continue;
                var mesh = groupMesh[kv.Key];
                for (int off = 0; off < list.Count; off += MAX_BATCH)
                {
                    int cnt = Mathf.Min(MAX_BATCH, list.Count - off);
                    list.CopyTo(off, buf, 0, cnt);
                    Graphics.DrawMeshInstanced(mesh, 0, kv.Key, buf, cnt, null, ShadowCastingMode.On, true, 0, cam);
                }
            }
        }

        // ---------- 鏡頭 ----------
        static float ZoomFactor(string mode) { return mode == "near" ? 0.8f : mode == "far" ? 1.3f : 1f; }

        void UpdateCamera(ViewState v, ViewPlayer tgt, float dt, string zoomMode)
        {
            float sw = Mathf.Max(80, ViewW > 0 ? ViewW : Screen.width), sh = Mathf.Max(80, ViewH > 0 ? ViewH : Screen.height);
            float zoom = ZoomFactor(zoomMode);
            float fx = 0, fz = 0, span;
            if (tgt != null)
            {
                fx = (float)(tgt.X + Math.Cos(tgt.Ang) * 1.6); fz = (float)(tgt.Z + Math.Sin(tgt.Ang) * 1.6);
                span = (15f + (float)(tgt.R - Rules.BASE_R) * 16f) * zoom;
                if (v.Phase == "countdown") span *= 1.15f;
            }
            else span = (float)R * 2.25f;
            if (!hasFocus) { focus = new Vector2(fx, fz); hasFocus = true; }
            float k = 1f - Mathf.Exp(-dt * 7f);
            focus += (new Vector2(fx, fz) - focus) * k;
            if (camH < 0) camH = span;
            camH += (span - camH) * (1f - Mathf.Exp(-dt * 2.5f));
            cam.aspect = sw / sh;
            float vSpan = sw >= sh ? camH : camH * (sh / sw);
            AspectSpan = vSpan;
            float el = 52f * Mathf.Deg2Rad;
            float dist = (vSpan / 2f) / Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2f) * 1.02f;
            float ox = 0, oz = 0;
            if (shake > 0.001f) { ox = (UnityEngine.Random.value - 0.5f) * shake; oz = (UnityEngine.Random.value - 0.5f) * shake; shake *= Mathf.Exp(-dt * 9f); }
            var focus3 = W(focus.x, focus.y, 0);
            // 把蛇頭放在「扣掉上方 HUD、下方操作區」之後剩下的可視帶中央
            focus3.z -= shiftPx / sh * vSpan / Mathf.Sin(el);
            cam.transform.position = new Vector3(focus3.x + ox, Mathf.Sin(el) * dist, focus3.z - Mathf.Cos(el) * dist + oz);
            cam.transform.LookAt(focus3, Vector3.up);
            // 太陽跟著焦點，影子才會一直清楚
            sun.transform.position = focus3 + new Vector3(-8, 18, -10);
        }

        public void Dispose()
        {
            foreach (var o in owned) if (o != null) UnityEngine.Object.Destroy(o);
            owned.Clear(); mats.Clear(); groups.Clear(); groupMesh.Clear();
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
