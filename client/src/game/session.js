/* 對戰場次（單機與線上共用）：跑規則／套用快照、處理輸入、事件音效、產生給 React 的 HUD 快照。
 * 不依賴 DOM 與 WebGL：world 可以是 null（測試用）。 */
import Rules from '@shared/rules.js';
import AI from '@shared/ai.js';
import { net } from '../net.js';
import { getBest, getSave, record, setBest } from '../store.js';
import * as sound from '../sound.js';

/* 鏡頭仰角 52°：畫面上下被壓扁，把螢幕方向換算成場地方向時要補回來 */
export const TILT = Math.sin((52 * Math.PI) / 180);

export function normalizeResult(result, players) {
  const map = list => { const m = {}; for (const [k, v] of list || []) m[k] = v; return m; };
  return {
    mode: result.mode, reason: result.reason, winner: result.winner == null ? -1 : result.winner, time: result.time || 0,
    level: Math.max(1, result.level || 1), ranking: result.ranking || [],
    scores: map(result.scores), kills: map(result.kills), lens: map(result.lens), players: players || []
  };
}

function blankPlayer(pl) {
  return {
    slot: pl.slot, name: pl.name, animal: pl.animal, kind: pl.kind, level: pl.level, alive: true, left: false, pts: [], prevPts: null,
    x: 0, z: 0, ang: 0, prevAng: 0, ta: 0, r: Rules.BASE_R, len: Rules.START_LEN, score: 0, kills: 0, eaten: 0, boosting: false, energy: 1, tired: false, finalLen: Rules.START_LEN
  };
}

export class GameSession {
  /** opts: { kind, setup?, startInfo?, slot?, world?, now? } */
  constructor(opts) {
    this.kind = opts.kind;
    this.world = opts.world || null;
    this.now = opts.now || (() => performance.now() / 1000);
    this.listeners = new Set();
    this.onResult = null;
    this.acc = 0; this.ff = false; this.overAt = 0; this.resultDone = false; this.watch = -1;
    this.boostKeys = false; this.boostTouch = false; this.boostOn = false;
    this.keys = new Set(); this.aimSent = NaN; this.aimAt = 0; this.pendingAim = NaN;
    this.safeTop = 0; this.safeBottom = 0; this.zoom = 'normal';
    this.flash = null; this.flashId = 0; this.hudAt = -1; this.hud = null; this.lastCount = -1;
    this.paused = () => false;

    if (this.kind === 'online') {
      this.slot = opts.slot;
      this.view = Rules.viewFromStart(opts.startInfo);
    } else {
      const s = opts.setup;
      this.slot = 0;
      this.state = Rules.createGame({ seed: s.seed, players: s.players, speed: s.speed, obstacles: s.obstacles, timeLimit: s.timeLimit, themeId: s.themeId });
      this.brains = new Map();
      for (const p of s.players) if (p.kind === 'ai') this.brains.set(p.slot, AI.createBrain(p.level, (s.seed + p.slot * 977) >>> 0));
      this.view = Rules.viewFromStart(Rules.startInfo(this.state));
      this.syncView();
    }
  }

  subscribe(fn) { this.listeners.add(fn); return () => this.listeners.delete(fn); }
  me() { return this.slot >= 0 ? this.view.players.find(p => p.slot === this.slot) : null; }

  /* ---------- 單機：把規則狀態同步到畫面檢視 ---------- */
  syncView() {
    const s = this.state, v = this.view;
    v.phase = s.phase; v.time = s.time; v.countdown = Math.max(0, s.countdown);
    v.foods = s.foods;
    s.players.forEach((p, i) => {
      const o = v.players[i];
      o.alive = p.alive; o.left = p.left; o.pts = p.pts; o.x = p.x; o.z = p.z; o.ang = p.ang; o.ta = p.ta; o.r = p.r; o.len = p.len;
      o.score = p.score; o.kills = p.kills; o.eaten = p.eaten; o.boosting = p.boosting; o.energy = p.energy; o.tired = p.tired;
      o.finalLen = p.alive ? Math.round(p.len) : p.finalLen;
      if (p.alive && p.pts.length) { o.x = p.pts[0]; o.z = p.pts[1]; }
    });
  }

