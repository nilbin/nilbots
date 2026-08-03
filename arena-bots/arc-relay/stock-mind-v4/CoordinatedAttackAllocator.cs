using BotArena.Sdk;

/// <summary>
/// Optional parity control for the frozen stock doctrine. It changes only
/// target allocation: strategy, composition, movement and custody remain the
/// stock mind's. The historical sheet omits the extension and never enters
/// this path.
/// </summary>
internal sealed class CoordinatedAttackAllocator
{
    private ActorIdentity? _lockedPrimary;
    private int _lockedAtTick;

    internal IReadOnlyDictionary<int,
        GenericActorContext.ObservedEnemyState> Allocate(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        GenericActorContext.ModeObservationState.ArcRelay arc,
        AttackCoordinationPolicy? policy,
        int ownTeamId)
    {
        if (policy is null || mind.Enemies.Length == 0)
            return new Dictionary<int,
                GenericActorContext.ObservedEnemyState>();

        HashSet<ActorIdentity> carriers = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId is { } carrier
                && carrier.TeamId != ownTeamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        HashSet<ActorIdentity> ownCarriers = arc.VisibleCores
            .Where(core => core.Disposition
                == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId is { } carrier
                && carrier.TeamId == ownTeamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        MindBody[] participants = mind.Bodies
            .Where(body => !ownCarriers.Contains(body.ActorId))
            .OrderBy(body => body.UnitId)
            .ToArray();
        GenericActorContext.ObservedEnemyState[] enemies = mind.Enemies
            .OrderBy(enemy => enemy,
                Comparer<GenericActorContext.ObservedEnemyState>.Create(
                    (left, right) => CompareTargets(
                        policy, left, right, carriers, participants)))
            .ToArray();

        GenericActorContext.ObservedEnemyState primary = enemies[0];
        if (_lockedPrimary is { } locked
            && mind.Tick - _lockedAtTick < policy.LockTicks)
        {
            primary = enemies.FirstOrDefault(enemy =>
                enemy.ActorId == locked) ?? primary;
        }
        if (_lockedPrimary != primary.ActorId)
        {
            _lockedPrimary = primary.ActorId;
            _lockedAtTick = mind.Tick;
        }

        GenericActorContext.ObservedEnemyState[] targetOrder =
        [
            primary,
            .. enemies.Where(enemy => enemy.ActorId != primary.ActorId),
        ];
        var committedDamage = new Dictionary<ActorIdentity, int>();
        var attackerCounts = new Dictionary<ActorIdentity, int>();
        var result = new Dictionary<int,
            GenericActorContext.ObservedEnemyState>();

        foreach (MindBody body in participants)
        {
            GenericActorContext.ObservedEnemyState? selected = SelectTarget(
                targetOrder,
                body,
                policy,
                contract,
                attackerCounts,
                committedDamage,
                requireFireReady: true);
            selected ??= SelectTarget(
                targetOrder,
                body,
                policy,
                contract,
                attackerCounts,
                committedDamage,
                requireFireReady: false);
            if (selected is null)
                continue;

            result[body.UnitId] = selected;
            attackerCounts[selected.ActorId] =
                attackerCounts.GetValueOrDefault(selected.ActorId) + 1;
            if (ArenaBasics.CanFireAt(contract, body, selected))
            {
                committedDamage[selected.ActorId] = committedDamage
                    .GetValueOrDefault(selected.ActorId)
                    + ArenaBasics.ExpectedAttackDamage(contract, body);
            }
        }
        return result;
    }

    private static GenericActorContext.ObservedEnemyState? SelectTarget(
        IEnumerable<GenericActorContext.ObservedEnemyState> targets,
        MindBody body,
        AttackCoordinationPolicy policy,
        GenericActorResolvedMatchContract contract,
        IReadOnlyDictionary<ActorIdentity, int> attackerCounts,
        IReadOnlyDictionary<ActorIdentity, int> committedDamage,
        bool requireFireReady) => targets.FirstOrDefault(target =>
        attackerCounts.GetValueOrDefault(target.ActorId)
            < policy.MaximumAttackersPerTarget
        && committedDamage.GetValueOrDefault(target.ActorId)
            < target.Health + policy.OverkillDamage
        && (requireFireReady
            ? ArenaBasics.CanFireAt(contract, body, target)
            : ArenaBasics.CanAimAt(contract, body, target)));

    private static int CompareTargets(
        AttackCoordinationPolicy policy,
        GenericActorContext.ObservedEnemyState left,
        GenericActorContext.ObservedEnemyState right,
        IReadOnlySet<ActorIdentity> carriers,
        IReadOnlyCollection<MindBody> participants)
    {
        int comparison = PriorityRank(
                policy.TargetPriorities, left, carriers, participants)
            .CompareTo(PriorityRank(
                policy.TargetPriorities, right, carriers, participants));
        if (comparison != 0)
            return comparison;
        foreach (string tieBreaker in policy.TieBreakers)
        {
            comparison = tieBreaker switch
            {
                "health" => left.Health.CompareTo(right.Health),
                "distance" => participants.Min(body => body.Position
                        .ChebyshevDistance(left.Position))
                    .CompareTo(participants.Min(body => body.Position
                        .ChebyshevDistance(right.Position))),
                "actor-id" => CompareActorIds(left.ActorId, right.ActorId),
                _ => 0,
            };
            if (comparison != 0)
                return comparison;
        }
        return CompareActorIds(left.ActorId, right.ActorId);
    }

    private static int PriorityRank(
        IReadOnlyList<string> priorities,
        GenericActorContext.ObservedEnemyState enemy,
        IReadOnlySet<ActorIdentity> carriers,
        IReadOnlyCollection<MindBody> participants)
    {
        for (int index = 0; index < priorities.Count; index++)
        {
            bool matches = priorities[index] switch
            {
                "enemy-carrier" => carriers.Contains(enemy.ActorId),
                "lowest-health" => true,
                "nearest" => participants.Count > 0,
                _ => false,
            };
            if (matches)
                return index;
        }
        return priorities.Count;
    }

    private static int CompareActorIds(ActorIdentity left, ActorIdentity right)
    {
        int comparison = left.TeamId.CompareTo(right.TeamId);
        if (comparison != 0)
            return comparison;
        comparison = left.UnitId.CompareTo(right.UnitId);
        return comparison != 0
            ? comparison
            : left.LifeId.CompareTo(right.LifeId);
    }
}
