using System.Collections.Immutable;
using System.Reflection;

namespace BotArena.Engine.Tests;

public sealed class GenericActorChronologyFoundationTests
{
    [Fact]
    public void ParticipantSnapshotAndDescriptor_AreCanonicalAndFactoryFree()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.Deathmatch("free-for-all");
        GenericActorParticipantConfiguration[] configurations =
            Configurations(definition)
                .Reverse()
                .ToArray();

        ImmutableArray<GenericActorParticipantProvenance> snapshot =
            GenericActorParticipantProvenance.CreateCanonicalSnapshot(
                definition,
                configurations);
        GenericActorMatchDescriptor descriptor =
            GenericActorMatchDescriptor.Create(
                definition,
                9_007_199_254_740_993UL,
                configurations);

        Assert.Equal(
            [10, 20, 30, 40],
            snapshot.Select(participant => participant.ParticipantId));
        Assert.Equal(
            snapshot.ToArray(),
            descriptor.Participants.ToArray());
        Assert.Same(definition, descriptor.Definition);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(definition),
            descriptor.MatchContractFingerprint);
        Assert.Equal(9_007_199_254_740_993UL, descriptor.MatchSeed);
        Assert.Equal(
            BotArenaVersions.GenericActorEngineVersion,
            descriptor.EngineVersion);
        Assert.Equal(
            definition.CapabilityVersions.RuntimeProtocolVersion,
            descriptor.ActorRuntimeProtocolVersion);
        Assert.Equal(
            definition.CapabilityVersions.RuntimeConfigurationVersion,
            descriptor.ActorRuntimeConfigurationVersion);
        Assert.Equal("participant-10", descriptor.Participants[0].Name);
        Assert.Equal(
            "generic-test",
            descriptor.Participants[0].RuntimeKind);
        Assert.Equal(
            "artifact-10",
            descriptor.Participants[0].ArtifactHash);
        Assert.Equal("#00000a", descriptor.Participants[0].Accent);
        Assert.Equal("look-10", descriptor.Participants[0].LookId);
        Assert.Equal(
            "projectile-10",
            descriptor.Participants[0].ProjectileLookId);

        Assert.DoesNotContain(
            typeof(GenericActorParticipantProvenance)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => typeof(IGenericActorRuntimeFactory)
                .IsAssignableFrom(property.PropertyType));
        Assert.DoesNotContain(
            typeof(GenericActorMatchDescriptor)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => typeof(IGenericActorRuntimeFactory)
                .IsAssignableFrom(field.FieldType));
    }

    [Fact]
    public void ParticipantSnapshotAndDescriptor_RejectMalformedMetadataAndTopology()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.Deathmatch("head-to-head");
        GenericActorParticipantConfiguration[] valid =
            Configurations(definition).ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenericActorParticipantProvenance(
                -1,
                0,
                "name",
                "runtime",
                "artifact",
                "#000000",
                null,
                null));
        Assert.Throws<ArgumentException>(() =>
            new GenericActorParticipantProvenance(
                10,
                0,
                " ",
                "runtime",
                "artifact",
                "#000000",
                null,
                null));
        Assert.Throws<ArgumentException>(() =>
            new GenericActorParticipantProvenance(
                10,
                0,
                "name",
                "runtime",
                " ",
                "#000000",
                null,
                null));
        Assert.Throws<ArgumentException>(() =>
            new GenericActorParticipantProvenance(
                10,
                0,
                "name",
                "runtime",
                "artifact",
                "#000000",
                " ",
                null));

        Assert.Throws<ArgumentException>(() =>
            GenericActorParticipantProvenance.CreateCanonicalSnapshot(
                definition,
                valid.Take(1)));
        Assert.Throws<ArgumentException>(() =>
            GenericActorParticipantProvenance.CreateCanonicalSnapshot(
                definition,
                [valid[0], valid[0]]));
        Assert.Throws<ArgumentException>(() =>
            GenericActorParticipantProvenance.CreateCanonicalSnapshot(
                definition,
                [
                    valid[0] with
                    {
                        TeamId = 1,
                    },
                    valid[1],
                ]));
        Assert.Throws<ArgumentException>(() =>
            GenericActorParticipantProvenance.CreateCanonicalSnapshot(
                definition,
                [
                    valid[0],
                    valid[1],
                    valid[1] with
                    {
                        ParticipantId = 99,
                    },
                ]));
        Assert.Throws<ArgumentException>(() =>
            GenericActorParticipantProvenance.CreateCanonicalSnapshot(
                definition,
                [
                    valid[0] with
                    {
                        RuntimeFactory = null!,
                    },
                    valid[1],
                ]));

        ImmutableArray<GenericActorParticipantProvenance> provenance =
            GenericActorParticipantProvenance.CreateCanonicalSnapshot(
                definition,
                valid);
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchDescriptor(
                definition,
                1,
                provenance.Take(1)));
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchDescriptor(
                definition,
                1,
                BotArenaVersions.EngineVersion,
                "wrong-protocol",
                definition.CapabilityVersions.RuntimeConfigurationVersion,
                provenance));
        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchDescriptor(
                definition,
                1,
                BotArenaVersions.EngineVersion,
                definition.CapabilityVersions.RuntimeProtocolVersion,
                "wrong-configuration",
                provenance));
    }

    [Fact]
    public void AuthoritativeEvent_AudienceAndIdentityValuesAreValidated()
    {
        EventCase score = EventCases().Single(item =>
            item.Kind
            == GenericActorRuntimeObservation.EventKind.ScoreChanged);
        var spatial = new GenericActorAuthoritativeEvent.Audience.Spatial(
            new Position(2, 3));
        var teamPrivate =
            new GenericActorAuthoritativeEvent.Audience.TeamPrivate(4);
        var publicAudience =
            new GenericActorAuthoritativeEvent.Audience.Public();

        var publicEvent = new GenericActorAuthoritativeEvent(
            "authoritative:0",
            2,
            7,
            score.Kind,
            score.Payload,
            publicAudience);
        var spatialEvent = new GenericActorAuthoritativeEvent(
            "authoritative:1",
            2,
            8,
            score.Kind,
            score.Payload,
            spatial);
        var privateEvent = new GenericActorAuthoritativeEvent(
            "authoritative:2",
            2,
            9,
            score.Kind,
            score.Payload,
            teamPrivate);

        Assert.Same(publicAudience, publicEvent.EventAudience);
        Assert.Equal(new Position(2, 3), spatial.PrimaryPosition);
        Assert.Same(spatial, spatialEvent.EventAudience);
        Assert.Equal(4, teamPrivate.TeamId);
        Assert.Same(teamPrivate, privateEvent.EventAudience);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenericActorAuthoritativeEvent.Audience.TeamPrivate(-1));
        Assert.Throws<ArgumentException>(() =>
            new GenericActorAuthoritativeEvent(
                " ",
                0,
                0,
                score.Kind,
                score.Payload,
                publicAudience));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenericActorAuthoritativeEvent(
                "event",
                -1,
                0,
                score.Kind,
                score.Payload,
                publicAudience));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenericActorAuthoritativeEvent(
                "event",
                0,
                -1,
                score.Kind,
                score.Payload,
                publicAudience));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenericActorAuthoritativeEvent(
                "event",
                0,
                0,
                score.Kind,
                new GenericActorRuntimeObservation.EventPayload.ScoreChanged(
                    -1,
                    "kills",
                    1),
                publicAudience));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GenericActorAuthoritativeEvent(
                "event",
                0,
                0,
                GenericActorRuntimeObservation.EventKind.Attack,
                new GenericActorRuntimeObservation.EventPayload.Attack(
                    Actor(),
                    Action(),
                    -1,
                    new Position(1, 1),
                    ProjectileHeading.North),
                publicAudience));
    }

    [Fact]
    public void AuthoritativeEvent_EnforcesEveryKindPayloadMapping()
    {
        EventCase[] cases = EventCases();
        GenericActorRuntimeObservation.EventKind[] kinds =
            Enum.GetValues<GenericActorRuntimeObservation.EventKind>();

        Assert.Equal(kinds.Length, cases.Length);
        Assert.Equal(
            kinds.Order(),
            cases.Select(item => item.Kind).Distinct().Order());

        foreach (GenericActorRuntimeObservation.EventKind kind in kinds)
        {
            foreach (EventCase candidate in cases)
            {
                bool compatible =
                    ExpectedPayloadType(kind) == candidate.Payload.GetType();
                Func<GenericActorAuthoritativeEvent> construct = () =>
                    new GenericActorAuthoritativeEvent(
                        $"authoritative:{(int)kind}:{candidate.Ordinal}",
                        tick: 3,
                        globalOrdinal: candidate.Ordinal,
                        kind,
                        candidate.Payload,
                        new GenericActorAuthoritativeEvent.Audience.Public());

                if (!compatible)
                {
                    Assert.Throws<ArgumentException>(construct);
                    continue;
                }

                GenericActorAuthoritativeEvent authoritative = construct();
                Assert.Equal(kind, authoritative.Kind);
                Assert.Equal(candidate.Ordinal, authoritative.GlobalOrdinal);
                Assert.Equal(
                    candidate.Ordinal,
                    authoritative.Ordinal);
                Assert.Same(candidate.Payload, authoritative.UnredactedPayload);
                Assert.Same(candidate.Payload, authoritative.Payload);
            }
        }
    }

    private static GenericActorParticipantConfiguration[] Configurations(
        ActorResolvedMatchDefinition definition)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        return definition.Topology.Participants
            .Select(participant =>
                new GenericActorParticipantConfiguration
                {
                    ParticipantId = participant.ParticipantId,
                    TeamId = participant.TeamId,
                    Name = $"participant-{participant.ParticipantId}",
                    RuntimeFactory = factories[participant.ParticipantId],
                    RuntimeKind = "generic-test",
                    ArtifactHash =
                        $"artifact-{participant.ParticipantId}",
                    Accent =
                        $"#{participant.ParticipantId:x6}",
                    LookId = $"look-{participant.ParticipantId}",
                    ProjectileLookId =
                        $"projectile-{participant.ParticipantId}",
                })
            .ToArray();
    }

    private static EventCase[] EventCases()
    {
        ActorIdentity actor = Actor();
        ActorIdentity target = new(1, 0, 0);
        Position position = new(1, 1);
        GenericActorRuntimeActionResolution.ResolvedAction action = Action();
        var lifecycle =
            new GenericActorRuntimeObservation.EventPayload.Lifecycle(
                "split",
                "operation",
                actor,
                0,
                1,
                4,
                null);
        var formTransition =
            new GenericActorRuntimeObservation.EventPayload.FormTransition(
                actor,
                "anchor",
                "operation",
                "mobile",
                "turret",
                1,
                3);

        return
        [
            new(
                0,
                GenericActorRuntimeObservation.EventKind.Rotation,
                new GenericActorRuntimeObservation.EventPayload.Rotation(
                    actor,
                    action,
                    position,
                    Direction.North,
                    Direction.East)),
            new(
                1,
                GenericActorRuntimeObservation.EventKind.Movement,
                new GenericActorRuntimeObservation.EventPayload.Movement(
                    actor,
                    action,
                    position,
                    new Position(2, 1),
                    Direction.East)),
            new(
                2,
                GenericActorRuntimeObservation.EventKind.MovementBlocked,
                new GenericActorRuntimeObservation.EventPayload.MovementBlocked(
                    actor,
                    action,
                    position,
                    new Position(0, 1),
                    Direction.West)),
            new(
                3,
                GenericActorRuntimeObservation.EventKind.Attack,
                new GenericActorRuntimeObservation.EventPayload.Attack(
                    actor,
                    action,
                    12,
                    position,
                    ProjectileHeading.NorthEast)),
            new(
                4,
                GenericActorRuntimeObservation.EventKind.Damage,
                new GenericActorRuntimeObservation.EventPayload.Damage(
                    0,
                    actor,
                    target,
                    12,
                    1,
                    2,
                    position)),
            new(
                5,
                GenericActorRuntimeObservation.EventKind.Destruction,
                new GenericActorRuntimeObservation.EventPayload.Destruction(
                    target,
                    0,
                    actor,
                    12,
                    0,
                    "mobile",
                    position)),
            new(
                6,
                GenericActorRuntimeObservation.EventKind.LifeSpawned,
                new GenericActorRuntimeObservation.EventPayload.LifeSpawned(
                    actor,
                    10,
                    null,
                    0,
                    "mobile",
                    3,
                    position,
                    GenericActorRuntimeStart.SpawnReason.Initial,
                    null,
                    null)),
            new(
                7,
                GenericActorRuntimeObservation.EventKind.LifeRetired,
                new GenericActorRuntimeObservation.EventPayload.LifeRetired(
                    actor,
                    0,
                    "mobile",
                    position,
                    "replicated",
                    "split",
                    "operation")),
            new(
                8,
                GenericActorRuntimeObservation.EventKind.RuntimeFault,
                new GenericActorRuntimeObservation.EventPayload.RuntimeFault(
                    new GenericActorRuntimeFault(
                        10,
                        actor,
                        GenericActorRuntimeFault.FaultStage.TickExecution,
                        GenericActorRuntimeFaultCodes.TickExecutionFailed,
                        1,
                        false))),
            new(
                9,
                GenericActorRuntimeObservation.EventKind
                    .ParticipantDisqualified,
                new GenericActorRuntimeObservation.EventPayload.Participant(
                    10,
                    0)),
            new(
                10,
                GenericActorRuntimeObservation.EventKind.LifecycleQueued,
                lifecycle),
            new(
                11,
                GenericActorRuntimeObservation.EventKind.LifecycleCancelled,
                lifecycle),
            new(
                12,
                GenericActorRuntimeObservation.EventKind.LifecycleCompleted,
                lifecycle),
            new(
                13,
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionStarted,
                formTransition),
            new(
                14,
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted,
                formTransition),
            new(
                15,
                GenericActorRuntimeObservation.EventKind
                    .FormTransitionCancelled,
                formTransition),
            new(
                16,
                GenericActorRuntimeObservation.EventKind.ScoreChanged,
                new GenericActorRuntimeObservation.EventPayload.ScoreChanged(
                    0,
                    "kills",
                    1)),
            new(
                17,
                GenericActorRuntimeObservation.EventKind.ModeChanged,
                new GenericActorRuntimeObservation.EventPayload.ModeChanged(
                    new GenericActorRuntimeObservation.ModeObservationState
                        .Deathmatch("deathmatch"))),
            new(
                18,
                GenericActorRuntimeObservation.EventKind
                    .LifecycleClockCancelled,
                new GenericActorRuntimeObservation.EventPayload
                    .LifecycleClockCancelled(
                        0,
                        1,
                        new GenericActorRuntimeObservation.UnitSlotState
                            .AvailabilityPending(
                                GenericActorRuntimeObservation
                                    .AvailabilityReason.InitialUnlock,
                                3),
                        "participant-disqualified")),
            new(
                19,
                GenericActorRuntimeObservation.EventKind.ProjectileDeflected,
                new GenericActorRuntimeObservation.EventPayload
                    .ProjectileDeflected(
                        0,
                        actor,
                        target,
                        12,
                        13,
                        "bulwark-prime-aegis-shell",
                        Direction.West,
                        ProjectileHeading.East,
                        position)),
        ];
    }

    private static Type ExpectedPayloadType(
        GenericActorRuntimeObservation.EventKind kind) =>
        kind switch
        {
            GenericActorRuntimeObservation.EventKind.Rotation =>
                typeof(GenericActorRuntimeObservation.EventPayload.Rotation),
            GenericActorRuntimeObservation.EventKind.Movement =>
                typeof(GenericActorRuntimeObservation.EventPayload.Movement),
            GenericActorRuntimeObservation.EventKind.MovementBlocked =>
                typeof(GenericActorRuntimeObservation.EventPayload
                    .MovementBlocked),
            GenericActorRuntimeObservation.EventKind.Attack =>
                typeof(GenericActorRuntimeObservation.EventPayload.Attack),
            GenericActorRuntimeObservation.EventKind.Damage =>
                typeof(GenericActorRuntimeObservation.EventPayload.Damage),
            GenericActorRuntimeObservation.EventKind.Destruction =>
                typeof(GenericActorRuntimeObservation.EventPayload
                    .Destruction),
            GenericActorRuntimeObservation.EventKind.LifeSpawned =>
                typeof(GenericActorRuntimeObservation.EventPayload
                    .LifeSpawned),
            GenericActorRuntimeObservation.EventKind.LifeRetired =>
                typeof(GenericActorRuntimeObservation.EventPayload
                    .LifeRetired),
            GenericActorRuntimeObservation.EventKind.RuntimeFault =>
                typeof(GenericActorRuntimeObservation.EventPayload
                    .RuntimeFault),
            GenericActorRuntimeObservation.EventKind
                    .ParticipantDisqualified =>
                typeof(GenericActorRuntimeObservation.EventPayload
                    .Participant),
            GenericActorRuntimeObservation.EventKind.LifecycleQueued
                or GenericActorRuntimeObservation.EventKind
                    .LifecycleCancelled
                or GenericActorRuntimeObservation.EventKind
                    .LifecycleCompleted =>
                typeof(GenericActorRuntimeObservation.EventPayload.Lifecycle),
            GenericActorRuntimeObservation.EventKind
                    .FormTransitionStarted
                or GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted
                or GenericActorRuntimeObservation.EventKind
                    .FormTransitionCancelled =>
                typeof(GenericActorRuntimeObservation.EventPayload
                    .FormTransition),
            GenericActorRuntimeObservation.EventKind.ScoreChanged =>
                typeof(GenericActorRuntimeObservation.EventPayload
                    .ScoreChanged),
            GenericActorRuntimeObservation.EventKind.ModeChanged =>
                typeof(GenericActorRuntimeObservation.EventPayload
                    .ModeChanged),
            GenericActorRuntimeObservation.EventKind
                    .LifecycleClockCancelled =>
                typeof(GenericActorRuntimeObservation.EventPayload
                    .LifecycleClockCancelled),
            GenericActorRuntimeObservation.EventKind.ProjectileDeflected =>
                typeof(GenericActorRuntimeObservation.EventPayload
                    .ProjectileDeflected),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static ActorIdentity Actor() => new(0, 0, 0);

    private static GenericActorRuntimeActionResolution.ResolvedAction
        Action() =>
        new("wait", 0, ImmutableArray<GenericActorRuntimeActionArgument>.Empty);

    private sealed record EventCase(
        long Ordinal,
        GenericActorRuntimeObservation.EventKind Kind,
        GenericActorRuntimeObservation.EventPayload Payload);
}
