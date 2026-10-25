'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// 執行實際 jslib 的尺寸橋接函式；不模擬 Unity 或宣稱畫面已渲染。
let width = 390;
const context = {
  Module: { canvas: { getBoundingClientRect: () => ({ width }) } },
  autoAddDeps() {},
  mergeInto() {},
  LibraryManager: { library: {} },
};
vm.createContext(context);
vm.runInContext(fs.readFileSync(path.join(__dirname, '../../Assets/Plugins/WebGL/SnakeBridge.jslib'), 'utf8'), context);
for (width of [320, 390, 844, 768, 1024, 1440]) {
  assert.equal(context.SnakeBridge.SnakeCanvasCssWidth(), width);
}
width = 0;
assert.equal(context.SnakeBridge.SnakeCanvasCssWidth(), 1);
console.log('通過：WebGL 橋接讀取 canvas CSS 寬度與零寬防護（未執行 Unity）');
