import { useEffect, useRef, useState } from 'react';
import type { TacticalSheetCatalog } from '../api';
import { classSprite } from './classArt';
import { projectPhaseBots } from './botProjection';
import { GeoStatusRow } from './GeoStatusRow';
import { Modal } from './Modal';
import { describePlace } from './places';
import { condClauses } from './summary';
import {
  TacticsIcon, TacticsIconButton, type TacticsIconName,
} from './TacticsIcons';
import {
  materializeSpatialJob, projectedPostsForLegacyJob,
} from './mapFirstSpatial';
import type { MapTask } from './useMapTask';
import {
  POSTURES, RECOVERIES, TRAVEL_BEHAVIORS,
  effectiveJobResolution, groupColor, phaseShade, planJobKindChange,
  signatureMarkGeometry, viaLegal,
  type JobKind, type Tactic, type TacticsDocument,
} from './tacticsDocument';

export type MapFirstFocus =
  | { kind: 'start' }
  | { kind: 'abort' }
  | { kind: 'groups' }
  | { kind: 'phase'; phaseIndex: number }
  | { kind: 'transition'; phaseIndex: number }
  | { kind: 'group'; phaseIndex: number; groupId: string }
  | { kind: 'bot'; phaseIndex: number; groupId: string; slot: number }
  | { kind: 'cores' };

function activeRows(cases: Array<{
  required?: Parameters<typeof condClauses>[0];
  triggers?: Parameters<typeof condClauses>[0];
  unless?: Parameters<typeof condClauses>[0];
}> | undefined): number {
  return (cases ?? []).reduce((total, entry) => total
    + condClauses(entry.required).length
    + condClauses(entry.triggers).length
    + condClauses(entry.unless).length, 0);
}

/** Tactic-wide controls sit above a real phase/transition chronology. Every
 * chip routes to the existing full editor; this component owns no condition
 * or transition mutations. */
