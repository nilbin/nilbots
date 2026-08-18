import type { TacticalSheetCatalog } from '../api';
import { ZONE_OPTIONS } from './places';
import {
  mapRegionPresentation, nearestSpawnTeam, relationForPhysicalSide,
  relationForSpawn, relationForTeam, type MapTeamRelation, type MapViewSide,
} from './sheetMapPresentation';

export const OWN = '#22d3ee';
export const ENEMY = '#fb7185';
const NEUTRAL = '#8496a5';

export function relationColor(relation: MapTeamRelation): string {
  return relation === 'own' ? OWN : relation === 'enemy' ? ENEMY : NEUTRAL;
}

/**
 * The shared arena base layer, in tile units: checkered floor, walls, pads,
 * wells, heal tiles, reactors, spawn arrows, forbidden crosses. Extracted from
 * the retired MapPlanEditor so every tactics surface draws the same ground.
 */
export function BaseMap({ catalog, side = 'west', compact = false }: {
  catalog: TacticalSheetCatalog;
  side?: MapViewSide;
  compact?: boolean;
}) {
  const relationForPad = (point: { x: number; y: number }) => relationForTeam(
    nearestSpawnTeam(point, catalog.map.spawnAnchors), side,
  );
  return <>
    {catalog.map.tileRows.flatMap((row, y) => [...row].map((tile, x) => (
      <rect key={`tile:${x}:${y}`} x={x} y={y} width={1} height={1}
        fill={tile === '#' ? '#33404a' : (x + y) % 2 ? '#0b1319' : '#0f1820'}
        stroke="#17232c" strokeWidth={compact ? .025 : .035} />
    )))}
    {catalog.map.tileTags.filter((tag) => tag.kind === 'SpawnProtected').flatMap((tag) =>
      tag.tiles.map((point) => {
        const color = relationColor(relationForPad(point));
        return <rect key={`${tag.id}:${point.x}:${point.y}`} x={point.x + .05} y={point.y + .05}
          width={.9} height={.9} rx={.08} fill={`${color}13`} stroke={`${color}45`}
          strokeWidth={.055} strokeDasharray=".18 .12" />;
      }))}
    {catalog.map.regions.flatMap((region) => region.tiles.map((point) => {
      const presentation = mapRegionPresentation(region.id);
      const relation = relationForPhysicalSide(presentation.physicalSide, side);
      const color = presentation.kind === 'well' ? '#b49cff'
        : presentation.kind === 'heal' ? '#62d6a5' : relationColor(relation);
      const inset = presentation.kind === 'delivery' ? .08 : .14;
      return <rect key={`${region.id}:${point.x}:${point.y}`} x={point.x + inset} y={point.y + inset}
        width={1 - inset * 2} height={1 - inset * 2} rx={presentation.kind === 'delivery' ? .08 : .18}
        fill={`${color}${presentation.kind === 'delivery' ? '20' : '18'}`}
        stroke={`${color}${presentation.kind === 'delivery' ? '8a' : '55'}`}
        strokeWidth={presentation.kind === 'delivery' ? .075 : .05} />;
    }))}
    {!compact && catalog.map.regions.map((region) => {
      const presentation = mapRegionPresentation(region.id);
      if (presentation.kind === 'objective' || region.tiles.length === 0) return null;
      const cx = region.tiles.reduce((sum, point) => sum + point.x + .5, 0) / region.tiles.length;
      const cy = region.tiles.reduce((sum, point) => sum + point.y + .5, 0) / region.tiles.length;
      const color = presentation.kind === 'well' ? '#b49cff'
        : presentation.kind === 'heal' ? '#62d6a5'
          : relationColor(relationForPhysicalSide(presentation.physicalSide, side));
      return <g key={`landmark:${region.id}`} pointerEvents="none">
        <title>{presentation.label}</title>
        {presentation.kind === 'well' && <>
          <circle cx={cx} cy={cy} r={.42} fill="#33254f" stroke="#c8b2ff" strokeWidth={.1} />
          <circle cx={cx} cy={cy} r={.2} fill="#aa8dff" />
        </>}
        {presentation.kind === 'heal' && <>
          <rect x={cx - .36} y={cy - .36} width={.72} height={.72} rx={.16} fill="#173f36" stroke="#8ff0cd" strokeWidth={.09} />
          <path d={`M ${cx - .2} ${cy} H ${cx + .2} M ${cx} ${cy - .2} V ${cy + .2}`} stroke="#b9ffe6" strokeWidth={.13} />
        </>}
        {presentation.kind === 'reactor' && <>
          <path d={`M ${cx} ${cy - .43} L ${cx + .43} ${cy} L ${cx} ${cy + .43} L ${cx - .43} ${cy} Z`}
            fill="#080c10" stroke={color} strokeWidth={.11} />
          <circle cx={cx} cy={cy} r={.19} fill={color} />
        </>}
        {presentation.kind === 'delivery' && <>
          <circle cx={cx} cy={cy} r={.37} fill="#080c10" stroke={color} strokeWidth={.1} />
          <path d={`M ${cx} ${cy - .2} V ${cy + .13} M ${cx - .14} ${cy} L ${cx} ${cy + .16} L ${cx + .14} ${cy}`}
            fill="none" stroke={color} strokeWidth={.1} />
        </>}
      </g>;
    })}
    {!compact && catalog.map.spawnAnchors.map((spawn) => {
      const point = spawn.position;
      const color = relationColor(relationForSpawn(spawn.id, side));
      const east = side === 'east' ? spawn.facing !== 'East' : spawn.facing === 'East';
      const shape = east
        ? `M ${point.x + .18} ${point.y + .18} L ${point.x + .82} ${point.y + .5} L ${point.x + .18} ${point.y + .82} Z`
        : `M ${point.x + .82} ${point.y + .18} L ${point.x + .18} ${point.y + .5} L ${point.x + .82} ${point.y + .82} Z`;
      return <path key={spawn.id} d={shape} fill={color} stroke="#071017" strokeWidth={.055} />;
    })}
    {!compact && catalog.map.tileTags.filter((tag) => tag.kind === 'SignaturePlacementForbidden').flatMap((tag) =>
      tag.tiles.map((point) => (
        <path key={`${tag.id}:${point.x}:${point.y}`}
          d={`M ${point.x + .69} ${point.y + .1} L ${point.x + .9} ${point.y + .31} M ${point.x + .9} ${point.y + .1} L ${point.x + .69} ${point.y + .31}`}
          stroke="#dc8a4d" strokeWidth={.075} />
      )))}
    {!compact && <MapPlaceLabels catalog={catalog} side={side} />}
  </>;
}

