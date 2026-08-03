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
}
