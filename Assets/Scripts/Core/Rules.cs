using System;
using System.Collections.Generic;
using System.Linq;

namespace Snake3D
{
    /// <summary>果實：0 蘋果、1 金星、2 發光果實（出局的蛇變的）。</summary>
    public struct FoodDef
    {
        public string Name; public int Score; public int Grow; public double Ttl; public double Rad;
    }

    public sealed class FoodItem
    {
        public int Id; public double X, Z; public int Type; public int V; public double Ttl;
    }

    public sealed class GameEvent
    {
        public string T;            // count / go / eat / die / over
        public int N;               // count
        public int Slot = -1;       // eat / die
        public int Type;            // eat
        public int V;               // eat
        public double X, Z;         // eat / die
        public int Id;              // eat
        public string Why;          // die
        public int Killer = -1;     // die
        public int Orbs;            // die
        public int Winner = -1;     // over
        public string Reason;       // over
    }

    public sealed class PlayerInit
    {
        public int Slot = -1;
        public string Name;
        public string Animal = "cat";
        public string Kind = "human";   // human / ai
        public string Level;            // AI 難度
    }

    public sealed class GameOptions
    {
        public uint Seed;
        public List<PlayerInit> Players = new List<PlayerInit>();
        public string Speed = "normal";
        public int ThemeId = -1;        // -1 = 隨機
        public int TimeLimit;
        public string Obstacles = "none";
        public bool Track;
    }

    public sealed class PlayerState
    {
        public int Slot; public string Name, Animal, Kind, Level;
        public bool Alive = true, Left;
        public double X, Z, Ang, Ta, Len, Grow, R, V, Base;
        public List<double> Trail = new List<double>();
        public double[] Pts = new double[0];
        public int Score, Kills, Eaten;
        public bool Boost, Boosting;
        public double Energy = 1;
        public bool Tired;
        public double DeathTime = -1;
        public int FinalLen;
        // 畫面端內插用（單機：上一個節拍的位置）
        public double[] PrevPts; public double PrevAng;
    }

    public sealed class GameResult
    {
        public string Mode; public int Winner = -1; public string Reason; public double Time;
        public List<int> Ranking = new List<int>();
        public Dictionary<int, int> Scores = new Dictionary<int, int>();
        public Dictionary<int, int> Kills = new Dictionary<int, int>();
        public Dictionary<int, int> Lens = new Dictionary<int, int>();
        public Dictionary<int, int> Eaten = new Dictionary<int, int>();
        public int Level;
    }

    public struct Rock { public double X, Z, R; }

    public struct GridEntry { public PlayerState P; public int I; }

    public sealed class GameState
    {
        public uint Seed; public Rng Rnd; public double R; public string Mode, Speed;
        public int ThemeId, TimeLimit; public string Obstacles;
        public List<Rock> Rocks = new List<Rock>();
        public List<PlayerState> Players = new List<PlayerState>();
        public List<FoodItem> Foods = new List<FoodItem>();
        public int FoodSeq = 1; public bool Track;
        public List<FoodItem> FAdd = new List<FoodItem>(); public List<int> FRem = new List<int>();
        public string Phase = "countdown"; public double Countdown = Rules.COUNTDOWN; public int LastCount;
        public double Time; public int Tick;
        public List<GameEvent> Events = new List<GameEvent>();
        public double EndHold; public GameResult Result;
        public List<int> LastDead = new List<int>();
        public Dictionary<long, List<GridEntry>> Grid;
    }

