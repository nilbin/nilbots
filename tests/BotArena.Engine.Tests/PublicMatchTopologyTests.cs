namespace BotArena.Engine.Tests;

public class PublicMatchTopologyTests
{
    [Fact]
    public void CurrentDuel_SeparatesTeamsParticipantsUnitSlotsAndLives()
    {
        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, CreateMap());

        Assert.Equal([new PublicScoringTeam(0), new PublicScoringTeam(1)],
            contract.Topology.Teams.ToArray());
        Assert.Equal([new PublicParticipant(0, 0), new PublicParticipant(1, 1)],
            contract.Topology.Participants.ToArray());
        Assert.Equal([new PublicUnitSlot(0, 0, 0), new PublicUnitSlot(1, 0, 1)],
            contract.Topology.UnitSlots.ToArray());
        Assert.Equal(
            [
                new PublicInitialLife(0, 0, 0, "mobile"),
                new PublicInitialLife(1, 0, 0, "mobile"),
            ],
            contract.Topology.InitialLives.ToArray());
    }

    [Fact]
    public void Topology_SupportsDifferentParticipantAndTeamCountsAndSharedControllers()
    {
        PublicMatchContractManifest baseline =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, CreateMap());
        PublicMatchTopology topology = new()
        {
            Teams = [new(0), new(1)],
            Participants = [new(10, 0), new(11, 0), new(20, 1)],
            UnitSlots =
            [
                new(0, 0, 10),
                new(0, 1, 11),
                new(1, 0, 20),
                new(1, 1, 20),
            ],
            InitialLives =
            [
                new(0, 0, 0, "mobile"),
                new(1, 0, 0, "mobile"),
            ],
        };
        PublicMatchContractManifest future = baseline with
        {
            MatchContractFingerprint = "",
            Rules = baseline.Rules with
            {
                Limits = baseline.Rules.Limits with
                {
                    ParticipantCount = 3,
                    UnitSlotCount = 4,
                    MaxUnitsPerTeam = 2,
                },
            },
            Topology = topology,
        };

        string fingerprint = MatchContractFingerprint.ComputeMatch(future);

        Assert.NotEqual(future.Topology.Teams.Length, future.Topology.Participants.Length);
        Assert.Equal(
            2,
            future.Topology.UnitSlots.Count(slot =>
                slot.ControllerParticipantId == 20));
        Assert.Matches("^[0-9a-f]{64}$", fingerprint);
    }

    [Fact]
    public void Topology_RejectsCountsAndCrossTeamOwnershipThatDoNotMatchContract()
    {
        PublicMatchContractManifest baseline =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, CreateMap());
        PublicMatchTopology wrongCount = baseline.Topology with
        {
            Participants = [new(0, 0)],
        };
        PublicMatchTopology crossTeamController = baseline.Topology with
        {
            UnitSlots = [new(0, 0, 1), new(1, 0, 1)],
        };

        Assert.Throws<ArgumentException>(() =>
            MatchContractFingerprint.ComputeMatch(
                baseline with { Topology = wrongCount }));
        Assert.Throws<ArgumentException>(() =>
            MatchContractFingerprint.ComputeMatch(
                baseline with { Topology = crossTeamController }));
    }

    [Fact]
    public void Topology_RejectsMoreStableUnitSlotsThanThePerTeamMaximum()
    {
        PublicMatchContractManifest baseline =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, CreateMap());
        PublicMatchTopology extraSlot = baseline.Topology with
        {
            UnitSlots =
            [
                new(0, 0, 0),
                new(0, 1, 0),
                new(1, 0, 1),
            ],
        };
        PublicMatchContractManifest inconsistent = baseline with
        {
            Rules = baseline.Rules with
            {
                Limits = baseline.Rules.Limits with { UnitSlotCount = 3 },
            },
            Topology = extraSlot,
        };

        Assert.Throws<ArgumentException>(() =>
            MatchContractFingerprint.ComputeMatch(inconsistent));
    }

    [Fact]
    public void TopologyCanonicalization_IgnoresCollectionOrderButPreservesIdentity()
    {
        PublicMatchContractManifest baseline =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, CreateMap());
        PublicMatchTopology reversed = new()
        {
            Teams = [new(1), new(0)],
            Participants = [new(1, 1), new(0, 0)],
            UnitSlots = [new(1, 0, 1), new(0, 0, 0)],
            InitialLives =
            [
                new(1, 0, 0, "mobile"),
                new(0, 0, 0, "mobile"),
            ],
        };

        string reorderedFingerprint = MatchContractFingerprint.ComputeMatch(
            baseline with
            {
                MatchContractFingerprint = "",
                Topology = reversed,
            });

        Assert.Equal(baseline.MatchContractFingerprint, reorderedFingerprint);
    }

    private static ArenaMap CreateMap() =>
        ArenaMap.Create(
            "topology",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);
}
