/* 真實 CDP 觸控：舊隱藏設定遷移、手指指向、固定底部搖桿、取消與設定切換。 */
import assert from 'node:assert/strict';
import { launch, newPage, layoutIssues, fingerPoint, SIZES, APP } from './lib.mjs';

const requested = (process.argv[2] || 'phoneP,phoneL,tabletP,tabletL,desktop').split(',');
const sizes = requested.filter(name => name !== 'desktop');
const browser = await launch();
const touch = (cdp, type, points) => cdp.send('Input.dispatchTouchEvent', {
  type, touchPoints: points.map(([x, y], i) => ({ x, y, id: i + 1 }))
});
const angle = page => page.evaluate(() => window.__snake3d.session.state.players[0].ta);
const aim = (page, value) => page.evaluate(a => window.__snake3d.session.aim(a), value);
const closeSettings = page => page.keyboard.press('Escape');
const shot = async (page, tag) => {
  if (process.env.SHOTS) await page.screenshot({ path: `${process.env.SHOT_DIR || '.shots'}/${tag}.png` });
};

try {
  for (const sizeName of sizes) {
    const page = await newPage(browser, SIZES[sizeName], 'zh-Hant', APP + '?e2e=1');
    await page.evaluate(() => localStorage.setItem('snake3d.save.v1', JSON.stringify({ quality: 'low', touchMode: 'off', stickMode: 'fixed' })));
    await page.reload();
    await page.getByRole('button', { name: '設定', exact: true }).click();
    assert.equal(await page.getByRole('radio', { name: '依手指', exact: true }).getAttribute('aria-checked'), 'true', '舊隱藏設定遷移為依手指');
    assert.equal(await page.getByRole('radio', { name: '隱藏', exact: true }).count(), 0, '不再提供會停用輸入的隱藏選項');
    await closeSettings(page);
    await page.getByRole('button', { name: '單人挑戰', exact: true }).click();
    await page.getByRole('button', { name: '開始遊戲', exact: true }).click();
    try {
      await page.waitForFunction(() => window.__snake3d?.session.state.phase === 'play');
    } catch (error) {
      console.log('遊戲未進入可操作狀態', await page.evaluate(() => ({
        phase: window.__snake3d?.session.state.phase,
        time: window.__snake3d?.session.state.time,
        modal: !!document.querySelector('.modal')
      })), page.errors);
      await page.screenshot({ path: `${process.env.SHOT_DIR || '.shots'}/control-${sizeName}-failure.png` });
      throw error;
    }
    await page.evaluate(() => { window.__snake3d.session.paused = () => true; });
    const cdp = await page.context().newCDPSession(page);
    assert.equal(await page.locator('.stick').isVisible(), false, '手指模式不顯示搖桿');
    assert.equal(await page.locator('.boost').isVisible(), true, '手指模式仍可加速');

    for (const [dx, dz, expected] of [[4, 0, 0], [0, 4, Math.PI / 2], [0, -4, -Math.PI / 2]]) {
      await aim(page, Math.PI / 4);
      const point = await fingerPoint(page, dx, dz);
      await touch(cdp, 'touchStart', [point]);
      assert.ok(Math.abs(await angle(page) - expected) < 0.12, `${sizeName}: 按下即朝手指位置 ${expected} 轉向`);
      assert.equal(await page.locator('.stick').isVisible(), false, '操作中不出現搖桿');
      const next = await fingerPoint(page, -4, 0);
      await touch(cdp, 'touchMove', [next]);
      assert.ok(Math.abs(Math.abs(await angle(page)) - Math.PI) < 0.12, '移動後朝新位置轉向');
      await touch(cdp, 'touchEnd', []);
    }
    const point = await fingerPoint(page, 4, 0);
    await touch(cdp, 'touchStart', [point]);
    await touch(cdp, 'touchCancel', []);
    const up = await fingerPoint(page, 0, -4);
    await touch(cdp, 'touchStart', [up]);
    assert.ok(Math.abs(await angle(page) + Math.PI / 2) < 0.12, '取消後可重新操作');
    await touch(cdp, 'touchEnd', []);
    await shot(page, `control-${sizeName}-finger`);

    await touch(cdp, 'touchStart', [point]);
    await page.getByRole('button', { name: '設定', exact: true }).click();
    await aim(page, Math.PI / 4);
    await touch(cdp, 'touchMove', [up]);
    await touch(cdp, 'touchEnd', []);
    assert.ok(Math.abs(await angle(page) - Math.PI / 4) < 0.05, '彈窗阻擋背景轉向');
    await page.getByRole('radio', { name: '依搖桿', exact: true }).click();
    await closeSettings(page);
    for (const side of ['left', 'right']) {
      if (side === 'right') {
        await page.getByRole('button', { name: '設定', exact: true }).click();
        await page.getByRole('radio', { name: '右手邊', exact: true }).click();
        await page.locator('.modal .sa-vp').evaluate(el => { el.scrollTop = el.scrollHeight; });
        await shot(page, `control-${sizeName}-settings`);
        await closeSettings(page);
      }
      const box = await page.locator('.stick').boundingBox();
      const main = await page.locator('.game-main').boundingBox();
      const bottomGap = main.y + main.height - box.y - box.height;
      assert.ok(box && bottomGap >= 12 && bottomGap <= 30, '搖桿固定在底部安全距離');
      assert.ok(side === 'left' ? box.x < main.x + main.width / 2 : box.x > main.x + main.width / 2, '搖桿左右位置正確');
      await aim(page, Math.PI / 4);
      await touch(cdp, 'touchStart', [point]);
      await touch(cdp, 'touchEnd', []);
      assert.ok(Math.abs(await angle(page) - Math.PI / 4) < 0.05, '搖桿模式忽略搖桿區域外的點擊');
      const center = [box.x + box.width / 2, box.y + box.height / 2];
      await touch(cdp, 'touchStart', [center]);
      await touch(cdp, 'touchMove', [[center[0] + 55, center[1]]]);
      assert.ok(Math.abs(await angle(page)) < 0.05, '底部搖桿往右拖可轉向');
      await shot(page, `control-${sizeName}-joystick-${side}`);
      await touch(cdp, 'touchCancel', []);
      assert.equal(await page.locator('.stick-knob').evaluate(el => el.style.transform), 'translate(0px, 0px)', '取消後搖桿回中');
    }
    assert.deepEqual(await layoutIssues(page), [], '遊戲版面沒有溢出或重疊');
    assert.deepEqual(page.errors, [], '沒有瀏覽器錯誤');
    await page.reload();
    await page.getByRole('button', { name: '設定', exact: true }).click();
    assert.equal(await page.getByRole('radio', { name: '依搖桿', exact: true }).getAttribute('aria-checked'), 'true', '操作方式保存');
    assert.equal(await page.getByRole('radio', { name: '右手邊', exact: true }).getAttribute('aria-checked'), 'true', '底部左右位置保存');
    console.log(`✓ ${sizeName}: 手指指向、底部左右搖桿、取消、彈窗隔離、舊設定遷移與保存`);
    await page.context().close();
  }
  if (requested.includes('desktop')) {
    const page = await newPage(browser, SIZES.desktop, 'zh-Hant', APP + '?e2e=1');
    await page.evaluate(() => localStorage.setItem('snake3d.save.v1', JSON.stringify({ quality: 'low', controlMode: 'joystick' })));
    await page.reload();
    await page.getByRole('button', { name: '單人挑戰', exact: true }).click();
    await page.getByRole('button', { name: '開始遊戲', exact: true }).click();
    await page.waitForFunction(() => window.__snake3d?.session.state.phase === 'play');
    await page.evaluate(() => { window.__snake3d.session.paused = () => true; });
    const box = await page.locator('.stick').boundingBox();
    await aim(page, Math.PI / 4);
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width / 2 + 55, box.y + box.height / 2);
    assert.ok(Math.abs(await angle(page)) < 0.05, '桌機可用滑鼠拖曳底部搖桿');
    await page.mouse.up();
    await page.keyboard.down('ArrowUp');
    await page.waitForTimeout(150);
    await page.keyboard.up('ArrowUp');
    assert.ok(Math.abs(await angle(page) + Math.PI / 2) < 0.05, '搖桿模式仍支援鍵盤');
    await page.getByRole('button', { name: '設定', exact: true }).click();
    await page.getByRole('radio', { name: '依手指', exact: true }).click();
    await closeSettings(page);
    assert.equal(await page.locator('.stick').isVisible(), false, '桌機依手指模式不顯示搖桿');
    await aim(page, Math.PI / 4);
    const point = await fingerPoint(page, 4, 0);
    await page.mouse.click(...point);
    assert.ok(Math.abs(await angle(page) - Math.PI / 4) < 0.05, '依手指模式不把滑鼠誤判為手指');
    assert.deepEqual(await layoutIssues(page), [], '桌機版面正常');
    assert.deepEqual(page.errors, [], '桌機沒有瀏覽器錯誤');
    console.log('✓ desktop: 底部搖桿滑鼠操作、鍵盤轉向與手指模式的滑鼠隔離');
    await page.context().close();
  }
} finally {
  await browser.close();
}
