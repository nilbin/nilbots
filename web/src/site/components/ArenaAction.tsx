import {
  createContext,
  useContext,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
} from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import clsx from 'clsx';
import { type BotSummary } from '../api';
import { useAuth } from '../auth';
import {
  ownedLegacyDuelBots,
  rosterBotSupportsLegacyDuel,
} from '../botContractProfiles';
import { errorMessage } from '../errorMessage';
import {
  useBots,
  useChallenge,
  useMeta,
  useMyBots,
  useRankedChallenge,
} from '../queries';
import BotIdentity from './BotIdentity';

export type ArenaMode = 'ranked' | 'challenge';

export interface ArenaActionBot {
  id: string;
  slug?: string;
  name: string;
  accent?: string | null;
  lookId?: string | null;
  isOwner: boolean;
  ready: boolean;
}

interface ArenaLaunch {
  bot: ArenaActionBot | null;
  modes: readonly ArenaMode[];
  initialOpponentId: string;
  initialMapId: string;
}

interface ArenaActionProps {
  bot: ArenaActionBot;
  /** Limit the choices when context already answers the mode, as on a result page. */
  modes?: readonly ArenaMode[];
  initialMode?: ArenaMode;
  initialOpponentId?: string;
  initialMapId?: string;
  /** `multi` exposes both jobs; `compact` saves room in cards and table rows. */
  variant?: 'multi' | 'compact';
  triggerLabel?: string;
  className?: string;
}

interface ArenaActions {
  launch: (
    request: ArenaLaunch,
    mode: ArenaMode,
    trigger: HTMLButtonElement,
  ) => void;
}

const ArenaActionContext = createContext<ArenaActions | null>(null);

/**
 * The one Arena composer for the whole app.
 *
 * Every trigger sends typed context here. Queries, mutation state, focus restoration and
 * the modal exist once rather than once per roster row; data is enabled only while the
 * composer is open. The roster is also the contract-profile authority, so every launch
 * path gets the same legacy-Duel eligibility check.
 */
