import { useMemo, useRef, useState } from 'react';
import type { TacticalSheetCatalog } from '../api';
import { BaseMap } from './BaseMap';
import { makePathfinder } from './pathing';
import { describePlace } from './places';
import type { MapTask } from './useMapTask';
import {
  groupColor, signatureMarkGeometry,
  type Facing, type Point, type Rect, type SquadBot, type Tactic,
  type TacticGroup, type TacticPhase, type TacticsDocument,
} from './tacticsDocument';

/**
 * The map IS the editor: every Tactic draws its footprint here, the open
 * wizard's geometry is grabbable, and the active draw task turns clicks and
 * drags into posts, boxes, routes and marks. Condition footprints render
 * under group geometry so posts stay grabbable (mock z-order rule).
 */

export type MapSelectRef =
  | { tacticId: string; part: 'start' | 'release' | 'finish' | 'cores' }
  | { tacticId: string; part: 'group'; groupId: string };

type DragTarget =
  | { kind: 'post'; groupId: string; index: number; origin: Point }
  | { kind: 'postface'; groupId: string; index: number }
  | { kind: 'waypoint'; groupId: string; phase: 'stage' | 'act'; index: number }
  | { kind: 'point'; groupId: string; phase: 'stage' | 'act' }
  | { kind: 'rectmove'; groupId: string; phase: 'stage' | 'act' }
  | { kind: 'rectsize'; groupId: string; phase: 'stage' | 'act' }
  | { kind: 'sigpoint'; groupId: string; phase: 'stage' | 'act'; classId: string; index: number }
  | { kind: 'watchmove'; which: 'start' | 'release' }
  | { kind: 'watchsize'; which: 'start' | 'release' };

export interface GeometryEdit {
  tacticId: string;
  apply: (tactic: Tactic) => void;
}

const FACING_DELTA: Record<Facing, Point> = {
  n: [0, -1], e: [1, 0], s: [0, 1], w: [-1, 0],
};

