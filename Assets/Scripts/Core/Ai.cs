using System;
using System.Collections.Generic;
using System.Linq;

namespace Snake3D
{
    public sealed class AiLevel
    {
        public string Id; public int Every; public double Look, Care, FoodW, Noise; public bool Heads; public int Boost; public bool Hunt, Room;
    }

    public sealed class Brain
    {
        public string Level; public AiLevel Cfg; public Rng Rnd;
        public double? Wander; public int LastC = int.MinValue;
    }

    /// <summary>電腦對手（四段難度）。只透過 Rules.Aim / Rules.SetBoost 下指令，與網頁版 ai.js 逐行對應。</summary>
    public static class Ai
    {
        public static readonly string[] LEVEL_ORDER = { "toddler", "easy", "normal", "hard" };

        static readonly Dictionary<string, AiLevel> LEVELS = new Dictionary<string, AiLevel>
        {
            { "toddler", new AiLevel { Id = "toddler", Every = 8, Look = 3.0, Care = 0.55, FoodW = 0.35, Noise = 0.9, Heads = false, Boost = 0, Hunt = false } },
            { "easy",    new AiLevel { Id = "easy",    Every = 5, Look = 4.5, Care = 1,    FoodW = 1.0,  Noise = 0.15, Heads = false, Boost = 0, Hunt = false } },
            { "normal",  new AiLevel { Id = "normal",  Every = 3, Look = 9,   Care = 1,    FoodW = 1.1,  Noise = 0,    Heads = true,  Boost = 1, Hunt = false } },
            { "hard",    new AiLevel { Id = "hard",    Every = 2, Look = 12,  Care = 1,    FoodW = 1.25, Noise = 0,    Heads = true,  Boost = 2, Hunt = true, Room = true } }
        };
        const double TAU = Math.PI * 2;
        const int N_DIR = 16;

        public static Brain CreateBrain(string level, uint? seed)
        {
            string lv = level != null && LEVELS.ContainsKey(level) ? level : "normal";
            return new Brain { Level = lv, Cfg = LEVELS[lv], Rnd = new Rng(seed ?? 7u) };
        }

        // 探路的終點（與 JS 的共用 END 物件相同）
        static double endX, endZ, endH;

        struct Start { public double X, Z, H; }

        static double Probe(GameState s, PlayerState p, double a, double look, List<PlayerState> enemies, Start? from)
        {
            int skip = Rules.SelfSkip(p.R) + 3;
            double margin = p.R + 0.25;
            double stepLen = 0.45, rate = Rules.TurnRate(p.R) * (stepLen / Math.Max(1, p.V));
            double x = from.HasValue ? from.Value.X : p.X, z = from.HasValue ? from.Value.Z : p.Z, h = from.HasValue ? from.Value.H : p.Ang;
            endX = x; endZ = z; endH = h;
            for (double d = stepLen; d <= look; d += stepLen)
            {
                double da = Rules.WrapAng(a - h);
                h += Math.Max(-rate, Math.Min(rate, da));
                x += Math.Cos(h) * stepLen; z += Math.Sin(h) * stepLen;
                if (Rules.Hypot(x, z) + margin > s.R) return d;
                foreach (var k in s.Rocks) if (Rules.Hypot(k.X - x, k.Z - z) < k.R + margin) return d;
                bool hit = false;
                double cx = x, cz = z;
                Rules.NearPts(s, x, z, margin + 1, (q, i) =>
                {
                    if (q == p && i < skip) return false;
                    if (!q.Alive) return false;
                    double qx = q.Pts[i * 2], qz = q.Pts[i * 2 + 1];
                    if (Rules.Hypot(qx - cx, qz - cz) < margin + q.R) { hit = true; return true; }
                    return false;
                });
                if (hit) return d;
                if (enemies != null)
                {
                    double t = d / Math.Max(1, p.V);
                    foreach (var e in enemies)
                    {
                        double ex = e.X + Math.Cos(e.Ang) * e.V * t, ez = e.Z + Math.Sin(e.Ang) * e.V * t;
                        if (Rules.Hypot(ex - x, ez - z) < margin + e.R + 0.6) return d;
                    }
                }
                endX = x; endZ = z; endH = h;
            }
            return look;
        }

        static FoodItem PickTarget(Brain brain, GameState s, PlayerState p)
        {
            var cfg = brain.Cfg;
            FoodItem best = null; double bestSc = -1e9;
            var others = s.Players.Where(q => q.Alive && q != p).ToList();
            foreach (var f in s.Foods)
            {
                double d = Rules.Hypot(f.X - p.X, f.Z - p.Z);
                if (d > 40) continue;
                var F = Rules.FOOD[f.Type];
                double sc;
                if (brain.Level == "easy" || brain.Level == "toddler") sc = -d;
                else
                {
                    sc = (F.Score + 6) / (d + 4);
                    double da = Math.Abs(Rules.WrapAng(Math.Atan2(f.Z - p.Z, f.X - p.X) - p.Ang));
                    sc *= 1 - da / (Math.PI * 1.6);
                    if (f.Type == 1 && f.Ttl > 0 && d / p.Base > f.Ttl) continue;
                    if (Rules.Hypot(f.X, f.Z) > s.R - p.R - 1.2) sc *= 0.3;
                    if (cfg.Hunt)
                    {
                        foreach (var q in others) if (Rules.Hypot(f.X - q.X, f.Z - q.Z) < d * 0.6) { sc *= 0.35; break; }
                    }
                }
                if (sc > bestSc) { bestSc = sc; best = f; }
            }
            return best;
        }

