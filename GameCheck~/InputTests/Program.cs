using System;
using System.Collections.Generic;
using System.Reflection;
using Snake3D;
using UnityEngine.EventSystems;

// 只測試不呼叫 Unity 引擎的輸入狀態與回呼；不代表 Play Mode/UI 驗證。
static class Program
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    static T Get<T>(object target, string field)
    {
        return (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    static int Main()
    {
        var changes = new List<bool>();
        var hold = new HoldButton { Changed = (held, id) => changes.Add(held) };
        var first = new PointerEventData(null) { pointerId = 1 };
        var second = new PointerEventData(null) { pointerId = 2 };
        hold.OnPointerDown(new PointerEventData(null) { pointerId = -1 });
        Check(!hold.Held && changes.Count == 0, "滑鼠不能啟動觸控加速");
        hold.OnPointerDown(first);
        hold.OnPointerDown(second);
        hold.OnPointerUp(second);
        Check(hold.Held && changes.Count == 1, "第二根手指不得取代或釋放持有者");
        hold.OnPointerUp(first);
        Check(!hold.Held && changes.Count == 2 && !changes[1], "原手指放開必須停止加速");
        hold.OnPointerDown(second);
        hold.Reset();
        hold.Reset();
        Check(!hold.Held && changes.Count == 4, "重置必須釋放且不可重複發送");
        hold.OnPointerDown(first);
        hold.OnPointerExit(first);
        Check(!hold.Held && changes.Count == 6, "移出控制區必須釋放");

        var options = new GameOptions { Seed = 42, Obstacles = "none", ThemeId = 0 };
        options.Players.Add(new PlayerInit { Slot = 0, Kind = "human", Name = "P" });
        var state = Rules.CreateGame(options);
        while (state.Phase == "countdown") Rules.Step(state, Rules.DT);
        var game = new GamePage(GamePage.Kind.Challenge, state, new Dictionary<int, Brain>());
        Rules.SetBoost(state, 0, true);
        Set(game, "boostKeys", true);
        Set(game, "boostTouch", true);
        Set(game, "boostOn", true);
        Set(game, "pendingAim", 1.2);
        Set(game, "stickId", 7);
        game.ResetInput();
        Check(!Get<bool>(game, "boostOn") && !Get<bool>(game, "boostTouch") && !Get<bool>(game, "boostKeys"), "暫停必須清除所有加速來源");
        Check(double.IsNaN(Get<double>(game, "pendingAim")) && Get<int>(game, "stickId") == int.MinValue, "暫停必須清除待送方向與手指");
        Rules.Step(state, Rules.DT);
        Check(!state.Players[0].Boosting, "恢復後規則不得繼續加速");
        game.ResetInput();
        foreach (var size in new[] { (320f, 568f), (390f, 844f), (844f, 390f), (768f, 1024f), (1024f, 768f), (1440f, 900f) })
        {
            float scale = Lay.Scale(size.Item1, size.Item2, 1);
            float retina = Lay.Scale(size.Item1 * 2, size.Item2 * 2, 2);
            Check(Math.Abs(retina - scale * 2) < 0.001f, "相同 CSS 視窗在高 DPI 必須使用相同邏輯版面");
            Check(56 * scale >= 44, "主要觸控控制不可因窄螢幕縮小至 44 CSS px 以下");
            bool wide = size.Item1 / scale >= 900;
            Check(wide == (size.Item1 >= 1024), "手機、平板與桌機斷點錯誤");
        }
        Console.WriteLine("通過：多指持有、放開、移出、冪等重置、暫停輸入清理與恢復加速狀態（純 C#，未執行 Unity UI）");
        Console.WriteLine("通過：6 種 CSS 視窗與 1x/2x DPI 的縮放及斷點計算（未渲染畫面）");
        return 0;
    }
}
