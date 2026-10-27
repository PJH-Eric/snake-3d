/* 單機完整流程 e2e：單人挑戰與對戰電腦各完成一局 → 結算 overlay 三個動作 → 再來一局 → 回到設定／首頁；
 * 連續重開多局不留下殘留狀態或 WebGL 警告。 */
import { launch, newPage, layoutIssues, SIZES, APP } from './lib.mjs';

const out = (process.env.SHOT_DIR || '.shots') + '/';
const shots = !!process.env.SHOTS;
let fails = 0;
const ok = (c, m) => { console.log((c ? '  ✓ ' : '  ✗ ') + m); if (!c) fails++; };
const b = await launch();
const url = APP + '?e2e=1';
const btn = (p, name) => p.getByRole('button', { name }).first();
const result = page => page.locator('.res-actions');
const waitResult = page => result(page).waitFor({ timeout: 150000 }).then(() => true, () => false);
const speedUp = page => page.evaluate(() => { const s = window.__snake3d.session; const wait = () => s.state.phase === 'play'; return wait(); });

const page = await newPage(b, SIZES.desktop, 'zh-TW', url);
/* 軟體繪製（SwiftShader）資源有限：流程測試改用省電畫質，畫質本身另有視覺檢查 */
await page.evaluate(() => localStorage.setItem('snake3d.save.v1', JSON.stringify({ quality: 'low' })));
await page.reload(); await page.waitForSelector('.app');
const warns = [];
page.on('console', m => { if (m.type() === 'warning' || m.type() === 'error') warns.push(m.text()); });

/* ---------- 對戰電腦：一局到結算 ---------- */
await btn(page, '對戰電腦').click();
await btn(page, '開始遊戲').click();
ok(await waitResult(page), '對戰電腦：一局打完出現結算 overlay');
ok(await page.locator('.game .game-canvas').count() === 1, '結算疊在 3D 遊戲畫面上');
const labels = await page.locator('.res-actions button').allTextContents();
ok(labels.length === 3 && labels[0].includes('再來一局') && labels[1].includes('回到設定') && labels[2].includes('回到首頁'), '結算三個動作：再來一局／回到設定／回到首頁');
ok((await page.locator('.res-row').count()) === 4, '排行榜列出 4 位玩家');
if (shots) await page.screenshot({ path: out + 'flow-solo-result.png' });
ok((await layoutIssues(page)).length === 0, '結算畫面無版面問題');

/* 再來一局：新的場次、狀態乾淨 */
await page.locator('.res-actions').getByRole('button', { name: '再來一局' }).click();
await page.waitForFunction(() => window.__snake3d && window.__snake3d.session && window.__snake3d.session.state.time < 1 && !window.__snake3d.session.result, null, { timeout: 15000 });
ok(await page.locator('.res-actions').count() === 0, '再來一局：結算視窗關閉、新局開始（時間歸零）');
ok(await page.evaluate(() => window.__snake3d.session.view.players.every(p => p.alive && p.score === 0)), '新局所有玩家存活且分數為 0');

/* 暫停選單：重新開始、回首頁 */
await page.keyboard.press('Escape');
await page.getByRole('dialog').getByRole('button', { name: '重新開始' }).click();
await page.waitForFunction(() => window.__snake3d.session.state.time < 1);
ok(true, '暫停選單「重新開始」');
await page.keyboard.press('Escape');
await page.getByRole('dialog').getByRole('button', { name: '回首頁' }).click();
ok(await page.getByRole('button', { name: '單人挑戰' }).first().isVisible(), '暫停選單「回首頁」');

/* ---------- 單人挑戰：撞牆結束 → 回到設定 ---------- */
await btn(page, '單人挑戰').click();
await btn(page, '開始遊戲').click();
ok(await waitResult(page), '單人挑戰：撞牆後出現結算');
ok(await page.locator('.stat-box').count() === 4, '挑戰結算顯示分數、長度、等級、最高分');
await page.locator('.res-actions').getByRole('button', { name: '回到設定' }).click();
ok(await page.getByRole('heading', { name: '單人挑戰' }).isVisible(), '回到設定 → 回到單人挑戰設定頁');
ok(await page.getByText('這組設定的最高分').isVisible(), '設定頁保留紀錄資訊');
await btn(page, '返回').click();

/* ---------- 連續重開：無殘留、無 WebGL 警告 ---------- */
await btn(page, '單人挑戰').click();
await btn(page, '開始遊戲').click();
for (let i = 0; i < 4; i++) {
  await page.waitForFunction(() => window.__snake3d && window.__snake3d.session && window.__snake3d.session.state.phase === 'play', null, { timeout: 15000 });
  await page.keyboard.press('Escape');
  await page.getByRole('dialog').getByRole('button', { name: '重新開始' }).click();
  await page.waitForTimeout(200);
}
ok(await page.locator('.game-canvas').count() === 1, '連續重開 4 次：畫面上只有一個 3D 畫布');
ok(!warns.some(w => /WebGL|context/i.test(w)), 'WebGL 沒有警告（context 已正確釋放）：' + warns.filter(w => /WebGL|context/i.test(w)).join(' | '));
await page.keyboard.press('Escape');
await page.getByRole('dialog').getByRole('button', { name: '回首頁' }).click();
ok(page.errors.length === 0, '沒有 console／頁面錯誤：' + page.errors.join(' | '));
void speedUp;
await b.close();
console.log(fails ? `\n${fails} 項失敗` : '\n全部通過');
process.exit(fails ? 1 : 0);
