export interface ReplayChoice {
  id: string;
  url: string;
  map: string;
  bots: string[];
  ticks: number;
  reason: string | null;
}

/**
 * Switch between the replays a review build carries.
 *
 * Review is comparative — a fog treatment or a light intensity that reads well on one
 * fight can be wrong on the next, and reloading between them loses the comparison. This
 * only appears when `replays.json` is present, so the CLI's single-replay viewer and the
 * site are untouched.
 */
export default function ReplayPicker({
  choices,
  activeId,
  onSelect,
}: {
  choices: readonly ReplayChoice[];
  activeId: string | null;
  onSelect: (choice: ReplayChoice) => void;
}) {
  if (choices.length < 2) return null;

  return (
    <nav
      className="flex flex-wrap items-center gap-2"
      aria-label="Choose a replay to review"
    >
      <span className="font-mono text-[10px] tracking-widest text-arena-dim">REPLAY</span>
      {choices.map((choice) => {
        const active = choice.id === activeId;
        return (
          <button
            key={choice.id}
            type="button"
            onClick={() => onSelect(choice)}
            aria-current={active}
            className={
              'rounded-md border px-2.5 py-1 text-left text-xs transition-colors ' +
              (active
                ? 'border-arena-accent bg-arena-panel text-arena-text'
                : 'border-arena-edge bg-arena-panel/50 text-arena-dim hover:border-arena-dim')
            }
          >
            <span className="block font-semibold">{choice.bots.join(' v ')}</span>
            <span className="block font-mono text-[10px] text-arena-dim">
              {choice.map} · {choice.ticks}t{choice.reason ? ` · ${choice.reason}` : ''}
            </span>
          </button>
        );
      })}
    </nav>
  );
}
