import type {
  ReplayActorIdentity,
  ReplayModel,
  ReplayPosition,
  ReplayProjectileHeading,
} from '../replayModel';
import { boltsAt, headingStep } from './interpolate';

/**
 * A volley, recovered from the replay and drawn as one thing.
 *
 * The volley gun launches three ordinary projectiles. Nothing in the document says they
 * are one shot — there is no volley ID, no group handle, and the viewer must not invent a
 * rule the engine does not guarantee. What the engine *does* guarantee, and what the
 * contract states outright in `attackProfiles[].volley.identityOrder`, is
 * `contiguous-ascending-in-launch-order`: one actor, one tick, projectile IDs `n`,
 * `n+1`, … `n+k`, assigned leftmost lane first. Those three facts identify a volley
 * exactly, and any one of them missing means this is not a volley and the bolts are drawn
 * as bolts.
 *
 * So the grouping here is a *recognizer*, not a heuristic: same owner life, same launch
 * tick, at least two members, IDs contiguous with no gap. A ruleset that fires two
 * unrelated shots from one actor on one tick with adjacent IDs would be grouped, and that
 * is the honest cost of not putting a group ID in the replay — it is also, by the same
 * contract, not something any shipped or experimental profile does.
 *
 * The identities are decimal *text* (replay-v2 can exceed the safe integer range), so
 * contiguity is checked in BigInt rather than by parsing to a number.
 */

/** How long, in ticks, a segment that broke off stays on screen. */
const BREAK_SPAN = 0.85;

/**
 * When a bolt that never left the muzzle is considered gone.
 *
 * A volley fired into a wall at point-blank produces members whose authoritative
 * traversal path is *empty*: launched, contacted, consumed, all inside the launch tick.
 * `boltsAt` skips those outright — correctly, since there is no substep to interpolate —
 * and without this the fan would simply be missing two of its three blades on exactly the
 * launch that shows the volley's spread best. So they are held at the muzzle for a slice
 * of their launch tick and then break, which is what the engine says happened.
 */
const MUZZLE_DEATH = 0.28;

export interface VolleyLane {
  volleyId: string;
  laneIndex: number;
  laneCount: number;
}

export interface VolleyMember {
  id: string;
  laneIndex: number;
  /** Tile coordinates. */
  x: number;
  y: number;
  heading: ReplayProjectileHeading;
  /** 0 → 1 while a segment that broke off scatters; null while it is still flying. */
  breakAge: number | null;
  /** Why it broke, when it broke. A shell eats a bolt; a wall shatters one. */
  breakKind: 'absorbed' | 'spent' | null;
}

export interface VolleyPose {
  volleyId: string;
  ownerActor: ReplayActorIdentity;
  laneCount: number;
  launchTick: number;
  /**
   * Runs of *adjacent* lanes still in flight, leftmost lane first.
   *
   * More than one run means the arrow has been cut: a middle blade struck something and
   * the survivors either side keep going as separate pieces, which is exactly the picture
   * the rule produces and the one a viewer has to be able to read.
   */
  runs: VolleyMember[][];
  /** Segments breaking off right now. */
  broken: VolleyMember[];
}

interface VolleyTrack {
  id: string;
  volleyId: string;
  laneIndex: number;
  laneCount: number;
  ownerActor: ReplayActorIdentity;
  launchTick: number;
  launchHeading: ReplayProjectileHeading;
  /** Continuous playhead time at which this member stops existing, or null. */
  deathTime: number | null;
  deathAt: ReplayPosition | null;
  deathHeading: ReplayProjectileHeading | null;
  deathKind: 'absorbed' | 'spent';
  /** True when it was consumed on its launch tick without entering a tile. */
  diedAtMuzzle: boolean;
}

interface VolleyIndex {
  lanes: ReadonlyMap<string, VolleyLane>;
  tracks: ReadonlyMap<string, VolleyTrack>;
  volleys: readonly {
    volleyId: string;
    ownerActor: ReplayActorIdentity;
    launchTick: number;
    members: VolleyTrack[];
  }[];
}

const indexCache = new WeakMap<ReplayModel, VolleyIndex>();

const EMPTY_INDEX: VolleyIndex = {
  lanes: new Map(),
  tracks: new Map(),
  volleys: [],
};

/**
 * Which volley lane a projectile belongs to, if any.
 *
 * Both renderers ask this before drawing an ordinary bolt: a volley member is drawn as
 * part of its arrow and must not also appear as a projectile, or the "not projectiles per
 * se" glyph would be a glyph with three bolts sitting inside it.
 */
export function volleyLanes(
  replay: ReplayModel,
): ReadonlyMap<string, VolleyLane> {
  return volleyIndex(replay).lanes;
}

function decimal(value: string): bigint | null {
  try {
    return BigInt(value);
  } catch {
    return null;
  }
}

