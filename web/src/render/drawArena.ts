import { SCRAP_ACCENT } from '../presentation/scrapAccent';
import type {
  ReplayActorIdentity,
  ReplayCausalEvent,
  ReplayDirection,
  ReplayModel,
  ReplayProjectileHeading,
  ReplayStableUnitKey,
} from '../replayModel';
import { isAttackEvent, isDestructionEvent } from '../replayModel';
import {
  arenaTheme,
  presentationProjectileLook,
  teamAccentedBotImage,
  type ProjectileLook,
} from './arenaThemes';
import {
  stanceKindForForm,
  unitAccent,
  unitLook,
  unitProjectileLook,
  type StanceKind,
} from './unitPresentation';
import {
  volleyArrowOutline,
  volleyLanes,
  volleysAt,
  type VolleyMember,
} from './volley';
import {
  adjustAccentForLuminance,
  sampleCanvasLuminance,
} from './adaptiveAccent';
import { maxHealthForActor } from '../replayMetadata';
import {
  participantForUnit,
  visualIndexForUnit,
} from '../replayParticipants';
import {
  arrivalsAt,
  boltsAt,
  posesAt,
  type Arrival,
  type BotPose,
} from './interpolate';
import { roleTagCaption, roleTagColor } from '../presentation/roleTag';
import { arenaViewport, type ArenaFrame } from './arenaCamera';
import { frontlineCaptureVisual } from './frontlineCaptureVisual';
import { wallAtlasDestination } from './wallAtlasGeometry';
import { WallLayout } from './wallTopology';
import { drawFogMask } from './fogMask';
import { drawLightSpill, type LightKind, type LightSource } from './lightSpill';
import {
  createPresenter,
  type ReplayPresenter,
} from '../replayPresentation';
import {
  drawArcRelayGround,
  drawArcRelayOverlay,
} from './arcRelayVisual';
import {
  logicalArenaHeight,
  WORLD_VERTICAL_SCALE,
} from './arenaProjection';
import {
  teamVisionAt,
  teamVisionSeesActor,
  teamVisionSeesProjectile,
} from './teamVision';

const directionStep: Record<ReplayDirection, [number, number]> = {
  north: [0, -1],
  east: [1, 0],
  south: [0, 1],
  west: [-1, 0],
};

const directionAngle: Record<ReplayDirection, number> = {
  north: -Math.PI / 2,
  east: 0,
  south: Math.PI / 2,
  west: Math.PI,
};

const projectileStep: Record<ReplayProjectileHeading, [number, number]> = {
  ...directionStep,
  'north-east': [1, -1],
  'south-east': [1, 1],
  'south-west': [-1, 1],
  'north-west': [-1, -1],
};

const projectileAngle: Record<ReplayProjectileHeading, number> = {
  ...directionAngle,
  'north-east': -Math.PI / 4,
  'south-east': Math.PI / 4,
  'south-west': (3 * Math.PI) / 4,
  'north-west': (-3 * Math.PI) / 4,
};

const tintedProjectileSprites = new Map<string, HTMLCanvasElement>();
const maxTintedProjectileSprites = 32;
const canvasPresenters = new WeakMap<ReplayModel, ReplayPresenter>();

function tintedProjectileSprite(
  look: ProjectileLook,
  accent: string,
): HTMLCanvasElement | null {
  if (
    typeof document === 'undefined' ||
    !look.image?.complete ||
    look.image.naturalWidth <= 0
  )
    return null;
  const key = `${look.id}:${accent}`;
  const cached = tintedProjectileSprites.get(key);
  if (cached) {
    // Refresh insertion order so the bounded cache behaves as a tiny LRU.
    tintedProjectileSprites.delete(key);
    tintedProjectileSprites.set(key, cached);
    return cached;
  }

  const canvas = document.createElement('canvas');
  canvas.width = 256;
  canvas.height = 256;
  const sprite = canvas.getContext('2d');
  if (!sprite) return null;
  sprite.drawImage(look.image, 0, 0, canvas.width, canvas.height);
  sprite.globalCompositeOperation = 'source-in';
  sprite.fillStyle = accent;
  sprite.fillRect(0, 0, canvas.width, canvas.height);
  tintedProjectileSprites.set(key, canvas);
  if (tintedProjectileSprites.size > maxTintedProjectileSprites) {
    const oldest = tintedProjectileSprites.keys().next().value;
    if (oldest !== undefined) tintedProjectileSprites.delete(oldest);
  }
  return canvas;
}

export interface DrawOptions {
  time: number;
  selectedUnitKey: ReplayStableUnitKey | null;
  showVisibility: boolean;
  /**
   * Where the camera is looking, in tiles. Absent means the whole arena, framed the way
   * this renderer always framed it — which is what every golden frame is recorded at, and
   * what a caller that does not want a moving camera gets by saying nothing.
   */
  frame?: ArenaFrame | null;
  entrants?: readonly { teamId: number; crest: import('../components/EntrantCrest').CrestPresentation }[];
}

