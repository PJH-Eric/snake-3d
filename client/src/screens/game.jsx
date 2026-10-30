import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import { AnimalFace, Button, Chip, Heading, IconButton, Keycap, Modal, PressHold, Row, ScrollArea, Stack, Text, Icon, useModalOpen, isModalOpen } from '../ui/kit.jsx';
import { app, confirmBox, go } from '../app.js';
import { getSave, useSave } from '../store.js';
import { World3D } from '../game/world.js';
import { GameSession, TILT } from '../game/session.js';
import { attachBridge, backToRoom, clearUnread, leaveRoom, leaveToHome, online, rematch, useOnline } from '../online.js';
import { backToSetup, restart } from '../flow.js';
import { clock, levelName, playerName, t, useLang } from '../i18n.js';
import { slotColor } from '../data.js';
import * as sound from '../sound.js';
import { ChatBox } from './room.jsx';

function useViewport() {
  const get = () => ({ w: window.innerWidth, h: window.innerHeight });
  const [vp, setVp] = useState(get);
  useEffect(() => {
    const on = () => setVp(get());
    window.addEventListener('resize', on); window.addEventListener('orientationchange', on);
    return () => { window.removeEventListener('resize', on); window.removeEventListener('orientationchange', on); };
  }, []);
  return vp;
}

const coarse = () => typeof matchMedia !== 'undefined' && (matchMedia('(pointer: coarse)').matches || matchMedia('(hover: none)').matches);

/* ---------- 小地圖 ---------- */
function MiniMap({ session, size }) {
  const ref = useRef(null);
  useEffect(() => {
    const cv = ref.current;
    if (!cv) return;
    const ctx = cv.getContext('2d');
    const draw = () => {
      const v = session.view, S = cv.width, c = S / 2, k = (S / 2 - 4) / v.R;
      ctx.clearRect(0, 0, S, S);
      ctx.fillStyle = '#d8efb8'; ctx.beginPath(); ctx.arc(c, c, S / 2 - 3, 0, 7); ctx.fill();
      ctx.fillStyle = '#9aa0a6';
      for (const r of v.rocks) { ctx.beginPath(); ctx.arc(c + r[0] * k, c + r[1] * k, Math.max(1.5, r[2] * k), 0, 7); ctx.fill(); }
      for (const f of v.foods) {
        ctx.fillStyle = f.type === 1 ? '#ffc400' : f.type === 2 ? slotColor(f.v) : '#ff5a52';
        const rr = f.type === 1 ? 2.6 : 1.4;
        ctx.fillRect(c + f.x * k - rr, c + f.z * k - rr, rr * 2, rr * 2);
      }
      for (const p of v.players) {
        if (!p.alive || !p.pts || p.pts.length < 2) continue;
        ctx.strokeStyle = slotColor(p.slot); ctx.lineWidth = p.slot === session.slot ? 3 : 2; ctx.lineCap = 'round';
        ctx.beginPath();
        const n = p.pts.length / 2;
        for (let i = 0; i < n; i += 2) { const x = c + p.pts[i * 2] * k, y = c + p.pts[i * 2 + 1] * k; if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y); }
        ctx.stroke();
        ctx.fillStyle = '#4a3320'; ctx.beginPath(); ctx.arc(c + p.pts[0] * k, c + p.pts[1] * k, p.slot === session.slot ? 4 : 3, 0, 7); ctx.fill();
      }
    };
    draw();
    const id = setInterval(draw, 100);
    return () => clearInterval(id);
  }, [session, size]);
  return <canvas ref={ref} width={size * 2} height={size * 2} className={'minimap ' + (size < 110 ? 'compact' : '')} style={{ width: size, height: size }} role="img" aria-label={t('game.minimap')} />;
}

