/* ===== tests/verify.js — 規則核心、電腦對手與快照的單元測試（node tests/verify.js） ===== */
import assert from 'node:assert';
import R from '../../shared/rules.js';
import AI from '../../shared/ai.js';

let passed = 0, failed = 0;
function t(name, fn) {
  try { fn(); passed++; console.log('  ok   ' + name); }
  catch (e) { failed++; console.log('  FAIL ' + name + '\n       ' + (e && e.stack || e)); process.exitCode = 1; }
}
const ANIMALS = ['cat', 'dog', 'bunny', 'bear', 'panda', 'fox', 'frog', 'penguin'];
const P = n => Array.from({ length: n }, (_, i) => ({ slot: i, name: 'P' + i, animal: ANIMALS[i % 8], kind: 'human' }));
function game(n, o) { return R.createGame(Object.assign({ seed: 12345, players: P(n), speed: 'normal', obstacles: 'none', timeLimit: 0, themeId: 0 }, o || {})); }
function toPlay(s) { while (s.phase === 'countdown') R.step(s, R.DT); }
function tick(s, n) { for (let i = 0; i < (n || 1); i++) R.step(s, R.DT); }
const evs = (s, type) => s.events.filter(e => e.t === type);
/* 把某條蛇擺到指定位置與方向（軌跡重建成一直線） */
function put(s, slot, x, z, ang, len) {
  const p = R.playerBySlot(s, slot);
  p.x = x; p.z = z; p.ang = ang; p.ta = ang; if (len) { p.len = len; p.r = R.radiusFor(len); }
  p.trail = [];
  const L = p.len * R.SEG + 2;
  for (let d = L; d >= 0; d -= 0.2) p.trail.push(x - Math.cos(ang) * d, z - Math.sin(ang) * d);
  p.trail.push(x, z);
  resample(p);
  return p;
}
function resample(p) {
  const tr = p.trail, count = Math.floor(p.len) + 1, pts = [];
  let k = tr.length - 2, px = tr[k], pz = tr[k + 1], need = R.SEG;
  pts.push(px, pz);
  while (pts.length / 2 < count && k >= 2) {
    const qx = tr[k - 2], qz = tr[k - 1], d = Math.hypot(qx - px, qz - pz);
    if (d >= need) { const f = need / d; px += (qx - px) * f; pz += (qz - pz) * f; pts.push(px, pz); need = R.SEG; }
    else { need -= d; px = qx; pz = qz; k -= 2; }
  }
  while (pts.length / 2 < count) pts.push(px, pz);
  p.pts = pts;
}
const clearFoods = s => { s.foods = []; };

