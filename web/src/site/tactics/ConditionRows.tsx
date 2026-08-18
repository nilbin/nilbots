import { useState } from 'react';
import { CountStepper, TickRow, ZoneSelect } from './conditionControls';
import { WELL_OPTIONS, ZONE_OPTIONS } from './places';
import type { TacticConds, WellChoice } from './tacticsDocument';

export type ConditionRowKind =
  | 'enemiesAt' | 'contact' | 'coreLoose' | 'coreSoon' | 'enemiesDown'
  | 'carrier' | 'losses';

/**
 * THE condition editor: one tick row per available condition, parameters
 * inline on ticked rows, any ticked row satisfies the question (OR). Start,
 * Release and Finish all speak this idiom (via conditionControls). The
 * less-common rows sit behind a "More conditions" fold so the everyday set
 * stays uncluttered; any active folded row keeps the fold open.
 */
export function ConditionRows({ conds, kinds, moreKinds, accent, watchSymbol = '⚡', watchAdjective, watching, onChange, onDrawWatch, onStopWatch }: {
  conds: TacticConds;
  kinds: ConditionRowKind[];
  moreKinds?: ConditionRowKind[];
  accent?: string;
  /** The watch chrome's glyph: ▶ for a start watch, ⚡ for a release watch. */
  watchSymbol?: string;
  /** Names the box's colour in the hint ("pale", "orange"). */
  watchAdjective?: string;
  watching?: boolean;
  onChange: (conds: TacticConds) => void;
  onDrawWatch?: () => void;
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
          else if (kind === 'enemiesAt') next.enemiesAt = { count: 5, zone: { name: 'mid', rect: ZONE_OPTIONS[0].rect } };
          else if (kind === 'contact') next.contact = true;
          else if (kind === 'coreLoose') next.coreLoose = { well: 'centre' };
          else if (kind === 'coreSoon') next.coreSoon = { well: 'centre', ticks: 20 };
          else if (kind === 'enemiesDown') next.enemiesDown = { count: 3 };
          else if (kind === 'carrier') next.carrier = true;
          else if (kind === 'losses') next.losses = { count: 2 };
        });
      }} />
  );

  const rows: React.ReactNode[] = [];
  const renderKind = (kind: ConditionRowKind) => {
    if (kind === 'enemiesAt') {
      const on = Boolean(conds.enemiesAt);
      rows.push(row('enemiesAt', 'Enemies gather somewhere', on && conds.enemiesAt && <>
        <CountStepper value={conds.enemiesAt.count}
          onChange={(value) => set((next) => { next.enemiesAt!.count = value; })} />
        <ZoneSelect zone={conds.enemiesAt.zone}
          onChange={(zone) => set((next) => { next.enemiesAt!.zone = zone; })} />
      </>, on));
      if (on) {
        rows.push(<div key="enemiesAt-draw"
          className="flex min-h-8 items-center gap-2 rounded-sm border border-arena-edge px-2 t-micro text-arena-dim"
          style={accent ? { borderColor: `${accent}55` } : undefined}>
          <span style={{ color: accent ?? 'var(--color-arena-material)' }}>{watchSymbol}</span>
          <span>The {watchAdjective ? `${watchAdjective} box` : 'box'} on the map is the watched ground. Drag its corners any time.</span>
          <span className="grow" />
          {watching
            ? <button type="button" className="btn btn-on" onClick={onStopWatch}>Stop drawing</button>
            : onDrawWatch && <button type="button" className="btn btn-secondary" onClick={onDrawWatch}>Draw it instead</button>}
        </div>);
      }
    }
    if (kind === 'contact') rows.push(row('contact', 'Any enemy contact', null, Boolean(conds.contact)));
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
    }
    if (kind === 'carrier') {
      rows.push(row('carrier', 'We hold a Core — a friendly carrier exists', null, Boolean(conds.carrier)));
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
    }
    if (kind === 'enemiesDown') {
      const on = Boolean(conds.enemiesDown);
      rows.push(row('enemiesDown', 'Enemies are down', on && conds.enemiesDown
        && <CountStepper value={conds.enemiesDown.count}
          onChange={(value) => set((next) => { next.enemiesDown!.count = value; })} />, on));
    }
    if (kind === 'losses') {
      const on = Boolean(conds.losses);
      rows.push(row('losses', 'We are down bots', on && conds.losses
        && <CountStepper value={conds.losses.count} max={7}
          onChange={(value) => set((next) => { next.losses!.count = value; })} />, on));
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
