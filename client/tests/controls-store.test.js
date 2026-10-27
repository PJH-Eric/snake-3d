import { beforeEach, expect, it, vi } from 'vitest';

beforeEach(() => { localStorage.clear(); vi.resetModules(); });

it.each([
  [{}, 'finger'],
  [{ touchMode: 'off', stickMode: 'float' }, 'finger'],
  [{ touchMode: 'off', stickMode: 'fixed' }, 'finger'],
  [{ touchMode: 'auto', stickMode: 'fixed' }, 'joystick'],
  [{ controlMode: 'joystick', stickSide: 'right' }, 'joystick'],
  [{ controlMode: 'finger', stickMode: 'fixed' }, 'finger'],
])('載入舊設定 %j → %s', async (saved, expected) => {
  localStorage.setItem('snake3d.save.v1', JSON.stringify(saved));
  const { getSave, resetSettings, update } = await import('../src/store.js');
  expect(getSave().controlMode).toBe(expected);
  expect(getSave()).not.toHaveProperty('touchMode');
  expect(getSave()).not.toHaveProperty('stickMode');
  expect(JSON.parse(localStorage.getItem('snake3d.save.v1')).controlMode).toBe(expected);
  update({ controlMode: 'joystick', stickSide: 'right' });
  resetSettings();
  expect(getSave().controlMode).toBe('finger');
  expect(getSave().stickSide).toBe('left');
});
