/* 線上大廳聊天室（左下氣泡）：共用元件 lobby-chat.js 的轉接層 */
import './lobby-chat.js';
import './lobby-chat.css';
import { net } from './net.js';
import { app, myName } from './app.js';
import { getSave, update } from './store.js';
import { getLang } from './i18n.js';

export let lobbyChat = null;

export function initLobbyChat() {
  if (lobbyChat || typeof window === 'undefined' || !window.LobbyChat) return lobbyChat;
  const chat = lobbyChat = window.LobbyChat.create({
    game: 'snake-3d',
    active: () => app.get().screen === 'lobby',
    lang: () => getLang(),
    getName: () => myName(),
    setName: n => update({ nickname: n.slice(0, 12) }),
    join: name => net.send({ type: 'lobbychat', op: 'join', name }),
    leave: () => net.send({ type: 'lobbychat', op: 'leave' }),
    send: (text, name) => net.send({ type: 'lobbychat', op: 'send', text, name }),
    joinRoom: code => net.send({ type: 'join', room: code, as: 'player', name: myName(), animal: getSave().animal })
  });
  chat.setConnected(net.connected && net.status === 'open');
  net.onStatus(s => chat.setConnected(s === 'open'));
  net.on('lobbychat', m => {
    if (m.op === 'history') chat.history(m.history, m.online);
    else if (m.op === 'msg') chat.add(m.msg);
    else if (m.op === 'online') chat.online(m.online);
    else if (m.op === 'error') chat.error(m.code);
  });
  app.subscribe(() => chat.refresh());
  /* 語言切換：i18n 會改 <html lang>，跟著重畫聊天室文字 */
  if (window.MutationObserver) new MutationObserver(() => chat.relabel()).observe(document.documentElement, { attributes: true, attributeFilter: ['lang'] });
  return chat;
}
