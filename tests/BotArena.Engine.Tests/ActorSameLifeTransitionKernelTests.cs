using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class ActorSameLifeTransitionKernelTests
{
    [Theory]
    [InlineData(
        ActorSameLifeHealthDefinition.HealthPolicyKind
            .PreserveCurrentCappedToTargetMaximum,
        0,
        2)]
    [InlineData(
        ActorSameLifeHealthDefinition.HealthPolicyKind
            .AddFlatCappedToTargetMaximum,
        2,
        4)]
    [InlineData(
        ActorSameLifeHealthDefinition.HealthPolicyKind.SetToTargetMaximum,
        0,
        5)]
    [InlineData(
        ActorSameLifeHealthDefinition.HealthPolicyKind
            .PreserveRatioFloorMinimumOne,
        0,
        3)]
    public void CompletionEvaluatesEveryHealthPolicyFromCurrentHealth(
        ActorSameLifeHealthDefinition.HealthPolicyKind policy,
        int flatHealthGain,
        int expectedHealth)
    {
        ActorResolvedMatchDefinition definition = Definition(
            new GenericDeathmatchSessionTestFixture.SameLifeOptions
            {
                DurationTicks = 1,
                HealthPolicy = policy,
                FlatHealthGain = flatHealthGain,
                TargetMaxHealth = 5,
            });
        var kernel = new ActorSameLifeTransitionKernel(definition);
        ActorSameLifeTransitionActorSnapshot source =
            Source(definition) with
            {
                Health = 2,
            };

        ActorSameLifeTransitionReservation reservation =
            Queue(kernel, source);
        ActorSameLifeTransitionCompletion completion = kernel.Complete(
            reservation.DueTick,
            reservation,
            source with
            {
                PendingSameLifeTransition = reservation,
            });

        Assert.Equal(
            ActorSameLifeTransitionCompletion.CompletionOutcomeKind.Completed,
            completion.Outcome);
        Assert.Equal(expectedHealth, completion.State!.Health);
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, null)]
    public void CompletionPreservesCooldownAndNormalizesTargetEnergy(
        bool targetHasAttack,
        int? expectedEnergy)
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture
                .DefinitionWithSameLifeTransition(
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 3,
                        MaxEnergy = 5,
                        AttackEnergyCost = 1,
                    },
                    new GenericDeathmatchSessionTestFixture.SameLifeOptions
                    {
                        DurationTicks = 1,
                        TargetHasAttack = targetHasAttack,
                        TargetMaxEnergy = targetHasAttack ? 2 : null,
                    });
        var kernel = new ActorSameLifeTransitionKernel(definition);
        ActorSameLifeTransitionActorSnapshot source =
            Source(definition) with
            {
                Cooldown = 4,
                Energy = 4,
            };
        ActorSameLifeTransitionReservation reservation =
            Queue(kernel, source);

        ActorSameLifeTransitionCompletion completion = kernel.Complete(
            reservation.DueTick,
            reservation,
            source with
            {
                PendingSameLifeTransition = reservation,
            });

        Assert.Equal(4, completion.State!.Cooldown);
        Assert.Equal(expectedEnergy, completion.State.Energy);
    }

    [Fact]
    public void CompletionRequiresTheExactPendingReservation()
    {
        ActorResolvedMatchDefinition definition = Definition(
            new GenericDeathmatchSessionTestFixture.SameLifeOptions
            {
                DurationTicks = 1,
            });
        var kernel = new ActorSameLifeTransitionKernel(definition);
        ActorSameLifeTransitionActorSnapshot source = Source(definition);
        ActorSameLifeTransitionReservation reservation =
            Queue(kernel, source);
        ActorSameLifeTransitionReservation differentPending =
            reservation with
            {
                OperationId = "different-operation",
            };

        ActorSameLifeTransitionCompletion completion = kernel.Complete(
            reservation.DueTick,
            reservation,
            source with
            {
                PendingSameLifeTransition = differentPending,
            });

        Assert.Equal(
            ActorSameLifeTransitionCompletion.CompletionOutcomeKind.Cancelled,
            completion.Outcome);
        Assert.Equal(
            ActorSameLifeTransitionCompletion.CancellationReason
                .SourceStateChanged,
            completion.Reason);
        Assert.Null(completion.State);
    }

    [Fact]
    public void ChronologyHistoryRejectsReversingAnIrreversibleCompletion()
    {
        ActorResolvedMatchDefinition definition = Definition(
            new GenericDeathmatchSessionTestFixture.SameLifeOptions
            {
                DurationTicks = 1,
                IncludeReverseRoute = true,
                IrreversibleForLife = true,
            });
        ActorIdentity actor = Source(definition).ActorId;
        ActorFormTransitionDefinition forward =
            definition.Rules.SameLifeTransitions
                .OfType<ActorFormTransitionDefinition>()
                .Single(transition =>
                    transition.TransitionId == "anchor-mobile");
        ActorFormTransitionDefinition reverse =
            definition.Rules.SameLifeTransitions
                .OfType<ActorFormTransitionDefinition>()
                .Single(transition =>
                    transition.TransitionId == "unanchor-mobile");
        var history =
            new Dictionary<ActorIdentity, HashSet<string>>();

        GenericActorMatchChronology
            .ValidateAndAdvanceIrreversibleSameLifeHistory(
                definition,
                [CompletionEvent(actor, forward, tick: 0)],
                history,
                "events");

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            GenericActorMatchChronology
                .ValidateAndAdvanceIrreversibleSameLifeHistory(
                    definition,
                    [CompletionEvent(actor, reverse, tick: 1)],
                    history,
                    "events"));
        Assert.Contains(
            "irreversible",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static ActorResolvedMatchDefinition Definition(
        GenericDeathmatchSessionTestFixture.SameLifeOptions options) =>
        GenericDeathmatchSessionTestFixture
            .DefinitionWithSameLifeTransition(
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                    MaxHealth = 3,
                },
                options);

    private static ActorSameLifeTransitionReservation Queue(
        ActorSameLifeTransitionKernel kernel,
        ActorSameLifeTransitionActorSnapshot source)
    {
        ActorFormTransitionDefinition transition =
            kernel.MatchRoutes("mobile", "transform", "anchored").Single();
        ActorSameLifeTransitionQueueOutcome queued = kernel.Queue(
            tick: 0,
            new ActorSameLifeTransitionRequest(
                source.ActorId,
                transition.TransitionId,
                "test-operation"),
            source);
        Assert.Null(queued.Reason);
        return Assert.IsType<ActorSameLifeTransitionReservation>(
            queued.Reservation);
    }

    private static ActorSameLifeTransitionActorSnapshot Source(
        ActorResolvedMatchDefinition definition)
    {
        InitialLifeDeployment deployment =
            definition.InitialDeployment.Lives.Single(life =>
                life.TeamId == 0);
        InitialSpawnDefinition spawn =
            definition.InitialDeployment.Spawns.Single(value =>
                value.SpawnId == deployment.SpawnId);
        ActorFormDefinition form = definition.Rules.Forms.Single(value =>
            value.Id == deployment.FormId);
        ActorAttackProfileDefinition? attack =
            form.AttackProfileId is string attackProfileId
                ? definition.Rules.AttackProfiles.Single(value =>
                    value.Id == attackProfileId)
                : null;
        return new ActorSameLifeTransitionActorSnapshot(
            new ActorIdentity(
                deployment.TeamId,
                deployment.UnitId,
                deployment.LifeId),
            ParticipantId: 10,
            Generation: 0,
            deployment.FormId,
            spawn.Position,
            spawn.Facing,
            form.MaxHealth,
            Cooldown: 0,
            attack?.MaxEnergy is > 0 ? attack.MaxEnergy : null,
            HasPriorSameLifeTransition: false,
            IrreversibleReturnFormIds: ImmutableArray<string>.Empty,
            PendingSameLifeTransition: null);
    }

    private static GenericActorAuthoritativeEvent CompletionEvent(
        ActorIdentity actor,
        ActorFormTransitionDefinition transition,
        int tick) =>
        new(
            $"same-life-completion:{tick}",
            tick,
            globalOrdinal: tick,
            sourceOrdinal: 0,
            GenericActorRuntimeObservation.EventKind
                .FormTransitionCompleted,
            new GenericActorRuntimeObservation.EventPayload.FormTransition(
                actor,
                transition.TransitionId,
                $"operation:{tick}",
                transition.SourceFormId,
                transition.TargetFormId,
                StartedTick: tick,
                DueTick: tick),
            new GenericActorAuthoritativeEvent.Audience.Spatial(
                new Position(1, 3)));
}
