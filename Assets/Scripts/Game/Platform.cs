using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace Snake3D
{
    /// <summary>
    /// 平台橋：WebGL 走 .jslib（瀏覽器 WebSocket、剪貼簿、分享、震動），編輯器／桌機走 .NET 的 ClientWebSocket。
    /// </summary>
    public static class Platform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SnakeWsOpen(string url);
        [DllImport("__Internal")] static extern int SnakeWsState(int id);
        [DllImport("__Internal")] static extern int SnakeWsSend(int id, string msg);
        [DllImport("__Internal")] static extern void SnakeWsClose(int id);
        [DllImport("__Internal")] static extern int SnakeWsPoll(int id, byte[] buf, int cap);
        [DllImport("__Internal")] static extern IntPtr SnakeGetHref();
        [DllImport("__Internal")] static extern void SnakeReplaceUrl(string path);
        [DllImport("__Internal")] static extern int SnakeCopyText(string text);
        [DllImport("__Internal")] static extern int SnakeCanShare();
        [DllImport("__Internal")] static extern void SnakeShare(string title, string text, string url);
        [DllImport("__Internal")] static extern void SnakeVibrate(int ms);
        [DllImport("__Internal")] static extern void SnakeSetPage(string lang, string title);
        [DllImport("__Internal")] static extern int SnakeIsTouchDevice();

        public static string Href() { try { return Marshal.PtrToStringUTF8(SnakeGetHref()) ?? ""; } catch (Exception) { return ""; } }
        public static void ReplaceUrl(string path) { SnakeReplaceUrl(path); }
        public static bool Copy(string text) { return SnakeCopyText(text) == 1; }
        public static bool CanShare { get { return SnakeCanShare() == 1; } }
        public static void Share(string title, string text, string url) { SnakeShare(title, text, url); }
        public static void Vibrate(int ms) { SnakeVibrate(ms); }
        public static void SetPage(string lang, string title) { SnakeSetPage(lang, title); }
        public static bool TouchDevice { get { return SnakeIsTouchDevice() == 1; } }
#else
        public static string Href() { return ""; }
        public static void ReplaceUrl(string path) { }
        public static bool Copy(string text) { GUIUtility.systemCopyBuffer = text; return true; }
        public static bool CanShare { get { return false; } }
        public static void Share(string title, string text, string url) { }
        public static void Vibrate(int ms) {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
        public static void SetPage(string lang, string title) { }
        public static bool TouchDevice { get { return Input.touchSupported && Application.isMobilePlatform; } }
#endif
    }

    /// <summary>WebSocket 的最小介面：不用回呼，改成每幀輪詢事件（O 開啟／M 訊息／C 關閉／E 錯誤）。</summary>
    public abstract class SnakeSocket
    {
        public abstract bool Open { get; }
        public abstract bool Send(string text);
        public abstract void Close();
        /// <summary>取下一筆事件；沒有就回 null。</summary>
        public abstract string Poll();

        public static SnakeSocket Connect(string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebGlSocket(url);
#else
            return new NetSocket(url);
#endif
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    sealed class WebGlSocket : SnakeSocket
    {
        [DllImport("__Internal")] static extern int SnakeWsOpen(string url);
        [DllImport("__Internal")] static extern int SnakeWsState(int id);
        [DllImport("__Internal")] static extern int SnakeWsSend(int id, string msg);
        [DllImport("__Internal")] static extern void SnakeWsClose(int id);
        [DllImport("__Internal")] static extern int SnakeWsPoll(int id, byte[] buf, int cap);

        int id; byte[] buf = new byte[16384];
        public WebGlSocket(string url) { id = SnakeWsOpen(url); }
        public override bool Open { get { return id > 0 && SnakeWsState(id) == 1; } }
        public override bool Send(string text) { return id > 0 && SnakeWsSend(id, text) == 1; }
        public override void Close() { if (id > 0) SnakeWsClose(id); id = 0; }
        public override string Poll()
        {
            if (id <= 0) return null;
            int n = SnakeWsPoll(id, buf, buf.Length);
            if (n < 0) { buf = new byte[Math.Max(buf.Length * 2, -n + 16)]; n = SnakeWsPoll(id, buf, buf.Length); }
            if (n <= 0) return null;
            return Encoding.UTF8.GetString(buf, 0, n);
        }
    }
#else
    sealed class NetSocket : SnakeSocket
    {
        readonly ClientWebSocket ws = new ClientWebSocket();
        readonly ConcurrentQueue<string> q = new ConcurrentQueue<string>();
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        volatile bool open;
        Task chain = Task.CompletedTask;

        public NetSocket(string url) { Run(url); }

        async void Run(string url)
        {
            try
            {
                await ws.ConnectAsync(new Uri(url), cts.Token);
                open = true; q.Enqueue("O");
                var buf = new byte[8192];
                var sb = new StringBuilder();
                while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
                {
                    var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                    if (r.EndOfMessage) { q.Enqueue("M" + sb); sb.Length = 0; }
                }
            }
            catch (Exception) { q.Enqueue("E"); }
            open = false; q.Enqueue("C");
        }

        public override bool Open { get { return open; } }
        public override bool Send(string text)
        {
            if (!open || ws.State != WebSocketState.Open) return false;
            var data = Encoding.UTF8.GetBytes(text);
            // ClientWebSocket 同一時間只能有一個送出動作，所以串成佇列
            lock (this)
            {
                chain = chain.ContinueWith(_ => ws.State == WebSocketState.Open ? ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token) : Task.CompletedTask).Unwrap()
                    .ContinueWith(t => { var ignore = t.Exception; });
            }
            return true;
        }
        public override void Close() { open = false; try { cts.Cancel(); ws.Dispose(); } catch (Exception) { } }
        public override string Poll() { string s; return q.TryDequeue(out s) ? s : null; }
    }
#endif
}
