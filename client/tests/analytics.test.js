import { beforeEach, describe, expect, it, vi } from 'vitest';

const CONFIG = { measurement_id: 'G-TEST123', game_id: 'snake-3d', page_location: 'https://pjh-eric.github.io/snake-3d/' };

/* 每個情境重新載入模組（模組內有狀態），回傳 analytics 與「送出的事件」 */
async function load(host, config = CONFIG) {
  vi.resetModules();
  delete window.gtag; delete window.dataLayer; delete window.GAME_ANALYTICS_CONFIG;
  document.head.querySelectorAll('script[src*="googletagmanager"]').forEach(n => n.remove());
  const a = await import('../src/analytics.js');
  a.init(config, host);
  const events = () => (window.dataLayer || []).map(x => [...x]).filter(x => x[0] === 'event').map(x => ({ name: x[1], ...x[2] }));
  return { a, events };
}

describe('GA4 追蹤', () => {
  beforeEach(() => { document.head.innerHTML = ''; });

  it('本機與預覽（非正式網域）完全不動作：不載入 Google tag、不送事件', async () => {
    const { a, events } = await load('localhost');
    a.track('game_start', { mode: 'solo' });
    a.gameStart({ mode: 'solo', players: 1 }, 1);
    a.pageView('home');
    expect(window.gtag).toBeUndefined();
    expect(document.head.querySelector('script[src*="googletagmanager"]')).toBeNull();
    expect(events()).toEqual([]);
  });

  it('正式網域：載入 gtag 一次，關閉廣告個人化與自動 page view', async () => {
    await load('pjh-eric.github.io');
    const scripts = document.head.querySelectorAll('script[src*="googletagmanager.com/gtag/js?id=G-TEST123"]');
    expect(scripts.length).toBe(1);
    const cfg = [...window.dataLayer].map(x => [...x]).find(x => x[0] === 'config');
    expect(cfg[1]).toBe('G-TEST123');
    expect(cfg[2]).toMatchObject({ game_id: 'snake-3d', send_page_view: false, allow_google_signals: false, allow_ad_personalization_signals: false });
  });

  it('設定不合法就不啟用（game_id 格式、衡量 ID 格式）', async () => {
    expect((await load('pjh-eric.github.io', { ...CONFIG, game_id: 'Bad Id!' })).events()).toEqual([]);
    expect(window.gtag).toBeUndefined();
    await load('pjh-eric.github.io', { ...CONFIG, measurement_id: 'UA-1' });
    expect(window.gtag).toBeUndefined();
  });

  it('只送白名單事件與固定維度：不送暱稱、房號、seed、token 或任意網址', async () => {
    const { a, events } = await load('pjh-eric.github.io');
    a.track('purchase', { mode: 'solo' });
    a.track('game_start', { mode: 'solo', players: 2, name: '小明', room: 'ABC123', seed: 99, token: 'secret', url: 'https://x.test/?a=1', difficulty: 'hard', mode2: 'x' });
    const [e] = events();
    expect(events().length).toBe(1);
    expect(e).toMatchObject({ name: 'game_start', game_id: 'snake-3d', mode: 'solo', players: 2, difficulty: 'hard', app_language: 'zh-Hant', transport_type: 'beacon' });
    for (const k of ['name2', 'room', 'seed', 'token', 'url']) expect(e).not.toHaveProperty(k);
    expect(JSON.stringify(e)).not.toMatch(/小明|ABC123|secret|x\.test/);
  });

  it('一局只記一次開始與結束；觀戰者不算；沒開始就沒有結束', async () => {
    const { a, events } = await load('pjh-eric.github.io');
    a.gameEnd({ result: 'win' }, 1);
    expect(events()).toEqual([]);
    a.gameStart({ mode: 'ai', players: 4 }, 1);
    a.gameStart({ mode: 'ai', players: 4 }, 1);
    a.gameEnd({ result: 'win' }, 1);
    a.gameEnd({ result: 'win' }, 1);
    const names = events().map(e => e.name);
    expect(names).toEqual(['game_start', 'game_end']);
    expect(events()[1]).toMatchObject({ mode: 'ai', players: 4, result: 'win' });
    expect(events()[1].duration_sec).toBeGreaterThanOrEqual(0);
    a.gameStart({ mode: 'online', players: 3, spectator: true }, 2);
    a.gameEnd({ result: 'lose' }, 2);
    expect(events().length).toBe(2);
  });

  it('建立／加入房間：伺服器確認後才記，via 區分大廳與邀請連結；失敗不記', async () => {
    const { a, events } = await load('pjh-eric.github.io');
    a.roomRequest({ type: 'create' });
    a.roomResponse('joined');
    a.roomResponse('joined');
    a.roomRequest({ type: 'join', room: 'R1', token: 't' });
    a.roomResponse('joined');
    a.roomRequest({ type: 'join', room: 'R2' });
    a.roomResponse('joinFailed');
    a.roomResponse('joined');
    expect(events().map(e => [e.name, e.via])).toEqual([['room_create', 'lobby'], ['room_join', 'invite_link']]);
  });

  it('畫面追蹤：同一畫面不重複，網址固定為 canonical 加 #/畫面', async () => {
    const { a } = await load('pjh-eric.github.io');
    a.pageView('home'); a.pageView('home'); a.pageView('lobby'); a.pageView('Bad Screen!');
    const views = [...window.dataLayer].map(x => [...x]).filter(x => x[1] === 'page_view').map(x => x[2]);
    expect(views.map(v => v.page_location)).toEqual(['https://pjh-eric.github.io/snake-3d/#/home', 'https://pjh-eric.github.io/snake-3d/#/lobby']);
  });

  it('回遊戲大廳連結：正式大廳網址並帶目前語言；點擊事件只送固定維度', async () => {
    const { a, events } = await load('pjh-eric.github.io');
    const u = new URL(a.lobbyUrl());
    expect(u.origin + u.pathname).toBe('https://pjh-eric.github.io/game-lobby/');
    expect(u.searchParams.get('lang')).toBe('zh-Hant');
    a.lobbyClick();
    expect(events()[0]).toMatchObject({ name: 'lobby_click', target: 'lobby_home_link', source_game_id: 'snake-3d' });
  });
});
