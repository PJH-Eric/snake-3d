/* 自訂捲動區：原生捲軸一律隱藏，改畫自己的軌道與滑塊（可拖曳、點軌道跳頁、滑鼠滾輪、觸控慣性與鍵盤方向鍵都沿用瀏覽器捲動）。 */
import { forwardRef, useCallback, useEffect, useImperativeHandle, useRef, useState } from 'react';

const MIN_THUMB = 36;

function Bar({ axis, vp, tick }) {
  const [geo, setGeo] = useState({ show: false, size: 0, pos: 0 });
  const trackRef = useRef(null);
  const y = axis === 'y';
  useEffect(() => {
    const el = vp.current, track = trackRef.current;
    if (!el || !track) return;
    const total = y ? el.scrollHeight : el.scrollWidth, view = y ? el.clientHeight : el.clientWidth;
    const trackLen = y ? track.clientHeight : track.clientWidth;
    if (total <= view + 1 || trackLen <= 0) { setGeo(g => (g.show ? { show: false, size: 0, pos: 0 } : g)); return; }
    const size = Math.max(MIN_THUMB, (view / total) * trackLen);
    const max = total - view, scroll = y ? el.scrollTop : el.scrollLeft;
    const pos = max > 0 ? (scroll / max) * (trackLen - size) : 0;
    setGeo(g => (g.show && Math.abs(g.size - size) < 0.5 && Math.abs(g.pos - pos) < 0.5 ? g : { show: true, size, pos }));
  });
  void tick;

  const onThumbDown = e => {
    e.preventDefault(); e.stopPropagation();
    const el = vp.current, track = trackRef.current;
    const startPt = y ? e.clientY : e.clientX, startScroll = y ? el.scrollTop : el.scrollLeft;
    const thumb = e.currentTarget;
    thumb.setPointerCapture(e.pointerId);
    const move = ev => {
      const total = y ? el.scrollHeight : el.scrollWidth, view = y ? el.clientHeight : el.clientWidth;
      const trackLen = y ? track.clientHeight : track.clientWidth;
      const room = trackLen - geo.size;
      if (room <= 0) return;
      const delta = ((y ? ev.clientY : ev.clientX) - startPt) * ((total - view) / room);
      if (y) el.scrollTop = startScroll + delta; else el.scrollLeft = startScroll + delta;
    };
    const up = () => { thumb.removeEventListener('pointermove', move); thumb.removeEventListener('pointerup', up); thumb.removeEventListener('pointercancel', up); };
    thumb.addEventListener('pointermove', move); thumb.addEventListener('pointerup', up); thumb.addEventListener('pointercancel', up);
  };
  const onTrackDown = e => {
    if (e.target !== trackRef.current) return;
    const el = vp.current, rect = trackRef.current.getBoundingClientRect();
    const pt = y ? e.clientY - rect.top : e.clientX - rect.left;
    const dir = pt < geo.pos ? -1 : 1;
    const page = (y ? el.clientHeight : el.clientWidth) * 0.9;
    el.scrollBy({ [y ? 'top' : 'left']: dir * page, behavior: 'smooth' });
  };

  return (
    <div ref={trackRef} className={'sa-track sa-' + axis + (geo.show ? '' : ' sa-hidden')} onPointerDown={onTrackDown} aria-hidden="true">
      <div className="sa-thumb" onPointerDown={onThumbDown}
        style={y ? { height: geo.size, transform: `translateY(${geo.pos}px)` } : { width: geo.size, transform: `translateX(${geo.pos}px)` }} />
    </div>
  );
}

const ScrollArea = forwardRef(function ScrollArea({ axis = 'y', className = '', children, label, onScroll, stickBottom = false, style }, ref) {
  const vp = useRef(null);
  const [tick, setTick] = useState(0);
  const stuck = useRef(true);
  useImperativeHandle(ref, () => vp.current);
  const bump = useCallback(() => setTick(n => n + 1), []);

  useEffect(() => {
    const el = vp.current;
    if (!el) return;
    const ro = new ResizeObserver(() => { bump(); if (stickBottom && stuck.current) el.scrollTop = el.scrollHeight; });
    ro.observe(el);
    const mo = new MutationObserver(() => { bump(); if (stickBottom && stuck.current) el.scrollTop = el.scrollHeight; });
    mo.observe(el, { childList: true, subtree: true, characterData: true });
    return () => { ro.disconnect(); mo.disconnect(); };
  }, [bump, stickBottom]);

  const handleScroll = e => {
    const el = e.currentTarget;
    if (stickBottom) stuck.current = el.scrollHeight - el.scrollTop - el.clientHeight < 24;
    bump();
    if (onScroll) onScroll(e);
  };

  return (
    <div className={'sa ' + className} style={style}>
      <div ref={vp} className={'sa-vp sa-vp-' + axis} onScroll={handleScroll} tabIndex={0} role="region" aria-label={label}>
        {children}
      </div>
      {(axis === 'y' || axis === 'both') && <Bar axis="y" vp={vp} tick={tick} />}
      {(axis === 'x' || axis === 'both') && <Bar axis="x" vp={vp} tick={tick} />}
    </div>
  );
});

export default ScrollArea;
