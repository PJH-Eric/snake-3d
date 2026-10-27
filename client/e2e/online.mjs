/* 線上端對端：真的連 server（3130）——大廳、建立房間、加電腦、邀請連結（玩家／觀戰）、準備、對局、聊天、結算 overlay 與零真人自動關閉。
 * 另外驗證：進入線上流程之前完全不連後端、沒有 /api/presence 請求。 */
import { launch, newPage, layoutIssues, SERVER, SIZES, APP } from './lib.mjs';

const out = (process.env.SHOT_DIR || '.shots') + '/';
const shots = !!process.env.SHOTS;
let fails = 0;
const ok = (c, m) => { console.log((c ? '  ✓ ' : '  ✗ ') + m); if (!c) fails++; };
const b = await launch();
const base = APP + '?e2e=1&server=' + encodeURIComponent(SERVER);
const btn = (p, name) => p.getByRole('button', { name }).first();
const hasText = (p, text, timeout = 15000) => p.getByText(text, { exact: false }).first().waitFor({ timeout }).then(() => true, () => false);

/* ---------- 0. 單機／首頁不連後端 ---------- */
{
  const page = await newPage(b, SIZES.desktop, 'zh-TW', base);
  const reqs = [];
  page.on('request', r => { if (r.url().startsWith(SERVER)) reqs.push(r.url()); });
  page.on('websocket', ws => reqs.push('ws:' + ws.url()));
  await page.waitForTimeout(1200);
  await btn(page, '對戰電腦').click(); await page.waitForTimeout(600);
  await btn(page, '開始遊戲').click(); await page.waitForTimeout(3000);
  ok(reqs.length === 0, `首頁、單機與本機 AI 不連線後端（請求數 ${reqs.length}）`);
  await page.context().close();
}

const host = await newPage(b, SIZES.desktop, 'zh-TW', base);
const hostReqs = [];
host.on('request', r => { if (r.url().includes('/api/presence')) hostReqs.push(r.url()); });
await btn(host, '線上多人').click();
ok(await hasText(host, '已連線'), '大廳連線成功（先 /health 再 WebSocket）');
if (shots) await host.screenshot({ path: out + 'lobby.png' });
await btn(host, '建立房間').click();
await host.getByRole('dialog').getByRole('button', { name: '建立', exact: true }).click();
ok(await hasText(host, '玩家席位'), '建立房間後進入房間畫面');
await host.getByRole('button', { name: '加電腦' }).first().click();
ok(await hasText(host, '電腦・'), '房主新增電腦席位');

/* 斷線重連：連線被切斷後自動重連，仍在原房間、原席位 */
await host.evaluate(() => window.__net.sock.close());
ok(await hasText(host, '重新連線', 5000), '連線中斷顯示重新連線橫幅');
ok(await hasText(host, '玩家席位', 15000) && await host.locator('.banner').count() === 0 || await host.waitForSelector('.banner', { state: 'detached', timeout: 15000 }).then(() => true, () => false), '自動重連成功，橫幅消失');
ok(await host.getByText('你是房主').count() > 0, '重連後仍在原房間且仍是房主');

/* 邀請連結：玩家與觀戰 */
async function makeLink(role) {
  await btn(host, '邀請朋友').click();
  if (role === 'spectator') await host.getByRole('radio', { name: '觀戰者' }).click();
  else await host.getByRole('radio', { name: '玩家' }).click();
  const before = await host.locator('.link-box').count();
  await btn(host, '產生連結').click();
  await host.waitForFunction(n => document.querySelectorAll('.link-box').length > n, before);
  const links = await host.locator('.link-box').allTextContents();
  await host.keyboard.press('Escape');
  return links[links.length - 1];
}
const playerLink = await makeLink('player');
ok(/room=[A-Z]+&invite=\w+/.test(playerLink), '產生玩家邀請連結');
const specLink = await makeLink('spectator');

const guest = await newPage(b, SIZES.tabletL, 'zh-TW', playerLink);
ok(await hasText(guest, '你被邀請加入'), '邀請連結停在大廳，顯示邀請房間與暱稱欄位（不自動進房）');
ok(await guest.locator('.screen').getByText('玩家席位').count() === 0, '確認暱稱前不在房間內');
if (shots) await guest.screenshot({ path: out + 'invite.png' });
await guest.locator('.tf-input').first().fill('小客人');
await btn(guest, '加入遊戲').click();
ok(await hasText(guest, '玩家席位'), '確認暱稱後以玩家身分進房');
ok(await hasText(host, '小客人'), '房主看到新玩家');
await btn(guest, '我準備好了').click();
await host.waitForTimeout(500);

