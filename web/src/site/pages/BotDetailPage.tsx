import { Fragment, useState } from 'react';
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
import { ErrorState, LoadingState } from '../components/StateView';
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
function generationHistory(bot: BotDetail): readonly GenerationRatings[] {
  const carried = (bot as BotDetail & { ratingHistory?: readonly GenerationRatings[] })
    .ratingHistory;
  return carried && carried.length > 0 ? carried : noHistory;
}

/** One array, so the chart's layout memo is not invalidated by every render. */
const noHistory: readonly GenerationRatings[] = [];

export default function BotDetailPage() {
  // Slug or id — the API resolves either, so old GUID links keep working.
  const { botKey } = useParams<{ botKey: string }>();
  const { data: bot, error, refetch } = useBot(botKey);
  const [expanded, setExpanded] = useState<string | null>(null);
  const missing = error instanceof ApiError && error.status === 404;

  if (missing)
    return (
      <div className="panel pad">
        <p className="t-body font-semibold">No bot called “{botKey}”.</p>
        <p className="t-meta mt-1">
          It may have been renamed or never existed.{' '}
          <Link to="/bots" className="text-arena-accent hover:underline">
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
  const liveVersion = bot.versions.find((version) => version.isActive) ?? null;

  return (
    <div className="flex flex-col gap-3.5">
      {/* The hero states who this bot is and what you can do to it. Everything below
          used to sit at one weight in a single column, so a page that is mostly read
          for one thing — is the new generation better — gave that no more prominence
          than the build log. */}
      <header className="flex flex-wrap items-end justify-between gap-2.5">
        <div className="flex min-w-0 flex-col gap-2">
          <BotIdentity
            name={bot.name}
            accent={bot.accent}
            lookId={bot.lookId}
            size="lg"
            emphasized
            nameClassName="type-display"
          />
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
      </header>

      <CurrentLadderStanding standing={bot.currentStanding} />

      {/* The document's `.two-up`: one column and a 340px rail, and it becomes one
          column below 900px rather than at Tailwind's 1024. */}
      <div className="grid gap-3 min-[900px]:grid-cols-[minmax(0,1fr)_340px] min-[900px]:items-start">
        <div className="flex min-w-0 flex-col gap-3.5">
          {/* First in the column because it is the question the page is opened with:
              the submit just landed, is this generation better than the last one. */}
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
                  Nothing submitted yet. <code className="val">nilbots submit</code>{' '}
                  from the bot's directory puts one here.
                </p>
              )}
              {bot.versions.length > 0 && (
                <table className="t-body w-full border-collapse">
                  <thead>
                    <tr>
                      <th scope="col" className="lab w-16 border-b border-arena-edge px-2 pb-2 text-left">
                        Gen
                      </th>
                      <th scope="col" className="lab border-b border-arena-edge px-2 pb-2 text-left">
                        Status
                      </th>
                      <th scope="col" className="lab hidden border-b border-arena-edge px-2 pb-2 text-left sm:table-cell">
                        Artifact
                      </th>
                      <th scope="col" className="lab border-b border-arena-edge px-2 pb-2 text-right">
                        Submitted
                      </th>
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

          <MatchHistory botId={bot.id} botSlug={bot.slug} />
        </div>

        {/* The rail is what you *do*, and it leads with the terminal — because that is
            where building happens, so the page's job is to hand you the right command
            for the bot you are looking at rather than to reproduce the CLI in HTML. */}
        <aside className="flex flex-col gap-3.5">
          <WorkOnThisBot />
          <ChallengePanel bot={bot} />
        </aside>
      </div>

      {bot.isOwner && (
        <div className="grid gap-3 min-[900px]:grid-cols-2 min-[900px]:items-start">
          <AppearanceEditor
            bot={bot}
            botKey={botKey!}
            entitlementRevision={
              bot.versions.filter((version) => version.status === 'Built').length
            }
          />
          <SubmitPanel bot={bot} botKey={botKey!} />
        </div>
      )}
    </div>
  );
}

/** The three commands that move a bot forward, in the order you use them. */
function WorkOnThisBot() {
  return (
    <section className="panel pad">
      <h2 className="lab mb-2">Work on this bot</h2>
      {/* `--bot` defaults to the built-in `hunter`, not the working directory, so the
          command has to name the project the way the document prints it — otherwise
          the page hands you hunter against hunter. */}
      <pre className="term">
        <Prompt />
        {'nilbots play --bot . --opponent hunter\n'}
        <Prompt />
        {'nilbots set --opponent champions\n'}
        <Prompt />
        {'nilbots submit'}
      </pre>
      <p className="t-meta mt-[11px]">
        Run them from the bot's directory. Everything above appears here when it
        builds.
      </p>
    </section>
  );
}

/** The shell prompt, dimmed so the command beside it is what reads. */
function Prompt() {
  return <span className="text-arena-dim">{'$ '}</span>;
}
