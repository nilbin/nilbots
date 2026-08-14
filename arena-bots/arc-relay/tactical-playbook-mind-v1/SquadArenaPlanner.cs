using BotArena.Sdk;

/// <summary>
/// One already-selected physical intent. Orders owns the objective; movement
/// planning may only realize it in shared space.
/// </summary>
internal sealed record SquadMovementIntent(
    MindBody Body,
    string Key,
    string GroupKey,
    IReadOnlyCollection<Position> Goals,
    Position? PreferredStep,
    TacticalMovementPriority Priority,
    bool Pinned,
    bool PrefersHold,
    bool CanStage,
    int? LeaderUnitId,
    string Trace);

/// <summary>
/// Enhanced Priority Inheritance with Backtracking (EPIBT) for Arc Relay's
/// eight-body online movement problem. This is the published length-three
/// multi-action algorithm: inherited operations initialize every timestep,
/// a colliding lower-priority body inherits the caller's priority, bounded
/// revisits let already planned bodies reconsider, and failure backtracks.
///
/// <para>The sheet/compiler still owns orders, goals and posture. The small
/// Hungarian pass below only gives bodies sharing one authored objective
/// distinct contribution tiles. EPIBT alone owns physical collision planning;
/// there is no WHCA* reservation window, sequential greedy fallback, bespoke
/// clearance mover, or post-plan shove.</para>
/// </summary>
internal sealed class SquadArenaPlanner : IArenaStepper
{
    internal const int OperationLength = 3;
    internal const int RevisitLimit = 10;
    internal const int DeniedStepAvoidanceTicks = 4;
    private const long Impossible = 1_000_000_000;

    private readonly Dictionary<int, Decision> _decisions = [];
    private readonly Dictionary<int, PersistentState> _states = [];
    private readonly Dictionary<int, DeniedStep> _deniedSteps = [];
    private GenericActorResolvedMatchContract? _contract;
    private MindContext? _mind;
    private bool _mirrored;

    public void BeginTick(
        GenericActorResolvedMatchContract contract,
        MindContext mind)
    {
        _contract = contract;
        _mind = mind;
        _mirrored = ArenaBasics.MirroredFrame(contract, mind);
        _decisions.Clear();
        HashSet<int> live = mind.Bodies.Select(body => body.UnitId).ToHashSet();
        foreach (int stale in _states.Keys.Where(unit => !live.Contains(unit))
                     .ToArray())
            _states.Remove(stale);
        foreach (int stale in _deniedSteps.Keys
                     .Where(unit => !live.Contains(unit)).ToArray())
            _deniedSteps.Remove(stale);
        RememberDeniedSteps(mind);
    }

