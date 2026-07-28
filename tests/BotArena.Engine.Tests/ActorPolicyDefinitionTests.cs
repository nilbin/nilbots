namespace BotArena.Engine.Tests;

public sealed class ActorPolicyDefinitionTests
{
    [Fact]
    public void TeamPerceptionAndAlliedProjectileContactAreClosed()
    {
        var perception = new ActorTeamPerceptionDefinition(
            ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion);
        ActorCollisionDefinition collisions = Collisions(
            ActorCollisionDefinition.AlliedProjectileContactKind
                .BlockWithoutDamage);

        Assert.Equal(
            ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion,
            perception.Kind);
        Assert.Equal(
            ActorTeamPerceptionDefinition.SameTickDecisionSharingKind.None,
            perception.SameTickDecisionSharing);
        Assert.Equal(
            ActorCollisionDefinition.AlliedProjectileContactKind
                .BlockWithoutDamage,
            collisions.AlliedProjectileContact);
        Assert.Equal(
            ActorCollisionDefinition.MovementDestinationProjectileResultKind
                .NonPassingContactBlocksAtOriginConsumesProjectileByIdAndQueuesDamage,
            collisions.MovementDestinationProjectileResult);
        Assert.Equal(
            ActorCollisionDefinition.AlliedMovementDestinationOverrideKind
                .PassThroughDoesNotBlockOrConsumeOtherwiseUseContactPolicy,
            collisions.AlliedMovementDestinationOverride);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorTeamPerceptionDefinition(
                (ActorTeamPerceptionDefinition.PerceptionKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Collisions(
                (ActorCollisionDefinition.AlliedProjectileContactKind)99));
        Assert.Throws<ArgumentException>(() =>
            new ActorCollisionDefinition(
                actorsBlockWalls: false,
                actorsBlockActors: true,
                sameDestinationMovesBlockAll: true,
                swapMovesBlocked: true,
                followingVacatedActorAllowed: false,
                projectilesBlockMovement: true,
                movingOntoProjectileCausesHit: true,
                wallsConsumeProjectiles: true,
                projectilesIgnoreFiringLife: true,
                projectilesStopOnFirstEnemyActor: true,
                projectilesCollideWithProjectiles: false,
                ActorCollisionDefinition.AlliedProjectileContactKind
                    .PassThrough));
    }

    [Fact]
    public void TickResolutionAcceptsOnlyTheDisclosedSupportedOrder()
    {
        ActorTickResolutionPhase[] phases =
            ActorTickResolutionDefinition
                .CreateSupportedPhases()
                .ToArray();
        var definition = new ActorTickResolutionDefinition(
            observationsUsePreTickState: true,
            decisionsResolveAsJointStep: true,
            ActorDamageResolutionDefinition.CanonicalJointV1,
            phases);

        phases[0] = ActorTickResolutionPhase.ResolveMatchCompletion;

        Assert.Equal(
            ActorTickResolutionPhase.ResolveTickStartLifecycle,
            definition.Phases[0]);
        Assert.Equal(
            ActorTickResolutionPhase.ResolveMatchCompletion,
            definition.Phases[^1]);
        Assert.Equal(
            ActorTickResolutionDefinition.MovementActionResolutionKind
                .SubmittedAbsoluteCardinalOneTileFacingUnchanged,
            definition.MovementActionResolution);
        Assert.Equal(
            ActorTickResolutionDefinition.RotationActionResolutionKind
                .SetFacingToSubmittedAbsoluteCardinalPositionUnchanged,
            definition.RotationActionResolution);
        Assert.Equal(
            ActorTickResolutionDefinition.ActionFaultCountingKind
                .OnlyFaultedOutcomeIncrementsParticipantCounter,
            definition.ActionFaultCounting);
        Assert.Equal(
            ActorTickResolutionDefinition.MatchCompletionPrecedenceKind
                .FaultEligibilityShortCircuitThenModeEarlyThenEligibleTimeout,
            definition.MatchCompletionPrecedence);
        Assert.Contains(
            ActorTickResolutionPhase.ResolveFaultEligibilityCompletion,
            definition.Phases);
        Assert.Throws<ArgumentException>(() =>
            new ActorTickResolutionDefinition(
                observationsUsePreTickState: true,
                decisionsResolveAsJointStep: true,
                ActorDamageResolutionDefinition.CanonicalJointV1,
                definition.Phases.Reverse().ToArray()));
        Assert.Throws<ArgumentException>(() =>
            new ActorTickResolutionDefinition(
                observationsUsePreTickState: false,
                decisionsResolveAsJointStep: true,
                ActorDamageResolutionDefinition.CanonicalJointV1,
                definition.Phases));
    }

    private static ActorCollisionDefinition Collisions(
        ActorCollisionDefinition.AlliedProjectileContactKind alliedContact) =>
        new(
            actorsBlockWalls: true,
            actorsBlockActors: true,
            sameDestinationMovesBlockAll: true,
            swapMovesBlocked: true,
            followingVacatedActorAllowed: false,
            projectilesBlockMovement: true,
            movingOntoProjectileCausesHit: true,
            wallsConsumeProjectiles: true,
            projectilesIgnoreFiringLife: true,
            projectilesStopOnFirstEnemyActor: true,
            projectilesCollideWithProjectiles: false,
            alliedContact);
}
