import * as THREE from 'three';
import type { ReplayDocument } from '../types';
import { botLook, projectileLook, presentationAccent } from '../render/arenaThemes';
import { posesAt } from '../render/interpolate';

/**
 * The things that move.
 *
 * Bots and projectiles are quads lying **flat on the floor**, not billboards standing up to
 * face the camera. The sprites were drawn as a plan view — a chassis seen from directly
 * above — so standing one upright would show a top-down drawing pretending to be a side
 * view, which is the exact tell that makes cheap 2.5D look wrong. Laid flat they are simply
 * a plan view seen at an angle, which is what they are, and the foreshortening is correct
 * rather than fudged.
 *
 * They hover a hair above the floor so the depth buffer has something to separate them
 * from it, and so they catch the key light rather than z-fighting with their own shadow.
 */

const BOT_HOVER = 0.035;
const PROJECTILE_HOVER = 0.14;

export interface ArenaActors {
  group: THREE.Group;
  /** Move everything to where it should be at this moment of the replay. */
  update: (time: number) => void;
  dispose: () => void;
}

export function buildActors(replay: ReplayDocument): ArenaActors {
  const group = new THREE.Group();
  const disposables: { dispose: () => void }[] = [];
  const { participants } = replay.header;

  const bots = participants.map((participant, slot) => {
    const look = botLook(participant?.lookId, slot);
    const accent = presentationAccent(look, participant?.accent ?? '#38bdf8');
    const size = Math.max(0.7, look.scale * 0.78);

    const geometry = new THREE.PlaneGeometry(size, size);
    geometry.rotateX(-Math.PI / 2);
    const material = new THREE.MeshStandardMaterial({
      map: spriteTexture(look.image),
      color: 0xffffff,
      transparent: true,
      // Sprites have hard alpha edges; testing rather than blending keeps them from
      // sorting against each other and the floor.
      alphaTest: 0.35,
      roughness: 0.55,
      metalness: 0.3,
      emissive: new THREE.Color(accent),
      // A little self-illumination so a bot reads against a dark floor without needing a
      // light of its own, which would multiply the shadow cost per bot.
      emissiveIntensity: 0.22,
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.castShadow = true;
    mesh.visible = false;
    group.add(mesh);
    disposables.push(geometry, material);
    return mesh;
  });

  // Projectiles are pooled: a replay can fire many, but few are in the air at once, and
  // creating a mesh per shot would allocate during playback.
  const pool: THREE.Mesh[] = [];
  const projectileMaterial = (slot: number) => {
    const participant = participants[slot];
    const look = projectileLook(participant?.projectileLookId);
    const accent = presentationAccent(botLook(participant?.lookId, slot), participant?.accent ?? '#38bdf8');
    return new THREE.MeshStandardMaterial({
      map: spriteTexture(look.image),
      color: 0xffffff,
      transparent: true,
      alphaTest: 0.2,
      emissive: new THREE.Color(accent),
      // Bright: a bolt is a light source in every other renderer here, and dimming it to
      // match a lit surface would lose the one thing that makes shots readable at a glance.
      emissiveIntensity: 1.6,
      roughness: 0.4,
    });
  };
  const materials = participants.map((_, slot) => projectileMaterial(slot));
  for (const material of materials) disposables.push(material);

  const projectileGeometry = new THREE.PlaneGeometry(0.5, 0.5);
  projectileGeometry.rotateX(-Math.PI / 2);
  disposables.push(projectileGeometry);

  const borrow = (index: number): THREE.Mesh => {
    while (pool.length <= index) {
      const mesh = new THREE.Mesh(projectileGeometry, materials[0]);
      mesh.visible = false;
      group.add(mesh);
      pool.push(mesh);
    }
    return pool[index];
  };

  const update = (time: number) => {
    for (const pose of posesAt(replay, time)) {
      const mesh = bots[pose.slot];
      if (!mesh) continue;
      mesh.visible = pose.status === 'Active';
      mesh.position.set(pose.x + 0.5, BOT_HOVER, pose.y + 0.5);
      // `angle` is already the interpolated screen-space rotation the 2D renderer uses, so
      // the two viewers turn a bot through exactly the same arc. Negated because the plane
      // lies face-up: its local +y ran north before the rotateX, and now runs south.
      mesh.rotation.y = -pose.angle;
    }

    const tick = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));
    const projectiles = replay.ticks[tick]?.projectiles ?? [];
    projectiles.forEach((projectile, index) => {
      const mesh = borrow(index);
      mesh.material = materials[projectile.ownerSlot] ?? materials[0];
      mesh.visible = true;
      mesh.position.set(projectile.x + 0.5, PROJECTILE_HOVER, projectile.y + 0.5);
    });
    for (let index = projectiles.length; index < pool.length; index++)
      pool[index].visible = false;
  };

  return {
    group,
    update,
    dispose: () => {
      for (const item of disposables) item.dispose();
    },
  };
}

/** Rasterised sprite size. Generous: these are read close-up under a perspective camera. */
const SPRITE_PIXELS = 256;

/**
 * Rasterise a chassis or bolt sprite into a texture.
 *
 * **Via a canvas, not straight from the image.** Every sprite here is an SVG carrying only
 * a `viewBox` and no intrinsic width or height, which makes it an unreliable WebGL texture
 * source — browsers disagree about what `naturalWidth` even is for one, and a zero there
 * yields a texture that samples as fully transparent. With `alphaTest` on, that discards
 * every fragment and the bot simply is not there, which is exactly how this first went
 * wrong: two Active bots, nothing on the floor, no error anywhere.
 *
 * The 2D renderer already rasterises these for tinting, so this is the same trick rather
 * than a new one. Drawing at a fixed size also makes the result independent of however the
 * browser chose to size the SVG.
 */
function spriteTexture(image: HTMLImageElement | null): THREE.Texture | null {
  if (!image || typeof document === 'undefined') return null;

  const canvas = document.createElement('canvas');
  canvas.width = SPRITE_PIXELS;
  canvas.height = SPRITE_PIXELS;
  const context = canvas.getContext('2d');
  if (!context) return null;

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.anisotropy = 8;

  const paint = () => {
    context.clearRect(0, 0, SPRITE_PIXELS, SPRITE_PIXELS);
    context.drawImage(image, 0, 0, SPRITE_PIXELS, SPRITE_PIXELS);
    texture.needsUpdate = true;
  };

  if (image.complete) paint();
  else image.addEventListener('load', paint, { once: true });
  return texture;
}
