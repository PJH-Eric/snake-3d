/* 共用外框：畫面容器、右上角設定＋語言、設定視窗、連線橫幅與確認視窗。 */
import { useEffect, useRef, useState } from 'react';
import { Button, ConfirmModal, IconButton, Keycap, Modal, ScrollArea, Segmented, Slider, Text, Toggle, ToastHost, Heading, Spinner } from './ui/kit.jsx';
import { app, closeConfirm, useApp } from './app.js';
import { LANGS, LANG_NAMES, getLang, setLang, t, useLang } from './i18n.js';
import * as sound from './sound.js';
import { resetSettings, update, useSave } from './store.js';
import { toast } from './app.js';

/** 畫面外框：標題列＋可捲動內容（內容超出視窗時用自訂捲軸）。 */
export function Screen({ title, onBack, backLabel, children, width = 880, actions }) {
  return (
    <main className="screen">
      <div className="screen-body">
        <ScrollArea className="screen-scroll" label={title}>
          <div className="screen-inner" style={{ '--mw': width + 'px' }}>
            {(title || onBack) && (
              <div className="page-title">
                {onBack && <IconButton icon="back" label={backLabel || t('common.back')} onClick={onBack} variant="paper" />}
                <Heading level={2}>{title}</Heading>
                {actions}
              </div>
            )}
            {children}
          </div>
        </ScrollArea>
      </div>
    </main>
  );
}

const LANG_BADGE = { 'zh-Hant': '繁', 'zh-Hans': '简', en: 'EN' };

/** 語言選單：地球鈕＋目前語言徽章，點開後列出全部語言（↑↓ 移動、Esc 關閉、點外面關閉）。 */
function LangMenu() {
  const lang = useLang();
  const [open, setOpen] = useState(false);
  const wrap = useRef(null);
  const btn = useRef(null);
  const close = focus => { setOpen(false); if (focus) btn.current && btn.current.focus(); };
  useEffect(() => {
    if (!open) return undefined;
    const onDoc = e => { if (wrap.current && !wrap.current.contains(e.target)) setOpen(false); };
    document.addEventListener('pointerdown', onDoc, true);
    wrap.current.querySelector('[aria-checked="true"]').focus();
    return () => document.removeEventListener('pointerdown', onDoc, true);
  }, [open]);
  const onKey = (e, i) => {
    const items = wrap.current.querySelectorAll('[role="menuitemradio"]');
    const go = n => { e.preventDefault(); items[(n + items.length) % items.length].focus(); };
    e.stopPropagation();
    if (e.key === 'ArrowDown') go(i + 1);
    else if (e.key === 'ArrowUp') go(i - 1);
    else if (e.key === 'Escape') { e.preventDefault(); close(true); }
    else if (e.key === 'Tab') setOpen(false);
  };
  return (
    <div className="langmenu" ref={wrap}>
      <IconButton icon="globe" variant="paper" className="langbtn" badge={LANG_BADGE[lang]} buttonRef={btn}
        label={t('lang.switch') + '：' + LANG_NAMES[lang]} onClick={() => setOpen(o => !o)} />
      {open && (
        <div className="langpop" role="menu" aria-label={t('lang.switch')}>
          {LANGS.map((code, i) => (
            <button key={code} type="button" role="menuitemradio" aria-checked={code === lang} lang={code} className={'langopt ' + (code === lang ? 'cur' : '')}
              onKeyDown={e => onKey(e, i)} onClick={() => { sound.sfx('click'); setLang(code); close(true); }}>{LANG_NAMES[code]}</button>
          ))}
        </div>
      )}
    </div>
  );
}

export function TopRight() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <div className="topright">
        <LangMenu />
        <IconButton icon="settings" label={t('set.title')} onClick={() => setOpen(true)} variant="orange" />
      </div>
      {open && <SettingsModal onClose={() => setOpen(false)} />}
    </>
  );
}

export function SettingsModal({ onClose }) {
  const d = useSave();
  useLang();
  const onOff = (key, label, hint) => <Toggle checked={d[key]} onChange={v => update({ [key]: v })} label={label} hint={hint} />;
  return (
    <Modal title={t('set.title')} onClose={onClose} width={620} className="settings"
      footer={<><Button variant="paper" onClick={() => { resetSettings(); toast(t('set.reset.done'), 2.4); }}>{t('set.reset')}</Button><Button onClick={onClose}>{t('common.done')}</Button></>}>
      <Heading level={3}>{t('set.sound')}</Heading>
      {onOff('bgm', t('set.bgm'))}
      <Slider value={d.bgmVol} onChange={v => update({ bgmVol: v })} label={t('set.bgmVol')} disabled={!d.bgm} />
      {onOff('sfx', t('set.sfx'))}
      <Slider value={d.sfxVol} onChange={v => update({ sfxVol: v })} label={t('set.sfxVol')} disabled={!d.sfx} />
      <Heading level={3}>{t('set.display')}</Heading>
      <Segmented label={t('set.cam')} value={d.zoom} onChange={v => update({ zoom: v })}
        options={[{ value: 'near', label: t('set.cam.near') }, { value: 'normal', label: t('set.cam.normal') }, { value: 'far', label: t('set.cam.far') }]} />
      {onOff('minimap', t('set.map'))}
      <Segmented label={t('set.quality')} hint={t('set.quality.hint')} value={d.quality} onChange={v => update({ quality: v })}
        options={[{ value: 'high', label: t('set.quality.high') }, { value: 'normal', label: t('set.quality.normal') }, { value: 'low', label: t('set.quality.low') }]} />
      <Segmented label={t('set.stick')} hint={t('set.stick.hint')} value={d.controlMode} onChange={v => update({ controlMode: v })}
        options={[{ value: 'finger', label: t('set.stick.float') }, { value: 'joystick', label: t('set.stick.fixed') }]} />
      {d.controlMode === 'joystick' && <Segmented label={t('set.side')} hint={t('set.side.hint')} value={d.stickSide} onChange={v => update({ stickSide: v })}
        options={[{ value: 'left', label: t('set.side.left') }, { value: 'right', label: t('set.side.right') }]} />}
      {onOff('vibrate', t('set.vibrate'))}
      {onOff('reduceMotion', t('set.calm'))}
      <Text small muted>{t('set.keys')} <Keycap>←↑↓→</Keycap> <Keycap>WASD</Keycap> <Keycap>Space</Keycap> <Keycap>Shift</Keycap></Text>
    </Modal>
  );
}

export function GlobalLayer() {
  const toasts = useApp(s => s.toasts);
  const banner = useApp(s => s.banner);
  const confirm = useApp(s => s.confirm);
  const outage = useApp(s => s.outage);
  return (
    <>
      <TopRight />
      {banner && <div className="banner" role="status"><Spinner />{banner}</div>}
      <ToastHost toasts={toasts} />
      {confirm && <ConfirmModal {...confirm} onClose={closeConfirm} />}
      {outage && (
        <Modal title={t('net.outage.title')} width={520} onClose={() => app.set({ outage: false })}
          footer={<Button onClick={() => app.set({ outage: false })}>{t('common.ok')}</Button>}>
          <Text>{t('net.outage.message', { month: monthText() })}</Text>
        </Modal>
      )}
    </>
  );
}

import { OutageWindow } from './net.js';
function monthText() { return OutageWindow.resetMonth(new Date(), getLang() === 'en'); }
