using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// Contract-driven tactical building blocks, MIND-SHAPED. Every helper takes
/// the whole <see cref="MindContext"/> plus the one body it is acting for, and
/// writes the command onto that body rather than returning a decision — which
/// is the point of the profile: the mind decides once, for everybody, and the
/// commands are the record of what it decided.
///
/// <para><b>Traffic is solved here, not by you.</b> Every helper that moves a
/// body takes a <see cref="Claims"/> set and adds its destination to it, so the
/// second body to be commanded this tick will not walk into the first one's
/// tile. Under the per-life profile that was 500+ lines per bot of
/// agreement-without-a-channel machinery, and same-destination blocks were the
/// single largest source of wasted ticks. Under a mind it is a
/// <see cref="HashSet{T}"/>.</para>
///
/// <para>Keep or replace these as your doctrine develops. Strategy belongs in
/// Roles.cs and BOTNAME.cs.</para>
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

    /// <summary>
    /// Tiles already spoken for this tick — every destination another own body
    /// has been commanded into, plus anything you want kept clear. Create one
    /// per tick and pass it to every movement helper; that is the whole
    /// collision-avoidance system.
    /// </summary>
    internal sealed class Claims
    {
        private readonly HashSet<Position> _tiles = [];

        public IReadOnlySet<Position> Tiles => _tiles;

        public bool Reserve(Position tile) => _tiles.Add(tile);

        public bool IsClaimed(Position tile) => _tiles.Contains(tile);

        /// <summary>
        /// Seeds the set with every body that is standing still by default —
        /// own bodies included, because a body you have not commanded yet is
        /// still standing where it is.
        /// </summary>
        public static Claims ForTick(MindContext mind)
        {
            var claims = new Claims();
            foreach (MindBody body in mind.Bodies)
                claims.Reserve(body.Position);
            return claims;
        }

        /// <summary>
        /// Releases the tile a commanded body is vacating — but ONLY where the
        /// contract lets another body follow it in the same tick. Read the
        /// rule, do not assume it: under
        /// <c>collisions.followingVacatedActorAllowed = false</c> (which is
        /// what this game declares) a sibling that steps into a tile someone
        /// else is leaving is Blocked, so freeing the tile would hand your own
        /// army a wasted tick. Where the contract does allow it, holding the
        /// tile would cost a legal step instead — so the rule decides.
        /// </summary>
        public void Vacate(
            GenericActorResolvedMatchContract contract,
            Position tile)
        {
            if (contract.Rules.Collisions.FollowingVacatedActorAllowed)
                _tiles.Remove(tile);
        }
    }

    /// <summary>
    /// Fabricates into a ready slot, preferring one you name. A mind can decide
    /// a body's job before that body exists — see the build order in
    /// BOTNAME.cs — which is a strategic object the per-life profile could not
    /// express at all, because the Prime that decided it died before its child
    /// arrived.
    /// </summary>
    public static bool TryFabricate(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        int? preferredUnitId = null)
    {
        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Fabrication)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality? action = body.ActionLegalities
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
            return false;

        GenericActorActionArgument.UnitTarget target =
            targets.AllowedValues
                .OrderByDescending(value =>
                    preferredUnitId is int wanted && value.UnitId == wanted)
                .ThenBy(value => value.TeamId)
                .ThenBy(value => value.UnitId)
                .First();
        body.Command(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.UnitTargetArgument(target)],
            $"building {target.TeamId}:{target.UnitId}");
        return true;
    }

    /// <summary>
    /// Steps out of a hostile bolt's path when one is about to arrive, keeping
    /// the objective if this body is already holding it.
    /// </summary>
    public static bool TryDodge(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Claims claims)
    {
        GenericActorContext.ObservedProjectile[] hostile =
            mind.VisibleProjectiles
                ?.Where(projectile =>
                    projectile.OwnerTeamId != body.ActorId.TeamId)
                .OrderBy(projectile => projectile.ProjectileId)
                .ToArray()
            ?? [];
        if (!hostile.Any(projectile =>
                ReachesWithinAdvances(projectile, body.Position, 2)))
        {
            return false;
        }

        GenericActorActionLegality? move = AvailableAction(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (move is null || constraint is null)
            return false;

        HashSet<Position> blocked = Occupied(mind, hostile, claims, body);
        Position[] objectiveTiles = ActiveObjectiveTiles(contract, mind);
        bool holdingObjective = objectiveTiles.Contains(body.Position);
        Direction[] preference = OrderedDirections(contract, mind, body);
        Direction? selected = constraint.AllowedValues
            .Where(direction => Directions.Contains(direction))
            .Select(direction =>
            {
                (int dx, int dy) = direction.Vector();
                return (
                    Direction: direction,
                    Destination: body.Position.Offset(dx, dy));
            })
            .Where(candidate =>
                CanEnter(contract.Map, candidate.Destination, blocked)
                && !hostile.Any(projectile =>
                    ReachesWithinAdvances(
                        projectile,
                        candidate.Destination,
                        2)))
            .OrderByDescending(candidate =>
                !holdingObjective
                || objectiveTiles.Contains(candidate.Destination))
            .ThenBy(candidate =>
                DistanceToObjective(candidate.Destination, objectiveTiles))
            .ThenByDescending(candidate =>
                hostile.Min(projectile =>
                    candidate.Destination.ChebyshevDistance(
                        projectile.Position)))
            .ThenBy(candidate =>
                Array.IndexOf(preference, candidate.Direction))
            .Select(candidate => (Direction?)candidate.Direction)
            .FirstOrDefault();
        if (selected is not Direction direction)
            return false;

        (int stepX, int stepY) = direction.Vector();
        claims.Vacate(contract, body.Position);
        claims.Reserve(body.Position.Offset(stepX, stepY));
        body.Command(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            $"dodging toward {direction}");
        return true;
    }

    /// <summary>
    /// Fires at <paramref name="target"/> if this body's own geometry allows
    /// it, or at the best target it can reach when none is named.
    ///
    /// <para>Target CHOICE is the mind's job and belongs in your doctrine —
    /// picking one enemy and handing it to every gun is the whole of the old
    /// C2/C3 coordination grades, and under a mind it is one
    /// <c>OrderBy(...).First()</c> in Roles.cs. Solving the shot is per body,
    /// because geometry is per body, so it stays here.</para>
    /// </summary>
    public static bool TryShoot(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        GenericActorContext.ObservedEnemyState? target = null)
    {
        GenericActorRulesContract.Form? form = contract.Rules.Forms
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    body.FormId,
                    StringComparison.Ordinal));
        GenericActorRulesContract.AttackProfile? attack =
            form?.AttackProfileId is string attackProfileId
                ? contract.Rules.AttackProfiles.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Id,
                        attackProfileId,
                        StringComparison.Ordinal))
                : null;
        if (attack is null || mind.Enemies.IsEmpty)
            return false;

        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action =>
                action.Kind == GenericActorRulesContract.ActionKind.Attack)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        GenericActorActionLegality[] actions = body.ActionLegalities
            .Where(action =>
                action.Available && actionIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .ToArray();
        if (actions.Length == 0)
            return false;

        IEnumerable<GenericActorContext.ObservedEnemyState> candidates =
            target is not null
                ? [target]
                : mind.Enemies
                    .OrderBy(enemy => enemy.Health)
                    .ThenBy(enemy =>
                        body.Position.ChebyshevDistance(enemy.Position))
                    .ThenBy(enemy => enemy.ActorId);

        foreach (GenericActorContext.ObservedEnemyState enemy in candidates)
        {
            if (!TryRay(
                    body.Position,
                    enemy.Position,
                    out ProjectileHeading heading,
                    out int distance)
                || distance > attack.Projectile.MaxTravelTiles
                || !ClearRay(
                    contract.Map,
                    body.Position,
                    enemy.Position,
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
                    body.Command(
                        action.ActionId,
                        action.ActionCode,
                        [
                            new GenericActorActionArgument
                                .ProjectileHeadingArgument(heading),
                        ],
                        $"fire at {enemy.ActorId}");
                    return true;
                }

                int aimOffset = SignedHeadingDifference(
                    body.Facing.ToProjectileHeading(),
                    heading);
                if (aimOffset < attack.ShotProgram.MinInitialAimSteps
                    || aimOffset > attack.ShotProgram.MaxInitialAimSteps)
                {
                    continue;
                }
                if (aimOffset == 0
                    && (attack.ShotProgram.PayloadOptional
                        || !attack.ShotProgram.Enabled))
                {
                    body.Command(
                        action.ActionId,
                        action.ActionCode,
                        [],
                        $"straight fire at {enemy.ActorId}");
                    return true;
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
                body.Command(
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
                    $"aimed fire at {enemy.ActorId}");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// One step toward <paramref name="goals"/>, or the rotation that unlocks
    /// that step, reserving the destination against the rest of your army.
    ///
    /// <para>Every input is observed or chain-derived: the route starts where
    /// the body actually is. Do not replace that with a home-relative plan —
    /// contracts differ in where automatic arrivals land (see
    /// <see cref="ExpectedArrivalTiles"/>), so "spawn at home, walk to the
    /// front" is a plan only some of them honour.</para>
    /// </summary>
    public static bool TryStepToward(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IReadOnlyCollection<Position> goals,
        Claims claims,
        string why = "advancing")
    {
        if (goals.Count == 0 || goals.Contains(body.Position))
            return false;

        GenericActorActionLegality? move = AvailableAction(
            contract,
            body,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            constraint = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .SingleOrDefault();
        if (move is null || constraint is null)
            return false;

        // Hostile bolts always block or hit. Whether ALLIED bolts do is the
        // contract's allied-contact policy — read it rather than assume
        // pass-through, so this survives a future collision arm.
        bool alliedBoltsPass = contract.Rules.Collisions
            .AlliedProjectileContact
            .Contains("pass-through", StringComparison.Ordinal);
        HashSet<Position> blocked = Occupied(
            mind,
            (mind.VisibleProjectiles ?? [])
                .Where(projectile =>
                    !alliedBoltsPass
                    || projectile.OwnerTeamId != body.ActorId.TeamId),
            claims,
            body);
        // Never step into a lane a bolt is about to cross. This is the half of
        // evasion a router forgets: dodging when threatened is useless if the
        // next tick's route walks straight back in.
        blocked.UnionWith(Hazards(mind, body));
        blocked.Remove(body.Position);
        if (body.PreviousActionResolution
                is { Outcome: GenericActorActionResolution
                    .ActionOutcome.Blocked } previous
            && previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.DirectionArgument>()
                .SingleOrDefault() is { } blockedDirection)
        {
            (int dx, int dy) = blockedDirection.Value.Vector();
            blocked.Add(body.Position.Offset(dx, dy));
        }

        // The legality mask says what is legal THIS tick, not where routes may
        // go: under a facing-coupled arm it offers only the current facing, and
        // seeding the search from it prunes every turn-requiring route — the
        // body freezes instead of turning. Plan on map geometry; when the mask
        // refuses the planned step, spend a rotation to unlock it next tick.
        Direction? step = FindFirstStep(
            contract.Map,
            body.Position,
            goals.ToHashSet(),
            blocked,
            Directions.ToHashSet(),
            OrderedDirections(contract, mind, body));
        if (step is not Direction direction)
            return false;

        if (!constraint.AllowedValues.Contains(direction))
        {
            GenericActorActionLegality? rotate = AvailableAction(
                contract,
                body,
                GenericActorRulesContract.ActionKind.Rotation);
            GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
                heading = rotate?.Constraints
                    .OfType<GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint>()
                    .SingleOrDefault();
            if (rotate is null
                || heading is null
                || !heading.AllowedValues.Contains(direction)
                || body.Facing == direction)
            {
                return false;
            }
            body.Command(
                rotate.ActionId,
                rotate.ActionCode,
                [
                    new GenericActorActionArgument.DirectionArgument(
                        direction),
                ],
                $"turning {direction} to unlock the next step");
            return true;
        }

        (int moveX, int moveY) = direction.Vector();
        claims.Vacate(contract, body.Position);
        claims.Reserve(body.Position.Offset(moveX, moveY));
        body.Command(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            $"{why} via {direction}");
        return true;
    }

    /// <summary>
    /// Tiles of the objective the match is currently fought over, taken from
    /// the observed active index rather than any fixed position.
    /// </summary>
    public static Position[] ActiveObjectiveTiles(
        GenericActorResolvedMatchContract contract,
        MindContext mind) =>
        mind.Mode is GenericActorContext.ModeObservationState.Frontline mode
            ? ObjectiveTiles(contract, mode.ActivePositionIndex)
            : [];

    /// <summary>
    /// Tiles of one objective in the ordered chain. Empty for an index outside
    /// the chain, which is the honest answer for "one step past the end".
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
    /// Tiles of the objective one step BEHIND a team's advance — the ground it
    /// already took. Derived from the chain and the team's declared index
    /// delta, never from a spawn, so it moves with the front.
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
        GenericActorResolvedMatchContract.FrontlineTeamAdvance? advance =
            binding.TeamAdvances.FirstOrDefault(entry =>
                entry.TeamId == teamId);
        return advance is null
            ? []
            : ObjectiveTiles(
                contract,
                activePositionIndex - advance.ObjectiveIndexDelta);
    }

    /// <summary>
    /// Whether this contract places automatic returns and activations by the
    /// objective chain instead of by the slot's spawn anchor. A mind plans the
    /// return, so this is the difference between a body that rejoins the fight
    /// and a body that has to walk the length of the map first.
    /// </summary>
    public static bool ArrivalsRallyForward(
        GenericActorResolvedMatchContract contract) =>
        contract.Rules.Lifecycle.AutomaticReturnPlacement.Contains(
            "own-side-chain-adjacent-objective",
            StringComparison.Ordinal);

    /// <summary>
    /// Where a slot can expect its next automatic arrival. This is the
    /// contract's INTENT — a chain-deriving host still falls back to the anchor
    /// when the derived region offers no free tile. Use it to price a death
    /// before it happens; use the observed position once it has happened.
    /// </summary>
    public static Position[] ExpectedArrivalTiles(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        int teamId,
        int unitId)
    {
        int activeIndex =
            mind.Mode is GenericActorContext.ModeObservationState.Frontline m
                ? m.ActivePositionIndex
                : -1;
        Position[] rally = ArrivalsRallyForward(contract)
            ? OwnSideObjectiveTiles(contract, teamId, activeIndex)
            : [];
        if (rally.Length > 0)
            return rally;

        string? spawnId = contract.LifecycleAssignments
            .FirstOrDefault(assignment =>
                assignment.TeamId == teamId && assignment.UnitId == unitId)
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

    /// <summary>The objective weight a form carries, zero when it carries none.</summary>
    public static int ObjectiveWeight(
        GenericActorResolvedMatchContract contract,
        string formId) =>
        contract.Rules.Forms
            .FirstOrDefault(form =>
                string.Equals(form.Id, formId, StringComparison.Ordinal))
            ?.ObjectiveWeight
        ?? 0;

    /// <summary>
    /// What is standing on the active objective right now, SPLIT the way the
    /// capture channel splits it.
    ///
    /// <para>The old helper returned one number per side, which was the wrong
    /// shape the moment the channel shipped: under it, a team's CLAIM weight
    /// counts only bodies that did not change tile this tick, while its DENIAL
    /// weight counts all of them. Those are different numbers driving different
    /// decisions — the claim decides whether you are gaining, the denial
    /// decides whether they are — and summing them hides exactly the choice the
    /// channel exists to make you make.</para>
    ///
    /// <para><b>Own stillness is exact; enemy stillness is not.</b> Your mind
    /// knows which of its own bodies moved, because
    /// <see cref="MindBody.MovedLastTick"/> is published. No such fact exists
    /// for enemies, so <see cref="Presence.EnemyClaimWeight"/> is an upper
    /// bound computed as if every visible enemy body were still, and enemy
    /// weight in general counts only what your team currently SEES.</para>
    /// </summary>
    public static Presence ObjectivePresence(
        GenericActorResolvedMatchContract contract,
        MindContext mind)
    {
        HashSet<Position> tiles =
            ActiveObjectiveTiles(contract, mind).ToHashSet();
        if (tiles.Count == 0)
            return new Presence(0, 0, 0, 0, []);

        MindBody[] onPoint = mind.Bodies
            .Where(body => tiles.Contains(body.Position))
            .OrderBy(body => body.UnitId)
            .ToArray();
        int ownDenial = onPoint.Sum(body =>
            ObjectiveWeight(contract, body.FormId));
        int ownClaim = onPoint
            .Where(body => !body.MovedLastTick)
            .Sum(body => ObjectiveWeight(contract, body.FormId));
        int enemyDenial = mind.Enemies
            .Where(enemy => tiles.Contains(enemy.Position))
            .Sum(enemy => ObjectiveWeight(contract, enemy.FormId));
        return new Presence(
            ownClaim,
            ownDenial,
            enemyDenial,
            enemyDenial,
            [.. onPoint]);
    }

    /// <param name="OwnClaimWeight">
    /// Own objective weight that did NOT change tile this tick — what actually
    /// builds a claim under the channel.
    /// </param>
    /// <param name="OwnDenialWeight">
    /// All own objective weight on the point, moving or not — what denies the
    /// opposition.
    /// </param>
    /// <param name="EnemyClaimWeight">
    /// Upper bound: enemy stillness is not published, so this assumes every
    /// visible enemy body held its tile.
    /// </param>
    /// <param name="EnemyDenialWeight">All visible enemy weight on the point.</param>
    /// <param name="OwnBodiesOnPoint">Own bodies standing on it, by unit.</param>
    public sealed record Presence(
        int OwnClaimWeight,
        int OwnDenialWeight,
        int EnemyClaimWeight,
        int EnemyDenialWeight,
        ImmutableArray<MindBody> OwnBodiesOnPoint);

    /// <summary>
    /// The capture policy VALUES this contract plays by, so a doctrine prices
    /// pushes from the contract instead of from habit.
    /// Null when the mode is not objective-based.
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
            ScalesWithSurplusWeight(capture.ControlPolicy),
            IsChannel(capture.ControlPolicy),
            capture.DecayClock.Contains(
                "enemy-sole-erosion-only",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether a second body on the point adds pressure, per the DECLARED
    /// control policy.
    ///
    /// <para>This used to be a substring test for one policy's name, which
    /// answered <c>false</c> for the capture channel — and the channel scales
    /// gain by weight difference harder than anything else in the game. A bot
    /// that trusted it priced every push as "one body nulls any number of
    /// opposing bodies", which is the opposite of the truth on the arm the
    /// scaffold shipped alongside.</para>
    ///
    /// <para>An unrecognized policy THROWS rather than quietly answering
    /// <c>false</c>. A new policy is a new game, and finding out by losing is
    /// worse than finding out by crashing on the first tick of your own test
    /// run.</para>
    /// </summary>
    public static bool ScalesWithSurplusWeight(string controlPolicy) =>
        controlPolicy switch
        {
            "binary-positive-weight-per-team-no-stacking-non-sole-applies-"
                + "configured-decay-opposition-erodes-to-neutral" => false,
            "net-positive-objective-weight-difference-scales-gain-non-"
                + "positive-applies-configured-decay-opposition-erodes-to-"
                + "neutral" => true,
            "stationary-claim-weight-versus-total-denial-weight-scales-gain-"
                + "capped-opposition-erodes-at-multiple-then-builds" => true,
            _ => throw new InvalidOperationException(
                $"Unknown capture control policy '{controlPolicy}'. Read the "
                + "contract and decide what it means for your doctrine rather "
                + "than defaulting to the old one."),
        };

    /// <summary>
    /// True when this contract runs the capture CHANNEL: gain counts only
    /// bodies that held their tile, denial counts all of them, and hostile
    /// damage to a controlling body standing on the objective reverts the
    /// current run. Screening the channeler is the intended play, and this is
    /// how you find out that it is.
    /// </summary>
    public static bool IsChannel(string controlPolicy) =>
        string.Equals(
            controlPolicy,
            "stationary-claim-weight-versus-total-denial-weight-scales-gain-"
                + "capped-opposition-erodes-at-multiple-then-builds",
            StringComparison.Ordinal);

    /// <param name="Threshold">Progress required to complete one capture.</param>
    /// <param name="GainPerSoleTeamTick">Base progress per tick of control.</param>
    /// <param name="DecayAmount">Progress removed at each decay application.</param>
    /// <param name="DecayIntervalTicks">Ticks between decay applications.</param>
    /// <param name="RedeployPauseTicks">
    /// Ticks after an advance during which control cannot resume.
    /// </param>
    /// <param name="HoldTicks">
    /// How long a completed advance is protected, or null when captures never
    /// lock. A capture completed inside another team's live hold is SPENT.
    /// </param>
    /// <param name="SurplusWeightScalesGain">
    /// True when net objective weight scales capture pressure.
    /// </param>
    /// <param name="StillnessGated">
    /// True on the capture channel: only bodies that did not change tile build
    /// a claim, and damage to a controller on the point reverts the run.
    /// </param>
    /// <param name="OnlyEnemySolePresenceDecays">
    /// True when empty and contested ticks preserve a claim.
    /// </param>
    public sealed record CaptureRules(
        int Threshold,
        int GainPerSoleTeamTick,
        int DecayAmount,
        int DecayIntervalTicks,
        int RedeployPauseTicks,
        int? HoldTicks,
        bool SurplusWeightScalesGain,
        bool StillnessGated,
        bool OnlyEnemySolePresenceDecays);

    /// <summary>
    /// Which team's advance the territory ratchet is protecting right now, and
    /// the tick that protection lifts — READ from the observation, not inferred.
    /// Null when no hold binds this tick.
    /// </summary>
    public static Hold? LiveHold(MindContext mind, int teamId) =>
        mind.Mode
            is GenericActorContext.ModeObservationState.Frontline
            {
                HoldOwnerTeamId: int owner,
                HoldEndsAtTick: int endsAt,
            }
            ? new Hold(owner, owner == teamId, endsAt, endsAt - mind.Tick)
            : null;

    /// <param name="OwnerTeamId">Team whose advance is protected.</param>
    /// <param name="Mine">True when your team owns the hold.</param>
    /// <param name="EndsAtTick">First tick the hold no longer binds.</param>
    /// <param name="RemainingTicks">Ticks it still has to run.</param>
    public sealed record Hold(
        int OwnerTeamId,
        bool Mine,
        int EndsAtTick,
        int RemainingTicks);

    /// <summary>
    /// This body's chassis, READ from the observation.
    ///
    /// <para>It used to be recovered by splitting form IDs on <c>-</c> and
    /// hoping the convention held. It does not have to: <c>classId</c> is
    /// published per body, and under a mixed composition it is published
    /// per body because bodies in one army genuinely differ. Ask the body.</para>
    /// </summary>
    public static string? ClassOf(MindBody body) => body.ClassId;

    /// <summary>
    /// "Should I eat this?" — what one hostile bolt costs and how long you have
    /// to decide, read from the bolt itself. Null when it cannot reach.
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

        int advances = (distance + projectile.TilesPerAdvance - 1)
            / projectile.TilesPerAdvance;
        int ticks = projectile.TicksUntilAdvance
            + (advances - 1) * projectile.TicksPerAdvance;
        return new Incoming(ticks, projectile.DamagePerHit, distance);
    }

    /// <param name="TicksUntilArrival">Ticks until the bolt arrives.</param>
    /// <param name="Damage">Health one contact removes.</param>
    /// <param name="Tiles">Tiles between the bolt and the target.</param>
    public sealed record Incoming(
        int TicksUntilArrival,
        int Damage,
        int Tiles);

    /// <summary>
    /// A team's advance direction as a map heading, derived from the ordered
    /// objective regions and the team's declared index direction. Null when the
    /// mode declares no advance.
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
        GenericActorResolvedMatchContract.FrontlineTeamAdvance? advance =
            binding.TeamAdvances.FirstOrDefault(entry =>
                entry.TeamId == teamId);
        if (advance is null)
            return null;

        (double X, double Y)? Centroid(string regionId)
        {
            GenericActorMapContract.Region? region =
                contract.Map.Regions.FirstOrDefault(entry =>
                    string.Equals(
                        entry.RegionId,
                        regionId,
                        StringComparison.Ordinal));
            if (region is null || region.Tiles.IsEmpty)
                return null;
            return (
                region.Tiles.Average(tile => (double)tile.X),
                region.Tiles.Average(tile => (double)tile.Y));
        }

        (double X, double Y)? first =
            Centroid(binding.OrderedObjectiveRegionIds[0]);
        (double X, double Y)? last =
            Centroid(binding.OrderedObjectiveRegionIds[^1]);
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
    /// A mirror-fair direction preference: advance first, retreat last, the two
    /// perpendiculars ordered by a deterministic stream. An absolute order
    /// (always prefer East) gives the east-advancing team a systematic edge on
    /// a mirror-symmetric map, measured as a 40-of-40 side sweep.
    ///
    /// <para>It draws from <see cref="MindContext.Random"/>, the mind's own
    /// private stream. Under the per-life profile this had to use the TEAM
    /// stream or nine bodies would silently disagree about the same tie-break —
    /// that was the whole reason TeamRandom exists. A mind cannot disagree with
    /// itself, so the private stream is correct here and TeamRandom is what you
    /// would reach for to coordinate with an ALLIED mind.</para>
    /// </summary>
    public static Direction[] OrderedDirections(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        IBotRandom? random = null)
    {
        Direction forward =
            AdvanceDirection(contract, body.ActorId.TeamId) ?? body.Facing;
        Direction backward = Opposite(forward);
        Direction[] laterals = Directions
            .Where(direction =>
                direction != forward && direction != backward)
            .ToArray();
        if (laterals.Length == 2 && (random ?? mind.Random).NextBool())
            (laterals[0], laterals[1]) = (laterals[1], laterals[0]);
        return [forward, .. laterals, backward];
    }

    private static GenericActorActionLegality? AvailableAction(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorRulesContract.ActionKind kind)
    {
        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return body.ActionLegalities
            .Where(action =>
                action.Available && actionIds.Contains(action.ActionId))
            .OrderBy(action => action.ActionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static HashSet<Position> Occupied(
        MindContext mind,
        IEnumerable<GenericActorContext.ObservedProjectile> projectiles,
        Claims claims,
        MindBody moving)
    {
        var blocked = new HashSet<Position>(claims.Tiles);
        blocked.Remove(moving.Position);
        foreach (GenericActorContext.ObservedAllyState ally in mind.Allies)
            blocked.Add(ally.Position);
        foreach (GenericActorContext.ObservedEnemyState enemy in mind.Enemies)
            blocked.Add(enemy.Position);
        foreach (GenericActorContext.ObservedProjectile projectile
                 in projectiles)
        {
            blocked.Add(projectile.Position);
        }
        // A tile held for a future arrival REFUSES entry, and the refusal is
        // published rather than guessable: a visible tile carries the claim
        // that makes it unavailable. Walking into one is not a near miss, it
        // is a blocked move every single tick, which is how a body ends up
        // oscillating in front of an invisible wall.
        foreach (GenericActorContext.ObservedTile tile in mind.VisibleTiles)
        {
            if (tile.SpawnReservation is not null)
                blocked.Add(tile.Position);
        }
        return blocked;
    }

    /// <summary>
    /// Tiles a hostile bolt reaches within its next two advances. Stepping into
    /// one is the same mistake as standing in one, and a router that only
    /// avoids walls will happily walk a body into a lane it was about to be
    /// shot down.
    /// </summary>
    private static HashSet<Position> Hazards(
        MindContext mind,
        MindBody body)
    {
        var hazards = new HashSet<Position>();
        foreach (GenericActorContext.ObservedProjectile projectile
                 in mind.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == body.ActorId.TeamId)
                continue;
            foreach (Direction direction in Directions)
            {
                (int dx, int dy) = direction.Vector();
                Position candidate = body.Position.Offset(dx, dy);
                if (ReachesWithinAdvances(projectile, candidate, 2))
                    hazards.Add(candidate);
            }
            if (ReachesWithinAdvances(projectile, body.Position, 2))
                hazards.Add(body.Position);
        }
        return hazards;
    }

    /// <summary>
    /// The first step of a shortest route to <paramref name="goals"/>.
    /// <paramref name="blockedNow"/> holds this tick's transient occupants and
    /// applies to the FIRST step only, because only the first step executes:
    /// the route is replanned next tick, by which time those tiles have moved.
    /// Walls block at every depth; they are the only thing that does.
    /// </summary>
    private static Direction? FindFirstStep(
        GenericActorMapContract map,
        Position start,
        IReadOnlySet<Position> goals,
        IReadOnlySet<Position> blockedNow,
        IReadOnlySet<Direction> allowedFirstSteps,
        Direction[]? searchOrder = null)
    {
        Direction[] order = searchOrder ?? Directions;
        var visited = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in order)
        {
            if (!allowedFirstSteps.Contains(direction))
                continue;
            Position next = Offset(start, direction);
            if (!CanEnter(map, next, blockedNow) || !visited.Add(next))
                continue;
            if (goals.Contains(next))
                return direction;
            queue.Enqueue((next, direction));
        }

        while (queue.Count > 0)
        {
            (Position Position, Direction First) current = queue.Dequeue();
            foreach (Direction direction in order)
            {
                Position next = Offset(current.Position, direction);
                if (!CanEnter(map, next, NoPositions) || !visited.Add(next))
                    continue;
                if (goals.Contains(next))
                    return current.First;
                queue.Enqueue((next, current.First));
            }
        }
        return null;
    }

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
                && (!CanEnter(map, cursor.Offset(stepX, 0), NoPositions)
                    || !CanEnter(map, cursor.Offset(0, stepY), NoPositions)))
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

    private static Position Offset(Position position, Direction direction)
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

    private static Direction Opposite(Direction direction) =>
        direction switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            _ => Direction.East,
        };
}