    public void PlanTeam(
        IReadOnlyCollection<SquadMovementIntent> source,
        ArenaBasics.Claims claims)
    {
        GenericActorResolvedMatchContract contract = _contract
            ?? throw new InvalidOperationException("BeginTick was not called.");
        MindContext mind = _mind
            ?? throw new InvalidOperationException("BeginTick was not called.");
        SquadMovementIntent[] intents = source
            .OrderBy(intent => intent.Body.UnitId)
            .ToArray();
        Dictionary<int, SquadMovementIntent> byUnit = intents.ToDictionary(
            intent => intent.Body.UnitId);
        Dictionary<int, Position> assignments = AssignContributions(
            contract, intents);
        Dictionary<int, Position[]> courses = intents.ToDictionary(
            intent => intent.Body.UnitId,
            intent => ResolveCourse(
                contract, mind, intent, assignments[intent.Body.UnitId]));

        var inherited = new Dictionary<int, Operation>();
        var candidates = new Dictionary<int, Operation[]>();
        foreach (SquadMovementIntent intent in intents)
        {
            inherited[intent.Body.UnitId] = InheritedOperation(
                contract,
                mind,
                intent,
                assignments[intent.Body.UnitId]);
            // An EPIBT revisit changes only the collision set and inherited
            // priority. This body's map, legality, authored goal, assignment
            // and course are all frozen for the tick, so regenerating the
            // same operation set inside every recursive displacement wastes
            // sandbox fuel without changing a single candidate or tie-break.
            candidates[intent.Body.UnitId] = GenerateOperations(
                contract,
                mind,
                claims,
                intent,
                assignments[intent.Body.UnitId],
                courses[intent.Body.UnitId]);
        }
        var selected = inherited.ToDictionary(pair => pair.Key, pair => pair.Value);
        var visits = intents.ToDictionary(intent => intent.Body.UnitId, _ => 0);
        var branch = new HashSet<int>();

        // EPIBT calls smaller p "higher priority". The authored/debt rank is
        // the product precedence; static distance is only a stable tie-break.
        int[] order = PriorityOrder(contract, intents, assignments);
        Dictionary<int, int> priority = order
            .Select((unit, index) => (unit, index))
            .ToDictionary(value => value.unit, value => value.index);

        foreach (int unit in order)
        {
            if (visits[unit] != 0)
                continue;
            selected.Remove(unit);
            if (!SelectOperation(unit, priority[unit]))
                selected[unit] = inherited[unit];
        }

        // Recursive selection checks cycles as each candidate lands, but a
        // top-level failure historically restored its inherited operation
        // after that check. Several otherwise unrelated sheets therefore
        // published a one-tick three-body rotation that the engine correctly
        // refused. Never emit a joint first step the engine cannot execute.
        // Waiting the whole squad for this one tick is deliberately blunt but
        // deterministic; it clears the inherited horizon, then the next tick
        // plans afresh instead of preserving an impossible rotation.
        bool jointSafetyFallback = !FirstStepIsExecutable(selected.Values);
        if (jointSafetyFallback)
        {
            // Without a changed constraint the deterministic planner would
            // derive the same impossible rotation again next tick. Deny the
            // lowest-priority moving body's requested tile for the same short
            // physical-memory window used after an engine rejection. This
            // preserves every authored goal and gives EPIBT a concrete reason
            // to choose another course on the re-plan.
            KeyValuePair<int, Operation>? breaker = selected
                .Where(pair => pair.Value.Actions[0].Kind
                    == MicroActionKind.Move)
                .OrderByDescending(pair => priority[pair.Key])
                .ThenByDescending(pair => pair.Key)
                .Cast<KeyValuePair<int, Operation>?>()
                .FirstOrDefault();
            if (breaker is { } blocked
                && blocked.Value.Actions[0].Destination is Position tile)
            {
                _deniedSteps[blocked.Key] = new DeniedStep(
                    byUnit[blocked.Key].Body.ActorId,
                    tile,
                    mind.Tick + DeniedStepAvoidanceTicks);
            }
            foreach (SquadMovementIntent intent in intents)
                selected[intent.Body.UnitId] = WaitOperation(intent.Body);
        }

        foreach (SquadMovementIntent intent in intents)
        {
            Operation operation = selected[intent.Body.UnitId];
            bool physicallySatisfied = intent.PrefersHold
                || intent.Body.Position == assignments[intent.Body.UnitId];
            MicroAction first = operation.Actions[0];
            Position? tile = first.Kind switch
            {
                MicroActionKind.Move => first.Destination,
                MicroActionKind.Rotate => RotationRequestTile(
                    intent.Body.Position, first.Facing),
                _ => null,
            };
            string action = first.Kind switch
            {
                MicroActionKind.Move => "move",
                MicroActionKind.Rotate => "turn",
                _ when physicallySatisfied => "hold",
                _ => "wait",
            };
            string inheritedTrace = operation.Inherited ? ":inherited" : "";
            string reason = $"epibt-{action}:p{priority[intent.Body.UnitId]}"
                + $":v{visits[intent.Body.UnitId]}{inheritedTrace}"
                + $":op-{operation.ActionSequence}"
                + $":goal-{assignments[intent.Body.UnitId].X}"
                + $"-{assignments[intent.Body.UnitId].Y}"
                + (tile is Position next
                    ? $":next-{next.X}-{next.Y}"
                    : $":{intent.Trace}");
            if (ActiveDeniedStep(intent.Body) is DeniedStep denied)
            {
                reason += $":avoid-denied-{denied.Tile.X}-{denied.Tile.Y}"
                    + $"@{denied.UntilTick}";
            }
            if (jointSafetyFallback)
                reason += ":joint-plan-safety-fallback";
            // A facing-locked resting body may need to rotate before its
            // inherited operation vacates the tile. That preparation turn is
            // part of yielding too; suppressing it would invalidate the
            // operation and recreate the blocker on every tick.
            bool yielding = physicallySatisfied
                && first.Kind is MicroActionKind.Move
                    or MicroActionKind.Rotate
                && operation.Actions.Any(candidate =>
                    candidate.Kind == MicroActionKind.Move);
            _decisions[intent.Body.UnitId] = new Decision(
                tile,
                yielding ? reason + ":yield-resting-body" : reason,
                yielding,
                physicallySatisfied);
            _states[intent.Body.UnitId] = new PersistentState(
                intent.Body.ActorId,
                intent.Key,
                assignments[intent.Body.UnitId],
                operation,
                courses[intent.Body.UnitId]);
        }
        return;

        bool SelectOperation(int unit, int inheritedPriority)
        {
            SquadMovementIntent intent = byUnit[unit];
            visits[unit]++;
            branch.Add(unit);
            selected.Remove(unit);

            foreach (Operation candidate in candidates[unit])
            {
                int[] conflicts = selected
                    .Where(pair => OperationsCollide(candidate, pair.Value))
                    .Select(pair => pair.Key)
                    .Order()
                    .ToArray();
                if (conflicts.Length == 0)
                {
                    selected[unit] = candidate;
                    // The engine permits a following chain only when it ends
                    // in a tile that is actually vacated. Pairwise vertex and
                    // swap checks cannot see a three-body rotation, so mirror
                    // that joint rule here before publishing the operation.
                    if (!OperationsHaveAcyclicMovement(selected
                            .Where(pair => visits[pair.Key] > 0)
                            .Select(pair => pair.Value)))
                    {
                        selected.Remove(unit);
                        continue;
                    }
                    branch.Remove(unit);
                    return true;
                }
                // Published EPIBT skips an operation that simultaneously
                // collides with more than one agent; recursion would have no
                // unambiguous priority-inheritance edge.
                if (conflicts.Length > 1)
                    continue;
                int other = conflicts[0];
                if (branch.Contains(other)
                    || visits[other] >= RevisitLimit
                    || priority[other] <= inheritedPriority)
                {
                    continue;
                }

                Operation displaced = selected[other];
                selected.Remove(other);
                selected[unit] = candidate;
                if (SelectOperation(other, inheritedPriority))
                {
                    branch.Remove(unit);
                    return true;
                }
                selected.Remove(unit);
                selected[other] = displaced;
            }

            // EPIBT's failure action is always the shifted operation from the
            // previous tick, not whichever provisional operation happened to
            // occupy this agent when a higher-priority branch revisited it.
            selected[unit] = inherited[unit];
            branch.Remove(unit);
            return false;
        }
    }

    public Position? Step(StepRequest request) =>
        _decisions.GetValueOrDefault(request.Body.UnitId)?.Tile;

    public string Trace(int unitId) =>
        _decisions.GetValueOrDefault(unitId)?.Trace
        ?? "epibt-wait:no-physical-intent";

    public bool TryGetRestingYield(
        int unitId,
        out Position destination,
        out string trace)
    {
        Decision? decision = _decisions.GetValueOrDefault(unitId);
        if (decision is { Yielding: true, Tile: Position tile })
        {
            destination = tile;
            trace = decision.Trace;
            return true;
        }
        destination = default;
        trace = "epibt-hold:no-yield";
        return false;
    }

    /// <summary>
    /// True when the joint contribution assignment deliberately placed this
    /// body on its current tile. This is not a blocked move: another member
    /// of the same authored objective may own the exact destination while
    /// this body contributes from the surrounding ring. Keep that distinction
    /// visible to the executor and its trace.
    /// </summary>
    public bool IsAtContribution(int unitId) =>
        _decisions.GetValueOrDefault(unitId)?.PhysicallySatisfied == true;

    public void NoteCommitted(MindBody body, Position destination)
    {
        // The joint operation set is complete before command emission.
    }

