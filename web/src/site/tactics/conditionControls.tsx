import { ZONE_OPTIONS } from './places';
import type { ZoneRef } from './tacticsDocument';

/**
 * The shared vocabulary of every condition-like editor — Start's and
 * Release's trigger rows and Finish's outcome rows all compose these three
 * primitives, so the idiom cannot drift apart between steps.
 */

/** One togglable row: tick box, label, inline parameters while on.
 * `radio` renders the single-choice dot the release latch uses. */
export function TickRow({ on, radio, label, extra, onToggle }: {
  on: boolean;
  radio?: boolean;
  label: string;
  extra?: React.ReactNode;
  onToggle: () => void;
}) {
  return (
    <button type="button" onClick={onToggle}
      className={`flex min-h-9 w-full items-center gap-2 rounded-sm border px-2 py-1 text-left transition-colors ${on
        ? 'border-arena-ok/45 bg-arena-ok/10 text-arena-text'
        : 'border-arena-edge text-arena-dim hover:text-arena-text'}`}>
      <span className={`flex h-4 w-4 shrink-0 items-center justify-center border text-[10px] font-bold ${radio
        ? 'rounded-full' : 'rounded-[3px]'} ${on
        ? 'border-arena-ok bg-arena-ok/15 text-arena-ok'
        : 'border-arena-text/35 text-transparent'}`}>{radio ? '●' : '✓'}</span>
      <span className="t-micro grow">{label}</span>
      {on && extra}
    </button>
  );
}

/** A bounded count parameter ("N enemies", "N bots"). */
export function CountStepper({ value, min = 1, max = 8, onChange }: {
  value: number;
  min?: number;
  max?: number;
  onChange: (value: number) => void;
}) {
  return (
    <span className="flex items-center gap-1 rounded-sm border border-arena-edge"
      onClick={(event) => event.stopPropagation()} role="presentation">
      <button type="button" className="min-h-7 px-2"
        onClick={() => onChange(Math.max(min, value - 1))}>−</button>
      <span className="tabular t-micro w-4 text-center">{value}</span>
      <button type="button" className="min-h-7 px-2"
        onClick={() => onChange(Math.min(max, value + 1))}>＋</button>
    </span>
  );
}

/** The named-zone picker over a ZoneRef; while a drawn custom box is the
 * value it shows as its own "the drawn box" option. */
export function ZoneSelect({ zone, onChange }: {
  zone: ZoneRef;
  onChange: (zone: ZoneRef) => void;
}) {
  const matched = ZONE_OPTIONS.find((option) =>
    option.rect.every((value, index) => value === zone.rect[index]));
  return (
    <select className="field !min-h-7 !w-auto !py-0 t-micro"
      value={matched?.id ?? 'custom'}
      onClick={(event) => event.stopPropagation()}
      onChange={(event) => {
        const option = ZONE_OPTIONS.find((value) => value.id === event.target.value);
        if (option) onChange({ name: option.id, rect: option.rect });
      }}>
      {!matched && <option value="custom">the drawn box</option>}
      {ZONE_OPTIONS.map((option) =>
        <option key={option.id} value={option.id}>{option.label}</option>)}
    </select>
  );
}
