import {
  useEffect,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type PointerEvent as ReactPointerEvent,
} from 'react';
import clsx from 'clsx';
import type { TacticalSheetCatalog } from '../api';
import type {
  MapPlanDocument,
  Point,
  SheetIssue,
} from './sheetAuthoring';
import type { UnitTrack } from './sheetTrials';
import IdentifierInput from './IdentifierInput';
import {
  mapRegionPresentation,
  nearestSpawnTeam,
  relationForPhysicalSide,
  relationForSpawn,
  relationForTeam,
  type MapTeamRelation,
} from './sheetMapPresentation';

export type PlotKind = 'route' | 'zone' | 'anchor';
export type MapPreviewSide = 'west' | 'east';
type Side = MapPreviewSide;
type Tool = 'select' | 'draw';
type Drag = { kind: PlotKind; id: string; index?: number; start?: Point };

const OWN = '#22d3ee';
const ENEMY = '#fb7185';
const NEUTRAL = '#d3b56f';

export default function MapPlanEditor({
  catalog,
  mapPlan,
  issues,
  tracks,
  trackLabel,
  focus,
  returnPrompt,
  onChange,
  onRename,
}: {
  catalog: TacticalSheetCatalog;
  mapPlan: MapPlanDocument;
  issues: SheetIssue[];
  tracks: UnitTrack[];
  trackLabel: string | null;
  focus?: { kind: PlotKind; id: string } | null;
  returnPrompt?: { label: string; description: string; onReturn: () => void } | null;
  onChange: (next: MapPlanDocument) => void;
  onRename: (kind: PlotKind, previous: string, next: string) => void;
}) {
  const svg = useRef<SVGSVGElement>(null);
  const [kind, setKind] = useState<PlotKind>(focus?.kind ?? 'zone');
  const [selectedId, setSelectedId] = useState(focus?.id ?? '');
  const [side, setSide] = useState<Side>('west');
  const [tool, setTool] = useState<Tool>('select');
  const [drag, setDrag] = useState<Drag | null>(null);
  const [zonePreview, setZonePreview] = useState<[Point, Point] | null>(null);
  const [selectedWaypoint, setSelectedWaypoint] = useState<number | null>(null);
  const [expanded, setExpanded] = useState(false);
  const [trackFilter, setTrackFilter] = useState<'all' | 'own' | 'enemy'>('all');

  const ids = objectIds(mapPlan, kind);
  const selected = ids.includes(selectedId) ? selectedId : ids[0] ?? '';

  useEffect(() => {
    if (!focus) return;
    setKind(focus.kind);
    setSelectedId(focus.id);
  }, [focus]);

  useEffect(() => {
    if (!expanded) return;
    const previous = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = previous; };
  }, [expanded]);

  const commit = (change: (next: MapPlanDocument) => void) => {
    const next = structuredClone(mapPlan);
    change(next);
    onChange(next);
  };

  const sourcePoint = (event: ReactPointerEvent<SVGSVGElement>): Point | null => {
    const bounds = svg.current?.getBoundingClientRect();
    if (!bounds) return null;
    let x = clamp(Math.floor((event.clientX - bounds.left) / bounds.width * catalog.map.width), 0, catalog.map.width - 1);
    let y = clamp(Math.floor((event.clientY - bounds.top) / bounds.height * catalog.map.height), 0, catalog.map.height - 1);
    if (side === 'east') {
      x = catalog.map.width - 1 - x;
      y = catalog.map.height - 1 - y;
    }
    return { x, y };
  };

  const pointerDown = (event: ReactPointerEvent<SVGSVGElement>) => {
    if (event.button !== 0 || side === 'east') return;
    const point = sourcePoint(event);
    if (!point) return;
    if (tool !== 'draw') return;
    event.currentTarget.setPointerCapture(event.pointerId);
    if (kind === 'route' && selected) {
      commit((next) => next.routes[selected].waypoints.push([point.x, point.y]));
      const index = mapPlan.routes[selected].waypoints.length;
      setSelectedWaypoint(index);
      setDrag({ kind, id: selected, index });
    } else if (kind === 'zone' && selected) {
      setDrag({ kind, id: selected, start: point });
      setZonePreview([point, point]);
    } else if (kind === 'anchor' && selected) {
      commit((next) => { next.anchors[selected] = [point.x, point.y]; });
      setDrag({ kind, id: selected });
    }
  };

  const pointerMove = (event: ReactPointerEvent<SVGSVGElement>) => {
    if (!drag) return;
    const point = sourcePoint(event);
    if (!point) return;
    if (drag.kind === 'zone' && drag.start) setZonePreview([drag.start, point]);
    if (drag.kind === 'route' && drag.index !== undefined) commit((next) => {
      next.routes[drag.id].waypoints[drag.index!] = [point.x, point.y];
    });
    if (drag.kind === 'anchor') commit((next) => {
      next.anchors[drag.id] = [point.x, point.y];
    });
  };

  const pointerUp = (event: ReactPointerEvent<SVGSVGElement>) => {
    if (drag?.kind === 'zone' && zonePreview) {
      const [a, b] = zonePreview;
      commit((next) => {
        next.zones[drag.id].rect = [
          Math.min(a.x, b.x), Math.min(a.y, b.y),
          Math.max(a.x, b.x), Math.max(a.y, b.y),
        ];
      });
    }
    if (event.currentTarget.hasPointerCapture(event.pointerId))
      event.currentTarget.releasePointerCapture(event.pointerId);
    setDrag(null);
    setZonePreview(null);
  };

  const startHandle = (
    event: ReactPointerEvent<SVGCircleElement>,
    next: Drag,
  ) => {
    if (side === 'east') return;
    event.stopPropagation();
    svg.current?.setPointerCapture(event.pointerId);
    setTool('select');
    if (next.kind === 'route') setSelectedWaypoint(next.index ?? null);
    setDrag(next);
  };

  const nudge = (event: ReactKeyboardEvent<SVGSVGElement>) => {
    const delta: Record<string, Point> = {
      ArrowLeft: { x: -1, y: 0 }, ArrowRight: { x: 1, y: 0 },
      ArrowUp: { x: 0, y: -1 }, ArrowDown: { x: 0, y: 1 },
    };
    const movement = delta[event.key];
    if (!movement || !selected || side === 'east') return;
    event.preventDefault();
    const moved = ([x, y]: [number, number]): [number, number] => [
      clamp(x + movement.x, 0, catalog.map.width - 1),
      clamp(y + movement.y, 0, catalog.map.height - 1),
    ];
    commit((next) => {
      if (kind === 'anchor') next.anchors[selected] = moved(next.anchors[selected]);
      if (kind === 'route') {
        const route = next.routes[selected];
        const index = selectedWaypoint ?? route.waypoints.length - 1;
        route.waypoints[index] = moved(route.waypoints[index]);
      }
      if (kind === 'zone') {
        const [x1, y1, x2, y2] = next.zones[selected].rect;
        const dx = clamp(movement.x, -x1, catalog.map.width - 1 - x2);
        const dy = clamp(movement.y, -y1, catalog.map.height - 1 - y2);
        next.zones[selected].rect = [x1 + dx, y1 + dy, x2 + dx, y2 + dy];
      }
    });
  };

  const addGeometry = () => {
    const id = uniqueId(kind, ids);
    commit((next) => {
      if (kind === 'zone') next.zones[id] = { rect: [11, 10, 13, 12] };
      if (kind === 'route') next.routes[id] = { waypoints: [[7, 13], [11, 13]] };
      if (kind === 'anchor') next.anchors[id] = [11, 13];
    });
    setSelectedId(id);
    setTool('draw');
  };

  const transform = (point: Point): Point => side === 'east'
    ? { x: catalog.map.width - 1 - point.x, y: catalog.map.height - 1 - point.y }
    : point;
  const visibleTracks = tracks.filter((track) => trackFilter === 'all'
    || relationForTeam(track.teamId === 0 ? 0 : track.teamId === 1 ? 1 : null, side) === trackFilter);

  return <section className={clsx(
    'panel min-w-0 pad',
    expanded && 'fixed inset-0 z-[70] overflow-y-auto rounded-none border-0 bg-arena-bg',
  )} data-sheet-path="mapPlan">
    {returnPrompt && <div className="mb-3 flex flex-wrap items-center gap-3 rounded-sm border border-arena-material/45 bg-arena-material/10 p-3">
      <div className="min-w-0 flex-1">
        <p className="type-display text-[15px]">Finish plotting this condition</p>
        <p className="t-micro mt-1">{returnPrompt.description}</p>
      </div>
      <button type="button" className="btn btn-secondary min-h-11 shrink-0" onClick={returnPrompt.onReturn}>← {returnPrompt.label}</button>
    </div>}
    <div className="flex flex-wrap items-start gap-2">
      <div className="mr-auto max-w-3xl">
        <p className="lab text-arena-material">Map plan</p>
        <h2 className="type-display mt-1 text-[22px]">Draw the ground your orders name</h2>
        <p className="t-meta mt-1">Routes, zones and anchors live on Tapline itself. Social tiles and both spawn sides remain visible while you draw.</p>
      </div>
      <div className="flex rounded-sm border border-arena-edge p-1" aria-label="Team-side preview">
        {(['west', 'east'] as const).map((value) => <button key={value} type="button"
          className={clsx('btn min-h-11', side === value && 'btn-on')}
          aria-pressed={side === value} onClick={() => setSide(value)}>{value}</button>)}
      </div>
      <button type="button" className="btn min-h-11" onClick={() => setExpanded((value) => !value)}>
        {expanded ? 'Close full map' : 'Open full map'}
      </button>
    </div>

    <div className="mt-3 grid gap-3 lg:grid-cols-[230px_minmax(0,1fr)]">
      <aside className="order-2 lg:order-1">
        <div className="grid grid-cols-3 gap-1 rounded-sm border border-arena-edge p-1">
          {(['zone', 'route', 'anchor'] as const).map((value) => <button key={value} type="button"
            className={clsx('btn min-h-11', kind === value && 'btn-on')}
            onClick={() => {
              setKind(value); setSelectedId(objectIds(mapPlan, value)[0] ?? '');
              setTool('select'); setSelectedWaypoint(null);
            }}>{humanize(value)}</button>)}
        </div>
        <div className="mt-2 flex max-h-52 gap-1 overflow-auto lg:flex-col" aria-label={`${kind} list`}>
          {ids.map((id) => <button key={id} type="button" className={clsx(
            'min-h-11 min-w-[150px] rounded-sm border px-3 text-left t-micro lg:min-w-0',
            selected === id ? 'border-arena-material/60 bg-arena-material/10 text-arena-text' : 'border-arena-edge bg-arena-deep/30',
          )} onClick={() => setSelectedId(id)}>{humanize(id)}</button>)}
          {ids.length === 0 && <p className="t-micro p-2">No {kind}s yet.</p>}
        </div>
        <button type="button" className="btn btn-secondary mt-2 min-h-11 w-full" onClick={addGeometry}>＋ Add {kind}</button>
        {selected && <>
          <label className="mt-3 block">
            <span className="t-micro mb-1 block">Tactical name</span>
            <IdentifierInput value={selected}
              isTaken={(next) => next !== selected && ids.includes(next)}
              onCommit={(next) => {
                if (next !== selected) {
                  onRename(kind, selected, next);
                  setSelectedId(next);
                }
              }} />
          </label>
          {kind === 'route' && <div className="mt-2 grid gap-1">
            <button type="button" className="btn min-h-11" disabled={mapPlan.routes[selected].waypoints.length <= 2}
              onClick={() => commit((next) => {
                const index = selectedWaypoint ?? next.routes[selected].waypoints.length - 1;
                next.routes[selected].waypoints.splice(index, 1);
                setSelectedWaypoint(null);
              })}>Remove selected waypoint</button>
          </div>}
          <button type="button" className="btn mt-2 min-h-11 w-full text-arena-hot" onClick={() => {
            commit((next) => {
              if (kind === 'zone') delete next.zones[selected];
              if (kind === 'route') delete next.routes[selected];
              if (kind === 'anchor') delete next.anchors[selected];
            });
            setSelectedId('');
          }}>Remove {kind}</button>
        </>}
        <div className="mt-3 border-t border-arena-edge pt-3">
          <p className="lab">Map checks</p>
          <IssueList issues={issues.filter((issue) => issue.path.startsWith('mapPlan'))} />
        </div>
      </aside>

      <div className="order-1 min-w-0 lg:order-2">
        <div className="mb-2 flex flex-wrap items-center gap-2">
          <div className="flex rounded-sm border border-arena-edge p-1" aria-label="Map tool">
            <button type="button" className={clsx('btn min-h-11', tool === 'select' && 'btn-on')}
              aria-pressed={tool === 'select'} onClick={() => setTool('select')}>Select</button>
            <button type="button" className={clsx('btn min-h-11', tool === 'draw' && 'btn-secondary')}
              aria-pressed={tool === 'draw'} disabled={!selected || side === 'east'}
              onClick={() => setTool('draw')}>
              {kind === 'zone' ? 'Draw rectangle' : kind === 'route' ? 'Add waypoints' : 'Place pin'}
            </button>
          </div>
          <span className="pill">{side} preview</span>
          {tracks.length > 0 && <div className="ml-auto flex rounded-sm border border-arena-edge p-1">
            {(['all', 'own', 'enemy'] as const).map((value) => <button key={value} type="button"
              className={clsx('btn min-h-11', trackFilter === value && 'btn-on')}
              onClick={() => setTrackFilter(value)}>{value} tracks</button>)}
          </div>}
        </div>
        <div className="overflow-auto rounded-sm border border-arena-edge bg-[#06090c]">
          <svg ref={svg} viewBox={`0 0 ${catalog.map.width} ${catalog.map.height}`}
            preserveAspectRatio="xMidYMid meet"
            className={clsx('block w-full min-w-[620px] select-none', tool === 'draw' && 'touch-none')}
            style={{ aspectRatio: `${catalog.map.width}/${catalog.map.height}` }}
            role="img" tabIndex={0}
            aria-label={`${side} Tapline map. Arrow keys move the selected ${kind}.`}
            onPointerDown={pointerDown} onPointerMove={pointerMove}
            onPointerUp={pointerUp} onPointerCancel={pointerUp} onKeyDown={nudge}>
            <BaseMap catalog={catalog} side={side} transform={transform} />
            {Object.entries(mapPlan.zones).map(([id, zone]) => {
              const preview = id === selected && zonePreview
                ? zonePreview
                : [{ x: zone.rect[0], y: zone.rect[1] }, { x: zone.rect[2], y: zone.rect[3] }] as [Point, Point];
              const a = transform(preview[0]); const b = transform(preview[1]);
              return <rect key={id} x={Math.min(a.x, b.x)} y={Math.min(a.y, b.y)}
                width={Math.abs(a.x - b.x) + 1} height={Math.abs(a.y - b.y) + 1}
                fill={kind === 'zone' && id === selected ? '#d07a3535' : '#d3b56f12'}
                stroke={kind === 'zone' && id === selected ? '#e89a58' : '#d3b56f66'}
                strokeWidth={kind === 'zone' && id === selected ? 0.15 : 0.07} />;
            })}
            {visibleTracks.map((track) => {
              const points = track.points.map((point) => transform(point));
              return points.length < 2 ? null : <polyline key={track.key}
                points={points.map((point) => `${point.x + .5},${point.y + .5}`).join(' ')}
                fill="none" stroke={track.teamId === 0 ? OWN : ENEMY}
                strokeWidth={0.13} strokeOpacity={0.38} strokeLinecap="round" strokeLinejoin="round" />;
            })}
            {Object.entries(mapPlan.routes).map(([id, route]) => {
              const points = route.waypoints.map(([x, y]) => transform({ x, y }));
              return <g key={id} opacity={kind === 'route' && id !== selected ? 0.38 : 1}>
                <polyline points={points.map((point) => `${point.x + .5},${point.y + .5}`).join(' ')}
                  fill="none" stroke={id === selected ? '#39d7f2' : '#7694a8'}
                  strokeWidth={id === selected ? .18 : .1} strokeLinejoin="round" strokeLinecap="round" />
                {side === 'west' && id === selected && points.map((point, index) => <g key={index}>
                  <circle cx={point.x + .5} cy={point.y + .5} r={.7} fill="transparent"
                    onPointerDown={(event) => startHandle(event, { kind: 'route', id, index })} />
                  <circle cx={point.x + .5} cy={point.y + .5} r={selectedWaypoint === index ? .38 : .29}
                    fill="#39d7f2" stroke={selectedWaypoint === index ? '#f0b94f' : '#e8fbff'}
                    strokeWidth={.1} pointerEvents="none" />
                </g>)}
              </g>;
            })}
            {Object.entries(mapPlan.anchors).map(([id, [x, y]]) => {
              const point = transform({ x, y });
              return <g key={id} opacity={kind === 'anchor' && id !== selected ? .4 : 1}>
                <circle cx={point.x + .5} cy={point.y + .5} r={id === selected ? .38 : .25}
                  fill="#f472b6" stroke="#ffe4f2" strokeWidth={.09} />
                {side === 'west' && id === selected && <circle cx={point.x + .5} cy={point.y + .5}
                  r={.7} fill="transparent" onPointerDown={(event) => startHandle(event, { kind: 'anchor', id })} />}
              </g>;
            })}
          </svg>
        </div>
        <div className="mt-2 flex flex-wrap gap-1.5" aria-label="Map legend">
          <Legend symbol="◉" label="Well" color="#b49cff" />
          <Legend symbol="+" label="Healing" color="#62d6a5" />
          <Legend symbol="◆" label="Reactor" color={OWN} />
          <Legend symbol="↓" label="Delivery" color={OWN} />
          <Legend symbol="▦" label="Protected spawn" color={OWN} />
          <Legend symbol="▶" label="Body spawn" color={OWN} />
          <Legend symbol="×" label="Skill blocked" color="#dc8a4d" />
        </div>
        <p className="t-micro mt-2">Cyan is yours, coral is the opponent, purple is a Core Well. {side === 'east' ? 'East is a read-only rotated preview.' : 'Draw from the west perspective; the executor rotates it for east.'}</p>
        {trackLabel && <p className="t-micro mt-1 text-arena-material">Actual movement overlay: {trackLabel}</p>}
      </div>
    </div>
  </section>;
}

