using System;
using System.Collections.Generic;

namespace Snake3D
{
    /// <summary>畫面端看到的一條蛇（單機從 PlayerState 複製，線上從伺服器快照解出來）。</summary>
    public sealed class ViewPlayer
    {
        public int Slot; public string Name, Animal, Kind, Level;
        public bool Alive = true, Left;
        public double[] Pts = new double[0], PrevPts;
        public double X, Z, Ang, PrevAng, Ta, R = Rules.BASE_R, Len = Rules.START_LEN;
        public int Score, Kills, Eaten, FinalLen = Rules.START_LEN;
        public bool Boosting, Tired; public double Energy = 1;
    }

    public sealed class ViewState
    {
        public double R; public string Mode, Speed; public int ThemeId, TimeLimit; public List<Rock> Rocks = new List<Rock>();
        public string Phase = "countdown"; public double Countdown = Rules.COUNTDOWN, Time;
        public List<FoodItem> Foods = new List<FoodItem>();
        public List<ViewPlayer> Players = new List<ViewPlayer>();
        // 線上：兩筆快照之間的內插
        public double SnapAt, PrevAt, Gap = 0.1;

        public ViewPlayer BySlot(int slot)
        {
            foreach (var p in Players) if (p.Slot == slot) return p;
            return null;
        }

        /// <summary>單機：把規則狀態複製成畫面用的資料（蛇身陣列直接引用，不複製）。</summary>
        public static ViewState FromGame(GameState s)
        {
            var v = new ViewState { R = s.R, Mode = s.Mode, Speed = s.Speed, ThemeId = s.ThemeId, TimeLimit = s.TimeLimit, Rocks = s.Rocks };
            foreach (var p in s.Players) v.Players.Add(new ViewPlayer { Slot = p.Slot, Name = p.Name, Animal = p.Animal, Kind = p.Kind, Level = p.Level });
            v.Sync(s);
            return v;
        }

        public void Sync(GameState s)
        {
            Phase = s.Phase; Countdown = s.Countdown; Time = s.Time; Foods = s.Foods;
            for (int i = 0; i < s.Players.Count && i < Players.Count; i++)
            {
                var p = s.Players[i]; var q = Players[i];
                q.Alive = p.Alive; q.Left = p.Left; q.Pts = p.Pts; q.PrevPts = p.PrevPts;
                q.X = p.X; q.Z = p.Z; q.Ang = p.Ang; q.PrevAng = p.PrevAng; q.Ta = p.Ta; q.R = p.R; q.Len = p.Len;
                q.Score = p.Score; q.Kills = p.Kills; q.Eaten = p.Eaten; q.FinalLen = p.FinalLen;
                q.Boosting = p.Boosting; q.Tired = p.Tired; q.Energy = p.Energy;
            }
        }

        // ---------- 線上：開局資訊與快照（對應 rules.js 的 startInfo / viewFromStart / applySnapshot） ----------
        public static ViewState FromStartInfo(object info)
        {
            var v = new ViewState
            {
                R = Json.Num(info, "R"), Mode = Json.Str(info, "mode"), Speed = Json.Str(info, "speed"),
                ThemeId = Json.Int(info, "themeId"), TimeLimit = Json.Int(info, "timeLimit")
            };
            var rocks = Json.Arr(info, "rocks");
            if (rocks != null) foreach (var o in rocks) { var a = (List<object>)o; v.Rocks.Add(new Rock { X = Json.N(a[0]), Z = Json.N(a[1]), R = Json.N(a[2]) }); }
            var foods = Json.Arr(info, "foods");
            if (foods != null) foreach (var o in foods) v.Foods.Add(DecFood((List<object>)o));
            var players = Json.Arr(info, "players");
            if (players != null)
                foreach (var o in players)
                    v.Players.Add(new ViewPlayer
                    {
                        Slot = Json.Int(o, "slot"), Name = Json.Str(o, "name"), Animal = Json.Str(o, "animal", "cat"),
                        Kind = Json.Str(o, "kind", "human"), Level = Json.Str(o, "level", null)
                    });
            return v;
        }

        static FoodItem DecFood(List<object> a)
        {
            return new FoodItem { Id = (int)Json.N(a[0]), X = Json.N(a[1]) / 10, Z = Json.N(a[2]) / 10, Type = (int)Json.N(a[3]), V = (int)Json.N(a[4]), Ttl = Json.N(a[5]) };
        }

        public static double[] DecPts(List<object> a)
        {
            var o = new double[a.Count];
            double x = 0, z = 0;
            for (int i = 0; i + 1 < a.Count; i += 2) { x += Json.N(a[i]); z += Json.N(a[i + 1]); o[i] = x / 10; o[i + 1] = z / 10; }
            return o;
        }

        public void ApplySnapshot(object snap, double now)
        {
            int ph = Json.Int(snap, "ph");
            Phase = ph == 0 ? "countdown" : ph == 1 ? "play" : "over";
            Time = Json.Num(snap, "t"); Countdown = Json.Num(snap, "c");
            if (SnapAt > 0) { double g = now - SnapAt; if (g > 0.01 && g < 1) Gap = Gap * 0.8 + g * 0.2; }
            PrevAt = SnapAt; SnapAt = now;
            var ps = Json.Arr(snap, "p");
            for (int i = 0; ps != null && i < ps.Count && i < Players.Count; i++)
            {
                var a = (List<object>)ps[i]; var p = Players[i];
                bool was = p.Alive;
                p.Alive = Json.N(a[0]) != 0; p.Score = (int)Json.N(a[1]); p.Kills = (int)Json.N(a[2]); p.Eaten = (int)Json.N(a[3]);
                p.Boosting = Json.N(a[4]) != 0; p.Left = Json.N(a[5]) != 0;
                p.Energy = Json.N(a[6]) / 100; p.Len = Json.N(a[7]) / 10; p.PrevAng = p.Ang; p.Ang = Json.N(a[8]) / 100; p.Ta = Json.N(a[9]) / 100;
                p.Tired = a.Count > 11 && Json.N(a[11]) != 0;
                p.R = Rules.RadiusFor(p.Len);
                p.PrevPts = was && p.Pts != null && p.Pts.Length > 0 ? p.Pts : null;
                p.Pts = p.Alive ? DecPts((List<object>)a[10]) : new double[0];
                if (p.Pts.Length > 0) { p.X = p.Pts[0]; p.Z = p.Pts[1]; }
                if (p.Alive || was) p.FinalLen = (int)Rules.JsRound(p.Len);
            }
            var f = Json.Arr(snap, "f");
            if (f != null)
            {
                Foods = new List<FoodItem>();
                foreach (var o in f) Foods.Add(DecFood((List<object>)o));
            }
            else
            {
                var fr = Json.Arr(snap, "fr");
                if (fr != null && fr.Count > 0)
                {
                    var rm = new HashSet<int>();
                    foreach (var o in fr) rm.Add((int)Json.N(o));
                    Foods = Foods.FindAll(x => !rm.Contains(x.Id));
                }
                var fa = Json.Arr(snap, "fa");
                if (fa != null)
                {
                    var have = new HashSet<int>();
                    foreach (var x in Foods) have.Add(x.Id);
                    foreach (var o in fa) { var a = (List<object>)o; if (!have.Contains((int)Json.N(a[0]))) Foods.Add(DecFood(a)); }
                }
            }
        }
    }
}