    private Operation InheritedOperation(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        SquadMovementIntent intent,
        Position assignment)
    {
        Operation wait = WaitOperation(intent.Body);
        // Rooting is authoritative for this tick. A shifted operation from a
        // prior movable tick must never survive into a strike, skill, handoff,
        // drop or invest commitment merely because the authored rung key did
        // not change.
        if (intent.Pinned)
            return wait;
        if (!_states.TryGetValue(
                intent.Body.UnitId, out PersistentState? prior)
            || prior.Actor != intent.Body.ActorId
            || !string.Equals(prior.Key, intent.Key, StringComparison.Ordinal)
            || prior.Assignment != assignment
            || prior.Operation.States[0] != new PlannerState(
                intent.Body.Position, intent.Body.Facing))
        {
            return wait;
        }

        MicroAction[] shifted =
        [
            prior.Operation.Actions[1],
            prior.Operation.Actions[2],
            MicroAction.Wait,
        ];
        // A shifted operation was legal when its source horizon was built,
        // but its new first action still has to be available on this tick.
        // Movement/signature cadence can change between ticks. Publishing an
        // inherited Move while movement is unavailable makes the executor
        // submit Wait under an epibt-move trace, which is both false telemetry
        // and an operation EPIBT could never physically execute.
        if (!ActionAvailableNow(contract, intent.Body, shifted[0]))
            return wait;
        return BuildOperation(
                contract,
                intent.Body,
                shifted,
                DurableObstaclesExceptGoal(
                    contract, mind, intent.Body, assignment),
                inherited: true)
            ?? wait;
    }

    private Operation[] GenerateOperations(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        ArenaBasics.Claims claims,
        SquadMovementIntent intent,
        Position assignment,
        Position[] course)
    {
        if (intent.Pinned)
            return [WaitOperation(intent.Body)];

        HashSet<Position> durable = DurableObstacles(
            contract, mind, intent.Body, claims);
        if (IsCarrierReturn(intent))
            durable.UnionWith(CarrierReturnSpawnTiles(
                contract.LifecycleAssignments,
                contract.Map.SpawnAnchors,
                intent.Body.ActorId.TeamId));
        // Reactor bank/deployment tiles are persistent route obstacles for
        // unrelated traffic. An order that explicitly names one must still
        // enter it, so only this body's assigned destination overrides that
        // obstacle; reserved tiles en route remain blocked.
        durable.Remove(assignment);
        durable.Remove(intent.Body.Position);
        // A simultaneous hostile move can reject a step even though the
        // destination was visibly empty when this mind planned. The generic
        // one-tick BlockedNow memory is insufficient for carried Cores because
        // their mandatory relocation hold overwrites PreviousActionResolution
        // before the next movable beat. Keep the denied tile as a short-lived
        // obstacle so the next operation actually tries another way around.
        // This is physical collision memory only: it does not change the
        // authored goal, assignment, route commitment, or route direction.
        DeniedStep? activeDenied = ActiveDeniedStep(intent.Body);
        if (activeDenied is DeniedStep denied)
            durable.Add(denied.Tile);
        int[]? distanceField = null;
        bool courseStartsWithNecessaryDetour = course.Length > 1
            && GoalDistance(contract.Map, course[1], assignment)
                >= GoalDistance(
                    contract.Map, intent.Body.Position, assignment);
        if (IsCarrierReturn(intent))
        {
            HashSet<Position> distanceObstacles =
                ArenaBasics.PersistentRouteObstacles(
                    contract, mind, intent.Body.ActorId.TeamId);
            distanceObstacles.UnionWith(CarrierReturnSpawnTiles(
                contract.LifecycleAssignments,
                contract.Map.SpawnAnchors,
                intent.Body.ActorId.TeamId));
            distanceObstacles.Remove(assignment);
            // StaticRouteDistance historically removes the query's start.
            // Every generated state is already legal against durable, so only
            // the body's tick-start tile needs the same explicit exemption.
            distanceObstacles.Remove(intent.Body.Position);
            // Score a carried-Core detour against the same temporary obstacle
            // that makes the denied step illegal. Leaving this tile out of the
            // reverse field makes every legal detour initially look farther
            // than waiting on the nominal route; when the four-tick memory
            // expires the carrier retries that occupied square and can repeat
            // forever. One obstacle set must answer both "may I step there?"
            // and "which legal course makes progress?".
            if (activeDenied is DeniedStep carrierDenied)
                distanceObstacles.Add(carrierDenied.Tile);
            distanceField = ArenaBasics.StaticRouteDistanceField(
                contract,
                intent.Body,
                assignment,
                distanceObstacles);
        }
        else if (courseStartsWithNecessaryDetour || activeDenied is not null)
        {
            // Chebyshev progress cannot recognize the first step of a real
            // route around a persistent obstacle: sideways and briefly-away
            // steps look no better than rotating in place. Use the route's
            // own obstacle field only for that case. Ordinary direct progress
            // retains its cheaper and established score.
            distanceField = ArenaBasics.StaticRouteDistanceField(
                contract, intent.Body, assignment, durable);
        }
        var generated = new List<Operation>();
        Enumerate(
            new PlannerState(intent.Body.Position, intent.Body.Facing),
            [],
            depth: 0);

        // EPIBT's physical candidate set is unique occupied-cell sequences.
        // When two action strings trace the same cells, retain the one with
        // the better final heuristic/action tie-break.
        return generated
            .GroupBy(operation => operation.CellSequence,
                StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(operation => operation.Weight)
                .ThenBy(operation => operation.ActionSequence,
                    StringComparer.Ordinal)
                .First())
            // An authored waypoint route is player intent and its sticky next
            // step outranks a locally prettier automatic move. Direct goals
            // deliberately pass null and retain normal EPIBT freedom.
            .OrderBy(operation => AuthoredCourseRank(
                operation, intent.PreferredStep))
            .ThenBy(operation => operation.Weight)
            .ThenBy(operation => operation.ActionSequence,
                StringComparer.Ordinal)
            .ToArray();

        void Enumerate(
            PlannerState state,
            List<MicroAction> actions,
            int depth)
        {
            if (depth == OperationLength)
            {
                if (actions[^1].Kind == MicroActionKind.Rotate)
                    return;
                Operation? operation = BuildOperation(
                    contract,
                    intent.Body,
                    actions.ToArray(),
                    durable,
                    inherited: false,
                    assignment,
                    course,
                    intent.Body.PreviousPosition,
                    intent.PrefersHold
                        || intent.Body.Position == assignment,
                    distanceField);
                if (operation is not null)
                    generated.Add(operation);
                return;
            }

            foreach (MicroAction action in ActionsFrom(
                         contract,
                         intent.Body,
                         state,
                         durable,
                         respectCurrentAvailability: depth == 0))
            {
                actions.Add(action);
                Enumerate(Apply(state, action, contract, intent.Body),
                    actions, depth + 1);
                actions.RemoveAt(actions.Count - 1);
            }
        }
    }

