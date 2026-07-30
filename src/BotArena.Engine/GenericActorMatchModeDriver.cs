using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Internal closed seam between mode-neutral world orchestration and one
/// mode's scoring, public projection, and completion semantics.
/// </summary>
internal interface IGenericActorMatchModeDriver
{
    GenericActorModeState State { get; }

    GenericActorModeTickResult ApplyJointTick(
        GenericActorModeWorldView world,
        GenericActorModeTickInput input);

    GenericActorModeCompletion ResolveCompletion(
        GenericActorModeCompletionKind kind,
        int endTick,
        GenericActorModeWorldView world);

    GenericActorModeProjection Project(
        GenericActorModeWorldView world);

    /// <summary>
    /// The upgrade tracks one team may legally buy the next tier of right
    /// now, in declared order. Empty for every mode that owns no store, which
    /// is what keeps the mode-investment action's legality mask closed on a
    /// contract that does not declare it.
    /// <para>This and <see cref="TryInvest"/> are the mode driver's first
    /// ACTION surface: until the scrap economy it only observed post-combat
    /// world state. Modes owning verbs is the direction, and it is why the
    /// seam widened here rather than in the session.</para>
    /// </summary>
    IReadOnlyList<string> InvestableTracks(int teamId);

    /// <summary>
    /// Spends one tier for the acting body's team, in the action phase.
    /// Returns false when the mode owns no store, the track is unknown, the
    /// bank no longer covers it, or a cap is reached — the caller then blocks
    /// the action exactly as it blocks any other unavailable verb.
    /// </summary>
    /// <param name="tick">The tick being resolved.</param>
    /// <param name="actor">The casting body.</param>
    /// <param name="trackId">The requested track.</param>
    bool TryInvest(int tick, ActorIdentity actor, string trackId);

    /// <summary>
    /// The typed stat modifiers this mode currently applies to one body.
    /// <see cref="GenericActorModeStatModifiers.None"/> for every mode that
    /// applies none, so every historical contract resolves its declared form
    /// stats unchanged.
    /// </summary>
    GenericActorModeStatModifiers StatModifiersFor(ActorIdentity actor);
}

internal abstract record GenericActorModeState
{
    private GenericActorModeState()
    {
    }

    internal sealed record Deathmatch : GenericActorModeState
    {
        public Deathmatch(DeathmatchScoreState scores)
        {
            ArgumentNullException.ThrowIfNull(scores);
            Scores = scores;
        }

        public DeathmatchScoreState Scores { get; }
    }

    internal sealed record Frontline : GenericActorModeState
    {
        public Frontline(
            FrontlineControlState control,
            FrontlineScoreState scores)
        {
            ArgumentNullException.ThrowIfNull(control);
            ArgumentNullException.ThrowIfNull(scores);
            Control = control;
            Scores = scores;
        }

        public FrontlineControlState Control { get; }
        public FrontlineScoreState Scores { get; }
    }
}

internal sealed record GenericActorModeWorldView
{
    public GenericActorModeWorldView(
        IReadOnlyDictionary<int, long> activeHealthByTeam,
        IReadOnlyCollection<int> eligibleTeamIds,
        IReadOnlyCollection<GenericActorModeActiveLife> activeLives)
    {
        ArgumentNullException.ThrowIfNull(activeHealthByTeam);
        ArgumentNullException.ThrowIfNull(eligibleTeamIds);
        ArgumentNullException.ThrowIfNull(activeLives);

        KeyValuePair<int, long>[] health = activeHealthByTeam
            .OrderBy(entry => entry.Key)
            .ToArray();
        if (health.Length == 0
            || health.Any(entry => entry.Key < 0 || entry.Value < 0))
        {
            throw new ArgumentException(
                "Mode world health must be non-empty with non-negative teams and values.",
                nameof(activeHealthByTeam));
        }

        int[] eligible = [.. eligibleTeamIds];
        if (eligible.Any(teamId => teamId < 0)
            || eligible.Distinct().Count() != eligible.Length
            || eligible.Any(teamId =>
                !activeHealthByTeam.ContainsKey(teamId)))
        {
            throw new ArgumentException(
                "Eligible mode teams must be unique members of the health snapshot.",
                nameof(eligibleTeamIds));
        }

        GenericActorModeActiveLife[] lives = [.. activeLives];
        if (lives.Any(life => life is null)
            || lives
                .Select(life => life.ActorId)
                .Distinct()
                .Count() != lives.Length
            || lives.Any(life =>
                !activeHealthByTeam.ContainsKey(life.ActorId.TeamId)))
        {
            throw new ArgumentException(
                "Mode active lives must be non-null, unique, and belong to a health team.",
                nameof(activeLives));
        }
        Dictionary<int, long> summedHealth = health.ToDictionary(
            entry => entry.Key,
            _ => 0L);
        foreach (GenericActorModeActiveLife life in lives)
        {
            summedHealth[life.ActorId.TeamId] = checked(
                summedHealth[life.ActorId.TeamId] + life.Health);
        }
        if (health.Any(entry => summedHealth[entry.Key] != entry.Value))
        {
            throw new ArgumentException(
                "Mode active lives must exactly reproduce team active health.",
                nameof(activeLives));
        }

        ActiveHealthByTeam = health.ToImmutableSortedDictionary();
        EligibleTeamIds = eligible
            .Order()
            .ToImmutableArray();
        ActiveLives = lives
            .OrderBy(life => life.ActorId)
            .ToImmutableArray();
    }

