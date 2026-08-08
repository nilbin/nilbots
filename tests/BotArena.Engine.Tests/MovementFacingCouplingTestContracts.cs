using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Builds focused movement-facing-coupling contracts by rewriting only the
/// movement profile of the shared deathmatch fixture, so a coupling test
/// changes exactly one mechanic against a known arena and spawn pose.
/// </summary>
internal static class MovementFacingCouplingTestContracts
{
    /// <summary>
    /// Head-to-head deathmatch on the 9x7 shared arena. Team 0's life spawns
    /// at (1,3) facing East with a wall due West; team 1's spawns at (7,3)
    /// facing West.
    /// </summary>
    public static ActorResolvedMatchDefinition Deathmatch(
        ActorMovementFacingCoupling coupling,
        int faultsAllowedBeforeDisqualification = 0) =>
        WithCoupling(
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 4,
                    FaultsAllowedBeforeDisqualification =
                        faultsAllowedBeforeDisqualification,
                }),
            coupling);

    public static ActorResolvedMatchDefinition WithCoupling(
        ActorResolvedMatchDefinition source,
        ActorMovementFacingCoupling coupling)
    {
        ActorRulesDefinition rules = source.Rules;
        var rewritten = new ActorRulesDefinition(
            rules.RulesetId,
            rules.Limits,
            rules.SeedMechanics,
            rules.GameMode,
            rules.Lifecycle,
            rules.Forms,
            rules.MovementProfiles.Select(profile =>
                new ActorMovementProfileDefinition(
                    profile.Id,
                    profile.MovementLayer,
                    coupling)),
            rules.VisionProfiles,
            rules.AttackProfiles,
            rules.Actions,
            rules.FabricationTransitions,
            rules.SameLifeTransitions,
            rules.ReplicationTransitions,
            rules.TeamPerception,
            rules.Collisions,
            rules.TickResolution);
        return new ActorResolvedMatchDefinition(
            rewritten,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    public static GenericActorMatchSession Session(
        ActorResolvedMatchDefinition definition,
        Func<
            GenericActorRuntimeStart,
            GenericActorRuntimeObservation,
            GenericActorRuntimeDecision>? decide = null) =>
        new(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                GenericDeathmatchSessionTestFixture.Factories(
                    definition,
                    decide)),
            matchSeed: 4_242);

    /// <summary>The team-0 life, which spawns at (1,3) facing East.</summary>
    public static GenericActorWorldSnapshot.LifeSnapshot MoverAfter(
        GenericActorMatchStepResult step) =>
        step.PostState.ActiveLives.Single(life => life.ActorId.TeamId == 0);

    public static GenericActorRuntimeObservation.EventPayload.Movement
        MovementOf(GenericActorMatchStepResult step, int teamId) =>
        step.Events
            .Select(item => item.Payload)
            .OfType<GenericActorRuntimeObservation.EventPayload.Movement>()
            .Single(payload => payload.ActorId.TeamId == teamId);

    public static GenericActorRuntimeObservation.EventPayload.MovementBlocked
        BlockedMovementOf(GenericActorMatchStepResult step, int teamId) =>
        step.Events
            .Select(item => item.Payload)
            .OfType<
                GenericActorRuntimeObservation.EventPayload.MovementBlocked>()
            .Single(payload => payload.ActorId.TeamId == teamId);

    /// <summary>
    /// The published Direction domain for one action, as an array so xUnit
    /// compares elements instead of ImmutableArray's reference equality.
    /// </summary>
    public static Direction[] AllowedDirections(
        GenericActorRuntimeObservation observation,
        string actionId) =>
        [
            .. observation.ActionLegalities
                .Single(legality => string.Equals(
                    legality.ActionId,
                    actionId,
                    StringComparison.Ordinal))
                .Constraints
                .OfType<
                    GenericActorRuntimeActionLegality.ArgumentConstraint
                        .DirectionConstraint>()
                .Single()
                .AllowedValues,
        ];
}
