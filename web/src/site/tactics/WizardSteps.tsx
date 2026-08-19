import { useState } from 'react';
import type { TacticalSheetCatalog } from '../api';
import { BotChip } from './BotChip';
import { ChoiceCards } from './ChoiceCards';
import { CaseListEditor } from './ConditionRows';
import { GeoStatusRow } from './GeoStatusRow';
import { PhaseStrip } from './PhaseStrip';
import { SegChips } from './SegChips';
import { freshGroup } from './recipes';
import {
  deliveryClause, groupClause, possessive, startClause, summarize,
} from './summary';
import { describeGeometry } from './TacticsMapCanvas';
import type { MapTask, WatchTarget } from './useMapTask';
import {
  CORE_RULES, DELIVERY_PLANS, JOB_KINDS, MAX_DURATIONS, POSTURES, RECOVERIES,
  effectiveJob, freshJob, freshPhase, groupColor, jobOf, signatureMarkGeometry,
  tacticCanFinish, viaLegal,
  type DrawKind, type JobKind, type Posture, type Recovery, type Tactic,
  type TacticGroup, type TacticTransition,
  type TacticsDocument,
} from './tacticsDocument';
import {
  assignedCount, jobComplete, unassignedSlots, type WizardState,
} from './wizardState';
import { describePlace } from './places';

/**
 * The wizard's five steps. Each is a pure view over the wizard's working
 * tactic: `change` mutates a clone, the map tasks come from the page, and
 * everything a player answers here reopens exactly the same way later. The
 * Phases step is the heart: a selectable storyboard of the operation, each
 * phase carrying every Group's job and its own latched "Continue when…".
 */

export interface StepProps {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  wizard: WizardState;
  task: MapTask | null;
  change: (apply: (tactic: Tactic) => void) => void;
  arm: (slot: number | null) => void;
  /** Select the storyboard phase the drawer and the map focus on. */
  selectPhase: (index: number) => void;
  /** `draw` overrides the task's draw kind — required when arming right after
   * a kind change, before the wizard state has flushed. */
  startJobTask: (groupId: string, phaseIndex: number, field?: 'via', draw?: DrawKind) => void;
  /** Draw one post's own way in (overrides the group route for that bot). */
  startPostViaTask: (groupId: string, phaseIndex: number, postIndex: number) => void;
  startSigTask: (groupId: string, phaseIndex: number, classId: string) => void;
  startWatchTask: (target: WatchTarget) => void;
  endTask: () => void;
  /** Append a prefilled Deliver Core Tactic to the sheet (carrier picked,
   * escorts and screen auto-assigned) without leaving this wizard. */
  addDeliverCore?: () => void;
  /** A map click on a posted bot briefly flashes its way-in row. */
  highlightPost?: { groupId: string; postIndex: number } | null;
}

// Any row may sit on either side of a case: Required is the AND, Triggered
// by is the OR. The Required section starts minimal and folds the full row
// set, so "A and B together fire it" is two Required rows and no trigger.
const START_TRIGGERS = [
  'enemiesAt', 'coreLoose', 'coreSoon', 'carrier',
] as const;
const START_MORE = ['enemiesDown', 'losses', 'lossesWithin'] as const;
const START_REQUIRED_MORE = [
  'enemiesAt', 'coreLoose', 'coreSoon', 'carrier', 'enemiesDown', 'losses',
  'lossesWithin',
] as const;
const ABORT_TRIGGERS = ['lossesWithin', 'losses', 'coreLost'] as const;
const ABORT_MORE = ['enemiesAt'] as const;
const ABORT_REQUIRED_MORE = [
  'lossesWithin', 'losses', 'coreLost', 'enemiesAt',
] as const;
const TRANSITION_REQUIRED = ['groupsSet', 'healed', 'held'] as const;
const TRANSITION_REQUIRED_MORE = [
  'enemiesAt', 'contact', 'coreSoon', 'coreLoose', 'kills', 'coreAcquired',
  'coreBanked', 'coreLost', 'cleared', 'enemiesDown', 'losses', 'reached',
  'observed',
] as const;
const TRANSITION_TRIGGERS = [
  'enemiesAt', 'contact', 'coreSoon', 'coreLoose', 'kills', 'coreAcquired',
  'coreBanked', 'coreLost',
] as const;
const TRANSITION_MORE = [
  'cleared', 'enemiesDown', 'losses', 'reached', 'observed', 'held',
] as const;

