import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { type BotDetail } from '../api';
import {
  useBots,
  useChallenge,
  useMeta,
  useMyBots,
  useRankedChallenge,
} from '../queries';
import { errorMessage } from '../errorMessage';

export default function ChallengePanel({ bot }: { bot: BotDetail }) {
  // The roster was fetched twice here — once for everyone, once to intersect with mine.
  // One query, shared with every other consumer, and the intersection is derived.
  const { data: roster } = useBots();
  const { data: mine } = useMyBots(bot.isOwner);
  const { data: meta = null } = useMeta();
  const allBots = useMemo(
    () => (roster ?? []).filter((candidate) => candidate.activeVersion),
    [roster],
  );
  const myBots = useMemo(
    () => allBots.filter((candidate) => (mine ?? []).some((owned) => owned.id === candidate.id)),
    [allBots, mine],
  );
  const [opponentId, setOpponentId] = useState('');
  const [challengerId, setChallengerId] = useState('');
  const [mapId, setMapId] = useState('arena-01');
  const [ranked, setRanked] = useState(false);
  const navigate = useNavigate();
  const challenge = useChallenge();
  const rankedChallenge = useRankedChallenge();
  const failure = challenge.error ?? rankedChallenge.error;
  const busy = challenge.isPending || rankedChallenge.isPending;

  const isMine = bot.isOwner && bot.versions.some((v) => v.status === 'Built');
  // On my own bot page: challenge others with this bot. On another bot's page:
  // challenge it with one of my bots.
  const challenger = isMine ? bot.id : challengerId;
  const opponent = isMine ? opponentId : bot.id;
  const selectable = isMine
    ? allBots.filter((b) => b.id !== bot.id)
    : myBots.filter((b) => b.id !== bot.id);

  if (selectable.length === 0 || (!isMine && myBots.length === 0)) return null;

  const fight = async () => {
    if (ranked) {
      // No opponent: the server matchmakes by rating (DECISIONS #95).
      const set = await rankedChallenge.mutateAsync({ botId: challenger });
      navigate(`/sets/${set.id}`);
    } else {
      const match = await challenge.mutateAsync({
        botId: challenger,
        opponentBotId: opponent,
        mapId,
        // Explicitly unspecified, so the server picks. The untyped post omitted this
        // field entirely and nothing noticed — same result, no way to know it was meant.
        seed: null,
      });
      navigate(`/matches/${match.id}`);
    }
  };

  return (
    <section className="flex flex-wrap items-end gap-3 rounded-xl border border-arena-edge bg-arena-panel p-5">
      <div className="flex flex-col gap-1 text-xs text-arena-dim">
        {isMine ? (ranked ? 'Opponent (matchmade)' : 'Opponent') : 'Challenge with'}
        <select
          value={isMine ? opponentId : challengerId}
          disabled={isMine && ranked}
          onChange={(e) => (isMine ? setOpponentId(e.target.value) : setChallengerId(e.target.value))}
          className="rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-sm text-arena-text disabled:opacity-40"
        >
          <option value="">Choose a bot…</option>
          {selectable.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name} ({b.owner})
            </option>
          ))}
        </select>
      </div>
      <div className="flex flex-col gap-1 text-xs text-arena-dim">
        Map
        <select
          value={mapId}
          onChange={(e) => setMapId(e.target.value)}
          className="rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-sm text-arena-text"
        >
          {(meta?.maps ?? []).map((m) => (
            <option key={m.id} value={m.id}>
              {m.id} ({m.width}×{m.height})
            </option>
          ))}
        </select>
      </div>
      {isMine && (
        <label className="flex cursor-pointer items-center gap-2 pb-2 text-xs text-arena-dim select-none">
          <input
            type="checkbox"
            checked={ranked}
            onChange={(e) => setRanked(e.target.checked)}
            className="accent-(--color-arena-accent)"
          />
          Ranked set (6 games, mirrored starts, moves elo — opponent matchmade)
        </label>
      )}
      <button
        onClick={() => void fight()}
        disabled={busy || !challenger || (!ranked && !opponent)}
        className="rounded-md bg-arena-accent px-5 py-2 text-sm font-bold text-arena-bg disabled:opacity-40"
      >
        {busy ? 'SENDING…' : ranked ? 'FIGHT FOR RATING' : 'FIGHT'}
      </button>
      {failure && (
        <p className="w-full text-sm text-arena-hot">
          {errorMessage(failure, 'Challenge failed.')}
        </p>
      )}
    </section>
  );
}
