using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    // ================= 首頁 =================
    public sealed class HomePage : Page
    {
        public override void Build(RectTransform root)
        {
            var p = Lay.Page(root, 640);
            var col = p.Col;

            // 標題：每個字不同顏色
            var title = Ui.Wrap(col, 2, TextAnchor.UpperCenter, true);
            string[] pal = { "#ff7a45", "#3fa7f5", "#ff6f9f", "#4cc26a", "#a58bff", "#ffc233", "#2ec7c0" };
            int ci = 0;
            foreach (char c in Loc.T("app.title"))
            {
                var t = Ui.Label(title, c.ToString(), Loc.Lang == Loc.EN ? 44 : 60, Data.Hex(pal[ci++ % pal.Length]), FontStyle.Bold, TextAnchor.MiddleCenter, false);
                var o = t.gameObject.AddComponent<Outline>(); o.effectColor = Ui.Ink; o.effectDistance = new Vector2(3, -3);
            }
            var sub = Ui.Label(col, Loc.T("home.sub"), 26, Ui.Ink, FontStyle.Bold, TextAnchor.UpperCenter);

            var cast = Ui.Wrap(col, 6, TextAnchor.UpperCenter, true);
            foreach (var id in Data.AnimalIds) Ui.AnimalFace(cast, id, 66);

            Ui.Spacer(col, 8);
            var menu = Ui.VBox(col, 14, 0);
            MenuBtn(menu, Loc.T("menu.challenge"), "primary", () => App.I.Go(new ChallengePage()));
            MenuBtn(menu, Loc.T("menu.solo"), "pink", () => App.I.Go(new SoloPage()));
            MenuBtn(menu, Loc.T("menu.online"), "sky", () => App.I.Go(new LobbyPage()));
            MenuBtn(menu, Loc.T("menu.help"), "orange", () => App.I.Go(new HelpPage()));

            var d = Store.D; var sline = new List<string>();
            int best = Store.Best(d.challenge.speed, d.challenge.obstacles);
            if (best > 0) sline.Add(Loc.T("home.rec.best", "n", best));
            if (d.statSolo.play > 0) sline.Add(Loc.T("home.rec.solo", "w", d.statSolo.win, "p", d.statSolo.play));
            if (d.statOnline.play > 0) sline.Add(Loc.T("home.rec.online", "w", d.statOnline.win, "p", d.statOnline.play));
            if (sline.Count > 0) Ui.Label(col, Loc.T("home.rec", "list", string.Join(Loc.T("home.rec.sep"), sline.ToArray())), 20, Ui.Muted, FontStyle.Normal, TextAnchor.UpperCenter);
            Ui.Spacer(col, 20);
        }

        static void MenuBtn(RectTransform parent, string text, string style, Action on)
        {
            var b = Ui.Btn(parent, text, () => { Sound.Sfx("click"); on(); }, style, 0, 78, 32);
        }
    }

    // ================= 怎麼玩（純文字教學） =================
    public sealed class HelpPage : Page
    {
        public override bool Back() { App.I.Go(new HomePage()); return true; }

        public override void Build(RectTransform root)
        {
            var p = Lay.Page(root, 1000);
            var col = p.Col;
            Lay.TopBar(col, Loc.T("help.title"), () => App.I.Go(new HomePage()));
            RectTransform a, b;
            Lay.Cols(col, out a, out b);

            var s1 = Lay.Section(a, Loc.T("help.goal"));
            Lay.Para(s1, Loc.T("help.goal.p"));
            Lay.Para(s1, Loc.T("help.goal.n"), true, 22);

            var s2 = Lay.Section(b, Loc.T("help.ctl"));
            Lay.Para(s2, Loc.T("help.ctl.touch"));
            Lay.TextWithKeys(s2, Loc.T("help.ctl.mouse"), "W", "A", "S", "D", "/", Loc.T("help.ctl.arrow"));
            Lay.Para(s2, Loc.T("help.ctl.diag"), true, 22);
            Lay.TextWithKeys(s2, Loc.T("help.ctl.boost"), Loc.T("help.ctl.space"), "/", "Shift");
            Lay.Para(s2, Loc.T("help.ctl.energy"), true, 22);

            var s3 = Lay.Section(a, Loc.T("help.fruit"));
            for (int i = 1; i <= 4; i++) Bullet(s3, Loc.T("help.fruit." + i));
            var s4 = Lay.Section(b, Loc.T("help.out"));
            for (int i = 1; i <= 4; i++) Bullet(s4, Loc.T("help.out." + i));

            var s5 = Lay.Section(col, Loc.T("help.modes"));
            foreach (var m in new[] { "challenge", "solo", "online" })
            {
                var row = Ui.Wrap(s5, 0);
                var t = Ui.Label(row, Loc.T("help.mode." + m), 24, Ui.Ink, FontStyle.Bold, TextAnchor.UpperLeft, false);
                Lay.Para(s5, Loc.T("help.mode." + m + ".d"), true, 22);
            }

            var cta = Ui.Wrap(col, 12, TextAnchor.UpperCenter, true);
            Ui.Btn(cta, Loc.T("help.cta"), () => App.I.Go(new ChallengePage()), "primary", 260, 64, 26);
            Ui.Btn(cta, Loc.T("help.home"), () => App.I.Go(new HomePage()), "plain", 200, 64, 26);
            Ui.Spacer(col, 16);
        }

        static void Bullet(RectTransform parent, string text)
        {
            var row = Ui.HBox(parent, 8, 0, TextAnchor.UpperLeft);
            var dot = Ui.Label(row, "•", 24, Ui.Ink, FontStyle.Bold, TextAnchor.UpperCenter, false);
            Ui.Size(dot.rectTransform, minW: 14, prefW: 14);
            var t = Ui.Label(row, text, 24, Ui.Ink);
            Ui.Size(t.rectTransform, flexW: 1, minW: 0);
        }
    }

    // ================= 單人挑戰設定 =================
    public sealed class ChallengePage : Page
    {
        RectTransform bestBox;
        public override bool Back() { App.I.Go(new HomePage()); return true; }

        public override void Build(RectTransform root)
        {
            var ch = Store.D.challenge;
            var p = Lay.Page(root, 1000, 96);
            var col = p.Col;
            Lay.TopBar(col, Loc.T("ch.title"), () => App.I.Go(new HomePage()));
            RectTransform a, b;
            Lay.Cols(col, out a, out b);

            var you = Lay.Section(a, Loc.T("ch.you"));
            Lay.ProfileEditor(you, null);

            var play = Lay.Section(b, Loc.T("ch.play"));
            Lay.Para(play, Loc.T("ch.play.p"), true);
            bestBox = Ui.HBox(play, 10, 0, TextAnchor.MiddleLeft);
            PaintBest();

            var rules = Lay.Section(b, Loc.T("ch.rules"));
            Lay.RulesPanel(rules, ch.speed, ch.obstacles, 0, ch.theme, true, (k, v) =>
            {
                if (k == "speed") ch.speed = (string)v; else if (k == "obstacles") ch.obstacles = (string)v; else if (k == "theme") ch.theme = (int)v;
                Store.Save(); PaintBest();
            });
            Ui.Spacer(col, 8);
            Lay.CtaButton(p.Cta, Loc.T("common.start"), "primary", Flow.StartChallenge);
        }

        void PaintBest()
        {
            for (int i = bestBox.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(bestBox.GetChild(i).gameObject);
            var ch = Store.D.challenge;
            int best = Store.Best(ch.speed, ch.obstacles);
            Ui.Label(bestBox, Loc.T("ch.best"), 24, Ui.Ink, FontStyle.Normal, TextAnchor.MiddleLeft, false);
            Ui.Label(bestBox, best > 0 ? best.ToString() : "—", 34, Data.Hex("#e0801a"), FontStyle.Bold, TextAnchor.MiddleLeft, false);
        }
    }

    // ================= 對戰電腦設定 =================
    public sealed class SoloPage : Page
    {
        RectTransform oppBox; string[] anim;
        public override bool Back() { App.I.Go(new HomePage()); return true; }

        public override void Build(RectTransform root)
        {
            var so = Store.D.solo;
            var p = Lay.Page(root, 1000, 96);
            var col = p.Col;
            Lay.TopBar(col, Loc.T("solo.title"), () => App.I.Go(new HomePage()));
            RectTransform a, b;
            Lay.Cols(col, out a, out b);

            var you = Lay.Section(a, Loc.T("ch.you"));
            Lay.ProfileEditor(you, () => { PaintOpp(); });

            oppBox = Lay.Section(a, "");
            anim = Lay.PickAnimals(7, Store.D.animal);
            PaintOpp();

            var rules = Lay.Section(b, Loc.T("ch.rules"));
            Lay.RulesPanel(rules, so.speed, so.obstacles, so.timeLimit, so.theme, false, (k, v) =>
            {
                if (k == "speed") so.speed = (string)v; else if (k == "obstacles") so.obstacles = (string)v;
                else if (k == "timeLimit") so.timeLimit = (int)v; else if (k == "theme") so.theme = (int)v;
                Store.Save();
            });
            var win = Lay.Section(b, Loc.T("solo.win"));
            Lay.Para(win, Loc.T("solo.win.p"), true);
            Ui.Spacer(col, 8);
            Lay.CtaButton(p.Cta, Loc.T("common.start"), "pink", Flow.StartSolo);
        }

        void Clear(RectTransform rt) { for (int i = rt.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(rt.GetChild(i).gameObject); }

        /// <summary>電腦對手區：數量、整體難度、逐一調整。每次變動整區重畫（簡單、不會有殘留狀態）。</summary>
        void PaintOpp()
        {
            if (oppBox == null) return;
            Clear(oppBox);
            var so = Store.D.solo;
            var head = Ui.HBox(oppBox, 10, 0, TextAnchor.MiddleLeft);
            var ht = Ui.Label(head, Loc.T("solo.opps"), 28, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleLeft, false);
            Ui.Size(ht.rectTransform, flexW: 1, minW: 0);
            Ui.Pill(head, (1 + so.levels.Length) <= 4 ? Loc.T("solo.mid") : Loc.T("solo.big"), "off", 18);

            var faces = Ui.Wrap(oppBox, 6);
            Ui.AnimalFace(faces, Store.D.animal, 44);
            for (int i = 0; i < so.levels.Length; i++) Ui.AnimalFace(faces, anim[i], 44);

            var f1 = Ui.Field(oppBox, Loc.T("solo.num"));
            Ui.Stepper(f1, so.levels.Length, 1, 7, v => v.ToString(), v =>
            {
                var l = new List<string>(so.levels);
                while (l.Count < v) l.Add(l.Count > 0 ? l[l.Count - 1] : "normal");
                while (l.Count > v) l.RemoveAt(l.Count - 1);
                so.levels = l.ToArray(); Store.Save(); PaintOpp();
            });

            var f2 = Ui.Field(oppBox, Loc.T("solo.diff"));
            var lv = Ai.LEVEL_ORDER; var names = new string[lv.Length];
            for (int i = 0; i < lv.Length; i++) names[i] = Names.Level(lv[i]);
            bool same = true; for (int i = 1; i < so.levels.Length; i++) if (so.levels[i] != so.levels[0]) same = false;
            Ui.Seg(f2, names, same ? Array.IndexOf(lv, so.levels[0]) : -1, i =>
            {
                for (int k = 0; k < so.levels.Length; k++) so.levels[k] = lv[i];
                Store.Save(); PaintOpp();
            });
            if (same) Ui.Label(f2, Names.LevelHint(so.levels[0]), 20, Ui.Muted);

            var more = Ui.Btn(oppBox, (App.I.AiDetail ? Loc.T("solo.hide") : Loc.T("solo.detail")) + (App.I.AiDetail ? "  ▲" : "  ▼"), () => { App.I.AiDetail = !App.I.AiDetail; PaintOpp(); }, "plain", 0, 48, 22);
            if (App.I.AiDetail)
            {
                for (int i = 0; i < so.levels.Length; i++)
                {
                    int idx = i;
                    var row = Ui.Card(oppBox, 6, 10, Data.Hex("#fff3d6"), 14);
                    var hr = Ui.HBox(row, 8, 0, TextAnchor.MiddleLeft);
                    Ui.AnimalFace(hr, anim[i], 36);
                    Ui.Label(hr, Loc.T("solo.cpuN", "n", i + 1), 24, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleLeft, false);
                    Ui.Seg(row, names, Array.IndexOf(lv, so.levels[i]), k => { so.levels[idx] = lv[k]; Store.Save(); PaintOpp(); }, 70);
                }
            }
        }
    }
}
