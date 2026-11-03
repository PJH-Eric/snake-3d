/* 房間 RWD：真實建立房間、滿席、規則與聊天；離線電腦房的角色、七名對手及設定保留。 */
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { launch, newPage, layoutIssues, SIZES, APP, SERVER } from './lib.mjs';

const dicts = JSON.parse(await readFile(new URL('../../shared/locales.json', import.meta.url), 'utf8'));
const sizes = { narrow: { width: 320, height: 568, hasTouch: true, isMobile: true }, ...SIZES };
const browser = await launch();
const shots = process.env.SHOTS;
const out = process.env.SHOT_DIR || '.shots';

async function inspect(page, tag) {
  assert.deepEqual(await layoutIssues(page), [], tag);
  assert.deepEqual(page.errors, [], `${tag}: browser errors`);
  if (shots) await page.screenshot({ path: `${out}/${tag}.png` });
  console.log(`✓ ${tag}`);
}

async function toggle(page, title) {
  const button = page.getByRole('button', { name: title, exact: true });
  const before = await button.getAttribute('aria-expanded');
  await button.scrollIntoViewIfNeeded();
  await button.focus();
  await page.keyboard.press('Enter');
  assert.equal(await button.getAttribute('aria-expanded'), before === 'true' ? 'false' : 'true');
}

try {
  for (const lang of (process.argv[2] || 'zh-Hant,zh-Hans,en').split(',')) {
    const t = key => dicts[lang][key];
    for (const [name, size] of Object.entries(sizes)) {
      const tag = `${lang}-${name}`;
      const page = await newPage(browser, size, lang, APP + '?e2e=1&server=' + encodeURIComponent(SERVER));
      const requests = [];
      page.on('websocket', ws => requests.push(ws.url()));
      page.on('request', request => { if (request.url().startsWith(SERVER)) requests.push(request.url()); });
      await page.getByRole('button', { name: t('menu.solo'), exact: true }).click();
      const start = page.locator('.room-launch .btn');
      assert(await start.isVisible());
      const box = await start.boundingBox();
      assert(box.y + box.height <= size.height, `${tag}: start is in the first viewport`);
      await inspect(page, `solo-${tag}`);

      // 設定角色不需要擠在首屏；展開、輸入、收合再展開不能遺失暱稱。
      await toggle(page, t('ch.you'));
      await page.locator('.tf-input').fill('LongNickname');
      await toggle(page, t('ch.you'));
      await toggle(page, t('ch.you'));
      assert.equal(await page.locator('.tf-input').inputValue(), 'LongNickname');
      await inspect(page, `solo-profile-${tag}`);
      await toggle(page, t('ch.you'));

      for (let i = 0; i < 4; i++) {
        await page.getByRole('button', { name: t('ui.inc').replace('{label}', t('solo.num')), exact: true }).click();
      }
      assert.equal(await page.locator('.stepper-val').innerText(), '7');
      await page.getByRole('button', { name: t('solo.detail'), exact: true }).click();
      assert.equal(await page.locator('.opp').count(), 7);
      await page.locator('.opp').last().getByRole('radio').last().click();
      await inspect(page, `solo-seven-opponents-${tag}`);
      await toggle(page, t('ch.rules'));
      await page.getByRole('radiogroup', { name: t('rule.speed'), exact: true }).getByRole('radio').last().click();
      await page.getByRole('combobox', { name: t('rule.theme'), exact: true }).click();
      await inspect(page, `solo-theme-${tag}`);
      await page.keyboard.press('Escape');
      await toggle(page, t('ch.rules'));
      await toggle(page, t('ch.rules'));
      assert.equal(await page.getByRole('radiogroup', { name: t('rule.speed'), exact: true }).getByRole('radio').last().getAttribute('aria-checked'), 'true');
      assert.deepEqual(requests, [], `${tag}: solo stays offline`);

      await page.getByRole('button', { name: t('common.back'), exact: true }).click();
      await page.getByRole('button', { name: t('menu.online'), exact: true }).click();
      await page.getByRole('button', { name: t('lobby.create'), exact: true }).click();
      await page.getByRole('dialog').getByRole('button', { name: t('mk.ok'), exact: true }).click();
      await page.locator('.room-seats').waitFor();
      assert(await page.locator('.room-launch .btn').isDisabled());
      await inspect(page, `room-${tag}`);
      await page.getByRole('button', { name: t('seat.addAi'), exact: true }).first().click();
      await page.waitForFunction(() => !document.querySelector('.room-launch .btn').disabled);
      await toggle(page, t('room.rules'));
      await inspect(page, `room-rules-${tag}`);
      for (let i = 0; i < 4; i++) {
        const before = await page.locator('.seat').count();
        await page.getByRole('button', { name: t('ui.inc').replace('{label}', t('room.max')), exact: true }).click();
        await page.waitForFunction(n => document.querySelectorAll('.seat').length === n + 1, before);
      }
      await page.waitForFunction(() => document.querySelectorAll('.seat').length === 8);
      await toggle(page, t('room.rules'));
      while (await page.getByRole('button', { name: t('seat.addAi'), exact: true }).count()) {
        const before = await page.locator('.seat.empty').count();
        await page.getByRole('button', { name: t('seat.addAi'), exact: true }).first().click();
        await page.waitForFunction(n => document.querySelectorAll('.seat.empty').length < n, before);
      }
      // 難度選單獨占一列，不能被頭像及刪除鈕擠到單字斷行。
      const selectWidth = await page.locator('.seat > .select').last().evaluate(el => el.getBoundingClientRect().width);
      assert(selectWidth >= 200, `${tag}: CPU difficulty has room for a readable label`);
      await page.locator('.seat').last().getByRole('combobox').click();
      await inspect(page, `room-eight-seats-${tag}`);
      await page.keyboard.press('Escape');
      const chat = page.locator('.room-chat');
      const message = 'Room layout test ' + '長訊息測試'.repeat(8);
      await chat.locator('.tf-input').fill(message);
      await chat.getByRole('button', { name: t('chat.send'), exact: true }).click();
      await chat.getByText(message, { exact: false }).waitFor();
      await inspect(page, `room-chat-${tag}`);
      await page.getByRole('button', { name: t('room.invite'), exact: true }).click();
      await inspect(page, `room-invite-${tag}`);
      await page.keyboard.press('Escape');
      await page.getByRole('button', { name: t('room.leave'), exact: true }).first().click();
      await page.getByRole('dialog').getByRole('button', { name: t('leave.ok'), exact: true }).click();
      await page.locator('.room-seats').waitFor({ state: 'detached' });
      await page.context().close();
    }
  }
} finally {
  await browser.close();
}