/** Stroked place names drawn into the ground — MID and the three Wells in the
 * player's own language — so the drawer's readback ("hold ground at Mid",
 * "their Well") has something on the map to point at. */
function MapPlaceLabels({ catalog, side }: {
  catalog: TacticalSheetCatalog;
  side: MapViewSide;
}) {
  const height = catalog.map.height;
  const labels: { key: string; text: string; x: number; y: number; size: number }[] = [];
  const mid = ZONE_OPTIONS.find((zone) => zone.id === 'mid');
  if (mid) {
    labels.push({
      key: 'mid', text: 'MID', size: .62,
      x: (mid.rect[0] + mid.rect[2] + 1) / 2, y: mid.rect[1] - .65,
    });
  }
  const ownWell = side === 'west' ? 'well-north' : 'well-south';
  for (const region of catalog.map.regions) {
    if (!region.id.startsWith('well-') || region.tiles.length === 0) continue;
    const cx = region.tiles.reduce((sum, point) => sum + point.x + .5, 0) / region.tiles.length;
    const cy = region.tiles.reduce((sum, point) => sum + point.y + .5, 0) / region.tiles.length;
    const text = region.id === 'well-centre' ? 'CENTRE WELL'
      : region.id === ownWell ? 'YOUR WELL' : 'THEIR WELL';
    labels.push({
      key: region.id, text,
      size: region.id === 'well-centre' ? .44 : .5,
      // Label above a well in the top half, below otherwise, clear of MID.
      x: cx, y: cy < height / 2 ? cy - 1.95 : cy + 2.1,
    });
  }
  return <>
    {labels.map((label) => (
      <text key={label.key} x={label.x} y={label.y} textAnchor="middle"
        fontSize={label.size} fontWeight={600} letterSpacing=".06"
        style={{ fontStretch: '82%' }} fill="#7694a8" opacity=".8"
        paintOrder="stroke" stroke="#06090c" strokeWidth={.12}
        pointerEvents="none">{label.text}</text>
    ))}
  </>;
}
