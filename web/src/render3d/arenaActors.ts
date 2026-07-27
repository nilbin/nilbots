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

/** How tall a bot's hull stands. Below the walls, so cover still reads as cover. */
const BOT_HEIGHT = 0.26;
const PROJECTILE_HOVER = 0.2;

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
    const accent = new THREE.Color(
      presentationAccent(look, participant?.accent ?? '#38bdf8'),
    );
    const size = Math.max(0.82, look.scale * 0.9);

    // A bot is an object standing on the floor, so it gets a body. Laying the sprite flat
    // was correct about the *sprite* — it is a plan view and should be seen as one — but
    // wrong about the bot: a decal on the ground has no silhouette, casts a shadow the
    // shape of a postage stamp, and disappears against a dark floor. The plan view belongs
    // on the lid of a hull, which is where a plan view of a hull comes from.
    const chassis = new THREE.Group();

    // A hull that points somewhere. A cylinder was the first attempt and it made every
    // chassis read as the same glowing puck — which throws away the one thing the twelve
    // sprites exist to express. Longer than it is wide, and turned with the bot, so the
    // silhouette says both "machine" and "facing that way" before the lid art is legible.
    const hullGeometry = new THREE.BoxGeometry(size * 0.78, BOT_HEIGHT, size * 0.56);
    const hullMaterial = new THREE.MeshStandardMaterial({
      color: accent.clone().multiplyScalar(0.3),
      roughness: 0.4,
      metalness: 0.7,
      emissive: accent,
      // Emission rather than reflection: the arena is deliberately unlit underfoot, so a
      // hull that only reflected would be as invisible as the decal it replaced.
      emissiveIntensity: 0.42,
    });
    const hull = new THREE.Mesh(hullGeometry, hullMaterial);
    hull.position.y = BOT_HEIGHT / 2;
    hull.castShadow = true;
    hull.receiveShadow = true;
    chassis.add(hull);

    const lidGeometry = new THREE.PlaneGeometry(size, size);
    lidGeometry.rotateX(-Math.PI / 2);
    const lidMaterial = new THREE.MeshStandardMaterial({
      map: spriteTexture(look.image),
      transparent: true,
      // Sprites have hard alpha edges; testing rather than blending keeps them from
      // sorting against each other and the floor.
      alphaTest: 0.35,
      roughness: 0.5,
      metalness: 0.35,
      emissive: accent,
      emissiveIntensity: 0.35,
    });
    const lid = new THREE.Mesh(lidGeometry, lidMaterial);
    lid.position.y = BOT_HEIGHT + 0.004;
    chassis.add(lid);

    // A pool of accent light under the bot. The arena floor is near black, and a shadow
    // alone tells you where a bot is not — this tells you where it is.
    const glowGeometry = new THREE.PlaneGeometry(size * 2.1, size * 2.1);
    glowGeometry.rotateX(-Math.PI / 2);
    const glowMaterial = new THREE.MeshBasicMaterial({
      map: radialGlow(accent),
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      opacity: 0.5,
    });
    const glow = new THREE.Mesh(glowGeometry, glowMaterial);
    glow.position.y = 0.012;
    chassis.add(glow);

    chassis.visible = false;
    group.add(chassis);
    disposables.push(hullGeometry, hullMaterial, lidGeometry, lidMaterial, glowGeometry, glowMaterial);
    return { chassis };
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

  const projectileGeometry = new THREE.PlaneGeometry(0.6, 0.6);
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
      const bot = bots[pose.slot];
      if (!bot) continue;
      bot.chassis.visible = pose.status === 'Active';
      bot.chassis.position.set(pose.x + 0.5, 0, pose.y + 0.5);
      // The whole chassis turns, so the hull's long axis reads as the facing even when the
      // lid art is too small to make out. `angle` is the same interpolated rotation the 2D
      // renderer uses, so both viewers swing a bot through exactly the same arc.
      bot.chassis.rotation.y = -pose.angle;
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

/**
 * A soft radial pool of colour, drawn once per bot.
 *
 * Generated rather than shipped: it is a gradient, and adding an asset for something a
 * canvas can draw in six lines would be a download for every player to save this.
 */
function radialGlow(accent: THREE.Color): THREE.Texture | null {
  if (typeof document === 'undefined') return null;
  const size = 128;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext('2d');
  if (!context) return null;

  const gradient = context.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
  const rgb = `${Math.round(accent.r * 255)}, ${Math.round(accent.g * 255)}, ${Math.round(accent.b * 255)}`;
  gradient.addColorStop(0, `rgba(${rgb}, 0.85)`);
  gradient.addColorStop(0.45, `rgba(${rgb}, 0.22)`);
  gradient.addColorStop(1, `rgba(${rgb}, 0)`);
  context.fillStyle = gradient;
  context.fillRect(0, 0, size, size);

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
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
