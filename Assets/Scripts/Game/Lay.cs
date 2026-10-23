using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>選單畫面共用的版面小工具：置中的限寬欄、頂端列、雙欄／單欄、固定底部按鈕列、鍵帽、規則面板、角色編輯。</summary>
    public static class Lay
    {
        public static float LogicalW { get { return Screen.width / Mathf.Max(0.1f, App.I.UiScale); } }
        public static float LogicalH { get { return Screen.height / Mathf.Max(0.1f, App.I.UiScale); } }
        /// <summary>寬螢幕（桌機、平板橫向）用雙欄，窄螢幕（手機、平板直向）堆疊成單欄。</summary>
        public static bool Wide { get { return LogicalW >= 900f; } }

        public sealed class Parts { public ScrollBox Scroll; public RectTransform Col; public RectTransform Cta; }

        /// <summary>整頁：可捲動、置中、限寬；有 ctaH 時底部會有固定的按鈕列（捲動區讓出空間）。</summary>
        public static Parts Page(RectTransform root, float maxW, float ctaH = 0)
        {
            var sc = Ui.Scroll(root, 0, 0);
            Ui.Stretch(sc.Root, 0, 0, 0, ctaH);
            var row = Ui.HBox(sc.Content, 0, 0, TextAnchor.UpperCenter);
            var col = Ui.VBox(row, 14, 16);
            Ui.Size(col, minW: 0, prefW: maxW, flexW: 0);
            var p = new Parts { Scroll = sc, Col = col };
            if (ctaH > 0)
            {
                var bar = Ui.Obj("Cta", root);
                bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0); bar.pivot = new Vector2(0.5f, 0);
                bar.sizeDelta = new Vector2(0, ctaH); bar.anchoredPosition = Vector2.zero;
                var bg = bar.gameObject.AddComponent<Image>(); bg.color = new Color(Ui.Cream.r, Ui.Cream.g, Ui.Cream.b, 0.96f);
                var inner = Ui.HBox(bar, 0, 10, TextAnchor.MiddleCenter);
                Ui.Stretch(inner);
                p.Cta = inner;
            }
            return p;
        }

        public static Button CtaButton(RectTransform cta, string text, string style, Action on)
        {
            var b = Ui.Btn(cta, "▶  " + text, () => { Sound.Sfx("click"); on(); }, style, 0, 68, 28);
            Ui.Size((RectTransform)b.transform, minW: 0, prefW: 560, flexW: 0, minH: 68);
            return b;
        }

        /// <summary>頂端列：返回鈕＋標題，右邊留出設定／語言鈕的位置。</summary>
        public static Text TopBar(RectTransform col, string title, Action back, bool reserveRight = true)
        {
            var row = Ui.HBox(col, 10, 0, TextAnchor.MiddleLeft);
            Ui.Size(row, minH: 60);
            if (back != null) Ui.IconBtn(row, "back", () => { Sound.Sfx("click"); back(); }, Loc.T("common.back"), 56);
            var t = Ui.Label(row, title, 34, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleLeft, false);
            Ui.Size(t.rectTransform, flexW: 1, minW: 0);
            if (reserveRight) { var sp = Ui.Obj("Reserve", row); Ui.Size(sp, minW: 130, prefW: 130); }
            return t;
        }

        /// <summary>寬螢幕兩欄，窄螢幕兩欄都指到同一個直向容器（自然堆疊）。</summary>
        public static void Cols(RectTransform col, out RectTransform a, out RectTransform b)
        {
            if (!Wide) { a = col; b = col; return; }
            var row = Ui.HBox(col, 16, 0, TextAnchor.UpperLeft);
            a = Ui.VBox(row, 14, 0); b = Ui.VBox(row, 14, 0);
            Ui.Size(a, minW: 0, flexW: 1); Ui.Size(b, minW: 0, flexW: 1);
        }

        public static RectTransform Section(RectTransform parent, string title)
        {
            var card = Ui.Card(parent, 10, 16);
            if (!string.IsNullOrEmpty(title)) Ui.Label(card, title, 28, Ui.Ink, FontStyle.Bold);
            return card;
        }

        public static RectTransform Keycap(Transform parent, string text, bool wide = false)
        {
            var rt = Ui.Obj("Key", parent);
            var im = rt.gameObject.AddComponent<Image>(); im.sprite = Ui.Round(Color.white, Ui.Ink, 10, 3); im.type = Image.Type.Sliced; im.raycastTarget = false;
            var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.5f); sh.effectDistance = new Vector2(0, -3);
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>(); g.padding = new RectOffset(wide ? 14 : 8, wide ? 14 : 8, 4, 6);
            g.childControlWidth = true; g.childControlHeight = true; g.childAlignment = TextAnchor.MiddleCenter;
            var t = Ui.Label(rt, text, 22, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            Ui.Size(rt, minW: 42, minH: 42);
            return rt;
        }

        /// <summary>一段文字後面接著鍵帽（教學用）：以換行排列的一列。</summary>
        public static RectTransform TextWithKeys(RectTransform parent, string text, params string[] keys)
        {
            var w = Ui.Wrap(parent, 6);
            w.GetComponent<WrapGroup>().childAlignment = TextAnchor.MiddleLeft;
            var t = Ui.Label(w, text, 24, Ui.Ink, FontStyle.Normal, TextAnchor.MiddleLeft, false);
            foreach (var k in keys)
            {
                if (k == "/") { Ui.Label(w, "／", 24, Ui.Muted, FontStyle.Bold, TextAnchor.MiddleCenter, false); continue; }
                Keycap(w, k, k.Length > 1);
            }
            return w;
        }

        public static Text Para(RectTransform parent, string text, bool muted = false, int size = 24)
        {
            return Ui.Label(parent, text, size, muted ? Ui.Muted : Ui.Ink);
        }

        // ---------- 規則面板：單機、線上房間共用 ----------
        public static void RulesPanel(RectTransform parent, string speed, string obstacles, int timeLimit, int theme, bool noTime, Action<string, object> set)
        {
            var f1 = Ui.Field(parent, Loc.T("rule.speed"));
            var speeds = Rules.SPEED_ORDER; var sl = new string[speeds.Length];
            for (int i = 0; i < speeds.Length; i++) sl[i] = Names.Speed(speeds[i]);
            Ui.Seg(f1, sl, Array.IndexOf(speeds, speed), i => set("speed", speeds[i]));

            var f2 = Ui.Field(parent, Loc.T("rule.obs"));
            var obs = Rules.OBSTACLE_ORDER; var ol = new string[obs.Length];
            for (int i = 0; i < obs.Length; i++) ol[i] = Names.Obstacle(obs[i]);
            Ui.Seg(f2, ol, Array.IndexOf(obs, obstacles), i => set("obstacles", obs[i]));

            if (!noTime)
            {
                var f3 = Ui.Field(parent, Loc.T("rule.time"));
                var tl = Rules.TIME_LIMITS; var tn = new string[tl.Length];
                for (int i = 0; i < tl.Length; i++) tn[i] = Names.Time(tl[i]);
                Ui.Seg(f3, tn, Array.IndexOf(tl, timeLimit), i => set("timeLimit", tl[i]));
            }

            var f4 = Ui.Field(parent, Loc.T("rule.theme"));
            var th = new string[Rules.THEME_COUNT + 1];
            th[0] = Names.Theme(-1);
            for (int i = 0; i < Rules.THEME_COUNT; i++) th[i + 1] = Names.Theme(i);
            Ui.Seg(f4, th, theme + 1, i => set("theme", i - 1), 76);
        }

        public static string[] RulesSummary(string speed, string obstacles, int timeLimit, int theme)
        {
            return new[] { Loc.T("sum.speed", "v", Names.Speed(speed)), Loc.T("sum.obs", "v", Names.Obstacle(obstacles)), Names.Time(timeLimit), Names.Theme(theme) };
        }

        // ---------- 暱稱與角色 ----------
        public static void ProfileEditor(RectTransform parent, Action onChange)
        {
            var f1 = Ui.Field(parent, Loc.T("prof.nick"));
            Ui.TextInput(f1, Store.D.nickname, Loc.T("prof.placeholder", "n", 16), 16, v => { Store.D.nickname = v; Store.Save(); if (onChange != null) onChange(); });
            var f2 = Ui.Field(parent, Loc.T("prof.animal"));
            var wrap = Ui.Wrap(f2, 8);
            var btns = new List<Button>();
            for (int i = 0; i < Data.AnimalIds.Length; i++)
            {
                string id = Data.AnimalIds[i];
                var b = Ui.Btn(wrap, "", null, id == Store.D.animal ? "sel" : "plain", 0, 104, 18);
                var bt = (RectTransform)b.transform;
                // 把預設文字換成「臉＋名字」直向排列
                for (int k = bt.childCount - 1; k >= 0; k--) UnityEngine.Object.Destroy(bt.GetChild(k).gameObject);
                var hg = bt.GetComponent<HorizontalLayoutGroup>(); UnityEngine.Object.Destroy(hg);
                var vg = bt.gameObject.AddComponent<VerticalLayoutGroup>(); vg.padding = new RectOffset(6, 6, 6, 4); vg.spacing = 2; vg.childAlignment = TextAnchor.MiddleCenter;
                vg.childControlWidth = true; vg.childControlHeight = true; vg.childForceExpandWidth = false; vg.childForceExpandHeight = false;
                var face = Ui.AnimalFace(bt, id, 56);
                var nm = Ui.Label(bt, Names.Animal(id), 18, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleCenter, false);
                Ui.Size(bt, minW: 88, minH: 104, prefW: 88);
                btns.Add(b);
                int idx = i;
                b.onClick.AddListener(() =>
                {
                    Sound.Sfx("click");
                    Store.D.animal = Data.AnimalIds[idx]; Store.Save();
                    for (int k = 0; k < btns.Count; k++) Ui.SetBtnStyle(btns[k], k == idx ? "sel" : "plain");
                    if (onChange != null) onChange();
                });
            }
        }

        public static string[] PickAnimals(int n, string exclude)
        {
            var pool = new List<string>();
            foreach (var a in Data.AnimalIds) if (a != exclude) pool.Add(a);
            for (int i = pool.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); var t = pool[i]; pool[i] = pool[j]; pool[j] = t; }
            return pool.GetRange(0, Math.Min(n, pool.Count)).ToArray();
        }
    }
}
