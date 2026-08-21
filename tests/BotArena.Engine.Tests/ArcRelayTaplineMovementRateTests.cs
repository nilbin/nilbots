using System.Text.Json;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins Tapline-11's explicit travel-rate model beside immutable Tapline-10.
/// </summary>
public sealed class ArcRelayTaplineMovementRateTests
{
    [Fact]
    public void Tapline11UnifiesControlsAndMakesHandlingBandsActualRates()
    {
        ActorResolvedMatchDefinition previous = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline10);
        ActorResolvedMatchDefinition current = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline11);

        Assert.Equal("arc-relay-tapline-11", current.Rules.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(previous.Map),
            ActorContractFingerprint.ComputeMap(current.Map));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(previous.Rules),
            ActorContractFingerprint.ComputeRules(current.Rules));

        Assert.Equal(
            [
                ("deliberate", 2, 3),
                ("standard", 4, 5),
                ("swift", 1, 1),
            ],
            current.Rules.MovementProfiles
                .OrderBy(profile => profile.Id, StringComparer.Ordinal)
                .Select(profile => (
                    profile.Id,
                    profile.MoveOpportunitiesPerCycle,
                    profile.MoveCycleTicks))
                .ToArray());
        Assert.All(
            current.Rules.MovementProfiles,
            profile => Assert.Equal(
                ActorMovementFacingCoupling.CombatStrafe,
                profile.FacingCoupling));
        Assert.DoesNotContain(
            current.Rules.Actions,
            action => action.Id == ArcRelayH0Definition.StrafeActionId);
    }

    [Fact]
    public void Tapline11RetainsTapline10MapCombatAndObjectiveRules()
    {
        ActorResolvedMatchDefinition previous = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline10);
        ActorResolvedMatchDefinition current = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline11);

        Assert.Equal(previous.Map.Id, current.Map.Id);
        Assert.True(previous.Map.TileRows.SequenceEqual(current.Map.TileRows));
        using JsonDocument previousContract = JsonDocument.Parse(
            ActorContractManifestSerializer.ToCanonicalJson(previous));
        using JsonDocument currentContract = JsonDocument.Parse(
            ActorContractManifestSerializer.ToCanonicalJson(current));
        JsonElement previousRules = previousContract.RootElement
            .GetProperty("rules");
        JsonElement currentRules = currentContract.RootElement
            .GetProperty("rules");
        Assert.Equal(
            previousRules.GetProperty("visionProfiles").GetRawText(),
            currentRules.GetProperty("visionProfiles").GetRawText());
        Assert.Equal(
            previousRules.GetProperty("attackProfiles").GetRawText(),
            currentRules.GetProperty("attackProfiles").GetRawText());
        Assert.Equal(
            previousRules.GetProperty("gameMode").GetRawText(),
            currentRules.GetProperty("gameMode").GetRawText());
    }

    [Fact]
    public void Tapline10RetainsHistoricalHandlingAndUniversalTravel()
    {
        ActorResolvedMatchDefinition historical = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline10);

        Assert.Equal(
            ActorMovementFacingCoupling.FaceMovementHeadingProjected,
            Movement(historical, "swift").FacingCoupling);
        Assert.Equal(
            ActorMovementFacingCoupling.CombatStrafe,
            Movement(historical, "standard").FacingCoupling);
        Assert.Equal(
            ActorMovementFacingCoupling.FacingLocked,
            Movement(historical, "deliberate").FacingCoupling);
        Assert.All(historical.Rules.MovementProfiles, profile =>
        {
            Assert.Equal(1, profile.MoveOpportunitiesPerCycle);
            Assert.Equal(1, profile.MoveCycleTicks);
        });
        Assert.Contains(
            historical.Rules.Actions,
            action => action.Id == ArcRelayH0Definition.StrafeActionId);
    }

    [Fact]
    public void EveryChassisCanBankThreeEnemyWellCoresInsideSixHundredTicksOnPracticalRoutes()
    {
        ActorResolvedMatchDefinition current = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline11);

        Assert.Equal(
            [334, 382, 429],
            WinningTicks(current, routeSteps: 31));
        Assert.Equal(
            [394, 469, 549],
            WinningTicks(current, routeSteps: 41));

        // The deliberately circuitous 58-step outer return remains viable for
        // swift logistics, but is not a 600-tick standard/deliberate win plan.
        // This is a disclosed route choice, not a hidden carrier exception.
        Assert.Equal(
            [520, 637, 752],
            WinningTicks(current, routeSteps: 58));
    }

    [Fact]
    public void LiveCarrierMustSatisfyBothCoreClockAndChassisCadence()
    {
        string[] composition =
        [
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Minesmith,
            ArcRelayLaunchClassIds.Hush,
            ArcRelayLaunchClassIds.Towline,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            composition,
            composition,
            loopProfile: ArcRelayLoopProfile.Tapline11);
        ActorActionDefinition move = definition.Rules.Actions.Single(action =>
            action.Kind == ActorActionKind.Movement);
        ActorActionDefinition wait = definition.Rules.Actions.Single(action =>
            action.Kind == ActorActionKind.Wait);
        ActorMovementProfileDefinition standard = Movement(
            definition,
            "standard");
        var samples = new List<CarrierAvailabilitySample>();
        int? pickupTick = null;
        ActorIdentity? carrierId = null;
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) => new GenericMindRuntimeDecisions(
                [
                    .. observation.Bodies.Select(body => Command(
                        observation,
                        body)),
                ]));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 35_511UL);
        var outcomes = new Dictionary<int,
            GenericActorRuntimeActionResolution.ActionOutcome>();

        int lastTick = -1;
        while (!session.IsCompleted
            && (pickupTick is null || lastTick <= pickupTick + 12))
        {
            GenericActorMatchStepResult step = session.Step();
            lastTick = step.Tick;
            if (carrierId is ActorIdentity actor)
            {
                GenericActorMatchActorResolution resolution =
                    step.ActionResolutions.Single(value =>
                        value.ActorId == actor);
                outcomes[step.Tick] = resolution.Resolution.Outcome;
            }
        }

        Assert.True(pickupTick.HasValue, $"Match ended before pickup: {session.Result}");

        CarrierAvailabilitySample coreClosed = samples.First(sample =>
            sample.ChassisReady && !sample.CoreReady);
        CarrierAvailabilitySample chassisClosed = samples.First(sample =>
            !sample.ChassisReady && sample.CoreReady);
        CarrierAvailabilitySample bothReady = samples.First(sample =>
            sample.ChassisReady && sample.CoreReady);
        Assert.False(coreClosed.MaskAvailable);
        Assert.False(chassisClosed.MaskAvailable);
        Assert.True(bothReady.MaskAvailable);
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            outcomes[coreClosed.Tick]);
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            outcomes[chassisClosed.Tick]);
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Success,
            outcomes[bothReady.Tick]);

        GenericMindCommand Command(
            GenericMindRuntimeObservation observation,
            GenericMindRuntimeObservation.ObservedBodyState body)
        {
            if (body.ActorId.TeamId != 0 || body.ActorId.UnitId != 7)
                return Wait(body);

            var mode = Assert.IsType<
                GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                    observation.Mode);
            ArcRelayCoreState? carried = mode.VisibleCores.SingleOrDefault(core =>
                core.CarrierActorId == body.ActorId);
            Position destination = carried is null
                ? new Position(15, 13)
                : new Position(2, 5);
            if (carried is not null)
            {
                pickupTick ??= observation.Tick;
                carrierId ??= body.ActorId;
                int tick = observation.Tick;
                samples.Add(new CarrierAvailabilitySample(
                    tick,
                    ActorMovementCadence.IsMoveOpportunity(
                        standard,
                        body.ActorId,
                        tick),
                    tick >= carried.NextRelocationTick,
                    body.ActionLegalities.Single(value =>
                        value.ActionId == move.Id).Available));
            }
            Direction? direction = NextDirection(
                definition.Map,
                body.Position,
                destination,
                observation.Bodies
                    .Where(value => value.ActorId != body.ActorId)
                    .Select(value => value.Position)
                    .ToHashSet());
            return direction is null
                ? Wait(body)
                : new GenericMindCommand(
                    body.ActorId.UnitId,
                    body.ActorId.LifeId,
                    move.Id,
                    move.Code,
                    [new GenericActorRuntimeActionArgument
                        .ProjectileHeadingArgument(
                            direction.Value.ToProjectileHeading())]);
        }

        GenericMindCommand Wait(
            GenericMindRuntimeObservation.ObservedBodyState body) => new(
            body.ActorId.UnitId,
            body.ActorId.LifeId,
            wait.Id,
            wait.Code,
            []);

    }

    private static Direction? NextDirection(
        ActorMapDefinition map,
        Position start,
        Position destination,
        HashSet<Position> occupied)
    {
        if (start == destination)
            return null;
        var seen = new HashSet<Position> { start };
        var queue = new Queue<(Position Position, Direction First)>();
        foreach (Direction direction in Enum.GetValues<Direction>())
        {
            (int dx, int dy) = direction.Vector();
            Position next = start.Offset(dx, dy);
            if (CanEnter(next))
            {
                seen.Add(next);
                queue.Enqueue((next, direction));
            }
        }
        while (queue.Count > 0)
        {
            (Position position, Direction first) = queue.Dequeue();
            if (position == destination)
                return first;
            foreach (Direction direction in Enum.GetValues<Direction>())
            {
                (int dx, int dy) = direction.Vector();
                Position next = position.Offset(dx, dy);
                if (seen.Add(next) && CanEnter(next))
                    queue.Enqueue((next, first));
            }
        }
        return null;

        bool CanEnter(Position position) =>
            position.X >= 0 && position.X < map.Width
            && position.Y >= 0 && position.Y < map.Height
            && !map.IsWall(position)
            && !occupied.Contains(position);
    }

    private sealed record CarrierAvailabilitySample(
        int Tick,
        bool ChassisReady,
        bool CoreReady,
        bool MaskAvailable);

    private static int[] WinningTicks(
        ActorResolvedMatchDefinition definition,
        int routeSteps)
    {
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        int latestOpeningTick = mode.Wells.Max(well => well.FirstBirthTick)
            + mode.WellBirthJitterTicks;
        return new[] { "swift", "standard", "deliberate" }
            .Select(id =>
            {
                ActorMovementProfileDefinition profile = Movement(
                    definition,
                    id);
                int empty = WorstTravelTicks(
                    profile,
                    routeSteps,
                    coreRelocationIntervalTicks: 1);
                int carried = WorstTravelTicks(
                    profile,
                    routeSteps,
                    mode.CoreRelocationIntervalTicks);
                return latestOpeningTick + carried
                    + 2 * (Math.Max(
                        mode.BankTriggeredWellRechargeTicks,
                        empty) + carried);
            })
            .ToArray();
    }

    private static int WorstTravelTicks(
        ActorMovementProfileDefinition profile,
        int steps,
        int coreRelocationIntervalTicks) =>
        Enumerable.Range(0, profile.MoveCycleTicks)
        .Select(startTick => TravelTicks(
            profile,
            startTick,
            steps,
            coreRelocationIntervalTicks))
        .Max();

    private static int TravelTicks(
        ActorMovementProfileDefinition profile,
        int startTick,
        int steps,
        int coreRelocationIntervalTicks)
    {
        ActorIdentity actorId = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        int tick = startTick;
        int nextCoreRelocationTick = startTick + coreRelocationIntervalTicks;
        int moved = 0;
        while (moved < steps)
        {
            tick++;
            if (tick < nextCoreRelocationTick
                || !ActorMovementCadence.IsMoveOpportunity(
                    profile,
                    actorId,
                    tick))
            {
                continue;
            }
            moved++;
            nextCoreRelocationTick = tick + coreRelocationIntervalTicks;
        }
        return tick - startTick;
    }

    private static ActorMovementProfileDefinition Movement(
        ActorResolvedMatchDefinition definition,
        string id) => definition.Rules.MovementProfiles.Single(profile =>
            profile.Id == id);
}