    /// <summary>自由方向貪吃蛇規則核心（純邏輯、可注入種子）。與網頁版 rules.js 逐行對應。</summary>
    public static class Rules
    {
        public const double DT = 1.0 / 30;
        public const double COUNTDOWN = 3;
        public const double END_HOLD = 1.6;
        public const double SEG = 0.42;
        public const int START_LEN = 10;
        public const double BASE_R = 0.42;
        public const double MAX_EXTRA_R = 0.5;
        public const double TURN = 4.2;
        public const double BOOST_MUL = 1.75;
        public const double ENERGY_DRAIN = 0.45, ENERGY_REGEN = 0.16, ENERGY_RESUME = 0.15;
        public const double GROW_RATE = 8;
        public const int THEME_COUNT = 6;
        public const int MAX_FOODS = 260;
        public const int MAX_ORBS_PER_DEATH = 50;
        public const double TAU = Math.PI * 2;
        public static readonly int[] TIME_LIMITS = { 0, 120, 180, 300 };
        public static readonly string[] SPEED_ORDER = { "slow", "normal", "fast" };
        public static readonly string[] OBSTACLE_ORDER = { "none", "few", "many" };
        public static readonly string[] SLOT_COLORS = { "#ff7a45", "#3fa7f5", "#ff6f9f", "#4cc26a", "#a58bff", "#ffc233", "#2ec7c0", "#e0584d" };

        public static readonly FoodDef[] FOOD =
        {
            new FoodDef { Name = "apple", Score = 10, Grow = 2, Ttl = 0, Rad = 0.42 },
            new FoodDef { Name = "gold", Score = 30, Grow = 6, Ttl = 15, Rad = 0.55 },
            new FoodDef { Name = "orb", Score = 5, Grow = 1, Ttl = 45, Rad = 0.36 }
        };

        public static double SpeedOf(string id)
        {
            switch (id) { case "slow": return 3.8; case "fast": return 5.8; default: return 4.8; }
        }
        public static double ObstacleK(string id)
        {
            switch (id) { case "few": return 0.007; case "many": return 0.015; default: return 0; }
        }
        static bool KnownSpeed(string id) { return id == "slow" || id == "normal" || id == "fast"; }
        static bool KnownObstacle(string id) { return id == "none" || id == "few" || id == "many"; }

        /// <summary>JS 的 Math.round（.5 往正無限大進位）。</summary>
        public static double JsRound(double x) { return Math.Floor(x + 0.5); }

        public static double WrapAng(double a) { return Math.Atan2(Math.Sin(a), Math.Cos(a)); }
        public static double ArenaFor(int n) { return n <= 1 ? 24 : n <= 4 ? 30 : 38; }
        public static double RadiusFor(double len) { return BASE_R + Math.Min(MAX_EXTRA_R, Math.Max(0, len - START_LEN) * 0.004); }
        public static double TurnRate(double r) { return TURN * Math.Sqrt(BASE_R / r); }
        public static int AppleTarget(double R) { return (int)JsRound(R * R * 0.045); }
        public static int SelfSkip(double r) { return (int)Math.Ceiling((r * 1.6) / SEG) + 2; }
        /// <summary>
        /// 兩個數的長度。預設用 Math.Sqrt；UseJsHypot 開啟時改用 V8 Math.hypot 的演算法（只給「與網頁版逐節拍比對」的測試用，
        /// 讓兩邊連最後一位浮點誤差都一致）。
        /// </summary>
        public static bool UseJsHypot;
        public static double Hypot(double a, double b)
        {
            if (!UseJsHypot) return Math.Sqrt(a * a + b * b);
            a = Math.Abs(a); b = Math.Abs(b);
            double max = Math.Max(a, b);
            if (max == 0) return 0;
            double sum = 0, comp = 0;
            double n = a / max, summand = n * n - comp, pre = sum + summand;
            comp = (pre - sum) - summand; sum = pre;
            n = b / max; summand = n * n - comp; pre = sum + summand;
            comp = (pre - sum) - summand; sum = pre;
            return Math.Sqrt(sum) * max;
        }

        // ---------- 建立對局 ----------
        struct Spawn { public double X, Z, A; }

