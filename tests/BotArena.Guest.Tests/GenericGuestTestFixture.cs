using BotArena.Engine;
using BotArena.Engine.Tests;
using BotArena.Sdk;
using ActorIdentity = BotArena.Sdk.ActorIdentity;
using Direction = BotArena.Sdk.Direction;
using Position = BotArena.Sdk.Position;

namespace BotArena.Guest.Tests;

internal static class GenericGuestTestFixture
{
    public static GenericActorMatchStart Start()
    {
        ActorResolvedMatchDefinition source =
            GenericActorContractTestFixture.Deathmatch("teams");
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(source);
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);
        return new GenericActorMatchStart
        {
            SchemaVersion =
                GenericActorContractVersions.MatchStartSchemaVersion,
            RuntimeContractVersion =
                GenericActorContractVersions.RuntimeContractVersion,
            ActorId = new ActorIdentity(0, 1, 0),
            ParticipantId = 11,
            ActorRandomSeed = 0x1234_5678_9ABC_DEF0UL,
            TeamRandomSeed = 0x0FED_CBA9_8765_4321UL,
            Origin = new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Initial,
                Generation: 0,
                ParentActorId: null,
                SourceTransitionId: null,
                SourceOperationId: null),
            Contract = contract,
        };
    }

    public static GenericActorContext Context(
        GenericActorMatchStart start,
        int tick = 0,
        ActorIdentity? actorId = null,
        int? generation = null,
        string? fingerprint = null) =>
        new(
            GenericActorContext.CurrentSchemaVersion,
            tick,
            fingerprint ?? start.Contract.MatchContractFingerprint,
            new GenericActorContext.ObservedSelfState(
                actorId ?? start.ActorId,
                generation ?? start.Origin.Generation,
                "mobile",
                new Position(2, 2),
                Direction.North,
                health: 4,
                cooldown: 0,
                energy: null,
                previousActionResolution: null,
                pendingSameLifeTransition: null),
            [
                new GenericActorContext.ObservedUnitSlot(
                    start.ActorId.TeamId,
                    start.ActorId.UnitId,
                    new GenericActorContext.UnitSlotState.Active(
                        start.ActorId,
                        start.Origin.Generation,
                        "mobile")),
            ],
            [
                new GenericActorContext.ObservedParticipantStatus(
                    10,
                    0,
                    runtimeFaultCount: 0,
                    disqualified: false),
                new GenericActorContext.ObservedParticipantStatus(
                    11,
                    0,
                    runtimeFaultCount: 0,
                    disqualified: false),
                new GenericActorContext.ObservedParticipantStatus(
                    20,
                    1,
                    runtimeFaultCount: 0,
                    disqualified: false),
                new GenericActorContext.ObservedParticipantStatus(
                    21,
                    1,
                    runtimeFaultCount: 0,
                    disqualified: false),
            ],
            allies: [],
            enemies: [],
            visibleTiles: [],
            visibleProjectiles: null,
            visibleEvents: [],
            heardSounds: null,
            new GenericActorContext.ScoreboardState(
                [
                    new GenericActorContext.TeamScoreState(
                        0,
                        eligible: true,
                        [
                            new GenericActorContext.ScoreValue(
                                "kills",
                                0),
                        ]),
                    new GenericActorContext.TeamScoreState(
                        1,
                        eligible: true,
                        [
                            new GenericActorContext.ScoreValue(
                                "kills",
                                0),
                        ]),
                ]),
            new GenericActorContext.ModeObservationState.Deathmatch(
                "deathmatch"),
            [
                new GenericActorActionLegality(
                    "wait",
                    0,
                    allowedByForm: true,
                    available: true,
                    constraints: []),
            ]);

    public static byte[] GenericHello() =>
        ActorWireProtocol.EncodeHello(
            ActorWireProtocol.MajorVersion,
            ActorWireProtocol.MajorVersion,
            ActorContractProfile.GenericV2);

    public static byte[] LegacyActorHello() =>
        ActorWireProtocol.EncodeHello(
            ActorWireProtocol.MajorVersion,
            ActorWireProtocol.MajorVersion);
}
