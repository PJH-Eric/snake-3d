/* 觸控與設定 e2e：真實觸控事件（CDP）驅動手指指向與加速鈕；設定保存、底部搖桿、左右手、語言與焦點。 */
import { launch, newPage, layoutIssues, fingerPoint, SIZES, APP } from './lib.mjs';

const out = (process.env.SHOT_DIR || '.shots') + '/';
const shots = !!process.env.SHOTS;
let fails = 0;
const ok = (c, m) => { console.log((c ? '  ✓ ' : '  ✗ ') + m); if (!c) fails++; };
const b = await launch();
const url = APP + '?e2e=1';
const btn = (p, name) => p.getByRole('button', { name }).first();

async function touch(cdp, type, pts) {
  await cdp.send('Input.dispatchTouchEvent', { type, touchPoints: pts.map((p, i) => ({ x: p[0], y: p[1], id: i + 1 })) });
}
const state = page => page.evaluate(() => { const s = window.__snake3d && window.__snake3d.session; if (!s) return null; const p = s.state.players[0]; return { ta: p.ta, boost: p.boost, alive: p.alive, phase: s.state.phase, boxes: !!document.querySelector('.boost') }; });

for (const [sizeName, size] of [['phoneL', SIZES.phoneL], ['tabletP', SIZES.tabletP]]) {
  console.log('\n[' + sizeName + ']');
  const page = await newPage(b, size, 'zh-Hant', url);
  const cdp = await page.context().newCDPSession(page);
  await btn(page, '單人挑戰').click();
  await btn(page, '開始遊戲').click();
  await page.waitForFunction(() => window.__snake3d && window.__snake3d.session.state.phase === 'play', null, { timeout: 15000 });
  /* 凍結模擬（輸入仍寫入規則狀態），避免軟體繪製時蛇先撞牆而讓觸控元件消失 */
  await page.evaluate(() => { window.__snake3d.session.paused = () => true; });
  ok(await page.locator('.boost').isVisible(), '觸控裝置自動顯示加速鈕');
  ok(!(await page.locator('.energy').count()), '觸控模式不顯示桌機能量條（改為加速鈕外圈）');
  const w = size.width, h = size.height;

  /* 依手指：直接指向場地位置，不顯示搖桿。 */
  await touch(cdp, 'touchStart', [await fingerPoint(page, 4, 0)]);
  ok(!(await page.locator('.stick').isVisible()), '依手指操作時不顯示搖桿');
  await page.waitForTimeout(150);
  let st = await state(page);
  ok(Math.abs(st.ta) < 0.05, `指向右側 → 目標角度 ≈ 0（${st.ta.toFixed(2)}）`);
  await touch(cdp, 'touchMove', [await fingerPoint(page, 0, 4)]);
  await page.waitForTimeout(150);
  st = await state(page);
  ok(Math.abs(st.ta - Math.PI / 2) < 0.05, `指向下方 → 目標角度 ≈ π/2（${st.ta.toFixed(2)}）`);
  if (shots) await page.screenshot({ path: `${out}touch-${sizeName}-finger.png` });
  await touch(cdp, 'touchEnd', []);
  await page.waitForTimeout(100);
  ok(!(await page.locator('.stick').isVisible()), '放開手指後仍不顯示搖桿');

  /* 加速鈕 */
  const box = await page.locator('.boost-core').boundingBox();
  const bx = box.x + box.width / 2, by = box.y + box.height / 2;
  await touch(cdp, 'touchStart', [[bx, by]]);
  await page.waitForTimeout(150);
  ok((await state(page)).boost === true, '按住加速鈕 → 加速');
  await touch(cdp, 'touchEnd', []);
  await page.waitForTimeout(150);
  ok((await state(page)).boost === false, '放開 → 停止加速');

  /* 搖桿與加速鈕同時（多指） */
  await touch(cdp, 'touchStart', [await fingerPoint(page, -4, 0), [bx, by]]);
  await page.waitForTimeout(150);
  st = await state(page);
  ok(st.boost === true && Math.abs(Math.abs(st.ta) - Math.PI) < 0.05, `多指：同時轉向與加速（ta=${st.ta.toFixed(2)}, boost=${st.boost}）`);
  await touch(cdp, 'touchEnd', []);

  /* 開啟設定（暫停單機規則），再關閉恢復；輸入被清除 */
  await page.getByRole('button', { name: '設定' }).click();
  await page.waitForTimeout(300);
  ok((await state(page)).boost === false, '開啟設定視窗時清除加速狀態');
  await page.evaluate(() => { window.__snake3d.session.paused = () => !!document.querySelector('.modal'); });
  const t0 = await page.evaluate(() => window.__snake3d.session.state.time);
  await page.waitForTimeout(600);
  const t1 = await page.evaluate(() => window.__snake3d.session.state.time);
  ok(t1 === t0, '單機：開啟設定時規則暫停');
  /* 固定搖桿＋右手 */
  await page.getByRole('radio', { name: '依搖桿', exact: true }).click();
  await page.getByRole('radio', { name: '右手邊' }).click();
  if (shots) await page.screenshot({ path: `${out}touch-${sizeName}-settings.png` });
  await page.keyboard.press('Escape');
  await page.waitForTimeout(400);
  await page.evaluate(() => { window.__snake3d.session.paused = () => true; });
  const stickBox = await page.locator('.stick').boundingBox();
  const boostBox = await page.locator('.boost').boundingBox();
  ok(stickBox && stickBox.x > w / 2, '固定搖桿在右手邊顯示');
  ok(boostBox && boostBox.x < w / 2, '加速鈕換到另一側（左）');
  if (shots) await page.screenshot({ path: `${out}touch-${sizeName}-fixed.png` });
  /* 固定模式：點區域外不觸發 */
  await touch(cdp, 'touchStart', [[w * 0.2, h * 0.2]]);
  await page.waitForTimeout(100);
  await touch(cdp, 'touchEnd', []);
  /* 設定重新整理後仍保留 */
  await page.reload();
  await page.waitForSelector('.app');
  await page.getByRole('button', { name: '設定' }).click();
  ok(await page.getByRole('radio', { name: '依搖桿', exact: true }).getAttribute('aria-checked') === 'true', '重新進入後設定仍保留（localStorage）');
  await page.keyboard.press('Escape');
  ok((await layoutIssues(page)).length === 0, '首頁無版面問題');
  ok(!page.errors.length, '沒有 console 錯誤：' + page.errors.join(' | '));
  await page.context().close();
}