  /* ---------- 每幀 ---------- */
  tick(dt) {
    dt = Math.min(0.1, dt);
    this.readKeyboard();
    let alpha = 1;
    if (this.kind === 'online') {
      const v = this.view;
      alpha = v.snapAt > 0 ? Math.max(0, Math.min(1, (this.now() - v.snapAt) / Math.max(0.05, v.gap))) : 1;
      this.flushAim(false);
    } else alpha = this.stepSolo(dt);
    this.pickWatch();
    this.updateHud(false);
    return alpha;
  }

  frame(dt, alpha) {
    if (this.world) this.world.draw(this.view, this.slot, alpha, dt, this.zoom, this.watch, this.safeTop, this.safeBottom);
  }

  stepSolo(dt) {
    if (this.paused()) return Math.min(1, this.acc / Rules.DT);
    const s = this.state, me = this.slot >= 0 ? Rules.playerBySlot(s, this.slot) : null;
    const dead = me && !me.alive && s.phase === 'play';
    this.acc += dt * (this.ff && dead ? 3 : 1);
    const events = [];
    let guard = 0;
    while (this.acc >= Rules.DT && guard++ < 12) {
      s.players.forEach((p, i) => { const o = this.view.players[i]; o.prevPts = p.pts; o.prevAng = p.ang; });
      for (const p of s.players) { const br = this.brains.get(p.slot); if (br) AI.drive(br, s, p); }
      Rules.step(s, Rules.DT);
      for (const e of s.events) events.push(e);
      this.acc -= Rules.DT;
      if (s.phase === 'over' && s.endHold <= 0) break;
    }
    this.syncView();
    if (events.length) this.handleEvents(events);
    if (s.phase === 'over' && !this.resultDone) {
      if (!this.overAt) this.overAt = this.now();
      if (s.endHold <= 0 || this.now() - this.overAt > 2.2) {
        this.resultDone = true;
        this.finish(normalizeResult(s.result, s.players.map(p => ({ slot: p.slot, name: p.name, animal: p.animal, kind: p.kind, level: p.level }))));
      }
    }
    return Math.min(1, this.acc / Rules.DT);
  }

  /* ---------- 線上：伺服器快照與結果 ---------- */
  onSnap(snap) {
    const me = this.me(), myTa = me ? me.ta : 0;
    Rules.applySnapshot(this.view, snap, this.now());
    if (me && !Number.isNaN(this.aimSent)) me.ta = myTa;
    if (snap.e && snap.e.length) this.handleEvents(snap.e);
  }
  onServerResult(result, players) {
    if (this.resultDone) return;
    this.resultDone = true;
    this.finish(normalizeResult(result, players));
  }

  finish(res) {
    this.result = res;
    const me = this.slot;
    const win = res.winner === me && me >= 0;
    if (this.kind === 'challenge') {
      const st = this.state.players[0];
      const set = this.state;
      res.newRecord = setBest(set.speed, set.obstacles, st.score);
      res.best = getBest(set.speed, set.obstacles);
      record('statChallenge', false);
    } else if (me >= 0) record(this.kind === 'online' ? 'statOnline' : 'statSolo', win);
    this.hudAt = -1;
    this.updateHud(true);
    if (this.onResult) this.onResult(res);
  }

  handleEvents(events) {
    if (this.world) this.world.consume(events, this.slot, getSave().reduceMotion);
    for (const e of events) {
      switch (e.t) {
        case 'eat': if (e.slot === this.slot) sound.sfx(e.type === 1 ? 'gold' : 'eat'); break;
        case 'die':
          if (e.slot === this.slot) { sound.sfx('die'); sound.vibrate(240); } else sound.sfx('item');
          break;
        case 'count': sound.sfx('count'); this.flashText(String(e.n)); break;
        case 'go': sound.sfx('go'); sound.music('battle'); this.flashText('go'); break;
        case 'over':
          if (this.view.mode === 'challenge') sound.sfx('lose');
          else sound.sfx(e.winner == null || e.winner < 0 ? 'draw' : e.winner === this.slot || this.slot < 0 ? 'win' : 'lose');
          break;
        default:
      }
    }
  }
  flashText(text) { this.flash = { id: ++this.flashId, text }; this.hudAt = -1; }

  /* ---------- 觀戰／出局後的視角 ---------- */
  pickWatch() {
    const me = this.me();
    if (me && me.alive) { this.watch = -1; return; }
    const cur = this.watch >= 0 ? this.view.players.find(p => p.slot === this.watch) : null;
    if (cur && cur.alive) return;
    let best = null;
    for (const p of this.view.players) if (p.alive && (!best || p.score > best.score)) best = p;
    this.watch = best ? best.slot : -1;
  }
  nextWatch() {
    const alive = this.view.players.filter(p => p.alive);
    if (!alive.length) return;
    const i = alive.findIndex(p => p.slot === this.watch);
    this.watch = alive[(i + 1) % alive.length].slot;
    this.hudAt = -1; this.updateHud(true);
  }
  setFast(on) { this.ff = on; this.hudAt = -1; this.updateHud(true); }

