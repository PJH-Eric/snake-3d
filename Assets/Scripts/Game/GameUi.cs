using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>觸控搖桿的感應區：只理會手指（pointerId ≥ 0），滑鼠不能操控蛇（桌機一律用鍵盤）。</summary>
    public sealed class TouchPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<PointerEventData> Down, Drag, Up;
        public void OnPointerDown(PointerEventData e) { if (e.pointerId >= 0 && Down != null) Down(e); }
        public void OnDrag(PointerEventData e) { if (e.pointerId >= 0 && Drag != null) Drag(e); }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId >= 0 && Up != null) Up(e); }
    }

    /// <summary>按住才有效的按鈕（加速）。手指移出按鈕範圍或放開都算放開。</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<bool, int> Changed;
        public bool Held { get; private set; }
        int id = int.MinValue;
        public void OnPointerDown(PointerEventData e)
        {
            if (e.pointerId < 0) return;   // 滑鼠不算
            Held = true; id = e.pointerId; if (Changed != null) Changed(true, id);
        }
        public void OnPointerUp(PointerEventData e) { Release(e.pointerId); }
        public void OnPointerExit(PointerEventData e) { Release(e.pointerId); }
        void Release(int pid) { if (!Held || pid != id) return; Held = false; id = int.MinValue; if (Changed != null) Changed(false, pid); }
        void OnDisable() { if (Held) { Held = false; if (Changed != null) Changed(false, id); } }
    }

    public sealed class ChatMsg { public string Role, Name, Text; }

    /// <summary>聊天室元件（房間與對戰側欄共用）：訊息捲動區＋輸入列＋快速回覆。</summary>
    public sealed class ChatBox
    {
        public RectTransform Root;
        ScrollBox log; InputField input; readonly Action<string> send;
        int count;

        public ChatBox(Transform parent, float logHeight, Action<string> send, bool quick = true)
        {
            this.send = send;
            Root = Ui.VBox(parent, 8, 0);
            log = Ui.Scroll(Root, 6, 6);
            Ui.Size(log.Root, minH: 90, prefH: logHeight, flexH: 1);
            var bg = log.Root.gameObject.AddComponent<Image>(); bg.sprite = Ui.Round(Color.white, Ui.Ink, 12, 2); bg.type = Image.Type.Sliced; bg.raycastTarget = false;
            bg.transform.SetAsFirstSibling();
            var row = Ui.HBox(Root, 8, 0, TextAnchor.MiddleLeft);
            input = Ui.TextInput(row, "", Loc.T("chat.ph"), 60, null, v => { if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Submit(); });
            Ui.Btn(row, Loc.T("chat.send"), Submit, "sky", 80, 56, 22);
            if (quick)
            {
                var wrap = Ui.Wrap(Root, 6);
                for (int i = 1; i <= 5; i++)
                {
                    string t = Loc.T("chat.q" + i);
                    Ui.Btn(wrap, t, () => this.send(t), "plain", 0, 44, 20);
                }
            }
        }

        public bool Focused { get { return input != null && input.isFocused; } }

        void Submit()
        {
            string t = (input.text ?? "").Trim();
            if (t.Length == 0) return;
            send(t); input.text = "";
        }

        public void Set(List<ChatMsg> list)
        {
            for (int i = log.Content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(log.Content.GetChild(i).gameObject);
            count = 0;
            foreach (var m in list) Add(m, false);
            ToBottom();
        }

        public void Push(ChatMsg m)
        {
            bool stick = log.Rect.verticalNormalizedPosition <= 0.05f || count < 6;
            Add(m, true);
            while (log.Content.childCount > 80) { UnityEngine.Object.DestroyImmediate(log.Content.GetChild(0).gameObject); }
            if (stick) ToBottom();
        }

        void Add(ChatMsg m, bool anim)
        {
            count++;
            string text;
            Color col = Ui.Ink; var st = FontStyle.Normal;
            if (m.Role == "sys") { text = Loc.Message(m.Text); col = Ui.Muted; st = FontStyle.Italic; }
            else
            {
                string nm = Names.P("human", "cat", m.Name);
                text = nm + (m.Role == "spectator" ? Loc.T("chat.spec") : "") + "：" + m.Text;
            }
            Ui.Label(log.Content, text, 20, col, st);
        }

        void ToBottom() { Canvas.ForceUpdateCanvases(); log.Rect.verticalNormalizedPosition = 0f; }
    }

    /// <summary>小地圖：用程式在一張小貼圖上畫出場地、石頭、金星與每條蛇。</summary>
    public sealed class MiniMap
    {
        public readonly RawImage Image; readonly Texture2D tex; readonly Color32[] px; const int S = 132;
        public MiniMap(Transform parent)
        {
            var rt = Ui.Obj("MiniMap", parent);
            Image = rt.gameObject.AddComponent<RawImage>(); Image.raycastTarget = false;
            tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            px = new Color32[S * S];
            Image.texture = tex;
        }

        public void Destroy() { if (tex != null) UnityEngine.Object.Destroy(tex); }

        void Disc(float cx, float cy, float r, Color32 c)
        {
            int x0 = Mathf.Max(0, (int)(cx - r - 1)), x1 = Mathf.Min(S - 1, (int)(cx + r + 1));
            int y0 = Mathf.Max(0, (int)(cy - r - 1)), y1 = Mathf.Min(S - 1, (int)(cy + r + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a <= 0) continue;
                    ref Color32 p = ref px[y * S + x];
                    float na = a * (c.a / 255f);
                    p.r = (byte)(p.r * (1 - na) + c.r * na); p.g = (byte)(p.g * (1 - na) + c.g * na); p.b = (byte)(p.b * (1 - na) + c.b * na);
                    p.a = (byte)Mathf.Min(255, p.a + na * 255f);
                }
        }

        public void Draw(ViewState v, int selfSlot)
        {
            var th = Data.Theme(v.ThemeId);
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
            float R = S / 2f - 6f, k = R / (float)v.R, c = S / 2f;
            Disc(c, c, R + 3.5f, th.Wall);
            Disc(c, c, R, th.FloorA);
            foreach (var r in v.Rocks) Disc(c + (float)r.X * k, c - (float)r.Z * k, Mathf.Max(1.5f, (float)r.R * k), th.Rock1);
            foreach (var f in v.Foods) if (f.Type == 1) Disc(c + (float)f.X * k, c - (float)f.Z * k, 2.6f, Data.Hex("#ffd23d"));
            foreach (var p in v.Players)
            {
                if (!p.Alive || p.Pts == null || p.Pts.Length < 2) continue;
                bool mine = p.Slot == selfSlot;
                Color32 col = Data.SlotColor(p.Slot);
                for (int i = 0; i + 1 < p.Pts.Length; i += 4) Disc(c + (float)p.Pts[i] * k, c - (float)p.Pts[i + 1] * k, mine ? 1.9f : 1.4f, col);
                Disc(c + (float)p.Pts[0] * k, c - (float)p.Pts[1] * k, mine ? 4.5f : 3.2f, mine ? (Color32)Color.white : col);
                if (mine) Disc(c + (float)p.Pts[0] * k, c - (float)p.Pts[1] * k, 2.2f, Ui.Ink);
            }
            tex.SetPixels32(px); tex.Apply(false);
        }
    }
}
