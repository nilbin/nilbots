import { useId, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { BotDetail } from '../api';
import { errorMessage } from '../errorMessage';
import {
  createLabsMatchRequest,
  eligibleLabsOpponents,
  eligibleLabsPlaylists,
} from '../labs';
import {
  useBots,
  useCreateLabsMatch,
  useLabsCatalog,
} from '../queries';

export default function LabsPanel({ bot }: { bot: BotDetail }) {
  const { data: catalog } = useLabsCatalog(bot.isOwner);
  const playlists = useMemo(
    () => (catalog ? eligibleLabsPlaylists(bot, catalog) : []),
    [bot, catalog],
  );
  const [playlistId, setPlaylistId] = useState('');
  const playlist =
    playlists.find(
      (candidate) => candidate.playlistVersionId === playlistId,
    ) ?? playlists[0];
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
  const headingId = useId();
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
      aria-labelledby={headingId}
      className="panel pad flex flex-col gap-3"
    >
      <header>
        <p
          className="lab mb-1 text-arena-accent"
        >
          Labs · unranked
        </p>
        <h2 id={headingId} className="t-body font-semibold text-arena-text">
          {playlist.displayName}
        </h2>
        <p className="t-meta mt-1">
          Experimental two-bot match. Results do not move either bot's rating.
        </p>
      </header>

      {playlists.length > 1 && (
        <label className="t-meta flex flex-col gap-1">
          Experiment
          <select
            aria-label="Labs experiment"
            value={playlist.playlistVersionId}
            onChange={(event) => {
              setPlaylistId(event.target.value);
              setOpponentId('');
              creation.reset();
            }}
            className="field"
          >
            {playlists.map((candidate) => (
              <option
                key={candidate.playlistVersionId}
                value={candidate.playlistVersionId}
              >
                {candidate.displayName}
              </option>
            ))}
          </select>
        </label>
      )}

      {roster.isPending ? (
        <p className="t-meta" role="status">
          Finding compatible bots…
        </p>
      ) : roster.error ? (
        <div className="flex flex-wrap items-center gap-2" role="alert">
          <p className="t-meta min-w-0 grow text-arena-hot">
            {errorMessage(roster.error, 'Compatible bots could not be loaded.')}
          </p>
          <button
            type="button"
            onClick={() => void roster.refetch()}
            className="btn"
          >
            Try again
          </button>
        </div>
      ) : opponents.length === 0 ? (
        <p className="t-meta">
          No compatible opponent is active yet.
        </p>
      ) : (
        <div className="flex flex-col gap-2">
          <label className="t-meta flex flex-col gap-1">
            Opponent
            <select
              aria-label="Labs opponent"
              value={selectedOpponent}
              onChange={(event) => setOpponentId(event.target.value)}
              className="field"
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
            className="btn btn-on self-start"
          >
            {creation.isPending ? 'Starting…' : 'Run lab match'}
          </button>
        </div>
      )}

      {creation.error && (
        <p className="t-meta text-arena-hot" role="alert">
          {errorMessage(creation.error, 'Labs match could not be started.')}
        </p>
      )}
    </section>
  );
}
