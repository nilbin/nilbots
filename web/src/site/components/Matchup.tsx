import { Fragment } from 'react';
import clsx from 'clsx';
import BotIdentity, { type BotIdentitySize } from './BotIdentity';
import { type MatchSummaryParticipant } from '../api';

/**
 * What a matchup needs from a participant, taken as a `Pick` of the generated summary
 * type rather than written out: a field the server renames breaks here at build time, and
 * the detail projection carries the same five so it fits this without a second type.
 */
export type MatchupParticipant = Pick<
  MatchSummaryParticipant,
  | 'slot'
  | 'nameSnapshot'
  | 'ownerDisplayNameSnapshot'
  | 'accentSnapshot'
  | 'lookIdSnapshot'
>;

interface MatchupProps {
  participants: readonly MatchupParticipant[];
  /**
   * The winning slot, when the API has revealed one. Passed in rather than worked out
   * here: `null` covers both "drawn" and "still broadcasting, not telling you yet", and
   * only the caller holding the whole match can tell those apart.
   */
  winnerSlot?: number | null;
  size?: BotIdentitySize;
  /** `row` for a feed line, `stack` for a card, where names get a line each. */
  layout?: 'row' | 'stack';
  /** Owners beside the names. Worth the room on a card, noise on a feed row. */
  showOwners?: boolean;
  className?: string;
}

/**
 * Who fought — the identity chips of every participant, joined by `vs`.
 *
 * It walks the collection instead of destructuring `[a, b]`. A match is a list on the
 * wire and the engine's topology is teams of units rather than two bots; a layout that
 * reads the first two renders half a fight the day a third arrives, and renders it
 * silently.
 *
 * The winner is marked by weight alone. The row's verdict says who won in words, so a
 * second glyph or a colour here would be the same fact three times.
 */
export default function Matchup({
  participants,
  winnerSlot = null,
  size = 'xs',
  layout = 'row',
  showOwners = false,
  className,
}: MatchupProps) {
  const stacked = layout === 'stack';
  return (
    <div
      className={clsx(
        'flex min-w-0',
        stacked ? 'flex-col gap-1.5' : 'flex-wrap items-center gap-x-2.5 gap-y-1',
        className,
      )}
    >
      {participants.map((participant, index) => (
        <Fragment key={participant.slot}>
          {index > 0 && (
            <span className="type-label text-[10.5px] text-arena-dim">vs</span>
          )}
          <span
            className={clsx(
              'flex min-w-0 items-center gap-2',
              stacked && 'justify-between',
            )}
          >
            <BotIdentity
              name={participant.nameSnapshot}
              accent={participant.accentSnapshot}
              lookId={participant.lookIdSnapshot}
              size={size}
              emphasized={participant.slot === winnerSlot}
            />
            {showOwners && (
              <span className="min-w-0 truncate text-[11.5px] tracking-[0.04em] text-arena-dim [font-stretch:84%]">
                {participant.ownerDisplayNameSnapshot}
              </span>
            )}
          </span>
        </Fragment>
      ))}
    </div>
  );
}