export function MapFirstTimeline({
  tactic, phaseIndex, focus, gate, dirty, squadSize,
  canUndo, canRedo, onFocus, onSelectPhase, onAddPhase, onEditSquad,
  onUndo, onRedo, onCommit, onCancel,
}: {
  tactic: Tactic;
  phaseIndex: number;
  focus: MapFirstFocus | null;
  gate: string | null;
  dirty: boolean;
  squadSize: number;
  canUndo: boolean;
  canRedo: boolean;
  onFocus: (focus: MapFirstFocus) => void;
  onSelectPhase: (phaseIndex: number) => void;
  onAddPhase: () => void;
  onEditSquad: () => void;
  onUndo: () => void;
  onRedo: () => void;
  onCommit: () => void;
  onCancel: () => void;
}) {
  const startRows = activeRows(tactic.start.cases);
  const abortRows = activeRows(tactic.abort?.cases);
  const timelineRef = useRef<HTMLDivElement>(null);
  const [scrollState, setScrollState] = useState({ left: false, right: false });
  useEffect(() => {
    const timeline = timelineRef.current;
    if (!timeline) return;
    const update = () => setScrollState({
      left: timeline.scrollLeft > 2,
      right: timeline.scrollLeft + timeline.clientWidth < timeline.scrollWidth - 2,
    });
    update();
    timeline.addEventListener('scroll', update, { passive: true });
    const observer = new ResizeObserver(update);
    observer.observe(timeline);
    return () => {
      timeline.removeEventListener('scroll', update);
      observer.disconnect();
    };
  }, [tactic.phases.length]);
  return <div className="rounded-sm border border-arena-edge bg-arena-panel/80 p-1.5 shadow-sm"
    data-testid="map-first-timeline">
    <div className="mb-1.5 flex min-w-0 flex-wrap items-center gap-1.5 border-b border-arena-edge pb-1.5">
      <button type="button" className="btn shrink-0" onClick={onCancel}
        title={dirty ? 'Discard tactic changes and return to the Sheet' : 'Return to the Sheet'}>
        ← Back to Sheet
      </button>
      <TacticsIconButton icon="undo" label="Undo tactic edit"
        disabled={!canUndo} onClick={onUndo} />
      <TacticsIconButton icon="redo" label="Redo tactic edit"
        disabled={!canRedo} onClick={onRedo} />
      <span className="min-w-28 grow px-1">
        <span className="type-display block truncate text-[14px]">{tactic.name}</span>
        <span className={`block text-[9.5px] ${dirty ? 'text-arena-warning' : 'text-arena-dim'}`}>
          {dirty ? 'Unsaved tactic changes' : 'Editing tactic'}
        </span>
      </span>
      <nav className="flex shrink-0 items-center gap-1" aria-label="Tactic controls">
        <TacticsIconButton icon="start"
          label={`Start: ${tactic.start.mode === 'always' ? 'always' : `${startRows} condition${startRows === 1 ? '' : 's'}`}`}
          pressed={focus?.kind === 'start'} testId="map-first-start"
          onClick={() => onFocus({ kind: 'start' })} />
        <TacticsIconButton icon="core" label="Core plan"
          pressed={focus?.kind === 'cores'} onClick={() => onFocus({ kind: 'cores' })} />
        <TacticsIconButton icon="abort"
          label={`Abort: ${tactic.abort ? `${abortRows} condition${abortRows === 1 ? '' : 's'}` : 'not set'}`}
          configured={Boolean(tactic.abort)} pressed={focus?.kind === 'abort'}
          testId="map-first-abort" onClick={() => onFocus({ kind: 'abort' })} />
      </nav>
      <button type="button" className={`btn shrink-0 ${squadSize < 8 ? 'text-arena-hot' : ''}`}
        onClick={onEditSquad} data-testid="tactic-edit-squad">
        Squad {squadSize}/8
      </button>
      <span className="mx-0.5 h-5 border-l border-arena-edge" />
      <button type="button" className="btn btn-primary shrink-0" disabled={gate !== null}
        title={gate ?? 'Apply this tactic to the Sheet'} onClick={onCommit}
        data-testid="map-first-commit">Done</button>
    </div>
    {gate && <p className="mb-1 truncate px-1 text-[9.5px] text-arena-hot" title={gate}>{gate}</p>}
    <div className="relative min-w-0">
    <div ref={timelineRef} className="flex min-w-0 items-stretch gap-1 overflow-x-auto px-0.5"
      aria-label="Phase timeline">
      {tactic.phases.map((phase, index) => {
        const rows = activeRows(phase.continue);
        const active = phaseIndex === index;
        return <div key={phase.id} className="flex shrink-0 items-center gap-1">
          <button type="button"
            className={`flex min-w-[96px] items-center gap-1.5 rounded-sm border px-1.5 py-1 text-left ${active
              ? 'border-arena-material bg-arena-material/10'
              : 'border-arena-edge bg-arena-deep/35 hover:border-arena-edge2'}`}
            data-testid={`map-first-phase-${index + 1}`}
            title={`Select ${phase.name}`}
            onClick={() => onSelectPhase(index)}>
            <span className="flex h-6 w-1.5 shrink-0 flex-col overflow-hidden rounded-[2px]">
              {tactic.groups.map((group) => <span key={group.id} className="grow"
                style={{ background: phaseShade(groupColor(tactic, group.id), index, tactic.phases.length) }} />)}
            </span>
            <span>
              <span className="block max-w-28 truncate t-micro font-semibold uppercase tracking-wide">
                {index + 1} · {phase.name}
              </span>
            </span>
          </button>
          <TacticsIconButton icon="edit" label={`Edit ${phase.name}`}
            pressed={focus?.kind === 'phase' && focus.phaseIndex === index}
            onClick={() => onFocus({ kind: 'phase', phaseIndex: index })} />
          {index === tactic.phases.length - 1
            ? <button type="button"
              className={`relative inline-flex h-8 shrink-0 items-center gap-1 rounded-sm border px-2 text-[10.5px] font-semibold transition-colors ${focus?.kind === 'transition' && focus.phaseIndex === index
                ? 'border-arena-material bg-arena-material/15 text-arena-material'
                : rows > 0
                  ? 'border-arena-ok/55 bg-arena-ok/8 text-arena-ok'
                  : 'border-[#c8763c55] bg-arena-deep/30 text-arena-dim hover:border-arena-edge2 hover:text-arena-text'}`}
              title={`Finish conditions: ${rows ? `${phase.continue?.length ?? 0} cases, ${rows} rows` : 'not configured'}`}
              data-testid={`map-first-transition-${index + 1}`}
              onClick={() => onFocus({ kind: 'transition', phaseIndex: index })}>
              <TacticsIcon name="finish" size={15} /> Finish
              {rows > 0 && !(focus?.kind === 'transition' && focus.phaseIndex === index)
                && <span className="absolute right-0.5 top-0.5 h-1.5 w-1.5 rounded-full bg-arena-ok" />}
            </button>
            : <TacticsIconButton icon="transition"
              label={`Continue to ${tactic.phases[index + 1]?.name} conditions: ${rows ? `${phase.continue?.length ?? 0} cases, ${rows} rows` : 'not configured'}`}
              configured={rows > 0}
              pressed={focus?.kind === 'transition' && focus.phaseIndex === index}
              className="!border-[#c8763c55]"
              testId={`map-first-transition-${index + 1}`}
              onClick={() => onFocus({ kind: 'transition', phaseIndex: index })} />}
        </div>;
      })}
      {tactic.phases.length < 8 && <TacticsIconButton icon="add-phase" label="Add phase"
        testId="map-first-add-phase" onClick={onAddPhase} />}
    </div>
    {scrollState.left && <button type="button" aria-label="Earlier phases"
      className="absolute inset-y-0 left-0 flex w-7 items-center justify-start bg-gradient-to-r from-arena-panel px-1 text-arena-text"
      onClick={() => timelineRef.current?.scrollBy({ left: -240, behavior: 'smooth' })}>‹</button>}
    {scrollState.right && <button type="button" aria-label="Later phases"
      className="absolute inset-y-0 right-0 flex w-7 items-center justify-end bg-gradient-to-l from-arena-panel px-1 text-arena-text"
      onClick={() => timelineRef.current?.scrollBy({ left: 240, behavior: 'smooth' })}>›</button>}
    </div>
  </div>;
}

