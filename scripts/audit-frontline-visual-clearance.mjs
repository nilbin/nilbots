#!/usr/bin/env node

import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const repository = resolve(import.meta.dirname, '..');
const mapPath = resolve(repository, 'maps/experimental/frontline-01.json');
const map = JSON.parse(await readFile(mapPath, 'utf8'));
const rows = map.tiles;

const BOT_VISUAL_SPAN = 1.12;
const BOT_RADIUS = BOT_VISUAL_SPAN / 2;
const SAFETY = 0.02;
const CURRENT_BODY_OUTSET = 0.055;
const atlas = { contentPixels: 192, gutterPixels: 32 };
const currentCapSpan =
  1 + (atlas.gutterPixels / atlas.contentPixels) * 2 - CURRENT_BODY_OUTSET * 2;
const CURRENT_CAP_OUTSET = (currentCapSpan - 1) / 2;
const PROPOSED_BODY_INSET = BOT_RADIUS + SAFETY - 0.5;

const walls = [];
const open = [];
for (let y = 0; y < map.height; y += 1) {
  for (let x = 0; x < map.width; x += 1) {
    (rows[y][x] === '#' ? walls : open).push({ x, y });
  }
}

const isOpen = (x, y) =>
  x >= 0 &&
  y >= 0 &&
  x < map.width &&
  y < map.height &&
  rows[y][x] !== '#';
const isWall = (x, y) =>
  x < 0 ||
  y < 0 ||
  x >= map.width ||
  y >= map.height ||
  rows[y][x] === '#';

const tileCentre = ({ x, y }) => ({ x: x + 0.5, y: y + 0.5 });
const wallRect = ({ x, y }, inset) => ({
  minX: x + inset,
  minY: y + inset,
  maxX: x + 1 - inset,
  maxY: y + 1 - inset,
});

function pointRectDistance(point, rect) {
  const dx = Math.max(rect.minX - point.x, 0, point.x - rect.maxX);
  const dy = Math.max(rect.minY - point.y, 0, point.y - rect.maxY);
  return Math.hypot(dx, dy);
}

function pointSegmentDistance(point, from, to) {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  const lengthSquared = dx * dx + dy * dy;
  if (lengthSquared === 0) return Math.hypot(point.x - from.x, point.y - from.y);
  const t = Math.max(
    0,
    Math.min(
      1,
      ((point.x - from.x) * dx + (point.y - from.y) * dy) /
        lengthSquared,
    ),
  );
  return Math.hypot(point.x - (from.x + t * dx), point.y - (from.y + t * dy));
}

