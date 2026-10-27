import { describe, expect, it } from 'vitest';
import { resolveConfig, wsUrl } from '../src/config.js';

const PAGE = 'https://play.example.invalid/game/';

describe('server URL 設定', () => {
  it.each(['https://', 'https://bad host.invalid', 'ftp://example.invalid', 'https://example.invalid?mode=game', 'https://example.invalid#game', 'https://user:pass@example.invalid'])(
    '錯誤網址立即拒絕：%s', bad => {
      const c = resolveConfig(bad, PAGE);
      expect(c.status).toBe('invalid');
      expect(c.serverUrl).toBeNull();
    });

  it('大小寫與尾斜線正規化並保留服務路徑，HTTPS 用 WSS', () => {
    const c = resolveConfig(' HTTPS://Example.Invalid/service/ ', PAGE);
    expect(c).toMatchObject({ status: 'ok', serverUrl: 'https://example.invalid/service' });
    expect(wsUrl(c.serverUrl)).toBe('wss://example.invalid/service/ws');
    expect(wsUrl('HTTPS://example.invalid/service/')).toBe('wss://example.invalid/service/ws');
  });

  it('HTTPS 頁面拒絕 HTTP 後端（混合內容）', () => {
    expect(resolveConfig('http://example.invalid', PAGE)).toMatchObject({ status: 'invalid', error: 'cfg.mixed' });
  });

  it('HTTP、IPv6、連接埠與服務路徑保留', () => {
    const c = resolveConfig('http://[::1]:3130/service/', 'http://localhost/game/');
    expect(c.status).toBe('ok');
    expect(wsUrl(c.serverUrl)).toBe('ws://[::1]:3130/service/ws');
  });

  it('沒有注入網址時同源（非 dev server）只保留 origin', () => {
    expect(resolveConfig('', 'HTTPS://Play.Example.Invalid/game/')).toMatchObject({ status: 'ok', serverUrl: 'https://play.example.invalid', source: 'same-origin' });
  });

  it('dev server 不做同源回退；沒有網址時維持單機，也不回退 localhost', () => {
    expect(resolveConfig('', 'http://localhost:5173/', { devServer: true })).toMatchObject({ status: 'unset', serverUrl: null });
    expect(resolveConfig('', '')).toMatchObject({ status: 'unset', serverUrl: null });
  });

  it('?server= 優先於建置注入；room／invite 參數被讀出', () => {
    const c = resolveConfig('https://built.example.invalid', 'https://play.example.invalid/?server=https%3A%2F%2Fq.example.invalid&room=ABCD&invite=tok123');
    expect(c).toMatchObject({ serverUrl: 'https://q.example.invalid', source: 'query', room: 'ABCD', invite: 'tok123' });
  });
});