export function TacticsMapCanvas({
  catalog, document, editingTacticId, selectedTacticId, showAll, task, hints,
  onTilePicked, onRectDrawn, onGeometryLive, onGestureStart, onGestureEnd, onSelect,
}: {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  editingTacticId: string | null;
  selectedTacticId: string | null;
  showAll: boolean;
  task: MapTask | null;
  hints: Map<string, { rect: Rect; text: string }> | null;
  onTilePicked: (x: number, y: number) => void;
  onRectDrawn: (rect: Rect) => void;
  onGeometryLive: (edit: GeometryEdit) => void;
  onGestureStart: () => void;
  onGestureEnd: () => void;
  onSelect: (ref: MapSelectRef) => void;
}) {
  const svgRef = useRef<SVGSVGElement | null>(null);
  const [hover, setHover] = useState<Point | null>(null);
  const [rectDraft, setRectDraft] = useState<{ start: Point; cur: Point } | null>(null);
  const dragRef = useRef<DragTarget | null>(null);
  const pressRef = useRef<[number, number] | null>(null);
  const path = useMemo(() => makePathfinder(catalog), [catalog]);
  const width = catalog.map.width;
  const height = catalog.map.height;

  const displayed = useMemo(() => {
    const out: { tactic: Tactic; dim: boolean; tag: string | null }[] = [];
    if (editingTacticId) {
      const tactic = document.tactics.find((value) => value.id === editingTacticId);
      if (tactic) out.push({ tactic, dim: false, tag: null });
      return out;
    }
    document.tactics.forEach((tactic, index) => {
      if (tactic.id === selectedTacticId) out.push({ tactic, dim: false, tag: null });
      else if (showAll) out.push({ tactic, dim: true, tag: `T${index + 1}` });
    });
    return out;
  }, [document, editingTacticId, selectedTacticId, showAll]);

  function clientToTile(event: React.PointerEvent): Point | null {
    const svg = svgRef.current;
    if (!svg) return null;
    const ctm = svg.getScreenCTM();
    if (!ctm) return null;
    const point = new DOMPoint(event.clientX, event.clientY).matrixTransform(ctm.inverse());
    if (point.x < 0 || point.y < 0 || point.x >= width || point.y >= height) return null;
    return [Math.floor(point.x), Math.floor(point.y)];
  }

  function startDrag(event: React.PointerEvent, target: DragTarget) {
    if (task) return;
    event.stopPropagation();
    event.preventDefault();
    dragRef.current = target;
    onGestureStart();
    svgRef.current?.setPointerCapture(event.pointerId);
  }

  function onPointerDown(event: React.PointerEvent) {
    const tile = clientToTile(event);
    pressRef.current = tile ? [event.clientX, event.clientY] : null;
    const wantsRect = task
      && (task.kind === 'watch' ? true : task.draw === 'rect');
    if (wantsRect && tile && path.floor(tile[0], tile[1])) {
      setRectDraft({ start: tile, cur: tile });
      svgRef.current?.setPointerCapture(event.pointerId);
      event.preventDefault();
    }
  }

  function onPointerMove(event: React.PointerEvent) {
    const tile = clientToTile(event);
    const drag = dragRef.current;
    if (drag) {
      if (tile && path.floor(tile[0], tile[1])) applyDrag(drag, tile);
      return;
    }
    if (rectDraft && tile) {
      setRectDraft({ ...rectDraft, cur: tile });
      return;
    }
    if (task) setHover(tile);
  }

  function onPointerUp(event: React.PointerEvent) {
    const drag = dragRef.current;
    if (drag) {
      dragRef.current = null;
      finishDrag(drag);
      onGestureEnd();
      return;
    }
    if (rectDraft) {
      const { start, cur } = rectDraft;
      setRectDraft(null);
      onRectDrawn([
        Math.min(start[0], cur[0]), Math.min(start[1], cur[1]),
        Math.max(start[0], cur[0]), Math.max(start[1], cur[1]),
      ]);
      return;
    }
    const moved = pressRef.current
      && Math.hypot(event.clientX - pressRef.current[0], event.clientY - pressRef.current[1]) > 7;
    pressRef.current = null;
    if (moved) return;
    const tile = clientToTile(event);
    if (task && tile && task.kind !== 'watch' && task.draw !== 'rect') {
      if (path.floor(tile[0], tile[1])) onTilePicked(tile[0], tile[1]);
      return;
    }
    const selectable = (event.target as Element).closest('[data-select]');
    if (selectable) {
      const raw = selectable.getAttribute('data-select')!;
      const [tacticId, part, groupId] = raw.split(':');
      if (part === 'group') onSelect({ tacticId, part, groupId });
      else onSelect({ tacticId, part: part as 'start' | 'release' | 'finish' | 'cores' });
    }
  }

  function editGeometry(apply: (tactic: Tactic) => void) {
    if (!editingTacticId) return;
    onGeometryLive({ tacticId: editingTacticId, apply });
  }

  function applyDrag(drag: DragTarget, [x, y]: Point) {
    editGeometry((tactic) => {
      if (drag.kind === 'watchmove' || drag.kind === 'watchsize') {
        const conds = drag.which === 'start' ? tactic.start.conds : tactic.release.conds;
        const row = conds?.enemiesAt;
        if (!row) return;
        const rect: Rect = [...row.zone.rect];
        if (drag.kind === 'watchsize') {
          row.zone = { rect: [rect[0], rect[1], Math.max(rect[0], x), Math.max(rect[1], y)] };
        } else {
          const w = rect[2] - rect[0];
          const h = rect[3] - rect[1];
          const nx = Math.min(Math.max(x, 0), width - 1 - w);
          const ny = Math.min(Math.max(y, 0), height - 1 - h);
          row.zone = { rect: [nx, ny, nx + w, ny + h] };
        }
        return;
      }
      const group = tactic.groups.find((value) => value.id === drag.groupId);
      if (!group) return;
      if (drag.kind === 'postface') {
        const post = group.stage.posts?.[drag.index];
        if (!post) return;
        const dx = x - post.at[0];
        const dy = y - post.at[1];
        if (!dx && !dy) return;
        post.facing = Math.abs(dx) >= Math.abs(dy)
          ? (dx > 0 ? 'e' : 'w')
          : (dy > 0 ? 's' : 'n');
        return;
      }
      if (drag.kind === 'sigpoint') {
        const mark = group.sigs?.[drag.phase]?.[drag.classId];
        if (!mark) return;
        const points = 'points' in mark ? mark.points : mark.line;
        points[drag.index] = [x, y];
        return;
      }
      const phase = drag.kind === 'post' ? group.stage
        : drag.phase === 'stage' ? group.stage : group.act;
      if (drag.kind === 'post') {
        if (phase.posts?.[drag.index]) phase.posts[drag.index].at = [x, y];
      } else if (drag.kind === 'waypoint') {
        if (phase.path?.[drag.index]) phase.path[drag.index] = [x, y];
      } else if (drag.kind === 'point') {
        phase.point = [x, y];
      } else if (drag.kind === 'rectsize' && phase.rect) {
        phase.rect = [phase.rect[0], phase.rect[1],
          Math.max(phase.rect[0], x), Math.max(phase.rect[1], y)];
      } else if (drag.kind === 'rectmove' && phase.rect) {
        const w = phase.rect[2] - phase.rect[0];
        const h = phase.rect[3] - phase.rect[1];
        const nx = Math.min(Math.max(x, 0), width - 1 - w);
        const ny = Math.min(Math.max(y, 0), height - 1 - h);
        phase.rect = [nx, ny, nx + w, ny + h];
      }
    });
  }

  /** Dropping a posted bot onto a groupmate's tile swaps their posts. */
  function finishDrag(drag: DragTarget) {
    if (drag.kind !== 'post') return;
    editGeometry((tactic) => {
      const group = tactic.groups.find((value) => value.id === drag.groupId);
      const post = group?.stage.posts?.[drag.index];
      if (!group || !post) return;
      const other = group.stage.posts!.findIndex((candidate, index) =>
        index !== drag.index
        && candidate.at[0] === post.at[0] && candidate.at[1] === post.at[1]);
      if (other >= 0) group.stage.posts![other].at = drag.origin;
    });
  }

  const bots = document.squad;
  const editable = (tactic: Tactic) => tactic.id === editingTacticId;

  return (
    <svg
      ref={svgRef}
      viewBox={`0 0 ${width} ${height}`}
      preserveAspectRatio="xMidYMid meet"
      className="block w-full select-none"
      style={{ aspectRatio: `${width}/${height}`, touchAction: task || editingTacticId ? 'none' : undefined }}
      data-testid="tactics-map"
      role="img"
      aria-label="Tapline arena, west perspective"
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerLeave={() => setHover(null)}
    >
      <BaseMap catalog={catalog} />
      {displayed.map(({ tactic, dim, tag }) => (
        <g key={tactic.id} opacity={dim ? .32 : 1}>
          <ConditionFootprints tactic={tactic} dim={dim} editable={editable(tactic)}
            startDrag={startDrag} pathfinder={path} />
          {tactic.groups.map((group) => (
            <GroupOverlay key={group.id} tactic={tactic} group={group} bots={bots}
              catalog={catalog} dim={dim} editable={editable(tactic)}
              startDrag={startDrag} pathfinder={path} />
          ))}
          {dim && tag && <ChipLabel x={2.5} y={1.6} text={tag} color="#7694a8" />}
        </g>
      ))}
      <TaskLayer task={task} hover={hover} rectDraft={rectDraft}
        hints={hints} pathfinder={path} document={document} />
    </svg>
  );
}

