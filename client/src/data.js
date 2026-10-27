/* 動物角色與舞台主題（顏色與形狀數值沿用原設計）。 */
export const ANIMALS = {
  cat: { ear: 'point', body: '#ffb86b', dark: '#e88f3b', belly: '#fff0da', earIn: '#ff9db4', nose: '#ff7f9f', seg0: '#ffb259', seg1: '#f08d2a', tail: '#e07a1f', tailScale: 0.8 },
  dog: { ear: 'floppy', body: '#e8b87c', dark: '#c58b4d', belly: '#fff4e2', earCol: '#a8693a', nose: '#3a2a2a', seg0: '#e8b87c', seg1: '#b98650', tail: '#fff4e2', tailScale: 0.9, muzzle: '#fff4e2' },
  bunny: { ear: 'long', body: '#fff7fb', dark: '#e6cfe0', belly: '#ffe3ef', earIn: '#ffb3d1', nose: '#ff8aa5', seg0: '#fffafd', seg1: '#ffc9de', tail: '#ffffff', tailScale: 1.5 },
  bear: { ear: 'round', body: '#b9824f', dark: '#936235', belly: '#f4dab6', earIn: '#ebc496', nose: '#3a2a2a', seg0: '#b9824f', seg1: '#8b5a30', tail: '#8b5a30', tailScale: 0.9, muzzle: '#f4dab6' },
  panda: { ear: 'round', body: '#ffffff', dark: '#d9d9e0', belly: '#ffffff', earCol: '#2d2d36', nose: '#2d2d36', seg0: '#ffffff', seg1: '#3b3b46', tail: '#3b3b46', tailScale: 1, patches: true },
  fox: { ear: 'point', body: '#ff8c3f', dark: '#dd6420', belly: '#fff3e6', earIn: '#3a2a2a', nose: '#3a2a2a', seg0: '#ff8c3f', seg1: '#fff1de', tail: '#ffffff', tailScale: 1.4 },
  frog: { ear: 'none', body: '#72d56c', dark: '#43b04a', belly: '#e0f8b0', seg0: '#72d56c', seg1: '#b8ec70', tail: '#43b04a', tailScale: 0.7, eyesTop: true },
  penguin: { ear: 'none', body: '#465176', dark: '#2c3452', belly: '#ffffff', seg0: '#4a5a8c', seg1: '#ffffff', tail: '#2c3452', tailScale: 0.8, beak: '#ffb43b' }
};
export const animalDef = id => ANIMALS[id] || ANIMALS.cat;

