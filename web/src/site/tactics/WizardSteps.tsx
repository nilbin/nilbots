import { useRef, useState } from 'react';
import type { TacticalSheetCatalog } from '../api';
import { BotChip } from './BotChip';
import { ChoiceCards } from './ChoiceCards';
import { CaseListEditor } from './ConditionRows';
import { GeoStatusRow } from './GeoStatusRow';
import { Modal } from './Modal';
import { PhaseStrip } from './PhaseStrip';
import { SegChips } from './SegChips';
import { freshGroup } from './recipes';
import {
  deliveryClause, groupClause, possessive, startClause, summarize,
} from './summary';
import { describeGeometry } from './TacticsMapCanvas';
import type { MapTask, WatchTarget } from './useMapTask';
import {
  ABORT_ROWS, CORE_RULES, DELIVERY_PLANS, JOB_KINDS, MAX_DURATIONS, POSTURES,
  TRAVEL_BEHAVIORS,
  RECOVERIES, START_ROWS, TRANSITION_ROWS,
  abortCasesCanStart,
  effectiveJob, effectiveJobResolution, followMembership, freshJob, freshPhase, groupColor, jobOf,
  jobHasGeometry, phaseShade, planJobKindChange, signatureMarkGeometry,
  tacticCanFinish, viaLegal,
  type CondKind, type CustodyPlan, type DrawKind, type JobKind,
  type MembershipPocket, type Posture,
  type Recovery, type Tactic, type TacticGroup, type TacticTransition,
  type TacticsDocument, type WorkshopChoice,
} from './tacticsDocument';
import {
  assignedCount, jobComplete, unassignedSlots, type WizardState,
} from './wizardState';
import { describePlace, WORKSHOP_OPTIONS } from './places';
import { materializeSpatialJob } from './mapFirstSpatial';

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
  startSigTask: (groupId: string, phaseIndex: number, slot: number, classId: string) => void;
  startWatchTask: (target: WatchTarget) => void;
  /** Draw the carrier's way home for one phase's custody override. */
  startCarryTask: (phaseIndex: number) => void;
  endTask: () => void;
  /** Append a prefilled Deliver Core Tactic to the sheet (carrier picked,
   * escorts and screen auto-assigned) without leaving this wizard. */
  addDeliverCore?: () => void;
  /** A map click on a posted bot briefly flashes its way-in row. */
  highlightPost?: { groupId: string; postIndex: number } | null;
  /** Transient feedback toast — what a reshuffle carried along, and such. */
  notify?: (text: string) => void;
  /** Editor-only authoring sugar: on Done, mirror this Tactic's abort cases
   * into the chosen target's Start and place it immediately above. */
  abortSwitchTargetId?: string | null;
  setAbortSwitchTarget?: (tacticId: string | null) => void;
}

// One vocabulary per context (mirrors the server's row lists), grouped by
// ConditionRows for overview. Positive rows are all AND in the editor;
// Unless drops readiness because that row reads only one way.
const noEveryoneRows = (rows: readonly CondKind[]) =>
  rows.filter((kind) => kind !== 'groupsSet' && kind !== 'healed' && kind !== 'veterancy');
const noReadinessRows = (rows: readonly CondKind[]) =>
  rows.filter((kind) => kind !== 'groupsSet');

