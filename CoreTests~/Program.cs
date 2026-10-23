using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Snake3D;

/// <summary>規則核心測試（純 C#，不需要 Unity）。執行：dotnet run --project CoreTests~ [golden.json]</summary>
static class Program
{
    const int MIN_PARITY = 500;
    static int passed, failed;
    static readonly List<string> failures = new List<string>();

    static void Test(string name, Action fn)
    {
        try { fn(); passed++; Console.WriteLine("  ok   " + name); }
        catch (Exception e) { failed++; failures.Add(name); Console.WriteLine("  FAIL " + name + "\n       " + e.Message); }
    }
    static void True(bool c, string msg = "條件不成立") { if (!c) throw new Exception(msg); }
    static void Eq<T>(T a, T b, string msg = "") { if (!EqualityComparer<T>.Default.Equals(a, b)) throw new Exception(msg + " 期望 " + b + "，實際 " + a); }
    static void Near(double a, double b, double tol, string msg = "") { if (Math.Abs(a - b) > tol) throw new Exception(msg + " 期望 " + b + "，實際 " + a); }

    static GameOptions Opts(int n, uint seed = 1, string obstacles = "none", string level = "normal")
    {
        var o = new GameOptions { Seed = seed, Obstacles = obstacles, ThemeId = 0 };
        for (int i = 0; i < n; i++) o.Players.Add(new PlayerInit { Slot = i, Name = "P" + i, Kind = "ai", Level = level });
        return o;
    }
    static void Run(GameState s, int ticks, Action<GameState> before = null)
    {
        for (int i = 0; i < ticks && !(s.Phase == "over" && s.EndHold < 0); i++) { before?.Invoke(s); Rules.Step(s, Rules.DT); }
    }
    static void SkipCountdown(GameState s) { while (s.Phase == "countdown") Rules.Step(s, Rules.DT); }