    internal static int AuthoredCourseRank(
        Operation operation,
        Position? authoredNextStep)
    {
        if (authoredNextStep is null)
            return 0;
        MicroAction? firstMove = operation.Actions
            .Cast<MicroAction?>()
            .FirstOrDefault(action => action?.Kind == MicroActionKind.Move);
        if (firstMove?.Destination == authoredNextStep)
            return 0;
        return firstMove is null ? 2 : 1;
    }

    private static IEnumerable<MicroAction> ActionsFrom(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        PlannerState state,
        IReadOnlySet<Position> durable,
        bool respectCurrentAvailability)
    {
        GenericActorRulesContract.MovementFacingCoupling coupling =
            ArenaBasics.MovementCoupling(contract, body);
        bool movementAvailable = !respectCurrentAvailability
            || ActionAvailableNow(
                contract,
                body,
                GenericActorRulesContract.ActionKind.Movement);
        bool rotationAvailable = !respectCurrentAvailability
            || ActionAvailableNow(
                contract,
                body,
                GenericActorRulesContract.ActionKind.Rotation);
        if (coupling
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked)
        {
            ProjectileHeading forward =
                (ProjectileHeading)((int)state.Facing * 2);
            Position destination = ArenaBasics.Step(state.Position, forward);
            if (movementAvailable
                && ArenaBasics.CanStep(
                    contract.Map, state.Position, destination, forward)
                && !durable.Contains(destination))
            {
                yield return MicroAction.Move(destination, forward);
            }
            if (rotationAvailable)
            {
                foreach (Direction direction in Enum.GetValues<Direction>())
                {
                    if (direction != state.Facing)
                        yield return MicroAction.Rotate(direction);
                }
            }
            yield return MicroAction.Wait;
            yield break;
        }

        if (movementAvailable)
        {
            foreach (ProjectileHeading heading in ArenaBasics.RouteHeadings(
                         contract, body))
            {
                Position destination = ArenaBasics.Step(state.Position, heading);
                if (ArenaBasics.CanStep(
                        contract.Map, state.Position, destination, heading)
                    && !durable.Contains(destination))
                {
                    yield return MicroAction.Move(destination, heading);
                }
            }
        }
        yield return MicroAction.Wait;
    }

    private static bool ActionAvailableNow(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        GenericActorRulesContract.ActionKind kind)
    {
        HashSet<string> actionIds = contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return body.ActionLegalities.Any(action =>
            action.Available && actionIds.Contains(action.ActionId));
    }

    private static bool ActionAvailableNow(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        MicroAction action) => action.Kind switch
        {
            MicroActionKind.Move => ActionAvailableNow(
                contract, body,
                GenericActorRulesContract.ActionKind.Movement),
            MicroActionKind.Rotate => ActionAvailableNow(
                contract, body,
                GenericActorRulesContract.ActionKind.Rotation),
            _ => true,
        };

    private static Operation? BuildOperation(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        IReadOnlyList<MicroAction> actions,
        IReadOnlySet<Position> durable,
        bool inherited,
        Position? assignment = null,
        IReadOnlyList<Position>? course = null,
        Position? priorPosition = null,
        bool prefersHold = false,
        IReadOnlyList<int>? distanceField = null)
    {
        var states = new PlannerState[OperationLength];
        var state = new PlannerState(body.Position, body.Facing);
        for (int index = 0; index < OperationLength; index++)
        {
            MicroAction action = actions[index];
            if (action.Kind == MicroActionKind.Move
                && (action.Destination is not Position destination
                    || durable.Contains(destination)
                    || !ArenaBasics.IsLegalTerrainStep(
                        contract.Map, state.Position, destination)))
            {
                return null;
            }
            state = Apply(state, action, contract, body);
            states[index] = state;
        }

        long weight = assignment is Position goal
            ? GoalProgressWeight(states.Select(candidate =>
                distanceField is null
                    ? GoalDistance(contract.Map, candidate.Position, goal)
                    : ArenaBasics.DistanceFromField(
                        contract.Map,
                        distanceField,
                        candidate.Position) ?? 100_000).ToArray())
            : 0;
        // EPIBT orders equal-h operations by the earliest useful action:
        // move, then rotate, then wait. This must be lexicographic rather
        // than a sum. A summed score makes RRF look cheaper than RFW and can
        // therefore rotate a facing-locked body away from the useful heading
        // solely to avoid a final wait. Base four leaves room for all three
        // action ranks without allowing beta to outweigh one h step.
        weight += OperationActionTieBreak(actions);
        // A body whose authored intent is already satisfied should stay put
        // when nobody needs its ground. It still carries a complete operation
        // set so EPIBT priority inheritance can push it out of a corridor.
        // Both penalties are less than one distance step: once WWW collides,
        // a legal turn/vacate remains preferable to increasing somebody
        // else's h. Without the rotation penalty, ordinary EPIBT beta ordering
        // makes an at-target body swivel forever because rotate beats wait.
        if (prefersHold)
            weight += PreferredHoldPenalty(actions);
        if (course is { Count: > 1 }
            && actions.Any(action => action.Destination == course[1]))
            weight -= 20_000;
        if (priorPosition is Position prior
            && actions[0].Destination == prior)
            weight += 40_000;

        return new Operation(
            actions.ToArray(),
            states,
            weight,
            inherited);
    }

