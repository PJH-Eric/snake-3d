/* ===== rules.js — 自由方向貪吃蛇規則核心（純邏輯、可注入種子、前後端共用） =====
 *
 * 場地是圓形圍欄（半徑 R），蛇用「目標角度」控制方向，每秒最多轉 TURN 弧度（越粗轉得越慢），
 * 所以有最小迴轉半徑。蛇身取自頭走過的軌跡，每 SEG 一節；越長越粗。
 * 全場共用固定節拍 DT（1/30 秒），所有蛇先一起移動，再一起判定碰撞（公平的同時碰撞）。
 *
 * 輸入：Rules.aim(s, slot, angle) 指定想去的方向；Rules.setBoost(s, slot, on) 加速（消耗能量條）。
 * 快照：startInfo → viewFromStart → applySnapshot；畫面端只讀 view，不跑規則。
 * 角度：0 = 世界 +x（畫面右），PI/2 = 世界 +z（畫面下）。
 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.Rules = factory();
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  const DT = 1 / 30;
  const COUNTDOWN = 3;
  const END_HOLD = 1.6;
  const SEG = 0.42;                 /* 蛇身每節間距 */
  const START_LEN = 10;             /* 開局節數 */
  const BASE_R = 0.42;              /* 開局粗細（半徑） */
  const MAX_EXTRA_R = 0.5;
  const TURN = 4.2;                 /* 每秒最大轉向（弧度），粗了會變慢 */
  const BOOST_MUL = 1.75;
  const ENERGY_DRAIN = 0.45, ENERGY_REGEN = 0.16, ENERGY_RESUME = 0.15;
  const GROW_RATE = 8;              /* 每秒最多長幾節（吃到的長度慢慢長出來） */
  const THEME_COUNT = 6;
  const TIME_LIMITS = [0, 120, 180, 300];
  const SPEEDS = { slow: { v: 3.8, name: '慢' }, normal: { v: 4.8, name: '中' }, fast: { v: 5.8, name: '快' } };
  const SPEED_ORDER = ['slow', 'normal', 'fast'];
  const OBSTACLES = { none: { k: 0, name: '沒有' }, few: { k: 0.007, name: '少量' }, many: { k: 0.015, name: '很多' } };
  const OBSTACLE_ORDER = ['none', 'few', 'many'];
  const SLOT_COLORS = ['#ff7a45', '#3fa7f5', '#ff6f9f', '#4cc26a', '#a58bff', '#ffc233', '#2ec7c0', '#e0584d'];
  /* 果實：0 蘋果、1 金星、2 發光果實（出局的蛇變的） */
  const FOOD = [
    { name: 'apple', score: 10, grow: 2, ttl: 0, rad: 0.42 },
    { name: 'gold', score: 30, grow: 6, ttl: 15, rad: 0.55 },
    { name: 'orb', score: 5, grow: 1, ttl: 45, rad: 0.36 }
  ];
  const MAX_FOODS = 260;
  const MAX_ORBS_PER_DEATH = 50;
  const TAU = Math.PI * 2;

  function rng(seed) {
    let a = seed >>> 0;
    return function () {
      a = (a + 0x6D2B79F5) >>> 0;
      let t = a;
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  const wrapAng = a => Math.atan2(Math.sin(a), Math.cos(a));
  const arenaFor = n => (n <= 1 ? 24 : n <= 4 ? 30 : 38);
  const radiusFor = len => BASE_R + Math.min(MAX_EXTRA_R, Math.max(0, len - START_LEN) * 0.004);
  const turnRate = r => TURN * Math.sqrt(BASE_R / r);
  const appleTarget = R => Math.round(R * R * 0.045);
  /* 碰到自己：要隔開幾節以上才算（脖子附近不算） */
  const selfSkip = r => Math.ceil((r * 1.6) / SEG) + 2;

  /* ---------- 建立對局 ---------- */
  function createGame(o) {
    const rnd = rng(o.seed >>> 0);
    const n = o.players.length;
    const speed = SPEEDS[o.speed] ? o.speed : 'normal';
    const mode = n === 1 ? 'challenge' : 'versus';
    const R = arenaFor(n);
    const s = {
      seed: o.seed >>> 0, rnd, R, mode, speed,
      themeId: o.themeId != null && o.themeId >= 0 ? (o.themeId | 0) % THEME_COUNT : Math.floor(rnd() * THEME_COUNT),
      timeLimit: mode === 'challenge' ? 0 : (TIME_LIMITS.indexOf(o.timeLimit) >= 0 ? o.timeLimit : 0),
      obstacles: OBSTACLES[o.obstacles] ? o.obstacles : 'none',
      rocks: [], players: [], foods: [], foodSeq: 1, track: !!o.track, fAdd: [], fRem: [],
      phase: 'countdown', countdown: COUNTDOWN, lastCount: 0, time: 0, tick: 0, events: [], endHold: 0, result: null, lastDead: []
    };
    /* 出生點：圓周上平均分佈、打亂，頭朝中心；單人挑戰從中間出發 */
    const spawns = [];
    if (mode === 'challenge') spawns.push({ x: -2, z: 0, a: 0 });
    else {
      const off = rnd() * TAU;
      const order = [];
      for (let i = 0; i < 8; i++) order.push({ i, k: rnd() });
      order.sort((a, b) => a.k - b.k);
      for (let i = 0; i < 8; i++) {
        const t = off + (order[i].i / 8) * TAU;
        spawns.push({ x: Math.cos(t) * R * 0.62, z: Math.sin(t) * R * 0.62, a: wrapAng(t + Math.PI + (rnd() - 0.5) * 0.6) });
      }
    }
    genRocks(s, spawns, rnd);
    const base = SPEEDS[speed].v;
    o.players.forEach((pl, i) => {
      const sp = spawns[i % spawns.length];
      const p = {
        slot: pl.slot != null ? pl.slot : i, name: pl.name || ('玩家' + (i + 1)), animal: pl.animal || 'cat', kind: pl.kind || 'human', level: pl.level || null,
        alive: true, left: false, x: sp.x, z: sp.z, ang: sp.a, ta: sp.a, len: START_LEN, grow: 0, r: BASE_R, v: base, base,
        trail: [], pts: null, score: 0, kills: 0, eaten: 0, boost: false, boosting: false, energy: 1, tired: false,
        deathTime: -1, finalLen: START_LEN
      };
      /* 起始軌跡：從頭往後拉一條直線 */
      const L = START_LEN * SEG + 1;
      for (let d = L; d >= 0; d -= 0.2) p.trail.push(sp.x - Math.cos(sp.a) * d, sp.z - Math.sin(sp.a) * d);
      p.trail.push(sp.x, sp.z);
      samplePts(p);
      s.players.push(p);
    });
    for (let i = 0; i < appleTarget(R); i++) addFood(s, 0);
    return s;
  }

  function genRocks(s, spawns, rnd) {
    const R = s.R;
    const want = Math.round(Math.PI * R * R * OBSTACLES[s.obstacles].k);
    let tries = 0;
    while (s.rocks.length < want && tries++ < 400) {
      const t = rnd() * TAU, d = Math.sqrt(rnd()) * (R - 3.5);
      const x = Math.cos(t) * d, z = Math.sin(t) * d, r = 0.7 + rnd() * 0.7;
      let ok = true;
      for (const sp of spawns) {
        /* 出生點與它前方一段路都不放石頭 */
        if (Math.hypot(sp.x - x, sp.z - z) < 6 + r) { ok = false; break; }
        const fx = sp.x + Math.cos(sp.a) * 6, fz = sp.z + Math.sin(sp.a) * 6;
        if (Math.hypot(fx - x, fz - z) < 4 + r) { ok = false; break; }
      }
      if (ok) for (const k of s.rocks) if (Math.hypot(k[0] - x, k[1] - z) < k[2] + r + 2.2) { ok = false; break; }
      if (ok) s.rocks.push([x, z, r]);
    }
  }

  const playerBySlot = (s, slot) => s.players.find(p => p.slot === slot);

  /* 依軌跡取出每節位置（頭在前），並剪掉用不到的舊軌跡 */
  function samplePts(p) {
    const tr = p.trail;
    const count = Math.floor(p.len) + 1;
    const pts = new Array(count * 2);
    let k = tr.length - 2;
    pts[0] = tr[k]; pts[1] = tr[k + 1];
    let made = 1, need = SEG, px = tr[k], pz = tr[k + 1];
    while (made < count && k >= 2) {
      const qx = tr[k - 2], qz = tr[k - 1];
      const d = Math.hypot(qx - px, qz - pz);
      if (d >= need) {
        const f = need / d;
        px += (qx - px) * f; pz += (qz - pz) * f;
        pts[made * 2] = px; pts[made * 2 + 1] = pz; made++;
        need = SEG;
      } else { need -= d; px = qx; pz = qz; k -= 2; }
    }
    while (made < count) { pts[made * 2] = px; pts[made * 2 + 1] = pz; made++; }
    if (k > 400) tr.splice(0, k - 40);
    p.pts = pts;
  }

  /* ---------- 果實 ---------- */
  function addFood(s, type, x, z, v) {
    if (s.foods.length >= MAX_FOODS) return null;
    if (x == null) {
      let ok = false;
      for (let tries = 0; tries < 12 && !ok; tries++) {
        const t = s.rnd() * TAU, d = Math.sqrt(s.rnd()) * (s.R - 1.4);
        x = Math.cos(t) * d; z = Math.sin(t) * d; ok = true;
        for (const k of s.rocks) if (Math.hypot(k[0] - x, k[1] - z) < k[2] + 0.7) { ok = false; break; }
      }
      if (!ok) return null;
    }
    const f = { id: s.foodSeq++, x, z, type, v: v != null ? v : Math.floor(s.rnd() * 3), ttl: FOOD[type].ttl };
    s.foods.push(f);
    if (s.track) s.fAdd.push(f);
    return f;
  }
  function removeFood(s, i) {
    const f = s.foods[i];
    s.foods.splice(i, 1);
    if (s.track) s.fRem.push(f.id);
  }

  /* ---------- 輸入 ---------- */
  function aim(s, slot, angle) {
    const p = playerBySlot(s, slot);
    if (!p || !p.alive || typeof angle !== 'number' || !isFinite(angle)) return false;
    p.ta = wrapAng(angle);
    return true;
  }
  function setBoost(s, slot, on) {
    const p = playerBySlot(s, slot);
    if (p && p.alive) p.boost = !!on;
  }

  /* ---------- 一個節拍 ---------- */
  function step(s, dt) {
    s.events = [];
    if (s.phase === 'countdown') {
      s.countdown -= dt;
      const c = Math.max(0, Math.ceil(s.countdown - 1e-9));
      if (c !== s.lastCount && c > 0) { s.lastCount = c; s.events.push({ t: 'count', n: c }); }
      if (s.countdown <= 1e-9) { s.phase = 'play'; s.events.push({ t: 'go' }); }
      return s;
    }
    if (s.phase === 'over') { s.endHold -= dt; return s; }

    s.time += dt; s.tick++;
    for (let i = s.foods.length - 1; i >= 0; i--) {
      const f = s.foods[i];
      if (f.ttl > 0) { f.ttl -= dt; if (f.ttl <= 0) removeFood(s, i); }
    }
    for (const p of s.players) if (p.alive) movePlayer(s, p, dt);
    collide(s);
    eat(s);
    refill(s, dt);
    checkEnd(s);
    return s;
  }

  function turnToward(p, dt) {
    const da = wrapAng(p.ta - p.ang), mx = turnRate(p.r) * dt;
    p.ang = wrapAng(p.ang + Math.max(-mx, Math.min(mx, da)));
  }

  function movePlayer(s, p, dt) {
    /* 能量條：加速時消耗，放開回充；用光後要回到一定量才能再加速 */
    if (p.boost && !p.tired && p.energy > 0) {
      p.boosting = true;
      p.energy -= ENERGY_DRAIN * dt;
      if (p.energy <= 0) { p.energy = 0; p.tired = true; p.boosting = false; }
    } else {
      p.boosting = false;
      p.energy = Math.min(1, p.energy + ENERGY_REGEN * dt);
      if (p.tired && p.energy >= ENERGY_RESUME) p.tired = false;
    }
    turnToward(p, dt);
    const mul = s.mode === 'challenge' ? Math.min(1.5, 1 + 0.04 * Math.floor(p.eaten / 5)) : 1;
    p.v = p.base * mul * (p.boosting ? BOOST_MUL : 1);
    p.x += Math.cos(p.ang) * p.v * dt;
    p.z += Math.sin(p.ang) * p.v * dt;
    p.trail.push(p.x, p.z);
    if (p.grow > 0) { const g = Math.min(p.grow, GROW_RATE * dt); p.len += g; p.grow -= g; }
    p.r = radiusFor(p.len);
    samplePts(p);
  }

  /* 空間格：每 2 單位一格，存所有活著的蛇身（第 1 節以後） */
  const CELL = 2;
  function buildGrid(s) {
    const g = new Map();
    for (const p of s.players) {
      if (!p.alive) continue;
      const pts = p.pts, n = pts.length / 2;
      for (let i = 1; i < n; i++) {
        const x = pts[i * 2], z = pts[i * 2 + 1];
        const key = Math.floor(x / CELL) * 4096 + Math.floor(z / CELL);
        let a = g.get(key);
        if (!a) { a = []; g.set(key, a); }
        a.push(p, i);
      }
    }
    s.grid = g;
    return g;
  }
  /* 對格子裡 (x,z) 附近 rad 範圍的每一節呼叫 fn(p, i)；fn 回傳 true 就停 */
  function nearPts(s, x, z, rad, fn) {
    const g = s.grid;
    if (!g) return;
    const x0 = Math.floor((x - rad) / CELL), x1 = Math.floor((x + rad) / CELL);
    const z0 = Math.floor((z - rad) / CELL), z1 = Math.floor((z + rad) / CELL);
    for (let cx = x0; cx <= x1; cx++) for (let cz = z0; cz <= z1; cz++) {
      const a = g.get(cx * 4096 + cz);
      if (!a) continue;
      for (let k = 0; k < a.length; k += 2) if (fn(a[k], a[k + 1])) return;
    }
  }

  function collide(s) {
    buildGrid(s);
    const alive = s.players.filter(p => p.alive);
    const dead = new Map();
    /* 頭撞頭：兩條都出局 */
    for (let a = 0; a < alive.length; a++) for (let b = a + 1; b < alive.length; b++) {
      const p = alive[a], q = alive[b];
      if (Math.hypot(p.x - q.x, p.z - q.z) < (p.r + q.r) * 0.8) {
        if (!dead.has(p.slot)) dead.set(p.slot, { p, why: 'head', killer: null });
        if (!dead.has(q.slot)) dead.set(q.slot, { p: q, why: 'head', killer: null });
      }
    }
    for (const p of alive) {
      if (dead.has(p.slot)) continue;
      if (Math.hypot(p.x, p.z) + p.r * 0.6 > s.R) { dead.set(p.slot, { p, why: 'wall', killer: null }); continue; }
      let hit = null;
      for (const k of s.rocks) if (Math.hypot(k[0] - p.x, k[1] - p.z) < k[2] + p.r * 0.75) { hit = { p, why: 'rock', killer: null }; break; }
      if (!hit) {
        const skip = selfSkip(p.r);
        nearPts(s, p.x, p.z, p.r + 1.2, (q, i) => {
          if (q === p && i < skip) return false;
          const qx = q.pts[i * 2], qz = q.pts[i * 2 + 1];
          if (Math.hypot(qx - p.x, qz - p.z) < (p.r + q.r) * 0.8) { hit = { p, why: q === p ? 'self' : 'other', killer: q === p ? null : q.slot }; return true; }
          return false;
        });
      }
      if (hit) dead.set(p.slot, hit);
    }
    for (const d of dead.values()) {
      if (d.killer != null) { const k = playerBySlot(s, d.killer); if (k && !dead.has(k.slot)) k.kills++; }
      killPlayer(s, d.p, d.why, d.killer);
    }
  }

  function eat(s) {
    for (const p of s.players) {
      if (!p.alive) continue;
      for (let i = s.foods.length - 1; i >= 0; i--) {
        const f = s.foods[i], F = FOOD[f.type];
        const dx = f.x - p.x, dz = f.z - p.z, rr = p.r + F.rad + 0.15;
        if (dx * dx + dz * dz > rr * rr) continue;
        removeFood(s, i);
        p.score += F.score; p.grow += F.grow; p.eaten++;
        s.events.push({ t: 'eat', slot: p.slot, type: f.type, v: f.v, x: f.x, z: f.z, id: f.id });
      }
    }
  }

  function refill(s, dt) {
    let apples = 0, golds = 0;
    for (const f of s.foods) { if (f.type === 0) apples++; else if (f.type === 1) golds++; }
    const want = appleTarget(s.R);
    for (let k = 0; k < 2 && apples < want; k++, apples++) addFood(s, 0);
    if (s.time > 8 && golds < 2 && s.rnd() < 0.08 * dt) addFood(s, 1);
  }

  function killPlayer(s, p, why, killer) {
    if (!p.alive) return;
    p.alive = false; p.deathTime = s.time; p.finalLen = Math.round(p.len); p.boost = false; p.boosting = false;
    /* 整條身體變成發光果實（每隔一節一顆） */
    const pts = p.pts, n = pts.length / 2;
    const step2 = Math.max(2, Math.ceil(n / MAX_ORBS_PER_DEATH));
    let orbs = 0;
    for (let i = 0; i < n; i += step2) {
      const x = pts[i * 2] + (s.rnd() - 0.5) * 0.5, z = pts[i * 2 + 1] + (s.rnd() - 0.5) * 0.5;
      if (Math.hypot(x, z) > s.R - 0.8) continue;
      if (addFood(s, 2, x, z, p.slot)) orbs++;
    }
    s.events.push({ t: 'die', slot: p.slot, why, killer: killer != null ? killer : null, x: p.x, z: p.z, orbs });
    s.lastDead.push(p.slot);
    p.pts = []; p.trail = [];
  }

  function removePlayer(s, slot) {
    const p = playerBySlot(s, slot);
    if (!p) return;
    p.left = true;
    if (p.alive) killPlayer(s, p, 'left', null);
  }

  /* ---------- 勝負 ---------- */
  function checkEnd(s) {
    if (s.phase !== 'play') return;
    const alive = s.players.filter(p => p.alive);
    if (s.mode === 'challenge') {
      if (!alive.length) finish(s, null, 'crash');
      s.lastDead = [];
      return;
    }
    if (alive.length <= 1) {
      if (alive.length === 1) { finish(s, alive[0].slot, 'last'); return; }
      /* 最後幾個同時出局：分數高的贏，同分平手 */
      const last = s.players.filter(p => s.lastDead.indexOf(p.slot) >= 0 && !p.left);
      const pool = last.length ? last : s.players;
      const best = Math.max.apply(null, pool.map(p => p.score));
      const top = pool.filter(p => p.score === best);
      finish(s, top.length === 1 ? top[0].slot : null, 'together');
      return;
    }
    if (s.timeLimit > 0 && s.time >= s.timeLimit) {
      const best = Math.max.apply(null, alive.map(p => p.score));
      const top = alive.filter(p => p.score === best);
      finish(s, top.length === 1 ? top[0].slot : null, 'time');
    }
    s.lastDead = [];
  }

  function finish(s, winner, reason) {
    s.phase = 'over'; s.endHold = END_HOLD;
    const ranking = s.players.slice().sort((a, b) => {
      if (a.slot === winner) return -1;
      if (b.slot === winner) return 1;
      if (a.alive !== b.alive) return a.alive ? -1 : 1;
      if (a.alive && b.alive) return b.score - a.score;
      if (a.deathTime !== b.deathTime) return b.deathTime - a.deathTime;
      return b.score - a.score;
    }).map(p => p.slot);
    s.result = {
      mode: s.mode, winner, reason, time: s.time, ranking,
      scores: s.players.map(p => [p.slot, p.score]),
      kills: s.players.map(p => [p.slot, p.kills]),
      lens: s.players.map(p => [p.slot, p.alive ? Math.round(p.len) : p.finalLen]),
      level: s.mode === 'challenge' ? 1 + Math.floor(s.players[0].eaten / 5) : 0,
      eaten: s.players.map(p => [p.slot, p.eaten])
    };
    s.events.push({ t: 'over', winner, reason });
  }

  /* ---------- 快照（伺服器 → 畫面） ---------- */
  const q1 = n => Math.round(n * 10);
  const r2 = n => Math.round(n * 100) / 100;
  const encFood = f => [f.id, q1(f.x), q1(f.z), f.type, f.v, Math.ceil(f.ttl)];
  const decFood = a => ({ id: a[0], x: a[1] / 10, z: a[2] / 10, type: a[3], v: a[4], ttl: a[5] });

  /* 蛇身：每隔一節取一點，0.1 單位量化，後面的點存差值 */
  function encPts(pts) {
    const out = [];
    const n = pts.length / 2;
    let lx = 0, lz = 0;
    for (let i = 0; i < n; i += 2) {
      const x = q1(pts[i * 2]), z = q1(pts[i * 2 + 1]);
      out.push(x - lx, z - lz); lx = x; lz = z;
    }
    if (n > 1 && (n - 1) % 2) { const x = q1(pts[(n - 1) * 2]), z = q1(pts[(n - 1) * 2 + 1]); out.push(x - lx, z - lz); }
    return out;
  }
  function decPts(a) {
    const out = new Array(a.length);
    let x = 0, z = 0;
    for (let i = 0; i < a.length; i += 2) { x += a[i]; z += a[i + 1]; out[i] = x / 10; out[i + 1] = z / 10; }
    return out;
  }

  function startInfo(s) {
    return {
      R: s.R, mode: s.mode, themeId: s.themeId, timeLimit: s.timeLimit, speed: s.speed, obstacles: s.obstacles,
      rocks: s.rocks.map(k => [r2(k[0]), r2(k[1]), r2(k[2])]),
      foods: s.foods.map(encFood),
      players: s.players.map(p => ({ slot: p.slot, name: p.name, animal: p.animal, kind: p.kind, level: p.level }))
    };
  }

  function snapshot(s, full) {
    const snap = {
      ph: s.phase === 'countdown' ? 0 : s.phase === 'play' ? 1 : 2,
      t: r2(s.time), c: s.phase === 'countdown' ? Math.max(0, Math.ceil(s.countdown - 1e-9)) : 0,
      p: s.players.map(p => [p.alive ? 1 : 0, p.score, p.kills, p.eaten, p.boosting ? 1 : 0, p.left ? 1 : 0,
        Math.round(p.energy * 100), q1(p.alive ? p.len : p.finalLen), Math.round(p.ang * 100), Math.round(p.ta * 100), p.alive ? encPts(p.pts) : [], p.tired ? 1 : 0])
    };
    /* full：完整果實清單並清掉累積的差異；peek：給單一連線補完整清單，不動差異（其他人還要收） */
    if (full === 'peek') snap.f = s.foods.map(encFood);
    else if (full) { snap.f = s.foods.map(encFood); s.fAdd = []; s.fRem = []; }
    else {
      if (s.fAdd.length) snap.fa = s.fAdd.map(encFood);
      if (s.fRem.length) snap.fr = s.fRem.slice();
      s.fAdd = []; s.fRem = [];
    }
    return snap;
  }

  function viewFromStart(info) {
    const v = {
      R: info.R, mode: info.mode, themeId: info.themeId, timeLimit: info.timeLimit, speed: info.speed,
      rocks: info.rocks.map(k => k.slice()), phase: 'countdown', countdown: COUNTDOWN, time: 0,
      foods: (info.foods || []).map(decFood), players: [], snapAt: 0, prevAt: 0, gap: 0.1
    };
    for (const pl of info.players) {
      v.players.push({ slot: pl.slot, name: pl.name, animal: pl.animal, kind: pl.kind, level: pl.level, alive: true, left: false,
        pts: [], prevPts: null, x: 0, z: 0, ang: 0, ta: 0, r: BASE_R, len: START_LEN, score: 0, kills: 0, eaten: 0,
        boosting: false, energy: 1, tired: false, finalLen: START_LEN });
    }
    return v;
  }

  function applySnapshot(v, snap, now) {
    v.phase = snap.ph === 0 ? 'countdown' : snap.ph === 1 ? 'play' : 'over';
    v.time = snap.t; v.countdown = snap.c;
    const t = now != null ? now : 0;
    if (v.snapAt) { const g = t - v.snapAt; if (g > 0.01 && g < 1) v.gap = v.gap * 0.8 + g * 0.2; }
    v.prevAt = v.snapAt; v.snapAt = t;
    snap.p.forEach((a, i) => {
      const p = v.players[i];
      if (!p) return;
      const wasAlive = p.alive;
      p.alive = !!a[0]; p.score = a[1]; p.kills = a[2]; p.eaten = a[3]; p.boosting = !!a[4]; p.left = !!a[5];
      p.energy = a[6] / 100; p.len = a[7] / 10; p.prevAng = p.ang; p.ang = a[8] / 100; p.ta = a[9] / 100; p.tired = !!a[11];
      p.r = radiusFor(p.len);
      p.prevPts = wasAlive && p.pts && p.pts.length ? p.pts : null;
      p.pts = p.alive ? decPts(a[10]) : [];
      if (p.pts.length) { p.x = p.pts[0]; p.z = p.pts[1]; }
      if (p.alive) p.finalLen = Math.round(p.len); else if (wasAlive) p.finalLen = Math.round(p.len);
    });
    if (snap.f) v.foods = snap.f.map(decFood);
    else {
      if (snap.fr && snap.fr.length) { const rm = new Set(snap.fr); v.foods = v.foods.filter(f => !rm.has(f.id)); }
      if (snap.fa) {
        const have = new Set(v.foods.map(f => f.id));
        for (const a of snap.fa) if (!have.has(a[0])) v.foods.push(decFood(a));
      }
    }
  }

  return {
    DT, COUNTDOWN, END_HOLD, SEG, START_LEN, BASE_R, TURN, BOOST_MUL, THEME_COUNT, TIME_LIMITS, SPEEDS, SPEED_ORDER, OBSTACLES, OBSTACLE_ORDER,
    SLOT_COLORS, FOOD, MAX_FOODS, ENERGY_DRAIN, ENERGY_REGEN,
    rng, wrapAng, arenaFor, radiusFor, turnRate, selfSkip, createGame, aim, steer: aim, setBoost, step, removePlayer,
    buildGrid, nearPts, startInfo, snapshot, viewFromStart, applySnapshot, playerBySlot, encPts, decPts
  };
});
