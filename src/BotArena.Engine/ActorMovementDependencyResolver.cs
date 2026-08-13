namespace BotArena.Engine;

/// <summary>
/// Resolves the occupancy dependency graph for a joint actor movement step.
/// This is deliberately shared by authoritative execution and chronology
/// validation: replay evidence must reconstruct the same atomic transition.
/// </summary>
internal static class ActorMovementDependencyResolver
{
    public static HashSet<ActorIdentity> ResolveBlocked(
        IReadOnlyDictionary<ActorIdentity, Position> targets,
        IReadOnlyDictionary<Position, ActorIdentity> occupants,
        IEnumerable<ActorIdentity> initiallyBlocked,
        bool followingVacatedActorAllowed)
    {
        var blocked = initiallyBlocked.ToHashSet();

        foreach (IGrouping<Position,
                     KeyValuePair<ActorIdentity, Position>> claims
                 in targets.GroupBy(pair => pair.Value))
        {
            if (claims.Count() <= 1)
                continue;
            foreach (KeyValuePair<ActorIdentity, Position> claim in claims)
                blocked.Add(claim.Key);
        }

        if (!followingVacatedActorAllowed)
        {
            foreach ((ActorIdentity actorId, Position target) in targets)
            {
                if (occupants.ContainsKey(target))
                    blocked.Add(actorId);
            }
            return blocked;
        }

        // Each mover has at most one dependency: the actor occupying its
        // requested destination. An acyclic chain succeeds exactly when it
        // terminates at a pre-step vacancy. A stationary/blocked occupant or
        // any cycle makes the chain fail. Actor ordering is irrelevant to the
        // result, but canonical ordering keeps diagnostics and tests stable.
        var canAdvance = new Dictionary<ActorIdentity, bool>();
        foreach (ActorIdentity actorId in targets.Keys.Order())
        {
            if (!CanAdvance(actorId, []))
                blocked.Add(actorId);
        }
        return blocked;

        bool CanAdvance(
            ActorIdentity actorId,
            HashSet<ActorIdentity> path)
        {
            if (blocked.Contains(actorId))
                return false;
            if (canAdvance.TryGetValue(actorId, out bool cached))
                return cached;
            if (!path.Add(actorId))
                return false;

            Position target = targets[actorId];
            bool result = !occupants.TryGetValue(target, out ActorIdentity? next)
                || targets.ContainsKey(next) && CanAdvance(next, path);
            path.Remove(actorId);
            // A false encountered while walking into a cycle is local to that
            // dependency path. Cache only proven acyclic success; otherwise a
            // node first visited from inside a cycle could poison a later read.
            if (result)
                canAdvance[actorId] = true;
            return result;
        }
    }
}
