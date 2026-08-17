import {
  useEffect,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type PointerEvent as ReactPointerEvent,
} from 'react';
import clsx from 'clsx';
import type { TacticalSheetCatalog } from '../api';
import { BaseMap, type MapPreviewSide } from './MapPlanEditor';
import type { MapPlanDocument, Point, RoleDefinition } from './sheetAuthoring';
import type { UnitTrack } from './sheetTrials';
import type { LinkedPlay } from './workshopTypes';
import { playSpatialGuides } from './playGeometry';

const OWN = '#22d3ee';
const ENEMY = '#fb7185';
const PLAY = '#f0a35e';
const DERIVED = '#b49cff';

export default function PlayMapCanvas({
  catalog,
  mapPlan,
  roles,
  play,
  side,
  selectedId,
  startDrawingId,
  tracks = [],
  onSelect,
  onMoveAnchor,
  onSetZone,
  onAppendRoute,
  onNudge,
  onDrawingStarted,
}: {
  catalog: TacticalSheetCatalog;
  mapPlan: MapPlanDocument;
  roles: RoleDefinition[];
  play: LinkedPlay;
  side: MapPreviewSide;
  selectedId: string | null;
  startDrawingId?: string | null;
  tracks?: UnitTrack[];
  onSelect: (id: string) => void;
  onMoveAnchor: (id: string, point: [number, number]) => void;
  onSetZone: (id: string, rect: [number, number, number, number]) => void;
  onAppendRoute: (id: string, point: [number, number]) => void;
  onNudge: (id: string, dx: number, dy: number) => void;
  onDrawingStarted?: () => void;
}) {
  const svg = useRef<SVGSVGElement>(null);
  const [dragging, setDragging] = useState<string | null>(null);
  const [drawing, setDrawing] = useState(false);
  const [zoneStart, setZoneStart] = useState<[number, number] | null>(null);
  const [zonePreview, setZonePreview] = useState<[number, number, number, number] | null>(null);
  const [zoom, setZoom] = useState<1 | 1.5 | 2>(1);
  const owned = new Map(play.ownedGeometry.map((entry) => [entry.id, entry]));
  const selected = selectedId ? owned.get(selectedId) : undefined;
  const guides = playSpatialGuides({
    localId: '', name: '', sheetId: null, revision: null, enterLadder: false,
    orders: { schema: 'arc-relay-orders-v2', sheetId: '', mapPlan: '',
      roles, sharedOrders: {}, capacities: {}, conditions: {} },
    mapPlan,
    workshop: { schema: 'arc-relay-workshop-v1', plays: [play] },
  }, play.id, catalog);
  useEffect(() => {
    if (!startDrawingId || startDrawingId !== selectedId
      || side === 'east' || !selected || selected.derivedFrom)
      return;
    setDrawing(true);
    onDrawingStarted?.();
  }, [onDrawingStarted, selected, selectedId, side, startDrawingId]);
  const transform = (point: Point): Point => side === 'east'
    ? { x: catalog.map.width - 1 - point.x, y: catalog.map.height - 1 - point.y }
    : point;
  const sourcePoint = (event: ReactPointerEvent<SVGSVGElement>): [number, number] | null => {
    const bounds = svg.current?.getBoundingClientRect();
    if (!bounds) return null;
    let x = clamp(Math.floor((event.clientX - bounds.left) / bounds.width * catalog.map.width), 0, catalog.map.width - 1);
    let y = clamp(Math.floor((event.clientY - bounds.top) / bounds.height * catalog.map.height), 0, catalog.map.height - 1);
    if (side === 'east') {
      x = catalog.map.width - 1 - x;
      y = catalog.map.height - 1 - y;
    }
    return [x, y];
  };
  const finishDrag = (event: ReactPointerEvent<SVGSVGElement>) => {
    if (zoneStart && zonePreview && selected?.kind === 'zone') {
      onSetZone(selected.id, zonePreview);
      setZoneStart(null);
      setZonePreview(null);
    }
    if (!dragging) return;
    const point = sourcePoint(event);
    if (point) onMoveAnchor(dragging, point);
    if (event.currentTarget.hasPointerCapture(event.pointerId))
      event.currentTarget.releasePointerCapture(event.pointerId);
    setDragging(null);
  };

  const beginDraw = (event: ReactPointerEvent<SVGSVGElement>) => {
    if (!drawing || side === 'east' || !selected || selected.derivedFrom)
      return;
    const point = sourcePoint(event);
    if (!point) return;
    event.preventDefault();
    if (selected.kind === 'anchor') onMoveAnchor(selected.id, point);
    else if (selected.kind === 'route') onAppendRoute(selected.id, point);
    else {
      event.currentTarget.setPointerCapture(event.pointerId);
      setZoneStart(point);
      setZonePreview([point[0], point[1], point[0], point[1]]);
    }
  };

  const nudge = (event: ReactKeyboardEvent<SVGSVGElement>) => {
    const movement: Record<string, [number, number]> = {
      ArrowLeft: [-1, 0], ArrowRight: [1, 0],
      ArrowUp: [0, -1], ArrowDown: [0, 1],
    };
    const delta = movement[event.key];
    if (!delta || !selected || selected.derivedFrom || side === 'east') return;
    event.preventDefault();
    onNudge(selected.id, delta[0], delta[1]);
  };

  return <div className="overflow-hidden rounded-sm border border-arena-edge bg-[#06090c]">
    <div className="flex flex-wrap items-center gap-2 border-b border-arena-edge p-2">
      <button type="button" className={clsx('btn min-h-11', !drawing && 'btn-on')}
        aria-pressed={!drawing} onClick={() => setDrawing(false)}>Select</button>
      <button type="button" className={clsx('btn min-h-11', drawing && 'btn-secondary')}
        aria-pressed={drawing} disabled={!selected || Boolean(selected.derivedFrom) || side === 'east'}
        onClick={() => setDrawing(true)}>
        {selected?.kind === 'zone' ? 'Draw rectangle'
          : selected?.kind === 'route' ? 'Add route points'
            : selected?.kind === 'anchor' ? 'Place point' : 'Choose an object'}
      </button>
      {drawing && selected?.kind === 'route' && <span className="t-micro">Tap in travel order. Each tap adds a visible waypoint.</span>}
      {drawing && selected?.kind === 'zone' && <span className="t-micro">Drag over the area that matters.</span>}
      <div className="ml-auto flex rounded-sm border border-arena-edge p-1" aria-label="Map zoom">
        {([1, 1.5, 2] as const).map((value) => <button key={value} type="button"
          className={clsx('btn min-h-9 min-w-11 border-0', zoom === value && 'btn-on')}
          aria-pressed={zoom === value} onClick={() => setZoom(value)}>
          {value === 1 ? 'Fit' : `${value}×`}
        </button>)}
      </div>
    </div>
    <div className="max-w-full overflow-auto overscroll-contain" data-testid="program-map-viewport">
    <svg ref={svg} viewBox={`0 0 ${catalog.map.width} ${catalog.map.height}`}
      className={clsx('block max-w-none select-none', (dragging || drawing) && 'touch-none')}
      style={{ aspectRatio: `${catalog.map.width}/${catalog.map.height}`, width: `${zoom * 100}%` }}
      role="img" tabIndex={0}
      aria-label={`${play.name} on the ${side} Tapline perspective. Arrow keys move the selected Program object.`}
      onKeyDown={nudge}
      onPointerMove={(event) => {
        if (zoneStart && selected?.kind === 'zone') {
          const point = sourcePoint(event);
          if (point) setZonePreview([
            Math.min(zoneStart[0], point[0]), Math.min(zoneStart[1], point[1]),
            Math.max(zoneStart[0], point[0]), Math.max(zoneStart[1], point[1]),
          ]);
        }
        if (!dragging || side === 'east') return;
        event.preventDefault();
      }}
      onPointerDown={beginDraw} onPointerUp={finishDrag} onPointerCancel={finishDrag}>
      <BaseMap catalog={catalog} side={side} transform={transform} />
      {guides.map((guide) => {
        const sourceRaw = mapPlan.anchors[guide.sourceAnchorId];
        const targetRaw = mapPlan.anchors[guide.targetAnchorId];
        if (!sourceRaw || !targetRaw) return null;
        const source = transform({ x: sourceRaw[0], y: sourceRaw[1] });
        const target = transform({ x: targetRaw[0], y: targetRaw[1] });
        const color = guide.valid ? '#f0a35e' : '#fb7185';
        return <g key={guide.id} pointerEvents="none">
          {guide.range !== undefined && <circle cx={source.x + .5} cy={source.y + .5}
            r={guide.range + .5} fill="none" stroke={color} strokeWidth={.06}
            strokeDasharray=".24 .2" opacity={.55} />}
          <line x1={source.x + .5} y1={source.y + .5}
            x2={target.x + .5} y2={target.y + .5}
            stroke={color} strokeWidth={.15} strokeDasharray={guide.valid ? undefined : '.25 .16'} />
          <rect x={target.x + .08} y={target.y + .08} width={.84} height={.84}
            rx={.12} fill={`${color}22`} stroke={color} strokeWidth={.12} />
          <title>{guide.label} · {guide.distance} tiles{guide.range ? ` / range ${guide.range}` : ''} · line {guide.lineOfSight}</title>
        </g>;
      })}
      {play.slots.filter((slot) => slot.facingAnchorId).map((slot) => {
        const postRaw = mapPlan.anchors[slot.anchorId];
        const facingRaw = mapPlan.anchors[slot.facingAnchorId!];
        if (!postRaw || !facingRaw) return null;
        const post = transform({ x: postRaw[0], y: postRaw[1] });
        const facing = transform({ x: facingRaw[0], y: facingRaw[1] });
        return <g key={`${slot.id}:facing`} pointerEvents="none">
          <line x1={post.x + .5} y1={post.y + .5} x2={facing.x + .5} y2={facing.y + .5}
            stroke="#fff1d8" strokeWidth={.13} />
          <circle cx={facing.x + .5} cy={facing.y + .5} r={.18} fill="#fff1d8" />
        </g>;
      })}
      {zonePreview && (() => {
        const a = transform({ x: zonePreview[0], y: zonePreview[1] });
        const b = transform({ x: zonePreview[2], y: zonePreview[3] });
        return <rect x={Math.min(a.x, b.x)} y={Math.min(a.y, b.y)}
          width={Math.abs(a.x - b.x) + 1} height={Math.abs(a.y - b.y) + 1}
          fill="#f0a35e35" stroke="#fff1d8" strokeWidth={.18} pointerEvents="none" />;
      })()}
      {Object.entries(mapPlan.zones).map(([id, zone]) => {
        const metadata = owned.get(id);
        const a = transform({ x: zone.rect[0], y: zone.rect[1] });
        const b = transform({ x: zone.rect[2], y: zone.rect[3] });
        return <rect key={id} x={Math.min(a.x, b.x)} y={Math.min(a.y, b.y)}
          width={Math.abs(a.x - b.x) + 1} height={Math.abs(a.y - b.y) + 1}
          fill={metadata ? metadata.derivedFrom ? '#b49cff20' : '#f0a35e22' : '#7694a806'}
          stroke={selectedId === id ? '#fff1d8' : metadata?.derivedFrom ? DERIVED : metadata ? PLAY : '#7694a825'}
          strokeWidth={selectedId === id ? .18 : metadata ? .1 : .035}
          strokeDasharray={metadata?.derivedFrom ? '.2 .12' : undefined}
          onPointerDown={(event) => { if (metadata && !drawing) { event.stopPropagation(); onSelect(id); } }} />;
      })}
      {tracks.map((track) => {
        const points = track.points.map(transform);
        return points.length < 2 ? null : <polyline key={track.key}
          points={points.map((point) => `${point.x + .5},${point.y + .5}`).join(' ')}
          fill="none" stroke={track.teamId === 0 ? OWN : ENEMY}
          strokeWidth={.15} strokeOpacity={.38} strokeLinecap="round" />;
      })}
      {Object.entries(mapPlan.routes).map(([id, route]) => {
        const metadata = owned.get(id);
        const points = route.waypoints.map(([x, y]) => transform({ x, y }));
        return <polyline key={id}
          points={points.map((point) => `${point.x + .5},${point.y + .5}`).join(' ')}
          fill="none" stroke={selectedId === id ? '#fff1d8' : metadata ? '#39d7f2' : '#7694a835'}
          strokeWidth={selectedId === id ? .22 : metadata ? .14 : .05}
          strokeLinejoin="round" strokeLinecap="round"
          onPointerDown={(event) => { if (metadata && !drawing) { event.stopPropagation(); onSelect(id); } }} />;
      })}
      {Object.entries(mapPlan.anchors).map(([id, [x, y]]) => {
        const metadata = owned.get(id);
        const point = transform({ x, y });
        if (!metadata) return <circle key={id} cx={point.x + .5} cy={point.y + .5}
          r={.13} fill="#7694a855" />;
        const slot = play.slots.find((entry) => entry.anchorId === id);
        return <g key={id}>
          <circle cx={point.x + .5} cy={point.y + .5}
            r={selectedId === id ? .48 : .34} fill={slot ? '#10272f' : '#2d1725'}
            stroke={selectedId === id ? '#fff1d8' : slot ? '#39d7f2' : '#f472b6'} strokeWidth={.12} />
          {slot && <text x={point.x + .5} y={point.y + .63} textAnchor="middle"
            fontSize={.36} fontWeight={800} fill="#e9fbff" pointerEvents="none">
            {slot.roleId.slice(0, 1).toUpperCase()}
          </text>}
          <circle cx={point.x + .5} cy={point.y + .5} r={.72} fill="transparent"
            onPointerDown={(event) => {
              if (drawing) return;
              event.stopPropagation();
              onSelect(id);
              if (side === 'east') return;
              event.preventDefault();
              event.currentTarget.ownerSVGElement?.setPointerCapture(event.pointerId);
              setDragging(id);
            }} />
        </g>;
      })}
      {play.geometryGroups.filter((group) => group.kind === 'relay-corridor' && group.waypoints).map((group) => {
        const points = group.waypoints!.map(([x, y]) => transform({ x, y }));
        return <polyline key={group.id}
          points={points.map((point) => `${point.x + .5},${point.y + .5}`).join(' ')}
          fill="none" stroke={PLAY} strokeWidth={.1} strokeDasharray=".35 .2"
          strokeOpacity={.8} pointerEvents="none" />;
      })}
    </svg>
    </div>
    <div className="flex flex-wrap gap-3 border-t border-arena-edge px-2 py-2 t-micro">
      <span className="text-arena-material">● Program point</span>
      <span className="text-[#b49cff]">□ derived readiness zone</span>
      <span className="text-[#39d7f2]">— linked route</span>
      <span className="text-[#f0a35e]">□ ability target · range guide</span>
      <span className="ml-auto">{side === 'west'
        ? 'Drag a Program point or focus the map and use arrow keys.'
        : 'East is a read-only rotated preview.'}</span>
    </div>
  </div>;
}

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}
