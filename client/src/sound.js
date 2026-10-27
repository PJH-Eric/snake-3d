/* 即時合成的音效與背景音樂（沒有外部音檔，授權單純）。
 * 要換成正式音檔時：保留 Sfx(name) / Music(mode) 這兩個介面，內部改成播放 AudioBuffer 即可。 */
import { getSave, subscribeSave } from './store.js';

const RATE = 22050;
let ctx = null, bgmGain = null, sfxGain = null, bgmSrc = null, mode = null, unlocked = false;
const sfxBuf = new Map(), bgmBuf = new Map(), lastAt = new Map();
let seed = 12345;
const rnd = () => { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296; };

class Buf {
  constructor(sec) { this.d = new Float32Array(Math.floor(sec * RATE)); }
  tone(freq, t0, dur, type, vol, slideTo = 0) {
    const s0 = Math.floor(t0 * RATE), n = Math.floor((dur + 0.02) * RATE);
    let phase = 0;
    for (let i = 0; i < n && s0 + i < this.d.length; i++) {
      const t = i / RATE;
      let f = freq;
      if (slideTo > 0 && dur > 0) f = freq * Math.pow(Math.max(20, slideTo) / freq, Math.min(1, t / dur));
      phase += f / RATE;
      const p = phase - Math.floor(phase);
      const w = type === 'triangle' ? 4 * Math.abs(p - 0.5) - 1 : Math.sin(p * Math.PI * 2);
      const env = t < 0.012 ? t / 0.012 : Math.exp(-6.9 * (t - 0.012) / Math.max(0.02, dur));
      this.d[s0 + i] += w * vol * env;
    }
  }
  noise(t0, dur, vol, highpass) {
    const s0 = Math.floor(t0 * RATE), n = Math.floor(dur * RATE);
    let lp = 0;
    for (let i = 0; i < n && s0 + i < this.d.length; i++) {
      const x = rnd() * 2 - 1;
      lp += (x - lp) * 0.25;
      this.d[s0 + i] += (highpass ? x - lp : lp) * vol * Math.exp(-6.9 * i / n);
    }
  }
  trim() {
    let last = this.d.length - 1;
    while (last > 100 && Math.abs(this.d[last]) < 0.0005) last--;
    this.d = this.d.slice(0, Math.min(this.d.length, last + 200));
    return this;
  }
  normalize(always) {
    let peak = 0.0001;
    for (const v of this.d) peak = Math.max(peak, Math.abs(v));
    if (always || peak > 0.95) { let k = 0.9 / peak; if (!always && k > 1) k = 1; for (let i = 0; i < this.d.length; i++) this.d[i] *= k; }
    return this;
  }
  toBuffer() {
    const b = ctx.createBuffer(1, this.d.length, RATE);
    b.copyToChannel(this.d, 0);
    return b;
  }
}

function makeSfx(name) {
  const b = new Buf(1.2);
  const seq = (freqs, step, dur, type, vol) => freqs.forEach((f, i) => b.tone(f, i * step, dur, type, vol));
  switch (name) {
    case 'click': b.tone(760, 0, 0.07, 'sine', 0.22, 520); break;
    case 'eat': b.tone(520, 0, 0.08, 'triangle', 0.4, 880); b.tone(880, 0.06, 0.1, 'sine', 0.3); b.noise(0, 0.03, 0.08, true); break;
    case 'gold': seq([784, 988, 1319, 1760], 0.05, 0.16, 'triangle', 0.34); break;
    case 'boost': b.noise(0, 0.2, 0.18, true); break;
    case 'item': seq([660, 880, 1320], 0.06, 0.14, 'triangle', 0.34); break;
    case 'die': b.tone(520, 0, 0.45, 'triangle', 0.32, 90); b.noise(0, 0.22, 0.28, false); break;
    case 'count': b.tone(880, 0, 0.12, 'sine', 0.4); break;
    case 'go': b.tone(1320, 0, 0.25, 'triangle', 0.45); b.tone(1760, 0.08, 0.35, 'triangle', 0.35); break;
    case 'win': seq([523, 659, 784, 1047, 784, 1047, 1319], 0.11, 0.24, 'triangle', 0.4); break;
    case 'lose': seq([440, 392, 349, 294], 0.16, 0.3, 'sine', 0.35); break;
    case 'draw': seq([523, 523, 392], 0.14, 0.26, 'triangle', 0.35); break;
    case 'join': b.tone(660, 0, 0.1, 'sine', 0.25); b.tone(880, 0.08, 0.14, 'sine', 0.25); break;
    case 'bump': b.tone(190, 0, 0.1, 'sine', 0.3, 110); b.noise(0, 0.06, 0.12, false); break;
    case 'chat': b.tone(980, 0, 0.06, 'sine', 0.15); break;
    default: return null;
  }
  return b.trim().normalize(false).toBuffer();
}

