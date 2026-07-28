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
  type LabsPlaylist,
  type MatchPlayability,
} from '../api';
import {
  arenaOpponents,
  indexArenaPlayability,
  ownedArenaBotIds,
  ownedPlayableArenaRoster,
  playableArenaRoster,
} from '../arenaCapabilities';
import { useAuth } from '../auth';
import { errorMessage } from '../errorMessage';
import {
  createLabsMatchRequest,
  eligibleLabsOpponents,
  eligibleLabsPlaylistsForRosterBot,
  eligibleOwnedLabsRoster,
} from '../labs';
import {
  useArenaCapabilities,
  useBots,
  useChallenge,
  useCreateLabsMatch,
  useLabsCatalog,
  useMeta,
  useRankedChallenge,
} from '../queries';
import BotIdentity from './BotIdentity';
import { playerAccent } from '../../presentation/playerAccent';
import { styleVariables } from '../../presentation/styleVariables';

export type ArenaMode = 'ranked' | 'challenge' | 'labs';
export type ChallengeContextRole = 'entrant' | 'opponent';

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
  challengeContextRole: ChallengeContextRole;
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
  /**
   * Whether the contextual bot enters a one-off challenge or is its fixed target.
   * Challenge defaults to targeting the contextual bot. Play/replay callers that keep
   * an owned bot as the entrant must say so explicitly.
   */
  challengeContextRole?: ChallengeContextRole;
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
const MAX_BOT_CHOICES = 8;

/**
 * The one Arena composer for the whole app.
 *
 * Every trigger sends typed context here. Queries, mutation state, focus restoration and
 * the modal exist once rather than once per roster row; data is enabled only while the
 * composer is open. `/api/arena` is the authority for bot admission, effective
 * allowances and Duel format. Labs eligibility joins that authoritative ownership with
 * active artifact profiles from the roster and hosted playlist versions from the catalog.
 */
