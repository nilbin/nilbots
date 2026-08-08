using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Shared plumbing for the class-skill arms: canonical pairs, a scripted
/// in-process session driver, and the small decision helpers the skill probes
/// need. Skills are class capabilities, so every fixture here starts from a
/// resolved class cell rather than from a synthetic contract.
/// </summary>
internal static class FrontlineLabsSkillArmTestFixture
{
    public const ulong Seed = 7UL;

    public static FrontlineLabsClassDefinition[] Classes { get; } =
    [
        FrontlineLabsClassDefinition.Bulwark,
        FrontlineLabsClassDefinition.Fabricator,
        FrontlineLabsClassDefinition.Striker,
    ];

    public static IEnumerable<(
        FrontlineLabsClassDefinition TeamZero,
        FrontlineLabsClassDefinition TeamOne)> CanonicalPairs()
    {
        for (int first = 0; first < Classes.Length; first++)
        {
            for (int second = first; second < Classes.Length; second++)
                yield return (Classes[first], Classes[second]);
        }
    }

    public static ActorResolvedMatchDefinition Arm(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsSkillKit skills) =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.None,
            (teamZero, teamOne),
            skills: skills);

    /// <summary>
    /// Runs a scripted match on one arm and returns the recorded chronology,
    /// which is what the causality validator has already accepted.
    /// </summary>
    public static GenericActorMatchChronology Run(
        ActorResolvedMatchDefinition definition,
        Func<
            GenericActorRuntimeStart,
            GenericActorRuntimeObservation,
            GenericActorRuntimeDecision> decide,
        ulong seed = Seed)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition, decide);
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            seed);
        session.Run();
        return session.Chronology;
    }

    public static GenericActorRuntimeDecision ShootStraight() =>
        new("shoot-straight", 105, [], null);

    public static GenericActorRuntimeDecision Mobilize() =>
        new("mobilize", 104, [], null);

    /// <summary>
    /// Row 13 is the map's other open corridor and — unlike row 7 — most of it
    /// is not tagged TransitionPlacementForbidden, so it is where a scripted
    /// probe can actually enter a stance. Walking there is six steps south
    /// then one step inward for either team.
    /// </summary>
    public const int StanceRowY = 13;

    /// <summary>
    /// Moves toward <paramref name="target"/> on the stance row, one axis at a
    /// time (south first, then along the row). Returns null once the life is
    /// already standing on the target tile.
    /// </summary>
    public static GenericActorRuntimeDecision? WalkTo(
        GenericActorRuntimeObservation observation,
        Position target)
    {
        Position self = observation.Self.Position;
        if (self.Y < target.Y)
            return GenericDeathmatchSessionTestFixture.Move(Direction.South);
        if (self.Y > target.Y)
            return GenericDeathmatchSessionTestFixture.Move(Direction.North);
        if (self.X < target.X)
            return GenericDeathmatchSessionTestFixture.Move(Direction.East);
        if (self.X > target.X)
            return GenericDeathmatchSessionTestFixture.Move(Direction.West);
        return null;
    }

    public static bool Allows(
        GenericActorRuntimeObservation observation,
        string actionId) =>
        observation.ActionLegalities.Any(legality =>
            string.Equals(
                legality.ActionId,
                actionId,
                StringComparison.Ordinal)
            && legality.Available);

    public static ImmutableArray<
            GenericActorRuntimeObservation.EventPayload.Attack>
        Attacks(GenericActorMatchTickFrame frame) =>
        [
            .. frame.Events
                .Where(item => item.Kind
                    == GenericActorRuntimeObservation.EventKind.Attack)
                .Select(item =>
                    (GenericActorRuntimeObservation.EventPayload.Attack)
                    item.Payload),
        ];

    public static ImmutableArray<
            GenericActorRuntimeObservation.EventPayload.ProjectileDeflected>
        Deflections(GenericActorMatchTickFrame frame) =>
        [
            .. frame.Events
                .Where(item => item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .ProjectileDeflected)
                .Select(item =>
                    (GenericActorRuntimeObservation.EventPayload
                        .ProjectileDeflected)item.Payload),
        ];

    /// <summary>
    /// Form-transition facts of one kind, in ordinal order, across both the
    /// tick-start and resolution boundaries — the automatic return can land on
    /// either, and a test that only looked at one would silently pass.
    /// </summary>
    public static ImmutableArray<
            GenericActorRuntimeObservation.EventPayload.FormTransition>
        Transitions(
            GenericActorMatchTickFrame frame,
            GenericActorRuntimeObservation.EventKind kind) =>
        [
            .. frame.TickStart.Events
                .Concat(frame.Events)
                .Where(item => item.Kind == kind)
                .OrderBy(item => item.Ordinal)
                .Select(item =>
                    (GenericActorRuntimeObservation.EventPayload
                        .FormTransition)item.Payload),
        ];

    public static bool IsAutomatic(
        GenericActorRuntimeObservation.EventPayload.FormTransition value) =>
        value.Reason
        == GenericActorRuntimeObservation.FormTransitionReason
            .AutomaticThresholdReturn;

    public static ImmutableArray<
            GenericActorRuntimeObservation.EventPayload.Damage>
        Damages(GenericActorMatchTickFrame frame) =>
        [
            .. frame.Events
                .Where(item => item.Kind
                    == GenericActorRuntimeObservation.EventKind.Damage)
                .Select(item =>
                    (GenericActorRuntimeObservation.EventPayload.Damage)
                    item.Payload),
        ];
}
