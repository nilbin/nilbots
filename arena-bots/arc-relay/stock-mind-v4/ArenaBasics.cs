using BotArena.Sdk;

/// <summary>
/// Contract-driven movement, targeting, and action helpers for Arc Relay.
/// Every command is selected from the body's current typed legality mask.
/// </summary>
internal static class ArenaBasics
{
    private static readonly ProjectileHeading[] EightWay =
    [
        ProjectileHeading.North,
        ProjectileHeading.NorthEast,
        ProjectileHeading.East,
        ProjectileHeading.SouthEast,
        ProjectileHeading.South,
        ProjectileHeading.SouthWest,
        ProjectileHeading.West,
        ProjectileHeading.NorthWest,
    ];

    private static readonly ProjectileHeading[] Cardinal =
    [
        ProjectileHeading.North,
        ProjectileHeading.East,
        ProjectileHeading.South,
        ProjectileHeading.West,
    ];

    internal sealed class Claims
    {
        private readonly HashSet<Position> _tiles = [];

        public IReadOnlySet<Position> Tiles => _tiles;

        public bool Reserve(Position tile) => _tiles.Add(tile);

        public static Claims ForTick(MindContext mind)
        {
            var claims = new Claims();
            foreach (MindBody body in mind.Bodies)
                claims.Reserve(body.Position);
            return claims;
        }
    }

    public static GenericActorContext.ModeObservationState.ArcRelay? ArcState(
        MindContext mind) =>
        mind.Mode as GenericActorContext.ModeObservationState.ArcRelay;

    public static GenericActorRulesContract.ArcRelayGameMode? ArcRules(
        GenericActorResolvedMatchContract contract) =>
        contract.Rules.GameMode as GenericActorRulesContract.ArcRelayGameMode;

    public const string InvestActionId = "invest";

    /// <summary>
    /// Class-keyed default build under veterancy rules: gun-forward chassis
    /// sharpen damage, sensor chassis extend vision, everything else buys
    /// vitality (which heal zones make recoverable).
    /// </summary>
    public static string DefaultBuildTrack(string formId) => formId switch
    {
        "arc-body-longshot" or "arc-body-mortar" or "arc-body-sunder"
            or "arc-body-kestrel" or "arc-body-switchback" => "damage",
        "arc-body-veil" or "arc-body-nest" or "arc-body-lantern"
            or "arc-body-hush" => "vision",
        _ => "vitality",
    };

    /// <summary>
    /// 0 when the nearest shooter's 8-way heading toward the target lands in
    /// the target's blind rear quadrant (a backstab under predation rules),
    /// 1 otherwise — an ascending late tie-break for target choice.
    /// </summary>
    public static int RearExposedRank(
        IReadOnlyCollection<MindBody> shooters,
        GenericActorContext.ObservedEnemyState target)
    {
        MindBody? nearest = shooters
            .OrderBy(body => body.Position.ChebyshevDistance(target.Position))
            .ThenBy(body => body.UnitId)
            .FirstOrDefault();
        if (nearest is null)
            return 1;
        int dx = Math.Sign(target.Position.X - nearest.Position.X);
        int dy = Math.Sign(target.Position.Y - nearest.Position.Y);
        (int forward, int lateral) = target.Facing switch
        {
            Direction.North => (-dy, dx),
            Direction.South => (dy, dx),
            Direction.East => (dx, dy),
            Direction.West => (-dx, dy),
            _ => (-1, 0),
        };
        return forward >= 0 && Math.Abs(lateral) <= forward ? 0 : 1;
    }

    public static Position? Reactor(
        MindContext mind,
        int teamId) =>
        ArcState(mind)?.Reactors
            .FirstOrDefault(reactor => reactor.TeamId == teamId)
            ?.Position;

    public static GenericActorContext.ArcRelayCoreState? CarriedCore(
        MindContext mind,
        ActorIdentity actorId) =>
        ArcState(mind)?.VisibleCores.FirstOrDefault(core =>
            core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Carried
            && core.CarrierActorId == actorId);

    public static GenericActorContext.ObservedEnemyState? VisibleEnemyCarrier(
        MindContext mind,
        int ownTeamId)
    {
        GenericActorContext.ModeObservationState.ArcRelay? arc = ArcState(mind);
        if (arc is null)
            return null;

        HashSet<ActorIdentity> carrierIds = arc.VisibleCores
            .Where(core =>
                core.Disposition
                    == GenericActorContext.ArcRelayCoreDisposition.Carried
                && core.CarrierActorId is { } carrier
                && carrier.TeamId != ownTeamId)
            .Select(core => core.CarrierActorId!)
            .ToHashSet();
        return mind.Enemies
            .Where(enemy => carrierIds.Contains(enemy.ActorId))
            .OrderBy(enemy => enemy.Health)
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
    }

