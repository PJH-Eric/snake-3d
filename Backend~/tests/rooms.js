/* ===== tests/rooms.js — 房間生命週期、邀請、席位權限（不開網路，用假時鐘） ===== */
'use strict';
const assert = require('assert');
const { createHub, cleanName } = require('../lib/rooms.js');
const Rules = require('../public/js/rules.js');

let passed = 0;
const failed = [];
function test(name, fn) {
  try { fn(); passed++; console.log('  ✓ ' + name); } catch (e) { failed.push(name); console.log('  ✗ ' + name + '\n      ' + (e && e.stack ? e.stack.split('\n').slice(0, 3).join('\n      ') : e)); }
}

function setup() {
  let t = 1000;
  const inbox = new Map();
  const hub = createHub({
    now: () => t,
    emit(key, msg) { if (!inbox.has(key)) inbox.set(key, []); inbox.get(key).push(msg); }
  });
  const api = {
    hub, inbox,
    advance(ms) { t += ms; hub.tick(); },
    run(ms) { for (let n = 0; n < ms; n += 16) { t += 16; hub.tick(); } },
    get now() { return t; },
    join(key, name) { hub.connect(key, { name: name || key, animal: 'cat' }); return key; },
    last(key, type) { const l = (inbox.get(key) || []).filter(m => m.type === type); return l[l.length - 1]; },
    all(key, type) { return (inbox.get(key) || []).filter(m => m.type === type); },
    clear() { inbox.clear(); },
    room(key) { const m = api.last(key, 'room'); return m ? m.room : null; }
  };
  return api;
}

console.log('\n暱稱與聊天消毒');
test('暱稱去掉控制字元與 HTML 符號、限制 16 個字（房名 20）', () => {
  assert.strictEqual(cleanName('<b>小明</b>\n'), 'b小明/b');
  assert.strictEqual(cleanName('一二三四五六七八九十一二三四五六七八'), '一二三四五六七八九十一二三四五六');
  assert.strictEqual(cleanName('一二三四五六七八九十一二三四五六七八九十一二', '預設', 20), '一二三四五六七八九十一二三四五六七八九十');
  assert.strictEqual(cleanName('   ', '預設'), '預設');
});