/* 語言按鈕緊鄰設定按鈕；語言與音量等偏好保存 */
{
  const page = await newPage(b, SIZES.phoneP, 'zh-Hant', url);
  const lang = page.locator('.topright .langbtn'), gear = page.locator('.topright .ibtn-orange');
  const a = await lang.boundingBox(), g = await gear.boundingBox();
  ok(g.x - (a.x + a.width) < 16 && g.x > a.x && Math.abs(g.y - a.y) < 6, `語言按鈕緊鄰設定按鈕（間距 ${Math.round(g.x - (a.x + a.width))}px）`);
  await lang.click();
  ok(await page.getByRole('menuitemradio').count() === 3, '語言選單列出三種語言');
  ok(await page.getByRole('menuitemradio', { name: '繁體中文' }).getAttribute('aria-checked') === 'true', '目前語言（繁體）已勾選');
  await page.keyboard.press('Escape');
  ok(await page.getByRole('menuitemradio').count() === 0 && await page.evaluate(() => document.activeElement.classList.contains('langbtn')), 'Esc 關閉選單並把焦點還給語言鈕');
  await lang.click(); await page.getByRole('menuitemradio', { name: '简体中文' }).click();
  ok(await page.evaluate(() => document.documentElement.lang) === 'zh-Hans' && (await page.title()).includes('贪吃蛇'), '切換為簡體中文');
  await lang.click(); await page.getByRole('menuitemradio', { name: 'English' }).click();
  ok((await page.title()).includes('3D Snake') && await page.evaluate(() => localStorage.getItem('game-island-lang')) === 'en', '切換為英文並寫入共用 key');
  await page.reload(); await page.waitForSelector('.app');
  ok(await page.evaluate(() => document.documentElement.lang) === 'en', '語言偏好重新整理後保留');
  await page.goto(url + (url.includes('?') ? '&' : '?') + 'lang=zh-Hans'); await page.waitForSelector('.app');
  ok(await page.evaluate(() => document.documentElement.lang) === 'zh-Hans', '網址 ?lang= 優先於已保存的語言');
  await page.goto(url); await page.waitForSelector('.app');
  await page.getByRole('button', { name: 'Settings' }).click();
  const bgm = page.getByRole('switch', { name: 'Music', exact: true });
  await bgm.click();
  await page.keyboard.press('Escape');
  await page.reload(); await page.waitForSelector('.app');
  await page.getByRole('button', { name: 'Settings' }).click();
  ok(await page.getByRole('switch', { name: 'Music', exact: true }).getAttribute('aria-checked') === 'false', '音樂開關保存');
  await page.getByRole('button', { name: 'Reset' }).click();
  ok(await page.getByRole('switch', { name: 'Music', exact: true }).getAttribute('aria-checked') === 'true', '恢復預設');
  /* 焦點鎖定與關閉後回到原焦點 */
  await page.keyboard.press('Escape');
  const focusedBack = await page.evaluate(() => document.activeElement && document.activeElement.getAttribute('aria-label'));
  ok(focusedBack === 'Settings', '關閉設定後焦點回到設定按鈕（' + focusedBack + '）');
  await page.context().close();
}
await b.close();
console.log(fails ? `\n${fails} 項失敗` : '\n全部通過');
process.exit(fails ? 1 : 0);
