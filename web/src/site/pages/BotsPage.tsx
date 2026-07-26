import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { botLook } from '../../render/arenaThemes';
import BotIdentity from '../components/BotIdentity';
import { EmptyState, ErrorState, LoadingState } from '../components/StateView';
import { api, type BotSummary, type Meta } from '../api';

export default function BotsPage() {
  const [bots, setBots] = useState<BotSummary[] | null>(null);
  const [meta, setMeta] = useState<Meta | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [reloads, setReloads] = useState(0);
  const [query, setQuery] = useState('');
  const [ratedOnly, setRatedOnly] = useState(false);

  useEffect(() => {
    setError(null);
    // A rejection has to land somewhere: without this, `bots` stays null and the page
    // reads "Loading…" indefinitely with nothing to act on.
    void api.get<BotSummary[]>('/api/bots').then(setBots).catch(setError);
    // Meta is decoration here — a failure to load it should not take the roster down.
    void api.get<Meta>('/api/meta').then(setMeta).catch(() => undefined);
  }, [reloads]);

  // The roster passed thirty bots without anything to narrow it by, which is the point
  // where a grid stops being browsable (UI audit). Name and owner are what people
  // actually look for, and both are already on the card.
  const shown = useMemo(() => {
    if (bots === null) return null;
    const needle = query.trim().toLowerCase();
    return bots.filter((bot) => {
      if (ratedOnly && !bot.ratings.some((r) => r.rankedSets > 0)) return false;
      if (needle === '') return true;
      return (
        bot.name.toLowerCase().includes(needle) || bot.owner.toLowerCase().includes(needle)
      );
    });
  }, [bots, query, ratedOnly]);

  return (
    <div>
      <div className="mb-3 flex flex-wrap items-center gap-3">
        <h2 className="font-mono text-xs tracking-widest text-arena-dim">ALL BOTS</h2>
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Filter by bot or owner…"
          aria-label="Filter bots by name or owner"
          className="min-w-52 flex-1 rounded-md border border-arena-edge bg-arena-bg px-3 py-1.5 text-sm text-arena-text placeholder:text-arena-dim"
        />
        <label className="flex cursor-pointer items-center gap-2 text-xs text-arena-dim select-none">
          <input
            type="checkbox"
            checked={ratedOnly}
            onChange={(e) => setRatedOnly(e.target.checked)}
            className="accent-(--color-arena-accent)"
          />
          Ranked only
        </label>
        {shown !== null && bots !== null && (
          <span className="font-mono text-xs text-arena-dim">
            {shown.length}/{bots.length}
          </span>
        )}
      </div>
      {error !== null ? (
        <ErrorState error={error} onRetry={() => setReloads((n) => n + 1)} />
      ) : shown === null ? (
        <LoadingState />
      ) : shown.length === 0 ? (
        <EmptyState
          title={query ? `No bot matches “${query}”.` : 'No bots yet'}
          detail={query ? undefined : 'Bots appear here once they are submitted.'}
        />
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {shown.map((bot) => {
            const look = botLook(bot.lookId);
            // Experiment ladders are research data, not standings a visitor should read
            // as another game they could be playing (GameRules.ShippedNames, #97).
            const ladders = bot.ratings.filter(
              (ladder) =>
                ladder.rankedSets > 0 &&
                (meta === null ||
                  ladder.rulesVersion === meta.gameRulesVersion ||
                  !ladder.rulesVersion.includes('-exp-')),
            );
            return (
              <li key={bot.id}>
                <Link
                  to={`/bots/${bot.slug}`}
                  className="flex flex-col gap-1 rounded-lg border border-arena-edge bg-arena-panel/60 p-4 transition-colors hover:border-arena-dim"
                >
                  <BotIdentity
                    name={bot.name}
                    accent={bot.accent}
                    lookId={bot.lookId}
                    size="sm"
                    emphasized
                  />
                  <span className="text-xs text-arena-dim">
                    {look.label} · by {bot.owner}
                    {ladders.map((ladder) => (
                      <span key={ladder.rulesVersion} className="ml-2 font-mono text-arena-accent">
                        {ladder.rating} elo
                        <span className="text-arena-dim"> @{ladder.rulesVersion}</span>
                      </span>
                    ))}
                  </span>
                  {/* The artifact hash used to sit here, truncated. It is a verification
                      aid, not a way to tell two bots apart at a glance — it belongs on the
                      bot's own page, where the full value is shown. */}
                  <span className="font-mono text-[11px] text-arena-dim">
                    {bot.activeVersion
                      ? `v${bot.activeVersion.versionNumber} active · ${bot.versionCount} version${bot.versionCount === 1 ? '' : 's'}`
                      : 'no active version yet'}
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