        public static GameState CreateGame(GameOptions o)
        {
            var rnd = new Rng(o.Seed);
            int n = o.Players.Count;
            string speed = KnownSpeed(o.Speed) ? o.Speed : "normal";
            string mode = n == 1 ? "challenge" : "versus";
            double R = ArenaFor(n);
            var s = new GameState();
            s.Seed = o.Seed; s.Rnd = rnd; s.R = R; s.Mode = mode; s.Speed = speed;
            s.ThemeId = o.ThemeId >= 0 ? o.ThemeId % THEME_COUNT : (int)Math.Floor(rnd.Next() * THEME_COUNT);
            s.TimeLimit = mode == "challenge" ? 0 : (Array.IndexOf(TIME_LIMITS, o.TimeLimit) >= 0 ? o.TimeLimit : 0);
            s.Obstacles = KnownObstacle(o.Obstacles) ? o.Obstacles : "none";
            s.Track = o.Track;

            var spawns = new List<Spawn>();
            if (mode == "challenge") spawns.Add(new Spawn { X = -2, Z = 0, A = 0 });
            else
            {
                double off = rnd.Next() * TAU;
                var order = new List<KeyValuePair<int, double>>();
                for (int i = 0; i < 8; i++) order.Add(new KeyValuePair<int, double>(i, rnd.Next()));
                var sorted = order.OrderBy(e => e.Value).ToList();
                for (int i = 0; i < 8; i++)
                {
                    double t = off + (sorted[i].Key / 8.0) * TAU;
                    spawns.Add(new Spawn
                    {
                        X = Math.Cos(t) * R * 0.62, Z = Math.Sin(t) * R * 0.62,
                        A = WrapAng(t + Math.PI + (rnd.Next() - 0.5) * 0.6)
                    });
                }
            }
            GenRocks(s, spawns, rnd);
            double bas = SpeedOf(speed);
            for (int i = 0; i < n; i++)
            {
                var pl = o.Players[i];
                var sp = spawns[i % spawns.Count];
                var p = new PlayerState
                {
                    Slot = pl.Slot >= 0 ? pl.Slot : i,
                    Name = string.IsNullOrEmpty(pl.Name) ? ("玩家" + (i + 1)) : pl.Name,
                    Animal = string.IsNullOrEmpty(pl.Animal) ? "cat" : pl.Animal,
                    Kind = string.IsNullOrEmpty(pl.Kind) ? "human" : pl.Kind,
                    Level = pl.Level,
                    X = sp.X, Z = sp.Z, Ang = sp.A, Ta = sp.A, Len = START_LEN, R = BASE_R, V = bas, Base = bas,
                    FinalLen = START_LEN
                };
                double L = START_LEN * SEG + 1;
                for (double d = L; d >= 0; d -= 0.2) { p.Trail.Add(sp.X - Math.Cos(sp.A) * d); p.Trail.Add(sp.Z - Math.Sin(sp.A) * d); }
                p.Trail.Add(sp.X); p.Trail.Add(sp.Z);
                SamplePts(p);
                s.Players.Add(p);
            }
            int apples = AppleTarget(R);
            for (int i = 0; i < apples; i++) AddFood(s, 0);
            return s;
        }

        static void GenRocks(GameState s, List<Spawn> spawns, Rng rnd)
        {
            double R = s.R;
            int want = (int)JsRound(Math.PI * R * R * ObstacleK(s.Obstacles));
            int tries = 0;
            while (s.Rocks.Count < want && tries++ < 400)
            {
                double t = rnd.Next() * TAU, d = Math.Sqrt(rnd.Next()) * (R - 3.5);
                double x = Math.Cos(t) * d, z = Math.Sin(t) * d, r = 0.7 + rnd.Next() * 0.7;
                bool ok = true;
                foreach (var sp in spawns)
                {
                    if (Hypot(sp.X - x, sp.Z - z) < 6 + r) { ok = false; break; }
                    double fx = sp.X + Math.Cos(sp.A) * 6, fz = sp.Z + Math.Sin(sp.A) * 6;
                    if (Hypot(fx - x, fz - z) < 4 + r) { ok = false; break; }
                }
                if (ok) foreach (var k in s.Rocks) if (Hypot(k.X - x, k.Z - z) < k.R + r + 2.2) { ok = false; break; }
                if (ok) s.Rocks.Add(new Rock { X = x, Z = z, R = r });
            }
        }

