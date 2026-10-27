import { useState } from 'react';
import AI from '@shared/ai.js';
import Rules from '@shared/rules.js';
import { AnimalFace, Button, Chip, Heading, IconButton, Modal, Panel, Row, ScrollArea, Segmented, Select, Stack, Stepper, Text, TextField } from '../ui/kit.jsx';
import { Screen } from '../shell.jsx';
import { app, confirmBox, toast } from '../app.js';
import { net } from '../net.js';
import { animalName, levelName, obstacleName, playerName, roomName, speedName, t, themeName, timeName, useLang } from '../i18n.js';
import { inviteLink, isHost, leaveRoom, online, rematch, useOnline } from '../online.js';
import * as sound from '../sound.js';
import { useStatus } from './hooks.js';

/* ---------- 聊天室（房間與對戰側欄共用） ---------- */
export function ChatBox({ height }) {
  useLang();
  const chat = useOnline(s => s.chat);
  const [text, setText] = useState('');
  const send = msg => { const v = (msg ?? text).trim(); if (!v) return; net.send({ type: 'chat', text: v }); setText(''); };
  return (
    <div className="chat" style={height ? { height } : { minHeight: 260 }} role="group" aria-label={t('chat.title')}>
      <ScrollArea className="chat-log" stickBottom label={t('chat.aria')}>
        <div className="chat-log-inner" role="log" aria-live="polite">
          {chat.map((m, i) => (
            <div key={i} className={'chat-msg ' + (m.role === 'sys' ? 'sys' : '')}>
              {m.role === 'sys' ? m.text ? serverText(m.text) : '' : <><b>{m.name}</b>{m.role === 'spectator' && <span className="spec">{t('chat.spec')}</span>}：{m.text}</>}
            </div>
          ))}
        </div>
      </ScrollArea>
      <div className="chat-quick">
        {[1, 2, 3, 4, 5].map(i => <Button key={i} size="sm" variant="paper" quiet onClick={() => send(t('chat.q' + i))}>{t('chat.q' + i)}</Button>)}
      </div>
      <div className="chat-send">
        <TextField value={text} onChange={setText} maxLength={60} placeholder={t('chat.ph')} ariaLabel={t('chat.aria')} onEnter={() => send()} enterKeyHint="send" />
        <IconButton icon="send" label={t('chat.send')} onClick={() => send()} variant="green" size={48} />
      </div>
    </div>
  );
}
import { serverMessage } from '../i18n.js';
const serverText = serverMessage;

/* ---------- 邀請視窗 ---------- */
function InviteModal({ onClose }) {
  useLang();
  const room = useOnline(s => s.room);
  const [role, setRole] = useState('player');
  if (!room) return null;
  const list = room.invites || [];
  const copy = async link => {
    try { await navigator.clipboard.writeText(link); toast(t('invm.copied'), 2.4); } catch { toast(t('invm.copyFail'), 3.2); }
  };
  const share = async link => {
    if (navigator.share) { try { await navigator.share({ title: t('app.title'), text: t('invm.shareText'), url: link }); } catch { /* 取消分享 */ } } else copy(link);
  };
  return (
    <Modal title={t('invm.title')} onClose={onClose} width={620} footer={<Button onClick={onClose}>{t('common.done')}</Button>}>
      <Text muted>{t('invm.p')}</Text>
      <Segmented label={t('invm.role')} value={role} onChange={setRole} options={[{ value: 'player', label: t('invm.rolePlayer') }, { value: 'spectator', label: t('invm.roleSpec') }]} />
      <Row>
        <Button onClick={() => net.send({ type: 'invite', role })}>{t('invm.make')}</Button>
        <Button variant="danger" disabled={!list.length} onClick={() => confirmBox({ title: t('invm.revokeTitle'), text: t('invm.revokeText'), okLabel: t('invm.revokeOk'), danger: true, onOk: () => net.send({ type: 'revoke' }) })}>{t('invm.revokeAll')}</Button>
      </Row>
      {list.length === 0 ? <Text muted>{t('invm.none')}</Text> : list.map(i => {
        const link = inviteLink(i.token);
        return (
          <Panel key={i.token} tone="cream" className="stack">
            <Chip tone={i.role === 'spectator' ? 'orange' : 'green'}>{i.role === 'spectator' ? t('invm.spec') : t('invm.player')}</Chip>
            <div className="link-box">{link}</div>
            <Row><Button size="sm" icon="copy" onClick={() => copy(link)}>{t('invm.copy')}</Button><Button size="sm" variant="sky" icon="share" onClick={() => share(link)}>{t('invm.share')}</Button></Row>
          </Panel>
        );
      })}
    </Modal>
  );
}

