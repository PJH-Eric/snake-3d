/* 畫面矩陣：每個畫面 × 尺寸 × 語言，檢查版面問題並截圖（SHOT_DIR）。
 * 用法：node e2e/matrix.mjs [sizes,逗號分隔] [langs,逗號分隔] */
import { launch, newPage, layoutIssues, SIZES, SERVER, APP } from './lib.mjs';

const sizeNames = (process.argv[2] || Object.keys(SIZES).join(',')).split(',');
const langs = (process.argv[3] || 'zh-TW,en').split(',');
const out = (process.env.SHOT_DIR || '.shots') + '/';
const only = process.env.ONLY ? process.env.ONLY.split(',') : null;
const b = await launch();
let failed = 0;

async function report(page, label, tag) {
  if (only && !only.some(o => label === o || label.startsWith(o + '-'))) return;
  const issues = await layoutIssues(page);
  const errs = page.errors.splice(0);
  if (only || process.env.SHOTS) await page.screenshot({ path: `${out}${tag}-${label}.png` });
  if (issues.length || errs.length) { failed++; console.log(`✗ ${tag} ${label}\n   ${[...issues, ...errs].join('\n   ')}`); } else console.log(`✓ ${tag} ${label}`);
}
const want = name => !only || only.includes(name);
const click = (page, name, opt = {}) => page.getByRole('button', { name, exact: opt.exact ?? false }).first().click();

for (const lang of langs) for (const sn of sizeNames) {
  const size = SIZES[sn], tag = `${lang}-${sn}`;
  const page = await newPage(b, size, lang);
  const zh = lang === 'zh-TW';
  if (true) await report(page, 'home', tag);
  if (true) {
    await page.getByRole('button', { name: zh ? '設定' : 'Settings' }).click(); await page.waitForTimeout(250);
    await report(page, 'settings', tag);
    await page.keyboard.press('Escape'); await page.waitForTimeout(150);
  }
  if (true) { await click(page, zh ? '怎麼玩' : 'How'); await page.waitForTimeout(200); await report(page, 'help', tag); await page.getByRole('button', { name: zh ? '回首頁' : 'Home' }).last().click(); }
  if (true) { await click(page, zh ? '單人挑戰' : 'Solo challenge', { exact: false }); await page.waitForTimeout(200); await report(page, 'challenge', tag); await page.getByRole('button', { name: zh ? '返回' : 'Back' }).first().click(); }
  if (true) { await click(page, zh ? '對戰電腦' : 'Vs'); await page.waitForTimeout(200); await report(page, 'solo', tag); await click(page, zh ? '逐一調整難度' : 'Set each one'); await page.waitForTimeout(150); await report(page, 'solo-detail', tag);
    if (true) { await click(page, zh ? '開始遊戲' : 'Start'); await page.waitForTimeout(1500); await report(page, 'game-countdown', tag); await page.waitForTimeout(4500); await report(page, 'game-play', tag);
      await page.keyboard.press('Escape'); await page.waitForTimeout(250); await report(page, 'pause', tag); await page.keyboard.press('Escape'); await page.waitForTimeout(150);
      const info = page.getByRole('button', { name: zh ? '展開資訊欄與聊天室' : 'Open info panel' }).first();
      if (await info.count() && await info.isVisible()) { await info.click(); await page.waitForTimeout(300); await report(page, 'game-side', tag); }
    } }
  await page.context().close();
}
await b.close();
console.log(failed ? `\n${failed} 組有問題` : '\n全部通過');
process.exit(failed ? 1 : 0);