        public static PlayerState PlayerBySlot(GameState s, int slot)
        {
            foreach (var p in s.Players) if (p.Slot == slot) return p;
            return null;
        }

        /// <summary>依軌跡取出每節位置（頭在前），並剪掉用不到的舊軌跡。</summary>
        public static void SamplePts(PlayerState p)
        {
            var tr = p.Trail;
            int count = (int)Math.Floor(p.Len) + 1;
            var pts = new double[count * 2];
            int k = tr.Count - 2;
            pts[0] = tr[k]; pts[1] = tr[k + 1];
            int made = 1; double need = SEG, px = tr[k], pz = tr[k + 1];
            while (made < count && k >= 2)
            {
                double qx = tr[k - 2], qz = tr[k - 1];
                double d = Hypot(qx - px, qz - pz);
                if (d >= need)
                {
                    double f = need / d;
                    px += (qx - px) * f; pz += (qz - pz) * f;
                    pts[made * 2] = px; pts[made * 2 + 1] = pz; made++;
                    need = SEG;
                }
                else { need -= d; px = qx; pz = qz; k -= 2; }
            }
            while (made < count) { pts[made * 2] = px; pts[made * 2 + 1] = pz; made++; }
            if (k > 400) tr.RemoveRange(0, k - 40);
            p.Pts = pts;
        }

        // ---------- 果實 ----------
        public static FoodItem AddFood(GameState s, int type) { return AddFood(s, type, null, 0, -1); }

        public static FoodItem AddFood(GameState s, int type, double? px, double z, int v)
        {
            if (s.Foods.Count >= MAX_FOODS) return null;
            double x;
            if (px == null)
            {
                bool ok = false; x = 0; z = 0;
                for (int tries = 0; tries < 12 && !ok; tries++)
                {
                    double t = s.Rnd.Next() * TAU, d = Math.Sqrt(s.Rnd.Next()) * (s.R - 1.4);
                    x = Math.Cos(t) * d; z = Math.Sin(t) * d; ok = true;
                    foreach (var k in s.Rocks) if (Hypot(k.X - x, k.Z - z) < k.R + 0.7) { ok = false; break; }
                }
                if (!ok) return null;
            }
            else x = px.Value;
            var f = new FoodItem
            {
                Id = s.FoodSeq++, X = x, Z = z, Type = type,
                V = v >= 0 ? v : (int)Math.Floor(s.Rnd.Next() * 3), Ttl = FOOD[type].Ttl
            };
            s.Foods.Add(f);
            if (s.Track) s.FAdd.Add(f);
            return f;
        }

        static void RemoveFood(GameState s, int i)
        {
            var f = s.Foods[i];
            s.Foods.RemoveAt(i);
            if (s.Track) s.FRem.Add(f.Id);
        }

        // ---------- 輸入 ----------
        public static bool Aim(GameState s, int slot, double angle)
        {
            var p = PlayerBySlot(s, slot);
            if (p == null || !p.Alive || double.IsNaN(angle) || double.IsInfinity(angle)) return false;
            p.Ta = WrapAng(angle);
            return true;
        }

        public static void SetBoost(GameState s, int slot, bool on)
        {
            var p = PlayerBySlot(s, slot);
            if (p != null && p.Alive) p.Boost = on;
        }

