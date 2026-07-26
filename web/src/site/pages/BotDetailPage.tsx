import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import ProjectilePreview from '../../components/ProjectilePreview';
import { botLook, projectileLook } from '../../render/arenaThemes';
import AppearanceEditor from '../components/AppearanceEditor';
import BotIdentity from '../components/BotIdentity';
import BotStatisticsPanel from '../components/BotStatisticsPanel';
import ChallengePanel from '../components/ChallengePanel';
import CurrentLadderStanding from '../components/CurrentLadderStanding';
import MatchHistory from '../components/MatchHistory';
import StatusBadge from '../components/StatusBadge';
import SubmitPanel from '../components/SubmitPanel';
import { ApiError } from '../api';
import { useBot } from '../queries';


export default function BotDetailPage() {
  // Slug or id — the API resolves either, so old GUID links keep working.
  const { botKey } = useParams<{ botKey: string }>();
  const { data: bot, error } = useBot(botKey);
  const [expanded, setExpanded] = useState<string | null>(null);
  const missing = error instanceof ApiError && error.status === 404;

  if (missing)
    return (
      <div className="rounded-xl border border-arena-edge bg-arena-panel p-6">
        <p className="font-semibold">No bot called “{botKey}”.</p>
        <p className="mt-1 text-sm text-arena-dim">
          It may have been renamed or never existed.{' '}
          <Link to="/bots" className="text-arena-accent hover:underline">
            Browse every bot
          </Link>
          .
        </p>
      </div>
    );
  if (!bot) return <p className="text-sm text-arena-dim">Loading…</p>;
  const look = botLook(bot.lookId);
  const projectile = projectileLook(bot.projectileLookId);

  return (
    <div className="flex flex-col gap-8">
      <header className="flex flex-wrap items-center gap-3">
        <BotIdentity
          name={bot.name}
          accent={bot.accent}
          lookId={bot.lookId}
          size="lg"
          nameClassName="font-black tracking-wide"
        />
        <span className="flex flex-wrap items-center gap-3">
          <span className="text-sm text-arena-dim">
            {look.label} · {projectile.label} · by {bot.owner}
          </span>
          <ProjectilePreview
            look={projectile}
            accent={bot.accent}
            className="h-7 w-14"
          />
        </span>
      </header>

      <CurrentLadderStanding standing={bot.currentStanding} />

      <BotStatisticsPanel botId={bot.id} />

      {bot.isOwner && (
        <AppearanceEditor
          bot={bot}
          botKey={botKey!}
          entitlementRevision={
            bot.versions.filter((version) => version.status === 'Built').length
          }
        />
      )}

      <ChallengePanel bot={bot} />

      <section>
        <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim">VERSIONS</h2>
        {bot.versions.length === 0 && (
          <p className="text-sm text-arena-dim">No versions submitted yet.</p>
        )}
        <ul className="flex flex-col gap-2">
          {bot.versions.map((version) => (
            <li key={version.id} className="rounded-lg border border-arena-edge bg-arena-panel/60 p-4">
              <div className="flex flex-wrap items-center gap-3">
                <span className="font-semibold">v{version.versionNumber}</span>
                <StatusBadge status={version.status} />
                {version.isActive && (
                  <span className="rounded bg-arena-accent/15 px-2 py-0.5 font-mono text-[11px] text-arena-accent">
                    ACTIVE
                  </span>
                )}
                {version.artifactHash && (
                  <span className="font-mono text-[11px] text-arena-dim">
                    {version.artifactHash.slice(0, 14)}…
                  </span>
                )}
                <span className="ml-auto font-mono text-[11px] text-arena-dim">
                  {new Date(version.createdAt).toLocaleString()}
                </span>
                {bot.isOwner && (version.buildLog || version.sources) && (
                  <button
                    onClick={() => setExpanded(expanded === version.id ? null : version.id)}
                    className="rounded border border-arena-edge px-2 py-0.5 text-xs text-arena-dim hover:text-arena-text"
                  >
                    {expanded === version.id ? 'Hide details' : 'Details'}
                  </button>
                )}
              </div>
              {expanded === version.id && (
                <div className="mt-3 flex flex-col gap-3">
                  {version.sources?.map((source) => (
                    <div key={source.relativePath}>
                      <p className="mb-1 font-mono text-[11px] text-arena-dim">{source.relativePath}</p>
                      <pre className="max-h-64 overflow-auto rounded bg-arena-bg p-3 font-mono text-xs whitespace-pre-wrap">
                        {source.content}
                      </pre>
                    </div>
                  ))}
                  {version.buildLog && (
                    <div>
                      <p className="mb-1 font-mono text-[11px] text-arena-dim">build log</p>
                      <pre className="max-h-48 overflow-auto rounded bg-arena-bg p-3 font-mono text-[11px] whitespace-pre-wrap text-arena-dim">
                        {version.buildLog}
                      </pre>
                    </div>
                  )}
                </div>
              )}
            </li>
          ))}
        </ul>
      </section>

      <MatchHistory botId={bot.id} botSlug={bot.slug} />

      {bot.isOwner && <SubmitPanel bot={bot} botKey={botKey!} />}
    </div>
  );
}
