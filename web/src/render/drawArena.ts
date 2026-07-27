import type {
  ReplayActorIdentity,
  ReplayCausalEvent,
  ReplayDirection,
  ReplayModel,
  ReplayProjectileHeading,
  ReplayStableUnitKey,
} from '../replayModel';
import {
  arenaTheme,
  botLook,
  presentationAccent,
  projectileLook,
  type ProjectileLook,
} from './arenaThemes';
import {
  adjustAccentForLuminance,
  sampleCanvasLuminance,
} from './adaptiveAccent';
import { maxHealthForActor } from '../replayMetadata';
import {
  participantForActor,
  participantForUnit,
  visualIndexForUnit,
} from '../replayParticipants';
import { posesAt, type BotPose } from './interpolate';
import { wallAtlasDestination } from './wallAtlasGeometry';
import { drawFogMask } from './fogMask';
import { drawLightSpill, type LightKind, type LightSource } from './lightSpill';

const directionStep: Record<ReplayDirection, [number, number]> = {
  north: [0, -1],
  east: [1, 0],
  south: [0, 1],
  west: [-1, 0],
};

const wallNeighbourStep: readonly [number, number][] = [
  [0, -1],
  [1, -1],
  [1, 0],
  [1, 1],
  [0, 1],
  [-1, 1],
  [-1, 0],
  [-1, -1],
];

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
}