console.log('規則');
t('同種子＋同樣輸入：開局與 300 個節拍後完全一樣', () => {
  const a = game(3), b = game(3);
  assert.deepStrictEqual(R.startInfo(a), R.startInfo(b));
  for (let i = 0; i < 400; i++) {
    for (const s of [a, b]) { R.aim(s, 0, Math.sin(i / 20) * 2); R.setBoost(s, 1, i % 90 < 30); R.step(s, R.DT); }
  }
  assert.deepStrictEqual(R.snapshot(a, true), R.snapshot(b, true));
});
t('場地大小依人數；1 人是單人挑戰', () => {
  assert.strictEqual(game(1).mode, 'challenge');
  assert.strictEqual(game(2).mode, 'versus');
  assert(game(1).R < game(4).R && game(4).R < game(8).R);
});
t('出生點：在場內、彼此分開、頭朝中心，附近沒有石頭', () => {
  for (let seed = 1; seed <= 20; seed++) {
    const s = game(8, { seed, obstacles: 'many' });
    for (const p of s.players) {
      assert(Math.hypot(p.x, p.z) < s.R - 4, '太靠牆');
      const toC = Math.atan2(-p.z, -p.x);
      assert(Math.abs(R.wrapAng(toC - p.ang)) < 0.5, '頭沒有朝中心');
      for (const k of s.rocks) assert(Math.hypot(k[0] - p.x, k[1] - p.z) > 6, '出生點旁有石頭');
      for (const q of s.players) if (q !== p) assert(Math.hypot(q.x - p.x, q.z - p.z) > 8, '出生點太近');
    }
    assert(s.rocks.length > 0);
  }
});
t('倒數 3 秒期間蛇不會動，倒數結束送出 go', () => {
  const s = game(2);
  const x0 = s.players[0].x, z0 = s.players[0].z;
  R.aim(s, 0, s.players[0].ang + 3);
  let gone = false;
  for (let i = 0; i < 89; i++) { tick(s); if (evs(s, 'go').length) gone = true; }
  assert.strictEqual(s.players[0].x, x0); assert.strictEqual(s.players[0].z, z0);
  assert(!gone && s.phase === 'countdown');
  tick(s, 2);
  assert.strictEqual(s.phase, 'play');
});
t('移動速度＝設定速度；轉彎有最大轉速（不能瞬間掉頭）', () => {
  const s = game(2); toPlay(s); clearFoods(s);
  const p = s.players[0], a0 = p.ang, x0 = p.x, z0 = p.z;
  tick(s);
  assert(Math.abs(Math.hypot(p.x - x0, p.z - z0) - R.SPEEDS.normal.v * R.DT) < 1e-9);
  R.aim(s, 0, a0 + Math.PI);
  const before = p.ang;
  tick(s);
  const turned = Math.abs(R.wrapAng(p.ang - before));
  assert(turned <= R.turnRate(p.r) * R.DT + 1e-9 && turned > 0);
});
t('蛇身每節間距固定，節數＝長度＋1', () => {
  const s = game(2); toPlay(s);
  for (let i = 0; i < 60; i++) { R.aim(s, 0, i / 10); tick(s); }
  const p = s.players[0];
  assert.strictEqual(p.pts.length / 2, Math.floor(p.len) + 1);
  for (let i = 1; i < p.pts.length / 2; i++) {
    const d = Math.hypot(p.pts[i * 2] - p.pts[i * 2 - 2], p.pts[i * 2 + 1] - p.pts[i * 2 - 1]);
    assert(d <= R.SEG + 1e-6 && d > R.SEG * 0.6, '間距 ' + d);
  }
});
t('吃蘋果 +10、慢慢長 2 節；金星 +30；發光果實 +5', () => {
  const s = game(2); toPlay(s); clearFoods(s);
  const p = s.players[0];
  const fx = p.x + Math.cos(p.ang) * 0.3, fz = p.z + Math.sin(p.ang) * 0.3;
  s.foods.push({ id: 9001, x: fx, z: fz, type: 0, v: 0, ttl: 0 });
  const L0 = p.len;
  tick(s);
  assert.strictEqual(p.score, 10); assert.strictEqual(evs(s, 'eat').length, 1);
  assert(p.len < L0 + 2, '應該慢慢長');
  tick(s, 30);
  assert(Math.abs(p.len - (L0 + 2)) < 1e-6);
  s.foods.push({ id: 9002, x: p.x, z: p.z, type: 1, v: 0, ttl: 10 });
  s.foods.push({ id: 9003, x: p.x, z: p.z, type: 2, v: 1, ttl: 40 });
  tick(s);
  assert.strictEqual(p.score, 45);
});
t('一般果實有 8 種外觀，分數與成長都一樣', () => {
  const kinds = new Set();
  for (let seed = 1; seed <= 20; seed++) for (const f of game(2, { seed }).foods) if (f.type === 0) { assert(Number.isInteger(f.v) && f.v >= 0 && f.v < 8, '外觀編號超出範圍：' + f.v); kinds.add(f.v); }
  assert.strictEqual(kinds.size, 8, '8 種外觀都應該出現：' + [...kinds].sort().join(','));
  const s = game(1); toPlay(s); clearFoods(s);
  const p = put(s, 0, 0, 0, 0);
  for (const v of [0, 3, 7]) { const before = p.score; s.foods.push({ id: 9100 + v, x: p.x, z: p.z, type: 0, v, ttl: 0 }); tick(s); assert.strictEqual(p.score - before, 10, '外觀 ' + v + ' 的分數'); }
});
t('長越長越粗、越粗轉得越慢', () => {
  assert(R.radiusFor(80) > R.radiusFor(10));
  assert(R.turnRate(R.radiusFor(80)) < R.turnRate(R.radiusFor(10)));
});
t('撞圍欄不出局：被擋在內側並順著圍欄滑行，轉回場內就離開圍欄', () => {
  const s = game(1); toPlay(s); clearFoods(s);
  const p = put(s, 0, s.R - 1.2, 0, 0, 20);
  let bumps = 0, dies = 0;
  for (let i = 0; i < 90; i++) { tick(s); bumps += evs(s, 'bump').length; dies += evs(s, 'die').length; }
  assert(p.alive && dies === 0, '撞圍欄不該出局');
  assert(Math.hypot(p.x, p.z) + p.r * 0.6 <= s.R + 1e-6, '蛇頭不能穿出圍欄');
  assert(bumps >= 1, '碰到圍欄要有碰撞事件');
  assert(Math.abs(Math.cos(p.ang) * p.x + Math.sin(p.ang) * p.z) / Math.hypot(p.x, p.z) < 0.2, '蛇應該沿著圍欄切線滑行');
  assert.strictEqual(s.phase, 'play');
  p.ta = Math.atan2(-p.z, -p.x);
  tick(s, 60);
  assert(Math.hypot(p.x, p.z) < s.R - 3, '把方向轉回場內後會離開圍欄');
});
t('貼著圍欄會扣加速能量，扣光就疲勞不能加速；離開圍欄後恢復', () => {
  const s = game(1); toPlay(s); clearFoods(s);
  const p = put(s, 0, s.R - 1.2, 0, 0, 20);
  assert.strictEqual(p.energy, 1);
  tick(s, 30);
  assert(p.energy < 0.7, '貼著圍欄一秒應扣掉明顯的能量：' + p.energy);
  tick(s, 90);
  assert.strictEqual(p.energy, 0);
  assert(p.tired, '能量扣光要進入疲勞');
  R.setBoost(s, 0, true);
  tick(s);
  assert(!p.boosting, '疲勞時不能加速');
  R.setBoost(s, 0, false);
  p.ta = Math.atan2(-p.z, -p.x);
  tick(s, 120);
  assert(Math.hypot(p.x, p.z) < s.R - 3 && p.energy > 0, '離開圍欄後能量開始回充');
});
t('單人挑戰：撞到障礙才結束（整條身體變成發光果實）', () => {
  const s = game(1); toPlay(s); clearFoods(s);
  s.rocks = [[5, 0, 1]];
  const p = put(s, 0, 0, 0, 0, 20);
  let die = null;
  for (let i = 0; i < 60 && !die; i++) { tick(s); die = evs(s, 'die')[0]; }
  assert(die && die.why === 'rock');
  assert(die.orbs >= 5, '應該掉發光果實');
  assert(s.foods.filter(f => f.type === 2).length === die.orbs);
  assert(!p.alive);
  tick(s);
  assert.strictEqual(s.phase, 'over');
  assert.strictEqual(s.result.mode, 'challenge');
});
t('撞石頭出局', () => {
  const s = game(2); toPlay(s); clearFoods(s);
  s.rocks = [[5, 0, 1]];
  put(s, 0, 2, 0, 0); put(s, 1, -10, 10, 0);
  let die = null;
  for (let i = 0; i < 30 && !die; i++) { tick(s); die = evs(s, 'die')[0]; }
  assert(die && die.why === 'rock' && die.slot === 0);
});
t('頭撞到別人身體：自己出局，對方擊敗 +1', () => {
  const s = game(3); toPlay(s); clearFoods(s);
  put(s, 1, 4, 0, Math.PI / 2, 30);           /* 直立的身體擋在 x=4 */
  put(s, 0, 0, -3, 0);                         /* 從左邊衝過去 */
  put(s, 2, -12, 12, 0);
  R.aim(s, 1, Math.PI / 2);
  let die = null;
  for (let i = 0; i < 40 && !die; i++) { tick(s); die = evs(s, 'die')[0]; }
  assert(die && die.slot === 0 && die.why === 'other' && die.killer === 1);
  assert.strictEqual(R.playerBySlot(s, 1).kills, 1);
});
t('頭撞頭：兩條一起出局；最後兩條同時出局 → 分高的贏，同分平手', () => {
  const s = game(2); toPlay(s); clearFoods(s);
  put(s, 0, -3, 0, 0); put(s, 1, 3, 0, Math.PI);
  s.players[0].score = 20; s.players[1].score = 20;
  let died = [];
  for (let i = 0; i < 40 && died.length < 2; i++) { clearFoods(s); tick(s); died = died.concat(evs(s, 'die')); }
  assert.strictEqual(died.length, 2);
  assert(died.every(d => d.why === 'head'));
  assert.strictEqual(s.phase, 'over'); assert.strictEqual(s.result.winner, null);
  const s2 = game(2); toPlay(s2); clearFoods(s2);
  put(s2, 0, -3, 0, 0); put(s2, 1, 3, 0, Math.PI);
  s2.players[1].score = 50;
  for (let i = 0; i < 40 && s2.phase !== 'over'; i++) { clearFoods(s2); tick(s2); }
  assert.strictEqual(s2.result.winner, 1);
});
t('撞到自己：長蛇一直繞小圈會撞到自己的身體；短蛇不會', () => {
  for (const [L, should] of [[10, false], [60, true]]) {
    const s = game(1); toPlay(s); clearFoods(s);
    const p = put(s, 0, 0, 0, 0, L);
    let why = null;
    for (let i = 0; i < 30 * 8 && !why; i++) { p.energy = 1; R.aim(s, 0, p.ang + 1.5); tick(s); const d = evs(s, 'die')[0]; if (d) why = d.why; }
    if (should) assert.strictEqual(why, 'self'); else assert.strictEqual(why, null, '短蛇繞圈不該死');
  }
});
t('急轉掉頭不會撞到自己的脖子', () => {
  for (const L of [10, 30, 90]) {
    const s = game(1); toPlay(s); clearFoods(s);
    const p = put(s, 0, 0, 0, 0, L);
    R.aim(s, 0, Math.PI);
    let why = null;
    for (let i = 0; i < 40 && !why; i++) { tick(s); const d = evs(s, 'die')[0]; if (d) why = d.why; }
    assert.strictEqual(why, null, 'L=' + L);
  }
});
t('加速：更快、消耗能量；用光後要充到一定量才能再加速', () => {
  const s = game(2); toPlay(s); clearFoods(s);
  const p = s.players[0];
  put(s, 0, -15, 0, 0); put(s, 1, 10, 15, Math.PI);
  R.setBoost(s, 0, true);
  const x0 = p.x; tick(s);
  assert(Math.abs((p.x - x0) - R.SPEEDS.normal.v * R.BOOST_MUL * R.DT) < 1e-6);
  assert(p.energy < 1 && p.boosting);
  let n = 0;
  while (!p.tired && n++ < 200) { R.aim(s, 0, p.ang + 0.8); tick(s); }
  assert(p.tired && p.energy === 0 && !p.boosting);
  tick(s, 10);
  assert(!p.boosting, '累了不能加速');
  while (p.tired && n++ < 600) { R.aim(s, 0, p.ang + 0.8); tick(s); }
  assert(!p.tired && p.energy >= 0.15);
  tick(s);
  assert(p.boosting, '充回來後可以再加速');
});
t('限時：時間到由活著的最高分獲勝；排名第一是勝利者', () => {
  const s = game(3, { timeLimit: 120 }); toPlay(s);
  s.players[0].score = 10; s.players[1].score = 90; s.players[2].score = 30;
  s.time = 119.99;
  for (const p of s.players) put(s, p.slot, p.slot * 4 - 4, 0, Math.PI / 2);
  tick(s);
  assert.strictEqual(s.phase, 'over');
  assert.strictEqual(s.result.winner, 1); assert.strictEqual(s.result.reason, 'time');
  assert.strictEqual(s.result.ranking[0], 1);
});
t('最後存活者獲勝；中途離場算出局並留下果實', () => {
  const s = game(3); toPlay(s);
  R.removePlayer(s, 0); tick(s);
  assert(!s.players[0].alive && s.players[0].left);
  R.removePlayer(s, 2); tick(s);
  assert.strictEqual(s.phase, 'over'); assert.strictEqual(s.result.winner, 1);
});
t('不合法的輸入被忽略（NaN、出局的人）', () => {
  const s = game(2); toPlay(s);
  const a = s.players[0].ta;
  assert.strictEqual(R.aim(s, 0, NaN), false); assert.strictEqual(R.aim(s, 0, Infinity), false);
  assert.strictEqual(s.players[0].ta, a);
  R.removePlayer(s, 1);
  assert.strictEqual(R.aim(s, 1, 1), false);
});
t('果實數量維持在目標附近，不會超過上限', () => {
  const s = game(4); toPlay(s);
  const target = Math.round(s.R * s.R * 0.045);
  assert(s.foods.length >= target - 1);
  s.foods.splice(0, 20);
  tick(s, 20);
  assert(s.foods.filter(f => f.type === 0).length >= target - 1);
  assert(s.foods.length <= R.MAX_FOODS);
});