function Seat({ s, room, host }) {
  const me = room.you.seat === s.i;
  const hostSeat = room.hostSeat === s.i;
  const canEdit = host && room.phase === 'lobby';
  const levels = AI.LEVEL_ORDER.map(v => ({ value: v, label: levelName(v) }));
  if (s.kind === 'empty') {
    return (
      <div className="seat empty">
        <Text muted>{t('seat.empty')}</Text>
        {canEdit && <Button size="sm" variant="orange" onClick={() => net.send({ type: 'addai', level: online.get().aiLevel || 'normal' })}>{t('seat.addAi')}</Button>}
      </div>
    );
  }
  const name = playerName(s.kind, s.animal, s.name);
  return (
    <div className={'seat ' + (me ? 'mine' : '')}>
      <AnimalFace animal={s.animal} size={52} />
      <div className="who">
        <span>{name}</span>
        <Row gap={4}>
          {hostSeat && <Chip tone="orange" icon="crown">{t('seat.host')}</Chip>}
          {me && <Chip tone="sky">{t('seat.you')}</Chip>}
          {s.kind === 'ai' && <Chip>{t('seat.cpu')}・{levelName(s.level)}</Chip>}
          {s.kind === 'human' && !hostSeat && <Chip tone={s.ready ? 'green' : 'paper'}>{s.ready ? t('seat.ready') : t('seat.notReady')}</Chip>}
          {s.kind === 'human' && !s.connected && <Chip tone="red">{t('seat.off')}</Chip>}
        </Row>
        {s.kind === 'ai' && canEdit && (
          <Select ariaLabel={t('seat.lvl', { name })} value={s.level} options={levels} onChange={v => net.send({ type: 'aiLevel', seat: s.i, level: v })} />
        )}
      </div>
      {canEdit && s.kind === 'ai' && <IconButton icon="trash" label={t('seat.remove', { name })} variant="paper" size={44} onClick={() => net.send({ type: 'removeai', seat: s.i })} />}
      {canEdit && s.kind === 'human' && !me && (
        <IconButton icon="logout" label={t('seat.kick', { name })} variant="danger" size={44}
          onClick={() => confirmBox({ title: t('seat.kickTitle', { name }), text: t('seat.kickText'), okLabel: t('seat.kickOk'), danger: true, onOk: () => net.send({ type: 'kick', seat: s.i }) })} />
      )}
    </div>
  );
}

