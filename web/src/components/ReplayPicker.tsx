import clsx from 'clsx';

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
      <span className="lab">Replay</span>
      {choices.map((choice) => {
        const active = choice.id === activeId;
        return (
          <button
            key={choice.id}
            type="button"
            onClick={() => onSelect(choice)}
            aria-current={active}
            className={clsx(
              'panel-quiet px-2.5 py-1 text-left transition-colors',
              active
                ? 'border-arena-edge2 bg-arena-raise text-arena-text'
                : 'text-arena-dim hover:border-arena-edge2 hover:text-arena-text',
            )}
          >
            <span className="t-body block font-semibold">{choice.bots.join(' v ')}</span>
            <span className="val block">
              {choice.map} · {choice.ticks}t{choice.reason ? ` · ${choice.reason}` : ''}
            </span>
          </button>
        );
      })}
    </nav>
  );
}