        // ---------- 一個節拍 ----------
        public static GameState Step(GameState s, double dt)
        {
            s.Events = new List<GameEvent>();
            if (s.Phase == "countdown")
            {
                s.Countdown -= dt;
                int c = (int)Math.Max(0, Math.Ceiling(s.Countdown - 1e-9));
                if (c != s.LastCount && c > 0) { s.LastCount = c; s.Events.Add(new GameEvent { T = "count", N = c }); }
                if (s.Countdown <= 1e-9) { s.Phase = "play"; s.Events.Add(new GameEvent { T = "go" }); }
                return s;
            }
            if (s.Phase == "over") { s.EndHold -= dt; return s; }

            s.Time += dt; s.Tick++;
            for (int i = s.Foods.Count - 1; i >= 0; i--)
            {
                var f = s.Foods[i];
                if (f.Ttl > 0) { f.Ttl -= dt; if (f.Ttl <= 0) RemoveFood(s, i); }
            }
            foreach (var p in s.Players) if (p.Alive) MovePlayer(s, p, dt);
            Collide(s);
            Eat(s);
            Refill(s, dt);
            CheckEnd(s);
            return s;
        }

        static void TurnToward(PlayerState p, double dt)
        {
            double da = WrapAng(p.Ta - p.Ang), mx = TurnRate(p.R) * dt;
            p.Ang = WrapAng(p.Ang + Math.Max(-mx, Math.Min(mx, da)));
        }

        static void MovePlayer(GameState s, PlayerState p, double dt)
        {
            p.PrevPts = p.Pts; p.PrevAng = p.Ang;
            if (p.Boost && !p.Tired && p.Energy > 0)
            {
                p.Boosting = true;
                p.Energy -= ENERGY_DRAIN * dt;
                if (p.Energy <= 0) { p.Energy = 0; p.Tired = true; p.Boosting = false; }
            }
            else
            {
                p.Boosting = false;
                p.Energy = Math.Min(1, p.Energy + ENERGY_REGEN * dt);
                if (p.Tired && p.Energy >= ENERGY_RESUME) p.Tired = false;
            }
            TurnToward(p, dt);
            double mul = s.Mode == "challenge" ? Math.Min(1.5, 1 + 0.04 * Math.Floor(p.Eaten / 5.0)) : 1;
            p.V = p.Base * mul * (p.Boosting ? BOOST_MUL : 1);
            p.X += Math.Cos(p.Ang) * p.V * dt;
            p.Z += Math.Sin(p.Ang) * p.V * dt;
            p.Trail.Add(p.X); p.Trail.Add(p.Z);
            if (p.Grow > 0) { double g = Math.Min(p.Grow, GROW_RATE * dt); p.Len += g; p.Grow -= g; }
            p.R = RadiusFor(p.Len);
            SamplePts(p);
        }

        // ---------- 空間格 ----------
        const double CELL = 2;

        public static Dictionary<long, List<GridEntry>> BuildGrid(GameState s)
        {
            var g = new Dictionary<long, List<GridEntry>>();
            foreach (var p in s.Players)
            {
                if (!p.Alive) continue;
                var pts = p.Pts; int n = pts.Length / 2;
                for (int i = 1; i < n; i++)
                {
                    double x = pts[i * 2], z = pts[i * 2 + 1];
                    long key = (long)Math.Floor(x / CELL) * 4096 + (long)Math.Floor(z / CELL);
                    List<GridEntry> a;
                    if (!g.TryGetValue(key, out a)) { a = new List<GridEntry>(); g[key] = a; }
                    a.Add(new GridEntry { P = p, I = i });
                }
            }
            s.Grid = g;
            return g;
        }

        /// <summary>對 (x,z) 附近 rad 範圍的每一節呼叫 fn(p, i)；fn 回傳 true 就停。</summary>
        public static void NearPts(GameState s, double x, double z, double rad, Func<PlayerState, int, bool> fn)
        {
            var g = s.Grid;
            if (g == null) return;
            long x0 = (long)Math.Floor((x - rad) / CELL), x1 = (long)Math.Floor((x + rad) / CELL);
            long z0 = (long)Math.Floor((z - rad) / CELL), z1 = (long)Math.Floor((z + rad) / CELL);
            for (long cx = x0; cx <= x1; cx++)
                for (long cz = z0; cz <= z1; cz++)
                {
                    List<GridEntry> a;
                    if (!g.TryGetValue(cx * 4096 + cz, out a)) continue;
                    for (int k = 0; k < a.Count; k++) if (fn(a[k].P, a[k].I)) return;
                }
        }

