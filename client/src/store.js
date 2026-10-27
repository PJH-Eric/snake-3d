import { useSyncExternalStore } from 'react';
import { ANIMAL_IDS } from './i18n.js';

const KEY = 'snake3d.save.v1';

const defaults = () => ({
  bgm: true, bgmVol: 0.5, sfx: true, sfxVol: 0.8, vibrate: true, reduceMotion: false,
  touchMode: 'auto', stickMode: 'float', stickSide: 'left', zoom: 'normal', minimap: true, quality: 'normal',
  nickname: '', animal: '', adj: Math.floor(Math.random() * 10),
  solo: { levels: ['easy', 'normal', 'hard'], speed: 'normal', obstacles: 'few', timeLimit: 0, theme: -1 },
  challenge: { speed: 'normal', obstacles: 'few', theme: -1 },
  best: {}, statSolo: { play: 0, win: 0 }, statOnline: { play: 0, win: 0 }, statChallenge: { play: 0, win: 0 },
  clientKey: ''
});
export const SETTING_KEYS = ['bgm', 'bgmVol', 'sfx', 'sfxVol', 'vibrate', 'reduceMotion', 'touchMode', 'stickMode', 'stickSide', 'zoom', 'minimap', 'quality'];

function load() {
  const base = defaults();
  let saved = null;
  try { saved = JSON.parse(localStorage.getItem(KEY) || 'null'); } catch { saved = null; }
  const d = saved && typeof saved === 'object' ? { ...base, ...saved } : base;
  d.solo = { ...base.solo, ...(d.solo || {}) };
  if (!Array.isArray(d.solo.levels) || !d.solo.levels.length) d.solo.levels = base.solo.levels;
  d.challenge = { ...base.challenge, ...(d.challenge || {}) };
  d.best = d.best && typeof d.best === 'object' ? d.best : {};
  for (const k of ['statSolo', 'statOnline', 'statChallenge']) d[k] = { ...base[k], ...(d[k] || {}) };
  if (!ANIMAL_IDS.includes(d.animal)) d.animal = ANIMAL_IDS[Math.floor(Math.random() * ANIMAL_IDS.length)];
  if (!d.clientKey || d.clientKey.length < 8) d.clientKey = 'u' + Array.from(crypto.getRandomValues(new Uint8Array(10)), b => b.toString(16).padStart(2, '0')).join('');
  return d;
}

let state = load();
const subs = new Set();
let snapshot = state;

function persist() {
  try { localStorage.setItem(KEY, JSON.stringify(state)); } catch { /* 儲存失敗就算了，不影響遊戲 */ }
}
function emit() { snapshot = { ...state }; subs.forEach(fn => fn()); }

export const getSave = () => state;
export function update(patch) {
  state = { ...state, ...(typeof patch === 'function' ? patch(state) : patch) };
  persist(); emit();
}
export function resetSettings() {
  const f = defaults();
  const patch = {};
  for (const k of SETTING_KEYS) patch[k] = f[k];
  update(patch);
}
export function useSave() {
  return useSyncExternalStore(fn => { subs.add(fn); return () => subs.delete(fn); }, () => snapshot);
}
export function subscribeSave(fn) { subs.add(fn); return () => subs.delete(fn); }

export const bestKey = (speed, obstacles) => 'free-' + speed + '-' + obstacles;
export const getBest = (speed, obstacles) => state.best[bestKey(speed, obstacles)] || 0;
export function setBest(speed, obstacles, score) {
  const k = bestKey(speed, obstacles), old = state.best[k] || 0;
  if (score > old) { update({ best: { ...state.best, [k]: score } }); return true; }
  return false;
}
export function record(statKey, win) {
  const c = state[statKey];
  update({ [statKey]: { play: c.play + 1, win: c.win + (win ? 1 : 0) } });
}
export function setClientKey(k) { if (k && k !== state.clientKey) update({ clientKey: k }); }

persist();