function volleyIndex(replay: ReplayModel): VolleyIndex {
  const cached = indexCache.get(replay);
  if (cached !== undefined) return cached;

  // Launch tick + owner life, from the authoritative attack event. Replay-v3 names the
  // event `attack`; replay-v1/v2 name the same thing `shot`, and neither of those
  // rulesets has a volley profile — accepting both costs one comparison and means this
  // does not silently stop recognizing anything when the naming is unified.
  const launches = new Map<
    string,
    {
      ownerActor: ReplayActorIdentity;
      launchTick: number;
      shots: { id: string; value: bigint; heading: ReplayProjectileHeading }[];
    }
  >();
  for (const tick of replay.ticks) {
    for (const event of tick.events) {
      if (event.type !== 'attack' && event.type !== 'shot') continue;
      const owner = event.sourceActor;
      const id = event.projectileId;
      if (!owner || id === null) continue;
      const value = decimal(id);
      if (value === null) continue;
      const key = `${owner.actorKey}@${tick.tick}`;
      const entry = launches.get(key) ?? {
        ownerActor: owner,
        launchTick: tick.tick,
        shots: [],
      };
      entry.shots.push({
        id,
        value,
        heading: event.projectileHeading ?? 'east',
      });
      launches.set(key, entry);
    }
  }

  const lanes = new Map<string, VolleyLane>();
  const tracks = new Map<string, VolleyTrack>();
  const volleys: {
    volleyId: string;
    ownerActor: ReplayActorIdentity;
    launchTick: number;
    members: VolleyTrack[];
  }[] = [];
  for (const [volleyId, entry] of launches) {
    if (entry.shots.length < 2) continue;
    const ordered = [...entry.shots].sort((left, right) =>
      left.value < right.value ? -1 : left.value > right.value ? 1 : 0,
    );
    const contiguous = ordered.every(
      (shot, index) =>
        index === 0 || shot.value === ordered[index - 1].value + 1n,
    );
    if (!contiguous) continue;
    const members = ordered.map<VolleyTrack>((shot, laneIndex) => ({
      id: shot.id,
      volleyId,
      laneIndex,
      laneCount: ordered.length,
      ownerActor: entry.ownerActor,
      launchTick: entry.launchTick,
      launchHeading: shot.heading,
      deathTime: null,
      deathAt: null,
      deathHeading: null,
      deathKind: 'spent',
      diedAtMuzzle: false,
    }));
    for (const member of members) {
      lanes.set(member.id, {
        volleyId,
        laneIndex: member.laneIndex,
        laneCount: member.laneCount,
      });
      tracks.set(member.id, member);
    }
    volleys.push({
      volleyId,
      ownerActor: entry.ownerActor,
      launchTick: entry.launchTick,
      members,
    });
  }
  if (tracks.size === 0) {
    indexCache.set(replay, EMPTY_INDEX);
    return EMPTY_INDEX;
  }

  // Where and when each member stopped existing. Derived from authoritative state — the
  // last tile a traversal entered, and the first tick that state no longer carries it —
  // never accumulated while drawing, so scrubbing backwards produces the same picture.
  const absorbed = new Set<string>();
  for (const [index, tick] of replay.ticks.entries()) {
    const surviving = new Set(
      (tick.after.projectiles ?? []).map(
        (projectile) => projectile.projectileId,
      ),
    );
    const touched = new Map<
      string,
      { at: ReplayPosition; heading: ReplayProjectileHeading; moved: boolean }
    >();
    for (const projectile of tick.before.projectiles ?? []) {
      if (!tracks.has(projectile.projectileId)) continue;
      touched.set(projectile.projectileId, {
        at: projectile.position,
        heading: projectile.heading ?? projectile.launchDirection,
        moved: true,
      });
    }
    for (const traversal of tick.projectileTraversals) {
      if (!tracks.has(traversal.projectileId)) continue;
      const last = traversal.path[traversal.path.length - 1] ?? traversal.from;
      touched.set(traversal.projectileId, {
        at: last,
        heading:
          traversal.finalHeading ??
          traversal.heading ??
          traversal.launchDirection,
        moved:
          traversal.path.length > 0 ||
          (touched.get(traversal.projectileId)?.moved ?? false),
      });
    }
    for (const event of tick.events) {
      if (event.type !== 'projectile-absorbed') continue;
      if (event.projectileId !== null && tracks.has(event.projectileId))
        absorbed.add(event.projectileId);
    }
    for (const [id, state] of touched) {
      const track = tracks.get(id)!;
      if (track.deathTime !== null) continue;
      if (surviving.has(id)) continue;
      track.diedAtMuzzle = !state.moved;
      track.deathTime = state.moved ? index + 1 : index + MUZZLE_DEATH;
      track.deathAt = state.at;
      track.deathHeading = state.heading;
      track.deathKind = absorbed.has(id) ? 'absorbed' : 'spent';
    }
  }

  const built: VolleyIndex = { lanes, tracks, volleys };
  indexCache.set(replay, built);
  return built;
}

