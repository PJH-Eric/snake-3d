import { describe, expect, it } from 'vitest';
import { PerspectiveCamera, Vector3 } from 'three';
import { screenAim } from '../src/game/steering.js';

describe('手指指向場地位置', () => {
  for (const [width, height] of [[390, 844], [844, 390], [820, 1180], [1180, 820]]) {
    it(`${width}×${height}：透視、鏡頭偏移與縮放後仍指向正確方向`, () => {
      const rect = { left: 170, top: 40, width, height };
      const player = { x: 7, z: -3, r: 0.5 };
      for (const distance of [20, 40]) {
        const camera = new PerspectiveCamera(40, width / height, 0.5, 400);
        camera.position.set(10, distance, distance - 5);
        camera.lookAt(10, 0, -5);
        camera.updateMatrixWorld();
        for (const [dx, dz] of [[4, 0], [0, 4], [-4, 0], [0, -4], [3, 3]]) {
          const p = new Vector3(player.x + dx, 0, player.z + dz).project(camera);
          const actual = screenAim(camera, rect, rect.left + (p.x + 1) * width / 2,
            rect.top + (1 - p.y) * height / 2, player);
          expect(actual).toBeCloseTo(Math.atan2(dz, dx), 8);
        }
        const head = new Vector3(player.x, 0, player.z).project(camera);
        expect(screenAim(camera, rect, rect.left + (head.x + 1) * width / 2,
          rect.top + (1 - head.y) * height / 2, player)).toBeNull();
      }
    });
  }
});
