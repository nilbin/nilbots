import * as THREE from 'three';
import type { ReplayDocument } from '../types';
import { botLook, projectileLook, presentationAccent } from '../render/arenaThemes';
import { boltsAt, headingAngle, posesAt } from '../render/interpolate';
import { replayMaxHealth } from '../replayMetadata';
import { chassisModel } from './chassisModel';
import { CAMERA_PITCH } from './arenaScene';

/**
 * The things that move.
 *
 * A **bot is a solid**, extruded from its own sprite by `chassisModel` — so a Vanguard has
 * a Vanguard's silhouette from any angle, casts a Vanguard-shaped shadow, and reads as a
 * machine standing on the floor.
 *
 * **A projectile is a rig**: the same extruded silhouette, painted in the owner's accent
 * because the flat renderer paints it too, plus a tracer stretched out behind and a pool of
 * its own light on the floor below. It hovers, banks through its turns and wobbles, so a
 * bolt reads as a thing in the air rather than a mark sliding across the ground.
 *
 * Everything else here is what the arena needs to *say*: which bot is being followed, how
 * much health each has left, and which of them can be seen from where.
 */

/** How tall a bot's hull stands. Below the walls, so cover still reads as cover. */
const BOT_HEIGHT = 0.26;
const PROJECTILE_HOVER = 0.2;

/** Where health pips hang above the floor. */
const PIP_HEIGHT = 0.72;

/**
 * Clearance between the selection ring's outer edge and the pips above it.
 *
 * The setback is derived from the ring rather than fixed, because the ring is sized from the
 * chassis and the chassis are not all one size. A constant put the pips on top of the ring
 * for a bot of average scale and would have been wrong in the other direction for the rest.
 */
const PIP_CLEARANCE = 0.3;

/**
 * Share of the remaining turn a bolt takes each frame.
 *
 * Frame-rate dependent, deliberately. The honest form is `1 - exp(-dt/τ)`, and it is not
 * worth it: this is a cosmetic bank through a corner lasting a few frames, the renderer
 * runs on `requestAnimationFrame` in a narrow band of refresh rates, and a bolt that banks
 * marginally faster on a 120 Hz screen is not something anyone can see.
 */
const BOLT_TURN_RATE = 0.22;

export interface ArenaActors {
  group: THREE.Group;
  /** Move everything to where it should be at this moment of the replay. */
  update: (time: number, selectedSlot: number | null, showVisibility: boolean) => void;
  dispose: () => void;
}

