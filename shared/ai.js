/* ===== ai.js — 電腦對手（四段難度，瀏覽器與伺服器共用） =====
 *
 * 電腦跟真人走同一套規則：只透過 Rules.aim（想去的角度）與 Rules.setBoost 下指令，不偷改狀態。
 * 每次思考時，往 16 個方向「探路」（從頭往前一步一步檢查圍牆、石頭、蛇身），
 * 再依「前方空不空、離目標近不近、要不要大轉彎」打分數挑方向。難度差異：
 *   幼幼班  很久才想一次、看得很近，常常發呆亂晃，有時根本沒注意前面
 *   簡單    看得近，朝最近的果實走，不會躲別人的頭、不會加速
 *   普通    看得遠，挑「划算」的果實（金星、發光果實），會躲別人頭的去路，看到金星會加速
 *   困難    看得最遠、想得最勤，會搶別人搶不到的果實、繞到比自己短的蛇前面攔截、危險時加速逃開
 */
import R from './rules.js';

const AI = (function () {
  'use strict';

  const LEVEL_ORDER = ['toddler', 'easy', 'normal', 'hard'];
  const LEVELS = {
    toddler: { name: '幼幼班', every: 8, look: 3.0, care: 0.55, foodW: 0.35, noise: 0.9, heads: false, boost: 0, hunt: false },
    easy:    { name: '簡單',   every: 5, look: 4.5, care: 1, foodW: 1.0, noise: 0.15, heads: false, boost: 0, hunt: false },
    normal:  { name: '普通',   every: 3, look: 9, care: 1, foodW: 1.1, noise: 0, heads: true, boost: 1, hunt: false },
    hard:    { name: '困難',   every: 2, look: 12, care: 1, foodW: 1.25, noise: 0, heads: true, boost: 2, hunt: true, room: true }
  };
  const TAU = Math.PI * 2;
  const N_DIR = 16;

  function createBrain(level, seed) {
    const lv = LEVELS[level] ? level : 'normal';
    return { level: lv, cfg: LEVELS[lv], rnd: R.rng((seed == null ? 7 : seed) >>> 0), wander: null, target: null, boostUntil: 0 };
  }

  /* 模擬「從現在的方向一邊轉向 a 一邊前進」的路線，回傳走多遠會撞到東西（沒撞到就回傳 look）。
   * 轉彎需要時間，所以要沿著真正會走的弧線檢查，而不是直直的射線。 */
  const END = { x: 0, z: 0, h: 0 };
  function probe(s, p, a, look, enemies, from) {
    const skip = R.selfSkip(p.r) + 3;
    const margin = p.r + 0.25;
    const stepLen = 0.45, rate = R.turnRate(p.r) * (stepLen / Math.max(1, p.v));
    let x = from ? from.x : p.x, z = from ? from.z : p.z, h = from ? from.h : p.ang;
    END.x = x; END.z = z; END.h = h;
    for (let d = stepLen; d <= look; d += stepLen) {
      const da = R.wrapAng(a - h);
      h += Math.max(-rate, Math.min(rate, da));
      x += Math.cos(h) * stepLen; z += Math.sin(h) * stepLen;
      if (Math.hypot(x, z) + margin > s.R) return d;
      for (const k of s.rocks) if (Math.hypot(k[0] - x, k[1] - z) < k[2] + margin) return d;
      let hit = false;
      R.nearPts(s, x, z, margin + 1, (q, i) => {
        if (q === p && i < skip) return false;
        if (!q.alive) return false;
        const qx = q.pts[i * 2], qz = q.pts[i * 2 + 1];
        if (qx == null) return false;
        if (Math.hypot(qx - x, qz - z) < margin + q.r) { hit = true; return true; }
        return false;
      });
      if (hit) return d;
      if (enemies) {
        const t = d / Math.max(1, p.v);
        for (const e of enemies) {
          const ex = e.x + Math.cos(e.ang) * e.v * t, ez = e.z + Math.sin(e.ang) * e.v * t;
          if (Math.hypot(ex - x, ez - z) < margin + e.r + 0.6) return d;
        }
      }
      END.x = x; END.z = z; END.h = h;
    }
    return look;
  }

  function pickTarget(brain, s, p) {
    const cfg = brain.cfg;
    let best = null, bestSc = -1e9;
    const others = s.players.filter(q => q.alive && q !== p);
    for (const f of s.foods) {
      const d = Math.hypot(f.x - p.x, f.z - p.z);
      if (d > 40) continue;
      const F = R.FOOD[f.type];
      let sc;
      if (brain.level === 'easy' || brain.level === 'toddler') sc = -d;
      else {
        sc = ((f.type === 0 ? R.fruitScore(f.v) : F.score) + 6) / (d + 4);
        /* 正後方的果實要大迴轉，打點折扣 */
        const da = Math.abs(R.wrapAng(Math.atan2(f.z - p.z, f.x - p.x) - p.ang));
        sc *= 1 - da / (Math.PI * 1.6);
        if (f.type === 1 && f.ttl > 0 && d / p.base > f.ttl) continue;
        if (Math.hypot(f.x, f.z) > s.R - p.r - 1.2) sc *= 0.3;     /* 太靠牆的不太去 */
        if (cfg.hunt) {
          for (const q of others) if (Math.hypot(f.x - q.x, f.z - q.z) < d * 0.6) { sc *= 0.35; break; }
        }
      }
      if (sc > bestSc) { bestSc = sc; best = f; }
    }
    return best;
  }

  function think(brain, s, p) {
    const cfg = brain.cfg, rnd = brain.rnd;
    if (!s.grid) R.buildGrid(s);
    const enemies = cfg.heads ? s.players.filter(q => q.alive && q !== p && Math.hypot(q.x - p.x, q.z - p.z) < cfg.look + 6) : null;

    /* 目標點 */
    let tx = null, tz = null, goal = null, hunting = false;
    if (cfg.hunt && p.len > R.START_LEN + 6) {
      for (const q of s.players) {
        if (!q.alive || q === p || q.len > p.len * 1.15) continue;
        const d = Math.hypot(q.x - p.x, q.z - p.z);
        if (d > 9 || d < 2) continue;
        tx = q.x + Math.cos(q.ang) * 3.2; tz = q.z + Math.sin(q.ang) * 3.2; hunting = true;
        break;
      }
    }
    if (tx == null) {
      goal = pickTarget(brain, s, p);
      if (goal) { tx = goal.x; tz = goal.z; }
    }
    if (brain.level === 'toddler') {
      if (brain.wander == null || rnd() < 0.25) brain.wander = R.wrapAng(p.ang + (rnd() - 0.5) * 2.2);
    }
    const toTarget = tx != null ? Math.atan2(tz - p.z, tx - p.x) : (brain.wander != null ? brain.wander : p.ang);

    const careful = rnd() < cfg.care;
    let bestA = p.ang, bestSc = -1e9, bestClear = 0, aheadClear = cfg.look;
    const cands = [];
    for (let k = 0; k < N_DIR; k++) {
      const a = R.wrapAng(p.ang + (k / N_DIR) * TAU - Math.PI);
      const turn = Math.abs(R.wrapAng(a - p.ang));
      const clear = careful ? probe(s, p, a, cfg.look, enemies) : cfg.look;
      if (turn < 0.2) aheadClear = Math.min(aheadClear, clear);
      let sc = (clear / cfg.look) * 3;
      if (clear < 1.6 + p.r) sc -= 8;
      sc += Math.cos(R.wrapAng(a - toTarget)) * cfg.foodW * (brain.level === 'toddler' ? 0.6 : 1);
      sc += Math.cos(turn) * 0.35;
      if (cfg.noise) sc += (rnd() - 0.5) * cfg.noise;
      cands.push({ a, sc, clear, ex: END.x, ez: END.z, eh: END.h });
    }
    /* 困難：前幾名再看「走過去之後還有多少空間」，避免鑽進自己圍出來的死路 */
    if (cfg.room) {
      cands.sort((u, v) => v.sc - u.sc);
      for (let i = 0; i < Math.min(5, cands.length); i++) {
        const c = cands[i];
        if (c.clear < cfg.look) continue;
        const from = { x: c.ex, z: c.ez, h: c.eh };
        let room = 0;
        for (const off of [0, -0.9, 0.9, -1.8, 1.8]) room += probe(s, p, R.wrapAng(c.eh + off), cfg.look, null, from);
        c.sc += (room / (cfg.look * 5)) * 3 - 1.5;
      }
    }
    for (const c of cands) if (c.sc > bestSc) { bestSc = c.sc; bestA = c.a; bestClear = c.clear; }
    R.aim(s, p.slot, bestA);

    /* 加速 */
    let boost = false;
    if (cfg.boost >= 1 && goal && goal.type === 1 && Math.hypot(goal.x - p.x, goal.z - p.z) < 9 && bestClear >= cfg.look * 0.8 && p.energy > 0.35) boost = true;
    if (cfg.boost >= 2) {
      if (hunting && p.energy > 0.4 && bestClear >= cfg.look * 0.7) boost = true;
      if (goal && goal.type === 2 && Math.hypot(goal.x - p.x, goal.z - p.z) < 6 && p.energy > 0.6 && bestClear >= cfg.look * 0.8) boost = true;
    }
    R.setBoost(s, p.slot, boost);
  }

  /* 對外：每隔幾個節拍想一次（各條蛇錯開），其餘時間維持上次的決定 */
  function drive(brain, s, p) {
    if (!p || !p.alive || s.phase === 'over') return;
    if (s.phase === 'countdown') { if (brain.lastC !== Math.ceil(s.countdown)) { brain.lastC = Math.ceil(s.countdown); think(brain, s, p); } return; }
    if ((s.tick + p.slot) % brain.cfg.every !== 0) return;
    think(brain, s, p);
  }

  return { LEVELS, LEVEL_ORDER, createBrain, think, drive };
})();

export default AI;