export function StartStep({ wizard, change, task, startWatchTask, endTask }: StepProps) {
  const tactic = wizard.tactic;
  const modes = [
    { id: 'always' as const, label: 'Always', blurb: 'Applies whenever nothing above it in the Sheet does.' },
    { id: 'when' as const, label: 'Only when…', blurb: 'Gated behind conditions on top of Sheet order.' },
  ];
  const watchFor = (scope: 'start' | 'abort') =>
    task?.kind === 'watch' && task.target.scope === scope
      ? {
        caseIndex: task.target.caseIndex,
        side: task.target.side,
        row: task.target.row,
      }
      : null;
  const [abortOpen, setAbortOpen] = useState(Boolean(tactic.abort));
  return <>
    <p className="t-meta">First things first: <b>when does this Tactic apply at all?</b>{' '}
      The Sheet still reads top to bottom — this only gates it further. Once it
      starts, it is committed: it keeps control until it finishes, aborts, or
      runs out its time limit.</p>
    <SegChips options={modes} value={tactic.start.mode}
      onChange={(mode) => change((next) => {
        next.start = mode === 'when'
          ? { mode, cases: next.start.cases ?? [{ triggers: {} }] }
          : { mode: 'always' };
      })} />
    <p className="t-micro text-arena-dim">{modes.find((mode) => mode.id === tactic.start.mode)?.blurb}</p>
    {tactic.start.mode === 'when' && (
      <CaseListEditor
        cases={tactic.start.cases ?? [{ triggers: {} }]}
        maximum={3}
        accent="#e6ddd4"
        watchSymbol="▶"
        watchAdjective="pale"
        requiredKinds={[]}
        moreRequiredKinds={[...START_REQUIRED_MORE]}
        triggerKinds={[...START_TRIGGERS]}
        moreTriggerKinds={[...START_MORE]}
        makeCase={() => ({ triggers: {} })}
        onChange={(cases) => change((next) => { next.start.cases = cases; })}
        watching={watchFor('start')}
        onDrawWatch={(caseIndex, side, row) =>
          startWatchTask({ scope: 'start', caseIndex, side, row })}
        onStopWatch={endTask}
      />
    )}
    <Readback label="Readback">
      {startClause(tactic) ?? (tactic.start.mode === 'when'
        ? 'No condition ticked yet — tick one below, or this Tactic never applies.'
        : 'Always available — the Sheet order alone decides when it runs.')}
    </Readback>
    <div className="rounded-sm border border-arena-edge p-2"
      style={{ borderLeft: '3px solid #f4526b' }}>
      <div className="flex items-center gap-2">
        <b className="t-body">Abort when…</b>
        <span className="t-micro text-arena-dim">bails out of every phase and hands the Sheet back</span>
        <span className="grow" />
        <button type="button" className="btn"
          onClick={() => {
            if (tactic.abort) {
              change((next) => { delete next.abort; });
              setAbortOpen(false);
            } else {
              change((next) => {
                next.abort = { cases: [{ triggers: { lossesWithin: { count: 4, ticks: 30 } } }] };
              });
              setAbortOpen(true);
            }
          }}>
          {tactic.abort ? 'Remove' : '＋ Add'}
        </button>
      </div>
      {tactic.abort && abortOpen && (
        <div className="mt-2">
          <CaseListEditor
            cases={tactic.abort.cases}
            maximum={2}
            accent="#f4526b"
            watchSymbol="⨯"
            watchAdjective="red"
            requiredKinds={[]}
            moreRequiredKinds={[...ABORT_REQUIRED_MORE]}
            triggerKinds={[...ABORT_TRIGGERS]}
            moreTriggerKinds={[...ABORT_MORE]}
            makeCase={() => ({ triggers: {} })}
            onChange={(cases) => change((next) => { next.abort = { cases }; })}
            watching={watchFor('abort')}
            onDrawWatch={(caseIndex, side, row) =>
              startWatchTask({ scope: 'abort', caseIndex, side, row })}
            onStopWatch={endTask}
          />
          <p className="t-micro mt-1 text-arena-dim">After an abort the whole Sheet is reread —
            a higher emergency Tactic (a Regroup gated on the same losses) can take over,
            and this one may later restart from its first phase.</p>
        </div>
      )}
    </div>
    <div>
      <div className="lab mb-1">Time limit <span className="text-arena-dim">(optional)</span></div>
      <ChoiceCards options={MAX_DURATIONS} value={tactic.maxDuration ?? 'none'} columns={4}
        onChange={(maxDuration) => change((next) => {
          if (maxDuration === 'none') delete next.maxDuration;
          else next.maxDuration = maxDuration;
        })} />
    </div>
  </>;
}

