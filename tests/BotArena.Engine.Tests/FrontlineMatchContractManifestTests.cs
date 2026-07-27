using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public class FrontlineMatchContractManifestTests
{
    [Fact]
    public void DefaultFactory_BuildsAllStableSlotsButOnlyInitialPrimeLives()
    {
        FrontlineRules frontline = new();

        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(
                CreateRules(frontline),
                LoadFrontlineMap());

        Assert.Equal(2, contract.Rules.Limits.TeamCount);
        Assert.Equal(2, contract.Rules.Limits.ParticipantCount);
        Assert.Equal(6, contract.Rules.Limits.UnitSlotCount);
        Assert.Equal(6, contract.Topology.UnitSlots.Length);
        Assert.Equal(2, contract.Topology.InitialLives.Length);
        foreach (int teamId in Enumerable.Range(0, 2))
        {
            Assert.Equal(
                [0, 1, 2],
                contract.Topology.UnitSlots
                    .Where(slot => slot.TeamId == teamId)
                    .Select(slot => slot.UnitId));
            Assert.Contains(
                new PublicInitialLife(
                    teamId,
                    UnitId: 0,
                    LifeId: 0,
                    frontline.PrimeForm.FormId),
                contract.Topology.InitialLives);
        }
    }

    [Fact]
    public void DefaultFactory_ProjectsVariableMaximumUnitSlots()
    {
        FrontlineRules frontline = new()
        {
            MaxUnitsPerTeam = 5,
            FabricationUnlockTicks = [120, 220, 320, 420],
        };

        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(
                CreateRules(frontline),
                LoadFrontlineMap());

        Assert.Equal(10, contract.Rules.Limits.UnitSlotCount);
        Assert.Equal(10, contract.Topology.UnitSlots.Length);
        Assert.All(
            contract.Topology.Teams,
            team => Assert.Equal(
                [0, 1, 2, 3, 4],
                contract.Topology.UnitSlots
                    .Where(slot => slot.TeamId == team.TeamId)
                    .Select(slot => slot.UnitId)));
        Assert.Equal(2, contract.Topology.InitialLives.Length);
    }

    [Fact]
    public void CustomTopologyFactory_ValidatesExactProjectedCounts()
    {
        GameRules rules = CreateRules(new FrontlineRules());
        ArenaMap map = LoadFrontlineMap();
        PublicMatchTopology valid =
            MatchDefinitionResolver.Resolve(rules, map).Topology;
        PublicMatchTopology missingSlot = valid with
        {
            UnitSlots = valid.UnitSlots
                .Where(slot => slot != new PublicUnitSlot(1, 2, 1))
                .ToImmutableArray(),
        };

        Assert.Throws<MatchDefinitionValidationException>(() =>
            PublicRulesManifestFactory.CreateMatchContract(
                rules,
                map,
                missingSlot));
    }

    [Fact]
    public void CustomTopologyFactory_PreservesValidExplicitParticipantIdentity()
    {
        GameRules rules = CreateRules(new FrontlineRules());
        PublicMatchTopology topology = new()
        {
            Teams = [new(0), new(1)],
            Participants = [new(10, 0), new(20, 1)],
            UnitSlots =
            [
                new(0, 0, 10),
                new(0, 1, 10),
                new(0, 2, 10),
                new(1, 0, 20),
                new(1, 1, 20),
                new(1, 2, 20),
            ],
            InitialLives =
            [
                new(0, 0, 0, "prime-mobile"),
                new(1, 0, 0, "prime-mobile"),
            ],
        };

        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(
                rules,
                LoadFrontlineMap(),
                topology);

        Assert.Equal(
            [10, 20],
            contract.Topology.Participants
                .Select(participant => participant.ParticipantId));
        Assert.Matches("^[0-9a-f]{64}$", contract.MatchContractFingerprint);
    }

    private static GameRules CreateRules(FrontlineRules frontline) =>
        GameRules.V0_1 with
        {
            RulesVersion = "frontline-manifest-test",
            Frontline = frontline,
        };

    private static ArenaMap LoadFrontlineMap() =>
        ArenaMap.FromJson(File.ReadAllText(
            Path.Combine(
                FindRepoRoot(),
                "maps",
                "experimental",
                "frontline-01.json")));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }
}