        sealed class Death { public PlayerState P; public string Why; public int Killer = -1; }

        static void Collide(GameState s)
        {
            BuildGrid(s);
            var alive = s.Players.Where(p => p.Alive).ToList();
            var dead = new Dictionary<int, Death>();
            var deadOrder = new List<int>();
            Action<int, Death> mark = (slot, d) => { if (!dead.ContainsKey(slot)) deadOrder.Add(slot); dead[slot] = d; };
            for (int a = 0; a < alive.Count; a++)
                for (int b = a + 1; b < alive.Count; b++)
                {
                    var p = alive[a]; var q = alive[b];
                    if (Hypot(p.X - q.X, p.Z - q.Z) < (p.R + q.R) * 0.8)
                    {
                        if (!dead.ContainsKey(p.Slot)) mark(p.Slot, new Death { P = p, Why = "head" });
                        if (!dead.ContainsKey(q.Slot)) mark(q.Slot, new Death { P = q, Why = "head" });
                    }
                }
            foreach (var p in alive)
            {
                if (dead.ContainsKey(p.Slot)) continue;
                if (Hypot(p.X, p.Z) + p.R * 0.6 > s.R) { mark(p.Slot, new Death { P = p, Why = "wall" }); continue; }
                Death hit = null;
                foreach (var k in s.Rocks)
                    if (Hypot(k.X - p.X, k.Z - p.Z) < k.R + p.R * 0.75) { hit = new Death { P = p, Why = "rock" }; break; }
                if (hit == null)
                {
                    int skip = SelfSkip(p.R);
                    Death found = null;
                    NearPts(s, p.X, p.Z, p.R + 1.2, (q, i) =>
                    {
                        if (q == p && i < skip) return false;
                        double qx = q.Pts[i * 2], qz = q.Pts[i * 2 + 1];
                        if (Hypot(qx - p.X, qz - p.Z) < (p.R + q.R) * 0.8)
                        {
                            found = new Death { P = p, Why = q == p ? "self" : "other", Killer = q == p ? -1 : q.Slot };
                            return true;
                        }
                        return false;
                    });
                    hit = found;
                }
                if (hit != null) mark(p.Slot, hit);
            }
            foreach (int slot in deadOrder)
            {
                var d = dead[slot];
                if (d.Killer >= 0) { var k = PlayerBySlot(s, d.Killer); if (k != null && !dead.ContainsKey(k.Slot)) k.Kills++; }
                KillPlayer(s, d.P, d.Why, d.Killer);
            }
        }

        static void Eat(GameState s)
        {
            foreach (var p in s.Players)
            {
                if (!p.Alive) continue;
                for (int i = s.Foods.Count - 1; i >= 0; i--)
                {
                    var f = s.Foods[i]; var F = FOOD[f.Type];
                    double dx = f.X - p.X, dz = f.Z - p.Z, rr = p.R + F.Rad + 0.15;
                    if (dx * dx + dz * dz > rr * rr) continue;
                    RemoveFood(s, i);
                    p.Score += F.Score; p.Grow += F.Grow; p.Eaten++;
                    s.Events.Add(new GameEvent { T = "eat", Slot = p.Slot, Type = f.Type, V = f.V, X = f.X, Z = f.Z, Id = f.Id });
                }
            }
        }

        static void Refill(GameState s, double dt)
        {
            int apples = 0, golds = 0;
            foreach (var f in s.Foods) { if (f.Type == 0) apples++; else if (f.Type == 1) golds++; }
            int want = AppleTarget(s.R);
            for (int k = 0; k < 2 && apples < want; k++, apples++) AddFood(s, 0);
            if (s.Time > 8 && golds < 2 && s.Rnd.Next() < 0.08 * dt) AddFood(s, 1);
        }

