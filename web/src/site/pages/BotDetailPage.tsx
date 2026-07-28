import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import ProjectilePreview from '../../components/ProjectilePreview';
import { botLook, projectileLook } from '../../render/arenaThemes';
import AppearanceEditor from '../components/AppearanceEditor';
import BotIdentity from '../components/BotIdentity';
import BotStatisticsPanel from '../components/BotStatisticsPanel';
import ChallengePanel from '../components/ChallengePanel';
import CurrentLadderStanding from '../components/CurrentLadderStanding';
import GenerationsChart, {
  type GenerationRatings,
} from '../components/GenerationsChart';
import MatchHistory from '../components/MatchHistory';
import StatusBadge from '../components/StatusBadge';
import SubmitPanel from '../components/SubmitPanel';
import { ApiError, type BotDetail } from '../api';
import { useBot } from '../queries';

/**
 * Each generation's ratings, oldest generation first.
 *
 * Always empty today, and deliberately: nothing on the server records a rating per
 * generation. `currentStanding` is one number for the bot as a whole, and a version row
 * knows when it was submitted and nothing about how it did — so a line drawn from what
 * exists would be a picture of an improvement nobody measured. The chart is built for
 * the series and draws it the day an endpoint returns one; this is where it arrives.
 */
function generationHistory(_bot: BotDetail): readonly GenerationRatings[] {
  return noHistory;
}