export function RoomPage() {
  useLang();
  const room = useOnline(s => s.room);
  const status = useStatus();
  const [inviteOpen, setInviteOpen] = useState(false);
  if (!room) {
    return <Screen title={t('room.title')}><Panel><Row><Text>{t('room.entering')}</Text></Row></Panel></Screen>;
  }
  const host = isHost(room);
  const player = room.you.role === 'player';
  const lobby = room.phase === 'lobby';
  const st = room.settings;
  const patch = p => net.send({ type: 'settings', patch: p });
  const filled = room.seats.filter(s => s.kind !== 'empty').length;
  const full = filled >= room.max;
  const mySeat = player ? room.seats[room.you.seat] : null;
  const confirmLeave = () => confirmBox({ title: t('leave.title'), text: t('leave.text'), okLabel: t('leave.ok'), danger: true, onOk: leaveRoom });

  let hint = '';
  if (room.phase === 'playing') hint = t('room.playingNow');
  else if (room.phase === 'finished') hint = host ? t('room.endedHost') : t('room.endedGuest');
  else if (host && filled < 2) hint = t('room.fewPlayers');
  else if (host && !room.canStart) hint = t('room.waitReady');

  return (
    <Screen title={roomName(room.name)} width={1100} onBack={confirmLeave} backLabel={t('room.leave')}
      actions={<Chip className="code-box">{room.id}</Chip>}>
      <Row>
        <Chip tone="sky">{host ? t('you.host') : player ? t('you.player') : t('you.spec')}</Chip>
        <Chip>{t('room.count', { p: filled, m: room.max })}</Chip>
        <Chip tone={lobby ? 'green' : 'orange'}>{t('phase.' + room.phase)}</Chip>
        {status !== 'open' && <Chip tone="red">{t('conn.retry')}</Chip>}
      </Row>
      <div className="room-main">
        <Stack gap={14}>
          <Panel>
            <Row justify="space-between"><Heading level={3}>{t('room.seats')}</Heading>{host && <Button size="sm" variant="sky" icon="link" onClick={() => setInviteOpen(true)}>{t('room.invite')}</Button>}</Row>
            <div className="seat-list">{room.seats.map(s => <Seat key={s.i} s={s} room={room} host={host} />)}</div>
          </Panel>
          <Panel>
            <Heading level={3}>{t('spec.title')}</Heading>
            {room.spectators.length === 0 ? <Text muted small>{t('spec.none')}</Text> : <Row gap={6}>{room.spectators.map(p => <Chip key={p.pid} icon="eye">{p.name}</Chip>)}</Row>}
            {!host && lobby && player && <Row><Button size="sm" variant="paper" onClick={() => net.send({ type: 'watch' })}>{t('room.toSpec')}</Button></Row>}
            {!player && <Row>
              <Button size="sm" disabled={!lobby || full} onClick={() => net.send({ type: 'sit' })}>{t('room.joinPlay')}</Button>
              {full && lobby && <Text small muted>{t('room.seatsFull')}</Text>}
              {!lobby && <Text small muted>{t('room.noSwitch')}</Text>}
            </Row>}
          </Panel>
          <Panel>
            <Heading level={3}>{t('room.rules')}</Heading>
            {!host && <Text small muted>{t('room.hostOnly')}</Text>}
            <div className="rule-grid">
              <Segmented label={t('rule.speed')} value={st.speed} onChange={v => patch({ speed: v })} options={Rules.SPEED_ORDER.map(v => ({ value: v, label: speedName(v), disabled: !host || !lobby }))} />
              <Segmented label={t('rule.obs')} value={st.obstacles} onChange={v => patch({ obstacles: v })} options={Rules.OBSTACLE_ORDER.map(v => ({ value: v, label: obstacleName(v), disabled: !host || !lobby }))} />
              <Segmented label={t('rule.time')} value={st.timeLimit} onChange={v => patch({ timeLimit: v })} options={Rules.TIME_LIMITS.map(v => ({ value: v, label: timeName(v), disabled: !host || !lobby }))} />
              <Select label={t('rule.theme')} value={st.theme} disabled={!host || !lobby} onChange={v => patch({ theme: v })} options={[-1, 0, 1, 2, 3, 4, 5].map(v => ({ value: v, label: themeName(v) }))} />
              {host && lobby && <Stepper label={t('room.max')} value={room.max} min={2} max={8} onChange={v => patch({ max: v })} format={v => t('mk.n', { n: v })} />}
            </div>
          </Panel>
          <Panel tone="cream">
            {hint && <Text>{hint}</Text>}
            <Row>
              {host && lobby && <Button size="lg" disabled={!room.canStart} onClick={() => { sound.sfx('go'); net.send({ type: 'start' }); }}>{t('room.startBtn')}</Button>}
              {host && room.phase === 'finished' && <Button size="lg" onClick={rematch}>{t('res.again')}</Button>}
              {!host && player && lobby && <Button size="lg" variant={mySeat && mySeat.ready ? 'paper' : 'primary'} onClick={() => net.send({ type: 'ready', value: !(mySeat && mySeat.ready) })}>{mySeat && mySeat.ready ? t('room.unready') : t('room.imReady')}</Button>}
              <Button variant="danger" onClick={confirmLeave}>{t('room.leave')}</Button>
            </Row>
          </Panel>
        </Stack>
        <Panel className="stack">
          <Heading level={3}>{t('room.chat')}</Heading>
          <ChatBox height={420} />
        </Panel>
      </div>
      {inviteOpen && <InviteModal onClose={() => setInviteOpen(false)} />}
    </Screen>
  );
}
void app; void animalName;