export function GroupsStep({ catalog, document, wizard, change, arm }: StepProps) {
  const tactic = wizard.tactic;
  const unassigned = unassignedSlots(tactic, document);
  const assigned = assignedCount(tactic);
  const squadReady = document.squad.length === 8;
  // HTML5 drag state: which slot is airborne, and which box would take it.
  const [dragSlot, setDragSlot] = useState<number | null>(null);
  const [dropTarget, setDropTarget] = useState<string | null>(null);
  const place = (slot: number, groupId: string | null) => change((next) => {
    for (const group of next.groups) {
      group.slots = group.slots.filter((value) => value !== slot);
    }
    if (groupId) next.groups.find((group) => group.id === groupId)?.slots.push(slot);
  });
  /** Dropping one bot onto another swaps their group memberships. */
  const swap = (a: number, b: number) => change((next) => {
    if (a === b) return;
    const home = (slot: number) =>
      next.groups.find((group) => group.slots.includes(slot)) ?? null;
    const homeA = home(a);
    const homeB = home(b);
    for (const group of next.groups) {
      group.slots = group.slots.filter((value) => value !== a && value !== b);
    }
    homeA?.slots.push(b);
    homeB?.slots.push(a);
  });
  const endDrag = () => { setDragSlot(null); setDropTarget(null); };
  const chipDragProps = (slot: number) => ({
    draggable: true,
    onDragStart: (event: React.DragEvent) => {
      event.dataTransfer.setData('text/plain', String(slot));
      event.dataTransfer.effectAllowed = 'move';
      setDragSlot(slot);
    },
    onDragEnd: endDrag,
  });
  const dropProps = (target: string | null) => ({
    onDragOver: (event: React.DragEvent) => {
      if (dragSlot === null) return;
      event.preventDefault();
      event.dataTransfer.dropEffect = 'move';
      if (dropTarget !== target) setDropTarget(target);
    },
    onDragLeave: (event: React.DragEvent) => {
      if (event.currentTarget.contains(event.relatedTarget as Node)) return;
      if (dropTarget === target) setDropTarget(null);
    },
    onDrop: (event: React.DragEvent) => {
      event.preventDefault();
      const slot = Number(event.dataTransfer.getData('text/plain'));
      if (Number.isInteger(slot)) place(slot, target);
      endDrag();
      arm(null);
    },
  });
  const hot = (target: string | null) =>
    dragSlot !== null && dropTarget === target;
  return <>
    <p className="t-meta">Split the whole team into up to three temporary Groups. They live only
      inside this Tactic — every bot belongs to exactly one, through every phase.</p>
    <div className="flex items-center gap-2">
      <span className="lab">Assigned</span>
      <div className="h-1.5 grow overflow-hidden rounded bg-arena-deep">
        <div className="h-full bg-arena-ok transition-all" style={{ width: `${(assigned / 8) * 100}%` }} />
      </div>
      <span className="val">{assigned}/8</span>
    </div>
    <div>
      <div className="lab mb-1">Unassigned</div>
      <div {...dropProps(null)}
        className={`flex min-h-9 flex-wrap gap-1 rounded-sm border border-dashed p-1 transition-colors ${hot(null)
          ? 'border-arena-material bg-arena-material/10' : 'border-arena-edge'}`}>
        {unassigned.length === 0 && (
          <span className="t-micro self-center text-arena-dim">
            {squadReady ? 'Everyone is assigned.' : 'Fill the squad on the left to get all eight in the tray.'}
          </span>
        )}
        {unassigned.map((slot) => (
          <BotChip key={slot} bot={document.squad[slot]} catalog={catalog}
            armed={wizard.armedSlot === slot}
            {...chipDragProps(slot)}
            onClick={() => arm(wizard.armedSlot === slot ? null : slot)} />
        ))}
      </div>
    </div>
    {tactic.groups.map((group) => (
      <div key={group.id}
        {...dropProps(group.id)}
        className={`rounded-sm border p-2 transition-colors ${hot(group.id)
          ? 'border-arena-material bg-arena-material/10'
          : wizard.armedSlot !== null ? 'cursor-pointer border-arena-material/60' : 'border-arena-edge'}`}
        onClick={() => {
          if (wizard.armedSlot === null) return;
          place(wizard.armedSlot, group.id);
          arm(null);
        }}
        role="presentation"
      >
        <div className="mb-1 flex items-center gap-2">
          <span className="h-2.5 w-2.5 rounded-[3px]" style={{ background: groupColor(tactic, group.id) }} />
          <input
            className="min-w-0 grow border-b border-dashed border-transparent bg-transparent text-[13px] font-semibold text-arena-text outline-none transition-colors hover:border-arena-edge2 focus:border-arena-material"
            value={group.name}
            aria-label="Group name"
            onClick={(event) => event.stopPropagation()}
            onChange={(event) => change((next) => {
              const target = next.groups.find((value) => value.id === group.id);
              if (target) target.name = event.target.value;
            })}
          />
          <span className="val">
            {group.slots.length}{wizard.hints.targetSizes.get(group.id) ? `/${wizard.hints.targetSizes.get(group.id)}` : ''} bots
          </span>
          {tactic.groups.length > 1 && (
            <button type="button" className="btn" title="Dissolve group"
              onClick={(event) => {
                event.stopPropagation();
                change((next) => {
                  next.groups = next.groups.filter((value) => value.id !== group.id);
                  for (const phase of next.phases) delete phase.jobs[group.id];
                });
              }}>✕</button>
          )}
        </div>
        <div className="flex min-h-8 flex-wrap gap-1">
          {group.slots.map((slot) => (
            <span key={slot}
              onDragOver={(event) => {
                if (dragSlot === null || dragSlot === slot) return;
                event.preventDefault();
                event.stopPropagation();
              }}
              onDrop={(event) => {
                if (dragSlot === null || dragSlot === slot) return;
                event.preventDefault();
                event.stopPropagation();
                swap(dragSlot, slot);
                endDrag();
                arm(null);
              }}>
              <BotChip bot={document.squad[slot]} catalog={catalog}
                {...chipDragProps(slot)}
                onClick={() => { place(slot, null); arm(null); }} />
            </span>
          ))}
          {group.slots.length === 0 && (
            <span className="t-micro self-center text-arena-dim">
              {wizard.armedSlot !== null
                ? 'Click to place the armed bot here.'
                : 'Empty — arm or drag a bot into this box.'}
            </span>
          )}
        </div>
      </div>
    ))}
    <div className="flex items-center gap-2">
      {tactic.groups.length < 3 && (
        <button type="button" className="btn"
          onClick={() => change((next) => {
            const group = freshGroup(next.groups);
            next.groups.push(group);
            for (const phase of next.phases) {
              phase.jobs[group.id] = freshJob('none', 'hold');
            }
          })}>
          ＋ Add a Group
        </button>
      )}
      <span className="grow" />
      <button type="button" className="btn" disabled={!squadReady}
        onClick={() => change((next) => {
          const weight = (group: TacticGroup) =>
            group.slots.length / (wizard.hints.targetSizes.get(group.id) ?? 8);
          for (const slot of unassignedSlots(next, document)) {
            const target = [...next.groups].sort((a, b) => weight(a) - weight(b))[0];
            target.slots.push(slot);
          }
        })}>
        Distribute the rest
      </button>
    </div>
    <p className="t-micro text-arena-dim">Drag bots between the tray and Groups — dropping one bot on
      another swaps them. Clicking works too: arm a bot, then click its Group.</p>
  </>;
}