export function ArenaActionProvider({ children }: { children: React.ReactNode }) {
  const [launch, setLaunch] = useState<ArenaLaunch | null>(null);
  const [mode, setMode] = useState<ArenaMode>('ranked');
  const [selectedBotId, setSelectedBotId] = useState('');
  const [opponentId, setOpponentId] = useState('');
  const [mapId, setMapId] = useState('');
  const dialogRef = useRef<HTMLDialogElement>(null);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const titleId = useId();
  const navigate = useNavigate();
  const location = useLocation();
  const { user, loading: authLoading } = useAuth();
  const challenge = useChallenge();
  const rankedChallenge = useRankedChallenge();

  const needsOwnedBots =
    launch !== null &&
    Boolean(user) &&
    (launch.bot === null || !launch.bot.isOwner);
  const {
    data: mine = null,
    error: mineError,
    refetch: refetchMine,
  } = useMyBots(needsOwnedBots);
  const needsRoster = launch !== null && Boolean(user);
  const {
    data: roster = null,
    error: rosterError,
    refetch: refetchRoster,
  } = useBots(needsRoster);
  const duelRoster = useMemo(
    () => (roster ?? []).filter(rosterBotSupportsLegacyDuel),
    [roster],
  );
  const readyOwnedBots = useMemo(
    () => ownedLegacyDuelBots(duelRoster, mine ?? []),
    [duelRoster, mine],
  );
  const selectedOwnedBot =
    readyOwnedBots.find((candidate) => candidate.id === selectedBotId) ?? null;
  const bot =
    launch?.bot ??
    (selectedOwnedBot
      ? {
          ...selectedOwnedBot,
          isOwner: true,
          ready: true,
        }
      : null);

  const launchBotEligible =
    launch?.bot === null ||
    (launch?.bot !== undefined &&
      launch.bot.ready &&
      duelRoster.some((candidate) => candidate.id === launch.bot?.id));
  const challengeOpen =
    launch !== null && mode === 'challenge' && Boolean(user) && bot !== null;
  const {
    data: meta = null,
    error: metaError,
    refetch: refetchMeta,
  } = useMeta(challengeOpen);
  const opponents = useMemo(
    () =>
      duelRoster.filter((candidate) => candidate.id !== bot?.id),
    [bot?.id, duelRoster],
  );

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (launch && !dialog.open) dialog.showModal();
    if (!launch && dialog.open) dialog.close();
  }, [launch]);

  // A public target needs one of mine; a global launch needs the bot to play as. Remove
  // the select only when there is genuinely one answer.
  useEffect(() => {
    if (
      !launch ||
      launch.bot?.isOwner ||
      selectedBotId !== '' ||
      readyOwnedBots.length !== 1
    )
      return;
    setSelectedBotId(readyOwnedBots[0].id);
  }, [launch, readyOwnedBots, selectedBotId]);

  useEffect(() => {
    if (bot && opponentId === bot.id) setOpponentId('');
  }, [bot, opponentId]);

  useEffect(() => {
    if (
      mode === 'challenge' &&
      bot?.isOwner &&
      roster !== null &&
      opponentId !== '' &&
      !opponents.some((candidate) => candidate.id === opponentId)
    )
      setOpponentId('');
  }, [bot?.isOwner, mode, opponentId, opponents, roster]);

  useEffect(() => {
    if (
      mode === 'challenge' &&
      meta !== null &&
      mapId !== '' &&
      !meta.maps.some((map) => map.id === mapId)
    )
      setMapId('');
  }, [mapId, meta, mode]);

  const close = () => setLaunch(null);
  const chooseMode = (next: ArenaMode) => {
    setMode(next);
    challenge.reset();
    rankedChallenge.reset();
  };
  const actions = useMemo<ArenaActions>(
    () => ({
      launch: (request, nextMode, trigger) => {
        triggerRef.current = trigger;
        setLaunch(request);
        setMode(nextMode);
        setSelectedBotId('');
        setOpponentId(request.initialOpponentId);
        setMapId(request.initialMapId);
        challenge.reset();
        rankedChallenge.reset();
      },
    }),
    // Mutations remain mounted with the provider and their reset functions are stable.
    // The context value changes only if either hook replaces that function.
    [challenge.reset, rankedChallenge.reset],
  );

  const play = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!launch || !bot || !launchBotEligible) return;
    try {
      if (mode === 'ranked') {
        const set = await rankedChallenge.mutateAsync({ botId: bot.id });
        close();
        navigate(`/sets/${set.id}`);
        return;
      }
      const match = await challenge.mutateAsync({
        botId: bot.isOwner ? bot.id : selectedBotId,
        opponentBotId: bot.isOwner ? opponentId : bot.id,
        mapId: mapId === '' ? null : mapId,
        seed: null,
      });
      close();
      navigate(`/matches/${match.id}`);
    } catch {
      // Mutation state owns the actionable error inside the dialog.
    }
  };

  const returnUrl = `${location.pathname}${location.search}${location.hash}`;
  const failure = challenge.error ?? rankedChallenge.error;
  const busy = challenge.isPending || rankedChallenge.isPending;
  const challenger = bot?.isOwner ? bot.id : selectedBotId;
  const opponent = bot?.isOwner ? opponentId : bot?.id ?? '';
  const globalLoading =
    launch?.bot === null &&
    (mine === null || roster === null) &&
    mineError === null &&
    rosterError === null;
  const rosterLoading =
    needsRoster && roster === null && rosterError === null;

  return (
    <ArenaActionContext.Provider value={actions}>
      {children}
      <dialog
        ref={dialogRef}
        aria-labelledby={titleId}
        onCancel={(event) => {
          event.preventDefault();
          close();
        }}
        onClose={() => {
          setLaunch(null);
          triggerRef.current?.focus();
        }}
        onClick={(event) => {
          if (event.target === event.currentTarget) close();
        }}
        className="arena-dialog panel max-h-[min(680px,calc(100dvh-32px))] w-[min(420px,calc(100vw-24px))] overflow-y-auto p-0 text-left text-arena-text"
      >
        {launch && (
          <>
            <header className="flex items-start gap-3 border-b border-arena-edge px-4 py-3.5">
              <span className="min-w-0 grow">
                <span className="lab mb-1 block">Arena</span>
                <span id={titleId} className="sr-only">
                  {arenaDialogTitle(bot, mode)}
                </span>
                <span
                  className="flex min-w-0 items-center gap-2"
                  aria-hidden="true"
                >
                  {bot ? (
                    <BotIdentity
                      name={bot.name}
                      accent={bot.accent}
                      lookId={bot.lookId}
                      size="sm"
                      emphasized
                    />
                  ) : (
                    <span className="t-body font-semibold text-arena-text">
                      Choose how to play
                    </span>
                  )}
                </span>
              </span>
              <button
                type="button"
                onClick={close}
                className="btn min-h-10 shrink-0"
                aria-label="Close Arena choices"
              >
                Close
              </button>
            </header>

            {authLoading ? (
              <p className="t-meta px-4 py-8 text-center" role="status">
                Checking your account…
              </p>
            ) : !user ? (
              <SignInState returnUrl={returnUrl} onClose={close} />
            ) : launch.bot && !launch.bot.ready ? (
              <Unavailable
                bot={launch.bot}
                reason="not-ready"
                onClose={close}
              />
            ) : rosterLoading ? (
              <p className="t-meta px-4 py-8 text-center" role="status">
                Checking Arena eligibility…
              </p>
            ) : rosterError ? (
              <QueryIssue
                error={rosterError}
                fallback="Arena eligibility could not be loaded."
                onRetry={() => void refetchRoster()}
              />
            ) : launch.bot && !launchBotEligible ? (
              <Unavailable
                bot={launch.bot}
                reason="not-compatible"
                onClose={close}
              />
            ) : globalLoading ? (
              <p className="t-meta px-4 py-8 text-center" role="status">
                Loading your bots…
              </p>
            ) : launch.bot === null && mineError ? (
              <QueryIssue
                error={mineError}
                fallback="Your ready bots could not be loaded."
                onRetry={() => void refetchMine()}
              />
            ) : launch.bot === null && readyOwnedBots.length === 0 ? (
              <NoOwnedBot onClose={close} />
            ) : (
              <form onSubmit={play} className="pad flex flex-col gap-4">
                {launch.bot === null && (
                  <label className="t-meta flex flex-col gap-1">
                    Play with
                    <select
                      value={selectedBotId}
                      onChange={(event) => setSelectedBotId(event.target.value)}
                      className="field"
                    >
                      <option value="">Choose your bot…</option>
                      {readyOwnedBots.map((candidate) => (
                        <option key={candidate.id} value={candidate.id}>
                          {candidate.name}
                        </option>
                      ))}
                    </select>
                  </label>
                )}

                {launch.modes.length > 1 && (
                  <div
                    className="grid grid-cols-2 gap-1"
                    role="group"
                    aria-label="How to play"
                  >
                    {launch.modes.map((candidate) => (
                      <button
                        key={candidate}
                        type="button"
                        aria-pressed={mode === candidate}
                        onClick={() => chooseMode(candidate)}
                        className={clsx(
                          'btn min-h-11',
                          mode === candidate && 'btn-on',
                        )}
                      >
                        {candidate === 'ranked' ? 'Ranked set' : 'Challenge'}
                      </button>
                    ))}
                  </div>
                )}

                {mode === 'ranked' ? (
                  <RankedChoice />
                ) : bot === null ? (
                  <>
                    <ChallengeSummary />
                    <p className="t-meta">
                      Choose the active bot that should enter the arena.
                    </p>
                  </>
                ) : !bot.isOwner && mineError ? null : (
                  <ChallengeChoice
                    bot={bot}
                    challengerId={selectedBotId}
                    opponentId={opponentId}
                    mapId={mapId}
                    ownedBots={readyOwnedBots}
                    opponents={opponents}
                    maps={meta?.maps ?? []}
                    loadingMine={
                      !bot.isOwner && mine === null && mineError === null
                    }
                    loadingRoster={
                      bot.isOwner && roster === null && rosterError === null
                    }
                    onChallengerChange={setSelectedBotId}
                    onOpponentChange={setOpponentId}
                    onMapChange={setMapId}
                  />
                )}

                {bot === null && mode === 'ranked' && (
                  <p className="t-meta">
                    Choose the active bot that should enter the arena.
                  </p>
                )}

                {mineError && needsOwnedBots && (
                  <QueryIssue
                    error={mineError}
                    fallback="Your available bots could not be loaded."
                    onRetry={() => void refetchMine()}
                    compact
                  />
                )}
                {metaError && mode === 'challenge' && (
                  <QueryIssue
                    error={metaError}
                    fallback="Map names could not be loaded. Random map remains available."
                    onRetry={() => void refetchMeta()}
                    compact
                  />
                )}
                {failure && (
                  <p className="t-body text-arena-hot" role="alert">
                    {errorMessage(
                      failure,
                      'The arena could not start this fight.',
                    )}
                  </p>
                )}

                <span className="flex flex-wrap items-center gap-2">
                  <button
                    type="submit"
                    disabled={
                      busy ||
                      !bot ||
                      !launchBotEligible ||
                      challenger === '' ||
                      (mode === 'challenge' &&
                        (opponent === '' || mineError !== null))
                    }
                    className="btn btn-on min-h-10"
                  >
                    {busy
                      ? 'Starting…'
                      : mode === 'ranked'
                        ? 'Start ranked set'
                        : 'Start challenge'}
                  </button>
                  <span className="t-micro">
                    Allowance is checked before anything is queued.
                  </span>
                </span>
              </form>
            )}
          </>
        )}
      </dialog>
    </ArenaActionContext.Provider>
  );
}

