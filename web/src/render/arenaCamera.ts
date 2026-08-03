import type { ReplayModel, ReplayStableUnitKey } from '../replayModel';
import { isAttackEvent, isDestructionEvent } from '../replayModel';
import { posesAt } from './interpolate';

/**
 * Where the arena is looked at from, shared by both renderers.
 *
 * The viewer used to frame the whole map and never move again, which is correct and
 * lifeless: a 40×30 arena on a phone puts two duelling machines inside a tenth of the
 * screen. This follows the fight instead — and because the flat renderer and the WebGL
 * one must agree about what "the action" is, the *decision* lives here as arithmetic on
 * tile coordinates and neither renderer owns any of it. Canvas2D turns a frame into a
 * tile size and an origin; the 3D camera turns the same frame into a distance and a look
 * target. Same numbers, two projections.
 *
 * Three properties are load-bearing and each is tested:
 *
 * - **It never cuts.** Every change is a critically damped spring toward the target, so
 *   the camera has no discontinuities even when the target does.
 * - **It has a deadband.** A spawn or a step sideways does not re-aim: the committed frame
 *   is kept while it still holds the fitted box *near its middle*. Re-aiming on every frame
 *   is what turns a following camera into a zoom that hunts; never re-aiming while the box
 *   is technically still inside is what leaves the fight off to one side.
 * - **The user wins.** Any pan or zoom gesture drops auto-fit until it is re-engaged from
 *   the chrome. A camera that fights the hand on it is worse than one that never moves.
 */

/**
 * The margin the whole-arena framing has always had, in tiles.
 *
 * Fractional rather than a whole tile: at 24×18 a full tile is 4% of width and 5.5% of
 * height given away to black, and on a letterboxed phone every pixel of arena is scarce.
 * `drawArena` and the canvas hit-test both take it from here — they used to state it
 * twice, with a comment on each asking the other not to drift.
 */
export const ARENA_MARGIN_TILES = 0.4;

/**
 * How much room is left around the fitted lives, in tiles.
 *
 * **Read it as clear floor beyond a body, not as padding**: the fitted points are tile
 * centres, and a machine is about a tile across, so this leaves a tile of ground past the
 * outermost life on every side — enough that a step, a spring lag and the deadband's own
 * slack together cannot walk anyone off the edge of the frame.
 *
 * It is deliberately smaller than it looks, because the margin is multiplied by the
 * viewport before it is seen. A frame is grown to the viewport's shape after this, so on a
 * 2.16:1 phone in landscape the *horizontal* span is roughly `2 × margin × aspect` — at the
 * 2.6 this used to be, a lone survivor was framed in eleven tiles of a twenty-three-tile
 * map and read as a speck with a lot of floor around it. Halving the margin halves the
 * black, and the fit is still a fit.
 */
const FOCUS_MARGIN_TILES = 1.5;

/**
 * The closest the camera may ever get, in tiles across.
 *
 * A single surviving bot fits in about two tiles, and a camera that honours that shows a
 * machine and no arena — no cover, no objective, no idea where the shot came from. Six is
 * about two tiles of context either side of a duel, which is where cover lives. Modes with
 * a stronger spectator grammar can ask for a wider floor through `minSpan`.
 */
const MIN_SPAN_TILES = 6;

/**
 * Mode-owned closest shot for the automatic director.
 *
 * Arc Relay needs the carrier's bank lane and nearby cover in frame even when the current
 * beat has only one anchor. This belongs beside the shared fit arithmetic: putting the
 * value in a renderer made Canvas2D hold ten tiles while WebGL silently fell back to six.
 */
export function directorMinSpan(replay: ReplayModel): number | undefined {
  return replay.contract.kind === 'v3-generic' &&
    replay.contract.modeKind === 'arc-relay'
    ? 10
    : undefined;
}

