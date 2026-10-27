/* Three.js 3D 舞台：圓形動物園場地、圍欄、石頭、樹、動物蛇（頭＋實例化身體）、果實、粒子與名牌。
 * 座標：規則核心的 (x, z) 直接對應 Three 的 (x, z)；畫面上方是 -z，鏡頭仰角 52°。 */
import * as THREE from 'three';
import Rules from '@shared/rules.js';
import { animalDef, FRUIT_BODY, FRUIT_LEAF, slotColor, theme } from '../data.js';

const EL = (52 * Math.PI) / 180;
const FOV = 40;
const BODY_CAP = 520;
const FOOD_CAP = 300;
const PUFF_CAP = 320;
const tmpM = new THREE.Matrix4(), tmpQ = new THREE.Quaternion(), tmpP = new THREE.Vector3(), tmpS = new THREE.Vector3(), tmpE = new THREE.Euler(), tmpC = new THREE.Color();
const ZERO_Q = new THREE.Quaternion();
/* 場地道具的外型種類（純視覺；碰撞與果實規則由 shared/rules.js 決定） */
const ROCK_KINDS = ['rock', 'mushroom', 'stump', 'crystal'];
const GEM_COLORS = ['#7fdcec', '#ff9ec7', '#c9a7ff'];
const FLOWER_COLORS = ['#ff9ec7', '#ffe27a', '#ffffff', '#c9a7ff', '#ffb259'];
/* 果實：0 蘋果（圓）、1 梨子（高）、2 蜜桃（扁）；[寬, 高] */
const FRUIT_SHAPE = [[0.84, 0.78], [0.7, 1.0], [0.95, 0.74]];
const lerp = (a, b, t) => a + (b - a) * t;

function starGeometry() {
  const shape = new THREE.Shape();
  for (let i = 0; i < 10; i++) {
    const a = Math.PI / 2 + (i / 10) * Math.PI * 2, r = i % 2 === 0 ? 0.5 : 0.22;
    const x = Math.cos(a) * r, y = Math.sin(a) * r;
    if (i === 0) shape.moveTo(x, y); else shape.lineTo(x, y);
  }
  shape.closePath();
  const g = new THREE.ExtrudeGeometry(shape, { depth: 0.16, bevelEnabled: true, bevelThickness: 0.04, bevelSize: 0.04, bevelSegments: 1 });
  g.translate(0, 0, -0.08);
  return g;
}

function tagTexture(text, color) {
  const pad = 14, fs = 44;
  const c = document.createElement('canvas');
  const ctx = c.getContext('2d');
  ctx.font = `800 ${fs}px "Noto Sans TC","PingFang TC","Microsoft JhengHei",sans-serif`;
  const w = Math.min(520, Math.ceil(ctx.measureText(text).width) + pad * 2);
  c.width = w; c.height = fs + pad * 2;
  const g = c.getContext('2d');
  g.font = `800 ${fs}px "Noto Sans TC","PingFang TC","Microsoft JhengHei",sans-serif`;
  g.textAlign = 'center'; g.textBaseline = 'middle';
  g.lineJoin = 'round'; g.lineWidth = 9; g.strokeStyle = '#4a3320';
  g.strokeText(text, w / 2, c.height / 2 + 2, w - pad);
  g.fillStyle = color;
  g.fillText(text, w / 2, c.height / 2 + 2, w - pad);
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.minFilter = THREE.LinearFilter; tex.generateMipmaps = false;
  return { tex, aspect: w / c.height };
}