export function PhasesStep(props: StepProps) {
  const { wizard, change, selectPhase, endTask } = props;
  const tactic = wizard.tactic;
  const index = Math.min(wizard.phaseIndex, tactic.phases.length - 1);
  const phase = tactic.phases[index];
  if (!phase) return null;
  const last = index === tactic.phases.length - 1;
  return <>
    <p className="t-meta"><b>The operation, phase by phase.</b> Each phase says what every
      Group does; its “Continue when…” is latched — once the next phase begins, a lapsed
      condition never rewinds it. Transitions only ever go forward.</p>
    <div className="flex flex-wrap items-center gap-1" data-testid="phase-storyboard">
      {tactic.phases.map((entry, entryIndex) => (
        <span key={entry.id} className="flex items-center gap-1">
          {entryIndex > 0 && <span className="text-[10px]" style={{ color: '#c8763c' }}>⚡</span>}
          <button type="button"
            className={`btn !min-h-7 !px-2 !text-[10.5px] ${entryIndex === index ? 'btn-on' : ''}`}
            data-testid={`phase-chip-${entryIndex + 1}`}
            onClick={() => { selectPhase(entryIndex); endTask(); }}>
            {entry.name}
          </button>
        </span>
      ))}
      {tactic.phases.length < 8 && (
        <button type="button" className="btn !min-h-7" data-testid="add-phase"
          onClick={() => {
            change((next) => {
              const previous = next.phases[next.phases.length - 1];
              if ((previous.continue ?? []).length === 0) {
                previous.continue = [{ triggers: {}, to: 'next' }];
              }
              next.phases.push(freshPhase(next, `Phase ${next.phases.length + 1}`));
              const added = next.phases[next.phases.length - 1];
              delete added.continue;
            });
            selectPhase(tactic.phases.length);
          }}>
          ＋ Add phase
        </button>
      )}
    </div>
    <div className="flex items-center gap-2">
      <input
        className="min-w-0 grow border-b border-dashed border-transparent bg-transparent text-[13px] font-semibold text-arena-text outline-none transition-colors hover:border-arena-edge2 focus:border-arena-material"
        value={phase.name}
        aria-label="Phase name"
        onChange={(event) => change((next) => {
          next.phases[index].name = event.target.value;
        })}
      />
      <span className="val">phase {index + 1} of {tactic.phases.length}</span>
      {tactic.phases.length > 1 && (
        <button type="button" className="btn" title="Remove this phase"
          onClick={() => {
            change((next) => {
              const removed = next.phases[index];
              next.phases.splice(index, 1);
              for (const entry of next.phases) {
                for (const rule of entry.continue ?? []) {
                  if (rule.to === removed.id) rule.to = 'next';
                }
              }
              const tail = next.phases[next.phases.length - 1];
              if (tail) {
                tail.continue = (tail.continue ?? [])
                  .map((rule) => rule.to === 'next' ? { ...rule, to: 'finish' } : rule);
                if (tail.continue.length === 0) delete tail.continue;
              }
            });
            selectPhase(Math.max(0, index - 1));
            endTask();
          }}>✕</button>
      )}
    </div>
    {tactic.groups.map((group) => (
      <JobSection key={group.id} {...props} group={group} phaseIndex={index} />
    ))}
    <div className="rounded-sm border border-arena-edge p-2"
      style={{ borderLeft: '3px solid #c8763c' }}>
      <div className="mb-1 flex items-center gap-2">
        <b className="t-body">{last ? 'Ends when…' : 'Continue when…'}</b>
        <span className="t-micro text-arena-dim">
          {last
            ? 'the Tactic finishes; leave empty to hold here forever'
            : 'latched — the moment it fires, the operation moves forward'}
        </span>
      </div>
      <CaseListEditor<TacticTransition>
        cases={phase.continue ?? []}
        maximum={3}
        accent="#c8763c"
        watchAdjective="orange"
        requiredKinds={[...TRANSITION_REQUIRED]}
        moreRequiredKinds={[...TRANSITION_REQUIRED_MORE]}
        triggerKinds={[...TRANSITION_TRIGGERS]}
        moreTriggerKinds={[...TRANSITION_MORE]}
        makeCase={() => ({ triggers: {}, to: last ? 'finish' : 'next' })}
        onChange={(rules) => change((next) => {
          if (rules.length === 0) delete next.phases[index].continue;
          else next.phases[index].continue = rules;
        })}
        caseFooter={(rule, update) => (
          <span className="flex items-center gap-1.5">
            <span className="lab">then</span>
            <select className="field !min-h-7 !w-auto !py-0 t-micro"
              value={rule.to}
              onChange={(event) => update((next) => { next.to = event.target.value; })}>
              {!last && <option value="next">the next phase</option>}
              {tactic.phases.map((candidate, candidateIndex) =>
                candidateIndex > index + 1 && (
                  <option key={candidate.id} value={candidate.id}>
                    skip to {candidate.name}
                  </option>
                ))}
              <option value="finish">the Tactic finishes</option>
            </select>
          </span>
        )}
        watching={props.task?.kind === 'watch'
          && props.task.target.scope === 'continue'
          && props.task.target.phaseIndex === index
          ? {
            caseIndex: props.task.target.ruleIndex,
            side: props.task.target.side,
            row: props.task.target.row,
          }
          : null}
        onDrawWatch={(ruleIndex, side, row) => props.startWatchTask({
          scope: 'continue', phaseIndex: index, ruleIndex, side, row,
        })}
        onStopWatch={endTask}
      />
      {last && (phase.continue ?? []).length === 0 && (
        <p className="t-micro mt-1 text-arena-dim">No ending ticked: once here, the squad holds
          this phase until a Tactic-wide abort, the time limit, or the match itself intervenes.</p>
      )}
    </div>
  </>;
}