/**
 * Contextual launcher used beside a bot identity or result.
 *
 * The multi-button exposes both meaningful actions in roomy contexts. Compact callers
 * retain both inside the same dialog rather than reproducing a form in every card.
 */
export default function ArenaAction({
  bot,
  modes: requestedModes,
  initialMode,
  initialOpponentId = '',
  initialMapId = '',
  variant = 'compact',
  triggerLabel,
  className,
}: ArenaActionProps) {
  const actions = useArenaActions();
  const modes =
    requestedModes ?? (bot.isOwner ? ['ranked', 'challenge'] : ['challenge']);
  const normalizedModes = modes as readonly ArenaMode[];
  const firstMode =
    initialMode && normalizedModes.includes(initialMode)
      ? initialMode
      : normalizedModes[0] ?? 'challenge';
  const launch: ArenaLaunch = {
    bot,
    modes: normalizedModes,
    initialOpponentId,
    initialMapId,
  };

  return (
    <span className={clsx('inline-flex', className)}>
      <ArenaTriggers
        botName={bot.name}
        modes={normalizedModes}
        defaultMode={firstMode}
        variant={variant}
        triggerLabel={triggerLabel}
        onOpen={(mode, trigger) => actions.launch(launch, mode, trigger)}
      />
    </span>
  );
}