function ConditionFootprints({ tactic, dim, editable, startDrag, pathfinder }: {
  tactic: Tactic;
  dim: boolean;
  editable: boolean;
  startDrag: (event: React.PointerEvent, target: DragTarget) => void;
  pathfinder: ReturnType<typeof makePathfinder>;
}) {
  const parts: React.ReactNode[] = [];
  const watch = (which: 'start' | 'release', color: string, symbol: string, above: boolean) => {
    const source = which === 'start' ? tactic.start : tactic.release;
    const active = which === 'start' ? source.mode === 'when' : source.mode === 'ready-when';
    if (!active || !source.conds) return;
    const select = `${tactic.id}:${which}`;
    if (source.conds.enemiesAt) {
      const [x0, y0, x1, y1] = source.conds.enemiesAt.zone.rect;
      parts.push(<g key={`${which}-watch`}>
        <rect x={x0 + .06} y={y0 + .06} width={x1 - x0 + .88} height={y1 - y0 + .88} rx={.16}
          fill={`${color}0e`} stroke={color} strokeWidth={.1} strokeDasharray=".12 .2"
          data-select={select} cursor="pointer" />
        {!dim && <ChipLabel x={(x0 + x1 + 1) / 2} y={above ? y0 - .35 : y1 + 2.15}
          text={`${symbol} ${source.conds.enemiesAt.count}+ ENEMIES HERE`} color={color} select={select} />}
        {editable && <>
          <Handle cx={x0} cy={y0} onDown={(event) => startDrag(event, { kind: 'watchmove', which })} cursor="move" />
          <Handle cx={x1 + 1} cy={y1 + 1} onDown={(event) => startDrag(event, { kind: 'watchsize', which })} />
        </>}
      </g>);
    }
  };
  watch('start', '#e6ddd4', '▶', true);
  watch('release', '#c8763c', '⚡', false);

  if (tactic.finish.outcomes.cleared) {
    const [x0, y0, x1, y1] = tactic.finish.outcomes.cleared.zone.rect;
    const color = '#38d9c5';
    parts.push(<g key="cleared">
      <rect x={x0 + .28} y={y0 + .28} width={x1 - x0 + .44} height={y1 - y0 + .44} rx={.14}
        fill="none" stroke={color} strokeWidth={.07} strokeDasharray=".3 .16" opacity=".8"
        data-select={`${tactic.id}:finish`} cursor="pointer" />
      {!dim && <ChipLabel x={x1 - .8} y={y1 + 1.35} text="✓ CLEAR TO FINISH" color={color}
        select={`${tactic.id}:finish`} opacity={.95} />}
    </g>);
  }

  if (tactic.delivery.plan !== 'none') {
    const anchors = tactic.groups
      .map((group) => stageAnchor(group))
      .filter((value): value is Point => Boolean(value));
    const from = anchors.length
      ? pathfinder.nearestFloor([
        Math.round(anchors.reduce((sum, [x]) => sum + x, 0) / anchors.length),
        Math.round(anchors.reduce((sum, [, y]) => sum + y, 0) / anchors.length),
      ])
      : [3, 5] as Point;
    const home: Point = [2, 5];
    const run = pathfinder.findPath(from, home);
    const color = '#b49cff';
    if (run) {
      const mid = run[Math.floor(run.length / 2)];
      parts.push(<g key="delivery">
        <polyline points={run.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
          fill="none" stroke={color} strokeWidth={.1} strokeDasharray=".1 .26" opacity=".85"
          data-select={`${tactic.id}:cores`} cursor="pointer" />
        <circle cx={home[0] + .5} cy={home[1] + .5} r={.7} fill="none" stroke={color}
          strokeWidth={.08} strokeDasharray=".18 .14" />
        {!dim && <ChipLabel x={mid[0] + .5} y={mid[1] + 1.9}
          text={`⌂ CORE RUN${tactic.delivery.plan === 'escort' ? ' · ESCORTED' : ''}`}
          color={color} select={`${tactic.id}:cores`} />}
      </g>);
    }
  }
  return <>{parts}</>;
}

function GroupOverlay({ tactic, group, bots, catalog, dim, editable, startDrag, pathfinder }: {
  tactic: Tactic;
  group: TacticGroup;
  bots: SquadBot[];
  catalog: TacticalSheetCatalog;
  dim: boolean;
  editable: boolean;
  startDrag: (event: React.PointerEvent, target: DragTarget) => void;
  pathfinder: ReturnType<typeof makePathfinder>;
}) {
  const color = groupColor(tactic, group.id);
  const select = `${tactic.id}:group:${group.id}`;
  const parts: React.ReactNode[] = [];
  const stage = group.stage;

  if (stage.rect) {
    const [x0, y0, x1, y1] = stage.rect;
    parts.push(<g key="stage-rect">
      <rect x={x0} y={y0} width={x1 - x0 + 1} height={y1 - y0 + 1}
        fill={`${color}22`} stroke={color} strokeWidth={.12} strokeDasharray=".34 .18" rx={.1}
        data-select={select} cursor="pointer" />
      {editable && <>
        <Handle cx={x1 + 1} cy={y1 + 1}
          onDown={(event) => startDrag(event, { kind: 'rectsize', groupId: group.id, phase: 'stage' })} />
        <Handle cx={x0} cy={y0} cursor="move"
          onDown={(event) => startDrag(event, { kind: 'rectmove', groupId: group.id, phase: 'stage' })} />
      </>}
    </g>);
  }
  if (stage.path && stage.path.length > 1) {
    const points = pathfinder.connect(stage.path);
    parts.push(<g key="stage-path">
      <polyline points={points.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
        fill="none" stroke={color} strokeWidth={.13} strokeDasharray=".3 .2"
        strokeLinejoin="round" strokeLinecap="round" data-select={select} cursor="pointer" />
      {stage.path.map(([x, y], index) => editable
        ? <Handle key={index} cx={x + .5} cy={y + .5}
          onDown={(event) => startDrag(event, { kind: 'waypoint', groupId: group.id, phase: 'stage', index })} />
        : <circle key={index} cx={x + .5} cy={y + .5} r={.14} fill={color} />)}
    </g>);
  }
  if (stage.point) {
    const [x, y] = stage.point;
    const approach = pathfinder.findPath([3, 5], stage.point);
    parts.push(<g key="stage-point">
      {approach && <polyline points={approach.map(([px, py]) => `${px + .5},${py + .5}`).join(' ')}
        fill="none" stroke={color} strokeWidth={.08} strokeDasharray=".14 .22" opacity=".7" />}
      <circle cx={x + .5} cy={y + .5} r={.42} fill="none" stroke={color} strokeWidth={.11}
        strokeDasharray=".2 .13" data-select={select} cursor="pointer" />
      {editable
        ? <Handle cx={x + .5} cy={y + .5}
          onDown={(event) => startDrag(event, { kind: 'point', groupId: group.id, phase: 'stage' })} />
        : <circle cx={x + .5} cy={y + .5} r={.15} fill={color} />}
    </g>);
  }
  if (stage.posts) {
    stage.posts.forEach((post, index) => {
      const [x, y] = post.at;
      const cx = x + .5;
      const cy = y + .5;
      const [fx, fy] = FACING_DELTA[post.facing];
      const angle = Math.atan2(fy, fx);
      const a1 = angle - Math.PI / 4;
      const a2 = angle + Math.PI / 4;
      const wr = .95;
      const tipX = cx + Math.cos(angle) * .88;
      const tipY = cy + Math.sin(angle) * .88;
      const bot = bots[group.slots[index]];
      const entry = bot ? catalog.classes.find((value) => value.id === bot.class) : null;
      parts.push(<g key={`post-${index}`}>
        <path d={`M ${cx} ${cy}
            L ${cx + Math.cos(a1) * wr} ${cy + Math.sin(a1) * wr}
            A ${wr} ${wr} 0 0 1 ${cx + Math.cos(a2) * wr} ${cy + Math.sin(a2) * wr} Z`}
          fill={color} fillOpacity=".13" pointerEvents="none" />
        <polygon points={`${tipX + Math.cos(angle) * .14},${tipY + Math.sin(angle) * .14} ${tipX - Math.cos(angle - .6) * .3},${tipY - Math.sin(angle - .6) * .3} ${tipX - Math.cos(angle + .6) * .3},${tipY - Math.sin(angle + .6) * .3}`}
          fill={color} opacity=".9" pointerEvents="none" />
        <g data-select={select} cursor="pointer">
          <circle cx={cx} cy={cy} r={.4} fill="#10272f" stroke={color} strokeWidth={.12} />
          <text x={cx} y={y + .68} textAnchor="middle" fontSize=".4" fontWeight="800"
            fill="#e9fbff" pointerEvents="none">{bot ? bot.name[0] : index + 1}</text>
          <title>{bot ? `${bot.name} (${entry?.name ?? bot.class}) — drag the arrow tip to turn it` : `${group.name} post ${index + 1}`}</title>
        </g>
        {editable && <>
          <Handle cx={cx} cy={cy} r={.42}
            onDown={(event) => startDrag(event, { kind: 'post', groupId: group.id, index, origin: [x, y] })} />
          <Handle cx={tipX} cy={tipY} r={.3}
            onDown={(event) => startDrag(event, { kind: 'postface', groupId: group.id, index })} />
        </>}
      </g>);
    });
  }

  const act = group.act;
  if (act.kind === 'converge' && act.point) {
    const starts = convergeStarts(group, pathfinder);
    parts.push(<g key="converge" data-testid="converge-fan">
      {starts.map((from, index) => {
        const run = pathfinder.findPath(from, act.point!) ?? [from, act.point!];
        return <ArrowPath key={index} points={run} color={color} select={select}
          width={starts.length > 1 ? .09 : .16} opacity={starts.length > 1 ? .75 : .92} />;
      })}
      <g data-select={select} cursor="pointer">
        <circle cx={act.point[0] + .5} cy={act.point[1] + .5} r={.5} fill="none" stroke={color} strokeWidth={.1} />
        <circle cx={act.point[0] + .5} cy={act.point[1] + .5} r={.24} fill="none" stroke={color} strokeWidth={.1} />
        <circle cx={act.point[0] + .5} cy={act.point[1] + .5} r={.07} fill={color} />
      </g>
      {editable && <Handle cx={act.point[0] + .5} cy={act.point[1] + .5} r={.62}
        onDown={(event) => startDrag(event, { kind: 'point', groupId: group.id, phase: 'act' })} />}
    </g>);
  }
  if ((act.kind === 'advance' || act.kind === 'patrol') && act.path && act.path.length > 1) {
    const loop = act.kind === 'patrol' ? [...act.path, act.path[0]] : act.path;
    parts.push(<g key="act-path">
      <ArrowPath points={pathfinder.connect(loop)} color={color} select={select} loop={act.kind === 'patrol'} />
      {act.path.map(([x, y], index) => editable
        ? <Handle key={index} cx={x + .5} cy={y + .5}
          onDown={(event) => startDrag(event, { kind: 'waypoint', groupId: group.id, phase: 'act', index })} />
        : <circle key={index} cx={x + .5} cy={y + .5} r={.13} fill={color} />)}
    </g>);
  }
  if (act.kind === 'return') {
    const start = stageAnchor(group) ?? [3, 5] as Point;
    const home: Point = [2, 5];
    const run = pathfinder.findPath(start, home) ?? [start, home];
    parts.push(<g key="return">
      <ArrowPath points={run} color={color} select={select} dash=".3 .2" />
      <circle cx={home[0] + .5} cy={home[1] + .5} r={.55} fill="none" stroke={color}
        strokeWidth={.09} strokeDasharray=".22 .14" />
    </g>);
  }

  if (stage.recovery === 'heal-tile' || act.recovery === 'heal-tile') {
    const heals = catalog.map.regions.filter((region) => region.id.startsWith('heal-'));
    const from = stageCentroid(group, pathfinder);
    let best: Point | null = null;
    for (const heal of heals) {
      for (const tile of heal.tiles) {
        const candidate: Point = [tile.x, tile.y];
        if (!best || chebyshev(candidate, from) < chebyshev(best, from)) best = candidate;
      }
    }
    if (best) {
      const run = pathfinder.findPath(from, best);
      parts.push(<g key="heal-link">
        {run && <polyline points={run.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
          fill="none" stroke={color} strokeWidth={.07} strokeDasharray=".08 .22" opacity=".55" />}
        <circle cx={best[0] + .5} cy={best[1] + .5} r={.68} fill="none" stroke="#8ff0cd"
          strokeWidth={.08} strokeDasharray=".16 .14" opacity=".9" />
      </g>);
    }
  }

  for (const phaseKey of ['stage', 'act'] as const) {
    for (const [classId, mark] of Object.entries(group.sigs?.[phaseKey] ?? {})) {
      const geometry = signatureMarkGeometry(catalog, classId);
      if (!geometry) continue;
      const dashProps = phaseKey === 'act' ? { strokeDasharray: '.16 .12' } : {};
      const points = 'points' in mark ? mark.points : mark.line;
      parts.push(<g key={`sig-${phaseKey}-${classId}`}>
        {'line' in mark && mark.line.length >= 2 && (
          <polyline points={mark.line.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
            fill="none" stroke={color} strokeWidth={.09} strokeDasharray=".5 .22" opacity=".8"
            data-select={select} cursor="pointer" />
        )}
        {points.map(([x, y], index) => <g key={index}>
          <g data-select={select} cursor="pointer">
            <circle cx={x + .5} cy={y + .5} r={.34} fill="#06090c" fillOpacity=".72"
              stroke={color} strokeWidth={.09} {...dashProps} />
            <text x={x + .5} y={y + .68} textAnchor="middle" fontSize=".38" fill={color}
              pointerEvents="none">✦</text>
            <title>{`${group.name} — ${geometry.label} (${phaseKey})`}</title>
          </g>
          {editable && <Handle cx={x + .5} cy={y + .5} r={.42}
            onDown={(event) => startDrag(event, { kind: 'sigpoint', groupId: group.id, phase: phaseKey, classId, index })} />}
        </g>)}
      </g>);
    }
  }

  if (!dim) {
    const anchor = stageAnchor(group);
    if (anchor) {
      parts.push(<ChipLabel key="stage-chip" x={anchor[0] + .5} y={anchor[1] - .48}
        text={`${group.name.toUpperCase()} · STAGE`} color={color} select={select} />);
    }
    const actPoint = actAnchor(group);
    if (actPoint) {
      parts.push(<ChipLabel key="act-chip" x={actPoint[0] + .5} y={actPoint[1] + 2.15}
        text={`${group.name.toUpperCase()} · ACT`} color={color} select={select} />);
    }
  }
  return <>{parts}</>;
}

function TaskLayer({ task, hover, rectDraft, hints, pathfinder, document }: {
  task: MapTask | null;
  hover: Point | null;
  rectDraft: { start: Point; cur: Point } | null;
  hints: Map<string, { rect: Rect; text: string }> | null;
  pathfinder: ReturnType<typeof makePathfinder>;
  document: TacticsDocument;
}) {
  if (!task) return null;
  const parts: React.ReactNode[] = [];
  const hint = task.kind === 'phase'
    ? task.hint ?? (hints?.get(task.groupId) ?? null)
    : null;
  const hasGeometry = task.kind === 'phase' && taskGeometryExists(task, document);
  if (hint && !hasGeometry) {
    const [x0, y0, x1, y1] = hint.rect;
    parts.push(<rect key="hint" x={x0} y={y0} width={x1 - x0 + 1} height={y1 - y0 + 1}
      fill="none" stroke="#fff1d8" strokeWidth={.09} strokeDasharray=".4 .26" rx={.2}
      pointerEvents="none" className="animate-pulse" />);
  }
  if (rectDraft) {
    const x0 = Math.min(rectDraft.start[0], rectDraft.cur[0]);
    const y0 = Math.min(rectDraft.start[1], rectDraft.cur[1]);
    const x1 = Math.max(rectDraft.start[0], rectDraft.cur[0]);
    const y1 = Math.max(rectDraft.start[1], rectDraft.cur[1]);
    parts.push(<rect key="rect-draft" x={x0} y={y0} width={x1 - x0 + 1} height={y1 - y0 + 1}
      fill="#fff1d835" stroke="#fff1d8" strokeWidth={.14} pointerEvents="none" />);
  }
  if (hover) {
    const [x, y] = hover;
    const blocked = !pathfinder.floor(x, y);
    parts.push(<rect key="hover" x={x + .04} y={y + .04} width={.92} height={.92} rx={.1}
      fill="none" stroke={blocked ? '#f4526b' : '#fff1d8'} strokeWidth={.1} opacity=".85"
      pointerEvents="none" />);
  }
  return <>{parts}</>;
}

function taskGeometryExists(
  task: Extract<MapTask, { kind: 'phase' }>,
  document: TacticsDocument,
): boolean {
  const tactic = document.tactics.find((value) => value.id === task.tacticId);
  const group = tactic?.groups.find((value) => value.id === task.groupId);
  if (!group) return false;
  const phase = task.phase === 'stage' ? group.stage : group.act;
  return Boolean(phase.posts?.length || phase.rect || (phase.path?.length ?? 0) > 0 || phase.point);
}

function ArrowPath({ points, color, select, loop = false, dash, width = .16, opacity = .92 }: {
  points: Point[];
  color: string;
  select: string;
  loop?: boolean;
  dash?: string;
  width?: number;
  opacity?: number;
}) {
  if (points.length < 2) return null;
  const [ax, ay] = points[points.length - 2];
  const [bx, by] = points[points.length - 1];
  const angle = Math.atan2(by - ay, bx - ax);
  const cx = bx + .5;
  const cy = by + .5;
  const headR = Math.max(.34, width * 3.4);
  return <>
    <polyline points={points.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
      fill="none" stroke={color} strokeWidth={width} strokeDasharray={dash}
      strokeLinejoin="round" strokeLinecap="round" data-select={select} cursor="pointer"
      opacity={opacity} />
    {loop
      ? <text x={points[0][0] + .78} y={points[0][1] + .3} fontSize=".5" fill={color}>↻</text>
      : <polygon points={`${cx},${cy} ${cx - Math.cos(angle - .5) * headR},${cy - Math.sin(angle - .5) * headR} ${cx - Math.cos(angle + .5) * headR},${cy - Math.sin(angle + .5) * headR}`}
        fill={color} opacity={opacity} />}
  </>;
}

function Handle({ cx, cy, onDown, cursor = 'grab', r = .3 }: {
  cx: number;
  cy: number;
  onDown: (event: React.PointerEvent) => void;
  cursor?: string;
  r?: number;
}) {
  return <circle cx={cx} cy={cy} r={r} fill="#fff1d8" fillOpacity=".001"
    cursor={cursor} onPointerDown={onDown} />;
}

function ChipLabel({ x, y, text, color, select, opacity = 1 }: {
  x: number;
  y: number;
  text: string;
  color: string;
  select?: string;
  opacity?: number;
}) {
  const w = text.length * .27 + .5;
  const cx = Math.min(Math.max(x - w / 2, .2), 31 - w - .2);
  const cy = Math.max(y - .62, .2);
  return <g opacity={opacity} data-select={select} cursor={select ? 'pointer' : undefined}>
    <rect x={cx} y={cy} width={w} height={.78} rx={.16} fill="#06090c" fillOpacity=".82"
      stroke={color} strokeWidth={.05} />
    <text x={cx + w / 2} y={cy + .55} textAnchor="middle" fontSize=".42" letterSpacing=".03"
      fontWeight="650" fill={color}>{text}</text>
  </g>;
}

export function stageAnchor(group: TacticGroup): Point | null {
  const stage = group.stage;
  if (stage.posts?.length) return stage.posts[0].at;
  if (stage.rect) return [stage.rect[0], stage.rect[1]];
  if (stage.path?.length) return stage.path[0];
  if (stage.point) return stage.point;
  return null;
}

function actAnchor(group: TacticGroup): Point | null {
  const act = group.act;
  if (act.kind === 'converge' && act.point) return act.point;
  if ((act.kind === 'advance' || act.kind === 'patrol') && act.path && act.path.length > 1) {
    return act.path[Math.floor(act.path.length / 2)];
  }
  if (act.kind === 'return') return [2, 5];
  return null;
}

function stageCentroid(group: TacticGroup, pathfinder: ReturnType<typeof makePathfinder>): Point {
  const stage = group.stage;
  let tiles: Point[] = [];
  if (stage.posts?.length) tiles = stage.posts.map((post) => post.at);
  else if (stage.rect) {
    tiles = [[Math.round((stage.rect[0] + stage.rect[2]) / 2), Math.round((stage.rect[1] + stage.rect[3]) / 2)]];
  } else if (stage.path?.length) tiles = stage.path;
  else if (stage.point) tiles = [stage.point];
  if (!tiles.length) return [3, 5];
  const sx = Math.round(tiles.reduce((sum, [x]) => sum + x, 0) / tiles.length);
  const sy = Math.round(tiles.reduce((sum, [, y]) => sum + y, 0) / tiles.length);
  return pathfinder.nearestFloor([sx, sy]);
}

function convergeStarts(group: TacticGroup, pathfinder: ReturnType<typeof makePathfinder>): Point[] {
  const stage = group.stage;
  const count = Math.max(1, group.slots.length);
  const sample = (tiles: Point[]): Point[] => {
    if (!tiles.length) return [stageCentroid(group, pathfinder)];
    if (tiles.length <= count) return tiles;
    return Array.from({ length: count }, (_, index) =>
      tiles[Math.round(index * (tiles.length - 1) / Math.max(1, count - 1))]);
  };
  if (stage.posts?.length) return stage.posts.map((post) => post.at);
  if (stage.rect) {
    const [x0, y0, x1, y1] = stage.rect;
    const tiles: Point[] = [];
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      if (pathfinder.floor(x, y)) tiles.push([x, y]);
    }
    return sample(tiles);
  }
  if (stage.path && stage.path.length > 1) return sample(pathfinder.connect(stage.path));
  if (stage.point) return [stage.point];
  return [[3, 5]];
}

function chebyshev([ax, ay]: Point, [bx, by]: Point): number {
  return Math.max(Math.abs(ax - bx), Math.abs(ay - by));
}

export function describeGeometry(phase: TacticPhase, group: TacticGroup): string {
  const kind = phase.kind;
  if (kind === 'posts') {
    const count = phase.posts?.length ?? 0;
    if (!count) return `No posts yet — needs one per bot (${group.slots.length}).`;
    const where = describePlace(...phase.posts![0].at);
    return `${count} post${count === 1 ? '' : 's'} at ${where}${count === group.slots.length ? '' : ` (${group.slots.length} bots)`}`;
  }
  if (kind === 'area') {
    if (!phase.rect) return 'No staging area yet — drag a box on the map.';
    return `Staging area near ${describePlace(Math.round((phase.rect[0] + phase.rect[2]) / 2), Math.round((phase.rect[1] + phase.rect[3]) / 2))}`;
  }
  if (kind === 'line') {
    if (!phase.path || phase.path.length < 2) return 'No line yet — click two or more points.';
    return `Picket line through ${describePlace(...phase.path[0])}`;
  }
  if (kind === 'approach' || kind === 'converge') {
    if (!phase.point) {
      return kind === 'approach' ? 'No approach point yet — click the map.' : 'No destination yet — click the map.';
    }
    return `${kind === 'approach' ? 'Approach' : 'Converge on'} ${describePlace(...phase.point)}`;
  }
  if (kind === 'advance' || kind === 'patrol') {
    if (!phase.path || phase.path.length < 2) return 'No route yet — click waypoints in travel order.';
    return `${phase.path.length}-point ${kind === 'patrol' ? 'loop' : 'route'} toward ${describePlace(...phase.path[phase.path.length - 1])}`;
  }
  return '';
}