/** Compact membership next to the map. Bots already represented on the map
 * collapse into honest readouts; an unposted bot remains a draggable token
 * which the real SVG accepts as an exact Post. Group-to-Group drops delegate
 * to the same membership-following mutation path as the classic editor. */
export function MapFirstGroupTrays({
  catalog, document, tactic, phaseIndex, selectedGroupId,
  selectedSlots, onFocus, onMoveBot, onSelectGroup, onSelectBot,
}: {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  tactic: Tactic;
  phaseIndex: number;
  selectedGroupId: string | null;
  selectedSlots: readonly number[];
  onFocus: (focus: MapFirstFocus) => void;
  onMoveBot: (slot: number, groupId: string) => void;
  onSelectGroup: (groupId: string) => void;
  onSelectBot: (groupId: string, slot: number, additive: boolean) => void;
}) {
  const projection = projectPhaseBots(catalog, document, tactic, phaseIndex);
  return <aside className="flex min-w-0 flex-col gap-1.5" data-testid="map-first-group-trays">
    <div className="flex items-center gap-1">
      <span className="lab">Groups</span>
      <span className="grow" />
      <button type="button" className="btn !min-h-6 !px-1.5 text-[9.5px]"
        onClick={() => onFocus({ kind: 'groups' })}>
        Edit membership
      </button>
    </div>
    {tactic.groups.map((group) => {
      const color = groupColor(tactic, group.id);
      const resolution = effectiveJobResolution(tactic, phaseIndex, group.id);
      const positions = projection.bots.filter((bot) => bot.groupId === group.id);
      const exact = new Set(positions.filter((bot) => bot.exact).map((bot) => bot.slot));
      const preview = new Set(positions.filter((bot) => !bot.exact).map((bot) => bot.slot));
      const posts = resolution?.job.kind === 'posts';
      const unresolved = projection.unresolvedGroupIds.includes(group.id);
      return <section key={group.id}
        className={`rounded-sm border bg-arena-deep/30 p-1.5 ${selectedGroupId === group.id
          ? 'border-arena-material/70' : 'border-arena-edge'}`}
        style={{ borderLeft: `3px solid ${color}` }}
        onDragOver={(event) => {
          if (event.dataTransfer.types.includes('application/x-nilbots-bot')) event.preventDefault();
        }}
        onDrop={(event) => {
          const raw = event.dataTransfer.getData('application/x-nilbots-bot');
          if (!raw) return;
          event.preventDefault();
          const slot = Number(raw);
          if (Number.isInteger(slot)) onMoveBot(slot, group.id);
        }}>
        <button type="button" draggable className="mb-1 flex w-full cursor-grab items-center gap-1.5 text-left active:cursor-grabbing"
          aria-pressed={group.slots.length > 0
            && group.slots.every((slot) => selectedSlots.includes(slot))}
          title={`Drag ${group.name} onto the map to place the whole Group`}
          onDragStart={(event) => {
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('application/x-nilbots-group', JSON.stringify({
              tacticId: tactic.id, phaseIndex, groupId: group.id,
            }));
          }}
          onClick={() => onSelectGroup(group.id)}>
          <TacticsIcon name="move" size={13} className="shrink-0 text-arena-dim" />
          <span className="t-micro min-w-0 grow truncate font-semibold">{group.name || 'Unnamed Group'}</span>
          <span className="val">{group.slots.length}</span>
        </button>
        <div className="flex flex-col gap-1">
          {group.slots.map((slot) => {
            const bot = document.squad[slot];
            if (!bot) return null;
            const placed = exact.has(slot) || preview.has(slot);
            const draggablePost = posts && !exact.has(slot);
            const state = exact.has(slot) ? null
              : preview.has(slot) ? 'Projected'
                : draggablePost ? 'Drag to map' : unresolved ? 'No preview' : 'Unplaced';
            return <button key={slot} type="button"
              draggable
              onDragStart={(event) => {
                event.dataTransfer.effectAllowed = 'move';
                event.dataTransfer.setData('application/x-nilbots-bot', String(slot));
                if (draggablePost) {
                  event.dataTransfer.setData('application/x-nilbots-post', JSON.stringify({
                    tacticId: tactic.id, phaseIndex, groupId: group.id, slot,
                  }));
                }
              }}
              aria-pressed={selectedSlots.includes(slot)}
              onClick={(event) => onSelectBot(group.id, slot,
                event.metaKey || event.ctrlKey || event.shiftKey)}
              className={`flex min-h-8 items-center gap-1.5 rounded-sm border px-1.5 text-left ${selectedSlots.includes(slot)
                ? 'border-arena-material bg-arena-material/12'
                : draggablePost
                ? 'border-dashed border-arena-material/70 bg-arena-material/5'
                : placed ? 'border-transparent bg-arena-panel2/35' : 'border-arena-edge'}`}
              title={draggablePost
                ? `Drag ${bot.name} onto an exact Post tile`
                : state ? `${bot.name} — ${state}` : `${bot.name} — exact Post`}>
              {classSprite(bot.class) && <img src={classSprite(bot.class)} alt=""
                className={`h-6 w-6 shrink-0 object-contain ${placed ? 'opacity-75' : ''}`} />}
              <span className="min-w-0 grow truncate text-[10.5px] font-semibold">{bot.name}</span>
              {state && <span className={`text-[8.5px] uppercase tracking-wide ${draggablePost
                ? 'text-arena-material' : 'text-arena-dim'}`}>{state}</span>}
            </button>;
          })}
        </div>
        {resolution?.inherited && <p className="mt-1 truncate text-[9.5px] text-arena-material">
          Carried from {tactic.phases[resolution.sourcePhaseIndex]?.name}
        </p>}
        {unresolved && <p className="mt-1 text-[9.5px] text-arena-dim">
          This assignment has no honest position preview. Its saved behavior is unchanged.
        </p>}
      </section>;
    })}
    <p className="text-[9.5px] leading-snug text-arena-dim">
      <b className="text-arena-text">P</b> marks a stable preview. Preview bots are not draggable on the map.
    </p>
  </aside>;
}