    public static GenericActorContext.ArcRelayCoreState[] LooseCores(
        MindContext mind) =>
        ArcState(mind)?.VisibleCores
            .Where(core =>
                core.Disposition == GenericActorContext.ArcRelayCoreDisposition.Loose)
            .OrderBy(core => core.CoreId.SourceWellId, StringComparer.Ordinal)
            .ThenBy(core => core.CoreId.SourceOrdinal)
            .ToArray()
        ?? [];

    public static bool TryMoveToward(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyCollection<Position> goals,
        Claims claims,
        string reason)
    {
        if (goals.Count == 0 || goals.Contains(body.Position))
            return false;

        HashSet<Position> blocked = BlockedNow(contract, mind, body, claims);
        ProjectileHeading[] routeHeadings = RouteHeadings(contract, body);
        ProjectileHeading? desired = FindFirstStep(
            contract.Map,
            body.Position,
            goals.ToHashSet(),
            blocked,
            routeHeadings,
            PreferredHeadings(body, goals));
        if (desired is not ProjectileHeading heading)
            return false;

        Position desiredDestination = Step(body.Position, heading);
        GenericActorActionLegality? move = MovementAction(
            contract,
            mind,
            body,
            heading,
            desiredDestination,
            requireAvailable: false);
        if (move is null)
        {
            // Facing-locked bodies expose only their current forward heading
            // in the movement mask. A different cardinal route step therefore
            // needs an explicit preparation turn before it can be submitted.
            Direction? routeFacing = ToDirection(heading);
            return routeFacing is Direction routeTurn
                && TryRotate(contract, body, routeTurn, $"turn for {reason}");
        }

        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (move.Available
            && headings is not null
            && headings.AllowedValues.Contains(heading))
        {
            (int dx, int dy) = heading.Vector();
            Position destination = body.Position.Offset(dx, dy);
            claims.Reserve(destination);
            body.Command(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
                $"{reason} via {heading}");
            return true;
        }

        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        Direction? cardinal = ToDirection(heading);
        if (move.Available
            && cardinal is Direction direction
            && directions is not null
            && directions.AllowedValues.Contains(direction))
        {
            (int dx, int dy) = direction.Vector();
            Position destination = body.Position.Offset(dx, dy);
            claims.Reserve(destination);
            body.Command(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(direction)],
                $"{reason} via {direction}");
            return true;
        }

