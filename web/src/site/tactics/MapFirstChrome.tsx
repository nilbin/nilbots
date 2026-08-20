import { useState } from 'react';
import type { TacticalSheetCatalog } from '../api';
import { classSprite } from './classArt';
import { projectPhaseBots } from './botProjection';
import { Modal } from './Modal';
import { condClauses } from './summary';
import {
  TacticsIcon, TacticsIconButton, type TacticsIconName,
} from './TacticsIcons';
import {
  materializeSpatialJob, projectedPostsForLegacyJob,
  type FormationObjective,
} from './mapFirstSpatial';
import {
  POSTURES, RECOVERIES, TRAVEL_BEHAVIORS,
  effectiveJobResolution, groupColor, phaseShade, planJobKindChange,
  signatureMarkGeometry,
  type JobKind, type Posture, type Recovery, type Tactic, type TacticsDocument,
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
  tactic, phaseIndex, focus, gate, onFocus, onAddPhase, onCommit, onCancel,
}: {
  tactic: Tactic;
  phaseIndex: number;
  focus: MapFirstFocus | null;
  gate: string | null;
  onFocus: (focus: MapFirstFocus) => void;
  onAddPhase: () => void;
  onCommit: () => void;
  onCancel: () => void;
}) {
  const startRows = activeRows(tactic.start.cases);
  const abortRows = activeRows(tactic.abort?.cases);
  return <div className="panel p-2" data-testid="map-first-timeline">
    <div className="mb-1.5 flex items-center gap-2">
      <span className="type-display min-w-0 grow truncate text-[14px]">{tactic.name}</span>
      {gate && <span className="max-w-64 truncate text-[9px] text-arena-hot" title={gate}>{gate}</span>}
      <button type="button" className="btn !min-h-7" onClick={onCancel}>Cancel</button>
      <button type="button" className="btn btn-primary !min-h-7" disabled={gate !== null}
        title={gate ?? 'Save tactic'} onClick={onCommit} data-testid="map-first-commit">
        <span className="flex items-center gap-1"><TacticsIcon name="save" size={14} /> Save tactic</span>
      </button>
    </div>
    <div className="mb-1.5 flex flex-wrap items-center gap-1.5 border-b border-arena-edge pb-1.5">
      <span className="lab mr-1">Whole tactic</span>
      <button type="button"
        className={`btn !min-h-7 ${focus?.kind === 'start' ? 'btn-on' : ''}`}
        data-testid="map-first-start"
        onClick={() => onFocus({ kind: 'start' })}>
        <span className="flex items-center gap-1"><TacticsIcon name="start" size={14} />
          Start · {tactic.start.mode === 'always' ? 'Always' : `${startRows} condition${startRows === 1 ? '' : 's'}`}</span>
      </button>
      <button type="button"
        className={`btn !min-h-7 ${focus?.kind === 'cores' ? 'btn-on' : ''}`}
        onClick={() => onFocus({ kind: 'cores' })}>
        <span className="flex items-center gap-1"><TacticsIcon name="core" size={14} /> Core plan</span>
      </button>
      <span className="grow" />
      <button type="button"
        className={`btn !min-h-7 ${focus?.kind === 'abort' ? 'btn-on' : tactic.abort ? 'text-arena-hot' : 'text-arena-dim'}`}
        data-testid="map-first-abort"
        onClick={() => onFocus({ kind: 'abort' })}>
        <span className="flex items-center gap-1"><TacticsIcon name="abort" size={14} />
          Abort · {tactic.abort ? `${abortRows} condition${abortRows === 1 ? '' : 's'}` : 'Not set'}</span>
      </button>
    </div>
    <div className="flex min-w-0 items-stretch gap-1 overflow-x-auto pb-0.5"
      aria-label="Phase timeline">
      {tactic.phases.map((phase, index) => {
        const rows = activeRows(phase.continue);
        const active = phaseIndex === index;
        return <div key={phase.id} className="flex shrink-0 items-center gap-1">
          <button type="button"
            className={`flex min-w-[112px] items-center gap-2 rounded-sm border px-2 py-1.5 text-left ${active
              ? 'border-arena-material bg-arena-material/10'
              : 'border-arena-edge bg-arena-deep/35 hover:border-arena-edge2'}`}
            data-testid={`map-first-phase-${index + 1}`}
            onClick={() => onFocus({ kind: 'phase', phaseIndex: index })}>
            <span className="flex h-7 w-2 shrink-0 flex-col overflow-hidden rounded-[2px]">
              {tactic.groups.map((group) => <span key={group.id} className="grow"
                style={{ background: phaseShade(groupColor(tactic, group.id), index, tactic.phases.length) }} />)}
            </span>
            <span>
              <span className="block t-micro font-semibold uppercase tracking-wide">{index + 1} · {phase.name}</span>
              <span className="block text-[9.5px] text-arena-dim">{tactic.groups.length} Group{tactic.groups.length === 1 ? '' : 's'}</span>
            </span>
          </button>
          <TacticsIconButton icon="transition"
            label={`${index === tactic.phases.length - 1 ? 'Finish' : `Continue to ${tactic.phases[index + 1]?.name}`} conditions: ${rows ? `${phase.continue?.length ?? 0} cases, ${rows} rows` : 'not configured'}`}
            configured={rows > 0}
            pressed={focus?.kind === 'transition' && focus.phaseIndex === index}
            className="!border-[#c8763c55]"
            testId={`map-first-transition-${index + 1}`}
            onClick={() => onFocus({ kind: 'transition', phaseIndex: index })} />
        </div>;
      })}
      {tactic.phases.length < 8 && <TacticsIconButton icon="add-phase" label="Add phase"
        testId="map-first-add-phase" onClick={onAddPhase} />}
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
        <button type="button" className="mb-1 flex w-full items-center gap-1.5 text-left"
          aria-pressed={group.slots.length > 0
            && group.slots.every((slot) => selectedSlots.includes(slot))}
          onClick={() => onSelectGroup(group.id)}>
          <span className="t-micro min-w-0 grow truncate font-semibold">{group.name || 'Unnamed Group'}</span>
          <span className="val">{group.slots.length}</span>
        </button>
        <div className="flex flex-col gap-1">
          {group.slots.map((slot) => {
            const bot = document.squad[slot];
            if (!bot) return null;
            const placed = exact.has(slot) || preview.has(slot);
            const draggablePost = posts && !exact.has(slot);
            const state = exact.has(slot) ? 'On map'
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
              onDoubleClick={() => onFocus({ kind: 'bot', phaseIndex, groupId: group.id, slot })}
              className={`flex min-h-8 items-center gap-1.5 rounded-sm border px-1.5 text-left ${selectedSlots.includes(slot)
                ? 'border-arena-material bg-arena-material/12'
                : draggablePost
                ? 'border-dashed border-arena-material/70 bg-arena-material/5'
                : placed ? 'border-transparent bg-arena-panel2/35' : 'border-arena-edge'}`}
              title={draggablePost
                ? `Drag ${bot.name} onto an exact Post tile`
                : `${bot.name} — ${state}`}>
              {classSprite(bot.class) && <img src={classSprite(bot.class)} alt=""
                className={`h-6 w-6 shrink-0 object-contain ${placed ? 'opacity-75' : ''}`} />}
              <span className="min-w-0 grow truncate text-[10.5px] font-semibold">{bot.name}</span>
              <span className={`text-[8.5px] uppercase tracking-wide ${draggablePost
                ? 'text-arena-material' : 'text-arena-dim'}`}>{state}</span>
            </button>;
          })}
        </div>
        <div className="mt-1 flex items-center gap-1">
          {resolution?.inherited && <button type="button"
            className="min-w-0 grow truncate text-left text-[9.5px] text-arena-material hover:underline"
            onClick={() => onFocus({
              kind: 'group', phaseIndex: resolution.sourcePhaseIndex, groupId: group.id,
            })}>
            Carried from {tactic.phases[resolution.sourcePhaseIndex]?.name} · edit source
          </button>}
          <TacticsIconButton icon="edit" label={`Open ${group.name} details`}
            className="!h-7 !w-7" onClick={() =>
              onFocus({ kind: 'group', phaseIndex, groupId: group.id })} />
        </div>
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

function IconChoiceRow({ label, options, value, icons, onChange }: {
  label: string;
  options: readonly { id: string; label: string; blurb: string }[];
  value: string;
  icons: Record<string, TacticsIconName>;
  onChange: (value: string) => void;
}) {
  return <div className="flex items-center gap-1" aria-label={label}>
    <span className="lab mr-0.5 hidden xl:inline">{label}</span>
    {options.map((option) => <TacticsIconButton key={option.id}
      icon={icons[option.id]} label={`${label}: ${option.label}. ${option.blurb}`}
      pressed={value === option.id} onClick={() => onChange(option.id)} />)}
  </div>;
}

/** The ordinary phase editor: intentionally only Posts and Patrol are offered
 * as new spatial authoring. Everything else is preserved as an explicit
 * loaded/advanced state until the player deliberately converts it. */
export function MapFirstGroupToolbar({
  catalog, document, tactic, phaseIndex, groupId, selectedSlots,
  change, onDrawPosts, onDrawPatrol, onOpenDetails, onOpenCores, onSuggest,
}: {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  tactic: Tactic;
  phaseIndex: number;
  groupId: string;
  selectedSlots: readonly number[];
  change: (apply: (tactic: Tactic) => void) => void;
  onDrawPosts: () => void;
  onDrawPatrol: () => void;
  onOpenDetails: () => void;
  onOpenCores: () => void;
  onSuggest: (objective: FormationObjective) => void;
}) {
  const group = tactic.groups.find((value) => value.id === groupId);
  const raw = tactic.phases[phaseIndex]?.jobs[groupId];
  const resolution = effectiveJobResolution(tactic, phaseIndex, groupId);
  const effective = resolution?.job ?? raw;
  const [pending, setPending] = useState<{ kind: 'posts' | 'patrol'; next: NonNullable<typeof effective>; removed: string[] } | null>(null);
  if (!group || !raw || !effective) return null;
  const normal = effective.kind === 'posts' || effective.kind === 'patrol';
  const color = groupColor(tactic, groupId);
  const activeSlots = selectedSlots.filter((slot) => group.slots.includes(slot));
  const selectedBot = activeSlots.length === 1
    ? document.squad.find((bot) => bot.slot === activeSlots[0]) : null;
  const classEntry = selectedBot
    ? catalog.classes.find((entry) => entry.id === selectedBot.class) : null;
  const hasSpatialSignature = selectedBot
    ? Boolean(signatureMarkGeometry(catalog, selectedBot.class)) : false;
  const currentTravel = raw.travel ?? 'observe';

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
    <div className="panel flex min-w-0 flex-wrap items-center gap-1.5 p-1.5"
      data-testid="map-first-group-toolbar" style={{ borderLeft: `3px solid ${color}` }}>
      <button type="button" className="btn !min-h-8 max-w-40 truncate"
        onClick={onOpenDetails} title={`Open ${group.name} details`}>
        {group.name} · {group.slots.length}
      </button>
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
      <IconChoiceRow label="Posture" options={POSTURES} value={raw.posture}
        icons={postureIcons} onChange={(posture) => change((next) => {
          next.phases[phaseIndex].jobs[groupId].posture = posture as Posture;
        })} />
      {effective.kind !== 'patrol' && <IconChoiceRow label="Travel"
        options={TRAVEL_BEHAVIORS} value={currentTravel} icons={travelIcons}
        onChange={(travel) => change((next) => {
          next.phases[phaseIndex].jobs[groupId].travel = travel as Posture;
        })} />}
      <IconChoiceRow label="Recovery" options={RECOVERIES} value={raw.recovery}
        icons={recoveryIcons} onChange={(recovery) => change((next) => {
          next.phases[phaseIndex].jobs[groupId].recovery = recovery as Recovery;
        })} />
      <TacticsIconButton icon="core" label="Core custody and delivery" onClick={onOpenCores}
        configured={Boolean(tactic.phases[phaseIndex].custody)} />
      {effective.kind === 'posts' && <details className="relative">
        <summary className="flex h-8 w-8 cursor-pointer list-none items-center justify-center rounded-sm border border-arena-edge text-arena-dim hover:border-arena-edge2 hover:text-arena-text"
          aria-label="Suggest formation" title="Suggest formation">
          <TacticsIcon name="formation" size={18} />
        </summary>
        <div className="absolute left-0 top-9 z-30 flex w-44 flex-col gap-1 rounded-sm border border-arena-edge bg-arena-panel p-1.5 shadow-lg">
          <span className="lab">Suggest explicit posts</span>
          {([
            ['mid', 'Mid'], ['well-own', 'Your Well'],
            ['well-centre', 'Centre Well'], ['well-enemy', 'Their Well'],
          ] as const).map(([objective, label]) => <button key={objective} type="button"
            className="btn justify-start" onClick={(event) => {
              onSuggest(objective);
              event.currentTarget.closest('details')?.removeAttribute('open');
            }}>{label}</button>)}
        </div>
      </details>}
      <TacticsIconButton icon="route" label="Routes, signatures and Group details"
        configured={Boolean(effective.via?.length || effective.posts?.some((post) => post.via?.length)
          || Object.keys(raw.sigs ?? {}).length)} onClick={onOpenDetails} />
      <span className="grow" />
      {selectedBot ? <button type="button" className="flex min-h-8 items-center gap-1.5 rounded-sm border border-arena-edge px-1.5 text-left"
        onClick={onOpenDetails} title={`${classEntry?.name ?? selectedBot.class}: ${classEntry?.signatureName ?? 'class details'}`}>
        {classSprite(selectedBot.class) && <img src={classSprite(selectedBot.class)} alt="" className="h-7 w-7 object-contain" />}
        <span className="t-micro"><b>{selectedBot.name}</b><br />{classEntry?.name ?? selectedBot.class}</span>
        <TacticsIcon name="signature" size={15} className={hasSpatialSignature ? 'text-arena-material' : 'text-arena-dim'} />
      </button> : activeSlots.length > 1
        ? <span className="pill">{activeSlots.length} selected · spatial edit</span>
        : <span className="t-micro text-arena-dim">Select a bot, or click the Group for formation editing.</span>}
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