const postureIcons: Record<string, TacticsIconName> = {
  observe: 'observe', hold: 'hold', aggressive: 'aggressive',
};
const travelIcons: Record<string, TacticsIconName> = {
  observe: 'march', hold: 'respond', aggressive: 'brawl',
};
const recoveryIcons: Record<string, TacticsIconName> = {
  'fight-on': 'fight-on', 'heal-tile': 'heal-tile', 'heal-up': 'heal-up',
};

/** The ordinary phase editor: intentionally only Posts and Patrol are offered
 * as new spatial authoring. Everything else is preserved as an explicit
 * loaded/advanced state until the player deliberately converts it. */
export function MapFirstGroupToolbar({
  catalog, document, tactic, phaseIndex, groupId,
  change, onDrawPosts, onDrawPatrol,
}: {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  tactic: Tactic;
  phaseIndex: number;
  groupId: string;
  change: (apply: (tactic: Tactic) => void) => void;
  onDrawPosts: () => void;
  onDrawPatrol: () => void;
}) {
  const group = tactic.groups.find((value) => value.id === groupId);
  const raw = tactic.phases[phaseIndex]?.jobs[groupId];
  const resolution = effectiveJobResolution(tactic, phaseIndex, groupId);
  const effective = resolution?.job ?? raw;
  const [pending, setPending] = useState<{ kind: 'posts' | 'patrol'; next: NonNullable<typeof effective>; removed: string[] } | null>(null);
  if (!group || !raw || !effective) return null;
  const normal = effective.kind === 'posts' || effective.kind === 'patrol';
  const color = groupColor(tactic, groupId);

  const applyKind = (kind: 'posts' | 'patrol') => {
    if (effective.kind === kind) {
      if (kind === 'posts') onDrawPosts();
      else onDrawPatrol();
      return;
    }
    const plan = planJobKindChange(effective, kind);
    if (plan.removed.length) {
      setPending({ kind, ...plan });
      return;
    }
    change((next) => {
      next.phases[phaseIndex].jobs[groupId] = structuredClone(plan.next);
    });
    if (kind === 'posts') onDrawPosts();
    else onDrawPatrol();
  };

  const convert = projectedPostsForLegacyJob(
    catalog, document, tactic, phaseIndex, groupId);

  return <>
    <div className="flex min-w-0 flex-wrap items-center gap-1.5 rounded-sm border border-arena-edge bg-arena-panel/65 p-1.5"
      data-testid="map-first-group-toolbar" style={{ borderLeft: `3px solid ${color}` }}>
      <span className="max-w-40 truncate px-1 t-micro font-semibold">{group.name} · {group.slots.length}</span>
      <span className="h-5 border-l border-arena-edge" />
      {normal ? <div className="flex items-center gap-1" role="group" aria-label="Spatial assignment">
        <TacticsIconButton icon="posts" label="Exact Posts" pressed={effective.kind === 'posts'}
          onClick={() => applyKind('posts')} testId="map-first-posts-mode" />
        <TacticsIconButton icon="patrol" label="Patrol route" pressed={effective.kind === 'patrol'}
          onClick={() => applyKind('patrol')} testId="map-first-patrol-mode" />
      </div> : <div className="flex items-center gap-1 rounded-sm border border-arena-warning/45 px-2 py-1">
        <span className="t-micro text-arena-warning">Loaded {effective.kind}</span>
        <button type="button" className="btn !min-h-7" disabled={!convert}
          title={convert ? 'Create explicit editable Posts from the visible positions' : 'This job has no complete legal position preview'}
          onClick={() => convert && change((next) => {
            const target = materializeSpatialJob(next, phaseIndex, groupId);
            if (!target) return;
            target.kind = 'posts';
            target.posts = structuredClone(convert);
            delete target.rect;
            delete target.path;
            delete target.point;
          })}>Convert to posts</button>
      </div>}
      {resolution?.inherited && <span className="pill text-arena-material"
        title={`Ground inherited from ${tactic.phases[resolution.sourcePhaseIndex]?.name}; first map edit makes an independent copy`}>
        Carry on · {tactic.phases[resolution.sourcePhaseIndex]?.name}
      </span>}
      <span className="grow" />
      <span className="t-micro text-arena-dim">Drag the Group or its bots directly onto the map.</span>
    </div>
    {pending && <Modal onClose={() => setPending(null)}>
      <div className="lab mb-1">Change spatial assignment?</div>
      <h3 className="mb-2 text-lg font-semibold">The current map work does not fit.</h3>
      <p className="t-meta">Switching to <b>{pending.kind === 'posts' ? 'Posts' : 'Patrol'}</b> removes {pending.removed.join(', ')}.</p>
      <div className="mt-3 flex gap-2">
        <button type="button" className="btn btn-primary" autoFocus onClick={() => setPending(null)}>Cancel · keep it</button>
        <button type="button" className="btn btn-secondary" onClick={() => {
          change((next) => {
            next.phases[phaseIndex].jobs[groupId] = structuredClone(pending.next);
          });
          const kind: JobKind = pending.kind;
          setPending(null);
          if (kind === 'posts') onDrawPosts();
          else onDrawPatrol();
        }}>Change assignment</button>
      </div>
    </Modal>}
  </>;
}