/** Pure canvas renderer: consumes replay data, never computes game rules (plan §32). */
export function drawArena(
  ctx: CanvasRenderingContext2D,
  replay: ReplayModel,
  { time, selectedUnitKey, showVisibility, frame = null, entrants = [] }: DrawOptions,
  width: number,
  height: number,
): void {
  const {
    width: mapWidth,
    height: mapHeight,
    tileRows: mapTiles,
  } = replay.map;
  // The tile size and origin the camera asks for. `arenaViewport` also owns the whole-map
  // fallback and its margin, so `ArenaCanvas`'s hit-test converts a click back to a tile
  // through the same arithmetic — the two used to state it separately, with a comment on
  // each asking the other not to drift.
  const { tile, originX, originY } = arenaViewport(
    frame,
    mapWidth,
    mapHeight,
    width,
    logicalArenaHeight(height),
  );
  const px = (x: number) => originX + x * tile;
  const py = (y: number) => originY + y * tile;

  /**
   * How far a wall's top is displaced per tile of distance from the arena centre.
   *
   * The whole of the flat renderer's wall effect, and it is deliberately tiny: at 0.012 a wall in the
   * far corner of a 24x18 map moves about a sixth of a tile. Enough to read as height,
   * small enough that the grid still looks like a grid — and the tile a bot occupies must
   * remain unambiguous, because players reason about cover in tile coordinates.
   */
  const WALL_LIFT = 0.012;

  /**
   * Where a wall tile's *top* is drawn, given where its base sits.
   *
   * Outward from the centre, not toward it. A camera above the middle of the arena sees
   * the top of a wall as nearer than its base, so it projects further from the centre of
   * frame — which also means the centre of the map has no displacement at all, and that is
   * correct rather than a special case.
   */
  const liftX = (x: number) => (x + 0.5 - mapWidth / 2) * tile * WALL_LIFT;
  const liftY = (y: number) => (y + 0.5 - mapHeight / 2) * tile * WALL_LIFT;
  const theme = arenaTheme(replay.map.presentation?.themeId ?? undefined);

  ctx.clearRect(0, 0, width, height);
  ctx.fillStyle = theme.palette.canvas;
  ctx.fillRect(0, 0, width, height);

  const tickCount = replay.ticks.length;
  const tick =
    tickCount === 0
      ? 0
      : Math.min(Math.floor(Math.max(0, time)), tickCount - 1);
  const fraction =
    tickCount === 0 ? 0 : Math.max(0, Math.min(time - tick, 1));
  const currentTick = replay.ticks[tick];
  let presenter = canvasPresenters.get(replay);
  if (!presenter) {
    presenter = createPresenter(replay);
    canvasPresenters.set(replay, presenter);
  }
  const tickPresentation =
    tickCount === 0 ? null : presenter.at(tick);
  const captureVisual =
    tickPresentation === null
      ? null
      : frontlineCaptureVisual(tickPresentation);
  const poses = posesAt(replay, time);
  const previousPoseByActor = new Map(
    tick > 0
      ? posesAt(replay, tick - 0.001).map((pose) => [pose.actorKey, pose] as const)
      : [],
  );
  // Lives that materialized at the start of this tick, by the life they belong to, so the
  // body pass can condense the chassis and the effect pass can ring it without either
  // asking the model the same question twice.
  const arrivals = new Map<string, Arrival>(
    arrivalsAt(replay, time).map((arrival) => [arrival.actorKey, arrival]),
  );
  const boundaryWall = validWallFamily(
    replay.map.presentation?.boundaryWall ?? undefined,
    theme.walls.defaults.boundary,
  );
  const interiorWall = validWallFamily(
    replay.map.presentation?.interiorWall ?? undefined,
    theme.walls.defaults.interior,
  );
  const wallLayout = new WallLayout(
    replay,
    boundaryWall,
    interiorWall,
    (family) => validWallFamily(family, interiorWall),
  );
  // Look and accent both come from `unitPresentation`, which is also what the 2.5D
  // renderer and the bot panel ask — a class form with no authored art, or two teams that
  // submitted the same accent, must not be resolved differently depending on which
  // renderer happens to be running.
  const accentFor = (unitKey: ReplayStableUnitKey | null): string =>
    unitKey === null ? '#ffffff' : unitAccent(replay, unitKey);
  const accentAt = (accent: string, x: number, y: number): string => {
    const background = sampleCanvasLuminance(ctx, x, y, width, height);
    return background === null
      ? accent
      : adjustAccentForLuminance(accent, background);
  };

  // Selection chooses a team perspective. Unioning the team's recorded observations
  // keeps fog replay-honest while matching the player's shared-vision expectation.
  const teamVision = teamVisionAt(
    replay,
    currentTick,
    selectedUnitKey,
    showVisibility,
  );
  // Hearing remains attached to the selected observer: unlike tile visibility, a bearing
  // is relative to one body and cannot be merged into a single team-space arc.
  const hearingSource =
    teamVision !== null && selectedUnitKey !== null
      ? currentTick?.actorTurns.find(
          (turn) => turn.actor.unitKey === selectedUnitKey,
        )
      : undefined;
  const hiddenByFog = (pose: BotPose): boolean =>
    !teamVisionSeesActor(teamVision, pose);

  // FOV mode stays honest: bolts the selected team can't see aren't drawn at all
  // (an unseen bolt is precisely the threat it doesn't know about). Derived here rather
  // than inside the projectile pass because a volley's arrow has to obey exactly the same
  // rule its bolts do, and two copies of this would eventually stop agreeing.
  const boltHidden = (
    projectileId: string,
    x: number,
    y: number,
  ): boolean =>
    !teamVisionSeesProjectile(teamVision, projectileId, x, y);

  // A knock on impact, decaying across the tick it happened in.
  //
  // Derived from the tick rather than accumulated in a variable: this renderer is called
  // fresh every frame with a time, and any state kept between calls would make the same
  // moment of the same replay render differently depending on how it was reached —
  // scrubbing backwards, or a golden-frame test drawing one tick in isolation.
  //
  // Destruction shakes harder than a hit, and nothing else shakes at all. A camera that
  // moves on every shot stops meaning anything.
  const shake = shakeOffset();
  ctx.save();
  // A single owner-ruled projection keeps tile arithmetic, fog, routes and event effects
  // in one coordinate system. The inverse is used by the canvas hit-test; nothing here
  // changes an authoritative position.
  ctx.scale(1, WORLD_VERTICAL_SCALE);
  if (shake) ctx.translate(shake.x, shake.y);

  drawFloor();
  drawZone();
  drawVision();
  drawWalls();
  const arcRelayVisual = {
    ctx,
    replay,
    tick: currentTick,
    time,
    fraction,
    tile,
    mapWidth,
    mapHeight,
    px,
    py,
    poses,
    accentFor,
    entrants,
  };
  drawArcRelayGround(arcRelayVisual);
  drawSpill();
  if (teamVision !== null) drawFog(teamVision.visibleTiles);
  drawProjectiles();
  drawVolleys();
  drawHeardSounds();
  // Before the bodies, because it happens on the floor: the 3D renderer puts the same ring
  // on the ground plane, so drawing it over the chassis here would make the flat viewer
  // paint over the machine it is delivering while the other one lights it from below.
  drawArrivals();
  // Loose scrap sits on the floor under the bodies that come to take it.
  drawScrapPiles();
  drawShadowsAndBots();
  drawArcRelayOverlay(arcRelayVisual);
  drawShots();
  drawImpacts();

  ctx.restore();

  function shakeOffset(): { x: number; y: number } | null {
    let strength = 0;
    for (const event of currentTick?.events ?? []) {
      if (isDestructionEvent(event.type)) strength = Math.max(strength, 1);
      else if (event.type === 'damage')
        strength = Math.max(strength, 0.45);
    }
    if (strength === 0) return null;

    // Impacts land late in the tick — the same 0.6 the flash uses — so the shake starts
    // when the hit is seen rather than when the tick begins.
    const since = (fraction - 0.6) / 0.4;
    if (since < 0 || since > 1) return null;

    const decay = (1 - since) ** 2;
    const amplitude = tile * 0.05 * strength * decay;
    // Two incommensurate frequencies so it reads as a knock rather than a wobble.
    return {
      x: Math.sin(since * Math.PI * 7.3) * amplitude,
      y: Math.cos(since * Math.PI * 5.1) * amplitude * 0.7,
    };
  }

  function drawFloor(): void {
    ctx.save();
    ctx.shadowColor = 'rgba(22, 119, 174, 0.18)';
    ctx.shadowBlur = Math.max(12, tile * 0.7);
    ctx.fillStyle = theme.palette.arena;
    ctx.fillRect(px(0), py(0), tile * mapWidth, tile * mapHeight);
    ctx.restore();

    if (!drawTextureField(theme.floorTexture)) {
      ctx.fillStyle = theme.palette.arena;
      ctx.fillRect(px(0), py(0), tile * mapWidth, tile * mapHeight);
    }
    ctx.fillStyle = theme.palette.floorTint;
    ctx.fillRect(px(0), py(0), tile * mapWidth, tile * mapHeight);
  }

  function drawZone(): void {
    if (replay.map.frontline) {
      drawFrontlinePositions();
      return;
    }
    if (replay.map.objectiveTiles.length === 0) return;
    drawZoneTiles(replay.map.objectiveTiles);
  }

  function drawFrontlinePositions(): void {
    const frontline = replay.map.frontline;
    if (!frontline) return;
    const objective =
      currentTick?.after.objective ?? replay.initialWorld?.objective;
    const activePositionIndex =
      objective?.kind === 'frontline'
        ? objective.activePositionIndex
        : Math.floor(frontline.positions.length / 2);

    ctx.save();
    ctx.lineWidth = Math.max(1, tile * 0.025);
    for (const position of frontline.positions) {
      if (position.positionIndex === activePositionIndex) continue;
      const distance = Math.abs(
        position.positionIndex - activePositionIndex,
      );
      ctx.fillStyle = hexWithAlpha(theme.palette.zone, 0.025);
      ctx.strokeStyle = hexWithAlpha(
        theme.palette.zone,
        Math.max(0.12, 0.3 - distance * 0.045),
      );
      for (const point of position.tiles) {
        ctx.fillRect(px(point.x), py(point.y), tile, tile);
        ctx.strokeRect(px(point.x), py(point.y), tile, tile);
      }
    }

    for (const home of frontline.teamHomes) {
      const homeUnit = replay.units.find(
        (unit) => unit.teamId === home.teamId,
      );
      const color = accentFor(homeUnit?.unitKey ?? null);
      ctx.fillStyle = hexWithAlpha(color, 0.04);
      ctx.strokeStyle = hexWithAlpha(color, 0.28);
      for (const point of home.protectedSpawnPad) {
        ctx.fillRect(px(point.x), py(point.y), tile, tile);
        ctx.strokeRect(px(point.x), py(point.y), tile, tile);
      }
    }

    const world = currentTick?.after ?? replay.initialWorld;
    for (const unit of world?.units ?? []) {
      if (!unit.reservedSpawn) continue;
      const color = accentFor(unit.unitKey);
      const centreX = px(unit.reservedSpawn.x) + tile / 2;
      const centreY = py(unit.reservedSpawn.y) + tile / 2;
      const pulse = 0.82 + 0.12 * Math.sin(time * Math.PI * 2);
      ctx.fillStyle = hexWithAlpha(color, 0.12);
      ctx.strokeStyle = hexWithAlpha(color, 0.9);
      ctx.lineWidth = Math.max(2, tile * 0.045);
      ctx.setLineDash([tile * 0.11, tile * 0.07]);
      ctx.beginPath();
      ctx.arc(
        centreX,
        centreY,
        tile * 0.31 * pulse,
        0,
        Math.PI * 2,
      );
      ctx.fill();
      ctx.stroke();
      ctx.setLineDash([]);
    }
    ctx.restore();

    const active = frontline.positions.find(
      (position) => position.positionIndex === activePositionIndex,
    );
    if (active) {
      drawZoneTiles(active.tiles);
      drawFrontlineCaptureState(active.tiles);
    }
  }

  /**
   * Exact Frontline claim/erosion/ratchet state over the theme-owned field.
   *
   * The neutral zone material remains underneath. Team colour is applied only
   * at render time, progress is arc length, an eroder gets a separate moving
   * outer arc without receiving premature filled credit, and a live ratchet
   * gets a whole-footprint owner wash plus a countdown arc.
   */
  function drawFrontlineCaptureState(
    tiles: readonly { x: number; y: number }[],
  ): void {
    if (!captureVisual || tiles.length === 0) return;

    // TickPresentation accents are already contrast-corrected by
    // playerAccent. Sampling and correcting them again here washed dark team
    // oranges toward white, defeating the ownership cue this layer exists to
    // provide.
    const claimAccent = captureVisual.claimantAccent;
    const challengerAccent = captureVisual.challengerAccent;
    const holdAccent = captureVisual.holdAccent;
    const pulse = 0.5 + 0.5 * Math.sin(time * Math.PI * 2.2);

    const shape = new Path2D();
    for (const point of tiles) {
      shape.rect(
        px(point.x) + tile * 0.075,
        py(point.y) + tile * 0.075,
        tile * 0.85,
        tile * 0.85,
      );
    }

    const ownershipAccent = holdAccent ?? claimAccent;
    if (ownershipAccent) {
      const alpha =
        captureVisual.state === 'holding'
          ? 0.16 + pulse * 0.1
          : captureVisual.state === 'contested'
            ? 0.045
            : 0.055 + captureVisual.progressFraction * 0.06;
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.fillStyle = hexWithAlpha(ownershipAccent, alpha);
      ctx.fill(shape);
      ctx.restore();
    }
    if (captureVisual.contested) {
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.fillStyle = hexWithAlpha(
        '#f4c477',
        0.055 + pulse * 0.045,
      );
      ctx.fill(shape);
      ctx.restore();
    }

    // A solid exterior in the hold owner's colour is the at-a-glance
    // ownership cue. Erosion instead puts the challenger on the exterior while
    // the incumbent keeps the stored-progress arc.
    const boundaryAccent =
      captureVisual.state === 'holding'
        ? holdAccent
        : captureVisual.contested
          ? '#f4c477'
          : captureVisual.state === 'eroding'
            ? challengerAccent
            : claimAccent;
    if (boundaryAccent) {
      const occupied = new Set(
        tiles.map((point) => `${point.x},${point.y}`),
      );
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.strokeStyle = hexWithAlpha(
        boundaryAccent,
        captureVisual.state === 'holding' ? 0.95 : 0.72,
      );
      ctx.shadowColor = boundaryAccent;
      ctx.shadowBlur = Math.max(4, tile * 0.14);
      ctx.lineWidth = Math.max(
        2,
        tile * (captureVisual.state === 'holding' ? 0.055 : 0.04),
      );
      const edge = (
        fromX: number,
        fromY: number,
        toX: number,
        toY: number,
      ) => {
        ctx.beginPath();
        ctx.moveTo(fromX, fromY);
        ctx.lineTo(toX, toY);
        ctx.stroke();
      };
      for (const point of tiles) {
        const left = px(point.x) + tile * 0.075;
        const right = px(point.x + 1) - tile * 0.075;
        const top = py(point.y) + tile * 0.075;
        const bottom = py(point.y + 1) - tile * 0.075;
        if (!occupied.has(`${point.x},${point.y - 1}`))
          edge(left, top, right, top);
        if (!occupied.has(`${point.x + 1},${point.y}`))
          edge(right, top, right, bottom);
        if (!occupied.has(`${point.x},${point.y + 1}`))
          edge(left, bottom, right, bottom);
        if (!occupied.has(`${point.x - 1},${point.y}`))
          edge(left, top, left, bottom);
      }
      ctx.restore();
    }

    // The knockback. A channel that lost work draws the length it *had*
    // outside the length it has, hot and flashing for the beat, so the eye
    // reads a gap rather than a bar that quietly got shorter. An erosion draws
    // the same ghost dimly and without the flash — a drain, not a hit.
    const revert = captureVisual.revert;
    if (revert !== null && revert.ghostFraction > 0) {
      const hot = revert.kind === 'interrupt';
      const ghostAccent = hot
        ? '#fff1d0'
        : (captureVisual.revertAccent ?? claimAccent);
      if (ghostAccent) {
        if (hot) {
          ctx.save();
          ctx.globalCompositeOperation = 'lighter';
          ctx.fillStyle = hexWithAlpha(
            '#ffd9a1',
            (0.06 + 0.1 * pulse) * revert.strength,
          );
          ctx.fill(shape);
          ctx.restore();
        }
        for (const point of tiles) {
          drawCaptureArc(
            px(point.x) + tile / 2,
            py(point.y) + tile / 2,
            tile * 0.315,
            revert.ghostFraction,
            ghostAccent,
            tile * (hot ? 0.085 : 0.06),
            -Math.PI / 2,
            revert.strength * (hot ? 0.6 + pulse * 0.4 : 0.3),
          );
        }
      }
    }

    for (const point of tiles) {
      const x = px(point.x) + tile / 2;
      const y = py(point.y) + tile / 2;
      if (claimAccent && captureVisual.progressFraction > 0) {
        drawCaptureArc(
          x,
          y,
          tile * 0.315,
          captureVisual.progressFraction,
          claimAccent,
          tile * 0.07,
          -Math.PI / 2,
          0.92,
        );
      }
      if (
        captureVisual.progressDirection === 'eroding' &&
        challengerAccent
      ) {
        drawCaptureArc(
          x,
          y,
          tile * 0.405,
          0.24,
          challengerAccent,
          tile * 0.05,
          -Math.PI / 2 - time * Math.PI * 0.72,
          0.7 + pulse * 0.26,
        );
      }
      if (captureVisual.state === 'holding' && holdAccent) {
        ctx.save();
        ctx.setLineDash([
          Math.max(3, tile * 0.08),
          Math.max(2, tile * 0.05),
        ]);
        drawCaptureArc(
          x,
          y,
          tile * 0.475,
          captureVisual.holdFraction,
          holdAccent,
          tile * 0.055,
          -Math.PI / 2 + time * Math.PI * 0.18,
          0.74 + pulse * 0.24,
        );
        ctx.restore();
      }
    }
  }

  function drawCaptureArc(
    x: number,
    y: number,
    radius: number,
    fractionOfCircle: number,
    color: string,
    width: number,
    startsAt: number,
    alpha: number,
  ): void {
    if (fractionOfCircle <= 0) return;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.strokeStyle = hexWithAlpha(color, alpha);
    ctx.shadowColor = color;
    ctx.shadowBlur = Math.max(5, tile * 0.18);
    ctx.lineWidth = Math.max(2, width);
    ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.arc(
      x,
      y,
      radius,
      startsAt,
      startsAt + Math.PI * 2 * Math.min(1, fractionOfCircle),
    );
    ctx.stroke();
    ctx.restore();
  }

  function drawZoneTiles(
    tiles: readonly { x: number; y: number }[],
  ): void {
    const pulse = 0.88 + Math.sin(time * Math.PI * 2) * 0.12;
    const zoneTiles = new Set(
      tiles.map(({ x, y }) => `${x},${y}`),
    );
    const zoneShape = new Path2D();
    for (const { x, y } of tiles)
      zoneShape.rect(px(x), py(y), tile, tile);

    if (theme.zoneTexture?.complete && theme.zoneTexture.naturalWidth > 0) {
      // World-space UVs: changing the zone mask reveals or hides material;
      // it never rescales the artwork to fit that particular zone.
      const materialSize = tile * theme.zoneTextureScale;
      ctx.save();
      ctx.clip(zoneShape);
      for (let y = py(0); y < py(mapHeight); y += materialSize) {
        for (let x = px(0); x < px(mapWidth); x += materialSize) {
          ctx.drawImage(
            theme.zoneTexture,
            x,
            y,
            materialSize,
            materialSize,
          );
        }
      }
      ctx.restore();
    }

    ctx.save();
    ctx.shadowColor = theme.palette.zone;
    ctx.shadowBlur = Math.max(5, tile * 0.16);
    ctx.fillStyle = hexWithAlpha(
      theme.palette.zone,
      (theme.zoneTexture ? 0.07 : 0.15) * pulse,
    );
    ctx.fill(zoneShape);
    ctx.restore();

    ctx.save();
    ctx.strokeStyle = hexWithAlpha(theme.palette.zone, 0.72 * pulse);
    ctx.lineWidth = Math.max(1.5, tile * 0.045);
    ctx.lineCap = 'square';
    ctx.setLineDash([Math.max(5, tile * 0.18), Math.max(3, tile * 0.11)]);
    const strokeEdge = (
      fromX: number,
      fromY: number,
      toX: number,
      toY: number,
    ) => {
      ctx.beginPath();
      ctx.moveTo(fromX, fromY);
      ctx.lineTo(toX, toY);
      ctx.stroke();
    };
    for (const { x, y } of tiles) {
      const left = px(x);
      const top = py(y);
      if (!zoneTiles.has(`${x},${y - 1}`))
        strokeEdge(left, top, left + tile, top);
      if (!zoneTiles.has(`${x + 1},${y}`))
        strokeEdge(left + tile, top, left + tile, top + tile);
      if (!zoneTiles.has(`${x},${y + 1}`))
        strokeEdge(left, top + tile, left + tile, top + tile);
      if (!zoneTiles.has(`${x - 1},${y}`))
        strokeEdge(left, top, left, top + tile);
    }
    ctx.restore();
  }

  /**
   * Light thrown onto the arena by this tick's flashes, and the tail of the previous
   * tick's — a muzzle flash that vanished on the tick boundary would strobe.
   */
  function drawSpill(): void {
    const sources: LightSource[] = [];
    // Through the same resolution as the bots themselves: light thrown by a shot is that
    // team's light, and reading the participant accent straight made it white-on-white
    // once two teams submitted the same colour.
    const eventAccent = (event: ReplayCausalEvent) =>
      accentFor((event.sourceActor ?? event.targetActor)?.unitKey ?? null);

    const collect = (index: number, age: number) => {
      const at = replay.ticks[index];
      if (!at) return;
      for (const event of at.events) {
        const kind: LightKind | null =
          isAttackEvent(event.type)
            ? 'shot'
            : event.type === 'damage'
              ? 'impact'
              : isDestructionEvent(event.type)
                ? 'destroyed'
                : null;
        if (!kind) continue;
        // A shot's origin is `from`; a generation-3 impact or destruction
        // carries its one position as `to`. Reading only `from` left every v3
        // hit unlit.
        const at = event.from ?? (kind === 'shot' ? null : event.to);
        if (!at) continue;
        sources.push({
          kind,
          x: at.x,
          y: at.y,
          age,
          color: eventAccent(event),
        });
      }
    };

    collect(tick, fraction);
    collect(tick - 1, 1 + fraction);

    drawLightSpill(ctx, sources, { px, py, tile });
  }

  function drawFog(visible: ReadonlySet<string>): void {
    // Show the selected team's field of view by FOGGING what it can NOT see.
    // Vision range 6 spans most of a small map, so tinting the visible tiles
    // read as "everything highlighted"; darkening the blind area reads at any size.
    // Walls overhang their tile by a gutter, so a visible wall is cleared at its drawn
    // extent — otherwise the tile grid cuts the sprite in half.
    const { contentPixels, gutterPixels } = theme.walls.atlas;
    const { destinationGutter } = wallAtlasDestination(tile, contentPixels, gutterPixels);

    drawFogMask(
      ctx,
      { px, py, tile, wallGutter: destinationGutter },
      {
        mapWidth,
        mapHeight,
        visible,
        isWall: (x, y) => mapTiles[y][x] === '#',
      },
    );
  }

  function drawWalls(): void {
    const usedFamilies = new Set<string>();
    for (let y = 0; y < mapHeight; y++) {
      for (let x = 0; x < mapWidth; x++)
        if (mapTiles[y][x] === '#') usedFamilies.add(wallFamilyAt(x, y)!);
    }

    // The cast shadow stays on the floor while everything above it lifts. That gap is
    // what the eye reads as height; moving the shadow with the wall would just slide the
    // whole thing sideways and read as a misalignment.
    for (const familyId of usedFamilies) {
      const family = theme.walls.families.get(familyId);
      if (family?.shadowAtlasTexture)
        drawWallAtlasLayer(familyId, family.shadowAtlasTexture, false);
    }

    // The face: the side of the wall the lift exposes. Drawn at the base position in near
    // black, so whatever sliver the displaced top does not cover reads as a wall side in
    // shadow rather than as a hole in the floor.
    for (const familyId of usedFamilies) {
      ctx.save();
      ctx.clip(wallFamilyShape(familyId));
      ctx.fillStyle = 'rgba(3, 6, 10, 0.92)';
      ctx.fillRect(px(0), py(0), tile * mapWidth, tile * mapHeight);
      ctx.restore();
    }

    for (const familyId of usedFamilies) {
      const family = theme.walls.families.get(familyId);
      ctx.save();
      ctx.clip(wallFamilyShape(familyId, true));
      if (!drawTextureField(family?.materialTexture ?? null)) {
        ctx.fillStyle = '#2e3d55';
        ctx.fillRect(px(0), py(0), tile * mapWidth, tile * mapHeight);
      }
      ctx.fillStyle = theme.palette.wallTint;
      ctx.fillRect(px(0), py(0), tile * mapWidth, tile * mapHeight);
      ctx.restore();
    }

    for (const familyId of usedFamilies) {
      const family = theme.walls.families.get(familyId);
      if (family?.edgeAtlasTexture)
        drawWallAtlasLayer(familyId, family.edgeAtlasTexture, true);
    }
  }

  function drawWallAtlasLayer(
    familyId: string,
    image: HTMLImageElement,
    /** Whether this layer is part of the wall's top, which is displaced, or its base. */
    lifted: boolean,
  ): void {
    if (!image.complete || image.naturalWidth === 0) return;
    const { columns, contentPixels, gutterPixels } = theme.walls.atlas;
    const sourceTile = image.naturalWidth / columns;
    const { destinationTile, destinationGutter } = wallAtlasDestination(
      tile,
      contentPixels,
      gutterPixels,
    );

    // The mask selects a fully baked topology sprite. The canvas contributes
    // placement only; edges, corners, hardware, relief, and shadow live in art.
    for (let y = 0; y < mapHeight; y++) {
      for (let x = 0; x < mapWidth; x++) {
        if (wallFamilyAt(x, y) !== familyId) continue;
        const mask = wallLayout.maskAt(x, y, familyId);
        ctx.drawImage(
          image,
          (mask % columns) * sourceTile,
          Math.floor(mask / columns) * sourceTile,
          sourceTile,
          sourceTile,
          px(x) - destinationGutter + (lifted ? liftX(x) : 0),
          py(y) - destinationGutter + (lifted ? liftY(y) : 0),
          destinationTile,
          destinationTile,
        );
      }
    }
  }

  function wallFamilyShape(familyId: string, lifted = false): Path2D {
    const shape = new Path2D();
    for (let y = 0; y < mapHeight; y++)
      for (let x = 0; x < mapWidth; x++)
        if (wallFamilyAt(x, y) === familyId)
          shape.rect(
            px(x) + (lifted ? liftX(x) : 0),
            py(y) + (lifted ? liftY(y) : 0),
            tile,
            tile,
          );
    return shape;
  }

  function wallFamilyAt(x: number, y: number): string | null {
    return wallLayout.familyAt(x, y);
  }

  function validWallFamily(candidate: string | undefined, fallback: string): string {
    return candidate && theme.walls.families.has(candidate) ? candidate : fallback;
  }

  function drawTextureField(image: HTMLImageElement | null): boolean {
    if (!image?.complete || image.naturalWidth === 0) return false;
    // Materials are mapped once over the arena, then geometry masks reveal
    // them. The renderer never slices them into independently bordered cells.
    ctx.drawImage(
      image,
      0,
      0,
      image.naturalWidth,
      image.naturalHeight,
      px(0),
      py(0),
      tile * mapWidth,
      tile * mapHeight,
    );
    return true;
  }

  function drawVision(): void {
    // Directional sight (rules with cone vision): a faint 90° wedge in each active
    // bot's facing direction, in its accent — so "who is looking where, and who is in
    // whose blind arc" reads at a glance. The exact per-tile cone (wall-accurate) is
    // still available by selecting a bot with the field-of-view toggle.
    for (const pose of poses) {
      if (pose.status !== 'active') continue;
      const form = replay.forms.find(
        (candidate) => candidate.formId === pose.formId,
      );
      if (!form) continue;
      // Legacy omnidirectional replays did not draw a range halo. Preserve
      // their pixels while making new explicit 360-degree forms readable.
      if (form.omnidirectionalVision && replay.sourceVersion === 1) continue;
      const radius = form.visionRange * tile;
      const accent = accentFor(pose.unitKey);
      const cx = px(pose.x) + tile / 2;
      const cy = py(pose.y) + tile / 2;
      const gradient = ctx.createRadialGradient(cx, cy, tile * 0.4, cx, cy, radius);
      gradient.addColorStop(0, hexWithAlpha(accent, 0.08));
      gradient.addColorStop(1, hexWithAlpha(accent, 0));
      ctx.fillStyle = gradient;
      ctx.beginPath();
      if (form.omnidirectionalVision) {
        ctx.arc(cx, cy, radius, 0, Math.PI * 2);
      } else {
        ctx.moveTo(cx, cy);
        ctx.arc(
          cx,
          cy,
          radius,
          pose.angle - Math.PI / 4,
          pose.angle + Math.PI / 4,
        );
        ctx.closePath();
      }
      ctx.fill();
      if (form.omnidirectionalVision) {
        ctx.strokeStyle = hexWithAlpha(accent, 0.14);
        ctx.lineWidth = Math.max(1, tile * 0.025);
        ctx.setLineDash([Math.max(3, tile * 0.12), Math.max(3, tile * 0.12)]);
        ctx.stroke();
        ctx.setLineDash([]);
        continue;
      }
      // The omnidirectional Chebyshev-1 proximity ring: the 8 adjacent tiles are
      // always visible, even directly behind — without this the glyph understates
      // real vision (owner finding).
      ctx.fillStyle = hexWithAlpha(accent, 0.045);
      ctx.beginPath();
      ctx.arc(cx, cy, tile * 1.5, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  function drawProjectiles(): void {
    // Both renderers consume the same normalized authoritative substep interpolation.
    const grouped = volleyLanes(replay);
    const bolts = boltsAt(replay, time).filter(
      (bolt) => !grouped.has(bolt.id),
    );
    if (bolts.length === 0) return;
    // Omniscient spectators see the locked future arc. A selected defender
    // sees only physically manifested segments; the owner authored the plan.
    const programmed = new Map<
      string,
      {
        ownerActor: ReplayActorIdentity;
        path: readonly { x: number; y: number }[];
      }
    >();
    for (const bolt of bolts)
      if (bolt.programmedPath)
        programmed.set(bolt.id, {
          ownerActor: bolt.ownerActor,
          path: bolt.programmedPath,
        });
    for (const plan of programmed.values()) {
      if (
        teamVision !== null &&
        teamVision.teamId !== plan.ownerActor.teamId
      )
        continue;
      const sample = plan.path[Math.floor(plan.path.length / 2)];
      const accent = sample
        ? accentAt(
            accentFor(plan.ownerActor.unitKey),
            px(sample.x) + tile / 2,
            py(sample.y) + tile / 2,
          )
        : accentFor(plan.ownerActor.unitKey);
      ctx.strokeStyle = hexWithAlpha(accent, 0.22);
      ctx.lineWidth = Math.max(1, tile * 0.045);
      ctx.setLineDash([Math.max(2, tile * 0.12), Math.max(2, tile * 0.1)]);
      ctx.beginPath();
      plan.path.forEach((point, index) => {
        const cx = px(point.x) + tile / 2;
        const cy = py(point.y) + tile / 2;
        if (index === 0) ctx.moveTo(cx, cy);
        else ctx.lineTo(cx, cy);
      });
      ctx.stroke();
      ctx.setLineDash([]);
    }

    for (const bolt of bolts)
      drawBolt(
        bolt.x,
        bolt.y,
        bolt.heading,
        bolt.id,
        bolt.ownerActor,
        bolt.imminent,
        bolt.tilesPerAdvance,
      );

    function drawBolt(
      x: number,
      y: number,
      direction: ReplayProjectileHeading,
      projectileId: string,
      ownerActor: ReplayActorIdentity,
      imminent: boolean,
      tilesPerAdvance: number,
    ): void {
      if (boltHidden(projectileId, x, y)) return;
      const cx = px(x) + tile / 2;
      const cy = py(y) + tile / 2;
      const accent = accentAt(accentFor(ownerActor.unitKey), cx, cy);
      const angle = projectileAngle[direction];
      const pulse = 0.75 + 0.25 * Math.sin(fraction * Math.PI);
      if (imminent) {
        const [dx, dy] = projectileStep[direction];
        for (let step = 1; step <= tilesPerAdvance; step++) {
          ctx.fillStyle = hexWithAlpha(accent, step === 1 ? 0.18 : 0.1);
          ctx.fillRect(px(x + dx * step), py(y + dy * step), tile, tile);
        }
      }
      ctx.save();
      ctx.translate(cx, cy);
      ctx.rotate(angle);
      ctx.globalCompositeOperation = 'lighter';
      ctx.shadowColor = accent;
      ctx.shadowBlur = Math.max(5, tile * 0.22);
      ctx.lineCap = 'round';
      ctx.strokeStyle = hexWithAlpha(accent, (imminent ? 0.34 : 0.22) * pulse);
      ctx.lineWidth = Math.max(5, tile * 0.22);
      ctx.beginPath();
      ctx.moveTo(-tile * 0.58, 0);
      ctx.lineTo(tile * 0.03, 0);
      ctx.stroke();
      ctx.globalCompositeOperation = 'source-over';
      ctx.shadowBlur = 0;
      ctx.strokeStyle = hexWithAlpha(accent, 0.85 * pulse);
      ctx.lineWidth = Math.max(2, tile * 0.08);
      ctx.beginPath();
      ctx.moveTo(-tile * 0.48, 0);
      ctx.lineTo(tile * 0.08, 0);
      ctx.stroke();
      ctx.restore();
      drawProjectileHead(
        cx,
        cy,
        angle,
        ownerActor.unitKey,
        accent,
        0.95 * pulse,
      );
    }

  }

  function drawProjectileHead(
    cx: number,
    cy: number,
    angle: number,
    ownerUnitKey: ReplayStableUnitKey | null,
    accent: string,
    alpha: number,
  ): void {
    const look = ownerUnitKey
      ? unitProjectileLook(replay, ownerUnitKey)
      : presentationProjectileLook();
    const sprite = tintedProjectileSprite(look, accent);
    const size = tile * look.scale;

    // A contact shadow on the floor beneath the bolt. Small, soft and offset the same way
    // a bot's is, so a projectile reads as travelling *over* the arena rather than being
    // painted onto it — the bots already had this and the thing they shoot did not, which
    // is what made bolts look like decals.
    ctx.save();
    ctx.filter = `blur(${Math.max(1, tile * 0.03)}px)`;
    ctx.fillStyle = `rgba(0, 0, 0, ${0.34 * alpha})`;
    ctx.beginPath();
    ctx.ellipse(cx, cy + tile * 0.18, size * 0.3, size * 0.14, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();

    ctx.save();
    ctx.translate(cx, cy);
    ctx.rotate(angle);
    ctx.globalAlpha = alpha;
    ctx.shadowColor = accent;
    ctx.shadowBlur = Math.max(4, tile * 0.18);
    if (sprite) {
      ctx.drawImage(sprite, -size / 2, -size / 2, size, size);
    } else {
      ctx.fillStyle = accent;
      ctx.beginPath();
      ctx.moveTo(size * 0.44, 0);
      ctx.lineTo(-size * 0.2, -size * 0.22);
      ctx.lineTo(-size * 0.2, size * 0.22);
      ctx.closePath();
      ctx.fill();
    }
    ctx.restore();
  }

  /**
   * A volley, drawn as one wide arrow sweeping forward rather than as three bolts.
   *
   * The glyph is a filled crescent: the leading edge runs through every surviving blade's
   * forward point, the trailing edge behind them, and the two are joined with a curve so
   * the fan reads as one connected thing at gameplay zoom. The blades themselves are
   * bright nodes on the spine — the count stays legible, which matters because "three
   * lanes, one gone" is the whole story of a volley meeting cover.
   *
   * When a blade terminates its run is cut, so the picture states it: the remaining
   * segments carry on as their own arrows and the lost one throws a shard backwards along
   * its heading. A shell-eaten blade breaks differently from a wall-shattered one — it
   * collapses in place instead of scattering, because nothing about it was violent.
   */
  function drawVolleys(): void {
    const volleys = volleysAt(replay, time);
    if (volleys.length === 0) return;
    for (const volley of volleys) {
      const accent = accentFor(volley.ownerActor.unitKey);
      for (const run of volley.runs) {
        const visible = run.filter(
          (member) => !boltHidden(member.id, member.x, member.y),
        );
        if (visible.length !== run.length) continue;
        drawVolleyRun(run, accent);
      }
      for (const member of volley.broken) {
        if (boltHidden(member.id, member.x, member.y)) continue;
        drawBrokenSegment(member, accent);
      }
    }
  }

  function drawVolleyRun(
    run: readonly VolleyMember[],
    authoredAccent: string,
  ): void {
    const outline = volleyArrowOutline(run, 0.46, 0.62);
    const mid = run[Math.floor(run.length / 2)];
    const accent = accentAt(
      authoredAccent,
      px(mid.x) + tile / 2,
      py(mid.y) + tile / 2,
    );
    const toX = (point: { x: number }) => px(point.x) + tile / 2;
    const toY = (point: { y: number }) => py(point.y) + tile / 2;
    const pulse = 0.78 + 0.22 * Math.sin(fraction * Math.PI);

    // One closed ribbon: forward edge left-to-right, rear edge back again. Curved rather
    // than polygonal — a fan of three headings gives three points, and a hard chevron
    // through them reads as a crude polygon where the sweep should be.
    const ribbon = new Path2D();
    curveThrough(ribbon, outline.leading.map((p) => ({ x: toX(p), y: toY(p) })), true);
    curveThrough(
      ribbon,
      [...outline.trailing].reverse().map((p) => ({ x: toX(p), y: toY(p) })),
      false,
    );
    ribbon.closePath();

    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    // Light, not a plate: the ribbon can span four tiles once the fan has spread, and at
    // that width anything opaque enough to read as a surface hides the arena under it.
    // The body is therefore unlit — only the leading edge and the blades glow.
    ctx.fillStyle = hexWithAlpha(accent, 0.13 * pulse);
    ctx.fill(ribbon);
    // The forward edge is the part that has to arrive first, so it carries the hard line.
    const edge = new Path2D();
    curveThrough(edge, outline.leading.map((p) => ({ x: toX(p), y: toY(p) })), true);
    ctx.shadowColor = accent;
    ctx.shadowBlur = Math.max(6, tile * 0.3);
    ctx.strokeStyle = hexWithAlpha(accent, 0.95 * pulse);
    ctx.lineWidth = Math.max(2.5, tile * 0.085);
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.stroke(edge);
    ctx.strokeStyle = `rgba(240, 251, 255, ${0.85 * pulse})`;
    ctx.lineWidth = Math.max(1, tile * 0.022);
    ctx.stroke(edge);
    ctx.restore();

    // A node per blade. Without them a three-lane arrow and a two-lane arrow are the same
    // shape at slightly different widths, and losing a lane is the event worth seeing.
    for (const node of outline.nodes) {
      const cx = px(node.x) + tile / 2;
      const cy = py(node.y) + tile / 2;
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.shadowColor = accent;
      ctx.shadowBlur = Math.max(4, tile * 0.2);
      ctx.fillStyle = hexWithAlpha(accent, 0.9 * pulse);
      ctx.beginPath();
      ctx.ellipse(
        cx,
        cy,
        tile * 0.13,
        tile * 0.09,
        Math.atan2(node.ny, node.nx),
        0,
        Math.PI * 2,
      );
      ctx.fill();
      ctx.fillStyle = `rgba(245, 252, 255, ${0.85 * pulse})`;
      ctx.beginPath();
      ctx.arc(cx, cy, tile * 0.045, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
  }

  function drawBrokenSegment(
    member: VolleyMember,
    authoredAccent: string,
  ): void {
    const age = member.breakAge ?? 0;
    const cx = px(member.x) + tile / 2;
    const cy = py(member.y) + tile / 2;
    const accent = accentAt(authoredAccent, cx, cy);
    const [sx, sy] = projectileStep[member.heading];
    const length = Math.hypot(sx, sy) || 1;
    const nx = sx / length;
    const ny = sy / length;
    const fade = (1 - age) ** 2;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.shadowColor = accent;
    ctx.shadowBlur = Math.max(4, tile * 0.2);
    ctx.lineCap = 'round';
    if (member.breakKind === 'deflected') {
      // Turned, not broken: the blade folds where the shell caught it — the
      // return bolt the deflection launched renders itself.
      ctx.strokeStyle = hexWithAlpha(accent, 0.8 * fade);
      ctx.lineWidth = Math.max(2, tile * 0.07 * (1 - age * 0.6));
      const shrink = tile * 0.34 * (1 - age);
      ctx.beginPath();
      ctx.ellipse(cx, cy, shrink, shrink * 0.55, Math.atan2(ny, nx), 0, Math.PI * 2);
      ctx.stroke();
    } else {
      // Three shards thrown back off the point of contact, spreading and dimming.
      for (const spread of [-0.55, 0, 0.55]) {
        const angle = Math.atan2(ny, nx) + Math.PI + spread * (0.4 + age);
        const reach = tile * (0.16 + age * 0.55);
        ctx.strokeStyle = hexWithAlpha(accent, 0.85 * fade);
        ctx.lineWidth = Math.max(1.5, tile * 0.05 * (1 - age));
        ctx.beginPath();
        ctx.moveTo(cx + Math.cos(angle) * reach * 0.35, cy + Math.sin(angle) * reach * 0.35);
        ctx.lineTo(cx + Math.cos(angle) * reach, cy + Math.sin(angle) * reach);
        ctx.stroke();
      }
    }
    ctx.restore();
  }

  /**
   * A smooth curve through the given points, appended to `path`.
   *
   * Midpoint-quadratic rather than Catmull-Rom: three points is the common case and the
   * cheap construction is indistinguishable there, while never overshooting outside the
   * hull — an arrow that bulges past its own outermost blade would be claiming reach the
   * volley does not have.
   */
  function curveThrough(
    path: Path2D,
    points: readonly { x: number; y: number }[],
    start: boolean,
  ): void {
    if (points.length === 0) return;
    if (start) path.moveTo(points[0].x, points[0].y);
    else path.lineTo(points[0].x, points[0].y);
    if (points.length === 1) return;
    for (let index = 1; index < points.length - 1; index++) {
      const current = points[index];
      const next = points[index + 1];
      path.quadraticCurveTo(
        current.x,
        current.y,
        (current.x + next.x) / 2,
        (current.y + next.y) / 2,
      );
    }
    const last = points[points.length - 1];
    path.lineTo(last.x, last.y);
  }

  function drawHeardSounds(): void {
    // Redacted hearing, made visible: the selected bot's heard sounds render as
    // neutral arcs on the bearing octant, at a radius keyed to the distance band.
    // Deliberately identity-free and coordinate-free — exactly what the bot knows.
    if (hearingSource === undefined) return;
    const sounds = hearingSource.observation.heardSounds;
    if (!sounds || sounds.length === 0) return;
    const me = poses.find(
      (pose) => pose.actorKey === hearingSource.actorKey,
    );
    if (!me) return;
    const cx = px(me.x) + tile / 2;
    const cy = py(me.y) + tile / 2;
    for (const sound of sounds) {
      // Octant 0 = North, clockwise; canvas angles start East, clockwise (y down).
      const angle = -Math.PI / 2 + (sound.bearing * Math.PI) / 4;
      const radius = tile * (1.6 + sound.distance * 0.9);
      ctx.strokeStyle = 'rgba(250, 204, 21, 0.85)';
      ctx.lineWidth = Math.max(2, tile * 0.09);
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.arc(cx, cy, radius, angle - Math.PI / 10, angle + Math.PI / 10);
      ctx.stroke();
    }
  }

  function drawShadowsAndBots(): void {
    for (const pose of poses) {
      if (
        pose.status !== 'active' &&
        time >
          (replay.result?.endTick ?? replay.ticks.length - 1) + 0.9
      )
        continue;
      if (hiddenByFog(pose)) continue;
      drawShadow(pose);
    }
    for (const pose of poses) {
      if (pose.status !== 'active' || hiddenByFog(pose)) continue;
      drawLocomotionMotion(pose);
    }
    // Under the bodies, on the floor, exactly like the 3D renderer puts them
    // there: a ring drawn over a chassis would paint on the machine it is
    // describing.
    for (const pose of poses) {
      if (pose.status !== 'active' || hiddenByFog(pose)) continue;
      drawBodyMechanics(pose);
    }
    for (const pose of poses) {
      drawBot(pose);
    }
    // And the load rides above, because that is where it is.
    for (const pose of poses) {
      if (pose.status !== 'active' || hiddenByFog(pose)) continue;
      drawCarriedScrap(pose);
    }
    // Last, so a label is never painted over: the mind's own word for what
    // this body is doing.
    for (const pose of poses) {
      if (pose.status !== 'active' || hiddenByFog(pose)) continue;
      drawRoleTag(pose);
    }
  }

  /**
   * THE WATCHABILITY DELIVERABLE (§12.3). A small caption under each labelled
   * body, coloured by a stable hash of the tag so `channeler` is the same
   * colour all match and across matches — and drawn for VISIBLE ENEMIES too,
   * because half the drama of a set-piece is seeing both sides' assignments
   * and knowing one of them is wrong.
   *
   * Where a tag is absent nothing is drawn at all: an unlabelled body should
   * look unlabelled, not broken.
   */
  function drawRoleTag(pose: BotPose): void {
    const unit = tickPresentation?.units.find(
      (candidate) => candidate.unitKey === pose.unitKey,
    );
    const tag = unit?.roleTag;
    if (!tag) return;
    const caption = roleTagCaption(tag);
    // Camera close-ups must enlarge the machine, not turn sheet metadata into a billboard.
    const size = Math.min(13, Math.max(7, Math.round(tile * 0.24)));
    const cx = px(pose.x) + tile / 2;
    const cy = py(pose.y) + tile * 0.99;
    ctx.save();
    ctx.font = `${size}px ui-monospace, SFMono-Regular, Menlo, monospace`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
    // A hairline of the field colour behind the glyphs, so the caption stays
    // readable over a lit floor without a panel behind it.
    ctx.lineWidth = Math.max(2, size * 0.34);
    ctx.strokeStyle = 'rgba(2, 6, 12, 0.85)';
    ctx.lineJoin = 'round';
    ctx.strokeText(caption, cx, cy);
    ctx.fillStyle = roleTagColor(tag);
    ctx.fillText(caption, cx, cy);
    ctx.restore();
  }

  /**
   * What a body is doing about the two new mechanics: holding the point,
   * guarding whoever is, or wearing the tier its team just bought.
   *
   * The flat renderer's cheap half of the 3D cues: a solid ring for a body
   * channelling, a dashed one at a wider radius for its screen, and a brass
   * ring thrown outward for a purchase. The first two are team-coloured
   * because a channel belongs to a team; the third is scrap's own colour. All
   * three are on the floor so the chassis stays legible.
   */
  function drawBodyMechanics(pose: BotPose): void {
    const unit = tickPresentation?.units.find(
      (candidate) => candidate.unitKey === pose.unitKey,
    );
    const purchase = tickPresentation?.economy?.purchases.find(
      (entry) => entry.teamId === pose.teamId,
    );
    const cx = px(pose.x) + tile / 2;
    const cy = py(pose.y) + tile / 2;
    if (purchase) {
      // A tier this body's team just bought, thrown outward and out. Brass,
      // and from the machine rather than from a tile, so it cannot be read as
      // an impact.
      const spread = 1 - purchase.strength;
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.strokeStyle = hexWithAlpha(
        SCRAP_ACCENT,
        purchase.strength ** 1.4 * 0.9,
      );
      ctx.shadowColor = SCRAP_ACCENT;
      ctx.shadowBlur = Math.max(4, tile * 0.2);
      ctx.lineWidth = Math.max(2, tile * 0.07);
      ctx.beginPath();
      ctx.arc(cx, cy, tile * 0.42 * (1 + spread * 1.9), 0, Math.PI * 2);
      ctx.stroke();
      ctx.restore();
    }
    if (!unit?.channelRole) return;
    const accent = accentFor(pose.unitKey);
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    if (unit.channelRole === 'channeling') {
      const swell = 0.5 + 0.5 * Math.sin(time * Math.PI * 1.6);
      ctx.strokeStyle = hexWithAlpha(accent, 0.5 + 0.32 * swell);
      ctx.shadowColor = accent;
      ctx.shadowBlur = Math.max(4, tile * 0.16);
      ctx.lineWidth = Math.max(2, tile * 0.055);
      ctx.beginPath();
      ctx.arc(cx, cy, tile * (0.4 + 0.02 * swell), 0, Math.PI * 2);
      ctx.stroke();
    } else {
      ctx.strokeStyle = hexWithAlpha(accent, 0.34);
      ctx.lineWidth = Math.max(1.5, tile * 0.035);
      ctx.setLineDash([Math.max(3, tile * 0.1), Math.max(3, tile * 0.1)]);
      ctx.beginPath();
      ctx.arc(cx, cy, tile * 0.47, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }
    ctx.restore();
  }

  /**
   * A loaded body, said in scrap's own neutral colour.
   *
   * Cheap by design — this is the floor for the self-contained CLI viewer and
   * a device with no WebGL — so the orbiting shards of the 3D cue become a
   * ring of dots over the hull and a wash under it. The dot count is the load,
   * which is the number that decides whether the body is worth chasing.
   */
  function drawCarriedScrap(pose: BotPose): void {
    const unit = tickPresentation?.units.find(
      (candidate) => candidate.unitKey === pose.unitKey,
    );
    const load = unit?.carriedScrap ?? 0;
    if (load <= 0) return;
    const cx = px(pose.x) + tile / 2;
    const cy = py(pose.y) + tile / 2;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    const wash = ctx.createRadialGradient(cx, cy, 0, cx, cy, tile * 0.7);
    wash.addColorStop(
      0,
      hexWithAlpha(SCRAP_ACCENT, 0.16 + 0.2 * (unit?.carriedFraction ?? 0)),
    );
    wash.addColorStop(1, hexWithAlpha(SCRAP_ACCENT, 0));
    ctx.fillStyle = wash;
    ctx.beginPath();
    ctx.arc(cx, cy, tile * 0.7, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = hexWithAlpha(SCRAP_ACCENT, 0.95);
    ctx.shadowColor = SCRAP_ACCENT;
    ctx.shadowBlur = Math.max(3, tile * 0.12);
    const shards = Math.min(load, 6);
    for (let index = 0; index < shards; index++) {
      const angle =
        time * Math.PI * 0.8 + (index / shards) * Math.PI * 2;
      ctx.beginPath();
      ctx.arc(
        cx + Math.cos(angle) * tile * 0.34,
        cy - tile * 0.34 + Math.sin(angle) * tile * 0.12,
        tile * 0.055,
        0,
        Math.PI * 2,
      );
      ctx.fill();
    }
    ctx.restore();
  }

  /**
   * Loose scrap on the floor.
   *
   * A diamond over its own wash, sized gently by amount and blinking out in
   * the last quarter of its 80 ticks — the same sentence the 3D ingot says,
   * in the two dimensions this renderer has.
   */
  function drawScrapPiles(): void {
    for (const pile of tickPresentation?.economy?.piles ?? []) {
      const cx = px(pile.position.x) + tile / 2;
      const cy = py(pile.position.y) + tile / 2;
      const blink = pile.expiring
        ? 0.35 + 0.65 * (0.5 + 0.5 * Math.sin(time * Math.PI * 6))
        : 1;
      const alive = 0.35 + 0.65 * pile.lifeFraction;
      const bulk =
        tile * 0.17 * (1 + 0.42 * Math.min(1, Math.log2(1 + pile.amount) / 3));
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      const wash = ctx.createRadialGradient(cx, cy, 0, cx, cy, tile * 0.46);
      wash.addColorStop(0, hexWithAlpha(SCRAP_ACCENT, 0.26 * alive * blink));
      wash.addColorStop(1, hexWithAlpha(SCRAP_ACCENT, 0));
      ctx.fillStyle = wash;
      ctx.beginPath();
      ctx.arc(cx, cy, tile * 0.46, 0, Math.PI * 2);
      ctx.fill();

      ctx.translate(cx, cy);
      ctx.rotate(time * Math.PI * 0.35);
      ctx.fillStyle = hexWithAlpha(SCRAP_ACCENT, 0.9 * blink);
      ctx.shadowColor = SCRAP_ACCENT;
      ctx.shadowBlur = Math.max(4, tile * 0.16);
      ctx.beginPath();
      ctx.moveTo(0, -bulk);
      ctx.lineTo(bulk, 0);
      ctx.lineTo(0, bulk);
      ctx.lineTo(-bulk, 0);
      ctx.closePath();
      ctx.fill();

      // The clock, as a hexagon that shrinks with what is left of the pile's life.
      ctx.strokeStyle = hexWithAlpha(
        SCRAP_ACCENT,
        (0.2 + 0.5 * pile.lifeFraction) * blink,
      );
      ctx.lineWidth = Math.max(1.5, tile * 0.028);
      ctx.beginPath();
      const collar = bulk * (1.35 + 0.5 * pile.lifeFraction);
      for (let corner = 0; corner < 6; corner++) {
        const angle = (corner / 6) * Math.PI * 2;
        const x = Math.cos(angle) * collar;
        const y = Math.sin(angle) * collar;
        if (corner === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      }
      ctx.closePath();
      ctx.stroke();
      ctx.restore();
    }
  }

  /**
   * How much of this body is here yet: 1 unless it is materializing on this very tick.
   *
   * One number, asked by the chassis and by its shadow, so the two cannot come up out of
   * the pad at different rates.
   */
  function emergence(pose: BotPose): number {
    const arrival = arrivals.get(pose.actorKey);
    return arrival ? 0.35 + 0.65 * easeOut(arrival.age) : 1;
  }

  function drawShadow(pose: BotPose): void {
    const cx = px(pose.x) + tile / 2;
    const cy = py(pose.y) + tile / 2 + tile * 0.2;
    const form = replay.forms.find(
      (candidate) => candidate.formId === pose.formId,
    );
    const visualIndex = visualIndexForUnit(replay, pose.unitKey);
    const look = unitLook(replay, pose.unitKey, pose.formId);
    const hover =
      pose.status === 'active' &&
      form?.canMove !== false &&
      look.locomotionCue === 'low-hover'
        ? Math.sin((time + visualIndex * 0.31) * Math.PI * 2) *
          tile *
          0.018
        : 0;
    // A materializing body has a materializing shadow, or it stands on somebody else's.
    const emerge = emergence(pose);
    ctx.save();
    ctx.filter = `blur(${Math.max(1, tile * 0.045)}px)`;
    ctx.fillStyle = `rgba(0, 0, 0, ${
      (look.locomotionCue === 'low-hover' ? 0.38 : 0.58) * emerge
    })`;
    ctx.beginPath();
    ctx.ellipse(
      cx,
      cy - hover,
      tile * (look.locomotionCue === 'low-hover' ? 0.33 : 0.4) * emerge,
      tile * (look.locomotionCue === 'low-hover' ? 0.14 : 0.18) * emerge,
      0,
      0,
      Math.PI * 2,
    );
    ctx.fill();
    ctx.restore();
  }

  function drawLocomotionMotion(pose: BotPose): void {
    const look = unitLook(replay, pose.unitKey, pose.formId);
    const dx = pose.motionX;
    const dy = pose.motionY;
    const distance = Math.hypot(dx, dy);
    const cx = px(pose.x) + tile / 2;
    const cy = py(pose.y) + tile / 2;
    if (look.locomotionCue === 'low-hover') {
      const pulse = 0.5 + 0.5 * Math.sin((time + pose.unitId * 0.17) * Math.PI * 2);
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.strokeStyle = `rgba(229, 208, 157, ${0.14 + pulse * 0.13})`;
      ctx.lineWidth = Math.max(1, tile * 0.025);
      ctx.beginPath();
      ctx.ellipse(cx, cy + tile * 0.18, tile * 0.3, tile * 0.1, 0, 0, Math.PI * 2);
      ctx.stroke();
      ctx.restore();
    }
    if (distance === 0) return;
    const nx = dx / distance;
    const ny = dy / distance;
    const sideX = -ny;
    const sideY = nx;
    // A moving body keeps a wake through the whole authoritative A-to-B
    // segment. Fading it to zero at every integer tick made consecutive
    // movement look like a sequence of chess-piece starts and stops.
    const life = 1;
    const backX = cx - nx * tile * 0.36;
    const backY = cy - ny * tile * 0.36;
    ctx.save();
    const accent = accentFor(pose.unitKey);
    ctx.strokeStyle = hexWithAlpha(accent, 0.38 * life);
    ctx.fillStyle = `rgba(197, 177, 137, ${0.2 * life})`;
    ctx.lineCap = 'round';
    if (look.locomotionCue === 'low-hover') {
      ctx.globalCompositeOperation = 'lighter';
      ctx.shadowColor = accent;
      ctx.shadowBlur = Math.max(3, tile * 0.1);
      ctx.lineWidth = Math.max(1.5, tile * 0.042);
      for (const side of [-1, 1]) {
        ctx.beginPath();
        ctx.moveTo(
          backX + sideX * side * tile * 0.13,
          backY + sideY * side * tile * 0.13,
        );
        ctx.lineTo(
          backX - nx * tile * (0.22 + life * 0.14) + sideX * side * tile * 0.09,
          backY - ny * tile * (0.22 + life * 0.14) + sideY * side * tile * 0.09,
        );
        ctx.stroke();
      }
    } else if (look.locomotionCue === 'skids') {
      ctx.lineWidth = Math.max(1.5, tile * 0.035);
      for (const side of [-1, 1]) {
        ctx.beginPath();
        ctx.moveTo(
          backX + sideX * side * tile * 0.2,
          backY + sideY * side * tile * 0.2,
        );
        ctx.lineTo(
          backX - nx * tile * 0.32 + sideX * side * tile * 0.2,
          backY - ny * tile * 0.32 + sideY * side * tile * 0.2,
        );
        ctx.stroke();
      }
    } else {
      const count = look.locomotionCue === 'treads' ? 5 : 3;
      for (let index = 0; index < count; index += 1) {
        const spread = (index - (count - 1) / 2) * tile * 0.11;
        const trail = tile * (0.12 + index * 0.04) * life;
        ctx.beginPath();
        ctx.ellipse(
          backX - nx * trail + sideX * spread,
          backY - ny * trail + sideY * spread,
          tile * 0.055,
          tile * 0.03,
          Math.atan2(ny, nx),
          0,
          Math.PI * 2,
        );
        ctx.fill();
      }
    }
    ctx.restore();
  }

  function drawBot(pose: BotPose): void {
    const participant = participantForUnit(replay, pose.unitKey);
    const accent = accentFor(pose.unitKey);
    const visualIndex = visualIndexForUnit(replay, pose.unitKey);
    // The **effective** form, every tick. A life that mobilizes back out of a turret is
    // wearing a mobile form again from that tick on, and the chassis has to say so; the
    // pose already carries the authoritative form, so nothing here is remembered between
    // frames.
    const look = unitLook(replay, pose.unitKey, pose.formId);
    const form = replay.forms.find(
      (candidate) => candidate.formId === pose.formId,
    );
    // Which turret-shaped skill this life is standing in, if any. A stance is a third
    // body — not a mobile chassis and not an omnidirectional emplacement — and every
    // decision below that used to be "can it move?" has to ask this too.
    const stance = stanceKindForForm(pose.formId);
    const cx = px(pose.x) + tile / 2;
    const hover =
      pose.status === 'active' &&
      form?.canMove !== false &&
      look.locomotionCue === 'low-hover'
        ? Math.sin((time + visualIndex * 0.31) * Math.PI * 2) *
          tile *
          0.022
        : 0;
    const moving = Math.hypot(pose.motionX, pose.motionY) > 0;
    const travelLift = moving
      ? tile * (look.locomotionCue === 'low-hover' ? 0.026 : 0.009)
      : 0;
    const cy = py(pose.y) + tile / 2 + hover - travelLift;
    const radius = tile * 0.38;
    const destroyedNow = (currentTick?.events ?? []).some(
      (event) =>
        isDestructionEvent(event.type) &&
        event.targetActor?.actorKey === pose.actorKey,
    );
    const destroyed = pose.status !== 'active' || destroyedNow;
    const ghosted = hiddenByFog(pose);
    const fired = (currentTick?.events ?? []).some(
      (event) =>
        isAttackEvent(event.type) &&
        event.sourceActor?.actorKey === pose.actorKey,
    );
    const damaged = (currentTick?.events ?? []).some(
      (event) =>
        event.type === 'damage' &&
        event.targetActor?.actorKey === pose.actorKey,
    );
    const recoil =
      fired && shotProgress() > 0
        ? Math.sin(Math.min(1, shotProgress()) * Math.PI) * tile * 0.09
        : 0;
    const destructionProgress = destroyedNow
      ? Math.max(0, Math.min((fraction - 0.55) / 0.45, 1))
      : destroyed
        ? 1
        : 0;

    ctx.save();
    ctx.translate(cx, cy);

    // Arriving: the body scales up out of the pad and settles, under the ring closing on
    // it. Applied to the whole chassis transform so every cue drawn below — the selection
    // ring, the emplacement collar, the health bar — comes up with it as one machine.
    const emerge = emergence(pose);
    if (emerge < 1) {
      ctx.translate(0, tile * 0.3 * (1 - emerge));
      ctx.globalAlpha *= Math.min(1, emerge * 1.4);
      ctx.scale(emerge, emerge);
    }

    if (!destroyed && !ghosted && pose.pendingFormTransition) {
      const transition = pose.pendingFormTransition;
      const duration = Math.max(
        1,
        transition.completesAtTick - transition.startedAtTick + 1,
      );
      const progress = Math.max(
          0,
          Math.min(
            1,
            (time - transition.startedAtTick) / duration,
          ),
      );
      const windupRadius = radius + tile * 0.16;
      ctx.save();
      ctx.rotate(time * Math.PI * 0.45);
      ctx.strokeStyle = hexWithAlpha(accent, 0.24);
      ctx.lineWidth = Math.max(2, tile * 0.045);
      ctx.setLineDash([tile * 0.08, tile * 0.07]);
      ctx.beginPath();
      ctx.arc(0, 0, windupRadius, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.strokeStyle = hexWithAlpha(accent, 0.92);
      ctx.lineWidth = Math.max(2.5, tile * 0.06);
      ctx.beginPath();
      ctx.arc(
        0,
        0,
        windupRadius,
        -Math.PI / 2,
        -Math.PI / 2 + Math.PI * 2 * progress,
      );
      ctx.stroke();
      for (let index = 0; index < 4; index++) {
        const angle = index * (Math.PI / 2);
        ctx.fillStyle = hexWithAlpha(accent, 0.78);
        ctx.fillRect(
          Math.cos(angle) * (windupRadius + tile * 0.035) -
            tile * 0.025,
          Math.sin(angle) * (windupRadius + tile * 0.035) -
            tile * 0.025,
          tile * 0.05,
          tile * 0.05,
        );
      }
      ctx.restore();
    }

    // An emplacement ring, for an emplacement. A stance is *not* one: it keeps a facing,
    // and a radial ring around it would say the opposite of the rule it is under.
    if (!destroyed && form?.canMove === false && stance === null) {
      ctx.save();
      ctx.strokeStyle = hexWithAlpha(accent, 0.58);
      ctx.fillStyle = hexWithAlpha(accent, 0.08);
      ctx.lineWidth = Math.max(2, tile * 0.055);
      ctx.beginPath();
      ctx.arc(0, 0, radius + tile * 0.08, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
      for (let index = 0; index < 4; index++) {
        const angle = index * (Math.PI / 2);
        ctx.beginPath();
        ctx.moveTo(
          Math.cos(angle) * (radius + tile * 0.02),
          Math.sin(angle) * (radius + tile * 0.02),
        );
        ctx.lineTo(
          Math.cos(angle) * (radius + tile * 0.18),
          Math.sin(angle) * (radius + tile * 0.18),
        );
        ctx.stroke();
      }
      ctx.restore();
    }

    if (destroyed) {
      ctx.globalAlpha = 0.56 - destructionProgress * 0.2;
      ctx.rotate(pose.angle + destructionProgress * 0.55);
      const collapse = 1 - destructionProgress * 0.14;
      ctx.scale(collapse, collapse);
    } else {
      ctx.rotate(pose.angle);
    }
    if (ghosted) ctx.globalAlpha = 0.15; // true position, but the selected bot can't see it

    if (pose.unitKey === selectedUnitKey) {
      ctx.strokeStyle = hexWithAlpha(accent, 0.9);
      ctx.lineWidth = 2;
      ctx.setLineDash([4, 3]);
      ctx.beginPath();
      ctx.arc(0, 0, radius + 5, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    // Keep the authoritative root, shadow, and selection pad exactly on their sampled
    // path, while the pictured chassis carries a few pixels of already-revealed momentum.
    // This rounds the perceived corner and lets a stop settle mechanically without moving
    // the occupied tile or reading the next action. The WebGL inner hull uses the same
    // 0.055-tile ceiling.
    if (!destroyed && form?.canMove !== false) {
      const previousPose = previousPoseByActor.get(pose.actorKey);
      const inertia = motionEase(fraction);
      const priorX = previousPose?.motionX ?? 0;
      const priorY = previousPose?.motionY ?? 0;
      const inertialX = priorX + (pose.motionX - priorX) * inertia;
      const inertialY = priorY + (pose.motionY - priorY) * inertia;
      const worldLagX = -inertialX * tile * 0.055;
      const worldLagY = -inertialY * tile * 0.055;
      const cos = Math.cos(pose.angle);
      const sin = Math.sin(pose.angle);
      ctx.translate(
        cos * worldLagX + sin * worldLagY,
        -sin * worldLagX + cos * worldLagY,
      );
    }

    ctx.translate(-recoil, 0);
    const image = teamAccentedBotImage(look, accent);
    if (image?.complete && image.naturalWidth > 0) {
      const size = tile * look.scale;
      if (damaged && shotProgress() > 0.55)
        ctx.filter = `brightness(${1.45 + (1 - shotProgress()) * 0.8}) saturate(0.65)`;
      ctx.drawImage(image, -size / 2, -size / 2, size, size);
      ctx.filter = 'none';
    } else {
      drawFallbackChassis(participant?.name ?? '', radius, accent, destroyed);
    }

    // Directional drive light is fixed to the chassis, while its exhaust
    // points opposite the replay's actual displacement. Together with the
    // authoritative nose marker this makes forward drive, reverse and strafe
    // visually different without rotating or moving the body away from replay
    // truth.
    if (!destroyed && !ghosted && moving && form?.canMove !== false) {
      const distance = Math.hypot(pose.motionX, pose.motionY);
      const worldX = pose.motionX / distance;
      const worldY = pose.motionY / distance;
      const localX = Math.cos(pose.angle) * worldX + Math.sin(pose.angle) * worldY;
      const localY = -Math.sin(pose.angle) * worldX + Math.cos(pose.angle) * worldY;
      const exhaustX = -localX;
      const exhaustY = -localY;
      const sideX = -exhaustY;
      const sideY = exhaustX;
      const sourceX = exhaustX * radius * 0.54;
      const sourceY = exhaustY * radius * 0.54;
      const drive = 1;
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.strokeStyle = hexWithAlpha(accent, 0.74 * drive);
      ctx.shadowColor = accent;
      ctx.shadowBlur = Math.max(4, tile * 0.13);
      ctx.lineWidth = Math.max(1.4, tile * 0.038);
      ctx.lineCap = 'round';
      for (const side of [-1, 1]) {
        ctx.beginPath();
        ctx.moveTo(
          sourceX + sideX * side * radius * 0.19,
          sourceY + sideY * side * radius * 0.19,
        );
        ctx.lineTo(
          sourceX + exhaustX * radius * 0.32 + sideX * side * radius * 0.1,
          sourceY + exhaustY * radius * 0.32 + sideY * side * radius * 0.1,
        );
        ctx.stroke();
      }
      ctx.restore();
    }

    // Which way this machine is pointing, stated outright.
    //
    // Facing and movement are decoupled by the generic contract — a bot may step north
    // while facing east, and it stays facing east until it spends an action turning. Read
    // off a chassis alone that is nearly invisible: several looks are close to
    // symmetrical, one is a disc on purpose, and reviewers consistently read the result as
    // the strafing that was removed. So the nose carries a marker: a bright wedge riding
    // the hull's leading edge, in the owner's accent, which also puts team colour on the
    // body itself rather than only under it.
    //
    // Not drawn on a stationary form, which has no facing to show — an emplacement that
    // sees and fires in every direction pointing somewhere would be a lie about the rules.
    //
    // A stance is stationary and still very much has one: the volley gun fires along it
    // and the shell only guards the quadrant in front of it, so the marker stays.
    if (!destroyed && (form?.canMove !== false || stance !== null))
      drawFacingMarker(radius, accent);
    if (!destroyed && !ghosted && stance !== null)
      drawStance(stance, radius, accent);

    ctx.restore();

    if (!destroyed && !ghosted)
      drawHealthPips(
        pose,
        cx,
        cy - radius - tile * 0.22,
        accent,
        maxHealthForActor(replay, {
          formId: pose.formId,
          health: pose.health,
        }),
      );
  }

  /**
   * The heading wedge, drawn in the bot's already-rotated frame so +x is its facing.
   *
   * Outlined in the arena's near-black before it is filled: the marker has to survive
   * landing on a pale hull plate as well as on the floor, and an unoutlined accent chevron
   * disappears into the Aureate Warden's gold the moment it is drawn over it.
   */
  function drawFacingMarker(radius: number, accent: string): void {
    const tip = radius * 1.62;
    const base = radius * 1.08;
    const half = radius * 0.44;
    ctx.beginPath();
    ctx.moveTo(tip, 0);
    ctx.lineTo(base, -half);
    ctx.lineTo(base, half);
    ctx.closePath();
    ctx.lineJoin = 'round';
    ctx.lineWidth = Math.max(1.5, tile * 0.045);
    ctx.strokeStyle = 'rgba(6, 11, 18, 0.85)';
    ctx.stroke();
    ctx.fillStyle = accent;
    ctx.fill();
    // A short spine back from the wedge, so the heading still reads when the wedge itself
    // is behind a wall's overhang at the top of the arena.
    ctx.beginPath();
    ctx.moveTo(base, 0);
    ctx.lineTo(radius * 0.12, 0);
    ctx.lineCap = 'round';
    ctx.lineWidth = Math.max(1.5, tile * 0.05);
    ctx.strokeStyle = hexWithAlpha(accent, 0.55);
    ctx.stroke();
  }

  /**
   * The hardware a stance bolts on, drawn in the bot's already-rotated frame so +x is its
   * facing.
   *
   * The stance forms swap chassis art like an Anchor does, and that alone was not enough:
   * two class fallbacks at gameplay zoom are two dark silhouettes of similar mass, and a
   * reviewer watching a match cannot be asked to tell them apart from memory. So each
   * stance also grows *structure* in the owner's accent, and the structure is the rule:
   *
   * - **Volley** puts three barrels where the three bolts come out — at the fan's own
   *   −45°, 0°, +45° — so the shape predicts the shot before it is fired.
   * - **Aegis** fills the facing quadrant and only the facing quadrant. Flanking is the
   *   counter-play, so the arc has to be a *boundary* a viewer can see the edge of: the
   *   plate stops hard at ±45°, and the unguarded rear carries a thin broken line that
   *   says "nothing here" rather than nothing at all, which reads as forgotten.
   */
  function drawStance(
    kind: StanceKind,
    radius: number,
    accent: string,
  ): void {
    const shimmer = 0.82 + 0.18 * Math.sin(time * Math.PI * 1.6);
    ctx.save();
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    if (kind === 'volley') {
      const inner = radius * 0.48;
      const outer = radius * 1.62;
      for (const angle of [-Math.PI / 4, 0, Math.PI / 4]) {
        const dx = Math.cos(angle);
        const dy = Math.sin(angle);
        ctx.strokeStyle = 'rgba(6, 11, 18, 0.9)';
        ctx.lineWidth = Math.max(4, tile * 0.15);
        ctx.beginPath();
        ctx.moveTo(dx * inner, dy * inner);
        ctx.lineTo(dx * outer, dy * outer);
        ctx.stroke();
        ctx.strokeStyle = hexWithAlpha(accent, 0.95);
        ctx.lineWidth = Math.max(2.5, tile * 0.09);
        ctx.beginPath();
        ctx.moveTo(dx * inner, dy * inner);
        ctx.lineTo(dx * outer, dy * outer);
        ctx.stroke();
        // A muzzle bead, so an unfired barrel still reads as a barrel.
        ctx.fillStyle = `rgba(240, 251, 255, ${0.9 * shimmer})`;
        ctx.beginPath();
        ctx.arc(dx * outer, dy * outer, Math.max(1.5, tile * 0.045), 0, Math.PI * 2);
        ctx.fill();
      }
      // The brace the three barrels are mounted on: an arc behind them, closing the fan
      // into one machine rather than three sticks pushed into a bot.
      ctx.strokeStyle = hexWithAlpha(accent, 0.7);
      ctx.lineWidth = Math.max(2.5, tile * 0.06);
      ctx.beginPath();
      ctx.arc(0, 0, radius * 0.92, -Math.PI / 4, Math.PI / 4);
      ctx.stroke();
      ctx.restore();
      return;
    }

    const guardInner = radius * 0.82;
    const guardOuter = radius * 1.55;
    const half = Math.PI / 4;
    // The protected quadrant, filled. Additive so it lifts off the floor without hiding
    // whatever is standing on it.
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.fillStyle = hexWithAlpha(accent, 0.3 * shimmer);
    ctx.beginPath();
    ctx.arc(0, 0, guardOuter, -half, half);
    ctx.arc(0, 0, guardInner, half, -half, true);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
    // Its outer face: the plate itself, thick and unmistakable.
    ctx.strokeStyle = 'rgba(6, 11, 18, 0.85)';
    ctx.lineWidth = Math.max(5, tile * 0.19);
    ctx.beginPath();
    ctx.arc(0, 0, guardOuter, -half, half);
    ctx.stroke();
    ctx.strokeStyle = hexWithAlpha(accent, 0.98);
    ctx.lineWidth = Math.max(3, tile * 0.12);
    ctx.beginPath();
    ctx.arc(0, 0, guardOuter, -half, half);
    ctx.stroke();
    // Where the guard stops. Both edges get a rib running out past the plate, because
    // "the shield ends here" is the sentence a flanker is reading.
    for (const edge of [-half, half]) {
      ctx.strokeStyle = hexWithAlpha(accent, 0.9);
      ctx.lineWidth = Math.max(2, tile * 0.06);
      ctx.beginPath();
      ctx.moveTo(Math.cos(edge) * radius * 0.45, Math.sin(edge) * radius * 0.45);
      ctx.lineTo(
        Math.cos(edge) * guardOuter * 1.12,
        Math.sin(edge) * guardOuter * 1.12,
      );
      ctx.stroke();
    }
    // And the open three quarters, stated rather than left blank.
    ctx.strokeStyle = hexWithAlpha(accent, 0.2);
    ctx.lineWidth = Math.max(1.5, tile * 0.03);
    ctx.setLineDash([tile * 0.07, tile * 0.09]);
    ctx.beginPath();
    ctx.arc(0, 0, guardOuter * 0.94, half, Math.PI * 2 - half);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.restore();
  }

  function drawFallbackChassis(
    name: string,
    radius: number,
    accent: string,
    destroyed: boolean,
  ): void {
    const variant = nameHash(name);
    ctx.fillStyle = '#232f42';
    ctx.beginPath();
    const sides = [0, 6, 8][variant % 3];
    if (sides === 0) ctx.arc(0, 0, radius, 0, Math.PI * 2);
    else
      for (let i = 0; i <= sides; i++) {
        const angle = (i / sides) * Math.PI * 2 + Math.PI / sides;
        ctx[i === 0 ? 'moveTo' : 'lineTo'](
          Math.cos(angle) * radius,
          Math.sin(angle) * radius,
        );
      }
    ctx.closePath();
    ctx.fill();
    ctx.lineWidth = Math.max(2, tile * 0.06);
    ctx.strokeStyle = accent;
    ctx.stroke();

    ctx.fillStyle = accent;
    ctx.beginPath();
    ctx.moveTo(radius * 1.05, 0);
    ctx.lineTo(radius * 0.25, -radius * 0.55);
    ctx.lineTo(radius * 0.25, radius * 0.55);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = destroyed ? '#475569' : '#e2f3ff';
    ctx.beginPath();
    ctx.arc(radius * 0.18, 0, radius * 0.12, 0, Math.PI * 2);
    ctx.fill();
  }

  function drawHealthPips(
    pose: BotPose,
    cx: number,
    cy: number,
    accent: string,
    actorMaxHealth: number,
  ): void {
    const basePip = Math.max(3, tile * 0.10);
    const pip = Math.max(
      2,
      Math.min(
        basePip,
        (tile * 0.85) /
          Math.max(1, 1 + (actorMaxHealth - 1) * 1.6),
      ),
    );
    const gap = pip * 1.6;
    const startX = cx - ((actorMaxHealth - 1) * gap) / 2;
    const readableAccent = accentAt(accent, cx, cy);
    for (let i = 0; i < actorMaxHealth; i++) {
      ctx.fillStyle =
        i < pose.health ? readableAccent : 'rgba(100,116,139,0.35)';
      ctx.beginPath();
      ctx.arc(startX + i * gap, cy, pip / 2, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  function shotProgress(): number {
    // Beams flash during the second half of the tick window (movement settles first).
    return Math.max(0, Math.min((fraction - 0.45) / 0.45, 1));
  }

  function drawShots(): void {
    const progress = shotProgress();
    if (progress <= 0) return;
    for (const event of currentTick?.events ?? []) {
      if (!isAttackEvent(event.type)) continue;
      const from = eventPoint(event.from);
      const to = eventPoint(event.to);
      if (!from || !to) continue;
      const ownerUnitKey = event.sourceActor?.unitKey ?? null;
      const authoredAccent = accentFor(ownerUnitKey);
      const alpha = progress < 0.7 ? 0.95 : 0.95 * (1 - (progress - 0.7) / 0.3);
      const tipX = from.x + (to.x - from.x) * Math.min(progress / 0.7, 1);
      const tipY = from.y + (to.y - from.y) * Math.min(progress / 0.7, 1);
      const accent = accentAt(
        authoredAccent,
        (from.x + tipX) / 2,
        (from.y + tipY) / 2,
      );
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.lineCap = 'round';
      ctx.shadowColor = accent;
      ctx.shadowBlur = Math.max(5, tile * 0.28);
      ctx.strokeStyle = hexWithAlpha(accent, alpha * 0.24);
      ctx.lineWidth = Math.max(7, tile * 0.25);
      drawBeam(from.x, from.y, tipX, tipY);
      ctx.globalCompositeOperation = 'source-over';
      ctx.shadowBlur = 0;
      ctx.strokeStyle = hexWithAlpha(accent, alpha * 0.9);
      ctx.lineWidth = Math.max(3, tile * 0.09);
      drawBeam(from.x, from.y, tipX, tipY);
      ctx.strokeStyle = `rgba(239, 250, 255, ${alpha})`;
      ctx.lineWidth = Math.max(1, tile * 0.025);
      drawBeam(from.x, from.y, tipX, tipY);
      // Muzzle glow.
      ctx.globalCompositeOperation = 'lighter';
      const muzzle = ctx.createRadialGradient(
        from.x,
        from.y,
        0,
        from.x,
        from.y,
        tile * 0.3,
      );
      muzzle.addColorStop(0, `rgba(255, 255, 255, ${alpha})`);
      muzzle.addColorStop(0.3, hexWithAlpha(accent, alpha * 0.8));
      muzzle.addColorStop(1, hexWithAlpha(accent, 0));
      ctx.fillStyle = muzzle;
      ctx.beginPath();
      ctx.arc(from.x, from.y, tile * 0.3, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
      drawProjectileHead(
        tipX,
        tipY,
        Math.atan2(to.y - from.y, to.x - from.x),
        ownerUnitKey,
        accentAt(authoredAccent, tipX, tipY),
        alpha,
      );
    }
  }

  function drawBeam(fromX: number, fromY: number, toX: number, toY: number): void {
    ctx.beginPath();
    ctx.moveTo(fromX, fromY);
    ctx.lineTo(toX, toY);
    ctx.stroke();
  }

  /**
   * A life materializing, which is the exact opposite picture from a life ending.
   *
   * Destruction throws outward: sparks fly off, a shockwave expands, the body tips and
   * fades. So an arrival **condenses**. A wide ring collapses onto the pad and lands as a
   * flash at the moment the body reaches full size, and the accent is the unit's own —
   * through `unitPresentation`, like everything else, so a class arm whose participants
   * all submitted the same colour still arrives in its team's.
   *
   * It matters most under forward rally, where bodies arrive *at the front* rather than
   * behind the line: without this, a machine simply exists mid-fight one frame after it
   * did not, and the fabrication that paid for it is invisible.
   */
  function drawArrivals(): void {
    for (const arrival of arrivals.values()) {
      const centreX = px(arrival.x) + tile / 2;
      const centreY = py(arrival.y) + tile / 2;
      const accent = accentAt(
        accentFor(arrival.unitKey),
        centreX,
        centreY,
      );
      const collapse = easeOut(arrival.age);

      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.shadowColor = accent;
      ctx.shadowBlur = Math.max(4, tile * 0.24);

      // The ring: wide and faint, closing on the body and brightening as it goes.
      const radius = tile * (1.15 - 0.78 * collapse);
      ctx.strokeStyle = hexWithAlpha(accent, 0.25 + 0.6 * collapse);
      ctx.lineWidth = Math.max(1.5, tile * (0.03 + 0.05 * collapse));
      ctx.beginPath();
      ctx.arc(centreX, centreY, radius, 0, Math.PI * 2);
      ctx.stroke();

      // Four marks riding the ring in, so the collapse has a direction and does not read
      // as a circle simply getting smaller.
      ctx.strokeStyle = hexWithAlpha(accent, 0.75 * (1 - collapse));
      ctx.lineWidth = Math.max(1.5, tile * 0.045);
      ctx.lineCap = 'round';
      for (let index = 0; index < 4; index++) {
        const angle = index * (Math.PI / 2) + Math.PI / 4;
        ctx.beginPath();
        ctx.moveTo(
          centreX + Math.cos(angle) * radius,
          centreY + Math.sin(angle) * radius,
        );
        ctx.lineTo(
          centreX + Math.cos(angle) * (radius + tile * 0.3),
          centreY + Math.sin(angle) * (radius + tile * 0.3),
        );
        ctx.stroke();
      }

      // And the landing: a short bloom at the tile, brightest as the ring arrives.
      const landing = Math.max(0, (arrival.age - 0.55) / 0.45);
      if (landing > 0) {
        // Bright enough to land, not so bright that it paints over the machine it just
        // delivered — the body has to be readable the instant it can act.
        const bloom = Math.sin(landing * Math.PI);
        ctx.fillStyle = hexWithAlpha(accent, 0.34 * bloom);
        ctx.beginPath();
        ctx.arc(centreX, centreY, tile * 0.42 * (0.6 + 0.4 * bloom), 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.restore();
    }
  }

  function drawImpacts(): void {
    const progress = shotProgress();
    if (progress < 0.6) return;
    const flash = (progress - 0.6) / 0.4;
    for (const event of currentTick?.events ?? []) {
      if (event.type === 'damage') {
        const at = eventPoint(event.from ?? event.to);
        if (!at) continue;
        const ownerAccent = accentFor(
          event.sourceActor?.unitKey ?? null,
        );
        ctx.save();
        ctx.globalCompositeOperation = 'lighter';
        ctx.shadowColor = ownerAccent;
        ctx.shadowBlur = Math.max(5, tile * 0.28);
        ctx.strokeStyle = hexWithAlpha(ownerAccent, 0.95 * (1 - flash));
        ctx.lineWidth = Math.max(2, tile * 0.07);
        ctx.beginPath();
        ctx.arc(at.x, at.y, tile * (0.25 + flash * 0.3), 0, Math.PI * 2);
        ctx.stroke();
        drawSparks(at.x, at.y, flash, ownerAccent, 7);
        ctx.restore();
      }
      if (event.type === 'projectile-deflected') {
        drawDeflection(event, flash);
        continue;
      }
      if (isDestructionEvent(event.type)) {
        const at = eventPoint(event.from ?? event.to);
        if (!at) continue;
        ctx.save();
        ctx.globalCompositeOperation = 'lighter';
        ctx.shadowColor = '#fbbf24';
        ctx.shadowBlur = Math.max(6, tile * 0.35);
        drawSparks(at.x, at.y, flash, '#fbbf24', 12);
        ctx.strokeStyle = `rgba(251, 191, 36, ${0.8 * (1 - flash)})`;
        ctx.lineWidth = Math.max(3, tile * 0.08);
        ctx.beginPath();
        ctx.arc(at.x, at.y, tile * (0.22 + flash * 0.72), 0, Math.PI * 2);
        ctx.stroke();
        ctx.restore();
      }
    }
  }

  /**
   * A bolt turned on a shell, which is emphatically not a hit on the guard.
   *
   * A damage impact throws a shockwave out and sparks off the victim. This says
   * something different: the bolt was *taken and returned*. The guarded arc lights
   * along its whole length, the way a struck plate rings; the incoming bolt folds at
   * the contact tile and hands off — a short streak leaves the plate back the way the
   * bolt came, in the *defender's* accent, into the return projectile the deflection
   * launched (an ordinary bolt the pipeline draws from this same tick). The camera
   * does not move, because `shakeOffset` reacts to damage and destruction only.
   *
   * The arc is drawn from the defender's facing rather than the contact bearing. That
   * is the point of the effect: every deflection re-states which quadrant is covered,
   * so a player watching a shell get poked repeatedly learns where to go instead.
   */
  function drawDeflection(event: ReplayCausalEvent, flash: number): void {
    const target = event.targetActor;
    const contact = eventPoint(event.to ?? event.from);
    if (!contact) return;
    const guardAccent = accentFor(target?.unitKey ?? null);
    const boltAccent = accentFor(event.sourceActor?.unitKey ?? null);
    const defender = target
      ? poses.find((pose) => pose.actorKey === target.actorKey)
      : undefined;
    const ring = 1 - flash;

    if (defender) {
      const cx = px(defender.x) + tile / 2;
      const cy = py(defender.y) + tile / 2;
      // The authoritative facing the guard was wearing when it ate the bolt, straight off
      // the event — not re-derived from where the bolt came from, which is exactly the
      // thing the viewer must not guess at.
      const facing = event.toFacing ?? null;
      const angle =
        facing !== null ? directionAngle[facing] : defender.angle;
      const half = Math.PI / 4;
      const radius = tile * 0.38 * 1.46;
      ctx.save();
      ctx.translate(cx, cy);
      ctx.rotate(angle);
      ctx.globalCompositeOperation = 'lighter';
      ctx.shadowColor = guardAccent;
      ctx.shadowBlur = Math.max(6, tile * 0.34);
      ctx.lineCap = 'butt';
      // Rings along the plate rather than out from it: same radius throughout, only the
      // brightness moves.
      ctx.strokeStyle = `rgba(226, 245, 255, ${0.95 * ring})`;
      ctx.lineWidth = Math.max(4, tile * 0.16) * (0.6 + 0.4 * ring);
      ctx.beginPath();
      ctx.arc(0, 0, radius, -half, half);
      ctx.stroke();
      ctx.strokeStyle = hexWithAlpha(guardAccent, 0.85 * ring);
      ctx.lineWidth = Math.max(7, tile * 0.28) * (0.5 + 0.5 * ring);
      ctx.beginPath();
      ctx.arc(0, 0, radius, -half, half);
      ctx.stroke();
      ctx.restore();
    }

    // The bolt folds at the plate, then hands off: a short streak leaves the contact
    // tile back along the reversed approach, in the defender's accent — into the
    // return projectile the same tick launches. The fold is quick (first half of the
    // flash) so the streak owns the beat.
    const fold = Math.min(1, flash * 2);
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.shadowColor = boltAccent;
    ctx.shadowBlur = Math.max(4, tile * 0.2);
    ctx.strokeStyle = hexWithAlpha(boltAccent, 0.9 * (1 - fold));
    ctx.lineWidth = Math.max(2, tile * 0.06);
    ctx.beginPath();
    ctx.arc(contact.x, contact.y, tile * 0.3 * (1 - fold * 0.75), 0, Math.PI * 2);
    ctx.stroke();

    const origin = eventPoint(event.from);
    const away =
      origin && (origin.x !== contact.x || origin.y !== contact.y)
        ? Math.atan2(origin.y - contact.y, origin.x - contact.x)
        : null;
    if (away !== null && flash > 0.35) {
      const run = (flash - 0.35) / 0.65;
      const reach = tile * (0.2 + run * 0.75);
      ctx.shadowColor = guardAccent;
      ctx.strokeStyle = hexWithAlpha(guardAccent, 0.95 * (1 - run * 0.6));
      ctx.lineCap = 'round';
      ctx.lineWidth = Math.max(2.5, tile * 0.09) * (1 - run * 0.4);
      ctx.beginPath();
      ctx.moveTo(
        contact.x + Math.cos(away) * tile * 0.08,
        contact.y + Math.sin(away) * tile * 0.08,
      );
      ctx.lineTo(
        contact.x + Math.cos(away) * reach,
        contact.y + Math.sin(away) * reach,
      );
      ctx.stroke();
      ctx.fillStyle = `rgba(233, 247, 255, ${0.9 * (1 - run)})`;
      ctx.beginPath();
      ctx.arc(
        contact.x + Math.cos(away) * reach,
        contact.y + Math.sin(away) * reach,
        tile * 0.07,
        0,
        Math.PI * 2,
      );
      ctx.fill();
    }
    ctx.restore();
  }

  function drawSparks(
    centerX: number,
    centerY: number,
    progress: number,
    color: string,
    count: number,
  ): void {
    ctx.strokeStyle = hexWithAlpha(color, 0.9 * (1 - progress));
    ctx.lineWidth = Math.max(1, tile * 0.035);
    ctx.lineCap = 'round';
    for (let index = 0; index < count; index++) {
      const angle = (index / count) * Math.PI * 2 + progress * 0.8;
      const inner = tile * (0.13 + progress * 0.28);
      const outer = inner + tile * (0.12 + progress * 0.28);
      ctx.beginPath();
      ctx.moveTo(
        centerX + Math.cos(angle) * inner,
        centerY + Math.sin(angle) * inner,
      );
      ctx.lineTo(
        centerX + Math.cos(angle) * outer,
        centerY + Math.sin(angle) * outer,
      );
      ctx.stroke();
    }
  }

  function eventPoint(
    point: { x: number; y: number } | null,
  ): { x: number; y: number } | null {
    if (!point) return null;
    return {
      x: px(point.x) + tile / 2,
      y: py(point.y) + tile / 2,
    };
  }
}

function nameHash(name: string): number {
  let hash = 2166136261;
  for (let i = 0; i < name.length; i++) hash = ((hash ^ name.charCodeAt(i)) * 16777619) >>> 0;
  return hash;
}

/** Fast then settling — an arrival lands rather than easing to a stop. */
function easeOut(t: number): number {
  const clamped = Math.max(0, Math.min(t, 1));
  return 1 - (1 - clamped) ** 3;
}

function motionEase(t: number): number {
  return t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2;
}

function hexWithAlpha(hex: string, alpha: number): string {
  const match = /^#([0-9a-f]{6})$/i.exec(hex);
  if (!match) return hex;
  const value = parseInt(match[1], 16);
  const r = (value >> 16) & 0xff;
  const g = (value >> 8) & 0xff;
  const b = value & 0xff;
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}
