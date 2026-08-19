import { useState } from 'react';
import { CountStepper, TickRow, ZoneSelect } from './conditionControls';
import { WELL_OPTIONS, ZONE_OPTIONS } from './places';
import type {
  CondKind, TacticCase, TacticConds, WellChoice,
} from './tacticsDocument';

/**
 * THE condition editor: one tick row per available condition, parameters
 * inline on ticked rows. Inside "Required" every ticked row must hold;
 * inside "Triggered by" any ticked row fires — and CaseListEditor stacks
 * whole alternatives of the two. No boolean words anywhere. The less-common
 * rows sit behind a "More conditions" fold so the everyday set stays
 * uncluttered; any active folded row keeps the fold open.
 */

export type ConditionRowKind = CondKind;

const ROW_LABELS: Partial<Record<CondKind, string>> = {
  groupsSet: 'Every Group is in position (the dead excuse themselves)',
  held: 'We have been at this phase a while',
  contact: 'Any enemy contact',
  carrier: 'We hold a Core — a friendly carrier exists',
  coreAcquired: 'A Core is acquired',
  coreBanked: 'A Core is banked',
  coreLost: 'The Core is lost',
  reached: 'The destination is reached',
  observed: 'The ground has been watched long enough',
};

export function ConditionRows({
  conds, kinds, moreKinds, accent, watchSymbol = '⚡', watchAdjective,
  watchingRow, onChange, onDrawWatch, onStopWatch,
}: {
  conds: TacticConds;
  kinds: ConditionRowKind[];
  moreKinds?: ConditionRowKind[];
  accent?: string;
  /** The watch chrome's glyph: ▶ for a start watch, ⚡ for a phase watch. */
  watchSymbol?: string;
  /** Names the box's colour in the hint ("pale", "orange"). */
  watchAdjective?: string;
  /** Which drawable row of THIS rows object is being drawn right now. */
  watchingRow?: 'enemiesAt' | 'cleared' | null;
  onChange: (conds: TacticConds) => void;
  onDrawWatch?: (row: 'enemiesAt' | 'cleared') => void;
  onStopWatch?: () => void;
}) {
  const set = (change: (next: TacticConds) => void) => {
    const next = structuredClone(conds);
    change(next);
    onChange(next);
  };

  const [moreOpen, setMoreOpen] = useState(false);
  const moreActive = (moreKinds ?? []).some((kind) => Boolean(conds[kind]));

  const row = (kind: ConditionRowKind, label: string, extra?: React.ReactNode, on?: boolean) => (
    <TickRow key={kind} on={Boolean(on)} label={label} extra={extra}
      onToggle={() => {
        set((next) => {
          if (on) delete next[kind];
          else if (kind === 'groupsSet') next.groupsSet = true;
          else if (kind === 'held') next.held = { ticks: 40 };
          else if (kind === 'enemiesAt') next.enemiesAt = { count: 5, zone: { name: 'mid', rect: ZONE_OPTIONS[0].rect } };
          else if (kind === 'contact') next.contact = true;
          else if (kind === 'coreLoose') next.coreLoose = { well: 'centre' };
          else if (kind === 'coreSoon') next.coreSoon = { well: 'centre', ticks: 20 };
          else if (kind === 'enemiesDown') next.enemiesDown = { count: 3 };
          else if (kind === 'carrier') next.carrier = true;
          else if (kind === 'losses') next.losses = { count: 2 };
          else if (kind === 'lossesWithin') next.lossesWithin = { count: 4, ticks: 30 };
          else if (kind === 'kills') next.kills = { count: 3 };
          else if (kind === 'cleared') next.cleared = { zone: { name: 'mid', rect: ZONE_OPTIONS[0].rect } };
          else if (kind === 'coreAcquired') next.coreAcquired = true;
          else if (kind === 'coreBanked') next.coreBanked = true;
          else if (kind === 'coreLost') next.coreLost = true;
          else if (kind === 'reached') next.reached = true;
          else if (kind === 'observed') next.observed = true;
        });
      }} />
  );

  const drawChrome = (which: 'enemiesAt' | 'cleared', key: string, text: string) => (
    <div key={key}
      className="flex min-h-8 items-center gap-2 rounded-sm border border-arena-edge px-2 t-micro text-arena-dim"
      style={accent ? { borderColor: `${accent}55` } : undefined}>
      <span style={{ color: accent ?? 'var(--color-arena-material)' }}>{watchSymbol}</span>
      <span>{text}</span>
      <span className="grow" />
      {watchingRow === which
        ? <button type="button" className="btn btn-on" onClick={onStopWatch}>Stop drawing</button>
        : onDrawWatch && <button type="button" className="btn btn-secondary"
          onClick={() => onDrawWatch(which)}>Draw it instead</button>}
    </div>
  );

  const rows: React.ReactNode[] = [];
  const renderKind = (kind: ConditionRowKind) => {
    const flagLabel = ROW_LABELS[kind];
    if (kind === 'enemiesAt') {
      const on = Boolean(conds.enemiesAt);
      rows.push(row('enemiesAt', 'Enemies gather somewhere', on && conds.enemiesAt && <>
        <CountStepper value={conds.enemiesAt.count}
          onChange={(value) => set((next) => { next.enemiesAt!.count = value; })} />
        <ZoneSelect zone={conds.enemiesAt.zone}
          onChange={(zone) => set((next) => { next.enemiesAt!.zone = zone; })} />
      </>, on));
      if (on) {
        rows.push(drawChrome('enemiesAt', 'enemiesAt-draw',
          `The ${watchAdjective ? `${watchAdjective} box` : 'box'} on the map is the watched ground. Drag its corners any time.`));
      }
      return;
    }
    if (kind === 'cleared') {
      const on = Boolean(conds.cleared);
      rows.push(row('cleared', 'An area is cleared of enemies', on && conds.cleared && (
        <ZoneSelect zone={conds.cleared.zone}
          onChange={(zone) => set((next) => { next.cleared = { zone }; })} />
      ), on));
      if (on) {
        rows.push(drawChrome('cleared', 'cleared-draw',
          'The teal box on the map must be clear of enemies.'));
      }
      return;
    }
    if (kind === 'held') {
      const on = Boolean(conds.held);
      rows.push(row('held', ROW_LABELS.held!, on && conds.held && <>
        <CountStepper value={conds.held.ticks} min={10} max={600} step={10}
          onChange={(value) => set((next) => { next.held!.ticks = value; })} />
        <span className="t-micro text-arena-dim">ticks</span>
      </>, on));
      return;
    }
    if (kind === 'coreLoose') {
      const on = Boolean(conds.coreLoose);
      rows.push(row('coreLoose', 'A Core is up for grabs', on && conds.coreLoose && (
        <select
          className="field !min-h-7 !w-auto !py-0 t-micro"
          value={conds.coreLoose.well}
          onClick={(event) => event.stopPropagation()}
          onChange={(event) => set((next) => {
            next.coreLoose!.well = event.target.value as WellChoice;
          })}
        >
          {WELL_OPTIONS.map((well) => <option key={well.id} value={well.id}>{well.label}</option>)}
        </select>
      ), on));
      return;
    }
    if (kind === 'coreSoon') {
      const on = Boolean(conds.coreSoon);
      rows.push(row('coreSoon', 'A Core births soon', on && conds.coreSoon && <>
        <select
          className="field !min-h-7 !w-auto !py-0 t-micro"
          value={conds.coreSoon.well}
          onClick={(event) => event.stopPropagation()}
          onChange={(event) => set((next) => {
            next.coreSoon!.well = event.target.value as WellChoice;
          })}
        >
          {WELL_OPTIONS.map((well) => <option key={well.id} value={well.id}>{well.label}</option>)}
        </select>
        <CountStepper value={conds.coreSoon.ticks} min={5} max={120} step={5}
          onChange={(value) => set((next) => { next.coreSoon!.ticks = value; })} />
        <span className="t-micro text-arena-dim">ticks</span>
      </>, on));
      return;
    }
    if (kind === 'enemiesDown') {
      const on = Boolean(conds.enemiesDown);
      rows.push(row('enemiesDown', 'Enemies are down', on && conds.enemiesDown
        && <CountStepper value={conds.enemiesDown.count}
          onChange={(value) => set((next) => { next.enemiesDown!.count = value; })} />, on));
      return;
    }
    if (kind === 'losses') {
      const on = Boolean(conds.losses);
      rows.push(row('losses', 'We are down bots', on && conds.losses
        && <CountStepper value={conds.losses.count} max={8}
          onChange={(value) => set((next) => { next.losses!.count = value; })} />, on));
      return;
    }
    if (kind === 'lossesWithin') {
      const on = Boolean(conds.lossesWithin);
      rows.push(row('lossesWithin', 'We lost bots in a short window', on && conds.lossesWithin && <>
        <CountStepper value={conds.lossesWithin.count} max={8}
          onChange={(value) => set((next) => { next.lossesWithin!.count = value; })} />
        <span className="t-micro text-arena-dim">within</span>
        <CountStepper value={conds.lossesWithin.ticks} min={10} max={600} step={10}
          onChange={(value) => set((next) => { next.lossesWithin!.ticks = value; })} />
        <span className="t-micro text-arena-dim">ticks</span>
      </>, on));
      return;
    }
    if (kind === 'kills') {
      const on = Boolean(conds.kills);
      rows.push(row('kills', 'Enemies destroyed this phase', on && conds.kills
        && <CountStepper value={conds.kills.count}
          onChange={(value) => set((next) => { next.kills!.count = value; })} />, on));
      return;
    }
    if (flagLabel) {
      rows.push(row(kind, flagLabel, null, Boolean(conds[kind])));
    }
  };
  for (const kind of kinds) renderKind(kind);
  if (moreKinds && moreKinds.length > 0) {
    if (moreOpen || moreActive) {
      for (const kind of moreKinds) renderKind(kind);
    }
    if (!moreActive) {
      rows.push(
        <button key="more" type="button"
          className="min-h-7 self-start px-1 t-micro text-arena-dim underline decoration-arena-edge2 underline-offset-2 transition-colors hover:text-arena-text"
          onClick={() => setMoreOpen((open) => !open)}>
          {moreOpen ? 'Fewer conditions' : `More conditions (${moreKinds.length})…`}
        </button>,
      );
    }
  }
  return <div className="flex flex-col gap-1.5">{rows}</div>;
}