function BehaviorChoice({ icon, label, blurb, selected, onClick }: {
  icon: TacticsIconName;
  label: string;
  blurb: string;
  selected: boolean;
  onClick: () => void;
}) {
  return <button type="button" aria-pressed={selected} title={blurb} onClick={onClick}
    className={`flex min-h-9 w-full items-center gap-2 rounded-sm border px-2 py-1.5 text-left transition-colors ${selected
      ? 'border-arena-material bg-arena-material/10 text-arena-text'
      : 'border-arena-edge bg-arena-deep/25 text-arena-dim hover:border-arena-edge2 hover:text-arena-text'}`}>
    <TacticsIcon name={icon} size={17} className={selected ? 'text-arena-material' : undefined} />
    <span className="min-w-0">
      <span className="block text-[11.5px] font-semibold leading-tight">{label}</span>
      {selected && <span className="mt-1 block text-[11px] leading-snug text-arena-dim">{blurb}</span>}
    </span>
  </button>;
}

/** Readable behavior lives beside the map instead of competing with spatial
 * tools above it. This remains the same three ordinary job fields. */
export function MapFirstBehaviorPanel({ tactic, phaseIndex, groupId, change }: {
  tactic: Tactic;
  phaseIndex: number;
  groupId: string;
  change: (apply: (tactic: Tactic) => void) => void;
}) {
  const group = tactic.groups.find((value) => value.id === groupId);
  const raw = tactic.phases[phaseIndex]?.jobs[groupId];
  const effective = effectiveJobResolution(tactic, phaseIndex, groupId)?.job ?? raw;
  if (!group || !raw || !effective) return null;
  const color = groupColor(tactic, groupId);
  const travel = raw.travel ?? 'observe';
  return <aside className="flex min-w-0 flex-col gap-2 rounded-sm border border-arena-edge bg-arena-panel/55 p-2"
    data-testid="map-first-behavior-panel" style={{ borderTop: `3px solid ${color}` }}>
    <div className="flex items-center gap-2 border-b border-arena-edge pb-2">
      <span className="h-6 w-1.5 shrink-0 rounded-[2px]" style={{ background: color }} />
      <span className="min-w-0">
        <span className="lab block">Behavior</span>
        <span className="block truncate text-[12px] font-semibold">{group.name}</span>
      </span>
    </div>
    <fieldset className="flex flex-col gap-1">
      <legend className="lab mb-1">At the destination</legend>
      {POSTURES.map((option) => <BehaviorChoice key={option.id}
        icon={postureIcons[option.id]} label={option.label} blurb={option.blurb}
        selected={raw.posture === option.id} onClick={() => change((next) => {
          next.phases[phaseIndex].jobs[groupId].posture = option.id;
        })} />)}
    </fieldset>
    {effective.kind !== 'patrol' && <fieldset className="flex flex-col gap-1">
      <legend className="lab mb-1">On the way</legend>
      {TRAVEL_BEHAVIORS.map((option) => <BehaviorChoice key={option.id}
        icon={travelIcons[option.id]} label={option.label} blurb={option.blurb}
        selected={travel === option.id} onClick={() => change((next) => {
          next.phases[phaseIndex].jobs[groupId].travel = option.id;
        })} />)}
    </fieldset>}
    <fieldset className="flex flex-col gap-1">
      <legend className="lab mb-1">Recovery</legend>
      {RECOVERIES.map((option) => <BehaviorChoice key={option.id}
        icon={recoveryIcons[option.id]} label={option.label} blurb={option.blurb}
        selected={raw.recovery === option.id} onClick={() => change((next) => {
          next.phases[phaseIndex].jobs[groupId].recovery = option.id;
        })} />)}
    </fieldset>
  </aside>;
}

