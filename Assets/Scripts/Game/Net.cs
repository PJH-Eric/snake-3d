using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Snake3D
{
    /// <summary>
    /// 全站唯一的 server URL 入口（對應網頁版 config.js）。優先序：
    /// 1. 網址參數 ?server=https://example.com　2. 建置時注入（Resources/ServerUrl.txt，由建置腳本依 GAME_SERVER_URL 寫入）
    /// 3. WebGL 頁面本身就是伺服器發出來的 → 同源　4. 都沒有 → 只能玩單機。不做 localhost 回退。
    /// </summary>
    public static class Cfg
    {
        public static string ServerUrl;     // 不含結尾斜線
        public static string Source = "none";
        public static string Status = "unset";   // ok / unset / invalid
        public static string Error;              // 本地化 key
        public static string QueryRoom, QueryInvite;

        static string Query(string href, string key)
        {
            int q = href.IndexOf('?');
            if (q < 0) return "";
            string s = href.Substring(q + 1);
            int h = s.IndexOf('#'); if (h >= 0) s = s.Substring(0, h);
            foreach (var part in s.Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq < 0) continue;
                if (part.Substring(0, eq) == key) return UnityWebRequest.UnEscapeURL(part.Substring(eq + 1));
            }
            return "";
        }

        public static void Resolve(string injected, string href)
        {
            string query = Query(href, "server");
            string raw = (query.Length > 0 ? query : (injected ?? "")).Trim();
            string source = query.Length > 0 ? "query" : "injected";
            QueryRoom = Query(href, "room"); QueryInvite = Query(href, "invite");
            Error = null;
            if (raw.Length == 0)
            {
                string origin = Origin(href);
                if (origin != null) { ServerUrl = origin; Source = "same-origin"; Status = "ok"; return; }
                ServerUrl = null; Source = "none"; Status = "unset"; return;
            }
            if (!TryHttpUri(raw, out var uri) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            { ServerUrl = null; Source = source; Status = "invalid"; Error = "cfg.notAbs"; return; }
            if (href.StartsWith("https:", StringComparison.OrdinalIgnoreCase) && uri.Scheme == Uri.UriSchemeHttp)
            { ServerUrl = null; Source = source; Status = "invalid"; Error = "cfg.mixed"; return; }
            ServerUrl = uri.AbsoluteUri.TrimEnd('/'); Source = source; Status = "ok";
        }

        static bool TryHttpUri(string value, out Uri uri)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Host.Length > 0
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        static string Origin(string href)
        {
            return TryHttpUri(href, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : null;
        }

        public static string WsUrl(string baseUrl)
        {
            string u = baseUrl.TrimEnd('/');
            if (u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) u = "wss://" + u.Substring(8);
            else if (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) u = "ws://" + u.Substring(7);
            return u + "/ws";
        }
    }

    /// <summary>只在線上連線持續 30 秒未成功時提示；恢復或離開後重設。</summary>
    public sealed class ConnectionOutageWindow
    {
        double started = -1;
        bool shown;
        public bool Active { get { return started >= 0; } }

        public void Observe(string status, double now)
        {
            if (status == "idle" || status == "unset" || status == "open")
            {
                started = -1;
                shown = false;
            }
            else if (!Active && (status == "connecting" || status == "waking" || status == "retrying")) started = now;
        }

        public bool ShouldShow(double now)
        {
            if (!Active || shown || now - started < 30) return false;
            shown = true;
            return true;
        }

        public static string ResetMonth(DateTime now, bool english)
        {
            var next = now.AddMonths(1);
            return english ? next.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture) : next.Month.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>連線層（對應網頁版 net.js）：先打 /health 喚醒冷啟動的伺服器，再連 WebSocket，斷線自動重連。</summary>
    public sealed class Net : MonoBehaviour
    {
        public static Net I;
        public static string Status = "idle";   // idle | waking | connecting | open | retrying | offline | unset
        public static int StatusDetail;
        public static event Action<string, int> StatusChanged;

        SnakeSocket sock;
        bool wantOpen; int retry; float retryAt = -1; int session;
        string helloName = "", helloAnimal = "cat";
        readonly ConnectionOutageWindow outage = new ConnectionOutageWindow();
        Ui.ModalHandle outageModal;
        readonly Dictionary<string, List<Action<object>>> handlers = new Dictionary<string, List<Action<object>>>();

        public static bool Connected { get { return I != null && I.sock != null && I.sock.Open; } }

        void Awake() { I = this; }

        public static void On(string type, Action<object> fn)
        {
            List<Action<object>> l;
            if (!I.handlers.TryGetValue(type, out l)) { l = new List<Action<object>>(); I.handlers[type] = l; }
            l.Add(fn);
        }

        public static void SetProfile(string name, string animal) { I.helloName = name; I.helloAnimal = animal; }

        static void SetStatus(string s, int detail = 0)
        {
            Status = s; StatusDetail = detail;
            if (I != null)
            {
                I.outage.Observe(s, Time.realtimeSinceStartup);
                if (!I.outage.Active && I.outageModal != null) { I.outageModal.Close(); I.outageModal = null; }
            }
            if (StatusChanged != null) StatusChanged(s, detail);
        }

        public static void OpenConn(string name, string animal)
        {
            if (name != null) { I.helloName = name; I.helloAnimal = animal; }
            I.wantOpen = true;
            if (Cfg.Status != "ok" || string.IsNullOrEmpty(Cfg.ServerUrl)) { SetStatus("unset"); return; }
            if (I.sock != null) return;
            if (Status == "waking" || Status == "connecting") return;
            I.StartCoroutine(I.Wake(++I.session));
        }

        public static void CloseConn()
        {
            if (I == null) return;
            I.wantOpen = false; I.session++; I.retryAt = -1;
            if (I.sock != null) { var s = I.sock; I.sock = null; s.Close(); }
            SetStatus("idle");
        }

        public static bool Send(Dictionary<string, object> msg)
        {
            if (I == null || I.sock == null || !I.sock.Open) return false;
            return I.sock.Send(Json.Serialize(msg));
        }

        public static Dictionary<string, object> Msg(string type, params object[] kv)
        {
            var d = new Dictionary<string, object> { { "type", type } };
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        IEnumerator Wake(int mySession)
        {
            SetStatus("connecting");
            float started = Time.realtimeSinceStartup;
            while (wantOpen && mySession == session)
            {
                using (var req = UnityWebRequest.Get(Cfg.ServerUrl + "/health"))
                {
                    req.timeout = 8;
                    yield return req.SendWebRequest();
                    // 離開或重連後，舊請求即使失敗也不能覆寫新 session 的狀態。
                    if (!wantOpen || mySession != session) yield break;
                    if (req.result == UnityWebRequest.Result.Success) { Connect(); yield break; }
                }
                if (Time.realtimeSinceStartup - started > 90) break;
                SetStatus("waking", Mathf.RoundToInt(Time.realtimeSinceStartup - started));
                yield return new WaitForSecondsRealtime(2.5f);
            }
            if (wantOpen && mySession == session) { SetStatus("offline"); ScheduleRetry(); }
        }

        void Connect()
        {
            sock = SnakeSocket.Connect(Cfg.WsUrl(Cfg.ServerUrl));
            SetStatus("connecting");
        }

        void ScheduleRetry()
        {
            if (!wantOpen) return;
            float wait = Mathf.Min(8f, 0.6f * Mathf.Pow(1.6f, retry++));
            retryAt = Time.realtimeSinceStartup + wait;
        }

        void Update()
        {
            if (App.I != null && outage.ShouldShow(Time.realtimeSinceStartup))
            {
                var modal = App.I.Modal(Loc.T("net.outage.title"), true, 560);
                outageModal = modal;
                Ui.Label(modal.Body, Loc.T("net.outage.message", "month", ConnectionOutageWindow.ResetMonth(DateTime.Now, Loc.Lang == Loc.EN)), 24, Ui.Ink);
                Ui.Btn(modal.Body, Loc.T("common.ok"), () => modal.Close(), "primary", 160, 56, 22);
            }
            if (retryAt > 0 && Time.realtimeSinceStartup >= retryAt) { retryAt = -1; if (wantOpen && sock == null) OpenConn(null, null); }
            if (sock == null) return;
            var me = sock;
            for (int guard = 0; guard < 200 && sock == me; guard++)
            {
                string ev = me.Poll();
                if (ev == null) break;
                char c = ev[0];
                if (c == 'O')
                {
                    retry = 0;
                    me.Send(Json.Serialize(Msg("hello", "key", Store.ClientKey(), "name", helloName, "animal", helloAnimal)));
                    SetStatus("open");
                }
                else if (c == 'M') Dispatch(ev.Substring(1));
                else if (c == 'C')
                {
                    if (sock != me) break;
                    sock = null; me.Close();
                    if (wantOpen) { SetStatus("retrying"); ScheduleRetry(); } else SetStatus("idle");
                }
            }
        }

        void Dispatch(string text)
        {
            object msg;
            if (!Json.TryParse(text, out msg)) return;
            string type = Json.Str(msg, "type");
            if (type == "welcome")
            {
                string k = Json.Str(msg, "key");
                if (k.Length > 0) Store.SetClientKey(k);
            }
            if (type == "replaced") wantOpen = false;
            List<Action<object>> l;
            if (handlers.TryGetValue(type, out l)) foreach (var fn in l.ToArray()) fn(msg);
        }

        void OnDestroy() { if (sock != null) sock.Close(); }
    }
}
