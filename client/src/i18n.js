import dicts from '@shared/locales.json';
import { useSyncExternalStore } from 'react';

export const LANGS = ['zh-Hant', 'zh-Hans', 'en'];
export const LANG_NAMES = { 'zh-Hant': '繁體中文', 'zh-Hans': '简体中文', en: 'English' };
const DEFAULT = 'zh-Hant';
/* 與其他遊戲共用同一個 key，同一來源下語言偏好互通；舊版 key 讀到時沿用一次 */
const KEY = 'game-island-lang';
const LEGACY_KEY = 'snake3d.lang';
let lang = DEFAULT;
const subs = new Set();

function normalize(v) {
  if (!v) return null;
  v = String(v).toLowerCase().replace(/_/g, '-');
  if (v === 'en' || v.startsWith('en-')) return 'en';
  if (v.startsWith('zh')) return /^zh-(hans|cn|sg)/.test(v) ? 'zh-Hans' : 'zh-Hant';
  return null;
}

/* 語言決定順序：網址 ?lang= → localStorage → 繁中 */
function detect() {
  try { const q = normalize(new URLSearchParams(location.search).get('lang')); if (q) return q; } catch { /* 沒有網址就略過 */ }
  try { const saved = normalize(localStorage.getItem(KEY) || localStorage.getItem(LEGACY_KEY)); if (saved) return saved; } catch { /* 無法讀取就用預設 */ }
  return DEFAULT;
}
lang = detect();

function sync() {
  if (typeof document === 'undefined') return;
  document.documentElement.lang = lang;
  document.title = t('meta.title');
}

export function getLang() { return lang; }
export function setLang(next) {
  next = normalize(next);
  if (!next || next === lang) return;
  lang = next;
  try { localStorage.setItem(KEY, next); } catch { /* 儲存失敗不影響遊戲 */ }
  try {
    const u = new URL(location.href);
    if (u.searchParams.has('lang')) { u.searchParams.set('lang', next); history.replaceState(history.state, '', u); }
  } catch { /* 沒有網址就略過 */ }
  sync();
  subs.forEach(fn => fn());
}
export function useLang() {
  return useSyncExternalStore(fn => { subs.add(fn); return () => subs.delete(fn); }, () => lang);
}

function raw(key, l = lang) {
  const v = dicts[l] && dicts[l][key];
  if (v != null) return v;
  return dicts[DEFAULT][key];
}

/** t('res.time', { m: 1, s: 5 }) */
export function t(key, params) {
  let v = raw(key);
  if (v == null) return key;
  if (params) for (const k of Object.keys(params)) v = v.split('{' + k + '}').join(String(params[k]));
  return v;
}
export const hasKey = key => raw(key) != null;

/* ---------- 伺服器傳來的繁中系統句子：用 srv.* 句型轉成目前語言 ---------- */
let srv = null;
const esc = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
function buildSrv() {
  srv = [];
  for (const [key, text] of Object.entries(dicts[DEFAULT])) {
    if (!key.startsWith('srv.')) continue;
    const names = [];
    let re = '^', i = 0;
    while (i < text.length) {
      const a = text.indexOf('{', i), b = a < 0 ? -1 : text.indexOf('}', a);
      if (a < 0 || b < 0) { re += esc(text.slice(i)); break; }
      re += esc(text.slice(i, a));
      names.push(text.slice(a + 1, b));
      re += '(.+?)';
      i = b + 1;
    }
    srv.push({ re: new RegExp(re + '$'), key, names });
  }
}
export function serverMessage(text) {
  if (!text || lang === DEFAULT) return text;
  if (!srv) buildSrv();
  for (const p of srv) {
    const m = p.re.exec(text);
    if (!m) continue;
    const params = {};
    p.names.forEach((n, i) => { params[n] = m[i + 1]; });
    return t(p.key, params);
  }
  return text;
}

/* ---------- 名稱 ---------- */
export const animalName = id => t('animal.' + id);
export const speedName = id => t('speed.' + id);
export const obstacleName = id => t('obs.' + id);
export const themeName = id => (id < 0 ? t('opt.random') : t('theme.' + id));
export const levelName = id => t('level.' + id + '.name');
export const levelHint = id => t('level.' + id + '.hint');
export const timeName = sec => (sec > 0 ? t('opt.min', { n: sec / 60 }) : t('opt.unlimited'));
export const ANIMAL_IDS = ['cat', 'dog', 'bunny', 'bear', 'panda', 'fox', 'frog', 'penguin'];
export function autoName(adj, animal) {
  if (!ANIMAL_IDS.includes(animal)) animal = 'cat';
  return t('adj.' + adj) + t('name.sep') + animalName(animal);
}
/** 電腦玩家顯示動物名；真人顯示暱稱。 */
export const playerName = (kind, animal, name) => (kind === 'ai' && ANIMAL_IDS.includes(animal) ? animalName(animal) : name || '');
/** 房名「某某的房間」是伺服器產生的繁中預設名，英文介面要換掉。 */
export function roomName(n) {
  const m = /^(.*)的房間$/.exec(n || '');
  return m && m[1] ? t('room.default', { name: m[1] }) : n || '';
}
export const clock = sec => { const s = Math.max(0, Math.floor(sec)); return Math.floor(s / 60) + ':' + String(s % 60).padStart(2, '0'); };

sync();
