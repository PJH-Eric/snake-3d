/* 單機開局流程：建立對局設定（含種子），交給對戰畫面建立規則狀態與電腦大腦。 */
import { app, go, myName } from './app.js';
import { getSave } from './store.js';
import { ANIMAL_IDS } from './i18n.js';

let runId = 0;
export function newSeed() {
  const a = new Uint32Array(1);
  crypto.getRandomValues(a);
  return a[0] >>> 0;
}

export function pickAnimals(n, exclude, rnd = Math.random) {
  const pool = ANIMAL_IDS.filter(a => a !== exclude);
  for (let i = pool.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [pool[i], pool[j]] = [pool[j], pool[i]]; }
  return pool.slice(0, n);
}

export function soloSetup(d, seed = newSeed(), animals = pickAnimals(d.solo.levels.length, d.animal)) {
  const so = d.solo;
  const players = [{ slot: 0, name: myName(), animal: d.animal, kind: 'human' }];
  so.levels.forEach((lv, i) => players.push({ slot: i + 1, name: animals[i], animal: animals[i], kind: 'ai', level: lv }));
  return { kind: 'solo', runId: ++runId, seed, speed: so.speed, obstacles: so.obstacles, timeLimit: so.timeLimit, themeId: so.theme, players };
}
export function challengeSetup(d, seed = newSeed()) {
  const ch = d.challenge;
  return {
    kind: 'challenge', runId: ++runId, seed, speed: ch.speed, obstacles: ch.obstacles, timeLimit: 0, themeId: ch.theme,
    players: [{ slot: 0, name: myName(), animal: d.animal, kind: 'human' }]
  };
}

export function startSolo() { app.set({ lastRun: 'solo' }); go('game', soloSetup(getSave())); }
export function startChallenge() { app.set({ lastRun: 'challenge' }); go('game', challengeSetup(getSave())); }
export function restart() { if (app.get().lastRun === 'challenge') startChallenge(); else startSolo(); }
export function backToSetup() { go(app.get().lastRun === 'challenge' ? 'challenge' : 'solo'); }