/* ---------- 側欄：操作 Summary（上）＋ 聊天室（下，線上才有） ---------- */
function SidePanel({ hud, kind, drawer, onClose, onMenu, onWatch }) {
  useLang();
  const touch = coarse();
  const rows = hud.players;
  const canWatch = canWatchHud(hud);
  return (
    <aside className={'game-side ' + (drawer ? 'drawer' : '')} aria-label={t('side.aria')}>
      <div className="side-head">
        <Heading level={2}>{t('side.title')}</Heading>
        <IconButton icon={kind === 'online' ? 'close' : 'pause'} label={kind === 'online' ? t('side.exitOnline') : t('side.exitPause')} onClick={onMenu} variant="paper" size={44} />
        <IconButton icon="back" label={t('side.collapse')} onClick={onClose} size={44} />
      </div>
      <ScrollArea className="side-scroll" label={t('game.players')}>
        <div className="side-sum">
          <Text small muted>{touch ? t('help.ctl.touch') : t('set.keys')}</Text>
          {hud.slot < 0 && <Chip tone="orange" icon="eye">{t('game.spectating')}</Chip>}
          {canWatch && <Text small muted>{t('side.watchHint')}</Text>}
          {rows.map(p => {
            const me = p.slot === hud.slot;
            const name = playerName(p.kind, p.animal, p.name);
            const state = p.left ? t('sum.left') : !p.alive ? t('sum.out') : null;
            const role = p.kind === 'ai' ? (p.level ? t('sum.cpuLv', { lv: levelName(p.level) }) : t('sum.cpu')) : t('sum.player');
            const pick = canWatch && p.alive && !me;
            const Row = pick ? 'button' : 'div';
            return (
              <Row key={p.slot} type={pick ? 'button' : undefined} className={'sum-row ' + (me ? 'me ' : '') + (!p.alive ? 'out ' : '') + (pick ? 'pick ' : '') + (hud.watch === p.slot && canWatch ? 'fol' : '')}
                aria-pressed={pick ? hud.watch === p.slot : undefined} onClick={pick ? () => onWatch(p.slot) : undefined}>
                <span className="swatch" style={{ background: slotColor(p.slot) }} />
                <AnimalFace animal={p.animal} size={36} />
                <span className="nm">{name}{me ? t('res.youTag') : ''}<small>{state || role}・{t('sum.len', { n: p.len })}{hud.mode === 'versus' ? `・${t('game.kills')} ${p.kills}` : ''}</small></span>
                <span className="sc" aria-label={t('sum.score')}>{p.score}</span>
              </Row>
            );
          })}
        </div>
      </ScrollArea>
      {kind === 'online' && <div className="side-chat"><ChatBox height="100%" /></div>}
    </aside>
  );
}