function JobSection(props: StepProps & { group: TacticGroup; phaseIndex: number }) {
  const {
    catalog, document, wizard, group, phaseIndex, task, change,
    startJobTask, startPostViaTask, startSigTask, endTask, highlightPost,
  } = props;
  const tactic = wizard.tactic;
  const job = jobOf(tactic, phaseIndex, group.id);
  if (!job) return null;
  const first = phaseIndex === 0;
  const carried = job.kind === 'none' && !first
    ? effectiveJob(tactic, phaseIndex, group.id)
    : null;
  const color = groupColor(tactic, group.id);
  const kinds = JOB_KINDS
    .filter((kind) => kind.id !== 'escort'
      // Escort only makes sense when someone can end up carrying — keyed off
      // content (a delivery plan or a Core habit), never off the recipe.
      || job.kind === 'escort' || tactic.delivery.plan !== 'none'
      || tactic.groups.some((value) => value.coreRule !== 'ignore'))
    .map((kind) => kind.id === 'none' && first
      ? { ...kind, label: 'No ground', blurb: 'Start loose — no assigned ground this phase.' }
      : kind);
  const kindDef = JOB_KINDS.find((kind) => kind.id === job.kind);
  const complete = jobComplete(tactic, phaseIndex, group.id);
  const drawing = task?.kind === 'job' && task.groupId === group.id
    && task.phaseIndex === phaseIndex;
  const postures = posturesFor(tactic, job.kind);
  const mutateJob = (apply: (target: NonNullable<ReturnType<typeof jobOf>>) => void) =>
    change((next) => {
      const target = next.phases[phaseIndex]?.jobs[group.id];
      if (target) apply(target);
    });
  const setKind = (kind: JobKind) => {
    mutateJob((target) => {
      if (target.kind !== kind) {
        target.kind = kind;
        delete target.posts;
        delete target.rect;
        delete target.path;
        delete target.point;
        if (!viaLegal(kind)) delete target.via;
      }
    });
    const def = JOB_KINDS.find((value) => value.id === kind);
    // Arm for the kind just chosen — the wizard state hasn't flushed yet, so
    // without the override the task would draw the PREVIOUS kind's geometry.
    if (def?.draw) startJobTask(group.id, phaseIndex, undefined, def.draw);
    else endTask();
  };
  const groupClasses = [...new Set(group.slots
    .map((slot) => document.squad[slot]?.class)
    .filter((classId): classId is string => Boolean(classId)))];
  const spatialClasses = groupClasses
    .filter((classId) => signatureMarkGeometry(catalog, classId));
  const passiveClasses = groupClasses
    .filter((classId) => !signatureMarkGeometry(catalog, classId));
  const className = (classId: string) =>
    catalog.classes.find((value) => value.id === classId)?.name ?? classId;
  return (
    <div className="rounded-sm border border-arena-edge p-2"
      id={`section-${group.id}-${phaseIndex}`}
      style={{ borderLeft: `3px solid ${color}` }}>
      <div className="mb-1 flex items-center gap-2">
        <span className="h-2.5 w-2.5 rounded-[3px]" style={{ background: color }} />
        <b className="t-body">{group.name.trim() || 'Unnamed Group'}</b>
        <span className="val">{group.slots.length} bot{group.slots.length === 1 ? '' : 's'}</span>
      </div>
      <div className="lab">This phase</div>
      <SegChips options={kinds} value={job.kind} accent={color}
        onChange={(kind) => setKind(kind as JobKind)} />
      <p className="t-micro mb-1 text-arena-dim">{kindDef?.blurb}</p>
      {kindDef?.draw
        ? <GeoStatusRow complete={complete}
          label={describeGeometry(job, group)}
          drawing={drawing && task?.kind === 'job' && task.field !== 'via'}
          onDraw={() => startJobTask(group.id, phaseIndex)}
          onStop={endTask}
          onClear={() => mutateJob((target) => {
            delete target.posts;
            delete target.rect;
            delete target.path;
            delete target.point;
          })} />
        : <GeoStatusRow complete label={job.kind === 'return'
          ? 'Route home is worked out automatically.'
          : job.kind === 'escort'
            ? 'They shadow the carrier — no drawing needed.'
            : carried
              ? `${group.name} carries on: ${describeGeometry(carried, group)}`
              : first
                ? `${group.name} starts loose — nothing to draw.`
                : `${group.name} has no earlier ground to carry — pick a job or draw one in an earlier phase.`}
          drawing={false} />}
      {job.kind === 'posts' && (job.posts?.length ?? 0) > 0 && (
        <p className="t-micro mt-1 text-arena-dim">Each posted bot stands on its tile facing one of
          four ways — drag its arrow tip to turn it; the wedge is what it watches.</p>
      )}
      {job.kind === 'posts' && (job.posts?.length ?? 0) > 0 ? (
        // Per-post ways in: each bot can sneak to its own post by its own
        // drawn route instead of the direct path through the middle.
        job.posts!.map((post, index) => {
          const bot = document.squad[group.slots[index]];
          const mine = task?.kind === 'job' && task.groupId === group.id
            && task.phaseIndex === phaseIndex && task.field === 'via'
            && task.postIndex === index;
          const flashed = highlightPost?.groupId === group.id
            && highlightPost.postIndex === index;
          return <div key={index} id={`way-in-${group.id}-${index}`}
            className={flashed
              ? 'animate-pulse rounded-sm ring-2 ring-arena-material' : undefined}>
            <GeoStatusRow
              complete={(post.via?.length ?? 0) >= 2}
              label={`${bot?.name ?? `Post ${index + 1}`} — way in${post.via?.length
                ? ` · ${post.via.length} waypoints` : ' (direct)'}`}
              drawing={mine}
              drawLabel="Draw route"
              onDraw={() => startPostViaTask(group.id, phaseIndex, index)}
              onStop={endTask}
              onClear={post.via ? () => mutateJob((target) => {
                const targetPost = target.posts?.[index];
                if (targetPost) delete targetPost.via;
              }) : undefined}
            />
          </div>;
        })
      ) : viaLegal(job.kind) && kindDef?.draw && (
        <GeoStatusRow
          complete={(job.via?.length ?? 0) >= 2}
          label={job.via?.length
            ? `Approach route — ${job.via.length} waypoints walked in first.`
            : 'Approach route (optional) — swing wide instead of the direct path.'}
          drawing={task?.kind === 'job' && task.groupId === group.id
            && task.phaseIndex === phaseIndex && task.field === 'via'
            && task.postIndex == null}
          drawLabel="Draw route"
          onDraw={() => startJobTask(group.id, phaseIndex, 'via')}
          onStop={endTask}
          onClear={job.via ? () => mutateJob((target) => {
            delete target.via;
          }) : undefined}
        />
      )}
      <div className="lab mt-2">Posture</div>
      <SegChips options={postures} value={job.posture} accent={color}
        onChange={(posture) => mutateJob((target) => {
          target.posture = posture as Posture;
        })} />
      <p className="t-micro mb-1 text-arena-dim">{POSTURES.find((value) => value.id === job.posture)?.blurb}</p>
      {(() => {
        // Travel needs an arrival to hand over at — hide it where the job
        // is all march (patrol/return/escort) or carries nothing forward.
        // A STANDING posture's fight belongs to its ground (it is gated on
        // the station so a fight episode can't anchor the march short of a
        // post), so with no on-the-way choice the march never stops to
        // fight — the default chip and hint must say so rather than claim
        // "same". A mover's posture genuinely governs the whole way.
        const arrival = job.kind === 'none' ? carried?.kind : job.kind;
        if (!arrival || ['patrol', 'return', 'escort'].includes(arrival)) {
          return null;
        }
        const standing = ['posts', 'area', 'line', 'approach'].includes(arrival);
        const arrived = POSTURES.find((value) => value.id === job.posture)?.label
          ?? 'The posture';
        const chosen = job.travel ?? 'same';
        const hint = (() => {
          if (chosen === 'same') {
            if (!standing) return 'One posture the whole way — the march behaves exactly like the destination.';
            if (['wait', 'observe'].includes(job.posture)) {
              return 'They walk in without stopping to fight, and stay quiet on the ground too.';
            }
            return `They walk in without stopping to fight — ${arrived} starts on arrival. Pick “Answer attacks” to fight en route.`;
          }
          return {
            wait: `March in the cautious rear stance without picking fights; ${arrived} takes over on arrival.`,
            hold: `Answer attacks on the way without chasing; ${arrived} takes over on arrival.`,
            aggressive: `Brawl the whole way in; ${arrived} takes over on arrival.`,
            steal: `Steal on the way in; ${arrived} takes over on arrival.`,
            escort: `Screen on the way in; ${arrived} takes over on arrival.`,
            observe: `Watch on the way in; ${arrived} takes over on arrival.`,
          }[chosen];
        })();
        return <>
          <div className="lab">On the way <span className="text-arena-dim">(optional)</span></div>
          <SegChips
            options={[
              { id: 'same', label: standing ? 'Just march' : 'Same as here' },
              { id: 'wait', label: standing ? 'Hang back' : 'Quietly' },
              { id: 'hold', label: 'Answer attacks' },
              { id: 'aggressive', label: 'Brawl in' },
            ]}
            value={chosen}
            accent={color}
            onChange={(travel) => mutateJob((target) => {
              if (travel === 'same') delete target.travel;
              else target.travel = travel as Posture;
            })} />
          <p className="t-micro mb-1 text-arena-dim">{hint}</p>
        </>;
      })()}
      <div className="lab">Recovery</div>
      <SegChips options={RECOVERIES} value={job.recovery} accent={color}
        onChange={(recovery) => mutateJob((target) => {
          target.recovery = recovery as Recovery;
        })} />
      <p className="t-micro text-arena-dim">{RECOVERIES.find((value) => value.id === job.recovery)?.blurb}</p>
      {(spatialClasses.length > 0 || passiveClasses.length > 0) && (
        <div className="lab mt-2">Signatures — used during this phase</div>
      )}
      {spatialClasses.map((classId) => {
        const geometry = signatureMarkGeometry(catalog, classId)!;
        const mark = job.sigs?.[classId];
        const points = mark ? ('points' in mark ? mark.points : mark.line) : [];
        const has = points.length > 0;
        const mine = task?.kind === 'sig' && task.groupId === group.id
          && task.phaseIndex === phaseIndex && task.classId === classId;
        const placed = has
          ? geometry.kind === 'points'
            ? ` · ${points.length}/${geometry.max} marks at ${describePlace(...points[0])}`
            : ` · lane through ${describePlace(...points[0])}`
          : ' · no marks yet — optional';
        return <GeoStatusRow key={classId} complete={has}
          label={`${geometry.icon} ${className(classId)} — ${geometry.label}${placed}`}
          drawLabel="Place marks"
          drawing={mine}
          onDraw={() => startSigTask(group.id, phaseIndex, classId)}
          onStop={endTask}
          onClear={has ? () => mutateJob((target) => {
            if (target.sigs) delete target.sigs[classId];
          }) : undefined} />;
      })}
      {spatialClasses.length === 0 && passiveClasses.length > 0 && (
        <p className="t-micro text-arena-dim">No spatial signatures in this Group — their skills
          are bundled by posture.</p>
      )}
      {spatialClasses.length > 0 && passiveClasses.length > 0 && (
        <p className="t-micro mt-1 text-arena-dim">
          {passiveClasses.map(className).join(', ')}: signatures run themselves, bundled by posture.
        </p>
      )}
    </div>
  );
}

