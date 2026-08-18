import { condClauses } from './summary';
import type { Tactic } from './tacticsDocument';

/** The little Stage — ⚡ Release — Act — ✓ chronology strip on every card. */
export function PhaseStrip({ tactic, big = false }: { tactic: Tactic; big?: boolean }) {
  const acts = tactic.groups.some((group) => group.act.kind !== 'none');
  const outcomes = Object.keys(tactic.finish.outcomes).length;
  const gated = tactic.start.mode === 'when'
    && condClauses(tactic.start.conds).length > 0;
  const chip = (label: string, tone?: string) => (
    <span
      className={`rounded-[3px] border border-arena-edge px-1 py-px font-semibold uppercase tracking-wide ${big ? 'text-[10.5px]' : 'text-[9px]'}`}
      style={tone ? { color: tone, borderColor: `${tone}55` } : undefined}
    >
      {label}
    </span>
  );
  const dash = <span className="text-arena-dim/60">—</span>;
  return (
    <div className="mt-1 flex flex-wrap items-center gap-1 text-arena-dim">
      {gated && <>{chip('▶ when…')}{dash}</>}
      {chip('Stage')}
      {dash}
      {chip('⚡ Release', '#c8763c')}
      {dash}
      {acts && <>{chip('Act')}{dash}</>}
      {chip(`✓ ${outcomes || 'no'} outcome${outcomes === 1 ? '' : 's'}${tactic.finish.maxAttempt !== 'none' ? ' · timer' : ''}`, '#38d9c5')}
    </div>
  );
}
