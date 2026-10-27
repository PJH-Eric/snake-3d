import { useEffect } from 'react';
import { GlobalLayer } from './shell.jsx';
import { useApp } from './app.js';
import { useSave } from './store.js';
import { bootInvite, initOnline } from './online.js';
import { installUnlock, music } from './sound.js';
import { useLang } from './i18n.js';
import { net } from './net.js';
import { pageView } from './analytics.js';
import { ChallengePage, HelpPage, HomePage, SoloPage } from './screens/menus.jsx';
import { LobbyPage } from './screens/lobby.jsx';
import { RoomPage } from './screens/room.jsx';
import { GamePage } from './screens/game.jsx';

let booted = false;
function boot() {
  if (booted) return;
  booted = true;
  installUnlock();
  initOnline();
  bootInvite();
  if (new URLSearchParams(location.search).has('e2e')) window.__net = net;   /* e2e 才會用到：模擬斷線 */
}

export default function App() {
  useLang();
  const screen = useApp(s => s.screen);
  const params = useApp(s => s.params);
  const save = useSave();
  useEffect(boot, []);
  useEffect(() => { document.documentElement.classList.toggle('reduce-motion', !!save.reduceMotion); }, [save.reduceMotion]);
  useEffect(() => { if (screen !== 'game') music('menu'); }, [screen]);
  useEffect(() => { pageView(screen || 'home'); }, [screen]);

  let page;
  switch (screen) {
    case 'help': page = <HelpPage />; break;
    case 'challenge': page = <ChallengePage />; break;
    case 'solo': page = <SoloPage />; break;
    case 'lobby': page = <LobbyPage />; break;
    case 'room': page = <RoomPage />; break;
    case 'game': page = <GamePage key={params.kind === 'online' ? 'online' : params.runId} params={params} />; break;
    default: page = <HomePage />;
  }
  return <div className="app">{page}<GlobalLayer /></div>;
}
