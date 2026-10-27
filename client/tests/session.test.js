import { beforeEach, describe, expect, it } from 'vitest';
import Rules from '../../shared/rules.js';
import { GameSession, TILT } from '../src/game/session.js';
import { getBest, getSave } from '../src/store.js';
import { challengeSetup, newSeed, pickAnimals, soloSetup } from '../src/flow.js';

/** 以固定時鐘推進場次，直到出現結果（最多 maxSec 遊戲秒）。 */
function run(session, maxSec = 600, each) {
  let t = 0;
  session.now = () => t;
  for (let i = 0; i < maxSec * 30 && !session.result; i++) { t += 1 / 30; session.tick(1 / 30); if (each) each(i); }
  return session.result;
}

describe('單機場次（規則核心 + 電腦對手 + 結算）', () => {
  beforeEach(() => localStorage.clear());

  it('對戰電腦：完整一局到結算，記錄戰績；再開一局狀態乾淨', () => {
    const setup = soloSetup(getSave(), 12345, ['fox', 'bear']);
    expect(setup.players.map(p => p.kind)).toEqual(['human', 'ai', 'ai', 'ai']);
    const s1 = new GameSession({ kind: 'solo', setup });
    const res = run(s1);
    expect(res).toBeTruthy();
    expect(res.mode).toBe('versus');
    expect(res.ranking.length).toBe(4);
    expect(new Set(res.ranking).size).toBe(4);
    expect(res.players.length).toBe(4);
    expect(getSave().statSolo.play).toBe(1);
    expect(s1.getHud().over).toBe(true);

    const s2 = new GameSession({ kind: 'solo', setup: soloSetup(getSave(), 999, ['fox', 'bear']) });
    expect(s2.result).toBeUndefined();
    expect(s2.view.phase).toBe('countdown');
    expect(s2.view.players.every(p => p.alive && p.score === 0)).toBe(true);
    expect(run(s2)).toBeTruthy();
    expect(getSave().statSolo.play).toBe(2);
    s1.dispose(); s2.dispose();
  });

  it('單人挑戰：撞到障礙結束並更新最高分；結果只觸發一次', () => {
    const setup = challengeSetup(getSave(), 777);
    const s = new GameSession({ kind: 'challenge', setup });
    const me = s.state.players[0];
    s.state.rocks.push([me.x + Math.cos(me.ang) * 5, me.z + Math.sin(me.ang) * 5, 1.2]);
    let fired = 0;
    s.onResult = () => { fired++; };
    s.state.players[0].score = 50;
    const res = run(s);
    expect(res.mode).toBe('challenge');
    expect(res.newRecord).toBe(true);
    expect(getBest(setup.speed, setup.obstacles)).toBe(50);
    for (let i = 0; i < 90; i++) s.tick(1 / 30);
    expect(fired).toBe(1);
    expect(getSave().statChallenge.play).toBe(1);
  });

  it('出局或觀戰時可點資訊欄切換視角，只能選還活著的人', () => {
    const s = new GameSession({ kind: 'solo', setup: soloSetup(getSave(), 31, ['fox', 'bear']) });
    let t = 0; s.now = () => t;
    for (let i = 0; i < 100; i++) { t += 1 / 30; s.tick(1 / 30); }
    expect(s.state.phase).toBe('play');
    const [me, a, b] = s.state.players;
    expect(s.canWatch()).toBe(false);
    s.setWatch(a.slot);
    expect(s.watch).toBe(-1);
    me.alive = false; a.score = 1; b.score = 9;
    t += 1 / 30; s.tick(1 / 30);
    expect(s.canWatch()).toBe(true);
    expect(s.watch).toBe(b.slot);
    s.setWatch(a.slot);
    expect(s.watch).toBe(a.slot);
    s.setWatch(me.slot);
    expect(s.watch).toBe(a.slot);
    s.dispose();
  });

  it('暫停（開啟視窗）時單機規則不前進，恢復後繼續', () => {
    const s = new GameSession({ kind: 'solo', setup: soloSetup(getSave(), 5, ['fox']) });
    let paused = true;
    s.paused = () => paused;
    let t = 0; s.now = () => t;
    for (let i = 0; i < 60; i++) { t += 1 / 30; s.tick(1 / 30); }
    expect(s.state.countdown).toBe(Rules.COUNTDOWN);
    paused = false;
    for (let i = 0; i < 60; i++) { t += 1 / 30; s.tick(1 / 30); }
    expect(s.state.countdown).toBeLessThan(Rules.COUNTDOWN - 1.5);
  });

  it('鍵盤方向換算：往上＝場地 -z（含鏡頭仰角補償），Space 加速並可放開', () => {
    const s = new GameSession({ kind: 'solo', setup: soloSetup(getSave(), 5, ['fox']) });
    let t = 0; s.now = () => t;
    s.keyDown('ArrowUp'); s.keyDown('Space');
    s.tick(1 / 30);
    expect(s.state.players[0].ta).toBeCloseTo(Math.atan2(-1 / TILT, 0), 5);
    expect(s.state.players[0].boost).toBe(true);
    s.keyUp('Space'); s.tick(1 / 30);
    expect(s.state.players[0].boost).toBe(false);
    s.resetInput();
    s.dispose();
  });

  it('出局後自動改看分數最高的對手，可換人看', () => {
    const s = new GameSession({ kind: 'solo', setup: soloSetup(getSave(), 3, ['fox', 'bear']) });
    let t = 0; s.now = () => t;
    for (let i = 0; i < 1500; i++) { t += 1 / 30; s.tick(1 / 30); if (!s.me().alive || s.result) break; }
    expect(s.me().alive).toBe(false);
    const alive = s.view.players.filter(p => p.alive);
    expect(alive.length).toBeGreaterThan(0);
    expect(s.watch).toBe(alive.reduce((b, p) => (p.score > b.score ? p : b), alive[0]).slot);
    const first = s.watch;
    s.nextWatch();
    if (alive.length > 1) expect(s.watch).not.toBe(first);
    expect(alive.map(p => p.slot)).toContain(s.watch);
  });

  it('隨機種子與出場動物：不重複、不含自己', () => {
    expect(new Set(pickAnimals(7, 'cat')).size).toBe(7);
    expect(pickAnimals(7, 'cat')).not.toContain('cat');
    expect(typeof newSeed()).toBe('number');
  });
});