export function buildActors(replay: ReplayDocument): ArenaActors {
  const group = new THREE.Group();
  const disposables: { dispose: () => void }[] = [];
  const { participants } = replay.header;
  // Rules-owned, and read rather than assumed: three-health replays predate the field.
  const maxHealth = replayMaxHealth(replay);
  // A replay can be closed while a chassis is still being fetched and triangulated. Adding
  // the model to a torn-down scene would resurrect meshes nothing will ever dispose.
  let live = true;

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

    // A pool of accent light under the bot, and the **only** place the owner's colour
    // appears on a bot in this renderer — which makes it load-bearing rather than
    // decoration. The flat renderer casts the same pool, so this is a match rather than an
    // invention; it just has more work to do here, because a lit chassis wears its own
    // paint and two players fielding the same look are otherwise the same object.
    const glowGeometry = new THREE.PlaneGeometry(size * 2.4, size * 2.4);
    glowGeometry.rotateX(-Math.PI / 2);
    const glowMaterial = new THREE.MeshBasicMaterial({
      map: radialGlow(accent),
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      opacity: 0.72,
    });
    const glow = new THREE.Mesh(glowGeometry, glowMaterial);
    glow.position.y = 0.012;
    chassis.add(glow);

    // Every material this bot is allowed to fade, with the opacity it wants at full
    // strength — a glow pool at 1.0 is not the same picture as a hull at 1.0.
    const fading: { material: THREE.Material; base: number }[] = [
      { material: hullMaterial, base: 1 },
      { material: lidMaterial, base: 1 },
      { material: glowMaterial, base: 0.72 },
    ];

    // Swap the box for the real thing once the sprite has been parsed and triangulated.
    //
    // The box is a placeholder, not the design. It is here because the model arrives over a
    // fetch and a first frame with nothing on the floor is worse than a first frame with a
    // block on it — but a block is what a chassis looks like when this step is missing,
    // which is exactly how it shipped once.
    void chassisModel(look.imageUrl).then((model) => {
      if (!live || !model) return;
      // Cloned because the parse is cached per look, and a mirror match would otherwise
      // have both bots claiming one Group — three.js reparents rather than shares, so the
      // second bot would silently steal the first one's body. The clone shares geometry,
      // which is the expensive half and the point of caching in the first place.
      const body = model.clone();
      body.scale.setScalar(size);
      // The materials, though, have to be this bot's own. Fog ghosts a bot by dropping its
      // opacity, and the cached ones are shared with the other bot wearing the same look
      // and with every replay opened after this one — so fading through them would dim both
      // bots at once and leave them dim for the rest of the session.
      body.traverse((node) => {
        const mesh = node as THREE.Mesh;
        if (!mesh.isMesh || Array.isArray(mesh.material)) return;
        mesh.material = mesh.material.clone();
        fading.push({ material: mesh.material, base: 1 });
        disposables.push(mesh.material);
      });
      chassis.add(body);
      chassis.remove(hull);
      chassis.remove(lid);
    });

    // A ring on the floor for the bot the panel is following. The flat renderer draws a
    // dashed circle around the sprite; a ring lying on the floor is the same statement in a
    // scene, and it survives the bot being behind a wall because it is drawn additively.
    const ringOuter = size * 0.9;
    const ringGeometry = new THREE.RingGeometry(size * 0.82, ringOuter, 40);
    ringGeometry.rotateX(-Math.PI / 2);
    const ringMaterial = new THREE.MeshBasicMaterial({
      color: accent,
      transparent: true,
      opacity: 0.6,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const ring = new THREE.Mesh(ringGeometry, ringMaterial);
    ring.position.y = 0.02;
    ring.visible = false;
    chassis.add(ring);

    // Health, as pips floating over the bot — the one piece of state the flat renderer puts
    // on the arena rather than in a panel, because it is the thing you need while watching
    // rather than afterwards.
    //
    // **Not a child of the chassis**, which is the obvious place for them and the wrong one.
    // The chassis turns with the bot, so pips parented to it turn too: edge-on whenever the
    // bot faced east, and the offset that lifts them clear of the hull swinging round with
    // the facing. They live in world space and are moved to follow instead.
    const pips = new THREE.Group();
    const pipGeometry = new THREE.CircleGeometry(0.06, 12);
    const litPip = new THREE.MeshBasicMaterial({
      color: accent,
      transparent: true,
      depthWrite: false,
      // Health is information, not scenery: it stays legible over the bot's own hull, which
      // is exactly where it lands from a raised camera.
      depthTest: false,
    });
    const lostPip = new THREE.MeshBasicMaterial({
      color: new THREE.Color('#64748b'),
      transparent: true,
      opacity: 0.35,
      depthWrite: false,
      depthTest: false,
    });
    const pipMeshes: THREE.Mesh[] = [];
    for (let index = 0; index < maxHealth; index++) {
      const pip = new THREE.Mesh(pipGeometry, litPip);
      pip.position.x = (index - (maxHealth - 1) / 2) * 0.17;
      // Square to the camera. It never rolls or orbits, so its pitch is a constant and one
      // rotation is enough — a billboard would be a per-frame lookAt for a fixed picture.
      pip.rotation.x = -CAMERA_PITCH;
      pip.renderOrder = 10;
      pips.add(pip);
      pipMeshes.push(pip);
    }
    group.add(pips);
    disposables.push(ringGeometry, ringMaterial, pipGeometry, litPip, lostPip);

    chassis.visible = false;
    group.add(chassis);
    disposables.push(hullGeometry, hullMaterial, lidGeometry, lidMaterial, glowGeometry, glowMaterial);

    fading.push(
      { material: ringMaterial, base: 0.6 },
      { material: litPip, base: 1 },
      { material: lostPip, base: 0.35 },
    );
    const fade = (factor: number) => {
      for (const { material, base } of fading) {
        material.opacity = base * factor;
        material.transparent = factor < 1 || base < 1;
      }
    };

    return {
      chassis,
      ring,
      pips,
      pipSetback: ringOuter + PIP_CLEARANCE,
      pipMeshes,
      litPip,
      lostPip,
      fading,
      fade,
    };
  });

  // A bolt is a rig, not a sprite: a glowing silhouette of the owner's projectile look, a
  // tracer stretched out behind it, and a pool of its own light on the floor under it.
  //
  // Pooled **per owner** rather than globally. Each slot fires a different look in a
  // different colour, so one shared pool would mean swapping a mesh's geometry *and*
  // material every frame a bolt changed hands — and there are only ever a handful in the
  // air, so the saving from sharing is smaller than the cost of the churn.
  const tracerGeometry = new THREE.PlaneGeometry(1, 0.34);
  tracerGeometry.rotateX(-Math.PI / 2);
  tracerGeometry.translate(-0.5, 0, 0);
  const glowDisc = new THREE.PlaneGeometry(1.5, 1.5);
  glowDisc.rotateX(-Math.PI / 2);
  disposables.push(tracerGeometry, glowDisc);

  const arsenals = participants.map((participant, slot) => {
    const look = projectileLook(participant?.projectileLookId);
    const accent = new THREE.Color(
      presentationAccent(botLook(participant?.lookId, slot), participant?.accent ?? '#38bdf8'),
    );
    const tracerMaterial = new THREE.MeshBasicMaterial({
      map: tracerTexture(accent),
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    const wash = new THREE.MeshBasicMaterial({
      map: radialGlow(accent),
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      opacity: 0.45,
    });
    // Until the silhouette has been parsed, a bolt is a bright speck. It is in flight for
    // a fraction of a second and the model is usually cached before the first shot, so
    // this is a seam nobody sees rather than a fallback anybody relies on.
    const spark = new THREE.OctahedronGeometry(0.16);
    const sparkMaterial = new THREE.MeshBasicMaterial({ color: accent });
    disposables.push(tracerMaterial, wash, spark, sparkMaterial);

    const arsenal = {
      look,
      accent,
      rigs: [] as { group: THREE.Group; head: THREE.Group }[],
      model: null as THREE.Group | null,
      tracerMaterial,
      wash,
      spark,
      sparkMaterial,
    };

    void chassisModel(look.imageUrl, accent).then((model) => {
      if (!live || !model) return;
      arsenal.model = model;
      // Rigs already built are retrofitted; ones built later pick it up on creation.
      for (const rig of arsenal.rigs) dressHead(arsenal, rig.head);
    });
    return arsenal;
  });

  type Arsenal = (typeof arsenals)[number];

  // Facing carried between frames, keyed by the bolt's replay identity rather than its pool
  // slot — a pool index is reassigned the moment another bolt despawns, which would hand one
  // bolt's turn-in-progress to an unrelated one.
  const heading = new Map<string, number>();
  let lastTime = Number.NaN;

  function dressHead(arsenal: Arsenal, head: THREE.Group): void {
    head.clear();
    const model = arsenal.model;
    if (model) {
      const body = model.clone();
      body.scale.setScalar(arsenal.look.scale * 0.72);
      head.add(body);
    } else {
      head.add(new THREE.Mesh(arsenal.spark, arsenal.sparkMaterial));
    }
  }

  function borrow(slot: number, index: number): { group: THREE.Group; head: THREE.Group } {
    const arsenal = arsenals[slot] ?? arsenals[0];
    while (arsenal.rigs.length <= index) {
      const rig = new THREE.Group();
      const head = new THREE.Group();
      head.position.y = PROJECTILE_HOVER;
      dressHead(arsenal, head);

      // The tracer trails *behind* the head — geometry is pre-translated so the quad hangs
      // off the origin's −x, which is backwards along the heading once the rig turns.
      const tracer = new THREE.Mesh(tracerGeometry, arsenal.tracerMaterial);
      tracer.position.set(0, PROJECTILE_HOVER - 0.02, 0);
      tracer.scale.x = 1.35;

      const wash = new THREE.Mesh(glowDisc, arsenal.wash);
      wash.position.y = 0.016;

      rig.add(head, tracer, wash);
      rig.visible = false;
      group.add(rig);
      arsenal.rigs.push({ group: rig, head });
    }
    return arsenal.rigs[index];
  }

  const update = (time: number, selectedSlot: number | null, showVisibility: boolean) => {
    const tick = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));
    // What the followed bot could see this tick. The flat renderer ghosts an enemy it has
    // no line on rather than removing it, because the panel answers "what did this bot
    // know?" and an unseen opponent drawn at full strength would be lying.
    const fogSource =
      showVisibility && selectedSlot !== null
        ? replay.ticks[tick]?.bots.find((bot) => bot.slot === selectedSlot)
        : undefined;
    const hidden = (slot: number) =>
      fogSource !== undefined &&
      slot !== selectedSlot &&
      !fogSource.visibleEnemies.some((enemy) => enemy.slot === slot);

    for (const pose of posesAt(replay, time)) {
      const bot = bots[pose.slot];
      if (!bot) continue;
      bot.chassis.visible = pose.status === 'Active';
      bot.chassis.position.set(pose.x + 0.5, 0, pose.y + 0.5);
      bot.ring.visible = pose.slot === selectedSlot && pose.status === 'Active';

      // Pips follow rather than ride, and sit forward of the bot in *screen* terms — a
      // raised camera projects height towards the viewer, so lifting them alone would drop
      // them onto the hull rather than above it.
      bot.pips.visible = bot.chassis.visible;
      bot.pips.position.set(pose.x + 0.5, PIP_HEIGHT, pose.y + 0.5 - bot.pipSetback);
      for (const [index, pip] of bot.pipMeshes.entries())
        pip.material = index < pose.health ? bot.litPip : bot.lostPip;

      bot.fade(hidden(pose.slot) ? 0.15 : 1);
      // The whole chassis turns, so the hull's long axis reads as the facing even when the
      // lid art is too small to make out. `angle` is the same interpolated rotation the 2D
      // renderer uses, so both viewers swing a bot through exactly the same arc.
      bot.chassis.rotation.y = -pose.angle;
    }

    // `boltsAt` is the same derivation the flat renderer uses — interpolated across the
    // authoritative substeps, so a bolt flies rather than teleporting between tiles the way
    // reading `ticks[tick].projectiles` straight made it.
    //
    // A scrub is not motion. Dragging the playhead across half a match would otherwise have
    // every bolt easing through the turns it "made" on the way, so a jump snaps instead.
    const jumped = Math.abs(time - lastTime) > 1.5;
    lastTime = time;

    // In FOV mode a bolt the followed bot cannot see is not drawn at all, rather than
    // ghosted the way a bot is. An unseen bolt is precisely the threat it does not know
    // about, and a faint one on screen would still answer the question.
    const seen =
      fogSource !== undefined
        ? new Set(fogSource.visibleTiles.map(([x, y]) => `${x},${y}`))
        : null;

    const flying = arsenals.map(() => 0);
    const alive = new Set<string>();
    for (const bolt of boltsAt(replay, time)) {
      if (seen && !seen.has(`${Math.round(bolt.x)},${Math.round(bolt.y)}`)) continue;
      const slot = arsenals[bolt.ownerSlot] ? bolt.ownerSlot : 0;
      const rig = borrow(slot, flying[slot]++);
      rig.group.visible = true;
      rig.group.position.set(bolt.x + 0.5, 0, bolt.y + 0.5);

      // Bolts turn, they do not pivot. The engine's headings are eight discrete octants, so
      // a programmed arc changes facing by 45° between one substep and the next — correct,
      // and it reads as the bolt blinking into a new orientation. Easing towards the
      // authoritative heading lets it bank through the corner instead. Position is never
      // eased, only facing: where a bolt *is* stays exactly what the replay recorded.
      const key = `${slot}:${bolt.id}`;
      alive.add(key);
      const target = -headingAngle[bolt.heading];
      const memory = heading.get(key);
      const turned =
        memory === undefined || jumped
          ? target
          : memory + shortestTurn(memory, target) * BOLT_TURN_RATE;
      heading.set(key, turned);
      rig.group.rotation.y = turned;

      // And a bolt in flight is not on rails. A small bob and roll, out of phase per bolt so
      // two in the air never move as one, which is what makes a pair read as two objects.
      const phase = bolt.id * 2.399;
      rig.head.position.y = PROJECTILE_HOVER + Math.sin(time * 8.5 + phase) * 0.032;
      rig.head.rotation.x = Math.sin(time * 6.1 + phase) * 0.22;
      rig.head.rotation.z = Math.sin(time * 4.7 + phase * 1.7) * 0.12;
    }
    // Forget bolts that have landed, or the map grows for the length of the replay.
    for (const key of heading.keys()) if (!alive.has(key)) heading.delete(key);
    // Everything not claimed this frame goes back in the pool rather than being destroyed.
    arsenals.forEach((arsenal, slot) => {
      for (let index = flying[slot]; index < arsenal.rigs.length; index++)
        arsenal.rigs[index].group.visible = false;
    });
  };

  return {
    group,
    update,
    dispose: () => {
      live = false;
      for (const item of disposables) item.dispose();
      // Chassis geometry and materials are deliberately not disposed here: they are owned
      // by the module-level parse cache and shared by every replay this page opens. Freeing
      // them would leave the next match holding disposed buffers.
    },
  };
}

