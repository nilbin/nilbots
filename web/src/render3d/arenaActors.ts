import * as THREE from 'three';
import type {
  ReplayActorLifeKey,
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import { botLook, projectileLook, presentationAccent } from '../render/arenaThemes';
import {
  boltsAt,
  directionAngle,
  headingAngle,
  posesAt,
} from '../render/interpolate';
import { replayMaxHealth } from '../replayMetadata';
import {
  participantForUnit,
  visualIndexForUnit,
} from '../replayParticipants';
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
/** The height bolts fly at. Exported because a bolt's dissipation has to happen there too. */
export const PROJECTILE_HOVER = 0.2;

/** Where health pips hang: above the floor, and back along Z to clear the bot on screen. */
const PIP_HEIGHT = 0.72;
const PIP_SETBACK = 0.55;

/**
 * How a followed bot is lit up.
 *
 * The gain is **multiplicative, not additive**, and that distinction is the whole design.
 * A hull grey here is near-black and barely lit, so *adding* even 0.05 of emission to it is
 * comparable to everything else it receives — two attempts at a flat add both came out as a
 * solid teal lozenge, the exact failure that took accent off these models to begin with.
 * Multiplying leaves near-zero near zero and lifts what the artist already drew bright, so
 * the followed bot's trim glows and its hull stays hull.
 *
 * The pool of light underneath does the rest, since it is accent-coloured already.
 */
const SELECTED_TRIM_GAIN = 2.6;
const SELECTED_TINT = 0.12;
const SELECTED_POOL = 1;
const UNSELECTED_POOL = 0.72;

/** Emission added at the peak of a hit, over whatever the material already emits. */
const HIT_FLASH = 1.6;

/**
 * Idle life: what a bot does when it is doing nothing.
 *
 * Every axis is a sum of two incommensurate rates, offset by stable unit identity so allied
 * bodies never breathe together. Vertical motion remains lighter than lateral motion:
 * these are ground machines, and a bot bobbing like a projectile reads as hovering.
 */
const IDLE_SWAY = 0.05;
const IDLE_RISE = 0.022;
const IDLE_ROLL = 0.055;
const IDLE_YAW = 0.05;

/**
 * How hard a bot drifts through a corner.
 *
 * A tile grid only ever asks for 90° turns, and taken flat that is a chassis snapping to a
 * new heading — correct, and lifeless. So the body over-rotates into the corner, banks, and
 * lets its back end step out: a handbrake turn.
 *
 * The slide has to outlive the rotation. A one-tick angular-rate spike is technically
 * correct and visually gone before it registers; a short response curve lets slip build
 * through a corner and recover over the following tick.
 */
const DRIFT_YAW = 0.55;
const DRIFT_LEAN = 0.4;
const DRIFT_SLIDE = 0.26;

function driftResponse(age: number): number {
  return Math.exp(-(((age - 0.85) / 0.72) ** 2));
}

const WHITE = new THREE.Color(0xffffff);

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
  update: (
    time: number,
    selectedUnitKey: ReplayStableUnitKey | null,
    showVisibility: boolean,
  ) => void;
  /** Which bot, if any, is under a ray cast from the camera. */
  pick: (raycaster: THREE.Raycaster) => ReplayStableUnitKey | null;
  dispose: () => void;
}

