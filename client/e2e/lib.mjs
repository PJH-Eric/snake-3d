/* e2e 共用：啟動 Edge（以 SwiftShader 軟體繪製 WebGL）、尺寸矩陣、版面溢出檢查。
 * 需要先 npm run build 並以 vite preview（4173）供應前端；線上測試另需 server（3130）。 */
import { chromium } from 'playwright-core';

export const APP = process.env.APP_URL || 'http://localhost:4173/';
export const SERVER = process.env.SERVER_URL || 'http://localhost:3130';
export const SIZES = {
  phoneP: { width: 390, height: 844, hasTouch: true, isMobile: true },
  phoneL: { width: 844, height: 390, hasTouch: true, isMobile: true },
  tabletP: { width: 820, height: 1180, hasTouch: true, isMobile: true },
  tabletL: { width: 1180, height: 820, hasTouch: true, isMobile: true },
  desktop: { width: 1440, height: 900 }
};

export async function launch() {
  return chromium.launch({ channel: 'msedge', args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--autoplay-policy=no-user-gesture-required'] });
}

export async function newPage(browser, size, lang = 'zh-TW', url = APP) {
  const ctx = await browser.newContext({ viewport: { width: size.width, height: size.height }, hasTouch: !!size.hasTouch, isMobile: !!size.isMobile, deviceScaleFactor: 1 });
  await ctx.addInitScript(l => { try { localStorage.setItem('snake3d.lang', l); } catch (e) { /* ignore */ } }, lang);
  const page = await ctx.newPage();
  page.errors = [];
  page.on('pageerror', e => page.errors.push('pageerror: ' + e.message));
  page.on('console', m => { if (m.type() === 'error') page.errors.push('console: ' + m.text()); });
  await page.goto(url);
  await page.waitForSelector('.app');
  return page;
}

/** 回傳目前畫面的版面問題：水平溢出、元素超出視窗、按鈕／晶片文字被截斷。 */
export async function layoutIssues(page) {
  return page.evaluate(() => {
    const issues = [];
    const vw = window.innerWidth, vh = window.innerHeight;
    if (document.documentElement.scrollWidth > vw + 1) issues.push('document 水平溢出 ' + document.documentElement.scrollWidth + '>' + vw);
    const name = el => (el.getAttribute('aria-label') || el.textContent || el.className || el.tagName).toString().trim().slice(0, 28);
    const visible = el => { const r = el.getBoundingClientRect(); const cs = getComputedStyle(el); return r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && cs.display !== 'none' && cs.opacity !== '0'; };
    const inScroll = el => el.closest('.sa-vp');
    for (const el of document.querySelectorAll('.app *, .mask *')) {
      if (!visible(el)) continue;
      const r = el.getBoundingClientRect();
      if (el.closest('canvas') || el.tagName === 'CANVAS') continue;
      const sa = inScroll(el);
      /* 水平方向任何元素都不能超出視窗（捲動區內也一樣，因為只提供垂直捲動） */
      if (r.right > vw + 1 || r.left < -1) {
        if (!el.closest('.sa-vp-x') && !el.closest('.sa-track')) issues.push(`水平超出：${el.tagName}.${(el.className + '').split(' ')[0]} "${name(el)}" [${Math.round(r.left)},${Math.round(r.right)}]`);
      }
      /* 非捲動區內的元素不能超出下緣（固定定位 HUD） */
      if (!sa && el.closest('.game-main') && (r.bottom > vh + 1 || r.top < -1) && !el.closest('.game-canvas')) issues.push(`垂直超出：.${(el.className + '').split(' ')[0]} "${name(el)}"`);
    }
    for (const el of document.querySelectorAll('.btn, .chip, .seg-item, .hud-pill, .ibtn')) {
      if (!visible(el)) continue;
      if (el.scrollWidth > el.clientWidth + 2 && getComputedStyle(el).overflow !== 'visible') issues.push(`文字被截斷：${name(el)}`);
      const r = el.getBoundingClientRect();
      if ((el.matches('.btn, .ibtn, .seg-item')) && (r.height < 32 || r.width < 32)) issues.push(`觸控區過小：${name(el)} ${Math.round(r.width)}x${Math.round(r.height)}`);
    }
    /* 重疊：右上角設定／語言不得遮住可操作的主要控制（簡易檢查：與 .hud-tl、.hud-top 的矩形相交） */
    const tr = document.querySelector('.topright');
    if (tr) {
      const a = tr.getBoundingClientRect();
      for (const el of document.querySelectorAll('.hud-tl, .hud-timer, .hud-chip, .minimap, .page-title .ibtn, .page-title .btn, .game-side .ibtn, .game-side .hd, .game-side, .boost, .ghost')) {
        if (!visible(el)) continue;
        const b = el.getBoundingClientRect();
        if (a.left < b.right - 1 && a.right > b.left + 1 && a.top < b.bottom - 1 && a.bottom > b.top + 1) issues.push(`與設定／語言重疊：.${(el.className + '').split(' ')[0]} "${name(el)}"`);
      }
    }
    return [...new Set(issues)];
  });
}