    /// <summary>
    /// Compare equal-horizon operations by when they make useful progress,
    /// not by how many move glyphs they contain. The old final-position-only
    /// score preferred FFF scenic loops over FWW when both ended on a healer;
    /// a body one tile from recovery could therefore orbit forever. Final
    /// distance remains the dominant term, then earliest arrival, then the
    /// distance travelled through the horizon.
    /// </summary>
    internal static long GoalProgressWeight(IReadOnlyList<int> distances)
    {
        if (distances.Count != OperationLength)
            throw new ArgumentException(
                $"An operation needs {OperationLength} distance samples.",
                nameof(distances));
        int final = distances[^1];
        int firstArrival = Enumerable.Range(0, distances.Count)
            .FirstOrDefault(index => distances[index] == 0, distances.Count);
        long path = Math.Min(
            900_000L,
            distances.Aggregate(0L, (sum, distance) =>
                sum + Math.Min(distance, 90)) * 10_000L);
        return final * 10_000_000L + firstArrival * 1_000_000L + path;
    }

    /// <summary>
    /// Equal-distance operations prefer useful work as early as possible.
    /// Keep this visible to tests: summing action ranks once made R-R-M beat
    /// R-M-W and trapped facing-locked carriers in a deterministic turn loop.
    /// </summary>
    internal static long OperationActionTieBreak(
        IReadOnlyList<MicroAction> actions)
    {
        long result = 0;
        foreach (MicroAction action in actions)
        {
            result = result * 4 + action.Kind switch
            {
                MicroActionKind.Move => 0,
                MicroActionKind.Rotate => 1,
                _ => 2,
            };
        }
        return result;
    }

    internal static long PreferredHoldPenalty(
        IReadOnlyList<MicroAction> actions)
    {
        if (actions.Any(action => action.Kind == MicroActionKind.Move))
            return 50_000;
        return actions.Any(action => action.Kind == MicroActionKind.Rotate)
            ? 25_000
            : 0;
    }

    /// <summary>
    /// Base EPIBT priority is authored urgency/debt, distance, then unit id.
    /// A follow edge adds one semantic constraint: the named leader must be
    /// planned before its follower. This is a stable topological ordering,
    /// not a new movement rule, and it prevents a follower's provisional
    /// wait from claiming the tile its leader needs to vacate a corridor.
    /// </summary>
    private static int[] PriorityOrder(
        GenericActorResolvedMatchContract contract,
        IReadOnlyCollection<SquadMovementIntent> intents,
        IReadOnlyDictionary<int, Position> assignments)
    {
        SquadMovementIntent[] remaining = intents
            .OrderBy(intent => intent.Priority.Rank)
            .ThenBy(intent => GoalDistance(
                contract.Map,
                intent.Body.Position,
                assignments[intent.Body.UnitId]))
            .ThenBy(intent => intent.Body.UnitId)
            .ToArray();
        HashSet<int> present = remaining
            .Select(intent => intent.Body.UnitId)
            .ToHashSet();
        Dictionary<int, int> servicePredecessors =
            ResolveServiceQueuePredecessors(remaining.Select(intent => new
                ServiceQueueMember(
                    intent.Body.UnitId,
                    intent.GroupKey,
                    intent.CanStage,
                    intent.Goals.Count == 0
                        ? 100_000
                        : intent.Goals.Min(goal => GoalDistance(
                            contract.Map, intent.Body.Position, goal)))));
        var emitted = new HashSet<int>();
        var result = new List<int>(remaining.Length);

        while (result.Count < remaining.Length)
        {
            SquadMovementIntent? next = remaining.FirstOrDefault(intent =>
                !emitted.Contains(intent.Body.UnitId)
                && (intent.LeaderUnitId is not int leader
                    || !present.Contains(leader)
                    || emitted.Contains(leader))
                && (!servicePredecessors.TryGetValue(
                        intent.Body.UnitId, out int predecessor)
                    || emitted.Contains(predecessor)));
            // A malformed/cyclic follow graph must not make planning partial.
            // Unit-id fallback is deterministic; the compiler owns rejecting
            // authored cycles, while the executor remains total on input.
            next ??= remaining
                .Where(intent => !emitted.Contains(intent.Body.UnitId))
                .OrderBy(intent => intent.Body.UnitId)
                .First();
            emitted.Add(next.Body.UnitId);
            result.Add(next.Body.UnitId);
        }
        return result.ToArray();
    }

    /// <summary>
    /// Bodies approaching the same one-tile service form a deterministic
    /// queue. The nearest body leads and an exact distance tie uses unit id.
    /// This prevents a rear patient or Well worker from inheriting priority,
    /// stepping into the lead body's tile, then making both reverse on the
    /// following tick. Fight contribution ground is deliberately excluded:
    /// it is a tactical target set, not a one-tile service queue.
    /// </summary>
    internal static Dictionary<int, int> ResolveServiceQueuePredecessors(
        IEnumerable<ServiceQueueMember> source)
    {
        var result = new Dictionary<int, int>();
        foreach (IGrouping<string, ServiceQueueMember> group in source
                     .Where(member => member.CanStage
                         && IsSingleServiceGroup(member.GroupKey))
                     .GroupBy(member => member.GroupKey,
                         StringComparer.Ordinal))
        {
            ServiceQueueMember[] ordered = group
                .OrderBy(member => member.Distance)
                .ThenBy(member => member.UnitId)
                .ToArray();
            for (int index = 1; index < ordered.Length; index++)
                result[ordered[index].UnitId] = ordered[index - 1].UnitId;
        }
        return result;
    }

    private static bool IsSingleServiceGroup(string groupKey) =>
        groupKey.StartsWith("move:to:heal:", StringComparison.Ordinal)
        || groupKey.StartsWith("move:to:well:", StringComparison.Ordinal);

    private static PlannerState Apply(
        PlannerState state,
        MicroAction action,
        GenericActorResolvedMatchContract contract,
        MindBody body) => action.Kind switch
        {
            MicroActionKind.Move => new PlannerState(
                action.Destination!.Value,
                ArenaBasics.FacingAfterMove(
                    state.Facing,
                    action.Heading!.Value,
                    ArenaBasics.MovementCoupling(contract, body))),
            MicroActionKind.Rotate => state with { Facing = action.Facing },
            _ => state,
        };

    internal static bool OperationsCollide(Operation first, Operation second)
    {
        Position firstPrior = first.Start;
        Position secondPrior = second.Start;
        for (int tick = 0; tick < OperationLength; tick++)
        {
            Position firstNow = first.States[tick].Position;
            Position secondNow = second.States[tick].Position;
            if (firstNow == secondNow
                || firstPrior == secondNow && secondPrior == firstNow)
                return true;
            firstPrior = firstNow;
            secondPrior = secondNow;
        }
        return false;
    }

