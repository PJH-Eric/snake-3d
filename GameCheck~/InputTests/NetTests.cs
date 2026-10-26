using System;
using System.Collections;
using System.Reflection;
using Snake3D;
using UnityEngine.Networking;

// 只手動推進正式 Net.Wake 的 IEnumerator；HTTP 邊界使用替身，不啟動 Unity 或真實網路。
static class NetTests
{
    static void Set(Net net, string field, object value)
    {
        typeof(Net).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(net, value);
    }

    static IEnumerator Wake(Net net, int session)
    {
        return (IEnumerator)typeof(Net).GetMethod("Wake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(net, new object[] { session });
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        var outage = new ConnectionOutageWindow();
        Check(!outage.ShouldShow(100), "單機／首頁不應出現線上提示");
        outage.Observe("connecting", 100);
        Check(!outage.ShouldShow(129.99), "30 秒前應繼續等待連線");
        outage.Observe("waking", 120);
        Check(outage.ShouldShow(130), "喚醒及重試不應重設 30 秒期限");
        Check(!outage.ShouldShow(200), "同一次故障不重複彈窗");
        outage.Observe("open", 201);
        Check(!outage.Active && !outage.ShouldShow(300), "恢復後清除提示");
        outage.Observe("connecting", 400);
        outage.Observe("idle", 410);
        Check(!outage.ShouldShow(500), "離開線上後取消提示");
        outage.Observe("connecting", 600);
        Check(outage.ShouldShow(630), "重新進入線上可以再次顯示提示");
        for (int month = 1; month <= 12; month++)
            Check(ConnectionOutageWindow.ResetMonth(new DateTime(2026, month, 28), false) == (month % 12 + 1).ToString(), "次月計算及跨年");
        Check(ConnectionOutageWindow.ResetMonth(new DateTime(2026, 12, 31), true) == "January", "英文跨年月份");
        Console.WriteLine("通過：30 秒斷線提示、冷啟動、取消、恢復、去重及 12 個月份邊界");

        var previousNet = Net.I;
        string previousStatus = Net.Status, previousUrl = Cfg.ServerUrl;
        int previousDetail = Net.StatusDetail;
        var previousGet = UnityWebRequest.GetForTest;
        try
        {
            foreach (bool reentered in new[] { false, true })
            {
                var net = new Net();
                Net.I = net;
                Cfg.ServerUrl = "https://example.invalid";
                Set(net, "wantOpen", true);
                Set(net, "session", 1);
                var response = new UnityWebRequest { result = UnityWebRequest.Result.InProgress };
                UnityWebRequest.GetForTest = url => response;
                var wake = Wake(net, 1);
                try
                {
                    Check(wake.MoveNext(), "健康檢查應先等待回應");
                    Net.CloseConn();
                    if (reentered)
                    {
                        Set(net, "wantOpen", true);
                        Set(net, "session", 3);
                        Net.Status = "connecting";
                    }
                    response.result = UnityWebRequest.Result.ConnectionError;
                    Check(!wake.MoveNext(), "離開大廳或重新進入後，舊健康檢查必須直接結束");
                    Check(Net.Status == (reentered ? "connecting" : "idle"), "舊請求不得覆寫新連線或單機狀態");
                }
                finally { (wake as IDisposable)?.Dispose(); }
            }

            var active = new Net();
            Net.I = active;
            Set(active, "wantOpen", true);
            Set(active, "session", 1);
            UnityWebRequest.GetForTest = url => new UnityWebRequest { result = UnityWebRequest.Result.ConnectionError };
            var current = Wake(active, 1);
            try
            {
                Check(current.MoveNext() && current.MoveNext(), "有效連線健康檢查失敗後仍應等待重試");
                Check(Net.Status == "waking", "有效連線必須顯示喚醒狀態");
            }
            finally { (current as IDisposable)?.Dispose(); }
            Console.WriteLine("通過：舊健康檢查在離開／重新進入後退出，有效連線仍保留喚醒重試（HTTP 替身，非 Unity 網路測試）");
        }
        finally
        {
            UnityWebRequest.GetForTest = previousGet;
            Net.I = previousNet;
            Net.Status = previousStatus;
            Net.StatusDetail = previousDetail;
            Cfg.ServerUrl = previousUrl;
        }
    }
}