/* ---------- 結算 overlay ---------- */
function ResultModal({ res, kind, hud }) {
  useLang();
  const d = useSave();
  const room = useOnline(s => s.room);
  const slot = hud.slot;
  const isHost = !!(room && room.you && room.you.host);
  const by = s => res.players.find(p => p.slot === s);
  const nameOf = s => { const p = by(s); return p ? playerName(p.kind, p.animal, p.name) : ''; };
  const mins = Math.floor(res.time / 60), secs = Math.floor(res.time % 60);
  let title, sub = '';
  if (res.mode === 'challenge') { title = res.newRecord ? t('res.newRecord') : t('res.over'); sub = t('res.overSub'); }
  else if (res.winner < 0) { title = t('res.draw'); sub = res.reason === 'time' ? t('res.draw.time') : t('res.draw.last'); }
  else {
    const tag = res.reason === 'time' ? '.time' : '.last';
    if (res.winner === slot) { title = t('res.youWin'); sub = t('res.youWin' + tag); }
    else if (slot < 0) { title = t('res.wins', { name: nameOf(res.winner) }); sub = t('res.wins' + tag); }
    else { title = t('res.lose'); sub = t('res.lose' + tag, { name: nameOf(res.winner) }); }
  }
  const stat = kind === 'online' ? d.statOnline : d.statSolo;
  const again = kind === 'online' ? rematch : restart;
  const againDisabled = kind === 'online' && !isHost;
  const second = kind === 'online' ? { label: t('res.room'), run: backToRoom } : { label: t('res.setup'), run: backToSetup };
  const homeRun = () => (kind === 'online' ? leaveToHome() : go('home'));
  return (
    <Modal label={t('res.aria')} width={640} dismissible={false} maskClass="result-ov" className="result-modal">
      <div data-result-share="" className="share-result-scope">
      <div className="res-title">{title}</div>
      {sub && <div className="res-sub">{sub}</div>}
      {res.mode === 'challenge' ? (
        <div className="stat-boxes">
          <div className="stat-box"><b>{res.scores[0] ?? 0}</b><span>{t('res.score')}</span></div>
          <div className="stat-box"><b>{res.lens[0] ?? 0}</b><span>{t('res.len')}</span></div>
          <div className="stat-box"><b>{res.level}</b><span>{t('res.level')}</span></div>
          <div className="stat-box"><b>{res.best || '—'}</b><span>{t('res.best')}</span></div>
        </div>
      ) : (
        <div className="res-table">
          {res.ranking.map((s, i) => {
            const p = by(s);
            if (!p) return null;
            return (
              <div key={s} className={'res-row ' + (s === slot ? 'me ' : '') + (s === res.winner ? 'win' : '')}>
                <b>{i + 1}</b><AnimalFace animal={p.animal} size={36} />
                <span className="nm">{nameOf(s)}{s === slot ? t('res.youTag') : ''}<small>{t('res.len2', { n: res.lens[s] ?? 0 })}・{t('game.kills')} {res.kills[s] ?? 0}</small></span>
                <b>{t('res.pts', { n: res.scores[s] ?? 0 })}</b>
              </div>
            );
          })}
        </div>
      )}
      <Text small muted>{t(res.mode === 'challenge' ? 'res.time' : 'res.total', { m: mins, s: secs })}{kind !== 'challenge' && slot >= 0 && stat.play > 0 ? '　' + t('res.stat', { w: stat.win, p: stat.play }) : ''}</Text>
      {againDisabled && <Text small muted>{slot < 0 ? t('res.hostOnly') : t('res.hostNote')}</Text>}
      </div>
      <div className="res-actions">
        <Button size="lg" disabled={againDisabled} onClick={again} autoFocus={!againDisabled}>{t('res.again')}</Button>
        <Button size="lg" variant="sky" onClick={second.run}>{second.label}</Button>
        <Button size="lg" variant="paper" onClick={homeRun}>{t('res.home')}</Button>
      </div>
    </Modal>
  );
}

/* ---------- 暫停選單 ---------- */
function PauseModal({ kind, onClose }) {
  useLang();
  return (
    <Modal title={t('menu.pause')} onClose={onClose} width={460}>
      <Text>{kind === 'online' ? t('menu.pause.online') : t('menu.pause.solo')}</Text>
      <Stack>
        <Button size="lg" onClick={onClose}>{t('menu.continue')}</Button>
        {kind !== 'online' && <Button variant="orange" onClick={() => { onClose(); restart(); }}>{t('menu.restart')}</Button>}
        {kind === 'online'
          ? <Button variant="danger" onClick={() => { onClose(); confirmBox({ title: t('leave.title'), text: t('leave.text'), okLabel: t('leave.ok'), danger: true, onOk: leaveRoom }); }}>{t('menu.leaveRoom')}</Button>
          : null}
        <Button variant="paper" onClick={() => { onClose(); if (kind === 'online') confirmBox({ title: t('leave.title'), text: t('leave.text'), okLabel: t('leave.ok'), danger: true, onOk: leaveToHome }); else go('home'); }}>{t('menu.home')}</Button>
      </Stack>
    </Modal>
  );
}

