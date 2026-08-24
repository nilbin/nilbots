import { condClauses } from './summary';
import { phaseShade, type Tactic } from './tacticsDocument';

/** The little named-phase chronology strip on every card:
 * ▶ when… — Set trap ⚡ Close trap ⚡ … — ✓/∞. */
export function PhaseStrip({ tactic, big = false }: { tactic: Tactic; big?: boolean }) {
  const gated = tactic.start.mode === 'when'
    && (tactic.start.cases ?? []).some((value) =>
      condClauses(value.required).length + condClauses(value.triggers).length > 0);
  const finishes = (tactic.phases[tactic.phases.length - 1]?.continue ?? []).length > 0
    || (tactic.maxDuration && tactic.maxDuration !== 'none');
  const chip = (label: string, tone?: string, key?: string) => (
    <span key={key ?? label}
      className={`rounded-[3px] border border-arena-edge px-1 py-px font-semibold uppercase tracking-wide ${big ? 'text-[10.5px]' : 'text-[9px]'}`}
      style={tone ? { color: tone, borderColor: `${tone}55` } : undefined}
    >
      {label}
    </span>
  );
  const bolt = (key: string) => (
    <span key={key} className="text-[9px]" style={{ color: '#c8763c' }}>⚡</span>
  );
  const parts: React.ReactNode[] = [];
  if (gated) {
    parts.push(chip('▶ when…', undefined, 'gate'));
    parts.push(<span key="gate-dash" className="text-arena-dim/60">—</span>);
  }
  tactic.phases.forEach((phase, index) => {
    if (index > 0) parts.push(bolt(`bolt-${index}`));
    parts.push(chip(phase.name,
      phaseShade('#7694a8', index, tactic.phases.length), `phase-${phase.id}`));
  });
  parts.push(<span key="end-dash" className="text-arena-dim/60">—</span>);
  parts.push(chip(
    finishes ? '✓ finish' : '∞ holds',
    finishes ? '#38d9c5' : '#b49cff',
    'end'));
  if (tactic.abort) parts.push(chip(
    tactic.abort.throughPhase ? '⨯ early abort' : '⨯ abort',
    '#f4526b', 'abort'));
  return (
    <div className="mt-1 flex flex-wrap items-center gap-1 text-arena-dim">
      {parts}
    </div>
  );
}
