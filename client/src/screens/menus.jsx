import { useMemo, useState } from 'react';
import AI from '@shared/ai.js';
import Rules from '@shared/rules.js';
import { AnimalFace, Button, Chip, Heading, Icon, Keycap, Panel, RadioCard, Row, Segmented, Select, Stack, Stepper, Text, TextField } from '../ui/kit.jsx';
import { Screen } from '../shell.jsx';
import { go, myName } from '../app.js';
import { getBest, update, useSave } from '../store.js';
import { ANIMAL_IDS, animalName, levelHint, levelName, obstacleName, speedName, t, themeName, timeName, useLang } from '../i18n.js';
import { pickAnimals, startChallenge, startSolo } from '../flow.js';
import { SLOT_COLORS, THEME_OPTIONS } from '../data.js';
import { lobbyClick, lobbyUrl } from '../analytics.js';

export function Profile({ onChange }) {
  const d = useSave();
  useLang();
  return (
    <Stack>
      <TextField label={t('prof.nick')} value={d.nickname} maxLength={12} placeholder={t('prof.placeholder', { n: 12 })}
        onChange={v => { update({ nickname: v }); onChange && onChange(); }} />
      <div className="field-col">
        <div className="field-label"><span>{t('prof.animal')}</span></div>
        <div className="animal-grid" role="radiogroup" aria-label={t('prof.pick')}>
          {ANIMAL_IDS.map(id => (
            <RadioCard key={id} checked={d.animal === id} onClick={() => { update({ animal: id }); onChange && onChange(); }}>
              <AnimalFace animal={id} size={52} />{animalName(id)}
            </RadioCard>
          ))}
        </div>
      </div>
      <Text small muted>{t('common.you')}：{myName()}</Text>
    </Stack>
  );
}

export function RulesPanel({ speed, obstacles, timeLimit, theme, hideTime, onChange }) {
  useLang();
  return (
    <div className="rule-grid">
      <Segmented label={t('rule.speed')} ariaLabel={t('rule.speed.aria')} value={speed} onChange={v => onChange('speed', v)}
        options={Rules.SPEED_ORDER.map(v => ({ value: v, label: speedName(v) }))} />
      <Segmented label={t('rule.obs')} value={obstacles} onChange={v => onChange('obstacles', v)}
        options={Rules.OBSTACLE_ORDER.map(v => ({ value: v, label: obstacleName(v) }))} />
      {!hideTime && (
        <Segmented label={t('rule.time')} ariaLabel={t('rule.time.aria')} value={timeLimit} onChange={v => onChange('timeLimit', v)}
          options={Rules.TIME_LIMITS.map(v => ({ value: v, label: timeName(v) }))} />
      )}
      <Select label={t('rule.theme')} ariaLabel={t('rule.theme.aria')} value={theme} onChange={v => onChange('theme', v)}
        options={THEME_OPTIONS.map(v => ({ value: v, label: themeName(v) }))} />
    </div>
  );
}

/* ================= 首頁 ================= */
export function HomePage() {
  const d = useSave();
  useLang();
  const rec = [];
  const best = getBest(d.challenge.speed, d.challenge.obstacles);
  if (best > 0) rec.push(t('home.rec.best', { n: best }));
  if (d.statSolo.play > 0) rec.push(t('home.rec.solo', { w: d.statSolo.win, p: d.statSolo.play }));
  if (d.statOnline.play > 0) rec.push(t('home.rec.online', { w: d.statOnline.win, p: d.statOnline.play }));
  const title = Array.from(t('app.title'));
  return (
    <Screen width={640}>
      <div className="home">
        <h1 className="hd hd-1" aria-label={t('app.title')}>
          {title.map((c, i) => <span key={i} aria-hidden="true" style={{ color: SLOT_COLORS[i % 7], WebkitTextStroke: '2px #4a3320', paintOrder: 'stroke fill' }}>{c}</span>)}
        </h1>
        <a className="lobby-link" href={lobbyUrl()} onClick={lobbyClick}><Icon name="back" size={18} /><span>{t('home.lobby')}</span></a>
        <Text className="home-sub">{t('home.sub')}</Text>
        <Row justify="center" gap={6}>{ANIMAL_IDS.map(id => <AnimalFace key={id} animal={id} size={56} />)}</Row>
        <nav className="home-menu" aria-label={t('home.menu')}>
          <Button size="lg" onClick={() => go('challenge')} className="btn-wide">{t('menu.challenge')}</Button>
          <Button size="lg" variant="orange" onClick={() => go('solo')}>{t('menu.solo')}</Button>
          <Button size="lg" variant="sky" onClick={() => go('lobby')}>{t('menu.online')}</Button>
          <Button size="lg" variant="paper" onClick={() => go('help')} className="btn-wide">{t('menu.help')}</Button>
        </nav>
        {rec.length > 0 && <Text className="home-rec">{t('home.rec', { list: rec.join(t('home.rec.sep')) })}</Text>}
      </div>
    </Screen>
  );
}

