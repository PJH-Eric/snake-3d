using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snake3D
{
    /// <summary>畫面（頁面）基底：App 負責建立根物件、切換與清理。</summary>
    public abstract class Page
    {
        public string Name;
        public RectTransform Root;
        public abstract void Build(RectTransform parent);
        public virtual void Tick() { }
        public virtual void Close() { }
        /// <summary>切換語言：預設整頁重建；遊戲畫面會改寫成只換標籤。</summary>
        public virtual void Relabel() { App.I.Rebuild(); }
        /// <summary>返回鍵／Esc。回傳 true 表示已處理。</summary>
        public virtual bool Back() { return false; }
    }

    /// <summary>
    /// 應用程式外殼：Canvas、字級縮放、安全區、事件系統、背景相機、畫面切換、彈窗、提示、右上角設定／語言鈕。
    /// 專案內沒有場景物件需要手動擺放，執行時自己長出來（RuntimeInitializeOnLoadMethod）。
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        public static App I;

        public RectTransform CanvasRt, Safe, ScreenRoot, Layer, ToastLayer, Chrome;
        public Page Current;
        Canvas canvas; CanvasScaler scaler;
        Camera backdrop;
        int lastW, lastH; Rect lastSafe;
        float lastDensity;
        bool pageReflowPending;
        readonly List<Ui.ModalHandle> modals = new List<Ui.ModalHandle>();
        Text banner; RectTransform bannerBox;
        Button gearBtn, langBtn;

        // 共用狀態（跨畫面）
        public string LastRun = "solo";           // solo / challenge：結算後「再來一局」用
        public int AutoAdj = -1;
        public bool AiDetail;
        public Online Online;
        public bool InputSuspended { get; private set; }
        bool hasFocus = true, appPaused;

        void OnApplicationFocus(bool focused) { hasFocus = focused; RefreshInputSuspended(); }
        void OnApplicationPause(bool paused) { appPaused = paused; RefreshInputSuspended(); }

        void RefreshInputSuspended()
        {
            InputSuspended = !hasFocus || appPaused;
            if (InputSuspended && Current is GamePage game) game.ResetInput();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Snake3D.App");
            DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.mobileKeyboardSupport = true;   // 手機／平板點輸入框時叫出系統鍵盤
#endif
            Store.Load();
            Loc.Init(Store.D.lang, Application.systemLanguage.ToString());
            string injected = "";
            var ta = Resources.Load<TextAsset>("ServerUrl");
            if (ta != null) injected = ta.text.Trim();
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(injected)) injected = Environment.GetEnvironmentVariable("GAME_SERVER_URL") ?? "";
#endif
            Cfg.Resolve(injected, Platform.Href());

            BuildRoot();
            Sound.Init(gameObject);
            gameObject.AddComponent<Net>();
            Online = new Online(this);
            Loc.Changed += OnLang;
            Platform.SetPage(Loc.Lang, Loc.T("meta.title"));
            Sound.Music("menu");

            if (!string.IsNullOrEmpty(Cfg.QueryRoom) && !string.IsNullOrEmpty(Cfg.QueryInvite))
            {
                Online.Invite = new InviteState { Room = Cfg.QueryRoom.ToUpperInvariant(), Token = Cfg.QueryInvite };
                Go(new LobbyPage());
            }
            else Go(new HomePage());
        }

        // ---------- 根物件 ----------
        void BuildRoot()
        {
            var camGo = new GameObject("BackdropCamera");
            camGo.transform.SetParent(transform, false);
            backdrop = camGo.AddComponent<Camera>();
            backdrop.clearFlags = CameraClearFlags.SolidColor; backdrop.backgroundColor = Data.Hex("#ffeec2");
            backdrop.cullingMask = 0; backdrop.depth = -50;
            camGo.AddComponent<AudioListener>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
            }

            var cgo = new GameObject("Canvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            cgo.layer = 5;
            canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            scaler = cgo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            cgo.AddComponent<GraphicRaycaster>();
            CanvasRt = (RectTransform)cgo.transform;

            Safe = Ui.Obj("Safe", CanvasRt); Ui.Stretch(Safe);
            ScreenRoot = Ui.Obj("Screens", Safe); Ui.Stretch(ScreenRoot);
            Chrome = Ui.Obj("Chrome", Safe); Ui.Stretch(Chrome);      // 右上角按鈕與連線橫幅，永遠在畫面之上
            Layer = Ui.Obj("Modals", Safe); Ui.Stretch(Layer);
            ToastLayer = Ui.Obj("Toasts", Safe); Ui.Stretch(ToastLayer);

            // 右上角：語言與設定
            var row = Ui.HBox(Chrome, 10, 0, TextAnchor.MiddleRight);
            Ui.Anchor(row, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -12), new Vector2(140, 56));
            langBtn = Ui.IconBtn(row, "globe", () => { Sound.Sfx("click"); Loc.Set(Loc.Lang == Loc.ZH ? Loc.EN : Loc.ZH); }, "lang", 56, "sky");
            gearBtn = Ui.IconBtn(row, "gear", () => { Sound.Sfx("click"); SettingsModal.Open(); }, "settings", 56, "sun");

            bannerBox = Ui.Obj("Banner", Chrome);
            Ui.Anchor(bannerBox, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(560, 46));
            var bi = bannerBox.gameObject.AddComponent<Image>(); bi.sprite = Ui.Round(Data.Hex("#fff0b8"), Ui.Ink, 16, 3); bi.type = Image.Type.Sliced; bi.raycastTarget = false;
            banner = Ui.Label(bannerBox, "", 22, Ui.Ink, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            Ui.Stretch(banner.rectTransform, 12, 0, 12, 0);
            bannerBox.gameObject.SetActive(false);
            ApplyScale();
        }

        void ApplyScale()
        {
            int w = Screen.width, h = Screen.height;
            var sa = Screen.safeArea;
            float density = Platform.PixelScale;
            if (w == lastW && h == lastH && sa == lastSafe && density == lastDensity) return;
            bool wasWide = lastSafe.width / Mathf.Max(0.1f, UiScale) >= 900;
            bool wasLandscape = lastW > lastH;
            lastW = w; lastH = h; lastSafe = sa; lastDensity = density;
            // CSS 尺寸決定斷點；畫布像素倍率只處理高 DPI，不縮小手機的控制項。
            scaler.scaleFactor = Lay.Scale(sa.width, sa.height, density);
            if (w > 0 && h > 0)
            {
                Safe.anchorMin = new Vector2(sa.xMin / w, sa.yMin / h);
                Safe.anchorMax = new Vector2(sa.xMax / w, sa.yMax / h);
                Safe.offsetMin = Vector2.zero; Safe.offsetMax = Vector2.zero;
            }
            bannerBox.sizeDelta = new Vector2(Mathf.Min(560, Lay.LogicalW - 24), 46);
            if (Current != null && !(Current is GamePage) && (wasWide != Lay.Wide || wasLandscape != (w > h)))
                pageReflowPending = true;
        }

        public float UiScale { get { return scaler != null ? scaler.scaleFactor : 1f; } }

        // ---------- 每幀 ----------
        void Update()
        {
            ApplyScale();
            // 軟鍵盤可能改變 viewport／方向；不要在組字時銷毀正在輸入的欄位。
            if (pageReflowPending && !TextInputFocused()) Rebuild();
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
            if (Current != null) Current.Tick();
            if (modals.Count > 0) PruneModals();
            bool game = Current is GamePage;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = !game || ModalOpen;
        }

        static bool TextInputFocused()
        {
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return false;
            var input = es.currentSelectedGameObject.GetComponent<InputField>();
            return input != null && input.isFocused;
        }

        bool clearSel;
        void LateUpdate()
        {
            // 滑鼠／觸控點完按鈕後不要留著「鍵盤焦點」的黃色底，只有用鍵盤操作時才顯示
            if (Input.GetMouseButtonUp(0)) clearSel = true;
            for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).phase == TouchPhase.Ended) clearSel = true;
            if (clearSel && EventSystem.current != null)
            {
                clearSel = false;
                var selected = EventSystem.current.currentSelectedGameObject;
                // 點擊輸入欄後保留焦點，否則暱稱與聊天室會立刻失焦。
                if (selected == null || selected.GetComponent<InputField>() == null)
                    EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void HandleBack()
        {
            if (ModalOpen) { CloseTopModal(); return; }
            if (Current != null && Current.Back()) return;
        }

        // ---------- 畫面切換 ----------
        public void Go(Page p)
        {
            pageReflowPending = false;
            CloseAllModals();
            if (Current != null)
            {
                Current.Close();
                if (Current.Root != null) Destroy(Current.Root.gameObject);
            }
            Current = p;
            Net.SetOutageScreen(p is LobbyPage || p is RoomPage);
            p.Root = Ui.Obj("Page:" + p.GetType().Name, ScreenRoot);
            Ui.Stretch(p.Root);
            p.Build(p.Root);
            bool game = p is GamePage;
            Sound.Music(game ? "battle" : "menu");
            // 所有畫面都保留緊鄰的語言與設定入口。
            SetChromeButtons();
            if (!(Online != null && Online.NeedsNet(p))) { if (Net.I != null) Net.CloseConn(); }
            RefreshBanner();
        }

        public void Rebuild()
        {
            pageReflowPending = false;
            if (Current == null) return;
            var p = Current;
            var oldScroll = p.Root.GetComponentInChildren<ScrollRect>();
            float position = oldScroll != null ? oldScroll.verticalNormalizedPosition : 1f;
            for (int i = p.Root.childCount - 1; i >= 0; i--)
            {
                var child = p.Root.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            p.Build(p.Root);
            Canvas.ForceUpdateCanvases();
            var newScroll = p.Root.GetComponentInChildren<ScrollRect>();
            if (newScroll != null) newScroll.verticalNormalizedPosition = position;
        }

        void SetChromeButtons()
        {
            gearBtn.transform.parent.gameObject.SetActive(true);
            langBtn.gameObject.SetActive(true);
        }

        void OnLang()
        {
            Platform.SetPage(Loc.Lang, Loc.T("meta.title"));
            SettingsModal.OnLang();
            if (Current != null) Current.Relabel();
            RefreshBanner();
        }

        // ---------- 彈窗 ----------
        public bool ModalOpen { get { PruneModals(); return modals.Count > 0; } }

        void PruneModals() { modals.RemoveAll(m => m.Mask == null); }

        public Ui.ModalHandle Modal(string title, bool closable = true, float width = 560)
        {
            if (Current is GamePage game) game.ResetInput();
            var m = Ui.Modal(Layer, title, closable, width);
            modals.Add(m);
            return m;
        }

        public void CloseTopModal()
        {
            PruneModals();
            // 不越過不可關閉的頂層結算去關閉底下的彈窗。
            if (modals.Count > 0 && modals[modals.Count - 1].Closable)
                modals[modals.Count - 1].Close();
        }

        public void CloseAllModals()
        {
            foreach (var m in modals.ToArray()) m.Close();
            modals.Clear();
        }

        public Ui.ModalHandle ConfirmBox(string title, string text, string ok, Action onOk, bool danger = true)
        {
            if (Current is GamePage game) game.ResetInput();
            var m = Ui.Confirm(Layer, title, text, ok, onOk, danger ? "danger" : "primary");
            modals.Add(m);
            return m;
        }

        // ---------- 提示與連線橫幅 ----------
        public void Toast(string text, float seconds = 2.6f)
        {
            var rt = Ui.Obj("Toast", ToastLayer);
            Ui.Anchor(rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(Mathf.Min(560, Lay.LogicalW - 24), 20));
            var img = rt.gameObject.AddComponent<Image>(); img.sprite = Ui.Round(Data.Hex("#4a3320"), Data.Hex("#4a3320"), 16, 0); img.type = Image.Type.Sliced; img.raycastTarget = false;
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>(); g.padding = new RectOffset(18, 18, 12, 12); g.childControlWidth = true; g.childControlHeight = true;
            var f = rt.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Ui.Label(rt, text, 22, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StartCoroutine(KillLater(rt.gameObject, seconds));
        }

        IEnumerator KillLater(GameObject go, float s)
        {
            yield return new WaitForSecondsRealtime(s);
            if (go != null) Destroy(go);
        }

        string bannerText = ""; bool bannerOn;
        public void Banner(string text)
        {
            bannerText = text ?? ""; bannerOn = bannerText.Length > 0;
            if (bannerBox == null) return;
            bannerBox.gameObject.SetActive(bannerOn);
            banner.text = bannerText;
        }
        void RefreshBanner() { if (Online != null) Online.RepaintBanner(); }

        // ---------- 共用小工具 ----------
        public string MyName()
        {
            if (AutoAdj < 0) AutoAdj = UnityEngine.Random.Range(0, Names.ADJ_N);
            string n = (Store.D.nickname ?? "").Trim();
            return n.Length > 0 ? n : Names.Auto(AutoAdj, Store.D.animal);
        }

        public static void Vibrate(int ms) { if (Store.D.vibrate) Platform.Vibrate(ms); }
    }
}