/** Selecting one bot opens only the things that are truly bot-specific in
 * this phase: its way in, its signature mark, and useful range readback. */
export function MapFirstBotPanel({
  catalog, document, tactic, phaseIndex, groupId, slot, task, change,
  onDrawRoute, onDrawPatrol, onDrawSignature, onStop,
}: {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  tactic: Tactic;
  phaseIndex: number;
  groupId: string;
  slot: number;
  task: MapTask | null;
  change: (apply: (tactic: Tactic) => void) => void;
  onDrawRoute: (postIndex: number | null) => void;
  onDrawPatrol: () => void;
  onDrawSignature: (classId: string) => void;
  onStop: () => void;
}) {
  const bot = document.squad[slot];
  const group = tactic.groups.find((value) => value.id === groupId);
  const raw = tactic.phases[phaseIndex]?.jobs[groupId];
  const resolution = effectiveJobResolution(tactic, phaseIndex, groupId);
  const effective = resolution?.job ?? raw;
  const entry = catalog.classes.find((value) => value.id === bot?.class);
  if (!bot || !group || !raw || !effective || !entry) return null;
  const postIndex = effective.kind === 'posts'
    ? effective.posts?.findIndex((post, index) => (post.slot ?? group.slots[index]) === slot) ?? -1
    : -1;
  const post = postIndex >= 0 ? effective.posts?.[postIndex] : null;
  const route = post?.via ?? effective.via;
  const routeDrawing = task?.kind === 'job' && task.groupId === groupId
    && task.phaseIndex === phaseIndex && task.field === 'via'
    && (postIndex < 0 || task.postIndex === postIndex);
  const geometry = signatureMarkGeometry(catalog, bot.class);
  const mark = raw.sigs?.[bot.class];
  const marks = mark ? ('points' in mark ? mark.points : mark.line) : [];
  const signatureDrawing = task?.kind === 'sig' && task.groupId === groupId
    && task.phaseIndex === phaseIndex && task.classId === bot.class;
  const clearRoute = route?.length ? () => change((next) => {
    const target = materializeSpatialJob(next, phaseIndex, groupId);
    if (!target) return;
    if (postIndex >= 0) {
      const targetPost = target.posts?.[postIndex];
      if (targetPost) delete targetPost.via;
    } else {
      delete target.via;
    }
  }) : undefined;
  const classMembers = group.slots.filter((candidate) =>
    document.squad[candidate]?.class === bot.class).length;
  return <div className="flex flex-col gap-2" data-testid="map-first-bot-panel">
    <div className="flex items-center gap-3 rounded-sm border border-arena-edge bg-arena-panel2/30 p-2">
      {classSprite(bot.class) && <img src={classSprite(bot.class)} alt="" className="h-14 w-14 object-contain drop-shadow" />}
      <div className="min-w-0 grow">
        <h3 className="type-display truncate text-[16px]">{entry.signatureName}</h3>
        <p className="text-[10.5px] font-semibold text-arena-material">
          {classMembers > 1 ? `${entry.name}s` : entry.name} in {group.name}
        </p>
        <p className="mt-1 text-[9.5px] leading-snug text-arena-dim">{entry.fantasy}</p>
      </div>
    </div>
    <div className={`grid gap-1 ${entry.visionProximityRange > 0 ? 'grid-cols-3' : 'grid-cols-2'}`}>
      <span className="rounded-sm border border-arena-edge p-1.5 text-center text-[10.5px]">
        <b className="block text-arena-text">{entry.attackRange} tiles</b> Attack reach
      </span>
      <span className="rounded-sm border border-arena-edge p-1.5 text-center text-[10.5px]">
        <b className="block text-arena-text">{entry.visionRange} tiles</b> Vision
      </span>
      {entry.visionProximityRange > 0 && <span className="rounded-sm border border-arena-edge p-1.5 text-center text-[10.5px]">
        <b className="block text-arena-text">{entry.visionProximityRange} tiles</b> Proximity vision
      </span>}
    </div>
    <div>
      <div className="lab mb-1">Movement</div>
      {effective.kind === 'patrol' ? <GeoStatusRow complete={(effective.path?.length ?? 0) >= 2}
        label={effective.path?.length
          ? `Group patrol · ${effective.path.length} waypoints`
          : 'Group patrol has no route yet'}
        drawing={task?.kind === 'job' && task.groupId === groupId
          && task.phaseIndex === phaseIndex && task.draw === 'route' && task.field !== 'via'}
        drawLabel="Draw patrol route" onDraw={onDrawPatrol} onStop={onStop} />
        : (postIndex >= 0 || viaLegal(effective.kind))
          ? <GeoStatusRow complete={(route?.length ?? 0) >= 2}
            label={route?.length
              ? `Approach route · ${route.length} waypoints before arrival`
              : post ? `Direct approach to the post at ${describePlace(...post.at)}`
                : 'Direct approach to the destination'}
            drawing={routeDrawing} drawLabel="Draw approach route"
            onDraw={() => onDrawRoute(postIndex >= 0 ? postIndex : null)}
            onStop={onStop} onClear={clearRoute} />
          : <p className="t-micro text-arena-dim">This assignment works out its own movement.</p>}
    </div>
    <div>
      <div className="lab mb-1">Signature</div>
      {geometry ? <GeoStatusRow complete={marks.length > 0}
        label={marks.length
          ? `${geometry.label} · ${marks.length}/${geometry.max} mark${marks.length === 1 ? '' : 's'}`
          : `${geometry.label} · no map marks yet`}
        drawing={signatureDrawing} drawLabel="Place signature marks"
        onDraw={() => onDrawSignature(bot.class)} onStop={onStop}
        onClear={marks.length ? () => change((next) => {
          const target = next.phases[phaseIndex]?.jobs[groupId];
          if (target?.sigs) delete target.sigs[bot.class];
        }) : undefined} />
        : <div className="rounded-sm border border-arena-edge bg-arena-deep/30 p-2">
          <p className="t-micro font-semibold">{entry.signatureName}</p>
          <p className="mt-1 t-micro text-arena-dim">Used automatically when this bot’s behavior calls for it; no map mark is needed.</p>
        </div>}
    </div>
  </div>;
}