export function MapTargetPreview({
  catalog,
  mapPlan,
  highlight,
  facingAnchor,
  onOpenMap,
}: {
  catalog: TacticalSheetCatalog;
  mapPlan: MapPlanDocument;
  highlight?: { kind: PlotKind; id: string } | null;
  facingAnchor?: string;
  onOpenMap?: () => void;
}) {
  return <button type="button" onClick={onOpenMap} disabled={!onOpenMap}
    className="group block w-full overflow-hidden rounded-sm border border-arena-edge bg-[#06090c] text-left disabled:cursor-default">
    <svg viewBox={`0 0 ${catalog.map.width} ${catalog.map.height}`}
      className="block w-full" style={{ aspectRatio: `${catalog.map.width}/${catalog.map.height}` }} aria-hidden="true">
      <BaseMap catalog={catalog} side="west" transform={(point) => point} compact />
      {Object.entries(mapPlan.zones).map(([id, zone]) => <rect key={id}
        x={zone.rect[0]} y={zone.rect[1]} width={zone.rect[2] - zone.rect[0] + 1}
        height={zone.rect[3] - zone.rect[1] + 1}
        fill={highlight?.kind === 'zone' && highlight.id === id ? '#d07a354d' : '#d3b56f10'}
        stroke={highlight?.kind === 'zone' && highlight.id === id ? '#f0a35e' : '#d3b56f44'} strokeWidth={.1} />)}
      {Object.entries(mapPlan.routes).map(([id, route]) => <polyline key={id}
        points={route.waypoints.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')} fill="none"
        stroke={highlight?.kind === 'route' && highlight.id === id ? '#39d7f2' : '#668296'}
        strokeWidth={highlight?.kind === 'route' && highlight.id === id ? .22 : .08} />)}
      {Object.entries(mapPlan.anchors).map(([id, [x, y]]) => <circle key={id}
        cx={x + .5} cy={y + .5} r={highlight?.kind === 'anchor' && highlight.id === id ? .4 : .2}
        fill={facingAnchor === id ? '#f0a35e' : '#f472b6'}
        stroke={facingAnchor === id ? '#ffe0b8' : 'none'} strokeWidth={.12} />)}
      {facingAnchor && mapPlan.anchors[facingAnchor] && <g>
        <circle cx={mapPlan.anchors[facingAnchor][0] + .5}
          cy={mapPlan.anchors[facingAnchor][1] + .5} r={.58} fill="none"
          stroke="#f0a35e" strokeWidth={.12} strokeDasharray=".22 .12" />
        <path d={`M ${mapPlan.anchors[facingAnchor][0] + .2} ${mapPlan.anchors[facingAnchor][1] + .5} h .6`}
          stroke="#ffe0b8" strokeWidth={.12} />
      </g>}
    </svg>
    {onOpenMap && <span className="block border-t border-arena-edge px-2 py-1.5 t-micro text-arena-material group-hover:text-arena-text">Edit on full map →</span>}
  </button>;
}

export function BaseMap({ catalog, side, transform, compact = false }: {
  catalog: TacticalSheetCatalog;
  side: Side;
  transform: (point: Point) => Point;
  compact?: boolean;
}) {
  const relationForPad = (point: Point) => relationForTeam(
    nearestSpawnTeam(point, catalog.map.spawnAnchors), side,
  );
  return <>
    {catalog.map.tileRows.flatMap((row, y) => [...row].map((tile, x) => {
      const point = transform({ x, y });
      return <rect key={`tile:${x}:${y}`} x={point.x} y={point.y} width={1} height={1}
        fill={tile === '#' ? '#33404a' : (x + y) % 2 ? '#0b1319' : '#0f1820'}
        stroke="#17232c" strokeWidth={compact ? .025 : .035} />;
    }))}
    {catalog.map.tileTags.filter((tag) => tag.kind === 'SpawnProtected').flatMap((tag) =>
      tag.tiles.map((raw) => {
        const point = transform(raw); const color = relationColor(relationForPad(raw));
        return <rect key={`${tag.id}:${raw.x}:${raw.y}`} x={point.x + .05} y={point.y + .05}
          width={.9} height={.9} rx={.08} fill={`${color}13`} stroke={`${color}45`}
          strokeWidth={.055} strokeDasharray=".18 .12" />;
      }))}
    {catalog.map.regions.flatMap((region) => region.tiles.map((raw) => {
      const point = transform(raw); const presentation = mapRegionPresentation(region.id);
      const relation = relationForPhysicalSide(presentation.physicalSide, side);
      const color = presentation.kind === 'well' ? '#b49cff'
        : presentation.kind === 'heal' ? '#62d6a5' : relationColor(relation);
      const inset = presentation.kind === 'delivery' ? .08 : .14;
      return <rect key={`${region.id}:${raw.x}:${raw.y}`} x={point.x + inset} y={point.y + inset}
        width={1 - inset * 2} height={1 - inset * 2} rx={presentation.kind === 'delivery' ? .08 : .18}
        fill={`${color}${presentation.kind === 'delivery' ? '20' : '18'}`}
        stroke={`${color}${presentation.kind === 'delivery' ? '8a' : '55'}`}
        strokeWidth={presentation.kind === 'delivery' ? .075 : .05} />;
    }))}
    {!compact && catalog.map.regions.map((region) => {
      const presentation = mapRegionPresentation(region.id);
      if (presentation.kind === 'objective' || region.tiles.length === 0) return null;
      const points = region.tiles.map((point) => transform(point));
      const cx = points.reduce((sum, point) => sum + point.x + .5, 0) / points.length;
      const cy = points.reduce((sum, point) => sum + point.y + .5, 0) / points.length;
      const color = presentation.kind === 'well' ? '#b49cff'
        : presentation.kind === 'heal' ? '#62d6a5'
          : relationColor(relationForPhysicalSide(presentation.physicalSide, side));
      return <g key={`landmark:${region.id}`} pointerEvents="none">
        <title>{presentation.label}</title>
        {presentation.kind === 'well' && <><circle cx={cx} cy={cy} r={.42} fill="#33254f" stroke="#c8b2ff" strokeWidth={.1} /><circle cx={cx} cy={cy} r={.2} fill="#aa8dff" /></>}
        {presentation.kind === 'heal' && <><rect x={cx - .36} y={cy - .36} width={.72} height={.72} rx={.16} fill="#173f36" stroke="#8ff0cd" strokeWidth={.09} /><path d={`M ${cx - .2} ${cy} H ${cx + .2} M ${cx} ${cy - .2} V ${cy + .2}`} stroke="#b9ffe6" strokeWidth={.13} /></>}
        {presentation.kind === 'reactor' && <><path d={`M ${cx} ${cy - .43} L ${cx + .43} ${cy} L ${cx} ${cy + .43} L ${cx - .43} ${cy} Z`} fill="#080c10" stroke={color} strokeWidth={.11} /><circle cx={cx} cy={cy} r={.19} fill={color} /></>}
        {presentation.kind === 'delivery' && <><circle cx={cx} cy={cy} r={.37} fill="#080c10" stroke={color} strokeWidth={.1} /><path d={`M ${cx} ${cy - .2} V ${cy + .13} M ${cx - .14} ${cy} L ${cx} ${cy + .16} L ${cx + .14} ${cy}`} fill="none" stroke={color} strokeWidth={.1} /></>}
      </g>;
    })}
    {!compact && catalog.map.spawnAnchors.map((spawn) => {
      const point = transform(spawn.position);
      const relation = relationForSpawn(spawn.id, side); const color = relationColor(relation);
      const east = side === 'east' ? spawn.facing === 'East' ? false : true : spawn.facing === 'East';
      const shape = east
        ? `M ${point.x + .18} ${point.y + .18} L ${point.x + .82} ${point.y + .5} L ${point.x + .18} ${point.y + .82} Z`
        : `M ${point.x + .82} ${point.y + .18} L ${point.x + .18} ${point.y + .5} L ${point.x + .82} ${point.y + .82} Z`;
      return <path key={spawn.id} d={shape} fill={color} stroke="#071017" strokeWidth={.055} />;
    })}
    {!compact && catalog.map.tileTags.filter((tag) => tag.kind === 'SignaturePlacementForbidden').flatMap((tag) => tag.tiles.map((raw) => {
      const point = transform(raw);
      return <path key={`${tag.id}:${raw.x}:${raw.y}`} d={`M ${point.x + .69} ${point.y + .1} L ${point.x + .9} ${point.y + .31} M ${point.x + .9} ${point.y + .1} L ${point.x + .69} ${point.y + .31}`}
        stroke="#dc8a4d" strokeWidth={.075} />;
    }))}
  </>;
}

function IssueList({ issues }: { issues: SheetIssue[] }) {
  if (issues.length === 0) return <p className="t-micro mt-2 text-arena-ok">Geometry is internally consistent.</p>;
  return <ul className="mt-2 space-y-1 text-xs">{issues.map((issue, index) => <li key={`${issue.path}:${index}`}
    className={issue.severity === 'error' ? 'text-arena-hot' : 'text-arena-material'}>{issue.message}</li>)}</ul>;
}

function Legend({ symbol, label, color }: { symbol: string; label: string; color: string }) {
  return <span className="flex min-h-8 items-center gap-1.5 rounded-sm border border-arena-edge bg-arena-deep/30 px-2 t-micro">
    <span className="font-mono text-[15px]" style={{ color }}>{symbol}</span>{label}
  </span>;
}

function objectIds(mapPlan: MapPlanDocument, kind: PlotKind): string[] {
  return Object.keys(kind === 'zone' ? mapPlan.zones : kind === 'route' ? mapPlan.routes : mapPlan.anchors);
}

function relationColor(relation: MapTeamRelation) {
  return relation === 'own' ? OWN : relation === 'enemy' ? ENEMY : NEUTRAL;
}

function uniqueId(prefix: string, ids: string[]) {
  if (!ids.includes(prefix)) return prefix;
  for (let index = 2; ; index++) if (!ids.includes(`${prefix}-${index}`)) return `${prefix}-${index}`;
}

function humanize(value: string) {
  return value.replace(/[-_]+/g, ' ').replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}