const cross = (a, b, c) =>
  (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
const between = (value, a, b) =>
  value >= Math.min(a, b) - 1e-9 && value <= Math.max(a, b) + 1e-9;
function segmentsIntersect(a, b, c, d) {
  const abC = cross(a, b, c);
  const abD = cross(a, b, d);
  const cdA = cross(c, d, a);
  const cdB = cross(c, d, b);
  if (
    ((abC > 0 && abD < 0) || (abC < 0 && abD > 0)) &&
    ((cdA > 0 && cdB < 0) || (cdA < 0 && cdB > 0))
  )
    return true;
  return (
    (Math.abs(abC) <= 1e-9 &&
      between(c.x, a.x, b.x) &&
      between(c.y, a.y, b.y)) ||
    (Math.abs(abD) <= 1e-9 &&
      between(d.x, a.x, b.x) &&
      between(d.y, a.y, b.y)) ||
    (Math.abs(cdA) <= 1e-9 &&
      between(a.x, c.x, d.x) &&
      between(a.y, c.y, d.y)) ||
    (Math.abs(cdB) <= 1e-9 &&
      between(b.x, c.x, d.x) &&
      between(b.y, c.y, d.y))
  );
}

function segmentRectDistance(from, to, rect) {
  if (
    pointRectDistance(from, rect) === 0 ||
    pointRectDistance(to, rect) === 0
  )
    return 0;
  const corners = [
    { x: rect.minX, y: rect.minY },
    { x: rect.maxX, y: rect.minY },
    { x: rect.maxX, y: rect.maxY },
    { x: rect.minX, y: rect.maxY },
  ];
  let distance = Math.min(
    pointRectDistance(from, rect),
    pointRectDistance(to, rect),
  );
  for (let index = 0; index < corners.length; index += 1) {
    const edgeFrom = corners[index];
    const edgeTo = corners[(index + 1) % corners.length];
    if (segmentsIntersect(from, to, edgeFrom, edgeTo)) return 0;
    distance = Math.min(
      distance,
      pointSegmentDistance(edgeFrom, from, to),
      pointSegmentDistance(edgeTo, from, to),
      pointSegmentDistance(from, edgeFrom, edgeTo),
      pointSegmentDistance(to, edgeFrom, edgeTo),
    );
  }
  return distance;
}

function minimumForPoints(inset) {
  let result = { distance: Infinity, open: null, wall: null };
  for (const tile of open) {
    const point = tileCentre(tile);
    for (const wall of walls) {
      const distance = pointRectDistance(point, wallRect(wall, inset));
      if (distance < result.distance)
        result = { distance, open: tile, wall };
    }
  }
  return result;
}

const moves = [];
const directions = [
  { x: 1, y: 0, kind: 'cardinal' },
  { x: 0, y: 1, kind: 'cardinal' },
  { x: 1, y: 1, kind: 'strict-diagonal' },
  { x: 1, y: -1, kind: 'strict-diagonal' },
];
for (const tile of open) {
  for (const direction of directions) {
    const to = { x: tile.x + direction.x, y: tile.y + direction.y };
    if (!isOpen(to.x, to.y)) continue;
    if (
      direction.kind === 'strict-diagonal' &&
      (!isOpen(tile.x + direction.x, tile.y) ||
        !isOpen(tile.x, tile.y + direction.y))
    )
      continue;
    moves.push({ from: tile, to, kind: direction.kind });
  }
}

function minimumForMoves(inset) {
  let result = { distance: Infinity, move: null, wall: null };
  for (const move of moves) {
    const from = tileCentre(move.from);
    const to = tileCentre(move.to);
    for (const wall of walls) {
      const distance = segmentRectDistance(from, to, wallRect(wall, inset));
      if (distance < result.distance)
        result = { distance, move, wall };
    }
  }
  return result;
}

const oneTileCorridors = open.filter(
  ({ x, y }) =>
    (isWall(x - 1, y) && isWall(x + 1, y)) ||
    (isWall(x, y - 1) && isWall(x, y + 1)),
);

const authoredSpecialTiles = new Set([
  ...map.spawns.map(({ x, y }) => `${x},${y}`),
  ...map.frontline.positions.flatMap(({ tiles }) =>
    tiles.map(([x, y]) => `${x},${y}`),
  ),
  ...map.frontline.homePads.flatMap(({ tiles }) =>
    tiles.map(([x, y]) => `${x},${y}`),
  ),
]);
const specialMinimum = [...authoredSpecialTiles].reduce(
  (best, key) => {
    const [x, y] = key.split(',').map(Number);
    const point = { x: x + 0.5, y: y + 0.5 };
    for (const wall of walls) {
      const distance = pointRectDistance(point, wallRect(wall, 0));
      if (distance < best.distance)
        best = { distance, tile: { x, y }, wall };
    }
    return best;
  },
  { distance: Infinity, tile: null, wall: null },
);

const round = (value) => Number(value.toFixed(6));
const audit = {
  map: 'maps/experimental/frontline-01.json',
  dimensions: { width: map.width, height: map.height },
  authority: {
    openTileCount: open.length,
    wallTileCount: walls.length,
    legalCardinalSegments: moves.filter(({ kind }) => kind === 'cardinal').length,
    legalStrictDiagonalSegments: moves.filter(
      ({ kind }) => kind === 'strict-diagonal',
    ).length,
    oneTileCorridorCount: oneTileCorridors.length,
    oneTileCorridorExamples: oneTileCorridors.slice(0, 12),
  },
  visual: {
    botSpan: BOT_VISUAL_SPAN,
    botRadius: BOT_RADIUS,
    requestedSafety: SAFETY,
    currentBodyOutset: CURRENT_BODY_OUTSET,
    currentCapOutset: round(CURRENT_CAP_OUTSET),
    proposedBodyInset: round(PROPOSED_BODY_INSET),
  },
  centreClearance: {
    authoritative: summarize(minimumForPoints(0)),
    currentBody: summarize(minimumForPoints(-CURRENT_BODY_OUTSET)),
    currentCap: summarize(minimumForPoints(-CURRENT_CAP_OUTSET)),
    proposedBody: summarize(minimumForPoints(PROPOSED_BODY_INSET)),
  },
  movementClearance: {
    authoritative: summarize(minimumForMoves(0)),
    currentBody: summarize(minimumForMoves(-CURRENT_BODY_OUTSET)),
    currentCap: summarize(minimumForMoves(-CURRENT_CAP_OUTSET)),
    proposedBody: summarize(minimumForMoves(PROPOSED_BODY_INSET)),
  },
  specialTileMinimum: summarize(specialMinimum),
};

function summarize(result) {
  return {
    ...result,
    distance: round(result.distance),
    signedBotMargin: round(result.distance - BOT_RADIUS),
  };
}

process.stdout.write(`${JSON.stringify(audit, null, 2)}\n`);
