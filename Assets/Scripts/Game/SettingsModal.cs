using System;
using UnityEngine;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>設定彈窗：任何畫面右上角的齒輪都開同一個彈窗（不跳頁）。聲音、語言、鏡頭、小地圖、畫質、操作、減少動態。</summary>
    public static class SettingsModal
    {
        static Ui.ModalHandle handle;
        public static bool IsOpen { get { return handle != null && handle.Mask != null; } }

        static void Row(RectTransform parent, string label, string hint, Action<RectTransform> ctl)
        {
            var f = Ui.Field(parent, label, hint);
            ctl(f);
        }

        static RectTransform Inline(RectTransform parent)
        {
            return Ui.HBox(parent, 12, 0, TextAnchor.MiddleLeft);
        }

        public static void Open()
        {
            if (IsOpen) return;
            var d = Store.D;
            var m = App.I.Modal(Loc.T("set.title"), true, 640);
            handle = m;
            var b = m.Body;

            Ui.Label(b, Loc.T("set.sound"), 28, Ui.Ink, FontStyle.Bold);
            Slider bgmVol = null, sfxVol = null;
            Row(b, Loc.T("set.bgm"), null, f =>
            {
                var r = Inline(f);
                Ui.Switch(r, d.bgm, v => { d.bgm = v; Store.Save(); Sound.Apply(); if (bgmVol != null) bgmVol.interactable = v; });
                bgmVol = Ui.Slide(r, d.bgmVol, v => { d.bgmVol = v; Store.Save(); Sound.Apply(); });
                bgmVol.interactable = d.bgm;
            });
            Row(b, Loc.T("set.sfx"), null, f =>
            {
                var r = Inline(f);
                Ui.Switch(r, d.sfx, v => { d.sfx = v; Store.Save(); Sound.Apply(); if (sfxVol != null) sfxVol.interactable = v; if (v) Sound.Sfx("eat"); });
                sfxVol = Ui.Slide(r, d.sfxVol, v => { d.sfxVol = v; Store.Save(); Sound.Apply(); });
                sfxVol.interactable = d.sfx;
                // 放開滑桿時播一聲，讓人聽得出音量
                var up = sfxVol.gameObject.AddComponent<PointerUpSound>();
            });

            Ui.Label(b, Loc.T("set.display"), 28, Ui.Ink, FontStyle.Bold);
            Row(b, Loc.T("set.lang"), null, f =>
                Ui.Seg(f, new[] { Loc.T("lang.zh-Hant"), Loc.T("lang.en") }, Loc.Lang == Loc.EN ? 1 : 0, i => Loc.Set(i == 1 ? Loc.EN : Loc.ZH), 120));
            Row(b, Loc.T("set.cam"), null, f =>
                Ui.Seg(f, new[] { Loc.T("set.cam.near"), Loc.T("set.cam.normal"), Loc.T("set.cam.far") }, Array.IndexOf(new[] { "near", "normal", "far" }, d.zoom),
                    i => { d.zoom = new[] { "near", "normal", "far" }[i]; Store.Save(); }));
            Row(b, Loc.T("set.map"), null, f => Ui.Switch(f, d.minimap, v => { d.minimap = v; Store.Save(); }));
            Row(b, Loc.T("set.quality"), Loc.T("set.quality.hint"), f =>
                Ui.Seg(f, new[] { Loc.T("set.quality.high"), Loc.T("set.quality.normal"), Loc.T("set.quality.low") }, Array.IndexOf(new[] { "high", "normal", "low" }, d.quality),
                    i => { d.quality = new[] { "high", "normal", "low" }[i]; Store.Save(); }));
            Row(b, Loc.T("set.stick"), Loc.T("set.stick.hint"), f =>
                Ui.Seg(f, new[] { Loc.T("set.stick.float"), Loc.T("set.stick.fixed") }, d.stickMode == "fixed" ? 1 : 0, i => { d.stickMode = i == 1 ? "fixed" : "float"; Store.Save(); }));
            Row(b, Loc.T("set.side"), Loc.T("set.side.hint"), f =>
                Ui.Seg(f, new[] { Loc.T("set.side.left"), Loc.T("set.side.right") }, d.stickSide == "right" ? 1 : 0, i => { d.stickSide = i == 1 ? "right" : "left"; Store.Save(); }));
            Row(b, Loc.T("set.touch"), null, f =>
                Ui.Seg(f, new[] { Loc.T("set.touch.auto"), Loc.T("set.touch.on"), Loc.T("set.touch.off") }, Array.IndexOf(new[] { "auto", "on", "off" }, d.touchMode),
                    i => { d.touchMode = new[] { "auto", "on", "off" }[i]; Store.Save(); }));
            Row(b, Loc.T("set.vibrate"), null, f => Ui.Switch(f, d.vibrate, v => { d.vibrate = v; Store.Save(); if (v) Platform.Vibrate(40); }));
            Row(b, Loc.T("set.calm"), null, f => Ui.Switch(f, d.reduceMotion, v => { d.reduceMotion = v; Store.Save(); }));
            Ui.Label(b, Loc.T("set.keys"), 20, Ui.Muted);

            var act = Ui.Wrap(b, 10);
            Ui.Btn(act, Loc.T("set.reset"), () =>
            {
                Store.ResetSettings(); Sound.Apply();
                m.Close(); App.I.Toast(Loc.T("set.reset.done")); Open();
            }, "plain", 180, 56, 22);
            Ui.Btn(act, Loc.T("common.done"), () => m.Close(), "primary", 150, 56, 22);
        }

        /// <summary>切換語言：重開彈窗並保留捲動位置。</summary>
        public static void OnLang()
        {
            if (!IsOpen) return;
            var sr = handle.Panel.GetComponentInChildren<ScrollRect>();
            float pos = sr != null ? sr.verticalNormalizedPosition : 1f;
            handle.Close();
            Open();
            Canvas.ForceUpdateCanvases();
            var nr = handle.Panel.GetComponentInChildren<ScrollRect>();
            if (nr != null) nr.verticalNormalizedPosition = pos;
        }
    }

    public sealed class PointerUpSound : MonoBehaviour, UnityEngine.EventSystems.IPointerUpHandler
    {
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { Sound.Sfx("eat"); }
    }
}