/** The signed angle to turn through, taking the short way round. */
function shortestTurn(from: number, to: number): number {
  let delta = to - from;
  while (delta > Math.PI) delta -= 2 * Math.PI;
  while (delta < -Math.PI) delta += 2 * Math.PI;
  return delta;
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

/**
 * The streak a bolt drags behind it: bright at the head, gone by the tail.
 *
 * The flat renderer draws this with two additive strokes and a shadow blur. Here it is one
 * quad with a gradient, which is the same picture for one draw call — and unlike the
 * strokes it does not have to be rebuilt per frame, which is the thing currently making
 * fullscreen playback stutter in the other renderer.
 */
function tracerTexture(accent: THREE.Color): THREE.Texture | null {
  if (typeof document === 'undefined') return null;
  const width = 128;
  const height = 32;
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext('2d');
  if (!context) return null;

  const rgb = `${Math.round(accent.r * 255)}, ${Math.round(accent.g * 255)}, ${Math.round(accent.b * 255)}`;
  // Left edge is the tail, right edge the head, because the quad is laid out along −x and
  // read back to front.
  const along = context.createLinearGradient(0, 0, width, 0);
  along.addColorStop(0, `rgba(${rgb}, 0)`);
  along.addColorStop(0.65, `rgba(${rgb}, 0.32)`);
  along.addColorStop(1, `rgba(${rgb}, 0.85)`);
  context.fillStyle = along;
  context.fillRect(0, 0, width, height);

  // Soften the long edges so the streak is a beam rather than a ribbon with corners.
  const across = context.createLinearGradient(0, 0, 0, height);
  across.addColorStop(0, 'rgba(0, 0, 0, 1)');
  across.addColorStop(0.5, 'rgba(0, 0, 0, 0)');
  across.addColorStop(1, 'rgba(0, 0, 0, 1)');
  context.globalCompositeOperation = 'destination-out';
  context.fillStyle = across;
  context.fillRect(0, 0, width, height);

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
}

/** Rasterised sprite size. Generous: these are read close-up under a perspective camera. */
const SPRITE_PIXELS = 256;

/**
 * Rasterise a chassis sprite into a texture, for the placeholder lid.
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