/** Always-available entry: the dialog first asks which ready owned bot should play. */
export function GlobalArenaAction({
  className,
}: {
  className?: string;
}) {
  const actions = useArenaActions();
  const modes: readonly ArenaMode[] = ['ranked', 'challenge'];
  const launch: ArenaLaunch = {
    bot: null,
    modes,
    initialOpponentId: '',
    initialMapId: '',
  };
  return (
    <button
      type="button"
      aria-haspopup="dialog"
      onClick={(event) => actions.launch(launch, 'ranked', event.currentTarget)}
      className={clsx(
        'btn btn-on inline-flex min-h-9 shrink-0 items-center gap-1.5',
        className,
      )}
    >
      Play
      <span aria-hidden="true" className="text-arena-dim">
        ▾
      </span>
    </button>
  );
}

function useArenaActions() {
  const actions = useContext(ArenaActionContext);
  if (!actions)
    throw new Error('ArenaAction must be rendered inside ArenaActionProvider.');
  return actions;
}

function ArenaTriggers({
  botName,
  modes,
  defaultMode,
  variant,
  triggerLabel,
  onOpen,
}: {
  botName: string;
  modes: readonly ArenaMode[];
  defaultMode: ArenaMode;
  variant: 'multi' | 'compact';
  triggerLabel?: string;
  onOpen: (mode: ArenaMode, trigger: HTMLButtonElement) => void;
}) {
  if (variant === 'multi' && modes.length > 1) {
    return (
      <span
        role="group"
        aria-label={`Play with ${botName}`}
        className="inline-flex"
      >
        {modes.map((mode, index) => (
          <button
            key={mode}
            type="button"
            aria-haspopup="dialog"
            onClick={(event) => onOpen(mode, event.currentTarget)}
            className={clsx(
              'btn btn-on min-h-10',
              index === 0 ? 'rounded-r-none' : '-ml-px rounded-l-none',
            )}
          >
            {mode === 'ranked' ? 'Ranked set' : 'Challenge'}
          </button>
        ))}
      </span>
    );
  }

  const mode = defaultMode;
  const label =
    triggerLabel ??
    (modes.length > 1
      ? 'Play'
      : mode === 'ranked'
        ? 'Ranked set'
        : 'Challenge');
  return (
    <button
      type="button"
      aria-haspopup="dialog"
      onClick={(event) => onOpen(mode, event.currentTarget)}
      className="btn btn-on inline-flex min-h-10 items-center gap-1.5"
    >
      {label}
      <span aria-hidden="true" className="text-arena-dim">
        ▾
      </span>
    </button>
  );
}

