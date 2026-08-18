import type { TacticalSheetCatalog } from '../api';
import { BotChip } from './BotChip';
import { ChoiceCards } from './ChoiceCards';
import { ConditionRows } from './ConditionRows';
import { GeoStatusRow } from './GeoStatusRow';
import { PhaseStrip } from './PhaseStrip';
import { SegChips } from './SegChips';
import { freshGroup } from './recipes';
import {
  actClause, deliveryClause, groupClause, releaseClause, startClause, summarize,
} from './summary';
import { describeGeometry } from './TacticsMapCanvas';
import type { MapTask } from './useMapTask';
import {
  ACT_KINDS, CORE_RULES, DELIVERY_PLANS, MAX_ATTEMPTS, POSTURES, RECOVERIES,
  STAGE_KINDS, groupColor, signatureMarkGeometry, type ActKind, type Posture,
  type Recovery, type StageKind, type Tactic, type TacticGroup,
  type TacticsDocument, type ZoneRef,
} from './tacticsDocument';
import {
  actComplete, assignedCount, stageComplete, unassignedSlots, type WizardState,
} from './wizardState';
import { ZONE_OPTIONS } from './places';

/**
 * The wizard's eight steps. Each is a pure view over the wizard's working
 * tactic: `change` mutates a clone, the map tasks come from the page, and
 * everything a player answers here reopens exactly the same way later.
 */

export interface StepProps {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  wizard: WizardState;
  task: MapTask | null;
  change: (apply: (tactic: Tactic) => void) => void;
  arm: (slot: number | null) => void;
  startPhaseTask: (groupId: string, phase: 'stage' | 'act') => void;
  startSigTask: (groupId: string, phase: 'stage' | 'act', classId: string) => void;
  startWatchTask: (which: 'start' | 'release') => void;
  endTask: () => void;
}

export function StartStep({ wizard, change, task, startWatchTask, endTask }: StepProps) {
  const tactic = wizard.tactic;
  const modes = [
    { id: 'always' as const, label: 'Always', blurb: 'Applies whenever nothing above it in the Sheet does.' },
    { id: 'when' as const, label: 'Only when…', blurb: 'Gated behind conditions on top of Sheet order.' },
  ];
  return <>
    <p className="t-body">First things first: <b>when does this Tactic apply at all?</b>{' '}
      The Sheet still reads top to bottom — this only gates it further.</p>
    <SegChips options={modes} value={tactic.start.mode}
      onChange={(mode) => change((next) => {
        next.start = mode === 'when'
          ? { mode, conds: next.start.conds ?? {} }
          : { mode: 'always' };
      })} />
    <p className="t-micro text-arena-dim">{modes.find((mode) => mode.id === tactic.start.mode)?.blurb}</p>
    {tactic.start.mode === 'when' && (
      <ConditionRows
        conds={tactic.start.conds ?? {}}
        kinds={['enemiesAt', 'coreLoose', 'carrier', 'losses']}
        accent="#e6ddd4"
        watching={task?.kind === 'watch' && task.which === 'start'}
        onChange={(conds) => change((next) => { next.start.conds = conds; })}
        onDrawWatch={() => startWatchTask('start')}
        onStopWatch={endTask}
      />
    )}
    <Readback label="Readback">
      {startClause(tactic) ?? 'Always available — the Sheet order alone decides when it runs.'}
    </Readback>
  </>;
}

