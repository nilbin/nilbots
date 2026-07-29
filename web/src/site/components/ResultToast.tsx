import { Link } from 'react-router-dom';
import { botLook } from '../../render/arenaThemes';
import type {
  MatchChallengedPayload,
  MatchSettledPayload,
  SetSettledPayload,
} from '../api';
import ToastFrame, { ToastArtwork, type ToastTone } from './ToastFrame';

type ResultPayload =
  | MatchChallengedPayload
  | MatchSettledPayload
  | SetSettledPayload;

/**
 * A loss uses the same frame, artwork, and value treatment as a win. Outcome colour is
 * confined to the small direction glyph in `ToastFrame`, never the card or its copy.
 */
export default function ResultToast({
  payload,
  queued,
  onDismiss,
}: {
  payload: ResultPayload;
  queued: number;
  onDismiss: () => void;
}) {
  const ranked = payload.kind === 'set-settled';
  const challenged = payload.kind === 'match-challenged';
  const chassis = botLook(payload.botLookId);

  return (
    <ToastFrame
      tone={resultTone(payload)}
      eyebrow={challenged ? 'Challenge' : ranked ? 'Ranked set' : 'Match complete'}
      title={
        challenged
          ? `${payload.botName} was challenged`
          : `${payload.botName} ${VERBS[payload.outcome] ?? 'drew'}`
      }
      artwork={
        <ToastArtwork accent={payload.botAccent}>
          <img
            src={chassis.imageUrl}
            alt=""
            className="size-16 object-contain sm:size-20"
          />
        </ToastArtwork>
      }
      action={
        <Link
          to={
            payload.kind === 'set-settled'
              ? `/sets/${payload.matchSetId}`
              : `/matches/${payload.matchId}`
          }
          onClick={onDismiss}
          className="btn mt-3 inline-flex items-center gap-1"
        >
          {payload.kind === 'set-settled'
            ? 'See the set'
            : challenged
              ? 'Watch it live'
              : 'Watch it back'}{' '}
          <span aria-hidden>→</span>
        </Link>
      }
      queuedLabel={queued > 0 ? `+${queued} more` : undefined}
      value={
        payload.kind === 'set-settled'
          ? `${payload.ratingChange >= 0 ? '+' : ''}${Math.round(payload.ratingChange)}`
          : undefined
      }
      dismissLabel="Dismiss result notification"
      onDismiss={onDismiss}
    >
      {payload.kind === 'match-challenged' ? (
        <p className="t-meta mt-1">
          by <span className="text-arena-text">{payload.challengerName}</span>{' '}
          on <span className="val">{payload.mapId}</span>
        </p>
      ) : payload.kind === 'set-settled' ? (
        <p className="t-meta mt-1">
          <span className="val text-arena-text">
            {payload.score}–{payload.opponentScore}
          </span>{' '}
          against {payload.opponentName}
        </p>
      ) : (
        <p className="t-meta mt-1">
          against {payload.opponentName} on{' '}
          <span className="val">{payload.mapId}</span>
        </p>
      )}
    </ToastFrame>
  );
}

function resultTone(payload: ResultPayload): ToastTone {
  if (payload.kind === 'match-challenged') return 'neutral';
  if (payload.outcome === 'Win') return 'ok';
  if (payload.outcome === 'Loss') return 'hot';
  return 'neutral';
}

const VERBS: Record<string, string> = {
  Win: 'won',
  Loss: 'lost',
  Draw: 'drew',
};
