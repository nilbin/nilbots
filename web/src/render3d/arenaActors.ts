import * as THREE from 'three';
import { SCRAP_ACCENT } from '../presentation/scrapAccent';
import { roleTagCaption, roleTagColor } from '../presentation/roleTag';
import { parsePlayRoleTag } from '../presentation/playAwareness';

/** Rasterised role-caption size; generous, because it is read at gameplay scale. */
const ROLE_LABEL_WIDTH = 256;
const ROLE_LABEL_HEIGHT = 64;
const ROLE_LABEL_SCALE = 1.5;
import type {
  ReplayActorLifeKey,
  ReplayModel,
  ReplayStableUnitKey,
  ReplayWorldSnapshot,
} from '../replayModel';
import { isAttackEvent, isDestructionEvent } from '../replayModel';
import { teamAccentedBotImage } from '../render/arenaThemes';
import {
  defaultFormIdForUnit,
  stanceFormForUnit,
  stanceKindForForm,
  unitAccent,
  unitEmplacedLook,
  unitLook,
  unitProjectileLook,
  unitStanceLook,
} from '../render/unitPresentation';
import {
  arrivalsAt,
  boltsAt,
  directionAngle,
  headingAngle,
  posesAt,
  type BotPose,
} from '../render/interpolate';
import { volleyLanes } from '../render/volley';
import { buildVolleyArrows } from './volleyArrows';
import {
  maxHealthForActor,
  replayMaxHealth,
} from '../replayMetadata';
import { createPresenter } from '../replayPresentation';
import { arcVeterancyFor } from '../replayArcVeterancy';
import {
  isGenuineLookModel,
  lookModel,
  modelSpec,
} from './lookModel';
import {
  createArcModelMotionRig,
  type ArcSignatureBodyState,
} from './arcModelMotion';
import { CAMERA_PITCH } from './arenaScene';
import {
  teamVisionAt,
  teamVisionSeesActor,
  teamVisionSeesProjectile,
} from '../render/teamVision';

/**
 * The things that move.
 *
 * A **bot is a solid**. Authored GLBs give modeled class looks genuine hull, armor and
 * hardware depth; looks without one retain the sprite-derived extrusion fallback. Either
 * path casts a look-shaped shadow and reads as a machine occupying the ground
 * layer. A look may explicitly request a shallow hover cue; that lifts only its
 * rendered body, not its authoritative position or collision footprint.
 *
 * **A projectile is a rig**: a genuine model where one exists or the same extruded fallback,
 * painted in the owner's accent because the flat renderer paints it too, plus a tracer
 * stretched out behind and a pool of its own light on the floor below. It hovers, banks
 * through its turns and wobbles, so a bolt reads as a thing in the air rather than a mark
 * sliding across the ground.
 *
 * Everything else here is what the arena needs to *say*: which bot is being followed, how
 * much health each has left, and which of them can be seen from where.
 */

/**
 * The turret: the chassis' forward section, repeated around the axis it turns about.
 *
 * The offsets follow from tipping a model that lies in x ∈ ±½ and y ∈ [0, depth] onto its
 * nose — that leaves it half below the floor and off-centre by half its depth, so the lift
 * seats it and the shift puts its middle back on the axis.
 */
const TURRET_ARMS = 4;
const TURRET_LIFT = 0.5;
const TURRET_SHIFT = 0.16;
const TURRET_SCALE = 0.82;
const TURRET_SPIN = 0.55;

/**
 * The stance bodies: a third shape, and deliberately not a turret.
 *
 * A turret earns its radial symmetry from the rules — it sees and fires in every
 * direction. Both class stances keep a facing and both are *about* that facing: the
 * volley gun's three bolts leave along it, and the aegis shell consumes only what arrives
 * inside the quadrant it points at. Rearing a stance up on its nose and spinning it would
 * therefore be a lie about the one thing the viewer needs, so a stance stays flat on the
 * floor, keeps its nose, and grows hardware instead.
 *
 * The half-angle is a quarter turn either side, because that is the fan the volley
 * profile fires and the quadrant the guard covers. It is not a styling choice; changing
 * it would misstate the rule.
 */
const STANCE_HALF_ANGLE = Math.PI / 4;
/** Barrel length and thickness, in chassis widths. */
const BARREL_REACH = 1.15;
const BARREL_GAUGE = 0.1;
/** How tall the aegis plate stands, and where its face sits. */
const PLATE_HEIGHT = 0.34;
const PLATE_RADIUS = 0.72;

/**
 * When the turret takes over from the chassis, as a share of the deploy, and how long the
 * deploy runs for when the replay does not say.
 *
 * **Anchor is Wait-only while it runs**, so a bot that stood still and then swapped for a
 * turret between two frames threw away the one part of it there was anything to watch. And
 * the span cannot simply be read from the transition: a form change is free to complete in
 * the tick it started, which is exactly what Frontline's own fixture does — `started=9
 * done=9`, with `pendingFormTransition` never set in any snapshot. So the deploy is a fixed
 * span anchored on the tick the form actually changed, and a real multi-tick windup wins
 * where the replay describes one.
 */
const DEPLOY_TICKS = 1.5;

/**
 * A stance entry, on the other hand, is over in the tick it was asked for.
 *
 * **The volley is cast on the tick after the stance is entered**, and that single fact is
 * what the animation has to obey. Borrowing Anchor's 1.5-tick fallback ran the windup half
 * a tick past the shot: the fan was 60% open and the body still swelling at the instant
 * three bolts left it, then the pose snapped to full and immediately folded away — the
 * telegraphed move arrived after the thing it was telegraphing, and the pose the striker
 * fires from existed for about two frames. One tick lands the fan fully open exactly on the
 * tick boundary the bolts leave from, and makes the entry and the return meet there
 * continuously rather than jumping (the two segments used to hand over at 0.67 and 1.0 of
 * the same channel, which is a visible snap on the busiest frame of the move).
 */
const STANCE_TICKS = 1;

/**
 * The stance handover, and the charge either side of it.
 *
 * The mobile body used to be crushed to 58% and swapped, at that size, for a stance body
 * that appeared at 71% — a 22% size discontinuity in one frame, on top of a model swap, on
 * top of a squash that read as the machine being stepped on. Both halves now cross at the
 * *same* size, and it is a size slightly larger than rest: the striker gathers itself, and
 * the fan comes out of a charged body rather than out of a crushed one.
 */
const STANCE_HANDOVER = 0.5;
const STANCE_CHARGE_SCALE = 1.08;
/** How much the accent pool flares and spreads under a charging stance, at full charge. */
const STANCE_POOL_GAIN = 0.55;
const STANCE_POOL_SPREAD = 0.45;

/** How long the completed windup ring is held, in deploy-lengths, before it fades. */
const RING_HOLD = 0.25;
const TURRET_TAKEOVER = 0.6;

/**
 * The eight tiles around a bot. A wall in any of them cancels the drift outright.
 *
 * Damping the parts that reached towards a wall was tried twice and leaked both times: the
 * slide, then the slide and the yaw. The nose does not swing along either axis — a bot
 * turning through a corner throws it diagonally, into a tile neither check was looking at.
 * The blunt rule is the honest one: a bot with a wall beside it has nowhere to drift into,
 * which is also true of the thing being depicted.
 */
const NEIGHBOURS: readonly (readonly [number, number])[] = [
  [-1, -1], [0, -1], [1, -1],
  [-1, 0], [1, 0],
  [-1, 1], [0, 1], [1, 1],
];

/** How tall a bot's hull stands. Below the walls, so cover still reads as cover. */
const BOT_HEIGHT = 0.26;
/** The height bolts fly at. Exported because a bolt's dissipation has to happen there too. */
export const PROJECTILE_HOVER = 0.2;

/** Where health pips hang: above the floor, and back along Z to clear the bot on screen. */
const PIP_HEIGHT = 0.72;
const PIP_SETBACK = 0.55;
const PIP_SPACING = 0.17;

/**
 * The engaged mark: crossed blades above the health row, for a body that has a
 * LIVE FIGHT.
 *
 * A spectator watching a warren of eight machines cannot tell who is committed
 * and who is walking past — the arena shows shots and hulls, and neither says
 * "this one has a target". So combat state gets the same treatment health did:
 * always on, on the body, tiny, and the same colour the arena already uses for
 * violence (`#f87171`, the strike aim and slash colour), so it joins a language
 * rather than adding one.
 *
 * Two thin crossed bars rather than a dot, because a dot at this size is a
 * *state* with no meaning attached and this file already spends dots on health
 * and triangles on veterancy. An X reads at a glance and survives the fog
 * dimming, which a hue-only cue would not.
 *
 * It rides in the pips group: that group is already positioned in world space
 * each frame, already squared to the camera, and already hides with the body —
 * so the mark cannot outlive a death or leak through fog by construction.
 */
const ENGAGED_MARK_LIFT = 0.23;
const ENGAGED_MARK_REACH = 0.085;
const ENGAGED_MARK_GAUGE = 0.028;
const ENGAGED_MARK_COLOR = '#f87171';

/**
 * How far in FRONT of a body its role caption sits, in tiles.
 *
 * The mirror of `PIP_SETBACK`. The pitched camera puts +Z lower on screen, so a caption
 * anchored just past the body's front edge reads as hanging under it — which is where the
 * flat renderer draws the same word (`drawRoleTag`, one tile down from the body's top).
 * The sprite is bottom-anchored (`center.set(0.5, 0)`), so it grows up from here toward
 * the chassis rather than down into the next body's tile.
 */
const ROLE_LABEL_SETBACK = 0.62;

/**
 * A load riding on a body, and the colour it rides in.
 *
 * Carried scrap is the one piece of state that makes a body worth *chasing*
 * rather than worth shooting, so it has to be visible on the machine itself
 * from across the arena — a panel row is read afterwards, and by then the
 * courier is home. Shards orbiting above the hull do that at any camera angle,
 * and they are deliberately in scrap's neutral colour rather than the team's:
 * what is on the body is loot, and it changes hands the moment the body dies.
 *
 * Six is the declared carry cap on the shipped arm; the pool covers it and any
 * larger cap simply saturates, which is the right failure for a cue whose job
 * is "loaded" rather than "loaded with exactly this many".
 */
const CARRY_SHARD_LIMIT = 6;
const CARRY_ORBIT_RADIUS = 0.34;
const CARRY_HEIGHT = 0.52;

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