export function ArenaActionProvider({ children }: { children: React.ReactNode }) {
  const [launch, setLaunch] = useState<ArenaLaunch | null>(null);
  const [mode, setMode] = useState<ArenaMode>('ranked');
  const [selectedBotId, setSelectedBotId] = useState('');
  const [choosingBot, setChoosingBot] = useState(false);
  const [opponentId, setOpponentId] = useState('');
  const [mapId, setMapId] = useState('');
  const [playlistId, setPlaylistId] = useState('');
  const dialogRef = useRef<HTMLDialogElement>(null);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const titleId = useId();
  const navigate = useNavigate();
  const location = useLocation();
  const { user, loading: authLoading } = useAuth();
  const challenge = useChallenge();
  const rankedChallenge = useRankedChallenge();
  const labsMatch = useCreateLabsMatch();

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
  const needsLabs =
    needsArena && launch?.modes.includes('labs') === true;
  const {
    data: labsCatalog = null,
    error: labsError,
    refetch: refetchLabs,
  } = useLabsCatalog(needsLabs);
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
  const ownedBotIds = useMemo(
    () => ownedArenaBotIds(capabilities),
    [capabilities],
  );
  const labsOwnedBots = useMemo(
    () =>
      labsCatalog === null
        ? []
        : eligibleOwnedLabsRoster(
            roster ?? [],
            labsCatalog,
            ownedBotIds,
          ),
    [labsCatalog, ownedBotIds, roster],
  );
  const ownedPlayBots = useMemo(() => {
    const playableIds = new Set([
      ...readyOwnedBots.map((candidate) => candidate.id),
      ...labsOwnedBots.map((candidate) => candidate.id),
    ]);
    return (roster ?? []).filter((candidate) => playableIds.has(candidate.id));
  }, [labsOwnedBots, readyOwnedBots, roster]);
  const selectedOwnedBot =
    ownedPlayBots.find((candidate) => candidate.id === selectedBotId) ?? null;
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
  const rosterBot =
    bot === null
      ? null
      : (roster ?? []).find((candidate) => candidate.id === bot.id) ?? null;
  const labsPlaylists = useMemo(
    () =>
      rosterBot !== null && labsCatalog !== null
        ? eligibleLabsPlaylistsForRosterBot(rosterBot, labsCatalog)
        : [],
    [labsCatalog, rosterBot],
  );
  const playlist =
    labsPlaylists.find(
      (candidate) => candidate.playlistVersionId === playlistId,
    ) ?? labsPlaylists[0] ?? null;
  const labsOpponents = useMemo(
    () =>
      playlist === null || bot === null
        ? []
        : eligibleLabsOpponents(
            roster ?? [],
            bot.id,
            playlist.requiredContractProfileId,
          ),
    [bot, playlist, roster],
  );
  const duelEligible = launchPlayability?.playable === true ||
    (launch?.bot === null &&
      bot !== null &&
      playabilityById.get(bot.id)?.playable === true);
  const labsEligible = bot?.isOwner === true && labsPlaylists.length > 0;
  const availableModes = useMemo(
    () => {
      if (!launch) return [];
      if (!bot) {
        return launch.modes.filter((candidate) =>
          candidate === 'labs'
            ? labsOwnedBots.length > 0
            : readyOwnedBots.length > 0,
        );
      }
      return launch.modes.filter((candidate) => {
        if (candidate === 'labs') return labsEligible;
        if (candidate === 'ranked') return bot.isOwner && duelEligible;
        return duelEligible;
      });
    },
    [
      bot,
      duelEligible,
      labsEligible,
      labsOwnedBots.length,
      launch,
      readyOwnedBots.length,
    ],
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
  const challengeTargetsContextBot =
    launch?.bot !== null &&
    launch?.bot !== undefined &&
    launch.challengeContextRole === 'opponent';
  const challengeEntrants = useMemo(
    () =>
      challengeTargetsContextBot && bot
        ? readyOwnedBots.filter((candidate) => candidate.id !== bot.id)
        : readyOwnedBots,
    [bot, challengeTargetsContextBot, readyOwnedBots],
  );
  const selectableOwnedBots = challengeTargetsContextBot
    ? challengeEntrants
    : ownedPlayBots;

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (launch && !dialog.open) dialog.showModal();
    if (!launch && dialog.open) dialog.close();
  }, [launch]);

  // A fixed target needs one of mine; a global launch needs the bot to play as. Remove
  // the select only when there is genuinely one answer.
  useEffect(() => {
    if (
      !launch ||
      !challengeTargetsContextBot ||
      selectedBotId !== '' ||
      selectableOwnedBots.length !== 1
    )
      return;
    setSelectedBotId(selectableOwnedBots[0].id);
  }, [
    challengeTargetsContextBot,
    launch,
    selectableOwnedBots,
    selectedBotId,
  ]);

  // The global launcher is a quick path, so it must open in a state that can actually
  // start. Keep the chooser visible, but preselect the first Duel-ready bot (falling
  // back to any playable bot for a Labs-only account) instead of presenting a disabled
  // primary action and waiting for the player to discover why.
  useEffect(() => {
    if (
      !launch ||
      launch.bot !== null ||
      selectedBotId !== '' ||
      ownedPlayBots.length === 0
    )
      return;
    const preferredBot = readyOwnedBots[0] ?? ownedPlayBots[0];
    setSelectedBotId(preferredBot.id);
  }, [launch, ownedPlayBots, readyOwnedBots, selectedBotId]);

  // Eligibility can change while the composer is open. Never leave an invisible,
  // previously valid bot selected after a capabilities refresh.
  useEffect(() => {
    if (!launch || selectedBotId === '') return;
    const candidates =
      launch.bot === null
        ? ownedPlayBots
        : challengeTargetsContextBot
          ? challengeEntrants
          : null;
    if (
      candidates !== null &&
      !candidates.some((candidate) => candidate.id === selectedBotId)
    ) {
      setSelectedBotId('');
      if (launch.bot === null) setChoosingBot(true);
    }
  }, [
    challengeEntrants,
    challengeTargetsContextBot,
    launch,
    ownedPlayBots,
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
      !challengeTargetsContextBot &&
      roster !== null &&
      opponentId !== '' &&
      !opponents.some((candidate) => candidate.id === opponentId)
    )
      setOpponentId('');
  }, [
    challengeTargetsContextBot,
    mode,
    opponentId,
    opponents,
    roster,
  ]);

  useEffect(() => {
    if (
      mode === 'labs' &&
      roster !== null &&
      opponentId !== '' &&
      !labsOpponents.some((candidate) => candidate.id === opponentId)
    )
      setOpponentId('');
  }, [labsOpponents, mode, opponentId, roster]);

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
    setOpponentId('');
    challenge.reset();
    rankedChallenge.reset();
    labsMatch.reset();
  };
  const actions = useMemo<ArenaActions>(
    () => ({
      launch: (request, nextMode, trigger) => {
        triggerRef.current = trigger;
        setLaunch(request);
        setMode(nextMode);
        setSelectedBotId('');
        setChoosingBot(request.bot === null);
        setOpponentId(request.initialOpponentId);
        setMapId(request.initialMapId);
        setPlaylistId('');
        challenge.reset();
        rankedChallenge.reset();
        labsMatch.reset();
      },
    }),
    // Mutations remain mounted with the provider and their reset functions are stable.
    // The context value changes only if either hook replaces that function.
    [challenge.reset, labsMatch.reset, rankedChallenge.reset],
  );

  const selectedModeEligible =
    mode === 'labs' ? labsEligible : duelEligible;

  const play = async (event: React.FormEvent) => {
    event.preventDefault();
    if (
      !launch ||
      !bot ||
      !selectedModeEligible ||
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
      if (mode === 'labs') {
        if (
          playlist === null ||
          !labsOpponents.some((candidate) => candidate.id === opponentId)
        )
          return;
        const match = await labsMatch.mutateAsync(
          createLabsMatchRequest(
            playlist.playlistVersionId,
            bot.id,
            opponentId,
          ),
        );
        close();
        navigate(`/matches/${match.id}`);
        return;
      }
      const participants = resolveChallengeParticipants(
        bot.id,
        launch.challengeContextRole,
        selectedBotId,
        opponentId,
      );
      const participantsAreValid =
        launch.challengeContextRole === 'opponent'
          ? challengeEntrants.some(
              (candidate) => candidate.id === participants.botId,
            ) && participants.opponentBotId === bot.id
          : participants.botId === bot.id &&
            opponents.some(
              (candidate) => candidate.id === participants.opponentBotId,
            );
      if (!participantsAreValid) return;
      const match = await challenge.mutateAsync({
        botId: participants.botId,
        opponentBotId: participants.opponentBotId,
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
  const failure =
    challenge.error ?? rankedChallenge.error ?? labsMatch.error;
  const busy =
    challenge.isPending ||
    rankedChallenge.isPending ||
    labsMatch.isPending;
  const challengeParticipants =
    bot === null
      ? { botId: '', opponentBotId: '' }
      : resolveChallengeParticipants(
          bot.id,
          launch?.challengeContextRole ?? 'opponent',
          selectedBotId,
          opponentId,
        );
  const challenger = challengeParticipants.botId;
  const opponent = challengeParticipants.opponentBotId;
  const challengeParticipantsValid =
    bot !== null &&
    launch !== null &&
    (launch.challengeContextRole === 'opponent'
      ? challengeEntrants.some(
          (candidate) => candidate.id === challengeParticipants.botId,
        ) && challengeParticipants.opponentBotId === bot.id
      : challengeParticipants.botId === bot.id &&
        opponents.some(
          (candidate) =>
            candidate.id === challengeParticipants.opponentBotId,
        ));
  const labsParticipantsValid =
    bot !== null &&
    playlist !== null &&
    labsOpponents.some((candidate) => candidate.id === opponentId);
  const entrantName =
    challenger === bot?.id
      ? bot.name
      : (roster ?? []).find((candidate) => candidate.id === challenger)?.name;
  const opponentName =
    opponent === bot?.id
      ? bot.name
      : (roster ?? []).find((candidate) => candidate.id === opponent)?.name;
  const arenaLoading =
    needsArena &&
    capabilities === null &&
    capabilitiesError === null;
  const rosterLoading =
    needsRoster && roster === null && rosterError === null;
  const labsLoading =
    needsLabs && labsCatalog === null && labsError === null;

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
        data-layout={
          launch?.bot !== null && mode === 'ranked' ? 'compact' : 'full'
        }
        className="arena-dialog panel w-[min(560px,calc(100vw-24px))] p-0 text-left text-arena-text"
      >
        {launch && (
          <>
            <header className="flex items-start gap-3 border-b border-arena-edge px-4 py-3.5">
              <div className="min-w-0 grow">
                <span className="lab mb-1 block">Arena</span>
                <h2
                  id={titleId}
                  className="type-display text-[20px] leading-tight text-arena-text"
                >
                  {arenaDialogTitle(
                    bot,
                    mode,
                    launch.challengeContextRole,
                  )}
                </h2>
                {bot &&
                  launch.bot !== null &&
                  !(
                    mode === 'challenge' &&
                    launch.challengeContextRole === 'opponent'
                  ) && (
                    <span className="mt-2 flex min-w-0 items-center gap-2">
                      <BotIdentity
                        name={bot.name}
                        accent={bot.accent}
                        lookId={bot.lookId}
                        size="xs"
                      />
                    </span>
                  )}
              </div>
              <button
                type="button"
                onClick={close}
                className="btn min-h-11 shrink-0"
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
            ) : labsLoading && availableModes.length === 0 ? (
              <p className="t-meta px-4 py-8 text-center" role="status">
                Checking hosted experiments…
              </p>
            ) : labsError &&
              availableModes.length === 0 &&
              launch.modes.includes('labs') ? (
              <QueryIssue
                error={labsError}
                fallback="Hosted experiments could not be loaded."
                onRetry={() => void refetchLabs()}
              />
            ) : launch.bot && availableModes.length === 0 ? (
              <Unavailable
                bot={bot ?? launch.bot}
                mode={mode}
                playability={launchPlayability}
                onClose={close}
                onRetry={() => {
                  void refetchCapabilities();
                  void refetchRoster();
                }}
              />
            ) : launch.bot === null && ownedPlayBots.length === 0 ? (
              <NoOwnedBot onClose={close} />
            ) : capabilities !== null ? (
              <form onSubmit={play} className="arena-form">
                <div className="arena-form-body pad">
                  {launch.bot === null && bot !== null && !choosingBot ? (
                    <section
                      className="bot-workbench panel flex min-w-0 items-center gap-3 p-3"
                      style={
                        bot.accent
                          ? styleVariables({
                              '--player-accent': playerAccent(
                                bot.accent,
                                'panel',
                              ),
                            })
                          : undefined
                      }
                    >
                      <span className="min-w-0 grow">
                        <span className="lab mb-1 block">
                          Your bot · selected
                        </span>
                        <BotIdentity
                          name={bot.name}
                          accent={bot.accent}
                          lookId={bot.lookId}
                          size="sm"
                          emphasized
                        />
                      </span>
                      <button
                        type="button"
                        onClick={() => setChoosingBot(true)}
                        className="btn min-h-11 shrink-0"
                      >
                        Change
                      </button>
                    </section>
                  ) : launch.bot === null ? (
                    <BotChoiceTiles
                      label="Choose your bot"
                      candidates={ownedPlayBots}
                      selectedId={selectedBotId}
                      onChange={(nextBotId) => {
                        setSelectedBotId(nextBotId);
                        setChoosingBot(false);
                        setOpponentId('');
                        setPlaylistId('');
                        challenge.reset();
                        rankedChallenge.reset();
                        labsMatch.reset();
                      }}
                      detail={(candidate) => {
                        const duel =
                          playabilityById.get(candidate.id)?.playable === true;
                        const labs = labsOwnedBots.some(
                          (entry) => entry.id === candidate.id,
                        );
                        const modes =
                          duel && labs
                            ? 'Duel + Labs'
                            : duel
                              ? 'Duel'
                              : 'Labs';
                        return `gen ${candidate.activeVersion?.versionNumber ?? '—'} · ${modes}`;
                      }}
                    />
                  ) : null}

                {availableModes.length > 1 && (
                  <div
                    className={clsx(
                      'grid gap-2',
                      availableModes.length === 3
                        ? 'grid-cols-3'
                        : 'grid-cols-2',
                    )}
                    role="group"
                    aria-label="How to play"
                  >
                    {availableModes.map((candidate) => (
                      <button
                        key={candidate}
                        type="button"
                        aria-pressed={mode === candidate}
                        onClick={() => chooseMode(candidate)}
                        className="choice-card min-h-[84px] px-2.5"
                      >
                        <span className="t-body block font-semibold">
                          {arenaModeLabel(candidate)}
                        </span>
                        <span className="t-micro mt-1 block">
                          {arenaModeDescription(
                            candidate,
                            challengeTargetsContextBot,
                          )}
                        </span>
                      </button>
                    ))}
                  </div>
                )}

                {mode === 'ranked' ? (
                  <RankedChoice format={capabilities.format.ranked} />
                ) : mode === 'labs' ? (
                  bot === null ? (
                    <>
                      <LabsSummary />
                      <p className="t-meta">
                        Choose a compatible bot to see its experiments.
                      </p>
                    </>
                  ) : (
                    <LabsChoice
                      playlists={labsPlaylists}
                      playlist={playlist}
                      opponentId={opponentId}
                      opponents={labsOpponents}
                      onPlaylistChange={(nextPlaylistId) => {
                        setPlaylistId(nextPlaylistId);
                        setOpponentId('');
                        labsMatch.reset();
                      }}
                      onOpponentChange={setOpponentId}
                    />
                  )
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
                    contextRole={launch.challengeContextRole}
                    challengerId={selectedBotId}
                    opponentId={opponentId}
                    mapId={mapId}
                    ownedBots={challengeEntrants}
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
                    onClose={close}
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
                {labsLoading && launch.modes.includes('labs') && (
                  <p className="t-meta" role="status">
                    Checking hosted experiments… Available Arena play can start
                    now.
                  </p>
                )}
                {labsError &&
                  availableModes.length > 0 &&
                  launch.modes.includes('labs') && (
                    <QueryIssue
                      error={labsError}
                      fallback="Hosted experiments could not be loaded. Other available Play modes still work."
                      onRetry={() => void refetchLabs()}
                      compact
                    />
                  )}
                </div>

                <div className="arena-form-submit">
                  <ArenaAllowanceStatus
                    mode={mode}
                    capabilities={capabilities}
                    refreshing={capabilitiesRefreshing}
                    onRefresh={() => void refetchCapabilities()}
                  />
                  {failure && (
                    <p
                      className="t-body mb-2.5 text-arena-hot"
                      role="alert"
                    >
                      {errorMessage(
                        failure,
                        'The match could not be started.',
                      )}
                    </p>
                  )}
                  <button
                    type="submit"
                    disabled={
                      busy ||
                      !bot ||
                      !selectedModeEligible ||
                      !availableModes.includes(mode) ||
                      !(mode === 'ranked'
                        ? capabilities.rankedAllowance.canStart
                        : capabilities.unrankedAllowance.canStart) ||
                      !arenaModeParticipantsReady(
                        mode,
                        challengeParticipantsValid,
                        labsParticipantsValid,
                      )
                    }
                    className="btn btn-strong min-h-12 w-full truncate justify-center px-3"
                  >
                    {busy
                      ? 'Starting…'
                      : mode === 'ranked'
                        ? 'Start ranked set'
                        : mode === 'labs'
                          ? 'Run lab match'
                          : entrantName && opponentName
                            ? `Start ${entrantName} vs ${opponentName}`
                            : 'Start challenge'}
                  </button>
                </div>
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
  challengeContextRole: requestedChallengeContextRole,
  triggerLabel,
  className,
}: ArenaActionProps) {
  const actions = useArenaActions();
  const modes =
    requestedModes ??
    (bot.isOwner ? ['ranked', 'challenge', 'labs'] : ['challenge']);
  const normalizedModes = modes as readonly ArenaMode[];
  const firstMode =
    initialMode && normalizedModes.includes(initialMode)
      ? initialMode
      : normalizedModes[0] ?? 'challenge';
  const challengeContextRole =
    requestedChallengeContextRole ??
    defaultChallengeContextRole();
  const launch: ArenaLaunch = {
    bot,
    modes: normalizedModes,
    challengeContextRole,
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
        challengeContextRole={challengeContextRole}
        triggerLabel={triggerLabel}
        onOpen={(mode, trigger) => actions.launch(launch, mode, trigger)}
      />
    </span>
  );
}

/** Always-available entry: the dialog first asks which compatible owned bot should play. */
export function GlobalArenaAction({
  className,
}: {
  className?: string;
}) {
  const actions = useArenaActions();
  const modes: readonly ArenaMode[] = ['ranked', 'challenge', 'labs'];
  const launch: ArenaLaunch = {
    bot: null,
    modes,
    challengeContextRole: 'entrant',
    initialOpponentId: '',
    initialMapId: '',
  };
  return (
    <button
      type="button"
      aria-haspopup="dialog"
      aria-label="Play"
      onClick={(event) => actions.launch(launch, 'ranked', event.currentTarget)}
      className={clsx(
        'arena-launcher arena-launcher-global shrink-0',
        className,
      )}
    >
      <ArenaLauncherFace label="Play" showKicker />
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
  challengeContextRole,
  triggerLabel,
  onOpen,
}: {
  botName: string;
  modes: readonly ArenaMode[];
  defaultMode: ArenaMode;
  variant: 'multi' | 'compact';
  challengeContextRole: ChallengeContextRole;
  triggerLabel?: string;
  onOpen: (mode: ArenaMode, trigger: HTMLButtonElement) => void;
}) {
  if (variant === 'multi' && modes.length > 1) {
    return (
      <span
        role="group"
        aria-label={`Arena actions for ${botName}`}
        className="inline-flex"
      >
        {modes.map((mode, index) => (
          <button
            key={mode}
            type="button"
            aria-haspopup="dialog"
            aria-label={
              mode === 'challenge' &&
              challengeContextRole === 'opponent'
                ? `Challenge ${botName}`
                : `${arenaModeLabel(mode)} with ${botName}`
            }
            onClick={(event) => onOpen(mode, event.currentTarget)}
            className={clsx(
              'btn btn-strong min-h-11',
              index === 0
                ? 'rounded-r-none'
                : index === modes.length - 1
                  ? '-ml-px rounded-l-none'
                  : '-ml-px rounded-none',
            )}
          >
            {arenaModeLabel(mode)}
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
      : arenaModeLabel(mode));
  return (
    <button
      type="button"
      aria-haspopup="dialog"
      aria-label={
        mode === 'challenge' && challengeContextRole === 'opponent'
          ? `Challenge ${botName}`
          : `${label} with ${botName}`
      }
      onClick={(event) => onOpen(mode, event.currentTarget)}
      className="arena-launcher arena-launcher-context"
    >
      <ArenaLauncherFace label={label} />
    </button>
  );
}

function ArenaLauncherFace({
  label,
  showKicker = false,
}: {
  label: string;
  showKicker?: boolean;
}) {
  return (
    <>
      <span aria-hidden="true" className="arena-launcher-mark">
        <span className="arena-launcher-glyph" />
      </span>
      <span className="arena-launcher-copy">
        {showKicker && (
          <span aria-hidden="true" className="arena-launcher-kicker">
            Arena
          </span>
        )}
        <span className="arena-launcher-label">{label}</span>
      </span>
      <span aria-hidden="true" className="arena-launcher-chevron" />
    </>
  );
}

function RankedChoice({
  format,
}: {
  format: ArenaCapabilities['format']['ranked'];
}) {
  return (
    <section className="panel-quiet pad">
      <h2 className="lab mb-2.5">Ranked set</h2>
      <span className="grid grid-cols-3 gap-3">
        <ArenaFormatStat value={format.gamesPerSet} label="games" />
        <ArenaFormatStat value={format.mapSeedPairs} label="map pairs" />
        <ArenaFormatStat
          value={format.matchmakingPoolSize}
          label="rival pool"
        />
      </span>
      <p className="t-micro mt-2.5">
        {format.mirroredSlots ? 'Both starting sides · ' : ''}
        rating moves once after the full set.
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
  return (
    <section className="panel-quiet pad">
      <h2 className="lab mb-2.5">One-off challenge</h2>
      <span className="grid grid-cols-2 gap-3">
        <ArenaFormatStat value={gamesPerMatch} label="games" />
        <ArenaFormatStat value={defaultMapId} label="default map" />
      </span>
      <p className="t-micro mt-2.5">
        Unranked · no rating change.
      </p>
    </section>
  );
}

function LabsSummary({ name }: { name?: string }) {
  return (
    <section className="panel-quiet pad">
      <h2 className="lab mb-2">
        {name ? `${name} · unranked` : 'Labs · unranked'}
      </h2>
      <p className="t-micro">
        Experimental two-bot game · no rating change.
      </p>
    </section>
  );
}

function ArenaFormatStat({
  value,
  label,
}: {
  value: number | string;
  label: string;
}) {
  return (
    <span>
      <span className="val block text-[17px] text-arena-text">{value}</span>
      <span className="t-micro mt-0.5 block">{label}</span>
    </span>
  );
}

function LabsChoice({
  playlists,
  playlist,
  opponentId,
  opponents,
  onPlaylistChange,
  onOpponentChange,
}: {
  playlists: readonly LabsPlaylist[];
  playlist: LabsPlaylist | null;
  opponentId: string;
  opponents: readonly BotSummary[];
  onPlaylistChange: (id: string) => void;
  onOpponentChange: (id: string) => void;
}) {
  if (playlist === null) {
    return (
      <section className="panel-quiet pad">
        <h2 className="lab mb-2">No compatible experiment</h2>
        <p className="t-meta">
          This active generation cannot run a hosted experiment right now.
        </p>
      </section>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <LabsSummary name={playlist.displayName} />
      {playlists.length > 1 && (
        <fieldset>
          <legend className="lab mb-2">Choose experiment</legend>
          <div className="grid gap-2 sm:grid-cols-2">
            {playlists.map((candidate) => (
              <button
                key={candidate.playlistVersionId}
                type="button"
                aria-pressed={
                  candidate.playlistVersionId === playlist.playlistVersionId
                }
                onClick={() =>
                  onPlaylistChange(candidate.playlistVersionId)
                }
                className="choice-card"
              >
                <span className="t-body block font-semibold">
                  {candidate.displayName}
                </span>
                <span className="t-micro mt-1 block">
                  Experimental · unranked
                </span>
              </button>
            ))}
          </div>
        </fieldset>
      )}
      {opponents.length === 0 ? (
        <p className="t-meta">
          No compatible opponent is active yet.
        </p>
      ) : (
        <BotChoiceTiles
          label="Choose opponent"
          candidates={opponents}
          selectedId={opponentId}
          onChange={onOpponentChange}
          showOwner
        />
      )}
    </div>
  );
}

function ChallengeChoice({
  bot,
  contextRole,
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
  onClose,
}: {
  bot: ArenaActionBot;
  contextRole: ChallengeContextRole;
  challengerId: string;
  opponentId: string;
  mapId: string;
  ownedBots: readonly BotSummary[];
  opponents: readonly BotSummary[];
  maps: readonly { id: string; width: number; height: number }[];
  defaultMapId: string;
  gamesPerMatch: number;
  onChallengerChange: (id: string) => void;
  onOpponentChange: (id: string) => void;
  onMapChange: (id: string) => void;
  onClose: () => void;
}) {
  const targetsContextBot = contextRole === 'opponent';

  if (targetsContextBot && ownedBots.length === 0) {
    return (
      <section className="panel-quiet pad">
        <h2 className="lab mb-2">You need a ready bot</h2>
        <p className="t-meta">
          Submit and activate {bot.isOwner ? 'another' : 'a'} successful build
          before challenging {bot.name}.{' '}
          <Link to="/garage" onClick={onClose} className="text-link">
            Open your Garage
          </Link>
          .
        </p>
      </section>
    );
  }

  if (!targetsContextBot && opponents.length === 0) {
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
      {targetsContextBot && (
        <section
          className="panel-quiet flex min-w-0 items-center justify-between gap-3 p-3 sm:col-span-2"
          aria-label={`Fixed opponent: ${bot.name}`}
        >
          <span className="min-w-0">
            <span className="lab mb-1 block">Opponent · selected</span>
            <BotIdentity
              name={bot.name}
              accent={bot.accent}
              lookId={bot.lookId}
              size="sm"
              emphasized
            />
          </span>
          <span className="pill shrink-0">Locked</span>
        </section>
      )}
      {targetsContextBot && (
        <BotChoiceTiles
          label="Your bot"
          candidates={ownedBots}
          selectedId={challengerId}
          onChange={onChallengerChange}
          className="sm:col-span-2"
          detail={(candidate) =>
            `gen ${candidate.activeVersion?.versionNumber ?? '—'}`
          }
        />
      )}
      {!targetsContextBot && (
        <BotChoiceTiles
          label="Choose opponent"
          candidates={opponents}
          selectedId={opponentId}
          onChange={onOpponentChange}
          showOwner
          className="sm:col-span-2"
        />
      )}
      <details className="panel-quiet sm:col-span-2">
        <summary className="flex cursor-pointer list-none items-center gap-2 px-3 py-2.5">
          <span className="lab">Match options</span>
          <span className="val ml-auto">
            {mapId === '' ? defaultMapId : mapId}
          </span>
          <span aria-hidden className="text-arena-material">
            +
          </span>
        </summary>
        <label className="t-meta flex flex-col gap-1 border-t border-arena-edge p-3">
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
      </details>
    </div>
  );
}

function BotChoiceTiles({
  label,
  candidates,
  selectedId,
  onChange,
  showOwner = false,
  detail,
  className,
}: {
  label: string;
  candidates: readonly BotSummary[];
  selectedId: string;
  onChange: (id: string) => void;
  showOwner?: boolean;
  detail?: (bot: BotSummary) => string;
  className?: string;
}) {
  const groupName = useId();
  const [query, setQuery] = useState('');
  const showSearch = candidates.length > MAX_BOT_CHOICES;
  const normalizedQuery = showSearch
    ? query.trim().toLocaleLowerCase()
    : '';
  const matches =
    normalizedQuery === ''
      ? candidates
      : candidates.filter((candidate) =>
          [candidate.name, candidate.owner]
            .filter(Boolean)
            .some((value) =>
              value.toLocaleLowerCase().includes(normalizedQuery),
            ),
        );
  const firstMatches = matches.slice(0, MAX_BOT_CHOICES);
  const selectedCandidate =
    candidates.find((candidate) => candidate.id === selectedId) ?? null;
  const visibleCandidates =
    selectedCandidate !== null &&
    !firstMatches.some((candidate) => candidate.id === selectedCandidate.id)
      ? [selectedCandidate, ...firstMatches.slice(0, MAX_BOT_CHOICES - 1)]
      : firstMatches;
  const visibleMatchCount = visibleCandidates.filter((candidate) =>
    matches.some((match) => match.id === candidate.id),
  ).length;
  const selectedIsPinned =
    selectedCandidate !== null &&
    !matches.some((candidate) => candidate.id === selectedCandidate.id);

  return (
    <fieldset className={className}>
      <legend className="lab mb-2">{label}</legend>
      {showSearch && (
        <label className="mb-2.5 block">
          <span className="sr-only">Find a bot in {label.toLowerCase()}</span>
          <input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Find by bot or player"
            className="field min-h-11 w-full"
          />
        </label>
      )}
      {showSearch && (
        <p className="t-micro mb-2" aria-live="polite">
          {matches.length === 0
            ? selectedCandidate
              ? 'No other bots match. Keeping your selection visible.'
              : 'No bots match that search.'
            : `Showing ${visibleMatchCount} of ${matches.length} matching ${matches.length === 1 ? 'bot' : 'bots'}.${selectedIsPinned ? ' Your selection stays visible.' : ''}`}
        </p>
      )}
      <div className="grid gap-2 sm:grid-cols-2">
        {visibleCandidates.map((candidate) => (
          <label
            key={candidate.id}
            data-selected={selectedId === candidate.id}
            className="choice-card"
            style={styleVariables({
              '--player-accent': playerAccent(candidate.accent, 'panel'),
            })}
          >
            <input
              type="radio"
              name={groupName}
              value={candidate.id}
              checked={selectedId === candidate.id}
              onChange={() => onChange(candidate.id)}
              className="sr-only"
            />
            <BotIdentity
              name={candidate.name}
              accent={candidate.accent}
              lookId={candidate.lookId}
              size="sm"
              emphasized={selectedId === candidate.id}
            />
            {(showOwner || detail) && (
              <span className="t-micro mt-1.5 block truncate pl-8">
                {[showOwner ? candidate.owner : null, detail?.(candidate)]
                  .filter((part) => part)
                  .join(' · ')}
              </span>
            )}
          </label>
        ))}
      </div>
    </fieldset>
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
  const units =
    mode === 'ranked'
      ? 'ranked sets'
      : mode === 'labs'
        ? 'unranked matches'
        : 'challenges';
  const window = `${allowance.rollingWindowHours}h rolling window`;

  return (
    <section
      className="pb-2.5"
      aria-label={`${arenaModeLabel(mode)} allowance`}
    >
      <span className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="lab">
          {mode === 'labs' ? 'Shared allowance' : 'Allowance'}
        </span>
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
      {mode === 'labs' && (
        <p className="t-micro mt-1">
          Labs and one-off challenges share this unranked allowance.
          Experiments may apply additional run limits when they start.
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
        className="btn btn-strong inline-flex"
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
      <h2 className="lab mb-2">You need a compatible bot</h2>
      <p className="t-meta">
        Submit and activate a generation that supports Duel or a hosted
        experiment before entering the arena.
      </p>
      <Link to="/garage" onClick={onClose} className="btn mt-3 inline-flex">
        Open your Garage
      </Link>
    </section>
  );
}

function Unavailable({
  bot,
  mode,
  playability,
  onClose,
  onRetry,
}: {
  bot: ArenaActionBot;
  mode: ArenaMode;
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
  const labsUnavailable = mode === 'labs' && !missingBuild;
  const botKey = bot.slug ?? bot.id;
  return (
    <section className="pad">
      <h2 className="lab mb-2">
        {playability === null
          ? 'Arena eligibility changed'
          : labsUnavailable
            ? 'No compatible experiment'
          : incompatible
            ? 'Not available in Duel'
            : missingBuild
            ? 'No active generation'
            : 'Unavailable in the Arena'}
      </h2>
      <p className="t-meta">
        {labsUnavailable
          ? `${bot.name}'s active generation does not match an experiment running right now.`
          : playability?.refusalDetail ??
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

function arenaDialogTitle(
  bot: ArenaActionBot | null,
  mode: ArenaMode,
  challengeContextRole: ChallengeContextRole,
) {
  if (!bot) {
    return mode === 'ranked'
      ? 'Choose a bot for a ranked set'
      : mode === 'labs'
        ? 'Choose a bot for a lab match'
        : 'Choose a bot for a one-off challenge';
  }
  if (mode === 'ranked') return `Ranked set with ${bot.name}`;
  if (mode === 'labs') return `Lab match with ${bot.name}`;
  return challengeContextRole === 'opponent'
    ? `Challenge ${bot.name}`
    : `Challenge with ${bot.name}`;
}

function arenaModeLabel(mode: ArenaMode) {
  if (mode === 'ranked') return 'Ranked set';
  if (mode === 'labs') return 'Labs';
  return 'Challenge';
}

function arenaModeDescription(
  mode: ArenaMode,
  challengeTargetsContextBot = false,
) {
  if (mode === 'ranked') return 'Matchmade · rated';
  if (mode === 'labs') return 'Experimental · unranked';
  return challengeTargetsContextBot
    ? 'Choose your bot · unranked'
    : 'Choose rival · unranked';
}

/**
 * Challenge is target-first unless a caller explicitly says that its contextual bot
 * should enter. Presentation variants must never be able to reverse participants.
 */
export function defaultChallengeContextRole(): ChallengeContextRole {
  return 'opponent';
}

export function resolveChallengeParticipants(
  contextBotId: string,
  contextRole: ChallengeContextRole,
  selectedBotId: string,
  selectedOpponentId: string,
) {
  return contextRole === 'opponent'
    ? {
        botId: selectedBotId,
        opponentBotId: contextBotId,
      }
    : {
        botId: contextBotId,
        opponentBotId: selectedOpponentId,
      };
}

export function arenaModeParticipantsReady(
  mode: ArenaMode,
  challengeParticipantsValid: boolean,
  labsParticipantsValid: boolean,
) {
  if (mode === 'challenge') return challengeParticipantsValid;
  if (mode === 'labs') return labsParticipantsValid;
  return true;
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
    : mode === 'labs'
      ? 'A lab match cannot start right now.'
      : 'A challenge cannot start right now.';
}