export function StartStep({
  document, wizard, change, task, startWatchTask, endTask,
  abortSwitchTargetId, setAbortSwitchTarget,
}: StepProps) {
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
        zoneIndex: task.target.zoneIndex,
      }
      : null;
  const [abortOpen, setAbortOpen] = useState(Boolean(tactic.abort));
  return <>
    <p className="t-meta" id="start-controls">First things first: <b>when does this Tactic apply at all?</b>{' '}
      The Sheet still reads top to bottom — this only gates it further. Once it
      starts, it is committed: it keeps control until it finishes, aborts, or
      runs out its time limit.</p>
    <SegChips options={modes} value={tactic.start.mode}
      onChange={(mode) => change((next) => {
        next.start = mode === 'when'
          ? { mode, cases: next.start.cases ?? [{ required: {} }] }
          : { mode: 'always' };
      })} />
    <p className="t-micro text-arena-dim">{modes.find((mode) => mode.id === tactic.start.mode)?.blurb}</p>
    {tactic.start.mode === 'when' && (
      <CaseListEditor
        cases={tactic.start.cases ?? [{ required: {} }]}
        maximum={3}
        accent="#e6ddd4"
        watchSymbol="▶"
        watchAdjective="pale"
        requiredKinds={[...START_ROWS]}
        triggerKinds={noEveryoneRows(START_ROWS)}
        unlessKinds={noReadinessRows(START_ROWS)}
        makeCase={() => ({ required: {} })}
        onChange={(cases) => change((next) => { next.start.cases = cases; })}
        watching={watchFor('start')}
        onDrawWatch={(caseIndex, side, row, zoneIndex) =>
          startWatchTask({ scope: 'start', caseIndex, side, row, zoneIndex })}
        onStopWatch={endTask}
      />
    )}
    <Readback label="Readback">
      {startClause(tactic) ?? (tactic.start.mode === 'when'
        ? 'No condition ticked yet — tick one below, or this Tactic never applies.'
        : 'Always available — the Sheet order alone decides when it runs.')}
    </Readback>
    <div className="rounded-sm border border-arena-edge p-2" id="abort-controls"
      style={{ borderLeft: '3px solid #f4526b' }}>
      <div className="flex items-center gap-2">
        <b className="t-body">Abort when…</b>
        <span className="t-micro text-arena-dim">bails out of every phase and hands the Sheet back</span>
        <span className="grow" />
        <button type="button" className="btn"
          onClick={() => {
            if (tactic.abort) {
              change((next) => { delete next.abort; });
              setAbortSwitchTarget?.(null);
              setAbortOpen(false);
            } else {
              change((next) => {
                next.abort = { cases: [{ required: { lossesWithin: { count: 4, ticks: 30 } } }] };
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
            requiredKinds={noEveryoneRows(ABORT_ROWS)}
            triggerKinds={noEveryoneRows(ABORT_ROWS)}
            unlessKinds={noReadinessRows(ABORT_ROWS)}
            makeCase={() => ({ required: {} })}
            onChange={(cases) => change((next) => { next.abort = { cases }; })}
            watching={watchFor('abort')}
            onDrawWatch={(caseIndex, side, row, zoneIndex) =>
              startWatchTask({ scope: 'abort', caseIndex, side, row, zoneIndex })}
            onStopWatch={endTask}
          />
          <p className="t-micro mt-1 text-arena-dim">After an abort the whole Sheet is reread —
            a higher emergency Tactic (a Regroup gated on the same losses) can take over,
            and this one may later restart from its first phase.</p>
          <div className="mt-2 rounded-sm border border-arena-edge bg-arena-panel/35 p-2">
            <label className="lab mb-1 block" htmlFor="abort-switch-target">
              On abort, prepare… <span className="text-arena-dim">(authoring shortcut)</span>
            </label>
            <select id="abort-switch-target" className="field w-full"
              value={abortSwitchTargetId ?? ''}
              disabled={!abortCasesCanStart(tactic.abort.cases)}
              onChange={(event) => setAbortSwitchTarget?.(event.target.value || null)}>
              <option value="">No linked fallback — reread the Sheet normally</option>
              {document.tactics.filter((candidate) => candidate.id !== tactic.id)
                .map((candidate) => <option value={candidate.id} key={candidate.id}>
                  {candidate.name}
                </option>)}
            </select>
            {abortCasesCanStart(tactic.abort.cases) ? (
              <p className="t-micro mt-1 text-arena-dim">
                On Done, the chosen Tactic gets these same conditions as its Start and moves
                directly above this one. The executor still rereads the Sheet top to bottom;
                an eligible emergency Tactic above both can still win.
              </p>
            ) : (
              <p className="t-micro mt-1 text-arena-hot">
                This abort uses a phase-only fact that cannot start another Tactic. Choose
                start-capable conditions to link a fallback.
              </p>
            )}
          </div>
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

export function GroupsStep({ catalog, document, wizard, change, arm, notify }: StepProps) {
  const tactic = wizard.tactic;
  const unassigned = unassignedSlots(tactic, document);
  const assigned = assignedCount(tactic);
  const squadReady = document.squad.length === 8;
  // HTML5 drag state: which slot is airborne, and which box would take it.
  const [dragSlot, setDragSlot] = useState<number | null>(null);
  const [dropTarget, setDropTarget] = useState<string | null>(null);
  // Belongings of bots parked in the tray mid-move: a Group→tray→Group
  // shuffle keeps posts and marks across the stop. Session-local on purpose.
  const pocket = useRef<MembershipPocket>(new Map());
  /** Every membership change routes through here so each phase's per-bot
   * config — posts, ways in, signature marks — follows the bots instead of
   * silently shifting onto whoever remains. Applied eagerly on a clone (not
   * inside the state updater) so the pocket and the toast see exactly one
   * run even where React double-invokes updaters. */
  const rehome = (mutate: (next: Tactic) => void) => {
    const next = structuredClone(tactic);
    const before = next.groups.map((group) => ({
      id: group.id, slots: [...group.slots],
    }));
    mutate(next);
    const summary = followMembership(
      next, document.squad, before, pocket.current);
    change((draft) => {
      draft.groups = next.groups;
      draft.phases = next.phases;
    });
    if (summary) notify?.(summary);
  };
  const place = (slot: number, groupId: string | null) => rehome((next) => {
    for (const group of next.groups) {
      group.slots = group.slots.filter((value) => value !== slot);
    }
    if (groupId) next.groups.find((group) => group.id === groupId)?.slots.push(slot);
  });
  /** Dropping one bot onto another swaps their group memberships. */
  const swap = (a: number, b: number) => rehome((next) => {
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
            {squadReady ? 'Everyone is assigned.' : 'Use Squad above to fill all eight before assigning the rest.'}
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
                // rehome pockets the freed bots' posts and marks before the
                // dissolved Group's job entries are cleaned away.
                rehome((next) => {
                  next.groups = next.groups.filter((value) => value.id !== group.id);
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
        onClick={() => rehome((next) => {
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
  return <>
    <p className="t-meta"><b>The operation, phase by phase.</b> Each phase says what every
      Group does; its “Continue when…” is latched — once the next phase begins, a lapsed
      condition never rewinds it. Transitions only ever go forward.</p>
    <div className="flex flex-wrap items-center gap-1" data-testid="phase-storyboard">
      {tactic.phases.map((entry, entryIndex) => (
        <span key={entry.id} className="flex items-center gap-1">
          {entryIndex > 0 && <span className="text-[10px]" style={{ color: '#c8763c' }}>⚡</span>}
          <button type="button"
            className={`btn flex items-center gap-1.5 !min-h-7 !px-2 !text-[10.5px] ${entryIndex === index ? 'btn-on' : ''}`}
            data-testid={`phase-chip-${entryIndex + 1}`}
            onClick={() => { selectPhase(entryIndex); endTask(); }}>
            <span className="flex h-2.5 w-2 flex-col overflow-hidden rounded-[2px]">
              {tactic.groups.map((entryGroup) => (
                <span key={entryGroup.id} className="grow" style={{
                  background: phaseShade(groupColor(tactic, entryGroup.id),
                    entryIndex, tactic.phases.length),
                }} />
              ))}
            </span>
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
                previous.continue = [{ required: {} }];
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
      <span className="val">phase {index + 1} of {tactic.phases.length}</span>{/* custody below */}
      {tactic.phases.length > 1 && (
        <button type="button" className="btn" title="Remove this phase"
          onClick={() => {
            change((next) => {
              next.phases.splice(index, 1);
              const tail = next.phases[next.phases.length - 1];
              if (tail && (tail.continue ?? []).every((rule) =>
                Object.keys(rule.required ?? {}).length === 0
                && Object.keys(rule.triggers ?? {}).length === 0
                && Object.keys(rule.unless ?? {}).length === 0)) delete tail.continue;
            });
            selectPhase(Math.max(0, index - 1));
            endTask();
          }}>✕</button>
      )}
    </div>
    {tactic.groups.map((group) => (
      <JobSection key={group.id} {...props} group={group} phaseIndex={index} />
    ))}
    <PhaseCustodyEditor {...props} phaseIndex={index} />
    <TransitionEditor {...props} phaseIndex={index} />
  </>;
}

/** Shared verbatim between Classic's phase page and Map-first's temporary
 * Core drawer. Keeping this as the one mutation surface prevents custody
 * behavior from drifting between layouts. */
export function PhaseCustodyEditor(props: StepProps & { phaseIndex: number }) {
  const { wizard, change, phaseIndex: index } = props;
  const tactic = wizard.tactic;
  const phase = tactic.phases[index];
  const [editingOverridePhaseId, setEditingOverridePhaseId] = useState<string | null>(
    phase?.custody ? phase.id : null,
  );
  if (!phase) return null;
  const showOverrideEditor = editingOverridePhaseId === phase.id || Boolean(phase.custody);
  return <div className="rounded-sm border border-arena-edge p-2"
      style={{ borderLeft: '3px solid #b49cff' }}>
      <div className="mb-1 flex items-start gap-2">
        <div className="grow">
          <div className="lab mb-0.5">Cores acquired here · {phase.name}</div>
          <b className="t-body">Custody policy</b>
        </div>
        {!showOverrideEditor && <button type="button" className="btn !min-h-7"
          onClick={() => setEditingOverridePhaseId(phase.id)}>Set pickup policy</button>}
      </div>
      {!showOverrideEditor
        ? <p className="t-micro text-arena-dim">Uses the Tactic default: {{
          none: 'keep working with the Core',
          solo: 'bank alone',
          escort: 'bank under escort',
        }[tactic.delivery.plan]}.</p>
        : <><SegChips
        options={[
          { id: 'same', label: 'Use Tactic default' },
          { id: 'solo', label: 'Banks alone' },
          { id: 'escort', label: 'Under escort' },
          { id: 'keep', label: 'Keep carrying' },
        ]}
        value={phase.custody?.plan ?? 'same'}
        accent="#b49cff"
        onChange={(plan) => {
          change((next) => {
            const target = next.phases[index];
            if (plan === 'same') delete target.custody;
            else if (plan === 'keep') target.custody = { plan };
            else {
              target.custody = {
                plan: plan as CustodyPlan,
                ...(target.custody?.via ? { via: target.custody.via } : {}),
              };
            }
          });
          if (plan === 'same') setEditingOverridePhaseId(null);
        }} />
      <p className="t-micro mb-1 text-arena-dim">
        {{
          same: {
            none: 'The Tactic has no delivery plan — carriers keep working and banking waits.',
            solo: 'The Tactic’s plan applies: a carrier breaks off and banks alone.',
            escort: 'The Tactic’s plan applies: the carrier banks under escort.',
          }[tactic.delivery.plan] ?? '',
          solo: 'A carrier breaks off and runs the Core home alone.',
          escort: 'The carrier’s group screens the run home.',
          keep: 'Hold the Core in hand and stay on this phase’s job. Later phase policies do not replace it.',
        }[phase.custody?.plan ?? 'same']}
      </p>
      {phase.custody && phase.custody.plan !== 'keep' && (
        <div className="flex items-center gap-2">
          <button type="button" className="btn btn-secondary"
            onClick={() => {
              change((next) => {
                const target = next.phases[index];
                if (target.custody) delete target.custody.via;
              });
              props.startCarryTask(index);
            }}>
            {phase.custody.via?.length ? 'Edit the carrier’s way home' : 'Draw the carrier’s way home'}
          </button>
          {phase.custody.via?.length
            ? <span className="t-micro text-arena-dim">{phase.custody.via.length} waypoints to the reactor</span>
            : <span className="t-micro text-arena-dim">optional — without one the carrier picks its own way</span>}
        </div>
      )}</>}
    </div>;
}

/** The complete existing transition case editor, reusable in Map-first's
 * temporary drawer. Required/Triggered/Unless, alternatives and every zone
 * drawing callback remain the exact same component and mutation path. */
export function TransitionEditor(props: StepProps & { phaseIndex: number }) {
  const { wizard, change, endTask, phaseIndex: index } = props;
  const tactic = wizard.tactic;
  const phase = tactic.phases[index];
  if (!phase) return null;
  const last = index === tactic.phases.length - 1;
  return <div className="rounded-sm border border-arena-edge p-2"
      id={`transition-controls-${phase.id}`}
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
        // Keyed per phase: the storyboard swaps phases through this ONE
        // component position, and without a key the fold state (initialized
        // once — "collapse when several alternatives exist") leaks from the
        // first phase onto every other, leaving multi-alternative phases
        // fully expanded.
        key={phase.id}
        cases={phase.continue ?? []}
        maximum={3}
        accent="#c8763c"
        watchAdjective="orange"
        slots={props.document.squad.map((bot) => ({
          slot: bot.slot,
          label: bot.name || `${bot.class} ${bot.slot + 1}`,
        }))}
        requiredKinds={[...TRANSITION_ROWS]}
        triggerKinds={noEveryoneRows(TRANSITION_ROWS)}
        unlessKinds={noReadinessRows(TRANSITION_ROWS)}
        makeCase={() => ({ required: {} })}
        onChange={(rules) => change((next) => {
          if (rules.length === 0) delete next.phases[index].continue;
          else next.phases[index].continue = rules;
        })}
        watching={props.task?.kind === 'watch'
          && props.task.target.scope === 'continue'
          && props.task.target.phaseIndex === index
          ? {
            caseIndex: props.task.target.ruleIndex,
            side: props.task.target.side,
            row: props.task.target.row,
            zoneIndex: props.task.target.zoneIndex,
          }
          : null}
        onDrawWatch={(ruleIndex, side, row, zoneIndex) => props.startWatchTask({
          scope: 'continue', phaseIndex: index, ruleIndex, side, row, zoneIndex,
        })}
        onStopWatch={endTask}
      />
      {last && (phase.continue ?? []).length === 0 && (
        <p className="t-micro mt-1 text-arena-dim">No ending ticked: once here, the squad holds
          this phase until a Tactic-wide abort, the time limit, or the match itself intervenes.</p>
      )}
    </div>;
}

export function JobSection(props: StepProps & {
  group: TacticGroup;
  phaseIndex: number;
  mapFirst?: boolean;
}) {
  const {
    catalog, document, wizard, group, phaseIndex, task, change,
    selectPhase, startJobTask, startPostViaTask, startSigTask, endTask, highlightPost,
  } = props;
  const tactic = wizard.tactic;
  const job = jobOf(tactic, phaseIndex, group.id);
  const [jobUndo, setJobUndo] = useState<NonNullable<typeof job> | null>(null);
  const [pendingKind, setPendingKind] = useState<{
    kind: JobKind;
    next: NonNullable<typeof job>;
    removed: string[];
  } | null>(null);
  if (!job) return null;
  const first = phaseIndex === 0;
  const carried = job.kind === 'none' && !first
    ? effectiveJob(tactic, phaseIndex, group.id)
    : null;
  const carrySource = job.kind === 'none'
    ? effectiveJobResolution(tactic, phaseIndex, group.id)
    : null;
  const color = groupColor(tactic, group.id);
  const kinds = JOB_KINDS
    .filter((kind) => !props.mapFirst || kind.id === 'posts' || kind.id === 'patrol'
      || kind.id === job.kind)
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
  const applyKindChange = (plan: NonNullable<typeof pendingKind>) => {
    if (plan.removed.length > 0) setJobUndo(structuredClone(job));
    change((next) => {
      next.phases[phaseIndex].jobs[group.id] = structuredClone(plan.next);
    });
    setPendingKind(null);
    const def = JOB_KINDS.find((value) => value.id === plan.kind);
    if (def?.draw && !jobHasGeometry(plan.next)) {
      // Arm for the kind just chosen — the wizard state has not flushed yet.
      startJobTask(group.id, phaseIndex, undefined, def.draw);
    } else {
      endTask();
    }
    if (plan.removed.length > 0) {
      props.notify?.('Job changed. Compatible map work was kept; Undo restores the rest.');
    }
  };
  const setKind = (kind: JobKind) => {
    if (job.kind === kind) return;
    const plan = { kind, ...planJobKindChange(job, kind) };
    if (plan.removed.length > 0) {
      setPendingKind(plan);
      return;
    }
    applyKindChange(plan);
  };
  const groupBots = group.slots
    .map((slot) => document.squad.find((bot) => bot.slot === slot))
    .filter((bot): bot is NonNullable<typeof bot> => Boolean(bot));
  // A trainee casts nothing: any signature would reset the channel, so the
  // marks belong to the guard Group and this section disappears entirely.
  const training = job.kind === 'train';
  const spatialBots = training ? [] : groupBots
    .filter((bot) => signatureMarkGeometry(catalog, bot.class));
  const passiveBots = training ? [] : groupBots
    .filter((bot) => !signatureMarkGeometry(catalog, bot.class));
  const workshop: WorkshopChoice = job.workshop ?? 'either';
  const className = (classId: string) =>
    catalog.classes.find((value) => value.id === classId)?.name ?? classId;
  return <>
    <div className="rounded-sm border border-arena-edge p-2"
      id={`section-${group.id}-${phaseIndex}`}
      style={{ borderLeft: `3px solid ${color}` }}>
      <div className="mb-1 flex items-center gap-2">
        <span className="h-2.5 w-2.5 rounded-[3px]" style={{ background: color }} />
        <b className="t-body">{group.name.trim() || 'Unnamed Group'}</b>
        <span className="val">{group.slots.length} bot{group.slots.length === 1 ? '' : 's'}</span>
      </div>
      <div className="lab">This phase</div>
      {job.kind === 'none' && !first && <div className="mb-1 rounded-sm border border-arena-edge bg-arena-panel2/40 p-2 t-micro text-arena-dim">
        {carrySource
          ? <>Continuing the assignment from <b>{tactic.phases[carrySource.sourcePhaseIndex].name}</b>.
            {' '}Current posture, recovery, travel, and signature marks still belong to this phase.
            <button type="button" className="btn btn-secondary ml-2"
              onClick={() => selectPhase(carrySource.sourcePhaseIndex)}>
              Edit in {tactic.phases[carrySource.sourcePhaseIndex].name}
            </button></>
          : <>No earlier assignment to continue. Choose a job and draw its ground.</>}
      </div>}
      {!kindDef && job.kind !== 'none' && <div className="mb-1 rounded-sm border border-arena-warning/50 bg-arena-warning/5 p-2 t-micro text-arena-warning">
        Existing advanced job: <b>{job.kind}</b>. It still runs as saved. {props.mapFirst
          ? 'Use the explicit Convert to posts action in the map toolbar only when you want to replace it.'
          : 'Choose one of the four normal jobs below only when you want to replace it.'}
      </div>}
      {!props.mapFirst && <SegChips options={kinds} value={job.kind} accent={color}
        onChange={(kind) => setKind(kind as JobKind)} />}
      {jobUndo && <button type="button" className="btn mt-1"
        onClick={() => {
          const restore = structuredClone(jobUndo);
          mutateJob((target) => {
            for (const key of Object.keys(target) as (keyof typeof target)[]) delete target[key];
            Object.assign(target, restore);
          });
          setJobUndo(null);
          endTask();
          props.notify?.('Previous job geometry restored.');
        }}>Undo job change</button>}
      <p className="t-micro mb-1 text-arena-dim">{kindDef?.blurb}</p>
      {training ? <>
        {group.slots.length !== 1 && (
          <p className="mb-1 rounded-sm border border-arena-warning/50 bg-arena-warning/5 p-2 t-micro text-arena-warning">
            Training is one bot’s job, and {group.name} has {group.slots.length} bots.
            Leave the trainee here alone and move the rest into their own Group —
            the guards who screen the Workshop mouths. This Tactic cannot be
            created until this Group is a single bot.
          </p>
        )}
        <div className="lab">Workshop</div>
        <SegChips options={WORKSHOP_OPTIONS} value={workshop} accent={color}
          onChange={(next) => mutateJob((target) => {
            target.workshop = next as WorkshopChoice;
          })} />
        <p className="t-micro mb-1 text-arena-dim">
          {WORKSHOP_OPTIONS.find((value) => value.id === workshop)?.blurb}
        </p>
        <GeoStatusRow complete={group.slots.length === 1}
          label={`Nothing to draw — the ${workshop === 'either' ? 'chosen' : workshop} room owns its training tile.`}
          drawing={false} />
        <ul className="mt-1 list-disc space-y-0.5 pl-4 t-micro text-arena-dim">
          <li>One bot walks in and <b>waits on the tile</b>; the rules run the channel.</li>
          <li>The channel breaks if it <b>moves, attacks, takes damage</b> or picks a Core up.</li>
          <li>Living enemies in the room <b>pause</b> it — that is what the guards are for.</li>
          <li>Your side and their side mirror with the entrant; north and south name a fixed room.</li>
        </ul>
        {job.recovery !== 'fight-on' && (
          <p className="mt-1 t-micro text-arena-warning">
            Breaking for the heal tile walks off the training tile and loses the
            channel’s progress — “Fight on” is the recovery that finishes a session.
          </p>
        )}
      </> : kindDef?.draw
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
      {props.mapFirst && carried?.kind === 'posts' && (carried.posts?.length ?? 0) > 0 && (
        <div className="mt-1 rounded-sm border border-arena-material/35 bg-arena-material/5 p-1.5">
          <div className="lab mb-1">Inherited exact Posts</div>
          {carried.posts!.map((post, postIndex) => {
            const bot = document.squad[post.slot ?? group.slots[postIndex]];
            const mine = task?.kind === 'job' && task.groupId === group.id
              && task.phaseIndex === phaseIndex && task.field === 'via'
              && task.postIndex === postIndex;
            return <GeoStatusRow key={postIndex}
              complete={(post.via?.length ?? 0) >= 2}
              label={`${bot?.name ?? `Post ${postIndex + 1}`} — way in${post.via?.length
                ? ` · ${post.via.length} waypoints` : ' (direct)'}`}
              drawing={mine} drawLabel="Draw route"
              onDraw={() => {
                change((next) => { materializeSpatialJob(next, phaseIndex, group.id); });
                startPostViaTask(group.id, phaseIndex, postIndex);
              }}
              onStop={endTask}
              onClear={post.via ? () => change((next) => {
                const target = materializeSpatialJob(next, phaseIndex, group.id);
                const targetPost = target?.posts?.[postIndex];
                if (targetPost) delete targetPost.via;
              }) : undefined} />;
          })}
        </div>
      )}
      {job.kind === 'posts' && (job.posts?.length ?? 0) > 0 && (
        <p className="t-micro mt-1 text-arena-dim">Each posted bot stands on its tile facing one of
          four ways — drag its arrow tip to turn it; the wedge is what it watches.</p>
      )}
      {job.kind === 'posts' && (job.posts?.length ?? 0) > 0 ? (
        // Per-post ways in: each bot can sneak to its own post by its own
        // drawn route instead of the direct path through the middle.
        job.posts!.map((post, index) => {
          const bot = document.squad[post.slot ?? group.slots[index]];
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
      {training ? (
        // Passive by grammar: any fight resets the channel, so there is no
        // posture to choose — saying so beats an advanced-value warning.
        <p className="t-micro mb-1 text-arena-dim">
          Fixed: the trainee <b>waits</b> on the tile and does not voluntarily fight.
          A posture that picks fights would reset the channel every time.
        </p>
      ) : <>
        {!POSTURES.some((value) => value.id === job.posture) && (
          <p className="t-micro text-arena-warning">Existing advanced posture: <b>{job.posture}</b>. It remains active until you choose a normal posture.</p>
        )}
        <SegChips options={postures} value={job.posture} accent={color}
          onChange={(posture) => mutateJob((target) => {
            target.posture = posture as Posture;
          })} />
        <p className="t-micro mb-1 text-arena-dim">{POSTURES.find((value) => value.id === job.posture)?.blurb}</p>
      </>}
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
        const arrived = training
          ? 'Waiting quietly on the training tile'
          : POSTURES.find((value) => value.id === job.posture)?.label
            ?? 'The posture';
        const chosen = job.travel ?? 'same';
        const visibleTravel = TRAVEL_BEHAVIORS.find((value) =>
          value.id === (chosen === 'same' ? 'observe' : chosen));
        const hint = visibleTravel
          ? `${visibleTravel.blurb} ${arrived} is the destination posture.`
          : `Existing advanced travel behavior: ${chosen}. ${arrived} is the destination posture.`;
        return <>
          <div className="lab">On the way <span className="text-arena-dim">(optional)</span></div>
          {job.travel && !TRAVEL_BEHAVIORS.some((value) => value.id === job.travel) && (
            <p className="t-micro text-arena-warning">Existing advanced travel behavior: <b>{job.travel}</b>.</p>
          )}
          <SegChips
            options={TRAVEL_BEHAVIORS}
            value={chosen === 'same' ? 'observe' : chosen}
            accent={color}
            onChange={(travel) => mutateJob((target) => {
              target.travel = travel as Posture;
            })} />
          <p className="t-micro mb-1 text-arena-dim">{hint}</p>
        </>;
      })()}
      <div className="lab">Recovery</div>
      {!RECOVERIES.some((value) => value.id === job.recovery) && (
        <p className="t-micro text-arena-warning">Existing advanced recovery: <b>{job.recovery}</b>. It remains active until replaced.</p>
      )}
      <SegChips options={RECOVERIES} value={job.recovery} accent={color}
        onChange={(recovery) => mutateJob((target) => {
          target.recovery = recovery as Recovery;
        })} />
      <p className="t-micro text-arena-dim">{RECOVERIES.find((value) => value.id === job.recovery)?.blurb}</p>
      {(spatialBots.length > 0 || passiveBots.length > 0) && (
        <div className="lab mt-2">Signatures — used during this phase</div>
      )}
      {spatialBots.map((bot) => {
        const classId = bot.class;
        const geometry = signatureMarkGeometry(catalog, classId)!;
        const plan = job.sigs?.[String(bot.slot)];
        const points = plan?.points ?? plan?.line ?? [];
        const has = points.length > 0;
        const mine = task?.kind === 'sig' && task.groupId === group.id
          && task.phaseIndex === phaseIndex && task.slot === bot.slot;
        const placed = has
          ? geometry.kind === 'points'
            ? ` · ${points.length} mark${points.length === 1 ? '' : 's'} (up to ${geometry.max}) at ${describePlace(...points[0])}`
            : ` · lane through ${describePlace(...points[0])}`
          : ' · no marks yet — optional';
        return <GeoStatusRow key={bot.slot} complete={has}
          label={`${geometry.icon} ${bot.name} · ${className(classId)} — ${geometry.label}${placed}`}
          drawLabel="Place marks"
          drawing={mine}
          onDraw={() => startSigTask(group.id, phaseIndex, bot.slot, classId)}
          onStop={endTask}
          onClear={has ? () => mutateJob((target) => {
            if (target.sigs) delete target.sigs[String(bot.slot)];
          }) : undefined} />;
      })}
      {spatialBots.length === 0 && passiveBots.length > 0 && (
        <p className="t-micro text-arena-dim">No spatial signatures in this Group — their skills
          are bundled by posture.</p>
      )}
      {spatialBots.length > 0 && passiveBots.length > 0 && (
        <p className="t-micro mt-1 text-arena-dim">
          {passiveBots.map((bot) => bot.name).join(', ')}: signatures run themselves, bundled by posture.
        </p>
      )}
    </div>
    {pendingKind && (
      <Modal onClose={() => setPendingKind(null)}>
        <div className="lab mb-1">Change this Group’s job?</div>
        <h3 className="mb-2 text-xl font-semibold">Some map work does not fit the new job.</h3>
        <p className="t-meta mb-2">
          Switching to <b>{JOB_KINDS.find((value) => value.id === pendingKind.kind)?.label
            ?? pendingKind.kind}</b> removes {pendingKind.removed.join(', ')}.
          Compatible geometry and signature marks stay in place.
        </p>
        <div className="flex flex-wrap gap-2">
          <button type="button" className="btn btn-primary" autoFocus
            onClick={() => setPendingKind(null)}>
            Cancel · keep current job
          </button>
          <button type="button" className="btn btn-secondary"
            onClick={() => applyKindChange(pendingKind)}>
            Change job
          </button>
        </div>
        <p className="t-micro mt-2 text-arena-dim">Cancel is the default. Undo is also available after changing.</p>
      </Modal>
    )}
  </>;
}

function posturesFor(tactic: Tactic, kind: JobKind) {
  void tactic;
  void kind;
  return POSTURES;
}

export function CoresStep({ wizard, change }: StepProps) {
  const tactic = wizard.tactic;
  return <div className="flex flex-col gap-2 rounded-sm border border-arena-edge p-2"
    style={{ borderLeft: '3px solid #8fd8ff' }}>
    <div>
      <div className="lab mb-0.5">Whole Tactic</div>
      <b className="t-body">Core defaults</b>
    </div>
    <p className="t-meta">Pickup is each Group's own habit. Delivery is one shared plan —
      the real carrier is whoever ends up holding the Core.</p>
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
      </div>
    ))}
    <p className="t-micro text-arena-dim">Relays can throw a carried Core ahead and reclaim it themselves.
      Cores bank at your reactor.</p>
    <div>
      <div className="lab mb-1">Tactic-wide delivery default</div>
      <ChoiceCards options={DELIVERY_PLANS} value={tactic.delivery.plan}
        onChange={(plan) => change((next) => { next.delivery.plan = plan; })} />
    </div>
    {tactic.delivery.plan !== 'none' && (
      <Readback label="Readback">
        {deliveryClause(tactic)}.
      </Readback>
    )}
    <DeliverCoreSuggestion tactic={tactic} change={change} />
  </div>;
}

export function ReviewStep({ catalog, document, wizard, change }: StepProps) {
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
    <DeliverCoreSuggestion tactic={tactic} change={change} />
    <p className="t-micro text-arena-dim">The overlays stay live after creating — drag posts, corners,
      waypoints and destinations on the map any time this Tactic is open.</p>
  </>;
}

/** Content-keyed nudge: a Tactic that can finish holding a Core, with no
 * plan for the run home, gets offered one — in place, or as a one-click
 * separate Deliver Core Tactic. */
function DeliverCoreSuggestion({ tactic, change }: {
  tactic: Tactic;
  change: (apply: (tactic: Tactic) => void) => void;
}) {
  const wantsCore = tacticCanFinish(tactic).finishCarrying
    && tactic.delivery.plan === 'none';
  if (!wantsCore) return null;
  return (
    <div className="panel-quiet pad">
      <div className="t-body mb-1 font-semibold">Your tactic may finish while carrying a Core.</div>
      <p className="t-micro text-arena-dim">A Core keeps the custody policy of the phase where it was picked up, even after the squad advances. Add a delivery plan here so that policy can bank it; otherwise the carrier keeps doing the pickup phase’s job.</p>
      <div className="mt-2 flex flex-wrap gap-1.5">
        <button type="button" className="btn btn-secondary"
          onClick={() => change((next) => { next.delivery.plan = 'escort'; })}>
          Plan the delivery in this Tactic
        </button>
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