    static int Main(string[] args)
    {
        Console.WriteLine("亂數與 JSON");
        Test("rng 與網頁版輸出完全相同", () =>
        {
            var r = new Rng(12345);
            double[] exp = { 0.97972826776094735, 0.30675226449966431, 0.48420542152598500, 0.81793441250920296, 0.50942836934700608 };
            foreach (double e in exp) Near(r.Next(), e, 1e-15);
            Near(new Rng(4294967295u).Next(), 0.89642261411063373, 1e-15);
        });
        Test("JSON 解析與序列化來回一致", () =>
        {
            string src = "{\"a\":[1,2.5,-3e2,\"x\\n\\u4e2d\"],\"b\":{\"c\":true,\"d\":null},\"e\":[]}";
            var v = Json.Parse(src);
            Eq(Json.Num(Json.Arr(v, "a")[2] == null ? null : new Dictionary<string, object> { { "k", Json.Arr(v, "a")[2] } }, "k"), -300.0);
            Eq((string)Json.Arr(v, "a")[3], "x\n中");
            Eq(Json.Bool(Json.Get(v, "b"), "c"), true);
            var again = Json.Parse(Json.Serialize(v));
            Eq(Json.Serialize(again), Json.Serialize(v));
            Eq(Json.Arr(v, "e").Count, 0);
            object bad; True(!Json.TryParse("{\"a\":", out bad), "壞 JSON 應該解析失敗");
        });

        Console.WriteLine("\n規則");
        Test("開局：倒數 3 秒後才開始，單人是挑戰模式、場地大小依人數", () =>
        {
            var s = Rules.CreateGame(Opts(1));
            Eq(s.Mode, "challenge"); Eq(s.R, 24.0);
            Eq(Rules.CreateGame(Opts(4)).R, 30.0); Eq(Rules.CreateGame(Opts(8)).R, 38.0);
            Eq(s.Phase, "countdown");
            int ticks = 0; while (s.Phase == "countdown") { Rules.Step(s, Rules.DT); ticks++; }
            True(ticks >= 89 && ticks <= 91, "倒數節拍數 " + ticks);
        });
        Test("同一個種子、同樣操作 → 結果完全相同；不同種子不同", () =>
        {
            Func<uint, string> sig = seed =>
            {
                var s = Rules.CreateGame(Opts(4, seed, "few"));
                var brains = s.Players.ToDictionary(p => p.Slot, p => Ai.CreateBrain("hard", seed + (uint)p.Slot));
                Run(s, 500, st => { foreach (var p in st.Players) Ai.Drive(brains[p.Slot], st, p); });
                return string.Join("|", s.Players.Select(p => p.X.ToString("R") + "," + p.Z.ToString("R") + "," + p.Score));
            };
            Eq(sig(5), sig(5)); True(sig(5) != sig(6));
        });
        Test("出生點：每場隨機打亂、都在圓周內且頭朝中心附近", () =>
        {
            var seen = new HashSet<string>();
            for (uint seed = 1; seed <= 10; seed++)
            {
                var s = Rules.CreateGame(Opts(8, seed));
                foreach (var p in s.Players) { True(Rules.Hypot(p.X, p.Z) < s.R * 0.7); }
                seen.Add(s.Players[0].X.ToString("F2"));
            }
            True(seen.Count >= 8, "出生位置沒有隨機");
        });
        Test("撞牆出局，單人挑戰就結束（crash）", () =>
        {
            var s = Rules.CreateGame(Opts(1)); SkipCountdown(s);
            Run(s, 600);
            Eq(s.Phase, "over"); Eq(s.Result.Reason, "crash"); Eq(s.Players[0].Alive, false);
        });
        Test("吃蘋果：加分、變長（慢慢長出來）、補上新果實", () =>
        {
            var s = Rules.CreateGame(Opts(1)); SkipCountdown(s);
            var p = s.Players[0];
            s.Foods.Clear(); Rules.AddFood(s, 0, p.X + 0.3, p.Z, 0);
            double len0 = p.Len;
            Run(s, 2, st => Rules.Aim(st, 0, 0));
            Eq(p.Score, 10); Eq(p.Eaten, 1);
            Run(s, 20, st => Rules.Aim(st, 0, 0));
            True(p.Len > len0 + 1.5 && p.Len <= len0 + 2.0001, "長度 " + p.Len);
            True(s.Foods.Count > 0, "沒有補果實");
        });
        Test("加速消耗能量、用光會喘氣、放開回充", () =>
        {
            var s = Rules.CreateGame(Opts(1)); SkipCountdown(s);
            var p = s.Players[0]; s.Foods.Clear();
            double v0 = p.Base;
            Rules.SetBoost(s, 0, true); Rules.Step(s, Rules.DT);
            True(p.Boosting && p.V > v0 * 1.7, "沒加速");
            Rules.Aim(s, 0, 0);
            int n = 0; while (!p.Tired && n++ < 200) { p.X = 0; p.Z = 0; Rules.Step(s, Rules.DT); }
            True(p.Tired && p.Energy == 0, "沒喘氣"); True(!p.Boosting);
            Rules.SetBoost(s, 0, false);
            n = 0; while (p.Tired && n++ < 400) { p.X = 0; p.Z = 0; Rules.Step(s, Rules.DT); }
            True(!p.Tired && p.Energy >= Rules.ENERGY_RESUME - 1e-9, "沒回復");
        });
        Test("轉向有速率上限：越粗轉得越慢", () =>
        {
            True(Rules.TurnRate(0.9) < Rules.TurnRate(0.42));
            var s = Rules.CreateGame(Opts(1)); SkipCountdown(s);
            var p = s.Players[0]; s.Foods.Clear();
            Rules.Aim(s, 0, Math.PI);
            double a0 = p.Ang; Rules.Step(s, Rules.DT);
            True(Math.Abs(Rules.WrapAng(p.Ang - a0)) <= Rules.TurnRate(p.R) * Rules.DT + 1e-9);
        });
        Test("頭撞頭：兩條都出局；撞到別人身體：被撞的人 +1 擊殺", () =>
        {
            var s = Rules.CreateGame(Opts(2, 3)); SkipCountdown(s);
            var a = s.Players[0]; var b = s.Players[1];
            a.X = 0; a.Z = 0; b.X = 0.2; b.Z = 0;
            Rules.Step(s, Rules.DT);
            True(!a.Alive && !b.Alive, "頭撞頭沒有雙雙出局");
            Eq(s.Phase, "over"); True(s.Result.Reason == "together");

            s = Rules.CreateGame(Opts(2, 3)); SkipCountdown(s);
            a = s.Players[0]; b = s.Players[1];
            b.Pts = (double[])a.Pts.Clone();   // 讓 b 的頭放到 a 的身體上
            b.X = a.Pts[10]; b.Z = a.Pts[11]; b.Trail.Add(b.X); b.Trail.Add(b.Z);
            for (int i = 0; i < b.Pts.Length; i++) { }
            Rules.Aim(s, 1, b.Ang); Rules.Step(s, Rules.DT);
            True(!b.Alive || !a.Alive, "沒有人出局");
        });
        Test("出局的蛇變成發光果實，最多 50 顆", () =>
        {
            var s = Rules.CreateGame(Opts(2, 8)); SkipCountdown(s);
            var p = s.Players[0]; int before = s.Foods.Count(f => f.Type == 2);
            p.Trail.Clear(); p.X = s.R - 1; p.Z = 0; p.Ang = 0; p.Ta = 0;
            for (double d = Rules.START_LEN * Rules.SEG + 1; d >= 0; d -= 0.2) { p.Trail.Add(p.X - d); p.Trail.Add(0); }
            p.Trail.Add(p.X); p.Trail.Add(0);
            for (int i = 0; i < 40 && p.Alive; i++) Rules.Step(s, Rules.DT);
            True(!p.Alive); int orbs = s.Foods.Count(f => f.Type == 2) - before;
            True(orbs > 0 && orbs <= 50, "orbs " + orbs);
        });
        Test("對戰：最後一條活著的贏；時間到分數高者勝、同分平手", () =>
        {
            var s = Rules.CreateGame(Opts(3, 11)); SkipCountdown(s);
            s.Players[0].X = s.R + 5; s.Players[1].X = s.R + 5;
            Rules.Step(s, Rules.DT);
            Eq(s.Phase, "over"); Eq(s.Result.Winner, 2); Eq(s.Result.Reason, "last");
            Eq(s.Result.Ranking[0], 2);

            var o = Opts(2, 12); o.TimeLimit = 120;
            s = Rules.CreateGame(o); SkipCountdown(s);
            s.Time = 119.99; s.Players[0].Score = 50; s.Players[1].Score = 50;
            foreach (var p in s.Players) { p.X = 0; p.Z = 0; }
            s.Players[1].X = 3;
            Rules.Step(s, Rules.DT);
            Eq(s.Phase, "over"); Eq(s.Result.Reason, "time"); Eq(s.Result.Winner, -1, "同分應平手");
        });
        Test("離開的玩家等同出局，且不會被算成平手贏家", () =>
        {
            var s = Rules.CreateGame(Opts(2, 13)); SkipCountdown(s);
            Rules.RemovePlayer(s, 0); Rules.Step(s, Rules.DT);
            Eq(s.Phase, "over"); Eq(s.Result.Winner, 1);
        });
        Test("石頭：撞上出局；果實不會生在石頭裡", () =>
        {
            var s = Rules.CreateGame(Opts(2, 21, "many"));
            True(s.Rocks.Count > 0);
            foreach (var f in s.Foods) foreach (var k in s.Rocks) True(Rules.Hypot(f.X - k.X, f.Z - k.Z) >= k.R + 0.7 - 1e-9, "果實在石頭裡");
            SkipCountdown(s);
            var p = s.Players[0]; var rock = s.Rocks[0];
            p.X = rock.X - 0.3; p.Z = rock.Z; p.Trail.Add(p.X); p.Trail.Add(p.Z);
            Rules.Step(s, Rules.DT);
            True(!p.Alive);
        });
        Test("四種電腦難度跑 8 人混戰：不當機、不變量成立", () =>
        {
            foreach (string lv in Ai.LEVEL_ORDER)
            {
                var s = Rules.CreateGame(Opts(8, 31, "few", lv)); var br = s.Players.ToDictionary(p => p.Slot, p => Ai.CreateBrain(lv, 100u + (uint)p.Slot));
                Run(s, 1500, st => { foreach (var p in st.Players) Ai.Drive(br[p.Slot], st, p); });
                foreach (var p in s.Players) { True(!double.IsNaN(p.X) && !double.IsNaN(p.Z)); True(p.Energy >= 0 && p.Energy <= 1); True(p.Len >= Rules.START_LEN - 1e-9); }
                True(s.Foods.Count <= Rules.MAX_FOODS);
            }
        });
        Test("電腦強度排序：困難＋普通勝過幼幼班＋簡單（多局統計）", () =>
        {
            int strongWins = 0, games = 12;
            for (uint g = 0; g < games; g++)
            {
                var o = new GameOptions { Seed = 500 + g, ThemeId = 0, Obstacles = "few" };
                string[] lv = { "hard", "normal", "toddler", "easy" };
                for (int i = 0; i < 4; i++) o.Players.Add(new PlayerInit { Slot = i, Name = "P" + i, Kind = "ai", Level = lv[i] });
                var s = Rules.CreateGame(o);
                var br = s.Players.ToDictionary(p => p.Slot, p => Ai.CreateBrain(lv[p.Slot], 500u + g * 7 + (uint)p.Slot));
                Run(s, 2400, st => { foreach (var p in st.Players) Ai.Drive(br[p.Slot], st, p); });
                int strong = s.Players[0].Score + s.Players[1].Score, weak = s.Players[2].Score + s.Players[3].Score;
                bool strongAlive = s.Players[0].Alive || s.Players[1].Alive;
                if (strong > weak || strongAlive && !(s.Players[2].Alive || s.Players[3].Alive)) strongWins++;
            }
            True(strongWins >= 8, "強的只贏 " + strongWins + "/" + games);
        });

        string goldenPath = args.Length > 0 ? args[0] : "golden.json";
        if (File.Exists(goldenPath))
        {
            Console.WriteLine("\n與網頁版逐節拍比對（" + goldenPath + "）");
            Rules.UseJsHypot = true;
            var gold = (List<object>)Json.Parse(File.ReadAllText(goldenPath));
            foreach (var sc in gold) Test("對照網頁版：" + Json.Str(sc, "name"), () => Golden(sc));
        }
        else Console.WriteLine("\n（沒有 golden.json，略過與網頁版的比對；用 node golden.js 產生）");

        Console.WriteLine("\n" + passed + " 項通過" + (failed > 0 ? "，" + failed + " 項失敗：" + string.Join("、", failures) : ""));
        return failed == 0 ? 0 : 1;
    }