function posturesFor(tactic: Tactic, kind: JobKind) {
  const coreish = tactic.delivery.plan !== 'none'
    || tactic.groups.some((value) => value.coreRule === 'hunt')
    || kind === 'return';
  const standing = ['posts', 'area', 'line', 'approach', 'none'].includes(kind);
  const ids: Posture[] = standing
    ? ['wait', 'hold', 'observe', 'aggressive', ...(coreish ? ['steal', 'escort'] as Posture[] : [])]
    : ['aggressive', 'hold', 'observe', ...(coreish ? ['steal', 'escort'] as Posture[] : [])];
  return POSTURES.filter((posture) => ids.includes(posture.id));
}

export function CoresStep({ catalog, document, wizard, change, addDeliverCore }: StepProps) {
  const tactic = wizard.tactic;
  const preferred = tactic.delivery.preferredSlots ?? [];
  return <>
    <p className="t-meta"><b>Cores.</b> Pickup is each Group's own habit, and ⭑ marks preferred
      carriers. Delivery is one shared plan — the real carrier is whoever ends up holding it.</p>
    {tactic.groups.map((group) => (
      <div key={group.id} className="rounded-sm border border-arena-edge p-2"
        style={{ borderLeft: `3px solid ${groupColor(tactic, group.id)}` }}>
        <div className="mb-1 flex items-center gap-2">
          <b className="t-body">{group.name.trim() || 'Unnamed Group'}</b>
          <span className="val">{group.slots.length} bot{group.slots.length === 1 ? '' : 's'}</span>
        </div>
        <SegChips options={CORE_RULES} value={group.coreRule} accent={groupColor(tactic, group.id)}
          onChange={(rule) => change((next) => {
            const target = next.groups.find((value) => value.id === group.id);
            if (target) target.coreRule = rule;
          })} />
        <p className="t-micro mb-1 text-arena-dim">{CORE_RULES.find((value) => value.id === group.coreRule)?.blurb}</p>
        <div className="flex flex-wrap gap-1">
          {group.slots.map((slot) => {
            const bot = document.squad[slot];
            if (!bot) return null;
            const on = preferred.includes(slot);
            return <BotChip key={slot} bot={bot} catalog={catalog} armed={on}
              note={on ? '⭑' : '☆'}
              onClick={() => change((next) => {
                const list = next.delivery.preferredSlots ?? [];
                next.delivery.preferredSlots = on
                  ? list.filter((value) => value !== slot)
                  : [...list, slot];
                if (next.delivery.preferredSlots.length === 0) delete next.delivery.preferredSlots;
              })} />;
          })}
        </div>
      </div>
    ))}
    <p className="t-micro text-arena-dim">Tap ☆ to prefer a bot as carrier — preferred bots position to
      receive a toss. Cores bank at your reactor.</p>
    <div>
      <div className="lab mb-1">If a Core ends up in our hands</div>
      <ChoiceCards options={DELIVERY_PLANS} value={tactic.delivery.plan}
        onChange={(plan) => change((next) => { next.delivery.plan = plan; })} />
    </div>
    {tactic.delivery.plan !== 'none' && (
      <Readback label="Readback">
        {deliveryClause(tactic, (slot) => document.squad[slot]?.name ?? null)}.
      </Readback>
    )}
    <DeliverCoreSuggestion document={document} tactic={tactic} change={change}
      addDeliverCore={addDeliverCore} />
  </>;
}