    /// <summary>
    /// Mirror the engine's atomic-follow rule over every operation timestep.
    /// A chain into a vacancy is legal; a closed rotation has no vacancy and
    /// is rejected as a unit. Pairwise collision checks alone cannot observe
    /// a cycle of three or more bodies.
    /// </summary>
    internal static bool OperationsHaveAcyclicMovement(
        IEnumerable<Operation> source)
    {
        Operation[] operations = source.ToArray();
        for (int tick = 0; tick < OperationLength; tick++)
        {
            if (!StepHasAcyclicMovement(operations, tick))
                return false;
        }
        return true;
    }

    internal static bool FirstStepIsExecutable(IEnumerable<Operation> source)
    {
        Operation[] operations = source.ToArray();
        if (!StepHasAcyclicMovement(operations, tick: 0))
            return false;
        // Acyclic dependency checks reject swaps and longer rotations. A
        // shared destination is the other way a joint first step can be
        // impossible even when nobody depends on an occupied start tile.
        return operations.Select(operation => operation.States[0].Position)
            .Distinct()
            .Count() == operations.Length;
    }

    private static bool StepHasAcyclicMovement(
        Operation[] operations,
        int tick)
    {
        Position[] before = operations.Select(operation => tick == 0
                ? operation.Start
                : operation.States[tick - 1].Position)
            .ToArray();
        Position[] after = operations
            .Select(operation => operation.States[tick].Position)
            .ToArray();
        var occupant = new Dictionary<Position, int>();
        for (int index = 0; index < before.Length; index++)
        {
            // A duplicated predicted pre-state means an earlier timestep
            // already violated vertex occupancy. Treat it as an invalid
            // joint operation rather than throwing from a diagnostic.
            if (!occupant.TryAdd(before[index], index))
                return false;
        }
        var dependency = new Dictionary<int, int>();
        for (int index = 0; index < operations.Length; index++)
        {
            if (after[index] != before[index]
                && occupant.TryGetValue(after[index], out int other))
                dependency[index] = other;
        }
        foreach (int start in dependency.Keys.Order())
        {
            var seen = new HashSet<int>();
            int current = start;
            while (dependency.TryGetValue(current, out int next))
            {
                if (!seen.Add(current))
                    return false;
                current = next;
            }
        }
        return true;
    }

    private static HashSet<Position> DurableObstacles(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        ArenaBasics.Claims? claims = null)
    {
        var result = ArenaBasics.PersistentRouteObstacles(
            contract, mind, body.ActorId.TeamId);
        // EPIBT coordinates this participant's squad only. An enemy may move
        // during the same authoritative tick, but assuming that cooperation
        // would turn an uncertain hostile tile into repeated blocked commands.
        result.UnionWith(mind.Enemies.Select(enemy => enemy.Position));
        if (claims is not null)
        {
            HashSet<Position> immediate = ArenaBasics.BlockedNow(
                contract, mind, body, claims);
            // Allies are represented by EPIBT's operation table rather than
            // duplicated as static occupancy walls.
            immediate.ExceptWith(mind.Bodies.Select(ally => ally.Position));
            result.UnionWith(immediate);
        }
        result.Remove(body.Position);
        return result;
    }

    private static HashSet<Position> DurableObstaclesExceptGoal(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        MindBody body,
        Position goal,
        ArenaBasics.Claims? claims = null)
    {
        HashSet<Position> result = DurableObstacles(
            contract, mind, body, claims);
        result.Remove(goal);
        return result;
    }

    private Position[] ResolveCourse(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        SquadMovementIntent intent,
        Position assignment)
    {
        if (intent.Pinned || intent.Body.Position == assignment)
            return [intent.Body.Position];
        HashSet<Position> obstacles = ArenaBasics.PersistentRouteObstacles(
            contract, mind, intent.Body.ActorId.TeamId);
        if (IsCarrierReturn(intent))
            obstacles.UnionWith(CarrierReturnSpawnTiles(
                contract.LifecycleAssignments,
                contract.Map.SpawnAnchors,
                intent.Body.ActorId.TeamId));
        Position[]? course = null;
        if (_states.TryGetValue(
                intent.Body.UnitId, out PersistentState? prior)
            && prior.Actor == intent.Body.ActorId
            && string.Equals(prior.Key, intent.Key, StringComparison.Ordinal)
            && prior.Assignment == assignment)
        {
            int cursor = Array.FindIndex(
                prior.Course, tile => tile == intent.Body.Position);
            if (cursor >= 0)
            {
                Position[] suffix = prior.Course[cursor..];
                bool stillLegal = suffix
                    .Skip(1)
                    .Select((tile, index) => (
                        From: suffix[index],
                        To: tile))
                    .All(step => ArenaBasics.IsLegalTerrainStep(
                            contract.Map, step.From, step.To)
                        && (step.To == assignment
                            || !obstacles.Contains(step.To)));
                if (stillLegal)
                    course = suffix;
            }
        }
        course ??= ArenaBasics.StaticRouteCourse(
            contract,
            intent.Body,
            assignment,
            obstacles,
            _mirrored) ?? [intent.Body.Position];
        if (intent.PreferredStep is Position preferred
            && ArenaBasics.IsLegalTerrainStep(
                contract.Map, intent.Body.Position, preferred))
        {
            return [intent.Body.Position, preferred, .. course.Skip(1)
                .Where(tile => tile != preferred)];
        }
        return course;
    }

    private static bool IsCarrierReturn(SquadMovementIntent intent) =>
        string.Equals(
            intent.GroupKey, "move:to:reactor:own", StringComparison.Ordinal);

    private void RememberDeniedSteps(MindContext mind)
    {
        foreach (MindBody body in mind.Bodies)
        {
            if (_deniedSteps.TryGetValue(body.UnitId, out DeniedStep? prior)
                && (prior.Actor != body.ActorId
                    || mind.Tick >= prior.UntilTick
                    || body.MovedLastTick))
            {
                _deniedSteps.Remove(body.UnitId);
            }

            if (body.PreviousActionResolution is not
                { Outcome: GenericActorActionResolution.ActionOutcome.Blocked }
                    resolution
                || resolution.AcceptedAction.Arguments
                    .OfType<GenericActorActionArgument.ProjectileHeadingArgument>()
                    .SingleOrDefault() is not { } heading)
            {
                continue;
            }

            (int dx, int dy) = heading.Value.Vector();
            _deniedSteps[body.UnitId] = new DeniedStep(
                body.ActorId,
                body.Position.Offset(dx, dy),
                mind.Tick + DeniedStepAvoidanceTicks);
        }
    }

