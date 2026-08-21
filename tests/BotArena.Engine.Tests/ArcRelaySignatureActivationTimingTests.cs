using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the joint-tick seam used by definition-authored signature activation
/// timing. The arena places a Relay on the centre Well and hostile bodies one
/// step away so a declared strike and a zero-tell Toss settle on the same tick.
/// </summary>
public sealed class ArcRelaySignatureActivationTimingTests
{
    private static readonly Position CarrierPosition = new(10, 4);

    [Fact]
    public void AfterCombatSignatureStartsOnlyAfterCommittedDamage()
    {
        SceneResult result = Run(lethalVolley: false);

        int damage = IndexOf(result.Events, payload => payload is
            GenericActorRuntimeObservation.EventPayload.Damage
            {
                TargetActorId.TeamId: 0,
                TargetActorId.UnitId: 0,
            });
        int launched = IndexOf(result.Events, payload => payload is
            GenericActorRuntimeObservation.EventPayload.ArcRelay
            {
                Fact: ArcRelayEvent.SignatureChanged
                {
                    SignatureId: "arc-toss",
                    Reason: "launched",
                },
            });

        Assert.True(damage >= 0, "the committed strike must land");
        Assert.True(launched > damage,
            "the after-combat signature must launch after damage");
        var arc = Assert.IsType<
            GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                result.PostState.Mode);
        Assert.Contains(arc.VisibleCores, core =>
            core.Disposition == ArcRelayCoreState.CoreDisposition.InFlight);
    }

    [Fact]
    public void LethalCommittedCombatPreventsAfterCombatSignatureStart()
    {
        SceneResult result = Run(lethalVolley: true);

        Assert.Contains(result.Events, value => value.Payload is
            GenericActorRuntimeObservation.EventPayload.Damage
            {
                TargetActorId.TeamId: 0,
                TargetActorId.UnitId: 0,
                NewHealth: 0,
            });
        Assert.DoesNotContain(result.Events, value => value.Payload is
            GenericActorRuntimeObservation.EventPayload.ArcRelay
            {
                Fact: ArcRelayEvent.SignatureChanged
                {
                    SignatureId: "arc-toss",
                    Reason: "started" or "launched",
                },
            });
        Assert.Contains(result.Events, value => value.Payload is
            GenericActorRuntimeObservation.EventPayload.ArcRelay
            {
                Fact: ArcRelayEvent.CoreDropped
                {
                    Kind: ArcRelayEvent.CoreDropKind.Destruction,
                },
            });
        var arc = Assert.IsType<
            GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                result.PostState.Mode);
        Assert.Contains(arc.VisibleCores, core =>
            core.Disposition == ArcRelayCoreState.CoreDisposition.Loose);
    }

    [Fact]
    public void FallingStarDamagesTheHostileCrossAndNeverItsOwnTeam()
    {
        ActorResolvedMatchDefinition definition = Definition(
            fallingStarScene: true);
        ActorActionDefinition wait = definition.Rules.Actions.Single(value =>
            value.Kind == ActorActionKind.Wait);
        Position target = new(2, 5);
        bool declared = false;
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (start, observation) => new GenericMindRuntimeDecisions(
                [
                    .. observation.Bodies.Select(body =>
                    {
                        GenericActorRuntimeActionLegality? star =
                            start.TeamId == 0
                            && body.ActorId.UnitId == 6
                            && !declared
                                ? body.ActionLegalities.FirstOrDefault(value =>
                                    value.ActionId == "falling-star"
                                    && value.Available)
                                : null;
                        if (star is not null)
                        {
                            Assert.Contains(
                                target,
                                star.Constraints.OfType<
                                        GenericActorRuntimeActionLegality
                                            .ArgumentConstraint
                                            .PositionTargetConstraint>()
                                    .Single().AllowedValues);
                            declared = true;
                            return NewCommand(
                                body,
                                star.ActionId,
                                star.ActionCode,
                                [new GenericActorRuntimeActionArgument
                                    .PositionTargetArgument(target)]);
                        }
                        return NewCommand(body, wait.Id, wait.Code, []);
                    }),
                ]));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 20_260_821UL);

        var damage = new List<ArcRelayEvent.SignatureDamage>();
        bool completed = false;
        for (int tick = 0; tick < 8 && !completed; tick++)
        {
            GenericActorMatchStepResult step = session.Step();
            IEnumerable<GenericActorAuthoritativeEvent> events =
                (step.AuthoritativeTickStart?.Events ?? [])
                    .Concat(step.AuthoritativeEvents);
            foreach (GenericActorAuthoritativeEvent item in events)
            {
                if (item.Payload is
                    GenericActorRuntimeObservation.EventPayload.ArcRelay
                    {
                        Fact: ArcRelayEvent.SignatureDamage hit,
                    })
                {
                    damage.Add(hit);
                }
                completed |= item.Payload is
                    GenericActorRuntimeObservation.EventPayload.ArcRelay
                    {
                        Fact: ArcRelayEvent.SignatureChanged
                        {
                            SignatureId: "falling-star",
                            Reason: "completed",
                        },
                    };
            }
        }

        Assert.True(declared);
        Assert.True(completed);
        Assert.Contains(damage, hit =>
            hit.TargetActorId.TeamId == 1 && hit.TargetActorId.UnitId == 0);
        Assert.DoesNotContain(damage, hit => hit.TargetActorId.TeamId == 0);
    }

    private static SceneResult Run(bool lethalVolley)
    {
        ActorResolvedMatchDefinition definition = Definition();
        ActorActionDefinition wait = definition.Rules.Actions.Single(value =>
            value.Kind == ActorActionKind.Wait);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (start, observation) => new GenericMindRuntimeDecisions(
                [
                    .. observation.Bodies.Select(body => Command(
                        start,
                        observation,
                        body,
                        wait,
                        lethalVolley)),
                ]));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 20_260_816UL);

        for (int tick = 0; tick < 120; tick++)
        {
            GenericActorMatchStepResult step = session.Step();
            bool decisive = lethalVolley
                ? step.AuthoritativeEvents.Any(value => value.Payload is
                    GenericActorRuntimeObservation.EventPayload.ArcRelay
                    {
                        Fact: ArcRelayEvent.CoreDropped
                        {
                            Kind: ArcRelayEvent.CoreDropKind.Destruction,
                        },
                    })
                : step.AuthoritativeEvents.Any(value => value.Payload is
                    GenericActorRuntimeObservation.EventPayload.ArcRelay
                    {
                        Fact: ArcRelayEvent.SignatureChanged
                        {
                            SignatureId: "arc-toss",
                            Reason: "launched",
                        },
                    });
            if (decisive)
            {
                return new SceneResult(
                    step.AuthoritativeEvents,
                    step.PostState);
            }
        }

        throw new Xunit.Sdk.XunitException(
            "The timing scene never reached its Toss/combat boundary.");
    }

    private static GenericMindCommand Command(
        GenericMindRuntimeStart start,
        GenericMindRuntimeObservation observation,
        GenericMindRuntimeObservation.ObservedBodyState body,
        ActorActionDefinition wait,
        bool lethalVolley)
    {
        var arc = Assert.IsType<
            GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                observation.Mode);
        ArcRelayCoreState? carried = arc.VisibleCores.FirstOrDefault(core =>
            core.CarrierActorId is { TeamId: 0, UnitId: 0 });

        if (start.TeamId == 0 && body.ActorId.UnitId == 0)
        {
            GenericActorRuntimeActionLegality? toss = body.ActionLegalities
                .FirstOrDefault(value =>
                    value.ActionId == "arc-toss" && value.Available);
            if (toss is not null)
            {
                Position target = toss.Constraints.OfType<
                        GenericActorRuntimeActionLegality.ArgumentConstraint
                            .PositionTargetConstraint>()
                    .Single().AllowedValues[0];
                return NewCommand(
                    body,
                    toss.ActionId,
                    toss.ActionCode,
                    [new GenericActorRuntimeActionArgument
                        .PositionTargetArgument(target)]);
            }
        }

        bool shouldDeclare = start.TeamId == 1
            && carried is not null
            && carried.NextRelocationTick == observation.Tick + 1
            && (lethalVolley || body.ActorId.UnitId == 0);
        if (shouldDeclare)
        {
            GenericActorRuntimeActionLegality shoot = body.ActionLegalities
                .Single(value =>
                    value.ActionId == ArcRelayH0Definition.ShootActionId);
            ProjectileHeading heading = Heading(
                body.Position,
                CarrierPosition);
            ImmutableArray<ProjectileHeading> allowed = shoot.Constraints
                .OfType<GenericActorRuntimeActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint>()
                .Single().AllowedValues;
            if (shoot.Available && allowed.Contains(heading))
            {
                return NewCommand(
                    body,
                    shoot.ActionId,
                    shoot.ActionCode,
                    [new GenericActorRuntimeActionArgument
                        .ProjectileHeadingArgument(heading)]);
            }
        }

        return NewCommand(body, wait.Id, wait.Code, []);
    }

    private static GenericMindCommand NewCommand(
        GenericMindRuntimeObservation.ObservedBodyState body,
        string actionId,
        int actionCode,
        ImmutableArray<GenericActorRuntimeActionArgument> arguments) =>
        new(
            body.ActorId.UnitId,
            body.ActorId.LifeId,
            actionId,
            actionCode,
            arguments);

    private static int IndexOf(
        ImmutableArray<GenericActorAuthoritativeEvent> events,
        Func<GenericActorRuntimeObservation.EventPayload, bool> predicate)
    {
        for (int index = 0; index < events.Length; index++)
        {
            if (predicate(events[index].Payload))
                return index;
        }
        return -1;
    }

    private static ProjectileHeading Heading(Position from, Position to) =>
        (Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y)) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => throw new InvalidOperationException("Bodies overlap."),
        };

    private static ActorResolvedMatchDefinition Definition(
        bool fallingStarScene = false)
    {
        string[] teamZeroClasses =
        [
            ArcRelayLaunchClassIds.Relay,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Minesmith,
        ];
        string[] teamOneClasses =
        [
            ArcRelayLaunchClassIds.Longshot,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Sunder,
            ArcRelayLaunchClassIds.Repulsor,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Mason,
        ];
        Position[] teamZero =
        [
            CarrierPosition,
            new(2, 1), new(2, 2), new(2, 3), new(2, 4), new(2, 5),
            new(2, 6), new(2, 7),
        ];
        Position[] teamOne =
        [
            fallingStarScene ? new(3, 5) : new(9, 3),
            new(10, 3), new(11, 3), new(9, 4),
            new(11, 4), new(9, 5), new(10, 5), new(11, 5),
        ];

        var slots = ImmutableArray.CreateBuilder<PublicUnitSlot>();
        var lives = ImmutableArray.CreateBuilder<PublicInitialLife>();
        var spawns = ImmutableArray.CreateBuilder<InitialSpawnDefinition>();
        var deployed = ImmutableArray.CreateBuilder<InitialLifeDeployment>();
        var lifecycle = ImmutableArray.CreateBuilder<
            ActorUnitSlotLifecycleAssignmentDefinition>();
        foreach ((int teamId, string[] classes, Position[] positions) in
                 new[]
                 {
                     (0, teamZeroClasses, teamZero),
                     (1, teamOneClasses, teamOne),
                 })
        {
            for (int unitId = 0; unitId < classes.Length; unitId++)
            {
                string formId = ArcRelayH0Definition.FormPrefix
                    + classes[unitId];
                string spawnId = $"team-{teamId}-unit-{unitId}";
                Direction facing = FacingToward(
                    positions[unitId],
                    CarrierPosition,
                    teamId);
                slots.Add(new PublicUnitSlot(
                    teamId, unitId, teamId, classes[unitId]));
                lives.Add(new PublicInitialLife(
                    teamId, unitId, 0, formId));
                spawns.Add(new InitialSpawnDefinition(
                    spawnId,
                    positions[unitId],
                    facing));
                deployed.Add(new InitialLifeDeployment(
                    teamId, unitId, 0, formId, spawnId));
                lifecycle.Add(new ActorUnitSlotLifecycleAssignmentDefinition(
                    teamId,
                    unitId,
                    ArcRelayH0Definition.LifecycleProfilePrefix
                        + classes[unitId],
                    initialGeneration: 0,
                    [formId],
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind.ActiveAtTickZero,
                    unlockTick: null,
                    assignedRespawnSpawnId: spawnId));
            }
        }

        var topology = new PublicMatchTopology
        {
            Teams = [new PublicScoringTeam(0), new PublicScoringTeam(1)],
            Participants =
            [
                new PublicParticipant(0, 0),
                new PublicParticipant(1, 1),
            ],
            UnitSlots = slots.ToImmutable(),
            InitialLives = lives.ToImmutable(),
        };
        return new ActorResolvedMatchDefinition(
            ArcRelayH0Definition.CreateRules(ArcRelayLoopProfile.Tapline7),
            Map(spawns.ToImmutable()),
            new HeadToHeadMatchFormatDefinition(),
            topology,
            new InitialDeploymentDefinition(
                spawns.ToImmutable(),
                deployed.ToImmutable()),
            lifecycle.ToImmutable(),
            [
                new(0, ArcRelayH0Definition.ReactorRoleId, "reactor-west",
                    Direction.East),
                new(0, ArcRelayH0Definition.HomePadRoleId, "home-west",
                    Direction.East),
                new(1, ArcRelayH0Definition.ReactorRoleId, "reactor-east",
                    Direction.West),
                new(1, ArcRelayH0Definition.HomePadRoleId, "home-east",
                    Direction.West),
            ],
            new ArcRelayActorModeMapBindingDefinition(
                ["well-centre", "well-north", "well-south"],
                ArcRelayH0Definition.ReactorRoleId,
                ArcRelayH0Definition.HomePadRoleId),
            ActorMatchCapabilityVersions.Mind);
    }

    private static ActorMapDefinition Map(
        ImmutableArray<InitialSpawnDefinition> spawns)
    {
        const int width = 21;
        const int height = 9;
        var rows = ImmutableArray.CreateBuilder<string>();
        for (int y = 0; y < height; y++)
        {
            char[] row = new string('.', width).ToCharArray();
            for (int x = 0; x < width; x++)
            {
                if (y == 0 || y == height - 1 || x == 0 || x == width - 1)
                    row[x] = '#';
            }
            rows.Add(new string(row));
        }
        Position[] required =
        [
            new(10, 1), CarrierPosition, new(10, 7),
            new(1, 4), new(19, 4),
        ];
        return new ActorMapDefinition(
            "arc-signature-activation-timing",
            version: 1,
            rows.ToImmutable(),
            [
                .. spawns.Select(spawn =>
                    new ActorMapSpawnAnchorDefinition(
                        spawn,
                        [ActorMovementLayer.Ground])),
            ],
            [
                Region("well-north", new Position(10, 1)),
                Region("well-centre", CarrierPosition),
                Region("well-south", new Position(10, 7)),
                Region("reactor-west", new Position(1, 4)),
                Region("reactor-east", new Position(19, 4)),
                Region("home-west", new Position(1, 3)),
                Region("home-east", new Position(19, 5)),
            ],
            [
                new ActorMapTileTagDefinition(
                    "arc-required-tiles",
                    ActorMapTileTagDefinition.TileTagKind
                        .SignaturePlacementForbidden,
                    [.. required]),
            ]);
    }

    private static Direction FacingToward(
        Position from,
        Position target,
        int teamId)
    {
        int dx = Math.Sign(target.X - from.X);
        int dy = Math.Sign(target.Y - from.Y);
        if (dx != 0)
            return dx > 0 ? Direction.East : Direction.West;
        if (dy != 0)
            return dy > 0 ? Direction.South : Direction.North;
        return teamId == 0 ? Direction.East : Direction.West;
    }

    private static ActorMapRegionDefinition Region(
        string id,
        Position position) =>
        new(id, ActorMapRegionDefinition.RegionKind.Objective, [position]);

    private sealed record SceneResult(
        ImmutableArray<GenericActorAuthoritativeEvent> Events,
        GenericActorWorldSnapshot PostState);
}