/** A framing of the arena: a centre and the span it shows, both in tiles. */
export interface ArenaFrame {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** Everything the fit needs that is not a position: the map, and the shape of the hole. */
export interface ArenaFraming {
  mapWidth: number;
  mapHeight: number;
  /** Viewport width ÷ height. */
  aspect: number;
  /** Padding around the fitted lives, in tiles. */
  margin?: number;
  /** Never closer than this many tiles across. */
  minSpan?: number;
}

/**
 * The whole arena, at this aspect ratio — the maximum zoom-out, and today's framing.
 *
 * Grows whichever axis is short so the frame matches the viewport's shape. A frame that
 * did not would be interpreted differently by the two renderers, since each fits it to a
 * different one of its axes.
 */
export function fullArenaFrame(framing: ArenaFraming): ArenaFrame {
  const width = Math.max(
    framing.mapWidth + ARENA_MARGIN_TILES,
    (framing.mapHeight + ARENA_MARGIN_TILES) * framing.aspect,
  );
  return {
    x: framing.mapWidth / 2,
    y: framing.mapHeight / 2,
    width,
    height: width / framing.aspect,
  };
}

/**
 * The frame that holds these positions, with room around them — centred on them.
 *
 * Clamped in *span* at both ends: never closer than `minSpan` tiles across and never wider
 * than the whole arena. The centre, though, is the middle of the fitted box, and the frame
 * is allowed to hang over the edge of the map to keep it there — see `frameCentre`, which
 * is where the fit used to lose the action to one side of the screen.
 */
export function focusFrame(
  points: readonly { x: number; y: number }[],
  framing: ArenaFraming,
): ArenaFrame {
  const full = fullArenaFrame(framing);
  if (points.length === 0) return full;

  const margin = framing.margin ?? FOCUS_MARGIN_TILES;
  const minSpan = framing.minSpan ?? MIN_SPAN_TILES;
  let left = Infinity;
  let right = -Infinity;
  let top = Infinity;
  let bottom = -Infinity;
  for (const point of points) {
    left = Math.min(left, point.x);
    right = Math.max(right, point.x);
    top = Math.min(top, point.y);
    bottom = Math.max(bottom, point.y);
  }

  let width = Math.max(right - left + margin * 2, minSpan);
  let height = Math.max(bottom - top + margin * 2, minSpan / framing.aspect);
  // Match the viewport's shape by growing, never by cropping: shrinking the long axis
  // would push a life out of frame, which is the one thing a fit may not do.
  if (width / height < framing.aspect) width = height * framing.aspect;
  else height = width / framing.aspect;

  if (width >= full.width) return full;
  return {
    x: frameCentre((left + right) / 2, width, framing.mapWidth),
    y: frameCentre((top + bottom) / 2, height, framing.mapHeight),
    width,
    height,
  };
}

/**
 * Where a span of `size` tiles may sit on an axis `extent` tiles long.
 *
 * **The action decides, and the frame may hang over the edge of the map to hold it in the
 * middle.** This used to keep the whole frame inside the padded map instead, which sounds
 * obviously right and is exactly why a fitted view sat off to one side on a phone: the fit
 * is grown to the viewport's shape just above, so on a letterboxed device that grown span
 * is most of the map's short axis — and a frame forbidden from leaving the map can then
 * only be centred inside a band a couple of tiles wide. A duel by the right-hand spawn
 * came out 30% of the screen right of centre with empty floor beside it, which is the one
 * thing a following camera exists to prevent (DECISIONS #175).
 *
 * One case still overrules the action: a span that already covers the whole axis has
 * nothing left to choose. Centring it on the map keeps the bars even and the arena whole,
 * where sliding it would push the map off one side and show background on the other for no
 * gain. That is also the only case where a fit shows a bar it did not have to.
 *
 * The remaining clamp is on the centre rather than on the frame — the camera looks *at* the
 * arena — and it only ever binds on a gesture, since a fitted centre is a spot on the board
 * by construction.
 */
function frameCentre(centre: number, size: number, extent: number): number {
  if (size >= extent + ARENA_MARGIN_TILES) return extent / 2;
  const pad = ARENA_MARGIN_TILES / 2;
  return Math.min(Math.max(centre, -pad), extent + pad);
}

/**
 * How far the action may drift inside the committed frame before it is re-aimed, as a
 * fraction of the committed span.
 */
const CENTRE_DRIFT = 0.1;

/**
 * Has the action left the frame we committed to?
 *
 * The deadband. `committed` is what the camera is already moving toward; `candidate` is
 * what a fresh fit of this instant would ask for. Re-aiming whenever those differ is what
 * makes a following camera thrash — a bot's single step, one fabrication, one death, and
 * the zoom pumps. So the committed frame is kept while it still holds the candidate, and
 * abandoned only when something has escaped it or when it has become so much larger than
 * the action that it is showing mostly floor.
 *
 * `tolerance` is a fraction of the committed span: a candidate edge must clear the
 * committed edge by that much before it counts as escaped, so a body drifting exactly
 * along the boundary cannot toggle the decision every frame.
 */
export function frameEscapes(
  committed: ArenaFrame,
  candidate: ArenaFrame,
  tolerance = 0.05,
  drift = CENTRE_DRIFT,
): boolean {
  const slackX = committed.width * tolerance;
  const slackY = committed.height * tolerance;
  if (
    candidate.x - candidate.width / 2 < committed.x - committed.width / 2 - slackX ||
    candidate.x + candidate.width / 2 > committed.x + committed.width / 2 + slackX ||
    candidate.y - candidate.height / 2 < committed.y - committed.height / 2 - slackY ||
    candidate.y + candidate.height / 2 > committed.y + committed.height / 2 + slackY
  ) {
    return true;
  }
  // Containment is not enough on its own. A life dying reshapes the fitted box without
  // moving it out of frame, so the action can end up sitting a tenth of the screen off to
  // one side — and stay there for the rest of the replay, because nothing ever escapes and
  // the span barely changed. Re-centring is a pan the spring absorbs; it is not the zoom
  // hunt the deadband exists to stop, which is why this band is twice the edge tolerance
  // rather than zero.
  if (
    Math.abs(candidate.x - committed.x) > committed.width * drift ||
    Math.abs(candidate.y - committed.y) > committed.height * drift
  ) {
    return true;
  }
  // The other direction, with a much wider band: pulling in is never urgent, and a tight
  // rule here is exactly what makes a camera breathe in and out on its own.
  return candidate.width < committed.width * 0.7;
}

/**
 * Where the camera should be looking at this instant, before any smoothing.
 *
 * Interpolated poses rather than the tick's snapped positions, because the camera is
 * re-evaluated every frame: fitting tile coordinates would make the target jump on each
 * tick boundary and the spring would show it as a stutter.
 *
 * **Selection is per unit, and the camera fits that unit's team.** Following a single
 * machine would frame it and nothing it is fighting, which answers a question nobody
 * watching a match is asking. A team whose lives are all gone falls back to everybody, so
 * a wipe does not leave the camera staring at an empty pad.
 */
export function focusPointsAt(
  replay: ReplayModel,
  time: number,
  selectedUnitKey: ReplayStableUnitKey | null,
): { x: number; y: number }[] {
  const active = posesAt(replay, time).filter(
    (pose) => pose.status === 'active',
  );
  const teamId =
    selectedUnitKey === null
      ? null
      : replay.units.find((unit) => unit.unitKey === selectedUnitKey)?.teamId ??
        null;
  const chosen =
    teamId === null
      ? autoDirectorPoints(replay, time, active)
      : active.filter((pose) => pose.teamId === teamId);
  // A tile's centre, which is where both renderers draw the body standing on it.
  return (chosen.length > 0 ? chosen : active).map((pose) => ({
    x: pose.x + 0.5,
    y: pose.y + 0.5,
  }));
}

type DirectorPoint = { x: number; y: number };

/**
 * Arc Relay's outcome-blind camera director.
 *
 * It reads only facts already revealed at `time`. The ordering is the spectator grammar:
 * a carrier entering the bank run, then violence, then a loose Core contest, then the next
 * scheduled birth. A quiet board falls back to the closest opposing pair rather than all
 * sixteen bodies, which is what made the old "fit" indistinguishable from overview.
 */
function autoDirectorPoints(
  replay: ReplayModel,
  time: number,
  active: readonly ReturnType<typeof posesAt>[number][],
): DirectorPoint[] {
  if (
    replay.contract.kind !== 'v3-generic' ||
    replay.contract.modeKind !== 'arc-relay'
  ) {
    return [...active];
  }
  const tickIndex = Math.max(
    0,
    Math.min(Math.floor(time), replay.ticks.length - 1),
  );
  const mode =
    replay.ticks[tickIndex]?.after.mode ?? replay.initialWorld?.mode;
  if (mode?.kind !== 'arc-relay' || !('visibleCores' in mode)) return [...active];

  const carrierRuns = mode.visibleCores
    .filter(
      (core) => core.disposition === 'carried' && core.carrierActor !== null,
    )
    .map((core) => {
      const carrier = active.find(
        (pose) => pose.actorKey === core.carrierActor!.actorKey,
      );
      const reactor = mode.reactors.find(
        (candidate) => candidate.teamId === core.carrierActor!.teamId,
      );
      return carrier && reactor
        ? {
            carrier,
            reactor,
            distance: Math.max(
              Math.abs(reactor.position.x - carrier.x),
              Math.abs(reactor.position.y - carrier.y),
            ),
            pulseCore: reactor.chargePips >= 2,
          }
        : null;
    })
    .filter((entry): entry is NonNullable<typeof entry> => entry !== null)
    .sort(
      (left, right) =>
        Number(right.pulseCore) - Number(left.pulseCore) ||
        left.distance - right.distance,
    );
  const bankRun = carrierRuns.find((run) => run.distance <= 6);
  if (bankRun) {
    return includeNearby(
      active,
      [
        bankRun.carrier,
        { x: bankRun.reactor.position.x, y: bankRun.reactor.position.y },
      ],
      2.75,
    );
  }

  // Hold a fight for three ticks so one impact does not make the target flicker away on
  // the next frame. These are historical, already-revealed events only.
  for (let age = 0; age <= 2; age += 1) {
    const tick = replay.ticks[tickIndex - age];
    if (!tick) continue;
    const fight = tick.events.filter(
      (event) =>
        event.type === 'damage' ||
        isDestructionEvent(event.type) ||
        isAttackEvent(event.type),
    );
    if (fight.length === 0) continue;
    const points: DirectorPoint[] = [];
    for (const event of fight) {
      const source = event.sourceActor
        ? active.find((pose) => pose.actorKey === event.sourceActor!.actorKey)
        : null;
      const target = event.targetActor
        ? active.find((pose) => pose.actorKey === event.targetActor!.actorKey)
        : null;
      if (source) points.push(source);
      else if (event.from) points.push(event.from);
      if (target) points.push(target);
      else if (event.to) points.push(event.to);
    }
    if (points.length > 0) return includeNearby(active, points, 2.5);
  }

  const loose = [...mode.visibleCores]
    .filter((core) => core.disposition === 'loose')
    .sort((left, right) =>
      left.coreId.sourceWellId.localeCompare(right.coreId.sourceWellId) ||
      left.coreId.sourceOrdinal - right.coreId.sourceOrdinal,
    )[0];
  if (loose) return includeNearby(active, [loose.position], 3.5);

  const nextWell = [...mode.wells]
    .filter((well) => well.nextScheduledBirthTick !== null)
    .sort(
      (left, right) =>
        left.nextScheduledBirthTick! - right.nextScheduledBirthTick! ||
        left.wellId.localeCompare(right.wellId),
    )[0];
  if (nextWell)
    return includeNearby(active, [nextWell.position], 3.25);

  let closest: [DirectorPoint, DirectorPoint] | null = null;
  let closestDistance = Infinity;
  for (const left of active) {
    for (const right of active) {
      if (left.teamId === right.teamId) continue;
      const distance = Math.abs(left.x - right.x) + Math.abs(left.y - right.y);
      if (distance < closestDistance) {
        closest = [left, right];
        closestDistance = distance;
      }
    }
  }
  return closest ? includeNearby(active, closest, 2.5) : [...active];
}

function includeNearby(
  active: readonly DirectorPoint[],
  anchors: readonly DirectorPoint[],
  radius: number,
): DirectorPoint[] {
  const chosen: DirectorPoint[] = [...anchors];
  for (const pose of active) {
    if (
      anchors.some(
        (anchor) =>
          Math.hypot(pose.x - anchor.x, pose.y - anchor.y) <= radius,
      )
    ) {
      chosen.push(pose);
    }
  }
  return chosen;
}

/**
 * Canvas2D's transform for a frame: how big a tile is, and where tile (0,0) starts.
 *
 * With no frame this is the historical whole-map framing, to the pixel — an integer tile
 * size and integer origins, which is what every recorded golden frame was drawn with. The
 * auto camera passes a frame and gets a fractional tile, because a camera that could only
 * zoom in whole pixels per tile would jerk.
 */
export function arenaViewport(
  frame: ArenaFrame | null,
  mapWidth: number,
  mapHeight: number,
  width: number,
  height: number,
): { tile: number; originX: number; originY: number } {
  if (!frame) {
    const tile = Math.floor(
      Math.min(
        width / (mapWidth + ARENA_MARGIN_TILES),
        height / (mapHeight + ARENA_MARGIN_TILES),
      ),
    );
    return {
      tile,
      originX: Math.floor((width - tile * mapWidth) / 2),
      originY: Math.floor((height - tile * mapHeight) / 2),
    };
  }
  const tile = Math.min(width / frame.width, height / frame.height);
  return {
    tile,
    originX: width / 2 - frame.x * tile,
    originY: height / 2 - frame.y * tile,
  };
}

/** How fast the camera converges, in radians per second of the critically damped spring. */
const SPRING = 3.1;

/**
 * The camera itself: a frame, a target, and the rule for getting from one to the other.
 *
 * Deliberately not a React hook and deliberately DOM-free — the 3D renderer drives it from
 * a `requestAnimationFrame` loop that must not re-render anything, and a test drives it
 * with a synthetic clock.
 */
export class ArenaCamera {
  /** The frame being drawn right now. */
  frame: ArenaFrame;
  /** What it is heading toward. Only replaced when the fit escapes it. */
  private target: ArenaFrame;
  private velocity = { x: 0, y: 0, span: 0 };
  private following = true;
  /** Set by `engage`, so the first fit after a gesture is never held off by the deadband. */
  private forceAim = false;

