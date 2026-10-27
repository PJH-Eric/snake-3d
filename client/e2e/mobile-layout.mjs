/* 手機窄版／橫向版面：底部控制分離、精簡 HUD、設定觸控大小與捲動可達性。 */
import { launch, newPage, layoutIssues, APP } from './lib.mjs';

const sizes = {
  narrow: { width: 320, height: 568, hasTouch: true, isMobile: true },
  phone: { width: 390, height: 844, hasTouch: true, isMobile: true },
  landscape: { width: 844, height: 390, hasTouch: true, isMobile: true },
  short: { width: 568, height: 320, hasTouch: true, isMobile: true }
};
const selected = (process.argv[2] || Object.keys(sizes).join(',')).split(',');
const intersects = (a, b) => a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y;
let failures = 0;
const check = (ok, message) => { console.log(`${ok ? '✓' : '✗'} ${message}`); if (!ok) failures++; };
const shot = async (page, name) => {
  if (process.env.SHOTS) await page.screenshot({ path: `${process.env.SHOT_DIR || '.shots'}/mobile-${name}.png` });
};
const browser = await launch();
try {
  for (const name of selected) {
    const size = sizes[name];
    const page = await newPage(browser, size, 'zh-Hant', APP + '?e2e=1');
    await page.evaluate(() => localStorage.setItem('snake3d.save.v1', JSON.stringify({
      controlMode: 'joystick', quality: 'low', nickname: '手機長暱稱測試二十個字也不能遮擋遊戲畫面'
    })));
    await page.reload();
    await page.getByRole('button', { name: '對戰電腦', exact: true }).click();
    await page.getByRole('button', { name: '開始遊戲', exact: true }).click();
    await page.waitForFunction(() => window.__snake3d?.session.state.phase === 'play');
    await page.evaluate(() => { window.__snake3d.session.paused = () => true; });
    await page.locator('.count').waitFor({ state: 'hidden' });
    const stick = await page.locator('.stick').boundingBox();
    const boost = await page.locator('.boost').boundingBox();
    check(!intersects(stick, boost), `${name}: 搖桿與加速鈕分離`);
    await page.mouse.move(stick.x + stick.width / 2, stick.y + stick.height / 2);
    await page.mouse.down();
    await page.mouse.move(stick.x + stick.width / 2 + 70, stick.y + stick.height / 2);
    const knob = await page.locator('.stick-knob').boundingBox();
    check(!intersects(knob, boost), `${name}: 搖桿推到底也不碰到加速鈕`);
    await page.mouse.up();
    const hud = await page.locator('.hud-top').boundingBox();
    check(hud.height <= 100, `${name}: 長暱稱 HUD 不超過兩列（${Math.round(hud.height)}px）`);
    if (size.width > size.height) {
      const map = await page.locator('.minimap').boundingBox();
      check(map.width <= 80, `${name}: 短橫向小地圖不占過多高度（${map.width}px）`);
    }
    check((await layoutIssues(page)).length === 0, `${name}: 對局無溢出或工具列重疊`);
    await shot(page, `${name}-game`);
    if (name === 'landscape') {
      await page.evaluate(() => {
        const root = document.documentElement.style;
        root.setProperty('--safe-l', '24px'); root.setProperty('--safe-r', '24px');
        root.setProperty('--safe-t', '8px'); root.setProperty('--safe-b', '20px');
      });
      const left = await page.locator('.hud-tl').boundingBox();
      const stickSafe = await page.locator('.stick').boundingBox();
      const boostSafe = await page.locator('.boost').boundingBox();
      check(left.x >= 24 && stickSafe.x >= 24 && boostSafe.x + boostSafe.width <= size.width - 24,
        'landscape: 工具列與控制避開左右安全區');
      check(stickSafe.y + stickSafe.height <= size.height - 20 && boostSafe.y + boostSafe.height <= size.height - 20,
        'landscape: 控制避開底部安全區');
      check((await layoutIssues(page)).length === 0, 'landscape: 模擬安全區後沒有版面問題');
      await shot(page, `${name}-safe-area`);
    }
    await page.getByRole('button', { name: '設定', exact: true }).click();
    await layoutIssues(page); // 等待彈窗進場動畫結束，再量測實際點選高度。
    const slider = page.getByRole('slider', { name: '音樂音量', exact: true });
    const row = slider.locator('..');
    const positions = await row.evaluate(el => {
      const label = el.querySelector('.field-label').getBoundingClientRect();
      const slider = el.querySelector('.slider').getBoundingClientRect();
      return Math.abs(label.y + label.height / 2 - slider.y - slider.height / 2);
    });
    check(positions < 3, `${name}: 音量標籤與滑桿在同一列`);
    const radios = await page.getByRole('radio').evaluateAll(nodes => nodes.every(el => el.getBoundingClientRect().height >= 48));
    check(radios, `${name}: 設定選項點選高度至少 48px`);
    await page.locator('.modal .sa-vp').evaluate(el => { el.scrollTop = el.scrollHeight; });
    check(await page.getByRole('button', { name: '完成', exact: true }).isVisible(), `${name}: 設定完成按鈕可見`);
    check((await layoutIssues(page)).length === 0, `${name}: 設定無溢出`);
    await shot(page, `${name}-settings`);
    await page.getByRole('button', { name: '完成', exact: true }).click();
    check(page.errors.length === 0, `${name}: 無瀏覽器錯誤 ${page.errors.join(' | ')}`);
    await page.context().close();
  }
} finally {
  await browser.close();
}
process.exitCode = failures ? 1 : 0;
