/* 連線層：只在使用者進入線上流程後才呼叫 open()；先打 /health 喚醒冷啟動的伺服器，再連 WebSocket，斷線自動重連。
 * close() 會停止所有計時器與請求。首頁、單機與本機 AI 完全不會碰這個模組的 open()。 */
import { config, wsUrl } from './config.js';
import { getSave, setClientKey } from './store.js';

/** 只在線上連線持續 30 秒未成功時提示一次；恢復或離開後重設。 */
export class OutageWindow {
  constructor() { this.started = -1; this.shown = false; this.eligible = false; this.status = 'idle'; }
  get active() { return this.started >= 0; }
  setScreen(eligible, now) {
    if (this.eligible === eligible) return;
    this.eligible = eligible; this.started = -1; this.shown = false;
    if (eligible) this.observe(this.status, now);
  }
  observe(status, now) {
    this.status = status;
    if (!this.eligible || status === 'idle' || status === 'unset' || status === 'open') { this.started = -1; this.shown = false; }
    else if (!this.active && ['connecting', 'waking', 'retrying', 'offline'].includes(status)) this.started = now;
  }
  shouldShow(now) {
    if (!this.eligible || !this.active || this.shown || now - this.started < 30) return false;
    this.shown = true;
    return true;
  }
  static resetMonth(date, english) {
    const next = new Date(date.getFullYear(), date.getMonth() + 1, 1);
    return english ? next.toLocaleString('en-US', { month: 'long' }) : String(next.getMonth() + 1);
  }
}

export class Net {
  constructor(opts = {}) {
    this.cfg = opts.config || config;
    this.makeSocket = opts.makeSocket || (url => new WebSocket(url));
    this.fetch = opts.fetch || ((...a) => fetch(...a));
    this.now = opts.now || (() => performance.now() / 1000);
    this.status = 'idle'; this.detail = 0;
    this.sock = null; this.wantOpen = false; this.retry = 0; this.session = 0;
    this.retryTimer = null; this.wakeTimer = null;
    this.profile = { name: '', animal: 'cat' };
    this.handlers = new Map(); this.statusSubs = new Set();
    this.outage = new OutageWindow(); this.outageTimer = null; this.onOutage = null;
  }

  get connected() { return !!this.sock && this.sock.readyState === 1; }
  on(type, fn) {
    if (!this.handlers.has(type)) this.handlers.set(type, new Set());
    this.handlers.get(type).add(fn);
    return () => this.handlers.get(type).delete(fn);
  }
  onStatus(fn) { this.statusSubs.add(fn); return () => this.statusSubs.delete(fn); }

  setStatus(s, detail = 0) {
    this.status = s; this.detail = detail;
    this.outage.observe(s, this.now());
    this.statusSubs.forEach(fn => fn(s, detail));
  }
  setOutageScreen(eligible) {
    this.outage.setScreen(eligible, this.now());
    clearInterval(this.outageTimer);
    this.outageTimer = null;
    if (eligible) {
      this.outageTimer = setInterval(() => { if (this.outage.shouldShow(this.now()) && this.onOutage) this.onOutage(); }, 1000);
    }
  }
  setProfile(name, animal) { this.profile = { name, animal }; }

  open(name, animal) {
    if (name != null) this.profile = { name, animal };
    this.wantOpen = true;
    if (this.cfg.status !== 'ok' || !this.cfg.serverUrl) { this.setStatus('unset'); return; }
    if (this.sock || this.status === 'waking' || this.status === 'connecting') return;
    this.wake(++this.session);
  }

  close() {
    this.wantOpen = false; this.session++;
    clearTimeout(this.retryTimer); clearTimeout(this.wakeTimer);
    this.retryTimer = this.wakeTimer = null;
    if (this.sock) { const s = this.sock; this.sock = null; s.onclose = s.onmessage = s.onopen = s.onerror = null; try { s.close(); } catch { /* 已關閉 */ } }
    this.setStatus('idle');
  }

  send(msg) {
    if (!this.connected) return false;
    this.sock.send(JSON.stringify(msg));
    return true;
  }

  async wake(mine) {
    this.setStatus('connecting');
    const started = this.now();
    while (this.wantOpen && mine === this.session) {
      const ctl = new AbortController();
      const timer = setTimeout(() => ctl.abort(), 8000);
      let ok = false;
      try { const r = await this.fetch(this.cfg.serverUrl + '/health', { signal: ctl.signal, cache: 'no-store' }); ok = r.ok; } catch { ok = false; }
      clearTimeout(timer);
      /* 離開或重連後，舊請求即使失敗也不能覆寫新 session 的狀態 */
      if (!this.wantOpen || mine !== this.session) return;
      if (ok) { this.connect(); return; }
      if (this.now() - started > 90) break;
      this.setStatus('waking', Math.round(this.now() - started));
      await new Promise(res => { this.wakeTimer = setTimeout(res, 2500); });
      if (mine !== this.session) return;
    }
    if (this.wantOpen && mine === this.session) { this.setStatus('offline'); this.scheduleRetry(); }
  }

  connect() {
    const s = this.makeSocket(wsUrl(this.cfg.serverUrl));
    this.sock = s;
    this.setStatus('connecting');
    s.onopen = () => {
      if (this.sock !== s) return;
      this.retry = 0;
      s.send(JSON.stringify({ type: 'hello', key: getSave().clientKey, name: this.profile.name, animal: this.profile.animal }));
      this.setStatus('open');
    };
    s.onmessage = e => { if (this.sock === s) this.dispatch(e.data); };
    s.onclose = () => {
      if (this.sock !== s) return;
      this.sock = null;
      if (this.wantOpen) { this.setStatus('retrying'); this.scheduleRetry(); } else this.setStatus('idle');
    };
    s.onerror = () => { /* onclose 會接手 */ };
  }

  scheduleRetry() {
    if (!this.wantOpen) return;
    const wait = Math.min(8, 0.6 * Math.pow(1.6, this.retry++));
    clearTimeout(this.retryTimer);
    this.retryTimer = setTimeout(() => { this.retryTimer = null; if (this.wantOpen && !this.sock) this.open(); }, wait * 1000);
  }

  dispatch(text) {
    let msg;
    try { msg = JSON.parse(text); } catch { return; }
    if (!msg || typeof msg.type !== 'string') return;
    if (msg.type === 'welcome' && msg.key) setClientKey(msg.key);
    if (msg.type === 'replaced') { this.wantOpen = false; }
    const set = this.handlers.get(msg.type);
    if (set) [...set].forEach(fn => fn(msg));
  }
}

export const net = new Net();