console.log('\n建立／加入／席位');
test('建立房間：建立者是房主，大廳列表看得到', () => {
  const x = setup(); x.join('aaaaaaaa1', 'A'); x.join('bbbbbbbb2', 'B');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat', roomName: '測試房', max: 4 });
  const v = x.room('aaaaaaaa1');
  assert(v && v.you.host && v.you.role === 'player' && v.name === '測試房');
  assert.strictEqual(x.hub.listRooms().length, 1);
  x.advance(20);
  assert(x.last('bbbbbbbb2', 'rooms').rooms.some(r => r.name === '測試房'));
});
test('快速加入：有房就進、沒房就自動開一間', () => {
  const x = setup(); x.join('aaaaaaaa1'); x.join('bbbbbbbb2');
  x.hub.handle('aaaaaaaa1', { type: 'quick', name: 'A', animal: 'cat' });
  assert(x.room('aaaaaaaa1').you.host, '沒房時應該自己開一間');
  x.hub.handle('bbbbbbbb2', { type: 'quick', name: 'B', animal: 'dog' });
  assert.strictEqual(x.room('bbbbbbbb2').id, x.room('aaaaaaaa1').id);
  assert.strictEqual(x.hub.listRooms().length, 1);
});
test('同一隻動物不會重複：搶到同一隻會自動換一隻', () => {
  const x = setup(); x.join('aaaaaaaa1'); x.join('bbbbbbbb2');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat' });
  x.hub.handle('bbbbbbbb2', { type: 'quick', name: 'B', animal: 'cat' });
  const seats = x.room('bbbbbbbb2').seats.filter(s => s.kind === 'human');
  assert.strictEqual(new Set(seats.map(s => s.animal)).size, 2);
});
test('席位滿了以公開方式加入會變觀戰者，不會默默當玩家', () => {
  const x = setup(); x.join('aaaaaaaa1'); x.join('bbbbbbbb2'); x.join('cccccccc3');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat', max: 2 });
  const id = x.room('aaaaaaaa1').id;
  x.hub.handle('bbbbbbbb2', { type: 'join', room: id, as: 'player', name: 'B' });
  x.hub.handle('cccccccc3', { type: 'join', room: id, as: 'player', name: 'C' });
  assert.strictEqual(x.room('cccccccc3').you.role, 'spectator');
  assert(x.last('cccccccc3', 'joined').note.includes('席位已滿'));
});
test('房主可以調整設定，人數上限不能低於已坐的席位', () => {
  const x = setup(); x.join('aaaaaaaa1'); x.join('bbbbbbbb2');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat', max: 4 });
  x.hub.handle('bbbbbbbb2', { type: 'quick', name: 'B', animal: 'dog' });
  x.hub.handle('aaaaaaaa1', { type: 'settings', patch: { max: 6, timeLimit: 300, speed: 'fast', theme: 3, obstacles: 'many' } });
  const v = x.room('aaaaaaaa1');
  assert.strictEqual(v.max, 6); assert.strictEqual(v.settings.timeLimit, 300); assert.strictEqual(v.settings.speed, 'fast');
  assert.strictEqual(v.settings.obstacles, 'many'); assert.strictEqual(v.settings.theme, 3);
  x.hub.handle('aaaaaaaa1', { type: 'settings', patch: { speed: 'warp', obstacles: 9, timeLimit: 77 } });
  assert.strictEqual(x.room('aaaaaaaa1').settings.speed, 'fast', '不合法的值要被擋掉');
  assert.strictEqual(x.room('aaaaaaaa1').settings.timeLimit, 300);
  x.join('cccccccc3'); x.hub.handle('cccccccc3', { type: 'quick', name: 'C', animal: 'fox' });
  x.hub.handle('aaaaaaaa1', { type: 'settings', patch: { max: 2 } });
  assert.strictEqual(x.last('aaaaaaaa1', 'error').code, 'max');
  assert.strictEqual(x.room('aaaaaaaa1').max, 6);
  x.hub.handle('bbbbbbbb2', { type: 'settings', patch: { timeLimit: 0 } });
  assert.strictEqual(x.room('aaaaaaaa1').settings.timeLimit, 300, '非房主不能改設定');
});

