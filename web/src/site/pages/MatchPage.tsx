import type { ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import clsx from 'clsx';
import Viewer from '../../components/Viewer';
import BotIdentity from '../components/BotIdentity';
import { ErrorState } from '../components/StateView';
import {
  ApiError,
  type MatchDetail,
  type MatchDetailParticipant,
  type MatchLive,
  type MatchSetDetail,
} from '../api';
import { useMatch, useMatchLive, useMatchReplay, useMatchSet } from '../queries';

/**
 * One match: the arena, what it counted for, and the receipt.
 *
 * **The redaction boundary is the layout.** `MatchPublicProjection.BroadcastSafe` withholds
 * exactly six things until a broadcast completes — `winnerSlot`, `endReason`, `endTick`,
 * `replayHash` and each participant's `outcome`/`finalHealth`/`damageDealt`/`faults`.
 * Everything else — map, seed, both artifact hashes, who is fighting, when it ran — is
 * public from the moment the match exists. That split is not an implementation detail to
 * route around; it *is* the product's claim, so the page is built as the two sides of it:
 * **Record** (the inputs, always there) and **Result** (what watching reveals). Nothing
 * else on the page needs a "hidden" branch.
 *
 * The `<Viewer>` owns its own header, provenance disclosure, transport and event index, so
 * the page draws no second header over it. What the page adds is the two things the viewer
 * structurally cannot know — that this game sits inside a set, and that the server settled
 * a ledger — plus a permanent, linkable, copyable record, because the viewer's `Verify`
 * disclosure lives inside a player that goes full screen and ships standalone from the CLI
 * where there is no page at all.
 */
export default function MatchPage() {
  const { matchId } = useParams<{ matchId: string }>();
  // Three queries, not one loop: the clock says where the broadcast is, the replay follows
  // it, and the detail carries the ledger. Each stops on its own condition.
  //
  // The detail runs *always* rather than only for a failed match: map, seed and both
  // artifact hashes are public from the moment the match exists, and the whole argument of
  // this page is that they are published before anyone knows the result.
  const { data: live, error: liveError, refetch } = useMatchLive(matchId);
  const { data: loadedReplay } = useMatchReplay(matchId, live);
  const { data: detail } = useMatch(matchId);
  const replay = loadedReplay?.replay;

  // Either response names the set, so the standing strip never waits on the slower one.
  const matchSetId = live?.matchSetId ?? detail?.matchSetId ?? null;
  const setGame = live?.setGame ?? detail?.setGame ?? null;
  const { data: set } = useMatchSet(matchSetId ?? undefined);

  const missing = liveError instanceof ApiError && liveError.status === 404;
  const failed = live?.status === 'Failed' || detail?.status === 'Failed';
  const finished = live?.broadcastComplete ?? false;

  // A mistyped id is an answer, not an alarm — so it renders as an empty shape with a way
  // back, rather than as the red state a dead server deserves.
  if (missing)
    return (
      <div className="mx-auto max-w-6xl py-10 text-center">
        <p className="t-body font-semibold text-arena-dim">No such match</p>
        <p className="t-micro mt-1">
          This match id does not exist.{' '}
          <Link to="/watch" className="text-arena-accent hover:underline">
            Back to Watch
          </Link>
          .
        </p>
      </div>
    );

  // A transport failure is the other kind: the server is unreachable rather than the match
  // absent, and the answer to that is a retry.
  if (liveError !== null) return <ErrorState error={liveError} onRetry={() => void refetch()} />;

  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-3">
      <Standing
        matchSetId={matchSetId}
        setGame={setGame}
        matchId={matchId}
        set={set}
        live={live}
        failed={failed}
      />

      {failed ? (
        <DidNotRun error={detail?.error ?? null} ranked={matchSetId !== null} />
      ) : (
        <Arena
          matchId={matchId}
          live={live}
          replayTicks={replay?.ticks.length ?? 0}
          finished={finished}
        >
          {replay && (
            <Viewer
              replay={replay}
              soundtrackPresentationId={matchId}
              live={
                finished
                  ? undefined
                  : {
                      tick: live?.presentationTick ?? 0,
                      ticksPerSecond: live?.presentationTicksPerSecond ?? 1,
                    }
              }
            />
          )}
        </Arena>
      )}

      {/* Result first, Record second: a reader arriving from a `match-settled` toast wants
          the outcome, and the receipt is what they check next. */}
      <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_330px]">
        <Result detail={detail} failed={failed} />
        <Record detail={detail} />
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ the standing ---
   The one line the viewer cannot know: that this game sits inside a set, and where. */

function Standing({
  matchSetId,
  setGame,
  matchId,
  set,
  live,
  failed,
}: {
  matchSetId: string | null;
  setGame: number | null;
  matchId: string | undefined;
  set: MatchSetDetail | undefined;
  live: MatchLive | undefined;
  failed: boolean;
}) {
  // An unranked match still renders the strip: "this changed nothing on the ladder" is
  // information, and its absence would read as a page that had not finished loading.
  if (matchSetId === null)
    return (
      <div className="panel-quiet pad flex flex-wrap items-center gap-x-3 gap-y-2">
        <p className="lab">Unranked challenge</p>
        <span className="t-micro">No rating moves on this one.</span>
        <StandingPill live={live} failed={failed} />
      </div>
    );

  // Pair is `ceil(game / 2)` — the index the server assigned, never the map. The total
  // needs the set, so the line names the pair immediately and gains "of 3" when it lands.
  const pair = setGame === null ? null : Math.ceil(setGame / GAMES_PER_PAIR);
  const pairs =
    set === undefined
      ? null
      : Math.ceil(
          Math.max(...set.games.map((game, index) => game.game ?? index + 1), 0) /
            GAMES_PER_PAIR,
        );

  return (
    <div className="panel-quiet pad flex flex-wrap items-center gap-x-3 gap-y-2">
      {/* The strip is a way back to the set, but the game chips inside it are links of
          their own, and a link cannot nest — so the label carries the navigation. */}
      <Link to={`/sets/${matchSetId}`} className="lab hover:text-arena-text">
        Ranked set
        {pair !== null && ` · Pair ${pair}${pairs === null ? '' : ` of ${pairs}`}`}
        {setGame !== null && ` · Game ${setGame}`} ↗
      </Link>
      {set !== undefined && set.games.length > 0 && (
        <nav className="flex flex-wrap items-center gap-1" aria-label="Games in this set">
          {set.games.map((game, index) => {
            const number = game.game ?? index + 1;
            const current = game.id === matchId;
            return (
              <Link
                key={game.id}
                to={`/matches/${game.id}`}
                aria-current={current ? 'page' : undefined}
                aria-label={`Game ${number}`}
                className={clsx(
                  'btn tabular w-7 px-0 text-center',
                  current && 'btn-on',
                )}
              >
                {number}
              </Link>
            );
          })}
        </nav>
      )}
      <StandingPill live={live} failed={failed} />
    </div>
  );
}

function StandingPill({ live, failed }: { live: MatchLive | undefined; failed: boolean }) {
  if (failed)
    return <span className="pill ml-auto text-arena-hot">Did not run</span>;
  if (live === undefined) return null;
  if (live.status === 'Pending') return <span className="pill ml-auto">Queued</span>;
  if (live.status === 'Running') return <span className="pill ml-auto">Fighting</span>;
  if (!live.broadcastComplete)
    return (
      <span className="pill ml-auto inline-flex items-center gap-1.5 text-arena-hot">
        <span className="inline-block size-1.5 animate-pulse rounded-full bg-arena-hot" />
        Live
      </span>
    );
  return <span className="pill ml-auto">Decided</span>;
}

/* ---------------------------------------------------------------------- the arena ---
   The tallest element, and the only one that changes shape while you watch. */

function Arena({
  matchId,
  live,
  replayTicks,
  finished,
  children,
}: {
  matchId: string | undefined;
  live: MatchLive | undefined;
  replayTicks: number;
  finished: boolean;
  children: ReactNode;
}) {
  const phase = waitingPhase(live, replayTicks, finished);
  if (phase !== null) return <Phase {...phase} />;
  // A definite height, because the viewer sizes itself with `h-full`. In landscape on a
  // coarse pointer it goes immersive and takes the viewport instead, which is why nothing
  // essential lives only above the arena.
  return (
    <div key={matchId} className="h-[min(72dvh,760px)] min-h-[460px]">
      {children}
    </div>
  );
}

/**
 * Loading here is four different things, and the page should say which.
 *
 * A spinner claims the *page* is working; on this screen the *server* is, and the four
 * waits are genuinely different — a queue, a fight nobody has seen, a countdown before
 * anyone may see it, and a download. Each gets the phase name, one plain sentence, and a
 * mono count where one exists.
 */
function waitingPhase(
  live: MatchLive | undefined,
  replayTicks: number,
  finished: boolean,
): { label: string; line: string; value?: string } | null {
  if (live === undefined)
    return { label: 'Reading the match', line: 'Asking the server where this one is.' };
  if (live.status === 'Pending')
    return { label: 'Queued', line: 'Waiting for a match worker.' };
  if (live.status === 'Running')
    return {
      label: 'Fighting',
      line: 'The bots are playing it out. No replay exists yet.',
    };
  if (!finished && live.countdownMs > 0)
    return {
      label: 'Broadcast starts in',
      // The server's clock, read on the live poll rather than counted down locally: a
      // second timer would drift away from the tick everyone else is being shown.
      value: `${Math.ceil(live.countdownMs / 1000)}s`,
      line: 'Nobody has seen it yet.',
    };
  if (replayTicks === 0) return { label: 'Loading replay', line: 'Fetching the record.' };
  return null;
}

function Phase({ label, line, value }: { label: string; line: string; value?: string }) {
  return (
    <div
      className="panel pad flex min-h-[240px] flex-col items-center justify-center gap-2 text-center"
      role="status"
    >
      <p className="lab">{label}</p>
      {value !== undefined && <p className="val text-arena-text">{value}</p>}
      <p className="t-body max-w-[46ch] text-arena-dim">{line}</p>
    </div>
  );
}

function DidNotRun({ error, ranked }: { error: string | null; ranked: boolean }) {
  return (
    <div className="panel pad flex flex-col gap-2.5">
      <p className="lab text-arena-hot">Did not run</p>
      {/* A machine wrote this, so it is shown as the machine wrote it. */}
      {error !== null && <pre className="term">{error}</pre>}
      <p className="t-body text-arena-dim">
        {ranked
          ? 'No rating changed — a set only settles when all six games complete.'
          : 'No rating changed, and no replay exists: this match never reached a conclusion.'}
      </p>
    </div>
  );
}

/* --------------------------------------------------------------------- the result ---
   What watching reveals: the ledger the server settled, and the hash that pins it. */

function Result({ detail, failed }: { detail: MatchDetail | undefined; failed: boolean }) {
  return (
    <section className="panel pad flex flex-col gap-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="lab">Result</h2>
        {detail !== undefined && detail.endReason !== null && (
          <span className="val text-arena-text">
            {detail.endReason.toLowerCase()}
            {detail.endTick !== null && ` · ${String(detail.endTick).padStart(3, '0')}`}
          </span>
        )}
      </div>

      {detail === undefined ? (
        <p className="t-meta">Reading the ledger…</p>
      ) : failed ? (
        <p className="t-body text-arena-dim">
          There is no result. The match failed before it reached an ending, so nothing was
          scored and nothing was rated.
        </p>
      ) : !revealed(detail) ? (
        // The one empty state that has to read as intent. It is a literal description of
        // `BroadcastSafe`, which turns the product's least obvious invariant into the
        // page's most reassuring sentence.
        <div className="flex flex-col gap-1.5">
          <p className="t-body">
            <b>Held until the broadcast finishes.</b>
          </p>
          <p className="t-body max-w-[58ch] text-arena-dim">
            Every viewer of this match sees the outcome at the same tick you do. The inputs
            are already public — map, seed, and both artifact hashes are in the record.
          </p>
        </div>
      ) : (
        <>
          {/* Below sm a table stops earning its head: it is scanned down two hundred rows
              on the ladder and paid for there; the body here is two. Same rows, stacked. */}
          <div className="flex flex-col gap-1.5 sm:hidden">
            {detail.participants.map((participant) => (
              <div key={participant.slot} className="panel-quiet pad flex flex-col gap-1.5">
                <div className="flex items-center justify-between gap-2">
                  <BotLink participant={participant} winner={isWinner(detail, participant)} />
                  <span className="val text-arena-text">
                    {participant.outcome?.toLowerCase() ?? '—'}
                  </span>
                </div>
                <p className="val">
                  HP {value(participant.finalHealth)} · DMG {value(participant.damageDealt)}{' '}
                  · FLT {value(participant.faults)}
                </p>
              </div>
            ))}
          </div>

          <table className="hidden w-full border-collapse sm:table">
            <thead>
              <tr>
                <Th>Bot</Th>
                <Th>Out</Th>
                <Th numeric>HP</Th>
                <Th numeric>Dmg</Th>
                <Th numeric>Faults</Th>
              </tr>
            </thead>
            <tbody>
              {/* Walked, never destructured: a match is a list on the wire, and a layout
                  that reads `[a, b]` renders half a fight the day a third arrives. */}
              {detail.participants.map((participant) => (
                <tr key={participant.slot} className="border-b border-arena-edge last:border-b-0">
                  <td className="py-2 pr-2 align-middle">
                    <BotLink participant={participant} winner={isWinner(detail, participant)} />
                  </td>
                  <td className="val py-2 pr-2 align-middle text-arena-text">
                    {participant.outcome?.toLowerCase() ?? '—'}
                  </td>
                  <Td>{value(participant.finalHealth)}</Td>
                  <Td>{value(participant.damageDealt)}</Td>
                  <Td>{value(participant.faults)}</Td>
                </tr>
              ))}
            </tbody>
          </table>

          {detail.replayHash !== null && (
            <div className="flex flex-col gap-2">
              <div className="grid grid-cols-[70px_1fr] items-baseline gap-x-3">
                <span className="lab">Replay</span>
                <span className="val break-all text-arena-text">{detail.replayHash}</span>
              </div>
              {/* The viewer's own Verify disclosure is inside a player that goes full
                  screen and ships standalone from the CLI, so the page keeps a copyable
                  copy of the same claim — and the exact three lines the command prints. */}
              <pre className="term">
                <span className="text-arena-dim">$ </span>curl -o match.json{' '}
                {`${window.location.origin}/api/matches/${detail.id}/replay`}
                {'\n'}
                <span className="text-arena-dim">$ </span>nilbots verify match.json
                {'\n'}
                <span className="text-arena-dim">
                  Stored hash:   {detail.replayHash}
                  {'\n'}
                  Computed hash: {detail.replayHash}
                  {'\n'}
                  OK: replay content matches its hash.
                </span>
              </pre>
              <p className="t-micro">
                The last three lines are what an untampered replay prints: the hash the
                server stored, and the hash your machine computes from the bytes it
                downloaded.
              </p>
            </div>
          )}
        </>
      )}
    </section>
  );
}

/* --------------------------------------------------------------------- the record ---
   The complete input set. Public from the moment the match exists, which is the point. */

function Record({ detail }: { detail: MatchDetail | undefined }) {
  return (
    <section className="panel pad flex flex-col gap-3">
      <h2 className="lab">Record</h2>
      {detail === undefined ? (
        <p className="t-meta">Reading the record…</p>
      ) : (
        <>
          <dl className="grid grid-cols-[70px_1fr] items-baseline gap-x-3 gap-y-[7px]">
            <dt className="lab">Map</dt>
            <dd className="val text-arena-text">{detail.mapId}</dd>
            <dt className="lab">Seed</dt>
            <dd className="val text-arena-text">{String(detail.seed)}</dd>
            <dt className="lab">Rules</dt>
            <dd className="val text-arena-text">{matchRules(detail) ?? '—'}</dd>
            <dt className="lab">Ran</dt>
            <dd className="val text-arena-text">{ranAt(detail)}</dd>
            <dt className="lab">Match</dt>
            <dd className="val break-all text-arena-text">{detail.id}</dd>
          </dl>

          <hr className="border-arena-edge" />

          {/* The strongest thing on the page, and it is free: `artifactHashSnapshot` is in
              the payload today and rendered nowhere else on the site. Map + seed + rules +
              every artifact hash is the complete input set, and publishing it beside the
              outcome is what turns "trust us" into "run it yourself". */}
          <div className="flex flex-col gap-2">
            <p className="lab">Artifacts</p>
            {detail.participants.map((participant) => (
              <div key={participant.slot} className="flex flex-col gap-1">
                <BotIdentity
                  name={botLabel(participant)}
                  accent={participant.accentSnapshot}
                  lookId={participant.lookIdSnapshot}
                  size="xs"
                />
                <span className="val break-all">{participant.artifactHashSnapshot}</span>
              </div>
            ))}
          </div>
        </>
      )}
    </section>
  );
}

/* ------------------------------------------------------------------------- pieces --- */

function BotLink({
  participant,
  winner,
}: {
  participant: MatchDetailParticipant;
  winner: boolean;
}) {
  // By id, not by a slug snapshot: `/api/bots/{key}` accepts a uuid and the bot route
  // canonicalizes, so a rename cannot strand this link on a name that no longer exists.
  return (
    <Link
      to={`/bots/${participant.botId}`}
      className="inline-flex min-w-0 transition-opacity hover:opacity-80"
    >
      {/* The winner is marked by weight, never colour: the outcome column already says it
          in words, and the only saturated colour here is a player's own accent. */}
      <BotIdentity
        name={botLabel(participant)}
        accent={participant.accentSnapshot}
        lookId={participant.lookIdSnapshot}
        size="xs"
        emphasized={winner}
      />
    </Link>
  );
}

function Th({ children, numeric }: { children: ReactNode; numeric?: boolean }) {
  return (
    <th
      scope="col"
      className={clsx(
        'lab border-b border-arena-edge pb-2',
        numeric ? 'pl-2 text-right' : 'pr-2 text-left',
      )}
    >
      {children}
    </th>
  );
}

function Td({ children }: { children: ReactNode }) {
  return (
    <td className="val py-2 pl-2 text-right align-middle whitespace-nowrap text-arena-text">
      {children}
    </td>
  );
}

/* -------------------------------------------------------------------- derivations --- */

const GAMES_PER_PAIR = 2;

/** Revealed is exactly what the server means by it: completed, and done broadcasting. */
function revealed(detail: MatchDetail): boolean {
  return detail.status === 'Completed' && !detail.broadcasting;
}

function isWinner(detail: MatchDetail, participant: MatchDetailParticipant): boolean {
  return detail.winnerSlot !== null && detail.winnerSlot === participant.slot;
}

function value(entry: number | null): string {
  return entry === null ? '—' : String(entry);
}

function ranAt(detail: MatchDetail): string {
  const when = new Date(detail.completedAt ?? detail.createdAt);
  return `${when.toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  })} · ${when.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })}`;
}

/**
 * Fields the design needs that the generated contract does not carry yet.
 *
 * Read off the response optionally rather than added to `schema.d.ts`, which is generated
 * from the server and must not be hand-edited. Production sends none of them, so each of
 * these reads null and its row renders an em-dash; the day the projection carries them,
 * regenerating the client deletes this type and nothing else changes.
 */
interface UnprojectedMatchFields {
  /**
   * Which rules this match ran under. `Match.GameRulesVersion` is on the entity;
   * `MatchDetailResponse` does not project it.
   *
   * `/api/meta` must **not** be substituted: that is the server's *current* rules version,
   * which is a different fact and would silently relabel every old match. Until the field
   * lands, an unranked match cannot state its rules until the replay document loads — so
   * never for a failed match, and not before reveal.
   */
  gameRulesVersion?: string;
}

interface UnprojectedParticipantFields {
  /**
   * Which generation of the bot fought. Participants carry `nameSnapshot` and no version
   * number, so these pages can only say "Pincer" where every other surface says
   * "Pincer gen-10". Do not synthesize it from the bot's current version — that relabels
   * old matches with a generation that did not play them. Needs `versionNumberSnapshot`
   * on `MatchDetailParticipantResponse`.
   */
  versionNumberSnapshot?: number;
}

function matchRules(detail: MatchDetail): string | null {
  return (detail as MatchDetail & UnprojectedMatchFields).gameRulesVersion ?? null;
}

function botLabel(participant: MatchDetailParticipant): string {
  const generation = (participant as MatchDetailParticipant & UnprojectedParticipantFields)
    .versionNumberSnapshot;
  return generation === undefined
    ? participant.nameSnapshot
    : `${participant.nameSnapshot} gen-${generation}`;
}
