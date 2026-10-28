import { useEffect, useState } from 'react';
import { AnimalFace, Button, Chip, Heading, Modal, Panel, Row, Segmented, Spinner, Stack, Stepper, Text, TextField } from '../ui/kit.jsx';
import { Screen } from '../shell.jsx';
import { go, myName } from '../app.js';
import { net } from '../net.js';
import { config } from '../config.js';
import { update, useSave } from '../store.js';
import { obstacleName, roomName, speedName, t, useLang } from '../i18n.js';
import { online, reasonText, sendJoin, sendProfile, useOnline } from '../online.js';
import { Profile } from './menus.jsx';
import { useStatus } from './hooks.js';

function ProfileModal({ onClose }) {
  useSave(); useLang();
  return (
    <Modal title={t('prof.title')} onClose={() => { sendProfile(); onClose(); }} width={560} footer={<Button onClick={() => { sendProfile(); onClose(); }}>{t('common.done')}</Button>}>
      <Profile />
    </Modal>
  );
}

function MakeRoomModal({ onClose }) {
  useLang();
  const d = useSave();
  const [name, setName] = useState('');
  const [max, setMax] = useState(4);
  const create = () => {
    net.send({ type: 'create', name: myName(), animal: d.animal, roomName: name.trim() || undefined, max });
    onClose();
  };
  return (
    <Modal title={t('mk.title')} onClose={onClose} width={520}
      footer={<><Button variant="paper" onClick={onClose}>{t('common.cancel')}</Button><Button onClick={create}>{t('mk.ok')}</Button></>}>
      <Text muted>{t('mk.hint')}</Text>
      <TextField label={t('mk.name')} value={name} maxLength={20} placeholder={t('mk.placeholder', { name: myName() })} onChange={setName} onEnter={create} />
      <Stepper label={t('mk.max')} value={max} min={2} max={8} onChange={setMax} format={v => t('mk.n', { n: v })} />
    </Modal>
  );
}

function InviteCard() {
  useLang();
  const inv = useOnline(s => s.invite);
  const status = useStatus();
  const [name, setName] = useState(myName());
  if (!inv) return null;
  const leave = () => { online.set({ invite: null }); if (typeof history !== 'undefined') history.replaceState(null, '', location.pathname); };
  if (inv.error) {
    return (
      <Panel tone="paper" className="invite-card" role="alert">
        <Heading level={3}>{t('inv.bad')}</Heading>
        <Text>{reasonText(inv.error) || t('inv.badFallback')}</Text>
        <Row><Button onClick={leave}>{t('inv.back')}</Button></Row>
      </Panel>
    );
  }
  if (!inv.info) {
    return (
      <Panel className="invite-card" aria-live="polite">
        <Row><Spinner /><Heading level={3}>{t('inv.checking')}</Heading></Row>
        <Text muted>{status === 'open' ? t('inv.checkingWait') : t('inv.waiting')}</Text>
        <Row><Button variant="paper" onClick={leave}>{t('inv.decline')}</Button></Row>
      </Panel>
    );
  }
  const i = inv.info;
  const why = i.phase !== 'lobby' ? t('inv.started') : i.full ? t('inv.full') : '';
  const spectate = i.willSpectate;
  return (
    <Panel tone="sky" className="invite-card" aria-label={t('inv.aria')}>
      <Heading level={3}>{t('inv.title', { name: roomName(i.name) })}</Heading>
      <Row><Chip tone={spectate ? 'orange' : 'green'}>{spectate ? t('inv.asSpec', { why }) : t('inv.asPlayer')}</Chip><Chip>{t('room.count', { p: i.players, m: i.max })}</Chip></Row>
      {spectate && <Text small muted>{t('inv.specNote')}</Text>}
      <TextField label={t('prof.nick')} value={name} maxLength={12} onChange={setName} placeholder={t('prof.placeholder', { n: 12 })} />
      <Text small muted>{t('inv.confirm')}</Text>
      <Row>
        <Button disabled={inv.joining || status !== 'open'} onClick={() => { update({ nickname: name.trim() }); sendJoin(name.trim() || myName()); }}>{spectate ? t('inv.joinSpec') : t('inv.joinPlay')}</Button>
        <Button variant="paper" onClick={leave}>{t('inv.decline')}</Button>
      </Row>
    </Panel>
  );
}