/**
 * Every volley on screen at continuous playhead `time`, with its live and breaking parts.
 *
 * Positions come from `boltsAt`, the same authoritative substep interpolation both
 * renderers already use for ordinary bolts, so an arrow is exactly where its bolts are.
 */
export function volleysAt(
  replay: ReplayModel,
  time: number,
): VolleyPose[] {
  const index = volleyIndex(replay);
  if (index.volleys.length === 0) return [];
  const live = new Map(
    boltsAt(replay, time)
      .filter((bolt) => index.tracks.has(bolt.id))
      .map((bolt) => [bolt.id, bolt] as const),
  );

  const poses: VolleyPose[] = [];
  for (const volley of index.volleys) {
    const flying: VolleyMember[] = [];
    const broken: VolleyMember[] = [];
    for (const track of volley.members) {
      const bolt = live.get(track.id);
      if (bolt) {
        flying.push({
          id: track.id,
          laneIndex: track.laneIndex,
          x: bolt.x,
          y: bolt.y,
          heading: bolt.heading,
          breakAge: null,
          breakKind: null,
        });
        continue;
      }
      const death = track.deathTime;
      if (death === null || track.deathAt === null) continue;
      const heading = track.deathHeading ?? track.launchHeading;
      // Held at the muzzle: launched and consumed inside one tick, so it never reached
      // `boltsAt` and would otherwise be a blade the fan never grew.
      if (
        track.diedAtMuzzle &&
        time >= track.launchTick &&
        time < death
      ) {
        flying.push({
          id: track.id,
          laneIndex: track.laneIndex,
          x: track.deathAt.x,
          y: track.deathAt.y,
          heading,
          breakAge: null,
          breakKind: null,
        });
        continue;
      }
      if (time < death || time >= death + BREAK_SPAN) continue;
      broken.push({
        id: track.id,
        laneIndex: track.laneIndex,
        x: track.deathAt.x,
        y: track.deathAt.y,
        heading,
        breakAge: (time - death) / BREAK_SPAN,
        breakKind: track.deathKind,
      });
    }
    if (flying.length === 0 && broken.length === 0) continue;

    flying.sort((left, right) => left.laneIndex - right.laneIndex);
    const runs: VolleyMember[][] = [];
    for (const member of flying) {
      const last = runs[runs.length - 1];
      if (
        last !== undefined &&
        last[last.length - 1].laneIndex === member.laneIndex - 1
      )
        last.push(member);
      else runs.push([member]);
    }
    poses.push({
      volleyId: volley.volleyId,
      ownerActor: volley.ownerActor,
      laneCount: volley.members.length,
      launchTick: volley.launchTick,
      runs,
      broken,
    });
  }
  return poses;
}

export interface ArrowOutline {
  /** Forward edge of the glyph, leftmost lane first. Same length as `trailing`. */
  leading: { x: number; y: number }[];
  /** Rear edge, in the same lane order. */
  trailing: { x: number; y: number }[];
  /** Each lane's own centre, for the bright nodes that keep the count legible. */
  nodes: { x: number; y: number; nx: number; ny: number }[];
}

/**
 * The outline of one connected run, in tile coordinates.
 *
 * A blade's forward point is pushed along *its own* heading rather than the run's, and
 * that is the whole shape: a symmetric fan's outer lanes point away from the middle, so
 * pushing each one forward along its own line bows the leading edge into a crescent with
 * the tips swept back. Nothing here knows it is drawing a crescent — the spread the
 * engine fired produces it.
 *
 * A single surviving blade is widened perpendicular into two virtual lanes so the same
 * strip covers it, which is why every consumer can assume at least two points.
 */
export function volleyArrowOutline(
  run: readonly VolleyMember[],
  reach: number,
  tail: number,
  soloWidth = 0.16,
): ArrowOutline {
  const lanes = run.map((member) => {
    const [sx, sy] = headingStep[member.heading];
    const length = Math.hypot(sx, sy) || 1;
    return { member, nx: sx / length, ny: sy / length };
  });
  const spread =
    lanes.length === 1
      ? [
          { ...lanes[0], offX: -lanes[0].ny * soloWidth, offY: lanes[0].nx * soloWidth },
          { ...lanes[0], offX: lanes[0].ny * soloWidth, offY: -lanes[0].nx * soloWidth },
        ]
      : lanes.map((lane) => ({ ...lane, offX: 0, offY: 0 }));

  return {
    leading: spread.map((lane) => ({
      x: lane.member.x + lane.offX + lane.nx * reach,
      y: lane.member.y + lane.offY + lane.ny * reach,
    })),
    trailing: spread.map((lane) => ({
      x: lane.member.x + lane.offX - lane.nx * tail,
      y: lane.member.y + lane.offY - lane.ny * tail,
    })),
    nodes: lanes.map((lane) => ({
      x: lane.member.x,
      y: lane.member.y,
      nx: lane.nx,
      ny: lane.ny,
    })),
  };
}
