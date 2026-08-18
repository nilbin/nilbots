import { namedZoneForRect, WELL_OPTIONS, ZONE_OPTIONS } from './places';
import type { TacticConds, WellChoice } from './tacticsDocument';

export type ConditionRowKind = 'enemiesAt' | 'contact' | 'coreLoose' | 'carrier' | 'losses';

/**
 * THE condition editor: one tick row per available condition, parameters
 * inline on ticked rows, any ticked row satisfies the question (OR). Start,
 * Release and Finish all speak this idiom; Release additionally caps active
 * rows at one (the runtime release latch is a single condition).
 */
export function ConditionRows({ conds, kinds, single, accent, watchSymbol = '⚡', watchAdjective, watching, onChange, onDrawWatch, onStopWatch }: {
  conds: TacticConds;
  kinds: ConditionRowKind[];
  single?: boolean;
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
    if (single) {
      const active: ConditionRowKind[] = [];
      if (next.enemiesAt) active.push('enemiesAt');
      if (next.contact) active.push('contact');
      if (next.coreLoose) active.push('coreLoose');
      if (next.carrier) active.push('carrier');
      if (next.losses) active.push('losses');
      // Keep only the newest tick: the release latch carries one condition.
      if (active.length > 1) {
        for (const kind of active) {
          if (!justToggled.includes(kind)) delete next[kind];
        }
      }
    }
    onChange(next);
  };
  let justToggled: ConditionRowKind[] = [];

  const row = (kind: ConditionRowKind, label: string, extra?: React.ReactNode, on?: boolean) => (
    <button
      key={kind}
      type="button"
      onClick={() => {
        justToggled = [kind];
        set((next) => {
          if (on) delete next[kind];
          else if (kind === 'enemiesAt') next.enemiesAt = { count: 5, zone: { name: 'mid', rect: ZONE_OPTIONS[0].rect } };
          else if (kind === 'contact') next.contact = true;
          else if (kind === 'coreLoose') next.coreLoose = { well: 'centre' };
          else if (kind === 'carrier') next.carrier = true;
          else if (kind === 'losses') next.losses = { count: 2 };
        });
      }}
      className={`flex min-h-9 w-full items-center gap-2 rounded-sm border px-2 py-1 text-left transition-colors ${on
        ? 'border-arena-ok/45 bg-arena-ok/10 text-arena-text'
        : 'border-arena-edge text-arena-dim hover:text-arena-text'}`}
    >
      <span className={`flex h-4 w-4 shrink-0 items-center justify-center rounded-[3px] border text-[10px] font-bold ${on
        ? 'border-arena-ok bg-arena-ok/15 text-arena-ok'
        : 'border-arena-text/35 text-transparent'}`}>✓</span>
      <span className="t-micro grow">{label}</span>
      {on && extra}
    </button>
  );

  const stepper = (value: number, apply: (value: number) => void) => (
    <span
      className="flex items-center gap-1 rounded-sm border border-arena-edge"
      onClick={(event) => event.stopPropagation()}
      role="presentation"
    >
      <button type="button" className="min-h-7 px-2" onClick={() => apply(Math.max(1, value - 1))}>−</button>
      <span className="tabular t-micro w-4 text-center">{value}</span>
      <button type="button" className="min-h-7 px-2" onClick={() => apply(Math.min(8, value + 1))}>＋</button>
    </span>
  );

  const rows: React.ReactNode[] = [];
  for (const kind of kinds) {
    if (kind === 'enemiesAt') {
      const on = Boolean(conds.enemiesAt);
      const zoneId = conds.enemiesAt ? namedZoneForRect(conds.enemiesAt.zone.rect)?.id ?? 'custom' : 'mid';
      rows.push(row('enemiesAt', 'Enemies gather somewhere', on && conds.enemiesAt && <>
        {stepper(conds.enemiesAt.count, (value) =>
          set((next) => { next.enemiesAt!.count = value; }))}
        <select
          className="field !min-h-7 !w-auto !py-0 t-micro"
          value={zoneId}
          onClick={(event) => event.stopPropagation()}
          onChange={(event) => {
            const zone = ZONE_OPTIONS.find((value) => value.id === event.target.value);
            if (zone) set((next) => { next.enemiesAt!.zone = { name: zone.id, rect: zone.rect }; });
          }}
        >
          {zoneId === 'custom' && <option value="custom">the drawn box</option>}
          {ZONE_OPTIONS.map((zone) => <option key={zone.id} value={zone.id}>{zone.label}</option>)}
        </select>
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
    if (kind === 'losses') {
      const on = Boolean(conds.losses);
      rows.push(row('losses', 'We are down bots', on && conds.losses
        && stepper(conds.losses.count, (value) =>
          set((next) => { next.losses!.count = Math.min(7, value); })), on));
    }
  }
  return <div className="flex flex-col gap-1.5">{rows}</div>;
}
