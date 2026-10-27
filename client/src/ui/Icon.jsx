/* 內建向量圖示（24x24，描邊風格，顏色跟著 currentColor）。 */
const P = {
  settings: 'M12 8.6a3.4 3.4 0 1 0 0 6.8 3.4 3.4 0 0 0 0-6.8Zm8.2 4.3-.1-1.8 1.7-1.3-1.6-2.8-2 .7a7 7 0 0 0-1.5-.9l-.4-2.1H9.8l-.4 2.1c-.5.2-1 .5-1.5.9l-2-.7-1.6 2.8 1.7 1.3a7 7 0 0 0 0 1.8l-1.7 1.3 1.6 2.8 2-.7c.5.4 1 .7 1.5.9l.4 2.1h3.2l.4-2.1c.5-.2 1-.5 1.5-.9l2 .7 1.6-2.8-1.7-1.3Z',
  globe: 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Zm0 0c-2.6 2.4-3.8 5.4-3.8 9s1.2 6.6 3.8 9m0-18c2.6 2.4 3.8 5.4 3.8 9s-1.2 6.6-3.8 9M3.5 9h17M3.5 15h17',
  pause: 'M8 5v14M16 5v14',
  close: 'M6 6l12 12M18 6 6 18',
  info: 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Zm0 8v5.5M12 7.6v.2',
  back: 'M15 5l-7 7 7 7',
  next: 'M9 5l7 7-7 7',
  flash: 'M13 3 5 13.5h5.5L9.5 21 19 9.8h-5.7L13 3Z',
  copy: 'M9 9h10v11H9zM5 15V4h10',
  share: 'M12 15V4m0 0L8 8m4-4 4 4M5 13v7h14v-7',
  plus: 'M12 5v14M5 12h14',
  minus: 'M5 12h14',
  check: 'M5 12.5l4.5 4.5L19 7.5',
  user: 'M12 4a4 4 0 1 0 0 8 4 4 0 0 0 0-8Zm-7 16c.6-3.6 3.4-5.5 7-5.5s6.4 1.9 7 5.5',
  eye: 'M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12Zm9.5-3a3 3 0 1 0 0 6 3 3 0 0 0 0-6Z',
  crown: 'M4 18h16l1-10-5 4-4-7-4 7-5-4 1 10Z',
  trash: 'M5 7h14M10 7V4h4v3m-8 0 1 13h10l1-13',
  chat: 'M4 5h16v11H11l-5 4v-4H4Z',
  send: 'M4 12 20 4l-4 16-4-6-8-2Z',
  chevron: 'M6 9l6 6 6-6',
  home: 'M4 11.5 12 4l8 7.5V20h-5v-5H9v5H4Z',
  play: 'M7 4.5v15L19 12Z',
  refresh: 'M19 8a8 8 0 1 0 1 5M19 3v5h-5',
  link: 'M10 14a4.5 4.5 0 0 0 6.4 0l3-3a4.5 4.5 0 0 0-6.4-6.4l-1 1M14 10a4.5 4.5 0 0 0-6.4 0l-3 3a4.5 4.5 0 0 0 6.4 6.4l1-1',
  star: 'M12 3l2.7 5.6 6.1.8-4.5 4.3 1.1 6.1L12 16.9 6.6 19.8l1.1-6.1L3.2 9.4l6.1-.8L12 3Z',
  list: 'M5 7h14M5 12h14M5 17h14',
  sound: 'M4 9.5h3.5L12 5.5v13l-4.5-4H4ZM16 9a4.5 4.5 0 0 1 0 6m2.5-9a8 8 0 0 1 0 12',
  logout: 'M10 5H5v14h5M14 8l4 4-4 4m4-4H9',
  spark: 'M12 3v5M12 16v5M3 12h5M16 12h5M6 6l3 3M15 15l3 3M18 6l-3 3M9 15l-3 3'
};

export default function Icon({ name, size = 24, className = '', strokeWidth = 2.4, fill = 'none' }) {
  return (
    <svg className={'icon ' + className} width={size} height={size} viewBox="0 0 24 24" fill={fill} stroke="currentColor"
      strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
      <path d={P[name] || P.info} />
    </svg>
  );
}