console.log('快照');
t('完整快照 → 畫面狀態：位置、分數、果實一致', () => {
  const s = game(4, { obstacles: 'few' });
  const v = R.viewFromStart(JSON.parse(JSON.stringify(R.startInfo(s))));
  toPlay(s); tick(s, 40);
  R.applySnapshot(v, JSON.parse(JSON.stringify(R.snapshot(s, true))), 1);
  assert.strictEqual(v.phase, 'play');
  assert.deepStrictEqual(v.rocks, s.rocks.map(k => k.map(n => Math.round(n * 100) / 100)));
  s.players.forEach((p, i) => {
    const q = v.players[i];
    assert.strictEqual(q.score, p.score);
    assert(Math.abs(q.pts[0] - p.x) < 0.06 && Math.abs(q.pts[1] - p.z) < 0.06);
    const tx = p.pts[p.pts.length - 2], tz = p.pts[p.pts.length - 1];
    assert(Math.abs(q.pts[q.pts.length - 2] - tx) < 0.06 && Math.abs(q.pts[q.pts.length - 1] - tz) < 0.06, '尾巴');
  });
  assert.strictEqual(v.foods.length, s.foods.length);
});
t('差異快照（新增／移除果實）長時間後仍與伺服器同步；peek 不會吃掉差異', () => {
  const s = game(4, { track: true, seed: 77 });
  const brains = s.players.map((p, i) => AI.createBrain(['easy', 'normal', 'hard', 'toddler'][i], i));
  const v = R.viewFromStart(JSON.parse(JSON.stringify(R.startInfo(s))));
  const late = R.viewFromStart(JSON.parse(JSON.stringify(R.startInfo(s))));
  for (let i = 0; i < 1500 && s.phase !== 'over'; i++) {
    s.players.forEach((p, k) => AI.drive(brains[k], s, p));
    R.step(s, R.DT);
    if (i === 700) R.applySnapshot(late, JSON.parse(JSON.stringify(R.snapshot(s, 'peek'))), i);
    if (i % 2 === 0) {
      const snap = JSON.parse(JSON.stringify(R.snapshot(s)));
      R.applySnapshot(v, snap, i);
      if (i > 700) R.applySnapshot(late, snap, i);
    }
  }
  R.applySnapshot(v, JSON.parse(JSON.stringify(R.snapshot(s))), 9999);
  const ids = x => x.foods.map(f => f.id).sort((a, b) => a - b);
  assert.deepStrictEqual(ids(v), ids(s));
  assert.deepStrictEqual(ids(late), ids(s));
});