        static void KillPlayer(GameState s, PlayerState p, string why, int killer)
        {
            if (!p.Alive) return;
            p.Alive = false; p.DeathTime = s.Time; p.FinalLen = (int)JsRound(p.Len); p.Boost = false; p.Boosting = false;
            var pts = p.Pts; int n = pts.Length / 2;
            int step2 = Math.Max(2, (int)Math.Ceiling(n / (double)MAX_ORBS_PER_DEATH));
            int orbs = 0;
            for (int i = 0; i < n; i += step2)
            {
                double x = pts[i * 2] + (s.Rnd.Next() - 0.5) * 0.5, z = pts[i * 2 + 1] + (s.Rnd.Next() - 0.5) * 0.5;
                if (Hypot(x, z) > s.R - 0.8) continue;
                if (AddFood(s, 2, x, z, p.Slot) != null) orbs++;
            }
            s.Events.Add(new GameEvent { T = "die", Slot = p.Slot, Why = why, Killer = killer, X = p.X, Z = p.Z, Orbs = orbs });
            s.LastDead.Add(p.Slot);
            p.Pts = new double[0]; p.Trail = new List<double>(); p.PrevPts = null;
        }

        public static void RemovePlayer(GameState s, int slot)
        {
            var p = PlayerBySlot(s, slot);
            if (p == null) return;
            p.Left = true;
            if (p.Alive) KillPlayer(s, p, "left", -1);
        }

        // ---------- 勝負 ----------
        static void CheckEnd(GameState s)
        {
            if (s.Phase != "play") return;
            var alive = s.Players.Where(p => p.Alive).ToList();
            if (s.Mode == "challenge")
            {
                if (alive.Count == 0) Finish(s, -1, "crash");
                s.LastDead.Clear();
                return;
            }
            if (alive.Count <= 1)
            {
                if (alive.Count == 1) { Finish(s, alive[0].Slot, "last"); return; }
                var last = s.Players.Where(p => s.LastDead.Contains(p.Slot) && !p.Left).ToList();
                var pool = last.Count > 0 ? last : s.Players;
                int best = pool.Max(p => p.Score);
                var top = pool.Where(p => p.Score == best).ToList();
                Finish(s, top.Count == 1 ? top[0].Slot : -1, "together");
                return;
            }
            if (s.TimeLimit > 0 && s.Time >= s.TimeLimit)
            {
                int best = alive.Max(p => p.Score);
                var top = alive.Where(p => p.Score == best).ToList();
                Finish(s, top.Count == 1 ? top[0].Slot : -1, "time");
            }
            s.LastDead.Clear();
        }

        sealed class RankCmp : IComparer<PlayerState>
        {
            readonly int winner;
            public RankCmp(int w) { winner = w; }
            public int Compare(PlayerState a, PlayerState b)
            {
                if (a.Slot == winner) return -1;
                if (b.Slot == winner) return 1;
                if (a.Alive != b.Alive) return a.Alive ? -1 : 1;
                if (a.Alive && b.Alive) return b.Score.CompareTo(a.Score);
                if (a.DeathTime != b.DeathTime) return b.DeathTime.CompareTo(a.DeathTime);
                return b.Score.CompareTo(a.Score);
            }
        }

        static void Finish(GameState s, int winner, string reason)
        {
            s.Phase = "over"; s.EndHold = END_HOLD;
            var res = new GameResult { Mode = s.Mode, Winner = winner, Reason = reason, Time = s.Time };
            res.Ranking = s.Players.OrderBy(p => p, new RankCmp(winner)).Select(p => p.Slot).ToList();
            foreach (var p in s.Players)
            {
                res.Scores[p.Slot] = p.Score; res.Kills[p.Slot] = p.Kills;
                res.Lens[p.Slot] = p.Alive ? (int)JsRound(p.Len) : p.FinalLen;
                res.Eaten[p.Slot] = p.Eaten;
            }
            res.Level = s.Mode == "challenge" ? 1 + (int)Math.Floor(s.Players[0].Eaten / 5.0) : 0;
            s.Result = res;
            s.Events.Add(new GameEvent { T = "over", Winner = winner, Reason = reason });
        }
    }
}