    public ImmutableSortedDictionary<int, long> ActiveHealthByTeam
    {
        get;
    }

    public ImmutableArray<int> EligibleTeamIds { get; }
    public ImmutableArray<GenericActorModeActiveLife> ActiveLives { get; }
}

internal sealed record GenericActorModeActiveLife
{
    public GenericActorModeActiveLife(
        ActorIdentity actorId,
        string formId,
        Position position,
        long health,
        Position? positionAtPreviousTickEnd = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formId);
        if (health <= 0)
            throw new ArgumentOutOfRangeException(nameof(health));

        ActorId = actorId;
        FormId = formId;
        Position = position;
        Health = health;
        PositionAtPreviousTickEnd = positionAtPreviousTickEnd;
    }

    public ActorIdentity ActorId { get; }
    public string FormId { get; }
    public Position Position { get; }
    public long Health { get; }

    /// <summary>
    /// Where this life stood at the end of the previous tick, or null when it
    /// had no previous tick. A mode that gates anything on stillness compares
    /// it to <see cref="Position"/>; a life with no previous position counts
    /// as stationary.
    /// </summary>
    public Position? PositionAtPreviousTickEnd { get; }

    /// <summary>
    /// True when this life did not change tile this tick. Positional, not
    /// intentional: a requested move that was blocked did not move, and
    /// rotating, shooting, or starting a transform never breaks it.
    /// </summary>
    public bool IsStationary =>
        PositionAtPreviousTickEnd is not { } previous
        || previous == Position;
}

internal sealed record GenericActorModeTickInput
{
    public GenericActorModeTickInput(
        int tick,
        IReadOnlyCollection<GenericActorModeDamageContact> damageContacts,
        IReadOnlyCollection<FrontlineScrapDestruction>? destructions = null)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        ArgumentNullException.ThrowIfNull(damageContacts);

        GenericActorModeDamageContact[] contacts = [.. damageContacts];
        if (contacts.Any(contact => contact is null))
        {
            throw new ArgumentException(
                "Mode damage contacts cannot contain null entries.",
                nameof(damageContacts));
        }

        FrontlineScrapDestruction[] deaths = [.. destructions ?? []];
        if (deaths.Any(death => death is null)
            || deaths
                .Select(death => death.ActorId)
                .Distinct()
                .Count() != deaths.Length)
        {
            throw new ArgumentException(
                "Mode destructions must be non-null and name each life once.",
                nameof(destructions));
        }

        Tick = tick;
        DamageContacts = contacts.ToImmutableArray();
        Destructions = deaths
            .OrderBy(death => death.ActorId)
            .ToImmutableArray();
    }

    public int Tick { get; }

    /// <summary>
    /// Preserves the world's already-canonical authoritative damage order.
    /// </summary>
    public ImmutableArray<GenericActorModeDamageContact> DamageContacts
    {
        get;
    }

    /// <summary>
    /// Lives destroyed this tick with the tile they died on, in canonical
    /// order. A mode that places anything at a death site needs the
    /// destruction itself rather than the contact that caused it — the
    /// contact names where the bolt was, the destruction names where the body
    /// was. Empty for every mode that places nothing.
    /// </summary>
    public ImmutableArray<FrontlineScrapDestruction> Destructions { get; }
}

internal sealed record GenericActorModeScoreChange
{
    public GenericActorModeScoreChange(
        int teamId,
        string channel,
        long newValue)
    {
        if (teamId < 0)
            throw new ArgumentOutOfRangeException(nameof(teamId));
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        TeamId = teamId;
        Channel = channel;
        NewValue = newValue;
    }

