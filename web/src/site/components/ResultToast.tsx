import { Link } from 'react-router-dom';
import { botLook } from '../../render/arenaThemes';
import type {
  MatchChallengedPayload,
  MatchSettledPayload,
  SetSettledPayload,
} from '../api';

/**
 * Something happened to one of your bots — in the same shape as the unlock toast:
 * eyebrow, artwork, headline, a way in.
 *
 * Sharing that shape is the point: both are the game telling you something happened to
 * your bot, and two unrelated toast designs would read as two unrelated products. What
 * changes is the accent and the headline, because a rating delta is the thing a player
 * came for and a cosmetic is not.
 *
 * A loss gets the same size, the same artwork, the same prominence (DECISIONS #119). The
 * ladder already shows the rating; a shrunken loss reads as the app hiding it.
 *
 * A *challenge* renders here too rather than in a toast of its own, because on the server
 * it is the same row: the notification you are looking at will be rewritten into its own
 * result. One component means the invitation and the outcome cannot drift apart visually.
 */
export default function ResultToast({
  payload,
  queued,
  onDismiss,
}: {
  payload: MatchChallengedPayload | MatchSettledPayload | SetSettledPayload;
  queued: number;
  onDismiss: () => void;
}) {
  const ranked = payload.kind === 'set-settled';
  const challenged = payload.kind === 'match-challenged';
  // A challenge has no outcome and cannot have one — it is written when the match is
  // queued, long before broadcast secrecy would allow a result to exist.
  const tone = challenged ? TONES.Challenged : (TONES[payload.outcome] ?? TONES.Draw);
  const chassis = botLook(payload.botLookId);

  return (
    <aside
      className={`unlock-toast fixed top-4 right-4 z-50 w-[min(28rem,calc(100vw-2rem))] overflow-hidden rounded-xl border bg-[#101720]/96 backdrop-blur ${tone.border} ${tone.glow}`}
      role="status"
      aria-live="polite"
    >
      <div className="unlock-toast__sheen pointer-events-none absolute inset-0" />
      <div
        className={`absolute inset-x-0 top-0 h-px bg-linear-to-r from-transparent to-transparent ${tone.rule}`}
      />
      <div className="relative flex gap-4 p-4 sm:p-5">
        <div
          className="relative flex size-24 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-white/10 bg-[radial-gradient(circle_at_50%_42%,rgba(255,255,255,0.08),rgba(10,14,20,0.85)_68%)]"
          style={{ boxShadow: `inset 0 -3px 0 ${payload.botAccent}88` }}
        >
          <div
            className="unlock-toast__halo absolute size-16 rounded-full blur-xl"
            style={{ backgroundColor: `${tone.halo}` }}
          />
          {chassis?.imageUrl && (
            <img
              src={chassis.imageUrl}
              alt=""
              className="relative size-20 object-contain drop-shadow-[0_7px_9px_rgba(0,0,0,0.55)]"
            />
          )}
        </div>

        <div className="min-w-0 flex-1 py-0.5">
          <p className={`font-mono text-[10px] font-bold tracking-[0.22em] ${tone.eyebrow}`}>
            {challenged ? 'CHALLENGE' : ranked ? 'RANKED SET' : 'MATCH COMPLETE'}
          </p>
          <h2 className="type-display mt-1 truncate text-[19px] text-arena-text">
            {challenged
              ? `${payload.botName} was challenged`
              : `${payload.botName} ${VERBS[payload.outcome] ?? 'drew'}`}
          </h2>

          {payload.kind === 'match-challenged' ? (
            <p className="mt-0.5 text-sm text-arena-dim">
              by {payload.challengerName} on{' '}
              <span className="font-mono text-xs">{payload.mapId}</span>
            </p>
          ) : payload.kind === 'set-settled' ? (
            <p className="mt-0.5 font-mono text-sm font-semibold text-arena-text">
              {payload.score}–{payload.opponentScore}{' '}
              <span className="font-sans text-xs font-normal text-arena-dim">
                against {payload.opponentName}
              </span>
            </p>
          ) : (
            <p className="mt-0.5 text-sm text-arena-dim">
              against {payload.opponentName} on{' '}
              <span className="font-mono text-xs">{payload.mapId}</span>
            </p>
          )}

          <Link
            to={payload.kind === 'set-settled' ? `/sets/${payload.matchSetId}` : `/matches/${payload.matchId}`}
            onClick={onDismiss}
            className="mt-3 inline-flex items-center gap-1 font-mono text-xs font-bold text-arena-accent transition-colors hover:text-arena-accent"
          >
            {payload.kind === 'set-settled'
              ? 'See the set'
              : challenged
                ? 'Watch it live'
                : 'Watch it back'}{' '}
            <span aria-hidden>→</span>
          </Link>
          {queued > 0 && (
            <p className="mt-2 font-mono text-[10px] text-arena-dim">+{queued} more</p>
          )}
        </div>

        {payload.kind === 'set-settled' && (
          // The number the player came for, so it gets the size to match.
          <p className={`self-start font-mono text-2xl font-black ${tone.delta}`}>
            {payload.ratingChange >= 0 ? '+' : ''}
            {Math.round(payload.ratingChange)}
          </p>
        )}

        <button
          type="button"
          onClick={onDismiss}
          aria-label="Dismiss result notification"
          className="-mt-1 -mr-1 flex size-7 shrink-0 items-center justify-center rounded-md text-arena-dim transition-colors hover:bg-white/5 hover:text-arena-text"
        >
          ×
        </button>
      </div>
    </aside>
  );
}

const VERBS: Record<string, string> = { Win: 'won', Loss: 'lost', Draw: 'drew' };

/** One tone per outcome, so colour is consistent with the arena's result palette. */
const TONES: Record<
  string,
  {
    border: string;
    glow: string;
    rule: string;
    eyebrow: string;
    delta: string;
    halo: string;
  }
> = {
  Win: {
    border: 'border-arena-ok/45',
    glow: 'shadow-[0_22px_70px_rgba(0,0,0,0.55),0_0_35px_rgba(52,211,153,0.13)]',
    rule: 'via-arena-ok',
    eyebrow: 'text-arena-ok',
    delta: 'text-arena-ok',
    halo: 'rgba(52,211,153,0.12)',
  },
  Loss: {
    border: 'border-arena-hot/40',
    glow: 'shadow-[0_22px_70px_rgba(0,0,0,0.55),0_0_35px_rgba(248,113,113,0.12)]',
    rule: 'via-arena-hot',
    eyebrow: 'text-arena-hot',
    delta: 'text-arena-hot',
    halo: 'rgba(248,113,113,0.12)',
  },
  // Not an outcome — an invitation. The arena accent rather than a result colour, because
  // green or red here would state a result the match has not produced yet.
  Challenged: {
    border: 'border-arena-accent/45',
    glow: 'shadow-[0_22px_70px_rgba(0,0,0,0.55),0_0_35px_rgba(56,189,248,0.13)]',
    rule: 'via-arena-accent',
    eyebrow: 'text-arena-accent',
    delta: 'text-arena-accent',
    halo: 'rgba(56,189,248,0.12)',
  },
  Draw: {
    border: 'border-arena-edge2',
    glow: 'shadow-[0_22px_70px_rgba(0,0,0,0.55)]',
    rule: 'via-arena-text',
    eyebrow: 'text-arena-dim',
    delta: 'text-arena-dim',
    halo: 'rgba(148,163,184,0.10)',
  },
};
