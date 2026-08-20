import { useMemo, useRef, useState } from 'react';
import type { TacticalSheetCatalog } from '../api';
import { BaseMap } from './BaseMap';
import { classSprite } from './classArt';
import { makePathfinder } from './pathing';
import { describePlace } from './places';
import type { MapTask, WatchTarget } from './useMapTask';
import {
  FACING_DELTAS, effectiveJob, groupColor, jobOf, phaseShade, postFacingIssue,
  signatureMarkGeometry, tilePlacementIssue,
  type Facing, type Point, type Rect, type SquadBot, type Tactic,
  type TacticConds, type TacticGroup, type TacticJob, type TacticsDocument,
  type WellChoice,
} from './tacticsDocument';

/**
 * The map IS the editor: every Tactic draws its footprint here — phase one
 * full-bodied, later phases as onward arrows and dimmer ground — the open
 * wizard's geometry is grabbable in every phase, and the active draw task
 * turns clicks and drags into posts, boxes, routes and marks. Condition
 * footprints render under group geometry so posts stay grabbable.
 */

export type MapSelectRef =
  | { tacticId: string; part: 'start' | 'cores' }
  | { tacticId: string; part: 'phase'; phaseIndex: number }
  | { tacticId: string; part: 'group'; groupId: string; phaseIndex: number }
  | { tacticId: string; part: 'post'; groupId: string; phaseIndex: number; postIndex: number };

type DragTarget =
  | { kind: 'post'; groupId: string; phaseIndex: number; index: number; origin: Point }
  | { kind: 'postface'; groupId: string; phaseIndex: number; index: number }
  | { kind: 'waypoint'; groupId: string; phaseIndex: number; index: number }
  | { kind: 'point'; groupId: string; phaseIndex: number }
  | { kind: 'rectmove'; groupId: string; phaseIndex: number }
  | { kind: 'rectsize'; groupId: string; phaseIndex: number }
  | { kind: 'sigpoint'; groupId: string; phaseIndex: number; classId: string; index: number }
  | { kind: 'watchmove'; target: WatchTarget }
  | { kind: 'watchsize'; target: WatchTarget };

export interface GeometryEdit {
  tacticId: string;
  apply: (tactic: Tactic) => void;
}

/** The conds object a watch target addresses, on a given tactic. */
export function watchConds(tactic: Tactic, target: WatchTarget): TacticConds | null {
  if (target.scope === 'start') {
    const entry = tactic.start.cases?.[target.caseIndex];
    return entry?.[target.side] ?? null;
  }
  if (target.scope === 'abort') {
    const entry = tactic.abort?.cases[target.caseIndex];
    return entry?.[target.side] ?? null;
  }
  const rule = tactic.phases[target.phaseIndex]?.continue?.[target.ruleIndex];
  return rule?.[target.side] ?? null;
}

