using System.Text.Json;
using Sdk = BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class ArcRelayForwardCombatTests
{
    private static readonly string[] Composition =
    [
        ArcRelayLaunchClassIds.Towline,
        ArcRelayLaunchClassIds.Patchbay,
        ArcRelayLaunchClassIds.Kestrel,
        ArcRelayLaunchClassIds.Palisade,
        ArcRelayLaunchClassIds.Hush,
        ArcRelayLaunchClassIds.Lantern,
        ArcRelayLaunchClassIds.Mortar,
        ArcRelayLaunchClassIds.Nest,
    ];

    [Fact]
    public void CurrentProfileVersionsCombatWithoutMovingTheAcceptedMap()
    {
        ActorResolvedMatchDefinition historical = ArcRelayH0Definition.Create(
            Composition,
            Composition,
            loopProfile: ArcRelayLoopProfile.DepthCounterflow);
        ActorResolvedMatchDefinition current = ArcRelayH0Definition.Create(
            Composition,
            Composition,
            loopProfile: ArcRelayLoopProfile.Current);

        Assert.Equal(
            ActorContractFingerprint.ComputeMap(historical.Map),
            ActorContractFingerprint.ComputeMap(current.Map));
        using JsonDocument historicalContract = JsonDocument.Parse(
            ActorContractManifestSerializer.ToCanonicalJson(historical));
        using JsonDocument currentContract = JsonDocument.Parse(
            ActorContractManifestSerializer.ToCanonicalJson(current));
        Assert.Equal(
            historicalContract.RootElement.GetProperty("rules")
                .GetProperty("gameMode").GetRawText(),
            currentContract.RootElement.GetProperty("rules")
                .GetProperty("gameMode").GetRawText());
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(historical.Rules),
            ActorContractFingerprint.ComputeRules(current.Rules));
        Assert.Equal(
            "arc-relay-forward-combat-01",
            current.Rules.RulesetId);
        Assert.All(current.Rules.AttackProfiles, attack =>
        {
            Assert.False(attack.OmnidirectionalAim);
            Assert.Equal(1, attack.FacingAimHalfWidthSectors);
            Assert.Equal(
                ActorAttackProfileDefinition.AimInterpretationKind
                    .AbsoluteSubmittedEightWayHeadingWithinFacingConeFacingUnchanged,
                attack.AimInterpretation);
        });

        ActorActionDefinition strafe = current.Rules.Actions.Single(action =>
            action.Id == ArcRelayH0Definition.StrafeActionId);
        Assert.Equal(ActorActionKind.Movement, strafe.Kind);
        Assert.Equal(
            ActorMovementFacingCoupling.PreserveFacing,
            strafe.MovementFacingOverride);
        Assert.Contains(
            ArcRelayH0Definition.StrafeActionId,
            current.Rules.Forms.Single(form =>
                form.Id == ArcRelayH0Definition.FormPrefix
                    + ArcRelayLaunchClassIds.Kestrel).AllowedActionIds);
        Assert.DoesNotContain(
            ArcRelayH0Definition.StrafeActionId,
            current.Rules.Forms.Single(form =>
                form.Id == ArcRelayH0Definition.FormPrefix
                    + ArcRelayLaunchClassIds.Towline).AllowedActionIds);

        Sdk.GenericActorResolvedMatchContract sdk =
            Sdk.ActorCanonicalContractReader.Parse(
                ActorContractManifestSerializer.ToCanonicalJson(current));
        Assert.All(
            sdk.Rules.AttackProfiles,
            attack => Assert.Equal(1, attack.FacingAimHalfWidthSectors));
        Assert.Equal(
            Sdk.GenericActorRulesContract.MovementFacingCoupling.PreserveFacing,
            sdk.Rules.Actions.Single(action =>
                    action.Id == ArcRelayH0Definition.StrafeActionId)
                .MovementFacingOverride);
    }

    [Fact]
    public void BasicAttackMaskContainsOnlyTheThreeForwardHeadings()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            Composition,
            Composition,
            loopProfile: ArcRelayLoopProfile.Current);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(definition);
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 20_260_803UL);

        session.Step();

        GenericMindRuntimeObservation west = factories.Values
            .SelectMany(factory => factory.Observations)
            .Single(observation => observation.TeamId == 0);
        GenericMindRuntimeObservation east = factories.Values
            .SelectMany(factory => factory.Observations)
            .Single(observation => observation.TeamId == 1);
        Assert.All(west.Bodies, body => Assert.Equal(
            [ProjectileHeading.NorthEast, ProjectileHeading.East,
                ProjectileHeading.SouthEast],
            AttackHeadings(body)));
        Assert.All(east.Bodies, body => Assert.Equal(
            [ProjectileHeading.SouthWest, ProjectileHeading.West,
                ProjectileHeading.NorthWest],
            AttackHeadings(body)));
    }

    [Fact]
    public void SubmittedRearBasicAttackOutsideLegalityMaskFaultsBeforeLaunch()
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            Composition,
            Composition,
            loopProfile: ArcRelayLoopProfile.Current);
        ActorActionDefinition wait = definition.Rules.Actions.Single(action =>
            action.Kind == ActorActionKind.Wait);
        ActorActionDefinition shoot = definition.Rules.Actions.Single(action =>
            action.Id == ArcRelayH0Definition.ShootActionId);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) => new GenericMindRuntimeDecisions(
                [
                    .. observation.Bodies.Select(body =>
                        body.ActorId.TeamId == 0 && body.ActorId.UnitId == 0
                            ? Command(body, shoot, ProjectileHeading.West)
                            : Command(body, wait, null)),
                ]));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 20_260_805UL);

        GenericActorMatchStepResult step = session.Step();
        GenericActorMatchActorResolution resolution = step.ActionResolutions
            .Single(value => value.ActorId.TeamId == 0
                && value.ActorId.UnitId == 0);
        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Faulted,
            resolution.Resolution.Outcome);
        // Values outside the advertised legality mask are deliberately not
        // projected into the typed submitted-action chronology.
        Assert.Null(resolution.Resolution.SubmittedAction);
        Assert.Equal(ActorActionKind.Wait, definition.Rules.Actions.Single(
            action => action.Id == resolution.Resolution.ValidatedAction.ActionId)
            .Kind);
        Assert.Empty(step.ProjectileTraversals);
    }

    [Fact]
    public void SwiftChoosesTurnWithMoveOrFacingPreservingStrafe()
    {
        string[] swiftComposition =
        [
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Hush,
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Mortar,
            ArcRelayLaunchClassIds.Nest,
        ];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            swiftComposition,
            swiftComposition,
            loopProfile: ArcRelayLoopProfile.Current);
        ActorActionDefinition wait = definition.Rules.Actions.Single(action =>
            action.Kind == ActorActionKind.Wait);
        ActorActionDefinition move = definition.Rules.Actions.Single(action =>
            action.Id == ArcRelayH0Definition.MoveActionId);
        ActorActionDefinition strafe = definition.Rules.Actions.Single(action =>
            action.Id == ArcRelayH0Definition.StrafeActionId);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) => new GenericMindRuntimeDecisions(
                [
                    .. observation.Bodies.Select(body => body.ActorId.TeamId != 0
                        || body.ActorId.UnitId is not (2 or 5)
                        ? Command(body, wait, null)
                        : body.ActorId.UnitId == 2
                            ? Command(body, move, ProjectileHeading.SouthWest)
                            : Command(body, strafe,
                                ProjectileHeading.NorthWest)),
                ]));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 20_260_804UL);

        GenericActorMatchStepResult step = session.Step();
        GenericActorWorldSnapshot.LifeSnapshot turned =
            step.PostState.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0 && life.ActorId.UnitId == 2);
        GenericActorWorldSnapshot.LifeSnapshot strafed =
            step.PostState.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0 && life.ActorId.UnitId == 5);

        Assert.Equal(new Position(2, 10), turned.Position);
        Assert.Equal(Direction.West, turned.Facing);
        Assert.Equal(new Position(2, 12), strafed.Position);
        Assert.Equal(Direction.East, strafed.Facing);
    }

    [Theory]
    [InlineData(ProjectileHeading.NorthEast, Direction.East)]
    [InlineData(ProjectileHeading.North, Direction.East)]
    [InlineData(ProjectileHeading.NorthWest, Direction.West)]
    [InlineData(ProjectileHeading.West, Direction.West)]
    public void StandardCombatStrafeOnlyReorientsRearTravel(
        ProjectileHeading heading,
        Direction expected)
    {
        Assert.Equal(
            expected,
            ActorMovementFacingResolver.AfterSuccessfulMove(
                Direction.East,
                heading,
                ActorMovementFacingCoupling.CombatStrafe));
    }

    private static ProjectileHeading[] AttackHeadings(
        GenericMindRuntimeObservation.ObservedBodyState body) =>
    [
        .. body.ActionLegalities.Single(action => action.ActionId
                == ArcRelayH0Definition.ShootActionId).Constraints
            .OfType<GenericActorRuntimeActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint>()
            .Single()
            .AllowedValues,
    ];

    private static GenericMindCommand Command(
        GenericMindRuntimeObservation.ObservedBodyState body,
        ActorActionDefinition action,
        ProjectileHeading? heading) =>
        new(
            body.ActorId.UnitId,
            body.ActorId.LifeId,
            action.Id,
            action.Code,
            heading is { } value
                ? [new GenericActorRuntimeActionArgument
                    .ProjectileHeadingArgument(value)]
                : []);
}