/** One array, so the chart's layout memo is not invalidated by every render. */
const noHistory: readonly GenerationRatings[] = [];

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
  const liveVersion = bot.versions.find((version) => version.isActive) ?? null;

  return (
    <div className="flex flex-col gap-8">
      {/* The hero states who this bot is and what you can do to it. Everything below
          used to sit at one weight in a single column, so a page that is mostly read
          for one thing — is the new generation better — gave that no more prominence
          than the build log. */}
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex min-w-0 flex-col gap-2">
          <BotIdentity
            name={bot.name}
            accent={bot.accent}
            lookId={bot.lookId}
            size="lg"
            emphasized
            nameClassName="type-display"
          />
          <span className="flex flex-wrap items-center gap-3 text-[13px] text-arena-dim">
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
      </header>

      <CurrentLadderStanding standing={bot.currentStanding} />

      <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_320px] lg:items-start">
        <div className="flex min-w-0 flex-col gap-8">
      {/* First in the column because it is the question the page is opened with: the
          submit just landed, is this generation better than the last one. */}
      <GenerationsChart
        series={generationHistory(bot)}
        accent={bot.accent}
        liveGeneration={liveVersion?.versionNumber ?? null}
        note={
          liveVersion
            ? `submitted ${new Date(liveVersion.createdAt).toLocaleDateString()}`
            : null
        }
        emptyTitle={
          bot.versions.length === 0
            ? 'Nothing submitted yet'
            : 'No rating per generation yet'
        }
        emptyDetail={
          bot.versions.length === 0
            ? 'The first generation appears here once nilbots submit has built one.'
            : 'The ladder records a rating for the bot, not for the generation that earned it, so there is no series to draw against these generations.'
        }
      />

      <BotStatisticsPanel botId={bot.id} />

      <section>
        {/* "Generations", not "versions": a bot is a line, and the word the CLI prints
            when it submits one is the word the page should use. */}
        <h2 className="type-label mb-3 text-[10.5px] text-arena-dim">
          Generations
        </h2>
        {bot.versions.length === 0 && (
          <p className="text-sm text-arena-dim">
            Nothing submitted yet. <code className="font-mono">nilbots submit</code>{' '}
            from the bot's directory puts one here.
          </p>
        )}
        {bot.versions.length > 0 && (
          <table className="w-full border-collapse text-[13px]">
            <thead>
              <tr>
                <th scope="col" className="type-label w-16 border-b border-arena-edge px-2 pb-2 text-left text-[10px] font-normal text-arena-dim">
                  Gen
                </th>
                <th scope="col" className="type-label border-b border-arena-edge px-2 pb-2 text-left text-[10px] font-normal text-arena-dim">
                  Status
                </th>
                <th scope="col" className="type-label hidden border-b border-arena-edge px-2 pb-2 text-left text-[10px] font-normal text-arena-dim sm:table-cell">
                  Artifact
                </th>
                <th scope="col" className="type-label border-b border-arena-edge px-2 pb-2 text-right text-[10px] font-normal text-arena-dim">
                  Submitted
                </th>
              </tr>
            </thead>
            <tbody>
              {bot.versions.map((version) => (
                <>
                  <tr
                    key={version.id}
                    className="border-b border-arena-edge last:border-b-0"
                  >
                    <td className="tabular px-2 py-2 font-mono text-arena-text">
                      {version.versionNumber}
                      {version.isActive && (
                        <span className="type-label ml-2 text-[9px] text-arena-accent">
                          live
                        </span>
                      )}
                    </td>
                    <td className="px-2 py-2">
                      <StatusBadge status={version.status} />
                    </td>
                    <td className="hidden px-2 py-2 font-mono text-[11px] text-arena-dim sm:table-cell">
                      {version.artifactHash
                        ? `${version.artifactHash.slice(0, 14)}…`
                        : '—'}
                    </td>
                    <td className="px-2 py-2 text-right text-[12px] text-arena-dim">
                      {new Date(version.createdAt).toLocaleDateString()}
                      {bot.isOwner && (version.buildLog || version.sources) && (
                        <button
                          onClick={() =>
                            setExpanded(
                              expanded === version.id ? null : version.id,
                            )
                          }
                          className="type-label ml-3 rounded border border-arena-edge px-1.5 py-0.5 text-[9px] text-arena-dim transition-colors hover:text-arena-text"
                        >
                          {expanded === version.id ? 'Hide' : 'Details'}
                        </button>
                      )}
                    </td>
                  </tr>
                  {expanded === version.id && (
                    <tr key={`${version.id}-detail`}>
                      <td colSpan={4} className="px-2 pb-3">
                        <div className="flex flex-col gap-3">
                          {version.sources?.map((source) => (
                            <div key={source.relativePath}>
                              <p className="mb-1 font-mono text-[11px] text-arena-dim">
                                {source.relativePath}
                              </p>
                              <pre className="max-h-64 overflow-auto rounded border border-arena-edge bg-arena-bg p-3 font-mono text-xs whitespace-pre-wrap">
                                {source.content}
                              </pre>
                            </div>
                          ))}
                          {version.buildLog && (
                            <div>
                              <p className="mb-1 font-mono text-[11px] text-arena-dim">
                                build log
                              </p>
                              <pre className="max-h-48 overflow-auto rounded border border-arena-edge bg-arena-bg p-3 font-mono text-[11px] whitespace-pre-wrap text-arena-dim">
                                {version.buildLog}
                              </pre>
                            </div>
                          )}
                        </div>
                      </td>
                    </tr>
                  )}
                </>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <MatchHistory botId={bot.id} botSlug={bot.slug} />
        </div>

        {/* The rail is what you *do*, and it leads with the terminal — because that is
            where building happens, so the page's job is to hand you the right command
            for the bot you are looking at rather than to reproduce the CLI in HTML. */}
        <aside className="flex flex-col gap-6">
          <WorkOnThisBot />
          <ChallengePanel bot={bot} />
          {bot.isOwner && (
            <AppearanceEditor
              bot={bot}
              botKey={botKey!}
              entitlementRevision={
                bot.versions.filter((version) => version.status === 'Built')
                  .length
              }
            />
          )}
          {bot.isOwner && <SubmitPanel bot={bot} botKey={botKey!} />}
        </aside>
      </div>
    </div>
  );
}

/** The three commands that move a bot forward, in the order you use them. */
function WorkOnThisBot() {
  return (
    <section className="rounded-lg border border-arena-edge bg-arena-panel/60 p-4">
      <h2 className="type-label mb-2.5 text-[10px] text-arena-dim">
        Work on this bot
      </h2>
      <pre className="overflow-x-auto rounded border border-arena-edge bg-arena-bg p-3 font-mono text-[12px] leading-[1.9] text-arena-text">
        <span className="text-arena-dim">$</span> nilbots play --opponent hunter{'\n'}
        <span className="text-arena-dim">$</span> nilbots set --opponent champions{'\n'}
        <span className="text-arena-dim">$</span> nilbots submit
      </pre>
      <p className="mt-2.5 text-[12.5px] text-arena-dim">
        Run them from the bot's directory. Everything above appears here when it
        builds.
      </p>
    </section>
  );
}