describe('線上場次（套用伺服器快照）', () => {
  it('依 startInfo 建立檢視，快照更新玩家、果實與事件；結果只處理一次', () => {
    const players = [{ slot: 0, name: 'A', animal: 'cat', kind: 'human' }, { slot: 1, name: 'B', animal: 'dog', kind: 'ai', level: 'easy' }];
    const server = Rules.createGame({ seed: 42, players, speed: 'normal', obstacles: 'few', timeLimit: 0, themeId: 1, track: true });
    const sess = new GameSession({ kind: 'online', startInfo: Rules.startInfo(server), slot: 0 });
    let t = 0; sess.now = () => t;
    expect(sess.view.players.length).toBe(2);
    for (let i = 0; i < 120; i++) { Rules.step(server, Rules.DT); if (i % 2 === 1) { t += 2 / 30; const snap = Rules.snapshot(server, i === 1); snap.e = server.events; sess.onSnap(snap); } }
    expect(sess.view.phase).toBe('play');
    expect(sess.view.players[0].pts.length).toBeGreaterThan(4);
    expect(sess.view.foods.length).toBeGreaterThan(10);
    let fired = 0;
    sess.onResult = () => { fired++; };
    const result = { mode: 'versus', winner: 1, reason: 'last', time: 12, ranking: [1, 0], scores: [[0, 10], [1, 30]], kills: [[0, 0], [1, 0]], lens: [[0, 10], [1, 14]], level: 0 };
    sess.onServerResult(result, players);
    sess.onServerResult(result, players);
    expect(fired).toBe(1);
    expect(sess.result.winner).toBe(1);
    expect(sess.getHud().over).toBe(true);
    expect(getSave().statOnline.play).toBe(1);
  });
});