/**
 * A stack of alternatives: each case is "Required — all must hold" plus
 * "Triggered by — any may fire"; any case being true is enough. The caller
 * may append a footer per case (the transition's destination picker).
 */
export function CaseListEditor<T extends TacticCase>({
  cases, maximum = 3, accent, watchSymbol, watchAdjective,
  requiredKinds, moreRequiredKinds, triggerKinds, moreTriggerKinds,
  makeCase, onChange,
  caseFooter, watching, onDrawWatch, onStopWatch,
}: {
  cases: T[];
  maximum?: number;
  accent?: string;
  watchSymbol?: string;
  watchAdjective?: string;
  requiredKinds: ConditionRowKind[];
  /** Folded extras for Required — any row may be ANDed there, so “A and B
   * both fire it” is Required with two rows and no Triggered by. */
  moreRequiredKinds?: ConditionRowKind[];
  triggerKinds: ConditionRowKind[];
  moreTriggerKinds?: ConditionRowKind[];
  makeCase: () => T;
  onChange: (cases: T[]) => void;
  caseFooter?: (value: T, update: (apply: (next: T) => void) => void, index: number) => React.ReactNode;
  /** Which case/side/row is being drawn on the map, if any. */
  watching?: { caseIndex: number; side: 'required' | 'triggers'; row: 'enemiesAt' | 'cleared' } | null;
  onDrawWatch?: (caseIndex: number, side: 'required' | 'triggers', row: 'enemiesAt' | 'cleared') => void;
  onStopWatch?: () => void;
}) {
  const update = (index: number, apply: (next: T) => void) => {
    const next = structuredClone(cases);
    apply(next[index]);
    onChange(next);
  };
  return (
    <div className="flex flex-col gap-2">
      {cases.map((value, index) => (
        <div key={index} className="rounded-sm border border-arena-edge p-2"
          style={accent ? { borderLeft: `3px solid ${accent}` } : undefined}>
          {index > 0 && (
            <div className="lab mb-1" style={accent ? { color: accent } : undefined}>
              Or alternatively
            </div>
          )}
          {(requiredKinds.length > 0 || (moreRequiredKinds?.length ?? 0) > 0) && <>
            <div className="lab mb-1">Required — all must hold</div>
            <ConditionRows
              conds={value.required ?? {}}
              kinds={requiredKinds}
              moreKinds={moreRequiredKinds}
              accent={accent}
              watchSymbol={watchSymbol}
              watchAdjective={watchAdjective}
              watchingRow={watching && watching.caseIndex === index
                && watching.side === 'required' ? watching.row : null}
              onChange={(conds) => update(index, (next) => {
                if (Object.keys(conds).length === 0) delete next.required;
                else next.required = conds;
              })}
              onDrawWatch={onDrawWatch
                ? (row) => onDrawWatch(index, 'required', row) : undefined}
              onStopWatch={onStopWatch}
            />
          </>}
          <div className="lab mb-1 mt-2">Triggered by — any may fire</div>
          <ConditionRows
            conds={value.triggers ?? {}}
            kinds={triggerKinds}
            moreKinds={moreTriggerKinds}
            accent={accent}
            watchSymbol={watchSymbol}
            watchAdjective={watchAdjective}
            watchingRow={watching && watching.caseIndex === index
              && watching.side === 'triggers' ? watching.row : null}
            onChange={(conds) => update(index, (next) => {
              if (Object.keys(conds).length === 0) delete next.triggers;
              else next.triggers = conds;
            })}
            onDrawWatch={onDrawWatch
              ? (row) => onDrawWatch(index, 'triggers', row) : undefined}
            onStopWatch={onStopWatch}
          />
          <div className="mt-2 flex items-center gap-2">
            {caseFooter?.(value, (apply) => update(index, apply), index)}
            <span className="grow" />
            {cases.length > 1 && (
              <button type="button" className="btn"
                onClick={() => onChange(cases.filter((_, i) => i !== index))}>
                Remove alternative
              </button>
            )}
          </div>
        </div>
      ))}
      {cases.length < maximum && (
        <button type="button" className="btn self-start"
          onClick={() => onChange([...cases, makeCase()])}>
          ＋ Add alternative
        </button>
      )}
    </div>
  );
}
