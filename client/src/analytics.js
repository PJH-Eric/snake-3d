/* GA4：與其他遊戲共用同一套事件與參數規格（game_start／game_end／room_create／room_join／lobby_click／page_view）。
 * 設定由 index.html 的 window.GAME_ANALYTICS_CONFIG 提供；只在正式網域載入 Google tag，本機與預覽完全不動作。
 * 只送固定維度，不送姓名、房號、seed、邀請 token 或任意網址。 */
import { getLang } from './i18n.js';

const LOBBY_URL = 'https://pjh-eric.github.io/game-lobby/';
const EVENTS = ['game_start', 'game_end', 'room_create', 'room_join', 'lobby_click'];
let config = null, active = null, pendingRoom = null, activeScreen = '';
const roundKeys = [];

function cleanUrl(value) {
  try { const u = new URL(value); return /^https?:$/.test(u.protocol) ? u.origin + u.pathname : ''; } catch { return ''; }
}

function init(options) {
  if (config || !options) return;
  const siteUrl = cleanUrl(options.page_location);
  if (!siteUrl || !/^[a-z0-9-]{1,40}$/.test(options.game_id || '')) return;
  config = { game_id: options.game_id, page_location: siteUrl };
  if (location.hostname !== new URL(siteUrl).hostname || location.hostname !== 'pjh-eric.github.io' || !/^G-[A-Z0-9]+$/.test(options.measurement_id || '')) return;
  window.dataLayer = window.dataLayer || [];
  window.gtag = window.gtag || function () { window.dataLayer.push(arguments); };
  window.gtag('js', new Date());
  window.gtag('config', options.measurement_id, {
    game_id: config.game_id, page_location: siteUrl, page_referrer: cleanUrl(document.referrer),
    send_page_view: false, allow_google_signals: false, allow_ad_personalization_signals: false
  });
  const script = document.createElement('script');
  script.async = true;
  script.src = 'https://www.googletagmanager.com/gtag/js?id=' + options.measurement_id;
  document.head.appendChild(script);
}

function parameters(input) {
  const p = input || {}, out = {};
  if (config) out.game_id = config.game_id;
  for (const key of ['source_game_id', 'target', 'variant', 'difficulty', 'entry_point']) {
    const v = p[key];
    if ((typeof v === 'string' || typeof v === 'number') && /^[a-zA-Z0-9_-]{1,40}$/.test(String(v))) out[key] = String(v);
  }
  if (['solo', 'ai', 'online'].includes(p.mode)) out.mode = p.mode;
  if (Number.isInteger(p.players) && p.players >= 1 && p.players <= 32) out.players = p.players;
  if (Number.isFinite(p.duration_sec) && p.duration_sec >= 0) out.duration_sec = Math.round(p.duration_sec);
  if (['win', 'lose', 'draw', 'complete', 'dnf'].includes(p.result)) out.result = p.result;
  if (['lobby', 'invite_link'].includes(p.via)) out.via = p.via;
  out.app_language = getLang();
  out.transport_type = 'beacon';
  return out;
}

export function track(name, params) {
  if (!EVENTS.includes(name)) return;
  try { if (typeof window.gtag === 'function') window.gtag('event', name, parameters(params)); } catch { /* 統計失敗不影響遊戲 */ }
}

/** 一局開始（觀戰者不算）。key 相同的局只記一次。 */
export function gameStart(params, key) {
  if (params && params.spectator) { active = null; return; }
  if (key != null) { if (roundKeys.includes(key)) return; roundKeys.push(key); if (roundKeys.length > 100) roundKeys.shift(); }
  active = { params: parameters(params), key, at: Date.now() };
  track('game_start', active.params);
}

export function gameEnd(params, key) {
  if (!active || (key != null && active.key !== key)) return;
  const m = active;
  active = null;
  track('game_end', { ...m.params, duration_sec: (Date.now() - m.at) / 1000, ...(params || {}) });
}

export function gameAbort() { active = null; }

/** 送出建立／加入房間的請求時呼叫；伺服器回 joined 才算成功。 */
export function roomRequest(msg) {
  if (!msg) return;
  if (msg.type === 'create' || msg.type === 'join') pendingRoom = { action: msg.type, via: msg.token ? 'invite_link' : 'lobby' };
  else if (msg.type === 'leave') { pendingRoom = null; active = null; }
}
export function roomResponse(type) {
  if (type === 'joined' && pendingRoom) { track('room_' + pendingRoom.action, { mode: 'online', via: pendingRoom.via }); pendingRoom = null; }
  else if (type === 'joinFailed' || type === 'closed') pendingRoom = null;
}

/** 單頁應用的畫面追蹤：同一畫面不重複送。 */
export function pageView(screen) {
  if (!config || !/^[a-z0-9_-]{1,40}$/.test(screen || '') || screen === activeScreen) return;
  activeScreen = screen;
  try {
    if (typeof window.gtag === 'function') window.gtag('event', 'page_view', {
      game_id: config.game_id, page_location: config.page_location + '#/' + screen, page_referrer: cleanUrl(document.referrer),
      page_title: document.title + ' - ' + screen, screen_name: screen
    });
  } catch { /* 畫面追蹤失敗不影響操作 */ }
}

/** 給連結加上 ?lang=，讓遊戲大廳沿用目前語言。 */
export function lobbyUrl() {
  const u = new URL(LOBBY_URL);
  u.searchParams.set('lang', getLang());
  return u.toString();
}
export function lobbyClick() { track('lobby_click', { target: 'lobby_home_link', source_game_id: config && config.game_id }); }

init(window.GAME_ANALYTICS_CONFIG);