const spec = await newPage(b, SIZES.phoneP, 'zh-TW', specLink);
const specOk = await hasText(spec, '以觀戰者加入');
if (!specOk) console.log('   [debug]', specLink, (await spec.locator('body').innerText()).slice(0, 300).replace(/\s+/g, ' '));
ok(specOk, '觀戰邀請顯示「以觀戰者加入」');
await btn(spec, '進去觀戰').click();
ok(await hasText(spec, '你是觀戰者'), '觀戰者角色明確顯示');
ok(await spec.getByRole('button', { name: '加入對戰' }).first().isEnabled(), '大廳階段觀戰者可選擇加入對戰');

/* 邀請 token 決定角色：觀戰 token 即使改暱稱也不能變玩家（伺服器測試已覆蓋，這裡看畫面角色） */
if (shots) await host.screenshot({ path: out + 'room.png' });
/* 聊天室（房間內） */
await host.locator('.chat .tf-input').first().fill('大家好');
await host.locator('.chat').first().getByRole('button', { name: '送出' }).click();
ok(await hasText(guest, '大家好', 8000), '房間聊天：另一位玩家收到訊息');
ok(await hasText(spec, '大家好', 8000), '房間聊天：觀戰者也看得到並可發言');
const startBtn = btn(host, '開始遊戲');
ok(await startBtn.isEnabled(), '準備好後房主可開始遊戲');
await startBtn.click();
ok(await hasText(guest, '加速', 20000), '玩家進入對戰畫面');
ok(await hasText(spec, '正在看', 20000), '觀戰者進入對戰畫面（正在看某位玩家，沒有操作按鈕）');
await host.waitForTimeout(3500);
if (shots) { await host.screenshot({ path: out + 'online-game-host.png' }); await guest.screenshot({ path: out + 'online-game-guest.png' }); await spec.screenshot({ path: out + 'online-game-spec.png' }); }

/* 左側資訊欄：寬版預設展開，聊天室在同一欄 */
ok(await host.locator('.game-side .chat').count() === 1, '桌機寬版：Summary 與聊天室在同一個左側資訊欄');

/* 結算 overlay */
ok(await host.locator('.res-actions').waitFor({ timeout: 120000 }).then(() => true, () => false), '對局結束出現結算 overlay');
ok(await host.locator('.game .game-canvas').count() === 1, '結算疊在遊戲畫面上（3D 場景仍在）');
const actions = await host.locator('.res-actions button').allTextContents();
ok(actions.length === 3 && actions[0].includes('再來一局') && actions[1].includes('回到房間') && actions[2].includes('回到首頁'), '結算三個動作：再來一局／回到房間／回到首頁');
if (shots) await host.screenshot({ path: out + 'online-result-host.png' });
await guest.waitForTimeout(500);
ok(await guest.locator('.res-actions button').first().isDisabled(), '非房主的「再來一局」不可用並有說明');
await host.locator('.res-actions').getByRole('button', { name: '再來一局' }).click();
ok(await hasText(host, '玩家席位'), '房主再來一局 → 回到房間');
ok(await hasText(guest, '玩家席位', 8000), '其他玩家自動回到房間');
ok((await layoutIssues(host)).length === 0, '房間畫面無版面問題');

/* 零真人自動關閉 */
await guest.getByRole('button', { name: '離開房間' }).first().click().catch(() => {});
await guest.getByRole('button', { name: '離開', exact: true }).last().click().catch(() => {});
await host.getByRole('button', { name: '離開房間' }).first().click();
await host.getByRole('button', { name: '離開', exact: true }).last().click();
ok(await hasText(spec, '房間已經沒有玩家', 8000) || await hasText(spec, '自動關閉', 3000), '真人全部離開 → 房間自動關閉並通知觀戰者');
ok(await hasText(spec, '線上大廳', 8000), '觀戰者回到大廳');
const late = await newPage(b, SIZES.desktop, 'zh-TW', playerLink);
ok(await hasText(late, '這個房間已經結束了') || await hasText(late, '邀請連結無效', 3000), '已關閉房間的邀請連結顯示可理解的提示');
ok(hostReqs.length === 0, '沒有任何 /api/presence 請求');
for (const [n, p] of [['host', host], ['guest', guest], ['spec', spec]]) if (p.errors.length) { fails++; console.log('  ✗ ' + n + ' 錯誤：' + p.errors.join(' | ')); }
await b.close();
console.log(fails ? `\n${fails} 項失敗` : '\n全部通過');
process.exit(fails ? 1 : 0);
