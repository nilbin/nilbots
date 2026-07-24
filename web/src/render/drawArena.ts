import type { Direction, ProjectileHeading, ReplayDocument } from '../types';
import {
  arenaTheme,
  botLook,
  presentationAccent,
} from './arenaThemes';
import { posesAt, type BotPose } from './interpolate';

const directionStep: Record<Direction, [number, number]> = {
  North: [0, -1],
  East: [1, 0],
  South: [0, 1],
  West: [-1, 0],
};

const directionAngle: Record<Direction, number> = {
  North: -Math.PI / 2,
  East: 0,
  South: Math.PI / 2,
  West: Math.PI,
};

const projectileStep: Record<ProjectileHeading, [number, number]> = {
  ...directionStep,
  NorthEast: [1, -1],
  SouthEast: [1, 1],
  SouthWest: [-1, 1],
  NorthWest: [-1, -1],
};

const projectileAngle: Record<ProjectileHeading, number> = {
  ...directionAngle,
  NorthEast: -Math.PI / 4,
  SouthEast: Math.PI / 4,
  SouthWest: (3 * Math.PI) / 4,
  NorthWest: (-3 * Math.PI) / 4,
};

export interface DrawOptions {
  time: number;
  selectedSlot: number | null;
  showVisibility: boolean;
}