        sealed class Cand { public double A, Sc, Clear, Ex, Ez, Eh; }

        public static void Think(Brain brain, GameState s, PlayerState p)
        {
            var cfg = brain.Cfg; var rnd = brain.Rnd;
            if (s.Grid == null) Rules.BuildGrid(s);
            List<PlayerState> enemies = cfg.Heads
                ? s.Players.Where(q => q.Alive && q != p && Rules.Hypot(q.X - p.X, q.Z - p.Z) < cfg.Look + 6).ToList() : null;

            double? tx = null, tz = null; FoodItem goal = null; bool hunting = false;
            if (cfg.Hunt && p.Len > Rules.START_LEN + 6)
            {
                foreach (var q in s.Players)
                {
                    if (!q.Alive || q == p || q.Len > p.Len * 1.15) continue;
                    double d = Rules.Hypot(q.X - p.X, q.Z - p.Z);
                    if (d > 9 || d < 2) continue;
                    tx = q.X + Math.Cos(q.Ang) * 3.2; tz = q.Z + Math.Sin(q.Ang) * 3.2; hunting = true;
                    break;
                }
            }
            if (tx == null)
            {
                goal = PickTarget(brain, s, p);
                if (goal != null) { tx = goal.X; tz = goal.Z; }
            }
            if (brain.Level == "toddler")
            {
                if (brain.Wander == null || rnd.Next() < 0.25) brain.Wander = Rules.WrapAng(p.Ang + (rnd.Next() - 0.5) * 2.2);
            }
            double toTarget = tx != null ? Math.Atan2(tz.Value - p.Z, tx.Value - p.X) : (brain.Wander ?? p.Ang);

            bool careful = rnd.Next() < cfg.Care;
            double bestA = p.Ang, bestSc = -1e9, bestClear = 0, aheadClear = cfg.Look;
            var cands = new List<Cand>();
            for (int k = 0; k < N_DIR; k++)
            {
                double a = Rules.WrapAng(p.Ang + ((double)k / N_DIR) * TAU - Math.PI);
                double turn = Math.Abs(Rules.WrapAng(a - p.Ang));
                double clear = careful ? Probe(s, p, a, cfg.Look, enemies, null) : cfg.Look;
                if (turn < 0.2) aheadClear = Math.Min(aheadClear, clear);
                double sc = (clear / cfg.Look) * 3;
                if (clear < 1.6 + p.R) sc -= 8;
                sc += Math.Cos(Rules.WrapAng(a - toTarget)) * cfg.FoodW * (brain.Level == "toddler" ? 0.6 : 1);
                sc += Math.Cos(turn) * 0.35;
                if (cfg.Noise != 0) sc += (rnd.Next() - 0.5) * cfg.Noise;
                cands.Add(new Cand { A = a, Sc = sc, Clear = clear, Ex = endX, Ez = endZ, Eh = endH });
            }
            if (cfg.Room)
            {
                cands = cands.OrderByDescending(c => c.Sc).ToList();   // 穩定排序，與 JS 相同
                for (int i = 0; i < Math.Min(5, cands.Count); i++)
                {
                    var c = cands[i];
                    if (c.Clear < cfg.Look) continue;
                    var from = new Start { X = c.Ex, Z = c.Ez, H = c.Eh };
                    double room = 0;
                    foreach (double off in new[] { 0, -0.9, 0.9, -1.8, 1.8 })
                        room += Probe(s, p, Rules.WrapAng(c.Eh + off), cfg.Look, null, from);
                    c.Sc += (room / (cfg.Look * 5)) * 3 - 1.5;
                }
            }
            foreach (var c in cands) if (c.Sc > bestSc) { bestSc = c.Sc; bestA = c.A; bestClear = c.Clear; }
            Rules.Aim(s, p.Slot, bestA);

            bool boost = false;
            if (cfg.Boost >= 1 && goal != null && goal.Type == 1 && Rules.Hypot(goal.X - p.X, goal.Z - p.Z) < 9 && bestClear >= cfg.Look * 0.8 && p.Energy > 0.35) boost = true;
            if (cfg.Boost >= 2)
            {
                if (hunting && p.Energy > 0.4 && bestClear >= cfg.Look * 0.7) boost = true;
                if (goal != null && goal.Type == 2 && Rules.Hypot(goal.X - p.X, goal.Z - p.Z) < 6 && p.Energy > 0.6 && bestClear >= cfg.Look * 0.8) boost = true;
            }
            Rules.SetBoost(s, p.Slot, boost);
        }

        /// <summary>每隔幾個節拍想一次（各條蛇錯開），其餘時間維持上次的決定。</summary>
        public static void Drive(Brain brain, GameState s, PlayerState p)
        {
            if (p == null || !p.Alive || s.Phase == "over") return;
            if (s.Phase == "countdown")
            {
                int c = (int)Math.Ceiling(s.Countdown);
                if (brain.LastC != c) { brain.LastC = c; Think(brain, s, p); }
                return;
            }
            if ((s.Tick + p.Slot) % brain.Cfg.Every != 0) return;
            Think(brain, s, p);
        }
    }
}