export function ReviewStep({ catalog, document, wizard, change, addDeliverCore }: StepProps) {
  const tactic = wizard.tactic;
  const botName = (slot: number) => document.squad[slot]?.name ?? null;
  return <>
    <p className="t-meta">Read it back. If the sentences say what you meant, it’s ready.</p>
    <div className="panel-feature pad">
      <div className="lab mb-1.5">The whole operation</div>
      <p className="t-body leading-relaxed">{summarize(tactic, botName)}</p>
      <PhaseStrip tactic={tactic} big />
    </div>
    {tactic.groups.map((group) => (
      <div key={group.id} className="panel pad"
        style={{ borderLeft: `3px solid ${groupColor(tactic, group.id)}` }}>
        <div className="mb-1 flex items-center gap-2">
          <b className="t-body">{group.name.trim() || 'Unnamed Group'}</b>
          <span className="val">{group.slots.length} bot{group.slots.length === 1 ? '' : 's'}</span>
        </div>
        <p className="t-meta">{groupClause(tactic, group, true)}.</p>
        <div className="mt-1.5 flex flex-wrap gap-1">
          {group.slots.map((slot) => document.squad[slot] && (
            <BotChip key={slot} bot={document.squad[slot]} catalog={catalog} />
          ))}
        </div>
      </div>
    ))}
    <DeliverCoreSuggestion document={document} tactic={tactic} change={change}
      addDeliverCore={addDeliverCore} />
    <p className="t-micro text-arena-dim">The overlays stay live after creating — drag posts, corners,
      waypoints and destinations on the map any time this Tactic is open.</p>
  </>;
}

