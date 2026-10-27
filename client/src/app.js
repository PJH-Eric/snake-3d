/* 全域畫面狀態：目前畫面、Toast、連線橫幅與確認視窗。畫面切換時依需要開關連線（只有線上流程才連）。 */
import { useSyncExternalStore } from 'react';
import { net } from './net.js';
import { getSave } from './store.js';
import { autoName } from './i18n.js';

export function createStore(initial) {
  let state = initial;
  const subs = new Set();
  return {
    get: () => state,
    set(patch) { state = { ...state, ...(typeof patch === 'function' ? patch(state) : patch) }; subs.forEach(fn => fn()); },
    subscribe(fn) { subs.add(fn); return () => subs.delete(fn); }
  };
}
export function useStore(store, selector = s => s) {
  return useSyncExternalStore(store.subscribe, () => selector(store.get()));
}

export const app = createStore({
  screen: 'home', params: {}, toasts: [], banner: '', confirm: null, outage: false, replaced: false,
  lastRun: 'solo'
});
export const useApp = selector => useStore(app, selector);

export const ONLINE_SCREENS = ['lobby', 'room'];
const needsNet = (screen, params) => ONLINE_SCREENS.includes(screen) || (screen === 'game' && params && params.kind === 'online');

/** 顯示用暱稱：沒填就用「形容詞＋動物」自動取名（跟著語言走）。 */
export function myName() {
  const d = getSave();
  return (d.nickname || '').trim() || autoName(d.adj, d.animal);
}
export function profile() {
  const d = getSave();
  return { name: myName(), animal: d.animal };
}

let toastSeq = 1;
export function toast(text, secs = 3.2) {
  const id = toastSeq++;
  app.set(s => ({ toasts: [...s.toasts, { id, text }] }));
  setTimeout(() => app.set(s => ({ toasts: s.toasts.filter(x => x.id !== id) })), secs * 1000);
}
export const banner = text => app.set({ banner: text || '' });
export function confirmBox(cfg) { app.set({ confirm: cfg }); }
export function closeConfirm() { app.set({ confirm: null }); }

export function go(screen, params = {}) {
  const prev = app.get();
  app.set({ screen, params, confirm: null, outage: false });
  const online = needsNet(screen, params);
  net.setOutageScreen(ONLINE_SCREENS.includes(screen));
  if (online) {
    if (!needsNet(prev.screen, prev.params)) { net.open(profile().name, profile().animal); }
  } else {
    net.close();
    banner('');
  }
}
