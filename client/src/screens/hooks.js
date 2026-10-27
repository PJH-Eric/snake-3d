import { useSyncExternalStore } from 'react';
import { net } from '../net.js';

let version = 0;
net.onStatus(() => { version++; });
export function useStatus() {
  return useSyncExternalStore(fn => net.onStatus(fn), () => net.status);
}
