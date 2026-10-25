using System;
using Snake3D;

static class ConfigTests
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        foreach (string invalid in new[]
        {
            "https://", "https://bad host.invalid", "ftp://example.invalid",
            "https://example.invalid?mode=game", "https://example.invalid#game", "https://user:pass@example.invalid"
        })
        {
            Cfg.Resolve(invalid, "https://play.example.invalid/game/");
            Check(Cfg.Status == "invalid" && Cfg.ServerUrl == null, "錯誤伺服器網址應立即拒絕：" + invalid);
        }

        Cfg.Resolve(" HTTPS://Example.Invalid/service/ ", "https://play.example.invalid/game/");
        Check(Cfg.Status == "ok" && Cfg.ServerUrl == "https://example.invalid/service", "大小寫與尾斜線應正規化並保留服務路徑");
        Check(Cfg.WsUrl(Cfg.ServerUrl) == "wss://example.invalid/service/ws", "正規化的 HTTPS 應使用 WSS");
        Check(Cfg.WsUrl("HTTPS://example.invalid/service/") == "wss://example.invalid/service/ws", "直接傳入大寫 HTTPS 也應使用 WSS");
        Cfg.Resolve("http://example.invalid", "https://play.example.invalid/game/");
        Check(Cfg.Status == "invalid" && Cfg.Error == "cfg.mixed", "HTTPS 頁面必須拒絕 HTTP 後端");
        Cfg.Resolve("http://[::1]:3130/service/", "http://localhost/game/");
        Check(Cfg.Status == "ok" && Cfg.WsUrl(Cfg.ServerUrl) == "ws://[::1]:3130/service/ws", "HTTP、IPv6、連接埠與服務路徑應保留");
        Cfg.Resolve("", "HTTPS://Play.Example.Invalid/game/");
        Check(Cfg.Status == "ok" && Cfg.ServerUrl == "https://play.example.invalid" && Cfg.Source == "same-origin", "同源網址應接受大小寫並只保留 authority");
        Cfg.Resolve("", "");
        Check(Cfg.Status == "unset" && Cfg.ServerUrl == null, "沒有網址時應維持單機，不回退 localhost");
        Console.WriteLine("通過：伺服器網址格式、大小寫、混合內容、IPv6／連接埠／子路徑與單機 fallback（純 C#）");
    }
}