  /* ---------- 輸入 ---------- */
  aim(angle) {
    const me = this.me();
    if (this.slot < 0 || this.view.phase === 'over' || !me || !me.alive) return;
    if (this.kind !== 'online') { Rules.aim(this.state, this.slot, angle); return; }
    me.ta = angle; this.pendingAim = angle;
    this.flushAim(false);
  }
  /** 線上：方向有明顯變化才送，最多每 50ms 一次。 */
  flushAim(force) {
    if (Number.isNaN(this.pendingAim)) return;
    const a = this.pendingAim, t = this.now();
    if (!Number.isNaN(this.aimSent) && Math.abs(Rules.wrapAng(a - this.aimSent)) < 0.03) { this.pendingAim = NaN; return; }
    if (!force && t - this.aimAt < 0.05) return;
    net.send({ type: 'input', a: Math.round(a * 1000) / 1000 });
    this.aimSent = a; this.aimAt = t; this.pendingAim = NaN;
  }
  setBoostTouch(on) { this.boostTouch = on; this.syncBoost(); }
  syncBoost() {
    const on = this.boostKeys || this.boostTouch;
    if (on === this.boostOn) return;
    this.boostOn = on;
    if (this.slot < 0) return;
    if (this.kind === 'online') net.send({ type: 'boost', on }); else Rules.setBoost(this.state, this.slot, on);
  }
  keyDown(code) { this.keys.add(code); }
  keyUp(code) { this.keys.delete(code); }
  resetInput() {
    this.keys.clear(); this.boostKeys = this.boostTouch = false; this.syncBoost(); this.pendingAim = NaN;
  }
  readKeyboard() {
    if (this.slot < 0) return;
    const k = this.keys;
    let dx = 0, dy = 0;
    if (k.has('KeyA') || k.has('ArrowLeft')) dx -= 1;
    if (k.has('KeyD') || k.has('ArrowRight')) dx += 1;
    if (k.has('KeyW') || k.has('ArrowUp')) dy -= 1;    /* 螢幕上方 = 場地 -z */
    if (k.has('KeyS') || k.has('ArrowDown')) dy += 1;
    if (dx || dy) this.aim(Math.atan2(dy / TILT, dx));
    const b = k.has('Space') || k.has('ShiftLeft') || k.has('ShiftRight');
    if (b !== this.boostKeys) { this.boostKeys = b; this.syncBoost(); }
  }

  /* ---------- HUD 快照（約 10Hz，內容沒變就不通知 React） ---------- */
  updateHud(force) {
    const t = this.now();
    if (!force && t - this.hudAt < 0.1) return;
    this.hudAt = t;
    const v = this.view, me = this.me();
    const hud = {
      phase: v.phase, mode: v.mode, time: v.time, timeLimit: v.timeLimit, slot: this.slot, watch: this.watch, ff: this.ff,
      count: v.phase === 'countdown' ? (this.kind === 'online' ? Math.floor(v.countdown) : Math.ceil(v.countdown - 1e-9)) : 0,
      flash: this.flash, over: !!this.result,
      me: me ? { alive: me.alive, energy: me.energy, tired: me.tired, score: me.score, len: Math.round(me.len), eaten: me.eaten } : null,
      players: v.players.map(p => ({
        slot: p.slot, name: p.name, animal: p.animal, kind: p.kind, level: p.level, alive: p.alive, left: p.left, score: p.score, kills: p.kills,
        len: p.alive ? Math.round(p.len) : p.finalLen
      }))
    };
    const sig = JSON.stringify(hud, (k, val) => (typeof val === 'number' && k === 'energy' ? Math.round(val * 50) : val));
    if (sig === this.hudSig) return;
    this.hudSig = sig; this.hud = hud;
    this.listeners.forEach(fn => fn());
  }
  getHud() { if (!this.hud) this.updateHud(true); return this.hud; }

  dispose() {
    this.resetInput();
    this.listeners.clear();
    this.onResult = null;
    if (this.world) { this.world.dispose(); this.world = null; }
  }
}