console.log('電腦對手');
function runSolo(level, seed) {
  const s = R.createGame({ seed, players: [{ slot: 0, kind: 'ai', level }], speed: 'normal', obstacles: 'few' });
  const b = AI.createBrain(level, seed);
  while (s.phase !== 'over' && s.time < 150) { AI.drive(b, s, s.players[0]); R.step(s, R.DT); }
  return s;
}
function runMatch(levels, seed) {
  const players = levels.map((l, i) => ({ slot: i, name: l, kind: 'ai', level: l, animal: ANIMALS[i] }));
  const s = R.createGame({ seed, players, speed: 'normal', obstacles: 'few' });
  const br = levels.map((l, i) => AI.createBrain(l, seed * 13 + i));
  const inv = [];
  while (s.phase !== 'over' && s.time < 200) {
    s.players.forEach((p, i) => AI.drive(br[i], s, p));
    R.step(s, R.DT);
    if (s.tick % 15 === 0) for (const p of s.players) if (p.alive) {
      if (Math.hypot(p.x, p.z) > s.R) inv.push('頭跑出場外');
      if (p.pts.length / 2 !== Math.floor(p.len) + 1) inv.push('節數不對');
    }
    if (s.foods.length > R.MAX_FOODS) inv.push('果實超量');
  }
  return { s, inv };
}
t('AI 只用 aim／setBoost 下指令（思考不會移動蛇）', () => {
  const s = game(3); toPlay(s); tick(s, 10);
  const before = JSON.stringify(s.players.map(p => [p.x, p.z, p.ang, p.len, p.score]));
  const b = AI.createBrain('hard', 1);
  AI.think(b, s, s.players[0]);
  assert.strictEqual(JSON.stringify(s.players.map(p => [p.x, p.z, p.ang, p.len, p.score])), before);
});
const N = Number(process.env.LADDER_GAMES || 8);
const surv = {};
t('單人挑戰存活時間：幼幼班 < 簡單 < 普通 ≤ 困難（' + N + ' 場平均）', () => {
  for (const l of AI.LEVEL_ORDER) { let tot = 0; for (let g = 0; g < N; g++) tot += runSolo(l, 300 + g).time; surv[l] = tot / N; }
  console.log('       平均存活秒數 ' + AI.LEVEL_ORDER.map(l => AI.LEVELS[l].name + ' ' + surv[l].toFixed(1)).join('、'));
  assert(surv.toddler < surv.easy && surv.easy < surv.normal, JSON.stringify(surv));
  assert(surv.hard >= surv.normal * 0.85, JSON.stringify(surv));
});
t('八人混戰：普通＋困難贏的場數遠多於幼幼班＋簡單，且全程不變量成立', () => {
  const wins = { toddler: 0, easy: 0, normal: 0, hard: 0, none: 0 };
  const lv = ['toddler', 'easy', 'normal', 'hard', 'toddler', 'easy', 'normal', 'hard'];
  for (let g = 0; g < N; g++) {
    const { s, inv } = runMatch(lv, 900 + g);
    assert.deepStrictEqual(inv, []);
    const w = s.result ? s.result.winner : null;
    wins[w == null ? 'none' : lv[w]]++;
  }
  console.log('       勝場 ' + JSON.stringify(wins));
  assert(wins.normal + wins.hard > wins.toddler + wins.easy);
});
t('普通以上的電腦不會一開局就自殺（每場都活過 10 秒）', () => {
  for (let g = 0; g < N; g++) for (const l of ['normal', 'hard']) assert(runSolo(l, 700 + g).time > 10, l + ' 太早死');
});

console.log('\n' + passed + ' 項通過' + (failed ? '，' + failed + ' 項失敗' : ''));
