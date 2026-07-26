import { Link } from 'react-router-dom';
import { botLook } from '../../render/arenaThemes';
import type { MatchSettledPayload, SetSettledPayload } from '../api';

/**
 * A finished fight, in the same shape as the unlock toast — eyebrow, artwork, headline,
 * a way in — but tinted by the outcome rather than achievement amber.
 *
 * Sharing that shape is the point: both are the game telling you something happened to
 * your bot, and two unrelated toast designs would read as two unrelated products. What
 * changes is the accent and the headline, because a rating delta is the thing a player
 * came for and a cosmetic is not.
 *
 * A loss gets the same size, the same artwork, the same prominence (DECISIONS #119). The
 * ladder already shows the rating; a shrunken loss reads as the app hiding it.
 */
export default function ResultToast({
  payload,
  queued,
  onDismiss,
}: {
  payload: MatchSettledPayload | SetSettledPayload;
  queued: number;
  onDismiss: () => void;
}) {
  const ranked = payload.kind === 'set-settled';
  const tone = TONES[payload.outcome] ?? TONES.Draw;
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
            {ranked ? 'RANKED SET' : 'MATCH COMPLETE'}
          </p>
          <h2 className="mt-1 truncate text-lg font-black tracking-wide text-slate-100">
            {payload.botName} {VERBS[payload.outcome] ?? 'drew'}
          </h2>

          {payload.kind === 'set-settled' ? (
            <p className="mt-0.5 font-mono text-sm font-semibold text-slate-200">
              {payload.score}–{payload.opponentScore}{' '}
              <span className="font-sans text-xs font-normal text-slate-400">
                against {payload.opponentName}
              </span>
            </p>
          ) : (
            <p className="mt-0.5 text-sm text-slate-400">
              against {payload.opponentName} on{' '}
              <span className="font-mono text-xs">{payload.mapId}</span>
            </p>
          )}

          <Link
            to={payload.kind === 'set-settled' ? `/sets/${payload.matchSetId}` : `/matches/${payload.matchId}`}
            onClick={onDismiss}
            className="mt-3 inline-flex items-center gap-1 font-mono text-xs font-bold text-arena-accent transition-colors hover:text-sky-300"
          >
            {payload.kind === 'set-settled' ? 'See the set' : 'Watch it back'}{' '}
            <span aria-hidden>→</span>
          </Link>
          {queued > 0 && (
            <p className="mt-2 font-mono text-[10px] text-slate-500">+{queued} more</p>
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
          className="-mt-1 -mr-1 flex size-7 shrink-0 items-center justify-center rounded-md text-slate-500 transition-colors hover:bg-white/5 hover:text-slate-200"
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
    border: 'border-emerald-300/45',
    glow: 'shadow-[0_22px_70px_rgba(0,0,0,0.55),0_0_35px_rgba(52,211,153,0.13)]',
    rule: 'via-emerald-200',
    eyebrow: 'text-emerald-300',
    delta: 'text-emerald-400',
    halo: 'rgba(52,211,153,0.12)',
  },
  Loss: {
    border: 'border-red-400/40',
    glow: 'shadow-[0_22px_70px_rgba(0,0,0,0.55),0_0_35px_rgba(248,113,113,0.12)]',
    rule: 'via-red-300',
    eyebrow: 'text-red-300',
    delta: 'text-red-400',
    halo: 'rgba(248,113,113,0.12)',
  },
  Draw: {
    border: 'border-slate-400/35',
    glow: 'shadow-[0_22px_70px_rgba(0,0,0,0.55)]',
    rule: 'via-slate-300',
    eyebrow: 'text-slate-300',
    delta: 'text-slate-300',
    halo: 'rgba(148,163,184,0.10)',
  },
};
