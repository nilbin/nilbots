import { useState } from 'react';
import { CountStepper, TickRow, ZoneSelect } from './conditionControls';
import { caseClause } from './summary';
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
  coreAcquired: 'A Core is taken this phase',
  coreBanked: 'A Core is banked',
  coreLost: 'The Core is lost',
  reached: 'The destination is reached',
  observed: 'The ground has been watched long enough',
};

/** Presentation grouping: every side shows its rows bucketed under these
 * headings, in this order, so a player scans for "the Core rows" instead
 * of a flat list (the old "More conditions" fold is gone). */
const ROW_GROUPS: { label: string; kinds: CondKind[] }[] = [
  { label: 'Our team', kinds: ['groupsSet', 'healed', 'losses', 'lossesWithin'] },
  { label: 'Enemies', kinds: ['contact', 'enemiesAt', 'enemiesDown', 'cleared', 'kills', 'killsWithin'] },
  { label: 'Cores', kinds: ['carrier', 'coreLoose', 'coreSoon', 'coreAcquired', 'coreBanked', 'coreLost'] },
  { label: 'Time & ground', kinds: ['held', 'reached', 'observed'] },
];

export function ConditionRows({
  conds, kinds, accent, watchSymbol = '⚡', watchAdjective,
  watchingRow, onChange, onDrawWatch, onStopWatch, onBranch,
}: {
  conds: TacticConds;
  kinds: ConditionRowKind[];
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
  /** Fork this case on one ticked row: a new alternative keeps everything
   * else and reads this row the other way (moved to Unless). */
  onBranch?: (kind: ConditionRowKind) => void;
}) {
  const set = (change: (next: TacticConds) => void) => {
    const next = structuredClone(conds);
    change(next);
    onChange(next);
  };

  const row = (kind: ConditionRowKind, label: string, extra?: React.ReactNode, on?: boolean) => {
    const fork = on && onBranch && kind !== 'groupsSet' && (
      <button type="button"
        className="btn !min-h-6 !px-1 !text-[10px] opacity-70 hover:opacity-100"
        title="Branch: add an alternative where this reads the other way"
        onClick={(event) => { event.stopPropagation(); onBranch(kind); }}>
        ⑂
      </button>
    );
    return (
    <TickRow key={kind} on={Boolean(on)} label={label}
      extra={(extra || fork) ? <>{extra}{fork}</> : undefined}
      onToggle={() => {
        set((next) => {
          if (on) delete next[kind];
          else if (kind === 'groupsSet') next.groupsSet = true;
          else if (kind === 'held') next.held = { ticks: 40 };
          else if (kind === 'healed') next.healed = { percent: 90 };
          else if (kind === 'enemiesAt') next.enemiesAt = { count: 5, zone: { name: 'mid', rect: ZONE_OPTIONS[0].rect } };
          else if (kind === 'contact') next.contact = true;
          else if (kind === 'coreLoose') next.coreLoose = { well: 'centre' };
          else if (kind === 'coreSoon') next.coreSoon = { well: 'centre', ticks: 20 };
          else if (kind === 'enemiesDown') next.enemiesDown = { count: 3 };
          else if (kind === 'carrier') next.carrier = true;
          else if (kind === 'losses') next.losses = { count: 2 };
          else if (kind === 'lossesWithin') next.lossesWithin = { count: 4, ticks: 30 };
          else if (kind === 'killsWithin') next.killsWithin = { count: 4, ticks: 40 };
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
  };

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
    if (kind === 'healed') {
      const on = Boolean(conds.healed);
      rows.push(row('healed', 'Everyone is healed up (the dead excuse themselves)', on && conds.healed && <>
        <CountStepper value={conds.healed.percent} min={10} max={100} step={5}
          onChange={(value) => set((next) => { next.healed!.percent = value; })} />
        <span className="t-micro text-arena-dim">%</span>
      </>, on));
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
    if (kind === 'coreAcquired') {
      const on = Boolean(conds.coreAcquired);
      const scoped = typeof conds.coreAcquired === 'object' ? conds.coreAcquired.well : 'any';
      rows.push(row('coreAcquired', ROW_LABELS.coreAcquired!, on && (
        <select
          className="field !min-h-7 !w-auto !py-0 t-micro"
          value={scoped}
          onClick={(event) => event.stopPropagation()}
          onChange={(event) => set((next) => {
            next.coreAcquired = event.target.value === 'any'
              ? true : { well: event.target.value as WellChoice };
          })}
        >
          <option value="any">from any Well</option>
          {WELL_OPTIONS.map((well) => <option key={well.id} value={well.id}>from {well.label}</option>)}
        </select>
      ), on));
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
    if (kind === 'killsWithin') {
      const on = Boolean(conds.killsWithin);
      rows.push(row('killsWithin', 'Enemies destroyed in a short window', on && conds.killsWithin && <>
        <CountStepper value={conds.killsWithin.count} max={8}
          onChange={(value) => set((next) => { next.killsWithin!.count = value; })} />
        <span className="t-micro text-arena-dim">within</span>
        <CountStepper value={conds.killsWithin.ticks} min={10} max={600} step={10}
          onChange={(value) => set((next) => { next.killsWithin!.ticks = value; })} />
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
  const groups = ROW_GROUPS
    .map((group) => group.kinds.filter((kind) => kinds.includes(kind)))
    .filter((sel) => sel.length > 0);
  const showHeadings = groups.length > 1;
  ROW_GROUPS.forEach((group) => {
    const sel = group.kinds.filter((kind) => kinds.includes(kind));
    if (sel.length === 0) return;
    if (showHeadings) {
      rows.push(<div key={`h-${group.label}`}
        className="mt-1 text-[9px] font-semibold uppercase tracking-[.12em] text-arena-dim/70 first:mt-0">
        {group.label}
      </div>);
    }
    for (const kind of sel) renderKind(kind);
  });
  return <div className="flex flex-col gap-1.5">{rows}</div>;
}

/**
 * A stack of alternatives: each case is "Required — all must hold" plus
 * "Triggered by — any may fire"; any case being true is enough. The caller
 * may append a footer per case (the transition's destination picker).
 */
export function CaseListEditor<T extends TacticCase>({
  cases, maximum = 3, accent, watchSymbol, watchAdjective,
  requiredKinds, triggerKinds, unlessKinds, makeCase, onChange,
  caseFooter, watching, onDrawWatch, onStopWatch,
}: {
  cases: T[];
  maximum?: number;
  accent?: string;
  watchSymbol?: string;
  watchAdjective?: string;
  requiredKinds: ConditionRowKind[];
  triggerKinds: ConditionRowKind[];
  /** The inverted side: rows offered under "Unless" — NONE may hold. */
  unlessKinds?: ConditionRowKind[];
  makeCase: () => T;
  onChange: (cases: T[]) => void;
  caseFooter?: (value: T, update: (apply: (next: T) => void) => void, index: number) => React.ReactNode;
  /** Which case/side/row is being drawn on the map, if any. */
  watching?: { caseIndex: number; side: 'required' | 'triggers' | 'unless'; row: 'enemiesAt' | 'cleared' } | null;
  onDrawWatch?: (caseIndex: number, side: 'required' | 'triggers' | 'unless', row: 'enemiesAt' | 'cleared') => void;
  onStopWatch?: () => void;
}) {
  const update = (index: number, apply: (next: T) => void) => {
    const next = structuredClone(cases);
    apply(next[index]);
    onChange(next);
  };
  const [unlessOpen, setUnlessOpen] = useState<Record<number, boolean>>({});
  // Alternatives collapse to their plain-English line; a sheet opening on
  // several starts collapsed, and adding one folds the others away.
  const [collapsed, setCollapsed] = useState<Record<number, boolean>>(
    () => cases.length > 1
      ? Object.fromEntries(cases.map((_, index) => [index, true]))
      : {});
  const isOpen = (index: number) =>
    watching?.caseIndex === index || !collapsed[index];
  const reindexAfterRemove = (map: Record<number, boolean>, removed: number) => {
    const next: Record<number, boolean> = {};
    for (const [key, open] of Object.entries(map)) {
      const at = Number(key);
      if (at === removed) continue;
      next[at > removed ? at - 1 : at] = open;
    }
    return next;
  };
  const appendCase = (next: T) => {
    setCollapsed(Object.fromEntries(cases.map((_, index) => [index, true])));
    onChange([...cases, next]);
  };
  const activeMainRows = (value: T) =>
    Object.keys(value.required ?? {}).length
    + Object.keys(value.triggers ?? {}).length;
  // "These two, then the inversion of this third one": the new alternative
  // keeps every other row and moves the forked one to Unless.
  const branch = (index: number, side: 'required' | 'triggers', kind: ConditionRowKind) => {
    const copy = structuredClone(cases[index]);
    const rows = copy[side];
    const value = rows?.[kind];
    if (rows === undefined || value === undefined) return;
    delete rows[kind];
    if (Object.keys(rows).length === 0) delete copy[side];
    copy.unless = { ...(copy.unless ?? {}), [kind]: value };
    appendCase(copy);
  };
  return (
    <div className="flex flex-col gap-2">
      {cases.map((value, index) => {
        const canBranch = cases.length < maximum && activeMainRows(value) >= 2;
        const open = isOpen(index);
        const line = caseClause(value);
        // The fold lives at the TOP of every alternative, as the whole
        // header bar: chevron + the live one-line reading. The same bar in
        // both states — tap to fold the editor away, tap to bring it back.
        const header = (
          <button type="button"
            className="flex min-h-7 w-full items-center gap-1.5 text-left"
            title={open ? 'Fold this alternative to its one-line reading' : 'Open this alternative to edit'}
            onClick={() => setCollapsed((state) => ({ ...state, [index]: open }))}>
            <span className="text-[10px] text-arena-dim">{open ? '▾' : '▸'}</span>
            {index > 0 && (
              <span className="lab shrink-0" style={accent ? { color: accent } : undefined}>
                Or alternatively
              </span>
            )}
            <span className={`t-micro ${open ? 'text-arena-dim' : 'text-arena-text/90'} min-w-0 grow truncate`}>
              {line
                ? line.replace(/^\w/, (letter) => letter.toUpperCase())
                : <span className="text-arena-dim">No conditions yet.</span>}
            </span>
          </button>
        );
        if (!open) {
          return (
            <div key={index} className="rounded-sm border border-arena-edge p-2"
              style={accent ? { borderLeft: `3px solid ${accent}` } : undefined}>
              <div className="flex items-center gap-2">
                <span className="min-w-0 grow">{header}</span>
                {caseFooter?.(value, (apply) => update(index, apply), index)}
              </div>
            </div>
          );
        }
        return (
        <div key={index} className="rounded-sm border border-arena-edge p-2"
          style={accent ? { borderLeft: `3px solid ${accent}` } : undefined}>
          {header}
          {requiredKinds.length > 0 && <>
            <div className="lab mb-1">Required — all must hold</div>
            <ConditionRows
              conds={value.required ?? {}}
              kinds={requiredKinds}
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
              onBranch={canBranch
                ? (kind) => branch(index, 'required', kind) : undefined}
            />
          </>}
          <div className="lab mb-1 mt-2">Triggered by — any may fire</div>
          <ConditionRows
            conds={value.triggers ?? {}}
            kinds={triggerKinds}
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
            onBranch={canBranch
              ? (kind) => branch(index, 'triggers', kind) : undefined}
          />
          {(unlessKinds?.length ?? 0) > 0 && (
            Object.keys(value.unless ?? {}).length > 0 || unlessOpen[index]
              ? <>
                <div className="lab mb-1 mt-2">Unless — none of these</div>
                <ConditionRows
                  conds={value.unless ?? {}}
                  kinds={unlessKinds!}
                  accent={accent}
                  watchSymbol={watchSymbol}
                  watchAdjective={watchAdjective}
                  watchingRow={watching && watching.caseIndex === index
                    && watching.side === 'unless' ? watching.row : null}
                  onChange={(conds) => update(index, (next) => {
                    if (Object.keys(conds).length === 0) delete next.unless;
                    else next.unless = conds;
                  })}
                  onDrawWatch={onDrawWatch
                    ? (row) => onDrawWatch(index, 'unless', row) : undefined}
                  onStopWatch={onStopWatch}
                />
              </>
              : <button type="button" className="btn mt-2 !min-h-6 !px-1.5 !text-[10px]"
                onClick={() => setUnlessOpen((open) => ({ ...open, [index]: true }))}>
                ＋ Unless…
              </button>
          )}
          <div className="mt-2 flex items-center gap-2">
            {caseFooter?.(value, (apply) => update(index, apply), index)}
            <span className="grow" />
            {cases.length > 1 && (
              <button type="button" className="btn"
                onClick={() => {
                  onChange(cases.filter((_, i) => i !== index));
                  setCollapsed((state) => reindexAfterRemove(state, index));
                  setUnlessOpen((state) => reindexAfterRemove(state, index));
                }}>
                Remove alternative
              </button>
            )}
          </div>
        </div>
        );
      })}
      {cases.length < maximum && (
        <button type="button" className="btn self-start"
          onClick={() => appendCase(makeCase())}>
          ＋ Add alternative
        </button>
      )}
    </div>
  );
}