  constructor(framing: ArenaFraming) {
    this.frame = fullArenaFrame(framing);
    this.target = { ...this.frame };
  }

  /** True while the camera is fitting the action rather than obeying a gesture. */
  get auto(): boolean {
    return this.following;
  }

  /** The frame the camera is converging on, for tests and for gesture arithmetic. */
  get aimed(): ArenaFrame {
    return { ...this.target };
  }

  /**
   * Offer a freshly fitted frame. Ignored unless the action has escaped the committed one.
   * Returns whether it was taken, which is what makes the deadband observable.
   */
  aim(candidate: ArenaFrame): boolean {
    if (!this.following) return false;
    if (!this.forceAim && !frameEscapes(this.target, candidate)) return false;
    this.forceAim = false;
    this.target = { ...candidate };
    return true;
  }

  /**
   * Re-shape for a new viewport without moving the camera off the action.
   *
   * A rotated phone changes the aspect ratio, not the fight, so the frame keeps its centre
   * and its span and only takes the new shape — and stays inside the arena at it.
   */
  reframe(framing: ArenaFraming): void {
    const full = fullArenaFrame(framing);
    this.target = shaped(this.target, full, framing);
    this.frame = shaped(this.frame, full, framing);
  }

  /** Snap to a frame with no animation. Used once, when a replay opens. */
  settle(frame: ArenaFrame): void {
    this.frame = { ...frame };
    this.target = { ...frame };
    this.velocity = { x: 0, y: 0, span: 0 };
  }

