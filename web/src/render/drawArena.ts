import type { ReplayDocument } from '../types';
import { posesAt, type BotPose } from './interpolate';

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

  drawFloor();
  if (showVisibility && selectedSlot !== null) drawVisibility(selectedSlot);
  drawWalls();
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

  function drawVisibility(slot: number): void {
    const botTick = currentTick.bots.find((b) => b.slot === slot);
    if (!botTick) return;
    const accent = participants[slot]?.accent ?? '#38bdf8';
    ctx.fillStyle = hexWithAlpha(accent, 0.10);
    for (const [x, y] of botTick.visibleTiles) {
      ctx.fillRect(px(x), py(y), tile, tile);
    }
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

  function drawShadowsAndBots(): void {
    for (const pose of poses) {
      if (pose.status !== 'Active' && time > replay.result.endTick + 0.9) continue;
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

    ctx.save();
    ctx.translate(cx, cy);

    if (destroyed) {
      ctx.globalAlpha = 0.45;
      ctx.rotate(pose.angle + 0.6);
    } else {
      ctx.rotate(pose.angle);
    }

    if (pose.slot === selectedSlot) {
      ctx.strokeStyle = hexWithAlpha(accent, 0.9);
      ctx.lineWidth = 2;
      ctx.setLineDash([4, 3]);
      ctx.beginPath();
      ctx.arc(0, 0, radius + 5, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    // Body: rounded chassis with an accent ring and a facing wedge.
    ctx.fillStyle = '#232f42';
    ctx.beginPath();
    ctx.arc(0, 0, radius, 0, Math.PI * 2);
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

    if (!destroyed) drawHealthPips(pose, cx, cy - radius - tile * 0.22, accent);
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

function hexWithAlpha(hex: string, alpha: number): string {
  const match = /^#([0-9a-f]{6})$/i.exec(hex);
  if (!match) return hex;
  const value = parseInt(match[1], 16);
  const r = (value >> 16) & 0xff;
  const g = (value >> 8) & 0xff;
  const b = value & 0xff;
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}