    public int TeamId { get; }
    public string Channel { get; }
    public long NewValue { get; }
}

internal sealed record GenericActorModeTickResult
{
    public GenericActorModeTickResult(
        IReadOnlyCollection<GenericActorModeScoreChange> scoreChanges,
        GenericActorRuntimeObservation.ModeObservationState? modeChange,
        bool modeObjectiveReached)
    {
        ArgumentNullException.ThrowIfNull(scoreChanges);
        GenericActorModeScoreChange[] changes = [.. scoreChanges];
        if (changes.Any(change => change is null)
            || changes
                .Select(change => (change.TeamId, change.Channel))
                .Distinct()
                .Count() != changes.Length)
        {
            throw new ArgumentException(
                "Mode score changes must be non-null and unique per team/channel.",
                nameof(scoreChanges));
        }

        ScoreChanges = changes.ToImmutableArray();
        ModeChange = modeChange;
        ModeObjectiveReached = modeObjectiveReached;
    }

    /// <summary>Exact mode-owned event order for this joint tick.</summary>
    public ImmutableArray<GenericActorModeScoreChange> ScoreChanges { get; }
    public GenericActorRuntimeObservation.ModeObservationState? ModeChange
    {
        get;
    }
    public bool ModeObjectiveReached { get; }
}

internal sealed record GenericActorModeProjection
{
    public GenericActorModeProjection(
        GenericActorRuntimeObservation.ScoreboardState scoreboard,
        GenericActorRuntimeObservation.ModeObservationState mode,
        IReadOnlyDictionary<ActorIdentity, int>? carriedScrapByActor = null)
    {
        ArgumentNullException.ThrowIfNull(scoreboard);
        ArgumentNullException.ThrowIfNull(mode);

        Scoreboard = scoreboard;
        Mode = mode;
        CarriedScrapByActor = carriedScrapByActor is null
            ? ImmutableDictionary<ActorIdentity, int>.Empty
            : carriedScrapByActor.ToImmutableDictionary();
    }

    public GenericActorRuntimeObservation.ScoreboardState Scoreboard
    {
        get;
    }
    public GenericActorRuntimeObservation.ModeObservationState Mode { get; }

    /// <summary>
    /// Mode state that annotates BODIES rather than teams: what each live body
    /// is carrying, which the host stamps onto self, ally, and enemy states.
    /// Empty for every mode that annotates nothing, and a body carrying
    /// nothing is simply absent.
    /// </summary>
    public ImmutableDictionary<ActorIdentity, int> CarriedScrapByActor
    {
        get;
    }
}

internal enum GenericActorModeCompletionKind
{
    FaultEligibility = 0,
    ModeObjective = 1,
    MaxTicks = 2,
}

internal abstract record GenericActorModeCompletion
{
    private protected GenericActorModeCompletion(
        string completionReason,
        int endTick,
        TeamStandings standings,
        GenericActorMatchModeResult modeResult)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(completionReason);
        if (endTick < 0)
            throw new ArgumentOutOfRangeException(nameof(endTick));
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(modeResult);

        CompletionReason = completionReason;
        EndTick = endTick;
        Standings = standings;
        ModeResult = modeResult;
    }

    public string CompletionReason { get; }
    public int EndTick { get; }
    public TeamStandings Standings { get; }
    public GenericActorMatchModeResult ModeResult { get; }

    internal sealed record Deathmatch : GenericActorModeCompletion
    {
        public Deathmatch(
            string completionReason,
            GenericDeathmatchResult compatibilityResult)
            : base(
                completionReason,
                compatibilityResult?.EndTick
                    ?? throw new ArgumentNullException(
                        nameof(compatibilityResult)),
                compatibilityResult.Standings,
                new GenericActorMatchModeResult.Deathmatch(
                    compatibilityResult.Reason,
                    compatibilityResult.Scores))
        {
            CompatibilityResult = compatibilityResult;
        }

        public GenericDeathmatchResult CompatibilityResult { get; }
    }

    internal sealed record Frontline : GenericActorModeCompletion
    {
        public Frontline(
            string completionReason,
            int endTick,
            TeamStandings standings,
            GenericFrontlineEndReason reason,
            GenericActorRuntimeObservation.ModeObservationState.Frontline
                control,
            FrontlineScoreState scores)
            : base(
                completionReason,
                endTick,
                standings,
                new GenericActorMatchModeResult.Frontline(
                    reason,
                    control,
                    scores))
        {
        }
    }
}