  /**
   * Advance the spring by `dt` seconds.
   *
   * Critically damped and solved implicitly, so it is stable at any step — a tab that was
   * backgrounded for two seconds returns a huge `dt`, and an explicit integrator would
   * answer that by flinging the camera across the map. The span is sprung in log space
   * because zoom is multiplicative: linear interpolation between two spans makes zooming
   * out feel fast and zooming in feel slow, on the same spring.
   */
  advance(dt: number): void {
    const step = Math.max(0, Math.min(dt, 0.1));
    if (step === 0) return;
    const x = damp(this.frame.x, this.velocity.x, this.target.x, step);
    const y = damp(this.frame.y, this.velocity.y, this.target.y, step);
    const span = damp(
      Math.log(this.frame.width),
      this.velocity.span,
      Math.log(this.target.width),
      step,
    );
    this.velocity = { x: x.velocity, y: y.velocity, span: span.velocity };
    const width = Math.exp(span.value);
    this.frame = {
      x: x.value,
      y: y.value,
      width,
      // Aspect belongs to the target, which is the frame that was fitted to the viewport.
      height: width / (this.target.width / this.target.height),
    };
  }

  /**
   * Stop following, without moving.
   *
   * What every gesture below does implicitly: someone who just aimed the camera by hand
   * wants it where they left it. Kept separate from `pan`/`zoom` so "stop following" and
   * "look here" are two statements rather than one with a zero argument.
   */
  release(): void {
    this.following = false;
  }

