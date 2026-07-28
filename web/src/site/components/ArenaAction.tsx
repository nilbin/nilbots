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
import {
  type ArenaCapabilities,
  type BotSummary,
  type MatchPlayability,
} from '../api';
import {
  arenaOpponents,
  indexArenaPlayability,
  ownedPlayableArenaRoster,
  playableArenaRoster,
} from '../arenaCapabilities';
import { useAuth } from '../auth';
import { errorMessage } from '../errorMessage';
import {
  useArenaCapabilities,
  useBots,
  useChallenge,
  useMeta,
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
 * composer is open. `/api/arena` is the authority for bot admission, effective
 * allowances and format; the public roster contributes display identity only.
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

  const needsArena = launch !== null && Boolean(user);
  const {
    data: capabilities = null,
    error: capabilitiesError,
    isFetching: capabilitiesRefreshing,
    refetch: refetchCapabilities,
  } = useArenaCapabilities(needsArena);
  const needsRoster = needsArena;
  const {
    data: roster = null,
    error: rosterError,
    refetch: refetchRoster,
  } = useBots(needsRoster);
  const playabilityById = useMemo(
    () => indexArenaPlayability(capabilities?.bots ?? []),
    [capabilities],
  );
  const duelRoster = useMemo(
    () =>
      capabilities === null
        ? []
        : playableArenaRoster(roster ?? [], capabilities),
    [capabilities, roster],
  );
  const readyOwnedBots = useMemo(
    () =>
      capabilities === null
        ? []
        : ownedPlayableArenaRoster(roster ?? [], capabilities),
    [capabilities, roster],
  );
  const selectedOwnedBot =
    readyOwnedBots.find((candidate) => candidate.id === selectedBotId) ?? null;
  const launchPlayability =
    launch?.bot === null || launch?.bot === undefined
      ? null
      : playabilityById.get(launch.bot.id) ?? null;
  const bot = launch?.bot
    ? {
        ...launch.bot,
        // Caller ownership controls only the pre-open label. Every submitted direction
        // is resolved from the authenticated Arena projection.
        isOwner: launchPlayability?.isOwned === true,
      }
    : selectedOwnedBot
      ? {
          ...selectedOwnedBot,
          isOwner: true,
        }
      : null;
  const launchBotEligible =
    launch?.bot === null ||
    launchPlayability?.playable === true;
  const availableModes = useMemo(
    () =>
      launch?.bot &&
      capabilities !== null &&
      launchPlayability?.isOwned !== true
        ? launch.modes.filter((candidate) => candidate !== 'ranked')
        : launch?.modes ?? [],
    [capabilities, launch, launchPlayability?.isOwned],
  );
  const challengeOpen =
    launch !== null && mode === 'challenge' && Boolean(user) && bot !== null;
  const {
    data: meta = null,
    error: metaError,
    refetch: refetchMeta,
  } = useMeta(challengeOpen);
  const opponents = useMemo(
    () => arenaOpponents(duelRoster, bot?.id),
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
      launchPlayability?.isOwned ||
      selectedBotId !== '' ||
      readyOwnedBots.length !== 1
    )
      return;
    setSelectedBotId(readyOwnedBots[0].id);
  }, [
    launch,
    launchPlayability?.isOwned,
    readyOwnedBots,
    selectedBotId,
  ]);

  useEffect(() => {
    if (bot && opponentId === bot.id) setOpponentId('');
  }, [bot, opponentId]);

  useEffect(() => {
    if (
      launch &&
      availableModes.length > 0 &&
      !availableModes.includes(mode)
    ) {
      setMode(availableModes[0]);
    }
  }, [availableModes, launch, mode]);

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
    if (
      !launch ||
      !bot ||
      !launchBotEligible ||
      !availableModes.includes(mode) ||
      !capabilities ||
      !(mode === 'ranked'
        ? capabilities.rankedAllowance.canStart
        : capabilities.unrankedAllowance.canStart)
    )
      return;
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
  const arenaLoading =
    needsArena &&
    capabilities === null &&
    capabilitiesError === null;
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
        className="arena-dialog panel w-[min(420px,calc(100vw-24px))] overflow-y-auto p-0 text-left text-arena-text"
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
            ) : arenaLoading ? (
              <p className="t-meta px-4 py-8 text-center" role="status">
                Checking Arena eligibility and allowance…
              </p>
            ) : capabilitiesError ? (
              <QueryIssue
                error={capabilitiesError}
                fallback="Arena availability could not be loaded."
                onRetry={() => void refetchCapabilities()}
              />
            ) : rosterLoading ? (
              <p className="t-meta px-4 py-8 text-center" role="status">
                Loading Arena identities…
              </p>
            ) : rosterError ? (
              <QueryIssue
                error={rosterError}
                fallback="Arena eligibility could not be loaded."
                onRetry={() => void refetchRoster()}
              />
            ) : launch.bot && !launchBotEligible ? (
              <Unavailable
                bot={bot ?? launch.bot}
                playability={launchPlayability}
                onClose={close}
                onRetry={() => {
                  void refetchCapabilities();
                  void refetchRoster();
                }}
              />
            ) : launch.bot === null && readyOwnedBots.length === 0 ? (
              <NoOwnedBot onClose={close} />
            ) : capabilities !== null ? (
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

                {availableModes.length > 1 && (
                  <div
                    className="grid grid-cols-2 gap-1"
                    role="group"
                    aria-label="How to play"
                  >
                    {availableModes.map((candidate) => (
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
                  <RankedChoice format={capabilities.format.ranked} />
                ) : bot === null ? (
                  <>
                    <ChallengeSummary
                      gamesPerMatch={
                        capabilities.format.unranked.gamesPerMatch
                      }
                      defaultMapId={
                        capabilities.format.unranked.defaultMapId
                      }
                    />
                    <p className="t-meta">
                      Choose the active bot that should enter the arena.
                    </p>
                  </>
                ) : (
                  <ChallengeChoice
                    bot={bot}
                    challengerId={selectedBotId}
                    opponentId={opponentId}
                    mapId={mapId}
                    ownedBots={readyOwnedBots}
                    opponents={opponents}
                    maps={meta?.maps ?? []}
                    defaultMapId={
                      capabilities.format.unranked.defaultMapId
                    }
                    gamesPerMatch={
                      capabilities.format.unranked.gamesPerMatch
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

                {metaError && mode === 'challenge' && (
                  <QueryIssue
                    error={metaError}
                    fallback="Map names could not be loaded. The default map remains available."
                    onRetry={() => void refetchMeta()}
                    compact
                  />
                )}
                <ArenaAllowanceStatus
                  mode={mode}
                  capabilities={capabilities}
                  refreshing={capabilitiesRefreshing}
                  onRefresh={() => void refetchCapabilities()}
                />
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
                      !availableModes.includes(mode) ||
                      challenger === '' ||
                      !(mode === 'ranked'
                        ? capabilities.rankedAllowance.canStart
                        : capabilities.unrankedAllowance.canStart) ||
                      (mode === 'challenge' &&
                        opponent === '')
                    }
                    className="btn btn-on min-h-10"
                  >
                    {busy
                      ? 'Starting…'
                      : mode === 'ranked'
                        ? 'Start ranked set'
                        : 'Start challenge'}
                  </button>
                </span>
              </form>
            ) : null}
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

function RankedChoice({
  format,
}: {
  format: ArenaCapabilities['format']['ranked'];
}) {
  const games = countLabel(format.gamesPerSet, 'game');
  const pairs = countLabel(format.mapSeedPairs, 'map-and-seed pair');
  const pool = countLabel(
    format.matchmakingPoolSize,
    'closest-rated bot',
  );
  return (
    <section className="panel-quiet pad">
      <h2 className="lab mb-2">Ranked set · {games}</h2>
      <p className="t-meta">
        The arena matchmakes from up to {pool}, then chooses{' '}
        {pairs}
        {format.mirroredSlots
          ? ' and plays each from both starting sides'
          : ''}
        . One set result moves the rating after all {games} complete.
      </p>
    </section>
  );
}

function ChallengeSummary({
  gamesPerMatch,
  defaultMapId,
}: {
  gamesPerMatch: number;
  defaultMapId: string;
}) {
  const games = countLabel(gamesPerMatch, 'game');
  return (
    <section className="panel-quiet pad">
      <h2 className="lab mb-2">One-off challenge · {games}</h2>
      <p className="t-meta">
        Choose the matchup and, optionally, a different map. The default is{' '}
        <span className="val">{defaultMapId}</span>. The result is unranked and
        does not move either bot's rating.
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
  defaultMapId,
  gamesPerMatch,
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
  defaultMapId: string;
  gamesPerMatch: number;
  onChallengerChange: (id: string) => void;
  onOpponentChange: (id: string) => void;
  onMapChange: (id: string) => void;
}) {
  if (!bot.isOwner && ownedBots.length === 0) {
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

  if (bot.isOwner && opponents.length === 0) {
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
        <ChallengeSummary
          gamesPerMatch={gamesPerMatch}
          defaultMapId={defaultMapId}
        />
      </div>
      {!bot.isOwner && (
        <label className="t-meta flex flex-col gap-1">
          Challenge with
          <select
            value={challengerId}
            onChange={(event) => onChallengerChange(event.target.value)}
            className="field"
          >
            <option value="">Choose your bot…</option>
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
            className="field"
          >
            <option value="">Choose an opponent…</option>
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
          <option value="">Default · {defaultMapId}</option>
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

function ArenaAllowanceStatus({
  mode,
  capabilities,
  refreshing,
  onRefresh,
}: {
  mode: ArenaMode;
  capabilities: ArenaCapabilities;
  refreshing: boolean;
  onRefresh: () => void;
}) {
  const allowance =
    mode === 'ranked'
      ? capabilities.rankedAllowance
      : capabilities.unrankedAllowance;
  const units = mode === 'ranked' ? 'ranked sets' : 'challenges';
  const window = `${allowance.rollingWindowHours}h rolling window`;

  return (
    <section
      className="border-y border-arena-edge py-2.5"
      aria-label={`${mode === 'ranked' ? 'Ranked' : 'Challenge'} allowance`}
    >
      <span className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="lab">Allowance</span>
        <span className="val text-arena-text">
          {allowance.remaining}/{allowance.limit}
        </span>
        <span className="t-micro">
          {units} left · {window}
        </span>
      </span>
      {mode === 'ranked' && (
        <p className="t-micro mt-1">
          {capabilities.rankedAllowance.inProgress}/
          {capabilities.rankedAllowance.concurrencyLimit} ranked sets in
          progress
        </p>
      )}
      {!allowance.canStart && (
        <div className="mt-1.5 flex flex-wrap items-center gap-2">
          <p className="t-meta min-w-0 grow text-arena-hot" role="status">
            {allowanceRefusalMessage(mode, allowance)}
          </p>
          <button
            type="button"
            onClick={onRefresh}
            disabled={refreshing}
            className="btn"
          >
            {refreshing ? 'Checking…' : 'Refresh'}
          </button>
        </div>
      )}
    </section>
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
  playability,
  onClose,
  onRetry,
}: {
  bot: ArenaActionBot;
  playability: MatchPlayability | null;
  onClose: () => void;
  onRetry: () => void;
}) {
  const missingBuild =
    playability?.refusalCode === 'matches.active_version_required';
  const incompatible =
    playability?.refusalCode === 'matches.contract_profile_required';
  const appearanceIssue =
    playability?.refusalCode?.startsWith('appearance.') === true;
  const botKey = bot.slug ?? bot.id;
  return (
    <section className="pad">
      <h2 className="lab mb-2">
        {playability === null
          ? 'Arena eligibility changed'
          : incompatible
          ? 'Not available in Duel'
          : missingBuild
            ? 'No active generation'
            : 'Unavailable in the Arena'}
      </h2>
      <p className="t-meta">
        {playability?.refusalDetail ??
          (playability === null
            ? `${bot.name} is missing from the current Arena roster. Reload its eligibility before trying again.`
            : incompatible
            ? `${bot.name}'s active generation targets another game mode.`
            : `${bot.name} cannot enter the Arena right now.`)}
      </p>
      <span className="mt-3 flex flex-wrap gap-2">
        {playability === null && (
          <button type="button" onClick={onRetry} className="btn">
            Try again
          </button>
        )}
        {missingBuild && bot.isOwner && (
          <Link
            to={`/bots/${botKey}#submit`}
            onClick={onClose}
            className="btn inline-flex"
          >
            Go to submission
          </Link>
        )}
        {appearanceIssue && bot.isOwner && (
          <Link
            to={`/bots/${botKey}/appearance`}
            onClick={onClose}
            className="btn inline-flex"
          >
            Review appearance
          </Link>
        )}
      </span>
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

function countLabel(count: number, singular: string) {
  return `${count} ${count === 1 ? singular : `${singular}s`}`;
}

function allowanceRefusalMessage(
  mode: ArenaMode,
  allowance:
    | ArenaCapabilities['unrankedAllowance']
    | ArenaCapabilities['rankedAllowance'],
) {
  if (allowance.refusalCode === 'matches.ranked_concurrent_limit') {
    return 'Wait for one of your ranked sets to finish before starting another.';
  }
  if (allowance.nextDailySlotAt) {
    return `Daily allowance used. The next slot opens ${new Intl.DateTimeFormat(
      undefined,
      {
        dateStyle: 'medium',
        timeStyle: 'short',
      },
    ).format(new Date(allowance.nextDailySlotAt))}.`;
  }
  return mode === 'ranked'
    ? 'A ranked set cannot start right now.'
    : 'A challenge cannot start right now.';
}
