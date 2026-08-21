import { useMemo, useRef, useState } from 'react';
import type { TacticalSheetCatalog } from '../api';
import { BaseMap } from './BaseMap';
import { projectPhaseBots, slotsInMapRect } from './botProjection';
import {
  facingFromPointer, materializeSpatialJob, rotatePostSelection,
  translatePostSelection,
} from './mapFirstSpatial';
import { classSprite } from './classArt';
import { makePathfinder } from './pathing';
import { describePlace } from './places';
import type { MapTask, WatchTarget } from './useMapTask';
import {
  FACING_DELTAS, effectiveJob, effectiveJobResolution, groupColor, jobOf, phaseShade,
  postFacingIssue, signatureMarkGeometry, tilePlacementIssue,
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
  | { tacticId: string; part: 'bot'; groupId: string; phaseIndex: number; slot: number }
  | { tacticId: string; part: 'post'; groupId: string; phaseIndex: number; postIndex: number };

type DragTarget =
  | { kind: 'post'; groupId: string; phaseIndex: number; index: number; slot: number; slots: number[]; origin: Point }
  | { kind: 'postface'; groupId: string; phaseIndex: number; index: number; slot: number; slots: number[] }
  | { kind: 'waypoint'; groupId: string; phaseIndex: number; index: number }
  | { kind: 'point'; groupId: string; phaseIndex: number }
  | { kind: 'rectmove'; groupId: string; phaseIndex: number }
  | { kind: 'rectsize'; groupId: string; phaseIndex: number }
  | { kind: 'sigpoint'; groupId: string; phaseIndex: number; slot: number; index: number }
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
  showAll, showAllPhases, showProjectedBots = false,
  showAdjacentPhases = true, allowInheritedSpatialEditing = false,
  selectedSlots = [], showVision = false,
  showAttackRange = false, showSignatureRange = false, task, hints,
  onTilePicked, onRectDrawn, onGeometryLive, onGestureStart, onGestureEnd,
  onSelect, onClearSelection, onTaskDone, onTileRejected, onPostsSwapped,
  onPostBotDropped, onGroupDropped, onSlotsSelected,
}: {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  editingTacticId: string | null;
  /** The wizard's storyboard selection; later phases render dimmer. */
  activePhaseIndex: number | null;
  selectedTacticId: string | null;
  showAll: boolean;
  showAllPhases: boolean;
  /** Presentation-only stable bot positions for non-Post assignments. */
  showProjectedBots?: boolean;
  /** Classic keeps its historical dim neighbours. Map-first defaults to one
   * clear active layer; its existing checkbox can still opt into all phases. */
  showAdjacentPhases?: boolean;
  /** Map-first copy-on-write permits handles on inherited Posts/Patrol. */
  allowInheritedSpatialEditing?: boolean;
  selectedSlots?: readonly number[];
  showVision?: boolean;
  showAttackRange?: boolean;
  showSignatureRange?: boolean;
  task: MapTask | null;
  hints: Map<string, { rect: Rect; text: string }> | null;
  onTilePicked: (x: number, y: number) => void;
  onRectDrawn: (rect: Rect) => void;
  onGeometryLive: (edit: GeometryEdit) => void;
  onGestureStart: () => void;
  onGestureEnd: () => void;
  onSelect: (ref: MapSelectRef, additive?: boolean) => void;
  onClearSelection?: () => void;
  /** A freehand map marquee selected every visible bot tile in its bounds. */
  onSlotsSelected?: (slots: number[], additive: boolean) => void;
  /** Finish an open route task (double-click, mirroring the Done button). */
  onTaskDone?: () => void;
  /** A draw-task click landed on an illegal tile; the reason is ready to show. */
  onTileRejected?: (reason: string) => void;
  /** A post drop landed on a groupmate and the two swapped tiles. */
  onPostsSwapped?: () => void;
  /** A Map-first tray bot was dropped to author its exact Post. */
  onPostBotDropped?: (groupId: string, slot: number, at: Point) => void;
  /** A Map-first Group tray was dropped to place/move its whole formation. */
  onGroupDropped?: (groupId: string, at: Point) => void;
}) {
  const svgRef = useRef<SVGSVGElement | null>(null);
  const [hover, setHover] = useState<Point | null>(null);
  const [rectDraft, setRectDraft] = useState<{ start: Point; cur: Point } | null>(null);
  const [selectionDraft, setSelectionDraft] = useState<{
    start: Point; cur: Point; press: [number, number]; additive: boolean;
  } | null>(null);
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

  function clientToTile(event: { clientX: number; clientY: number }): Point | null {
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
      return;
    }
    const selectable = (event.target as Element).closest('[data-select]');
    if (!task && editingTacticId && onSlotsSelected && tile && !selectable
      && event.button === 0) {
      setSelectionDraft({
        start: tile, cur: tile, press: [event.clientX, event.clientY],
        additive: event.metaKey || event.ctrlKey || event.shiftKey,
      });
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
    if (selectionDraft && tile) {
      setSelectionDraft({ ...selectionDraft, cur: tile });
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
        }, event.metaKey || event.ctrlKey || event.shiftKey);
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
    if (selectionDraft) {
      const draft = selectionDraft;
      setSelectionDraft(null);
      const moved = Math.hypot(
        event.clientX - draft.press[0], event.clientY - draft.press[1]) > 7;
      if (moved && editingTacticId) {
        const tactic = document.tactics.find((value) => value.id === editingTacticId);
        if (tactic) {
          const projection = projectPhaseBots(
            catalog, document, tactic, activePhaseIndex ?? 0);
          onSlotsSelected?.(
            slotsInMapRect(projection.bots, [draft.start, draft.cur]),
            draft.additive,
          );
        }
        pressRef.current = null;
        return;
      }
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
        onSelect({ tacticId, part, groupId: a, phaseIndex: Number(b) },
          event.metaKey || event.ctrlKey || event.shiftKey);
      } else if (part === 'post') {
        onSelect({
          tacticId, part, groupId: a,
          phaseIndex: Number(b), postIndex: Number(c),
        }, event.metaKey || event.ctrlKey || event.shiftKey);
      } else if (part === 'bot') {
        onSelect({
          tacticId, part, groupId: a,
          phaseIndex: Number(b), slot: Number(c),
        }, event.metaKey || event.ctrlKey || event.shiftKey);
      } else if (part === 'phase') {
        onSelect({ tacticId, part, phaseIndex: Number(a) });
      } else {
        onSelect({ tacticId, part: part as 'start' | 'cores' });
      }
    } else if (!task) {
      onClearSelection?.();
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
      const job = allowInheritedSpatialEditing
        && (drag.kind === 'post' || drag.kind === 'postface' || drag.kind === 'waypoint')
        ? materializeSpatialJob(tactic, drag.phaseIndex, drag.groupId)
        : tactic.phases[drag.phaseIndex]?.jobs[drag.groupId];
      if (!job) return;
      if (job.posts) {
        const owner = tactic.groups.find((group) => group.id === drag.groupId);
        job.posts.forEach((post, index) => {
          if (post.slot == null) post.slot = owner?.slots[index];
        });
      }
      if (drag.kind === 'postface') {
        const post = job.posts?.[drag.index];
        if (!post) return;
        const candidate = facingFromPointer(post.at, [x, y]);
        if (!candidate) return;
        if (!allowInheritedSpatialEditing) {
          if (!postFacingIssue(catalog, { at: post.at, facing: candidate })) post.facing = candidate;
          return;
        }
        if (!rotatePostSelection(catalog, job.posts ?? [], drag.slots, candidate)) return;
        for (const target of job.posts ?? []) {
          if (target.slot != null && drag.slots.includes(target.slot)) target.facing = candidate;
        }
        return;
      }
      if (drag.kind === 'sigpoint') {
        const plan = job.sigs?.[String(drag.slot)];
        const points = plan?.points ?? plan?.line;
        if (!points) return;
        points[drag.index] = [x, y];
        return;
      }
      if (drag.kind === 'post') {
        if (!allowInheritedSpatialEditing) {
          if (job.posts?.[drag.index]) job.posts[drag.index].at = [x, y];
          return;
        }
        const translated = translatePostSelection(
          catalog, job.posts ?? [], drag.slots, drag.slot, [x, y]);
        if (!translated) return;
        for (const post of job.posts ?? []) {
          const at = post.slot == null ? undefined : translated.positions.get(post.slot);
          if (at) post.at = at;
          const facing = post.slot == null ? undefined : translated.facings.get(post.slot);
          if (facing) post.facing = facing;
        }
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

  /** Map-first selection translation already resolves collisions. Classic
   * retains its established single-Post swap behavior unchanged. */
  function finishDrag(drag: DragTarget) {
    if (drag.kind !== 'post' || allowInheritedSpatialEditing) return;
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
      style={{
        aspectRatio: `${width}/${height}`,
        touchAction: task || editingTacticId ? 'none' : undefined,
        cursor: editingTacticId && !task ? 'crosshair' : undefined,
      }}
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
      onDragOver={(event) => {
        if ((onPostBotDropped && event.dataTransfer.types.includes('application/x-nilbots-post'))
          || (onGroupDropped && event.dataTransfer.types.includes('application/x-nilbots-group'))) {
          event.preventDefault();
          event.dataTransfer.dropEffect = 'move';
        }
      }}
      onDrop={(event) => {
        const groupRaw = onGroupDropped
          ? event.dataTransfer.getData('application/x-nilbots-group') : '';
        const raw = onPostBotDropped
          ? event.dataTransfer.getData('application/x-nilbots-post') : '';
        if (!groupRaw && !raw) return;
        event.preventDefault();
        const tile = clientToTile(event);
        if (!tile) return;
        try {
          if (groupRaw) {
            const payload = JSON.parse(groupRaw) as { groupId?: unknown };
            if (typeof payload.groupId === 'string') onGroupDropped?.(payload.groupId, tile);
            return;
          }
          const payload = JSON.parse(raw) as { groupId?: unknown; slot?: unknown };
          if (typeof payload.groupId === 'string' && Number.isInteger(payload.slot)) {
            onPostBotDropped?.(payload.groupId, Number(payload.slot), tile);
          }
        } catch {
          // Ignore foreign/expired drag payloads.
        }
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
                activePhaseIndex={activePhaseIndex} showAllPhases={showAllPhases}
                showAdjacentPhases={showAdjacentPhases} />
              {tactic.phases.map((phase, phaseIndex) => {
                const focus = activePhaseIndex ?? 0;
                const distance = Math.abs(phaseIndex - focus);
                if (!showAllPhases && distance > (showAdjacentPhases ? 1 : 0)) return null;
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
                      allowInheritedSpatialEditing={allowInheritedSpatialEditing}
                      selectedSlots={selectedSlots}
                      startDrag={startDrag} pathfinder={path} />
                  ))}
                  {phase.custody?.via && phase.custody.via.length > 1
                    && phase.custody.plan !== 'keep' && (() => {
                      const home: Point = [2, 5];
                      const run = path.connectSafe([...phase.custody.via, home]);
                      if (!run) return null;
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
              {showProjectedBots && !dim && (
                <BotProjectionLayer catalog={catalog} document={document}
                  tactic={tactic} phaseIndex={activePhaseIndex ?? 0}
                  pathfinder={path} selectedSlots={selectedSlots} />
              )}
              {!dim && (showVision || showAttackRange || showSignatureRange) && (
                <SelectedBotRanges catalog={catalog} document={document} tactic={tactic}
                  phaseIndex={activePhaseIndex ?? 0} selectedSlots={selectedSlots}
                  showVision={showVision} showAttackRange={showAttackRange}
                  showSignatureRange={showSignatureRange} />
              )}
            </g>
            {tagAnchor && tag && <ChipLabel x={tagAnchor[0] + .5} y={tagAnchor[1] - .35}
              text={tag} color="#7694a8" opacity={.9} mapWidth={width} mapHeight={height} />}
          </g>
        );
      })}
      {selectionDraft && (() => {
        const x = Math.min(selectionDraft.start[0], selectionDraft.cur[0]);
        const y = Math.min(selectionDraft.start[1], selectionDraft.cur[1]);
        const w = Math.abs(selectionDraft.start[0] - selectionDraft.cur[0]) + 1;
        const h = Math.abs(selectionDraft.start[1] - selectionDraft.cur[1]) + 1;
        return <rect data-testid="map-selection-marquee"
          x={x + .05} y={y + .05} width={w - .1} height={h - .1} rx={.16}
          fill="#e9fbff" fillOpacity=".1" stroke="#e9fbff" strokeWidth=".09"
          strokeDasharray=".2 .12" pointerEvents="none" />;
      })()}
      <TaskLayer task={task} hover={hover} rectDraft={rectDraft}
        hints={hints} pathfinder={path} document={document} />
    </svg>
  );
}

/** The bots implied by Area/Converge/Patrol are deliberately a preview: the
 * authored geometry remains the contract. Exact Posts are already drawn by
 * JobOverlay and are excluded here so a bot never appears twice. */
export function BotProjectionLayer({
  catalog, document, tactic, phaseIndex, pathfinder, selectedSlots = [],
}: {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  tactic: Tactic;
  phaseIndex: number;
  pathfinder: ReturnType<typeof makePathfinder>;
  selectedSlots?: readonly number[];
}) {
  const projection = projectPhaseBots(catalog, document, tactic, phaseIndex);
  const previous = phaseIndex > 0
    ? projectPhaseBots(catalog, document, tactic, phaseIndex - 1)
    : null;
  const projected = projection.bots.filter((bot) => !bot.exact);
  return <g data-testid="bot-projection-layer">
    <g opacity=".4" pointerEvents="none">
      {previous && projection.bots.map((bot) => {
        const from = previous.bots.find((candidate) => candidate.slot === bot.slot);
        if (!from || (from.at[0] === bot.at[0] && from.at[1] === bot.at[1])) return null;
        const route = pathfinder.findPath(from.at, bot.at);
        if (!route) return null;
        const color = groupColor(tactic, bot.groupId);
        return <polyline key={`continuity-${bot.slot}`}
          data-testid={`bot-continuity-${bot.slot}`}
          points={route.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
          fill="none" stroke={color} strokeWidth={.075} strokeDasharray=".1 .22"
          strokeLinejoin="round" strokeLinecap="round" />;
      })}
    </g>
    {projected.map((projectionBot) => {
      const bot = document.squad[projectionBot.slot];
      if (!bot) return null;
      const sprite = classSprite(bot.class);
      const color = groupColor(tactic, projectionBot.groupId);
      const [x, y] = projectionBot.at;
      const select = `${tactic.id}:bot:${projectionBot.groupId}:${phaseIndex}:${projectionBot.slot}`;
      return <g key={`${projectionBot.groupId}-${projectionBot.slot}`}
        data-select={select} cursor="pointer"
        data-testid={`projected-bot-${projectionBot.slot}`}>
        <circle cx={x + .5} cy={y + .5} r={.47} fill="#06090c" fillOpacity=".72"
          stroke={color} strokeWidth={.075} strokeDasharray=".12 .09" />
        {selectedSlots.includes(projectionBot.slot) && <circle cx={x + .5} cy={y + .5}
          r={.61} fill="none" stroke="#e9fbff" strokeWidth={.09} />}
        {sprite
          ? <image href={sprite} x={x} y={y} width={1} height={1} opacity=".9" />
          : <text x={x + .5} y={y + .64} textAnchor="middle" fontSize=".38"
            fontWeight="800" fill="#e9fbff">{projectionBot.slot + 1}</text>}
        <circle cx={x + .82} cy={y + .18} r={.14} fill="#06090c" stroke={color}
          strokeWidth={.045} />
        <text x={x + .82} y={y + .235} textAnchor="middle" fontSize=".16"
          fontWeight="800" fill={color}>P</text>
        <title>{`${bot.name} — Projected ${projectionBot.jobKind} position${projectionBot.carried
          ? `, carried from ${tactic.phases[projectionBot.sourcePhaseIndex]?.name}` : ''}. Preview only.`}</title>
      </g>;
    })}
    {projection.unresolvedGroupIds.map((groupId, index) => {
      const group = tactic.groups.find((candidate) => candidate.id === groupId);
      if (!group) return null;
      return <ChipLabel key={`unresolved-${groupId}`} x={5.2} y={25.8 - index}
        text={`${group.name.toUpperCase()} · NO POSITION PREVIEW`}
        color={groupColor(tactic, groupId)} opacity={.9}
        mapWidth={catalog.map.width} mapHeight={catalog.map.height} />;
    })}
  </g>;
}

/** Selected-only tactical reach. Values come from the server catalog's
 * authoritative current actor rules; legacy catalogs simply omit a layer
 * rather than guessing a distance. Vision is drawn only where an authored
 * Post supplies a real facing. */
export function SelectedBotRanges({
  catalog, document, tactic, phaseIndex, selectedSlots, showVision, showAttackRange,
  showSignatureRange = false,
}: {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  tactic: Tactic;
  phaseIndex: number;
  selectedSlots: readonly number[];
  showVision: boolean;
  showAttackRange: boolean;
  showSignatureRange?: boolean;
}) {
  const projection = projectPhaseBots(catalog, document, tactic, phaseIndex);
  return <g data-testid="selected-bot-ranges" pointerEvents="none">
    {projection.bots.filter((bot) => selectedSlots.includes(bot.slot)).map((projected) => {
      const bot = document.squad[projected.slot];
      const classEntry = catalog.classes.find((entry) => entry.id === bot?.class) as
        | (TacticalSheetCatalog['classes'][number] & {
          visionRange?: number;
          visionProximityRange?: number;
          attackRange?: number;
        }) | undefined;
      if (!bot || !classEntry) return null;
      const resolution = effectiveJobResolution(tactic, phaseIndex, projected.groupId);
      const post = resolution?.job.kind === 'posts'
        ? resolution.job.posts?.find((value, index) =>
          (value.slot ?? tactic.groups.find((group) => group.id === projected.groupId)?.slots[index])
            === projected.slot)
        : null;
      const [x, y] = projected.at;
      const cx = x + .5;
      const cy = y + .5;
      const color = groupColor(tactic, projected.groupId);
      const facingAngle: Record<Facing, number> = { e: 0, s: Math.PI / 2, w: Math.PI, n: -Math.PI / 2 };
      const angle = post ? facingAngle[post.facing] : null;
      const vision = classEntry.visionRange ?? 0;
      const proximity = classEntry.visionProximityRange ?? 0;
      const attack = classEntry.attackRange ?? 0;
      const signature = classEntry.signatureAuthoring.engagementRange ?? 0;
      const signatureDirectional = ['direction', 'heading']
        .includes(classEntry.signatureAuthoring.argumentKind);
      const half = Math.PI / 4;
      const sector = angle == null || vision <= 0 ? null : (() => {
        const x1 = cx + Math.cos(angle - half) * vision;
        const y1 = cy + Math.sin(angle - half) * vision;
        const x2 = cx + Math.cos(angle + half) * vision;
        const y2 = cy + Math.sin(angle + half) * vision;
        return `M ${cx} ${cy} L ${x1} ${y1} A ${vision} ${vision} 0 0 1 ${x2} ${y2} Z`;
      })();
      const signatureSector = angle == null || signature <= 0 || !signatureDirectional
        ? null : (() => {
          const x1 = cx + Math.cos(angle - half) * signature;
          const y1 = cy + Math.sin(angle - half) * signature;
          const x2 = cx + Math.cos(angle + half) * signature;
          const y2 = cy + Math.sin(angle + half) * signature;
          return `M ${cx} ${cy} L ${x1} ${y1} A ${signature} ${signature} 0 0 1 ${x2} ${y2} Z`;
        })();
      return <g key={`range-${projected.slot}`} data-testid={`selected-bot-range-${projected.slot}`}>
        {showVision && sector && <path d={sector} fill={`${color}16`} stroke={color}
          strokeWidth={.07} strokeDasharray=".16 .12" />}
        {showVision && proximity > 0 && <circle cx={cx} cy={cy} r={proximity}
          fill="none" stroke={color} strokeWidth={.055} strokeDasharray=".08 .14" opacity={.65} />}
        {showAttackRange && attack > 0 && <circle cx={cx} cy={cy} r={attack}
          fill="none" stroke="#f0b35a" strokeWidth={.075} strokeDasharray=".22 .13" opacity={.8} />}
        {showSignatureRange && signatureDirectional && signatureSector &&
          <path d={signatureSector} fill="#c084fc14" stroke="#c084fc"
            strokeWidth={.075} strokeDasharray=".12 .12" />}
        {showSignatureRange && !signatureDirectional && signature > 0 &&
          <circle cx={cx} cy={cy} r={signature} fill="none" stroke="#c084fc"
            strokeWidth={.075} strokeDasharray=".12 .12" opacity={.9} />}
        <title>{`${bot.name}${showVision && angle == null ? ': vision unavailable without an exact facing' : ''}${showSignatureRange && signatureDirectional && angle == null ? ': signature direction unavailable without an exact facing' : ''}`}</title>
      </g>;
    })}
  </g>;
}

function ConditionFootprints({
  tactic, catalog, dim, editable, startDrag, pathfinder,
  activePhaseIndex, showAllPhases, showAdjacentPhases,
}: {
  tactic: Tactic;
  catalog: TacticalSheetCatalog;
  dim: boolean;
  editable: boolean;
  startDrag: (event: React.PointerEvent, target: DragTarget) => void;
  pathfinder: ReturnType<typeof makePathfinder>;
  activePhaseIndex: number | null;
  showAllPhases: boolean;
  showAdjacentPhases: boolean;
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
    if (!showAllPhases
      && Math.abs(phaseIndex - focus) > (showAdjacentPhases ? 1 : 0)) return;
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
export function JobOverlay({
  tactic, group, phaseIndex, bots, catalog, dim, editable,
  allowInheritedSpatialEditing = false, selectedSlots = [], startDrag, pathfinder,
}: {
  tactic: Tactic;
  group: TacticGroup;
  phaseIndex: number;
  bots: SquadBot[];
  catalog: TacticalSheetCatalog;
  dim: boolean;
  editable: boolean;
  allowInheritedSpatialEditing?: boolean;
  selectedSlots?: readonly number[];
  startDrag: (event: React.PointerEvent, target: DragTarget) => void;
  pathfinder: ReturnType<typeof makePathfinder>;
}) {
  const raw = jobOf(tactic, phaseIndex, group.id);
  const resolution = effectiveJobResolution(tactic, phaseIndex, group.id);
  if (!raw || !resolution) return null;
  const job = resolution.job;
  const inherited = resolution.inherited;
  const geometryEditable = editable && (!inherited
    || (allowInheritedSpatialEditing && (job.kind === 'posts' || job.kind === 'patrol')));
  const color = phaseShade(
    groupColor(tactic, group.id), phaseIndex, tactic.phases.length);
  const select = `${tactic.id}:group:${group.id}:${phaseIndex}`;
  const parts: React.ReactNode[] = [];
  const first = phaseIndex === 0;
  const phaseName = tactic.phases[phaseIndex]?.name ?? `Phase ${phaseIndex + 1}`;
  const sourcePhaseName = tactic.phases[resolution.sourcePhaseIndex]?.name
    ?? `Phase ${resolution.sourcePhaseIndex + 1}`;
  const previous = !inherited && phaseIndex > 0
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
        const run = pathfinder.findPath(from, to);
        if (run) parts.push(<ArrowPath key="onward" points={run} color={color}
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
        {geometryEditable && <>
          <Handle cx={x1 + 1} cy={y1 + 1}
            onDown={(event) => startDrag(event, { kind: 'rectsize', groupId: group.id, phaseIndex })} />
          <Handle cx={x0} cy={y0} cursor="move"
            onDown={(event) => startDrag(event, { kind: 'rectmove', groupId: group.id, phaseIndex })} />
        </>}
      </g>);
    }
    if (job.path && job.path.length > 1) {
      const points = pathfinder.connectSafe(job.path);
      parts.push(<g key="path">
        {points && <polyline points={points.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
          fill="none" stroke={color} strokeWidth={.13} strokeDasharray=".3 .2"
          strokeLinejoin="round" strokeLinecap="round" data-select={select} cursor="pointer" />}
        {job.path.map(([x, y], index) => geometryEditable
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
        {geometryEditable
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
        const slot = post.slot ?? group.slots[index];
        const entry = bot ? catalog.classes.find((value) => value.id === bot.class) : null;
        const sprite = bot ? classSprite(bot.class) : undefined;
        // Facing angle in degrees: sprites are authored facing east (0°), the
        // same convention as the arena renderer.
        const deg = Math.round(angle * 180 / Math.PI);
        // Post-scoped select: clicking a posted bot lights up its own way-in
        // row in the drawer, not just the group section.
        const postSelect = `${tactic.id}:post:${group.id}:${phaseIndex}:${index}`;
        parts.push(<g key={`post-${index}`}>
          {selectedSlots.includes(slot) && <circle cx={cx} cy={cy} r={.63}
            fill="none" stroke="#e9fbff" strokeWidth={.09} pointerEvents="none" />}
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
              <title>{inherited
                ? `${bot.name} (${entry?.name ?? bot.class}) — carried from ${sourcePhaseName}`
                : `${bot.name} (${entry?.name ?? bot.class}) — drag the arrow tip to turn it`}</title>
            </g>
            : <g data-select={postSelect} cursor="pointer">
              <circle cx={cx} cy={cy} r={.36} fill="#10272f" stroke={color} strokeWidth={.12} />
              <text x={cx} y={y + .64} textAnchor="middle" fontSize=".38" fontWeight="800"
                fill="#e9fbff" pointerEvents="none">{index + 1}</text>
              <title>{bot
                ? inherited
                  ? `${bot.name} (${entry?.name ?? bot.class}) — carried from ${sourcePhaseName}`
                  : `${bot.name} (${entry?.name ?? bot.class}) — drag the arrow tip to turn it`
                : `${group.name} post ${index + 1}`}</title>
            </g>}
          {post.via && post.via.length > 1 && (
            <g>
              {(() => {
                const wayIn = pathfinder.connectSafe([...post.via!, post.at]);
                return wayIn ? <polyline points={wayIn
                  .map(([px, py]) => `${px + .5},${py + .5}`).join(' ')}
                  fill="none" stroke={color} strokeWidth={.07} strokeDasharray=".1 .22"
                  strokeLinejoin="round" strokeLinecap="round" opacity=".75"
                  data-select={select} cursor="pointer" /> : null;
              })()}
              {post.via.map(([wx, wy], wi) =>
                <circle key={wi} cx={wx + .5} cy={wy + .5} r={.1} fill={color} opacity=".75" />)}
            </g>
          )}
          {geometryEditable && <>
            <Handle cx={cx} cy={cy} r={.42}
              onDown={(event) => startDrag(event, {
                kind: 'post', groupId: group.id, phaseIndex, index, slot,
                slots: selectedSlots.includes(slot) ? [...selectedSlots] : [slot], origin: [x, y],
              })} />
            <Handle cx={tipX} cy={tipY} r={.3}
              onDown={(event) => startDrag(event, {
                kind: 'postface', groupId: group.id, phaseIndex, index, slot,
                slots: selectedSlots.includes(slot) ? [...selectedSlots] : [slot],
              })} />
          </>}
        </g>);
      });
    }
  }

  if (job.kind === 'converge' && job.point) {
    const starts = inherited
      ? []
      : previous ? jobStarts(previous, group, pathfinder) : [[3, 5] as Point];
    parts.push(<g key="converge" data-testid="converge-fan">
      {starts.map((from, index) => {
        const run = pathfinder.findPath(from, job.point!);
        return run ? <ArrowPath key={index} points={run} color={color} select={select}
          width={starts.length > 1 ? .09 : .16} opacity={starts.length > 1 ? .75 : .92} /> : null;
      })}
      <g data-select={select} cursor="pointer">
        <circle cx={job.point[0] + .5} cy={job.point[1] + .5} r={.5} fill="none" stroke={color} strokeWidth={.1} />
        <circle cx={job.point[0] + .5} cy={job.point[1] + .5} r={.24} fill="none" stroke={color} strokeWidth={.1} />
        <circle cx={job.point[0] + .5} cy={job.point[1] + .5} r={.07} fill={color} />
      </g>
      {geometryEditable && <Handle cx={job.point[0] + .5} cy={job.point[1] + .5} r={.62}
        onDown={(event) => startDrag(event, { kind: 'point', groupId: group.id, phaseIndex })} />}
    </g>);
  }
  if ((job.kind === 'advance' || job.kind === 'patrol') && job.path && job.path.length > 1) {
    const loop = job.kind === 'patrol' ? [...job.path, job.path[0]] : job.path;
    const connected = pathfinder.connectSafe(loop);
    parts.push(<g key="mover-path">
      {connected && <ArrowPath points={connected} color={color} select={select} loop={job.kind === 'patrol'} />}
      {job.path.map(([x, y], index) => geometryEditable
        ? <Handle key={index} cx={x + .5} cy={y + .5}
          onDown={(event) => startDrag(event, { kind: 'waypoint', groupId: group.id, phaseIndex, index })} />
        : <circle key={index} cx={x + .5} cy={y + .5} r={.13} fill={color} />)}
    </g>);
  }
  if (job.kind === 'return') {
    const start = (previous && jobAnchor(previous)) ?? [3, 5] as Point;
    const home: Point = [2, 5];
    const run = pathfinder.findPath(start, home);
    parts.push(<g key="return">
      {run && <ArrowPath points={run} color={color} select={select} dash=".3 .2" />}
      <circle cx={home[0] + .5} cy={home[1] + .5} r={.55} fill="none" stroke={color}
        strokeWidth={.09} strokeDasharray=".22 .14" />
    </g>);
  }
  if (job.via && job.via.length > 1) {
    const connected = pathfinder.connectSafe(job.via);
    parts.push(<g key="via">
      {connected && <polyline points={connected.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
        fill="none" stroke={color} strokeWidth={.08} strokeDasharray=".12 .26"
        strokeLinejoin="round" strokeLinecap="round" opacity=".8"
        data-select={select} cursor="pointer" />}
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

  for (const [slotKey, plan] of Object.entries(job.sigs ?? {})) {
    const slot = Number(slotKey);
    const classId = bots.find((bot) => bot.slot === slot)?.class;
    if (!classId) continue;
    const geometry = signatureMarkGeometry(catalog, classId);
    if (!geometry) continue;
    const dashProps = first ? {} : { strokeDasharray: '.16 .12' };
    const points = plan.points ?? plan.line ?? [];
    parts.push(<g key={`sig-${slot}`}>
      {plan.line && plan.line.length >= 2 && (
        <polyline points={plan.line.map(([x, y]) => `${x + .5},${y + .5}`).join(' ')}
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
          onDown={(event) => startDrag(event, {
            kind: 'sigpoint', groupId: group.id, phaseIndex, slot, index,
          })} />}
      </g>)}
    </g>);
  }

  if (!dim) {
    const anchor = jobAnchor(job);
    if (anchor) {
      parts.push(<ChipLabel key="chip"
        x={anchor[0] + .5}
        y={first ? anchor[1] - .48 : anchor[1] + 2.15}
        text={`${group.name.toUpperCase()} · ${phaseName.toUpperCase()}${inherited
          ? ` · FROM ${sourcePhaseName.toUpperCase()}` : ''}`}
        color={color} select={select} passThrough />);
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

function ChipLabel({
  x, y, text, color, select, passThrough = false,
  opacity = 1, mapWidth = 31, mapHeight = 27,
}: {
  x: number;
  y: number;
  text: string;
  color: string;
  select?: string;
  passThrough?: boolean;
  opacity?: number;
  mapWidth?: number;
  mapHeight?: number;
}) {
  const w = text.length * .27 + .5;
  const cx = Math.min(Math.max(x - w / 2, .2), mapWidth - w - .2);
  const cy = Math.min(Math.max(y - .62, .2), mapHeight - 1);
  return <g opacity={opacity} data-select={select}
    pointerEvents={passThrough ? 'none' : undefined}
    cursor={select && !passThrough ? 'pointer' : undefined}>
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