/* ================= 對戰畫面 ================= */
export function GamePage({ params }) {
  const lang = useLang();
  const kind = params.kind;
  const save = useSave();
  const modalOpen = useModalOpen();
  const startInfo = useOnline(s => s.startInfo);
  const mySlot = useOnline(s => s.mySlot);
  const unread = useOnline(s => s.unread);
  const vp = useViewport();
  const canvasRef = useRef(null), mainRef = useRef(null), stickRef = useRef(null), knobRef = useRef(null);
  const hudRef = useRef(null), boostRef = useRef(null);
  const [session, setSession] = useState(null);
  const [error, setError] = useState(false);
  const [result, setResult] = useState(null);
  const [menu, setMenu] = useState(false);
  const wide = vp.w >= 860 && vp.w > vp.h;
  const [sideOpen, setSideOpen] = useState(wide);
  const [mainW, setMainW] = useState(vp.w);
  const [sawTouch, setSawTouch] = useState(false);
  const stick = useRef(null);
  const sideWasWide = useRef(wide);

  /* 建立場次與 3D 舞台；畫面卸載時一定釋放 */
  useEffect(() => {
    const canvas = canvasRef.current;
    const opts = kind === 'online' ? { kind, startInfo, slot: mySlot } : { kind, setup: params };
    const s = new GameSession(opts);
    let world = null;
    try {
      world = new World3D(canvas, s.view, { quality: getSave().quality, selfSlot: s.slot, nameOf: p => playerName(p.kind, p.animal, p.name) });
      world.youTag = t('res.youTag');
      for (const p of s.view.players) { const sn = world.snakes.get(p.slot); if (sn) { sn.tagKey = ''; world.setTag(sn, p, p.slot === s.slot); } }
    } catch (e) {
      console.error(e); setError(true); s.dispose(); return undefined;
    }
    s.world = world;
    s.paused = () => kind !== 'online' && isModalOpen();
    s.onResult = res => setResult(res);
    sound.music('menu');
    setResult(null); setMenu(false);
    if (new URLSearchParams(location.search).has('e2e')) window.__snake3d = { session: s };
    setSession(s);
    const unbridge = kind === 'online' ? attachBridge({ onSnap: snap => s.onSnap(snap), onResult: (r, p) => s.onServerResult(r, p) }) : null;
    let raf = 0, last = performance.now();
    const loop = now => {
      const dt = Math.min(0.1, (now - last) / 1000); last = now;
      const alpha = s.tick(dt);
      s.frame(dt, alpha);
      const pointer = stick.current;
      if (pointer?.mode === 'finger' && !isModalOpen()) {
        const angle = world.aimAtScreen(pointer.x, pointer.y, s.me());
        if (angle != null) s.aim(angle);
      }
      raf = requestAnimationFrame(loop);
    };
    raf = requestAnimationFrame(loop);
    const ro = new ResizeObserver(() => {
      const m = mainRef.current;
      if (!m) return;
      setMainW(m.clientWidth);
      world.resize(m.clientWidth, m.clientHeight);
    });
    ro.observe(mainRef.current);
    world.resize(mainRef.current.clientWidth, mainRef.current.clientHeight);
    const lost = e => { e.preventDefault(); setError(true); };
    canvas.addEventListener('webglcontextlost', lost);
    return () => {
      cancelAnimationFrame(raf); ro.disconnect(); canvas.removeEventListener('webglcontextlost', lost);
      if (unbridge) unbridge();
      s.dispose(); setSession(null);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params.runId, kind === 'online' ? startInfo : null]);

  /* 設定同步到場次 */
  useEffect(() => { if (session) session.zoom = save.zoom; }, [session, save.zoom]);
  useEffect(() => { if (session) { session.resetInput(); } }, [session, modalOpen]);
  useEffect(() => {
    if (session && session.world) session.world.relabel(session.view, session.slot, t('res.youTag'));
  }, [session, lang]);

  /* 鍵盤 */
  useEffect(() => {
    if (!session) return undefined;
    const typing = e => e.target && (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA');
    const game = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space', 'ShiftLeft', 'ShiftRight']);
    const down = e => {
      if (typing(e) || isModalOpen()) return;
      if (e.code === 'Escape') { setMenu(true); return; }
      if (!game.has(e.code)) return;
      e.preventDefault();
      session.keyDown(e.code);
    };
    const up = e => { if (game.has(e.code)) session.keyUp(e.code); };
    const blur = () => session.resetInput();
    window.addEventListener('keydown', down); window.addEventListener('keyup', up); window.addEventListener('blur', blur);
    document.addEventListener('visibilitychange', blur);
    return () => { window.removeEventListener('keydown', down); window.removeEventListener('keyup', up); window.removeEventListener('blur', blur); document.removeEventListener('visibilitychange', blur); };
  }, [session]);

  const hud = useSyncExternalStore(
    fn => (session ? session.subscribe(fn) : () => {}),
    () => (session ? session.getHud() : null)
  );

  /* 版面：寬螢幕資訊欄在左側固定；窄螢幕是疊上去的抽屜（旋轉／縮放時依新版型重設） */
  useEffect(() => {
    if (sideWasWide.current !== wide) { sideWasWide.current = wide; setSideOpen(wide); }
  }, [wide]);
  useEffect(() => { if (sideOpen) clearUnread(); }, [sideOpen, unread]);

  const short = vp.w > vp.h && vp.h < 500;
  const compact = mainW < 700 || short;
  const me = hud && hud.me;
  const canSteer = !!hud && hud.slot >= 0 && hud.phase !== 'over' && !!me && me.alive;
  const touchOn = canSteer && (save.controlMode === 'joystick' || sawTouch || coarse());
  const portrait = vp.h > vp.w;
  useEffect(() => {
    if (!session || !mainRef.current) return;
    const measure = () => {
      const node = mainRef.current;
      if (!node) return;
      const main = node.getBoundingClientRect();
      const header = hudRef.current?.getBoundingClientRect();
      session.safeTop = Math.max(64, header ? header.bottom - main.top + 12 : 64);
      const controls = [boostRef.current, save.controlMode === 'joystick' ? stickRef.current : null];
      session.safeBottom = touchOn && portrait ? Math.max(0, ...controls.map(node => {
        const rect = node?.getBoundingClientRect();
        return rect?.height > 0 ? main.bottom - rect.top + 8 : 0;
      })) : 0;
    };
    const observer = new ResizeObserver(measure);
    if (hudRef.current) observer.observe(hudRef.current);
    measure();
    return () => observer.disconnect();
  }, [session, hud?.slot, compact, short, touchOn, portrait, mainW, vp.h, save.controlMode]);
  useEffect(() => { if (!touchOn && session) { session.setBoostTouch(false); } }, [touchOn, session]);

  /* ---------- 搖桿 ---------- */
  const resetPointer = useCallback(() => {
    stick.current = null;
    if (knobRef.current) knobRef.current.style.transform = 'translate(0,0)';
  }, []);
  useEffect(() => {
    resetPointer();
  }, [resetPointer, modalOpen, canSteer, save.controlMode, save.stickSide, mainW, vp.h, sideOpen]);
  useEffect(() => {
    window.addEventListener('blur', resetPointer);
    document.addEventListener('visibilitychange', resetPointer);
    return () => {
      window.removeEventListener('blur', resetPointer);
      document.removeEventListener('visibilitychange', resetPointer);
    };
  }, [resetPointer]);

  const padDown = e => {
    if (e.pointerType !== 'mouse') setSawTouch(true);
    if (!canSteer || stick.current || isModalOpen()) return;
    if (e.pointerType === 'mouse' && save.controlMode !== 'joystick') return;
    let ox = e.clientX, oy = e.clientY, max = 56;
    if (save.controlMode === 'joystick') {
      const r = stickRef.current.getBoundingClientRect();
      ox = r.left + r.width / 2; oy = r.top + r.height / 2;
      if (Math.hypot(e.clientX - ox, e.clientY - oy) > r.width / 2 + 18) return;
      max = Math.max(0, (stickRef.current.clientWidth - knobRef.current.offsetWidth) / 2);
    }
    stick.current = { id: e.pointerId, ox, oy, max, mode: save.controlMode, x: e.clientX, y: e.clientY };
    try { e.currentTarget.setPointerCapture(e.pointerId); } catch { /* 合成事件沒有可捕捉的指標 */ }
    padMove(e);
  };
  const padMove = e => {
    const s = stick.current;
    if (!s || s.id !== e.pointerId || isModalOpen()) return;
    s.x = e.clientX; s.y = e.clientY;
    if (s.mode === 'finger') {
      const angle = session?.world.aimAtScreen(s.x, s.y, session.me());
      if (angle != null) session.aim(angle);
      return;
    }
    const dx = e.clientX - s.ox, dy = e.clientY - s.oy, L = Math.hypot(dx, dy), max = s.max;
    const k = L > max ? max / L : 1;
    if (knobRef.current) knobRef.current.style.transform = `translate(${dx * k}px,${dy * k}px)`;
    if (L > 8 && session) session.aim(Math.atan2(dy / TILT, dx));
  };
  const padUp = e => {
    const s = stick.current;
    if (!s || s.id !== e.pointerId) return;
    resetPointer();
  };

  const openMenu = () => { if (!result) setMenu(true); };

  if (error) {
    return (
      <main className="screen"><div className="screen-body"><div className="screen-inner">
        <div className="panel panel-paper"><Heading level={3}>WebGL</Heading><Text>{t('net.error')}</Text><Row><Button onClick={() => go('home')}>{t('res.home')}</Button></Row></div>
      </div></div></main>
    );
  }

  const energy = me ? Math.max(0, Math.min(1, me.energy)) : 0;
  const challenge = hud && hud.mode === 'challenge';
  const remain = hud && hud.timeLimit > 0 ? Math.max(0, hud.timeLimit - hud.time) : null;
  const sideVisible = sideOpen && !!hud;
  const slotName = p => playerName(p.kind, p.animal, p.name);
  const watchP = hud && hud.watch >= 0 ? hud.players.find(p => p.slot === hud.watch) : null;
  const dead = hud && hud.slot >= 0 && me && !me.alive && hud.phase !== 'over';
  const spectating = hud && hud.slot < 0 && hud.phase !== 'over';

  return (
    <div className={'game' + (short ? ' game-short' : '')} style={{ '--sky-bg': '#cdeefa' }}>
      {sideVisible && wide && <SidePanel hud={hud} kind={kind} onClose={() => setSideOpen(false)} onMenu={openMenu} onWatch={s => session.setWatch(s)} />}
      <div className="game-main" ref={mainRef}>
        <canvas ref={canvasRef} className="game-canvas" tabIndex={-1} role="img" aria-label={t('game.stage')} />
        {hud && hud.slot >= 0 && <div className="touch-pad" style={{ position: 'absolute', inset: 0, zIndex: 7, touchAction: 'none' }}
          onPointerDown={padDown} onPointerMove={padMove} onPointerUp={padUp} onPointerCancel={padUp} onLostPointerCapture={padUp} />}
        <div className="touch">
          <div className={'stick ' + save.stickSide} ref={stickRef} style={{ display: touchOn && save.controlMode === 'joystick' ? 'block' : 'none' }} role="img" aria-label={t('stick.aria')}><div className="stick-knob" ref={knobRef} /></div>
        </div>
        {touchOn && (
          <div ref={boostRef} className={'boost ' + (save.stickSide === 'right' ? 'left' : 'right')}>
            <div className={'boost-ring ' + (me.tired ? 'tired' : '')} style={{ '--e': energy }} />
            <PressHold className="boost-core" label={t('boost.aria')} onChange={on => { if (session) session.setBoostTouch(on); if (on) sound.sfx('boost'); }}>
              <Icon name="flash" size={34} /><span>{t('boost.label')}</span>
            </PressHold>
          </div>
        )}

        {(!sideVisible || !wide) && hud && (
          <div className="hud-tl">
            <IconButton icon={kind === 'online' ? 'close' : 'pause'} label={kind === 'online' ? t('side.exitOnline') : t('side.exitPause')} onClick={openMenu} variant="paper" />
            <IconButton icon="info" label={t('side.open')} onClick={() => { setSideOpen(true); clearUnread(); }} badge={unread > 0 && kind === 'online' ? (unread > 9 ? '9+' : unread) : null} />
          </div>
        )}
        {hud && (
          <div ref={hudRef} className={'hud-top ' + (compact ? 'compact ' : '') + (short ? 'short' : '')} role="status" aria-label={t('game.time')}>
            <span className="hud-pill hud-timer">{challenge ? clock(hud.time) : remain != null ? clock(remain) : clock(hud.time)}</span>
            {challenge && me ? (
              <>
                <span className="hud-pill hud-chip">{me.score} {t('chip.score')}</span>
                <span className="hud-pill hud-chip">{t('chip.len')} {me.len}</span>
                <span className="hud-pill hud-chip">{t('chip.lv')} {1 + Math.floor(me.eaten / 5)}</span>
              </>
            ) : (compact ? chipList(hud, 1) : hud.players).map(p => {
              const pick = canWatchHud(hud) && p.alive && p.slot !== hud.slot;
              const Chip2 = pick ? 'button' : 'span';
              return (
                <Chip2 key={p.slot} type={pick ? 'button' : undefined} className={'hud-pill hud-chip ' + (p.slot === hud.slot ? 'me ' : '') + (!p.alive ? 'out ' : '') + (pick ? 'pick ' : '') + (pick && hud.watch === p.slot ? 'fol' : '')}
                  aria-pressed={pick ? hud.watch === p.slot : undefined} onClick={pick ? () => session.setWatch(p.slot) : undefined}>
                  <i style={{ background: slotColor(p.slot) }} />
                  <span className="hud-name" title={slotName(p)}>{compact && p.slot === hud.slot ? t('res.youTag') : slotName(p) + (p.slot === hud.slot ? t('res.youTag') : '')}</span>
                  <span className="hud-score">{p.score}</span>
                </Chip2>
              );
            })}
          </div>
        )}
        {session && save.minimap && <MiniMap session={session} size={short ? 72 : compact ? 84 : 132} />}
        {hud && hud.flash && hud.phase !== 'over' && <FlashCount key={hud.flash.id} flash={hud.flash} />}
        {hud && me && me.alive && !touchOn && (
          <div className="energy" role="progressbar" aria-label={t('game.energy')} aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(energy * 100)}>
            <Icon name="flash" size={22} className={'energy-ico ' + (me.tired ? 'tired' : '')} />
            <div className="energy-bar"><div className={'energy-fill ' + (me.tired ? 'tired' : '')} style={{ width: energy * 100 + '%' }} /></div>
          </div>
        )}
        {(dead || spectating) && !result && (
          <div className="ghost" role="status">
            <span>{dead ? t('ghost.out') + '　' : ''}{watchP ? t('ghost.watching', { name: slotName(watchP) }) : ''}</span>
            <Button size="sm" variant="sky" onClick={() => session.nextWatch()}>{t('ghost.next')}</Button>
            {kind !== 'online' && <Button size="sm" variant={hud.ff ? 'orange' : 'paper'} onClick={() => session.setFast(!hud.ff)}>{hud.ff ? t('ghost.normal') : t('ghost.ff')}</Button>}
          </div>
        )}
        {sideVisible && !wide && <SidePanel hud={hud} kind={kind} drawer onClose={() => setSideOpen(false)} onMenu={openMenu} onWatch={s => session.setWatch(s)} />}
      </div>
      {menu && <PauseModal kind={kind} onClose={() => setMenu(false)} />}
      {result && hud && <ResultModal res={result} kind={kind} hud={hud} />}
    </div>
  );
}

/** 出局或觀戰時可以點資訊欄的玩家切換視角（自己還活著時鏡頭固定跟自己） */
function canWatchHud(hud) {
  if (hud.phase === 'over') return false;
  if (hud.slot < 0) return true;
  const me = hud.players.find(p => p.slot === hud.slot);
  return !!me && !me.alive;
}

/** 窄版只顯示自己與分數最高的幾位，其餘在資訊欄 */
function chipList(hud, otherCount) {
  const me = hud.players.filter(p => p.slot === hud.slot);
  const others = hud.players.filter(p => p.slot !== hud.slot).sort((a, b) => b.score - a.score).slice(0, otherCount);
  return [...me, ...others];
}

function FlashCount({ flash }) {
  const [on, setOn] = useState(true);
  useEffect(() => { if (flash.text !== 'go') return undefined; const id = setTimeout(() => setOn(false), 700); return () => clearTimeout(id); }, [flash]);
  if (!on) return null;
  return <div className="count" aria-hidden="true">{flash.text === 'go' ? t('game.go') : flash.text}</div>;
}

void app; void online; void Chip; void useMemo;
