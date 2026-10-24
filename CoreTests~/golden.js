/* 產生金標準：用專案內權威伺服器的 rules.js / ai.js 跑固定種子的對局，逐節拍記錄，供 C# 版比對。
 * 用法：node golden.js <參照伺服器路徑，可省略> > golden.json */
const path = require('path');
const web = path.resolve(process.argv[2] || path.join(__dirname, '../Backend~'));
const R = require(path.join(web, 'public/js/rules.js'));
const AI = require(path.join(web, 'public/js/ai.js'));

const SCEN = [
  { name: 'versus4', seed: 12345, speed: 'normal', obstacles: 'few', timeLimit: 0, themeId: -1, levels: ['toddler', 'easy', 'normal', 'hard'], max: 2400 },
  { name: 'challenge', seed: 7, speed: 'fast', obstacles: 'many', timeLimit: 0, themeId: 2, levels: ['normal'], max: 2400 },
  { name: 'versus8', seed: 99, speed: 'slow', obstacles: 'none', timeLimit: 120, themeId: -1, levels: ['hard', 'hard', 'normal', 'normal', 'easy', 'easy', 'toddler', 'toddler'], max: 2400 },
  { name: 'versus3-boost', seed: 4242, speed: 'normal', obstacles: 'many', timeLimit: 0, themeId: 5, levels: ['hard', 'normal', 'hard'], max: 2400 }
];

const out = [];
for (const sc of SCEN) {
  const players = sc.levels.map((lv, i) => ({ slot: i, name: 'P' + i, animal: 'cat', kind: 'ai', level: lv }));
  const s = R.createGame({ seed: sc.seed, players, speed: sc.speed, obstacles: sc.obstacles, timeLimit: sc.timeLimit, themeId: sc.themeId });
  const brains = {};
  players.forEach(p => { brains[p.slot] = AI.createBrain(p.level, sc.seed + p.slot * 977); });
  const frames = [];
  for (let t = 0; t < sc.max; t++) {
    for (const p of s.players) { const br = brains[p.slot]; if (br) AI.drive(br, s, p); }
    R.step(s, R.DT);
    frames.push({
      ph: s.phase, c: +s.countdown.toFixed(6), t: s.tick,
      p: s.players.map(p => [p.alive ? 1 : 0, p.x, p.z, p.ang, p.len, p.score, p.kills, p.energy]),
      f: s.foods.length,
      ev: s.events.map(e => e.t + (e.slot != null ? ':' + e.slot : '') + (e.why ? ':' + e.why : '')).join(',')
    });
    if (s.phase === 'over' && s.endHold < 0) break;
  }
  out.push({
    name: sc.name, seed: sc.seed, speed: sc.speed, obstacles: sc.obstacles, timeLimit: sc.timeLimit, themeId: sc.themeId, levels: sc.levels,
    R: s.R, themeUsed: s.themeId, rocks: s.rocks, frames,
    result: s.result ? { winner: s.result.winner, reason: s.result.reason, ranking: s.result.ranking, scores: s.result.scores, level: s.result.level } : null
  });
}
process.stdout.write(JSON.stringify(out));