console.log('\n邀請連結');
function withInvite(opts) {
  const x = setup(); x.join('aaaaaaaa1', 'A'); x.join('bbbbbbbb2', 'B');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat', max: (opts && opts.max) || 4 });
  const id = x.room('aaaaaaaa1').id;
  x.hub.handle('aaaaaaaa1', { type: 'invite', role: 'player' });
  x.hub.handle('aaaaaaaa1', { type: 'invite', role: 'spectator' });
  const inv = x.all('aaaaaaaa1', 'invite');
  return { x, id, playerTok: inv[0].token, specTok: inv[1].token };
}
test('邀請資訊：有效 token 看得到房名與角色；亂寫的 token 是無效', () => {
  const { x, id, playerTok, specTok } = withInvite();
  x.hub.handle('bbbbbbbb2', { type: 'inviteInfo', room: id, token: playerTok });
  assert(x.last('bbbbbbbb2', 'inviteInfo').ok && x.last('bbbbbbbb2', 'inviteInfo').role === 'player');
  x.hub.handle('bbbbbbbb2', { type: 'inviteInfo', room: id, token: specTok });
  assert.strictEqual(x.last('bbbbbbbb2', 'inviteInfo').role, 'spectator');
  x.hub.handle('bbbbbbbb2', { type: 'inviteInfo', room: id, token: 'zzzzzzzzzzzz' });
  assert.strictEqual(x.last('bbbbbbbb2', 'inviteInfo').reason, 'invalid');
  x.hub.handle('bbbbbbbb2', { type: 'inviteInfo', room: 'QQQQ', token: playerTok });
  assert.strictEqual(x.last('bbbbbbbb2', 'inviteInfo').reason, 'invalid');
});
test('token 決定角色：觀戰邀請就算硬塞 as=player 也只能觀戰；暱稱會被消毒', () => {
  const { x, id, specTok } = withInvite();
  x.hub.handle('bbbbbbbb2', { type: 'join', room: id, token: specTok, as: 'player', name: '<img src=x>壞人' });
  const v = x.room('bbbbbbbb2');
  assert.strictEqual(v.you.role, 'spectator');
  assert(!v.spectators[0].name.includes('<'));
});
test('玩家邀請：確認暱稱後才入座；改暱稱不影響角色', () => {
  const { x, id, playerTok } = withInvite();
  x.hub.handle('bbbbbbbb2', { type: 'join', room: id, token: playerTok, as: 'spectator', name: '小客人' });
  const v = x.room('bbbbbbbb2');
  assert.strictEqual(v.you.role, 'player');
  assert(v.seats.some(s => s.name === '小客人'));
});
test('房主撤銷後，舊連結全部失效並回報「已撤銷」', () => {
  const { x, id, playerTok } = withInvite();
  x.hub.handle('aaaaaaaa1', { type: 'revoke' });
  x.hub.handle('bbbbbbbb2', { type: 'inviteInfo', room: id, token: playerTok });
  assert.strictEqual(x.last('bbbbbbbb2', 'inviteInfo').reason, 'revoked');
  x.hub.handle('bbbbbbbb2', { type: 'join', room: id, token: playerTok, name: 'B' });
  assert.strictEqual(x.last('bbbbbbbb2', 'joinFailed').reason, 'revoked');
  assert(!x.room('bbbbbbbb2') || !x.room('bbbbbbbb2').id, '撤銷後不該進得了房間');
});
test('邀請超過 24 小時過期', () => {
  const { x, id, playerTok } = withInvite();
  x.advance(24 * 3600 * 1000 + 5);
  x.hub.handle('bbbbbbbb2', { type: 'inviteInfo', room: id, token: playerTok });
  assert.strictEqual(x.last('bbbbbbbb2', 'inviteInfo').reason, 'expired');
});
test('非房主不能產生邀請', () => {
  const { x, id, playerTok } = withInvite();
  x.hub.handle('bbbbbbbb2', { type: 'join', room: id, token: playerTok, name: 'B' });
  const before = x.all('bbbbbbbb2', 'invite').length;
  x.hub.handle('bbbbbbbb2', { type: 'invite', role: 'player' });
  assert.strictEqual(x.all('bbbbbbbb2', 'invite').length, before);
});
test('玩家席位已滿時，玩家邀請轉為觀戰並說明；對局中也是', () => {
  const { x, id, playerTok } = withInvite({ max: 2 });
  x.join('cccccccc3', 'C'); x.join('dddddddd4', 'D');
  x.hub.handle('bbbbbbbb2', { type: 'join', room: id, token: playerTok, name: 'B' });
  x.hub.handle('cccccccc3', { type: 'join', room: id, token: playerTok, name: 'C' });
  assert.strictEqual(x.room('cccccccc3').you.role, 'spectator');
  x.hub.handle('bbbbbbbb2', { type: 'ready', value: true });
  x.hub.handle('aaaaaaaa1', { type: 'start' });
  assert.strictEqual(x.room('aaaaaaaa1').phase, 'playing');
  x.hub.handle('dddddddd4', { type: 'join', room: id, token: playerTok, name: 'D' });
  assert.strictEqual(x.room('dddddddd4').you.role, 'spectator');
});

