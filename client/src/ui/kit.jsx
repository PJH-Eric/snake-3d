/* 自訂 UI 元件組：頁面只使用這裡的元件，原生控制項（button／input）只在元件內部當語意底層，
 * 外觀與互動完整封裝，並保留鍵盤、觸控、焦點與 ARIA。 */
import { createContext, useCallback, useContext, useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import Icon from './Icon.jsx';
import ScrollArea from './ScrollArea.jsx';
import { t } from '../i18n.js';
import { animalDef } from '../data.js';
import * as sound from '../sound.js';
import { createStore, useStore } from '../app.js';

export { Icon, ScrollArea };

/* ---------- Modal 計數：遊戲輸入在視窗開啟時暫停 ---------- */
const modalState = createStore({ count: 0 });
export const isModalOpen = () => modalState.get().count > 0;
export const useModalOpen = () => useStore(modalState, s => s.count > 0);

/* ---------- 按鈕 ---------- */
export function Button({ children, onClick, variant = 'primary', size = 'md', icon, disabled, full, className = '', type = 'button', ariaLabel, quiet, autoFocus, title }) {
  const click = e => { if (!quiet) sound.sfx('click'); if (onClick) onClick(e); };
  return (
    <button type={type} className={`btn btn-${variant} btn-${size} ${full ? 'btn-full' : ''} ${className}`} onClick={click}
      disabled={disabled} aria-label={ariaLabel} autoFocus={autoFocus} title={title}>
      {icon && <Icon name={icon} size={size === 'lg' ? 26 : 22} />}
      {children != null && children !== '' && <span className="btn-text">{children}</span>}
    </button>
  );
}

export function IconButton({ icon, label, onClick, variant = 'sky', size = 48, badge, className = '', disabled, pressed, buttonRef }) {
  return (
    <button ref={buttonRef} type="button" className={`ibtn ibtn-${variant} ${className}`} style={{ '--sz': size + 'px' }} aria-label={label} title={label}
      disabled={disabled} aria-pressed={pressed} onClick={e => { sound.sfx('click'); onClick && onClick(e); }}>
      <Icon name={icon} size={Math.round(size * 0.5)} />
      {badge ? <span className="ibtn-badge" aria-hidden="true">{badge}</span> : null}
    </button>
  );
}

/* ---------- 版面小元件 ---------- */
export function Panel({ children, className = '', tone = 'paper', as: Tag = 'div', ...rest }) {
  return <Tag className={`panel panel-${tone} ${className}`} {...rest}>{children}</Tag>;
}
export function Chip({ children, tone = 'paper', icon, className = '' }) {
  return <span className={`chip chip-${tone} ${className}`}>{icon && <Icon name={icon} size={16} />}{children}</span>;
}
export function Heading({ level = 2, children, className = '' }) {
  const Tag = 'h' + level;
  return <Tag className={`hd hd-${level} ${className}`}>{children}</Tag>;
}
export function Text({ children, muted, small, className = '', as: Tag = 'p' }) {
  return <Tag className={`txt ${muted ? 'txt-muted' : ''} ${small ? 'txt-small' : ''} ${className}`}>{children}</Tag>;
}
export function Keycap({ children }) { return <kbd className="keycap">{children}</kbd>; }
export function Spinner({ label }) { return <span className="spinner" role="status" aria-label={label || t('conn.ing')} />; }
export function Row({ children, className = '', gap, wrap = true, align, justify }) {
  return <div className={`row ${wrap ? 'row-wrap' : ''} ${className}`} style={{ gap, alignItems: align, justifyContent: justify }}>{children}</div>;
}
export function Stack({ children, className = '', gap }) { return <div className={`stack ${className}`} style={{ gap }}>{children}</div>; }

/* ---------- 動物臉 ---------- */
export function AnimalFace({ animal, size = 48, className = '' }) {
  const a = animalDef(animal);
  const ink = '#4a3320';
  const ears = [];
  const ec = a.earCol || a.body;
  if (a.ear === 'point') for (const s of [-1, 1]) ears.push(<g key={s}><path d={`M${50 + s * 30} 30 L${50 + s * 38} 4 L${50 + s * 14} 20 Z`} fill={ec} stroke={ink} strokeWidth="4" strokeLinejoin="round" />{a.earIn && <path d={`M${50 + s * 28} 25 L${50 + s * 33} 11 L${50 + s * 19} 21 Z`} fill={a.earIn} />}</g>);
  if (a.ear === 'floppy') for (const s of [-1, 1]) ears.push(<ellipse key={s} cx={50 + s * 40} cy="42" rx="10" ry="20" fill={ec} stroke={ink} strokeWidth="4" transform={`rotate(${s * 14} ${50 + s * 40} 42)`} />);
  if (a.ear === 'long') for (const s of [-1, 1]) ears.push(<g key={s}><ellipse cx={50 + s * 18} cy="12" rx="9" ry="22" fill={ec} stroke={ink} strokeWidth="4" />{a.earIn && <ellipse cx={50 + s * 18} cy="14" rx="4" ry="15" fill={a.earIn} />}</g>);
  if (a.ear === 'round') for (const s of [-1, 1]) ears.push(<circle key={s} cx={50 + s * 32} cy="24" r="14" fill={ec} stroke={ink} strokeWidth="4" />);
  const eyeY = a.eyesTop ? 36 : 52;
  return (
    <svg className={'face ' + className} width={size} height={size} viewBox="0 0 100 100" aria-hidden="true" focusable="false">
      {ears}
      {a.eyesTop && [-1, 1].map(s => <circle key={s} cx={50 + s * 22} cy="26" r="14" fill={a.body} stroke={ink} strokeWidth="4" />)}
      <ellipse cx="50" cy="58" rx="40" ry="36" fill={a.body} stroke={ink} strokeWidth="4" />
      {a.muzzle && <ellipse cx="50" cy="72" rx="20" ry="13" fill={a.muzzle} />}
      {a.patches && [-1, 1].map(s => <ellipse key={s} cx={50 + s * 21} cy="52" rx="10" ry="13" fill="#2d2d36" transform={`rotate(${s * 18} ${50 + s * 21} 52)`} />)}
      {[-1, 1].map(s => <g key={s}><circle cx={50 + s * 20} cy={eyeY} r="7.5" fill="#fff" stroke={ink} strokeWidth="2.5" /><circle cx={50 + s * 20 + s * 1.5} cy={eyeY + 0.5} r="4" fill="#1d1630" /></g>)}
      {a.beak ? <path d="M42 66 L58 66 L50 76 Z" fill={a.beak} stroke={ink} strokeWidth="2.5" strokeLinejoin="round" />
        : a.nose ? <ellipse cx="50" cy="66" rx="5" ry="3.6" fill={a.nose} /> : <path d="M40 70 Q50 77 60 70" fill="none" stroke={ink} strokeWidth="3" strokeLinecap="round" />}
    </svg>
  );
}

/* ---------- Modal ---------- */
const FOCUSABLE = 'button:not([disabled]),input:not([disabled]),[tabindex]:not([tabindex="-1"]),a[href]';

export function Modal({ title, onClose, children, width = 560, dismissible = true, label, className = '', footer, maskClass = '' }) {
  const ref = useRef(null);
  const titleId = useId();
  const closeRef = useRef(onClose);
  closeRef.current = onClose;

  useLayoutEffect(() => {
    const opener = document.activeElement;
    modalState.set(s => ({ count: s.count + 1 }));
    const node = ref.current;
    const first = node && node.querySelector('[data-autofocus]');
    const target = first || (node && [...node.querySelectorAll(FOCUSABLE)].find(el => !el.closest('.sa-vp')) ) || node;
    if (target) target.focus({ preventScroll: true });
    return () => {
      modalState.set(s => ({ count: Math.max(0, s.count - 1) }));
      if (opener && opener.isConnected && typeof opener.focus === 'function') opener.focus({ preventScroll: true });
    };
  }, []);

  useEffect(() => {
    const onKey = e => {
      const node = ref.current;
      if (!node) return;
      /* 只處理最上層的視窗 */
      const all = document.querySelectorAll('.modal');
      if (all[all.length - 1] !== node) return;
      if (e.key === 'Escape' && dismissible) { e.stopPropagation(); closeRef.current && closeRef.current(); return; }
      if (e.key !== 'Tab') return;
      const items = [...node.querySelectorAll(FOCUSABLE)].filter(el => el.offsetParent !== null);
      if (!items.length) { e.preventDefault(); return; }
      const firstEl = items[0], lastEl = items[items.length - 1];
      if (e.shiftKey && document.activeElement === firstEl) { e.preventDefault(); lastEl.focus(); }
      else if (!e.shiftKey && document.activeElement === lastEl) { e.preventDefault(); firstEl.focus(); }
      else if (!node.contains(document.activeElement)) { e.preventDefault(); firstEl.focus(); }
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [dismissible]);

  return createPortal(
    <div className={'mask ' + maskClass} onPointerDown={e => { if (e.target === e.currentTarget && dismissible && onClose) onClose(); }}>
      <div ref={ref} className={'modal ' + className} role="dialog" aria-modal="true" aria-labelledby={title ? titleId : undefined} aria-label={title ? undefined : label}
        style={{ '--mw': width + 'px' }} tabIndex={-1}>
        {title && (
          <div className="modal-head">
            <h2 id={titleId} className="modal-title">{title}</h2>
            {dismissible && onClose && <IconButton icon="close" label={t('common.close')} onClick={onClose} variant="paper" size={44} />}
          </div>
        )}
        <ScrollArea className="modal-scroll"><div className="modal-body">{children}</div></ScrollArea>
        {footer && <div className="modal-foot">{footer}</div>}
      </div>
    </div>,
    document.body
  );
}

export function ConfirmModal({ title, text, okLabel, danger, onOk, onClose }) {
  return (
    <Modal title={title} onClose={onClose} width={460}
      footer={<><Button variant="paper" onClick={onClose}>{t('common.cancel')}</Button><Button variant={danger ? 'danger' : 'primary'} onClick={() => { onClose(); onOk(); }}>{okLabel || t('common.ok')}</Button></>}>
      <Text>{text}</Text>
    </Modal>
  );
}

/* ---------- 開關、滑桿、分段選擇 ---------- */
export function Toggle({ checked, onChange, label, hint }) {
  const id = useId();
  return (
    <div className="field-row">
      <div className="field-label"><span id={id}>{label}</span>{hint && <span className="field-hint">{hint}</span>}</div>
      <button type="button" role="switch" aria-checked={checked} aria-labelledby={id} className={'toggle ' + (checked ? 'on' : '')}
        onClick={() => { sound.sfx('click'); onChange(!checked); }}>
        <span className="toggle-knob" />
        <span className="toggle-txt" aria-hidden="true">{checked ? 'ON' : 'OFF'}</span>
      </button>
    </div>
  );
}

export function Slider({ value, onChange, min = 0, max = 1, step = 0.05, label, disabled }) {
  const id = useId();
  const track = useRef(null);
  const pct = ((value - min) / (max - min)) * 100;
  const setFrom = clientX => {
    const r = track.current.getBoundingClientRect();
    const f = Math.min(1, Math.max(0, (clientX - r.left) / r.width));
    const v = Math.round((min + f * (max - min)) / step) * step;
    onChange(Math.min(max, Math.max(min, +v.toFixed(4))));
  };
  const down = e => {
    if (disabled) return;
    e.currentTarget.setPointerCapture(e.pointerId);
    setFrom(e.clientX);
  };
  const move = e => { if (e.currentTarget.hasPointerCapture(e.pointerId)) setFrom(e.clientX); };
  const key = e => {
    if (disabled) return;
    let v = value;
    if (e.key === 'ArrowRight' || e.key === 'ArrowUp') v += step;
    else if (e.key === 'ArrowLeft' || e.key === 'ArrowDown') v -= step;
    else if (e.key === 'Home') v = min; else if (e.key === 'End') v = max; else return;
    e.preventDefault();
    onChange(Math.min(max, Math.max(min, +v.toFixed(4))));
  };
  return (
    <div className={'field-row ' + (disabled ? 'disabled' : '')}>
      <div className="field-label"><span id={id}>{label}</span></div>
      <div className="slider" ref={track} role="slider" tabIndex={disabled ? -1 : 0} aria-labelledby={id} aria-valuemin={min} aria-valuemax={max}
        aria-valuenow={value} aria-valuetext={Math.round(pct) + '%'} aria-disabled={disabled} onPointerDown={down} onPointerMove={move} onKeyDown={key}>
        <div className="slider-rail"><div className="slider-fill" style={{ width: pct + '%' }} /></div>
        <div className="slider-knob" style={{ left: pct + '%' }} />
      </div>
    </div>
  );
}

export function Segmented({ value, onChange, options, label, hint, ariaLabel, size = 'md', stacked }) {
  const id = useId();
  const refs = useRef([]);
  const idx = options.findIndex(o => o.value === value);
  const key = (e, i) => {
    let n = i;
    if (e.key === 'ArrowRight' || e.key === 'ArrowDown') n = (i + 1) % options.length;
    else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') n = (i - 1 + options.length) % options.length;
    else return;
    e.preventDefault();
    onChange(options[n].value);
    refs.current[n] && refs.current[n].focus();
  };
  const group = (
    <div className={`seg seg-${size}`} role="radiogroup" aria-labelledby={label ? id : undefined} aria-label={label ? undefined : ariaLabel}>
      {options.map((o, i) => (
        <button key={String(o.value)} ref={el => { refs.current[i] = el; }} type="button" role="radio" aria-checked={o.value === value}
          tabIndex={o.value === value || (idx < 0 && i === 0) ? 0 : -1} disabled={o.disabled}
          className={'seg-item ' + (o.value === value ? 'on' : '')} onKeyDown={e => key(e, i)}
          onClick={() => { sound.sfx('click'); onChange(o.value); }}>{o.label}</button>
      ))}
    </div>
  );
  if (!label) return group;
  return (
    <div className={'field-col ' + (stacked ? 'stacked' : '')}>
      <div className="field-label"><span id={id}>{label}</span>{hint && <span className="field-hint">{hint}</span>}</div>
      {group}
    </div>
  );
}

export function Stepper({ value, onChange, min, max, label, format = v => v }) {
  const id = useId();
  return (
    <div className="field-row">
      <div className="field-label"><span id={id}>{label}</span></div>
      <div className="stepper" role="group" aria-labelledby={id}>
        <IconButton icon="minus" label={t('ui.dec', { label })} onClick={() => onChange(Math.max(min, value - 1))} disabled={value <= min} variant="paper" size={44} />
        <output className="stepper-val" aria-live="polite">{format(value)}</output>
        <IconButton icon="plus" label={t('ui.inc', { label })} onClick={() => onChange(Math.min(max, value + 1))} disabled={value >= max} variant="paper" size={44} />
      </div>
    </div>
  );
}

/* ---------- 自訂下拉選單 ---------- */
export function Select({ value, onChange, options, label, ariaLabel, disabled, className = '' }) {
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const btn = useRef(null), pop = useRef(null);
  const id = useId(), listId = useId();
  const idx = Math.max(0, options.findIndex(o => o.value === value));
  const cur = options[idx];
  const close = useCallback(() => { setOpen(false); btn.current && btn.current.focus({ preventScroll: true }); }, []);

  useEffect(() => {
    if (!open) return;
    setActive(idx);
    const out = e => { if (!pop.current || (!pop.current.contains(e.target) && !btn.current.contains(e.target))) setOpen(false); };
    document.addEventListener('pointerdown', out, true);
    return () => document.removeEventListener('pointerdown', out, true);
  }, [open, idx]);
  useEffect(() => {
    if (!open || !pop.current) return;
    const el = pop.current.querySelector('[data-active="true"]');
    if (el && el.scrollIntoView) el.scrollIntoView({ block: 'nearest' });
  }, [open, active]);

  const key = e => {
    if (disabled) return;
    if (!open) { if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(e.key)) { e.preventDefault(); setOpen(true); } return; }
    if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); close(); }
    else if (e.key === 'ArrowDown') { e.preventDefault(); setActive(a => Math.min(options.length - 1, a + 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive(a => Math.max(0, a - 1)); }
    else if (e.key === 'Home') { e.preventDefault(); setActive(0); }
    else if (e.key === 'End') { e.preventDefault(); setActive(options.length - 1); }
    else if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onChange(options[active].value); close(); }
    else if (e.key === 'Tab') setOpen(false);
  };
  const body = (
    <div className={'select ' + className}>
      <button ref={btn} type="button" id={id} className="select-btn" role="combobox" aria-haspopup="listbox" aria-expanded={open} aria-controls={listId}
        aria-label={label ? undefined : ariaLabel} disabled={disabled} onKeyDown={key} onClick={() => { sound.sfx('click'); setOpen(o => !o); }}>
        <span className="select-val">{cur ? cur.label : t('ui.choose')}</span>
        <Icon name="chevron" size={20} />
      </button>
      {open && (
        <div ref={pop} className="select-pop">
          <ScrollArea className="select-scroll">
            <ul id={listId} role="listbox" className="select-list" aria-labelledby={id}>
              {options.map((o, i) => (
                <li key={String(o.value)} role="option" aria-selected={o.value === value} data-active={i === active}
                  className={'select-opt ' + (o.value === value ? 'on ' : '') + (i === active ? 'act' : '')}
                  onPointerDown={e => e.preventDefault()} onClick={() => { onChange(o.value); close(); }} onPointerEnter={() => setActive(i)}>
                  {o.label}{o.value === value && <Icon name="check" size={18} />}
                </li>
              ))}
            </ul>
          </ScrollArea>
        </div>
      )}
    </div>
  );
  if (!label) return body;
  return <div className="field-col"><div className="field-label"><span>{label}</span></div>{body}</div>;
}

/* ---------- 文字輸入 ---------- */
export function TextField({ value, onChange, placeholder, maxLength, label, ariaLabel, onEnter, inputRef, autoFocus, className = '', enterKeyHint }) {
  const id = useId();
  const field = (
    <span className={'tf ' + className}>
      <input id={id} ref={inputRef} className="tf-input" type="text" value={value} placeholder={placeholder} maxLength={maxLength} autoFocus={autoFocus}
        aria-label={label ? undefined : ariaLabel} enterKeyHint={enterKeyHint} autoComplete="off" autoCorrect="off" spellCheck={false}
        onChange={e => onChange(e.target.value)} onKeyDown={e => { if (e.key === 'Enter' && onEnter && !e.nativeEvent.isComposing) { e.preventDefault(); onEnter(); } e.stopPropagation(); }} />
    </span>
  );
  if (!label) return field;
  return <div className="field-col"><label className="field-label" htmlFor={id}><span>{label}</span></label>{field}</div>;
}

/* ---------- Toast ---------- */
export function ToastHost({ toasts }) {
  return (
    <div className="toasts" aria-live="polite" role="status">
      {toasts.map(x => <div key={x.id} className="toast">{x.text}</div>)}
    </div>
  );
}

export function PressHold({ onChange, className = '', children, label }) {
  /* 加速鈕：按住為開，放開為關；支援多指，離開元件也會放開 */
  const down = useRef(new Set());
  const set = (id, on) => {
    const before = down.current.size > 0;
    if (on) down.current.add(id); else down.current.delete(id);
    const after = down.current.size > 0;
    if (before !== after) onChange(after);
  };
  useEffect(() => () => { if (down.current.size) onChange(false); }, []); // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <button type="button" className={className} aria-label={label}
      onPointerDown={e => { e.preventDefault(); e.currentTarget.setPointerCapture(e.pointerId); set(e.pointerId, true); }}
      onPointerUp={e => set(e.pointerId, false)} onPointerCancel={e => set(e.pointerId, false)} onLostPointerCapture={e => set(e.pointerId, false)}
      onContextMenu={e => e.preventDefault()}
      onKeyDown={e => { if ((e.key === ' ' || e.key === 'Enter') && !e.repeat) { e.preventDefault(); set('k', true); } }}
      onKeyUp={e => { if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); set('k', false); } }}>
      {children}
    </button>
  );
}
