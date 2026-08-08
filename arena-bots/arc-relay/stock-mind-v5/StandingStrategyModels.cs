using System.Text.Json.Serialization;

internal sealed record StandingStrategyDocument
{
    public required string Schema { get; init; }
    public required string SheetId { get; init; }
    public required string MapId { get; init; }
    public required string[] Composition { get; init; }
    public required Dictionary<string, int[]> Zones { get; init; }
    public required Dictionary<string, int[][]> Paths { get; init; }
    public required Dictionary<string, int[][]> Formations { get; init; }
    public required StandingStrategyPlan StandingStrategy { get; init; }
}

internal sealed record StandingStrategyPlan
{
    public required string InitialPhase { get; init; }
    public required Dictionary<string, string> Parameters { get; init; }
    public string FocusPolicy { get; init; } = "carrier-first";
    public required StandingMemoryPolicy Memory { get; init; }
    public required StandingPhasePlan[] Phases { get; init; }
}

internal sealed record StandingMemoryPolicy
{
    public int EnemyUnavailableTicks { get; init; } = 21;
    public int LastSeenEnemyTicks { get; init; } = 30;
    public int SecuredCoreMemoryTicks { get; init; } = 40;
    public int ObjectiveProgressMemoryTicks { get; init; } = 90;
    public int StableControlTicks { get; init; } = 3;
}

internal sealed record StandingPhasePlan
{
    public required string Id { get; init; }
    public int MinimumTicks { get; init; }
    public StandingConditionGroup[] Entry { get; init; } = [];
    public required StandingAssignmentPlan[] Assignments { get; init; }
    public required StandingTransitionPlan[] Transitions { get; init; }
}

internal sealed record StandingTransitionPlan
{
    public int Priority { get; init; }
    public required string To { get; init; }
    public string Cause { get; init; } = "condition";
    public int StableTicks { get; init; } = 1;
    public required StandingConditionGroup[] When { get; init; }
}

internal sealed record StandingAssignmentPlan
{
    public int Priority { get; init; }
    public required string Id { get; init; }
    public required string Resilience { get; init; }
    public int Count { get; init; } = -1;
    public string[] CandidateClasses { get; init; } = [];
    public string[] CandidateRoles { get; init; } = [];
    public bool CarrierOnly { get; init; }
    public required string Behavior { get; init; }
    public required StandingPositionIntent Position { get; init; }
    public string Formation { get; init; } = "";
    public string Facing { get; init; } = "adaptive";
    public string Engagement { get; init; } = "focus";
    public string Signature { get; init; } = "normal";
    public string CorePolicy { get; init; } = "avoid";
    public string CoreFallback { get; init; } = "hold";
    public bool PreferCarrier { get; init; }
    public string CoreSource { get; init; } = "";
    public string Respawn { get; init; } = "rejoin";
    public StandingConditionGroup[] When { get; init; } = [];
}

internal sealed record StandingPositionIntent
{
    public required string Kind { get; init; }
    public required string Target { get; init; }
}

internal sealed record StandingConditionGroup
{
    public StandingCondition[] All { get; init; } = [];
    public StandingCondition[] Any { get; init; } = [];
}

internal sealed record StandingCondition
{
    public required string Fact { get; init; }
    public string Operator { get; init; } = "at-least";
    public int Value { get; init; } = 1;
    public string Zone { get; init; } = "";
    public string Subject { get; init; } = "";
}

internal sealed record StandingUnitAssignment(
    int UnitId,
    StandingAssignmentPlan Plan,
    int FormationIndex);

internal sealed record StandingSnapshot(
    int Tick,
    int LiveFriendlies,
    int KnownEnemiesUnavailable,
    int SecuredCores,
    int VisibleLooseCores,
    int FriendlyCarriers,
    int TicksWithoutObjectiveProgress,
    IReadOnlyDictionary<string, int> OutstandingCoresByWell,
    IReadOnlyDictionary<string, int> FriendliesByZone,
    IReadOnlyDictionary<string, int> StableFriendliesByZone,
    IReadOnlyDictionary<string, int> EnemiesByZone,
    IReadOnlyDictionary<string, int> RememberedEnemiesByZone);