/**
 * The ring of ground that belongs to the selected body.
 *
 * Radii are in chassis widths and sit at the tile boundary — **outside the silhouette,
 * never across it**, which is the failure the first attempt at a ring had. The band is
 * a tenth of a chassis wide, so at the mid follow shot it is a hairline rather than a
 * halo, and it clears the channel (0.62–0.78) and screen (0.86–0.96) rings so a body
 * that is selected *and* holding the point still reads as both.
 *
 * **It is broken, and that is not decoration.** Every other ring on this floor is solid
 * and every one of them reports a rules state — channelling, screening, healing,
 * deploying. A dashed one cannot be mistaken for any of them at a glance, it is the same
 * cue the flat renderer has always drawn for selection (`setLineDash`, `drawArena`), and
 * at the same radius it carries about half the visual weight of a continuous stroke,
 * which is the whole of the difference between a marker and a halo.
 *
 * The backing is what makes it survive a theme. An additive ring vanishes on a
 * near-white floor and a flat one vanishes on a near-black one, so a dim near-black
 * edge is laid under a bright accent one: whichever way the map goes, one of the pair is
 * in contrast with it.
 *
 * The height is the load-bearing number. The fog mask is a plane at 0.03 that darkens
 * the floor, and every other floor cue here sits under it — correctly, because they are
 * things happening on ground the fog is entitled to hide. This one is the viewer's own
 * state rather than the match's, so it goes above: a body at the soft edge of its team's
 * vision keeps a ring at full strength instead of being dimmed along with the ground it
 * is standing on.
 */
const SELECTION_RING_INNER = 1;
const SELECTION_RING_OUTER = 1.09;
const SELECTION_RING_BACKING_SPREAD = 0.045;
const SELECTION_RING_HEIGHT = 0.034;
const SELECTION_RING_DASHES = 10;
/** Share of each dash's arc that is drawn, matching the flat renderer's 4-on-3-off. */
const SELECTION_RING_DUTY = 0.58;
const SELECTION_RING_OPACITY = 0.5;
const SELECTION_RING_BACKING_OPACITY = 0.38;
/** How far the ring's accent is pulled toward white, so it reads as choice, not team. */
const SELECTION_RING_WHITEN = 0.4;

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
/** A look-authored low hover is presentation, never a movement-layer change. */
const LOW_HOVER_HEIGHT = 0.075;
const LOW_HOVER_BOB = 0.014;

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

/**
 * Replace the loading stand-ins with the resolved chassis representation.
 *
 * The small facing wedge is renderer-owned legacy hardware. SVG-derived solids need it
 * because a raised camera can otherwise lose their facing, while an authored GLB already
 * carries its own prow and directional silhouette. Unknown model sources keep the wedge:
 * that is the conservative fallback and preserves the pre-GLB rendering floor.
 */
export function installMobileModel(
  body: THREE.Group,
  model: THREE.Group,
  placeholders: {
    hull: THREE.Object3D;
    lid: THREE.Object3D;
    facingMarker: THREE.Object3D;
  },
): void {
  body.add(model);
  body.remove(placeholders.hull);
  body.remove(placeholders.lid);
  if (isGenuineLookModel(model))
    body.remove(placeholders.facingMarker);
}