function RankedChoice() {
  return (
    <section className="panel-quiet pad">
      <h2 className="lab mb-2">Ranked set · 6 games</h2>
      <p className="t-meta">
        The arena matchmakes near the selected bot's current rating, then chooses
        three map-and-seed pairs and plays each from both starting sides. One set
        result moves the rating after all six games complete.
      </p>
    </section>
  );
}

function ChallengeSummary() {
  return (
    <section className="panel-quiet pad">
      <h2 className="lab mb-2">One-off challenge · 1 game</h2>
      <p className="t-meta">
        Choose the matchup and an optional map. The result is unranked and does not
        move either bot's rating.
      </p>
    </section>
  );
}

function ChallengeChoice({
  bot,
  challengerId,
  opponentId,
  mapId,
  ownedBots,
  opponents,
  maps,
  loadingMine,
  loadingRoster,
  onChallengerChange,
  onOpponentChange,
  onMapChange,
}: {
  bot: ArenaActionBot;
  challengerId: string;
  opponentId: string;
  mapId: string;
  ownedBots: readonly BotSummary[];
  opponents: readonly { id: string; name: string; owner: string }[];
  maps: readonly { id: string; width: number; height: number }[];
  loadingMine: boolean;
  loadingRoster: boolean;
  onChallengerChange: (id: string) => void;
  onOpponentChange: (id: string) => void;
  onMapChange: (id: string) => void;
}) {
  if (!bot.isOwner && !loadingMine && ownedBots.length === 0) {
    return (
      <section className="panel-quiet pad">
        <h2 className="lab mb-2">You need a ready bot</h2>
        <p className="t-meta">
          Submit and activate a successful build before challenging {bot.name}.{' '}
          <Link to="/garage" className="text-link">
            Open your Garage
          </Link>
          .
        </p>
      </section>
    );
  }

  if (bot.isOwner && !loadingRoster && opponents.length === 0) {
    return (
      <section className="panel-quiet pad">
        <h2 className="lab mb-2">No opponent is ready</h2>
        <p className="t-meta">
          Nobody else with an active build is available for a one-off challenge.
          Ranked matchmaking remains a separate action.
        </p>
      </section>
    );
  }

  return (
    <div className="grid gap-3 sm:grid-cols-2">
      <div className="sm:col-span-2">
        <ChallengeSummary />
      </div>
      {!bot.isOwner && (
        <label className="t-meta flex flex-col gap-1">
          Challenge with
          <select
            value={challengerId}
            onChange={(event) => onChallengerChange(event.target.value)}
            disabled={loadingMine}
            className="field"
          >
            <option value="">
              {loadingMine ? 'Loading your bots…' : 'Choose your bot…'}
            </option>
            {ownedBots.map((candidate) => (
              <option key={candidate.id} value={candidate.id}>
                {candidate.name}
              </option>
            ))}
          </select>
        </label>
      )}
      {bot.isOwner && (
        <label className="t-meta flex flex-col gap-1">
          Opponent
          <select
            value={opponentId}
            onChange={(event) => onOpponentChange(event.target.value)}
            disabled={loadingRoster}
            className="field"
          >
            <option value="">
              {loadingRoster ? 'Loading opponents…' : 'Choose an opponent…'}
            </option>
            {opponents.map((candidate) => (
              <option key={candidate.id} value={candidate.id}>
                {candidate.name} · {candidate.owner}
              </option>
            ))}
          </select>
        </label>
      )}
      <label className="t-meta flex flex-col gap-1">
        Map
        <select
          value={mapId}
          onChange={(event) => onMapChange(event.target.value)}
          className="field"
        >
          <option value="">Random map</option>
          {maps.map((map) => (
            <option key={map.id} value={map.id}>
              {map.id} · {map.width}×{map.height}
            </option>
          ))}
        </select>
      </label>
    </div>
  );
}

