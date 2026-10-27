import { Plane, Raycaster, Vector2, Vector3 } from 'three';

const ray = new Raycaster();
const ground = new Plane(new Vector3(0, 1, 0), 0);
const ndc = new Vector2();
const target = new Vector3();

/** 將畫布上的手指位置投射到場地，包含鏡頭縮放、偏移與透視。 */
export function screenAim(camera, rect, x, y, player) {
  if (!player || rect.width <= 0 || rect.height <= 0) return null;
  ndc.set((x - rect.left) / rect.width * 2 - 1, 1 - (y - rect.top) / rect.height * 2);
  camera.updateMatrixWorld();
  ray.setFromCamera(ndc, camera);
  if (!ray.ray.intersectPlane(ground, target)) return null;
  const dx = target.x - player.x, dz = target.z - player.z;
  // 手指貼近蛇頭時維持方向，避免極小位移造成來回抖動。
  return Math.hypot(dx, dz) > player.r ? Math.atan2(dz, dx) : null;
}
