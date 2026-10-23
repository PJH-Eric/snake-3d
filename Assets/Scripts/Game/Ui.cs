using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>會自動換行的排列容器：子物件依序往右排，放不下就換下一行（chips、選項列都用它，避免擠出畫面）。</summary>
    public class WrapGroup : LayoutGroup
    {
        public float spacingX = 8, spacingY = 8;
        public bool centerRows;

        struct Slot { public float x, y, w, h; }
        Slot[] slots = new Slot[0];

        float Flow(float width)
        {
            int n = rectChildren.Count;
            if (slots.Length != n) slots = new Slot[n];
            float x = 0, y = 0, rowH = 0; int rowStart = 0;
            for (int i = 0; i < n; i++)
            {
                var c = rectChildren[i];
                float w = Mathf.Min(LayoutUtility.GetPreferredSize(c, 0), Mathf.Max(1, width));
                float h = Mathf.Max(LayoutUtility.GetPreferredSize(c, 1), LayoutUtility.GetMinSize(c, 1));
                if (x > 0 && x + w > width + 0.01f)
                {
                    CenterRow(rowStart, i, x - spacingX, width);
                    y += rowH + spacingY; x = 0; rowH = 0; rowStart = i;
                }
                slots[i] = new Slot { x = x, y = y, w = w, h = h };
                x += w + spacingX; rowH = Mathf.Max(rowH, h);
            }
            CenterRow(rowStart, n, x - spacingX, width);
            return n == 0 ? 0 : y + rowH;
        }

        void CenterRow(int from, int to, float used, float width)
        {
            if (!centerRows || to <= from) return;
            float off = Mathf.Max(0, (width - used) * 0.5f);
            for (int i = from; i < to; i++) slots[i].x += off;
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            float minW = 0, prefW = 0;
            foreach (var c in rectChildren) { minW = Mathf.Max(minW, LayoutUtility.GetMinSize(c, 0)); prefW += LayoutUtility.GetPreferredSize(c, 0) + spacingX; }
            SetLayoutInputForAxis(minW + padding.horizontal, Mathf.Max(minW, prefW - spacingX) + padding.horizontal, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float h = Flow(Mathf.Max(1, rectTransform.rect.width - padding.horizontal)) + padding.vertical;
            SetLayoutInputForAxis(h, h, -1, 1);
        }

        public override void SetLayoutHorizontal()
        {
            Flow(Mathf.Max(1, rectTransform.rect.width - padding.horizontal));
            for (int i = 0; i < rectChildren.Count; i++) SetChildAlongAxis(rectChildren[i], 0, padding.left + slots[i].x, slots[i].w);
        }

        public override void SetLayoutVertical()
        {
            Flow(Mathf.Max(1, rectTransform.rect.width - padding.horizontal));
            for (int i = 0; i < rectChildren.Count; i++) SetChildAlongAxis(rectChildren[i], 1, padding.top + slots[i].y, slots[i].h);
        }
    }

    /// <summary>點下去有「按下去」手感的小動畫（縮一點點）。</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float amount = 0.96f;
        public void OnPointerDown(PointerEventData e) { transform.localScale = new Vector3(amount, amount, 1); }
        public void OnPointerUp(PointerEventData e) { transform.localScale = Vector3.one; }
        public void OnPointerExit(PointerEventData e) { transform.localScale = Vector3.one; }
        void OnDisable() { transform.localScale = Vector3.one; }
    }

    public sealed class ScrollBox
    {
        public RectTransform Root, Content;
        public ScrollRect Rect;
    }

    /// <summary>自訂 UI 元件工廠：木牌、奶油紙張、圓角按鈕、開關、滑桿、選項列、捲動區、彈窗。全部程式產生，沒有任何預設控制項外觀。</summary>
    public static class Ui
    {
        public static readonly Color Ink = Data.Ink;
        public static readonly Color Cream = Data.Hex("#fff7e3");
        public static readonly Color Paper = Data.Hex("#fffdf4");
        public static readonly Color Muted = Data.Hex("#8a7258");
        public static readonly Color Leaf = Data.Hex("#5fcf72");
        public static readonly Color Dim = new Color(0.16f, 0.1f, 0.05f, 0.55f);

        static Font font;
        public static Font F
        {
            get
            {
                if (font == null)
                {
                    font = Resources.Load<Font>("UIFont");
                    if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return font;
            }
        }

        // ---------- 基本物件 ----------
        public static RectTransform Obj(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        public static void Stretch(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        public static LayoutElement Size(RectTransform rt, float minW = -1, float minH = -1, float prefW = -1, float prefH = -1, float flexW = -1, float flexH = -1)
        {
            var le = rt.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = minW; le.minHeight = minH; le.preferredWidth = prefW; le.preferredHeight = prefH; le.flexibleWidth = flexW; le.flexibleHeight = flexH;
            return le;
        }

        // ---------- 圓角底圖（程式畫出來的 9 切片） ----------
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static Sprite Round(Color fill, Color outline, int radius = 14, int border = 3)
        {
            string key = "r" + ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(outline) + radius + "_" + border;
            Sprite sp;
            if (sprites.TryGetValue(key, out sp)) return sp;
            int s = radius * 2 + 6;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            var px = new Color[s * s];
            float half = s / 2f - 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // 圓角矩形的有號距離（內部為負）
                    float qx = Mathf.Abs(x - half) - (half - radius), qy = Mathf.Abs(y - half) - (half - radius);
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                    float aOuter = Mathf.Clamp01(0.5f - d);
                    float aInner = Mathf.Clamp01(0.5f - (d + border));
                    Color c = Color.Lerp(outline, fill, aInner);
                    c.a = aOuter * Mathf.Lerp(outline.a, fill.a, aInner);
                    px[y * s + x] = c;
                }
            tex.SetPixels(px); tex.Apply(false, true);
            float b = radius + 2;
            sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprites[key] = sp;
            return sp;
        }

        public static Sprite Circle(Color fill, Color outline, int px = 64, int border = 0)
        {
            string key = "c" + ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(outline) + px + "_" + border;
            Sprite sp;
            if (sprites.TryGetValue(key, out sp)) return sp;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            var pix = new Color[px * px];
            float c0 = px / 2f - 0.5f, r = px / 2f - 1f;
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    float d = Mathf.Sqrt((x - c0) * (x - c0) + (y - c0) * (y - c0)) - r;
                    float aOuter = Mathf.Clamp01(0.5f - d);
                    float aInner = border > 0 ? Mathf.Clamp01(0.5f - (d + border)) : 1f;
                    Color col = border > 0 ? Color.Lerp(outline, fill, aInner) : fill;
                    col.a = aOuter * (border > 0 ? Mathf.Lerp(outline.a, fill.a, aInner) : fill.a);
                    pix[y * px + x] = col;
                }
            tex.SetPixels(pix); tex.Apply(false, true);
            sp = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 100f);
            sprites[key] = sp;
            return sp;
        }

        public static Image Img(Transform parent, Sprite sprite, Color color, bool sliced = true)
        {
            var rt = Obj("Image", parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite; im.color = color;
            im.type = sprite != null && sliced && sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            im.raycastTarget = false;
            return im;
        }

        // ---------- 文字 ----------
        public static Text Label(Transform parent, string text, int size = 22, Color? color = null, FontStyle style = FontStyle.Normal,
            TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = true)
        {
            var rt = Obj("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = F; t.text = text; t.fontSize = size; t.color = color ?? Ink; t.fontStyle = style; t.alignment = anchor;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false; t.raycastTarget = false;
            t.lineSpacing = 1.1f;
            return t;
        }

        // ---------- 版面容器 ----------
        public static RectTransform VBox(Transform parent, float spacing = 10, int pad = 0, TextAnchor align = TextAnchor.UpperLeft, bool fit = false)
        {
            var rt = Obj("VBox", parent);
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing; g.padding = new RectOffset(pad, pad, pad, pad); g.childAlignment = align;
            g.childControlWidth = true; g.childControlHeight = true; g.childForceExpandWidth = true; g.childForceExpandHeight = false;
            if (fit) { var f = rt.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
            return rt;
        }

        public static RectTransform HBox(Transform parent, float spacing = 10, int pad = 0, TextAnchor align = TextAnchor.MiddleLeft, bool fit = false)
        {
            var rt = Obj("HBox", parent);
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing; g.padding = new RectOffset(pad, pad, pad, pad); g.childAlignment = align;
            g.childControlWidth = true; g.childControlHeight = true; g.childForceExpandWidth = false; g.childForceExpandHeight = false;
            if (fit) { var f = rt.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
            return rt;
        }

        public static RectTransform Wrap(Transform parent, float spacing = 8, TextAnchor align = TextAnchor.UpperLeft, bool center = false)
        {
            var rt = Obj("Wrap", parent);
            var g = rt.gameObject.AddComponent<WrapGroup>();
            g.spacingX = spacing; g.spacingY = spacing; g.centerRows = center; g.childAlignment = align;
            return rt;
        }

        /// <summary>奶油紙張卡片：內部是直向排列。</summary>
        public static RectTransform Card(Transform parent, float spacing = 10, int pad = 16, Color? fill = null, int radius = 18)
        {
            var rt = VBox(parent, spacing, pad);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Round(fill ?? Paper, Ink, radius, 3); bg.type = Image.Type.Sliced; bg.raycastTarget = false;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.22f); sh.effectDistance = new Vector2(0, -4);
            return rt;
        }

        public static RectTransform Spacer(Transform parent, float h)
        {
            var rt = Obj("Spacer", parent);
            Size(rt, minH: h, prefH: h);
            return rt;
        }

        // ---------- 按鈕 ----------
        public static Color StyleColor(string style)
        {
            switch (style)
            {
                case "primary": return Data.Hex("#7ddf8a");
                case "orange": return Data.Hex("#ffb259");
                case "sky": return Data.Hex("#8fd8ff");
                case "pink": return Data.Hex("#ff9fc0");
                case "danger": return Data.Hex("#ff8a80");
                case "off": return Data.Hex("#e6dccb");
                case "sel": return Data.Hex("#ffe27a");
                case "wood": return Data.Hex("#e3b878");
                default: return Data.Hex("#fff3d6");
            }
        }

        public static Button Btn(Transform parent, string text, UnityAction onClick, string style = "plain", float minW = 0, float h = 56, int size = 24)
        {
            var rt = Obj("Button", parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = Round(StyleColor(style), Ink, 16, 3); im.type = Image.Type.Sliced;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = im;
            var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1, 1, 1, 1); cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1);
            cb.selectedColor = new Color(1f, 0.93f, 0.6f, 1f); cb.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.7f); cb.fadeDuration = 0.05f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(onClick);
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.45f); sh.effectDistance = new Vector2(0, -5);
            rt.gameObject.AddComponent<PressScale>();
            var hg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hg.padding = new RectOffset(18, 18, 6, 6); hg.childAlignment = TextAnchor.MiddleCenter;
            hg.childControlWidth = true; hg.childControlHeight = true; hg.childForceExpandWidth = true; hg.childForceExpandHeight = true;
            var tx = Label(rt, text, size, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            Size(tx.rectTransform, flexW: 1);
            Size(rt, minW: minW > 0 ? minW : -1, minH: h);
            return btn;
        }

        public static Text BtnText(Button b) { return b.GetComponentInChildren<Text>(); }

        public static void SetBtnStyle(Button b, string style)
        {
            var im = b.GetComponent<Image>();
            im.sprite = Round(StyleColor(style), Ink, 16, 3);
        }

        public static Button IconBtn(Transform parent, string icon, UnityAction onClick, string aria, float size = 56, string style = "plain")
        {
            var rt = Obj("IconButton", parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = Circle(StyleColor(style), Ink, 96, 4); im.type = Image.Type.Simple;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = im;
            var cb = btn.colors; cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1); cb.selectedColor = new Color(1f, 0.93f, 0.6f, 1f); cb.fadeDuration = 0.05f; btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(onClick);
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.45f); sh.effectDistance = new Vector2(0, -4);
            rt.gameObject.AddComponent<PressScale>();
            var ic = Img(rt, Icons.Get(icon), Ink, false);
            ic.rectTransform.anchorMin = new Vector2(0.2f, 0.2f); ic.rectTransform.anchorMax = new Vector2(0.8f, 0.8f);
            ic.rectTransform.offsetMin = Vector2.zero; ic.rectTransform.offsetMax = Vector2.zero;
            Size(rt, minW: size, minH: size, prefW: size, prefH: size);
            rt.name = "Icon:" + icon + ":" + aria;
            return btn;
        }

        // ---------- 開關、滑桿、選項列 ----------
        public static Button Switch(Transform parent, bool value, Action<bool> onChange)
        {
            var rt = Obj("Switch", parent);
            var track = rt.gameObject.AddComponent<Image>();
            track.type = Image.Type.Sliced;
            var btn = rt.gameObject.AddComponent<Button>(); btn.targetGraphic = track;
            Size(rt, minW: 76, minH: 44, prefW: 76, prefH: 44);
            var knobRt = Obj("Knob", rt);
            var knob = knobRt.gameObject.AddComponent<Image>();
            knob.sprite = Circle(Color.white, Ink, 64, 3); knob.raycastTarget = false;
            knobRt.anchorMin = new Vector2(0, 0.5f); knobRt.anchorMax = new Vector2(0, 0.5f); knobRt.pivot = new Vector2(0.5f, 0.5f);
            knobRt.sizeDelta = new Vector2(34, 34);
            bool v = value;
            Action paint = () =>
            {
                track.sprite = Round(v ? Data.Hex("#7ddf8a") : Data.Hex("#e6dccb"), Ink, 20, 3);
                knobRt.anchoredPosition = new Vector2(v ? 76 - 22 : 22, 0);
            };
            paint();
            btn.onClick.AddListener(() => { v = !v; paint(); if (onChange != null) onChange(v); });
            return btn;
        }

        public static Slider Slide(Transform parent, float value, Action<float> onChange)
        {
            var rt = Obj("Slider", parent);
            Size(rt, minW: 160, minH: 44, prefW: 220, prefH: 44, flexW: 1);
            var bg = Obj("Background", rt);
            var bgi = bg.gameObject.AddComponent<Image>(); bgi.sprite = Round(Data.Hex("#e6dccb"), Ink, 10, 3); bgi.type = Image.Type.Sliced;
            bg.anchorMin = new Vector2(0, 0.5f); bg.anchorMax = new Vector2(1, 0.5f); bg.pivot = new Vector2(0.5f, 0.5f);
            bg.offsetMin = new Vector2(10, -11); bg.offsetMax = new Vector2(-10, 11);
            var area = Obj("FillArea", rt);
            area.anchorMin = new Vector2(0, 0.5f); area.anchorMax = new Vector2(1, 0.5f); area.offsetMin = new Vector2(14, -7); area.offsetMax = new Vector2(-14, 7);
            var fill = Obj("Fill", area);
            var fi = fill.gameObject.AddComponent<Image>(); fi.sprite = Round(Data.Hex("#7ddf8a"), Data.Hex("#7ddf8a"), 7, 0); fi.type = Image.Type.Sliced;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            var hArea = Obj("HandleArea", rt);
            Stretch(hArea, 18, 0, 18, 0);
            var handle = Obj("Handle", hArea);
            var hi = handle.gameObject.AddComponent<Image>(); hi.sprite = Circle(Color.white, Ink, 64, 3);
            handle.sizeDelta = new Vector2(34, 34);
            var s = rt.gameObject.AddComponent<Slider>();
            s.fillRect = fill; s.handleRect = handle; s.targetGraphic = hi; s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0; s.maxValue = 1; s.value = Mathf.Clamp01(value);
            s.onValueChanged.AddListener(v => { if (onChange != null) onChange(v); });
            return s;
        }

        /// <summary>單選的選項列（會自動換行）。</summary>
        public static RectTransform Seg(Transform parent, string[] labels, int selected, Action<int> onChange, float minW = 84)
        {
            var wrap = Wrap(parent, 8);
            var btns = new List<Button>();
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = Btn(wrap, labels[i], null, i == selected ? "sel" : "plain", minW, 52, 22);
                b.onClick.AddListener(() =>
                {
                    for (int k = 0; k < btns.Count; k++) SetBtnStyle(btns[k], k == idx ? "sel" : "plain");
                    if (onChange != null) onChange(idx);
                });
                btns.Add(b);
            }
            return wrap;
        }

        /// <summary>數量調整：減號、數字、加號。</summary>
        public static RectTransform Stepper(Transform parent, int value, int min, int max, Func<int, string> fmt, Action<int> onChange)
        {
            var row = HBox(parent, 10, 0, TextAnchor.MiddleLeft);
            int v = value;
            Text lab = null;
            Button minus = null, plus = null;
            Action refresh = () =>
            {
                lab.text = fmt != null ? fmt(v) : v.ToString();
                minus.interactable = v > min; plus.interactable = v < max;
            };
            minus = IconBtn(row, "minus", () => { v = Mathf.Max(min, v - 1); refresh(); if (onChange != null) onChange(v); }, "-", 52);
            var holder = Obj("Value", row); Size(holder, minW: 96, minH: 52, prefW: 96, prefH: 52);
            lab = Label(holder, "", 26, Ink, FontStyle.Bold, TextAnchor.MiddleCenter, false); Stretch(lab.rectTransform);
            plus = IconBtn(row, "plus", () => { v = Mathf.Min(max, v + 1); refresh(); if (onChange != null) onChange(v); }, "+", 52);
            refresh();
            return row;
        }


        /// <summary>單行文字輸入（自訂外觀）。</summary>
        public static InputField TextInput(Transform parent, string text, string placeholder, int maxLen, Action<string> onChange, Action<string> onEnd = null)
        {
            var rt = Obj("Input", parent);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Round(Color.white, Ink, 14, 3); bg.type = Image.Type.Sliced;
            var inp = rt.gameObject.AddComponent<InputField>();
            inp.targetGraphic = bg;
            var tx = Label(rt, "", 24, Ink, FontStyle.Bold, TextAnchor.MiddleLeft, false);
            Stretch(tx.rectTransform, 14, 4, 14, 4);
            tx.supportRichText = false;
            var ph = Label(rt, placeholder, 24, new Color(Muted.r, Muted.g, Muted.b, 0.7f), FontStyle.Italic, TextAnchor.MiddleLeft, false);
            Stretch(ph.rectTransform, 14, 4, 14, 4);
            inp.textComponent = tx; inp.placeholder = ph;
            inp.lineType = InputField.LineType.SingleLine; inp.characterLimit = maxLen;
            inp.caretWidth = 3; inp.customCaretColor = true; inp.caretColor = Ink; inp.selectionColor = new Color(0.55f, 0.85f, 1f, 0.6f);
            inp.text = text ?? "";
            if (onChange != null) inp.onValueChanged.AddListener(v => onChange(v));
            if (onEnd != null) inp.onEndEdit.AddListener(v => onEnd(v));
            Size(rt, minW: 120, minH: 56, flexW: 1);
            return inp;
        }

        /// <summary>一行「標籤 ＋ 控制項」。標籤在上、控制項在下，窄螢幕也不會擠。</summary>
        public static RectTransform Field(Transform parent, string label, string hint = null)
        {
            var box = VBox(parent, 6);
            var l = Label(box, label, 22, Ink, FontStyle.Bold);
            if (!string.IsNullOrEmpty(hint)) Label(box, hint, 18, Muted);
            return box;
        }

        public static Text Pill(Transform parent, string text, string style = "plain", int size = 20)
        {
            var rt = Obj("Pill", parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = Round(StyleColor(style), Ink, 14, 2); im.type = Image.Type.Sliced; im.raycastTarget = false;
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.padding = new RectOffset(14, 14, 6, 6); g.childControlWidth = true; g.childControlHeight = true; g.childAlignment = TextAnchor.MiddleCenter;
            var t = Label(rt, text, size, Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            return t;
        }

        // ---------- 捲動區（自訂捲軸） ----------
        public static ScrollBox Scroll(Transform parent, float spacing = 14, int pad = 0)
        {
            var root = Obj("Scroll", parent);
            var sr = root.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Elastic; sr.elasticity = 0.08f;
            sr.scrollSensitivity = 40; sr.inertia = true; sr.decelerationRate = 0.12f;
            var view = Obj("Viewport", root);
            Stretch(view, 0, 0, 14, 0);
            view.gameObject.AddComponent<RectMask2D>();
            var content = Obj("Content", view);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            var g = content.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing; g.padding = new RectOffset(pad, pad, pad, pad);
            g.childControlWidth = true; g.childControlHeight = true; g.childForceExpandWidth = true; g.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // 自訂捲軸：細長的木頭滑軌＋圓角滑塊
            var bar = Obj("Scrollbar", root);
            bar.anchorMin = new Vector2(1, 0); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(1, 0.5f);
            bar.sizeDelta = new Vector2(10, 0); bar.offsetMin = new Vector2(-10, 4); bar.offsetMax = new Vector2(0, -4);
            var track = bar.gameObject.AddComponent<Image>(); track.sprite = Round(new Color(Ink.r, Ink.g, Ink.b, 0.12f), new Color(0, 0, 0, 0), 5, 0); track.type = Image.Type.Sliced;
            var slide = Obj("SlidingArea", bar); Stretch(slide);
            var handle = Obj("Handle", slide);
            var hi = handle.gameObject.AddComponent<Image>(); hi.sprite = Round(Data.Hex("#b9803f"), Ink, 5, 2); hi.type = Image.Type.Sliced;
            handle.sizeDelta = new Vector2(10, 40);
            var sb = bar.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = handle; sb.targetGraphic = hi; sb.direction = Scrollbar.Direction.BottomToTop;
            sr.viewport = view; sr.content = content; sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return new ScrollBox { Root = root, Content = content, Rect = sr };
        }

        // ---------- 動物臉（頭像） ----------
        public static RectTransform AnimalFace(Transform parent, string animalId, float size)
        {
            var a = Data.Animal(animalId);
            var root = Obj("Face:" + animalId, parent);
            Size(root, minW: size, minH: size, prefW: size, prefH: size);
            root.sizeDelta = new Vector2(size, size);
            float u = size / 64f;
            Func<float, float, float, float, Sprite, Color, RectTransform> put = (cx, cy, w, h, sp, col) =>
            {
                var im = Img(root, sp, col, false);
                var r = im.rectTransform;
                r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 0); r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(cx * u, cy * u); r.sizeDelta = new Vector2(w * u, h * u);
                return r;
            };
            var ink = Ink;
            Sprite ball = Circle(Color.white, Ink, 96, 5);
            // 耳朵（先畫，被臉蓋住一半）
            if (a.EyesTop)
            {
                put(19, 50, 22, 22, Circle(a.Body, ink, 96, 4), Color.white);
                put(45, 50, 22, 22, Circle(a.Body, ink, 96, 4), Color.white);
            }
            else if (a.Ear != "none")
            {
                Color ec = a.HasEarCol ? a.EarCol : a.Body;
                float ew = a.Ear == "long" ? 14 : 18, eh = a.Ear == "long" ? 30 : (a.Ear == "floppy" ? 26 : 18);
                float ey = a.Ear == "long" ? 54 : (a.Ear == "floppy" ? 42 : 52);
                float ex = a.Ear == "floppy" ? 11 : 18;
                put(32 - ex - (a.Ear == "long" ? 3 : 4), ey, ew, eh, Circle(ec, ink, 96, 4), Color.white);
                put(32 + ex + (a.Ear == "long" ? 3 : 4), ey, ew, eh, Circle(ec, ink, 96, 4), Color.white);
                if (a.HasEarIn && !a.HasEarCol)
                {
                    put(32 - ex - 4, ey - 1, ew * 0.5f, eh * 0.55f, Circle(a.EarIn, a.EarIn, 64, 0), Color.white);
                    put(32 + ex + 4, ey - 1, ew * 0.5f, eh * 0.55f, Circle(a.EarIn, a.EarIn, 64, 0), Color.white);
                }
            }
            // 臉
            put(32, 31, a.EyesTop ? 56 : 52, a.EyesTop ? 46 : 46, Circle(a.Body, ink, 128, 5), Color.white);
            if (a.HasBeak || a.HasMuzzle) put(32, 24, 30, 24, Circle(a.HasBeak ? Color.white : a.Muzzle, a.HasBeak ? Color.white : a.Muzzle, 64, 0), Color.white);
            if (a.Patches)
            {
                put(21, 34, 12, 15, Circle(a.EarCol, a.EarCol, 64, 0), Color.white);
                put(43, 34, 12, 15, Circle(a.EarCol, a.EarCol, 64, 0), Color.white);
            }
            // 眼睛
            var eye = Circle(Data.Hex("#1d1630"), Data.Hex("#1d1630"), 64, 0);
            var glint = Circle(Color.white, Color.white, 32, 0);
            foreach (float ex2 in new[] { 21.5f, 42.5f })
            {
                put(ex2, 34, 7, 9.5f, eye, Color.white);
                put(ex2 - 1.5f, 36.5f, 3, 3, glint, Color.white);
            }
            // 鼻子、嘴、腮紅
            if (a.HasBeak) put(32, 25, 10, 8, Circle(a.Beak, ink, 64, 3), Color.white);
            else if (a.HasNose) put(32, 26, 7, 5.5f, Circle(a.Nose, a.Nose, 64, 0), Color.white);
            if (!a.Cheeks)
            {
                var blush = Circle(Data.Hex("#ff8fb0"), Data.Hex("#ff8fb0"), 32, 0);
                put(14, 25, 8, 5, blush, new Color(1, 1, 1, 0.55f));
                put(50, 25, 8, 5, blush, new Color(1, 1, 1, 0.55f));
            }
            return root;
        }

        // ---------- 彈窗 ----------
        public sealed class ModalHandle
        {
            public RectTransform Mask, Panel, Body;
            public bool Closable;
            public Action OnClose;
            public void Close() { if (Mask != null) UnityEngine.Object.Destroy(Mask.gameObject); Mask = null; if (OnClose != null) OnClose(); }
        }

        /// <summary>置中的彈窗：背景遮罩、標題列（含關閉鈕）、可捲動內容。內容過長時用自訂捲軸，不會超出畫面。</summary>
        public static ModalHandle Modal(Transform canvasRoot, string title, bool closable = true, float width = 560, float maxHeightRatio = 0.9f)
        {
            var mask = Obj("ModalMask", canvasRoot);
            Stretch(mask);
            var dim = mask.gameObject.AddComponent<Image>(); dim.color = Dim;
            var h = new ModalHandle { Mask = mask, Closable = closable };
            if (closable)
            {
                var closeBtn = mask.gameObject.AddComponent<Button>(); closeBtn.targetGraphic = dim;
                closeBtn.transition = Selectable.Transition.None;
                closeBtn.onClick.AddListener(() => h.Close());
            }
            var panel = Obj("Panel", mask);
            h.Panel = panel;
            var pimg = panel.gameObject.AddComponent<Image>(); pimg.sprite = Round(Cream, Ink, 22, 4); pimg.type = Image.Type.Sliced;
            panel.anchorMin = new Vector2(0.5f, 0.5f); panel.anchorMax = new Vector2(0.5f, 0.5f); panel.pivot = new Vector2(0.5f, 0.5f);
            var sizer = panel.gameObject.AddComponent<ModalSizer>(); sizer.width = width; sizer.maxHeightRatio = maxHeightRatio;
            var col = VBox(panel, 10, 16); Stretch(col);
            var head = HBox(col, 10, 0, TextAnchor.MiddleLeft);
            var t = Label(head, title, 30, Ink, FontStyle.Bold, TextAnchor.MiddleLeft); Size(t.rectTransform, flexW: 1);
            if (closable) IconBtn(head, "close", () => h.Close(), "close", 52);
            var sc = Scroll(col, 12, 4);
            Size(sc.Root, minH: 80, flexH: 1, flexW: 1);
            h.Body = sc.Content;
            return h;
        }

        public static ModalHandle Confirm(Transform canvasRoot, string title, string text, string okText, Action onOk, string okStyle = "danger")
        {
            var m = Modal(canvasRoot, title, true, 480);
            Label(m.Body, text, 24, Ink);
            var row = Wrap(m.Body, 10);
            Btn(row, Loc.T("common.cancel"), () => m.Close(), "plain", 130);
            Btn(row, okText, () => { m.Close(); if (onOk != null) onOk(); }, okStyle, 130);
            return m;
        }
    }

    /// <summary>彈窗尺寸：寬度不超過畫面，高度依內容但不超過畫面高度的一定比例。</summary>
    public sealed class ModalSizer : MonoBehaviour
    {
        public float width = 560, maxHeightRatio = 0.9f;
        RectTransform rt, parent;
        void Awake() { rt = (RectTransform)transform; parent = (RectTransform)transform.parent; }
        void LateUpdate()
        {
            if (rt == null || parent == null) return;
            float pw = parent.rect.width, ph = parent.rect.height;
            float w = Mathf.Min(width, pw - 24);
            float maxH = ph * maxHeightRatio;
            // 內容高度 = 標題列 + 捲動區內容 + 內距；捲動區內容超過就以 maxH 為上限
            var col = rt.GetChild(0) as RectTransform;
            float want = 0;
            if (col != null)
            {
                var sr = col.GetComponentInChildren<ScrollRect>();
                float head = 0;
                for (int i = 0; i < col.childCount; i++)
                {
                    var c = col.GetChild(i) as RectTransform;
                    if (c == null || sr == null || c == sr.transform) continue;
                    head += LayoutUtility.GetPreferredSize(c, 1) + 10;
                }
                float body = sr != null && sr.content != null ? LayoutUtility.GetPreferredSize(sr.content, 1) + 8 : 0;
                want = head + body + 32;
            }
            float h = Mathf.Clamp(want, 160, maxH);
            if (Mathf.Abs(rt.sizeDelta.x - w) > 0.5f || Mathf.Abs(rt.sizeDelta.y - h) > 0.5f) rt.sizeDelta = new Vector2(w, h);
        }
    }
}
