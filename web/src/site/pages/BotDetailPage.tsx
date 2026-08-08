import { Fragment, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import ProjectilePreview from '../../components/ProjectilePreview';
import { botLook, projectileLook } from '../../render/arenaThemes';
import BotIdentity from '../components/BotIdentity';
import BotStatisticsPanel from '../components/BotStatisticsPanel';
import CurrentLadderStanding from '../components/CurrentLadderStanding';
import MatchHistory from '../components/MatchHistory';
import { ErrorState, LoadingState } from '../components/StateView';
import StatusBadge from '../components/StatusBadge';
import Th from '../components/TableHeader';
import { ApiError } from '../api';
import { useBot } from '../queries';
import { internalReturnTarget } from '../returnTarget';

export default function BotDetailPage() {
  // Slug or id — the API resolves either, so old GUID links keep working.
  const { botKey } = useParams<{ botKey: string }>();
  const location = useLocation();
  const { data: bot, error, refetch } = useBot(botKey);
  const [expanded, setExpanded] = useState<string | null>(null);
  const missing = error instanceof ApiError && error.status === 404;

  if (missing)
    return (
      <div className="panel pad">
        <p className="t-body font-semibold">No bot called “{botKey}”.</p>
        <p className="t-meta mt-1">
          It may have been renamed or never existed.{' '}
          <Link to="/archive/bots" className="text-link">
            Browse every bot
          </Link>
          .
        </p>
      </div>
    );
  // A failure that is not a 404 has to be reachable: without this branch a dead server
  // leaves `bot` undefined forever and the page sits on "Loading…" saying nothing.
  if (error) return <ErrorState error={error} onRetry={() => void refetch()} />;
  if (!bot) return <LoadingState label="Loading the bot…" />;
  const look = botLook(bot.lookId);
  const projectile = projectileLook(bot.projectileLookId);
  const returnTarget = internalReturnTarget(
    location.state,
    { to: '/archive/bots', label: 'Legacy archive' },
  );

  return (
    <div className="flex flex-col gap-3.5">
      <nav aria-label="Breadcrumb">
        <Link to={returnTarget.to} className="t-meta text-link">
          ← {returnTarget.label}
        </Link>
      </nav>
      <section className="rounded-sm border border-arena-material/50 bg-arena-material/5 px-3 py-2">
        <p className="lab text-arena-material">Legacy archive · read only</p>
        <p className="t-micro mt-1">Duel admission and submissions are retired. This record remains for historical matches and replay verification.</p>
      </section>
      {/* The hero states who this bot is and what you can do to it. Everything below
          used to sit at one weight in a single column, so a page that is mostly read
          for one thing — is the new generation better — gave that no more prominence
          than the build log. */}
      <header className="flex flex-wrap items-end justify-between gap-2.5">
        <div className="flex min-w-0 flex-col gap-2">
          <h1 className="min-w-0">
            <BotIdentity
              name={bot.name}
              accent={bot.accent}
              lookId={bot.lookId}
              size="lg"
              emphasized
              nameClassName="type-display"
            />
          </h1>
          {/* The mock carries this inside the identity chip as its sub-line, at the
              same 11.5px: chassis, then what the ladder knows about the bot. */}
          <span className="t-micro flex flex-wrap items-center gap-2.5">
            <span>
              {look.label} · {projectile.label} · by {bot.owner}
            </span>
            <ProjectilePreview
              look={projectile}
              accent={bot.accent}
              className="h-6 w-12"
            />
          </span>
        </div>
        <span className="flex flex-wrap items-center gap-2">
          <span className="pill">archived</span>
        </span>
      </header>

      <CurrentLadderStanding standing={bot.currentStanding} />

      {/* The document's `.two-up`: one column and a 340px rail, and it becomes one
          column below 900px rather than at Tailwind's 1024. */}
      <div className="grid grid-cols-[minmax(0,1fr)] gap-3">
        <div className="flex min-w-0 flex-col gap-3.5">
          <BotStatisticsPanel botId={bot.id} />

          {/* The document's table panel: the label sits in its own padded block above
              the rule, and the table in a second one under it. */}
          <section className="panel">
            <div className="pad pb-2">
              {/* "Generations", not "versions": a bot is a line, and the word the CLI
                  prints when it submits one is the word the page should use. */}
              <h2 className="lab">Generations</h2>
            </div>
            <div className="pad pt-1.5">
              {bot.versions.length === 0 && (
                <p className="t-meta">
                  No generation was submitted before Duel admission retired.
                </p>
              )}
              {bot.versions.length > 0 && (
                <table className="t-body w-full border-collapse">
                  <thead>
                    <tr>
                      <Th className="w-16">
                        Gen
                      </Th>
                      <Th>
                        Status
                      </Th>
                      <Th className="hidden sm:table-cell">
                        Artifact
                      </Th>
                      <Th numeric>
                        Submitted
                      </Th>
                    </tr>
                  </thead>
                  <tbody>
                    {bot.versions.map((version) => (
                      <Fragment key={version.id}>
                        <tr className="border-b border-arena-edge last:border-b-0">
                          <td className="tabular px-2 py-2 font-mono text-arena-text">
                            {version.versionNumber}
                            {/* Burnt orange is material rather than signal, so which
                                generation is live is a standing fact in the text
                                colour — the document's `.pill.live` — not a colour. */}
                            {version.isActive && (
                              <span className="pill ml-2 text-arena-text">live</span>
                            )}
                          </td>
                          <td className="px-2 py-2">
                            <StatusBadge status={version.status} />
                          </td>
                          <td className="val hidden px-2 py-2 sm:table-cell">
                            {version.artifactHash
                              ? `${version.artifactHash.slice(0, 14)}…`
                              : '—'}
                          </td>
                          <td className="px-2 py-2 text-right">
                            <span className="val whitespace-nowrap">
                              {new Date(version.createdAt).toLocaleDateString()}
                            </span>
                            {bot.isOwner && (version.buildLog || version.sources) && (
                              <button
                                type="button"
                                aria-expanded={expanded === version.id}
                                onClick={() =>
                                  setExpanded(
                                    expanded === version.id ? null : version.id,
                                  )
                                }
                                className="btn ml-3"
                              >
                                {expanded === version.id ? 'Hide' : 'Details'}
                              </button>
                            )}
                          </td>
                        </tr>
                        {expanded === version.id && (
                          <tr>
                            <td colSpan={4} className="px-2 pb-3">
                              <div className="flex flex-col gap-2.5">
                                {version.sources?.map((source) => (
                                  <div key={source.relativePath}>
                                    <p className="val mb-1">{source.relativePath}</p>
                                    <pre className="term max-h-64 overflow-auto whitespace-pre-wrap">
                                      {source.content}
                                    </pre>
                                  </div>
                                ))}
                                {version.buildLog && (
                                  <div>
                                    <p className="val mb-1">build log</p>
                                    <pre className="term max-h-48 overflow-auto whitespace-pre-wrap text-arena-dim">
                                      {version.buildLog}
                                    </pre>
                                  </div>
                                )}
                              </div>
                            </td>
                          </tr>
                        )}
                      </Fragment>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </section>

          <MatchHistory
            botId={bot.id}
            botSlug={bot.slug}
            botName={bot.name}
          />
        </div>

      </div>
    </div>
  );
}