/** Pure canvas renderer: consumes replay data, never computes game rules (plan §32). */
export function drawArena(
  ctx: CanvasRenderingContext2D,
  replay: ReplayDocument,
  { time, selectedSlot, showVisibility }: DrawOptions,
  width: number,
  height: number,
): void {
  const { mapWidth, mapHeight, mapTiles, participants } = replay.header;
  const tile = Math.floor(Math.min(width / (mapWidth + 1), height / (mapHeight + 1)));
  const originX = Math.floor((width - tile * mapWidth) / 2);
  const originY = Math.floor((height - tile * mapHeight) / 2);
  const px = (x: number) => originX + x * tile;
  const py = (y: number) => originY + y * tile;

  ctx.clearRect(0, 0, width, height);

  const tickCount = replay.ticks.length;
  const tick = Math.min(Math.floor(Math.max(0, time)), tickCount - 1);
  const fraction = Math.max(0, Math.min(time - tick, 1));
  const currentTick = replay.ticks[tick];
  const poses = posesAt(replay, time);
  const theme = arenaTheme(replay.header.themeId);
  const lookFor = (slot: number) =>
    botLook(participants[slot]?.lookId, slot);
  const accentFor = (slot: number): string => {
    const participant = participants[slot];
    return presentationAccent(
      lookFor(slot),
      participant?.accent ?? '#38bdf8',
    );
  };

  // FOV mode: fog what the selected bot can't see, and ghost enemies it has no
  // sight of — the panel view answers "what did this bot know?", so an unseen
  // opponent rendered at full strength would be lying.
  const fogSource =
    showVisibility && selectedSlot !== null
      ? currentTick.bots.find((b) => b.slot === selectedSlot)
      : undefined;
  const hiddenByFog = (slot: number): boolean =>
    fogSource !== undefined &&
    slot !== selectedSlot &&
    !fogSource.visibleEnemies.some((e) => e.slot === slot);

  drawFloor();
  drawZone();
  if (replay.header.visionCone) drawVisionCones();
  drawWalls();
  if (showVisibility && selectedSlot !== null) drawFog(selectedSlot);
  drawProjectiles();
  drawHeardSounds();
  drawShadowsAndBots();
  drawShots();
  drawImpacts();

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
    if (!replay.header.zoneTiles) return;
    const pulse = 0.88 + Math.sin(time * Math.PI * 2) * 0.12;
    const zoneTiles = new Set(
      replay.header.zoneTiles.map(([x, y]) => `${x},${y}`),
    );
    const zoneShape = new Path2D();
    for (const [x, y] of replay.header.zoneTiles)
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
    for (const [x, y] of replay.header.zoneTiles) {
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

  function drawFog(slot: number): void {
    // Show the selected bot's field of view by FOGGING what it can NOT see.
    // Vision range 6 spans most of a small map, so tinting the visible tiles
    // read as "everything highlighted"; darkening the blind area reads at any size.
    const botTick = currentTick.bots.find((b) => b.slot === slot);
    if (!botTick) return;
    const visible = new Set(botTick.visibleTiles.map(([x, y]) => `${x},${y}`));
    ctx.fillStyle = 'rgba(4, 7, 12, 0.55)';
    for (let y = 0; y < mapHeight; y++)
      for (let x = 0; x < mapWidth; x++)
        if (!visible.has(`${x},${y}`)) ctx.fillRect(px(x), py(y), tile, tile);
  }

  function drawWalls(): void {
    const wallShape = new Path2D();
    for (let y = 0; y < mapHeight; y++) {
      for (let x = 0; x < mapWidth; x++)
        if (mapTiles[y][x] === '#')
          wallShape.rect(px(x), py(y), tile, tile);
    }

    if (theme.wallShadowTexture)
      drawWallTrimLayer(theme.wallShadowTexture);

    ctx.save();
    ctx.clip(wallShape);
    if (!drawTextureField(theme.wallTexture)) {
      ctx.fillStyle = '#2e3d55';
      ctx.fillRect(px(0), py(0), tile * mapWidth, tile * mapHeight);
    }
    ctx.fillStyle = theme.palette.wallTint;
    ctx.fillRect(px(0), py(0), tile * mapWidth, tile * mapHeight);
    ctx.restore();

    if (theme.wallTrimTexture)
      drawWallTrimLayer(theme.wallTrimTexture);
  }

  function drawWallTrimLayer(image: HTMLImageElement): void {
    if (!image.complete || image.naturalWidth === 0) return;
    const sourceWidth = image.naturalWidth;
    const sourceHeight = image.naturalHeight;
    const sourceInnerX = sourceWidth * theme.wall.sourceInner;
    const sourceInnerY = sourceHeight * theme.wall.sourceInner;
    const sourceCornerX = sourceWidth * theme.wall.sourceCorner;
    const sourceCornerY = sourceHeight * theme.wall.sourceCorner;
    const inset = tile * theme.wall.inset;
    const outset = tile * theme.wall.outset;
    const edgeSpan = inset + outset;

    const draw = (
      sourceX: number,
      sourceY: number,
      sourceW: number,
      sourceH: number,
      destinationX: number,
      destinationY: number,
      destinationW: number,
      destinationH: number,
    ) =>
      ctx.drawImage(
        image,
        sourceX,
        sourceY,
        sourceW,
        sourceH,
        destinationX,
        destinationY,
        destinationW,
        destinationH,
      );

    // The renderer contributes topology only. Every visible edge, corner,
    // side face, fastener, and shadow pixel comes from the theme's donor art.
    for (let y = 0; y < mapHeight; y++) {
      for (let x = 0; x < mapWidth; x++) {
        if (!wallAt(x, y)) continue;
        const north = !wallAt(x, y - 1);
        const east = !wallAt(x + 1, y);
        const south = !wallAt(x, y + 1);
        const west = !wallAt(x - 1, y);
        const left = px(x);
        const top = py(y);

        if (north)
          draw(
            sourceCornerX,
            0,
            sourceWidth - sourceCornerX * 2,
            sourceInnerY,
            left,
            top - outset,
            tile,
            edgeSpan,
          );
        if (east)
          draw(
            sourceWidth - sourceInnerX,
            sourceCornerY,
            sourceInnerX,
            sourceHeight - sourceCornerY * 2,
            left + tile - inset,
            top,
            edgeSpan,
            tile,
          );
        if (south)
          draw(
            sourceCornerX,
            sourceHeight - sourceInnerY,
            sourceWidth - sourceCornerX * 2,
            sourceInnerY,
            left,
            top + tile - inset,
            tile,
            edgeSpan,
          );
        if (west)
          draw(
            0,
            sourceCornerY,
            sourceInnerX,
            sourceHeight - sourceCornerY * 2,
            left - outset,
            top,
            edgeSpan,
            tile,
          );

        if (north && west)
          draw(
            0,
            0,
            sourceCornerX,
            sourceCornerY,
            left - outset,
            top - outset,
            edgeSpan,
            edgeSpan,
          );
        if (north && east)
          draw(
            sourceWidth - sourceCornerX,
            0,
            sourceCornerX,
            sourceCornerY,
            left + tile - inset,
            top - outset,
            edgeSpan,
            edgeSpan,
          );
        if (south && east)
          draw(
            sourceWidth - sourceCornerX,
            sourceHeight - sourceCornerY,
            sourceCornerX,
            sourceCornerY,
            left + tile - inset,
            top + tile - inset,
            edgeSpan,
            edgeSpan,
          );
        if (south && west)
          draw(
            0,
            sourceHeight - sourceCornerY,
            sourceCornerX,
            sourceCornerY,
            left - outset,
            top + tile - inset,
            edgeSpan,
            edgeSpan,
          );
      }
    }
  }

  function wallAt(x: number, y: number): boolean {
    if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight) return true;
    return mapTiles[y][x] === '#';
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

  function drawVisionCones(): void {
    // Directional sight (rules with cone vision): a faint 90° wedge in each active
    // bot's facing direction, in its accent — so "who is looking where, and who is in
    // whose blind arc" reads at a glance. The exact per-tile cone (wall-accurate) is
    // still available by selecting a bot with the field-of-view toggle.
    const radius = replay.header.visionRange * tile;
    for (const pose of poses) {
      if (pose.status !== 'Active') continue;
      const accent = accentFor(pose.slot);
      const cx = px(pose.x) + tile / 2;
      const cy = py(pose.y) + tile / 2;
      const gradient = ctx.createRadialGradient(cx, cy, tile * 0.4, cx, cy, radius);
      gradient.addColorStop(0, hexWithAlpha(accent, 0.08));
      gradient.addColorStop(1, hexWithAlpha(accent, 0));
      ctx.fillStyle = gradient;
      ctx.beginPath();
      ctx.moveTo(cx, cy);
      ctx.arc(cx, cy, radius, pose.angle - Math.PI / 4, pose.angle + Math.PI / 4);
      ctx.closePath();
      ctx.fill();
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
    const traversals = currentTick.projectileTraversals ?? [];
    const bolts = currentTick.projectiles ?? [];
    if (bolts.length === 0 && traversals.length === 0) return;
    // FOV mode stays honest: bolts the selected bot can't see aren't drawn at all
    // (an unseen bolt is precisely the threat it doesn't know about).
    const seen =
      fogSource !== undefined
        ? new Set(fogSource.visibleTiles.map(([x, y]) => `${x},${y}`))
        : null;
    const movingIds = new Set(traversals.map((move) => move.id));

    // Omniscient spectators see the locked future arc. A selected defender
    // sees only physically manifested segments; the owner authored the plan.
    const programmed = new Map<number, { ownerSlot: number; path: number[][] }>();
    for (const move of traversals)
      if (move.programmedPath)
        programmed.set(move.id, {
          ownerSlot: move.ownerSlot,
          path: move.programmedPath,
        });
    for (const bolt of bolts)
      if (bolt.programmedPath)
        programmed.set(bolt.id ?? 0, {
          ownerSlot: bolt.ownerSlot,
          path: bolt.programmedPath,
        });
    for (const plan of programmed.values()) {
      if (fogSource !== undefined && selectedSlot !== plan.ownerSlot) continue;
      const accent = accentFor(plan.ownerSlot);
      ctx.strokeStyle = hexWithAlpha(accent, 0.22);
      ctx.lineWidth = Math.max(1, tile * 0.045);
      ctx.setLineDash([Math.max(2, tile * 0.12), Math.max(2, tile * 0.1)]);
      ctx.beginPath();
      plan.path.forEach(([x, y], index) => {
        const cx = px(x) + tile / 2;
        const cy = py(y) + tile / 2;
        if (index === 0) ctx.moveTo(cx, cy);
        else ctx.lineTo(cx, cy);
      });
      ctx.stroke();
      ctx.setLineDash([]);
    }

    for (const move of traversals) {
      if (move.path.length === 0) continue;
      const points = [[move.fromX, move.fromY], ...move.path];
      const progress = fraction * move.path.length;
      const segment = Math.min(Math.floor(progress), move.path.length - 1);
      const local = Math.min(1, progress - segment);
      const [fromX, fromY] = points[segment];
      const [toX, toY] = points[segment + 1];
      drawBolt(
        fromX + (toX - fromX) * local,
        fromY + (toY - fromY) * local,
        headingBetween(fromX, fromY, toX, toY),
        move.ownerSlot,
        false,
        1,
      );
    }
    for (const bolt of bolts)
      if (!movingIds.has(bolt.id ?? 0))
        drawBolt(
          bolt.x,
          bolt.y,
          bolt.heading ?? bolt.direction,
          bolt.ownerSlot,
          bolt.ticksUntilAdvance === 1,
          bolt.tilesPerAdvance ?? 1,
        );

    function drawBolt(
      x: number,
      y: number,
      direction: ProjectileHeading,
      ownerSlot: number,
      imminent: boolean,
      tilesPerAdvance: number,
    ): void {
      if (seen !== null && !seen.has(`${Math.round(x)},${Math.round(y)}`)) return;
      const accent = accentFor(ownerSlot);
      const cx = px(x) + tile / 2;
      const cy = py(y) + tile / 2;
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
      ctx.strokeStyle = hexWithAlpha(accent, 0.85 * pulse);
      ctx.lineWidth = Math.max(2, tile * 0.08);
      ctx.beginPath();
      ctx.moveTo(-tile * 0.48, 0);
      ctx.lineTo(tile * 0.08, 0);
      ctx.stroke();
      ctx.fillStyle = `rgba(238, 249, 255, ${0.95 * pulse})`;
      ctx.beginPath();
      ctx.moveTo(tile * 0.28, 0);
      ctx.lineTo(-tile * 0.03, -tile * 0.11);
      ctx.lineTo(-tile * 0.03, tile * 0.11);
      ctx.closePath();
      ctx.fill();
      ctx.restore();
    }

    function headingBetween(
      fromX: number,
      fromY: number,
      toX: number,
      toY: number,
    ): ProjectileHeading {
      const dx = Math.sign(toX - fromX);
      const dy = Math.sign(toY - fromY);
      if (dx === 0 && dy < 0) return 'North';
      if (dx > 0 && dy < 0) return 'NorthEast';
      if (dx > 0 && dy === 0) return 'East';
      if (dx > 0 && dy > 0) return 'SouthEast';
      if (dx === 0 && dy > 0) return 'South';
      if (dx < 0 && dy > 0) return 'SouthWest';
      if (dx < 0 && dy === 0) return 'West';
      return 'NorthWest';
    }
  }

  function drawHeardSounds(): void {
    // Redacted hearing, made visible: the selected bot's heard sounds render as
    // neutral arcs on the bearing octant, at a radius keyed to the distance band.
    // Deliberately identity-free and coordinate-free — exactly what the bot knows.
    if (fogSource === undefined) return;
    const sounds = fogSource.heardSounds;
    if (!sounds || sounds.length === 0) return;
    const me = poses.find((p) => p.slot === fogSource.slot);
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
      if (pose.status !== 'Active' && time > (replay.result?.endTick ?? replay.ticks.length - 1) + 0.9) continue;
      if (hiddenByFog(pose.slot)) continue;
      drawShadow(pose);
    }
    for (const pose of poses) {
      drawBot(pose);
    }
  }

  function drawShadow(pose: BotPose): void {
    const cx = px(pose.x) + tile / 2;
    const cy = py(pose.y) + tile / 2 + tile * 0.2;
    const hover = pose.status === 'Active'
      ? Math.sin((time + pose.slot * 0.31) * Math.PI * 2) * tile * 0.018
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
    const participant = participants[pose.slot];
    const accent = accentFor(pose.slot);
    const look = botLook(participant?.lookId, pose.slot);
    const cx = px(pose.x) + tile / 2;
    const hover = pose.status === 'Active'
      ? Math.sin((time + pose.slot * 0.31) * Math.PI * 2) * tile * 0.022
      : 0;
    const cy = py(pose.y) + tile / 2 + hover;
    const radius = tile * 0.38;
    const destroyedNow = currentTick.events.some(
      (event) => event.type === 'Destroyed' && event.slot === pose.slot,
    );
    const destroyed = pose.status !== 'Active' || destroyedNow;
    const ghosted = hiddenByFog(pose.slot);
    const fired = currentTick.events.some(
      (event) => event.type === 'Shot' && event.slot === pose.slot,
    );
    const damaged = currentTick.events.some(
      (event) => event.type === 'Damage' && event.targetSlot === pose.slot,
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

    if (destroyed) {
      ctx.globalAlpha = 0.56 - destructionProgress * 0.2;
      ctx.rotate(pose.angle + destructionProgress * 0.55);
      const collapse = 1 - destructionProgress * 0.14;
      ctx.scale(collapse, collapse);
    } else {
      ctx.rotate(pose.angle);
    }
    if (ghosted) ctx.globalAlpha = 0.15; // true position, but the selected bot can't see it

    if (pose.slot === selectedSlot) {
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

    if (!destroyed && !ghosted) drawHealthPips(pose, cx, cy - radius - tile * 0.22, accent);
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

  function drawHealthPips(pose: BotPose, cx: number, cy: number, accent: string): void {
    const pip = Math.max(3, tile * 0.10);
    const gap = pip * 1.6;
    const total = 3;
    const startX = cx - ((total - 1) * gap) / 2;
    for (let i = 0; i < total; i++) {
      ctx.fillStyle = i < pose.health ? accent : 'rgba(100,116,139,0.35)';
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
    for (const event of currentTick.events) {
      if (event.type !== 'Shot') continue;
      const from = eventPoint(event.fromX, event.fromY);
      const to = eventPoint(event.toX, event.toY);
      if (!from || !to) continue;
      const accent = accentFor(event.slot ?? 0);
      const alpha = progress < 0.7 ? 0.95 : 0.95 * (1 - (progress - 0.7) / 0.3);
      const tipX = from.x + (to.x - from.x) * Math.min(progress / 0.7, 1);
      const tipY = from.y + (to.y - from.y) * Math.min(progress / 0.7, 1);
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.lineCap = 'round';
      ctx.shadowColor = accent;
      ctx.shadowBlur = Math.max(5, tile * 0.28);
      ctx.strokeStyle = hexWithAlpha(accent, alpha * 0.24);
      ctx.lineWidth = Math.max(7, tile * 0.25);
      drawBeam(from.x, from.y, tipX, tipY);
      ctx.strokeStyle = hexWithAlpha(accent, alpha * 0.9);
      ctx.lineWidth = Math.max(3, tile * 0.09);
      drawBeam(from.x, from.y, tipX, tipY);
      ctx.strokeStyle = `rgba(239, 250, 255, ${alpha})`;
      ctx.lineWidth = Math.max(1, tile * 0.025);
      drawBeam(from.x, from.y, tipX, tipY);
      // Muzzle glow.
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
    for (const event of currentTick.events) {
      if (event.type === 'Damage') {
        const at = eventPoint(event.fromX, event.fromY);
        if (!at) continue;
        const ownerAccent = accentFor(event.slot ?? 0);
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
      if (event.type === 'Destroyed') {
        const at = eventPoint(event.fromX, event.fromY);
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

  function eventPoint(x?: number, y?: number): { x: number; y: number } | null {
    if (x === undefined || y === undefined) return null;
    return { x: px(x) + tile / 2, y: py(y) + tile / 2 };
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