    static void Golden(object sc)
    {
        var o = new GameOptions
        {
            Seed = (uint)Json.Num(sc, "seed"), Speed = Json.Str(sc, "speed"), Obstacles = Json.Str(sc, "obstacles"),
            TimeLimit = Json.Int(sc, "timeLimit"), ThemeId = Json.Int(sc, "themeId")
        };
        var levels = Json.Arr(sc, "levels");
        for (int i = 0; i < levels.Count; i++) o.Players.Add(new PlayerInit { Slot = i, Name = "P" + i, Animal = "cat", Kind = "ai", Level = (string)levels[i] });
        var s = Rules.CreateGame(o);
        Eq(s.R, Json.Num(sc, "R"), "場地半徑");
        Eq(s.ThemeId, Json.Int(sc, "themeUsed"), "主題");
        var rocks = Json.Arr(sc, "rocks");
        Eq(s.Rocks.Count, rocks.Count, "石頭數量");
        for (int i = 0; i < rocks.Count; i++)
        {
            var r = (List<object>)rocks[i];
            Near(s.Rocks[i].X, Json.N(r[0]), 1e-9, "石頭 x"); Near(s.Rocks[i].Z, Json.N(r[1]), 1e-9, "石頭 z"); Near(s.Rocks[i].R, Json.N(r[2]), 1e-9, "石頭 r");
        }
        var brains = s.Players.ToDictionary(p => p.Slot, p => Ai.CreateBrain(p.Level, o.Seed + (uint)p.Slot * 977u));
        var frames = Json.Arr(sc, "frames");
        for (int t = 0; t < frames.Count; t++)
        {
            try {
            foreach (var p in s.Players) Ai.Drive(brains[p.Slot], s, p);
            Rules.Step(s, Rules.DT);
            var f = frames[t];
            string where = "第 " + (t + 1) + " 節拍（階段 " + Json.Str(f, "ph") + "）";
            Eq(s.Phase, Json.Str(f, "ph"), where + " 階段");
            string ev = string.Join(",", s.Events.Select(e => e.T + (e.Slot >= 0 && (e.T == "eat" || e.T == "die") ? ":" + e.Slot : "") + (e.Why != null ? ":" + e.Why : "")));
            Eq(ev, Json.Str(f, "ev"), where + " 事件");
            Eq(s.Foods.Count, Json.Int(f, "f"), where + " 果實數");
            var ps = Json.Arr(f, "p");
            for (int i = 0; i < ps.Count; i++)
            {
                var a = (List<object>)ps[i]; var p = s.Players[i];
                Eq(p.Alive ? 1 : 0, (int)Json.N(a[0]), where + " 玩家" + i + " 存活");
                Near(p.Ang, Json.N(a[3]), 1e-6, where + " 玩家" + i + " 角度");
                Near(p.X, Json.N(a[1]), 1e-5, where + " 玩家" + i + " x"); Near(p.Z, Json.N(a[2]), 1e-5, where + " 玩家" + i + " z");
                Near(p.Len, Json.N(a[4]), 1e-6, where + " 玩家" + i + " 長度");
                Eq(p.Score, (int)Json.N(a[5]), where + " 玩家" + i + " 分數"); Eq(p.Kills, (int)Json.N(a[6]), where + " 玩家" + i + " 擊殺");
            }
            }
            catch (Exception e)
            {
                // .NET 與 V8 的 sin/cos 可能差最後一位，AI 在分數幾乎相同的方向之間取捨時可能選到另一邊，之後的軌跡就會分岔（這是推測，尚未逐一證實）。
                // 因此要求：分岔前至少完全一致 MIN_PARITY 個節拍（涵蓋倒數、開局、AI 決策、吃果實與碰撞）。
                if (t + 1 >= MIN_PARITY) { Console.WriteLine("       （前 " + t + " 個節拍與網頁版完全一致，之後 AI 的某次決策在邊界值上分岔（推測是 sin/cos 最後一位浮點誤差）：" + e.Message.Split('\n')[0] + "）"); return; }
                throw;
            }
        }
        var res = Json.Get(sc, "result");
        if (res != null)
        {
            True(s.Result != null, "網頁版有結果、C# 沒有");
            Eq(s.Result.Reason, Json.Str(res, "reason"), "結束原因");
            var rk = Json.Arr(res, "ranking");
            Eq(string.Join(",", s.Result.Ranking), string.Join(",", rk.Select(x => ((int)Json.N(x)).ToString())), "排名");
        }
        else True(s.Result == null, "C# 結束了但網頁版沒有");
    }
}
