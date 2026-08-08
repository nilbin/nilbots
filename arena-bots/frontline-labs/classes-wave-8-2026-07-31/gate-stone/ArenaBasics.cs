using BotArena.Sdk;

/// <summary>
/// Contract-driven tactical building blocks for the generated starter.
/// Keep or replace them as the bot develops; strategy belongs in GateStone.Tick.
/// </summary>
internal static class ArenaBasics
{
    private static readonly Direction[] Directions =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];
    private static readonly HashSet<Position> NoPositions = [];

    public static GenericActorDecision? TryFabricateReady(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Fabrication)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality? action = context.ActionLegalities
            .Where(candidate =>
                candidate.Available
                && actionIds.Contains(candidate.ActionId))
            .OrderBy(candidate => candidate.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .SingleOrDefault();
        if (action is null || targets is null || targets.AllowedValues.IsEmpty)
            return null;

        GenericActorActionArgument.UnitTarget target =
            targets.AllowedValues
                .OrderBy(value => value.TeamId)
                .ThenBy(value => value.UnitId)
                .First();
        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [
                new GenericActorActionArgument.UnitTargetArgument(target),
            ],
            $"activating companion {target.TeamId}:{target.UnitId}");
    }

    public static GenericActorDecision? TryDodge(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorContext.ObservedProjectile[] hostile =
            context.VisibleProjectiles
                ?.Where(projectile =>
                    projectile.OwnerTeamId
                        != context.Self.ActorId.TeamId)
                .OrderBy(projectile => projectile.ProjectileId)
                .ToArray()
            ?? [];
        if (!hostile.Any(projectile =>
                ReachesWithinAdvances(
                    projectile,
                    context.Self.Position,
                    maxAdvances: 2)))
        {
            return null;
        }

        GenericActorActionLegality? move = AvailableAction(
            contract,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (move is null || constraint is null)
            return null;

        HashSet<Position> occupied = Occupied(context, hostile);
        Position[] objectiveTiles = ActiveObjectiveTiles(contract, context);
        bool holdingObjective =
            objectiveTiles.Contains(context.Self.Position);
        Direction[] preference = OrderedDirections(contract, context);
        Direction? selected = constraint.AllowedValues
            .Where(direction => Directions.Contains(direction))
            .Select(direction =>
            {
                (int dx, int dy) = direction.Vector();
                return (
                    Direction: direction,
                    Destination: context.Self.Position.Offset(dx, dy));
            })
            .Where(candidate =>
                CanEnter(contract.Map, candidate.Destination, occupied)
                && !hostile.Any(projectile =>
                    ReachesWithinAdvances(
                        projectile,
                        candidate.Destination,
                        maxAdvances: 2)))
            .OrderByDescending(candidate =>
                !holdingObjective
                || objectiveTiles.Contains(candidate.Destination))
            .ThenBy(candidate =>
                DistanceToObjective(
                    candidate.Destination,
                    objectiveTiles))
            .ThenByDescending(candidate =>
                hostile.Min(projectile =>
                    candidate.Destination.ChebyshevDistance(
                        projectile.Position)))
            .ThenBy(candidate =>
                Array.IndexOf(preference, candidate.Direction))
            .Select(candidate => (Direction?)candidate.Direction)
            .FirstOrDefault();
        if (selected is not Direction direction)
            return null;

        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [
                new GenericActorActionArgument.DirectionArgument(direction),
            ],
            $"dodging imminent projectile toward {direction}");
    }

    public static GenericActorDecision? TryDirectShot(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    context.Self.FormId,
                    StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackProfileId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Id,
                        attackProfileId,
                        StringComparison.Ordinal))
                : null;
        if (attack is null || context.Enemies.IsEmpty)
            return null;

        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Attack)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality[] actions = context.ActionLegalities
            .Where(action =>
                action.Available
                && actionIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .ToArray();

        foreach (GenericActorContext.ObservedEnemyState target
                 in context.Enemies
                     .OrderBy(enemy => enemy.Health)
                     .ThenBy(enemy =>
                         context.Self.Position.ChebyshevDistance(
                             enemy.Position))
                     .ThenBy(enemy => enemy.ActorId))
        {
            if (!TryRay(
                    context.Self.Position,
                    target.Position,
                    out ProjectileHeading heading,
                    out int distance)
                || distance > attack.Projectile.MaxTravelTiles
                || !ClearRay(
                    contract.Map,
                    context.Self.Position,
                    target.Position,
                    attack.Projectile.DiagonalCornersMustBeClear))
            {
                continue;
            }

            foreach (GenericActorActionLegality action in actions)
            {
                GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint? headings =
                    action.Constraints
                        .OfType<GenericActorActionLegality.ArgumentConstraint
                            .ProjectileHeadingConstraint>()
                        .SingleOrDefault();
                if (headings is not null
                    && headings.AllowedValues.Contains(heading))
                {
                    return new GenericActorDecision(
                        action.ActionId,
                        action.ActionCode,
                        [
                            new GenericActorActionArgument
                                .ProjectileHeadingArgument(heading),
                        ],
                        $"direct fire at {target.ActorId}");
                }

                int aimOffset = SignedHeadingDifference(
                    context.Self.Facing.ToProjectileHeading(),
                    heading);
                if (aimOffset < attack.ShotProgram.MinInitialAimSteps
                    || aimOffset > attack.ShotProgram.MaxInitialAimSteps)
                {
                    continue;
                }
                // A facing-aligned shot needs no payload when programs are
                // optional — or when they are disabled entirely, which is
                // how straight-only chassis (their attack action declares no
                // parameters at all) fire every shot.
                if (aimOffset == 0
                    && (attack.ShotProgram.PayloadOptional
                        || !attack.ShotProgram.Enabled))
                {
                    return GenericActorDecision.WithoutArguments(
                        action.ActionId,
                        action.ActionCode,
                        $"straight fire at {target.ActorId}");
                }

                GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint? programs =
                    action.Constraints
                        .OfType<GenericActorActionLegality.ArgumentConstraint
                            .ShotProgramConstraint>()
                        .SingleOrDefault();
                if (programs is not { Allowed: true }
                    || !attack.ShotProgram.Enabled)
                {
                    continue;
                }

                GenericActorRulesContract.AimOnlyShotProgramValue aimOnly =
                    attack.ShotProgram.AimOnlyProgram;
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [
                        new GenericActorActionArgument.ShotProgramArgument(
                            new ShotProgram(
                                aimOffset,
                                aimOnly.BendDirection,
                                aimOnly.BendAfterTiles,
                                aimOnly.BendEveryTiles,
                                aimOnly.BendCount)),
                    ],
                    $"aimed direct fire at {target.ActorId}");
            }
        }
        return null;
    }

    /// <summary>
    /// One step toward the currently active objective, or the rotation that
    /// unlocks that step. Every input is observed or chain-derived: the route
    /// starts at <c>context.Self.Position</c> and ends on the active
    /// objective's tiles, so it is correct wherever a life happens to appear.
    /// Do not replace that with a home-relative plan — contracts differ in
    /// where automatic arrivals land (see
    /// <see cref="ExpectedArrivalTiles"/>), and "spawn at home, walk to the
    /// front" is a plan that only some of them honour.
    /// </summary>
    public static GenericActorDecision? TryAdvanceToActiveObjective(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        IEnumerable<Position>? temporarilyBlocked = null)
    {
        Position[] goals = ActiveObjectiveTiles(contract, context);
        if (goals.Length == 0
            || goals.Contains(context.Self.Position))
        {
            return null;
        }

        GenericActorActionLegality? move = AvailableAction(
            contract,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (move is null || constraint is null)
            return null;

        // Hostile projectiles always block or hit. Whether ALLIED bolts do is
        // the contract's allied-contact policy — read it rather than assume
        // pass-through, so this helper survives a future collision arm.
        bool alliedBoltsPass = contract.Rules.Collisions
            .AlliedProjectileContact
            .Contains("pass-through", StringComparison.Ordinal);
        HashSet<Position> occupied = Occupied(
            context,
            (context.VisibleProjectiles ?? [])
                .Where(projectile =>
                    !alliedBoltsPass
                    || projectile.OwnerTeamId
                        != context.Self.ActorId.TeamId));
        if (temporarilyBlocked is not null)
            occupied.UnionWith(temporarilyBlocked);
        if (
            context.Self.PreviousActionResolution
                is
                {
                    Outcome:
                        GenericActorActionResolution.ActionOutcome.Blocked,
                } previous
            && previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.DirectionArgument>()
                .SingleOrDefault()
                is { } blockedDirection)
        {
            (int dx, int dy) = blockedDirection.Value.Vector();
            occupied.Add(
                context.Self.Position.Offset(dx, dy));
        }
        // The legality mask says what is legal THIS tick, not where routes
        // may go: under a facing-coupled arm it offers only the current
        // facing, and seeding the search from it prunes every turn-requiring
        // route — the bot freezes instead of turning. Plan on map geometry;
        // when the mask refuses the planned step, spend a rotation to
        // unlock it next tick.
        Direction? step = FindFirstStep(
            contract.Map,
            context.Self.Position,
            goals.ToHashSet(),
            occupied,
            Directions.ToHashSet(),
            OrderedDirections(contract, context));
        if (step is not Direction direction)
            return null;

        if (!constraint.AllowedValues.Contains(direction))
        {
            GenericActorActionLegality? rotate = AvailableAction(
                contract,
                context,
                GenericActorRulesContract.ActionKind.Rotation);
            GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
                heading = rotate?.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint>()
                    .SingleOrDefault();
            if (rotate is null
                || heading is null
                || !heading.AllowedValues.Contains(direction)
                || context.Self.Facing == direction)
            {
                return null;
            }
            return new GenericActorDecision(
                rotate.ActionId,
                rotate.ActionCode,
                [
                    new GenericActorActionArgument.DirectionArgument(
                        direction),
                ],
                $"rotating toward {direction} to unlock the next step");
        }

        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [
                new GenericActorActionArgument.DirectionArgument(direction),
            ],
            $"advancing toward active objective via {direction}");
    }

    public static GenericActorDecision Wait(
        GenericActorContext context,
        string reason)
    {
        // Never throw: a deliberate fault is a protocol violation, while a
        // Blocked outcome is safe. Prefer an available wait, then an
        // unavailable one, then any available parameterless action, and as
        // a last resort the first declared action.
        GenericActorActionLegality? wait = context.ActionLegalities
            .FirstOrDefault(action =>
                action.Available
                && string.Equals(
                    action.ActionId,
                    "wait",
                    StringComparison.Ordinal))
            ?? context.ActionLegalities
                .FirstOrDefault(action =>
                    string.Equals(
                        action.ActionId,
                        "wait",
                        StringComparison.Ordinal))
            ?? context.ActionLegalities
                .FirstOrDefault(action =>
                    action.Available && action.Constraints.IsEmpty)
            ?? context.ActionLegalities.First();
        return GenericActorDecision.WithoutArguments(
            wait.ActionId,
            wait.ActionCode,
            reason);
    }

    private static GenericActorActionLegality? AvailableAction(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return context.ActionLegalities
            .Where(action =>
                action.Available
                && actionIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// Tiles of the objective the match is currently fought over, taken from
    /// the observed active index rather than any fixed position. Empty when
    /// the mode declares no ordered objectives.
    /// </summary>
    public static Position[] ActiveObjectiveTiles(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context) =>
        context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? ObjectiveTiles(contract, mode.ActivePositionIndex)
            : [];

    /// <summary>
    /// Which team's advance the territory ratchet is protecting right now, and
    /// the tick that protection lifts — READ from the observation, not inferred
    /// from it. Null when no hold binds this tick, which includes every
    /// contract whose capture definition declares no hold at all.
    ///
    /// <para>This used to be a derivation, and the derivation was expensive
    /// and partly wrong. The start was recoverable as
    /// <c>ControlResumesAtTick − RedeployPauseTicks</c>; the OWNER had no
    /// derivation at all, only a guess from the signed displacement of the
    /// front, which is wrong the first time an opponent regresses from a lead
    /// and is unavailable to a life born inside the hold, because private
    /// memory is life-scoped. Both facts are now published, so ask.</para>
    ///
    /// <para>Inside a live hold the two sides are playing opposite games: the
    /// owner's presence buys ground and the opponent's presence buys nothing,
    /// because a capture completed inside another team's hold is SPENT — the
    /// claim resets exactly as a successful capture does and the objective does
    /// not move. Compare <see cref="Hold.EndsAtTick"/> with
    /// <c>context.Tick</c> the same way you compare
    /// <c>ControlResumesAtTick</c>: the hold binds while the tick is strictly
    /// below it.</para>
    /// </summary>
    public static Hold? LiveHold(GenericActorContext context) =>
        context.Mode
            is GenericActorContext.ModeObservationState.Frontline
            {
                HoldOwnerTeamId: int owner,
                HoldEndsAtTick: int endsAt,
            }
            ? new Hold(
                owner,
                owner == context.Self.ActorId.TeamId,
                endsAt,
                endsAt - context.Tick)
            : null;

    /// <summary>
    /// A live territory-ratchet hold as this life sees it.
    /// </summary>
    /// <param name="OwnerTeamId">Team whose advance is protected.</param>
    /// <param name="Mine">
    /// True when this life's team owns the hold — the side for which standing
    /// on the objective is worth something.
    /// </param>
    /// <param name="EndsAtTick">
    /// First tick on which the hold no longer denies regression.
    /// </param>
    /// <param name="RemainingTicks">
    /// Ticks the hold still has to run from this observation's tick.
    /// </param>
    public sealed record Hold(
        int OwnerTeamId,
        bool Mine,
        int EndsAtTick,
        int RemainingTicks);

    /// <summary>
    /// Tiles of one objective in the ordered chain. Empty for an index outside
    /// the chain, which is the honest answer for "one step past the end" —
    /// callers walking the chain do not need their own bounds check.
    /// </summary>
    public static Position[] ObjectiveTiles(
        GenericActorResolvedMatchContract contract,
        int positionIndex)
    {
        if (contract.ModeMapBinding
                is not GenericActorResolvedMatchContract
                    .FrontlineModeMapBinding binding
            || positionIndex < 0
            || positionIndex >= binding.OrderedObjectiveRegionIds.Length)
        {
            return [];
        }

        string regionId = binding.OrderedObjectiveRegionIds[positionIndex];
        return contract.Map.Regions
            .FirstOrDefault(region =>
                string.Equals(
                    region.RegionId,
                    regionId,
                    StringComparison.Ordinal))
            ?.Tiles
            .ToArray()
            ?? [];
    }

    /// <summary>
    /// Tiles of the objective one step BEHIND this team's advance — the ground
    /// it already took. Derived from the chain and the team's declared index
    /// delta, never from a spawn, so it moves with the front. Empty when the
    /// team is already at its end of the chain or the mode declares no ordered
    /// objectives.
    /// </summary>
    public static Position[] OwnSideObjectiveTiles(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context) =>
        context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? OwnSideObjectiveTiles(
                contract,
                context.Self.ActorId.TeamId,
                mode.ActivePositionIndex)
            : [];

    /// <summary>
    /// Any team's own-side objective, so the same question can be asked about
    /// where the opposition falls back to.
    /// </summary>
    public static Position[] OwnSideObjectiveTiles(
        GenericActorResolvedMatchContract contract,
        int teamId,
        int activePositionIndex)
    {
        if (contract.ModeMapBinding
            is not GenericActorResolvedMatchContract
                .FrontlineModeMapBinding binding)
        {
            return [];
        }
        var advance = binding.TeamAdvances.FirstOrDefault(entry =>
            entry.TeamId == teamId);
        return advance is null
            ? []
            : ObjectiveTiles(
                contract,
                activePositionIndex - advance.ObjectiveIndexDelta);
    }

    /// <summary>
    /// Whether this contract places automatic returns and activations by the
    /// objective chain instead of by the slot's spawn anchor. Read
    /// <c>lifecycle.automaticReturnPlacement</c> — it is a policy ID, and the
    /// chain-derived value names the own-side chain-adjacent objective.
    /// Nothing in the observation schema changes with it, so a bot that never
    /// reads it simply believes the wrong thing about where it will reappear.
    /// </summary>
    public static bool ArrivalsRallyForward(
        GenericActorResolvedMatchContract contract) =>
        contract.Rules.Lifecycle.AutomaticReturnPlacement.Contains(
            "own-side-chain-adjacent-objective",
            StringComparison.Ordinal);

    /// <summary>
    /// Where this life's slot can expect its next automatic arrival: the
    /// own-side chain-adjacent objective region when the contract rallies
    /// arrivals forward, otherwise the slot's declared spawn anchor. This is
    /// the contract's INTENT — a chain-deriving host still falls back to the
    /// anchor when the derived region offers no free tile, and only
    /// <c>context.Self.Position</c> on a life's first tick is the fact. Use
    /// this to price a death before it happens ("how far from the fight does
    /// dying put me?"); use the observed position to plan once it has.
    /// Empty when the contract declares neither an anchor nor a chain.
    /// </summary>
    public static Position[] ExpectedArrivalTiles(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context) =>
        context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? ExpectedArrivalTiles(
                contract,
                context.Self.ActorId.TeamId,
                context.Self.ActorId.UnitId,
                mode.ActivePositionIndex)
            : ExpectedArrivalTiles(
                contract,
                context.Self.ActorId.TeamId,
                context.Self.ActorId.UnitId,
                activePositionIndex: -1);

    /// <summary>
    /// Any slot's expected arrival, so the same question can be asked about an
    /// enemy slot whose reinforcement timing you already read from the
    /// lifecycle assignments.
    /// </summary>
    public static Position[] ExpectedArrivalTiles(
        GenericActorResolvedMatchContract contract,
        int teamId,
        int unitId,
        int activePositionIndex)
    {
        Position[] rally = ArrivalsRallyForward(contract)
            ? OwnSideObjectiveTiles(contract, teamId, activePositionIndex)
            : [];
        if (rally.Length > 0)
            return rally;

        string? spawnId = contract.LifecycleAssignments
            .FirstOrDefault(assignment =>
                assignment.TeamId == teamId
                && assignment.UnitId == unitId)
            ?.AssignedRespawnSpawnId;
        if (spawnId is null)
            return [];
        return contract.InitialDeployment.Spawns
            .Where(spawn =>
                string.Equals(
                    spawn.SpawnId,
                    spawnId,
                    StringComparison.Ordinal))
            .Select(spawn => spawn.Position)
            .ToArray();
    }

    /// <summary>
    /// Objective weight standing on the active objective right now, split by
    /// side, plus whether this life is one of the bodies counted. Weight comes
    /// from each observed body's form, because a form may declare weight zero
    /// and hold ground for nothing.
    /// <para>
    /// What the numbers are worth is a contract fact, not a constant: read
    /// <see cref="CaptureRules.SurplusWeightScalesGain"/> to learn whether a
    /// second body adds pressure or is merely a second body, and
    /// <see cref="CaptureRules.OnlyEnemySolePresenceDecays"/> to learn whether
    /// standing contested is costing your claim or preserving it.
    /// </para>
    /// <para>
    /// Enemy weight counts only what your team currently SEES. An unobserved
    /// body on the objective still contests it, so treat this as a lower
    /// bound on the opposition.
    /// </para>
    /// </summary>
    public static (int OwnWeight, int EnemyWeight, bool SelfPresent)
        ObjectivePresence(
            GenericActorResolvedMatchContract contract,
            GenericActorContext context)
    {
        HashSet<Position> tiles =
            ActiveObjectiveTiles(contract, context).ToHashSet();
        if (tiles.Count == 0)
            return (0, 0, false);

        int Weight(string formId) => contract.Rules.Forms
            .FirstOrDefault(form =>
                string.Equals(form.Id, formId, StringComparison.Ordinal))
            ?.ObjectiveWeight
            ?? 0;

        bool selfPresent = tiles.Contains(context.Self.Position);
        int own = selfPresent ? Weight(context.Self.FormId) : 0;
        own += context.Allies
            .Where(ally => tiles.Contains(ally.Position))
            .Sum(ally => Weight(ally.FormId));
        int enemy = context.Enemies
            .Where(enemy => tiles.Contains(enemy.Position))
            .Sum(enemy => Weight(enemy.FormId));
        return (own, enemy, selfPresent);
    }

    /// <summary>
    /// The capture policy VALUES this contract plays by, gathered so a
    /// doctrine prices pushes from the contract instead of from habit. None of
    /// these fields changes the observation schema, so reading them is the
    /// only way to tell one capture policy from another.
    /// Null when the mode is not objective-based — a deathmatch contract
    /// carries no capture definition at all.
    /// </summary>
    public static CaptureRules? Capture(
        GenericActorResolvedMatchContract contract)
    {
        if (contract.Rules.GameMode
            is not GenericActorRulesContract.FrontlineGameMode frontline)
        {
            return null;
        }

        GenericActorRulesContract.FrontlineCapture capture = frontline.Capture;
        return new CaptureRules(
            capture.Threshold,
            capture.GainPerSoleTeamTick,
            capture.DecayAmount,
            capture.DecayIntervalTicks,
            capture.RedeployPauseTicks,
            capture.RatchetHoldTicks > 0 ? capture.RatchetHoldTicks : null,
            capture.ControlPolicy.Contains(
                "net-positive-objective-weight-difference",
                StringComparison.Ordinal),
            capture.DecayClock.Contains(
                "enemy-sole-erosion-only",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// What one objective push costs and what protects it, read from the
    /// capture definition. Canonical contracts omit inert fields, so an
    /// ABSENT value is a real answer rather than a gap.
    /// </summary>
    /// <param name="Threshold">
    /// Progress required to complete one capture.
    /// </param>
    /// <param name="GainPerSoleTeamTick">
    /// Base progress per tick of control. The control policy decides whether
    /// that base is multiplied by surplus weight; see
    /// <paramref name="SurplusWeightScalesGain"/>.
    /// </param>
    /// <param name="DecayAmount">Progress removed at each decay application.</param>
    /// <param name="DecayIntervalTicks">Ticks between decay applications.</param>
    /// <param name="RedeployPauseTicks">
    /// Ticks after an advance during which control cannot resume. The
    /// observation's <c>ControlResumesAtTick</c> is the live clock.
    /// </param>
    /// <param name="HoldTicks">
    /// How long a completed advance is protected from being pushed back, or
    /// <see langword="null"/> when the capture definition declares no hold at
    /// all — an absent hold field means captures never lock, and the front can
    /// come straight back. When a hold IS declared, a capture completed inside
    /// another team's live hold is SPENT: the claim resets exactly as a
    /// successful capture does and the objective does not move. Pricing a push
    /// as if every capture advances the front is the mistake this field exists
    /// to prevent. This is the contract's DURATION; for the hold that is
    /// running right now — whose it is and when it lifts — call
    /// <see cref="LiveHold"/>, which reads both from the observation instead
    /// of reconstructing them from the advance you happened to witness.
    /// </param>
    /// <param name="SurplusWeightScalesGain">
    /// True when net objective weight scales capture pressure, so a second
    /// body on the objective is worth more than the first body's presence.
    /// False when control is binary: one body of positive weight nulls any
    /// number of opposing bodies, and reinforcing a contested objective buys
    /// nothing but survivability.
    /// </param>
    /// <param name="OnlyEnemySolePresenceDecays">
    /// True when empty and contested ticks preserve a claim and only an enemy
    /// standing alone erodes it — leaving an objective is then cheap, and
    /// contesting one is a full stop rather than a slow bleed. False when the
    /// decay clock also runs while the objective is empty or contested.
    /// </param>
    public sealed record CaptureRules(
        int Threshold,
        int GainPerSoleTeamTick,
        int DecayAmount,
        int DecayIntervalTicks,
        int RedeployPauseTicks,
        int? HoldTicks,
        bool SurplusWeightScalesGain,
        bool OnlyEnemySolePresenceDecays);

    private static HashSet<Position> Occupied(
        GenericActorContext context,
        IEnumerable<GenericActorContext.ObservedProjectile> projectiles) =>
        context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .Concat(projectiles.Select(projectile => projectile.Position))
            .ToHashSet();

    /// <summary>
    /// The first step of a shortest route to <paramref name="goals"/>.
    /// <paramref name="blockedNow"/> holds this tick's transient occupants —
    /// bodies and bolts — and applies to the first step ONLY, because only the
    /// first step is executed: the route is replanned from scratch next tick,
    /// by which time those tiles have moved. Treating transient occupants as
    /// permanent walls makes the router surrender routes that will be open
    /// long before the body arrives, and it surrenders them exactly when
    /// bodies are densest — which is the state a contract that rallies
    /// arrivals onto one objective region produces on purpose. Walls block at
    /// every depth; they are the only thing that does.
    /// </summary>
    private static Direction? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> blockedNow,
        IReadOnlySet<Direction> allowedFirstSteps,
        Direction[]? searchOrder = null)
    {
        // Iteration order is the tie-break for equal-length paths — an
        // absolute order here is the same systematic side bias as an
        // absolute movement preference. Callers pass OrderedDirections.
        Direction[] order = searchOrder ?? Directions;
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in order)
        {
            if (!allowedFirstSteps.Contains(direction))
                continue;
            Position next = Offset(start, direction);
            if (!CanEnter(map, next, blockedNow)
                || !visited.Add(next))
            {
                continue;
            }
            if (goals.Contains(next))
                return direction;
            queue.Enqueue((next, direction));
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (Direction direction in order)
            {
                Position next = Offset(current.Position, direction);
                if (!CanEnter(map, next, NoPositions)
                    || !visited.Add(next))
                {
                    continue;
                }
                if (goals.Contains(next))
                    return current.First;
                queue.Enqueue((next, current.First));
            }
        }
        return null;
    }

    /// <summary>
    /// "Should I eat this?" — what one hostile bolt costs and how long you have
    /// to decide, read from the bolt rather than reverse-engineered from the
    /// attack profile that fired it (which a redacted owner may not even name).
    /// Null when the bolt cannot reach <paramref name="target"/> on its current
    /// heading and remaining range at all.
    ///
    /// <para>The arithmetic is the contract's own: the bolt crosses
    /// <c>TilesPerAdvance</c> tiles every <c>TicksPerAdvance</c> ticks and the
    /// next advance is <c>TicksUntilAdvance</c> away, so an exact tick of
    /// arrival exists — and <c>DamagePerHit</c> says whether arriving matters.
    /// Both new fields are per projectile: a volley bolt and an ordinary bolt
    /// need not agree on either.</para>
    /// </summary>
    public static Incoming? Threat(
        GenericActorContext.ObservedProjectile projectile,
        Position target)
    {
        if (!TryRay(
                projectile.Position,
                target,
                out ProjectileHeading heading,
                out int distance)
            || heading != projectile.Heading
            || distance > projectile.RemainingTiles)
        {
            return null;
        }

        // The first advance is TicksUntilAdvance away; each later one adds a
        // full cadence. Integer ceiling, because a partial advance does not
        // move the bolt.
        int advances = (distance + projectile.TilesPerAdvance - 1)
            / projectile.TilesPerAdvance;
        int ticks = projectile.TicksUntilAdvance
            + (advances - 1) * projectile.TicksPerAdvance;
        return new Incoming(ticks, projectile.DamagePerHit, distance);
    }

    /// <summary>One hostile bolt's exact bill and deadline.</summary>
    /// <param name="TicksUntilArrival">
    /// Ticks until the bolt reaches the target tile.
    /// </param>
    /// <param name="Damage">Health one contact removes.</param>
    /// <param name="Tiles">Tiles between the bolt and the target.</param>
    public sealed record Incoming(
        int TicksUntilArrival,
        int Damage,
        int Tiles);

    private static bool ReachesWithinAdvances(
        GenericActorContext.ObservedProjectile projectile,
        Position target,
        int maxAdvances)
    {
        if (!TryRay(
                projectile.Position,
                target,
                out ProjectileHeading heading,
                out int distance)
            || heading != projectile.Heading)
        {
            return false;
        }
        return distance <= Math.Min(
            projectile.TilesPerAdvance * maxAdvances,
            projectile.RemainingTiles);
    }

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

        (int StepX, int StepY) step = (Math.Sign(dx), Math.Sign(dy));
        heading = step switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => default,
        };
        return true;
    }

    private static bool ClearRay(
        GenericActorMapContract map,
        Position source,
        Position target,
        bool strictDiagonalCorners)
    {
        int stepX = Math.Sign(target.X - source.X);
        int stepY = Math.Sign(target.Y - source.Y);
        Position cursor = source;
        while (cursor != target)
        {
            Position next = cursor.Offset(stepX, stepY);
            if (next != target && !CanEnter(map, next, NoPositions))
                return false;
            if (strictDiagonalCorners
                && stepX != 0
                && stepY != 0
                && (
                    !CanEnter(
                        map,
                        cursor.Offset(stepX, 0),
                        NoPositions)
                    || !CanEnter(
                        map,
                        cursor.Offset(0, stepY),
                        NoPositions)
                ))
            {
                return false;
            }
            cursor = next;
        }
        return true;
    }

    private static bool CanEnter(
        GenericActorMapContract map,
        Position position,
        IReadOnlySet<Position> occupied) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#'
        && !occupied.Contains(position);

    private static Position Offset(
        Position position,
        Direction direction)
    {
        (int dx, int dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static int DistanceToObjective(
        Position position,
        IReadOnlyCollection<Position> objectiveTiles) =>
        objectiveTiles.Count == 0
            ? 0
            : objectiveTiles.Min(position.ChebyshevDistance);

    private static int SignedHeadingDifference(
        ProjectileHeading from,
        ProjectileHeading to)
    {
        int difference = ((int)to - (int)from + 8) % 8;
        return difference > 4 ? difference - 8 : difference;
    }

    /// <summary>
    /// This team's advance direction as a map heading, derived from the
    /// ordered objective regions and the team's declared index direction.
    /// Chain-derived on purpose: "away from my spawn" agrees with it only on
    /// contracts that put spawns behind the chain, and a bot that reappears
    /// beside the fight has no home vector to reason from.
    /// Null when the mode declares no advance (deathmatch, or degenerate
    /// geometry).
    /// </summary>
    public static Direction? AdvanceDirection(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context) =>
        AdvanceDirection(contract, context.Self.ActorId.TeamId);

    /// <summary>
    /// Any team's advance direction, so a doctrine can reason about where the
    /// opposition is pushing from as easily as about its own front.
    /// </summary>
    public static Direction? AdvanceDirection(
        GenericActorResolvedMatchContract contract,
        int teamId)
    {
        if (contract.ModeMapBinding
            is not GenericActorResolvedMatchContract
                .FrontlineModeMapBinding binding
            || binding.OrderedObjectiveRegionIds.Length < 2)
        {
            return null;
        }
        var advance = binding.TeamAdvances.FirstOrDefault(entry =>
            entry.TeamId == teamId);
        if (advance is null)
            return null;

        (double X, double Y)? Centroid(string regionId)
        {
            var region = contract.Map.Regions.FirstOrDefault(entry =>
                string.Equals(
                    entry.RegionId, regionId, StringComparison.Ordinal));
            if (region is null || region.Tiles.IsEmpty)
                return null;
            return (
                region.Tiles.Average(tile => (double)tile.X),
                region.Tiles.Average(tile => (double)tile.Y));
        }

        (double X, double Y)? first =
            Centroid(binding.OrderedObjectiveRegionIds[0]);
        (double X, double Y)? last = Centroid(
            binding.OrderedObjectiveRegionIds[^1]);
        if (first is not { } from || last is not { } to)
            return null;
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        if (advance.ObjectiveIndexDelta < 0)
        {
            dx = -dx;
            dy = -dy;
        }
        if (dx == 0 && dy == 0)
            return null;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx > 0 ? Direction.East : Direction.West
            : dy > 0 ? Direction.South : Direction.North;
    }

    /// <summary>
    /// A mirror-fair direction preference: advance first, retreat last, and
    /// the two perpendiculars ordered by the TEAM's deterministic random
    /// stream. An absolute order (always prefer East) gives the east-advancing
    /// team a systematic edge on a mirror-symmetric map — measured as a
    /// 40-of-40 side sweep in the wave-1 factorial — because both teams share
    /// the same absolute preference. Randomizing residual ties converts that
    /// bias into seed noise, which mirrored accounting can wash out.
    ///
    /// <para>
    /// It reads <c>context.TeamRandom</c>, not <c>context.Random</c>. The
    /// per-life stream differs between teammates, so any plan built on top of
    /// this helper silently diverged across the team — that is exactly the
    /// trap the wave-6 sweeps hit. The team stream re-derives per tick, so
    /// every teammate calling this at the same point in the same tick gets
    /// the same order, and a life born mid-match agrees immediately. Pass a
    /// different <paramref name="random"/> only when you deliberately want a
    /// per-life tie-break that no teammate will reproduce.
    /// </para>
    /// </summary>
    public static Direction[] OrderedDirections(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context,
        IBotRandom? random = null)
    {
        Direction forward =
            AdvanceDirection(contract, context) ?? context.Self.Facing;
        Direction backward = Opposite(forward);
        Direction[] laterals =
            Directions.Where(direction =>
                direction != forward && direction != backward)
            .ToArray();
        if (laterals.Length == 2
            && (random ?? context.TeamRandom).NextBool())
        {
            (laterals[0], laterals[1]) = (laterals[1], laterals[0]);
        }
        return [forward, .. laterals, backward];
    }

    private static Direction Opposite(Direction direction) =>
        direction switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            _ => Direction.East,
        };

    /// <summary>
    /// The class of the team controlling <paramref name="teamId"/>, derived
    /// from the contract: class chassis name every form
    /// <c>&lt;class&gt;-&lt;role&gt;</c> (the pinned convention), so the
    /// shared prefix of a team's slot forms is its class. Returns null on
    /// contracts without class chassis (the base arms) — write doctrine
    /// against the stats and routes either way; a typed classId replaces
    /// this helper's body in a later contract generation.
    /// </summary>
    public static string? ClassOf(
        GenericActorResolvedMatchContract contract,
        int teamId)
    {
        string[] prefixes = contract.LifecycleAssignments
            .Where(assignment => assignment.TeamId == teamId)
            .SelectMany(assignment => assignment.AllowedFormIds)
            .Select(formId => formId.Split('-')[0])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (prefixes.Length != 1)
            return null;
        string prefix = prefixes[0];
        bool isBaseCatalog = contract.Rules.Forms.Any(form =>
            string.Equals(form.Id, "prime-mobile", StringComparison.Ordinal));
        return isBaseCatalog ? null : prefix;
    }

    /// <summary>
    /// Contract capabilities a doctrine usually branches on, gathered in one
    /// place so bots read the contract instead of assuming an arm: whether
    /// this form's attack takes shot programs, whether any same-life route
    /// leads into a zero-weight (fortified) form, whether an explicit
    /// fabrication route exists for this form, and this slot's unlock tick.
    /// </summary>
    public static (
        bool ShotPrograms,
        bool AnchorRoute,
        bool FabricationRoute,
        int? UnlockTick)
        Capabilities(
            GenericActorResolvedMatchContract contract,
            GenericActorContext context)
    {
        string formId = context.Self.FormId;
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id, formId, StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Id, attackId, StringComparison.Ordinal))
                : null;
        HashSet<string> fortifiedForms = contract.Rules.Forms
            .Where(candidate => candidate.ObjectiveWeight == 0)
            .Select(candidate => candidate.Id)
            .ToHashSet(StringComparer.Ordinal);
        bool anchorRoute = contract.Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .Any(transition =>
                string.Equals(
                    transition.SourceFormId,
                    formId,
                    StringComparison.Ordinal)
                && fortifiedForms.Contains(transition.TargetFormId));
        bool fabricationRoute = contract.Rules.FabricationTransitions
            .OfType<GenericActorRulesContract
                .BoundedChildFabricationTransition>()
            .Any(transition =>
                transition.SourceFormIds.Contains(formId));
        int? unlockTick = contract.LifecycleAssignments
            .FirstOrDefault(assignment =>
                assignment.TeamId == context.Self.ActorId.TeamId
                && assignment.UnitId == context.Self.ActorId.UnitId)
            ?.UnlockTick;
        return (
            attack?.ShotProgram.Enabled == true,
            anchorRoute,
            fabricationRoute,
            unlockTick);
    }
}