console.log('\n開局條件');
test('至少 2 個席位、其他真人都按準備好才能開始', () => {
  const x = setup(); x.join('aaaaaaaa1'); x.join('bbbbbbbb2');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat' });
  x.hub.handle('aaaaaaaa1', { type: 'start' });
  assert.strictEqual(x.last('aaaaaaaa1', 'error').code, 'few');
  x.hub.handle('bbbbbbbb2', { type: 'quick', name: 'B', animal: 'dog' });
  x.hub.handle('aaaaaaaa1', { type: 'start' });
  assert.strictEqual(x.last('aaaaaaaa1', 'error').code, 'notready');
  x.hub.handle('bbbbbbbb2', { type: 'ready', value: true });
  assert(x.room('aaaaaaaa1').canStart);
  x.hub.handle('aaaaaaaa1', { type: 'start' });
  assert.strictEqual(x.room('aaaaaaaa1').phase, 'playing');
  assert(x.last('aaaaaaaa1', 'start') && x.last('bbbbbbbb2', 'start'));
});
test('電腦席位：房主可加、可調難度、可移除；單人加電腦就能開', () => {
  const x = setup(); x.join('aaaaaaaa1');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat' });
  x.hub.handle('aaaaaaaa1', { type: 'addai', level: 'hard' });
  x.hub.handle('aaaaaaaa1', { type: 'addai', level: 'toddler' });
  let v = x.room('aaaaaaaa1');
  assert.deepStrictEqual(v.seats.filter(s => s.kind === 'ai').map(s => s.level), ['hard', 'toddler']);
  x.hub.handle('aaaaaaaa1', { type: 'aiLevel', seat: 1, level: 'easy' });
  x.hub.handle('aaaaaaaa1', { type: 'removeai', seat: 2 });
  v = x.room('aaaaaaaa1');
  assert.deepStrictEqual(v.seats.filter(s => s.kind === 'ai').map(s => s.level), ['easy']);
  x.hub.handle('aaaaaaaa1', { type: 'start' });
  assert.strictEqual(x.room('aaaaaaaa1').phase, 'playing');
});