        if (cardinal is Direction turn)
            return TryRotate(contract, body, turn, $"turn for {reason}");
        return false;
    }

    /// <summary>
    /// The first terrain-only step toward a goal. Traffic may occupy it now;
    /// callers use this to ask that traffic to yield instead of routing a
    /// carrier in circles around its own bank.
    /// </summary>
    public static Position? StaticFirstStep(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Position goal)
    {
        if (body.Position == goal)
            return null;
        ProjectileHeading? heading = FindFirstStep(
            contract.Map,
            body.Position,
            new HashSet<Position> { goal },
            new HashSet<Position>(),
            RouteHeadings(contract, body),
            PreferredHeadings(body, [goal]));
        return heading is ProjectileHeading step
            ? Step(body.Position, step)
            : null;
    }

    /// <summary>
    /// The first terrain-shortest step toward a goal while treating visible
    /// spawn reservations as durable obstacles for the entire route. Carrier
    /// recovery uses this after repeated non-progress: a reservation beside a
    /// reactor must select one deterministic route around it, rather than let
    /// equal-distance local choices bounce between adjacent tiles.
    /// </summary>
    public static Position? StaticFirstStepAvoidingReservations(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position goal)
    {
        if (body.Position == goal)
            return null;
        HashSet<Position> reservations = mind.VisibleTiles
            .Where(tile => tile.SpawnReservation is not null)
            .Select(tile => tile.Position)
            .ToHashSet();
        reservations.Remove(body.Position);
        reservations.Remove(goal);
        ProjectileHeading? heading = FindFirstStep(
            contract.Map,
            body.Position,
            new HashSet<Position> { goal },
            reservations,
            RouteHeadings(contract, body),
            PreferredHeadings(body, [goal]),
            blockThroughout: true);
        return heading is ProjectileHeading step
            ? Step(body.Position, step)
            : null;
    }

    /// <summary>
    /// Static eight-way walk distance with the contract's strict diagonal
    /// corner rule. Carrier progress uses map distance rather than Chebyshev
    /// distance so moving around cover cannot masquerade as home progress.
    /// </summary>
    public static int? StaticDistance(
        GenericActorMapContract map,
        Position start,
        Position goal)
    {
        if (start == goal)
            return 0;
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, int Distance)>();
        queue.Enqueue((start, 0));
        while (queue.Count > 0)
        {
            (Position position, int distance) = queue.Dequeue();
            foreach (ProjectileHeading heading in EightWay)
            {
                Position next = Step(position, heading);
                if (!CanStep(map, position, next, heading)
                    || !visited.Add(next))
                {
                    continue;
                }
                if (next == goal)
                    return distance + 1;
                queue.Enqueue((next, distance + 1));
            }
        }
        return null;
    }

    /// <summary>
    /// Take a legal traffic-aware step whose static map distance does not
    /// increase. Equal-distance sidesteps are permitted so a permanently
    /// reserved spawn tile cannot pin a carrier on the nominal shortest ray.
    /// </summary>
    public static bool TryMoveHomeward(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position goal,
        Claims claims,
        string reason)
    {
        int? currentDistance = StaticDistance(
            contract.Map,
            body.Position,
            goal);
        if (currentDistance is null)
            return false;
        HashSet<Position> blocked = BlockedNow(contract, mind, body, claims);
        ProjectileHeading facing =
            (ProjectileHeading)((int)body.Facing * 2);
        (ProjectileHeading Heading, Position Destination, int Distance)?
            selected = RouteHeadings(contract, body)
                .Select(heading => (
                    Heading: heading,
                    Destination: Step(body.Position, heading)))
                .Where(candidate =>
                    CanStep(
                        contract.Map,
                        body.Position,
                        candidate.Destination,
                        candidate.Heading)
                    && !blocked.Contains(candidate.Destination))
                .Select(candidate => (
                    candidate.Heading,
                    candidate.Destination,
                    Distance: StaticDistance(
                        contract.Map,
                        candidate.Destination,
                        goal)))
                .Where(candidate => candidate.Distance is not null
                    && candidate.Distance <= currentDistance)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Heading == facing ? 0 : 1)
                .ThenBy(candidate => (int)candidate.Heading)
                .Select(candidate => (ValueTuple<ProjectileHeading, Position, int>?) (
                    candidate.Heading,
                    candidate.Destination,
                    candidate.Distance!.Value))
                .FirstOrDefault();
        return selected is { } step
            && TryMoveDirect(
                contract,
                mind,
                body,
                step.Destination,
                claims,
                reason);
    }

    /// <summary>
    /// Attempt one already-selected terrain-shortest step without inventing a
    /// lateral detour around transient traffic. Core carriers use this so a
    /// one-tick reservation cannot turn into an endless two-tile orbit.
    /// </summary>
    public static bool TryMoveDirect(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position destination,
        Claims claims,
        string reason)
    {
        int dx = destination.X - body.Position.X;
        int dy = destination.Y - body.Position.Y;
        if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || dx == 0 && dy == 0)
            return false;
        ProjectileHeading heading = FromVector(dx, dy);
        if (!CanStep(contract.Map, body.Position, destination, heading)
            || BlockedNow(contract, mind, body, claims).Contains(destination))
        {
            return false;
        }

        GenericActorActionLegality? move = MovementAction(
            contract,
            mind,
            body,
            heading,
            destination,
            requireAvailable: false);
        if (move is null)
        {
            Direction? routeFacing = ToDirection(heading);
            return routeFacing is Direction routeTurn
                && TryRotate(contract, body, routeTurn, $"turn for {reason}");
        }
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (move.Available
            && headings is not null
            && headings.AllowedValues.Contains(heading))
        {
            claims.Reserve(destination);
            body.Command(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
                $"{reason} via {heading}");
            return true;
        }

        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        Direction? cardinal = ToDirection(heading);
        if (move.Available
            && cardinal is Direction direction
            && directions is not null
            && directions.AllowedValues.Contains(direction))
        {
            claims.Reserve(destination);
            body.Command(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(direction)],
                $"{reason} via {direction}");
            return true;
        }
        return cardinal is Direction turn
            && TryRotate(contract, body, turn, $"turn for {reason}");
    }

    /// <summary>
    /// Move one allied non-carrier out of a reserved carrier lane. This is a
    /// one-tick traffic command, not a teleport or a special game action.
    /// </summary>
    public static bool TryMoveAside(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Claims claims,
        IReadOnlySet<Position> forbidden,
        string reason)
    {
        // A body already blocking a return lane must clear it even when the
        // only exit crosses predicted fire. Otherwise sustained covering fire
        // can pin an unthreatened carrier behind an ally indefinitely.
        HashSet<Position> blocked = BlockedNow(
            contract,
            mind,
            body,
            claims,
            avoidHostileProjectiles: false);
        ProjectileHeading? selected = RouteHeadings(contract, body)
            .Select(heading => (Heading: heading, Tile: Step(body.Position, heading)))
            .Where(candidate =>
                CanStep(contract.Map, body.Position, candidate.Tile,
                    candidate.Heading)
                && !blocked.Contains(candidate.Tile)
                && !forbidden.Contains(candidate.Tile))
            .OrderByDescending(candidate => forbidden.Min(tile =>
                candidate.Tile.ChebyshevDistance(tile)))
            .ThenBy(candidate => (int)candidate.Heading)
            .Select(candidate => (ProjectileHeading?)candidate.Heading)
            .FirstOrDefault();
        if (selected is not ProjectileHeading heading)
            return false;

        Position selectedDestination = Step(body.Position, heading);
        GenericActorActionLegality? move = MovementAction(
            contract,
            mind,
            body,
            heading,
            selectedDestination,
            requireAvailable: false);
        if (move is null)
        {
            Direction? routeFacing = ToDirection(heading);
            return routeFacing is Direction routeTurn
                && TryRotate(contract, body, routeTurn, $"turn for {reason}");
        }

        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (move.Available
            && headings is not null
            && headings.AllowedValues.Contains(heading))
        {
            Position destination = Step(body.Position, heading);
            claims.Reserve(destination);
            body.Command(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
                $"{reason} via {heading}");
            return true;
        }

        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        Direction? cardinal = ToDirection(heading);
        if (move.Available
            && cardinal is Direction direction
            && directions is not null
            && directions.AllowedValues.Contains(direction))
        {
            (int dx, int dy) = direction.Vector();
            Position destination = body.Position.Offset(dx, dy);
            claims.Reserve(destination);
            body.Command(
                move.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(direction)],
                $"{reason} via {direction}");
            return true;
        }
        return cardinal is Direction turn
            && TryRotate(contract, body, turn, reason);
    }

    public static bool TryEvade(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Claims claims)
    {
        GenericActorContext.ObservedProjectile[] hostile =
            mind.VisibleProjectiles?
                .Where(projectile => projectile.OwnerTeamId != body.ActorId.TeamId)
                .ToArray()
            ?? [];
        if (!hostile.Any(projectile =>
                Threatens(projectile, body.Position, advances: 2)))
        {
            return false;
        }

        HashSet<Position> safe = [];
        foreach (ProjectileHeading heading in RouteHeadings(contract, body))
        {
            (int dx, int dy) = heading.Vector();
            Position tile = body.Position.Offset(dx, dy);
            if (CanEnter(contract.Map, tile)
                && !hostile.Any(projectile => Threatens(projectile, tile, 2)))
            {
                safe.Add(tile);
            }
        }
        return TryMoveToward(
            contract,
            mind,
            body,
            safe,
            claims,
            "evading carrier-lane fire");
    }

    public static bool TryShoot(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState? preferred,
        bool preferredOnly = false)
    {
        GenericActorActionLegality? action = AvailableAction(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Attack,
            requireAvailable: false);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Id, body.FormId, StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, attackId, StringComparison.Ordinal))
                : null;
        if (action is null || headings is null || attack is null)
            return false;

        IEnumerable<GenericActorContext.ObservedEnemyState> targets =
            preferred is null
                ? mind.Enemies
                    .OrderBy(enemy => enemy.Health)
                    .ThenBy(enemy =>
                        body.Position.ChebyshevDistance(enemy.Position))
                    .ThenBy(enemy => enemy.ActorId)
                : preferredOnly
                    ? [preferred]
                    : [preferred, .. mind.Enemies
                        .Where(enemy => enemy != preferred)
                        .OrderBy(enemy => enemy.Health)
                        .ThenBy(enemy => enemy.ActorId)];

        foreach (GenericActorContext.ObservedEnemyState target in targets)
        {
            if (!TryRay(body.Position, target.Position, out ProjectileHeading heading,
                    out int distance)
                || distance > attack.Projectile.MaxTravelTiles
                || !headings.AllowedValues.Contains(heading)
                || !ClearRay(
                    contract.Map,
                    body.Position,
                    target.Position,
                    attack.Projectile.DiagonalCornersMustBeClear))
            {
                continue;
            }

            if (action.Available)
            {
                body.Command(
                    action.ActionId,
                    action.ActionCode,
                    [new GenericActorActionArgument
                        .ProjectileHeadingArgument(heading)],
                    $"focus fire on {target.ActorId}");
                return true;
            }
        }

        if (CarriedCore(mind, body.ActorId) is not null)
            return false;

        // A route-facing turn is a one-tick commitment: a facing-locked body
        // must actually take its promised forward step instead of swivelling
        // away immediately. After a successful route move, however, that same
        // body must be allowed to prepare an otherwise legal visible shot;
        // blocking both states made moving Deliberate formations patrol
        // forever without taking the promised next-tick aim opportunity.
        // Standard and Swift retain the stricter move commitment.
        GenericActorRulesContract.MovementProfile? movement =
            form?.MovementProfileId is string movementId
                ? contract.Rules.MovementProfiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, movementId,
                        StringComparison.Ordinal))
                : null;
        GenericActorRulesContract.ActionKind? previousKind =
            PreviousActionKind(contract, body);
        if (previousKind == GenericActorRulesContract.ActionKind.Rotation
            || (previousKind == GenericActorRulesContract.ActionKind.Movement
                && movement?.FacingCoupling
                    != GenericActorRulesContract.MovementFacingCoupling
                        .FacingLocked))
            return false;
        foreach (GenericActorContext.ObservedEnemyState target in targets)
        {
            if (!TryRay(body.Position, target.Position,
                    out ProjectileHeading heading, out int distance)
                || distance > attack.Projectile.MaxTravelTiles
                || headings.AllowedValues.Contains(heading)
                || !ClearRay(
                    contract.Map,
                    body.Position,
                    target.Position,
                    attack.Projectile.DiagonalCornersMustBeClear))
            {
                continue;
            }
            Direction desired = FacingForCone(body.Facing, heading);
            if (TryRotate(
                    contract,
                    body,
                    desired,
                    $"prepare aim on {target.ActorId}"))
            {
                return true;
            }
        }
        return false;
    }

    public static bool CanAimAt(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorContext.ObservedEnemyState target)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Id, body.FormId,
                    StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, attackId,
                        StringComparison.Ordinal))
                : null;
        return attack is not null
            && TryRay(body.Position, target.Position, out _, out int distance)
            && distance <= attack.Projectile.MaxTravelTiles
            && ClearRay(
                contract.Map,
                body.Position,
                target.Position,
                attack.Projectile.DiagonalCornersMustBeClear);
    }

    public static bool CanFireAt(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorContext.ObservedEnemyState target)
    {
        GenericActorActionLegality? action = AvailableAction(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Attack,
            requireAvailable: false);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (action is not { Available: true } || headings is null
            || !CanAimAt(contract, body, target)
            || !TryRay(body.Position, target.Position,
                out ProjectileHeading heading, out _))
        {
            return false;
        }
        return headings.AllowedValues.Contains(heading);
    }

    public static int ExpectedAttackDamage(
        GenericActorResolvedMatchContract contract,
        MindBody body)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Id, body.FormId,
                    StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, attackId,
                        StringComparison.Ordinal))
                : null;
        return attack is null
            ? 1
            : Math.Max(1, attack.Projectile.DamagePerHit
                * attack.ProjectilesPerAttack);
    }

    public static bool TryUnitSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        ActorIdentity target,
        string reason)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        GenericActorActionArgument.UnitTarget wanted =
            new(target.TeamId, target.UnitId);
        if (action is not { Available: true }
            || targets is null
            || !targets.AllowedValues.Contains(wanted))
        {
            return false;
        }

        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.UnitTargetArgument(wanted)],
            reason);
        return true;
    }

    public static bool TryHeadingSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        Position target,
        string reason)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .SingleOrDefault();
        if (action is not { Available: true }
            || headings is null
            || !TryRay(body.Position, target, out ProjectileHeading heading,
                out int distance)
            || signature?.Range is int range && distance > range
            || !headings.AllowedValues.Contains(heading)
            || !ClearRay(contract.Map, body.Position, target, true))
        {
            return false;
        }

        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
            reason);
        return true;
    }

    public static bool TryDirectionSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        Position target,
        string reason)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        Direction? selected = directions?.AllowedValues
            .OrderBy(direction =>
            {
                (int dx, int dy) = direction.Vector();
                return body.Position.Offset(dx, dy).ChebyshevDistance(target);
            })
            .ThenBy(direction => (int)direction)
            .Select(direction => (Direction?)direction)
            .FirstOrDefault();
        if (action is not { Available: true } || selected is not Direction value)
            return false;

        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(value)],
            reason);
        return true;
    }

    public static bool TryPositionSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        Position target,
        string reason,
        Func<Position, bool>? extraFilter = null)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        GenericActorActionLegality.ArgumentConstraint.PositionTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .PositionTargetConstraint>()
                .SingleOrDefault();
        Position? selected = targets?.AllowedValues
            .Where(position => extraFilter?.Invoke(position) ?? true)
            .OrderBy(position => position.ChebyshevDistance(target))
            .ThenBy(position => position.Y)
            .ThenBy(position => position.X)
            .Select(position => (Position?)position)
            .FirstOrDefault();
        if (action is not { Available: true } || selected is not Position position)
            return false;

        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.PositionTargetArgument(position)],
            reason);
        return true;
    }

    public static bool TryParameterlessSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string signatureKind,
        string reason)
    {
        GenericActorRulesContract.ArcRelaySignature? signature = Signature(
            contract,
            signatureKind);
        GenericActorActionLegality? action = signature is null
            ? null
            : body.Action(signature.ActionId);
        if (action is not { Available: true } || !action.Constraints.IsEmpty)
            return false;
        body.Command(action.ActionId, action.ActionCode, [], reason);
        return true;
    }

    public static Position Cutoff(
        GenericActorMapContract map,
        Position carrier,
        Position reactor,
        int leadTiles = 2)
    {
        Position cursor = carrier;
        for (int index = 0; index < leadTiles; index++)
        {
            int dx = Math.Sign(reactor.X - cursor.X);
            int dy = Math.Sign(reactor.Y - cursor.Y);
            Position diagonal = cursor.Offset(dx, dy);
            Position horizontal = cursor.Offset(dx, 0);
            Position vertical = cursor.Offset(0, dy);
            Position next = CanEnter(map, diagonal) ? diagonal
                : CanEnter(map, horizontal) ? horizontal
                : CanEnter(map, vertical) ? vertical
                : cursor;
            if (next == cursor)
                break;
            cursor = next;
        }
        return cursor;
    }

    public static Position[] ApproachTiles(
        GenericActorMapContract map,
        Position target) =>
        EightWay
            .Select(heading =>
            {
                (int dx, int dy) = heading.Vector();
                return target.Offset(dx, dy);
            })
            .Where(position => CanEnter(map, position))
            .Distinct()
            .ToArray();

    public static GenericActorRulesContract.ArcRelaySignature? Signature(
        GenericActorResolvedMatchContract contract,
        string kind) =>
        ArcRules(contract)?.Signatures.FirstOrDefault(signature =>
            string.Equals(signature.Kind, kind, StringComparison.Ordinal));

    public static bool HasSignature(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        string kind)
    {
        GenericActorRulesContract.ArcRelaySignature? signature =
            Signature(contract, kind);
        return signature is not null
            && body.Action(signature.ActionId) is { AllowedByForm: true };
    }

    private static bool TryRotate(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        Direction direction,
        string reason)
    {
        GenericActorActionLegality? rotate = AvailableAction(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (rotate is null
            || directions is null
            || body.Facing == direction
            || !directions.AllowedValues.Contains(direction))
        {
            return false;
        }
        body.Command(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            reason);
        return true;
    }

    private static GenericActorActionLegality? AvailableAction(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorRulesContract.ActionKind kind,
        bool requireAvailable = true)
    {
        HashSet<string> ids = contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return body.ActionLegalities
            .Where(action =>
                ids.Contains(action.ActionId)
                && (!requireAvailable || action.Available))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static GenericActorActionLegality? MovementAction(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        ProjectileHeading heading,
        Position destination,
        bool requireAvailable)
    {
        Dictionary<string, GenericActorRulesContract.ActionDefinition> actions =
            contract.Rules.Actions
                .Where(action => action.Kind
                    == GenericActorRulesContract.ActionKind.Movement)
                .ToDictionary(action => action.Id, StringComparer.Ordinal);
        GenericActorActionLegality[] candidates = body.ActionLegalities
            .Where(legality => actions.ContainsKey(legality.ActionId)
                && (!requireAvailable || legality.Available)
                && legality.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .ProjectileHeadingConstraint>()
                    .SingleOrDefault()?.AllowedValues.Contains(heading) == true)
            .OrderBy(legality => legality.ActionCode)
            .ToArray();
        if (candidates.Length <= 1)
            return candidates.SingleOrDefault();

        GenericActorActionLegality? strafe = candidates.FirstOrDefault(
            legality => actions[legality.ActionId].MovementFacingOverride
                == GenericActorRulesContract.MovementFacingCoupling
                    .PreserveFacing);
        GenericActorActionLegality? turn = candidates.FirstOrDefault(
            legality => actions[legality.ActionId].MovementFacingOverride
                is null);
        if (strafe is not null
            && turn is not null
            && WouldTurn(body.Facing, heading)
            && PreservesImmediateEngagement(
                contract,
                mind,
                body,
                destination))
        {
            return strafe;
        }
        return turn ?? strafe ?? candidates[0];
    }

    private static bool PreservesImmediateEngagement(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position destination)
    {
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            cone = AvailableAction(
                    contract,
                    body,
                    GenericActorRulesContract.ActionKind.Attack,
                    requireAvailable: false)
                ?.Constraints.OfType<GenericActorActionLegality
                    .ArgumentConstraint.ProjectileHeadingConstraint>()
                .SingleOrDefault();
        string? attackId = contract.Rules.Forms.FirstOrDefault(form =>
            string.Equals(form.Id, body.FormId, StringComparison.Ordinal))
            ?.AttackProfileId;
        GenericActorRulesContract.AttackProfile? attack =
            contract.Rules.AttackProfiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, attackId, StringComparison.Ordinal));
        if (cone is null || attack is null)
            return false;

        return mind.Enemies.Any(enemy =>
            TryRay(destination, enemy.Position, out ProjectileHeading heading,
                out int distance)
            && distance <= attack.Projectile.MaxTravelTiles + 1
            && cone.AllowedValues.Contains(heading)
            && ClearRay(
                contract.Map,
                destination,
                enemy.Position,
                attack.Projectile.DiagonalCornersMustBeClear));
    }

    private static bool WouldTurn(
        Direction facing,
        ProjectileHeading heading)
    {
        int facingSector = (int)facing * 2;
        int clockwise = ((int)heading - facingSector + 8) % 8;
        return heading is ProjectileHeading.North
                or ProjectileHeading.East
                or ProjectileHeading.South
                or ProjectileHeading.West
            ? (int)heading != facingSector
            : clockwise is not (1 or 7);
    }

    private static Direction FacingForCone(
        Direction current,
        ProjectileHeading target)
    {
        Direction[] candidates = Enum.GetValues<Direction>()
            .Where(direction => HeadingDistance(
                (ProjectileHeading)((int)direction * 2), target) <= 1)
            .OrderBy(direction => HeadingDistance(
                (ProjectileHeading)((int)current * 2),
                (ProjectileHeading)((int)direction * 2)))
            .ThenBy(direction => (int)direction)
            .ToArray();
        return candidates[0];
    }

    private static int HeadingDistance(
        ProjectileHeading first,
        ProjectileHeading second)
    {
        int difference = Math.Abs((int)first - (int)second);
        return Math.Min(difference, 8 - difference);
    }

    private static ProjectileHeading[] RouteHeadings(
        GenericActorResolvedMatchContract contract,
        MindBody body)
    {
        string? profileId = contract.Rules.Forms
            .FirstOrDefault(form =>
                string.Equals(form.Id, body.FormId, StringComparison.Ordinal))
            ?.MovementProfileId;
        GenericActorRulesContract.MovementProfile? profile =
            contract.Rules.MovementProfiles.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, profileId, StringComparison.Ordinal));
        return profile?.FacingCoupling
                == GenericActorRulesContract.MovementFacingCoupling.FacingLocked
            ? Cardinal
            : EightWay;
    }

    private static ProjectileHeading[] PreferredHeadings(
        MindBody body,
        IReadOnlyCollection<Position> goals)
    {
        Position nearest = goals
            .OrderBy(goal => body.Position.ChebyshevDistance(goal))
            .ThenBy(goal => goal.Y)
            .ThenBy(goal => goal.X)
            .First();
        int dx = Math.Sign(nearest.X - body.Position.X);
        int dy = Math.Sign(nearest.Y - body.Position.Y);
        ProjectileHeading facing =
            (ProjectileHeading)((int)body.Facing * 2);
        return EightWay
            .OrderBy(heading =>
            {
                (int hx, int hy) = heading.Vector();
                return Math.Abs(hx - dx) + Math.Abs(hy - dy);
            })
            // Among equally direct shortest-path choices, continue the
            // current bearing. This preserves straight Deliberate runs
            // without lengthening the route or changing any game rule.
            .ThenBy(heading => heading == facing ? 0 : 1)
            .ThenBy(heading => heading)
            .ToArray();
    }

    private static GenericActorRulesContract.ActionKind? PreviousActionKind(
        GenericActorResolvedMatchContract contract,
        MindBody body)
    {
        string? previousActionId = body.PreviousActionResolution
            ?.AcceptedAction.ActionId;
        if (previousActionId is null)
            return null;
        return contract.Rules.Actions
            .FirstOrDefault(action => string.Equals(
                action.Id,
                previousActionId,
                StringComparison.Ordinal))
            ?.Kind;
    }

    private static HashSet<Position> BlockedNow(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody moving,
        Claims claims,
        bool avoidHostileProjectiles = true)
    {
        var blocked = new HashSet<Position>(claims.Tiles);
        blocked.Remove(moving.Position);
        blocked.UnionWith(mind.Allies.Select(ally => ally.Position));
        blocked.UnionWith(mind.Enemies.Select(enemy => enemy.Position));
        blocked.UnionWith(mind.VisibleTiles
            .Where(tile => tile.SpawnReservation is not null)
            .Select(tile => tile.Position));
        // Threefold: never walk onto a loose Core whose origin socket we
        // already filled. Possession of an unbankable Core has no legal
        // disposal and always ends in a felt-degeneracy bar; guarding the
        // tile without pickup keeps the denial and the eligibility.
        if (mind.Mode is GenericActorContext.ModeObservationState.ArcRelay
                arcThreefold)
        {
            var ownFilled = arcThreefold.Reactors
                .FirstOrDefault(reactor =>
                    reactor.TeamId == moving.ActorId.TeamId)
                ?.FilledSocketWellIds;
            if (ownFilled is { IsEmpty: false } filledSockets)
            {
                blocked.UnionWith(arcThreefold.VisibleCores
                    .Where(core => core.Disposition
                            == GenericActorContext.ArcRelayCoreDisposition
                                .Loose
                        && filledSockets.Contains(
                            core.CoreId.SourceWellId,
                            StringComparer.Ordinal))
                    .Select(core => core.Position));
            }
        }

        foreach (GenericActorContext.ObservedProjectile projectile
                 in avoidHostileProjectiles ? mind.VisibleProjectiles ?? [] : [])
        {
            if (projectile.OwnerTeamId == moving.ActorId.TeamId)
                continue;
            blocked.Add(projectile.Position);
            Position cursor = projectile.Position;
            int remaining = Math.Min(
                projectile.RemainingTiles,
                projectile.TilesPerAdvance * 2);
            (int dx, int dy) = projectile.Heading.Vector();
            for (int step = 0; step < remaining; step++)
            {
                cursor = cursor.Offset(dx, dy);
                blocked.Add(cursor);
            }
        }

        if (contract.ModeMapBinding
            is GenericActorResolvedMatchContract.ArcRelayModeMapBinding binding)
        {
            HashSet<int> hostileParticipants = contract.Topology.Participants
                .Where(participant => participant.TeamId != moving.ActorId.TeamId)
                .Select(participant => participant.ParticipantId)
                .ToHashSet();
            HashSet<string> protectedRegions = contract.ParticipantRegionAssignments
                .Where(assignment =>
                    hostileParticipants.Contains(assignment.ParticipantId)
                    && string.Equals(
                        assignment.RegionRoleId,
                        binding.HomePadRegionRoleId,
                        StringComparison.Ordinal))
                .Select(assignment => assignment.MapRegionId)
                .ToHashSet(StringComparer.Ordinal);
            blocked.UnionWith(contract.Map.Regions
                .Where(region => protectedRegions.Contains(region.RegionId))
                .SelectMany(region => region.Tiles));
        }

        if (moving.PreviousActionResolution
                is { Outcome: GenericActorActionResolution.ActionOutcome.Blocked }
                previous
            && previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.ProjectileHeadingArgument>()
                .SingleOrDefault() is { } oldMove)
        {
            (int dx, int dy) = oldMove.Value.Vector();
            blocked.Add(moving.Position.Offset(dx, dy));
        }
        return blocked;
    }

    private static ProjectileHeading? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> blockedNow,
        IReadOnlyCollection<ProjectileHeading> allowed,
        ProjectileHeading[] preference,
        bool blockThroughout = false)
    {
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, ProjectileHeading First)>();
        foreach (ProjectileHeading heading in preference)
        {
            if (!allowed.Contains(heading))
                continue;
            Position next = Step(start, heading);
            if (!CanStep(map, start, next, heading)
                || blockedNow.Contains(next)
                || !visited.Add(next))
            {
                continue;
            }
            if (goals.Contains(next))
                return heading;
            queue.Enqueue((next, heading));
        }

        while (queue.Count > 0)
        {
            (Position position, ProjectileHeading first) = queue.Dequeue();
            foreach (ProjectileHeading heading in preference)
            {
                if (!allowed.Contains(heading))
                    continue;
                Position next = Step(position, heading);
                if (!CanStep(map, position, next, heading)
                    || blockThroughout && blockedNow.Contains(next)
                    || !visited.Add(next))
                    continue;
                if (goals.Contains(next))
                    return first;
                queue.Enqueue((next, first));
            }
        }
        return null;
    }

    private static bool Threatens(
        GenericActorContext.ObservedProjectile projectile,
        Position target,
        int advances) =>
        TryRay(projectile.Position, target, out ProjectileHeading heading,
            out int distance)
        && heading == projectile.Heading
        && distance <= Math.Min(
            projectile.RemainingTiles,
            projectile.TilesPerAdvance * advances);

    private static bool TryRay(
        Position source,
        Position target,
        out ProjectileHeading heading,
        out int distance)
    {
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (distance == 0
            || dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
        {
            heading = default;
            return false;
        }
        heading = FromVector(Math.Sign(dx), Math.Sign(dy));
        return true;
    }

    private static bool ClearRay(
        GenericActorMapContract map,
        Position source,
        Position target,
        bool strictCorners)
    {
        int dx = Math.Sign(target.X - source.X);
        int dy = Math.Sign(target.Y - source.Y);
        Position cursor = source;
        while (cursor != target)
        {
            Position next = cursor.Offset(dx, dy);
            if (next != target && !CanEnter(map, next))
                return false;
            if (strictCorners && dx != 0 && dy != 0
                && (!CanEnter(map, cursor.Offset(dx, 0))
                    || !CanEnter(map, cursor.Offset(0, dy))))
            {
                return false;
            }
            cursor = next;
        }
        return true;
    }

    private static bool CanStep(
        GenericActorMapContract map,
        Position from,
        Position to,
        ProjectileHeading heading)
    {
        if (!CanEnter(map, to))
            return false;
        (int dx, int dy) = heading.Vector();
        return dx == 0 || dy == 0
            || CanEnter(map, from.Offset(dx, 0))
                && CanEnter(map, from.Offset(0, dy));
    }

    private static bool CanEnter(GenericActorMapContract map, Position tile) =>
        tile.X >= 0
        && tile.Y >= 0
        && tile.X < map.Width
        && tile.Y < map.Height
        && map.TileRows[tile.Y][tile.X] != '#';

    private static Position Step(Position position, ProjectileHeading heading)
    {
        (int dx, int dy) = heading.Vector();
        return position.Offset(dx, dy);
    }

    private static ProjectileHeading FromVector(int dx, int dy) =>
        (dx, dy) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => throw new ArgumentOutOfRangeException(nameof(dx)),
        };

    private static Direction? ToDirection(ProjectileHeading heading) =>
        heading switch
        {
            ProjectileHeading.North => Direction.North,
            ProjectileHeading.East => Direction.East,
            ProjectileHeading.South => Direction.South,
            ProjectileHeading.West => Direction.West,
            _ => null,
        };
}
