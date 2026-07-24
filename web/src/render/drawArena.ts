import type { Direction, ProjectileHeading, ReplayDocument } from '../types';
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
    for (let y = 0; y < mapHeight; y++) {
      for (let x = 0; x < mapWidth; x++) {
        if (mapTiles[y][x] === '#') continue;
        // Subtle checkerboard so movement reads clearly.
        ctx.fillStyle = (x + y) % 2 === 0 ? '#101722' : '#0e141e';
        ctx.fillRect(px(x), py(y), tile, tile);
      }
    }
    ctx.strokeStyle = 'rgba(72, 96, 128, 0.08)';
    ctx.lineWidth = 1;
    for (let x = 0; x <= mapWidth; x++) {
      ctx.beginPath();
      ctx.moveTo(px(x) + 0.5, py(0));
      ctx.lineTo(px(x) + 0.5, py(mapHeight));
      ctx.stroke();
    }
    for (let y = 0; y <= mapHeight; y++) {
      ctx.beginPath();
      ctx.moveTo(px(0), py(y) + 0.5);
      ctx.lineTo(px(mapWidth), py(y) + 0.5);
      ctx.stroke();
    }
  }

  function drawZone(): void {
    // Zone-control tiles (experiment arms): the contested objective, kept subtle so
    // bots and beams stay readable on top of it.
    if (!replay.header.zoneTiles) return;
    for (const [x, y] of replay.header.zoneTiles) {
      ctx.fillStyle = 'rgba(250, 204, 21, 0.10)';
      ctx.fillRect(px(x), py(y), tile, tile);
      ctx.strokeStyle = 'rgba(250, 204, 21, 0.35)';
      ctx.lineWidth = 1;
      ctx.setLineDash([3, 3]);
      ctx.strokeRect(px(x) + 1.5, py(y) + 1.5, tile - 3, tile - 3);
      ctx.setLineDash([]);
    }
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
    for (let y = 0; y < mapHeight; y++) {
      for (let x = 0; x < mapWidth; x++) {
        if (mapTiles[y][x] !== '#') continue;
        ctx.fillStyle = '#2e3d55';
        ctx.fillRect(px(x), py(y), tile, tile);
        // Top-lit bevel keeps walls readable without visual noise.
        ctx.fillStyle = '#42557a';
        ctx.fillRect(px(x), py(y), tile, Math.max(2, tile * 0.14));
        ctx.fillStyle = '#1a2333';
        ctx.fillRect(px(x), py(y) + tile - Math.max(2, tile * 0.14), tile, Math.max(2, tile * 0.14));
        ctx.strokeStyle = 'rgba(10, 14, 20, 0.8)';
        ctx.lineWidth = 1;
        ctx.strokeRect(px(x) + 0.5, py(y) + 0.5, tile - 1, tile - 1);
      }
    }
  }

  function drawVisionCones(): void {
    // Directional sight (rules with cone vision): a faint 90° wedge in each active
    // bot's facing direction, in its accent — so "who is looking where, and who is in
    // whose blind arc" reads at a glance. The exact per-tile cone (wall-accurate) is
    // still available by selecting a bot with the field-of-view toggle.
    const radius = replay.header.visionRange * tile;
    for (const pose of poses) {
      if (pose.status !== 'Active') continue;
      const accent = participants[pose.slot]?.accent ?? '#38bdf8';
      const cx = px(pose.x) + tile / 2;
      const cy = py(pose.y) + tile / 2;
      const gradient = ctx.createRadialGradient(cx, cy, tile * 0.4, cx, cy, radius);
      gradient.addColorStop(0, hexWithAlpha(accent, 0.16));
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
      ctx.fillStyle = hexWithAlpha(accent, 0.10);
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
      const accent = participants[plan.ownerSlot]?.accent ?? '#38bdf8';
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
      const accent = participants[ownerSlot]?.accent ?? '#38bdf8';
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
      ctx.strokeStyle = hexWithAlpha(accent, (imminent ? 0.55 : 0.35) * pulse);
      ctx.lineWidth = Math.max(2, tile * 0.08);
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(-tile * 0.42, 0);
      ctx.lineTo(-tile * 0.1, 0);
      ctx.stroke();
      ctx.fillStyle = hexWithAlpha(accent, 0.95 * pulse);
      ctx.beginPath();
      ctx.moveTo(tile * 0.22, 0);
      ctx.lineTo(-tile * 0.1, -tile * 0.12);
      ctx.lineTo(-tile * 0.1, tile * 0.12);
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
    const cy = py(pose.y) + tile / 2 + tile * 0.18;
    ctx.fillStyle = 'rgba(0,0,0,0.35)';
    ctx.beginPath();
    ctx.ellipse(cx, cy, tile * 0.32, tile * 0.16, 0, 0, Math.PI * 2);
    ctx.fill();
  }

  function drawBot(pose: BotPose): void {
    const participant = participants[pose.slot];
    const accent = participant?.accent ?? '#38bdf8';
    const cx = px(pose.x) + tile / 2;
    const cy = py(pose.y) + tile / 2;
    const radius = tile * 0.34;
    const destroyed = pose.status !== 'Active';
    const ghosted = hiddenByFog(pose.slot);

    ctx.save();
    ctx.translate(cx, cy);

    if (destroyed) {
      ctx.globalAlpha = 0.45;
      ctx.rotate(pose.angle + 0.6);
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

    // Chassis variant derived from the bot's name: recognizable identity (§33.3-lite).
    const variant = nameHash(participant?.name ?? '');
    ctx.fillStyle = '#232f42';
    ctx.beginPath();
    const sides = [0, 6, 8][variant % 3];
    if (sides === 0) ctx.arc(0, 0, radius, 0, Math.PI * 2);
    else
      for (let i = 0; i <= sides; i++) {
        const a = (i / sides) * Math.PI * 2 + Math.PI / sides;
        ctx[i === 0 ? 'moveTo' : 'lineTo'](Math.cos(a) * radius, Math.sin(a) * radius);
      }
    ctx.closePath();
    ctx.fill();
    ctx.lineWidth = Math.max(2, tile * 0.06);
    ctx.strokeStyle = accent;
    ctx.stroke();
    if (variant & 4) {
      // Antenna.
      ctx.beginPath();
      ctx.moveTo(-radius * 0.9, 0);
      ctx.lineTo(-radius * 1.35, 0);
      ctx.stroke();
      ctx.fillStyle = accent;
      ctx.beginPath();
      ctx.arc(-radius * 1.35, 0, radius * 0.14, 0, Math.PI * 2);
      ctx.fill();
    }

    ctx.fillStyle = accent;
    ctx.beginPath();
    ctx.moveTo(radius * 1.05, 0);
    ctx.lineTo(radius * 0.25, -radius * 0.55);
    ctx.lineTo(radius * 0.25, radius * 0.55);
    ctx.closePath();
    ctx.fill();

    // Visor.
    ctx.fillStyle = '#0a0e14';
    ctx.beginPath();
    ctx.arc(radius * 0.1, 0, radius * 0.28, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = destroyed ? '#475569' : '#e2f3ff';
    ctx.beginPath();
    ctx.arc(radius * 0.18, 0, radius * 0.12, 0, Math.PI * 2);
    ctx.fill();

    ctx.restore();

    if (!destroyed && !ghosted) drawHealthPips(pose, cx, cy - radius - tile * 0.22, accent);
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
      const accent = participants[event.slot ?? 0]?.accent ?? '#38bdf8';
      const alpha = progress < 0.7 ? 0.95 : 0.95 * (1 - (progress - 0.7) / 0.3);
      ctx.strokeStyle = hexWithAlpha(accent, alpha);
      ctx.lineWidth = Math.max(2, tile * 0.09);
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(from.x, from.y);
      const tipX = from.x + (to.x - from.x) * Math.min(progress / 0.7, 1);
      const tipY = from.y + (to.y - from.y) * Math.min(progress / 0.7, 1);
      ctx.lineTo(tipX, tipY);
      ctx.stroke();
      // Muzzle glow.
      ctx.fillStyle = hexWithAlpha(accent, alpha * 0.8);
      ctx.beginPath();
      ctx.arc(from.x, from.y, tile * 0.12, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  function drawImpacts(): void {
    const progress = shotProgress();
    if (progress < 0.6) return;
    const flash = (progress - 0.6) / 0.4;
    for (const event of currentTick.events) {
      if (event.type === 'Damage') {
        const at = eventPoint(event.fromX, event.fromY);
        if (!at) continue;
        ctx.strokeStyle = `rgba(248, 113, 113, ${0.9 * (1 - flash)})`;
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(at.x, at.y, tile * (0.25 + flash * 0.3), 0, Math.PI * 2);
        ctx.stroke();
      }
      if (event.type === 'Destroyed') {
        const at = eventPoint(event.fromX, event.fromY);
        if (!at) continue;
        for (let i = 0; i < 6; i++) {
          const angle = (i / 6) * Math.PI * 2 + flash * 1.2;
          const distance = tile * (0.2 + flash * 0.55);
          ctx.fillStyle = `rgba(251, 191, 36, ${0.85 * (1 - flash)})`;
          ctx.beginPath();
          ctx.arc(
            at.x + Math.cos(angle) * distance,
            at.y + Math.sin(angle) * distance,
            tile * 0.07,
            0,
            Math.PI * 2,
          );
          ctx.fill();
        }
      }
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
