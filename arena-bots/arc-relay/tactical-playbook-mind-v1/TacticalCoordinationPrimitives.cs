using BotArena.Sdk;

/// <summary>
/// Pure deterministic selectors shared by the tactical executor and its
/// fixtures. Keeping these decisions independent of SDK command objects makes
/// coordination behavior directly reproducible without a match harness.
/// </summary>
internal static class TacticalCoordinationPrimitives
{
    internal readonly record struct SignatureCandidate(
        int UnitId,
        ActorIdentity Target,
        string SignatureId);

    internal readonly record struct EnemyCarrierCandidate(
        ActorIdentity ActorId,
        Position Position);

    internal static HashSet<int> SelectSignatureControllers(
        IEnumerable<SignatureCandidate> candidates) => candidates
        .OrderBy(value => value.Target.TeamId)
        .ThenBy(value => value.Target.UnitId)
        .ThenBy(value => value.Target.LifeId)
        .ThenBy(value => value.SignatureId, StringComparer.Ordinal)
        .ThenBy(value => value.UnitId)
        .GroupBy(
            value => (value.Target, value.SignatureId),
            value => value.UnitId)
        .Select(group => group.First())
        .ToHashSet();

    internal static bool NeedsFocusAssignment(
        int targetHealth,
        int committedDamage,
        int permittedOverkillDamage,
        bool coverEscapeLanes,
        int coveredOptions,
        int minimumCoveredOptions) =>
        committedDamage < targetHealth + permittedOverkillDamage
        || coverEscapeLanes && coveredOptions < minimumCoveredOptions;

    internal static bool ShouldReleaseFocus(
        bool destroyed,
        bool releaseOnDestroyed,
        bool outsideLeash,
        bool releaseOutsideLeash,
        bool reachable,
        int unreachableTicks,
        int releaseAfterUnreachableTicks) =>
        destroyed && releaseOnDestroyed
        || outsideLeash && releaseOutsideLeash
        || !reachable
        && releaseAfterUnreachableTicks > 0
        && unreachableTicks >= releaseAfterUnreachableTicks;

    internal static bool IsWithinEngagementLeash(
        Position assignment,
        Position enemyPosition,
        Position bodyPosition,
        int chaseLeash,
        bool selfDefenseEnabled,
        int selfDefenseThreatDistance) =>
        bodyPosition.ChebyshevDistance(assignment) <= chaseLeash
        || selfDefenseEnabled
        && bodyPosition.ChebyshevDistance(enemyPosition)
            <= selfDefenseThreatDistance;

    internal static bool HonorsProviderSeparation(
        Position provider,
        IEnumerable<Position> providersAlreadyAssignedToTarget,
        int minimumSeparation) => providersAlreadyAssignedToTarget.All(
            assigned => provider.ChebyshevDistance(assigned)
                >= minimumSeparation);

    internal static EnemyCarrierCandidate? SelectEnemyCarrier(
        IEnumerable<EnemyCarrierCandidate> candidates,
        Position fallbackAnchor,
        Position enemyReactor,
        int pursuitRadius) => candidates
        .Where(candidate => candidate.Position.ChebyshevDistance(
            fallbackAnchor) <= pursuitRadius)
        .OrderBy(candidate => candidate.Position.ChebyshevDistance(
            enemyReactor))
        .ThenBy(candidate => candidate.Position.ChebyshevDistance(
            fallbackAnchor))
        .ThenBy(candidate => candidate.ActorId.TeamId)
        .ThenBy(candidate => candidate.ActorId.UnitId)
        .ThenBy(candidate => candidate.ActorId.LifeId)
        .Cast<EnemyCarrierCandidate?>()
        .FirstOrDefault();

    internal static string SurvivalDirective(string fallback) => fallback switch
    {
        "evade" or "regroup" or "hold" or "self-defense" => fallback,
        _ => throw new InvalidDataException(
            $"Unknown support survival fallback '{fallback}'."),
    };
}
