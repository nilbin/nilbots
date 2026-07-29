namespace BotArena.App.Competition;

/// <summary>
/// Single source of truth for the existing hosted legacy Duel arena. This
/// definition names presentation/admission defaults without changing the
/// immutable rules version or historical competition identities.
/// </summary>
public sealed class DuelArenaDefinition
{
    public const int UnrankedGamesPerMatch = 1;

    public static DuelArenaDefinition Official { get; } = new(
        defaultUnrankedMapId: "arena-01",
        rankedMapPool:
        [
            "basic-01",
            "arena-01",
            "crossfire-01",
            "bastion-01",
            "gallery-01",
        ],
        DuelMirrored6V1.Instance);

    private DuelArenaDefinition(
        string defaultUnrankedMapId,
        IReadOnlyList<string> rankedMapPool,
        DuelMirrored6V1 rankedSeriesPolicy)
    {
        DefaultUnrankedMapId = defaultUnrankedMapId;
        RankedMapPool = Array.AsReadOnly([.. rankedMapPool]);
        RankedSeriesPolicy = rankedSeriesPolicy;
    }

    /// <summary>
    /// Map selected for a one-off unranked challenge when the player does not
    /// choose one explicitly.
    /// </summary>
    public string DefaultUnrankedMapId { get; }

    /// <summary>
    /// Current ranked Duel map pool. Each set samples three distinct entries.
    /// crossfire-01 joined with rules 0.3 after its sightline repair; causeway
    /// remains an adversarial test map but left ranked play after the gen-7
    /// geometry review (DECISIONS #62).
    /// </summary>
    public IReadOnlyList<string> RankedMapPool { get; }

    public DuelMirrored6V1 RankedSeriesPolicy { get; }

    /// <summary>
    /// Preserves the existing challenge request semantics: null and the empty
    /// string select the default; every non-empty id is validated by the map
    /// loader at admission.
    /// </summary>
    public string ResolveUnrankedMapId(string? requestedMapId) =>
        requestedMapId is { Length: > 0 }
            ? requestedMapId
            : DefaultUnrankedMapId;

    public IReadOnlyList<DuelMirrored6V1.ScheduledGame> CreateRankedSchedule(
        Random random) =>
        RankedSeriesPolicy.CreateSchedule(RankedMapPool, random);
}
