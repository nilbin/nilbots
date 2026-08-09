using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.Cli.Tests;

public sealed class HardSheetDslMigrationTests
{
    private static readonly string[] HardSheets =
    [
        "home-siege-v3",
        "predator-v2",
        "ripen-harvest-v1",
        "breakwater-v1",
        "trifold-balanced-v1",
    ];

    private static readonly string[] RootKeys =
    [
        "schema", "playbookId", "auditStatus", "composition", "layout",
        "memory", "roles", "custodyPolicies", "doctrines", "conditions",
    ];

    [Fact]
    public void HardFiveUseOnlyTheConsolidatedDirectGrammar()
    {
        foreach (string name in HardSheets)
        {
            string path = Playbook(name);
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(path);
            JsonObject source = JsonNode.Parse(File.ReadAllBytes(path))!
                .AsObject();

            Assert.True(compilation.UsesDirectLadder);
            Assert.Equal(RootKeys.Order(StringComparer.Ordinal),
                source.Select(value => value.Key)
                    .Order(StringComparer.Ordinal));
            Assert.Equal(
                ["cohorts", "focus", "roles"],
                source["doctrines"]!.AsObject().Select(value => value.Key)
                    .Order(StringComparer.Ordinal));
            Assert.InRange(compilation.LinkedData.Length, 1, 65_535);
            Assert.DoesNotContain(source, value => value.Key is
                "groups" or "formations" or "engagements"
                or "coordination" or "authoring" or "supportPolicies"
                or "tempo");
        }
    }

    [Fact]
    public void HomeSiegeExpressesRegroupingInEachRolesOwnLadder()
    {
        JsonObject source = Source("home-siege-v3");
        JsonObject roles = source["doctrines"]!["roles"]!.AsObject();

        Assert.NotNull(source["roles"]!.AsArray().Single(role =>
            role!["roleId"]!.GetValue<string>() == "interceptor"));
        Assert.All(roles, role => Assert.Contains(
            role.Value!["modes"]!.AsArray(),
            mode => mode!["while"]?.GetValue<string>()?.Contains(
                "line-cohesion-low", StringComparison.Ordinal) == true));
        Assert.Equal("cohesion",
            source["conditions"]!["line-cohesion-low"]!["fact"]!
                .GetValue<string>());
    }

    [Fact]
    public void PredatorDerivesItsTwoOfFourOrchardFromRungConsumers()
    {
        JsonObject source = Source("predator-v2");
        JsonObject doctrines = source["doctrines"]!.AsObject();

        Assert.Equal(2,
            doctrines["cohorts"]!["orchard"]!["take"]!.GetValue<int>());
        Assert.Equal(4, doctrines["roles"]!.AsObject().Count(role =>
            role.Value!["modes"]!.AsArray().Any(mode =>
                mode!["cohort"] is JsonValue value
                && value.TryGetValue<string>(out string? cohort)
                && cohort == "orchard")));
        Assert.NotNull(source["roles"]!.AsArray().Single(role =>
            role!["roleId"]!.GetValue<string>() == "bait"));
    }

    [Fact]
    public void PredatorBaitCanUseTheImplicitNoLiveFriendliesCondition()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(Playbook("predator-v2"));
        using JsonDocument source = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        DirectLadderRuntime runtime = DirectLadderRuntime.Create(
            source.RootElement);
        TacticalPlaybookPackage.BaitDrop bait = runtime.RuntimePlaybook
            .CustodyPolicies.Single(policy =>
                policy.CustodyId == "bait-custody").BaitDrop!;

        TacticalPlaybookPackage.Condition reclaim = Assert.Single(
            Assert.Single(bait.ReclaimAll).All);
        Assert.Equal("live-friendlies", reclaim.Fact);
        Assert.Equal("equals", reclaim.Operator);
        Assert.Equal(0, reclaim.Value);
    }

    [Fact]
    public void DirectRuntimePreservesEachAuthoredRoleCasualtyPolicy()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(
                Playbook("home-siege-v3"));
        using JsonDocument source = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        DirectLadderRuntime runtime = DirectLadderRuntime.Create(
            source.RootElement);
        Dictionary<string, string> casualty = runtime.RuntimePlaybook.Groups
            .ToDictionary(group => group.GroupId,
                group => group.Membership.Casualty,
                StringComparer.Ordinal);

        Assert.Equal("promote-role", casualty["runner"]);
        Assert.Equal("hold-vacancy", casualty["medic"]);
        Assert.Equal("hold-vacancy", casualty["interceptor"]);
        Assert.Equal("rebalance", casualty["line"]);
    }

    [Fact]
    public void RipenHarvestKeepsBothFetchPrioritiesInTheRunnerLadder()
    {
        JsonArray modes = Source("ripen-harvest-v1")["doctrines"]!["roles"]![
            "runner"]!["modes"]!.AsArray();

        Assert.Equal(
            ["orchard-custody", "farm-outer"],
            modes.Take(2).Select(mode =>
                mode!["fetch"]!.GetValue<string>()).ToArray());
    }

    [Fact]
    public void BreakwaterMakesCounterCourierParticipationAnExplicitRole()
    {
        JsonObject source = Source("breakwater-v1");
        JsonObject interceptor = source["doctrines"]!["roles"]![
            "interceptor"]!.AsObject();

        Assert.Contains(interceptor["modes"]!.AsArray(), mode =>
            mode!["intercept"]?.GetValue<string>() == "enemy-carriers");
        Assert.Equal(3, source["custodyPolicies"]!.AsArray().Count);
    }

    [Fact]
    public void TrifoldDerivesItsThreeWaySpreadFromTwoSharedCohorts()
    {
        JsonObject doctrines = Source("trifold-balanced-v1")["doctrines"]!
            .AsObject();
        JsonObject roles = doctrines["roles"]!.AsObject();

        Assert.Equal(3,
            doctrines["cohorts"]!["north-wing"]!["take"]!.GetValue<int>());
        Assert.Equal(3,
            doctrines["cohorts"]!["south-wing"]!["take"]!.GetValue<int>());
        Assert.All(roles, role =>
        {
            JsonArray modes = role.Value!["modes"]!.AsArray();
            Assert.Contains(modes, mode =>
                mode!["cohort"]?.GetValue<string>() == "north-wing");
            Assert.Contains(modes, mode =>
                mode!["cohort"]?.GetValue<string>() == "south-wing");
            Assert.Equal("centre-farm",
                modes[^1]!["patrol"]!["zone"]!.GetValue<string>());
        });
    }

    private static JsonObject Source(string name) => JsonNode.Parse(
        File.ReadAllBytes(Playbook(name)))!.AsObject();

    private static string Playbook(string name) => Path.Combine(
        FindRepoRoot(),
        "arena-bots",
        "arc-relay",
        "tactical-playbook-v1-2026-08-03",
        "playbooks",
        $"{name}.json");

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }
}
