using System.Security.Cryptography;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using BotArena.Engine;
using BotArena.Sdk;

namespace BotArena.Cli.Tests;

public sealed class ArcRelayTacticalPlaybookCompilerTests
{
    [Fact]
    public void HomeSiegeCompilesWithIndependentHashesAndCanonicalPayload()
    {
        string playbook = HomeSiege();
        TacticalPlaybookCompilation first =
            ArcRelayTacticalPlaybookCompiler.Compile(playbook);
        TacticalPlaybookCompilation second =
            ArcRelayTacticalPlaybookCompiler.Compile(playbook);

        Assert.Equal(first.LinkedData, second.LinkedData);
        Assert.Equal(Sha256(File.ReadAllBytes(playbook)), first.PlaybookSha256);
        Assert.Equal(
            Sha256(File.ReadAllBytes(first.LayoutPath)),
            first.LayoutSha256);
        Assert.Equal(8, first.Composition.Length);

        using var reader = new BinaryReader(new MemoryStream(first.LinkedData));
        Assert.Equal(ArcRelayTacticalPlaybookCompiler.EnvelopeMagic,
            reader.ReadInt32());
        Assert.Equal(ArcRelayTacticalPlaybookCompiler.PlaybookSchema,
            reader.ReadString());
        Assert.Equal(first.PlaybookSha256, reader.ReadString());
        Assert.Equal(first.LayoutSha256, reader.ReadString());
        byte[] canonicalPlaybook = reader.ReadBytes(reader.ReadInt32());
        byte[] canonicalLayout = reader.ReadBytes(reader.ReadInt32());
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);

        using JsonDocument playbookDocument = JsonDocument.Parse(
            canonicalPlaybook);
        using JsonDocument layoutDocument = JsonDocument.Parse(canonicalLayout);
        Assert.Equal("arbitration", playbookDocument.RootElement
            .EnumerateObject().First().Name);
        Assert.Equal("anchors", layoutDocument.RootElement
            .EnumerateObject().First().Name);
    }

    [Fact]
    public void UnknownFieldsAreRejectedInsteadOfSilentlyIgnored()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        source["surprise"] = true;
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("unknown field 'surprise'", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void ALayoutEditRequiresAnExplicitNewHash()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject layout = source["layout"]!.AsObject();
        string realLayout = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(HomeSiege())!,
            layout["path"]!.GetValue<string>()));
        layout["path"] = realLayout;
        layout["sha256"] = new string('0', 64);
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("layout hash mismatch", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void RuntimePackageAcceptsOnlyItsExactBoundContract()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HomeSiege());
        string[] baseline = BaselineComposition();
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            compilation.Composition,
            baseline,
            loopProfile: ArcRelayLoopProfile.Current);
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(
                ActorContractManifestSerializer.ToCanonicalJson(definition));

        TacticalPlaybookPackage package = TacticalPlaybookPackage.Load(
            compilation.LinkedData.ToImmutableArray(),
            contract,
            new BotArena.Sdk.Position(2, 11));

        Assert.Equal(compilation.PlaybookSha256, package.PlaybookSha256);
        Assert.Equal(compilation.LayoutSha256, package.LayoutSha256);
        Assert.Equal(new BotArena.Sdk.Position(24, 11),
            package.AnchorPosition("enemy-perimeter"));
    }

    private static string TemporaryJson(JsonNode source)
    {
        string path = Path.Combine(
            Path.GetTempPath(), $"nilbots-playbook-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, source.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
        return path;
    }

    private static string HomeSiege() => Path.Combine(
        FindRepoRoot(),
        "arena-bots",
        "arc-relay",
        "tactical-playbook-v1-2026-08-03",
        "playbooks",
        "home-siege-v2.json");

    private static string[] BaselineComposition()
    {
        string source = Path.Combine(
            FindRepoRoot(),
            "arena-bots",
            "arc-relay",
            "forward-combat-operation-proof-v1-2026-08-03",
            "sheets",
            "baseline.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(source));
        return document.RootElement.GetProperty("composition")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();
    }

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