/** Pure canvas renderer: consumes replay data, never computes game rules (plan §32). */
export function drawArena(
  ctx: CanvasRenderingContext2D,
  replay: ReplayModel,
  { time, selectedUnitKey, showVisibility }: DrawOptions,
  width: number,
  height: number,
): void {
  const {
    width: mapWidth,
    height: mapHeight,
    tileRows: mapTiles,
  } = replay.map;
  // A margin so edge walls are not flush with the canvas. Fractional rather than a whole
  // tile: at 24x18 a full tile is 4% of width and 5.5% of height given away to black, and
  // on a letterboxed phone every pixel of arena is already scarce. Must match
  // ArenaCanvas's hit-test, which converts clicks back to tiles with the same figure.
  const MARGIN_TILES = 0.4;
  const tile = Math.floor(
    Math.min(width / (mapWidth + MARGIN_TILES), height / (mapHeight + MARGIN_TILES)),
  );
  const originX = Math.floor((width - tile * mapWidth) / 2);
  const originY = Math.floor((height - tile * mapHeight) / 2);
  const px = (x: number) => originX + x * tile;
  const py = (y: number) => originY + y * tile;

  /**
   * How far a wall's top is displaced per tile of distance from the arena centre.
   *
   * The whole of the 2.5D wall effect, and it is deliberately tiny: at 0.012 a wall in the
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


  ctx.clearRect(0, 0, width, height);

  const tickCount = replay.ticks.length;
  const tick =
    tickCount === 0
      ? 0
      : Math.min(Math.floor(Math.max(0, time)), tickCount - 1);
  const fraction =
    tickCount === 0 ? 0 : Math.max(0, Math.min(time - tick, 1));
  const currentTick = replay.ticks[tick];
  const poses = posesAt(replay, time);
  const theme = arenaTheme(replay.map.presentation?.themeId ?? undefined);
  const boundaryWall = validWallFamily(
    replay.map.presentation?.boundaryWall ?? undefined,
    theme.walls.defaults.boundary,
  );
  const interiorWall = validWallFamily(
    replay.map.presentation?.interiorWall ?? undefined,
    theme.walls.defaults.interior,
  );
  const wallOverrides = new Map<string, string>();
  for (const group of replay.map.presentation?.wallGroups ?? []) {
    const family = validWallFamily(group.family, interiorWall);
    for (const position of group.tiles)
      wallOverrides.set(`${position.x},${position.y}`, family);
  }
  const lookFor = (unitKey: ReplayStableUnitKey) => {
    const participant = participantForUnit(replay, unitKey);
    return botLook(
      participant?.lookId ?? undefined,
      visualIndexForUnit(replay, unitKey),
    );
  };
  const accentFor = (unitKey: ReplayStableUnitKey | null): string => {
    if (unitKey === null) return '#ffffff';
    const participant = participantForUnit(replay, unitKey);
    return presentationAccent(
      lookFor(unitKey),
      participant?.accent ?? '#38bdf8',
    );
  };
  const accentAt = (accent: string, x: number, y: number): string => {
    const background = sampleCanvasLuminance(ctx, x, y, width, height);
    return background === null
      ? accent
      : adjustAccentForLuminance(accent, background);
  };

  // FOV mode: fog what the selected bot can't see, and ghost enemies it has no
  // sight of — the panel view answers "what did this bot know?", so an unseen
  // opponent rendered at full strength would be lying.
  const fogSource =
    showVisibility && selectedUnitKey !== null
      ? currentTick?.actorTurns.find(
          (turn) => turn.actor.unitKey === selectedUnitKey,
        )
      : undefined;
  const hiddenByFog = (pose: BotPose): boolean =>
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
  if (shake) ctx.translate(shake.x, shake.y);

  drawFloor();
  drawZone();
  drawVision();
  drawWalls();
  drawSpill();
  if (showVisibility && selectedUnitKey !== null)
    drawFog(selectedUnitKey);
  drawProjectiles();
  drawHeardSounds();
  drawShadowsAndBots();
  drawShots();
  drawImpacts();

  ctx.restore();

  function shakeOffset(): { x: number; y: number } | null {
    let strength = 0;
    for (const event of currentTick?.events ?? []) {
      if (event.type === 'destroyed') strength = Math.max(strength, 1);
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
    ctx.fillStyle = theme.palette.canvas;
    ctx.fillRect(0, 0, width, height);

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
    if (active) drawZoneTiles(active.tiles);
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
    const eventAccent = (event: ReplayCausalEvent) =>
      event.sourceActor ?? event.targetActor
        ? (participantForActor(
            replay,
            (event.sourceActor ?? event.targetActor)!,
          )?.accent ??
          '#ffffff')
        : '#ffffff';

    const collect = (index: number, age: number) => {
      const at = replay.ticks[index];
      if (!at) return;
      for (const event of at.events) {
        const kind: LightKind | null =
          event.type === 'shot'
            ? 'shot'
            : event.type === 'damage'
              ? 'impact'
              : event.type === 'destroyed'
                ? 'destroyed'
                : null;
        if (!kind) continue;
        if (!event.from) continue;
        sources.push({
          kind,
          x: event.from.x,
          y: event.from.y,
          age,
          color: eventAccent(event),
        });
      }
    };

    collect(tick, fraction);
    collect(tick - 1, 1 + fraction);

    drawLightSpill(ctx, sources, { px, py, tile });
  }

  function drawFog(unitKey: ReplayStableUnitKey): void {
    // Show the selected bot's field of view by FOGGING what it can NOT see.
    // Vision range 6 spans most of a small map, so tinting the visible tiles
    // read as "everything highlighted"; darkening the blind area reads at any size.
    const turn = currentTick?.actorTurns.find(
      (candidate) => candidate.actor.unitKey === unitKey,
    );
    if (!turn) return;
    const visible = new Set(
      turn.observation.visibleTiles.map(
        ({ position }) => `${position.x},${position.y}`,
      ),
    );

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
        let mask = 0;
        for (let bit = 0; bit < 8; bit++) {
          const [dx, dy] = wallNeighbourStep[bit];
          if (wallFamilyAt(x + dx, y + dy) === familyId)
            mask |= 1 << bit;
        }
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
    if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight)
      return boundaryWall;
    if (mapTiles[y][x] !== '#') return null;
    const override = wallOverrides.get(`${x},${y}`);
    if (override) return override;
    return x === 0 || y === 0 || x === mapWidth - 1 || y === mapHeight - 1
      ? boundaryWall
      : interiorWall;
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
    // Replay traversals are authoritative ordered substeps. Interpolating across the
    // path makes speed-two travel read A→B in the first half of the visual tick and
    // B→C in the second; a first-substep hit naturally ends at B.
    const traversals = currentTick?.projectileTraversals ?? [];
    const bolts = currentTick?.after.projectiles ?? [];
    if (bolts.length === 0 && traversals.length === 0) return;
    // FOV mode stays honest: bolts the selected bot can't see aren't drawn at all
    // (an unseen bolt is precisely the threat it doesn't know about).
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
    const movingIds = new Set(
      traversals.map((move) => move.projectileId),
    );

    // Omniscient spectators see the locked future arc. A selected defender
    // sees only physically manifested segments; the owner authored the plan.
    const programmed = new Map<
      string,
      {
        ownerActor: ReplayActorIdentity;
        path: readonly { x: number; y: number }[];
      }
    >();
    for (const move of traversals)
      if (move.programmedPath)
        programmed.set(move.projectileId, {
          ownerActor: move.ownerActor,
          path: move.programmedPath,
        });
    for (const bolt of bolts)
      if (bolt.programmedPath)
        programmed.set(bolt.projectileId, {
          ownerActor: bolt.ownerActor,
          path: bolt.programmedPath,
        });
    for (const plan of programmed.values()) {
      if (
        fogSource !== undefined &&
        selectedUnitKey !== plan.ownerActor.unitKey
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

    for (const move of traversals) {
      if (move.path.length === 0) continue;
      const points = [move.from, ...move.path];
      const progress = fraction * move.path.length;
      const segment = Math.min(Math.floor(progress), move.path.length - 1);
      const local = Math.min(1, progress - segment);
      const from = points[segment];
      const to = points[segment + 1];
      drawBolt(
        from.x + (to.x - from.x) * local,
        from.y + (to.y - from.y) * local,
        headingBetween(from.x, from.y, to.x, to.y),
        move.projectileId,
        move.ownerActor,
        false,
        1,
      );
    }
    for (const bolt of bolts)
      if (!movingIds.has(bolt.projectileId))
        drawBolt(
          bolt.position.x,
          bolt.position.y,
          bolt.heading ?? bolt.launchDirection,
          bolt.projectileId,
          bolt.ownerActor,
          bolt.ticksUntilAdvance === 1,
          bolt.tilesPerAdvance ?? 1,
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
      if (
        seenProjectileIds !== null
          ? !seenProjectileIds.has(projectileId)
          : seenTiles !== null &&
            !seenTiles.has(`${Math.round(x)},${Math.round(y)}`)
      )
        return;
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

    function headingBetween(
      fromX: number,
      fromY: number,
      toX: number,
      toY: number,
    ): ReplayProjectileHeading {
      const dx = Math.sign(toX - fromX);
      const dy = Math.sign(toY - fromY);
      if (dx === 0 && dy < 0) return 'north';
      if (dx > 0 && dy < 0) return 'north-east';
      if (dx > 0 && dy === 0) return 'east';
      if (dx > 0 && dy > 0) return 'south-east';
      if (dx === 0 && dy > 0) return 'south';
      if (dx < 0 && dy > 0) return 'south-west';
      if (dx < 0 && dy === 0) return 'west';
      return 'north-west';
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
    const look = projectileLook(
      ownerUnitKey
        ? (participantForUnit(replay, ownerUnitKey)?.projectileLookId ??
          undefined)
        : undefined,
    );
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

  function drawHeardSounds(): void {
    // Redacted hearing, made visible: the selected bot's heard sounds render as
    // neutral arcs on the bearing octant, at a radius keyed to the distance band.
    // Deliberately identity-free and coordinate-free — exactly what the bot knows.
    if (fogSource === undefined) return;
    const sounds = fogSource.observation.heardSounds;
    if (!sounds || sounds.length === 0) return;
    const me = poses.find(
      (pose) => pose.actorKey === fogSource.actorKey,
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
      drawBot(pose);
    }
  }

  function drawShadow(pose: BotPose): void {
    const cx = px(pose.x) + tile / 2;
    const cy = py(pose.y) + tile / 2 + tile * 0.2;
    const form = replay.forms.find(
      (candidate) => candidate.formId === pose.formId,
    );
    const visualIndex = visualIndexForUnit(replay, pose.unitKey);
    const hover =
      pose.status === 'active' && form?.canMove !== false
        ? Math.sin((time + visualIndex * 0.31) * Math.PI * 2) *
          tile *
          0.018
        : 0;
    ctx.save();
    ctx.filter = `blur(${Math.max(1, tile * 0.045)}px)`;
    ctx.fillStyle = 'rgba(0, 0, 0, 0.52)';
    ctx.beginPath();
    ctx.ellipse(
      cx,
      cy - hover,
      tile * 0.36,
      tile * 0.17,
      0,
      0,
      Math.PI * 2,
    );
    ctx.fill();
    ctx.restore();
  }

  function drawBot(pose: BotPose): void {
    const participant = participantForUnit(replay, pose.unitKey);
    const accent = accentFor(pose.unitKey);
    const visualIndex = visualIndexForUnit(replay, pose.unitKey);
    const look = botLook(participant?.lookId ?? undefined, visualIndex);
    const form = replay.forms.find(
      (candidate) => candidate.formId === pose.formId,
    );
    const cx = px(pose.x) + tile / 2;
    const hover =
      pose.status === 'active' && form?.canMove !== false
        ? Math.sin((time + visualIndex * 0.31) * Math.PI * 2) *
          tile *
          0.022
        : 0;
    const cy = py(pose.y) + tile / 2 + hover;
    const radius = tile * 0.38;
    const destroyedNow = (currentTick?.events ?? []).some(
      (event) =>
        event.type === 'destroyed' &&
        event.targetActor?.actorKey === pose.actorKey,
    );
    const destroyed = pose.status !== 'active' || destroyedNow;
    const ghosted = hiddenByFog(pose);
    const fired = (currentTick?.events ?? []).some(
      (event) =>
        event.type === 'shot' &&
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

    if (!destroyed && form?.canMove === false) {
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

    ctx.translate(-recoil, 0);
    if (look.image?.complete && look.image.naturalWidth > 0) {
      const size = tile * look.scale;
      if (damaged && shotProgress() > 0.55)
        ctx.filter = `brightness(${1.45 + (1 - shotProgress()) * 0.8}) saturate(0.65)`;
      ctx.drawImage(look.image, -size / 2, -size / 2, size, size);
      ctx.filter = 'none';
    } else {
      drawFallbackChassis(participant?.name ?? '', radius, accent, destroyed);
    }

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
      if (event.type !== 'shot') continue;
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

  function drawImpacts(): void {
    const progress = shotProgress();
    if (progress < 0.6) return;
    const flash = (progress - 0.6) / 0.4;
    for (const event of currentTick?.events ?? []) {
      if (event.type === 'damage') {
        const at = eventPoint(event.from);
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
      if (event.type === 'destroyed') {
        const at = eventPoint(event.from);
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

function hexWithAlpha(hex: string, alpha: number): string {
  const match = /^#([0-9a-f]{6})$/i.exec(hex);
  if (!match) return hex;
  const value = parseInt(match[1], 16);
  const r = (value >> 16) & 0xff;
  const g = (value >> 8) & 0xff;
  const b = value & 0xff;
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}
