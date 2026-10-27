/* 連續重開場次的記憶體檢查：JS heap 與 WebGL 資源是否隨場次成長。 */
import { launch, newPage, SIZES, APP } from './lib.mjs';
const b = await launch();
const page = await newPage(b, SIZES.desktop, 'zh-Hant', APP + '?e2e=1');
const rounds = Number(process.argv[2] || 12);
await page.getByRole('button', { name: '單人挑戰' }).first().click();
await page.getByRole('button', { name: '開始遊戲' }).first().click();
for (let i = 0; i < rounds; i++) {
  await page.waitForFunction(() => window.__snake3d && window.__snake3d.session && window.__snake3d.session.state.phase === 'play', null, { timeout: 20000 });
  const info = await page.evaluate(() => {
    const r = window.__snake3d.session.world.renderer.info;
    return { heap: Math.round(performance.memory.usedJSHeapSize / 1e6), geo: r.memory.geometries, tex: r.memory.textures, prog: r.programs ? r.programs.length : 0 };
  });
  console.log(i, JSON.stringify(info));
  await page.keyboard.press('Escape');
  await page.getByRole('dialog').getByRole('button', { name: '重新開始' }).click();
  await page.waitForTimeout(300);
}
console.log('errors:', page.errors.join('|') || 'none');
await b.close();