export class World3D {
  constructor(canvas, view, opts = {}) {
    this.canvas = canvas;
    this.quality = opts.quality || 'normal';
    this.nameOf = opts.nameOf || (p => p.name);
    this.th = theme(view.themeId);
    this.R = view.R;
    this.disposables = [];
    this.mats = new Map();
    this.snakes = new Map();
    this.puffs = [];
    this.clock = 0; this.shake = 0; this.focus = null; this.camH = -1; this.shiftPx = 0;
    this.w = 1; this.h = 1;

    const low = this.quality === 'low';
    const r = (this.renderer = new THREE.WebGLRenderer({ canvas, antialias: !low, powerPreference: 'high-performance', alpha: false }));
    r.setPixelRatio(Math.min(window.devicePixelRatio || 1, this.quality === 'high' ? 2.5 : low ? 1 : 1.75));
    r.shadowMap.enabled = !low;
    r.shadowMap.type = THREE.PCFSoftShadowMap;
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(this.th.sky);
    this.cam = new THREE.PerspectiveCamera(FOV, 1, 0.5, 400);

    const hemi = new THREE.HemisphereLight(new THREE.Color(this.th.h0).lerp(new THREE.Color('#fff'), 0.2), new THREE.Color(this.th.h1), this.th.night ? 1.25 : 1.35);
    this.scene.add(hemi);
    const sun = (this.sun = new THREE.DirectionalLight(this.th.lc, Math.min(2.0, this.th.li * 1.7)));
    sun.castShadow = !low;
    const sm = this.quality === 'high' ? 2048 : 1024;
    sun.shadow.mapSize.set(sm, sm);
    sun.shadow.bias = -0.0006; sun.shadow.normalBias = 0.05;
    sun.shadow.camera.near = 1; sun.shadow.camera.far = 90;
    this.scene.add(sun, sun.target);

    this.geo = {
      sphere: this.own(new THREE.SphereGeometry(0.5, low ? 10 : 16, low ? 7 : 10)),
      cone: this.own(new THREE.ConeGeometry(0.5, 1, 12)),
      cyl: this.own(new THREE.CylinderGeometry(0.5, 0.5, 1, 10)),
      star: this.own(starGeometry())
    };
    this.buildStage(view);
    this.buildFoods();
    this.buildPuffs();
    for (const p of view.players) this.ensureSnake(p, opts.selfSlot);
  }