    private DeniedStep? ActiveDeniedStep(MindBody body) =>
        _mind is { } mind
        && _deniedSteps.TryGetValue(body.UnitId, out DeniedStep? denied)
        && denied.Actor == body.ActorId
        && mind.Tick < denied.UntilTick
            ? denied
            : null;

    /// <summary>
    /// A loaded return avoids every automatic-return tile owned by its team,
    /// not merely the subset reserved on this observation. A dead body's
    /// reservation appears and disappears with lifecycle state; using that
    /// transient subset as a shortest-path wall can make an otherwise sound
    /// carrier alternate between two corridors on successive relocation
    /// beats. The full deployment catalog is immutable match input, so this
    /// route remains deterministic and leaves the spawn pad clear for every
    /// possible return.
    /// </summary>
    internal static Position[] CarrierReturnSpawnTiles(
        IEnumerable<GenericActorResolvedMatchContract.LifecycleAssignment>
            assignments,
        IEnumerable<GenericActorMapContract.SpawnAnchor> spawns,
        int teamId)
    {
        HashSet<string> assigned = assignments
            .Where(value => value.TeamId == teamId
                && value.AssignedRespawnSpawnId is not null)
            .Select(value => value.AssignedRespawnSpawnId!)
            .ToHashSet(StringComparer.Ordinal);
        return spawns
            .Where(spawn => assigned.Contains(spawn.SpawnId))
            .Select(spawn => spawn.Position)
            .Distinct()
            .OrderBy(position => position.Y)
            .ThenBy(position => position.X)
            .ToArray();
    }