console.log('\n對局');
function playing() {
  const x = setup(); x.join('aaaaaaaa1', 'A'); x.join('bbbbbbbb2', 'B'); x.join('cccccccc3', 'C');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat' });
  const id = x.room('aaaaaaaa1').id;
  x.hub.handle('bbbbbbbb2', { type: 'join', room: id, name: 'B', animal: 'dog' });
  x.hub.handle('cccccccc3', { type: 'join', room: id, as: 'spectator', name: 'C' });
  x.hub.handle('bbbbbbbb2', { type: 'ready', value: true });
  x.hub.handle('aaaaaaaa1', { type: 'start' });
  return { x, id };
}
test('伺服器權威：倒數後輸入才生效，快照持續廣播給玩家與觀戰者', () => {
  const { x } = playing();
  x.run(1000);
  const early = x.last('aaaaaaaa1', 'snap').s.p[0];
  x.run(1600);                                      /* 倒數期間蛇不動 */
  assert.deepStrictEqual(x.last('aaaaaaaa1', 'snap').s.p[0][10], early[10], '倒數期間蛇動了');
  x.run(300);
  const snapsBefore = x.all('cccccccc3', 'snap').length;
  const p0 = x.last('aaaaaaaa1', 'snap').s.p[0];
  const ang0 = p0[8] / 100;
  const want = ang0 + 1.2;
  x.hub.handle('aaaaaaaa1', { type: 'input', a: want });
  x.run(700);
  const p1 = x.last('aaaaaaaa1', 'snap').s.p[0];
  const turned = Math.atan2(Math.sin(p1[8] / 100 - ang0), Math.cos(p1[8] / 100 - ang0));
  assert(turned > 0.8, '玩家沒有轉向 ' + turned);
  assert.notDeepStrictEqual(p1[10].slice(0, 2), p0[10].slice(0, 2), '玩家沒有移動');
  assert(x.all('cccccccc3', 'snap').length > snapsBefore, '觀戰者沒收到快照');
  assert.strictEqual(early[1], 0);
});
test('不合法的方向輸入被忽略（字串、NaN）', () => {
  const { x } = playing();
  x.run(3200);
  const ta = () => x.last('aaaaaaaa1', 'snap').s.p[0][9];
  const before = ta();
  x.hub.handle('aaaaaaaa1', { type: 'input', a: 'left' });
  x.hub.handle('aaaaaaaa1', { type: 'input', a: null });
  x.hub.handle('aaaaaaaa1', { type: 'input', dir: 'U' });
  x.run(100);
  assert.strictEqual(ta(), before);
});
test('觀戰者的輸入與加速會被忽略', () => {
  const { x } = playing();
  x.run(3300);
  const dirs = () => x.last('cccccccc3', 'snap').s.p.map(a => a[9] + ':' + a[4]);
  const before = dirs();
  x.hub.handle('cccccccc3', { type: 'input', a: 1 });
  x.hub.handle('cccccccc3', { type: 'input', a: -2 });
  x.hub.handle('cccccccc3', { type: 'boost', on: true });
  x.run(300);
  assert.deepStrictEqual(dirs(), before);
});
test('玩家加速由伺服器處理，快照會標示加速中並消耗能量', () => {
  const { x } = playing();
  x.run(3300);
  x.hub.handle('aaaaaaaa1', { type: 'boost', on: true });
  x.run(300);
  const p = x.last('aaaaaaaa1', 'snap').s.p[0];
  assert.strictEqual(p[4], 1); assert(p[6] < 100, '能量沒有減少');
  x.hub.handle('aaaaaaaa1', { type: 'boost', on: false });
  x.run(100);
  assert.strictEqual(x.last('aaaaaaaa1', 'snap').s.p[0][4], 0);
});
test('中途進來的觀戰者拿到完整果實清單，之後的差異與其他人一致', () => {
  const { x, id } = playing();
  x.run(5000);
  x.join('dddddddd4', 'D');
  x.hub.handle('dddddddd4', { type: 'join', room: id, as: 'spectator', name: 'D' });
  x.run(2000);
  const R = require('../public/js/rules.js');
  const build = key => {
    const v = R.viewFromStart(x.last(key, 'start').game);
    const snaps = x.all(key, 'snap');
    const startIdx = x.all(key, 'start').length;
    void startIdx;
    for (const m of snaps) R.applySnapshot(v, m.s, 0);
    return v.foods.map(f => f.id).sort((a, b) => a - b);
  };
  assert.deepStrictEqual(build('dddddddd4'), build('cccccccc3'));
});
test('倒數與出局事件會夾在快照裡', () => {
  const x = setup(); x.join('aaaaaaaa1', 'A');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat' });
  x.hub.handle('aaaaaaaa1', { type: 'addai', level: 'toddler' });
  x.hub.handle('aaaaaaaa1', { type: 'start' });
  const types = new Set();
  for (let i = 0; i < 4000 && x.room('aaaaaaaa1').phase === 'playing'; i++) { x.advance(16); }
  for (const m of x.all('aaaaaaaa1', 'snap')) for (const e of (m.s.e || [])) types.add(e.t);
  assert(types.has('count') && types.has('go') && types.has('die') && types.has('over'), '事件：' + Array.from(types).join(','));
});
test('對局打完：發結算、房間進入 finished，房主可以再來一局', () => {
  const x = setup(); x.join('aaaaaaaa1', 'A');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat' });
  x.hub.handle('aaaaaaaa1', { type: 'settings', patch: { timeLimit: 120 } });
  x.hub.handle('aaaaaaaa1', { type: 'addai', level: 'toddler' });
  x.hub.handle('aaaaaaaa1', { type: 'start' });
  for (let i = 0; i < 20000 && x.room('aaaaaaaa1').phase === 'playing'; i++) x.advance(16);
  assert.strictEqual(x.room('aaaaaaaa1').phase, 'finished');
  const res = x.last('aaaaaaaa1', 'result');
  assert(res && res.result && res.players.length === 2);
  x.hub.handle('aaaaaaaa1', { type: 'rematch' });
  assert.strictEqual(x.room('aaaaaaaa1').phase, 'lobby');
});