/* ================= 怎麼玩 ================= */
export function HelpPage() {
  useLang();
  return (
    <Screen title={t('help.title')} onBack={() => go('home')} width={1000}>
      <div className="grid2">
        <Panel><Heading level={3}>{t('help.goal')}</Heading><Text>{t('help.goal.p')}</Text><Text muted small>{t('help.goal.n')}</Text></Panel>
        <Panel>
          <Heading level={3}>{t('help.ctl')}</Heading>
          <Text>{t('help.ctl.touch')}</Text>
          <Text>{t('help.ctl.mouse')}<Keycap>W</Keycap><Keycap>A</Keycap><Keycap>S</Keycap><Keycap>D</Keycap> / {t('help.ctl.arrow')}</Text>
          <Text muted small>{t('help.ctl.diag')}</Text>
          <Text>{t('help.ctl.boost')}<Keycap>{t('help.ctl.space')}</Keycap> / <Keycap>Shift</Keycap></Text>
          <Text muted small>{t('help.ctl.energy')}</Text>
        </Panel>
        <Panel><Heading level={3}>{t('help.fruit')}</Heading><ul className="help-list">{[1, 2, 3, 4].map(i => <li key={i}>{t('help.fruit.' + i)}</li>)}</ul></Panel>
        <Panel><Heading level={3}>{t('help.out')}</Heading><ul className="help-list">{[1, 2, 3, 4, 5].map(i => <li key={i}>{t('help.out.' + i)}</li>)}</ul></Panel>
      </div>
      <Panel>
        <Heading level={3}>{t('help.modes')}</Heading>
        {['challenge', 'solo', 'online'].map(m => <Text key={m}><b>{t('help.mode.' + m)}</b>{t('help.mode.' + m + '.d')}</Text>)}
      </Panel>
      <Row justify="center">
        <Button size="lg" onClick={startChallenge}>{t('help.cta')}</Button>
        <Button size="lg" variant="paper" onClick={() => go('home')}>{t('help.home')}</Button>
      </Row>
    </Screen>
  );
}

/* ================= 單人挑戰設定 ================= */
export function ChallengePage() {
  const d = useSave();
  useLang();
  const ch = d.challenge;
  const best = getBest(ch.speed, ch.obstacles);
  return (
    <Screen title={t('ch.title')} onBack={() => go('home')} width={1000}
      actions={<Button onClick={startChallenge}>{t('common.start')}</Button>}>
      <div className="grid2">
        <Panel><Heading level={3}>{t('ch.you')}</Heading><Profile /></Panel>
        <Stack gap={14}>
          <Panel>
            <Heading level={3}>{t('ch.play')}</Heading>
            <Text>{t('ch.play.p')}</Text>
            <Row><Text>{t('ch.best')}</Text><Chip tone="orange">{best > 0 ? best : '—'}</Chip></Row>
          </Panel>
          <Panel>
            <Heading level={3}>{t('ch.rules')}</Heading>
            <RulesPanel hideTime speed={ch.speed} obstacles={ch.obstacles} theme={ch.theme} onChange={(k, v) => update({ challenge: { ...ch, [k]: v } })} />
          </Panel>
        </Stack>
      </div>
    </Screen>
  );
}

/* ================= 對戰電腦設定 ================= */
export function SoloPage() {
  const d = useSave();
  useLang();
  const so = d.solo;
  const [detail, setDetail] = useState(false);
  const animals = useMemo(() => pickAnimals(7, d.animal), [d.animal]);
  const levels = AI.LEVEL_ORDER;
  const levelOptions = levels.map(v => ({ value: v, label: levelName(v) }));
  const same = so.levels.every(l => l === so.levels[0]);
  const setLevels = list => update({ solo: { ...so, levels: list } });
  const setCount = n => {
    const l = so.levels.slice();
    while (l.length < n) l.push(l.length ? l[l.length - 1] : 'normal');
    setLevels(l.slice(0, n));
  };
  return (
    <Screen title={t('solo.title')} onBack={() => go('home')} width={1000} actions={<Button variant="orange" onClick={startSolo}>{t('common.start')}</Button>}>
      <div className="grid2">
        <Stack gap={14}>
          <Panel><Heading level={3}>{t('ch.you')}</Heading><Profile /></Panel>
          <Panel>
            <Row justify="space-between"><Heading level={3}>{t('solo.opps')}</Heading><Chip tone="sky">{1 + so.levels.length <= 4 ? t('solo.mid') : t('solo.big')}</Chip></Row>
            <Row gap={6}><AnimalFace animal={d.animal} size={44} />{so.levels.map((_, i) => <AnimalFace key={i} animal={animals[i]} size={44} />)}</Row>
            <Stepper label={t('solo.num')} value={so.levels.length} min={1} max={7} onChange={setCount} />
            <Segmented label={t('solo.diff')} value={same ? so.levels[0] : null} options={levelOptions} onChange={v => setLevels(so.levels.map(() => v))} />
            {same && <Text small muted>{levelHint(so.levels[0])}</Text>}
            <Button variant="paper" size="sm" onClick={() => setDetail(v => !v)}>{detail ? t('solo.hide') : t('solo.detail')}</Button>
            {detail && so.levels.map((lv, i) => (
              <div key={i} className="opp">
                <AnimalFace animal={animals[i]} size={36} />
                <b>{t('solo.cpuN', { n: i + 1 })}</b>
                <Segmented size="sm" ariaLabel={t('solo.cpuLvl', { n: i + 1 })} value={lv} options={levelOptions}
                  onChange={v => setLevels(so.levels.map((x, k) => (k === i ? v : x)))} />
              </div>
            ))}
          </Panel>
        </Stack>
        <Stack gap={14}>
          <Panel>
            <Heading level={3}>{t('ch.rules')}</Heading>
            <RulesPanel speed={so.speed} obstacles={so.obstacles} timeLimit={so.timeLimit} theme={so.theme} onChange={(k, v) => update({ solo: { ...so, [k]: v } })} />
          </Panel>
          <Panel><Heading level={3}>{t('solo.win')}</Heading><Text>{t('solo.win.p')}</Text></Panel>
        </Stack>
      </div>
    </Screen>
  );
}
