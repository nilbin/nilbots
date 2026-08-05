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
        Position Position,
        Position? PreviousPosition = null);

    internal readonly record struct SecuredCoreCandidate(
        string CoreKey,
        string SourceWellId,
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

    internal static bool ShouldPreemptFocus(
        string mode,
        int candidatePriorityRank,
        int lockedPriorityRank,
        bool candidateIsCarrier,
        bool lockedIsCarrier,
        int candidateBankDistance,
        int lockedBankDistance) => mode switch
    {
        "never" => false,
        "higher-priority" => candidatePriorityRank < lockedPriorityRank,
        "urgent-carrier" => candidateIsCarrier
            && (!lockedIsCarrier
                || candidateBankDistance < lockedBankDistance),
        _ => throw new InvalidDataException(
            $"Unknown focus-lock preemption mode '{mode}'."),
    };

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

    internal static bool IsSelfDefenseExcursion(
        Position bodyPosition,
        Position assignment,
        Position enemyPosition,
        int chaseLeash,
        bool selfDefenseEnabled,
        int selfDefenseThreatDistance) => selfDefenseEnabled
        && bodyPosition.ChebyshevDistance(assignment) > chaseLeash
        && bodyPosition.ChebyshevDistance(enemyPosition)
            <= selfDefenseThreatDistance;

    internal static bool HasReturnedToFormation(
        Position bodyPosition,
        Position assignment,
        int arrivalRadius) => bodyPosition.ChebyshevDistance(assignment)
            <= arrivalRadius;

    internal static bool HonorsProviderSeparation(
        Position provider,
        IEnumerable<Position> providersAlreadyAssignedToTarget,
        int minimumSeparation) => providersAlreadyAssignedToTarget.All(
            assigned => provider.ChebyshevDistance(assigned)
                >= minimumSeparation);

    internal static int CoverageFallbackIndex(
        string fallback,
        IReadOnlyList<int> newlyCoveredOptions)
    {
        if (newlyCoveredOptions.Count == 0)
            return -1;
        int best = newlyCoveredOptions.Max();
        bool choose = fallback switch
        {
            "current-position" => best > 0,
            "best-coverage" => true,
            _ => throw new InvalidDataException(
                $"Unknown dodge-coverage fallback '{fallback}'."),
        };
        if (!choose)
            return -1;
        for (int index = 0; index < newlyCoveredOptions.Count; index++)
        {
            if (newlyCoveredOptions[index] == best)
                return index;
        }
        return -1;
    }

    internal static Position[] OrderCarrierAimOptions(
        bool mirrored,
        Position current,
        Position? previous,
        IEnumerable<Position> legalOptions,
        Func<Position, int?> bankDistance)
    {
        Position[] options = legalOptions.Distinct().ToArray();
        Position? continuation = previous is Position prior
            ? new Position(
                current.X + Math.Sign(current.X - prior.X),
                current.Y + Math.Sign(current.Y - prior.Y))
            : null;
        if (continuation is Position projected
            && (projected == current || !options.Contains(projected)))
        {
            continuation = null;
        }
        int bestBankward = options
            .Where(position => position != current)
            .Select(position => bankDistance(position) ?? int.MaxValue)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        return options
            .OrderBy(position => continuation == position
                ? 0
                : position != current
                    && bankDistance(position) == bestBankward
                        ? 1
                        : position == current ? 2 : 3)
            .ThenBy(position => bankDistance(position) ?? int.MaxValue)
            .ThenBy(position => mirrored ? -position.Y : position.Y)
            .ThenBy(position => mirrored ? -position.X : position.X)
            .ToArray();
    }

    internal static int CarrierMovementStepsBeforeProjectileContact(
        int distance,
        bool instantRay,
        int launchTiles,
        int tilesPerAdvance,
        int ticksPerAdvance,
        bool advancesOnLaunchTick)
    {
        if (distance < 1)
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (instantRay)
            return 1;
        if (launchTiles < 0)
            throw new ArgumentOutOfRangeException(nameof(launchTiles));
        if (tilesPerAdvance < 1)
            throw new ArgumentOutOfRangeException(nameof(tilesPerAdvance));
        if (ticksPerAdvance < 1)
            throw new ArgumentOutOfRangeException(nameof(ticksPerAdvance));

        int launchReach = launchTiles
            + (advancesOnLaunchTick ? tilesPerAdvance : 0);
        int remaining = Math.Max(0, distance - launchReach);
        int advances = (remaining + tilesPerAdvance - 1)
            / tilesPerAdvance;
        // The target gets its current-tick movement before a freshly launched
        // projectile can contact it. Each later projectile advance gives the
        // target the corresponding number of additional movement ticks.
        return 1 + advances * ticksPerAdvance;
    }

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

    /// <summary>
    /// Advances a causal carrier track along a deterministic shortest return
    /// lane. The prior observation breaks equal-route ties in favour of
    /// continuing the observed motion; callers bound legal steps to the
    /// authored pursuit leash.
    /// </summary>
    internal static Position PredictReturnLaneCutoff(
        bool mirrored,
        Position current,
        Position? previous,
        int leadTiles,
        Func<Position, int?> distanceToBank,
        Func<Position, Position, bool> legalStep)
    {
        Position cursor = current;
        Position? prior = previous;
        for (int step = 0; step < leadTiles; step++)
        {
            int? currentDistance = distanceToBank(cursor);
            if (currentDistance is null or 0)
                break;
            Position? continuation = prior is Position observed
                ? new Position(
                    cursor.X + Math.Sign(cursor.X - observed.X),
                    cursor.Y + Math.Sign(cursor.Y - observed.Y))
                : null;
            Position[] candidates = Enumerable.Range(-1, 3)
                .SelectMany(dy => Enumerable.Range(-1, 3)
                    .Select(dx => new Position(cursor.X + dx, cursor.Y + dy)))
                .Where(value => value != cursor)
                .Where(value => legalStep(cursor, value))
                .Select(value => (Position: value, Distance: distanceToBank(value)))
                .Where(value => value.Distance is not null
                    && value.Distance < currentDistance)
                .OrderBy(value => value.Position == continuation ? 0 : 1)
                .ThenBy(value => value.Distance)
                .ThenBy(value => mirrored ? -value.Position.Y : value.Position.Y)
                .ThenBy(value => mirrored ? -value.Position.X : value.Position.X)
                .Select(value => value.Position)
                .ToArray();
            if (candidates.Length == 0)
                break;
            prior = cursor;
            cursor = candidates[0];
        }
        return cursor;
    }

    internal static SecuredCoreCandidate? SelectSecuredCore(
        IEnumerable<SecuredCoreCandidate> candidates,
        IReadOnlySet<string> allowedSourceWells,
        Position fallbackAnchor,
        int guardRadius) => candidates
        .Where(candidate => allowedSourceWells.Contains(
                candidate.SourceWellId)
            && candidate.Position.ChebyshevDistance(fallbackAnchor)
                <= guardRadius)
        .OrderBy(candidate => candidate.Position.ChebyshevDistance(
            fallbackAnchor))
        .ThenBy(candidate => candidate.SourceWellId, StringComparer.Ordinal)
        .ThenBy(candidate => candidate.CoreKey, StringComparer.Ordinal)
        .Cast<SecuredCoreCandidate?>()
        .FirstOrDefault();

    internal static string SurvivalDirective(string fallback) => fallback switch
    {
        "evade" or "regroup" or "hold" or "self-defense" => fallback,
        _ => throw new InvalidDataException(
            $"Unknown support survival fallback '{fallback}'."),
    };
}
