/* 把一批截圖拼成一張總覽圖（方便一次檢視多個畫面）。
 * 用法：node e2e/sheet.mjs <截圖資料夾> <輸出.png> <檔名關鍵字（逗號分隔，需全部符合）> [單張寬度] */
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { launch } from './lib.mjs';

const [dir, outFile, filter = '', widthArg = '300'] = process.argv.slice(2);
const keys = filter.split(',').filter(Boolean);
const files = fs.readdirSync(dir).filter(f => f.endsWith('.png') && keys.every(k => f.includes(k))).sort();
const w = Number(widthArg);
const html = `<body style="margin:0;background:#222;font:12px sans-serif;color:#fff"><div style="display:flex;flex-wrap:wrap;gap:6px;padding:6px;align-items:flex-start">` +
  files.map(f => `<figure style="margin:0;width:${w}px"><img src="${pathToFileURL(path.resolve(dir, f)).href}" style="width:${w}px;display:block;border:1px solid #888"><figcaption>${f.replace('.png', '')}</figcaption></figure>`).join('') + '</div></body>';
const tmp = path.join(dir, '_sheet.html');
fs.writeFileSync(tmp, html);
const b = await launch();
const p = await b.newPage({ viewport: { width: 1600, height: 900 } });
await p.goto(pathToFileURL(tmp).href);
await p.waitForTimeout(500);
await p.screenshot({ path: outFile, fullPage: true });
await b.close();
fs.unlinkSync(tmp);
console.log(files.length + ' 張 → ' + outFile);
