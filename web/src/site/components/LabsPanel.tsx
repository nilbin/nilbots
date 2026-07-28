import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { BotDetail } from '../api';
import { errorMessage } from '../errorMessage';
import {
  createLabsMatchRequest,
  eligibleLabsOpponents,
  eligibleLabsPlaylist,
} from '../labs';
import {
  useBots,
  useCreateLabsMatch,
  useLabsCatalog,
} from '../queries';

export default function LabsPanel({ bot }: { bot: BotDetail }) {
  const { data: catalog } = useLabsCatalog(bot.isOwner);
  const playlist = catalog
    ? eligibleLabsPlaylist(bot, catalog)
    : undefined;
  const roster = useBots(Boolean(playlist));
  const opponents = useMemo(
    () =>
      playlist
        ? eligibleLabsOpponents(
            roster.data ?? [],
            bot.id,
            playlist.requiredContractProfileId,
          )
        : [],
    [bot.id, playlist, roster.data],
  );
  const [opponentId, setOpponentId] = useState('');
  const navigate = useNavigate();
  const creation = useCreateLabsMatch();

  if (!playlist) return null;

  const selectedOpponent = opponents.some(
    (opponent) => opponent.id === opponentId,
  )
    ? opponentId
    : '';

  const startMatch = () => {
    if (!selectedOpponent) return;
    creation.mutate(
      createLabsMatchRequest(
        playlist.playlistVersionId,
        bot.id,
        selectedOpponent,
      ),
      {
        onSuccess: (match) => navigate(`/matches/${match.id}`),
      },
    );
  };

  return (
    <section
      aria-labelledby="labs-heading"
      className="flex flex-wrap items-end gap-4 rounded-xl border border-arena-edge bg-arena-panel p-5"
    >
      <div className="min-w-56 flex-1">
        <p
          id="labs-heading"
          className="font-mono text-[11px] tracking-[0.2em] text-arena-accent"
        >
          LABS · UNRANKED
        </p>
        <h2 className="mt-1 font-semibold">{playlist.displayName}</h2>
        <p className="mt-1 text-sm text-arena-dim">
          Experimental two-bot match.
        </p>
      </div>

      {roster.isPending ? (
        <p className="pb-2 text-sm text-arena-dim">Finding compatible bots…</p>
      ) : roster.error ? (
        <div className="flex items-center gap-3 pb-1 text-sm text-red-400">
          <span>{errorMessage(roster.error, 'Compatible bots could not be loaded.')}</span>
          <button
            type="button"
            onClick={() => void roster.refetch()}
            className="text-arena-accent hover:underline"
          >
            Retry
          </button>
        </div>
      ) : opponents.length === 0 ? (
        <p className="pb-2 text-sm text-arena-dim">
          No compatible opponent is active yet.
        </p>
      ) : (
        <>
          <label className="flex flex-col gap-1 text-xs text-arena-dim">
            Opponent
            <select
              aria-label="Labs opponent"
              value={selectedOpponent}
              onChange={(event) => setOpponentId(event.target.value)}
              className="rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-sm text-arena-text"
            >
              <option value="">Choose a bot…</option>
              {opponents.map((opponent) => (
                <option key={opponent.id} value={opponent.id}>
                  {opponent.name} ({opponent.owner})
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            onClick={startMatch}
            disabled={creation.isPending || !selectedOpponent}
            className="rounded-md bg-arena-accent px-5 py-2 text-sm font-bold text-slate-950 disabled:opacity-40"
          >
            {creation.isPending ? 'STARTING…' : 'RUN LAB MATCH'}
          </button>
        </>
      )}

      {creation.error && (
        <p className="w-full text-sm text-red-400">
          {errorMessage(creation.error, 'Labs match could not be started.')}
        </p>
      )}
    </section>
  );
}