/** Content-keyed nudge: a Tactic that can finish holding a Core, with no
 * plan for the run home, gets offered one — in place, or as a one-click
 * separate Deliver Core Tactic. */
function DeliverCoreSuggestion({ document, tactic, change, addDeliverCore }: {
  document: TacticsDocument;
  tactic: Tactic;
  change: (apply: (tactic: Tactic) => void) => void;
  addDeliverCore?: () => void;
}) {
  const wantsCore = tacticCanFinish(tactic).finishCarrying
    && tactic.delivery.plan === 'none';
  const hasDeliver = document.tactics.some((value) =>
    value.id !== tactic.id
    && (value.start.cases ?? []).some((entry) =>
      entry.triggers?.carrier || entry.required?.carrier)
    && value.phases.some((phase) => Object.values(phase.jobs)
      .some((job) => job.kind === 'return')));
  if (!wantsCore || hasDeliver) return null;
  return (
    <div className="panel-quiet pad">
      <div className="t-body mb-1 font-semibold">Your tactic may finish while carrying a Core.</div>
      <p className="t-micro text-arena-dim">Plan the run home inside this Tactic, or add a separate
        Deliver Core tactic — prefilled, carrier and escorts picked from your squad.</p>
      <div className="mt-2 flex flex-wrap gap-1.5">
        <button type="button" className="btn btn-secondary"
          onClick={() => change((next) => { next.delivery.plan = 'escort'; })}>
          Plan the delivery in this Tactic
        </button>
        {addDeliverCore && (
          <button type="button" className="btn"
            onClick={addDeliverCore}>
            Add a separate Deliver Core tactic
          </button>
        )}
      </div>
    </div>
  );
}

function Readback({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="panel pad">
      <div className="lab mb-1">{label}</div>
      <p className="t-body leading-relaxed">{children}</p>
    </div>
  );
}

export { possessive };
