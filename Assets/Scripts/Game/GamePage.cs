using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>
    /// 對戰畫面（單機與線上共用）：3D 舞台、鍵盤／觸控輸入、HUD、小地圖、能量與加速鈕、
    /// 左側資訊欄（Summary＋線上聊天室，寬螢幕固定在左、窄螢幕是可收合的抽屜）、暫停選單與結算 overlay。
    /// </summary>
    public sealed class GamePage : Page
    {
        public enum Kind { Solo, Challenge, Online }

        // 鏡頭仰角 52°：畫面上下被壓扁，把螢幕方向換算成場地方向時要補回來
        static readonly double TILT = Math.Sin(52 * Math.PI / 180);

        public readonly Kind kind;
        readonly GameState state; readonly Dictionary<int, Brain> brains;
        public ViewState view; public int slot;     // slot = -1：觀戰
        readonly object startInfo;

        World3D world; MiniMap minimap;
        double acc; bool ff; float overAt; bool resultDone;
        float hudAt = -1; string hudSig = ""; int watch = -1;
        bool sawTouch;

        // 輸入
        bool boostKeys, boostTouch, boostOn;
        int stickId = int.MinValue; Vector2 stickOrigin;
        double aimSent = double.NaN; float aimAt; double pendingAim = double.NaN;
        double lastKeyAng = double.NaN;

        // UI
        RectTransform main, side, sideBody, stick, stickKnob, stickZone, touchRoot, hudRow, chips, ghost, statsBox, sumBox, countRt;
        Text timerT, countT, ghostText, boostLabel; Image energyFill, boostRing, boostBg; RectTransform energyBar;
        RawImage mapImg; Button toggleBtn, exitBtn; HoldButton boostHold; Text badgeT; RectTransform badge;
        ChatBox chat; ScrollBox sideScroll;
        bool sideOpen; float countAnim; string lastCount = ""; bool layoutDirty = true; int lastLayoutKey;
        int unread; Ui.ModalHandle menuModal; Ui.ModalHandle resultModal;
        Action<ChatMsg> chatSub; Action chatResetSub;
        string lastPhase; bool deadLayout;

        public GamePage(Kind kind, GameState state, Dictionary<int, Brain> brains)
        {
            this.kind = kind; this.state = state; this.brains = brains;
            view = ViewState.FromGame(state); slot = 0;
        }

        public GamePage(object startInfo, int slot)
        {
            kind = Kind.Online; this.startInfo = startInfo; this.slot = slot;
            view = ViewState.FromStartInfo(startInfo);
        }

        // ================= 建立 =================
        public override void Build(RectTransform root)
        {
            if (world == null)
            {
                world = new World3D(App.I.transform, view, Store.D.quality, slot >= 0);
                world.NameOf = p => Names.P(p.Kind, p.Animal, p.Name);
            }
            var narrow = !(Lay.LogicalW >= 860 && Lay.LogicalW > Lay.LogicalH);
            sideOpen = !narrow;

            main = Ui.Obj("Main", root); Ui.Stretch(main); main.pivot = new Vector2(0.5f, 0.5f);

            // 觸控感應區（最底層）
            if (slot >= 0)
            {
                var padRt = Ui.Obj("Pad", main); Ui.Stretch(padRt);
                var pad = padRt.gameObject.AddComponent<Image>(); pad.color = new Color(0, 0, 0, 0);
                var tp = padRt.gameObject.AddComponent<TouchPad>();
                tp.Down = OnStickDown; tp.Drag = OnStickDrag; tp.Up = OnStickUp;
            }

            BuildHud();
            if (slot >= 0) BuildTouch();
            BuildSide(root);
            layoutDirty = true;
        }

        void BuildHud()
        {
            // 上方：計時與分數 chips
            hudRow = Ui.HBox(main, 10, 0, TextAnchor.MiddleCenter);
            hudRow.anchorMin = new Vector2(0, 1); hudRow.anchorMax = new Vector2(1, 1); hudRow.pivot = new Vector2(0.5f, 1);
            hudRow.offsetMin = new Vector2(150, -72); hudRow.offsetMax = new Vector2(-150, -10);
            var tb = Ui.Obj("Timer", hudRow);
            var tbg = tb.gameObject.AddComponent<Image>(); tbg.sprite = Ui.Round(Ui.Paper, Ui.Ink, 14, 3); tbg.type = Image.Type.Sliced; tbg.raycastTarget = false;
            var tl = tb.gameObject.AddComponent<HorizontalLayoutGroup>(); tl.padding = new RectOffset(14, 14, 6, 6); tl.childControlWidth = true; tl.childControlHeight = true;
            timerT = Ui.Label(tb, "--:--", 30, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            Ui.Size(tb, minW: 100, minH: 52);
            chips = Ui.HBox(hudRow, 8, 0, TextAnchor.MiddleLeft);

            // 左上：離開／暫停、開資訊欄
            var tl2 = Ui.HBox(main, 8, 0, TextAnchor.MiddleLeft);
            tl2.anchorMin = new Vector2(0, 1); tl2.anchorMax = new Vector2(0, 1); tl2.pivot = new Vector2(0, 1);
            tl2.anchoredPosition = new Vector2(12, -12); tl2.sizeDelta = new Vector2(140, 56);
            exitBtn = Ui.IconBtn(tl2, kind == Kind.Online ? "close" : "pause", () => OpenMenu(), kind == Kind.Online ? Loc.T("side.exitOnline") : Loc.T("side.exitPause"), 56);
            toggleBtn = Ui.IconBtn(tl2, "info", () => SetSide(true), Loc.T("side.open"), 56, "sky");
            badge = Ui.Obj("Badge", toggleBtn.transform);
            Ui.Anchor(badge, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-4, -4), new Vector2(28, 28));
            var bi = badge.gameObject.AddComponent<Image>(); bi.sprite = Ui.Circle(Data.Hex("#ff5a52"), Ui.Ink, 48, 3); bi.raycastTarget = false;
            badgeT = Ui.Label(badge, "", 16, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter, false); Ui.Stretch(badgeT.rectTransform);
            badge.gameObject.SetActive(false);

            // 右上：小地圖（在設定／語言鈕下面）
            minimap = new MiniMap(main);
            mapImg = minimap.Image;
            var mr = mapImg.rectTransform;
            mr.anchorMin = mr.anchorMax = new Vector2(1, 1); mr.pivot = new Vector2(1, 1);
            mr.anchoredPosition = new Vector2(-12, -80); mr.sizeDelta = new Vector2(132, 132);

            // 倒數
            countRt = Ui.Obj("Count", main);
            Ui.Anchor(countRt, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 200));
            countT = Ui.Label(countRt, "", 150, Data.Hex("#ffe27a"), FontStyle.Bold, TextAnchor.MiddleCenter, false);
            Ui.Stretch(countT.rectTransform);
            var co = countT.gameObject.AddComponent<Outline>(); co.effectColor = Ui.Ink; co.effectDistance = new Vector2(5, -5);
            countRt.gameObject.SetActive(false);

            // 能量條（桌機）
            energyBar = Ui.Obj("Energy", main);
            Ui.Anchor(energyBar, new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 16), new Vector2(240, 34));
            var eb = energyBar.gameObject.AddComponent<Image>(); eb.sprite = Ui.Round(Ui.Paper, Ui.Ink, 14, 3); eb.type = Image.Type.Sliced; eb.raycastTarget = false;
            var fillBg = Ui.Obj("Bar", energyBar); Ui.Stretch(fillBg, 8, 8, 8, 8);
            var fb = fillBg.gameObject.AddComponent<Image>(); fb.sprite = Ui.Round(Data.Hex("#e6dccb"), Data.Hex("#e6dccb"), 8, 0); fb.type = Image.Type.Sliced; fb.raycastTarget = false;
            var fill = Ui.Obj("Fill", fillBg); Ui.Stretch(fill);
            energyFill = fill.gameObject.AddComponent<Image>(); energyFill.sprite = Ui.Round(Data.Hex("#ffb259"), Data.Hex("#ffb259"), 8, 0); energyFill.type = Image.Type.Filled;
            energyFill.fillMethod = Image.FillMethod.Horizontal; energyFill.fillOrigin = 0; energyFill.raycastTarget = false;

            // 出局／觀戰的提示列
            ghost = Ui.HBox(main, 10, 12, TextAnchor.MiddleCenter);
            ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0); ghost.pivot = new Vector2(0.5f, 0); ghost.anchoredPosition = new Vector2(0, 14);
            var gbg = ghost.gameObject.AddComponent<Image>(); gbg.sprite = Ui.Round(Ui.Paper, Ui.Ink, 16, 3); gbg.type = Image.Type.Sliced; gbg.raycastTarget = false;
            var gfit = ghost.gameObject.AddComponent<ContentSizeFitter>(); gfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; gfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ghostText = Ui.Label(ghost, "", 22, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleLeft, false);
            ghost.gameObject.SetActive(false);
        }

        static Sprite ringSprite;
        static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - N / 2f) * (x + 0.5f - N / 2f) + (y + 0.5f - N / 2f) * (y + 0.5f - N / 2f));
                    float a = Mathf.Clamp01(N / 2f - d + 0.5f) * Mathf.Clamp01(d - (N / 2f - 14f) + 0.5f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply(false);
            ringSprite = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100);
            return ringSprite;
        }

        void BuildTouch()
        {
            touchRoot = Ui.Obj("Touch", main); Ui.Stretch(touchRoot);
            // 固定搖桿的感應區（只在固定模式用到，也兼作定位）
            stickZone = Ui.Obj("StickZone", touchRoot);
            Ui.Anchor(stickZone, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 24), new Vector2(230, 230));
            stick = Ui.Obj("Stick", touchRoot);
            Ui.Anchor(stick, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            var sb = stick.gameObject.AddComponent<Image>(); sb.sprite = Ui.Circle(new Color(1, 1, 1, 0.28f), new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.55f), 128, 5); sb.raycastTarget = false;
            stickKnob = Ui.Obj("Knob", stick);
            Ui.Anchor(stickKnob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
            var kb = stickKnob.gameObject.AddComponent<Image>(); kb.sprite = Ui.Circle(new Color(1, 1, 1, 0.85f), Ui.Ink, 96, 5); kb.raycastTarget = false;
            stick.gameObject.SetActive(false);

            // 加速鈕：外圈是能量
            var bt = Ui.Obj("Boost", touchRoot);
            Ui.Anchor(bt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-26, 26), new Vector2(124, 124));
            var ring0 = Ui.Obj("RingBg", bt); Ui.Stretch(ring0);
            var r0 = ring0.gameObject.AddComponent<Image>(); r0.sprite = Ring(); r0.color = new Color(0.9f, 0.86f, 0.78f, 0.9f); r0.raycastTarget = false;
            var ring1 = Ui.Obj("Ring", bt); Ui.Stretch(ring1);
            boostRing = ring1.gameObject.AddComponent<Image>(); boostRing.sprite = Ring(); boostRing.color = Data.Hex("#ffb259"); boostRing.type = Image.Type.Filled;
            boostRing.fillMethod = Image.FillMethod.Radial360; boostRing.fillOrigin = 2; boostRing.fillClockwise = false; boostRing.raycastTarget = false;
            var core = Ui.Obj("Core", bt); Ui.Stretch(core, 12, 12, 12, 12);
            boostBg = core.gameObject.AddComponent<Image>(); boostBg.sprite = Ui.Circle(Data.Hex("#ffd36a"), Ui.Ink, 128, 5); boostBg.raycastTarget = true;
            var ic = Ui.Img(core, Icons.Get("flash"), Ui.Ink, false);
            ic.rectTransform.anchorMin = new Vector2(0.3f, 0.38f); ic.rectTransform.anchorMax = new Vector2(0.7f, 0.82f); ic.rectTransform.offsetMin = ic.rectTransform.offsetMax = Vector2.zero;
            boostLabel = Ui.Label(core, Loc.T("boost.label"), 20, Ui.Ink, FontStyle.Bold, TextAnchor.LowerCenter, false);
            Ui.Stretch(boostLabel.rectTransform, 0, 0, 0, 8);
            boostHold = core.gameObject.AddComponent<HoldButton>();
            boostHold.Changed = (on, id) => { sawTouch = true; boostTouch = on; SyncBoost(); };
        }

        void BuildSide(RectTransform root)
        {
            side = Ui.Obj("Side", root);
            side.anchorMin = new Vector2(0, 0); side.anchorMax = new Vector2(0, 1); side.pivot = new Vector2(0, 0.5f);
            side.sizeDelta = new Vector2(340, 0); side.anchoredPosition = Vector2.zero;
            var bg = side.gameObject.AddComponent<Image>(); bg.sprite = Ui.Round(Ui.Cream, Ui.Ink, 4, 3); bg.type = Image.Type.Sliced;
            var col = Ui.VBox(side, 8, 12); Ui.Stretch(col);
            var head = Ui.HBox(col, 8, 0, TextAnchor.MiddleLeft);
            var ht = Ui.Label(head, Loc.T("side.title"), 28, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleLeft, false);
            Ui.Size(ht.rectTransform, flexW: 1, minW: 0);
            Ui.IconBtn(head, kind == Kind.Online ? "close" : "pause", () => OpenMenu(), Loc.T("side.exitPause"), 48);
            Ui.IconBtn(head, "back", () => SetSide(false), Loc.T("side.collapse"), 48, "sky");

            sideScroll = Ui.Scroll(col, 8, 2);
            Ui.Size(sideScroll.Root, minH: 120, flexH: 1, flexW: 1);
            sumBox = Ui.VBox(sideScroll.Content, 6, 0);
            statsBox = Ui.HBox(sideScroll.Content, 8, 0, TextAnchor.MiddleCenter);

            if (kind == Kind.Online)
            {
                chat = new ChatBox(col, 230, t => Net.Send(Net.Msg("chat", "text", t)));
                Ui.Size(chat.Root, minH: 220, prefH: 300, flexH: 0);
                chat.Set(App.I.Online.ChatLog);
                chatSub = m => { chat.Push(m); if (!sideOpen && m.Role != "sys") { unread++; PaintBadge(); } };
                chatResetSub = () => chat.Set(App.I.Online.ChatLog);
                App.I.Online.ChatPushed += chatSub; App.I.Online.ChatReset += chatResetSub;
            }
            side.gameObject.SetActive(sideOpen);
        }

        void PaintBadge()
        {
            badge.gameObject.SetActive(unread > 0 && !sideOpen);
            badgeT.text = unread > 9 ? "9+" : unread.ToString();
        }

        void SetSide(bool open)
        {
            sideOpen = open;
            if (open) { unread = 0; PaintBadge(); }
            side.gameObject.SetActive(open);
            toggleBtn.gameObject.SetActive(!open);
            layoutDirty = true; hudSig = "";
        }

        public override void Close()
        {
            if (world != null) { world.Dispose(); world = null; }
            if (minimap != null) minimap.Destroy();
            if (kind == Kind.Online && App.I.Online != null)
            {
                if (chatSub != null) App.I.Online.ChatPushed -= chatSub;
                if (chatResetSub != null) App.I.Online.ChatReset -= chatResetSub;
                if (App.I.Online.Game == this) App.I.Online.Game = null;
            }
            if (menuModal != null) menuModal.Close();
            if (resultModal != null) resultModal.Close();
            Sound.Music("menu");
        }

        public override void Relabel()
        {
            hudSig = ""; hudAt = -1;
            world.Relabel(view, slot);
            if (boostLabel != null) boostLabel.text = Loc.T("boost.label");
            // 靜態文字（側欄標題、按鈕）要重建；遊戲狀態在 world / state，不受影響
            var keepOpen = sideOpen;
            for (int i = Root.childCount - 1; i >= 0; i--) Destroy(Root.GetChild(i).gameObject);
            if (chat != null && App.I.Online != null) { App.I.Online.ChatPushed -= chatSub; App.I.Online.ChatReset -= chatResetSub; }
            chat = null;
            Build(Root);
            SetSide(keepOpen);
        }

        static void Destroy(UnityEngine.Object o) { UnityEngine.Object.Destroy(o); }

        public override bool Back() { OpenMenu(); return true; }

        // ================= 輸入 =================
        static bool TypingInField()
        {
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return false;
            var inp = es.currentSelectedGameObject.GetComponent<InputField>();
            return inp != null && inp.isFocused;
        }

        bool TouchVisible()
        {
            if (slot < 0) return false;
            var me = view.BySlot(slot);
            if (view.Phase == "over" || (me != null && !me.Alive)) return false;
            string m = Store.D.touchMode;
            if (m == "on") return true;
            if (m == "off") return false;
            return sawTouch || Application.isMobilePlatform || Platform.TouchDevice;
        }

        void OnStickDown(PointerEventData e)
        {
            sawTouch = true;
            if (slot < 0 || stickId != int.MinValue || App.I.ModalOpen) return;
            bool fixedMode = Store.D.stickMode == "fixed";
            if (fixedMode)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(stickZone, e.position, null)) return;
                Vector3[] c = new Vector3[4]; stickZone.GetWorldCorners(c);
                stickOrigin = (Vector2)((c[0] + c[2]) * 0.5f);
            }
            else
            {
                stickOrigin = e.position;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(main, e.position, null, out local);
                stick.anchoredPosition = local;
            }
            stickId = e.pointerId;
            stick.gameObject.SetActive(true); stickKnob.anchoredPosition = Vector2.zero;
            if (fixedMode) OnStickDrag(e);
        }

        void OnStickDrag(PointerEventData e)
        {
            if (e.pointerId != stickId) return;
            float scale = Mathf.Max(0.1f, App.I.UiScale);
            Vector2 d = (e.position - stickOrigin) / scale;
            float L = d.magnitude, max = 56f;
            stickKnob.anchoredPosition = L > max ? d * (max / L) : d;
            if (L > 8f) Aim(Math.Atan2(-d.y / TILT, d.x));   // 螢幕上方是 +y，場地的 +z 是螢幕下方
        }

        void OnStickUp(PointerEventData e)
        {
            if (e.pointerId != stickId) return;
            stickId = int.MinValue;
            stickKnob.anchoredPosition = Vector2.zero;
            PlaceFixedStick();
        }

        void PlaceFixedStick()
        {
            if (stick == null) return;
            bool fixedMode = Store.D.stickMode == "fixed" && TouchVisible();
            if (fixedMode && stickId == int.MinValue)
            {
                // stickZone 以左下角（或右下角）為錨點：換算成以中心為錨點的座標
                var m = main.rect;
                float flip = Store.D.stickSide == "right" ? -1f : 1f;
                stick.anchoredPosition = new Vector2(flip * (-m.width * 0.5f + stickZone.sizeDelta.x * 0.5f + 24f), -m.height * 0.5f + stickZone.sizeDelta.y * 0.5f + 24f);
                stick.gameObject.SetActive(true);
            }
            else if (stickId == int.MinValue) stick.gameObject.SetActive(false);
        }

        void SyncBoost()
        {
            bool on = boostKeys || boostTouch;
            if (on == boostOn) return;
            boostOn = on;
            if (slot < 0) return;
            if (kind == Kind.Online) Net.Send(Net.Msg("boost", "on", on));
            else Rules.SetBoost(state, slot, on);
        }

        void Aim(double a)
        {
            if (slot < 0 || view.Phase == "over") return;
            var me = view.BySlot(slot);
            if (me == null || !me.Alive) return;
            if (kind != Kind.Online) { Rules.Aim(state, slot, a); return; }
            me.Ta = a; pendingAim = a;
            FlushAim(false);
        }

        /// <summary>線上：方向有明顯變化才送，最多每 50ms 一次。</summary>
        void FlushAim(bool force)
        {
            if (double.IsNaN(pendingAim)) return;
            double a = pendingAim;
            if (!double.IsNaN(aimSent) && Math.Abs(Rules.WrapAng(a - aimSent)) < 0.03) { pendingAim = double.NaN; return; }
            if (!force && Time.unscaledTime - aimAt < 0.05f) return;
            Net.Send(Net.Msg("input", "a", Math.Round(a * 1000) / 1000));
            aimSent = a; aimAt = Time.unscaledTime; pendingAim = double.NaN;
        }

        void ReadKeyboard()
        {
            if (slot < 0 || App.I.ModalOpen || TypingInField()) { if (boostKeys) { boostKeys = false; SyncBoost(); } return; }
            float dx = 0, dy = 0;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dx -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dx += 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dy -= 1;     // 螢幕上方 = 場地 -z
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dy += 1;
            if (dx != 0 || dy != 0) Aim(Math.Atan2(dy / TILT, dx));
            bool b = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (b != boostKeys) { boostKeys = b; SyncBoost(); }
        }

        // ================= 每格 =================
        public override void Tick()
        {
            if (world == null) return;
            float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime);
            ReadKeyboard();

            double alpha = 1;
            if (kind == Kind.Online)
            {
                alpha = view.SnapAt > 0 ? Math.Max(0, Math.Min(1, (Time.realtimeSinceStartupAsDouble - view.SnapAt) / Math.Max(0.05, view.Gap))) : 1;
                FlushAim(false);
            }
            else alpha = StepSolo(dt);

            PickWatch();
            UpdateLayout();
            float scale = App.I.UiScale;
            float top = 64f * scale;
            bool portrait = Screen.height > Screen.width;
            float bottom = TouchVisible() && portrait ? Mathf.Min(150f * scale, main.rect.height * scale * 0.16f) : 0f;
            world.Draw(view, slot, alpha, dt, Store.D.zoom, watch, top, bottom);
            UpdateHud(dt);
        }

        double StepSolo(float dt)
        {
            if (App.I.ModalOpen) return Math.Min(1, acc / Rules.DT);   // 開著選單或設定就暫停
            var me = slot >= 0 ? Rules.PlayerBySlot(state, slot) : null;
            bool dead = me != null && !me.Alive && state.Phase == "play";
            acc += dt * (ff && dead ? 3 : 1);
            var evs = new List<GameEvent>();
            int guard = 0;
            while (acc >= Rules.DT && guard++ < 12)
            {
                foreach (var p in state.Players) { p.PrevPts = p.Pts; p.PrevAng = p.Ang; }
                foreach (var p in state.Players) { Brain br; if (brains.TryGetValue(p.Slot, out br)) Ai.Drive(br, state, p); }
                Rules.Step(state, Rules.DT);
                evs.AddRange(state.Events);
                acc -= Rules.DT;
                if (state.Phase == "over" && state.EndHold <= 0) break;
            }
            view.Sync(state);
            if (evs.Count > 0) HandleEvents(evs);
            if (state.Phase == "over" && !resultDone)
            {
                if (overAt <= 0) overAt = Time.unscaledTime;
                if (state.EndHold <= 0 || Time.unscaledTime - overAt > 2.2f)
                {
                    resultDone = true;
                    ShowResult(ResultInfo.FromGame(state.Result, state));
                }
            }
            return Math.Min(1, acc / Rules.DT);
        }

        // ---------- 線上：套用伺服器快照 ----------
        public void OnSnap(object snap)
        {
            var me = slot >= 0 ? view.BySlot(slot) : null;
            double myTa = me != null ? me.Ta : 0;
            view.ApplySnapshot(snap, Time.realtimeSinceStartupAsDouble);
            if (me != null && !double.IsNaN(aimSent)) me.Ta = myTa;   // 自己的眼睛看向剛按的方向，伺服器的值晚一點才到
            var el = Json.Arr(snap, "e");
            if (el != null && el.Count > 0)
            {
                var evs = new List<GameEvent>();
                foreach (var o in el)
                {
                    var g = new GameEvent
                    {
                        T = Json.Str(o, "t"), N = Json.Int(o, "n"), Slot = Json.Int(o, "slot", -1), Type = Json.Int(o, "type"), V = Json.Int(o, "v"),
                        X = Json.Num(o, "x"), Z = Json.Num(o, "z"), Id = Json.Int(o, "id"), Why = Json.Str(o, "why"), Killer = Json.Int(o, "killer", -1),
                        Orbs = Json.Int(o, "orbs"), Winner = Json.Int(o, "winner", -1), Reason = Json.Str(o, "reason")
                    };
                    evs.Add(g);
                }
                HandleEvents(evs);
            }
        }

        void HandleEvents(List<GameEvent> evs)
        {
            world.Consume(evs, slot, Store.D.reduceMotion);
            foreach (var e in evs)
            {
                switch (e.T)
                {
                    case "eat": if (e.Slot == slot) Sound.Sfx(e.Type == 1 ? "gold" : "eat"); break;
                    case "die":
                        if (e.Slot == slot) { Sound.Sfx("die"); App.Vibrate(240); } else Sound.Sfx("item");
                        break;
                    case "count": Sound.Sfx("count"); FlashCount(e.N.ToString()); break;
                    case "go": Sound.Sfx("go"); Sound.Music("battle"); FlashCount(Loc.T("game.go")); break;
                    case "over":
                        if (view.Mode == "challenge") Sound.Sfx("lose");
                        else Sound.Sfx(e.Winner < 0 ? "draw" : (e.Winner == slot ? "win" : (slot < 0 ? "win" : "lose")));
                        break;
                }
            }
        }

        void FlashCount(string txt)
        {
            countRt.gameObject.SetActive(true); countT.text = txt; countAnim = 0f; lastCount = txt;
            if (txt == Loc.T("game.go")) countHideAt = Time.unscaledTime + 0.7f; else countHideAt = 0;
        }
        float countHideAt;

        void PickWatch()
        {
            var me = slot >= 0 ? view.BySlot(slot) : null;
            if (me != null && me.Alive) { watch = -1; return; }
            var cur = watch >= 0 ? view.BySlot(watch) : null;
            if (cur != null && cur.Alive) return;
            ViewPlayer best = null;
            foreach (var p in view.Players) if (p.Alive && (best == null || p.Score > best.Score)) best = p;
            watch = best != null ? best.Slot : -1;
        }

        void NextWatch()
        {
            var alive = view.Players.FindAll(p => p.Alive);
            if (alive.Count == 0) return;
            int i = alive.FindIndex(p => p.Slot == watch);
            watch = alive[(i + 1) % alive.Count].Slot;
            hudSig = "";
        }

        // ================= 版面 =================
        void UpdateLayout()
        {
            bool tv = TouchVisible();
            int key = Screen.width * 31 + Screen.height + (sideOpen ? 7 : 0) + (tv ? 13 : 0) + (Store.D.minimap ? 3 : 0)
                + (Store.D.stickMode == "fixed" ? 5 : 0) + (Store.D.stickSide == "right" ? 11 : 0);
            if (!layoutDirty && key == lastLayoutKey) return;
            layoutDirty = false; lastLayoutKey = key;

            float lw = Lay.LogicalW, lh = Lay.LogicalH;
            bool wide = lw >= 860 && lw > lh;
            float sideW = Mathf.Min(340f, lw - 70f);
            side.sizeDelta = new Vector2(sideW, 0);
            toggleBtn.gameObject.SetActive(!sideOpen);
            exitBtn.gameObject.SetActive(true);
            // 寬螢幕：資訊欄固定在左側，遊戲主區讓出空間；窄螢幕：資訊欄是疊在上面的抽屜
            float off = wide && sideOpen ? sideW : 0f;
            main.offsetMin = new Vector2(off, 0); main.offsetMax = Vector2.zero;
            hudRow.offsetMin = new Vector2(off > 0 ? 20 : 150, -72); hudRow.offsetMax = new Vector2(-150, -10);

            mapImg.gameObject.SetActive(Store.D.minimap);
            energyBar.gameObject.SetActive(!tv && slot >= 0);

            if (touchRoot != null)
            {
                touchRoot.gameObject.SetActive(tv);
                bool right = Store.D.stickSide == "right";
                var bt = (RectTransform)boostHold.transform.parent;
                bt.anchorMin = bt.anchorMax = new Vector2(right ? 0 : 1, 0); bt.pivot = new Vector2(right ? 0 : 1, 0);
                bt.anchoredPosition = new Vector2(right ? 26 : -26, 26);
                stickZone.anchorMin = stickZone.anchorMax = new Vector2(right ? 1 : 0, 0); stickZone.pivot = new Vector2(right ? 1 : 0, 0);
                stickZone.anchoredPosition = new Vector2(right ? -24 : 24, 24);
                PlaceFixedStick();
            }

            // 3D 相機只畫在遊戲主區
            Vector3[] c = new Vector3[4]; main.GetWorldCorners(c);
            float W = Mathf.Max(1, Screen.width), H = Mathf.Max(1, Screen.height);
            float mx = wide && sideOpen ? Mathf.Clamp01(c[0].x / W) : 0f;
            world.Cam.rect = new Rect(mx, 0, 1 - mx, 1);
            world.ViewW = W * (1 - mx); world.ViewH = H;
        }

        // ================= HUD =================
        void UpdateHud(float dt)
        {
            var me = slot >= 0 ? view.BySlot(slot) : null;
            bool showEnergy = me != null && me.Alive;
            if (showEnergy)
            {
                float e = Mathf.Clamp01((float)me.Energy);
                if (energyFill != null) { energyFill.fillAmount = e; energyFill.color = me.Tired ? Data.Hex("#c9b8a0") : Color.white; }
                if (boostRing != null) { boostRing.fillAmount = e; boostRing.color = me.Tired ? Data.Hex("#c9b8a0") : Data.Hex("#ffb259"); }
            }
            if (energyBar != null) energyBar.gameObject.SetActive(showEnergy && !TouchVisible() && slot >= 0);

            // 倒數數字的彈跳動畫
            if (countRt.gameObject.activeSelf)
            {
                countAnim += dt;
                float s = 1f + 0.45f * Mathf.Exp(-countAnim * 9f);
                countRt.localScale = Vector3.one * s;
                if (countHideAt > 0 && Time.unscaledTime > countHideAt) { countRt.gameObject.SetActive(false); countHideAt = 0; }
            }

            // 小地圖與文字 HUD 每 0.1 秒更新一次
            if (Time.unscaledTime - hudAt < 0.1f) return;
            hudAt = Time.unscaledTime;
            if (Store.D.minimap) minimap.Draw(view, slot);
            if (view.Phase != lastPhase) { lastPhase = view.Phase; layoutDirty = true; }
            if (me != null && !me.Alive && !deadLayout) { deadLayout = true; layoutDirty = true; }

            bool challenge = view.Mode == "challenge";
            if (view.Phase == "countdown")
            {
                int c = kind == Kind.Online ? (int)view.Countdown : (int)Math.Ceiling(view.Countdown - 1e-9);
                if (c > 0 && lastCount != c.ToString()) FlashCount(c.ToString());
            }
            else if (countRt.gameObject.activeSelf && lastCount != Loc.T("game.go")) countRt.gameObject.SetActive(false);

            string txt; Color tcol = Ui.Ink;
            if (view.Phase == "countdown") txt = Loc.T("game.ready");
            else if (view.TimeLimit > 0)
            {
                double left = Math.Max(0, view.TimeLimit - view.Time);
                txt = Names.Clock(left);
                if (left <= 15 && view.Phase == "play") tcol = Data.Hex("#e0584d");
            }
            else txt = Names.Clock(view.Time);
            timerT.text = txt; timerT.color = tcol;

            string sig = "";
            foreach (var p in view.Players) sig += p.Slot + "," + (p.Alive ? 1 : 0) + "," + p.Score + "," + LenOf(p) + "," + p.Eaten + "," + p.Kills + "|";
            sig += watch + "|" + Screen.width + "|" + Loc.Lang + "|" + sideOpen;
            if (sig == hudSig) return;
            hudSig = sig;
            PaintChips(me, challenge);
            PaintSummary(me, challenge);
            PaintGhost(me, challenge);
        }

        static int LenOf(ViewPlayer p) { return p.Alive ? (int)Rules.JsRound(p.Len) : p.FinalLen; }

        static void Clear(RectTransform rt) { for (int i = rt.childCount - 1; i >= 0; i--) { var c = rt.GetChild(i).gameObject; c.SetActive(false); UnityEngine.Object.Destroy(c); } }

        void Chip(RectTransform parent, string animal, string text, string small, bool me, bool dead, bool record = false)
        {
            var rt = Ui.Obj("Chip", parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = Ui.Round(record ? Data.Hex("#ffe27a") : (me ? Data.Hex("#fff0b8") : Ui.Paper), Ui.Ink, 14, 3); im.type = Image.Type.Sliced; im.raycastTarget = false;
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>(); g.padding = new RectOffset(8, 12, 4, 4); g.spacing = 6; g.childAlignment = TextAnchor.MiddleCenter;
            g.childControlWidth = true; g.childControlHeight = true;
            if (animal != null) Ui.AnimalFace(rt, animal, 32);
            if (!string.IsNullOrEmpty(small)) Ui.Label(rt, small, 18, Ui.Muted, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            Ui.Label(rt, text, 28, dead ? Ui.Muted : Ui.Ink, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            Ui.Size(rt, minH: 46);
            if (dead) im.color = new Color(1, 1, 1, 0.55f);
        }

        void PaintChips(ViewPlayer me, bool challenge)
        {
            Clear(chips);
            float mw = main.rect.width;
            if (challenge && me != null)
            {
                bool narrow = mw < 620;
                int best = Store.Best(view.Speed, ObstacleKey());
                int shown = Math.Max(best, me.Score);
                Chip(chips, narrow ? null : me.Animal, me.Score.ToString(), narrow ? null : Loc.T("chip.score"), true, false);
                if (!narrow) { Chip(chips, null, LenOf(me).ToString(), Loc.T("chip.len"), false, false); Chip(chips, null, (1 + me.Eaten / 5).ToString(), Loc.T("chip.lv"), false, false); }
                Chip(chips, null, shown.ToString(), Loc.T("chip.best"), false, false, me.Score > best && me.Score > 0);
                return;
            }
            var list = new List<ViewPlayer>(view.Players);
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            int room = mw < 620 ? 3 : mw < 900 ? 5 : 8;
            if (list.Count > room)
            {
                var top = list.GetRange(0, room);
                var mine = list.Find(p => p.Slot == slot);
                if (mine != null && !top.Contains(mine)) { top.RemoveAt(top.Count - 1); top.Add(mine); }
                list = top;
            }
            foreach (var p in list) Chip(chips, p.Animal, p.Score.ToString(), null, p.Slot == slot, !p.Alive);
        }

        string ObstacleKey() { return state != null ? state.Obstacles : Store.D.challenge.obstacles; }

        void PaintSummary(ViewPlayer me, bool challenge)
        {
            Clear(sumBox); Clear(statsBox);
            foreach (var p in view.Players)
            {
                var row = Ui.Card(sumBox, 4, 8, p.Slot == slot ? Data.Hex("#fff0b8") : Ui.Paper, 12);
                var hr = Ui.HBox(row, 8, 0, TextAnchor.MiddleLeft);
                var dot = Ui.Obj("Dot", hr); Ui.Size(dot, minW: 14, minH: 14, prefW: 14, prefH: 14);
                var di = dot.gameObject.AddComponent<Image>(); di.sprite = Ui.Circle(Data.SlotColor(p.Slot), Ui.Ink, 32, 2); di.raycastTarget = false;
                Ui.AnimalFace(hr, p.Animal, 34);
                var vb = Ui.VBox(hr, 0, 0);
                Ui.Size(vb, flexW: 1, minW: 0);
                Ui.Label(vb, Names.P(p.Kind, p.Animal, p.Name) + (p.Slot == slot ? Loc.T("res.youTag") : ""), 22, p.Alive ? Ui.Ink : Ui.Muted, FontStyle.Bold, TextAnchor.UpperLeft, false);
                string who = p.Kind == "ai" ? (string.IsNullOrEmpty(p.Level) ? Loc.T("sum.cpu") : Loc.T("sum.cpuLv", "lv", Names.Level(p.Level))) : Loc.T("sum.player");
                string st = p.Alive ? Loc.T("sum.len", "n", LenOf(p)) : (p.Left ? Loc.T("sum.left") : Loc.T("sum.out"));
                Ui.Label(vb, who + "・" + st, 17, Ui.Muted, FontStyle.Normal, TextAnchor.UpperLeft, false);
                Ui.Label(hr, p.Score.ToString(), 28, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleRight, false);
            }
            if (view.Players.Count > 0 && slot < 0) Ui.Pill(sumBox, Loc.T("game.spectating"), "off", 18);
            if (me != null)
            {
                Action<string, string> tile = (val, lab) =>
                {
                    var t = Ui.Card(statsBox, 0, 8, Ui.Paper, 12); Ui.Size(t, flexW: 1, minW: 0);
                    Ui.Label(t, val, 30, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleCenter, false);
                    Ui.Label(t, lab, 17, Ui.Muted, FontStyle.Normal, TextAnchor.MiddleCenter, false);
                };
                tile(me.Score.ToString(), Loc.T("res.score"));
                tile(LenOf(me).ToString(), Loc.T("res.len"));
                tile(challenge ? (1 + me.Eaten / 5).ToString() : me.Kills.ToString(), challenge ? Loc.T("res.level") : Loc.T("game.kills"));
            }
        }

        void PaintGhost(ViewPlayer me, bool challenge)
        {
            bool outNow = (me != null && !me.Alive) || slot < 0;
            if (!(outNow && view.Phase != "over" && !(challenge && me != null))) { ghost.gameObject.SetActive(false); return; }
            ghost.gameObject.SetActive(true);
            for (int i = ghost.childCount - 1; i >= 1; i--) UnityEngine.Object.Destroy(ghost.GetChild(i).gameObject);
            var wp = watch >= 0 ? view.BySlot(watch) : null;
            string prefix = me != null ? (me.Left ? Loc.T("ghost.left") : Loc.T("ghost.out")) + "・" : "";
            ghostText.text = prefix + (wp != null ? Loc.T("ghost.watching", "name", Names.P(wp.Kind, wp.Animal, wp.Name)) : Loc.T("game.spectating"));
            int alive = view.Players.FindAll(p => p.Alive).Count;
            if (alive > 1) Ui.Btn(ghost, Loc.T("ghost.next"), () => NextWatch(), "sky", 0, 48, 20);
            if (kind != Kind.Online) Ui.Btn(ghost, ff ? Loc.T("ghost.normal") : Loc.T("ghost.ff"), () => { ff = !ff; hudSig = ""; }, "orange", 0, 48, 20);
        }

        // ================= 選單與結算 =================
        public void OpenMenu()
        {
            if (App.I.ModalOpen) return;
            bool online = kind == Kind.Online;
            var m = App.I.Modal(Loc.T("menu.pause"), true, 480);
            menuModal = m;
            Ui.Label(m.Body, online ? Loc.T("menu.pause.online") : Loc.T("menu.pause.solo"), 24, Ui.Ink);
            var row = Ui.Wrap(m.Body, 10);
            if (online)
            {
                Ui.Btn(row, Loc.T("menu.leaveRoom"), () => { m.Close(); App.I.Online.ConfirmLeave(); }, "pink", 150, 56, 22);
                Ui.Btn(row, Loc.T("menu.continue"), () => m.Close(), "primary", 150, 56, 22);
            }
            else
            {
                Ui.Btn(row, Loc.T("menu.home"), () => { m.Close(); App.I.Go(new HomePage()); }, "plain", 130, 56, 22);
                Ui.Btn(row, Loc.T("menu.restart"), () => { m.Close(); Flow.Restart(); }, "orange", 130, 56, 22);
                Ui.Btn(row, Loc.T("menu.continue"), () => m.Close(), "primary", 130, 56, 22);
            }
        }

        bool recorded; int statW, statP; bool statShown;

        public void ShowResult(ResultInfo r)
        {
            if (resultModal != null && resultModal.Mask != null) return;
            if (menuModal != null && menuModal.Mask != null) menuModal.Close();
            var d = Store.D;
            bool challenge = r.Mode == "challenge";
            string head, sub; bool isNew = false; int best = 0;
            int mySlot = slot;
            if (challenge)
            {
                int sc = r.Score(mySlot >= 0 ? mySlot : 0);
                if (!recorded) { isNew = Store.SetBest(view.Speed, ObstacleKey(), sc); Store.Record(d.statChallenge, false); recorded = true; newRecord = isNew; }
                isNew = newRecord;
                best = Store.Best(view.Speed, ObstacleKey());
                head = isNew && sc > 0 ? Loc.T("res.newRecord") : Loc.T("res.over");
                sub = Loc.T("res.overSub");
            }
            else
            {
                int w = r.Winner;
                string wn = w >= 0 && r.By(w) != null ? Names.P(r.By(w).Kind, r.By(w).Animal, r.By(w).Name) : "";
                bool time = r.Reason == "time";
                if (w < 0) { head = Loc.T("res.draw"); sub = time ? Loc.T("res.draw.time") : Loc.T("res.draw.last"); }
                else if (w == mySlot) { head = Loc.T("res.youWin"); sub = time ? Loc.T("res.youWin.time") : Loc.T("res.youWin.last"); }
                else if (mySlot < 0) { head = Loc.T("res.wins", "name", wn); sub = time ? Loc.T("res.wins.time") : Loc.T("res.wins.last"); }
                else { head = Loc.T("res.lose"); sub = time ? Loc.T("res.lose.time", "name", wn) : Loc.T("res.lose.last", "name", wn); }
                if (mySlot >= 0 && !recorded)
                {
                    var c = kind == Kind.Online ? d.statOnline : d.statSolo;
                    Store.Record(c, w == mySlot); statW = c.win; statP = c.play; recorded = true;
                }
            }

            var m = App.I.Modal(head, false, 620);
            resultModal = m;
            Ui.Label(m.Body, sub, 24, Ui.Muted, FontStyle.Normal, TextAnchor.UpperCenter);
            if (challenge)
            {
                var grid = Ui.Wrap(m.Body, 10, TextAnchor.UpperCenter, true);
                Tile(grid, Loc.T("res.score"), r.Score(0).ToString(), true);
                Tile(grid, Loc.T("res.len"), r.Len(0).ToString(), false);
                Tile(grid, Loc.T("res.level"), r.Level.ToString(), false);
                Tile(grid, Loc.T("res.best"), best.ToString(), false);
                Ui.Label(m.Body, Loc.T("res.time", "m", (int)(r.Time / 60), "s", (int)(r.Time % 60)), 20, Ui.Muted, FontStyle.Normal, TextAnchor.UpperCenter);
            }
            else
            {
                int pos = 0;
                foreach (int s in r.Ranking)
                {
                    var p = r.By(s); if (p == null) continue;
                    pos++;
                    var row = Ui.Card(m.Body, 4, 8, s == r.Winner ? Data.Hex("#fff0b8") : Ui.Paper, 12);
                    var hr = Ui.HBox(row, 10, 0, TextAnchor.MiddleLeft);
                    var no = Ui.Label(hr, pos.ToString(), 28, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleCenter, false); Ui.Size(no.rectTransform, minW: 30, prefW: 30);
                    Ui.AnimalFace(hr, p.Animal, 38);
                    string nm = Names.P(p.Kind, p.Animal, p.Name) + (s == mySlot ? Loc.T("res.youTag") : "") + (p.Kind == "ai" && !string.IsNullOrEmpty(p.Level) ? "・" + Names.Level(p.Level) : "");
                    var nt = Ui.Label(hr, nm, 22, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleLeft); Ui.Size(nt.rectTransform, flexW: 1, minW: 0);
                    var vb = Ui.VBox(hr, 0, 0); Ui.Size(vb, minW: 80, prefW: 90);
                    Ui.Label(vb, Loc.T("res.pts", "n", r.Score(s)), 22, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleRight, false);
                    Ui.Label(vb, Loc.T("res.len2", "n", r.Len(s)), 17, Ui.Muted, FontStyle.Normal, TextAnchor.MiddleRight, false);
                }
                string foot = Loc.T("res.total", "m", (int)(r.Time / 60), "s", (int)(r.Time % 60)) + (statP > 0 ? "  " + Loc.T("res.stat", "w", statW, "p", statP) : "");
                Ui.Label(m.Body, foot, 20, Ui.Muted, FontStyle.Normal, TextAnchor.UpperCenter);
            }

            // 三個動作：再來一局／回到設定（回到房間）／回到首頁 —— 順序固定
            var act = Ui.Wrap(m.Body, 10, TextAnchor.UpperCenter, true);
            if (kind != Kind.Online)
            {
                Ui.Btn(act, Loc.T("res.again"), () => { m.Close(); Flow.Restart(); }, "primary", 170, 60, 24);
                Ui.Btn(act, Loc.T("res.setup"), () => { m.Close(); Flow.BackToSetup(); }, "orange", 170, 60, 24);
                Ui.Btn(act, Loc.T("res.home"), () => { m.Close(); App.I.Go(new HomePage()); }, "plain", 170, 60, 24);
            }
            else
            {
                bool host = App.I.Online.IsHost;
                var again = Ui.Btn(act, Loc.T("res.again"), () => { m.Close(); App.I.Online.Rematch(); }, "primary", 170, 60, 24);
                again.interactable = host;
                Ui.Btn(act, Loc.T("res.room"), () => { m.Close(); App.I.Online.BackToRoom(); }, "orange", 170, 60, 24);
                Ui.Btn(act, Loc.T("res.home"), () => { m.Close(); App.I.Online.LeaveToHome(); }, "plain", 170, 60, 24);
                if (!host) Ui.Label(m.Body, Loc.T("res.hostNote"), 18, Ui.Muted, FontStyle.Normal, TextAnchor.UpperCenter);
            }
        }
        bool newRecord;

        static void Tile(RectTransform parent, string label, string val, bool main)
        {
            var t = Ui.Card(parent, 0, 10, main ? Data.Hex("#fff0b8") : Ui.Paper, 14);
            Ui.Size(t, minW: 120, prefW: 130);
            Ui.Label(t, label, 18, Ui.Muted, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            Ui.Label(t, val, main ? 44 : 36, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleCenter, false);
        }
    }
}