export function TacticsMapCanvas({
  catalog, document, editingTacticId, activePhaseIndex, selectedTacticId,
  showAll, showAllPhases, task, hints,
  onTilePicked, onRectDrawn, onGeometryLive, onGestureStart, onGestureEnd,
  onSelect, onTaskDone, onTileRejected, onPostsSwapped,
}: {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  editingTacticId: string | null;
  /** The wizard's storyboard selection; later phases render dimmer. */
  activePhaseIndex: number | null;
  selectedTacticId: string | null;
  showAll: boolean;
  showAllPhases: boolean;
  task: MapTask | null;
  hints: Map<string, { rect: Rect; text: string }> | null;
  onTilePicked: (x: number, y: number) => void;
  onRectDrawn: (rect: Rect) => void;
  onGeometryLive: (edit: GeometryEdit) => void;
  onGestureStart: () => void;
  onGestureEnd: () => void;
  onSelect: (ref: MapSelectRef) => void;
  /** Finish an open route task (double-click, mirroring the Done button). */
  onTaskDone?: () => void;
  /** A draw-task click landed on an illegal tile; the reason is ready to show. */
  onTileRejected?: (reason: string) => void;
  /** A post drop landed on a groupmate and the two swapped tiles. */
  onPostsSwapped?: () => void;
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
      // With nothing selected the map would otherwise be blank; every Tactic
      // shows dimmed with its tag until one is picked.
      else if (showAll || !selectedTacticId) out.push({ tactic, dim: true, tag: `T${index + 1}` });
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

  const dragPressRef = useRef<[number, number] | null>(null);

  function startDrag(event: React.PointerEvent, target: DragTarget) {
    if (task) return;
    event.stopPropagation();
    event.preventDefault();
    dragRef.current = target;
    dragPressRef.current = [event.clientX, event.clientY];
    onGestureStart();
    svgRef.current?.setPointerCapture(event.pointerId);
  }

  function onPointerDown(event: React.PointerEvent) {
    const tile = clientToTile(event);
    pressRef.current = tile ? [event.clientX, event.clientY] : null;
    const wantsRect = task
      && (task.kind === 'watch' ? true : task.draw === 'rect');
    if (wantsRect && tile) {
      if (!path.floor(tile[0], tile[1])) {
        onTileRejected?.('That tile is a wall.');
        return;
      }
      setRectDraft({ start: tile, cur: tile });
      svgRef.current?.setPointerCapture(event.pointerId);
      event.preventDefault();
    }
  }

  /** Whether a live drag may land here, per what the drag is moving. */
  function dragTileLegal(drag: DragTarget, tile: Point): boolean {
    if (drag.kind === 'postface') return true; // direction, not a landing tile
    if (!path.floor(tile[0], tile[1])) return false;
    if (drag.kind === 'post' || drag.kind === 'point' || drag.kind === 'sigpoint') {
      return !tilePlacementIssue(catalog, tile, 'anchor');
    }
    if (drag.kind === 'waypoint') {
      return !tilePlacementIssue(catalog, tile, 'route');
    }
    return true;
  }

  function onPointerMove(event: React.PointerEvent) {
    const tile = clientToTile(event);
    const drag = dragRef.current;
    if (drag) {
      if (tile && dragTileLegal(drag, tile)) applyDrag(drag, tile);
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
      const pressed = dragPressRef.current;
      dragPressRef.current = null;
      // The drag handle sits on top of the post, so a stationary press on it
      // IS the click — surface it as the post's select instead of a no-op.
      const stationary = pressed && Math.hypot(
        event.clientX - pressed[0], event.clientY - pressed[1]) <= 7;
      if (stationary && drag.kind === 'post' && editingTacticId) {
        onGestureEnd();
        onSelect({
          tacticId: editingTacticId, part: 'post',
          groupId: drag.groupId, phaseIndex: drag.phaseIndex,
          postIndex: drag.index,
        });
        return;
      }
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
      // Job routes are walked, so waypoints follow the route rule; posts,
      // points and signature marks become anchors.
      const placement = (task.kind === 'job' || task.kind === 'carry') && task.draw === 'route'
        ? 'route' as const
        : 'anchor' as const;
      const issue = tilePlacementIssue(catalog, tile, placement);
      if (issue === null) onTilePicked(tile[0], tile[1]);
      else {
        onTileRejected?.(issue === 'a wall'
          ? 'That tile is a wall.'
          : `That tile is ${issue}.`);
      }
      return;
    }
    const selectable = (event.target as Element).closest('[data-select]');
    if (selectable) {
      const raw = selectable.getAttribute('data-select')!;
      const [tacticId, part, a, b, c] = raw.split(':');
      if (part === 'group') {
        onSelect({ tacticId, part, groupId: a, phaseIndex: Number(b) });
      } else if (part === 'post') {
        onSelect({
          tacticId, part, groupId: a,
          phaseIndex: Number(b), postIndex: Number(c),
        });
      } else if (part === 'phase') {
        onSelect({ tacticId, part, phaseIndex: Number(a) });
      } else {
        onSelect({ tacticId, part: part as 'start' | 'cores' });
      }
    }
  }

  function editGeometry(apply: (tactic: Tactic) => void) {
    if (!editingTacticId) return;
    onGeometryLive({ tacticId: editingTacticId, apply });
  }

  function applyDrag(drag: DragTarget, [x, y]: Point) {
    editGeometry((tactic) => {
      if (drag.kind === 'watchmove' || drag.kind === 'watchsize') {
        const conds = watchConds(tactic, drag.target);
        const holder = drag.target.row === 'cleared'
          ? conds?.cleared
          : drag.target.row === 'notFighting'
            ? conds?.notFighting
            : conds?.enemiesAt;
        if (!holder) return;
        const zone = holder.zones[drag.target.zoneIndex];
        if (!zone) return;
        const rect: Rect = [...zone.rect];
        if (drag.kind === 'watchsize') {
          holder.zones[drag.target.zoneIndex] = { rect: [rect[0], rect[1], Math.max(rect[0], x), Math.max(rect[1], y)] };
        } else {
          // Clamp inside the border wall ring, not just the map bounds.
          const w = rect[2] - rect[0];
          const h = rect[3] - rect[1];
          const nx = Math.min(Math.max(x, 1), width - 2 - w);
          const ny = Math.min(Math.max(y, 1), height - 2 - h);
          holder.zones[drag.target.zoneIndex] = { rect: [nx, ny, nx + w, ny + h] };
        }
        return;
      }
      const job = tactic.phases[drag.phaseIndex]?.jobs[drag.groupId];
      if (!job) return;
      if (drag.kind === 'postface') {
        const post = job.posts?.[drag.index];
        if (!post) return;
        const dx = x - post.at[0];
        const dy = y - post.at[1];
        if (!dx && !dy) return;
        const candidate: Facing = Math.abs(dx) >= Math.abs(dy)
          ? (dx > 0 ? 'e' : 'w')
          : (dy > 0 ? 's' : 'n');
        // The tip only snaps to directions whose watched tile is legal — a
        // post cannot be turned to stare into a wall or spawn ground.
        if (postFacingIssue(catalog, { at: post.at, facing: candidate })) return;
        post.facing = candidate;
        return;
      }
      if (drag.kind === 'sigpoint') {
        const mark = job.sigs?.[drag.classId];
        if (!mark) return;
        const points = 'points' in mark ? mark.points : mark.line;
        points[drag.index] = [x, y];
        return;
      }
      if (drag.kind === 'post') {
        if (job.posts?.[drag.index]) job.posts[drag.index].at = [x, y];
      } else if (drag.kind === 'waypoint') {
        if (job.path?.[drag.index]) job.path[drag.index] = [x, y];
      } else if (drag.kind === 'point') {
        job.point = [x, y];
      } else if (drag.kind === 'rectsize' && job.rect) {
        job.rect = [job.rect[0], job.rect[1],
          Math.max(job.rect[0], x), Math.max(job.rect[1], y)];
      } else if (drag.kind === 'rectmove' && job.rect) {
        const w = job.rect[2] - job.rect[0];
        const h = job.rect[3] - job.rect[1];
        const nx = Math.min(Math.max(x, 1), width - 2 - w);
        const ny = Math.min(Math.max(y, 1), height - 2 - h);
        job.rect = [nx, ny, nx + w, ny + h];
      }
    });
  }

  /** Dropping a posted bot onto a groupmate's tile swaps their posts. */
  function finishDrag(drag: DragTarget) {
    if (drag.kind !== 'post') return;
    // The live drag already moved the post in the document, so a shared tile
    // here means the drop is a swap — detect before the edit, announce after.
    const liveJob = document.tactics
      .find((value) => value.id === editingTacticId)
      ?.phases[drag.phaseIndex]?.jobs[drag.groupId];
    const livePost = liveJob?.posts?.[drag.index];
    const willSwap = Boolean(liveJob && livePost
      && liveJob.posts!.some((candidate, index) =>
        index !== drag.index
        && candidate.at[0] === livePost.at[0] && candidate.at[1] === livePost.at[1]));
    editGeometry((tactic) => {
      const job = tactic.phases[drag.phaseIndex]?.jobs[drag.groupId];
      const post = job?.posts?.[drag.index];
      if (!job || !post) return;
      const other = job.posts!.findIndex((candidate, index) =>
        index !== drag.index
        && candidate.at[0] === post.at[0] && candidate.at[1] === post.at[1]);
      if (other >= 0) job.posts![other].at = drag.origin;
    });
    if (willSwap) onPostsSwapped?.();
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
      onDoubleClick={(event) => {
        const draw = task && task.kind !== 'watch' ? task.draw : null;
        if (draw !== 'route' || !onTaskDone) return;
        event.preventDefault();
        onTaskDone();
      }}
    >
      <BaseMap catalog={catalog} />
      {displayed.map(({ tactic, dim, tag }) => {
        // A dimmed tactic's tag sits on its first drawn geometry, so three
        // overlays in "show every Tactic" stay tellable apart.
        const tagAnchor = dim && tag
          ? tactic.groups.map((group) => jobAnchor(jobOf(tactic, 0, group.id)))
            .find((value): value is Point => Boolean(value)) ?? [2, 2] as Point
          : null;
        return (
          <g key={tactic.id}>
            <g opacity={dim ? .32 : 1}>
              <ConditionFootprints tactic={tactic} catalog={catalog} dim={dim}
                editable={editable(tactic)} startDrag={startDrag} pathfinder={path}
                activePhaseIndex={activePhaseIndex} showAllPhases={showAllPhases} />
              {tactic.phases.map((phase, phaseIndex) => {
                const focus = activePhaseIndex ?? 0;
                const distance = Math.abs(phaseIndex - focus);
                if (!showAllPhases && distance > 1) return null;
                const phaseOpacity = phaseIndex === focus
                  ? 1 : distance === 1 ? .24 : .12;
                return (
                <g key={phase.id}
                  opacity={phaseOpacity}>
                  {tactic.groups.map((group) => (
                    <JobOverlay key={group.id} tactic={tactic} group={group}
                      phaseIndex={phaseIndex} bots={bots}
                      catalog={catalog} dim={dim}
                      editable={editable(tactic) && phaseIndex === focus}
                      startDrag={startDrag} pathfinder={path} />
                  ))}
                  {phase.custody?.via && phase.custody.via.length > 1
                    && phase.custody.plan !== 'keep' && (() => {
                      const home: Point = [2, 5];
                      const run = path.connect([...phase.custody.via, home]);
                      const shade = phaseShade(
                        '#b49cff', phaseIndex, tactic.phases.length);
                      const mid = run[Math.floor(run.length / 2)];
                      return <g>
                        <polyline
                          points={run.map(([px, py]) => `${px + .5},${py + .5}`).join(' ')}
                          fill="none" stroke={shade} strokeWidth={.11}
                          strokeDasharray=".14 .22" opacity=".9"
                          strokeLinejoin="round" strokeLinecap="round" />
                        {phase.custody.via.map(([wx, wy], wi) =>
                          <circle key={wi} cx={wx + .5} cy={wy + .5} r={.12} fill={shade} />)}
                        <circle cx={home[0] + .5} cy={home[1] + .5} r={.62} fill="none"
                          stroke={shade} strokeWidth={.08} strokeDasharray=".2 .14" />
                        {!dim && <ChipLabel x={mid[0] + .5} y={mid[1] + 1.9}
                          text={`⌂ CARRY HOME${phase.custody.plan === 'escort' ? ' · ESCORTED' : ''}`}
                          color={shade} />}
                      </g>;
                    })()}
                </g>
                );
              })}
            </g>
            {tagAnchor && tag && <ChipLabel x={tagAnchor[0] + .5} y={tagAnchor[1] - .35}
              text={tag} color="#7694a8" opacity={.9} mapWidth={width} mapHeight={height} />}
          </g>
        );
      })}
      <TaskLayer task={task} hover={hover} rectDraft={rectDraft}
        hints={hints} pathfinder={path} document={document} />
    </svg>
  );
}

function ConditionFootprints({
  tactic, catalog, dim, editable, startDrag, pathfinder,
  activePhaseIndex, showAllPhases,
}: {
  tactic: Tactic;
  catalog: TacticalSheetCatalog;
  dim: boolean;
  editable: boolean;
  startDrag: (event: React.PointerEvent, target: DragTarget) => void;
  pathfinder: ReturnType<typeof makePathfinder>;
  activePhaseIndex: number | null;
  showAllPhases: boolean;
}) {
  const parts: React.ReactNode[] = [];

  const watchBoxes = (
    conds: TacticConds | undefined,
    makeTarget: (row: 'enemiesAt' | 'cleared' | 'notFighting', zoneIndex: number) => WatchTarget,
    color: string,
    symbol: string,
    select: string,
    key: string,
    above: boolean,
    negated = false,
  ) => {
    const no = negated ? 'UNLESS ' : '';
    if (conds?.enemiesAt) {
      const enemiesAt = conds.enemiesAt;
      enemiesAt.zones.forEach((zone, zoneIndex) => {
      const [x0, y0, x1, y1] = zone.rect;
      const rowTarget = makeTarget('enemiesAt', zoneIndex);
      parts.push(<g key={`${key}-enemies-${zoneIndex}`}>
        <rect x={x0 + .06} y={y0 + .06} width={x1 - x0 + .88} height={y1 - y0 + .88} rx={.16}
          fill={`${color}0e`} stroke={color} strokeWidth={.1} strokeDasharray=".12 .2"
          data-select={select} cursor="pointer" />
        {!dim && <ChipLabel x={(x0 + x1 + 1) / 2} y={above ? y0 - .35 : y1 + 2.15}
          text={`${symbol} ${no}${enemiesAt.count}+ ENEMIES · AREA ${zoneIndex + 1}`} color={color} select={select} />}
        {editable && <>
          <Handle cx={x0} cy={y0} cursor="move"
            onDown={(event) => startDrag(event, { kind: 'watchmove', target: rowTarget })} />
          <Handle cx={x1 + 1} cy={y1 + 1}
            onDown={(event) => startDrag(event, { kind: 'watchsize', target: rowTarget })} />
        </>}
      </g>);
      });
    }
    if (conds?.cleared) {
      const teal = '#38d9c5';
      conds.cleared.zones.forEach((zone, zoneIndex) => {
      const [x0, y0, x1, y1] = zone.rect;
      const rowTarget = makeTarget('cleared', zoneIndex);
      parts.push(<g key={`${key}-cleared-${zoneIndex}`}>
        <rect x={x0 + .28} y={y0 + .28} width={x1 - x0 + .44} height={y1 - y0 + .44} rx={.14}
          fill="none" stroke={teal} strokeWidth={.07} strokeDasharray=".3 .16" opacity=".8"
          data-select={select} cursor="pointer" />
        {!dim && <ChipLabel x={x1 - .8} y={y1 + 1.35} text={`${negated ? '✕ UNLESS' : '✓'} CLEAR · AREA ${zoneIndex + 1}`} color={teal}
          select={select} opacity={.95} />}
        {editable && <>
          <Handle cx={x0} cy={y0} cursor="move"
            onDown={(event) => startDrag(event, { kind: 'watchmove', target: rowTarget })} />
          <Handle cx={x1 + 1} cy={y1 + 1}
            onDown={(event) => startDrag(event, { kind: 'watchsize', target: rowTarget })} />
        </>}
      </g>);
      });
    }
    if (conds?.notFighting) {
      const quiet = '#f0b35a';
      conds.notFighting.zones.forEach((zone, zoneIndex) => {
      const [x0, y0, x1, y1] = zone.rect;
      const rowTarget = makeTarget('notFighting', zoneIndex);
      parts.push(<g key={`${key}-not-fighting-${zoneIndex}`}>
        <rect x={x0 + .16} y={y0 + .16} width={x1 - x0 + .68} height={y1 - y0 + .68} rx={.14}
          fill="none" stroke={quiet} strokeWidth={.07} strokeDasharray=".12 .18" opacity=".8"
          data-select={select} cursor="pointer" />
        {!dim && <ChipLabel x={x0 + 1} y={y0 - .35}
          text={`${negated ? '✕ UNLESS' : '◇'} NO FIGHT · AREA ${zoneIndex + 1}`} color={quiet}
          select={select} opacity={.95} />}
        {editable && <>
          <Handle cx={x0} cy={y0} cursor="move"
            onDown={(event) => startDrag(event, { kind: 'watchmove', target: rowTarget })} />
          <Handle cx={x1 + 1} cy={y1 + 1}
            onDown={(event) => startDrag(event, { kind: 'watchsize', target: rowTarget })} />
        </>}
      </g>);
      });
    }
    if (conds?.coreLoose) {
      const at = wellCentre(catalog, conds.coreLoose.well);
      if (at) {
        parts.push(<g key={`${key}-core`}>
          <circle cx={at[0]} cy={at[1]} r={.95} fill="none" stroke={color}
            strokeWidth={.1} strokeDasharray=".14 .18" data-select={select} cursor="pointer" />
          {!dim && <ChipLabel x={at[0]} y={at[1] + 2.1} text={`${symbol} ${no}CORE UP HERE`}
            color={color} select={select} />}
        </g>);
      }
    }
  };

  (tactic.start.cases ?? []).forEach((entry, caseIndex) => {
    for (const side of ['required', 'triggers', 'unless'] as const) {
      watchBoxes(entry[side],
        (row, zoneIndex) => ({ scope: 'start', caseIndex, side, row, zoneIndex }),
        '#e6ddd4', '▶', `${tactic.id}:start`, `start-${caseIndex}-${side}`, true,
        side === 'unless');
    }
  });
  tactic.phases.forEach((phase, phaseIndex) => {
    const focus = activePhaseIndex ?? 0;
    if (!showAllPhases && Math.abs(phaseIndex - focus) > 1) return;
    (phase.continue ?? []).forEach((rule, ruleIndex) => {
      for (const side of ['required', 'triggers', 'unless'] as const) {
        watchBoxes(rule[side],
          (row, zoneIndex) => ({ scope: 'continue', phaseIndex, ruleIndex, side, row, zoneIndex }),
          '#c8763c', '⚡', `${tactic.id}:phase:${phaseIndex}`,
          `continue-${phaseIndex}-${ruleIndex}-${side}`, false,
          side === 'unless');
      }
    });
  });

  if (tactic.delivery.plan !== 'none') {
    const anchors = tactic.groups
      .map((group) => jobAnchor(jobOf(tactic, 0, group.id)))
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

/** One group's job in one phase. The first phase renders full-bodied
 * (ground you stand on); later phases render as the onward move — arrows
 * from the previous ground for movers, dashed ground for a new stand. */
function JobOverlay({ tactic, group, phaseIndex, bots, catalog, dim, editable, startDrag, pathfinder }: {
  tactic: Tactic;
  group: TacticGroup;
  phaseIndex: number;
  bots: SquadBot[];
  catalog: TacticalSheetCatalog;
  dim: boolean;
  editable: boolean;
  startDrag: (event: React.PointerEvent, target: DragTarget) => void;
  pathfinder: ReturnType<typeof makePathfinder>;
}) {
  const job = jobOf(tactic, phaseIndex, group.id);
  if (!job || job.kind === 'none') return null;
  const color = phaseShade(
    groupColor(tactic, group.id), phaseIndex, tactic.phases.length);
  const select = `${tactic.id}:group:${group.id}:${phaseIndex}`;
  const parts: React.ReactNode[] = [];
  const first = phaseIndex === 0;
  const phaseName = tactic.phases[phaseIndex]?.name ?? `Phase ${phaseIndex + 1}`;
  const previous = phaseIndex > 0
    ? effectiveJob(tactic, phaseIndex - 1, group.id)
    : null;

  const standing = ['posts', 'area', 'line', 'approach'].includes(job.kind);
  if (standing) {
    // Ground you stand on: full-bodied on phase one, dashed on later phases,
    // with an onward arrow from the previous ground.
    if (!first && previous) {
      const from = jobCentroid(previous, pathfinder);
      const to = jobAnchor(job);
      if (to) {
        const run = pathfinder.findPath(from, to) ?? [from, to];
        parts.push(<ArrowPath key="onward" points={run} color={color}
          select={select} dash=".14 .2" width={.09} opacity={.7} />);
      }
    }
    if (job.rect) {
      const [x0, y0, x1, y1] = job.rect;
      parts.push(<g key="rect">
        <rect x={x0} y={y0} width={x1 - x0 + 1} height={y1 - y0 + 1}
          fill={`${color}22`} stroke={color} strokeWidth={.12}
          strokeDasharray={first ? '.34 .18' : '.16 .24'} rx={.1}
          data-select={select} cursor="pointer" />
        {editable && <>
          <Handle cx={x1 + 1} cy={y1 + 1}
            onDown={(event) => startDrag(event, { kind: 'rectsize', groupId: group.id, phaseIndex })} />
          <Handle cx={x0} cy={y0} cursor="move"
            onDown={(event) => startDrag(event, { kind: 'rectmove', groupId: group.id, phaseIndex })} />
        </>}
      </g>);
    }
    if (job.path && job.path.length > 1) {
      const points = pathfinder.connect(job.path);
      parts.push(<g key="path">
        <polyline points={points.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
          fill="none" stroke={color} strokeWidth={.13} strokeDasharray=".3 .2"
          strokeLinejoin="round" strokeLinecap="round" data-select={select} cursor="pointer" />
        {job.path.map(([x, y], index) => editable
          ? <Handle key={index} cx={x + .5} cy={y + .5}
            onDown={(event) => startDrag(event, { kind: 'waypoint', groupId: group.id, phaseIndex, index })} />
          : <circle key={index} cx={x + .5} cy={y + .5} r={.14} fill={color} />)}
      </g>);
    }
    if (job.point) {
      const [x, y] = job.point;
      const approach = pathfinder.findPath([3, 5], job.point);
      parts.push(<g key="point">
        {first && approach && <polyline points={approach.map(([px, py]) => `${px + .5},${py + .5}`).join(' ')}
          fill="none" stroke={color} strokeWidth={.08} strokeDasharray=".14 .22" opacity=".7" />}
        <circle cx={x + .5} cy={y + .5} r={.42} fill="none" stroke={color} strokeWidth={.11}
          strokeDasharray=".2 .13" data-select={select} cursor="pointer" />
        {editable
          ? <Handle cx={x + .5} cy={y + .5}
            onDown={(event) => startDrag(event, { kind: 'point', groupId: group.id, phaseIndex })} />
          : <circle cx={x + .5} cy={y + .5} r={.15} fill={color} />}
      </g>);
    }
    if (job.posts) {
      job.posts.forEach((post, index) => {
        const [x, y] = post.at;
        const cx = x + .5;
        const cy = y + .5;
        const [fx, fy] = FACING_DELTAS[post.facing];
        const angle = Math.atan2(fy, fx);
        const a1 = angle - Math.PI / 4;
        const a2 = angle + Math.PI / 4;
        const wr = .95;
        const tipX = cx + Math.cos(angle) * .88;
        const tipY = cy + Math.sin(angle) * .88;
        const bot = bots[post.slot ?? group.slots[index]];
        const entry = bot ? catalog.classes.find((value) => value.id === bot.class) : null;
        const sprite = bot ? classSprite(bot.class) : undefined;
        // Facing angle in degrees: sprites are authored facing east (0°), the
        // same convention as the arena renderer.
        const deg = Math.round(angle * 180 / Math.PI);
        // Post-scoped select: clicking a posted bot lights up its own way-in
        // row in the drawer, not just the group section.
        const postSelect = `${tactic.id}:post:${group.id}:${phaseIndex}:${index}`;
        parts.push(<g key={`post-${index}`}>
          <path d={`M ${cx} ${cy}
              L ${cx + Math.cos(a1) * wr} ${cy + Math.sin(a1) * wr}
              A ${wr} ${wr} 0 0 1 ${cx + Math.cos(a2) * wr} ${cy + Math.sin(a2) * wr} Z`}
            fill={color} fillOpacity=".13" pointerEvents="none" />
          <polygon points={`${tipX + Math.cos(angle) * .14},${tipY + Math.sin(angle) * .14} ${tipX - Math.cos(angle - .6) * .3},${tipY - Math.sin(angle - .6) * .3} ${tipX - Math.cos(angle + .6) * .3},${tipY - Math.sin(angle + .6) * .3}`}
            fill={color} opacity=".9" pointerEvents="none" />
          {bot && sprite
            ? <g data-select={postSelect} cursor="pointer">
              <rect x={x + .04} y={y + .04} width={.92} height={.92} rx={.16}
                fill="#06090c" fillOpacity=".55" stroke={color} strokeWidth={.07} />
              <image href={sprite} x={x - .04} y={y - .04} width={1.08} height={1.08}
                transform={`rotate(${deg} ${cx} ${cy})`} />
              <title>{`${bot.name} (${entry?.name ?? bot.class}) — drag the arrow tip to turn it`}</title>
            </g>
            : <g data-select={postSelect} cursor="pointer">
              <circle cx={cx} cy={cy} r={.36} fill="#10272f" stroke={color} strokeWidth={.12} />
              <text x={cx} y={y + .64} textAnchor="middle" fontSize=".38" fontWeight="800"
                fill="#e9fbff" pointerEvents="none">{index + 1}</text>
              <title>{bot
                ? `${bot.name} (${entry?.name ?? bot.class}) — drag the arrow tip to turn it`
                : `${group.name} post ${index + 1}`}</title>
            </g>}
          {post.via && post.via.length > 1 && (
            <g>
              <polyline points={pathfinder.connect([...post.via, post.at])
                .map(([px, py]) => `${px + .5},${py + .5}`).join(' ')}
                fill="none" stroke={color} strokeWidth={.07} strokeDasharray=".1 .22"
                strokeLinejoin="round" strokeLinecap="round" opacity=".75"
                data-select={select} cursor="pointer" />
              {post.via.map(([wx, wy], wi) =>
                <circle key={wi} cx={wx + .5} cy={wy + .5} r={.1} fill={color} opacity=".75" />)}
            </g>
          )}
          {editable && <>
            <Handle cx={cx} cy={cy} r={.42}
              onDown={(event) => startDrag(event, { kind: 'post', groupId: group.id, phaseIndex, index, origin: [x, y] })} />
            <Handle cx={tipX} cy={tipY} r={.3}
              onDown={(event) => startDrag(event, { kind: 'postface', groupId: group.id, phaseIndex, index })} />
          </>}
        </g>);
      });
    }
  }

  if (job.kind === 'converge' && job.point) {
    const starts = previous ? jobStarts(previous, group, pathfinder) : [[3, 5] as Point];
    parts.push(<g key="converge" data-testid="converge-fan">
      {starts.map((from, index) => {
        const run = pathfinder.findPath(from, job.point!) ?? [from, job.point!];
        return <ArrowPath key={index} points={run} color={color} select={select}
          width={starts.length > 1 ? .09 : .16} opacity={starts.length > 1 ? .75 : .92} />;
      })}
      <g data-select={select} cursor="pointer">
        <circle cx={job.point[0] + .5} cy={job.point[1] + .5} r={.5} fill="none" stroke={color} strokeWidth={.1} />
        <circle cx={job.point[0] + .5} cy={job.point[1] + .5} r={.24} fill="none" stroke={color} strokeWidth={.1} />
        <circle cx={job.point[0] + .5} cy={job.point[1] + .5} r={.07} fill={color} />
      </g>
      {editable && <Handle cx={job.point[0] + .5} cy={job.point[1] + .5} r={.62}
        onDown={(event) => startDrag(event, { kind: 'point', groupId: group.id, phaseIndex })} />}
    </g>);
  }
  if ((job.kind === 'advance' || job.kind === 'patrol') && job.path && job.path.length > 1) {
    const loop = job.kind === 'patrol' ? [...job.path, job.path[0]] : job.path;
    parts.push(<g key="mover-path">
      <ArrowPath points={pathfinder.connect(loop)} color={color} select={select} loop={job.kind === 'patrol'} />
      {job.path.map(([x, y], index) => editable
        ? <Handle key={index} cx={x + .5} cy={y + .5}
          onDown={(event) => startDrag(event, { kind: 'waypoint', groupId: group.id, phaseIndex, index })} />
        : <circle key={index} cx={x + .5} cy={y + .5} r={.13} fill={color} />)}
    </g>);
  }
  if (job.kind === 'return') {
    const start = (previous && jobAnchor(previous)) ?? [3, 5] as Point;
    const home: Point = [2, 5];
    const run = pathfinder.findPath(start, home) ?? [start, home];
    parts.push(<g key="return">
      <ArrowPath points={run} color={color} select={select} dash=".3 .2" />
      <circle cx={home[0] + .5} cy={home[1] + .5} r={.55} fill="none" stroke={color}
        strokeWidth={.09} strokeDasharray=".22 .14" />
    </g>);
  }
  if (job.via && job.via.length > 1) {
    parts.push(<g key="via">
      <polyline points={pathfinder.connect(job.via).map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
        fill="none" stroke={color} strokeWidth={.08} strokeDasharray=".12 .26"
        strokeLinejoin="round" strokeLinecap="round" opacity=".8"
        data-select={select} cursor="pointer" />
      {job.via.map(([x, y], index) =>
        <circle key={index} cx={x + .5} cy={y + .5} r={.11} fill={color} opacity=".8" />)}
    </g>);
  }

  if (first && job.recovery === 'heal-tile') {
    const heals = catalog.map.regions.filter((region) => region.id.startsWith('heal-'));
    const from = jobCentroid(job, pathfinder);
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
          fill="none" stroke={color} strokeWidth={.07} strokeDasharray=".08 .22" opacity=".55"
          data-select={select} cursor="pointer" />}
        <circle cx={best[0] + .5} cy={best[1] + .5} r={.68} fill="none" stroke="#8ff0cd"
          strokeWidth={.08} strokeDasharray=".16 .14" opacity=".9"
          data-select={select} cursor="pointer" />
      </g>);
    }
  }

  for (const [classId, mark] of Object.entries(job.sigs ?? {})) {
    const geometry = signatureMarkGeometry(catalog, classId);
    if (!geometry) continue;
    const dashProps = first ? {} : { strokeDasharray: '.16 .12' };
    const points = 'points' in mark ? mark.points : mark.line;
    parts.push(<g key={`sig-${classId}`}>
      {'line' in mark && mark.line.length >= 2 && (
        <polyline points={mark.line.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
          fill="none" stroke={color} strokeWidth={.09} strokeDasharray=".5 .22" opacity=".8"
          data-select={select} cursor="pointer" />
      )}
      {points.map(([x, y], index) => <g key={index}>
        <g data-select={select} cursor="pointer">
          <circle cx={x + .5} cy={y + .5} r={.34} fill="#06090c" fillOpacity=".72"
            stroke={color} strokeWidth={.09} {...dashProps} />
          <text x={x + .5} y={y + .68} textAnchor="middle" fontSize=".44" fill={color}
            pointerEvents="none">{geometry.icon}</text>
          <title>{`${group.name} — ${geometry.label} (${phaseName})`}</title>
        </g>
        {editable && <Handle cx={x + .5} cy={y + .5} r={.42}
          onDown={(event) => startDrag(event, { kind: 'sigpoint', groupId: group.id, phaseIndex, classId, index })} />}
      </g>)}
    </g>);
  }

  if (!dim) {
    const anchor = jobAnchor(job);
    if (anchor) {
      parts.push(<ChipLabel key="chip"
        x={anchor[0] + .5}
        y={first ? anchor[1] - .48 : anchor[1] + 2.15}
        text={first
          ? `${group.name.toUpperCase()} · ${phaseName.toUpperCase()}`
          : `${group.name.toUpperCase()} · ${phaseName.toUpperCase()}`}
        color={color} select={select} />);
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
  const tactic = document.tactics.find((value) => value.id === task.tacticId);
  // The in-progress geometry draws in the task's own colour: the group's for
  // job and signature tasks, the watch chrome's for condition boxes.
  const taskColor = task.kind === 'watch'
    ? (task.target.scope === 'start' ? '#e6ddd4'
      : task.target.row === 'cleared' ? '#38d9c5'
        : task.target.row === 'notFighting' ? '#f0b35a' : '#c8763c')
    : task.kind === 'carry' ? '#b49cff'
      : (tactic ? groupColor(tactic, task.groupId) : '#e6ddd4');
  let hint = task.kind === 'job'
    ? task.hint ?? (task.phaseIndex === 0 ? hints?.get(task.groupId) ?? null : null)
    : null;
  let hasGeometry = task.kind === 'job' && taskGeometryExists(task, document);
  if (task.kind === 'watch' && tactic) {
    // A watch task pulses the currently watched preset zone until a custom
    // box has been dragged in its place.
    const conds = watchConds(tactic, task.target);
    const holder = task.target.row === 'cleared' ? conds?.cleared
      : task.target.row === 'notFighting' ? conds?.notFighting
        : conds?.enemiesAt;
    if (holder) {
      const zone = holder.zones[task.target.zoneIndex];
      if (!zone) return null;
      hint = {
        rect: zone.rect,
        text: task.target.row === 'cleared'
          ? 'Your team must freshly check this whole ground.'
          : task.target.row === 'notFighting'
            ? 'No friendly Fight may intersect this ground.'
            : 'Enemies are counted inside the box.',
      };
      hasGeometry = !zone.name && Boolean(zone.rect);
    }
  }
  if (hint && !hasGeometry) {
    const [x0, y0, x1, y1] = hint.rect;
    parts.push(<rect key="hint" x={x0} y={y0} width={x1 - x0 + 1} height={y1 - y0 + 1}
      fill="none" stroke="#fff1d8" strokeWidth={.09} strokeDasharray=".4 .26" rx={.2}
      pointerEvents="none" className="animate-pulse" />);
  }
  if (task.kind === 'job' && task.field === 'via' && task.postIndex != null
    && tactic) {
    // Drawing one post's way in: ring the target post and name its bot, so
    // there is never a doubt whose route the clicks belong to.
    const group = tactic.groups.find((value) => value.id === task.groupId);
    const post = tactic.phases[task.phaseIndex]?.jobs[task.groupId]
      ?.posts?.[task.postIndex];
    if (group && post) {
      const [px, py] = post.at;
      const bot = document.squad[group.slots[task.postIndex]];
      parts.push(<g key="via-target" pointerEvents="none">
        <circle cx={px + .5} cy={py + .5} r={.85} fill="none" stroke="#fff1d8"
          strokeWidth={.1} className="animate-pulse" />
        <circle cx={px + .5} cy={py + .5} r={.62} fill="none" stroke={taskColor}
          strokeWidth={.08} strokeDasharray=".18 .12" />
      </g>);
      parts.push(<ChipLabel key="via-target-chip" x={px + .5} y={py - .85}
        text={`${(bot?.name ?? `POST ${task.postIndex + 1}`).toUpperCase()} — WAY IN`}
        color="#fff1d8" />);
    }
  }
  if (rectDraft) {
    const x0 = Math.min(rectDraft.start[0], rectDraft.cur[0]);
    const y0 = Math.min(rectDraft.start[1], rectDraft.cur[1]);
    const x1 = Math.max(rectDraft.start[0], rectDraft.cur[0]);
    const y1 = Math.max(rectDraft.start[1], rectDraft.cur[1]);
    parts.push(<rect key="rect-draft" x={x0} y={y0} width={x1 - x0 + 1} height={y1 - y0 + 1}
      fill={`${taskColor}35`} stroke="#fff1d8" strokeWidth={.14} pointerEvents="none" />);
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
  task: Extract<MapTask, { kind: 'job' }>,
  document: TacticsDocument,
): boolean {
  const tactic = document.tactics.find((value) => value.id === task.tacticId);
  const job = tactic?.phases[task.phaseIndex]?.jobs[task.groupId];
  if (!job) return false;
  if (task.field === 'via') {
    return task.postIndex != null
      ? (job.posts?.[task.postIndex]?.via?.length ?? 0) > 0
      : (job.via?.length ?? 0) > 0;
  }
  return Boolean(job.posts?.length || job.rect || (job.path?.length ?? 0) > 0 || job.point);
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

function ChipLabel({ x, y, text, color, select, opacity = 1, mapWidth = 31, mapHeight = 27 }: {
  x: number;
  y: number;
  text: string;
  color: string;
  select?: string;
  opacity?: number;
  mapWidth?: number;
  mapHeight?: number;
}) {
  const w = text.length * .27 + .5;
  const cx = Math.min(Math.max(x - w / 2, .2), mapWidth - w - .2);
  const cy = Math.min(Math.max(y - .62, .2), mapHeight - 1);
  return <g opacity={opacity} data-select={select} cursor={select ? 'pointer' : undefined}>
    <rect x={cx} y={cy} width={w} height={.78} rx={.16} fill="#06090c" fillOpacity=".82"
      stroke={color} strokeWidth={.05} />
    <text x={cx + w / 2} y={cy + .55} textAnchor="middle" fontSize=".42" letterSpacing=".03"
      fontWeight="650" fill={color}>{text}</text>
  </g>;
}

export function jobAnchor(job: TacticJob | null): Point | null {
  if (!job) return null;
  if (job.posts?.length) return job.posts[0].at;
  if (job.rect) return [job.rect[0], job.rect[1]];
  if (job.path?.length) {
    return job.kind === 'advance' || job.kind === 'patrol'
      ? job.path[Math.floor(job.path.length / 2)]
      : job.path[0];
  }
  if (job.point) return job.point;
  if (job.kind === 'return') return [2, 5];
  return null;
}

function jobCentroid(job: TacticJob, pathfinder: ReturnType<typeof makePathfinder>): Point {
  let tiles: Point[] = [];
  if (job.posts?.length) tiles = job.posts.map((post) => post.at);
  else if (job.rect) {
    tiles = [[Math.round((job.rect[0] + job.rect[2]) / 2), Math.round((job.rect[1] + job.rect[3]) / 2)]];
  } else if (job.path?.length) tiles = job.path;
  else if (job.point) tiles = [job.point];
  if (!tiles.length) return [3, 5];
  const sx = Math.round(tiles.reduce((sum, [x]) => sum + x, 0) / tiles.length);
  const sy = Math.round(tiles.reduce((sum, [, y]) => sum + y, 0) / tiles.length);
  return pathfinder.nearestFloor([sx, sy]);
}

function jobStarts(
  previous: TacticJob,
  group: TacticGroup,
  pathfinder: ReturnType<typeof makePathfinder>,
): Point[] {
  const count = Math.max(1, group.slots.length);
  const sample = (tiles: Point[]): Point[] => {
    if (!tiles.length) return [jobCentroid(previous, pathfinder)];
    if (tiles.length <= count) return tiles;
    return Array.from({ length: count }, (_, index) =>
      tiles[Math.round(index * (tiles.length - 1) / Math.max(1, count - 1))]);
  };
  if (previous.posts?.length) return previous.posts.map((post) => post.at);
  if (previous.rect) {
    const [x0, y0, x1, y1] = previous.rect;
    const tiles: Point[] = [];
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      if (pathfinder.floor(x, y)) tiles.push([x, y]);
    }
    return sample(tiles);
  }
  if (previous.path && previous.path.length > 1) return sample(pathfinder.connect(previous.path));
  if (previous.point) return [previous.point];
  return [[3, 5]];
}

function chebyshev([ax, ay]: Point, [bx, by]: Point): number {
  return Math.max(Math.abs(ax - bx), Math.abs(ay - by));
}

/** The watched Well's map centre for a core-loose ring. Team-relative
 * choices resolve from the west perspective the whole canvas draws. */
function wellCentre(catalog: TacticalSheetCatalog, choice: WellChoice): Point | null {
  const id = choice === 'centre' ? 'well-centre'
    : choice === 'north' || choice === 'own' ? 'well-north'
      : 'well-south';
  const region = catalog.map.regions.find((value) => value.id === id);
  if (!region || region.tiles.length === 0) return null;
  const cx = region.tiles.reduce((sum, tile) => sum + tile.x + .5, 0) / region.tiles.length;
  const cy = region.tiles.reduce((sum, tile) => sum + tile.y + .5, 0) / region.tiles.length;
  return [cx, cy];
}

export function describeGeometry(job: TacticJob, group: TacticGroup): string {
  const kind = job.kind;
  if (kind === 'posts') {
    const count = job.posts?.length ?? 0;
    if (!count) return `No posts yet — needs one per bot (${group.slots.length}).`;
    const where = describePlace(...job.posts![0].at);
    return `${count} post${count === 1 ? '' : 's'} at ${where}${count === group.slots.length ? '' : ` (${group.slots.length} bots)`}`;
  }
  if (kind === 'area') {
    if (!job.rect) return 'No area yet — drag a box on the map.';
    return `Area near ${describePlace(Math.round((job.rect[0] + job.rect[2]) / 2), Math.round((job.rect[1] + job.rect[3]) / 2))}`;
  }
  if (kind === 'line') {
    if (!job.path || job.path.length < 2) return 'No line yet — click two or more points.';
    return `Picket line through ${describePlace(...job.path[0])}`;
  }
  if (kind === 'approach' || kind === 'converge') {
    if (!job.point) {
      return kind === 'approach' ? 'No approach point yet — click the map.' : 'No destination yet — click the map.';
    }
    return `${kind === 'approach' ? 'Approach' : 'Converge on'} ${describePlace(...job.point)}`;
  }
  if (kind === 'advance' || kind === 'patrol') {
    if (!job.path || job.path.length < 2) return 'No route yet — click waypoints in travel order.';
    return `${job.path.length}-point ${kind === 'patrol' ? 'loop' : 'route'} toward ${describePlace(...job.path[job.path.length - 1])}`;
  }
  return '';
}