export function buildActors(replay: ReplayModel): ArenaActors {
  const group = new THREE.Group();
  const disposables: { dispose: () => void }[] = [];
  // Rules-owned and form-extensible: this is only the allocation ceiling. Per-frame
  // visibility still uses the effective form's own maximum.
  const maxHealth = replayMaxHealth(replay);
  // A replay can be closed while a chassis is still being fetched and triangulated. Adding
  // the model to a torn-down scene would resurrect meshes nothing will ever dispose.
  let live = true;

  const bots = replay.units.map((unit) => {
    const participant = participantForUnit(replay, unit.unitKey);
    const visualIndex = visualIndexForUnit(replay, unit.unitKey);
    const look = botLook(participant?.lookId ?? undefined, visualIndex);
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
    // The machine itself, separate from the light it casts on the floor: recoil kicks this
    // and leaves the pool where it is, because a bot's shadow does not jump when it fires.
    const body = new THREE.Group();
    body.userData.renderForm = 'mobile';
    chassis.add(body);

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
    body.add(hull);

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
    body.add(lid);

    // A turret is deliberately a different silhouette, not the mobile chassis with its
    // movement disabled. The circular base has no privileged facing and the eight radial
    // vanes advertise absolute eight-way fire at a glance. Its group is switched from the
    // mobile body only when `posesAt` exposes the authoritative effective form.
    const turret = new THREE.Group();
    turret.visible = false;
    turret.userData.renderForm = 'stationary-omnidirectional';
    const turretBaseGeometry = new THREE.CylinderGeometry(
      size * 0.42,
      size * 0.5,
      BOT_HEIGHT * 0.78,
      24,
    );
    const turretMaterial = new THREE.MeshStandardMaterial({
      color: new THREE.Color('#293443'),
      roughness: 0.34,
      metalness: 0.82,
      emissive: accent,
      emissiveIntensity: 0.24,
    });
    const turretBase = new THREE.Mesh(turretBaseGeometry, turretMaterial);
    turretBase.position.y = (BOT_HEIGHT * 0.78) / 2;
    turretBase.castShadow = true;
    turretBase.receiveShadow = true;
    turret.add(turretBase);

    const turretCoreGeometry = new THREE.CylinderGeometry(
      size * 0.2,
      size * 0.27,
      BOT_HEIGHT * 0.48,
      16,
    );
    const turretCore = new THREE.Mesh(
      turretCoreGeometry,
      turretMaterial,
    );
    turretCore.position.y = BOT_HEIGHT * 0.86;
    turretCore.castShadow = true;
    turret.add(turretCore);

    const vaneGeometry = new THREE.BoxGeometry(
      size * 0.34,
      BOT_HEIGHT * 0.13,
      size * 0.055,
    );
    vaneGeometry.translate(size * 0.25, 0, 0);
    const vaneMaterial = new THREE.MeshStandardMaterial({
      color: accent.clone().multiplyScalar(0.45),
      roughness: 0.28,
      metalness: 0.72,
      emissive: accent,
      emissiveIntensity: 0.7,
    });
    for (let octant = 0; octant < 8; octant++) {
      const vane = new THREE.Mesh(vaneGeometry, vaneMaterial);
      vane.position.y = BOT_HEIGHT * 0.93;
      vane.rotation.y = (octant * Math.PI) / 4;
      vane.castShadow = true;
      turret.add(vane);
    }
    chassis.add(turret);

    // Anchor windup is state, not a guessed animation: the ring appears only while the
    // normalized actor carries an authoritative pending transition.
    const anchorGeometry = new THREE.TorusGeometry(
      size * 0.57,
      Math.max(0.018, size * 0.025),
      8,
      32,
    );
    anchorGeometry.rotateX(Math.PI / 2);
    const anchorMaterial = new THREE.MeshBasicMaterial({
      color: accent,
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    const anchorRing = new THREE.Mesh(anchorGeometry, anchorMaterial);
    anchorRing.userData.cue = 'form-transition-pending';
    anchorRing.position.y = 0.045;
    anchorRing.visible = false;
    chassis.add(anchorRing);

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

    // A generous invisible target for taps. `visible = false` would take it out of
    // raycasting too, so it is transparent with no colour written instead.
    const padGeometry = new THREE.CircleGeometry(0.62, 16);
    padGeometry.rotateX(-Math.PI / 2);
    const padMaterial = new THREE.MeshBasicMaterial({
      transparent: true,
      opacity: 0,
      depthWrite: false,
      colorWrite: false,
    });
    const pad = new THREE.Mesh(padGeometry, padMaterial);
    pad.position.y = 0.05;
    pad.userData.unitKey = unit.unitKey;
    chassis.add(pad);
    chassis.userData.unitKey = unit.unitKey;
    chassis.userData.defaultFormId = unit.initialFormId;
    disposables.push(padGeometry, padMaterial);

    // Every material this bot is allowed to fade, with the opacity it wants at full
    // strength — a glow pool at 1.0 is not the same picture as a hull at 1.0.
    // The pool's base opacity is not a constant: following a bot brightens it, and fog can
    // fade it, so the two have to compose rather than overwrite each other.
    const glowFade = { material: glowMaterial as THREE.Material, base: UNSELECTED_POOL };
    let lastFactor = 1;
    const fading: { material: THREE.Material; base: number }[] = [
      { material: hullMaterial, base: 1 },
      { material: lidMaterial, base: 1 },
      { material: turretMaterial, base: 1 },
      { material: vaneMaterial, base: 1 },
      glowFade,
    ];

    // What the highlight may repaint, each remembering the colours it wears unselected —
    // a tint that cannot be undone is a bot that stays lit after you follow another one.
    const tinting: {
      material: THREE.MeshStandardMaterial;
      baseColour: THREE.Color;
      baseEmissive: THREE.Color;
      baseIntensity: number;
    }[] = [];
    const tintable = (material: THREE.MeshStandardMaterial) =>
      tinting.push({
        material,
        baseColour: material.color.clone(),
        baseEmissive: material.emissive.clone(),
        baseIntensity: material.emissiveIntensity,
      });
    tintable(hullMaterial);
    tintable(lidMaterial);
    tintable(turretMaterial);
    tintable(vaneMaterial);

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
      const solid = model.clone();
      solid.scale.setScalar(size);
      // The materials, though, have to be this bot's own. Fog ghosts a bot by dropping its
      // opacity, and the cached ones are shared with the other bot wearing the same look
      // and with every replay opened after this one — so fading through them would dim both
      // bots at once and leave them dim for the rest of the session.
      solid.traverse((node) => {
        const mesh = node as THREE.Mesh;
        if (!mesh.isMesh || Array.isArray(mesh.material)) return;
        mesh.material = mesh.material.clone();
        fading.push({ material: mesh.material, base: 1 });
        tintable(mesh.material as THREE.MeshStandardMaterial);
        disposables.push(mesh.material);
        // A model that lands mid-highlight or mid-flash has to arrive wearing it, not
        // plain — `repaint` is over the whole registry, so this is simply running it again
        // now that the registry has grown.
        repaint();
      });
      body.add(solid);
      body.remove(hull);
      body.remove(lid);
    });

    // Following a bot lights *the bot*, not a ring drawn near it.
    //
    // A marker beside the thing is a marker you have to look away to read; the bot itself
    // carrying the state is one glance. A ring was tried first and it was never the right
    // shape of answer — too wide and it became a halo louder than the arena, tight enough
    // to hug and it read as drawn across the chassis even though the depth buffer had it
    // correctly behind.
    //
    // **This is not the accent tint that was removed** (DECISIONS #127). That one washed
    // every bot in team colour permanently, which is identity the flat renderer does not
    // give, and it flattened twelve chassis into one silhouette. This is *state*, on one bot
    // at a time, and it is deliberately weak: enough to lift the followed bot off the floor
    // and no further, so the chassis is still the chassis you picked.
    let highlighted = false;
    let flashing = 0;

    // Selection and a hit both repaint the same materials, so they are applied together
    // from one place. Written as two independent passes they would take turns clobbering
    // each other, and a followed bot would stop flashing when it was shot.
    const repaint = () => {
      for (const { material, baseColour, baseEmissive, baseIntensity } of tinting) {
        material.color
          .copy(baseColour)
          .lerp(accent, highlighted ? SELECTED_TINT : 0)
          .lerp(WHITE, flashing * 0.55);
        material.emissive.copy(baseEmissive).lerp(WHITE, flashing);
        material.emissiveIntensity =
          baseIntensity * (highlighted ? SELECTED_TRIM_GAIN : 1) + flashing * HIT_FLASH;
      }
    };

    // Both guard on change. They run per frame per bot, and re-deriving a dozen colours
    // sixty times a second to arrive at the answer already on screen is work for nothing.
    const highlight = (on: boolean) => {
      if (on === highlighted) return;
      highlighted = on;
      glowFade.base = on ? SELECTED_POOL : UNSELECTED_POOL;
      repaint();
      fade(lastFactor);
    };
    const flash = (strength: number) => {
      const clamped = Math.max(0, Math.min(strength, 1));
      if (clamped === flashing) return;
      flashing = clamped;
      repaint();
    };

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
    disposables.push(pipGeometry, litPip, lostPip);

    chassis.visible = false;
    group.add(chassis);
    disposables.push(
      hullGeometry,
      hullMaterial,
      lidGeometry,
      lidMaterial,
      turretBaseGeometry,
      turretCoreGeometry,
      turretMaterial,
      vaneGeometry,
      vaneMaterial,
      anchorGeometry,
      anchorMaterial,
      glowGeometry,
      glowMaterial,
    );

    fading.push(
      { material: litPip, base: 1 },
      { material: lostPip, base: 0.35 },
    );
    const fade = (factor: number) => {
      lastFactor = factor;
      for (const { material, base } of fading) {
        material.opacity = base * factor;
        material.transparent = factor < 1 || base < 1;
      }
    };

    return {
      unitKey: unit.unitKey,
      motionPhase: phaseForIdentity(unit.unitKey),
      chassis,
      body,
      turret,
      anchorRing,
      anchorMaterial,
      pad,
      highlight,
      flash,
      pips,
      pipMeshes,
      litPip,
      lostPip,
      fading,
      fade,
    };
  });
  const botsByUnit = new Map(
    bots.map((bot) => [bot.unitKey, bot]),
  );

  // A bolt is a rig, not a sprite: a glowing silhouette of the owner's projectile look, a
  // tracer stretched out behind it, and a pool of its own light on the floor under it.
  //
  // Pooled **per stable unit** rather than globally. Units can share a participant and
  // still need independent pools because old-life projectiles retain their exact owner
  // while a new life occupies the same unit.
  // different colour, so one shared pool would mean swapping a mesh's geometry *and*
  // material every frame a bolt changed hands — and there are only ever a handful in the
  // air, so the saving from sharing is smaller than the cost of the churn.
  const tracerGeometry = new THREE.PlaneGeometry(1, 0.34);
  tracerGeometry.rotateX(-Math.PI / 2);
  tracerGeometry.translate(-0.5, 0, 0);
  const glowDisc = new THREE.PlaneGeometry(1.5, 1.5);
  glowDisc.rotateX(-Math.PI / 2);
  disposables.push(tracerGeometry, glowDisc);

  const arsenalIndexByUnit = new Map(
    replay.units.map((unit, index) => [unit.unitKey, index]),
  );
  const arsenals = replay.units.map((unit) => {
    const participant = participantForUnit(replay, unit.unitKey);
    const visualIndex = visualIndexForUnit(replay, unit.unitKey);
    const look = projectileLook(participant?.projectileLookId ?? undefined);
    const accent = new THREE.Color(
      presentationAccent(
        botLook(participant?.lookId ?? undefined, visualIndex),
        participant?.accent ?? '#38bdf8',
      ),
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
      unitKey: unit.unitKey,
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

  function borrow(
    unitKey: ReplayStableUnitKey,
    index: number,
  ): { group: THREE.Group; head: THREE.Group } {
    const arsenal =
      arsenals[arsenalIndexByUnit.get(unitKey) ?? 0] ?? arsenals[0];
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

  const update = (
    time: number,
    selectedUnitKey: ReplayStableUnitKey | null,
    showVisibility: boolean,
  ) => {
    const tick = Math.max(
      0,
      Math.min(Math.floor(time), replay.ticks.length - 1),
    );
    const currentTick = replay.ticks[tick];
    // What the followed bot could see this tick. The flat renderer ghosts an enemy it has
    // no line on rather than removing it, because the panel answers "what did this bot
    // know?" and an unseen opponent drawn at full strength would be lying.
    const fogSource =
      showVisibility && selectedUnitKey !== null
        ? currentTick?.actorTurns.find(
            (turn) => turn.actor.unitKey === selectedUnitKey,
          )
        : undefined;
    const hidden = (pose: ReturnType<typeof posesAt>[number]) =>
      fogSource !== undefined &&
      pose.unitKey !== selectedUnitKey &&
      !fogSource.observation.allies.some(
        (ally) =>
          ally.actor.kind === 'exact' &&
          ally.actor.identity.actorKey === pose.actorKey,
      ) &&
      !fogSource.observation.enemies.some((enemy) =>
        enemy.actor.kind === 'exact'
          ? enemy.actor.identity.actorKey === pose.actorKey
          : enemy.actor.teamId === pose.teamId &&
            enemy.actor.unitId === pose.unitId,
      );

    const events = currentTick?.events ?? [];
    const fraction = Math.max(0, Math.min(time - tick, 1));
    // The facings this tick runs between, so a turn's *rate* can be derived rather than
    // remembered. Frame-to-frame memory would make a scrub read as a violent spin, and
    // would give a paused bot a drift that depended on how the playhead got there.
    const opening = currentTick?.before ?? replay.initialWorld;
    const closing = currentTick?.after ?? opening;
    // The tiles either side of this tick's, so movement can be splined through them.
    const previous = replay.ticks[tick - 1]?.before ?? opening;
    const earlier = replay.ticks[tick - 2]?.before ?? previous;
    const next = replay.ticks[tick + 1]?.after ?? closing;
    const turnedIn = [
      [opening, closing],
      [previous, opening],
      [earlier, previous],
    ] as const;
    // Beams and impacts land in the second half of the tick, after movement has settled —
    // the same window the flat renderer uses, so a hit lands at the same instant in both.
    const shotProgress = Math.max(0, Math.min((fraction - 0.45) / 0.45, 1));

    /**
     * Where a bot is, splined through the tiles either side of the one it is crossing.
     *
     * `posesAt` eases each tick independently, which is right for the flat renderer and
     * wrong here: a bot crossing four tiles in a row accelerates and comes to a **complete
     * stop** at every tile boundary, four times, which reads as stepping rather than
     * driving. A Catmull-Rom through the previous and next tiles gives continuous velocity
     * across a run of moves, and — because a stationary bot's neighbouring tiles are the
     * same tile — still eases in and out of a stop for free, with no special case.
     *
     * It also cuts corners very slightly, which on a grid that only turns 90° is exactly
     * what a machine carrying speed through a corner does.
     *
     * Position only. Facing still comes from `posesAt`, so both renderers swing a bot
     * through the same arc, and the tile a bot is *on* is never in question — this only
     * changes the path taken between two tiles the replay already recorded.
     */
    const glideAt = (actorKey: ReplayActorLifeKey) => {
      const at = (state: typeof opening) =>
        state?.actors.find((actor) => actor.actorKey === actorKey);
      const p1 = at(opening);
      const p2 = at(closing);
      if (!p1 || !p2) return { x: 0, y: 0 };
      const p0 = at(previous) ?? p1;
      const p3 = at(next) ?? p2;
      return {
        x: catmullRom(
          p0.position.x,
          p1.position.x,
          p2.position.x,
          p3.position.x,
          fraction,
        ),
        y: catmullRom(
          p0.position.y,
          p1.position.y,
          p2.position.y,
          p3.position.y,
          fraction,
        ),
      };
    };

    // Stable slots can be absent while locked, rebuilding, ready, or queued for
    // fabrication. Hide every reusable rig first so the prior frame cannot leave a
    // destroyed life behind.
    for (const bot of bots) {
      bot.chassis.visible = false;
      bot.pips.visible = false;
      bot.anchorRing.visible = false;
      bot.highlight(false);
      bot.flash(0);
    }

    for (const pose of posesAt(replay, time)) {
      const bot = botsByUnit.get(pose.unitKey);
      if (!bot) continue;

      const firing = events.some(
        (event) =>
          event.type === 'shot' &&
          event.sourceActor?.actorKey === pose.actorKey,
      );
      const struck = events.some(
        (event) =>
          event.type === 'damage' &&
          event.targetActor?.actorKey === pose.actorKey,
      );
      const dying = events.some(
        (event) =>
          event.type === 'destroyed' &&
          event.targetActor?.actorKey === pose.actorKey,
      );
      const collapse = dying
        ? Math.max(0, Math.min((fraction - 0.55) / 0.45, 1))
        : 0;
      const form =
        replay.forms.find((candidate) => candidate.formId === pose.formId) ??
        null;
      const stationary = form?.canMove === false;

      bot.chassis.visible = pose.status === 'active' || dying;
      bot.chassis.userData.actorKey = pose.actorKey;
      bot.chassis.userData.formId = pose.formId;
      bot.chassis.userData.stationary = stationary;
      bot.chassis.userData.omnidirectional =
        form?.omnidirectionalVision === true ||
        form?.omnidirectionalShooting === true;
      bot.body.visible = !stationary;
      bot.turret.visible = stationary;
      const glide = glideAt(pose.actorKey);
      bot.chassis.position.set(glide.x + 0.5, 0, glide.y + 0.5);
      bot.highlight(
        pose.unitKey === selectedUnitKey && pose.status === 'active',
      );

      // Pips follow rather than ride, and use the effective form's maximum. A mobile
      // child must not look wounded merely because its future turret form has more HP.
      bot.pips.visible = bot.chassis.visible;
      bot.pips.position.set(
        glide.x + 0.5,
        PIP_HEIGHT,
        glide.y + 0.5 - PIP_SETBACK,
      );
      const formMaxHealth = form?.maxHealth ?? maxHealth;
      for (const [index, pip] of bot.pipMeshes.entries()) {
        pip.visible = index < formMaxHealth;
        pip.material = index < pose.health ? bot.litPip : bot.lostPip;
      }

      // The windup cue comes from normalized pending state. It disappears on explicit
      // cancellation and the body switches only when `pose.formId` changes.
      const anchoring = pose.pendingFormTransition !== null;
      bot.anchorRing.visible = anchoring && bot.chassis.visible;
      if (anchoring) {
        const pulse = 0.72 + Math.sin(time * Math.PI * 4) * 0.18;
        bot.anchorMaterial.opacity = pulse;
        bot.anchorRing.scale.setScalar(0.92 + pulse * 0.16);
      }

      bot.flash(
        struck && shotProgress > 0.55
          ? (1 - shotProgress) / 0.45
          : 0,
      );
      const visibility = hidden(pose) ? 0.15 : 1;
      bot.fade(visibility * (1 - collapse * 0.75));
      if (anchoring)
        bot.anchorMaterial.opacity *= visibility * (1 - collapse);

      // Circular turret geometry has no privileged facing; keeping the authoritative
      // body rotation still makes a form change preserve exactly the life state recorded.
      bot.chassis.rotation.y = -pose.angle;

      let slip = 0;
      if (!stationary) {
        for (const [age, [fromState, toState]] of turnedIn.entries()) {
          const from = fromState?.actors.find(
            (actor) => actor.actorKey === pose.actorKey,
          );
          const to = toState?.actors.find(
            (actor) => actor.actorKey === pose.actorKey,
          );
          if (!from || !to) continue;
          slip +=
            shortestTurn(
              directionAngle(from.facing),
              directionAngle(to.facing),
            ) * driftResponse(fraction + age);
        }
      }
      const drift =
        Math.max(-1, Math.min(slip / (Math.PI / 2), 1)) *
        (1 - collapse);
      const idle =
        (stationary ? 0 : 1) *
        (1 - Math.abs(drift)) *
        (1 - collapse);
      const sway =
        Math.sin(time * 1.9 + bot.motionPhase) +
        Math.sin(time * 3.1 + bot.motionPhase * 2.3) * 0.55;
      const rise =
        Math.sin(time * 2.3 + bot.motionPhase * 1.7) +
        Math.sin(time * 1.3 + bot.motionPhase) * 0.5;
      const kick = firing
        ? Math.sin(shotProgress * Math.PI) * 0.14
        : 0;

      bot.chassis.rotation.z = collapse * 0.5;
      bot.chassis.rotation.x = collapse * 0.22;
      bot.chassis.position.y = -collapse * BOT_HEIGHT * 0.55;
      bot.chassis.scale.setScalar(1 - collapse * 0.16);
      bot.turret.scale.setScalar(
        1 +
          (firing
            ? Math.sin(shotProgress * Math.PI) * 0.045
            : 0),
      );

      bot.body.position.x = -kick;
      bot.body.position.z =
        drift * DRIFT_SLIDE + idle * sway * IDLE_SWAY;
      bot.body.position.y = idle * rise * IDLE_RISE;
      bot.body.rotation.y =
        -drift * DRIFT_YAW + idle * sway * IDLE_YAW;
      bot.body.rotation.x =
        drift * DRIFT_LEAN + idle * rise * IDLE_ROLL;
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
    const seenTiles =
      fogSource !== undefined
        ? new Set(
            fogSource.observation.visibleTiles.map(
              ({ position }) => `${position.x},${position.y}`,
            ),
          )
        : null;
    const seenProjectileIds =
      fogSource?.observation.visibleProjectiles === null ||
      fogSource === undefined
        ? null
        : new Set(
            fogSource.aliases.projectiles
              .filter((alias) =>
                fogSource.observation.visibleProjectiles!.some(
                  (projectile) =>
                    projectile.projectileHandle ===
                    alias.projectileHandle,
                ),
              )
              .map((alias) => alias.projectileId),
          );

    const flying = arsenals.map(() => 0);
    const alive = new Set<string>();
    for (const bolt of boltsAt(replay, time)) {
      if (
        seenProjectileIds !== null
          ? !seenProjectileIds.has(bolt.id)
          : seenTiles !== null &&
            !seenTiles.has(`${Math.round(bolt.x)},${Math.round(bolt.y)}`)
      )
        continue;
      const arsenalIndex =
        arsenalIndexByUnit.get(bolt.ownerActor.unitKey) ?? 0;
      const rig = borrow(
        bolt.ownerActor.unitKey,
        flying[arsenalIndex]++,
      );
      rig.group.visible = true;
      rig.group.position.set(bolt.x + 0.5, 0, bolt.y + 0.5);

      // Bolts turn, they do not pivot. The engine's headings are eight discrete octants, so
      // a programmed arc changes facing by 45° between one substep and the next — correct,
      // and it reads as the bolt blinking into a new orientation. Easing towards the
      // authoritative heading lets it bank through the corner instead. Position is never
      // eased, only facing: where a bolt *is* stays exactly what the replay recorded.
      const key = `${bolt.ownerActor.actorKey}:${bolt.id}`;
      alive.add(key);
      const target = -headingAngle[bolt.heading];
      const memory = heading.get(key);
      const turned =
        memory === undefined || jumped
          ? target
          : memory + shortestTurn(memory, target) * BOLT_TURN_RATE;
      heading.set(key, turned);
      rig.group.rotation.y = turned;

      // And a bolt in flight is not on rails — it drifts, up and across, and rolls as it
      // goes. Three things keep it from reading as a machine part on a cam:
      //
      // The **sideways** drift matters as much as the vertical one. Bobbing alone reads as
      // a bolt on a rail that happens to be springy; adding lateral wander makes it a thing
      // finding its way through air.
      //
      // Every bolt gets its own **phase and its own rates**, derived from its replay id.
      // Shared frequencies make two bolts in the air move as one object with a gap in it —
      // the tell that they are the same code and not two things. Derived rather than random
      // because a replay must look the same every time it is watched.
      const phase = phaseForIdentity(bolt.id);
      const wander =
        1 + (Math.floor(phase * 10_000) % 23) / 46;
      rig.head.position.y =
        PROJECTILE_HOVER +
        (Math.sin(time * 9.3 * wander + phase) + Math.sin(time * 5.1 + phase * 2.7) * 0.6) * 0.05;
      // Local −z is across the heading, since the rig is turned to face along +x.
      rig.head.position.z =
        (Math.sin(time * 6.7 * wander + phase * 1.9) + Math.sin(time * 3.9 + phase) * 0.5) * 0.055;
      rig.head.rotation.x = Math.sin(time * 6.1 * wander + phase) * 0.35;
      rig.head.rotation.z = Math.sin(time * 4.7 + phase * 1.7) * 0.18;
    }
    // Forget bolts that have landed, or the map grows for the length of the replay.
    for (const key of heading.keys()) if (!alive.has(key)) heading.delete(key);
    // Everything not claimed this frame goes back in the pool rather than being destroyed.
    arsenals.forEach((arsenal, slot) => {
      for (let index = flying[slot]; index < arsenal.rigs.length; index++)
        arsenal.rigs[index].group.visible = false;
    });
  };

  /**
   * Hit-test the bots.
   *
   * Against a **pad on the floor**, not against the chassis. A bot here is a few hundred
   * triangles of dart with gaps between its fins, and raycasting that would make selecting
   * one a test of aim — worse on a touch screen, where the finger is bigger than the bot.
   * The pad is a tile-sized disc under each machine, invisible and always facing up, which
   * is both easier to hit and closer to what a player means by "that one".
   */
  const pick = (
    raycaster: THREE.Raycaster,
  ): ReplayStableUnitKey | null => {
    const pads = bots.filter((bot) => bot.chassis.visible).map((bot) => bot.pad);
    const [nearest] = raycaster.intersectObjects(pads, false);
    return nearest
      ? (nearest.object.userData.unitKey as ReplayStableUnitKey)
      : null;
  };

  return {
    group,
    update,
    pick,
    dispose: () => {
      live = false;
      for (const item of disposables) item.dispose();
      // Chassis geometry and materials are deliberately not disposed here: they are owned
      // by the module-level parse cache and shared by every replay this page opens. Freeing
      // them would leave the next match holding disposed buffers.
    },
  };
}

/** Stable cosmetic phase without narrowing replay-v2's decimal-string identities. */
function phaseForIdentity(identity: string): number {
  let hash = 2_166_136_261;
  for (let index = 0; index < identity.length; index++) {
    hash ^= identity.charCodeAt(index);
    hash = Math.imul(hash, 16_777_619);
  }
  return ((hash >>> 0) / 0xffff_ffff) * Math.PI * 2;
}

/**
 * Catmull-Rom through four samples, evaluated between the middle two.
 *
 * Tangents are damped to 0.4 of the standard half-difference. At the full 0.5 a bot leaving
 * a corner at speed bulges far enough outside it to clip the wall it is driving around;
 * this keeps the cut small enough to read as carrying speed rather than as a bug.
 */
function catmullRom(p0: number, p1: number, p2: number, p3: number, t: number): number {
  const m1 = (p2 - p0) * 0.4;
  const m2 = (p3 - p1) * 0.4;
  const t2 = t * t;
  const t3 = t2 * t;
  return (
    (2 * t3 - 3 * t2 + 1) * p1 +
    (t3 - 2 * t2 + t) * m1 +
    (-2 * t3 + 3 * t2) * p2 +
    (t3 - t2) * m2
  );
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
