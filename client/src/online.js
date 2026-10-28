/* 線上對戰的資料與伺服器訊息處理。規則與快照由伺服器權威計算，前端只負責顯示與送出輸入。 */
import { net } from './net.js';
import { app, banner, createStore, go, toast, useStore, profile, ONLINE_SCREENS } from './app.js';
import { config } from './config.js';
import { getSave } from './store.js';
import { serverMessage, t } from './i18n.js';
import * as sound from './sound.js';
import { roomResponse } from './analytics.js';

export const online = createStore({
  room: null, rooms: [], notice: '', chat: [], inGame: false, mySlot: -1, startInfo: null, result: null,
  invite: null,                       /* { room, token, info, error, joining } */
  roomInvites: [],                    /* 房主產生的連結（token 清單在 room.invites） */
  chatSeq: 0, unread: 0
});
export const useOnline = selector => useStore(online, selector);

const REASONS = { invalid: 'inv.invalid', revoked: 'inv.revoked', expired: 'inv.expired', closed: 'inv.closed', banned: 'inv.banned', spec_full: 'inv.spec_full' };
export const reasonText = code => (REASONS[code] ? t(REASONS[code]) : '');

/* 對戰畫面的橋接：快照在畫面建立前就可能抵達，先暫存。 */
let bridge = null;
let pendingSnaps = [];
export function attachBridge(b) {
  bridge = b;
  const snaps = pendingSnaps; pendingSnaps = [];
  for (const s of snaps) b.onSnap(s);
  const r = online.get().result;
  if (r) b.onResult(r.result, r.players);
  return () => { if (bridge === b) bridge = null; };
}

const cur = () => app.get();
const inOnlineScreen = () => ONLINE_SCREENS.includes(cur().screen) || (cur().screen === 'game' && online.get().inGame);

function repaintBanner() {
  if (!inOnlineScreen()) { banner(''); return; }
  const n = net;
  switch (n.status) {
    case 'waking': banner(t('net.waking', { n: n.detail })); break;
    case 'connecting': banner(t('net.connecting')); break;
    case 'retrying': banner(t('net.retrying')); break;
    case 'offline': banner(t('net.offline')); break;
    default: banner('');
  }
}

function resetRoomState() {
  online.set({ room: null, inGame: false, chat: [], mySlot: -1, startInfo: null, result: null, unread: 0 });
  pendingSnaps = [];
}

function askInvite() {
  const inv = online.get().invite;
  if (inv && !inv.info && !inv.error && net.connected) net.send({ type: 'inviteInfo', room: inv.room, token: inv.token });
}

function cleanUrl() {
  if (typeof history === 'undefined') return;
  const u = new URL(location.href);
  u.searchParams.delete('room'); u.searchParams.delete('invite');
  try { history.replaceState(null, '', u.pathname + (u.search || '') + u.hash); } catch { /* 無法改網址就算了 */ }
}

