import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { botLook } from '../../render/arenaThemes';
import { api, type BotSummary } from '../api';

export default function BotsPage() {
  const [bots, setBots] = useState<BotSummary[] | null>(null);

  useEffect(() => {
    void api.get<BotSummary[]>('/api/bots').then(setBots);
  }, []);

  return (
    <div>
      <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim">ALL BOTS</h2>
      {bots === null ? (
        <p className="text-sm text-arena-dim">Loading…</p>
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {bots.map((bot) => {
            const look = botLook(bot.lookId);
            return (
              <li key={bot.id}>
                <Link
                  to={`/bots/${bot.id}`}
                  className="flex flex-col gap-1 rounded-lg border border-arena-edge bg-arena-panel/60 p-4 transition-colors hover:border-arena-dim"
                >
                  <span className="flex items-center gap-2 font-semibold">
                    <img src={look.imageUrl} alt="" className="size-8 object-contain" />
                    {bot.name}
                  </span>
                <span className="text-xs text-arena-dim">
                  {look.label} · by {bot.owner}
                  {bot.ratings
                    .filter((ladder) => ladder.rankedSets > 0)
                    .map((ladder) => (
                      <span key={ladder.rulesVersion} className="ml-2 font-mono text-arena-accent">
                        {ladder.rating} elo
                        <span className="text-arena-dim"> @{ladder.rulesVersion}</span>
                      </span>
                    ))}
                </span>
                <span className="font-mono text-[11px] text-arena-dim">
                  {bot.activeVersion
                    ? `v${bot.activeVersion.versionNumber} active · ${bot.activeVersion.artifactHash?.slice(0, 10)}…`
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