  own(o) { this.disposables.push(o); return o; }
  mat(color, emis = 0) {
    const key = color + '|' + emis;
    let m = this.mats.get(key);
    if (!m) {
      m = this.own(new THREE.MeshLambertMaterial({ color, emissive: emis ? new THREE.Color(color) : 0x000000, emissiveIntensity: emis }));
      this.mats.set(key, m);
    }
    return m;
  }
  part(parent, geo, mat, pos, scale, rot, shadow = true) {
    const m = new THREE.Mesh(geo, mat);
    m.position.set(pos[0], pos[1], pos[2]);
    m.scale.set(scale[0], scale[1], scale[2]);
    if (rot) m.rotation.set(rot[0] * Math.PI / 180, rot[1] * Math.PI / 180, rot[2] * Math.PI / 180);
    m.castShadow = shadow; m.receiveShadow = true;
    parent.add(m);
    return m;
  }
  instanced(geo, mat, cap, shadow = true, colors = false) {
    const m = new THREE.InstancedMesh(geo, mat, cap);
    m.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    if (colors) { m.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(cap * 3), 3); m.instanceColor.setUsage(THREE.DynamicDrawUsage); }
    m.count = 0; m.castShadow = shadow; m.receiveShadow = true; m.frustumCulled = false;
    this.scene.add(m);
    return m;
  }

  /* ---------- 舞台 ---------- */
  floorTexture() {
    const S = 128, c = document.createElement('canvas');
    c.width = c.height = S;
    const g = c.getContext('2d'), img = g.createImageData(S, S);
    let sd = (Math.round(this.R * 31) + 7) >>> 0;
    const rnd = () => { sd = (sd * 1664525 + 1013904223) >>> 0; return sd / 4294967296; };
    const p1 = rnd() * 6.28, p2 = rnd() * 6.28, p3 = rnd() * 6.28;
    const A = new THREE.Color(this.th.floorA), B = new THREE.Color(this.th.floorB);
    const cc = new THREE.Color();
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      const u = x / S * Math.PI * 2, w = y / S * Math.PI * 2;
      const n = Math.sin(u * 2 + p1) * Math.sin(w * 2 + p2) * 0.5 + Math.sin(u * 3 + w * 2 + p3) * 0.35 + Math.sin(u * 5 - w * 4 + p1) * 0.15;
      let k = Math.min(1, Math.max(0, n * 0.5 + 0.5)); k = k * k * (3 - 2 * k);
      cc.copy(A).lerp(B, k);
      const i = (y * S + x) * 4;
      img.data[i] = Math.round(cc.r * 255); img.data[i + 1] = Math.round(cc.g * 255); img.data[i + 2] = Math.round(cc.b * 255); img.data[i + 3] = 255;
    }
    g.putImageData(img, 0, 0);
    const tex = this.own(new THREE.CanvasTexture(c));
    tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
    tex.colorSpace = THREE.SRGBColorSpace;
    tex.anisotropy = 4;
    return tex;
  }

  buildStage(view) {
    const th = this.th, r = this.R, g = this.geo, scene = this.scene;
    const flat = geo => { geo.rotateX(-Math.PI / 2); return geo; };
    const outer = new THREE.Mesh(this.own(flat(new THREE.CircleGeometry(r + 70, 48))), this.mat(th.outer));
    outer.position.y = -0.06; outer.receiveShadow = true; scene.add(outer);
    const tex = this.floorTexture();
    tex.repeat.set((r + 0.4) * 2 / 9, (r + 0.4) * 2 / 9);
    const floor = new THREE.Mesh(this.own(flat(new THREE.CircleGeometry(r + 0.4, 96))), this.own(new THREE.MeshLambertMaterial({ map: tex })));
    floor.receiveShadow = true; scene.add(floor);

    /* 圍欄：柱子與柱帽 */
    const nPost = Math.round(Math.PI * 2 * (r + 0.3) / 1.7);
    const posts = this.instanced(g.cyl, this.mat(th.wall), nPost, true);
    const caps = this.instanced(g.sphere, this.mat(th.wallTop), nPost, true);
    for (let i = 0; i < nPost; i++) {
      const t = i / nPost * Math.PI * 2, x = Math.cos(t) * (r + 0.35), z = Math.sin(t) * (r + 0.35);
      posts.setMatrixAt(i, tmpM.compose(tmpP.set(x, 0.55, z), ZERO_Q, tmpS.set(0.34, 1.1, 0.34)));
      caps.setMatrixAt(i, tmpM.compose(tmpP.set(x, 1.12, z), ZERO_Q, tmpS.set(0.42, 0.42, 0.42)));
    }
    posts.count = caps.count = nPost;
    const railGeo = this.own(flat(new THREE.TorusGeometry(r + 0.35, 0.07, 6, 96)));
    for (const y of [0.45, 0.82]) {
      const rail = new THREE.Mesh(railGeo, this.mat(th.wallTop));
      rail.position.y = y; scene.add(rail);
    }

    /* 障礙物：依位置決定外型（石頭、香菇、樹墩、水晶），碰撞範圍都是同一個圓 */
    view.rocks.forEach((k, i) => {
      const grp = new THREE.Group();
      grp.position.set(k[0], 0, k[1]);
      this.buildProp(grp, ROCK_KINDS[(Math.abs(Math.round(k[0] * 7 + k[1] * 13)) + i) % ROCK_KINDS.length], k[2] * 2, k, th);
      scene.add(grp);
    });

    /* 地面小花：只是裝飾，不影響規則 */
    {
      let fs = (view.themeId * 104729 + Math.round(r) * 31) >>> 0;
      const fr = () => { fs = (fs * 1664525 + 1013904223) >>> 0; return fs / 4294967296; };
      const nFlower = this.quality === 'low' ? 14 : 34;
      const flowers = this.instanced(g.sphere, this.own(new THREE.MeshLambertMaterial({ color: 0xffffff })), nFlower, false, true);
      for (let i = 0; i < nFlower; i++) {
        const a = fr() * Math.PI * 2, dd = Math.sqrt(fr()) * (r - 1.2), sc = 0.2 + fr() * 0.12;
        flowers.setMatrixAt(i, tmpM.compose(tmpP.set(Math.cos(a) * dd, 0.05, Math.sin(a) * dd), ZERO_Q, tmpS.set(sc, sc * 0.5, sc)));
        flowers.setColorAt(i, tmpC.set(FLOWER_COLORS[i % FLOWER_COLORS.length]));
      }
      flowers.count = nFlower; flowers.instanceMatrix.needsUpdate = true; flowers.instanceColor.needsUpdate = true;
    }

    /* 場外裝飾：一圈樹 */
    let sd = (view.themeId * 7919 + Math.round(r)) >>> 0;
    const rnd = () => { sd = (sd * 1664525 + 1013904223) >>> 0; return sd / 4294967296; };
    const nDeco = this.quality === 'low' ? 18 : 30;
    const trunks = this.instanced(g.cyl, this.mat(th.wall), nDeco, true);
    const round = this.instanced(g.sphere, this.mat(th.crown), nDeco, true);
    const pointy = this.instanced(g.cone, this.mat(th.crown), nDeco, true);
    let nr = 0, np = 0;
    for (let i = 0; i < nDeco; i++) {
      const t = i / nDeco * Math.PI * 2 + rnd() * 0.15, dist = r + 3.5 + rnd() * 5, s = 1.4 + rnd() * 1.2;
      const x = Math.cos(t) * dist, z = Math.sin(t) * dist;
      trunks.setMatrixAt(i, tmpM.compose(tmpP.set(x, s * 0.45, z), ZERO_Q, tmpS.set(0.35 * s, 0.9 * s, 0.35 * s)));
      const pt = view.themeId === 3 || view.themeId === 4 || (view.themeId === 0 && i % 3 === 0);
      if (pt) pointy.setMatrixAt(np++, tmpM.compose(tmpP.set(x, s * 1.7, z), ZERO_Q, tmpS.set(1.7 * s, 1.9 * s, 1.7 * s)));
      else round.setMatrixAt(nr++, tmpM.compose(tmpP.set(x, s * 1.45, z), ZERO_Q, tmpS.set(1.9 * s, 1.7 * s, 1.9 * s)));
    }
    trunks.count = nDeco; round.count = nr; pointy.count = np;
  }

  /** 障礙物外型；d 是直徑（= 碰撞圓直徑），所有零件都收在這個範圍內。 */
  buildProp(grp, kind, d, k, th) {
    const g = this.geo, M = c => this.mat(c);
    if (kind === 'mushroom') {
      this.part(grp, g.cyl, M('#fff3dc'), [0, d * 0.28, 0], [d * 0.36, d * 0.56, d * 0.36]);
      this.part(grp, g.sphere, M('#ff6b61'), [0, d * 0.6, 0], [d * 0.98, d * 0.62, d * 0.98]);
      for (const [x, z, sc] of [[0.22, 0.12, 0.17], [-0.2, 0.2, 0.14], [0.02, -0.28, 0.15], [-0.26, -0.1, 0.11]]) this.part(grp, g.sphere, M('#ffffff'), [x * d, d * 0.8, z * d], [sc * d, sc * d * 0.5, sc * d], null, false);
    } else if (kind === 'stump') {
      this.part(grp, g.cyl, M('#9a6a3a'), [0, d * 0.26, 0], [d * 0.82, d * 0.52, d * 0.82]);
      this.part(grp, g.cyl, M('#e6bd86'), [0, d * 0.535, 0], [d * 0.7, d * 0.04, d * 0.7], null, false);
      this.part(grp, g.cyl, M('#b98650'), [0, d * 0.56, 0], [d * 0.36, d * 0.03, d * 0.36], null, false);
    } else if (kind === 'crystal') {
      const gem = GEM_COLORS[Math.abs(Math.round(k[0] * 3 + k[1] * 5)) % GEM_COLORS.length];
      this.part(grp, g.cone, this.mat(gem, 0.35), [0, d * 0.5, 0], [d * 0.42, d * 1.0, d * 0.42], [0, 15, 0]);
      this.part(grp, g.cone, this.mat(gem, 0.35), [d * 0.26, d * 0.3, d * 0.08], [d * 0.3, d * 0.6, d * 0.3], [0, 0, -18]);
      this.part(grp, g.cone, this.mat(gem, 0.35), [-d * 0.24, d * 0.25, -d * 0.1], [d * 0.28, d * 0.5, d * 0.28], [0, 0, 20]);
      this.part(grp, g.sphere, M(th.rock1), [0, d * 0.08, 0], [d * 0.9, d * 0.2, d * 0.9]);
    } else {
      this.part(grp, g.sphere, M(th.rock0), [0, d * 0.32, 0], [d, d * 0.72, d], [0, k[0] * 40, 0]);
      this.part(grp, g.sphere, M(th.rock1), [d * 0.22, d * 0.2, d * 0.18], [d * 0.55, d * 0.4, d * 0.5]);
    }
  }

  buildFoods() {
    const g = this.geo;
    this.apples = this.instanced(g.sphere, this.own(new THREE.MeshLambertMaterial({ color: 0xffffff })), FOOD_CAP, true, true);
    this.leaves = this.instanced(g.sphere, this.mat(FRUIT_LEAF), FOOD_CAP, false);
    this.stars = this.instanced(g.star, this.mat('#ffd23d', 0.5), 12, true);
    this.orbs = this.instanced(g.sphere, this.own(new THREE.MeshBasicMaterial({ color: 0xffffff })), FOOD_CAP, false, true);
  }
  buildPuffs() {
    this.puffMesh = this.instanced(this.geo.sphere, this.own(new THREE.MeshBasicMaterial({ color: 0xffffff })), PUFF_CAP, false, true);
  }

  /* ---------- 蛇 ---------- */
  ensureSnake(p, selfSlot) {
    if (this.snakes.has(p.slot)) return this.snakes.get(p.slot);
    const a = animalDef(p.animal), g = this.geo, scene = this.scene;
    const head = new THREE.Group();
    const body = this.mat(a.body), white = this.mat('#ffffff'), pupil = this.mat('#1d1630');
    const P = (geo, mat, pos, sc, rot, sh = true) => this.part(head, geo, mat, pos, sc, rot, sh);
    P(g.sphere, body, [0, 0.05, 0], [2.4, 2.2, 2.4]);
    if (a.muzzle) P(g.sphere, this.mat(a.muzzle), [0, -0.28, 0.85], [1.2, 0.85, 1.0]);
    if (a.beak) P(g.cone, this.mat(a.beak), [0, -0.12, 1.28], [0.7, 0.7, 0.5], [90, 0, 0]);
    else if (a.nose) P(g.sphere, this.mat(a.nose), [0, -0.02, 1.18], [0.36, 0.28, 0.3]);
    if (a.patches) for (const sx of [-1, 1]) P(g.sphere, this.mat(a.earCol), [sx * 0.62, 0.28, 0.82], [0.6, 0.78, 0.3], [0, 0, sx * 20]);
    if (a.eyesTop) {
      for (const sx of [-1, 1]) {
        P(g.sphere, body, [sx * 0.62, 0.95, 0.35], [0.95, 0.95, 0.95]);
        P(g.sphere, white, [sx * 0.62, 1.05, 0.62], [0.62, 0.62, 0.62], null, false);
        P(g.sphere, pupil, [sx * 0.62, 1.06, 0.88], [0.32, 0.32, 0.32], null, false);
      }
    } else {
      for (const sx of [-1, 1]) {
        P(g.sphere, white, [sx * 0.58, 0.4, 0.92], [0.62, 0.62, 0.62], null, false);
        P(g.sphere, pupil, [sx * 0.6, 0.4, 1.15], [0.34, 0.34, 0.34], null, false);
      }
      const earMat = this.mat(a.earCol || a.body);
      for (const sx of [-1, 1]) {
        if (a.ear === 'point') {
          P(g.cone, earMat, [sx * 0.78, 1.2, 0.05], [0.8, 1.0, 0.5], [0, 0, sx * -18]);
          if (a.earIn) P(g.cone, this.mat(a.earIn), [sx * 0.78, 1.14, 0.2], [0.5, 0.7, 0.3], [0, 0, sx * -18], false);
        } else if (a.ear === 'floppy') P(g.sphere, earMat, [sx * 1.2, 0.35, -0.05], [0.5, 1.2, 0.9], [0, 0, sx * -14]);
        else if (a.ear === 'long') {
          P(g.sphere, earMat, [sx * 0.55, 1.9, -0.15], [0.62, 1.9, 0.5], [0, 0, sx * 8]);
          if (a.earIn) P(g.sphere, this.mat(a.earIn), [sx * 0.55, 1.88, 0.05], [0.34, 1.4, 0.3], [0, 0, sx * 8], false);
        } else if (a.ear === 'round') {
          P(g.sphere, earMat, [sx * 0.95, 0.95, -0.05], [0.82, 0.82, 0.82]);
          if (a.earIn && !a.earCol) P(g.sphere, this.mat(a.earIn), [sx * 0.95, 0.95, 0.18], [0.46, 0.46, 0.46], null, false);
        }
      }
    }
    head.visible = false;
    scene.add(head);

    const colors = slotColor(p.slot);
    const sn = {
      slot: p.slot, animal: p.animal, head, a,
      bodyA: this.instanced(g.sphere, this.mat(a.seg0), BODY_CAP, true),
      bodyB: this.instanced(g.sphere, this.mat(a.seg1), BODY_CAP, true),
      tail: new THREE.Mesh(g.sphere, this.mat(a.tail)),
      arrow: new THREE.Mesh(g.cone, this.mat(colors, 0.15)),
      tag: null, tagKey: ''
    };
    sn.tail.castShadow = true; sn.tail.visible = false; scene.add(sn.tail);
    sn.arrow.rotation.x = Math.PI; sn.arrow.scale.set(0.7, 0.9, 0.7); sn.arrow.visible = false; scene.add(sn.arrow);
    this.snakes.set(p.slot, sn);
    this.setTag(sn, p, p.slot === selfSlot);
    return sn;
  }

  setTag(sn, p, mine) {
    const text = this.nameOf(p) + (mine ? this.youTag || '' : '');
    const key = text + '|' + p.slot;
    if (sn.tagKey === key) return;
    sn.tagKey = key;
    if (sn.tag) { sn.tag.material.map.dispose(); sn.tag.material.dispose(); this.scene.remove(sn.tag); }
    const { tex, aspect } = tagTexture(text, slotColor(p.slot));
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, depthTest: false, transparent: true }));
    sp.scale.set(0.75 * aspect, 0.75, 1); sp.renderOrder = 10; sp.visible = false;
    sn.tag = sp; sn.tagAspect = aspect;
    this.scene.add(sp);
  }

  /** 語言切換或自己的身分改變時重畫名牌 */
  relabel(view, selfSlot, youTag) {
    this.youTag = youTag;
    for (const p of view.players) { const sn = this.snakes.get(p.slot); if (sn) { sn.tagKey = ''; this.setTag(sn, p, p.slot === selfSlot); } }
  }

  resize(w, h) {
    this.w = Math.max(1, w); this.h = Math.max(1, h);
    this.renderer.setSize(this.w, this.h, false);
    this.cam.aspect = this.w / this.h;
    this.cam.updateProjectionMatrix();
  }

  segPos(pts, prev, i, a, y) {
    let x = pts[i * 2], z = pts[i * 2 + 1];
    if (prev && prev.length > i * 2 + 1) { x = prev[i * 2] + (x - prev[i * 2]) * a; z = prev[i * 2 + 1] + (z - prev[i * 2 + 1]) * a; }
    return tmpP.set(x, y, z);
  }

  drawSnake(p, sn, a) {
    const n = p.pts.length / 2, r = p.r, prev = p.prevPts;
    const hp = this.segPos(p.pts, prev, 0, a, r * 1.05);
    const ang = p.prevAng != null && p.prevAng !== p.ang ? p.prevAng + Rules.wrapAng(p.ang - p.prevAng) * a : p.ang;
    sn.head.position.copy(hp);
    sn.head.rotation.y = Math.PI / 2 - ang;
    sn.head.scale.setScalar(r);
    const headPos = hp.clone();
    let ia = 0, ib = 0;
    const taperN = Math.min(8, Math.max(1, n - 1));
    sn.tail.visible = false;
    for (let i = 1; i < n; i++) {
      const k = Math.min(1, Math.max(0, (n - 1 - i) / taperN)), isTail = i === n - 1;
      let s = r * 2 * lerp(0.5, 1, k);
      if (isTail) s = r * 2 * 0.5 * sn.a.tailScale;
      const pos = this.segPos(p.pts, prev, i, a, s * 0.5 * 0.95);
      if (isTail) { sn.tail.position.copy(pos); sn.tail.scale.set(s, s * 0.95, s); sn.tail.visible = true; continue; }
      tmpM.compose(pos, ZERO_Q, tmpS.set(s, s * 0.95, s));
      if ((i & 1) === 0) { if (ia < BODY_CAP) sn.bodyA.setMatrixAt(ia++, tmpM); } else if (ib < BODY_CAP) sn.bodyB.setMatrixAt(ib++, tmpM);
    }
    sn.bodyA.count = ia; sn.bodyB.count = ib;
    sn.bodyA.instanceMatrix.needsUpdate = sn.bodyB.instanceMatrix.needsUpdate = true;
    if (p.boosting && n > 2 && Math.random() < 0.5) this.addPuff(this.segPos(p.pts, prev, Math.min(n - 1, 3), a, r).clone(), slotColor(p.slot), 0.5, 1, 0.3);
    const lift = r * 2.6 + 0.55;
    sn.tag.position.set(headPos.x, headPos.y + lift, headPos.z);
    sn.arrow.position.set(headPos.x, headPos.y + lift + 0.95 + Math.sin(this.clock * 6) * 0.15, headPos.z);
  }

  drawFoods(view) {
    let na = 0, ns = 0, no = 0;
    const clock = this.clock;
    for (const f of view.foods) {
      const bob = Math.sin(clock * 3 + f.id * 1.7) * 0.07;
      if (f.type === 0 && na < FOOD_CAP) {
        const vi = Math.min(2, Math.max(0, f.v)), sh = FRUIT_SHAPE[vi];
        this.apples.setMatrixAt(na, tmpM.compose(tmpP.set(f.x, sh[1] * 0.5 + bob, f.z), ZERO_Q, tmpS.set(sh[0], sh[1], sh[0])));
        this.apples.setColorAt(na, tmpC.set(FRUIT_BODY[vi]));
        tmpQ.setFromEuler(tmpE.set(0, 0, 25 * Math.PI / 180));
        this.leaves.setMatrixAt(na, tmpM.compose(tmpP.set(f.x + 0.1, sh[1] + bob - 0.02, f.z), tmpQ, tmpS.set(0.34, 0.12, 0.2)));
        na++;
      } else if (f.type === 1) {
        if (f.ttl > 0 && f.ttl < 4 && ((clock * 8) | 0) % 2 === 0) continue;
        if (ns >= 12) continue;
        tmpQ.setFromEuler(tmpE.set(0, (clock * 2 + f.id * 0.7), 0));
        this.stars.setMatrixAt(ns++, tmpM.compose(tmpP.set(f.x, 0.75 + bob * 1.5, f.z), tmpQ, tmpS.set(1.25, 1.25, 1.25)));
      } else if (f.type === 2 && no < FOOD_CAP) {
        if (f.ttl > 0 && f.ttl < 5 && ((clock * 8) | 0) % 2 === 0) continue;
        const d = 0.62 + Math.sin(clock * 5 + f.id) * 0.05;
        this.orbs.setMatrixAt(no, tmpM.compose(tmpP.set(f.x, 0.38 + bob, f.z), ZERO_Q, tmpS.set(d, d, d)));
        this.orbs.setColorAt(no, tmpC.set(slotColor(f.v)).lerp(new THREE.Color('#ffffff'), 0.25));
        no++;
      }
    }
    this.apples.count = this.leaves.count = na; this.stars.count = ns; this.orbs.count = no;
    for (const m of [this.apples, this.leaves, this.stars, this.orbs]) m.instanceMatrix.needsUpdate = true;
    if (na) this.apples.instanceColor.needsUpdate = true;
    if (no) this.orbs.instanceColor.needsUpdate = true;
  }

  /* ---------- 粒子 ---------- */
  addPuff(pos, color, size, count, speed) {
    if (this.puffs.length > PUFF_CAP - 20) return;
    for (let i = 0; i < count; i++) {
      const dir = new THREE.Vector3(Math.random() * 2 - 1, 0.3 + Math.random() * 1.1, Math.random() * 2 - 1).normalize();
      this.puffs.push({ pos: pos.clone(), vel: dir.multiplyScalar(speed * (2 + Math.random() * 4)), life: 0, max: 0.35 + Math.random() * 0.35, size: size * (0.7 + Math.random() * 0.6), col: color });
    }
  }
  updatePuffs(dt) {
    let n = 0;
    for (let i = this.puffs.length - 1; i >= 0; i--) {
      const p = this.puffs[i];
      p.life += dt; p.pos.addScaledVector(p.vel, dt); p.vel.y -= 9 * dt;
      if (p.pos.y < 0.05) { p.pos.y = 0.05; p.vel.multiplyScalar(0.4); }
      if (p.life >= p.max) { this.puffs.splice(i, 1); continue; }
    }
    for (const p of this.puffs) {
      if (n >= PUFF_CAP) break;
      const k = 1 - p.life / p.max, s = p.size * (0.4 + 0.6 * k);
      this.puffMesh.setMatrixAt(n, tmpM.compose(p.pos, ZERO_Q, tmpS.set(s, s, s)));
      this.puffMesh.setColorAt(n, tmpC.set(p.col));
      n++;
    }
    this.puffMesh.count = n;
    this.puffMesh.instanceMatrix.needsUpdate = true;
    if (n) this.puffMesh.instanceColor.needsUpdate = true;
  }

  consume(events, selfSlot, reduceMotion) {
    for (const e of events || []) {
      if (e.t === 'eat') {
        const c = e.type === 0 ? FRUIT_BODY[Math.min(2, Math.max(0, e.v))] : e.type === 1 ? '#ffd23d' : slotColor(e.v);
        this.addPuff(new THREE.Vector3(e.x, 0.5, e.z), c, 0.28, reduceMotion ? 3 : 8, 0.55);
      } else if (e.t === 'die') {
        this.addPuff(new THREE.Vector3(e.x, 0.8, e.z), slotColor(e.slot), 0.5, reduceMotion ? 6 : 24, 1.2);
        if (e.slot === selfSlot && !reduceMotion) this.shake = 0.6;
      }
    }
  }

  /* ---------- 鏡頭 ---------- */
  updateCamera(view, tgt, dt, zoomMode) {
    const sw = Math.max(80, this.w), sh = Math.max(80, this.h);
    const zoom = zoomMode === 'near' ? 0.8 : zoomMode === 'far' ? 1.3 : 1;
    let fx = 0, fz = 0, span;
    if (tgt) {
      fx = tgt.x + Math.cos(tgt.ang) * 1.6; fz = tgt.z + Math.sin(tgt.ang) * 1.6;
      span = (15 + (tgt.r - Rules.BASE_R) * 16) * zoom;
      if (view.phase === 'countdown') span *= 1.15;
    } else span = this.R * 2.25;
    if (!this.focus) this.focus = { x: fx, z: fz };
    const k = 1 - Math.exp(-dt * 7);
    this.focus.x += (fx - this.focus.x) * k; this.focus.z += (fz - this.focus.z) * k;
    if (this.camH < 0) this.camH = span;
    this.camH += (span - this.camH) * (1 - Math.exp(-dt * 2.5));
    const vSpan = sw >= sh ? this.camH : this.camH * (sh / sw);
    const dist = (vSpan / 2) / Math.tan(FOV * Math.PI / 360) * 1.02;
    let ox = 0, oz = 0;
    if (this.shake > 0.001) { ox = (Math.random() - 0.5) * this.shake; oz = (Math.random() - 0.5) * this.shake; this.shake *= Math.exp(-dt * 9); }
    /* 把蛇頭放在「扣掉上方 HUD、下方操作區」之後剩下的可視帶中央（畫面上方是 -z） */
    const fz3 = this.focus.z + (this.shiftPx / sh) * vSpan / Math.sin(EL);
    this.cam.position.set(this.focus.x + ox, Math.sin(EL) * dist, fz3 + Math.cos(EL) * dist + oz);
    this.cam.lookAt(this.focus.x, 0, fz3);
    this.sun.position.set(this.focus.x - 8, 18, fz3 - 10);
    this.sun.target.position.set(this.focus.x, 0, fz3);
    const half = Math.max(14, vSpan * 0.75);
    const sc = this.sun.shadow.camera;
    if (Math.abs(sc.right - half) > 0.5) { sc.left = -half; sc.right = half; sc.top = half; sc.bottom = -half; sc.updateProjectionMatrix(); }
  }

  draw(view, selfSlot, alpha, dt, zoomMode, watchSlot, safeTop = 0, safeBottom = 0) {
    this.clock += dt;
    const me = view.players.find(p => p.slot === selfSlot), watch = view.players.find(p => p.slot === watchSlot);
    const target = me && me.alive ? me : watch && watch.alive ? watch : null;
    this.shiftPx = (safeBottom - safeTop) * 0.5;
    this.updateCamera(view, target, dt, zoomMode);
    const a = Math.min(1, Math.max(0, alpha));
    for (const p of view.players) {
      const sn = this.snakes.get(p.slot);
      if (!sn) continue;
      const show = p.alive && p.pts && p.pts.length >= 2;
      sn.head.visible = sn.tag.visible = !!show;
      sn.arrow.visible = !!show && p.slot === selfSlot;
      if (!show) { sn.bodyA.count = sn.bodyB.count = 0; sn.tail.visible = false; continue; }
      this.drawSnake(p, sn, a);
    }
    this.drawFoods(view);
    this.updatePuffs(dt);
    this.renderer.render(this.scene, this.cam);
  }

  dispose() {
    for (const o of this.disposables) if (o && o.dispose) o.dispose();
    for (const sn of this.snakes.values()) if (sn.tag) { sn.tag.material.map.dispose(); sn.tag.material.dispose(); }
    this.disposables = []; this.snakes.clear(); this.mats.clear();
    this.scene.traverse(o => { if (o.isInstancedMesh) o.dispose(); });
    this.renderer.dispose();
    try { this.renderer.forceContextLoss(); } catch { /* 已失去 */ }
  }
}
