using System.Collections.Immutable;
using BotArena.Engine;
using BotArena.Engine.Tests;
using BotArena.Sdk;
using ActorIdentity = BotArena.Sdk.ActorIdentity;
using Direction = BotArena.Sdk.Direction;
using Position = BotArena.Sdk.Position;

namespace BotArena.Guest.Tests;

internal static class GenericMindGuestTestFixture
{
    public static readonly MindWaitAction Wait = new("wait", 0);

    /// <summary>
    /// The mind-profile twin of the per-life contract: the same rules, map,
    /// format, topology and mode, with the capability tuple as the ONLY
    /// difference. Anything the two profiles disagree about therefore has to be
    /// the driver.
    /// </summary>
    public static GenericActorResolvedMatchContract Contract()
    {
        ActorResolvedMatchDefinition source =
            GenericActorContractTestFixture.Deathmatch("teams");
        var mind = new ActorResolvedMatchDefinition(
            source.Rules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            ActorMatchCapabilityVersions.Mind);
        return ActorCanonicalContractReader.Parse(
            ActorContractManifestSerializer.ToCanonicalJson(mind));
    }

    public static MindStart Start(
        GenericActorResolvedMatchContract? contract = null) =>
        new()
        {
            SchemaVersion =
                GenericMindContractVersions.MatchStartSchemaVersion,
            RuntimeContractVersion =
                GenericMindContractVersions.RuntimeContractVersion,
            ParticipantId = 11,
            TeamId = 0,
            AlliedParticipantIds = [],
            MindRandomSeed = 0x1234_5678_9ABC_DEF0UL,
            TeamRandomSeed = 0x0FED_CBA9_8765_4321UL,
            Contract = contract ?? Contract(),
        };

    public static MindBody Body(
        int unitId,
        int lifeId,
        Position position,
        int health = 4,
        bool movedLastTick = false,
        int lifeStartedTick = 0,
        string? roleTag = null) =>
        new(
            new ActorIdentity(0, unitId, lifeId),
            generation: 0,
            "mobile",
            position,
            Direction.North,
            health,
            cooldown: 0,
            energy: null,
            previousActionResolution: null,
            pendingSameLifeTransition: null,
            classId: null,
            routeCooldowns: [],
            carriedScrap: 0,
            previousPosition: null,
            movedLastTick,
            lifeStartedTick,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Initial,
                Generation: 0,
                ParentActorId: null,
                SourceTransitionId: null,
                SourceOperationId: null),
            roleTag,
            [
                new GenericActorActionLegality(
                    "wait",
                    0,
                    allowedByForm: true,
                    available: true,
                    []),
                new GenericActorActionLegality(
                    "move",
                    1,
                    allowedByForm: true,
                    available: true,
                    [
                        new GenericActorActionLegality.ArgumentConstraint
                            .DirectionConstraint(
                                [Direction.North, Direction.South]),
                    ]),
            ],
            Wait);

    /// <summary>
    /// One mind observation for a participant owning <paramref name="bodies"/>,
    /// with a slot table that always covers units 0..2 so a dead body still has
    /// a slot to come back into.
    /// </summary>
    public static MindContext Context(
        MindStart start,
        int tick,
        params MindBody[] bodies)
    {
        var slots = ImmutableArray.CreateBuilder<MindSlot>(3);
        for (int unitId = 0; unitId < 3; unitId++)
        {
            MindBody? live = bodies.FirstOrDefault(
                body => body.UnitId == unitId);
            slots.Add(new MindSlot(
                unitId,
                live is null
                    ? new GenericActorContext.UnitSlotState
                        .AutomaticReturnPending(tick + 19, "mobile", 1)
                    : new GenericActorContext.UnitSlotState.Active(
                        live.ActorId,
                        live.Generation,
                        live.FormId)));
        }

        return new MindContext(
            MindContext.CurrentSchemaVersion,
            tick,
            start.Contract.MatchContractFingerprint,
            bodies,
            slots.ToImmutable(),
            allies: [],
            [
                new GenericActorContext.ObservedEnemyState(
                    new ActorIdentity(1, 0, 0),
                    "mobile",
                    new Position(6, 6),
                    Direction.West,
                    health: 3,
                    pendingSameLifeTransition: null,
                    [new ActorIdentity(0, 0, 0)]),
            ],
            [
                new GenericActorContext.ObservedTile(
                    new Position(2, 2),
                    isWall: false,
                    [new ActorIdentity(0, 0, 0)]),
            ],
            visibleProjectiles: null,
            visibleEvents: [],
            heardSounds: null,
            new GenericActorContext.ScoreboardState(
                [
                    new GenericActorContext.TeamScoreState(
                        0,
                        eligible: true,
                        [new GenericActorContext.ScoreValue("kills", 0)]),
                    new GenericActorContext.TeamScoreState(
                        1,
                        eligible: true,
                        [new GenericActorContext.ScoreValue("kills", 0)]),
                ]),
            new GenericActorContext.ModeObservationState.Deathmatch(
                "deathmatch"),
            [
                new GenericActorContext.ObservedParticipantStatus(
                    11,
                    0,
                    runtimeFaultCount: 0,
                    disqualified: false),
                new GenericActorContext.ObservedParticipantStatus(
                    21,
                    1,
                    runtimeFaultCount: 0,
                    disqualified: false),
            ]);
    }

    public static byte[] MindHello() =>
        ActorWireProtocol.EncodeHello(
            ActorWireProtocol.MajorVersion,
            ActorWireProtocol.MajorVersion,
            ActorContractProfile.MindV1);

    /// <summary>
    /// The per-life doctrine every wrapped lineage is a variation of: build a
    /// total precedence order over self plus allies, decide from POSITION in
    /// that order, and reconstruct nothing that is not in the observation.
    /// Being a pure function of the frozen observation is what makes it a valid
    /// subject for the wrap pin: any divergence is the adapter's.
    /// </summary>
    public sealed class PrecedenceBot : IGenericActorBot
    {
        private int _ticks;

        public int StartLifeCalls { get; private set; }

        public ActorIdentity? StartedAs { get; private set; }

        public void StartLife(GenericActorMatchStart start)
        {
            StartLifeCalls++;
            StartedAs = start.ActorId;
        }

        public GenericActorDecision Tick(GenericActorContext context)
        {
            _ticks++;
            // Precedence over the whole team, computed identically by every
            // life because it is a function of the shared observation only.
            var order = context.Allies
                .Select(ally => (ally.ActorId.UnitId, ally.Health))
                .Append((context.Self.ActorId.UnitId, context.Self.Health))
                .OrderByDescending(entry => entry.Health)
                .ThenBy(entry => entry.UnitId)
                .ToArray();
            int index = Array.FindIndex(
                order,
                entry => entry.UnitId == context.Self.ActorId.UnitId);

            // The lowest-precedence body holds; the rest alternate on a phase
            // derived from their index and this LIFE's own age, so the decision
            // depends on per-life private memory as well as the observation.
            if (index == 0)
                return GenericActorDecision.WithoutArguments("wait", 0, $"hold:{_ticks}");
            return new GenericActorDecision(
                "move",
                1,
                [
                    new GenericActorActionArgument.DirectionArgument(
                        (index + _ticks) % 2 == 0
                            ? Direction.North
                            : Direction.South),
                ],
                $"step:{index}:{_ticks}");
        }
    }
}
