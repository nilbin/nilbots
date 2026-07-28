using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class GenericActorMatchActorTurnTests
{
    [Fact]
    public void DeathmatchEvidenceRejectsSubstitutedAcceptedOrValidatedActions()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 8_071);

        GenericDeathmatchTickStart tickStart = session.PrepareTick();
        session.Step(tickStart.Observations);
        GenericActorMatchActorTurn original =
            session.Chronology.Ticks[0].ActorTurns[0];
        ActorActionDefinition rotate = definition.Rules.Actions.Single(
            action => action.Kind == ActorActionKind.Rotation);
        var substituted =
            new GenericActorRuntimeActionResolution.ResolvedAction(
                rotate.Id,
                rotate.Code,
                [
                    new GenericActorRuntimeActionArgument.DirectionArgument(
                        Direction.North),
                ]);

        GenericActorMatchActorTurn accepted = Copy(
            original,
            original.ActionResolution with
            {
                AcceptedAction = substituted,
            });
        GenericActorMatchActorTurn validated = Copy(
            original,
            original.ActionResolution with
            {
                ValidatedAction = substituted,
            });

        Assert.Throws<ArgumentException>(() =>
            accepted.ValidateAgainst(session.MatchDescriptor));
        Assert.Throws<ArgumentException>(() =>
            validated.ValidateAgainst(session.MatchDescriptor));
    }

    [Fact]
    public void FaultEvidenceRequiresTheFormsCanonicalWait()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head");
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, _) => GenericDeathmatchSessionTestFixture.Unknown());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 8_072);

        GenericDeathmatchTickStart tickStart = session.PrepareTick();
        session.Step(tickStart.Observations);
        GenericActorMatchActorTurn original =
            session.Chronology.Ticks[0].ActorTurns[0];
        ActorActionDefinition rotate = definition.Rules.Actions.Single(
            action => action.Kind == ActorActionKind.Rotation);
        var substituted =
            new GenericActorRuntimeActionResolution.ResolvedAction(
                rotate.Id,
                rotate.Code,
                [
                    new GenericActorRuntimeActionArgument.DirectionArgument(
                        Direction.North),
                ]);
        GenericActorMatchActorTurn tampered = Copy(
            original,
            original.ActionResolution with
            {
                AcceptedAction = substituted,
                ValidatedAction = substituted,
            });

        Assert.Throws<ArgumentException>(() =>
            tampered.ValidateAgainst(session.MatchDescriptor));
    }

    [Fact]
    public void ResolutionEqualityIsSemanticAcrossImmutableArrayCopies()
    {
        var argument =
            new GenericActorRuntimeActionArgument.DirectionArgument(
                Direction.East);
        var leftAction =
            new GenericActorRuntimeActionResolution.ResolvedAction(
                "move",
                1,
                ImmutableArray.Create<
                    GenericActorRuntimeActionArgument>(argument));
        var rightAction =
            new GenericActorRuntimeActionResolution.ResolvedAction(
                "move",
                1,
                ImmutableArray.Create<
                    GenericActorRuntimeActionArgument>(
                        new GenericActorRuntimeActionArgument
                            .DirectionArgument(Direction.East)));
        var left = new GenericActorRuntimeActionResolution(
            leftAction,
            leftAction,
            leftAction,
            GenericActorRuntimeActionResolution.ActionOutcome.Success,
            RuntimeFault: null);
        var right = new GenericActorRuntimeActionResolution(
            rightAction,
            rightAction,
            rightAction,
            GenericActorRuntimeActionResolution.ActionOutcome.Success,
            RuntimeFault: null);

        Assert.NotEqual(left, right);
        Assert.True(
            GenericActorMatchActorTurn
                .ActionResolutionsSemanticallyEqual(left, right));
    }

    private static GenericActorMatchActorTurn Copy(
        GenericActorMatchActorTurn source,
        GenericActorRuntimeActionResolution resolution) =>
        new(
            source.Tick,
            source.ParticipantId,
            source.ActorId,
            source.Observation,
            source.SubmittedDecision,
            resolution);
}