export const THEMES = [
  { floorA: '#b9e47f', floorB: '#a7d86a', wall: '#b9803f', wallTop: '#d9a25f', rock0: '#a7aeb5', rock1: '#8a929a', outer: '#7fc25f', sky: '#cdeeff', li: 1.0, lc: '#fff4d6', h0: '#ffffff', h1: '#9fcf7a', crown: '#4cc26a' },
  { floorA: '#ffe2ee', floorB: '#ffcfe2', wall: '#ff7fb0', wallTop: '#ffb1cf', rock0: '#ffd23d', rock1: '#ff9f2e', outer: '#ffc2dc', sky: '#ffe3f1', li: 1.0, lc: '#fff0f6', h0: '#fff6fb', h1: '#ffb7d6', crown: '#ff9fc4' },
  { floorA: '#fbeab4', floorB: '#f3da98', wall: '#3fb6c9', wallTop: '#7fdcec', rock0: '#f0a98a', rock1: '#d98462', outer: '#f0d18a', sky: '#bfeaff', li: 1.1, lc: '#fff1c8', h0: '#ffffff', h1: '#f4d894', crown: '#6fc96a' },
  { floorA: '#f7fdff', floorB: '#dfeff7', wall: '#8fd0ee', wallTop: '#c9ecfa', rock0: '#ffffff', rock1: '#cfe0ea', outer: '#d6e8f1', sky: '#d9eef9', li: 0.95, lc: '#f2f8ff', h0: '#ffffff', h1: '#bcd6e6', crown: '#e9f6ff' },
  { floorA: '#3f6a86', floorB: '#375f7a', wall: '#7a5638', wallTop: '#a47a52', rock0: '#6b78a0', rock1: '#525d82', outer: '#223d58', sky: '#222a55', li: 0.75, lc: '#cfd8ff', h0: '#8fa0ff', h1: '#2a3f5e', night: true, crown: '#2f6b6f' },
  { floorA: '#dcf0b4', floorB: '#cbe59c', wall: '#7bb85b', wallTop: '#a6d985', rock0: '#b4ad92', rock1: '#938c72', outer: '#a9d68c', sky: '#e3f4cf', li: 1.0, lc: '#fff8dc', h0: '#ffffff', h1: '#a9d68c', crown: '#5fb868' },
  /* 6 沙漠綠洲 */
  { floorA: '#f4cf93', floorB: '#ecbf7c', wall: '#b5651d', wallTop: '#dc9250', rock0: '#c98a5e', rock1: '#a96c44', outer: '#e6b46f', sky: '#ffe3b8', li: 1.1, lc: '#fff0cf', h0: '#ffffff', h1: '#efc388', crown: '#6aae5e' },
  /* 7 楓紅秋林 */
  { floorA: '#e8b863', floorB: '#dca74f', wall: '#8b4f2a', wallTop: '#b9784a', rock0: '#a59b8c', rock1: '#857b6c', outer: '#c98b46', sky: '#ffe2bd', li: 1.0, lc: '#ffe8c4', h0: '#fff4e0', h1: '#d9a45a', crown: '#e2742d' },
  /* 8 珊瑚海底 */
  { floorA: '#58bfd9', floorB: '#49b0cd', wall: '#ff8f7a', wallTop: '#ffbfae', rock0: '#ff9fb3', rock1: '#e8728d', outer: '#2f8fb3', sky: '#8fd6f2', li: 1.0, lc: '#e9fbff', h0: '#e9fbff', h1: '#3b9fc4', crown: '#43b98f' },
  /* 9 星空宇宙 */
  { floorA: '#463a80', floorB: '#3d336f', wall: '#8a7bd9', wallTop: '#b6a9f2', rock0: '#7d7a99', rock1: '#5f5d7c', outer: '#251c4d', sky: '#150f33', li: 0.8, lc: '#dcd4ff', h0: '#a89cff', h1: '#2e2560', night: true, crown: '#7a5cff' },
  /* 10 櫻花公園 */
  { floorA: '#fde6ec', floorB: '#f8d5e0', wall: '#c98a9a', wallTop: '#e6b3c1', rock0: '#c9b9bf', rock1: '#a8979e', outer: '#f3c4d3', sky: '#ffe9f0', li: 1.0, lc: '#fff2f6', h0: '#fff9fb', h1: '#f3bccd', crown: '#ffa8c8' },
  /* 11 火山熔岩 */
  { floorA: '#6b4a47', floorB: '#5e403d', wall: '#2e2222', wallTop: '#574141', rock0: '#ff7a45', rock1: '#c4482a', outer: '#3a2626', sky: '#2a1717', li: 0.85, lc: '#ffd0b0', h0: '#ff9f6b', h1: '#4a2c2a', night: true, crown: '#8a3b2c' }
];

/* 舞台選單用的選項：-1 為隨機，其餘為各主題編號 */
import Rules from '@shared/rules.js';
export const THEME_OPTIONS = [-1, ...Array.from({ length: Rules.THEME_COUNT }, (_, i) => i)];
export const theme = id => THEMES[((id % THEMES.length) + THEMES.length) % THEMES.length];

/* 一般果實的 8 種外觀（順序對應 shared/rules.js 的 FRUIT_KINDS）：蘋果、橘子、梨子、蜜桃、檸檬、李子、櫻桃、藍莓 */
export const FRUIT_BODY = ['#ff5a52', '#ffa63d', '#a9dc5a', '#ffa9a0', '#ffe14d', '#8a5cc2', '#c4263d', '#4a6fd8'];
export const FRUIT_LEAF = '#4cc26a';
export const INK = '#4a3320';
export const CREAM = '#fff7e3';
export const SLOT_COLORS = ['#ff7a45', '#3fa7f5', '#ff6f9f', '#4cc26a', '#a58bff', '#ffc233', '#2ec7c0', '#e0584d'];
export const slotColor = slot => SLOT_COLORS[((slot % 8) + 8) % 8];