    private Dictionary<int, Position> AssignContributions(
        GenericActorResolvedMatchContract contract,
        IReadOnlyCollection<SquadMovementIntent> intents)
    {
        MindContext mind = _mind
            ?? throw new InvalidOperationException("BeginTick was not called.");
        var result = new Dictionary<int, Position>();
        foreach (IGrouping<string, SquadMovementIntent> grouping in intents
                     .GroupBy(intent => intent.GroupKey, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            SquadMovementIntent[] members = grouping
                .OrderBy(intent => intent.Priority.Rank)
                .ThenBy(intent => intent.Body.UnitId)
                .ToArray();
            Position[] authored = members
                .SelectMany(intent => intent.PrefersHold
                    ? [intent.Body.Position]
                    : intent.Goals)
                .Where(tile => ArenaBasics.CanEnter(contract.Map, tile))
                .Distinct()
                .OrderBy(tile => ArenaBasics.FrameY(tile, _mirrored))
                .ThenBy(tile => ArenaBasics.FrameX(tile, _mirrored))
                .ToArray();
            if (authored.Length == 0)
            {
                foreach (SquadMovementIntent member in members)
                    result[member.Body.UnitId] = member.Body.Position;
                continue;
            }
            HashSet<int> memberIds = members
                .Select(member => member.Body.UnitId)
                .ToHashSet();
            bool occupantRetainsService = grouping.Key.StartsWith(
                    "move:to:heal:", StringComparison.Ordinal)
                || grouping.Key.StartsWith(
                    "move:to:well:", StringComparison.Ordinal);
            bool serviceTileOwnedOutsideGroup = authored.Length == 1
                && members.All(member => member.CanStage)
                && occupantRetainsService
                && mind.Bodies.Any(body => !memberIds.Contains(body.UnitId)
                    && body.Position == authored[0]);
            // A one-tile service remains owned by the body physically using
            // it, even if that body temporarily selects a signature or fight
            // order and therefore leaves this movement group for a tick. The
            // waiting patient/carrier keeps its stable contribution tile
            // instead of chasing and orbiting the occupied service square.
            bool staged = authored.Length == 1
                && members.All(member => member.CanStage)
                && (members.Length > 1 || serviceTileOwnedOutsideGroup);
            HashSet<Position> persistentObstacles =
                ArenaBasics.PersistentRouteObstacles(
                    contract, mind, members[0].Body.ActorId.TeamId);
            Position[] candidates = staged
                ? ContributionRing(
                    contract.Map,
                    authored[0],
                    members.Length,
                    persistentObstacles)
                : authored;
            if (candidates.Length < members.Length)
            {
                foreach (SquadMovementIntent member in members)
                    result[member.Body.UnitId] = NearestGoal(
                        contract, member.Body, member.Goals);
                continue;
            }
            var costs = new long[members.Length][];
            for (int row = 0; row < members.Length; row++)
            {
                SquadMovementIntent member = members[row];
                costs[row] = new long[candidates.Length];
                for (int column = 0; column < candidates.Length; column++)
                {
                    Position candidate = candidates[column];
                    bool allowed = staged || member.Goals.Contains(candidate)
                        || member.PrefersHold
                            && candidate == member.Body.Position;
                    int? distance = allowed
                        ? ArenaBasics.StaticDistance(
                            contract.Map, member.Body.Position, candidate)
                        : null;
                    long cost = distance is int steps
                        ? steps * 1_000L
                        : Impossible;
                    if (staged && candidate == authored[0]
                        && serviceTileOwnedOutsideGroup)
                    {
                        cost = Impossible;
                    }
                    else if (staged && candidate == authored[0])
                        cost -= 8_000;
                    if (cost < Impossible
                        && _states.TryGetValue(
                            member.Body.UnitId, out PersistentState? state)
                        && state.Actor == member.Body.ActorId
                        && string.Equals(
                            state.Key, member.Key, StringComparison.Ordinal)
                        && state.Assignment == candidate)
                        cost -= 3_000;
                    costs[row][column] = cost;
                }
            }
            int[] chosen = SolveMinimumCost(costs);
            for (int row = 0; row < members.Length; row++)
            {
                SquadMovementIntent member = members[row];
                result[member.Body.UnitId] = chosen[row] >= 0
                    && costs[row][chosen[row]] < Impossible
                        ? candidates[chosen[row]]
                        : NearestGoal(contract, member.Body, member.Goals);
            }
        }
        return result;
    }

    private Position NearestGoal(
        GenericActorResolvedMatchContract contract,
        MindBody body,
        IReadOnlyCollection<Position> goals) => goals.Count == 0
            ? body.Position
            : goals
                .OrderBy(goal => GoalDistance(
                    contract.Map, body.Position, goal))
                .ThenBy(goal => ArenaBasics.FrameY(goal, _mirrored))
                .ThenBy(goal => ArenaBasics.FrameX(goal, _mirrored))
                .First();

    private static int GoalDistance(
        GenericActorMapContract map,
        Position from,
        Position goal) =>
        ArenaBasics.StaticDistance(map, from, goal) ?? 100_000;

    private static Position RotationRequestTile(
        Position position,
        Direction facing) => facing switch
        {
            Direction.North => position.Offset(0, -1),
            Direction.East => position.Offset(1, 0),
            Direction.South => position.Offset(0, 1),
            Direction.West => position.Offset(-1, 0),
            _ => position,
        };

    private static Position[] ContributionRing(
        GenericActorMapContract map,
        Position exact,
        int needed,
        IReadOnlySet<Position> persistentObstacles)
    {
        var result = new List<Position> { exact };
        for (int radius = 1;
             radius <= 3 && result.Count < needed + 4;
             radius++)
        {
            for (int y = exact.Y - radius; y <= exact.Y + radius; y++)
            for (int x = exact.X - radius; x <= exact.X + radius; x++)
            {
                var tile = new Position(x, y);
                if (tile.ChebyshevDistance(exact) == radius
                    && ArenaBasics.CanEnter(map, tile)
                    // The authored objective itself may deliberately be a
                    // special tile. Its automatically-derived contribution
                    // ring may not claim spawn reservations, constructs, or
                    // protected terrain: removing such a staging assignment
                    // from EPIBT's durable obstacles would make the planner
                    // repeatedly request a step the engine must reject.
                    && !persistentObstacles.Contains(tile))
                    result.Add(tile);
            }
        }
        return result.Distinct().ToArray();
    }

    private static Operation WaitOperation(MindBody body)
    {
        var state = new PlannerState(body.Position, body.Facing);
        return new Operation(
            [MicroAction.Wait, MicroAction.Wait, MicroAction.Wait],
            [state, state, state],
            0,
            Inherited: false);
    }

    /// <summary>Deterministic rectangular Hungarian assignment.</summary>
    internal static int[] SolveMinimumCost(IReadOnlyList<long[]> costs)
    {
        int rows = costs.Count;
        if (rows == 0)
            return [];
        int columns = costs[0].Length;
        if (columns < rows)
            throw new ArgumentException(
                "Assignment needs at least one column per row.");
        var u = new long[rows + 1];
        var v = new long[columns + 1];
        var p = new int[columns + 1];
        var way = new int[columns + 1];
        for (int i = 1; i <= rows; i++)
        {
            p[0] = i;
            int j0 = 0;
            var min = Enumerable.Repeat(long.MaxValue, columns + 1).ToArray();
            var used = new bool[columns + 1];
            do
            {
                used[j0] = true;
                int i0 = p[j0];
                long delta = long.MaxValue;
                int j1 = 0;
                for (int j = 1; j <= columns; j++)
                {
                    if (used[j])
                        continue;
                    long current = costs[i0 - 1][j - 1] - u[i0] - v[j];
                    if (current < min[j])
                    {
                        min[j] = current;
                        way[j] = j0;
                    }
                    if (min[j] < delta)
                    {
                        delta = min[j];
                        j1 = j;
                    }
                }
                if (delta == long.MaxValue)
                    break;
                for (int j = 0; j <= columns; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                        min[j] -= delta;
                }
                j0 = j1;
            } while (p[j0] != 0);
            do
            {
                int j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            } while (j0 != 0);
        }
        var answer = Enumerable.Repeat(-1, rows).ToArray();
        for (int column = 1; column <= columns; column++)
        {
            if (p[column] > 0)
                answer[p[column] - 1] = column - 1;
        }
        return answer;
    }

    internal enum MicroActionKind { Wait, Rotate, Move }

    internal readonly record struct MicroAction(
        MicroActionKind Kind,
        Position? Destination,
        ProjectileHeading? Heading,
        Direction Facing)
    {
        public static MicroAction Wait => new(
            MicroActionKind.Wait, null, null, default);
        public static MicroAction Rotate(Direction facing) => new(
            MicroActionKind.Rotate, null, null, facing);
        public static MicroAction Move(
            Position destination,
            ProjectileHeading heading) => new(
            MicroActionKind.Move, destination, heading, default);
    }

    internal sealed record Operation(
        MicroAction[] Actions,
        PlannerState[] States,
        long Weight,
        bool Inherited)
    {
        public Position Start => Actions[0].Kind == MicroActionKind.Move
            ? ReverseStart(States[0].Position, Actions[0].Heading!.Value)
            : States[0].Position;
        public string CellSequence => string.Join(";", States.Select(state =>
            $"{state.Position.X},{state.Position.Y}"));
        public string ActionSequence => string.Join("", Actions.Select(action =>
            action.Kind switch
            {
                MicroActionKind.Move => "F",
                MicroActionKind.Rotate => "R" + (int)action.Facing,
                _ => "W",
            }));

        private static Position ReverseStart(
            Position destination,
            ProjectileHeading heading)
        {
            (int dx, int dy) = heading.Vector();
            return destination.Offset(-dx, -dy);
        }
    }

    internal readonly record struct PlannerState(
        Position Position,
        Direction Facing);
    internal readonly record struct ServiceQueueMember(
        int UnitId,
        string GroupKey,
        bool CanStage,
        int Distance);
    private sealed record Decision(
        Position? Tile,
        string Trace,
        bool Yielding,
        bool PhysicallySatisfied);
    private sealed record PersistentState(
        ActorIdentity Actor,
        string Key,
        Position Assignment,
        Operation Operation,
        Position[] Course);
    private sealed record DeniedStep(
        ActorIdentity Actor,
        Position Tile,
        int UntilTick);
}