export function initOnline() {
  net.onStatus(() => { repaintBanner(); askInvite(); });
  net.onOutage = () => app.set({ outage: true });

  net.on('welcome', m => {
    online.set({ room: m.room || null });
    if (m.room && cur().screen === 'lobby' && m.room.phase !== 'playing') go('room');
    askInvite();
  });
  net.on('rooms', m => online.set({ rooms: m.rooms || [], onlineCount: Number.isInteger(m.online) && m.online >= 0 ? m.online : null }));
  net.on('room', m => {
    const room = m.room || null;
    online.set({ room });
    const s = cur();
    if (!room) {
      if (s.screen === 'room' || (s.screen === 'game' && online.get().inGame)) { resetRoomState(); go('lobby'); }
      return;
    }
    if (s.screen === 'lobby' && room.phase !== 'playing') go('room');
    else if (s.screen === 'game' && online.get().inGame && room.phase === 'lobby') {
      online.set({ inGame: false, result: null });
      go('room'); toast(t('net.newRound'), 4.2);
    }
  });
  net.on('joined', m => {
    roomResponse('joined');
    online.set({ invite: null }); cleanUrl();
    if (m.note) toast(serverMessage(m.note), 4);
    sound.sfx('join');
    if (cur().screen !== 'game') go('room');
  });
  net.on('joinFailed', m => {
    roomResponse('joinFailed');
    const inv = online.get().invite;
    if (inv && inv.joining) online.set({ invite: { ...inv, joining: false, error: m.reason } });
    else toast(reasonText(m.reason) || t('net.cantJoin'), 3.6);
  });
  net.on('inviteInfo', m => {
    const inv = online.get().invite;
    if (!inv) return;
    online.set({ invite: m.ok ? { ...inv, info: m } : { ...inv, error: m.reason } });
  });
  net.on('chatlog', m => online.set({ chat: m.msgs || [] }));
  net.on('chat', m => {
    const chat = [...online.get().chat, m.m].slice(-80);
    online.set(s => ({ chat, unread: m.m.role !== 'sys' ? s.unread + 1 : s.unread, chatSeq: s.chatSeq + 1 }));
    if (m.m.role !== 'sys') sound.sfx('chat');
  });
  net.on('closed', m => {
    const text = serverMessage(m.text) || t('net.closed');
    online.set({ notice: text });
    toast(text, 4.2);
  });
  net.on('kicked', m => {
    const text = serverMessage(m.text) || t('net.kickedToast');
    toast(text, 4.2);
    online.set({ notice: serverMessage(m.text) || t('net.kickedNote') });
  });
  net.on('error', m => toast(serverMessage(m.text) || t('net.error'), 3.6));
  net.on('start', m => {
    app.set({ confirm: null });
    pendingSnaps = [];
    online.set({ inGame: true, mySlot: typeof m.slot === 'number' ? m.slot : -1, startInfo: m.game, result: null });
    go('game', { kind: 'online' });
  });
  net.on('snap', m => {
    if (!online.get().inGame) return;
    if (bridge) bridge.onSnap(m.s);
    else { pendingSnaps.push(m.s); if (pendingSnaps.length > 40) pendingSnaps.shift(); }
  });
  net.on('result', m => {
    if (!online.get().inGame) return;
    online.set({ result: { result: m.result, players: m.players } });
    if (bridge) bridge.onResult(m.result, m.players);
  });
  net.on('replaced', () => { toast(t('net.replaced'), 5); app.set({ replaced: true }); banner(t('net.replacedBanner')); });
}

/* ---------- 動作 ---------- */
export const isHost = room => !!(room && room.you && room.you.host);

export function sendProfile() {
  const p = profile();
  net.setProfile(p.name, p.animal);
  net.send({ type: 'profile', name: p.name, animal: p.animal });
}
export function leaveRoom() { net.send({ type: 'leave' }); resetRoomState(); go('lobby'); }
export function leaveToHome() { net.send({ type: 'leave' }); resetRoomState(); go('home'); }
export function backToRoom() { online.set({ inGame: false, result: null }); go('room'); }
export function rematch() { net.send({ type: 'rematch' }); online.set({ inGame: false, result: null }); go('room'); toast(t('res.rematch'), 3.2); }
export function clearUnread() { online.set({ unread: 0 }); }

export function startInvite(room, token) {
  online.set({ invite: { room, token, info: null, error: null, joining: false } });
}
export function inviteLink(token) {
  const room = online.get().room;
  const u = new URL(location.href);
  u.search = ''; u.hash = '';
  u.searchParams.set('room', room.id);
  u.searchParams.set('invite', token);
  if (config.source === 'query' && config.serverUrl) u.searchParams.set('server', config.serverUrl);
  return u.toString();
}

/** 網址帶 ?room=&invite= 時進入邀請流程：只停在大廳，確認暱稱後才加入。 */
export function bootInvite() {
  if (config.room && config.invite) {
    startInvite(config.room.toUpperCase(), config.invite);
    go('lobby');
  }
}

export function sendJoin(name) {
  const inv = online.get().invite;
  if (!inv || !inv.info || inv.joining) return;
  online.set({ invite: { ...inv, joining: true } });
  net.send({ type: 'join', room: inv.room, token: inv.token, as: inv.info.role, name, animal: getSave().animal });
}
