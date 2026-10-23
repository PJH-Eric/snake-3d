using System;
using System.Collections.Generic;

namespace Snake3D
{
    /// <summary>顯示用文字：選項名稱、玩家名稱（電腦玩家用動物名）。</summary>
    public static class Names
    {
        public const int ADJ_N = 10;

        public static string Animal(string id) { return Loc.T("animal." + id); }
        public static string Speed(string id) { return Loc.T("speed." + id); }
        public static string Obstacle(string id) { return Loc.T("obs." + id); }
        public static string Theme(int id) { return id < 0 ? Loc.T("opt.random") : Loc.T("theme." + id); }
        public static string Level(string id) { return Loc.T("level." + id + ".name"); }
        public static string LevelHint(string id) { return Loc.T("level." + id + ".hint"); }
        public static string Time(int sec) { return sec > 0 ? Loc.T("opt.min", "n", sec / 60) : Loc.T("opt.unlimited"); }

        /// <summary>電腦玩家顯示動物名；真人顯示暱稱。</summary>
        public static string P(string kind, string animal, string name)
        {
            if (kind == "ai" && Array.IndexOf(Data.AnimalIds, animal) >= 0) return Animal(animal);
            return name ?? "";
        }

        public static string Auto(int adj, string animal)
        {
            if (Array.IndexOf(Data.AnimalIds, animal) < 0) animal = "cat";
            return Loc.T("adj." + adj) + Loc.T("name.sep") + Animal(animal);
        }

        public static string Clock(double sec)
        {
            int s = (int)Math.Max(0, Math.Floor(sec));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }

    /// <summary>結算資料：單機由 GameResult 轉成，線上由伺服器 JSON 轉成。兩邊走同一個畫面。</summary>
    public sealed class ResultInfo
    {
        public string Mode, Reason; public int Winner = -1; public double Time; public int Level = 1;
        public List<int> Ranking = new List<int>();
        public Dictionary<int, int> Scores = new Dictionary<int, int>(), Kills = new Dictionary<int, int>(), Lens = new Dictionary<int, int>();
        public List<ViewPlayer> Players = new List<ViewPlayer>();

        public static ResultInfo FromGame(GameResult r, GameState s)
        {
            var ri = new ResultInfo { Mode = r.Mode, Reason = r.Reason, Winner = r.Winner, Time = r.Time, Level = r.Level, Ranking = r.Ranking, Scores = r.Scores, Kills = r.Kills, Lens = r.Lens };
            foreach (var p in s.Players) ri.Players.Add(new ViewPlayer { Slot = p.Slot, Name = p.Name, Animal = p.Animal, Kind = p.Kind, Level = p.Level });
            return ri;
        }

        static void Pairs(object o, Dictionary<int, int> into)
        {
            var l = o as List<object>;
            if (l == null) return;
            foreach (var e in l)
            {
                var a = e as List<object>;
                if (a != null && a.Count >= 2) into[(int)Json.N(a[0])] = (int)Json.N(a[1]);
            }
        }

        public static ResultInfo FromJson(object result, object players)
        {
            var ri = new ResultInfo { Mode = Json.Str(result, "mode"), Reason = Json.Str(result, "reason"), Time = Json.Num(result, "time"), Level = Math.Max(1, Json.Int(result, "level", 1)) };
            var w = Json.Get(result, "winner");
            ri.Winner = w is double ? (int)(double)w : -1;
            var rk = Json.Get(result, "ranking") as List<object>;
            if (rk != null) foreach (var x in rk) ri.Ranking.Add((int)Json.N(x));
            Pairs(Json.Get(result, "scores"), ri.Scores); Pairs(Json.Get(result, "kills"), ri.Kills); Pairs(Json.Get(result, "lens"), ri.Lens);
            var pl = players as List<object>;
            if (pl != null) foreach (var p in pl)
                ri.Players.Add(new ViewPlayer { Slot = Json.Int(p, "slot"), Name = Json.Str(p, "name"), Animal = Json.Str(p, "animal", "cat"), Kind = Json.Str(p, "kind", "human"), Level = Json.Str(p, "level", "") });
            return ri;
        }

        public ViewPlayer By(int slot) { foreach (var p in Players) if (p.Slot == slot) return p; return null; }
        public int Score(int slot) { int v; return Scores.TryGetValue(slot, out v) ? v : 0; }
        public int Len(int slot) { int v; return Lens.TryGetValue(slot, out v) ? v : 0; }
    }
}
