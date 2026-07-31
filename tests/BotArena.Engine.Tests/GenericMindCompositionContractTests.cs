using System.Collections.Immutable;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Per-slot chassis and composition-aware topology profile IDs
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §9.2, §9.5, §9.7).
/// <para>
/// The load-bearing property under test is that this costs the shipped
/// generation NOTHING: a composition-free contract writes exactly the bytes it
/// wrote before, keeps its topology and match fingerprints, and keeps its
/// registered profile label. Compositions are additive, and they are
/// pre-registered rather than inferred.
/// </para>
/// </summary>
public sealed class GenericMindCompositionContractTests
{
    [Fact]
    public void AClasslessTopologyWritesExactlyTheBytesItWroteBefore()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.Create();

        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(definition);

        // #156 additive-canonical discipline: absent, not null. One encoding
        // for the absence means every pinned fingerprint holds.
        Assert.DoesNotContain(
            "\"controllerParticipantId\":0,\"classId\"",
            canonical,
            StringComparison.Ordinal);
        GenericActorResolvedMatchContract parsed =
            ActorCanonicalContractReader.Parse(canonical);
        Assert.All(
            parsed.Topology.UnitSlots,
            slot => Assert.Null(slot.ClassId));
    }

    [Fact]
    public void PerSlotChassisRoundTripsTheCanonicalContract()
    {
        ActorResolvedMatchDefinition source =
            GenericActorContractTestFixture.Deathmatch("head-to-head");
        ActorResolvedMatchDefinition composed = WithSlotChassis(
            source,
            [("striker", "bulwark")]);

        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(composed);
        GenericActorResolvedMatchContract parsed =
            ActorCanonicalContractReader.Parse(canonical);

        Assert.Equal(
            ["bulwark", "striker"],
            parsed.Topology.UnitSlots
                .Select(slot => slot.ClassId ?? "<absent>")
                .Order(StringComparer.Ordinal)
                .ToArray());
        // The slot's chassis is a fingerprinted topology fact, so declaring it
        // moves the topology and aggregate fingerprints — exactly as
        // GAME-MODE-ARCHITECTURE.md §3 requires — and it is the SLOT that
        // carries it, not the participant.
        Assert.NotEqual(
            ActorContractFingerprint.ComputeTopology(source.Topology),
            ActorContractFingerprint.ComputeTopology(composed.Topology));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(source),
            ActorContractFingerprint.ComputeMatch(composed));
        // ...and the RULES fingerprint does not move: two compositions play
        // the same mechanics.
        Assert.Equal(
            ActorContractFingerprint.ComputeRules(source.Rules),
            ActorContractFingerprint.ComputeRules(composed.Rules));
    }

    [Fact]
    public void ACompositionFreeTopologyKeepsItsRegisteredProfileId()
    {
        Assert.Equal(
            FrontlineLabsDefinition.TopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(
                FrontlineLabsDefinition.Create().Topology));
        Assert.Equal(
            FrontlineLabsDefinition.LegionMirrorTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(
                LegionTopology(8, 8, null, null, mixed: false)));
    }

    [Theory]
    [InlineData("warden", "warden",
        FrontlineLabsDefinition.LegionWardenMirrorTopologyProfileId)]
    [InlineData("spearhead", "spearhead",
        FrontlineLabsDefinition.LegionSpearheadMirrorTopologyProfileId)]
    [InlineData("spearhead", "warden",
        FrontlineLabsDefinition
            .LegionSpearheadVersusWardenTopologyProfileId)]
    [InlineData("warden", "spearhead",
        FrontlineLabsDefinition
            .LegionSpearheadVersusWardenTopologyProfileId)]
    public void MixedCompositionsResolveTheirRegisteredProfileId(
        string teamZero,
        string teamOne,
        string expected) =>
        Assert.Equal(
            expected,
            FrontlineLabsDefinition.TopologyProfileIdFor(
                LegionTopology(8, 8, teamZero, teamOne, mixed: true)));

    [Fact]
    public void AnUnregisteredCompositionFaultsRatherThanBorrowingALabel()
    {
        // A profile ID is a pre-registration. Mislabelling a composition would
        // carry the wrong topology into balance evidence, which is exactly why
        // TopologyProfileIdFor throws rather than borrow a neighbour's label.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrontlineLabsDefinition.TopologyProfileIdFor(
                LegionTopology(8, 8, "vanguard", "vanguard", mixed: true)));
        // ...including "a registered composition against an unlabelled army",
        // which is not a cell anybody pre-registered.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrontlineLabsDefinition.TopologyProfileIdFor(
                LegionTopology(8, 8, "warden", null, mixed: true)));
    }

    [Fact]
    public void TheMindSessionRefusesAnUnregisteredSlotChassis()
    {
        ActorResolvedMatchDefinition composed = WithSlotChassis(
            GenericActorContractTestFixture.Deathmatch("head-to-head"),
            [("striker", "vanguard")]);
        ActorResolvedMatchDefinition mind =
            GenericMindSessionTestFixture.OnMindProfile(composed);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(mind);

        Assert.Throws<NotSupportedException>(() =>
            new GenericActorMatchSession(
                mind,
                GenericMindSessionTestFixture.Configurations(
                    mind,
                    factories),
                matchSeed: 1).Dispose());
    }

    [Fact]
    public void EveryOwnSlotIsPublishedEveryTickWithItsChassis()
    {
        ActorResolvedMatchDefinition composed = WithSlotChassis(
            GenericActorContractTestFixture.Deathmatch("head-to-head"),
            [("striker", "bulwark")]);
        ActorResolvedMatchDefinition mind =
            GenericMindSessionTestFixture.OnMindProfile(composed);
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(mind);
        using var session = new GenericActorMatchSession(
            mind,
            GenericMindSessionTestFixture.Configurations(mind, factories),
            matchSeed: 2);

        for (int tick = 0; tick < 3; tick++)
            session.Step();

        Assert.All(
            factories.Values,
            factory => Assert.All(
                factory.Observations,
                observation => Assert.All(
                    observation.Slots,
                    slot =>
                    {
                        Assert.NotNull(slot.ClassId);
                        // §10: the chassis is FIXED in v1. The candidate set
                        // and the selection are reserved shapes only.
                        Assert.Empty(slot.CandidateClassIds);
                        Assert.Null(slot.SelectedClassId);
                    })));
    }

    private static ActorResolvedMatchDefinition WithSlotChassis(
        ActorResolvedMatchDefinition source,
        IReadOnlyList<(string TeamZero, string TeamOne)> chassisByUnit)
    {
        PublicMatchTopology topology = source.Topology with
        {
            UnitSlots =
            [
                .. source.Topology.UnitSlots.Select(slot =>
                    slot with
                    {
                        ClassId = slot.TeamId == 0
                            ? chassisByUnit[slot.UnitId].TeamZero
                            : chassisByUnit[slot.UnitId].TeamOne,
                    }),
            ],
        };
        return new ActorResolvedMatchDefinition(
            source.Rules,
            source.Map,
            source.Format,
            topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static PublicMatchTopology LegionTopology(
        int teamZeroSlots,
        int teamOneSlots,
        string? teamZeroToken,
        string? teamOneToken,
        bool mixed)
    {
        var slots = ImmutableArray.CreateBuilder<PublicUnitSlot>();
        void AddTeam(int teamId, int count, string? token)
        {
            for (int unitId = 0; unitId < count; unitId++)
            {
                slots.Add(new PublicUnitSlot(
                    teamId,
                    unitId,
                    10 + teamId,
                    // A team is MIXED when a slot declares a chassis that
                    // differs from the team's composition token.
                    mixed && token is not null
                        ? unitId == 0 ? "fabricator" : "striker"
                        : token));
            }
        }

        AddTeam(0, teamZeroSlots, teamZeroToken);
        AddTeam(1, teamOneSlots, teamOneToken);
        return new PublicMatchTopology
        {
            Teams = [new(0, teamZeroToken), new(1, teamOneToken)],
            Participants =
                [new(10, 0, teamZeroToken), new(11, 1, teamOneToken)],
            UnitSlots = slots.ToImmutable(),
            InitialLives =
                [new(0, 0, 0, "prime-mobile"), new(1, 0, 0, "prime-mobile")],
        };
    }
}