  /**
   * Stop following and frame the whole arena.
   *
   * What the chrome's toggle does when it is switched *off*, and deliberately not the same
   * as `release`: switching a fit off means "show me the board", and a toggle that stopped
   * following while staying zoomed in would strand a phone at whatever magnification the
   * last skirmish happened to leave — with no wheel to undo it.
   */
  showEverything(framing: ArenaFraming): void {
    this.following = false;
    this.target = fullArenaFrame(framing);
  }

  /**
   * A gesture. Drops auto-fit until something re-engages it.
   *
   * Bounded the same way the fit is — the point being looked at stays over the arena —
   * because a hand that can reach somewhere the fit cannot is a camera that jumps the
   * moment auto-fit is handed back.
   */
  pan(dx: number, dy: number, framing: ArenaFraming): void {
    this.release();
    this.target = {
      ...this.target,
      x: frameCentre(this.target.x + dx, this.target.width, framing.mapWidth),
      y: frameCentre(this.target.y + dy, this.target.height, framing.mapHeight),
    };
  }

  /** A gesture. `factor` above 1 moves closer. */
  zoom(factor: number, framing: ArenaFraming): void {
    this.release();
    const full = fullArenaFrame(framing);
    const minSpan = framing.minSpan ?? MIN_SPAN_TILES;
    const width = Math.min(
      Math.max(this.target.width / factor, minSpan),
      full.width,
    );
    this.target = {
      x: frameCentre(this.target.x, width, framing.mapWidth),
      y: frameCentre(this.target.y, width / framing.aspect, framing.mapHeight),
      width,
      height: width / framing.aspect,
    };
  }

