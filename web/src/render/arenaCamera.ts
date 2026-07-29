import type { ReplayModel, ReplayStableUnitKey } from '../replayModel';
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
 * - **It has a deadband.** A spawn, a death, or a step sideways does not re-aim: the
 *   committed frame is kept until the fitted box actually escapes it. Re-aiming on every
 *   frame is what turns a following camera into a zoom that hunts.
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

/** How much room is left around the fitted lives, in tiles. */
const FOCUS_MARGIN_TILES = 2.6;

/**
 * The closest the camera may ever get, in tiles across.
 *
 * A single surviving bot fits in about two tiles, and a camera that honours that shows a
 * machine and no arena — no cover, no objective, no idea where the shot came from.
 */
const MIN_SPAN_TILES = 8;

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
 * The frame that holds these positions, with room around them.
 *
 * Clamped at both ends: never closer than `minSpan` tiles across, never wider than the
 * whole arena, and never centred somewhere that would push the map off one side while
 * showing empty background on the other.
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
    x: clampCentre((left + right) / 2, width, framing.mapWidth),
    y: clampCentre((top + bottom) / 2, height, framing.mapHeight),
    width,
    height,
  };
}

/** Keep a span of `size` inside the padded map extent, centring it when it does not fit. */
function clampCentre(centre: number, size: number, extent: number): number {
  const pad = ARENA_MARGIN_TILES / 2;
  const low = -pad + size / 2;
  const high = extent + pad - size / 2;
  if (low >= high) return extent / 2;
  return Math.min(Math.max(centre, low), high);
}

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
      ? active
      : active.filter((pose) => pose.teamId === teamId);
  // A tile's centre, which is where both renderers draw the body standing on it.
  return (chosen.length > 0 ? chosen : active).map((pose) => ({
    x: pose.x + 0.5,
    y: pose.y + 0.5,
  }));
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

  /** A gesture. Drops auto-fit until something re-engages it. */
  pan(dx: number, dy: number, framing: ArenaFraming): void {
    this.release();
    this.target = {
      ...this.target,
      x: clampCentre(this.target.x + dx, this.target.width, framing.mapWidth),
      y: clampCentre(this.target.y + dy, this.target.height, framing.mapHeight),
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
      x: clampCentre(this.target.x, width, framing.mapWidth),
      y: clampCentre(this.target.y, width / framing.aspect, framing.mapHeight),
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

/** A frame at a new aspect ratio: same centre, same span, still inside the arena. */
function shaped(
  frame: ArenaFrame,
  full: ArenaFrame,
  framing: ArenaFraming,
): ArenaFrame {
  const width = Math.min(frame.width, full.width);
  const height = width / framing.aspect;
  return {
    x: clampCentre(frame.x, width, framing.mapWidth),
    y: clampCentre(frame.y, height, framing.mapHeight),
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