/* ---------- 背景音樂：音樂盒風五聲音階，戰鬥版速度加快 ---------- */
const PENT = [0, 2, 4, 7, 9];
const degree = d => PENT[((d % 5) + 5) % 5] + 12 * Math.floor(d / 5);
const hz = (semi, base) => base * Math.pow(2, semi / 12);
const N = -99;
const MEL_MENU = [4, N, 5, N, 7, N, 5, N, 6, N, 4, N, 2, N, N, N, 4, N, 5, N, 7, N, 9, N, 8, N, 7, N, 5, N, N, N,
  7, N, 8, N, 9, N, 8, N, 7, N, 5, N, 4, N, N, N, 2, N, 4, N, 5, N, 4, N, 2, N, 0, N, 0, N, N, N];
const MEL_BATTLE = [7, N, 7, 9, 8, N, 7, N, 5, N, 5, 7, 6, N, 4, N, 7, N, 7, 9, 10, N, 9, N, 8, N, 7, 5, 6, N, 7, N,
  9, N, 9, 8, 7, N, 5, N, 4, N, 5, 7, 5, N, N, N, 2, 4, 5, 7, 5, 4, 2, 4, 0, N, 2, N, 0, N, N, N];
const BASS_MENU = [0, 0, 0, 0, 3, 3, 3, 3, 4, 4, 4, 4, 2, 2, 2, 2];
const BASS_BATTLE = [0, 0, 0, 0, 0, 0, 0, 0, 3, 3, 3, 3, 4, 4, 2, 2];

function makeBgm(m) {
  const battle = m === 'battle';
  const mel = battle ? MEL_BATTLE : MEL_MENU, bass = battle ? BASS_BATTLE : BASS_MENU;
  const sixteenth = 60 / (battle ? 140 : 108) / 4;
  const total = mel.length * sixteenth;
  const b = new Buf(total + 0.8);
  for (let i = 0; i < mel.length; i++) {
    const t = i * sixteenth;
    if (mel[i] !== N) {
      const f = hz(degree(mel[i]), 523.25 / 2);
      b.tone(f, t, 0.5, 'sine', 0.5); b.tone(f * 2, t, 0.22, 'sine', 0.12);
    }
    if (i % 4 === 0) b.tone(hz(degree(bass[(i / 4) % bass.length]), 130.8), t, sixteenth * 3.6, 'triangle', 0.42);
    if (battle ? i % 2 === 1 : i % 8 === 4) b.noise(t, 0.04, 0.1, true);
  }
  const loopLen = Math.floor(total * RATE);
  for (let i = loopLen; i < b.d.length; i++) b.d[i - loopLen] += b.d[i];
  b.d = b.d.slice(0, loopLen);
  return b.normalize(true).toBuffer();
}

function applyVolume() {
  if (!ctx) return;
  const d = getSave();
  bgmGain.gain.value = d.bgm ? d.bgmVol * 0.45 : 0;
  sfxGain.gain.value = d.sfx ? d.sfxVol : 0;
}

function ensure() {
  if (ctx || typeof window === 'undefined') return;
  const AC = window.AudioContext || window.webkitAudioContext;
  if (!AC) return;
  ctx = new AC();
  bgmGain = ctx.createGain(); bgmGain.connect(ctx.destination);
  sfxGain = ctx.createGain(); sfxGain.connect(ctx.destination);
  applyVolume();
}

function startBgm() {
  if (!ctx || !unlocked || !mode) return;
  if (bgmSrc) { try { bgmSrc.stop(); } catch { /* 已停止 */ } bgmSrc = null; }
  let buf = bgmBuf.get(mode);
  if (!buf) { buf = makeBgm(mode); bgmBuf.set(mode, buf); }
  bgmSrc = ctx.createBufferSource();
  bgmSrc.buffer = buf; bgmSrc.loop = true;
  bgmSrc.connect(bgmGain);
  bgmSrc.start();
}

/** 瀏覽器要求使用者手勢後才能出聲：第一次點擊／按鍵／觸控時解鎖。 */
export function installUnlock() {
  if (typeof window === 'undefined') return;
  const go = () => {
    ensure();
    if (!ctx) return;
    ctx.resume().then(() => {
      if (!unlocked) { unlocked = true; startBgm(); }
    }).catch(() => { /* 解鎖失敗就維持靜音 */ });
  };
  for (const ev of ['pointerdown', 'keydown', 'touchend']) window.addEventListener(ev, go, { passive: true });
  subscribeSave(applyVolume);
}

export function sfx(name) {
  const d = getSave();
  if (!d.sfx || !ctx || !unlocked) return;
  const now = performance.now() / 1000, gap = name === 'eat' ? 0.04 : 0.02;
  if (now - (lastAt.get(name) || -1) < gap) return;
  lastAt.set(name, now);
  let buf = sfxBuf.get(name);
  if (buf === undefined) { buf = makeSfx(name); sfxBuf.set(name, buf); }
  if (!buf) return;
  const src = ctx.createBufferSource();
  src.buffer = buf; src.connect(sfxGain); src.start();
}

export function music(next) {
  if (next === mode) return;
  mode = next;
  if (!mode) { if (bgmSrc) { try { bgmSrc.stop(); } catch { /* 已停止 */ } bgmSrc = null; } return; }
  startBgm();
}

export function vibrate(ms) {
  if (!getSave().vibrate || typeof navigator === 'undefined' || !navigator.vibrate) return;
  try { navigator.vibrate(ms); } catch { /* 不支援就略過 */ }
}
