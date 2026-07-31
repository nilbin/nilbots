using System.Buffers.Binary;
using System.Collections.Immutable;
using BotArena.Sdk;

namespace BotArena.Sdk.Tests;

/// <summary>
/// Builds a deliberately maximal mind observation and reply, plus the raw
/// tagged-field reader the conformance tests use to check that the bytes carry
/// the reserved IDs and nothing else.
/// </summary>
internal static class GenericMindDynamicTestFixture
{
    public static readonly MindWaitAction Wait = new("wait", 0);

    public static MindBody Body(
        ActorIdentity actorId,
        int lifeStartedTick = 3,
        bool movedLastTick = true,
        Position? previousPosition = null,
        string? roleTag = "channeler",
        int carriedScrap = 5) =>
        new(
            actorId,
            generation: 2,
            "mobile",
            new Position(2, 3),
            Direction.East,
            health: 4,
            cooldown: 1,
            energy: 2,
            GenericActorDynamicTestFixture.Resolution(
                GenericActorActionResolution.ActionOutcome.Blocked),
            new GenericActorContext.PendingSameLifeTransition(
                "anchor",
                "anchor:0:0:4:9",
                "turret",
                startedTick: 8,
                dueTick: 10),
            "striker",
            [new GenericActorContext.ObservedRouteCooldown("anchor", 40)],
            carriedScrap,
            previousPosition ?? new Position(1, 3),
            movedLastTick,
            lifeStartedTick,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Fabrication,
                Generation: 2,
                new ActorIdentity(0, 0, 1),
                "fabricate",
                "fabricate:0:0:1:7"),
            roleTag,
            [
                GenericActorDynamicTestFixture.FullLegality(),
                new GenericActorActionLegality(
                    "wait",
                    0,
                    allowedByForm: true,
                    available: true,
                    []),
            ],
            Wait);

    /// <summary>
    /// One mind context carrying every optional field and both reserved
    /// collections, so a codec round-trip exercises the whole frame rather than
    /// the happy middle of it.
    /// </summary>
    public static MindContext Context(
        bool nullCapabilities = false,
        int bodyCount = 2,
        int schemaVersion = MindContext.CurrentSchemaVersion)
    {
        GenericActorContext source =
            GenericActorDynamicTestFixture.Context(
                nullCapabilities: nullCapabilities);
        MindBody[] bodies =
        [
            .. Enumerable.Range(0, bodyCount).Select(index =>
                Body(
                    new ActorIdentity(0, index, 4 + index),
                    roleTag: index == 0 ? "channeler" : "screen")),
        ];

        return new MindContext(
            schemaVersion,
            tick: 9,
            new string('a', 64),
            bodies,
            [
                new MindSlot(
                    0,
                    new GenericActorContext.UnitSlotState.Active(
                        new ActorIdentity(0, 0, 4),
                        2,
                        "mobile"),
                    "striker"),
                new MindSlot(
                    1,
                    new GenericActorContext.UnitSlotState
                        .AutomaticReturnPending(48, "mobile", 3),
                    "bulwark"),
                new MindSlot(
                    2,
                    new GenericActorContext.UnitSlotState.Ready()),
            ],
            source.Allies,
            source.Enemies,
            source.VisibleTiles,
            source.VisibleProjectiles,
            source.VisibleEvents,
            source.HeardSounds,
            source.Scoreboard,
            source.Mode,
            source.Participants);
    }

    public static MindDecisions Decisions(
        string? roleTag = "channeler",
        string? debugMessage = "one plan, nine bodies") =>
        new(
            GenericMindContractVersions.DecisionSchemaVersion,
            tick: 9,
            [
                new MindCommand(
                    0,
                    4,
                    "move",
                    1,
                    [
                        new GenericActorActionArgument.DirectionArgument(
                            Direction.North),
                    ],
                    roleTag,
                    "stepping to the point"),
                new MindCommand(1, 5, "wait", 0, []),
            ],
            intents: null,
            debugMessage);

    /// <summary>
    /// Reads one tagged object's field IDs and payloads without any codec
    /// knowledge, so a conformance assertion is about the BYTES rather than
    /// about the encoder agreeing with itself.
    /// </summary>
    public static ImmutableDictionary<ushort, byte[]> Fields(byte[] payload)
    {
        var fields = ImmutableDictionary.CreateBuilder<ushort, byte[]>();
        int offset = 0;
        while (offset < payload.Length)
        {
            ushort fieldId = BinaryPrimitives.ReadUInt16LittleEndian(
                payload.AsSpan(offset, 2));
            int length = BinaryPrimitives.ReadInt32LittleEndian(
                payload.AsSpan(offset + 2, 4));
            offset += 6;
            fields.Add(fieldId, payload.AsSpan(offset, length).ToArray());
            offset += length;
        }
        return fields.ToImmutable();
    }

    /// <summary>Splits a length-delimited collection into its items.</summary>
    public static ImmutableArray<byte[]> Items(byte[] collection)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(collection);
        var items = ImmutableArray.CreateBuilder<byte[]>(count);
        int offset = 4;
        for (int index = 0; index < count; index++)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(
                collection.AsSpan(offset, 4));
            offset += 4;
            items.Add(collection.AsSpan(offset, length).ToArray());
            offset += length;
        }
        return items.ToImmutable();
    }
}
