using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.Cli.Tests;

public sealed class ArcRelayTacticalPlaybookCompilerTests
{
    private static readonly string[] CurrentSheets =
    [
        "breakwater-v1",
        "home-siege-v3",
        "hunter-v1",
        "predator-v2",
        "ripen-harvest-v1",
        "trifold-balanced-v1",
        "wellwright-v1",
    ];

    [Fact]
    public void EveryCurrentSheetCompilesDirectlyToAtl1()
    {
        foreach (string name in CurrentSheets)
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(Playbook(name));

            Assert.True(compilation.UsesDirectLadder);
            Assert.Equal(
                ArcRelayTacticalPlaybookCompiler.DirectEnvelopeMagic,
                BitConverter.ToInt32(compilation.LinkedData));
            Assert.InRange(compilation.LinkedData.Length, 1, 65_535);
            using JsonDocument normalized = JsonDocument.Parse(
                compilation.NormalizedPlaybook);
            Assert.Equal(
                [
                    "auditStatus", "composition", "conditions",
                    "custodyPolicies", "doctrines", "layout", "memory",
                    "playbookId", "roles", "schema",
                ],
                normalized.RootElement.EnumerateObject()
                    .Select(value => value.Name)
                    .Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void FrozenHomeSiegeV2BytesRemainArchivedButAreNotCompilerInput()
    {
        string path = Playbook("home-siege-v2");
        byte[] bytes = File.ReadAllBytes(path);

        Assert.Equal(49_566, bytes.Length);
        Assert.Equal(
            "1812b16d03ff9a70c3f9e66e6a4f0b0a9293b9543925281e9df2657ed25e5fdb",
            Sha256(bytes));
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => ArcRelayTacticalPlaybookCompiler.Compile(path));
        Assert.Contains("unknown field 'perspective'", error.Message);
    }

    [Fact]
    public void FileAndInMemoryCompilationUseTheSameDirectPath()
    {
        string playbookPath = Playbook("hunter-v1");
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(playbookPath))!
            .AsObject();
        string layoutPath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(playbookPath)!,
            source["layout"]!["path"]!.GetValue<string>()));
        byte[] playbook = File.ReadAllBytes(playbookPath);
        byte[] layout = File.ReadAllBytes(layoutPath);

        TacticalPlaybookCompilation fromFile =
            ArcRelayTacticalPlaybookCompiler.Compile(playbookPath);
        TacticalPlaybookCompilation inMemory =
            ArcRelayTacticalPlaybookCompiler.Compile(
                playbook, layout, playbookPath, layoutPath);

        Assert.Equal(fromFile.LinkedData, inMemory.LinkedData);
        Assert.Equal(fromFile.NormalizedPlaybook, inMemory.NormalizedPlaybook);
        Assert.Equal(fromFile.NormalizedLayout, inMemory.NormalizedLayout);
    }

    [Fact]
    public void LayoutHashIsARealBinding()
    {
        JsonObject source = HunterSource();
        source["layout"]!["sha256"] = new string('0', 64);
        string path = WriteBesideHunter(source);
        try
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(path));
            Assert.Contains("layout hash mismatch", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyPlanesAreRejectedInsteadOfDesugared()
    {
        JsonObject source = HunterSource();
        source["groups"] = new JsonArray();
        string path = WriteBesideHunter(source);
        try
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(path));
            Assert.Contains("unknown field 'groups'", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ConditionTyposAndGlobalSelfFactsFailAtCompileTime()
    {
        JsonObject source = HunterSource();
        source["doctrines"]!["roles"]!["hunter"]!["modes"]![1]!["while"] =
            "shdaow-op";
        string typo = WriteBesideHunter(source);
        try
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(typo));
            Assert.Contains("unknown condition 'shdaow-op'", error.Message);
        }
        finally
        {
            File.Delete(typo);
        }

        source = HunterSource();
        source["custodyPolicies"]![0]!["safeConversion"] = "ghost-hurt";
        string self = WriteBesideHunter(source);
        try
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(self));
            Assert.Contains("legal only in a doctrine mode", error.Message);
        }
        finally
        {
            File.Delete(self);
        }
    }

    [Fact]
    public void SharedCoordinationNamesNeedMultipleHonestConsumers()
    {
        JsonObject source = HunterSource();
        JsonObject roles = source["doctrines"]!["roles"]!.AsObject();
        foreach (string role in new[] { "hauler", "carrier" })
        {
            roles[role]!["modes"]!.AsArray()
                .Select(value => value!.AsObject())
                .Single(value => value["cohort"]?.GetValue<string>()
                    == "centre-runners")
                .Remove("cohort");
        }
        string cohort = WriteBesideHunter(source);
        try
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(cohort));
            Assert.Contains("cohort needs at least two", error.Message);
        }
        finally
        {
            File.Delete(cohort);
        }

        source = HunterSource();
        source["doctrines"]!["focus"]!["one-off"] = new JsonObject
        {
            ["maximumAttackers"] = 1,
        };
        source["doctrines"]!["roles"]!["hunter"]!["modes"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value.ContainsKey("assault"))["focus"] = "one-off";
        string focus = WriteBesideHunter(source);
        try
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(focus));
            Assert.Contains("focus scope needs at least two", error.Message);
        }
        finally
        {
            File.Delete(focus);
        }
    }

    [Fact]
    public void FollowAndFetchStayFollowerAndCourierOwned()
    {
        JsonObject source = HunterSource();
        JsonObject medicFollow = source["doctrines"]!["roles"]!["medic"]![
                "modes"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value.ContainsKey("follow"));
        medicFollow["follow"] = "medic";
        string selfFollow = WriteBesideHunter(source);
        try
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(selfFollow));
            Assert.Contains("cannot follow itself", error.Message);
        }
        finally
        {
            File.Delete(selfFollow);
        }

        source = HunterSource();
        source["doctrines"]!["roles"]!["lancer"]!["modes"]![0]!["fetch"] =
            "missing-custody";
        string unknownCustody = WriteBesideHunter(source);
        try
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(unknownCustody));
            Assert.Contains("unknown custody", error.Message);
        }
        finally
        {
            File.Delete(unknownCustody);
        }
    }

    [Fact]
    public void ExplainNamesAuthoredRungsAndAtl1()
    {
        string output = Directory.CreateTempSubdirectory(
            "arc-relay-direct-explain").FullName;
        try
        {
            Assert.Equal(0, ArcRelayTacticalPlaybookCommand.Run(
                ["--playbook", Playbook("hunter-v1"), "--out", output]));
            using JsonDocument explain = JsonDocument.Parse(
                File.ReadAllBytes(Path.Combine(output, "explain.json")));

            Assert.Equal("ATL1", explain.RootElement
                .GetProperty("runtimePackage").GetProperty("envelope")
                .GetString());
            Assert.Contains(explain.RootElement.GetProperty("ladders")
                .EnumerateArray(), ladder => ladder.GetProperty("roleId")
                .GetString() == "hunter");
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static JsonObject HunterSource() => JsonNode.Parse(
        File.ReadAllBytes(Playbook("hunter-v1")))!.AsObject();

    private static string WriteBesideHunter(JsonObject source)
    {
        string path = Path.Combine(
            Path.GetDirectoryName(Playbook("hunter-v1"))!,
            $"hunter-compiler-case-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, source.ToJsonString());
        return path;
    }

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

    private static string Sha256(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
}
