/* Node 26 內建的 localStorage 在未提供 --localstorage-file 時為 undefined，會蓋掉 jsdom 的；改回使用 jsdom 的實作。 */
Object.defineProperty(globalThis, 'localStorage', {
  configurable: true,
  value: globalThis.jsdom.window.localStorage,
});