  /** Hand the camera back to the action. The next `aim` is always taken. */
  engage(): void {
    this.following = true;
    // Whatever a gesture left behind must not act as a deadband against the first fit
    // after re-engaging, or re-engaging on a frame that happens to contain everybody
    // would look like the control did nothing.
    this.forceAim = true;
  }
}

/** A frame at a new aspect ratio: same centre, same span, still looking at the arena. */
function shaped(
  frame: ArenaFrame,
  full: ArenaFrame,
  framing: ArenaFraming,
): ArenaFrame {
  const width = Math.min(frame.width, full.width);
  const height = width / framing.aspect;
  return {
    x: frameCentre(frame.x, width, framing.mapWidth),
    y: frameCentre(frame.y, height, framing.mapHeight),
    width,
    height,
  };
}

/**
 * One component of a critically damped spring, solved implicitly.
 *
 * Unconditionally stable and monotone: the value never passes the target, which is what
 * distinguishes this from the underdamped spring that would make the arena bounce.
 */
function damp(
  value: number,
  velocity: number,
  target: number,
  dt: number,
): { value: number; velocity: number } {
  const omega = SPRING;
  const f = 1 + 2 * dt * omega;
  const oo = omega * omega;
  const hoo = dt * oo;
  const hhoo = dt * hoo;
  const detInv = 1 / (f + hhoo);
  return {
    value: (value * f + velocity * dt + target * hhoo) * detInv,
    velocity: (velocity + hoo * (target - value)) * detInv,
  };
}