export function GroupsStep({ catalog, document, wizard, change, arm }: StepProps) {
  const tactic = wizard.tactic;
  const unassigned = unassignedSlots(tactic, document);
  const assigned = assignedCount(tactic);
  const squadReady = document.squad.length === 8;
  const place = (slot: number, groupId: string | null) => change((next) => {
    for (const group of next.groups) {
      group.slots = group.slots.filter((value) => value !== slot);
    }
    if (groupId) next.groups.find((group) => group.id === groupId)?.slots.push(slot);
  });
  return <>
    <p className="t-body">Split the whole team into up to three temporary Groups. They live only
      inside this Tactic — every bot belongs to exactly one.</p>
    <div className="flex items-center gap-2">
      <span className="lab">Assigned</span>
      <div className="h-1.5 grow overflow-hidden rounded bg-arena-deep">
        <div className="h-full bg-arena-material transition-all" style={{ width: `${(assigned / 8) * 100}%` }} />
      </div>
      <span className="val">{assigned}/8</span>
    </div>
    <div>
      <div className="lab mb-1">Unassigned</div>
      <div className="flex min-h-9 flex-wrap gap-1 rounded-sm border border-dashed border-arena-edge p-1">
        {unassigned.length === 0 && (
          <span className="t-micro self-center text-arena-dim">
            {squadReady ? 'Everyone is assigned.' : 'Fill the squad on the left to get all eight in the tray.'}
          </span>
        )}
        {unassigned.map((slot) => (
          <BotChip key={slot} bot={document.squad[slot]} catalog={catalog}
            armed={wizard.armedSlot === slot}
            onClick={() => arm(wizard.armedSlot === slot ? null : slot)} />
        ))}
      </div>
    </div>
    {tactic.groups.map((group) => (
      <div key={group.id}
        className={`rounded-sm border p-2 ${wizard.armedSlot !== null ? 'cursor-pointer border-arena-material/60' : 'border-arena-edge'}`}
        onClick={() => {
          if (wizard.armedSlot === null) return;
          place(wizard.armedSlot, group.id);
          arm(null);
        }}
        role="presentation"
      >
        <div className="mb-1 flex items-center gap-2">
          <span className="h-2.5 w-2.5 rounded-full" style={{ background: groupColor(tactic, group.id) }} />
          <input
            className="field !min-h-7 grow !py-0"
            value={group.name}
            aria-label="Group name"
            onClick={(event) => event.stopPropagation()}
            onChange={(event) => change((next) => {
              const target = next.groups.find((value) => value.id === group.id);
              if (target) target.name = event.target.value;
            })}
          />
          <span className="val">{group.slots.length} bots</span>
          {tactic.groups.length > 1 && (
            <button type="button" className="btn" title="Dissolve group"
              onClick={(event) => {
                event.stopPropagation();
                change((next) => {
                  next.groups = next.groups.filter((value) => value.id !== group.id);
                });
              }}>✕</button>
          )}
        </div>
        <div className="flex min-h-8 flex-wrap gap-1">
          {group.slots.map((slot) => (
            <BotChip key={slot} bot={document.squad[slot]} catalog={catalog}
              onClick={() => { place(slot, null); arm(null); }} />
          ))}
          {group.slots.length === 0 && (
            <span className="t-micro self-center text-arena-dim">
              {wizard.armedSlot !== null
                ? 'Click to place the armed bot here.'
                : 'Empty — arm a bot above, then click this box.'}
            </span>
          )}
        </div>
      </div>
    ))}
    <div className="flex items-center gap-2">
      {tactic.groups.length < 3 && (
        <button type="button" className="btn"
          onClick={() => change((next) => { next.groups.push(freshGroup(next.groups)); })}>
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
    <p className="t-micro text-arena-dim">Click a bot to arm it, then click its Group.
      Clicking an assigned bot returns it to the tray.</p>
  </>;
}

export function PhaseStep(props: StepProps & { phase: 'stage' | 'act' }) {
  const { wizard, phase } = props;
  const isStage = phase === 'stage';
  return <>
    <p className="t-body">{isStage
      ? <><b>Every Group stages its own way.</b> Where does each one set up, and how does it behave while it waits?</>
      : <><b>Each Group can carry its own second plan</b> for after release. A Group with no Act keeps doing its Stage job.</>}</p>
    {wizard.tactic.groups.map((group) => (
      <PhaseSection key={group.id} {...props} group={group} />
    ))}
  </>;
}

function PhaseSection(props: StepProps & { phase: 'stage' | 'act'; group: TacticGroup }) {
  const {
    catalog, document, wizard, phase, group, task, change,
    startPhaseTask, startSigTask, endTask,
  } = props;
  const tactic = wizard.tactic;
  const isStage = phase === 'stage';
  const geo = isStage ? group.stage : group.act;
  const color = groupColor(tactic, group.id);
  const kinds = isStage
    ? STAGE_KINDS.filter((kind) => kind.id !== 'none'
      || group.stage.kind === 'none' || group.act.kind !== 'none')
    : ACT_KINDS.filter((kind) => kind.id !== 'escort'
      // Escort only makes sense when someone can end up carrying — keyed off
      // content (a delivery plan or a Core habit), never off the recipe.
      || group.act.kind === 'escort' || tactic.delivery.plan !== 'none'
      || tactic.groups.some((value) => value.coreRule !== 'ignore'));
  const kindDef = (isStage ? STAGE_KINDS : ACT_KINDS).find((kind) => kind.id === geo.kind);
  const complete = isStage ? stageComplete(tactic, group.id) : actComplete(tactic, group.id);
  const drawing = task?.kind === 'phase' && task.groupId === group.id && task.phase === phase;
  const postures = posturesFor(tactic, group, phase);
  const setKind = (kind: StageKind | ActKind) => {
    change((next) => {
      const target = next.groups.find((value) => value.id === group.id);
      if (!target) return;
      const targetGeo = phase === 'stage' ? target.stage : target.act;
      if (targetGeo.kind !== kind) {
        targetGeo.kind = kind;
        delete targetGeo.posts;
        delete targetGeo.rect;
        delete targetGeo.path;
        delete targetGeo.point;
      }
    });
    const def = (isStage ? STAGE_KINDS : ACT_KINDS).find((value) => value.id === kind);
    if (def?.draw) startPhaseTask(group.id, phase);
    else endTask();
  };
  const spatialClasses = [...new Set(group.slots
    .map((slot) => document.squad[slot]?.class)
    .filter((classId): classId is string => Boolean(classId)))]
    .filter((classId) => signatureMarkGeometry(catalog, classId));
  return (
    <div className="rounded-sm border border-arena-edge p-2" id={`section-${group.id}-${phase}`}
      style={{ borderLeft: `3px solid ${color}` }}>
      <div className="mb-1 flex items-center gap-2">
        <span className="h-2.5 w-2.5 rounded-full" style={{ background: color }} />
        <b className="t-body">{group.name}</b>
        <span className="val">{group.slots.length} bot{group.slots.length === 1 ? '' : 's'}</span>
      </div>
      <div className="lab">{isStage ? 'Setup' : 'On release'}</div>
      <SegChips options={kinds} value={geo.kind} accent={color}
        onChange={(kind) => setKind(kind as StageKind & ActKind)} />
      <p className="t-micro mb-1 text-arena-dim">{kindDef?.blurb}</p>
      {kindDef?.draw
        ? <GeoStatusRow complete={complete} accent={color}
          label={describeGeometry(geo, group)}
          drawing={drawing}
          onDraw={() => startPhaseTask(group.id, phase)}
          onStop={endTask}
          onClear={() => change((next) => {
            const target = next.groups.find((value) => value.id === group.id);
            const targetGeo = target && (phase === 'stage' ? target.stage : target.act);
            if (!targetGeo) return;
            delete targetGeo.posts;
            delete targetGeo.rect;
            delete targetGeo.path;
            delete targetGeo.point;
          })} />
        : <GeoStatusRow complete label={geo.kind === 'return'
          ? 'Route home is worked out automatically.'
          : geo.kind === 'escort'
            ? 'They shadow the carrier — no drawing needed.'
            : !isStage && geo.kind === 'none'
              ? `${group.name} keeps its Stage behavior — staged Groups don’t wander off.`
              : 'Nothing to draw for this choice.'} drawing={false} />}
      <div className="lab mt-2">Posture</div>
      <SegChips options={postures} value={geo.posture} accent={color}
        onChange={(posture) => change((next) => {
          const target = next.groups.find((value) => value.id === group.id);
          if (target) (phase === 'stage' ? target.stage : target.act).posture = posture as Posture;
        })} />
      <p className="t-micro mb-1 text-arena-dim">{POSTURES.find((value) => value.id === geo.posture)?.blurb}</p>
      <div className="lab">Recovery</div>
      <SegChips options={RECOVERIES} value={geo.recovery} accent={color}
        onChange={(recovery) => change((next) => {
          const target = next.groups.find((value) => value.id === group.id);
          if (target) (phase === 'stage' ? target.stage : target.act).recovery = recovery as Recovery;
        })} />
      <p className="t-micro text-arena-dim">{RECOVERIES.find((value) => value.id === geo.recovery)?.blurb}</p>
      {spatialClasses.length > 0 && <>
        <div className="lab mt-2">Signatures — {isStage ? 'set while staging' : 'used after release'}</div>
        {spatialClasses.map((classId) => {
          const geometry = signatureMarkGeometry(catalog, classId)!;
          const mark = group.sigs?.[phase]?.[classId];
          const has = Boolean(mark && ('points' in mark ? mark.points.length : mark.line.length));
          const mine = task?.kind === 'sig' && task.groupId === group.id
            && task.phase === phase && task.classId === classId;
          const entry = catalog.classes.find((value) => value.id === classId);
          return <GeoStatusRow key={classId} complete={has} accent={color}
            label={`${entry?.name ?? classId} — ${geometry.label}${has ? '' : ' · no marks yet — optional'}`}
            drawLabel="Place marks"
            drawing={mine}
            onDraw={() => startSigTask(group.id, phase, classId)}
            onStop={endTask}
            onClear={has ? () => change((next) => {
              const target = next.groups.find((value) => value.id === group.id);
              if (target?.sigs?.[phase]) delete target.sigs[phase]![classId];
            }) : undefined} />;
        })}
      </>}
    </div>
  );
}

function posturesFor(tactic: Tactic, group: TacticGroup, phase: 'stage' | 'act') {
  const coreish = tactic.delivery.plan !== 'none'
    || tactic.groups.some((value) => value.coreRule === 'hunt')
    || group.act.kind === 'return';
  const ids: Posture[] = phase === 'stage'
    ? ['wait', 'hold', 'observe', ...(coreish ? ['steal', 'escort'] as Posture[] : [])]
    : ['aggressive', 'hold', 'observe', ...(coreish ? ['steal', 'escort'] as Posture[] : [])];
  const current = phase === 'stage' ? group.stage.posture : group.act.posture;
  if (!ids.includes(current)) ids.push(current);
  return POSTURES.filter((posture) => ids.includes(posture.id));
}

export function ReleaseStep({ wizard, change, task, startWatchTask, endTask }: StepProps) {
  const tactic = wizard.tactic;
  const modes = [
    { id: 'immediate' as const, label: 'Immediately', blurb: 'They go as soon as this Tactic starts.' },
    { id: 'ready' as const, label: 'When every Group is ready', blurb: 'Staged, in position, then go together.' },
    { id: 'ready-when' as const, label: 'Ready, plus a trigger', blurb: 'Hold the leash until the moment is right — one ticked condition lets go.' },
  ];
  return <>
    <p className="t-body">Between Stage and Act sits one shared moment: <b>when do they go?</b>{' '}
      Release ends every Group's Stage at the same time.</p>
    <SegChips options={modes} value={tactic.release.mode}
      onChange={(mode) => change((next) => {
        next.release = mode === 'ready-when'
          ? { mode, conds: next.release.conds ?? {} }
          : { mode };
      })} />
    <p className="t-micro text-arena-dim">{modes.find((mode) => mode.id === tactic.release.mode)?.blurb}</p>
    {tactic.release.mode === 'ready-when' && (
      <ConditionRows
        conds={tactic.release.conds ?? {}}
        kinds={['enemiesAt', 'contact', 'coreLoose']}
        single
        accent="#c8763c"
        watching={task?.kind === 'watch' && task.which === 'release'}
        onChange={(conds) => change((next) => { next.release.conds = conds; })}
        onDrawWatch={() => startWatchTask('release')}
        onStopWatch={endTask}
      />
    )}
    <Readback label="Readback">{releaseClause(tactic)}.</Readback>
    <p className="t-micro text-arena-dim">Groups with no Act just keep holding after release — nobody wanders off.</p>
  </>;
}

export function CoresStep({ catalog, document, wizard, change }: StepProps) {
  const tactic = wizard.tactic;
  const preferred = tactic.delivery.preferredSlots ?? [];
  return <>
    <p className="t-body"><b>Cores.</b> Pickup is each Group's own habit, and ⭑ marks preferred
      carriers. Delivery is one shared plan — the real carrier is whoever ends up holding it.</p>
    {tactic.groups.map((group) => (
      <div key={group.id} className="rounded-sm border border-arena-edge p-2"
        style={{ borderLeft: `3px solid ${groupColor(tactic, group.id)}` }}>
        <div className="mb-1 flex items-center gap-2">
          <b className="t-body">{group.name}</b>
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
  </>;
}

export function FinishStep({ document, wizard, change }: StepProps) {
  const tactic = wizard.tactic;
  const outcomes = tactic.finish.outcomes;
  const row = (
    key: keyof typeof outcomes,
    label: string,
    on: boolean,
    toggle: () => void,
    extra?: React.ReactNode,
  ) => (
    <button key={key} type="button" onClick={toggle}
      className={`flex min-h-9 w-full items-center gap-2 rounded-sm border px-2 py-1 text-left transition-colors ${on
        ? 'border-arena-text/40 bg-arena-deep/50 text-arena-text'
        : 'border-arena-edge text-arena-dim hover:text-arena-text'}`}>
      <span className={on ? 'font-bold' : 'opacity-30'}>✓</span>
      <span className="t-micro grow">{label}</span>
      {on && extra}
    </button>
  );
  const stepper = (value: number, apply: (value: number) => void) => (
    <span className="flex items-center gap-1 rounded-sm border border-arena-edge"
      onClick={(event) => event.stopPropagation()} role="presentation">
      <button type="button" className="min-h-7 px-2" onClick={() => apply(Math.max(1, value - 1))}>−</button>
      <span className="tabular t-micro w-4 text-center">{value}</span>
      <button type="button" className="min-h-7 px-2" onClick={() => apply(Math.min(8, value + 1))}>＋</button>
    </span>
  );
  const zoneSelect = (zone: ZoneRef, apply: (zone: ZoneRef) => void) => (
    <select className="field !min-h-7 !w-auto !py-0 t-micro"
      value={ZONE_OPTIONS.find((option) =>
        option.rect.every((value, index) => value === zone.rect[index]))?.id ?? 'custom'}
      onClick={(event) => event.stopPropagation()}
      onChange={(event) => {
        const option = ZONE_OPTIONS.find((value) => value.id === event.target.value);
        if (option) apply({ name: option.id, rect: option.rect });
      }}>
      {!ZONE_OPTIONS.some((option) => option.rect.every((value, index) => value === zone.rect[index]))
        && <option value="custom">the drawn box</option>}
      {ZONE_OPTIONS.map((option) => <option key={option.id} value={option.id}>{option.label}</option>)}
    </select>
  );
  return <>
    <p className="t-body">And last: <b>when is this Tactic finished?</b>{' '}
      Any selected outcome ends it, and the Sheet moves on.</p>
    <PhaseStrip tactic={tactic} big />
    <p className="t-micro text-arena-dim">Outcomes start counting once the Tactic releases —
      a holding Group can’t finish it while the others are still staging.</p>
    <div className="flex flex-col gap-1.5">
      {row('kills', `${outcomes.kills?.count ?? 3} enemies destroyed`, Boolean(outcomes.kills),
        () => change((next) => {
          if (next.finish.outcomes.kills) delete next.finish.outcomes.kills;
          else next.finish.outcomes.kills = { count: 3 };
        }),
        outcomes.kills && stepper(outcomes.kills.count, (value) =>
          change((next) => { next.finish.outcomes.kills = { count: value }; })))}
      {row('losses', `We have lost ${outcomes.losses?.count ?? 2} bots`, Boolean(outcomes.losses),
        () => change((next) => {
          if (next.finish.outcomes.losses) delete next.finish.outcomes.losses;
          else next.finish.outcomes.losses = { count: 2 };
        }),
        outcomes.losses && stepper(outcomes.losses.count, (value) =>
          change((next) => { next.finish.outcomes.losses = { count: value }; })))}
      {row('cleared', 'An area is cleared', Boolean(outcomes.cleared),
        () => change((next) => {
          if (next.finish.outcomes.cleared) delete next.finish.outcomes.cleared;
          else next.finish.outcomes.cleared = { zone: { name: 'mid', rect: ZONE_OPTIONS[0].rect } };
        }),
        outcomes.cleared && zoneSelect(outcomes.cleared.zone, (zone) =>
          change((next) => { next.finish.outcomes.cleared = { zone }; })))}
      {row('coreAcquired', 'A Core is acquired', Boolean(outcomes.coreAcquired),
        () => change((next) => {
          if (next.finish.outcomes.coreAcquired) delete next.finish.outcomes.coreAcquired;
          else next.finish.outcomes.coreAcquired = true;
        }))}
      {row('coreBanked', 'A Core is banked', Boolean(outcomes.coreBanked),
        () => change((next) => {
          if (next.finish.outcomes.coreBanked) delete next.finish.outcomes.coreBanked;
          else next.finish.outcomes.coreBanked = true;
        }))}
      {row('coreLost', 'The Core is lost', Boolean(outcomes.coreLost),
        () => change((next) => {
          if (next.finish.outcomes.coreLost) delete next.finish.outcomes.coreLost;
          else next.finish.outcomes.coreLost = true;
        }))}
      {row('reached', 'The destination is reached', Boolean(outcomes.reached),
        () => change((next) => {
          if (next.finish.outcomes.reached) delete next.finish.outcomes.reached;
          else next.finish.outcomes.reached = true;
        }))}
      {row('observed', 'The ground has been watched long enough', Boolean(outcomes.observed),
        () => change((next) => {
          if (next.finish.outcomes.observed) delete next.finish.outcomes.observed;
          else next.finish.outcomes.observed = true;
        }))}
      {row('persistent', 'Persistent — runs until a higher Tactic takes over', Boolean(outcomes.persistent),
        () => change((next) => {
          if (next.finish.outcomes.persistent) delete next.finish.outcomes.persistent;
          else next.finish.outcomes.persistent = true;
        }))}
    </div>
    <div>
      <div className="lab mb-1">Maximum attempt</div>
      <ChoiceCards options={MAX_ATTEMPTS} value={tactic.finish.maxAttempt} columns={2}
        onChange={(maxAttempt) => change((next) => { next.finish.maxAttempt = maxAttempt; })} />
    </div>
    <DeliverCoreSuggestion document={document} tactic={tactic} change={change} />
  </>;
}

export function ReviewStep({ catalog, document, wizard, change }: StepProps) {
  const tactic = wizard.tactic;
  const botName = (slot: number) => document.squad[slot]?.name ?? null;
  return <>
    <p className="t-body">Read it back. If the sentence says what you meant, it’s ready.</p>
    <div className="panel-feature pad">
      <div className="lab mb-1.5">The whole operation</div>
      <p className="t-body leading-relaxed">{summarize(tactic, botName)}</p>
      <PhaseStrip tactic={tactic} big />
    </div>
    {tactic.groups.map((group) => (
      <div key={group.id} className="panel pad"
        style={{ borderLeft: `3px solid ${groupColor(tactic, group.id)}` }}>
        <div className="mb-1 flex items-center gap-2">
          <b className="t-body">{group.name}</b>
          <span className="val">{group.slots.length} bot{group.slots.length === 1 ? '' : 's'}</span>
          <span className="grow" />
          <span className="pill">
            {POSTURES.find((value) => value.id === group.stage.posture)?.label}
            {group.act.kind !== 'none' && ` → ${POSTURES.find((value) => value.id === group.act.posture)?.label}`}
          </span>
        </div>
        <p className="t-meta">{groupClause(group, true)}.</p>
        <div className="mt-1.5 flex flex-wrap gap-1">
          {group.slots.map((slot) => document.squad[slot] && (
            <BotChip key={slot} bot={document.squad[slot]} catalog={catalog} />
          ))}
        </div>
      </div>
    ))}
    <DeliverCoreSuggestion document={document} tactic={tactic} change={change} />
    <p className="t-micro text-arena-dim">The overlays stay live after creating — drag posts, corners,
      waypoints and destinations on the map any time this Tactic is open.</p>
  </>;
}

/** Content-keyed nudge: an aggressive Tactic that can finish holding a Core,
 * with no plan for the run home, gets offered one. */
function DeliverCoreSuggestion({ document, tactic, change }: {
  document: TacticsDocument;
  tactic: Tactic;
  change: (apply: (tactic: Tactic) => void) => void;
}) {
  const wantsCore = Boolean(tactic.finish.outcomes.coreAcquired)
    && tactic.delivery.plan === 'none';
  const hasDeliver = document.tactics.some((value) =>
    value.id !== tactic.id && value.start.conds?.carrier
    && value.groups.some((group) => group.act.kind === 'return'));
  if (!wantsCore || hasDeliver) return null;
  return (
    <div className="panel-quiet pad">
      <div className="t-body mb-1 font-semibold">Your tactic may finish while carrying a Core.</div>
      <p className="t-micro text-arena-dim">Plan the run home inside this Tactic — or add a
        separate Deliver Core tactic from the recipe chooser afterwards.</p>
      <button type="button" className="btn btn-secondary mt-2"
        onClick={() => change((next) => { next.delivery.plan = 'escort'; })}>
        Plan the delivery in this Tactic
      </button>
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

export { actClause };
