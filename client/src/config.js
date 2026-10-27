/* 全站唯一的 server URL 入口。優先序：
 * 1. 網址參數 ?server=https://example.com
 * 2. 建置時注入的 GAME_SERVER_URL
 * 3. 頁面本身就是伺服器發出來的（同源，例如 npm start 本機單進程）— 只在非 Vite dev 時成立
 * 4. 都沒有 → 只能玩單機。不做 localhost 回退。 */

function parseHttp(value) {
  try {
    const u = new URL(value);
    if ((u.protocol !== 'http:' && u.protocol !== 'https:') || !u.hostname) return null;
    return u;
  } catch { return null; }
}

export function resolveConfig(injected, href, opts = {}) {
  let page;
  try { page = new URL(href); } catch { page = null; }
  const query = page ? page.searchParams.get('server') || '' : '';
  const raw = (query || injected || '').trim();
  const source = query ? 'query' : 'injected';
  const out = {
    serverUrl: null, source: 'none', status: 'unset', error: null,
    room: page ? page.searchParams.get('room') || '' : '',
    invite: page ? page.searchParams.get('invite') || '' : ''
  };
  if (!raw) {
    if (page && !opts.devServer && parseHttp(href)) { out.serverUrl = page.origin; out.source = 'same-origin'; out.status = 'ok'; }
    return out;
  }
  const u = parseHttp(raw);
  if (!u || u.username || u.password || u.search || u.hash) return { ...out, source, status: 'invalid', error: 'cfg.notAbs' };
  if (page && page.protocol === 'https:' && u.protocol === 'http:') return { ...out, source, status: 'invalid', error: 'cfg.mixed' };
  return { ...out, serverUrl: u.href.replace(/\/+$/, ''), source, status: 'ok' };
}

export function wsUrl(base) {
  const u = base.replace(/\/+$/, '');
  if (/^https:\/\//i.test(u)) return 'wss://' + u.slice(8) + '/ws';
  if (/^http:\/\//i.test(u)) return 'ws://' + u.slice(7) + '/ws';
  return u + '/ws';
}

export const config = resolveConfig(
  import.meta.env.GAME_SERVER_URL || '',
  typeof location !== 'undefined' ? location.href : '',
  { devServer: !!import.meta.env.DEV }
);