function SignInState({
  returnUrl,
  onClose,
}: {
  returnUrl: string;
  onClose: () => void;
}) {
  return (
    <section className="pad">
      <h2 className="lab mb-2">Sign in to play</h2>
      <p className="t-meta mb-3">
        Watching is public. Starting a fight needs an account so the arena can verify
        which bot is yours and apply the account allowance.
      </p>
      <Link
        to={`/login?returnUrl=${encodeURIComponent(returnUrl)}`}
        onClick={onClose}
        className="btn btn-on inline-flex"
      >
        Sign in and return
      </Link>
    </section>
  );
}

function QueryIssue({
  error,
  fallback,
  onRetry,
  compact = false,
}: {
  error: unknown;
  fallback: string;
  onRetry: () => void;
  compact?: boolean;
}) {
  return (
    <section
      className={compact ? 'flex flex-wrap items-center gap-2' : 'pad'}
      role="alert"
    >
      <p className="t-meta min-w-0 grow text-arena-hot">
        {errorMessage(error, fallback)}
      </p>
      <button type="button" onClick={onRetry} className="btn">
        Try again
      </button>
    </section>
  );
}

function NoOwnedBot({ onClose }: { onClose: () => void }) {
  return (
    <section className="pad">
      <h2 className="lab mb-2">You need a ready bot</h2>
      <p className="t-meta">
        Submit and activate a successful generation before entering the arena.
      </p>
      <Link to="/garage" onClick={onClose} className="btn mt-3 inline-flex">
        Open your Garage
      </Link>
    </section>
  );
}

function Unavailable({
  bot,
  reason,
  onClose,
}: {
  bot: ArenaActionBot;
  reason: 'not-ready' | 'not-compatible';
  onClose: () => void;
}) {
  const incompatible = reason === 'not-compatible';
  return (
    <section className="pad">
      <h2 className="lab mb-2">
        {incompatible ? 'Not available in Duel' : 'No active generation'}
      </h2>
      <p className="t-meta">
        {incompatible
          ? bot.isOwner
            ? `${bot.name}'s active generation targets another game mode. Use its available Labs action instead.`
            : `${bot.name}'s active generation targets another game mode and cannot enter the legacy Duel arena.`
          : bot.isOwner
            ? 'This bot needs a successful active generation before it can fight.'
            : `${bot.name} cannot be challenged until its owner activates a successful generation.`}
      </p>
      {!incompatible && bot.isOwner && bot.slug && (
        <Link
          to={`/bots/${bot.slug}#submit`}
          onClick={onClose}
          className="btn mt-3 inline-flex"
        >
          Go to submission
        </Link>
      )}
    </section>
  );
}

function arenaDialogTitle(bot: ArenaActionBot | null, mode: ArenaMode) {
  if (!bot) {
    return mode === 'ranked'
      ? 'Choose a bot for a ranked set'
      : 'Choose a bot for a one-off challenge';
  }
  if (mode === 'ranked') return `Ranked set with ${bot.name}`;
  return bot.isOwner ? `Challenge with ${bot.name}` : `Challenge ${bot.name}`;
}