console.log('\n房間關閉規則（以實體玩家為準）');
test('真人全部離開 → 房間立刻關閉，觀戰者收到 closed，邀請失效', () => {
  const { x, id, playerTok, specTok } = withInvite();
  x.join('cccccccc3', 'C');
  x.hub.handle('cccccccc3', { type: 'join', room: id, token: specTok, name: 'C' });
  x.hub.handle('aaaaaaaa1', { type: 'leave' });
  assert.strictEqual(x.hub.listRooms().length, 0);
  assert(x.last('cccccccc3', 'closed'), '觀戰者沒收到 closed');
  assert.strictEqual(x.room('cccccccc3'), null);
  x.hub.handle('bbbbbbbb2', { type: 'inviteInfo', room: id, token: playerTok });
  assert.strictEqual(x.last('bbbbbbbb2', 'inviteInfo').reason, 'closed');
  x.hub.handle('bbbbbbbb2', { type: 'join', room: id, token: playerTok, name: 'B' });
  assert.strictEqual(x.last('bbbbbbbb2', 'joinFailed').reason, 'closed');
});
test('只剩電腦與觀戰者，房間一樣關閉（電腦席位不算實體玩家）', () => {
  const x = setup(); x.join('aaaaaaaa1'); x.join('cccccccc3');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat' });
  const id = x.room('aaaaaaaa1').id;
  x.hub.handle('aaaaaaaa1', { type: 'addai', level: 'easy' });
  x.hub.handle('cccccccc3', { type: 'join', room: id, as: 'spectator', name: 'C' });
  x.hub.handle('aaaaaaaa1', { type: 'leave' });
  assert.strictEqual(x.hub.listRooms().length, 0);
  assert(x.last('cccccccc3', 'closed'));
});
test('對局進行中真人全走光 → 關閉並停止對局，重新連線不會讓房間復活', () => {
  const { x, id } = playing();
  x.run(500);
  x.hub.handle('aaaaaaaa1', { type: 'leave' });
  assert.strictEqual(x.hub.listRooms().length, 1, '還有一位真人，房間不該關');
  x.hub.handle('bbbbbbbb2', { type: 'leave' });
  assert.strictEqual(x.hub.listRooms().length, 0);
  const snaps = x.all('cccccccc3', 'snap').length;
  x.run(2000);
  assert.strictEqual(x.all('cccccccc3', 'snap').length, snaps, '關閉後不該再廣播快照');
  x.hub.connect('aaaaaaaa1', { name: 'A' });
  assert.strictEqual(x.last('aaaaaaaa1', 'welcome').room, null);
  x.hub.handle('aaaaaaaa1', { type: 'join', room: id, name: 'A' });
  assert.strictEqual(x.last('aaaaaaaa1', 'joinFailed').reason, 'closed');
});
test('房主離開 → 房主換給下一位真人；沒有真人才關閉', () => {
  const x = setup(); x.join('aaaaaaaa1', 'A'); x.join('bbbbbbbb2', 'B');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat' });
  x.hub.handle('bbbbbbbb2', { type: 'quick', name: 'B', animal: 'dog' });
  x.hub.handle('aaaaaaaa1', { type: 'leave' });
  assert(x.room('bbbbbbbb2').you.host);
  assert.strictEqual(x.hub.listRooms().length, 1);
});
test('斷線：30 秒內重連回到原位，超過 30 秒就離場並走關閉規則', () => {
  const { x, id } = playing();
  x.advance(500);
  x.hub.disconnect('bbbbbbbb2');
  x.advance(10000);
  assert.strictEqual(x.hub.listRooms().length, 1);
  x.hub.connect('bbbbbbbb2', { name: 'B' });
  const w = x.last('bbbbbbbb2', 'welcome');
  assert(w.room && w.room.id === id && w.room.you.role === 'player', '重連沒回到原席位');
  assert(x.last('bbbbbbbb2', 'start'), '重連後沒補送對局資料');
  x.hub.disconnect('bbbbbbbb2');
  x.advance(31000);
  const st = x.room('aaaaaaaa1');
  assert.strictEqual(st.seats.filter(s => s.kind === 'human').length, 1, '逾時後席位應該釋出');
  x.hub.disconnect('aaaaaaaa1');
  x.advance(31000);
  assert.strictEqual(x.hub.listRooms().length, 0, '全部真人逾時離場後房間應關閉');
  assert(x.last('cccccccc3', 'closed'));
});
test('被踢的人回到大廳，且不能用公開方式再進來', () => {
  const x = setup(); x.join('aaaaaaaa1', 'A'); x.join('bbbbbbbb2', 'B');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat' });
  const id = x.room('aaaaaaaa1').id;
  x.hub.handle('bbbbbbbb2', { type: 'join', room: id, name: 'B' });
  x.hub.handle('aaaaaaaa1', { type: 'kick', seat: 1 });
  assert(x.last('bbbbbbbb2', 'kicked'));
  assert.strictEqual(x.room('bbbbbbbb2'), null);
  x.hub.handle('bbbbbbbb2', { type: 'join', room: id, name: 'B' });
  assert.strictEqual(x.last('bbbbbbbb2', 'error').code, 'banned');
});
test('觀戰席上限 20 人', () => {
  const x = setup(); x.join('aaaaaaaa1');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat' });
  const id = x.room('aaaaaaaa1').id;
  for (let i = 0; i < 22; i++) { const k = 'spectator' + String(i).padStart(2, '0'); x.join(k, 'S' + i); x.hub.handle(k, { type: 'join', room: id, as: 'spectator', name: 'S' + i }); }
  assert.strictEqual(x.room('aaaaaaaa1').spectators.length, 20);
  assert.strictEqual(x.last('spectator21', 'error').text, '觀戰席已滿');
});
test('聊天：消毒、限速、玩家與觀戰者都能發言', () => {
  const x = setup(); x.join('aaaaaaaa1', 'A'); x.join('cccccccc3', 'C');
  x.hub.handle('aaaaaaaa1', { type: 'create', name: 'A', animal: 'cat' });
  const id = x.room('aaaaaaaa1').id;
  x.hub.handle('cccccccc3', { type: 'join', room: id, as: 'spectator', name: 'C' });
  x.hub.handle('cccccccc3', { type: 'chat', text: '<b>哈囉</b>' });
  const m = x.last('aaaaaaaa1', 'chat').m;
  assert.strictEqual(m.text, 'b哈囉/b'); assert.strictEqual(m.role, 'spectator');
  const n = x.all('aaaaaaaa1', 'chat').length;
  x.hub.handle('cccccccc3', { type: 'chat', text: '連發' });
  assert.strictEqual(x.all('aaaaaaaa1', 'chat').length, n, '連發沒有被限速');
  x.advance(600);
  x.hub.handle('aaaaaaaa1', { type: 'chat', text: 'a'.repeat(200) });
  assert.strictEqual(x.last('cccccccc3', 'chat').m.text.length, 60);
});

console.log('\n' + passed + ' 項通過' + (failed.length ? '，' + failed.length + ' 項失敗：\n  - ' + failed.join('\n  - ') : ''));
process.exit(failed.length ? 1 : 0);
void Rules;
