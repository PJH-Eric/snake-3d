import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Net, OutageWindow } from '../src/net.js';

describe('30 秒斷線提示（只在大廳與房間）', () => {
  it('單機／首頁、不在大廳房間時不提示', () => {
    const o = new OutageWindow();
    o.observe('offline', 0);
    expect(o.shouldShow(100)).toBe(false);
    o.setScreen(false, 0); o.observe('waking', 0);
    expect(o.shouldShow(100)).toBe(false);
  });
  it('進入大廳後滿 30 秒顯示一次；喚醒與重試不重設；恢復後清除、再次故障可再提示', () => {
    const o = new OutageWindow();
    o.setScreen(true, 0);
    o.observe('connecting', 0);
    expect(o.shouldShow(29.99)).toBe(false);
    o.observe('waking', 10); o.observe('retrying', 20);
    expect(o.shouldShow(30)).toBe(true);
    expect(o.shouldShow(200)).toBe(false);
    o.observe('open', 250);
    expect(o.active).toBe(false);
    o.observe('retrying', 300);
    expect(o.shouldShow(330)).toBe(true);
  });
  it('離開大廳（例如進入遊戲）取消提示；返回房間重新等 30 秒', () => {
    const o = new OutageWindow();
    o.setScreen(true, 0); o.observe('offline', 0);
    o.setScreen(false, 20);
    expect(o.active).toBe(false);
    o.observe('retrying', 25);
    expect(o.shouldShow(100)).toBe(false);
    o.setScreen(true, 900);
    expect(o.shouldShow(929.99)).toBe(false);
    expect(o.shouldShow(930)).toBe(true);
  });
  it('次月與跨年', () => {
    for (let m = 0; m < 12; m++) expect(OutageWindow.resetMonth(new Date(2026, m, 28), false)).toBe(String(((m + 1) % 12) + 1));
    expect(OutageWindow.resetMonth(new Date(2026, 11, 31), true)).toBe('January');
  });
});

class FakeSocket {
  constructor(url) { this.url = url; this.readyState = 0; this.sent = []; FakeSocket.all.push(this); }
  send(s) { this.sent.push(JSON.parse(s)); }
  close() { this.readyState = 3; if (this.onclose) this.onclose(); }
  open() { this.readyState = 1; this.onopen(); }
}
FakeSocket.all = [];

function make(fetchImpl, cfg) {
  FakeSocket.all = [];
  let now = 0;
  const net = new Net({ config: cfg || { status: 'ok', serverUrl: 'https://srv.example.invalid' }, fetch: fetchImpl, makeSocket: u => new FakeSocket(u), now: () => now });
  return { net, tick: s => { now += s; } };
}

describe('Net 連線層', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('沒有有效設定時是 unset，不發任何請求', () => {
    const f = vi.fn();
    const { net } = make(f, { status: 'unset', serverUrl: null });
    net.open('A', 'cat');
    expect(net.status).toBe('unset');
    expect(f).not.toHaveBeenCalled();
  });

  it('先 /health 再 WebSocket；onopen 送 hello 並進入 open', async () => {
    const f = vi.fn().mockResolvedValue({ ok: true });
    const { net } = make(f);
    net.open('小明', 'dog');
    await vi.advanceTimersByTimeAsync(0);
    expect(f.mock.calls[0][0]).toBe('https://srv.example.invalid/health');
    expect(FakeSocket.all[0].url).toBe('wss://srv.example.invalid/ws');
    FakeSocket.all[0].open();
    expect(net.status).toBe('open');
    expect(FakeSocket.all[0].sent[0]).toMatchObject({ type: 'hello', name: '小明', animal: 'dog' });
  });

  it('伺服器睡眠：健康檢查失敗顯示 waking，醒來後自動連線', async () => {
    let n = 0;
    const f = vi.fn(() => (++n < 3 ? Promise.reject(new Error('down')) : Promise.resolve({ ok: true })));
    const { net, tick } = make(f);
    net.open('A', 'cat');
    await vi.advanceTimersByTimeAsync(0);
    expect(net.status).toBe('waking');
    tick(2.5); await vi.advanceTimersByTimeAsync(2600);
    tick(2.5); await vi.advanceTimersByTimeAsync(2600);
    expect(FakeSocket.all.length).toBe(1);
  });

  it('離開線上後，舊健康檢查即使晚到也不能覆寫狀態或連線', async () => {
    let resolve;
    const f = vi.fn(() => new Promise(r => { resolve = r; }));
    const { net } = make(f);
    net.open('A', 'cat');
    net.close();
    expect(net.status).toBe('idle');
    resolve({ ok: true });
    await vi.advanceTimersByTimeAsync(10);
    expect(net.status).toBe('idle');
    expect(FakeSocket.all.length).toBe(0);
  });

  it('重新進入後，舊請求失敗不得覆寫新 session', async () => {
    const rejects = [];
    const f = vi.fn(() => new Promise((res, rej) => { rejects.push(rej); }));
    const { net } = make(f);
    net.open('A', 'cat'); net.close(); net.open('A', 'cat');
    rejects[0](new Error('old'));
    await vi.advanceTimersByTimeAsync(10);
    expect(net.status).toBe('connecting');
  });

  it('斷線自動重連；主動 close 不重連', async () => {
    const f = vi.fn().mockResolvedValue({ ok: true });
    const { net } = make(f);
    net.open('A', 'cat');
    await vi.advanceTimersByTimeAsync(0);
    FakeSocket.all[0].open();
    FakeSocket.all[0].close();
    expect(net.status).toBe('retrying');
    await vi.advanceTimersByTimeAsync(1000);
    expect(f.mock.calls.length).toBe(2);
    net.close();
    await vi.advanceTimersByTimeAsync(20000);
    expect(f.mock.calls.length).toBe(2);
    expect(net.status).toBe('idle');
  });

  it('replaced 之後不再重連', async () => {
    const f = vi.fn().mockResolvedValue({ ok: true });
    const { net } = make(f);
    net.open('A', 'cat');
    await vi.advanceTimersByTimeAsync(0);
    FakeSocket.all[0].open();
    net.dispatch(JSON.stringify({ type: 'replaced' }));
    FakeSocket.all[0].close();
    await vi.advanceTimersByTimeAsync(20000);
    expect(f.mock.calls.length).toBe(1);
  });
});
