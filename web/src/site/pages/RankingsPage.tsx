import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import clsx from 'clsx';
import BotIdentity from '../components/BotIdentity';
import { useLeaderboard, useMe, useMyBots } from '../queries';
import { ErrorState, LoadingState } from '../components/StateView';
import Th from '../components/TableHeader';
import Control from '../../components/ToggleButton';
import { playerAccent } from '../../presentation/playerAccent';
import { styleVariables } from '../../presentation/styleVariables';

/**
 * The rankings screen: your fleet and the current or archived ladder underneath it.
 *
 * A player owns several bots at once, so "your standing" is not one number — which is
 * why the strip walks every bot of yours on this ladder rather than pinning a favourite,
 * and why the table marks all of your rows.
 *
 * The table is a table, not a list of cards. Standings are read by comparing rows — is
 * that rating close to mine, who moved — and cards put every value in a different place
 * on every row, so the eye has to re-find each one. Tabular figures let a column be
 * scanned instead of read.
 *
 * A season may contain several playlist-specific ladders. The compatibility response only
 * exposes one flat family of legacy Duel standings, so this screen scopes itself to Duel
 * rather than pretending it can render the missing season hierarchy.
 */

export default function RankingsPage() {
  // The existing endpoint still accepts a legacy rules-version lookup. Keep that wire
  // detail behind this state: to a player these are the current and archived ladders.
  const [ladderKey, setLadderKey] = useState<string | null>(null);
  const { data: board = null, error, refetch } = useLeaderboard(ladderKey);
  const { data: me = null } = useMe();
  const { data: myBots = [] } = useMyBots(me !== null);
  const [query, setQuery] = useState('');
  const [mineOnly, setMineOnly] = useState(false);

  const entries = useMemo(() => board?.entries ?? [], [board]);
  const ownedBotIds = useMemo(
    () => new Set(myBots.map((bot) => bot.id)),
    [myBots],
  );

  // Ownership joins on immutable bot ids. Display names can collide and account names can
  // change, so neither is an identity boundary.
  const fleet = useMemo(
    () => entries.filter((entry) => ownedBotIds.has(entry.id)),
    [entries, ownedBotIds],
  );

  // Switching ladders can take your last bot off the board, and a filter left pressed
  // over an empty fleet is a table nobody can get back. It stops applying with the
  // control that sets it.
  const mineActive = mineOnly && fleet.length > 0;

  // Keep each bot's real rank while filtering: #7 is #7 whatever else is on screen.
  const shown = useMemo(() => {
    const needle = query.trim().toLowerCase();
    const mine = new Set(fleet.map((entry) => entry.id));
    return entries.filter(
      (entry) =>
        (!mineActive || mine.has(entry.id)) &&
        (needle === '' ||
          entry.name.toLowerCase().includes(needle) ||
          entry.owner.toLowerCase().includes(needle)),
    );
  }, [entries, fleet, mineActive, query]);

  const closed =
    board !== null && board.rulesVersion !== board.activeRulesVersion;

  return (
    <div className="mx-auto max-w-4xl">
      <p className="lab mb-2">
        Ranked Duel · {closed ? 'Archived standings' : 'Live ladder'}
      </p>
      <h1 className="type-display mb-2 text-[30px]">Rankings</h1>
      <p className="t-body mb-4 max-w-[62ch] text-arena-dim">
        These are the Duel standings. Ranked sets move each bot&apos;s rating on this
        ladder; other competitive playlists can have their own ladder within the same
        season.
      </p>

      {closed && (
        <p className="panel-quiet t-body mb-4 max-w-[62ch] border-l-2 border-l-arena-edge2 px-3 py-2 text-arena-dim">
          These Duel standings are <b className="text-arena-text">closed</b>. Their
          positions are final; new Duel sets enter the live ladder.
        </p>
      )}

      {board !== null && board.ladders.length > 1 && (
        <label className="lab mb-4 flex max-w-xs flex-col gap-1.5">
          Duel ladder
          <select
            value={board.rulesVersion}
            onChange={(event) =>
              setLadderKey(
                event.target.value === board.activeRulesVersion
                  ? null
                  : event.target.value,
              )
            }
            className="field t-body w-full normal-case"
          >
            {board.ladders.map((ladder) => (
              <option key={ladder} value={ladder}>
                {ladderChoiceLabel(
                  ladder,
                  board.activeRulesVersion,
                  board.ladders,
                )}
              </option>
            ))}
          </select>
        </label>
      )}

      {error !== null && (
        <ErrorState error={error} onRetry={() => void refetch()} />
      )}
      {error === null && board === null && (
        <LoadingState label="Loading the ladder…" />
      )}
      {board !== null && entries.length === 0 && (
        <div className="py-10 text-center">
          <p className="t-body font-semibold text-arena-dim">
            No ranked sets yet
          </p>
          <p className="t-meta mt-1">
            Standings appear once a bot completes a ranked set.
          </p>
          <Link to="/bots" className="btn mt-3 inline-flex">
            Browse bots
          </Link>
        </div>
      )}

      {board !== null && entries.length > 0 && (
        <div className="flex flex-col gap-3.5">
          {/* The header and the fleet are one panel, and neither renders for a visitor
              with nothing on this ladder: every line in it is about *your* bots, so
              signed out it would say nothing the page above has not already said. */}
          {fleet.length > 0 && (
            <section className="panel pad">
              <div className="mb-2.5 flex flex-wrap items-center justify-between gap-2.5">
                <p className="lab">
                  {headerLine(closed, fleet.length)}
                </p>
                <span className="pill">
                  best rank {Math.min(...fleet.map((entry) => entry.rank))}
                </span>
              </div>
              <div className="grid gap-2.5 [grid-template-columns:repeat(auto-fit,minmax(190px,1fr))]">
                {fleet.map((entry) => (
                  <Link
                    key={entry.id}
                    to={`/bots/${entry.slug}`}
                    state={{ returnTo: '/', returnLabel: 'Rankings' }}
                    className="panel px-3 py-[11px] transition-colors hover:border-arena-edge2"
                  >
                    <div className="mb-2 flex items-center justify-between gap-2">
                      <BotIdentity
                        name={entry.name}
                        accent={entry.accent}
                        lookId={entry.lookId}
                        size="md"
                        className="min-w-0"
                      />
                      {/* A rank is the loudest number on the screen, and it is display
                          type rather than mono: it is the bot's position, not a value
                          the engine computed. */}
                      <span className="type-display tabular shrink-0 text-[22px] text-arena-text">
                        {entry.rank}
                      </span>
                    </div>
                    <div className="flex items-center justify-between gap-2.5">
                      <span className="t-micro">
                        {entry.rankedSets}{' '}
                        {entry.rankedSets === 1 ? 'ranked set' : 'ranked sets'}
                      </span>
                      <span className="val ml-auto text-arena-text">{entry.rating}</span>
                    </div>
                  </Link>
                ))}
              </div>
            </section>
          )}

          <section className="panel">
            <div className="pad flex flex-wrap items-center justify-between gap-2.5 pb-0">
              <h2 className="lab">Duel ladder · {entries.length} bots</h2>
              <div className="flex flex-wrap items-center gap-1.5">
                {fleet.length > 0 && (
                  <Control pressed={mineActive} onClick={() => setMineOnly(!mineActive)}>
                    mine only
                  </Control>
                )}
              </div>
            </div>
            <div className="pad pt-2.5">
              <input
                type="search"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Filter by bot or owner…"
                aria-label="Filter the ladder by bot or owner"
                className="field mb-2.5"
              />
              {/* A phone drops secondary columns rather than scrolling sideways. */}
              <table className="t-body w-full border-collapse">
                <thead>
                  <tr>
                    <Th className="w-[30px]">#</Th>
                    <Th>Bot</Th>
                    <Th className="hidden w-28 sm:table-cell">Owner</Th>
                    <Th className="w-20" numeric>
                      Rating
                    </Th>
                    <Th className="hidden w-16 sm:table-cell" numeric>
                      Sets
                    </Th>
                  </tr>
                </thead>
                <tbody>
                  {shown.length === 0 && (
                    <tr>
                      <td colSpan={5} className="py-4 text-arena-dim">
                        {query.trim() === ''
                          ? 'No bot of yours is on this ladder.'
                          : `No bot on this ladder matches “${query}”.`}
                      </td>
                    </tr>
                  )}
                  {shown.map((entry) => {
                    // Yours is marked by a rule in that bot's own accent — the only
                    // chroma the table spends, and always somebody's choice rather than
                    // a system opinion.
                    const mine = ownedBotIds.has(entry.id);
                    const rail =
                      mine && entry.accent
                        ? playerAccent(entry.accent, 'panel')
                        : null;
                    return (
                      <tr
                        key={entry.id}
                        className={clsx(
                          'border-b border-arena-edge last:border-b-0',
                          mine && 'bg-arena-text/[0.028]',
                        )}
                      >
                        <td
                          className={clsx(
                            'type-display tabular p-2 align-middle text-[22px] text-arena-text',
                            rail && 'player-accent-rail',
                          )}
                          style={
                            rail
                              ? styleVariables({ '--player-accent': rail })
                              : undefined
                          }
                        >
                          {entry.rank}
                        </td>
                        <td className="p-2 align-middle">
                          <Link
                            to={`/bots/${entry.slug}`}
                            state={{ returnTo: '/', returnLabel: 'Rankings' }}
                            className="inline-flex min-w-0 transition-opacity hover:opacity-80"
                          >
                            <BotIdentity
                              name={entry.name}
                              accent={entry.accent}
                              lookId={entry.lookId}
                              size="sm"
                            />
                          </Link>
                        </td>
                        <td className="hidden truncate p-2 align-middle text-arena-dim sm:table-cell">
                          {entry.owner}
                        </td>
                        <td className="val p-2 text-right align-middle whitespace-nowrap text-arena-text">
                          {entry.rating}
                        </td>
                        <td className="val hidden p-2 text-right align-middle whitespace-nowrap sm:table-cell">
                          {entry.rankedSets}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </section>
        </div>
      )}
    </div>
  );
}

/**
 * The line above the fleet.
 *
 * Competition metadata does not reach this endpoint yet, so the heading says only what
 * the payload proves: whether these Duel standings are live or archived, and how many are
 * yours.
 */
function headerLine(
  archived: boolean,
  owned: number,
) {
  const yours = `your ${owned} ranked ${owned === 1 ? 'bot' : 'bots'}`;
  return `${archived ? 'Archived Duel standings' : 'Live Duel ladder'} · ${yours}`;
}

/**
 * The compatibility endpoint exposes opaque-to-players rules strings without season or
 * playlist identity. Give the controls useful Duel-scoped labels without pretending the
 * values describe a season. Real season and ladder names replace this adapter when the
 * competition API lands.
 */
function ladderChoiceLabel(
  ladder: string,
  active: string,
  ladders: readonly string[],
) {
  if (ladder === active) return 'Live Duel ladder';
  const archives = ladders.filter((candidate) => candidate !== active);
  if (archives.length === 1) return 'Archived Duel standings';
  return `Archived Duel standings ${archives.indexOf(ladder) + 1}`;
}
