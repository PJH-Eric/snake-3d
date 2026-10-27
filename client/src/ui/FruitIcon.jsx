/* 12 種一般果實的圖示（順序對應 data.js 的 FRUIT_BODY）：草莓、鳳梨、芒果、西瓜、水蜜桃、葡萄、橘子、蘋果、櫻桃、香蕉、芭樂、蓮霧 */
import { FRUIT_BODY, FRUIT_LEAF, INK } from '../data.js';

const LEAF = { fill: FRUIT_LEAF, stroke: INK, strokeWidth: 1.6, strokeLinejoin: 'round' };

export function FruitIcon({ kind, size = 32 }) {
  const c = FRUIT_BODY[kind];
  const body = { fill: c, stroke: INK, strokeWidth: 2, strokeLinejoin: 'round', strokeLinecap: 'round' };
  const dot = { fill: INK };
  let g;
  switch (kind) {
    case 0: /* 草莓 */
      g = <>
        <path d="M16 29C8 24 4 17 6 12c2-4 7-4 10-2 3-2 8-2 10 2 2 5-2 12-10 17z" {...body} />
        <path d="M9 10l4 2 3-4 3 4 4-2-3-4-4 1-3-3-3 3-4-1z" {...LEAF} />
        <circle cx="12" cy="17" r="1" {...dot} /><circle cx="19" cy="16" r="1" {...dot} /><circle cx="16" cy="21" r="1" {...dot} /><circle cx="13" cy="23" r="0.9" {...dot} /><circle cx="20" cy="21.5" r="0.9" {...dot} />
      </>;
      break;
    case 1: /* 鳳梨 */
      g = <>
        <path d="M16 11l-5-8 4 3 1-4 1 4 4-3z" {...LEAF} />
        <ellipse cx="16" cy="20" rx="8" ry="10" {...body} />
        <path d="M10 14l12 12M22 14L10 26M8 20h16" stroke={INK} strokeWidth="1.2" fill="none" opacity="0.6" />
      </>;
      break;
    case 2: /* 芒果 */
      g = <>
        <path d="M8 22C5 15 10 7 19 8c7 1 9 8 6 14-3 6-14 8-17 0z" {...body} />
        <path d="M18 8c0-3 3-5 6-4-1 3-3 5-6 4z" {...LEAF} />
        <path d="M11 13c1-2 3-3 5-3" stroke="#fff" strokeWidth="1.8" fill="none" strokeLinecap="round" opacity="0.7" />
      </>;
      break;
    case 3: /* 西瓜（半片） */
      g = <>
        <path d="M3 13a13 13 0 0 0 26 0z" fill={c} stroke={INK} strokeWidth="2" strokeLinejoin="round" />
        <path d="M6 13a10 10 0 0 0 20 0z" fill="#ff5a6a" />
        <ellipse cx="12" cy="17" rx="1" ry="1.6" {...dot} /><ellipse cx="16" cy="19.5" rx="1" ry="1.6" {...dot} /><ellipse cx="20" cy="17" rx="1" ry="1.6" {...dot} />
      </>;
      break;
    case 4: /* 水蜜桃 */
      g = <>
        <path d="M16 28C8 28 4 21 6 15c2-6 8-6 10-3 2-3 8-3 10 3 2 6-2 13-10 13z" {...body} />
        <path d="M16 12c-1 5 0 11 0 16" stroke={INK} strokeWidth="1.4" fill="none" opacity="0.5" />
        <path d="M16 12c0-4 3-7 7-7-1 4-3 7-7 7z" {...LEAF} />
      </>;
      break;
    case 5: /* 葡萄 */
      g = <>
        {[[11, 14], [21, 14], [16, 14], [13.5, 20], [18.5, 20], [16, 26]].map(([x, y], i) => <circle key={i} cx={x} cy={y} r="4.4" {...body} />)}
        <path d="M16 10V5" stroke={INK} strokeWidth="2" strokeLinecap="round" />
        <path d="M16 6c3-3 7-3 9-1-2 3-6 4-9 1z" {...LEAF} />
      </>;
      break;
    case 6: /* 橘子 */
      g = <>
        <circle cx="16" cy="18" r="11" {...body} />
        <path d="M16 7c0-2 2-4 5-4-1 3-2 4-5 4z" {...LEAF} />
        <path d="M10 14c1-2 3-3 5-3" stroke="#fff" strokeWidth="1.8" fill="none" strokeLinecap="round" opacity="0.7" />
      </>;
      break;
    case 7: /* 蘋果 */
      g = <>
        <path d="M16 10c-3-3-11-2-11 7 0 7 5 12 8 12 2 0 2-1 3-1s1 1 3 1c3 0 8-5 8-12 0-9-8-10-11-7z" {...body} />
        <path d="M16 10c0-3 1-5 3-6" stroke={INK} strokeWidth="2" fill="none" strokeLinecap="round" />
        <path d="M18 7c1-3 4-4 7-3-1 3-4 4-7 3z" {...LEAF} />
        <path d="M9 16c0-2 1-3 3-4" stroke="#fff" strokeWidth="1.8" fill="none" strokeLinecap="round" opacity="0.7" />
      </>;
      break;
    case 8: /* 櫻桃 */
      g = <>
        <path d="M10 22C11 14 15 8 22 5M22 5c0 6-1 11-3 17" stroke={INK} strokeWidth="1.8" fill="none" strokeLinecap="round" />
        <path d="M22 5c3-2 6-1 7 1-3 2-6 2-7-1z" {...LEAF} />
        <circle cx="10" cy="23" r="5.5" {...body} /><circle cx="19" cy="23" r="5.5" {...body} />
        <circle cx="8.3" cy="21" r="1.1" fill="#fff" opacity="0.7" /><circle cx="17.3" cy="21" r="1.1" fill="#fff" opacity="0.7" />
      </>;
      break;
    case 9: /* 香蕉 */
      g = <>
        <path d="M5 8c1 12 8 20 22 18-3 2-8 4-14 2C5 25 2 16 5 8z" {...body} />
        <path d="M5 8l2-3 3 1-1 4" fill="#8a6a2a" stroke={INK} strokeWidth="1.6" strokeLinejoin="round" />
        <path d="M27 26l2 2" stroke={INK} strokeWidth="2" strokeLinecap="round" />
      </>;
      break;
    case 10: /* 芭樂 */
      g = <>
        <path d="M16 29c-7 0-11-5-10-11 1-5 4-7 7-8 2-1 4-1 6 0 3 1 6 3 7 8 1 6-3 11-10 11z" {...body} />
        <path d="M16 10c0-3 2-6 6-6-1 4-3 6-6 6z" {...LEAF} />
        <circle cx="12" cy="18" r="1" fill="#6b8f33" /><circle cx="20" cy="19" r="1" fill="#6b8f33" /><circle cx="16" cy="23" r="1" fill="#6b8f33" /><circle cx="16" cy="16" r="1" fill="#6b8f33" />
      </>;
      break;
    default: /* 蓮霧 */
      g = <>
        <path d="M10 7h12c4 0 6 5 4 11-2 7-6 11-10 11S8 25 6 18C4 12 6 7 10 7z" {...body} />
        <path d="M11 6l2-3 3 2 3-2 2 3z" fill="#7a2a3c" stroke={INK} strokeWidth="1.6" strokeLinejoin="round" />
        <path d="M10 14c0-2 1-4 3-4" stroke="#fff" strokeWidth="1.8" fill="none" strokeLinecap="round" opacity="0.7" />
      </>;
  }
  return <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" focusable="false">{g}</svg>;
}