export function LobbyPage() {
  useLang();
  const d = useSave();
  const rooms = useOnline(s => s.rooms);
  const onlineCount = useOnline(s => s.onlineCount);
  const notice = useOnline(s => s.notice);
  const inv = useOnline(s => s.invite);
  const status = useStatus();
  const [prof, setProf] = useState(false);
  const [make, setMake] = useState(false);
  const noServer = config.status !== 'ok';
  useEffect(() => { net.send({ type: 'lobbySub' }); }, [status]);

  const connected = status === 'open';
  const join = (room, as) => net.send({ type: 'join', room: room.id, as, name: myName(), animal: d.animal });
  const label = { open: t('conn.open'), waking: t('conn.waking'), offline: t('conn.offline'), retrying: t('conn.retry'), idle: t('conn.idle'), connecting: t('conn.ing'), unset: t('conn.unset') }[status] || t('conn.idle');
  const tone = connected ? 'green' : status === 'offline' || status === 'unset' ? 'red' : 'orange';

  if (noServer) {
    return (
      <Screen title={t('lobby.title')} onBack={() => go('home')}>
        <Panel>
          <Heading level={3}>{config.status === 'invalid' ? t('conn.bad') : t('lobby.noserver')}</Heading>
          <Text>{config.status === 'invalid' ? t(config.error) : t('lobby.noserver.p')}</Text>
          <Row><Button onClick={() => go('solo')}>{t('lobby.solo')}</Button><Button variant="paper" onClick={() => go('home')}>{t('common.back')}</Button></Row>
        </Panel>
      </Screen>
    );
  }

  return (
    <Screen title={t('lobby.title')} onBack={() => go('home')} width={960} actions={<Chip tone={tone}>{label}</Chip>}>
      {notice && (
        <Panel tone="cream" role="status">
          <Text>{notice}</Text>
          <Row><Button size="sm" variant="paper" onClick={() => online.set({ notice: '' })}>{t('lobby.gotit')}</Button></Row>
        </Panel>
      )}
      {inv && <InviteCard />}
      <Panel>
        <Row justify="space-between">
          <Row><AnimalFace animal={d.animal} size={48} /><Stack gap={0}><Text small muted>{t('lobby.mine')}</Text><Heading level={3}>{myName()}</Heading></Stack></Row>
          <Button size="sm" variant="paper" onClick={() => setProf(true)}>{t('lobby.edit')}</Button>
        </Row>
      </Panel>
      <Row>
        <Button size="lg" disabled={!connected} onClick={() => net.send({ type: 'quick', name: myName(), animal: d.animal })}>{t('lobby.quick')}</Button>
        <Button size="lg" variant="sky" disabled={!connected} onClick={() => setMake(true)}>{t('lobby.create')}</Button>
      </Row>
      <Panel>
        <Row justify="space-between">
          <Heading level={3}>{t('lobby.rooms')}</Heading>
          {Number.isInteger(onlineCount) && <Chip tone="green">{t('lobby.onlineCount', { n: onlineCount })}</Chip>}
        </Row>
        {!connected ? <Text muted>{t('lobby.after')}</Text> : rooms.length === 0 ? <Text muted>{t('lobby.none')}</Text> : (
          <div className="room-list">
            {rooms.map(r => (
              <div key={r.id} className="room-card">
                <div className="info">
                  <Row gap={6}><b>{roomName(r.name)}</b><Chip>{t('room.code', { id: r.id })}</Chip><Chip tone={r.phase === 'lobby' ? 'green' : 'orange'}>{t('phase.' + r.phase)}</Chip></Row>
                  <Row gap={6}>
                    <Chip>{t('room.count', { p: r.players, m: r.max })}</Chip>
                    {r.spectators > 0 && <Chip>{t('room.spec', { n: r.spectators })}</Chip>}
                    <Chip>{t('room.host', { name: r.host })}</Chip>
                    <Chip>{t('room.speed', { v: speedName(r.settings.speed) })}</Chip>
                    <Chip>{t('room.rocks', { v: obstacleName(r.settings.obstacles) })}</Chip>
                  </Row>
                </div>
                <Button size="sm" disabled={!r.joinable} onClick={() => join(r, 'player')}>{t('room.join')}</Button>
                <Button size="sm" variant="sky" onClick={() => join(r, 'spectator')}>{t('room.watch')}</Button>
              </div>
            ))}
          </div>
        )}
      </Panel>
      {prof && <ProfileModal onClose={() => setProf(false)} />}
      {make && <MakeRoomModal onClose={() => setMake(false)} />}
    </Screen>
  );
}