export function buildActors(replay: ReplayModel): ArenaActors {
  const group = new THREE.Group();
  const disposables: { dispose: () => void }[] = [];
  const presenter = createPresenter(replay);
  const veterancy = arcVeterancyFor(replay);
  // Rules-owned and form-extensible: this is only the allocation ceiling. Per-frame
  // visibility still uses the effective form's own maximum.
  const maxHealth = replayMaxHealth(replay);
  // A replay can be closed while a chassis is still being fetched and triangulated. Adding
  // the model to a torn-down scene would resurrect meshes nothing will ever dispose.
  let live = true;


  const bots = replay.units.map((unit) => {
    // A unit's chassis is built once, so it is resolved from the form the unit starts the
    // match in rather than per tick. That is exact rather than approximate for the class
    // arms this exists for: a form transition moves a life between the mobile and emplaced
    // members of **one** family, so the family — and therefore the artwork — is fixed for
    // the unit even though the effective form is not.
    const defaultFormId = defaultFormIdForUnit(replay, unit.unitKey);
    const look = unitLook(replay, unit.unitKey, defaultFormId);
    const emplacedLook = unitEmplacedLook(replay, unit.unitKey, defaultFormId);
    const stanceForm = stanceFormForUnit(replay, unit.unitKey, defaultFormId);
    const stanceLook = unitStanceLook(replay, unit.unitKey, defaultFormId);
    const stancePresentation = stanceLook ?? look;
    const stanceHardware = modelSpec(stancePresentation.id)?.skillHardware;
    const lookSpec = modelSpec(look.id);
    const mobileLowHover = look.locomotionCue === 'low-hover';
    const stanceLowHover =
      stancePresentation.locomotionCue === 'low-hover';
    const accentValue = unitAccent(replay, unit.unitKey, defaultFormId);
    const accent = new THREE.Color(accentValue);
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
    const modelMotion = createArcModelMotionRig(
      lookSpec,
      size,
      accent,
      disposables,
    );
    if (modelMotion) {
      modelMotion.wake.visible = false;
      modelMotion.vents.visible = false;
      chassis.add(modelMotion.wake);
      body.add(modelMotion.vents);
    }

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
      map: spriteTexture(teamAccentedBotImage(look, accentValue)),
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

    // The nose: which way this machine is pointing, said outright.
    //
    // Facing and movement are decoupled by the generic contract — a bot can step north
    // while still facing east — and a chassis alone does not carry that at arena scale from
    // a raised camera, so reviewers read the result as the strafing that was removed. A
    // small lit wedge on the leading edge fixes it, and it is the one accent-coloured piece
    // *on* a bot here: DECISIONS #127 removed the accent **wash**, which flattened twelve
    // chassis into one glowing lozenge. A marker the size of a headlight is the opposite of
    // that — it adds a bit of information rather than removing all of it — and it doubles as
    // the team's colour on the body, which the pool of light underneath cannot do when the
    // bot is standing on a lit floor.
    //
    // It belongs to `body`, so an emplaced form loses it along with the rest of the mobile
    // chassis. A turret has no facing to show.
    const noseGeometry = new THREE.ConeGeometry(size * 0.13, size * 0.26, 4);
    // Cones point +Y; the sprites are drawn facing east, which is +X here.
    noseGeometry.rotateZ(-Math.PI / 2);
    const noseMaterial = new THREE.MeshStandardMaterial({
      color: accent,
      emissive: accent,
      emissiveIntensity: 1.5,
      roughness: 0.3,
      metalness: 0.2,
    });
    const nose = new THREE.Mesh(noseGeometry, noseMaterial);
    nose.userData.cue = 'facing-marker';
    nose.position.set(size * 0.46, BOT_HEIGHT + 0.02, 0);
    nose.castShadow = true;
    body.add(nose);

    // A turret is deliberately a different silhouette, not the mobile chassis with its
    // movement disabled: it has no privileged facing, which is what a form that sees and
    // fires in every direction actually looks like.
    //
    // **Derived from the sprite rather than built from primitives.** A cylinder with vanes
    // says "turret" but says nothing about *which* bot it is, and everything else in this
    // renderer is extruded from the look's own drawing (#127) precisely so a new chassis
    // needs no new model. So a turret is the chassis' forward section repeated around its
    // own axis — radially symmetric by construction, and still recognisably the machine it
    // was. Every look in the library gets one, and so does anything added later.
    const turret = new THREE.Group();
    turret.visible = false;
    turret.userData.renderForm = 'stationary-omnidirectional';
    const spokes: THREE.Group[] = [];

    // Built from the family's **emplaced** look when the fallback supplies one, and from
    // the unit's own chassis otherwise. A class form that has no artwork of its own gets
    // an emplacement that is unmistakably an emplacement; a legacy `turret` form keeps the
    // silhouette it has always had.
    const turretLook = emplacedLook ?? look;
    const genuineTurretArm =
      modelSpec(turretLook.id)?.part === 'turret-arm';
    void lookModel(
      turretLook,
      undefined,
      'front',
      accent,
    ).then((model) => {
      if (!live || !model) return;
      for (let arm = 0; arm < TURRET_ARMS; arm++) {
        const spoke = new THREE.Group();
        // The loader gave this actor an independent material set. Its four arms may share
        // those materials because they fade, flash and highlight as one machine, while
        // their scene nodes remain independent for the deployment fan.
        const section = model.clone(true);
        section.scale.setScalar(size);
        if (!genuineTurretArm) {
          // SVG fallback sections lie flat as chassis lids. Tip the cropped nose upright,
          // seat it on the floor, and pull it back onto the axis it turns about. A genuine
          // turret-arm GLB is authored in its deployed orientation already.
          section.rotation.z = Math.PI / 2;
          section.position.set(TURRET_SHIFT * size, TURRET_LIFT * size, 0);
        }
        spoke.add(section);
        turret.add(spoke);
        spokes.push(spoke);
      }
      registerModelMaterials(turret);
    });
    chassis.add(turret);

    // The stance body. Built only for a unit whose ruleset actually has one, so a duel or
    // a skill-free class arm allocates nothing and behaves exactly as it did.
    const stance = new THREE.Group();
    stance.visible = false;
    stance.userData.renderForm = 'stance-directional';
    stance.userData.stanceKind = stanceForm?.kind ?? null;
    /** Hinges the deploy swings open, and the plate it raises. */
    const barrels: THREE.Group[] = [];
    let plate: THREE.Object3D | null = null;
    let guardArc: THREE.Mesh | null = null;
    chassis.add(stance);

    // Anchor windup is state, not a guessed animation: the ring appears only while the
    // normalized actor carries an authoritative pending transition.
    // The windup cue: a ring that fills as the deploy completes.
    //
    // A dial rather than a plain torus, because the thing it reports is *how far through*
    // an Anchor is, and Anchor is Wait-only while it runs — "busy, and for this much
    // longer" is the most useful sentence the arena can say about that bot.
    const anchorGeometry = new THREE.RingGeometry(size * 0.5, size * 0.66, 64);
    anchorGeometry.rotateX(-Math.PI / 2);
    const anchorDial = progressRing();
    const anchorMaterial = new THREE.MeshBasicMaterial({
      color: accent,
      map: anchorDial?.texture ?? null,
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const anchorRing = new THREE.Mesh(anchorGeometry, anchorMaterial);
    anchorRing.userData.cue = 'form-transition-pending';
    anchorRing.position.y = 0.045;
    anchorRing.visible = false;
    chassis.add(anchorRing);
    if (anchorDial) disposables.push(anchorDial.texture);

    // Redrawn only when the arc would visibly move: this is a canvas upload, and a deploy
    // runs for whole ticks.
    let dialAt = -1;
    const paintAnchor = (progress: number) => {
      if (!anchorDial || Math.abs(progress - dialAt) < 1 / 48) return;
      dialAt = progress;
      anchorDial.paint(progress);
    };

    // And the turret's scan: a wedge that turns forever, because an emplacement watching
    // every direction at once has no facing to show and needs to say so somehow.
    const scanGeometry = new THREE.RingGeometry(size * 0.62, size * 1.08, 48, 1, 0, Math.PI * 0.55);
    scanGeometry.rotateX(-Math.PI / 2);
    const scanMaterial = new THREE.MeshBasicMaterial({
      color: accent,
      transparent: true,
      opacity: 0.34,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const scan = new THREE.Mesh(scanGeometry, scanMaterial);
    scan.userData.cue = 'stationary-scan';
    scan.position.y = 0.022;
    scan.visible = false;
    chassis.add(scan);
    disposables.push(scanGeometry, scanMaterial);

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
    glow.userData.cue = 'accent-pool';
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
    const fading: {
      material: THREE.Material;
      base: number;
      // Some materials are only correct in the transparent pass. Pips draw with depthTest
      // off so they read over walls, and an opaque-pass material that writes no depth is
      // simply painted over by whatever opaque thing is drawn after it — which is why they
      // cleared some walls and not others, depending on which happened to be nearer.
      alwaysTransparent?: boolean;
    }[] = [
      { material: hullMaterial, base: 1 },
      { material: lidMaterial, base: 1 },
      { material: noseMaterial, base: 1 },
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
    tintable(noseMaterial);

    /**
     * Enrol the loader-owned material set in this actor's presentation state.
     *
     * `lookModel` clones scene nodes and materials per call while keeping geometry shared.
     * That is already the ownership boundary fog/highlight need, so cloning again here
     * would only multiply GPU materials. Turret arms deliberately share one actor-local
     * set; the Set keeps that repeated scene from registering or disposing it four times.
     */
    const registerModelMaterials = (solid: THREE.Object3D) => {
      const seen = new Set<THREE.Material>();
      solid.traverse((node) => {
        const mesh = node as THREE.Mesh;
        if (!mesh.isMesh) return;
        const materials = Array.isArray(mesh.material)
          ? mesh.material
          : [mesh.material];
        for (const material of materials) {
          if (seen.has(material)) continue;
          seen.add(material);
          fading.push({ material, base: 1 });
          if (material instanceof THREE.MeshStandardMaterial)
            tintable(material);
          disposables.push(material);
        }
      });
      // A model that lands mid-highlight or mid-flash has to arrive wearing it, not plain.
      repaint();
    };

    // The stance hardware. Built here rather than beside the empty group above because it
    // enrols in `fading` and `tinting`, and both of those are declared with the rest of
    // the bot's paint — a stance still has to ghost under fog and light up when followed.
    if (stanceForm !== null) {
      // The stance's own model (or SVG-derived fallback). A form change swaps the machine,
      // not its paint.
      void lookModel(
        stancePresentation,
        undefined,
        undefined,
        accent,
      ).then((model) => {
        if (!live || !model) return;
        model.scale.setScalar(size * 0.94);
        registerModelMaterials(model);
        stance.add(model);
      });
    }

    if (stanceForm?.kind === 'volley' && stanceHardware !== 'volley') {
      // Three barrels on three hinges, at the exact headings the profile fires. Local −Y
      // rotation is the screen-space fan angle: the chassis is turned by `-pose.angle`,
      // so world (x, z) and screen (x, y) are the same plane in the same order.
      const barrelGeometry = new THREE.BoxGeometry(
        size * BARREL_REACH,
        size * BARREL_GAUGE,
        size * BARREL_GAUGE * 1.2,
      );
      const barrelMaterial = new THREE.MeshStandardMaterial({
        color: accent.clone().multiplyScalar(0.45),
        emissive: accent,
        emissiveIntensity: 1.1,
        roughness: 0.35,
        metalness: 0.6,
      });
      disposables.push(barrelGeometry, barrelMaterial);
      fading.push({ material: barrelMaterial, base: 1 });
      tintable(barrelMaterial);
      for (const fan of [-STANCE_HALF_ANGLE, 0, STANCE_HALF_ANGLE]) {
        const hinge = new THREE.Group();
        hinge.userData.fanAngle = fan;
        const barrel = new THREE.Mesh(barrelGeometry, barrelMaterial);
        barrel.position.set(size * BARREL_REACH * 0.5, BOT_HEIGHT * 0.9, 0);
        barrel.castShadow = true;
        hinge.add(barrel);
        stance.add(hinge);
        barrels.push(hinge);
      }
    }

    if (stanceForm?.kind === 'aegis') {
      // A standing curved plate across the guarded quadrant. Open-ended, so it is a wall
      // rather than a tube, and it stops hard at ±45° — the edge is the counter-play.
      if (stanceHardware !== 'aegis') {
        const plateGeometry = new THREE.CylinderGeometry(
          size * PLATE_RADIUS,
          size * PLATE_RADIUS,
          size * PLATE_HEIGHT,
          20,
          1,
          true,
          // Cylinder theta runs from +Z; the guarded quadrant is centred on +X.
          Math.PI / 2 - STANCE_HALF_ANGLE,
          STANCE_HALF_ANGLE * 2,
        );
        const plateMaterial = new THREE.MeshStandardMaterial({
          color: accent.clone().multiplyScalar(0.5),
          emissive: accent,
          emissiveIntensity: 0.9,
          roughness: 0.3,
          metalness: 0.65,
          side: THREE.DoubleSide,
        });
        disposables.push(plateGeometry, plateMaterial);
        fading.push({ material: plateMaterial, base: 1 });
        tintable(plateMaterial);
        const plateMesh = new THREE.Mesh(plateGeometry, plateMaterial);
        plateMesh.userData.cue = 'aegis-plate';
        plateMesh.position.y = (size * PLATE_HEIGHT) / 2;
        plateMesh.castShadow = true;
        stance.add(plateMesh);
        plate = plateMesh;
      }

      // And the same quadrant on the floor, because from a raised camera an upright plate
      // foreshortens and the *extent* of the guard is what a flanker is judging.
      const arcGeometry = new THREE.RingGeometry(
        size * 0.5,
        size * (PLATE_RADIUS + 0.16),
        28,
        1,
        -STANCE_HALF_ANGLE,
        STANCE_HALF_ANGLE * 2,
      );
      arcGeometry.rotateX(-Math.PI / 2);
      const arcMaterial = new THREE.MeshBasicMaterial({
        color: accent,
        transparent: true,
        opacity: 0.42,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      const arcMesh = new THREE.Mesh(arcGeometry, arcMaterial);
      arcMesh.userData.cue = 'aegis-guard-arc';
      arcMesh.position.y = 0.03;
      stance.add(arcMesh);
      disposables.push(arcGeometry, arcMaterial);
      // Deliberately NOT in `fading`: the guard arc's opacity carries the unfold ramp,
      // and `fade` assigns rather than multiplies, so enrolling it would overwrite the
      // ramp every frame with a flat base. It composes with fog after the fade instead,
      // exactly like the anchor dial and the turret scan do.
      guardArc = arcMesh;
    }

    // Swap the box for the genuine model or SVG-derived fallback once it is ready.
    //
    // The box is a placeholder, not the design. It is here because the model arrives over a
    // fetch and a first frame with nothing on the floor is worse than a first frame with a
    // block on it — but a block is what a chassis looks like when this step is missing,
    // which is exactly how it shipped once.
    void lookModel(
      look,
      undefined,
      undefined,
      accent,
    ).then((model) => {
      if (!live || !model) return;
      model.scale.setScalar(size);
      registerModelMaterials(model);
      installMobileModel(body, model, {
        hull,
        lid,
        facingMarker: nose,
      });
      modelMotion?.bind(model);
    });

    // The ring of ground under the selected body — a pair of flat discs, dark under
    // bright, laid on the floor at the tile boundary.
    //
    // A ring was tried in the first pass at selection and dropped; the note below still
    // records why, and it is worth keeping, because this one only works by not repeating
    // either half of it. It is on the FLOOR at the tile boundary rather than hugging the
    // machine, so it cannot read as drawn across a chassis the depth buffer has correctly
    // behind it. And it is not carrying selection on its own — the pool still brightens
    // and the trim still lifts — so it has no reason to be loud enough to become the halo
    // that got the first one removed. It exists because the camera now flies down to the
    // body you picked (owner request 2026-08), and at that shot "which of these is mine"
    // is a question the arena should answer on the ground rather than only in the paint.
    const selectionRing = new THREE.Group();
    selectionRing.userData.cue = 'selection-ring';
    selectionRing.userData.forUnitKey = unit.unitKey;
    selectionRing.position.y = SELECTION_RING_HEIGHT;
    selectionRing.visible = false;
    const selectionBackingGeometry = dashedRingGeometry(
      size * (SELECTION_RING_INNER - SELECTION_RING_BACKING_SPREAD),
      size * (SELECTION_RING_OUTER + SELECTION_RING_BACKING_SPREAD),
    );
    const selectionBackingMaterial = new THREE.MeshBasicMaterial({
      color: new THREE.Color('#05090d'),
      transparent: true,
      opacity: SELECTION_RING_BACKING_OPACITY,
      depthWrite: false,
      side: THREE.DoubleSide,
    });
    const selectionBacking = new THREE.Mesh(
      selectionBackingGeometry,
      selectionBackingMaterial,
    );
    // Ordered past the fog plane, which is a map-sized quad whose distance to the camera
    // says nothing about what it covers — leaving the transparent pass to sort these by
    // depth would put the ring under the fog on some frames and over it on others.
    selectionBacking.renderOrder = 3;
    selectionRing.add(selectionBacking);
    const selectionGeometry = dashedRingGeometry(
      size * SELECTION_RING_INNER,
      size * SELECTION_RING_OUTER,
    );
    const selectionMaterial = new THREE.MeshBasicMaterial({
      color: accent.clone().lerp(WHITE, SELECTION_RING_WHITEN),
      transparent: true,
      opacity: SELECTION_RING_OPACITY,
      depthWrite: false,
      side: THREE.DoubleSide,
    });
    const selectionEdge = new THREE.Mesh(selectionGeometry, selectionMaterial);
    selectionEdge.renderOrder = 4;
    selectionRing.add(selectionEdge);
    chassis.add(selectionRing);
    disposables.push(
      selectionBackingGeometry,
      selectionBackingMaterial,
      selectionGeometry,
      selectionMaterial,
    );
    // Enrolled in the fade like every other cue, so a selected body that is collapsing or
    // still coming up out of the floor takes its ring with it.
    fading.push(
      {
        material: selectionBackingMaterial,
        base: SELECTION_RING_BACKING_OPACITY,
        alwaysTransparent: true,
      },
      {
        material: selectionMaterial,
        base: SELECTION_RING_OPACITY,
        alwaysTransparent: true,
      },
    );

    // Following a bot lights *the bot* as well as the ground under it.
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
      selectionRing.visible = on;
      paintPool();
      repaint();
    };

    /**
     * The charge under a bot entering a stance: the accent pool flares and spreads.
     *
     * The pool is the one place a bot wears its owner's colour in this renderer, which
     * makes it the honest place to say "this machine is about to do something" — the same
     * channel the followed bot already brightens, pushed further for a moment. A stance
     * entry is a fifth of a second, so the cue has to be legible without being a second
     * light source; brightening what is already there is exactly that.
     *
     * Selection and charge compose rather than take turns: a followed striker that stopped
     * being lit while it wound up would report the wrong thing twice.
     */
    let charged = 0;
    const charge = (amount: number) => {
      const clamped = Math.max(0, Math.min(amount, 1));
      if (clamped === charged) return;
      charged = clamped;
      paintPool();
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
    pips.userData.cue = 'health-pips';
    pips.userData.forUnitKey = unit.unitKey;
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
      pip.position.x =
        (index - (maxHealth - 1) / 2) * PIP_SPACING;
      // Square to the camera. It never rolls or orbits, so its pitch is a constant and one
      // rotation is enough — a billboard would be a per-frame lookAt for a fixed picture.
      pip.rotation.x = -CAMERA_PITCH;
      pip.renderOrder = 10;
      pips.add(pip);
      pipMeshes.push(pip);
    }
    group.add(pips);
    disposables.push(pipGeometry, litPip, lostPip);

    // Veterancy, as brass chevrons in a second row under the health pips —
    // level 1 shows nothing, each earned level adds one. Brass on purpose:
    // the economy's purchase beat already taught that colour to mean "this
    // machine got stronger", and level is exactly that. They live in the
    // pips group so they follow the bot (and hide with it) for free.
    const chevronGeometry = new THREE.CircleGeometry(0.055, 3);
    const chevronMaterial = new THREE.MeshBasicMaterial({
      color: new THREE.Color('#d9a441'),
      transparent: true,
      depthWrite: false,
      depthTest: false,
    });
    const levelPips: THREE.Mesh[] = [];
    for (let index = 0; index < Math.max(0, veterancy.maxLevel - 1); index++) {
      const chevron = new THREE.Mesh(chevronGeometry, chevronMaterial);
      chevron.rotation.x = -CAMERA_PITCH;
      // A triangle from CircleGeometry(…, 3) points along +X; roll it a
      // quarter turn in its own plane so it reads as an upward chevron.
      chevron.rotation.z = Math.PI / 2;
      chevron.position.y = -0.16;
      chevron.renderOrder = 10;
      chevron.visible = false;
      pips.add(chevron);
      levelPips.push(chevron);
    }
    disposables.push(chevronGeometry, chevronMaterial);

    // Crossed blades over the health row — see ENGAGED_MARK_*. Two quads in one
    // group so the whole mark toggles with a single `visible`, and squared to
    // the camera like everything else in this row.
    const engagedMark = new THREE.Group();
    engagedMark.userData.cue = 'engaged-mark';
    engagedMark.userData.forUnitKey = unit.unitKey;
    engagedMark.position.y = ENGAGED_MARK_LIFT;
    engagedMark.rotation.x = -CAMERA_PITCH;
    engagedMark.visible = false;
    const bladeGeometry = new THREE.PlaneGeometry(
      ENGAGED_MARK_REACH * 2,
      ENGAGED_MARK_GAUGE,
    );
    const bladeMaterial = new THREE.MeshBasicMaterial({
      color: new THREE.Color(ENGAGED_MARK_COLOR),
      transparent: true,
      depthWrite: false,
      depthTest: false,
    });
    for (const roll of [Math.PI / 4, -Math.PI / 4]) {
      const blade = new THREE.Mesh(bladeGeometry, bladeMaterial);
      blade.rotation.z = roll;
      blade.renderOrder = 10;
      engagedMark.add(blade);
    }
    pips.add(engagedMark);
    disposables.push(bladeGeometry, bladeMaterial);

    // The channel ring: this body is holding the point still, or standing off
    // it while a teammate does. Radially symmetric on purpose — it is parented
    // to the chassis, which turns with the bot's facing, and a body may aim and
    // fire without breaking its channel, so an arc would swing every time it
    // looked somewhere else.
    const channelGeometry = new THREE.RingGeometry(
      size * 0.62,
      size * 0.78,
      36,
    );
    channelGeometry.rotateX(-Math.PI / 2);
    const channelMaterial = new THREE.MeshBasicMaterial({
      color: accent,
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const channelRing = new THREE.Mesh(channelGeometry, channelMaterial);
    channelRing.userData.cue = 'channel-ring';
    channelRing.position.y = 0.026;
    channelRing.visible = false;
    chassis.add(channelRing);

    // The screen's marker is the same ring, broken: six segments at a wider
    // radius, dim and still. One glance separates the body making progress
    // from the bodies keeping it alive.
    const screenGeometry = new THREE.RingGeometry(
      size * 0.86,
      size * 0.96,
      6,
    );
    screenGeometry.rotateX(-Math.PI / 2);
    const screenMaterial = new THREE.MeshBasicMaterial({
      color: accent,
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const screenRing = new THREE.Mesh(screenGeometry, screenMaterial);
    screenRing.userData.cue = 'screen-ring';
    screenRing.position.y = 0.024;
    screenRing.visible = false;
    chassis.add(screenRing);

    // The heal channel: standing on a heal zone, recovering. Green because
    // nothing else in the arena is, and a ring rather than a flash because a
    // channel is a state — it holds while the bot holds still, which is
    // exactly the read a spectator needs (a channelling bot is stationary,
    // rear-blind, and next to contested ground).
    const healGeometry = new THREE.RingGeometry(size * 0.7, size * 0.88, 36);
    healGeometry.rotateX(-Math.PI / 2);
    const healMaterial = new THREE.MeshBasicMaterial({
      color: new THREE.Color('#4ade80'),
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const healRing = new THREE.Mesh(healGeometry, healMaterial);
    healRing.userData.cue = 'heal-ring';
    healRing.position.y = 0.028;
    healRing.visible = false;
    chassis.add(healRing);
    disposables.push(healGeometry, healMaterial);

    // The purchase beat, on the machines it was spent on.
    //
    // A tier is bought out of the bank and applied to the team's lives, so the
    // honest place to say it is the bodies — not a toast, and not the home pad,
    // which a generic contract need not even declare. A brass ring thrown
    // outward from under every body of the buying team, once, for the length
    // of the beat: unmistakable, gone in a second, and impossible to confuse
    // with an impact, which is white and comes from a tile rather than from a
    // machine.
    const upgradeGeometry = new THREE.RingGeometry(
      size * 0.55,
      size * 0.72,
      32,
    );
    upgradeGeometry.rotateX(-Math.PI / 2);
    const upgradeMaterial = new THREE.MeshBasicMaterial({
      color: new THREE.Color(SCRAP_ACCENT),
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      side: THREE.DoubleSide,
    });
    const upgradeRing = new THREE.Mesh(upgradeGeometry, upgradeMaterial);
    upgradeRing.userData.cue = 'scrap-purchase';
    upgradeRing.position.y = 0.032;
    upgradeRing.visible = false;
    chassis.add(upgradeRing);

    // The load. World-space like the pips, for the same reason: parented to
    // the chassis these would swing round with the facing.
    const carry = new THREE.Group();
    carry.userData.cue = 'carried-scrap';
    carry.userData.forUnitKey = unit.unitKey;
    carry.visible = false;
    const shardGeometry = new THREE.OctahedronGeometry(0.062, 0);
    const shardMaterial = new THREE.MeshStandardMaterial({
      color: new THREE.Color(SCRAP_ACCENT).multiplyScalar(0.5),
      emissive: new THREE.Color(SCRAP_ACCENT),
      emissiveIntensity: 1.15,
      roughness: 0.3,
      metalness: 0.8,
    });
    const shards: THREE.Mesh[] = [];
    for (let index = 0; index < CARRY_SHARD_LIMIT; index++) {
      const shard = new THREE.Mesh(shardGeometry, shardMaterial);
      shard.visible = false;
      carry.add(shard);
      shards.push(shard);
    }
    // A wash under a loaded body, so a courier reads as valuable even when the
    // shards themselves are behind a wall from this camera.
    const haulGeometry = new THREE.PlaneGeometry(size * 2.1, size * 2.1);
    haulGeometry.rotateX(-Math.PI / 2);
    const haulMaterial = new THREE.MeshBasicMaterial({
      map: radialGlow(new THREE.Color(SCRAP_ACCENT)),
      transparent: true,
      opacity: 0,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    });
    const haul = new THREE.Mesh(haulGeometry, haulMaterial);
    haul.userData.cue = 'carried-scrap-pool';
    haul.position.y = 0.02;
    haul.visible = false;
    // THE ROLE CAPTION (§12.3), the 3D half of the flat renderer's. A sprite
    // rather than geometry, because a label has to stay readable and upright
    // from any camera angle; the glyphs, the dark hairline behind them and the
    // per-word colour all come from the same shared presentation module the
    // Canvas2D renderer uses, so the two renderers cannot drift into saying
    // the same thing two ways.
    // Guarded like every other rasterised texture here: the presentation tests
    // build actors without a DOM, and a renderer that only works in a browser
    // would make them unrunnable.
    const roleCanvas =
      typeof document === 'undefined'
        ? null
        : document.createElement('canvas');
    if (roleCanvas) {
      roleCanvas.width = ROLE_LABEL_WIDTH;
      roleCanvas.height = ROLE_LABEL_HEIGHT;
    }
    const roleTexture = roleCanvas
      ? new THREE.CanvasTexture(roleCanvas)
      : null;
    if (roleTexture) roleTexture.colorSpace = THREE.SRGBColorSpace;
    const roleMaterial = new THREE.SpriteMaterial({
      map: roleTexture,
      transparent: true,
      depthWrite: false,
      depthTest: false,
    });
    const roleLabel = new THREE.Sprite(roleMaterial);
    roleLabel.userData.cue = 'role-tag';
    roleLabel.center.set(0.5, 0);
    roleLabel.scale.set(ROLE_LABEL_SCALE, ROLE_LABEL_SCALE * 0.25, 1);
    roleLabel.position.y = 0.02;
    roleLabel.visible = false;
    group.add(roleLabel);
    if (roleTexture) disposables.push(roleTexture);
    disposables.push(roleMaterial);
    let paintedRole: string | null = null;
    /** Repaints only when the word changes: a tag is sticky for many ticks. */
    const paintRole = (tag: string | null) => {
      if (tag === paintedRole) return;
      paintedRole = tag;
      roleLabel.visible = tag !== null;
      if (tag === null || !roleCanvas || !roleTexture) return;
      const context = roleCanvas.getContext('2d');
      if (!context) return;
      context.clearRect(0, 0, roleCanvas.width, roleCanvas.height);
      context.textAlign = 'center';
      context.textBaseline = 'middle';
      context.lineJoin = 'round';
      context.strokeStyle = 'rgba(2, 6, 12, 0.85)';
      const caption = roleTagCaption(tag);
      // The caption is fitted to the texture rather than trusted to fit it. The tag
      // vocabulary is the author's, `roleTagCaption` allows fourteen characters, and at
      // the base size only about ten fit across 256px — so `ghost-patrol` rendered as
      // `host-patro`, clipped at both ends by the texture edge, which reads as a broken
      // label rather than a long one. Short tags keep the size they always had.
      const font = (size: number) =>
        `600 ${size}px ui-monospace, SFMono-Regular, Menlo, monospace`;
      let size = Math.round(ROLE_LABEL_HEIGHT * 0.62);
      context.font = font(size);
      // Room for the dark stroke, which is drawn centred on the glyph outline and so
      // spills half its width past each end of the text.
      const available = roleCanvas.width - ROLE_LABEL_HEIGHT * 0.34;
      const measured = context.measureText(caption).width;
      if (measured > available) {
        size = Math.max(16, Math.floor((size * available) / measured));
        context.font = font(size);
      }
      context.lineWidth = size * 0.28;
      context.strokeText(caption, roleCanvas.width / 2, roleCanvas.height / 2);
      context.fillStyle = roleTagColor(tag);
      context.fillText(caption, roleCanvas.width / 2, roleCanvas.height / 2);
      roleTexture.needsUpdate = true;
    };

    group.add(haul);
    group.add(carry);
    disposables.push(
      channelGeometry,
      channelMaterial,
      screenGeometry,
      screenMaterial,
      upgradeGeometry,
      upgradeMaterial,
      shardGeometry,
      shardMaterial,
      haulGeometry,
      haulMaterial,
    );

    chassis.visible = false;
    group.add(chassis);
    disposables.push(
      hullGeometry,
      hullMaterial,
      lidGeometry,
      lidMaterial,
      noseGeometry,
      noseMaterial,
      anchorGeometry,
      anchorMaterial,
      glowGeometry,
      glowMaterial,
    );

    fading.push(
      { material: litPip, base: 1, alwaysTransparent: true },
      { material: lostPip, base: 0.35, alwaysTransparent: true },
    );
    /**
     * How each material was authored, before anything faded it.
     *
     * Snapshotted lazily rather than at enrolment because models resolve late and register
     * their own materials; `fade` is the only thing that writes `transparent`, so the first
     * time it sees a material the value is still the author's.
     */
    const authoredTransparent = new Map<THREE.Material, boolean>();
    const fade = (factor: number) => {
      lastFactor = factor;
      for (const { material, base, alwaysTransparent } of fading) {
        if (!authoredTransparent.has(material))
          authoredTransparent.set(material, material.transparent);
        material.opacity = base * factor;
        // **Or'd with how it was authored, never assigned over it.** Assigning was a bug
        // with one visible victim: the accent pool is an additive, depth-write-free
        // `PlaneGeometry` two and a half tiles square, and following a bot raises its base
        // to exactly 1 — so at full strength this computed `false` and moved a radial glow
        // into the opaque pass, where alpha is not read. The soft circle of light under the
        // machine became a hard, flat rectangle around it, on the selected bot only. That is
        // the "box around a model": a material told to stop being transparent while it was
        // still drawing something transparent.
        material.transparent =
          authoredTransparent.get(material)! ||
          alwaysTransparent ||
          factor < 1 ||
          base < 1;
      }
    };

    /**
     * The accent pool's brightness and spread, from every input that owns a piece of it.
     *
     * Written once rather than by each caller, because there are two of them now and they
     * overlap: `highlight` used to assign `glowFade.base` outright, so a charge would have
     * been erased the moment a bot was followed and vice versa.
     */
    const paintPool = () => {
      glowFade.base =
        (highlighted ? SELECTED_POOL : UNSELECTED_POOL) *
        (1 + charged * STANCE_POOL_GAIN);
      glow.scale.setScalar(1 + charged * STANCE_POOL_SPREAD);
      fade(lastFactor);
    };

    return {
      unitKey: unit.unitKey,
      size,
      motionPhase: phaseForIdentity(unit.unitKey),
      modelMotion,
      mobileLowHover,
      stanceLowHover,
      chassis,
      body,
      turret,
      spokes,
      stance,
      stanceKind: stanceForm?.kind ?? null,
      barrels,
      plate,
      guardArc,
      anchorRing,
      paintAnchor,
      scan,
      scanMaterial,
      anchorMaterial,
      pad,
      highlight,
      charge,
      flash,
      pips,
      pipMeshes,
      engagedMark,
      litPip,
      lostPip,
      levelPips,
      channelRing,
      channelMaterial,
      screenRing,
      screenMaterial,
      healRing,
      healMaterial,
      upgradeRing,
      upgradeMaterial,
      carry,
      shards,
      shardMaterial,
      haul,
      haulMaterial,
      roleLabel,
      paintRole,
      fading,
      fade,
    };
  });
  const signedTravel = signedTravelByActor(replay);
  const signatureCooldowns = signatureCooldownsByActor(replay);
  /** Is this tile solid? Out of bounds counts as solid — the arena is enclosed. */
  const solid = (x: number, y: number) => {
    const row = replay.map.tileRows[y];
    return row === undefined || row[x] === undefined || row[x] === '#';
  };

  /**
   * Every form change a life goes through, in order, with the direction it goes in.
   *
   * This used to be a single first-only "when did this life deploy" entry per actor, and
   * that shape could not express a life going back: once an entry existed, the deploy
   * clock was clamped at 1 for the rest of the match, so a bulwark that mobilized out of
   * its turret kept the turret geometry and the scan wedge forever while its authoritative
   * form, its health maximum and its move events all said otherwise. Same-life transitions
   * are reversible by rule (`mobilize` is `transform` run backwards) and a life may make
   * the round trip more than once, so the renderer keeps the whole sequence and reads the
   * segment covering the playhead.
   *
   * `stationary` is the *target's* mobility, so a segment says which way the animation is
   * running rather than what the form is right now — during a windup the life is still
   * legally in its source form, which is exactly the part worth watching.
   *
   * A cancelled transition (lethal damage during a windup) is recorded as a segment back
   * toward the form the life kept, so a half-raised chassis settles instead of finishing a
   * deploy that never happened.
   *
   * A segment also carries which *shape* each end of it is, because "stationary" stopped
   * being enough the moment a class could be stationary in two different ways. Entering a
   * stance and anchoring are both `canMove === false` and they are not the same machine;
   * worse, the source shape is what a mobilize has to collapse, and by the time the
   * effective form flips back the source is no longer readable from the pose.
   */
  type FormShape = 'mobile' | 'turret' | 'stance';
  type FormSegment = {
    at: number;
    span: number;
    stationary: boolean;
    shape: FormShape;
    sourceShape: FormShape;
    /**
     * Either end of this transition is a stance.
     *
     * It decides two things a turret deploy answers differently: how long the animation
     * runs when the replay does not say (`STANCE_TICKS`, so the fan is out on the tick the
     * volley leaves), and whether the windup dial appears at all. The dial reports "busy,
     * and for this much longer" about a Wait-only Anchor; a stance is over in one tick, and
     * a progress ring that fills and empties inside 200 ms is a flicker, not a reading.
     */
    stanceMove: boolean;
  };
  const formTimelines = new Map<ReplayActorLifeKey, FormSegment[]>();
  const stationaryForm = (formId: string | null) =>
    formId === null
      ? null
      : (replay.forms.find((form) => form.formId === formId)?.canMove ??
          true) === false;
  const shapeOfForm = (formId: string | null): FormShape =>
    stanceKindForForm(formId) !== null
      ? 'stance'
      : stationaryForm(formId) === true
        ? 'turret'
        : 'mobile';
  for (const tick of replay.ticks)
    for (const event of tick.events) {
      if (!event.sourceActor) continue;
      const cancelled = event.type === 'form-transition-cancelled';
      if (
        !cancelled &&
        event.type !== 'form-transition-started' &&
        event.type !== 'form-changed'
      )
        continue;
      // A cancel leaves the life in the form it started from; everything else moves it to
      // the form the transition targets.
      const stationary = stationaryForm(
        cancelled ? event.fromFormId : event.toFormId,
      );
      if (stationary === null) continue;
      const at = cancelled
        ? event.tick
        : (event.formTransitionStartedAtTick ?? event.tick);
      const completes = cancelled
        ? event.tick
        : (event.formTransitionCompletesAtTick ?? event.tick);
      const key = event.sourceActor.actorKey;
      const timeline = formTimelines.get(key) ?? [];
      // `form-transition-started` and the replay-v2 `form-changed` both describe one
      // transition, and a replay carrying both must not queue it twice.
      if (
        timeline.some(
          (segment) =>
            segment.at === at && segment.stationary === stationary,
        )
      )
        continue;
      const shape = shapeOfForm(
        cancelled ? event.fromFormId : event.toFormId,
      );
      const sourceShape = shapeOfForm(
        cancelled ? event.toFormId : event.fromFormId,
      );
      const stanceMove = shape === 'stance' || sourceShape === 'stance';
      timeline.push({
        at,
        // A windup the replay describes always wins; the floor only covers the transitions
        // that begin and end in one tick, and those two kinds of move are not one length.
        span: Math.max(
          completes - at + 1,
          stanceMove ? STANCE_TICKS : DEPLOY_TICKS,
        ),
        stationary,
        shape,
        sourceShape,
        stanceMove,
      });
      formTimelines.set(key, timeline);
    }
  for (const timeline of formTimelines.values())
    timeline.sort((left, right) => left.at - right.at);

  /** The transition covering the playhead for this life, or null before its first one. */
  const segmentAt = (
    actorKey: ReplayActorLifeKey,
    time: number,
  ): FormSegment | null => {
    const timeline = formTimelines.get(actorKey);
    if (!timeline) return null;
    let current: FormSegment | null = null;
    for (const segment of timeline) {
      if (segment.at > time) break;
      current = segment;
    }
    return current;
  };

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
    const look = unitProjectileLook(replay, unit.unitKey);
    const accent = new THREE.Color(unitAccent(replay, unit.unitKey));
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

    void lookModel(look, accent).then((model) => {
      if (!live || !model) return;
      // `lookModel` returns actor-owned materials while sharing the cached geometry.
      // Every rig in this arsenal may share that one painted set, but the set still has
      // to be released when this replay closes.
      const seen = new Set<THREE.Material>();
      model.traverse((node) => {
        const mesh = node as THREE.Mesh;
        if (!mesh.isMesh) return;
        const materials = Array.isArray(mesh.material)
          ? mesh.material
          : [mesh.material];
        for (const material of materials) {
          if (seen.has(material)) continue;
          seen.add(material);
          disposables.push(material);
        }
      });
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

  // Volleys are a second kind of thing in the air, with their own pooled geometry, and
  // they live beside the bolt rigs because they are fed by the same interpolation.
  const arrows = buildVolleyArrows(replay);
  group.add(arrows.group);

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
    // The rules-derived half of what a body is doing — its channel role and
    // its load — comes from the shared presenter rather than being re-decided
    // here, for the same reason the overlays take theirs from it: two
    // renderers deciding what "channelling" means is two answers.
    const presentation = presenter.at(tick);
    // Selection follows the team's collective recorded vision, shared with the flat
    // renderer. This reveals no omniscient state and avoids making the two views disagree.
    const teamVision = teamVisionAt(
      replay,
      currentTick,
      selectedUnitKey,
      showVisibility,
    );
    const hidden = (pose: ReturnType<typeof posesAt>[number]) =>
      !teamVisionSeesActor(teamVision, pose);

    const events = currentTick?.events ?? [];
    const fraction = Math.max(0, Math.min(time - tick, 1));
    const handoffReceivers = new Set<ReplayStableUnitKey>();
    for (const turn of currentTick?.actorTurns ?? []) {
      if (turn.actionResolution.validatedActionId !== 'handoff-core') continue;
      const target = turn.actionResolution.validatedPayload?.unitKey;
      if (target) handoffReceivers.add(target);
    }
    const arcState =
      currentTick?.after.mode?.kind === 'arc-relay' &&
      'visibleSignatures' in currentTick.after.mode
        ? currentTick.after.mode
        : null;
    // Lives materializing on this tick, so a body that has just arrived can come up out of
    // the floor under the ring the overlays are closing on it.
    const arriving = new Map(
      arrivalsAt(replay, time).map((arrival) => [arrival.actorKey, arrival]),
    );
    // The facings this tick runs between, so a turn's *rate* can be derived rather than
    // remembered. Frame-to-frame memory would make a scrub read as a violent spin, and
    // would give a paused bot a drift that depended on how the playhead got there.
    const opening = currentTick?.before ?? replay.initialWorld;
    const closing = currentTick?.after ?? opening;
    // The tiles either side of this tick's, used for revealed turn drift and mechanical
    // start/stop response. Position interpolation itself is shared by both renderers in
    // `posesAt`, so the WebGL body and every overlay follow one authoritative glide.
    const previous = replay.ticks[tick - 1]?.before ?? opening;
    const earlier = replay.ticks[tick - 2]?.before ?? previous;
    const previousPoseByActor = new Map(
      tick > 0
        ? posesAt(replay, tick - 0.001).map((pose) => [pose.actorKey, pose] as const)
        : [],
    );
    const tickPoseByActor = new Map(
      posesAt(replay, tick).map((pose) => [pose.actorKey, pose] as const),
    );
    const turnedIn = [
      [opening, closing],
      [previous, opening],
      [earlier, previous],
    ] as const;
    // Beams and impacts land in the second half of the tick, after movement has settled —
    // the same window the flat renderer uses, so a hit lands at the same instant in both.
    const shotProgress = Math.max(0, Math.min((fraction - 0.45) / 0.45, 1));

    // Stable slots can be absent while locked, rebuilding, ready, or queued for
    // fabrication. Hide every reusable rig first so the prior frame cannot leave a
    // destroyed life behind.
    for (const bot of bots) {
      bot.chassis.visible = false;
      bot.pips.visible = false;
      bot.anchorRing.visible = false;
      bot.scan.visible = false;
      bot.stance.visible = false;
      bot.channelRing.visible = false;
      bot.screenRing.visible = false;
      bot.upgradeRing.visible = false;
      bot.carry.visible = false;
      bot.haul.visible = false;
      bot.roleLabel.visible = false;
      bot.highlight(false);
      bot.flash(0);
      if (bot.modelMotion) {
        bot.modelMotion.wake.visible = false;
        bot.modelMotion.vents.visible = false;
      }
      // A life destroyed mid-windup would otherwise leave its charge burning on an empty
      // pad, exactly the way the deploy state used to be left behind.
      bot.charge(0);
    }

    for (const pose of posesAt(replay, time)) {
      const bot = botsByUnit.get(pose.unitKey);
      if (!bot) continue;

      const firing = events.some(
        (event) =>
          isAttackEvent(event.type) &&
          event.sourceActor?.actorKey === pose.actorKey,
      );
      const struck = events.some(
        (event) =>
          event.type === 'damage' &&
          event.targetActor?.actorKey === pose.actorKey,
      );
      const dying = events.some(
        (event) =>
          isDestructionEvent(event.type) &&
          event.targetActor?.actorKey === pose.actorKey,
      );
      const collapse = dying
        ? Math.max(0, Math.min((fraction - 0.55) / 0.45, 1))
        : 0;
      // Arriving is the mirror of collapsing: the body comes up through the floor and
      // scales into place instead of sinking and tipping out of it. Both are the same two
      // channels — height and scale — so a life can never be mid-arrival and mid-death at
      // once, and the reader never has to tell two similar animations apart.
      const arrival = arriving.get(pose.actorKey);
      const emerge = arrival ? 1 - (1 - arrival.age) ** 3 : 1;
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
      // Deploying is two stages, not two things at once: the chassis rears up on its nose,
      // then the turret takes over and its arms unfold from folded-onto-one out to
      // quarters. Overlapping them — a turret growing inside a rotating chassis — read as
      // something extruding out of the bot's head, because that is what it was.
      //
      // The clock runs **both ways**. A mobilize is the same animation played backwards, so
      // it is the same segment with its direction flipped rather than a second mechanism —
      // and once a segment has finished, the authoritative form takes over from the
      // animation outright, which is what keeps a transition the replay cancelled, or one
      // whose events a partial replay never carried, from stranding a body mid-deploy.
      const deploy = segmentAt(pose.actorKey, time);
      const deployProgress = deploy
        ? (time - deploy.at) / deploy.span
        : Number.POSITIVE_INFINITY;
      const raising =
        deploy === null || deployProgress >= 1
          ? stationary
            ? 1
            : 0
          : deploy.stationary
            ? Math.max(0, deployProgress)
            : Math.min(1, 1 - deployProgress);
      const upright = easeInOut(raising);
      // Which of the two stationary shapes this deploy is running between. A stance never
      // tips onto its nose and never spins, so every turret step below is gated on it.
      const shape: FormShape =
        raising <= 0
          ? 'mobile'
          : deploy === null
            ? stanceKindForForm(pose.formId) !== null
              ? 'stance'
              : 'turret'
            : deploy.stationary
              ? deploy.shape
              : deploy.sourceShape;
      const emplacing = shape === 'turret';
      const stancing = shape === 'stance' && bot.stanceKind !== null;
      const tipping = emplacing ? Math.min(upright / TURRET_TAKEOVER, 1) : 0;
      const unfolding = emplacing
        ? Math.max(0, (upright - TURRET_TAKEOVER) / (1 - TURRET_TAKEOVER))
        : 0;
      // A stance takes over halfway through, so the mobile body is seen charging up and the
      // stance is seen coming out of it rather than one blinking into the other.
      const stanceOut = stancing ? upright : 0;
      const stanceTakeover = stanceOut > STANCE_HANDOVER;

      bot.body.visible = unfolding <= 0 && !stanceTakeover;
      bot.turret.visible = unfolding > 0;
      bot.stance.visible = stanceTakeover;
      bot.body.rotation.z = (Math.PI / 2) * tipping;
      bot.body.position.y = TURRET_LIFT * bot.size * tipping;
      // Charging up, then coming out — **the two halves cross at the same size**, which is
      // the whole of what stops the swap reading as a pop. The body swells into the
      // handover and the stance settles back out of it, so the only thing that changes at
      // the seam is which machine is being drawn.
      const gathering = Math.min(1, stanceOut / STANCE_HANDOVER);
      bot.body.scale.setScalar(
        stancing
          ? 1 + (STANCE_CHARGE_SCALE - 1) * easeInOut(gathering)
          : 1,
      );
      const opened = Math.max(
        0,
        (stanceOut - STANCE_HANDOVER) / (1 - STANCE_HANDOVER),
      );
      bot.stance.scale.setScalar(
        STANCE_CHARGE_SCALE + (1 - STANCE_CHARGE_SCALE) * easeOut(opened),
      );
      // The fan is the statement, so it is the part that snaps: fastest at the start and
      // fully open before the settle finishes, rather than still swinging when the bolts
      // leave. It stops exactly at the profile's own headings — an overshoot here would be
      // a lie about where the volley goes.
      for (const hinge of bot.barrels)
        hinge.rotation.y =
          -(hinge.userData.fanAngle as number) * easeOut(opened);
      if (bot.plate) bot.plate.scale.y = 0.12 + 0.88 * easeOut(opened);
      const guardOpacity = 0.42 * (0.35 + 0.65 * easeOut(opened));
      // And the machine lights up while it winds: brightest at full extension, which is the
      // frame the cast leaves on, then released with the return.
      bot.charge(stanceOut);
      for (const [arm, spoke] of bot.spokes.entries())
        spoke.rotation.y = ((arm * Math.PI * 2) / TURRET_ARMS) * easeInOut(unfolding);
      // It only turns once it is out; something spinning while it unfolds reads as falling
      // over rather than deploying.
      bot.turret.rotation.y = raising >= 1 && emplacing ? time * TURRET_SPIN : 0;
      const glide = pose;
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
      const effectiveMaxHealth = maxHealthForActor(replay, {
        formId: pose.formId,
        health: pose.health,
      });
      for (const [index, pip] of bot.pipMeshes.entries()) {
        pip.visible = index < effectiveMaxHealth;
        pip.position.x =
          (index - (effectiveMaxHealth - 1) / 2) * PIP_SPACING;
        pip.material = index < pose.health ? bot.litPip : bot.lostPip;
      }

      // What this body is doing about the two mechanics the arena now carries:
      // holding the point (or guarding whoever is), and whether it is worth
      // chasing across the map.
      const mechanics = presentation.units.find(
        (candidate) => candidate.unitKey === pose.unitKey,
      );
      const channelling =
        bot.chassis.visible && mechanics?.channelRole === 'channeling';
      const screening =
        bot.chassis.visible && mechanics?.channelRole === 'screening';
      const braced = channelling || handoffReceivers.has(pose.unitKey);
      // Crossed blades: this body has a live fight. Always on, for every body,
      // which is the only way a spectator can read who is committed in a
      // warren of eight machines.
      bot.engagedMark.visible = bot.pips.visible && mechanics?.engaged === true;
      bot.channelRing.visible = channelling;
      bot.screenRing.visible = screening;
      if (channelling) {
        // A slow swell rather than a blink: a channel is a thing that runs,
        // and the objective's own arc already reports how far along it is.
        const swell = 0.5 + 0.5 * Math.sin(time * Math.PI * 1.6);
        bot.channelMaterial.opacity = 0.42 + 0.3 * swell;
        bot.channelRing.scale.setScalar(1 + 0.06 * swell);
      }
      if (screening) bot.screenMaterial.opacity = 0.24;

      // Veterancy chevrons: one per level above 1, centred like the health
      // row. Level is a per-life fold, so a respawned body starts blank
      // without any reset logic here.
      const level = veterancy.levelAt(
        time,
        pose.teamId,
        pose.unitId,
        pose.lifeId,
      );
      for (const [index, chevron] of bot.levelPips.entries()) {
        chevron.visible = bot.pips.visible && index < level - 1;
        chevron.position.x =
          (index - (level - 2) / 2) * PIP_SPACING;
      }

      // The heal channel glows while zone-heals are landing and fades
      // within a cadence of the last one, with the same slow swell the
      // objective channel uses — both are states, not events.
      const healGlow = bot.chassis.visible
        ? veterancy.healGlowAt(time, pose.teamId, pose.unitId, pose.lifeId)
        : 0;
      bot.healRing.visible = healGlow > 0;
      if (healGlow > 0) {
        const swell = 0.5 + 0.5 * Math.sin(time * Math.PI * 1.6);
        bot.healMaterial.opacity = healGlow * (0.34 + 0.26 * swell);
        bot.healRing.scale.setScalar(1 + 0.05 * swell);
      }

      // A tier this body's team just bought, thrown outward and out.
      const purchase = presentation.economy?.purchases.find(
        (entry) => entry.teamId === pose.teamId,
      );
      bot.upgradeRing.visible =
        purchase !== undefined && bot.chassis.visible;
      if (purchase !== undefined) {
        const spread = 1 - purchase.strength;
        bot.upgradeRing.scale.setScalar(1 + spread * 1.9);
        bot.upgradeMaterial.opacity = purchase.strength ** 1.4 * 0.85;
      }

      // The mind's own word for what this body is doing, drawn for VISIBLE
      // ENEMIES too — half the drama of a set-piece is seeing both sides'
      // assignments and knowing one of them is wrong. An absent tag draws
      // nothing at all: an unlabelled body should look unlabelled.
      // Arc Relay tags were suppressed here once — and a guard near base
      // read as a bug because its intent was invisible (owner review). The
      // tag now IS the unit's live order, so it always draws when present.
      const roleTag = mechanics?.roleTag ?? null;
      const visibleRoleTag = roleTag && !parsePlayRoleTag(roleTag)
        ? roleTag
        : null;
      bot.paintRole(visibleRoleTag);
      bot.roleLabel.visible =
        bot.chassis.visible && visibleRoleTag !== null;
      // Every rig piece here carries ABSOLUTE world coordinates, because the bots share
      // one container rather than each owning a group that moves. The caption was the
      // one piece that never got its x/z — it was built with a Y offset and nothing else
      // — so all sixteen rendered on top of each other at the arena's origin corner, which
      // in full screen is a legible pile of stacked words in the top-left and at windowed
      // size was small enough to pass for map decal (owner review 2026-08).
      bot.roleLabel.position.set(
        glide.x + 0.5,
        bot.roleLabel.position.y,
        glide.y + 0.5 + ROLE_LABEL_SETBACK,
      );

      const load = mechanics?.carriedScrap ?? 0;
      bot.carry.visible = load > 0 && bot.chassis.visible;
      bot.haul.visible = bot.carry.visible;
      if (bot.carry.visible) {
        bot.carry.position.set(glide.x + 0.5, 0, glide.y + 0.5);
        bot.haul.position.set(glide.x + 0.5, 0.02, glide.y + 0.5);
        const carried = Math.min(load, bot.shards.length);
        for (const [index, shard] of bot.shards.entries()) {
          shard.visible = index < carried;
          if (!shard.visible) continue;
          const angle =
            time * Math.PI * 0.8 + (index / carried) * Math.PI * 2;
          shard.position.set(
            Math.cos(angle) * CARRY_ORBIT_RADIUS,
            CARRY_HEIGHT + 0.04 * Math.sin(time * Math.PI * 2 + index),
            Math.sin(angle) * CARRY_ORBIT_RADIUS,
          );
          shard.rotation.y = angle * 1.6;
          shard.rotation.x = 0.5;
        }
        // The pool answers "how loaded", so it grows with the load rather than
        // simply announcing that there is one.
        bot.haulMaterial.opacity =
          0.16 + 0.34 * (mechanics?.carriedFraction ?? 0);
      }

      // The windup cue runs on the transition's clock, for exactly as long as it does — in
      // either direction, because mobilizing has a windup the bot cannot act through just
      // as anchoring does.
      //
      // Reading `pendingFormTransition` instead — which is what this did — meant it never
      // appeared at all on a replay whose form change completes in the tick it started, and
      // Frontline's own fixture is exactly that: `started=9 done=9`, pending never set.
      // Tracked unclamped past completion so the circle reaches full and holds a moment
      // rather than blinking out a hair before it closes.
      //
      // A stance is the one transition it stays off for. The dial answers "how much longer
      // is this bot unable to act", which is a real question about a multi-tick Anchor and
      // no question at all about a move that is over in a fifth of a second — on the volley
      // it filled, reset and filled again across three ticks while the actual cast happened
      // in the middle of it. The charge under the striker carries that moment instead.
      const anchorProgress = deploy ? deployProgress : 0;
      bot.anchorRing.visible =
        deploy !== null &&
        !deploy.stanceMove &&
        anchorProgress > 0 &&
        anchorProgress < 1 + RING_HOLD &&
        bot.chassis.visible;
      if (bot.anchorRing.visible) {
        bot.paintAnchor(Math.min(anchorProgress, 1));
        const pulse = 0.78 + Math.sin(time * Math.PI * 4) * 0.16;
        bot.anchorMaterial.opacity =
          pulse * (anchorProgress <= 1 ? 1 : 1 - (anchorProgress - 1) / RING_HOLD);
        bot.anchorRing.rotation.y = time * Math.PI * 0.45;
      }

      // The scan appears once the turret is most of the way out, and turns against the
      // ring, so the two read as one machine rather than two things that happen to move.
      // Only an emplacement scans. A stance has a facing and shows it with its nose; a
      // sweeping wedge on one would claim the omnidirectional vision it does not have.
      bot.scan.visible = emplacing && upright > 0.6 && bot.chassis.visible;
      if (bot.scan.visible) bot.scan.rotation.y = -time * Math.PI * 0.22;

      bot.flash(
        struck && shotProgress > 0.55
          ? (1 - shotProgress) / 0.45
          : 0,
      );
      const visibility = hidden(pose) ? 0.15 : 1;
      bot.fade(
        visibility * (1 - collapse * 0.75) * Math.min(1, emerge * 1.4),
      );
      // Fog and death dim the cues too, and this runs after them so it composes rather
      // than being overwritten.
      if (bot.anchorRing.visible)
        bot.anchorMaterial.opacity *= visibility * (1 - collapse);
      if (bot.scan.visible) bot.scanMaterial.opacity = 0.34 * visibility * (1 - collapse);
      if (bot.guardArc)
        (bot.guardArc.material as THREE.MeshBasicMaterial).opacity =
          guardOpacity * visibility * (1 - collapse);

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
      // Room to throw its weight about, or not. A wall anywhere around cancels the drift:
      // the nose swings diagonally through a corner, so damping only the parts that reach
      // along an axis leaves it going through the wall anyway.
      const tileX = Math.round(glide.x);
      const tileY = Math.round(glide.y);
      const boxedIn = NEIGHBOURS.some(([dx, dy]) => solid(tileX + dx, tileY + dy));

      const drift =
        Math.max(-1, Math.min(slip / (Math.PI / 2), 1)) *
        (1 - collapse) *
        (boxedIn ? 0 : 1);
      const idle =
        (stationary ? 0 : 1) *
        (1 - Math.abs(drift)) *
        (1 - collapse) *
        (braced ? 0.18 : 1);
      const sway =
        Math.sin(time * 1.9 + bot.motionPhase) +
        Math.sin(time * 3.1 + bot.motionPhase * 2.3) * 0.55;
      const rise =
        Math.sin(time * 2.3 + bot.motionPhase * 1.7) +
        Math.sin(time * 1.3 + bot.motionPhase) * 0.5;
      const kick = firing
        ? Math.sin(shotProgress * Math.PI) * 0.14
        : 0;
      const hover =
        (LOW_HOVER_HEIGHT + rise * LOW_HOVER_BOB) *
        (1 - collapse) *
        emerge;

      const openingActor = opening?.actors.find(
        (actor) => actor.actorKey === pose.actorKey,
      );
      const closingActor = closing?.actors.find(
        (actor) => actor.actorKey === pose.actorKey,
      );
      const turnDelta =
        openingActor && closingActor
          ? shortestTurn(
              directionAngle(openingActor.facing),
              directionAngle(closingActor.facing),
            )
          : 0;
      const previousPose = previousPoseByActor.get(pose.actorKey);
      const previousMotion = previousPose
        ? { x: previousPose.motionX, y: previousPose.motionY }
        : actorStep(previous, opening, pose.actorKey);
      const previousSpeed = Math.hypot(previousMotion.x, previousMotion.y);
      const signatureActive =
        arcState?.visibleSignatures.some(
          (signature) => signature.ownerActor.actorKey === pose.actorKey,
        ) ?? false;
      const signatureCooling =
        signatureCooldowns.get(pose.actorKey)?.some(
          (window) => time >= window.startedTick && time < window.readyTick,
        ) ?? false;
      const signatureState: ArcSignatureBodyState = signatureActive
        ? 'active'
        : signatureCooling
          ? 'cooldown'
          : 'ready';
      const travel = signedTravel.get(pose.actorKey);
      const tickPose = tickPoseByActor.get(pose.actorKey);
      const motionFrame = bot.modelMotion?.update(
        {
          time,
          fraction,
          facingAngle: pose.angle,
          motionX: pose.motionX,
          motionY: pose.motionY,
          previousMotionX: previousMotion.x,
          previousMotionY: previousMotion.y,
          previousSpeed,
          turnDelta,
          signedTravel:
            (travel?.[tick] ?? 0) +
            (tickPose ? signedPoseDisplacement(tickPose, pose) : 0),
          braced,
          signatureState,
        },
        visibility * (1 - collapse),
      );

      bot.chassis.rotation.z = collapse * 0.5;
      bot.chassis.rotation.x = collapse * 0.22;
      bot.chassis.position.y =
        -collapse * BOT_HEIGHT * 0.55 - (1 - emerge) * BOT_HEIGHT * 1.6;
      bot.chassis.scale.setScalar(
        (1 - collapse * 0.16) * (0.35 + 0.65 * emerge),
      );
      bot.turret.scale.setScalar(
        TURRET_SCALE +
          (firing ? Math.sin(shotProgress * Math.PI) * 0.045 : 0),
      );

      bot.body.position.x = -kick + (motionFrame?.hullLagForward ?? 0);
      bot.body.position.z =
        drift * DRIFT_SLIDE +
        idle * sway * IDLE_SWAY +
        (motionFrame?.hullLagLateral ?? 0);
      bot.body.position.y =
        TURRET_LIFT * bot.size * tipping +
        (bot.mobileLowHover ? hover : idle * rise * IDLE_RISE);
      bot.stance.position.y = bot.stanceLowHover ? hover : 0;
      bot.body.rotation.y =
        -drift * DRIFT_YAW +
        idle * sway * IDLE_YAW +
        (motionFrame?.counterSteer ?? 0);
      bot.body.rotation.x =
        drift * DRIFT_LEAN +
        idle * rise * IDLE_ROLL +
        (motionFrame?.bank ?? 0);
      bot.body.rotation.z =
        (Math.PI / 2) * tipping + (motionFrame?.pitch ?? 0);
    }

    // `boltsAt` is the same derivation the flat renderer uses — interpolated across the
    // authoritative substeps, so a bolt flies rather than teleporting between tiles the way
    // reading `ticks[tick].projectiles` straight made it.
    //
    // A scrub is not motion. Dragging the playhead across half a match would otherwise have
    // every bolt easing through the turns it "made" on the way, so a jump snaps instead.
    const jumped = Math.abs(time - lastTime) > 1.5;
    lastTime = time;

    // In FOV mode a bolt the followed team cannot see is not drawn at all, rather than
    // ghosted the way a bot is. An unseen bolt is precisely the threat it does not know
    // about, and a faint one on screen would still answer the question.
    const boltHidden = (id: string, x: number, y: number) =>
      !teamVisionSeesProjectile(teamVision, id, x, y);

    // A volley's members are drawn as its arrow and nowhere else — three bolts sitting
    // inside the glyph would undo the one thing the glyph is for.
    const grouped = volleyLanes(replay);
    arrows.update(time, boltHidden);

    const flying = arsenals.map(() => 0);
    const alive = new Set<string>();
    for (const bolt of boltsAt(replay, time)) {
      if (grouped.has(bolt.id)) continue;
      if (boltHidden(bolt.id, bolt.x, bolt.y)) continue;
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
      arrows.dispose();
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

function actorStep(
  fromState: ReplayWorldSnapshot | null | undefined,
  toState: ReplayWorldSnapshot | null | undefined,
  actorKey: ReplayActorLifeKey,
): { x: number; y: number } {
  const from = fromState?.actors.find((actor) => actor.actorKey === actorKey);
  const to = toState?.actors.find((actor) => actor.actorKey === actorKey);
  return from && to
    ? {
        x: to.position.x - from.position.x,
        y: to.position.y - from.position.y,
      }
    : { x: 0, y: 0 };
}

/**
 * Distance rolled, signed by whether displacement leads or trails the body axes.
 * Magnitude is always the actual tile displacement; facing chooses direction only, so a
 * reverse or lateral move cannot accidentally inherit the visual speed of a forward move.
 */
function signedPoseDisplacement(
  from: Pick<BotPose, 'x' | 'y' | 'angle'>,
  to: Pick<BotPose, 'x' | 'y'>,
): number {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const distance = Math.hypot(dx, dy);
  if (distance === 0) return 0;
  const forward = dx * Math.cos(from.angle) + dy * Math.sin(from.angle);
  const lateral = -dx * Math.sin(from.angle) + dy * Math.cos(from.angle);
  const signedAxis = Math.abs(forward) >= Math.abs(lateral) ? forward : lateral;
  return distance * (signedAxis < 0 ? -1 : 1);
}

export function signedTravelByActor(
  replay: ReplayModel,
): Map<ReplayActorLifeKey, number[]> {
  const result = new Map<ReplayActorLifeKey, number[]>();
  const running = new Map<ReplayActorLifeKey, number>();
  let prior = new Map(
    posesAt(replay, 0).map((pose) => [pose.actorKey, pose] as const),
  );
  for (const pose of prior.values()) {
    const samples = Array.from({ length: replay.ticks.length + 1 }, () => 0);
    result.set(pose.actorKey, samples);
    running.set(pose.actorKey, 0);
  }
  for (let boundary = 1; boundary <= replay.ticks.length; boundary += 1) {
    const current = new Map(
      posesAt(replay, boundary).map((pose) => [pose.actorKey, pose] as const),
    );
    for (const pose of current.values()) {
      let samples = result.get(pose.actorKey);
      if (!samples) {
        samples = Array.from({ length: replay.ticks.length + 1 }, () => 0);
        result.set(pose.actorKey, samples);
      }
      const distance = running.get(pose.actorKey) ?? 0;
      const priorPose = prior.get(pose.actorKey);
      const next =
        distance + (priorPose ? signedPoseDisplacement(priorPose, pose) : 0);
      samples[boundary] = next;
      running.set(pose.actorKey, next);
    }
    prior = current;
  }
  return result;
}

/** Exact renderer-only recharge windows derived from visible authoritative activations. */
function signatureCooldownsByActor(
  replay: ReplayModel,
): Map<ReplayActorLifeKey, { startedTick: number; readyTick: number }[]> {
  const result = new Map<
    ReplayActorLifeKey,
    { startedTick: number; readyTick: number }[]
  >();
  if (
    replay.contract.kind !== 'v3-generic' ||
    replay.contract.rawContract.rules.gameMode.kind !== 'arc-relay'
  )
    return result;
  const cooldowns = new Map(
    replay.contract.rawContract.rules.gameMode.signatures.map((signature) => [
      signature.signatureId,
      signature.cooldownTicks,
    ]),
  );
  const seen = new Set<string>();
  for (const tick of replay.ticks) {
    if (
      tick.after.mode?.kind !== 'arc-relay' ||
      !('visibleSignatures' in tick.after.mode)
    )
      continue;
    for (const signature of tick.after.mode.visibleSignatures) {
      if (seen.has(signature.operationId)) continue;
      seen.add(signature.operationId);
      const cooldown = cooldowns.get(signature.signatureId);
      if (cooldown === undefined) continue;
      const windows = result.get(signature.ownerActor.actorKey) ?? [];
      windows.push({
        startedTick: signature.startedTick,
        readyTick: signature.startedTick + cooldown,
      });
      result.set(signature.ownerActor.actorKey, windows);
    }
  }
  return result;
}

/**
 * A ring texture that can be filled to a fraction of a turn.
 *
 * Drawn white and tinted by the material, so one of these serves any accent. The arc starts
 * at twelve o'clock and runs clockwise, which is where a progress ring is read from.
 */
function progressRing():
  | { texture: THREE.CanvasTexture; paint: (progress: number) => void }
  | null {
  if (typeof document === 'undefined') return null;
  const size = 256;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext('2d');
  if (!context) return null;

  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;

  const paint = (progress: number) => {
    context.clearRect(0, 0, size, size);
    const radius = size * 0.42;
    // The track: always there, so an untouched ring reads as a dial rather than a gap.
    context.strokeStyle = 'rgba(255, 255, 255, 0.22)';
    context.lineWidth = size * 0.05;
    context.beginPath();
    context.arc(size / 2, size / 2, radius, 0, Math.PI * 2);
    context.stroke();

    context.strokeStyle = 'rgba(255, 255, 255, 0.95)';
    context.lineWidth = size * 0.07;
    context.lineCap = 'round';
    context.beginPath();
    context.arc(
      size / 2,
      size / 2,
      radius,
      -Math.PI / 2,
      -Math.PI / 2 + Math.PI * 2 * Math.max(0, Math.min(progress, 1)),
    );
    context.stroke();
    texture.needsUpdate = true;
  };

  paint(0);
  return { texture, paint };
}

/**
 * A broken ring lying flat on the floor, as one geometry.
 *
 * `THREE.RingGeometry` only draws continuous annuli, so a dashed one would otherwise be a
 * mesh per dash — twenty objects per bot, three hundred in a sixteen-body match, to draw
 * a cue that is on screen for one of them. The dashes are emitted into a single buffer
 * instead, so a dashed ring costs exactly what a solid one does.
 *
 * Built in the XZ plane rather than built in XY and rotated, because that is the plane it
 * is read in: a caller that forgets the rotation gets a ring standing on edge, which is
 * the mistake every other flat piece of geometry in this file has to remember not to make.
 */
function dashedRingGeometry(inner: number, outer: number): THREE.BufferGeometry {
  const positions: number[] = [];
  const stride = (Math.PI * 2) / SELECTION_RING_DASHES;
  const arc = stride * SELECTION_RING_DUTY;
  const steps = 4;
  for (let dash = 0; dash < SELECTION_RING_DASHES; dash += 1) {
    for (let step = 0; step < steps; step += 1) {
      const from = dash * stride + (arc * step) / steps;
      const to = dash * stride + (arc * (step + 1)) / steps;
      const fx = Math.cos(from);
      const fz = Math.sin(from);
      const tx = Math.cos(to);
      const tz = Math.sin(to);
      positions.push(
        fx * inner, 0, fz * inner,
        fx * outer, 0, fz * outer,
        tx * outer, 0, tz * outer,
        fx * inner, 0, fz * inner,
        tx * outer, 0, tz * outer,
        tx * inner, 0, tz * inner,
      );
    }
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute(
    'position',
    new THREE.Float32BufferAttribute(positions, 3),
  );
  geometry.computeVertexNormals();
  return geometry;
}

/** Ease used for anything that starts and stops, matching the flat renderer's motion. */
function easeInOut(t: number): number {
  return t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2;
}

/**
 * Ease used for anything that is *released* rather than played: fastest at the start.
 *
 * A stance is not a deploy. The fan has to be open before the shot it announces, and an
 * ease that spends its first third barely moving spends it exactly where the telegraph
 * needed to be.
 */
function easeOut(t: number): number {
  return 1 - (1 - Math.max(0, Math.min(t, 1))) ** 3;
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
  const width = 160;
  const height = 48;
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext('2d');
  if (!context) return null;

  const image = context.createImageData(width, height);
  const red = Math.round(accent.r * 255);
  const green = Math.round(accent.g * 255);
  const blue = Math.round(accent.b * 255);

  // A capsule as a distance field, in pixels.
  //
  // Two crossed gradients give a rectangle, and a rectangle brightest along the edge where
  // it is cut is a streak that stops dead at the bolt — the front end looked sliced.
  // Tapering only the width did not fix it: the centre line stayed fully opaque to the last
  // texel, so it still ended in a bright sliver. Distance from a line segment rounds both
  // ends in two dimensions at once. The head cap sits a full radius inside the canvas,
  // because a cap drawn against the edge is clipped flat and the whole fix undone.
  const headRadius = height * 0.45;
  const tailRadius = height * 0.06;
  const headX = width - headRadius - 1;
  const tailX = headRadius * 0.5;

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const along = Math.max(0, Math.min((x - tailX) / (headX - tailX), 1));
      const radius = tailRadius + (headRadius - tailRadius) * along ** 0.7;
      const distance = Math.hypot(x - (tailX + (headX - tailX) * along), y - height / 2);

      const offset = (y * width + x) * 4;
      image.data[offset] = red;
      image.data[offset + 1] = green;
      image.data[offset + 2] = blue;
      const edge = distance / radius;
      const profile = edge >= 1 ? 0 : (1 - edge * edge) ** 1.5;
      image.data[offset + 3] = Math.round(255 * 0.92 * profile * (0.12 + 0.88 * along ** 1.5));
    }
  }
  context.putImageData(image, 0, 0);

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
