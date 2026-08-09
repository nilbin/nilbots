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
    public void EveryTrackedPlaybookCompilesAfterDslMigration()
    {
        string playbooks = Path.Combine(
            FindRepoRoot(),
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "playbooks");
        foreach (string path in Directory.GetFiles(playbooks, "*.json")
                     .Order(StringComparer.Ordinal))
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(path);
            Assert.NotEmpty(compilation.NormalizedPlaybook);
        }
    }

    [Fact]
    public void HomeSiegeV2ReferenceFreezePinsAcceptedBytes()
    {
        string root = FindRepoRoot();
        string freezePath = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "evidence",
            "home-siege-v2-reference-freeze.json");
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllBytes(freezePath));
        JsonElement reference = document.RootElement.GetProperty("reference");

        foreach (string name in new[]
                 {
                     "playbook", "layout", "compiledPlaybook",
                     "finalRun", "finalResults",
                 })
        {
            JsonElement artifact = reference.GetProperty(name);
            string path = Path.Combine(
                root,
                artifact.GetProperty("path").GetString()!);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(
                artifact.GetProperty("bytes").GetInt64(),
                bytes.LongLength);
            Assert.Equal(
                artifact.GetProperty("sha256").GetString(),
                Sha256(bytes));
        }

        Assert.Equal(
            "11c3309b9b0567790e28f253648f89bdb55930ae",
            document.RootElement.GetProperty("frozenAtCommit").GetString());
    }

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
        Assert.Equal("auditStatus", playbookDocument.RootElement
            .EnumerateObject().First().Name);
        Assert.Equal("anchors", layoutDocument.RootElement
            .EnumerateObject().First().Name);
    }

    [Fact]
    public void InMemoryCompilationIsByteIdenticalToCliFileCompilation()
    {
        string playbookPath = HomeSiege();
        TacticalPlaybookCompilation fromFile =
            ArcRelayTacticalPlaybookCompiler.Compile(playbookPath);

        TacticalPlaybookCompilation fromMemory =
            ArcRelayTacticalPlaybookCompiler.Compile(
                File.ReadAllBytes(playbookPath),
                File.ReadAllBytes(fromFile.LayoutPath),
                playbookPath,
                fromFile.LayoutPath);

        Assert.Equal(fromFile.PlaybookSha256, fromMemory.PlaybookSha256);
        Assert.Equal(fromFile.LayoutSha256, fromMemory.LayoutSha256);
        Assert.Equal(fromFile.NormalizedPlaybook, fromMemory.NormalizedPlaybook);
        Assert.Equal(fromFile.NormalizedLayout, fromMemory.NormalizedLayout);
        Assert.Equal(fromFile.LinkedData, fromMemory.LinkedData);
    }

    [Fact]
    public void InlineCustodyConditionsCompileIntoExpandedRuntimeGroups()
    {
        JsonObject source = AuthoredHomeSiege();
        Assert.Null(source["authoring"]!["conditionSets"]);
        Assert.NotEmpty(source["custodyPolicies"]![0]!["safeConversion"]!
            .GetValue<string>());

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HomeSiege());
        JsonObject normalized = JsonNode.Parse(
            compilation.NormalizedPlaybook)!.AsObject();
        JsonObject policy = normalized["custodyPolicies"]![0]!.AsObject();
        Assert.Null(policy["safeConversion"]);
        Assert.NotEmpty(policy["safeConversionAll"]!.AsArray());
    }

    [Fact]
    public void HomeSiegeV3BindsTheForwardRingOnlyToTheWestApproach()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HomeSiegeV3());
        using JsonDocument playbook = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        using JsonDocument layout = JsonDocument.Parse(
            compilation.NormalizedLayout);

        Assert.Contains(
            playbook.RootElement.GetProperty("formations").EnumerateArray(),
            value => value.GetProperty("formationId").GetString()
                == "living-ring-west-forward");
        JsonElement west = layout.RootElement.GetProperty("bindings")
            .EnumerateArray().Single(value => value
                .GetProperty("ownReactorSide").GetString() == "west");
        JsonElement east = layout.RootElement.GetProperty("bindings")
            .EnumerateArray().Single(value => value
                .GetProperty("ownReactorSide").GetString() == "east");
        Assert.Equal(
            "living-ring-west-forward",
            west.GetProperty("formationAliases")
                .GetProperty("living-ring").GetString());
        Assert.Empty(east.GetProperty("formationAliases").EnumerateObject());
    }

    [Fact]
    public void ManeuverCatalogExpandsToExhaustiveRuntimeOrders()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        Assert.Null(source["orders"]);
        Assert.Equal("maneuver-catalog",
            source["authoring"]!["kind"]!.GetValue<string>());
        Assert.Equal(10, source["authoring"]!["maneuvers"]!.AsObject().Count);

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HomeSiege());
        JsonObject normalized = JsonNode.Parse(compilation.NormalizedPlaybook)!
            .AsObject();
        Assert.Null(normalized["authoring"]);
        Assert.Equal(24, normalized["orders"]!.AsArray().Count);
        Assert.Contains(normalized["orders"]!.AsArray(), order =>
            order!["orderId"]!.GetValue<string>()
                == "line-task-conversion-escort"
            && order["movement"]!["kind"]!.GetValue<string>() == "carrier");
        Assert.All(normalized["coordination"]!["phases"]!.AsArray(), phase =>
            Assert.Equal(4, phase!["orderIds"]!.AsArray().Count));

        JsonObject denial = normalized["coordination"]!["tasks"]!.AsArray()
            .Select(task => task!.AsObject())
            .Single(task => task["taskId"]!.GetValue<string>()
                == "deny-visible-carrier");
        Assert.Equal(5, denial["minimumPrimaryBodies"]!.GetValue<int>());
        Assert.Single(denial["assignments"]!.AsArray());
        Assert.Contains(
            denial["completeWhen"]![0]!["all"]!.AsArray(),
            condition => condition!["fact"]!.GetValue<string>()
                    == "known-enemy-carriers"
                && condition["operator"]!.GetValue<string>() == "equals"
                && condition["value"]!.GetValue<int>() == 0);

        JsonObject conversion = normalized["coordination"]!["tasks"]!
            .AsArray().Select(task => task!.AsObject())
            .Single(task => task["taskId"]!.GetValue<string>()
                == "harvest-core-window");
        Assert.Equal(5, conversion["minimumPrimaryBodies"]!.GetValue<int>());
        Assert.Collection(
            conversion["assignments"]!.AsArray(),
            courier => Assert.Equal(
                "courier",
                courier!["assignmentId"]!.GetValue<string>()),
            escort => Assert.Equal(
                "escort",
                escort!["assignmentId"]!.GetValue<string>()));
    }

    [Fact]
    public void ContextParametersResolveIndependently()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        source["layout"]!["path"] = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(HomeSiege())!,
            source["layout"]!["path"]!.GetValue<string>()));
        source["authoring"]!["parameters"]![
            "conversion-front-enemy-unavailable"]!["value"] = 4;
        source["authoring"]!["parameters"]![
            "conversion-occupied-enemy-unavailable"]!["value"] = 2;
        string temporary = TemporaryJson(source);
        try
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(temporary);
            using JsonDocument normalized = JsonDocument.Parse(
                compilation.NormalizedPlaybook);
            JsonElement task = normalized.RootElement
                .GetProperty("coordination").GetProperty("tasks")
                .EnumerateArray().Single(value => value
                    .GetProperty("taskId").GetString()
                    == "harvest-core-window");
            Assert.Equal(2, task.GetProperty("when")[0]
                .GetProperty("all")[0].GetProperty("value").GetInt32());
            Assert.Equal(4, normalized.RootElement
                .GetProperty("custodyPolicies")[0]
                .GetProperty("safeConversionAll")[0]
                .GetProperty("all")[0]
                .GetProperty("value").GetInt32());
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void UnknownAuthoringParameterReferencesAreRejected()
    {
        JsonObject source = AuthoredHomeSiege();
        JsonObject condition = source["authoring"]!["predicates"]![
            "front-attrition-safe"]!.AsObject();
        condition["valueParameter"] = "missing-parameter";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "condition references unknown parameter 'missing-parameter'",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void UnknownPredicateReferencesAreRejected()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        source["custodyPolicies"]![0]!["safeConversion"] =
            "missing-predicate";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("condition set references unknown predicate "
                + "'missing-predicate'", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void UnknownAssignmentProfilesAreRejected()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        source["authoring"]!["maneuvers"]!["assault"]!["tracks"]![
            "main"]!["assignments"]!["runner-rush"]![
            "assignmentProfileId"] = "missing-profile";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "references unknown assignment profile 'missing-profile'",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
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
    public void ClassPriorityTermsCompile()
    {
        JsonObject source = AuthoredHomeSiege();
        source["engagements"]![0]!["targetPriorities"] =
            new JsonArray("class:kestrel", "enemy-carrier", "lowest-health");
        string temporary = TemporaryJson(source);
        try
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(temporary);
            using JsonDocument normalized = JsonDocument.Parse(
                compilation.NormalizedPlaybook);
            Assert.Equal(
                "class:kestrel",
                normalized.RootElement.GetProperty("engagements")[0]
                    .GetProperty("targetPriorities")[0].GetString());
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void EmptyClassPriorityTermsAreRejected()
    {
        JsonObject source = AuthoredHomeSiege();
        source["engagements"]![0]!["targetPriorities"] =
            new JsonArray("class:", "enemy-carrier");
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("invalid priority term 'class:'", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void AnyCompositionWildcardBindingsCompile()
    {
        (string playbook, string layout) = TemporaryLayoutVariant(source =>
        {
            JsonArray bindings = source["bindings"]!.AsArray();
            JsonObject wildcard = JsonNode.Parse(
                bindings[0]!.ToJsonString())!.AsObject();
            wildcard["matchContractFingerprint"] = "any-composition";
            bindings.Add(wildcard);
        });
        try
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(playbook);
            Assert.NotNull(compilation.NormalizedPlaybook);
        }
        finally
        {
            File.Delete(playbook);
            File.Delete(layout);
        }
    }

    [Fact]
    public void DuplicateWildcardBindingsAreRejected()
    {
        (string playbook, string layout) = TemporaryLayoutVariant(source =>
        {
            JsonArray bindings = source["bindings"]!.AsArray();
            string side = bindings[0]!["ownReactorSide"]!.GetValue<string>();
            for (int copy = 0; copy < 2; copy++)
            {
                JsonObject wildcard = JsonNode.Parse(
                    bindings[0]!.ToJsonString())!.AsObject();
                wildcard["matchContractFingerprint"] = "any-composition";
                wildcard["ownReactorSide"] = side;
                bindings.Add(wildcard);
            }
        });
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(playbook));
            Assert.Contains("duplicate layout binding", failure.Message);
        }
        finally
        {
            File.Delete(playbook);
            File.Delete(layout);
        }
    }

    [Fact]
    public void BindingParameterOverridesCompileWithinDeclaredRanges()
    {
        (string playbook, string layout) = TemporaryLayoutVariant(source =>
        {
            JsonObject binding = source["bindings"]![0]!.AsObject();
            binding["parameterOverrides"] = new JsonObject
            {
                ["conversion-front-enemy-unavailable"] = 4,
            };
        });
        try
        {
            Assert.NotNull(ArcRelayTacticalPlaybookCompiler
                .Compile(playbook).NormalizedPlaybook);
        }
        finally
        {
            File.Delete(playbook);
            File.Delete(layout);
        }
    }

    [Fact]
    public void BindingParameterOverridesRejectUnknownNamesAndRangeBreaks()
    {
        foreach ((JsonNode value, string expected) in new (JsonNode, string)[]
        {
            (new JsonObject { ["no-such-parameter"] = 3 },
                "unknown parameter 'no-such-parameter'"),
            (new JsonObject { ["conversion-front-enemy-unavailable"] = 99999 },
                "must be an integer in"),
        })
        {
            (string playbook, string layout) = TemporaryLayoutVariant(source =>
            {
                source["bindings"]![0]!.AsObject()["parameterOverrides"] =
                    value.DeepClone();
            });
            try
            {
                InvalidDataException failure =
                    Assert.Throws<InvalidDataException>(() =>
                        ArcRelayTacticalPlaybookCompiler.Compile(playbook));
                Assert.Contains(expected, failure.Message);
            }
            finally
            {
                File.Delete(playbook);
                File.Delete(layout);
            }
        }
    }

    [Fact]
    public void ForwardPassOptInCompilesAndRejectsUnknownModes()
    {
        JsonObject source = AuthoredHomeSiege();
        source["custodyPolicies"]![0]!["forwardPass"] = "relay-catcher";
        string accepted = TemporaryJson(source);
        source["custodyPolicies"]![0]!["forwardPass"] = "yeet";
        string rejected = TemporaryJson(source);
        try
        {
            Assert.NotNull(ArcRelayTacticalPlaybookCompiler
                .Compile(accepted).NormalizedPlaybook);
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(rejected));
            Assert.Contains("forwardPass", failure.Message);
        }
        finally
        {
            File.Delete(accepted);
            File.Delete(rejected);
        }
    }

    [Fact]
    public void FactVariantsRejectIrrelevantFields()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject condition = source["authoring"]!["predicates"]![
            "front-attrition-safe"]!.AsObject();
        condition["zone"] = "enemy-home";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("unknown field 'zone'", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void MemoryFactsAcceptAnExplicitBoundedFreshnessWindow()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject layout = source["layout"]!.AsObject();
        layout["path"] = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(HomeSiege())!,
            layout["path"]!.GetValue<string>()));
        JsonObject condition = source["authoring"]!["predicates"]![
            "secured-core"]!.AsObject();
        Assert.Equal("secured-cores", condition["fact"]!.GetValue<string>());
        condition["freshnessTicks"] = 12;
        string temporary = TemporaryJson(source);
        try
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(temporary);
            using JsonDocument normalized = JsonDocument.Parse(
                compilation.NormalizedPlaybook);
            Assert.Equal(12, normalized.RootElement
                .GetProperty("custodyPolicies")[0]
                .GetProperty("safeConversionAll")[0]
                .GetProperty("all")[2]
                .GetProperty("freshnessTicks")
                .GetInt32());
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void NonMemoryFactsRejectFreshnessInsteadOfIgnoringIt()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject condition = source["authoring"]!["predicates"]![
            "front-attrition-safe"]!.AsObject();
        Assert.Equal(
            "known-enemies-unavailable",
            condition["fact"]!.GetValue<string>());
        condition["freshnessTicks"] = 12;
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("unknown field 'freshnessTicks'", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void ConditionStringsRequireFlatExplicitBooleanGrammar()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        source["custodyPolicies"]![0]!["safeConversion"] =
            "(secured-core and front-attrition-safe)";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("is not of the form", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void TransitionMinimumPolicyIsExplicitAndBounded()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject transition = source["coordination"]!["phases"]![0]!
            ["transitions"]![0]!.AsObject();
        transition["minimumPolicy"] = "sometimes";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("minimumPolicy", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void CarrierEscortOrdersRequireCustodyGroupAuthorization()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject runnerOrder = source["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["orderId"]!.GetValue<string>()
                == "runner-rush");
        runnerOrder["movement"]!["kind"] = "carrier";
        runnerOrder["movement"]!["target"] = "";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("is not authorized by custody", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void EnemyCarrierMovementIsASeparateBoundedMovementVariant()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject layout = source["layout"]!.AsObject();
        layout["path"] = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(HomeSiege())!,
            layout["path"]!.GetValue<string>()));
        JsonObject lineOrder = source["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["orderId"]!.GetValue<string>()
                == "line-siege");
        lineOrder["movement"]!["kind"] = "enemy-carrier";
        lineOrder["movement"]!["target"] = "enemy-perimeter";
        lineOrder["movement"]!["chaseLeash"] = 6;
        string temporary = TemporaryJson(source);
        try
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(temporary);
            Assert.NotEmpty(compilation.LinkedData);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void EngagementCanPrioritizeTheCarrierClosestToItsBank()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject layout = source["layout"]!.AsObject();
        layout["path"] = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(HomeSiege())!,
            layout["path"]!.GetValue<string>()));
        JsonObject siege = source["engagements"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["engagementId"]!.GetValue<string>()
                == "siege-focus");
        siege["tieBreakers"] = new JsonArray(
            "enemy-reactor-distance", "health", "unit-id");
        string temporary = TemporaryJson(source);
        try
        {
            Assert.NotEmpty(ArcRelayTacticalPlaybookCompiler
                .Compile(temporary).LinkedData);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void SecuredCoreGuardRequiresAnExplicitCustodyPolicy()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject layout = source["layout"]!.AsObject();
        layout["path"] = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(HomeSiege())!,
            layout["path"]!.GetValue<string>()));
        JsonObject runnerOrder = source["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["orderId"]!.GetValue<string>()
                == "runner-siege");
        runnerOrder["movement"]!["kind"] = "secured-core";
        runnerOrder.Remove("custodyId");
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "missing required field 'custodyId'",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void MovementCompletionFactsReferenceDeclaredOrders()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject layout = source["layout"]!.AsObject();
        layout["path"] = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(HomeSiege())!,
            layout["path"]!.GetValue<string>()));
        JsonObject transition = source["coordination"]!["phases"]![0]!
            ["transitions"]![0]!.AsObject();
        JsonArray conditions = transition["when"]![0]!["all"]!.AsArray();
        conditions.Add(new JsonObject
        {
            ["fact"] = "movement-complete",
            ["subject"] = "line-rush",
            ["operator"] = "equals",
            ["value"] = 1,
        });
        string valid = TemporaryJson(source);
        try
        {
            Assert.NotEmpty(ArcRelayTacticalPlaybookCompiler
                .Compile(valid).LinkedData);
            conditions[^1]!["subject"] = "missing-order";
            string invalid = TemporaryJson(source);
            try
            {
                InvalidDataException failure =
                    Assert.Throws<InvalidDataException>(() =>
                        ArcRelayTacticalPlaybookCompiler.Compile(invalid));
                Assert.Contains(
                    "unknown order 'missing-order'", failure.Message);
            }
            finally
            {
                File.Delete(invalid);
            }
        }
        finally
        {
            File.Delete(valid);
        }
    }

    [Fact]
    public void MovementAndFormationPaceCannotContradict()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject order = source["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .First();
        string original = order["movement"]!["pace"]!.GetValue<string>();
        order["movement"]!["pace"] = original == "free"
            ? "slowest"
            : "free";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("conflicts with formation", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void CustodyCannotAuthorizeARoleThatForbidsCarrying()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject runner = source["roles"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["roleId"]!.GetValue<string>()
                == "runner");
        runner["carrierPreference"] = "forbid";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "forbids Core custody but is an authorized carrier",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void RoleOwnershipAcrossGroupsMustBeUnambiguous()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject line = source["groups"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["groupId"]!.GetValue<string>()
                == "line-group");
        line["roleIds"]!.AsArray().Add("runner");
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "role 'runner' must belong to exactly one group",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void RoleAndGroupCasualtyPoliciesCannotContradict()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject runner = source["groups"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["groupId"]!.GetValue<string>()
                == "runner-group");
        runner["membership"]!["casualty"] = "hold-vacancy";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("conflicts with group casualty", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void GroupPreferredMustCoverOwnedRoleMinima()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject medics = source["groups"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["groupId"]!.GetValue<string>()
                == "medic-group");
        medics["preferred"] = 1;
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "preferred cardinality cannot satisfy its roles' minimum",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void GroupMaximumCannotExceedOwnedRoleCapacity()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject runner = source["groups"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["groupId"]!.GetValue<string>()
                == "runner-group");
        runner["maximum"] = 2;
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "cardinality exceeds the capacity of its owned roles",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void EveryPhaseMustCoverEveryGroupLocalStateExplicitly()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject occupy = source["coordination"]!["phases"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["phaseId"]!.GetValue<string>()
                == "occupy");
        occupy["orderIds"]!.AsArray().Remove(
            occupy["orderIds"]!.AsArray()
                .Single(value => value!.GetValue<string>()
                    == "line-recover"));
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "has no order for group 'line-group' local state "
                + "'recovering'",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void PhaseCanSplitAStableGroupIntoTakeAndRemainderOrders()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject occupy = source["coordination"]!["phases"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["phaseId"]!.GetValue<string>()
                == "occupy");
        JsonObject field = source["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["orderId"]!.GetValue<string>()
                == "medics-siege");
        field["members"] = new JsonObject
        {
            ["kind"] = "take",
            ["roles"] = new JsonArray("medic"),
            ["classes"] = new JsonArray("patchbay"),
            ["count"] = 1,
        };
        JsonObject collection = JsonNode.Parse(field.ToJsonString())!
            .AsObject();
        collection["orderId"] = "medics-siege-remainder";
        collection["priority"] = 21;
        collection["members"] = new JsonObject
        {
            ["kind"] = "remainder",
        };
        source["orders"]!.AsArray().Add(collection);
        occupy["orderIds"]!.AsArray().Add("medics-siege-remainder");
        string temporary = TemporaryJson(source);
        try
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(temporary);
            Assert.NotEmpty(compilation.LinkedData);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void SplitGroupCannotOmitItsRemainderOrder()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject field = source["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["orderId"]!.GetValue<string>()
                == "medics-siege");
        field["members"] = new JsonObject
        {
            ["kind"] = "take",
            ["roles"] = new JsonArray("medic"),
            ["classes"] = new JsonArray("patchbay"),
            ["count"] = 1,
        };
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "require one or more take selections followed by exactly one "
                + "remainder selection",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void AuthoredTaskCompilesToExplicitLifecycleAndParticipantIr()
    {
        JsonObject source = AuthoredHomeSiege();
        source["coordination"]!["tasks"]!.AsArray().Add(TaskCard());
        string temporary = TemporaryJson(source);
        try
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(temporary);
            using JsonDocument normalized = JsonDocument.Parse(
                compilation.NormalizedPlaybook);
            JsonElement task = normalized.RootElement
                .GetProperty("coordination")
                .GetProperty("tasks").EnumerateArray()
                .Single(value => value.GetProperty("taskId").GetString()
                    == "convert-core");
            Assert.Equal("convert-core", task.GetProperty("taskId").GetString());
            Assert.Equal(
                "line-siege",
                task.GetProperty("assignments")[0]
                    .GetProperty("orderId").GetString());
            Assert.Equal(5,
                task.GetProperty("minimumPrimaryBodies").GetInt32());
            Assert.Single(task.GetProperty("when").EnumerateArray());
            Assert.Equal(
                "primary-order",
                task.GetProperty("reintegration")
                    .GetProperty("mode").GetString());
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void ExplainExpandsTaskOrdersWithoutHidingLifecycleConditions()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HomeSiege());
        var explainMethod = typeof(ArcRelayTacticalPlaybookCommand).GetMethod(
            "Explain",
            System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static)!;
        using JsonDocument explain = JsonDocument.Parse(
            (byte[])explainMethod.Invoke(null, [compilation])!);

        JsonElement task = explain.RootElement.GetProperty("tasks")
            .EnumerateArray().Single(value => value.GetProperty("taskId")
                .GetString() == "harvest-core-window");
        Assert.Equal(
            "harvest-core-window",
            task.GetProperty("taskId").GetString());
        Assert.Equal(
            "runner-harvest",
            task.GetProperty("assignments")[0]
                .GetProperty("order").GetProperty("orderId").GetString());
        Assert.NotEmpty(task.GetProperty("when").EnumerateArray());
        Assert.Equal(
            "primary-order",
            task.GetProperty("reintegration").GetProperty("mode").GetString());
        Assert.Equal(0, task.GetProperty("reintegration")
            .GetProperty("orders").GetArrayLength());
    }

    [Fact]
    public void TaskRejectsImpossibleParticipantCardinality()
    {
        JsonObject source = AuthoredHomeSiege();
        JsonObject task = TaskCard();
        task["assignments"]![0]!["minimum"] = 2;
        source["coordination"]!["tasks"]!.AsArray().Add(task);
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("cardinality must satisfy", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void TaskRejectsUnknownOrdersAndSelectionAnchors()
    {
        JsonObject source = AuthoredHomeSiege();
        JsonObject task = TaskCard();
        task["assignments"]![0]!["orderId"] = "missing-order";
        source["coordination"]!["tasks"]!.AsArray().Add(task);
        string missingOrder = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(missingOrder));
            Assert.Contains(
                "references unknown 'missing-order'",
                failure.Message);
        }
        finally
        {
            File.Delete(missingOrder);
        }

        source = AuthoredHomeSiege();
        task = TaskCard();
        task["assignments"]![0]!["distance"]!["target"] = "missing-anchor";
        source["coordination"]!["tasks"]!.AsArray().Add(task);
        string missingAnchor = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(missingAnchor));
            Assert.Contains("unknown selection anchor", failure.Message);
        }
        finally
        {
            File.Delete(missingAnchor);
        }
    }

    [Fact]
    public void PrimaryOrderReintegrationRejectsHiddenReleaseBehavior()
    {
        JsonObject source = AuthoredHomeSiege();
        JsonObject task = TaskCard();
        task["reintegration"]!["timeoutTicks"] = 5;
        source["coordination"]!["tasks"]!.AsArray().Add(task);
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "primary-order reintegration cannot declare",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void ReleaseOrdersMustCoverEveryPossibleGroupLocalStateExactlyOnce()
    {
        JsonObject source = AuthoredHomeSiege();
        JsonObject task = TaskCard();
        task["reintegration"] = new JsonObject
        {
            ["mode"] = "release-orders",
            ["orderIds"] = new JsonArray("line-siege", "line-recover"),
            ["complete"] = "no-live-friendlies",
            ["timeoutTicks"] = 20,
        };
        source["coordination"]!["tasks"]!.AsArray().Add(task);
        string complete = TemporaryJson(source);
        try
        {
            ArcRelayTacticalPlaybookCompiler.Compile(complete);
        }
        finally
        {
            File.Delete(complete);
        }

        task["reintegration"]!["orderIds"] = new JsonArray("line-siege");
        string incomplete = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(incomplete));
            Assert.Contains(
                "local state 'recovering' exactly once; found 0",
                failure.Message);
        }
        finally
        {
            File.Delete(incomplete);
        }
    }

    [Fact]
    public void OrderLocalStateMustBelongToItsGroup()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject runner = source["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["orderId"]!.GetValue<string>()
                == "runner-rush");
        runner["localState"] = "recovering";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "references unknown local state 'recovering' in group "
                + "'runner-group'",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void MovementTargetMustResolveInTheBoundLayoutByKind()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject layout = source["layout"]!.AsObject();
        layout["path"] = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(HomeSiege())!,
            layout["path"]!.GetValue<string>()));
        JsonObject runner = source["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["orderId"]!.GetValue<string>()
                == "runner-rush");
        runner["movement"]!["target"] = "missing-route";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "invalid route movement target 'missing-route'",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void ConditionZoneMustResolveInTheBoundLayout()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject layout = source["layout"]!.AsObject();
        layout["path"] = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(HomeSiege())!,
            layout["path"]!.GetValue<string>()));
        JsonObject condition = source["authoring"]!["predicates"]![
            "six-in-siege"]!.AsObject();
        Assert.Equal(
            "friendlies-in-zone-count",
            condition["fact"]!.GetValue<string>());
        condition["zone"] = "missing-zone";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "condition references unknown tactical zone 'missing-zone'",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void FormationMustProvideDistinctSlotsForRoleCapacity()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        JsonObject formation = source["formations"]![0]!.AsObject();
        JsonArray offsets = formation["placementBands"]![0]!["offsets"]!
            .AsArray();
        offsets[1] = JsonNode.Parse(offsets[0]!.ToJsonString());
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("overlapping slots", failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void PhaseFallbackMustNameAnExistingPhase()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject order = source["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["orderId"]!.GetValue<string>()
                == "runner-siege");
        order["fallback"]!["phaseId"] = "missing";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "fallback references unknown phase 'missing'",
                failure.Message);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void UnusedFallbackPhaseIsRejectedAsIrrelevant()
    {
        JsonObject source = ExpandedHomeSiege();
        JsonObject order = source["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["orderId"]!.GetValue<string>()
                == "runner-rush");
        order["fallback"]!["phaseId"] = "regroup";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("is irrelevant to its actions", failure.Message);
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
            loopProfile: ArcRelayLoopProfile.ForwardCombat);
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(
                ActorContractManifestSerializer.ToCanonicalJson(definition));

        TacticalPlaybookPackage package = TacticalPlaybookPackage.Load(
            compilation.LinkedData.ToImmutableArray(),
            contract,
            new BotArena.Sdk.Position(2, 11));

        Assert.Equal(compilation.PlaybookSha256, package.PlaybookSha256);
        Assert.Equal(compilation.LayoutSha256, package.LayoutSha256);
        Assert.Equal(new BotArena.Sdk.Position(26, 11),
            package.AnchorPosition("enemy-perimeter"));
        Assert.Equal(2, package.RouteCorridorWidth("outer-rush"));
        TacticalPlaybookPackage.Condition condition = package.Source
            .CustodyPolicies[0].SafeConversionAll[0].All[0];
        Assert.Equal("", condition.Subject);
        Assert.Equal("", condition.Zone);
        Assert.Equal(0, condition.FreshnessTicks);
        Assert.Equal(
            "incidental-delivery",
            package.Source.Orders.Single(value =>
                value.OrderId == "medics-rush").CustodyId);
    }

    [Fact]
    public void CompiledHomeSiegeStartsInTheActualTacticalMind()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HomeSiege());
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            compilation.Composition,
            BaselineComposition(),
            loopProfile: ArcRelayLoopProfile.ForwardCombat);
        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(
                ActorContractManifestSerializer.ToCanonicalJson(definition));
        var mind = new ArcRelayTacticalPlaybookMind();

        mind.StartMatch(new MindStart
        {
            SchemaVersion = 1,
            RuntimeContractVersion = 1,
            ParticipantId = 0,
            TeamId = 0,
            AlliedParticipantIds = [],
            MindRandomSeed = 1,
            TeamRandomSeed = 2,
            Contract = contract,
            EvaluationData = compilation.LinkedData.ToImmutableArray(),
        });
    }

    private static JsonObject TaskCard() => new()
    {
        ["taskId"] = "convert-core",
        ["priority"] = 20,
        ["activation"] = "while-true",
        ["preemption"] = "higher-priority",
        ["participantLoss"] = "replace",
        ["triggerStableTicks"] = 2,
        ["minimumTicks"] = 2,
        ["timeoutTicks"] = 90,
        ["cooldownTicks"] = 4,
        ["minimumPrimaryBodies"] = 5,
        ["eligiblePhases"] = new JsonArray("occupy"),
        ["assignments"] = new JsonArray
        {
            new JsonObject
            {
                ["assignmentId"] = "courier",
                ["orderId"] = "line-siege",
                ["roles"] = new JsonArray("line"),
                ["classes"] = new JsonArray("kestrel", "relay"),
                ["minimum"] = 1,
                ["preferred"] = 1,
                ["maximum"] = 1,
                ["carrier"] = "forbid",
                ["distance"] = new JsonObject
                {
                    ["kind"] = "anchor",
                    ["target"] = "enemy-perimeter",
                },
            },
        },
        ["when"] = "always",
        ["complete"] = "no-live-friendlies",
        ["reintegration"] = new JsonObject
        {
            ["mode"] = "primary-order",
            ["orderIds"] = new JsonArray(),
            ["timeoutTicks"] = 0,
        },
    };

    private static JsonObject AuthoredHomeSiege()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        source["layout"]!["path"] = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(HomeSiege())!,
            source["layout"]!["path"]!.GetValue<string>()));
        return source;
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

    private static (string Playbook, string Layout) TemporaryLayoutVariant(
        Action<JsonObject> mutate)
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        string realLayout = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(HomeSiege())!,
            source["layout"]!["path"]!.GetValue<string>()));
        JsonObject layout = JsonNode.Parse(File.ReadAllText(realLayout))!
            .AsObject();
        mutate(layout);
        string layoutPath = TemporaryJson(layout);
        source["layout"]!["path"] = layoutPath;
        source["layout"]!["sha256"] = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(layoutPath))).ToLowerInvariant();
        return (TemporaryJson(source), layoutPath);
    }

    private static JsonObject ExpandedHomeSiege()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HomeSiege());
        JsonObject expanded = JsonNode.Parse(compilation.NormalizedPlaybook)!
            .AsObject();
        DenormalizeSource(expanded);
        expanded["layout"]!["path"] = compilation.LayoutPath;
        return expanded;
    }

    private static void DenormalizeSource(JsonNode node)
    {
        if (node is JsonObject value)
        {
            // Compiler-owned links from the authored doctrine plane into the
            // runtime IR are not valid direct-source fields. These fixtures
            // deliberately turn normalized IR back into legacy direct input.
            value.Remove("fetchRungs");
            value.Remove("modeRungs");
            if (value.ContainsKey("fact"))
            {
                if (value["subject"]?.GetValue<string>() == "")
                    value.Remove("subject");
                if (value["zone"]?.GetValue<string>() == "")
                    value.Remove("zone");
                if (value["freshnessTicks"]?.GetValue<int>() == 0)
                    value.Remove("freshnessTicks");
            }
            if (value["all"] is JsonArray all && all.Count == 0)
                value.Remove("all");
            if (value["any"] is JsonArray any && any.Count == 0)
                value.Remove("any");
            if (value.ContainsKey("orderId"))
            {
                if (value["supportId"]?.GetValue<string>() == "")
                    value.Remove("supportId");
                if (value["custodyId"]?.GetValue<string>() == "")
                    value.Remove("custodyId");
            }
            foreach (JsonNode? child in value.Select(item => item.Value)
                         .ToArray())
            {
                if (child is not null)
                    DenormalizeSource(child);
            }
            return;
        }
        if (node is not JsonArray array)
            return;
        foreach (JsonNode? child in array)
        {
            if (child is not null)
                DenormalizeSource(child);
        }
    }

    [Fact]
    public void StandardLibraryEditionCompilesToTheFrozenNormalizedIr()
    {
        TacticalPlaybookCompilation frozen =
            ArcRelayTacticalPlaybookCompiler.Compile(HomeSiegeV3());
        TacticalPlaybookCompilation libraryEdition =
            ArcRelayTacticalPlaybookCompiler.Compile(HomeSiegeV3Library());

        // The runtime consumes only the normalized IR: identical IR means
        // identical behavior on every seed. The linked package hash is
        // ALLOWED to differ because it binds source-byte provenance.
        Assert.Equal(
            frozen.NormalizedPlaybook,
            libraryEdition.NormalizedPlaybook);
        Assert.Equal(
            frozen.NormalizedLayout,
            libraryEdition.NormalizedLayout);
        Assert.NotEqual(
            frozen.PlaybookSha256,
            libraryEdition.PlaybookSha256);
    }

    [Fact]
    public void LibraryAndPlaybookMayNotDefineTheSameEntry()
    {
        string root = FindRepoRoot();
        string temporary = Directory.CreateTempSubdirectory(
            "arc-library-collision").FullName;
        JsonObject playbook = JsonNode.Parse(
            File.ReadAllBytes(HomeSiegeV3Library()))!.AsObject();
        JsonObject authoring = playbook["authoring"]!.AsObject();
        authoring["library"]!.AsObject()["path"] = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "library",
            "standard-v1.json");
        playbook["layout"]!.AsObject()["path"] = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "layouts",
            "counterflow-home-siege-v3.json");
        // 'always' already lives in the standard library; redefining it
        // locally must be rejected as a collision, never an override.
        authoring["predicates"]!.AsObject()["always"] = new JsonObject
        {
            ["fact"] = "tick",
            ["operator"] = "at-least",
            ["value"] = 0,
        };
        string collision = Path.Combine(temporary, "collision.json");
        File.WriteAllText(collision, playbook.ToJsonString());

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => ArcRelayTacticalPlaybookCompiler.Compile(collision));
        Assert.Contains("defined by both the library", error.Message);
    }

    [Fact]
    public void FollowerOwnedFollowOrdersHavePredictableSelectorsAndNoFormation()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HunterV1());
        using JsonDocument playbook = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        JsonElement[] followOrders = playbook.RootElement.GetProperty("orders")
            .EnumerateArray().Where(order => order.TryGetProperty(
                "follow", out _))
            .ToArray();
        Assert.Equal(2, followOrders.Length);
        foreach (JsonElement order in followOrders)
        {
            Assert.Equal("", order.GetProperty("formationId").GetString());
            Assert.Equal("free",
                order.GetProperty("movement").GetProperty("pace").GetString());
            JsonElement follow = order.GetProperty("follow");
            Assert.Equal("hunter", follow.GetProperty("leaderRole").GetString());
            Assert.Equal("nearest", follow.GetProperty("selector").GetString());
            Assert.Equal(2, follow.GetProperty("leash").GetInt32());
            Assert.False(order.TryGetProperty("escort", out _));
        }
        Assert.Equal("trail", followOrders.Single(order => order
                .GetProperty("orderId").GetString() == "medic-follow")
            .GetProperty("follow").GetProperty("posture").GetString());
        Assert.Equal("screen", followOrders.Single(order => order
                .GetProperty("orderId").GetString() == "lancer-follow")
            .GetProperty("follow").GetProperty("posture").GetString());
    }

    [Fact]
    public void ConditionCoordinationKeepsTasksRoleLocalAndFocusContextual()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HunterV1());
        using JsonDocument playbook = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        Assert.All(playbook.RootElement.GetProperty("coordination")
            .GetProperty("tasks").EnumerateArray(), task =>
        {
            string[] roles = task.GetProperty("assignments").EnumerateArray()
                .SelectMany(row => row.GetProperty("roles").EnumerateArray())
                .Select(role => role.GetString()!).Distinct(
                    StringComparer.Ordinal).ToArray();
            Assert.Single(roles);
        });
        JsonElement[] engagements = playbook.RootElement
            .GetProperty("engagements").EnumerateArray().ToArray();
        Assert.All(engagements, engagement => Assert.StartsWith(
            "focus:", engagement.GetProperty("coordinationScope").GetString()));
        Assert.Contains(engagements, engagement =>
            engagement.GetProperty("coordinationScope").GetString()
                == "focus:assassin"
            && engagement.GetProperty("maximumAttackersPerTarget").GetInt32()
                == 1);
        Assert.Contains(engagements, engagement =>
            engagement.GetProperty("coordinationScope").GetString()
                == "focus:well"
            && engagement.GetProperty("maximumAttackersPerTarget").GetInt32()
                == 3);
    }

    [Fact]
    public void FollowIsOwnedByTheFollowersOwnTaskAndOrder()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HunterV1());
        using JsonDocument playbook = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        JsonElement[] tasks = playbook.RootElement
            .GetProperty("coordination")
            .GetProperty("tasks")
            .EnumerateArray()
            .ToArray();
        foreach (string role in new[] { "medic", "lancer" })
        {
            JsonElement follow = tasks.Single(task =>
                task.GetProperty("taskId").GetString() == $"{role}-follow");
            JsonElement row = Assert.Single(
                follow.GetProperty("assignments").EnumerateArray());
            Assert.Equal($"{role}-follow", row.GetProperty("orderId").GetString());
            Assert.Equal(role, Assert.Single(
                row.GetProperty("roles").EnumerateArray()).GetString());
            Assert.Equal(1, follow.GetProperty("priority").GetInt32());
        }
    }

    [Fact]
    public void RecoverOutranksFollowInTheFollowersOwnLadder()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HunterV1());
        using JsonDocument playbook = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        JsonElement[] tasks = playbook.RootElement
            .GetProperty("coordination")
            .GetProperty("tasks")
            .EnumerateArray()
            .ToArray();
        int recover = tasks
            .Single(task => task.GetProperty("taskId").GetString()
                == "medic-recover")
            .GetProperty("priority").GetInt32();
        int follow = tasks
            .Single(task => task.GetProperty("taskId").GetString()!
                .Equals("medic-follow", StringComparison.Ordinal))
            .GetProperty("priority").GetInt32();
        Assert.True(recover < follow,
            $"recover ({recover}) must outrank follow ({follow})");
    }

    [Fact]
    public void FetchRungsDeriveCustodyConsentAndLeaveNoAuthoredArbiter()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HunterV1());
        using JsonDocument playbook = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        JsonElement root = playbook.RootElement;
        Assert.False(root.TryGetProperty("arbitration", out _));

        JsonElement[] rungs = root.GetProperty("fetchRungs")
            .EnumerateArray().ToArray();
        Assert.Equal(
            ["hauler", "carrier", "lancer", "eyes", "reserve"],
            rungs.Select(rung => rung.GetProperty("roleId").GetString()!)
                .ToArray());
        Assert.All(rungs, rung => Assert.Equal(
            "well-custody",
            rung.GetProperty("custodyId").GetString()));

        JsonElement custody = Assert.Single(root
            .GetProperty("custodyPolicies").EnumerateArray());
        Assert.Equal(
            rungs.Select(rung => rung.GetProperty("roleId").GetString()!),
            custody.GetProperty("authorizedCarrierRoles")
                .EnumerateArray().Select(role => role.GetString()!));

        // Fetch is stated first in every courier role's own ladder.
        Assert.Equal(0, rungs.Single(rung =>
                rung.GetProperty("roleId").GetString() == "lancer")
            .GetProperty("priority").GetInt32());
        JsonElement lancerFollow = root.GetProperty("modeRungs")
            .EnumerateArray().Single(rung => rung.GetProperty("roleId")
                .GetString() == "lancer"
                && rung.GetProperty("taskId").GetString() == "lancer-follow");
        Assert.Equal(1, lancerFollow.GetProperty("priority").GetInt32());
    }

    [Fact]
    public void FetchMayNameTwoCustodiesAtTwoPriorities()
    {
        string root = FindRepoRoot();
        string path = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "playbooks",
            "ripen-harvest-v1.json");
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(path);
        using JsonDocument playbook = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        JsonElement[] runner = playbook.RootElement.GetProperty("fetchRungs")
            .EnumerateArray()
            .Where(rung => rung.GetProperty("roleId").GetString() == "runner")
            .ToArray();
        Assert.Equal(2, runner.Length);
        Assert.Equal(
            ["orchard-custody", "farm-outer"],
            runner.Select(rung => rung.GetProperty("custodyId").GetString()!)
                .ToArray());
        Assert.Equal(
            [5, 6],
            runner.Select(rung => rung.GetProperty("priority").GetInt32())
                .ToArray());
    }

    [Fact]
    public void RetiredCustodyAndArbitrationFieldsAreRejected()
    {
        JsonObject source = JsonNode.Parse(
            File.ReadAllBytes(HunterV1()))!.AsObject();
        source["arbitration"] = new JsonObject
        {
            ["mode"] = "first-legal",
            ["channels"] = new JsonArray(
                "custody-emergency", "self-preservation", "repair",
                "signature", "focus-fire", "movement", "facing", "hold"),
        };
        string arbitrationPath = Write(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(
                    arbitrationPath));
            Assert.Contains("unknown field 'arbitration'", failure.Message);
        }
        finally
        {
            File.Delete(arbitrationPath);
        }

        source = JsonNode.Parse(File.ReadAllBytes(HunterV1()))!.AsObject();
        source["custodyPolicies"]![0]!["authorizedCarrierRoles"] =
            new JsonArray("hauler");
        string rosterPath = Write(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(rosterPath));
            Assert.Contains("conflicting", failure.Message);
        }
        finally
        {
            File.Delete(rosterPath);
        }
    }

    [Fact]
    public void FetchRejectsAnUnknownOrRepeatedCustody()
    {
        JsonObject source = JsonNode.Parse(
            File.ReadAllBytes(HunterV1()))!.AsObject();
        JsonArray modes = source["doctrines"]!["roles"]!["lancer"]!["modes"]!
            .AsArray();
        modes[0]!["fetch"] = "missing-custody";
        string unknownPath = Write(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(unknownPath));
            Assert.Contains("unknown custody", failure.Message);
        }
        finally
        {
            File.Delete(unknownPath);
        }

        source = JsonNode.Parse(File.ReadAllBytes(HunterV1()))!.AsObject();
        modes = source["doctrines"]!["roles"]!["lancer"]!["modes"]!.AsArray();
        modes.Insert(1, new JsonObject { ["fetch"] = "well-custody" });
        string repeatedPath = Write(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(repeatedPath));
            Assert.Contains("twice", failure.Message);
        }
        finally
        {
            File.Delete(repeatedPath);
        }
    }

    [Fact]
    public void FollowNeedsNoCallerConsent()
    {
        JsonObject source = JsonNode.Parse(
            File.ReadAllBytes(HunterV1()))!.AsObject();
        JsonArray hunterModes = source["doctrines"]!["roles"]!["hunter"]!
            ["modes"]!.AsArray();
        JsonNode assault = hunterModes.Single(mode =>
            mode!.AsObject().ContainsKey("assault"))!;
        hunterModes.Remove(assault);
        string path = Write(source);
        TacticalPlaybookCompilation compilation;
        try
        {
            compilation = ArcRelayTacticalPlaybookCompiler.Compile(path);
        }
        finally
        {
            File.Delete(path);
        }
        using JsonDocument normalized = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        Assert.Contains(normalized.RootElement.GetProperty("orders")
            .EnumerateArray(), order => order.GetProperty("orderId")
                .GetString() == "medic-follow");
    }

    [Fact]
    public void MultiBodyFollowUsesTheFixedNearestSelector()
    {
        JsonObject source = JsonNode.Parse(
            File.ReadAllBytes(HunterV1()))!.AsObject();
        JsonObject followMode = source["doctrines"]!["roles"]!["medic"]!
            ["modes"]!.AsArray().Select(mode => mode!.AsObject())
            .Single(mode => mode.ContainsKey("follow"));
        followMode["follow"] = "carrier";
        string path = Write(source);
        TacticalPlaybookCompilation compilation;
        try
        {
            compilation = ArcRelayTacticalPlaybookCompiler.Compile(path);
        }
        finally
        {
            File.Delete(path);
        }
        using JsonDocument playbook = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        JsonElement follow = playbook.RootElement.GetProperty("orders")
            .EnumerateArray().Single(order => order.GetProperty("orderId")
                .GetString() == "medic-follow").GetProperty("follow");
        Assert.Equal("carrier", follow.GetProperty("leaderRole").GetString());
        Assert.Equal("nearest", follow.GetProperty("selector").GetString());
    }

    [Fact]
    public void ARoleMayNotFollowItself()
    {
        JsonObject source = JsonNode.Parse(
            File.ReadAllBytes(HunterV1()))!.AsObject();
        source["doctrines"]!["roles"]!["medic"]!["modes"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value.ContainsKey("follow"))["follow"] = "medic";

        // Written beside the original so the relative layout reference and
        // its pinned hash still resolve.
        string path = Path.Combine(
            Path.GetDirectoryName(HunterV1())!,
            $"hunter-v1-self-escort-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, source.ToJsonString());
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(path));
            Assert.Contains("cannot follow itself", failure.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The break-off ladder: every patrol-verb mode's order and
    /// route, strongest first, floor last — so a latched body can rally to
    /// the highest one that is actually running.</summary>
    [Fact]
    public void ABreakOffCarriesThePatrolLadderNotJustTheFloor()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HunterV1());
        using JsonDocument playbook = JsonDocument.Parse(
            compilation.NormalizedPlaybook);
        string engagementId = playbook.RootElement.GetProperty("orders")
            .EnumerateArray().Single(value => value.GetProperty("orderId")
                .GetString() == "hunter-patrol")
            .GetProperty("engagementId").GetString()!;
        JsonElement disengage = playbook.RootElement
            .GetProperty("engagements")
            .EnumerateArray()
            .Single(value => value.GetProperty("engagementId").GetString()
                == engagementId)
            .GetProperty("commit")
            .GetProperty("disengageWhen");
        Assert.Equal(
            "shadow-hunt-loop",
            disengage.GetProperty("withdrawTo").GetString());
        (string Order, string Route)[] ladder = disengage
            .GetProperty("withdrawRoutes")
            .EnumerateArray()
            .Select(rung => (
                rung.GetProperty("orderId").GetString()!,
                rung.GetProperty("route").GetString()!))
            .ToArray();
        Assert.Equal(
            [("hunter-patrol", "stage-loop")],
            ladder);
    }

    /// <summary>`role-health` is the weakest live body of a role, and it is
    /// a role-subject fact like `role-live-count`.</summary>
    [Fact]
    public void RoleHealthIsAKnownFactAndItsSubjectMustBeARole()
    {
        // The committed sheet gates its safe-roads patrol on it, so a clean
        // compile is the positive case: an unknown fact is a hard error.
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HunterV1());
        Assert.NotEmpty(compilation.NormalizedPlaybook);

        JsonObject source = JsonNode.Parse(
            File.ReadAllBytes(HunterV1()))!.AsObject();
        JsonObject predicate = source["conditions"]![
            "ghost-hurt"]!.AsObject();
        predicate["fact"] = "role-health";
        predicate["subject"] = "not-a-role";
        string path = Write(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(path));
            Assert.Contains("unknown role 'not-a-role'", failure.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SelfFactsAreCandidateScopedAndTakeNoRepeatedSubject()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HunterV1());
        JsonObject normalized = JsonNode.Parse(
            compilation.NormalizedPlaybook)!.AsObject();
        JsonObject recovery = normalized["coordination"]!["tasks"]!
            .AsArray()
            .Select(node => node!.AsObject())
            .Single(task => task["taskId"]!.GetValue<string>()
                == "hunter-patrol");
        JsonObject assignment = recovery["assignments"]![0]!.AsObject();
        Assert.Equal(
            "self-health",
            assignment["candidateWhen"]![0]!["all"]![0]!["fact"]!
                .GetValue<string>());
        Assert.Equal(
            "self-health",
            assignment["candidateUntil"]![0]!["all"]![0]!["fact"]!
                .GetValue<string>());

        JsonObject repeatedSubject = JsonNode.Parse(
            File.ReadAllBytes(HunterV1()))!.AsObject();
        repeatedSubject["conditions"]!["ghost-hurt"]!
            .AsObject()["subject"] = "hunter";
        string repeatedSubjectPath = Write(repeatedSubject);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(
                    repeatedSubjectPath));
            Assert.Contains("takes no subject or zone", failure.Message);
        }
        finally
        {
            File.Delete(repeatedSubjectPath);
        }

        JsonObject globalSelf = JsonNode.Parse(
            File.ReadAllBytes(HunterV1()))!.AsObject();
        globalSelf["custodyPolicies"]![0]!["safeConversion"] = "ghost-hurt";
        string globalSelfPath = Write(globalSelf);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(globalSelfPath));
            Assert.Contains("legal only in a doctrine mode", failure.Message);
        }
        finally
        {
            File.Delete(globalSelfPath);
        }
    }

    [Fact]
    public void ConsolidatedSheetsUseTopLevelConditionsAndScavengeOnly()
    {
        JsonObject legacyCatalog = JsonNode.Parse(
            File.ReadAllBytes(HunterV1()))!.AsObject();
        legacyCatalog["authoring"] = new JsonObject
        {
            ["conditionSets"] = new JsonObject(),
        };
        string legacyCatalogPath = Write(legacyCatalog);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(
                    legacyCatalogPath));
            Assert.Contains("unknown field 'authoring'",
                failure.Message);
        }
        finally
        {
            File.Delete(legacyCatalogPath);
        }

        JsonObject legacyCollect = JsonNode.Parse(
            File.ReadAllBytes(HunterV1()))!.AsObject();
        JsonObject ghost = legacyCollect["doctrines"]!["roles"]!["hunter"]!
            .AsObject();
        ghost["collect"] = ghost["scavenge"]!.DeepClone();
        ghost.Remove("scavenge");
        string legacyCollectPath = Write(legacyCollect);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(
                    legacyCollectPath));
            Assert.Contains("unknown field 'collect'", failure.Message);
        }
        finally
        {
            File.Delete(legacyCollectPath);
        }
    }

    [Fact]
    public void HunterAuthorsOnlyTheConsolidatedPhaseThreePlanes()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(HunterV1()))!
            .AsObject();
        Assert.Equal(
            [
                "schema", "playbookId", "auditStatus", "composition",
                "layout", "memory", "roles", "custodyPolicies",
                "doctrines", "conditions",
            ],
            source.Select(property => property.Key).ToArray());
        Assert.Equal(
            [
                "enemyUnavailableTicks", "lastSeenEnemyTicks",
                "securedCoreTicks", "objectiveProgressTicks",
            ],
            source["memory"]!.AsObject()
                .Select(property => property.Key).ToArray());
        Assert.Equal(
            ["roles", "cohorts", "focus"],
            source["doctrines"]!.AsObject()
                .Select(property => property.Key).ToArray());
        Assert.Equal(1, source["doctrines"]!["focus"]!["assassin"]![
            "maximumAttackers"]!.GetValue<int>());
        Assert.Equal(3, source["doctrines"]!["cohorts"]![
            "north-runners"]!["take"]!.GetValue<int>());

        string[] roles = source["roles"]!.AsArray()
            .Select(role => role!["roleId"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(
            roles,
            source["doctrines"]!["roles"]!.AsObject()
                .Select(property => property.Key).ToArray());
        Assert.All(source["doctrines"]!["roles"]!.AsObject(), doctrine =>
            Assert.NotEmpty(doctrine.Value!["modes"]!.AsArray()));

        string authored = source.ToJsonString();
        foreach (string retired in new[]
                 {
                     "perspective", "groups", "formations", "engagements",
                     "supportPolicies", "coordination", "authoring",
                     "formationStableTicks", "squad", "operations", "staff",
                     "escort", "muster", "tags", "formationPosture",
                     "tempo", "tempo-active",
                 })
        {
            Assert.DoesNotContain($"\"{retired}\"", authored);
        }
        string[] legalVerbs =
        ["patrol", "intercept", "assault", "recover", "fetch", "follow"];
        Assert.Equal(
            ["assault", "fetch", "follow", "patrol", "recover"],
            source["doctrines"]!["roles"]!.AsObject()
                .SelectMany(doctrine => doctrine.Value!["modes"]!.AsArray())
                .SelectMany(mode => legalVerbs.Where(
                    verb => mode!.AsObject().ContainsKey(verb)))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void NarrowCoordinationCompilesWithoutAFormationOrTempoPlane()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HunterV1());
        JsonObject normalized = JsonNode.Parse(
            compilation.NormalizedPlaybook)!.AsObject();

        Assert.True(compilation.LinkedData.Length < 65_536);
        Assert.Empty(normalized["formations"]!.AsArray());
        Assert.DoesNotContain("tempo", normalized.ToJsonString());

        JsonObject[] cohortRows = normalized["coordination"]!["tasks"]!
            .AsArray()
            .SelectMany(task => task!["assignments"]!.AsArray())
            .Select(row => row!.AsObject())
            .Where(row => row.ContainsKey("cohort"))
            .ToArray();
        Assert.Equal(15, cohortRows.Length);
        Assert.All(cohortRows, row =>
        {
            JsonObject cohort = row["cohort"]!.AsObject();
            Assert.Equal(3, cohort["take"]!.GetValue<int>());
            Assert.Equal("nearest", cohort["select"]!.GetValue<string>());
            Assert.Contains(cohort["destinationKind"]!.GetValue<string>(),
                new[] { "route", "zone" });
        });
        Assert.Equal(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["centre-runners"] = 3,
                ["home-guard"] = 3,
                ["north-runners"] = 3,
                ["south-runners"] = 3,
                ["well-surge"] = 3,
            },
            cohortRows.GroupBy(row =>
                    row["cohort"]!["cohortId"]!.GetValue<string>(),
                    StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.Ordinal));

        JsonObject[] focusPolicies = normalized["engagements"]!.AsArray()
            .Select(value => value!.AsObject())
            .Where(value => value["coordinationScope"]!
                .GetValue<string>().StartsWith("focus:",
                    StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(focusPolicies);
        Assert.All(focusPolicies, policy => Assert.Equal(
            policy["coordinationScope"]!.GetValue<string>() switch
            {
                "focus:assassin" => 1,
                "focus:home" => 3,
                "focus:patrol" => 2,
                "focus:well" => 3,
                string scope => throw new Xunit.Sdk.XunitException(
                    $"Unexpected focus scope '{scope}'."),
            },
            policy["maximumAttackersPerTarget"]!.GetValue<int>()));

        JsonObject[] postured = normalized["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .Where(value => value["posture"]?.GetValue<string>() is
                "front" or "core" or "rear")
            .ToArray();
        Assert.NotEmpty(postured);
        Assert.Contains(postured, order => order["movement"]!["target"]!
            .GetValue<string>() == "north-well-loop");
    }

    [Fact]
    public void CohortMembershipComesFromSeveralRoleLocalRungs()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(HunterV1()))!
            .AsObject();
        foreach (string role in new[] { "hauler", "carrier" })
        {
            source["doctrines"]!["roles"]![role]!["modes"]!.AsArray()
                .Select(mode => mode!.AsObject())
                .Single(mode => mode["cohort"]?.GetValue<string>()
                    == "centre-runners")
                .Remove("cohort");
        }
        string path = Write(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(path));
            Assert.Contains(
                "a cohort needs at least two role rungs", failure.Message);
        }
        finally
        {
            File.Delete(path);
        }

        JsonObject inline = JsonNode.Parse(File.ReadAllBytes(HunterV1()))!
            .AsObject();
        foreach (string role in new[] { "hauler", "carrier" })
        {
            inline["doctrines"]!["roles"]![role]!["modes"]!.AsArray()
                .Select(mode => mode!.AsObject())
                .Single(mode => mode["cohort"]?.GetValue<string>()
                    == "centre-runners")
                .Remove("cohort");
        }
        inline["doctrines"]!["roles"]!["medic"]!["modes"]!.AsArray()
            .Select(mode => mode!.AsObject())
            .Single(mode => mode["cohort"]?.GetValue<string>()
                == "centre-runners")["cohort"] = new JsonObject
                {
                    ["take"] = 1,
                    ["select"] = "nearest",
                };
        inline["doctrines"]!["cohorts"]!.AsObject()
            .Remove("centre-runners");
        string inlinePath = Write(inline);
        try
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(inlinePath);
            JsonObject normalized = JsonNode.Parse(
                compilation.NormalizedPlaybook)!.AsObject();
            JsonObject inlineRow = Assert.Single(
                normalized["coordination"]!["tasks"]!.AsArray()
                    .SelectMany(task => task!["assignments"]!.AsArray())
                    .Select(row => row!.AsObject()),
                row => row["cohort"]?["cohortId"]?
                    .GetValue<string>().StartsWith(
                        "inline-", StringComparison.Ordinal) == true);
            JsonObject inlineCohort = inlineRow["cohort"]!.AsObject();
            Assert.Equal(1, inlineCohort["take"]!.GetValue<int>());
        }
        finally
        {
            File.Delete(inlinePath);
        }
    }

    [Fact]
    public void CohortNearestHasOneAuthoredDestination()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(HunterV1()))!
            .AsObject();
        JsonObject carrierNorth = source["doctrines"]!["roles"]!["carrier"]![
                "modes"]!.AsArray()
            .Select(mode => mode!.AsObject())
            .Single(mode => mode["cohort"]?.GetValue<string>()
                == "north-runners");
        carrierNorth["patrol"] = "centre-well-loop";
        string path = Write(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(path));
            Assert.Contains(
                "has more than one destination", failure.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void OneOffFocusIsInlineRatherThanAOneConsumerSharedName()
    {
        JsonObject shared = JsonNode.Parse(File.ReadAllBytes(HunterV1()))!
            .AsObject();
        shared["doctrines"]!["focus"]!["one-off"] = new JsonObject
        {
            ["maximumAttackers"] = 1,
        };
        JsonObject hunterAssault = shared["doctrines"]!["roles"]!["hunter"]![
                "modes"]!.AsArray()
            .Select(mode => mode!.AsObject())
            .Single(mode => mode.ContainsKey("assault"));
        hunterAssault["focus"] = "one-off";
        string sharedPath = Write(shared);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(sharedPath));
            Assert.Contains(
                "shared focus scope needs at least two rung consumers",
                failure.Message);
        }
        finally
        {
            File.Delete(sharedPath);
        }

        JsonObject inline = JsonNode.Parse(File.ReadAllBytes(HunterV1()))!
            .AsObject();
        inline["doctrines"]!["roles"]!["hunter"]!["modes"]!.AsArray()
            .Select(mode => mode!.AsObject())
            .Single(mode => mode.ContainsKey("assault"))["focus"] =
                new JsonObject { ["maximumAttackers"] = 1 };
        string inlinePath = Write(inline);
        try
        {
            TacticalPlaybookCompilation compilation =
                ArcRelayTacticalPlaybookCompiler.Compile(inlinePath);
            Assert.Contains("focus:inline:hunter-assault",
                System.Text.Encoding.UTF8.GetString(
                    compilation.NormalizedPlaybook));
        }
        finally
        {
            File.Delete(inlinePath);
        }
    }

    [Fact]
    public void TempoFactIsRetiredInFavourOfADrawnPatrol()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(HunterV1()))!
            .AsObject();
        source["conditions"]!["old-clock"] = new JsonObject
        {
            ["fact"] = "tempo-active",
            ["operator"] = "equals",
            ["value"] = 1,
            ["subject"] = "north",
        };
        string path = Write(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(path));
            Assert.Contains(
                "tempo-active is retired", failure.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FollowRejectsAnUnknownLeaderAndAnUnknownPosture()
    {
        JsonObject unknownLeader = JsonNode.Parse(File.ReadAllBytes(HunterV1()))!
            .AsObject();
        unknownLeader["doctrines"]!["roles"]!["medic"]!["modes"]!.AsArray()
            .Select(mode => mode!.AsObject())
            .Single(mode => mode.ContainsKey("follow"))["follow"] = "missing";
        string unknownLeaderPath = Write(unknownLeader);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(
                    unknownLeaderPath));
            Assert.Contains("unknown role 'missing'", failure.Message);
        }
        finally
        {
            File.Delete(unknownLeaderPath);
        }

        JsonObject unknownPosture = JsonNode.Parse(
            File.ReadAllBytes(HunterV1()))!.AsObject();
        unknownPosture["doctrines"]!["roles"]!["medic"]!["modes"]!
            .AsArray().Select(mode => mode!.AsObject())
            .Single(mode => mode.ContainsKey("follow"))["posture"] = "wedge";
        string unknownPosturePath = Write(unknownPosture);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(
                    unknownPosturePath));
            Assert.Contains("posture for follow must be one of: trail, screen",
                failure.Message);
        }
        finally
        {
            File.Delete(unknownPosturePath);
        }
    }

    [Fact]
    public void EachRoleLadderDefinesItsOwnPriorityWithoutCrossRoleAgreement()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(HunterV1()))!
            .AsObject();
        JsonArray carrierLadder = source["doctrines"]!["roles"]!["carrier"]![
            "modes"]!.AsArray();
        JsonNode first = carrierLadder[1]!.DeepClone();
        carrierLadder[1] = carrierLadder[2]!.DeepClone();
        carrierLadder[2] = first;
        string path = Write(source);
        TacticalPlaybookCompilation compilation;
        try
        {
            compilation = ArcRelayTacticalPlaybookCompiler.Compile(path);
        }
        finally
        {
            File.Delete(path);
        }
        JsonObject normalized = JsonNode.Parse(
            compilation.NormalizedPlaybook)!.AsObject();
        JsonObject firstPatrol = normalized["orders"]!.AsArray()
            .Select(order => order!.AsObject()).Single(order =>
                order["orderId"]!.GetValue<string>() == "carrier-patrol-1");
        Assert.Equal("north-well-loop",
            firstPatrol["movement"]!["target"]!.GetValue<string>());
        Assert.Equal(1, normalized["modeRungs"]!.AsArray()
            .Select(rung => rung!.AsObject()).Single(rung =>
                rung["taskId"]!.GetValue<string>() == "carrier-patrol-1")
            ["priority"]!.GetValue<int>());
    }

    [Fact]
    public void ConsolidatedPatrolBeatAndRepairCompileIntoTheRuntimeIr()
    {
        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HunterV1());
        JsonObject normalized = JsonNode.Parse(
            compilation.NormalizedPlaybook)!.AsObject();

        Assert.Equal(
            ["steady"],
            normalized["coordination"]!["phases"]!.AsArray()
                .Select(phase => phase!["phaseId"]!.GetValue<string>())
                .ToArray());
        JsonObject patrol = normalized["orders"]!.AsArray()
            .Select(order => order!.AsObject())
            .Single(order => order["orderId"]!.GetValue<string>()
                == "hunter-floor");
        Assert.Equal("shadow-hunt-loop",
            patrol["movement"]!["target"]!.GetValue<string>());
        Assert.All(normalized["coordination"]!["phases"]!.AsArray(), phase =>
            Assert.Equal(7, phase!["orderIds"]!.AsArray().Count));

        JsonObject support = Assert.Single(
            normalized["supportPolicies"]!.AsArray())!.AsObject();
        Assert.Equal("medic-repair", support["supportId"]!.GetValue<string>());
        Assert.Equal("medic", Assert.Single(support["providers"]!.AsArray())!
            .GetValue<string>());
        JsonObject medicFollow = normalized["orders"]!.AsArray()
            .Select(order => order!.AsObject())
            .Single(order => order["orderId"]!.GetValue<string>()
                == "medic-follow");
        Assert.Equal("medic-repair",
            medicFollow["supportId"]!.GetValue<string>());
        JsonObject assassin = normalized["orders"]!.AsArray()
            .Select(order => order!.AsObject())
            .Single(order => order["orderId"]!.GetValue<string>()
                == "hauler-assault");
        Assert.Equal("", assassin["supportId"]!.GetValue<string>());
    }

    /// <summary>Write a modified sheet BESIDE the original so its relative
    /// layout reference and pinned hash still resolve.</summary>
    private static string Write(JsonObject source)
    {
        string path = Path.Combine(
            Path.GetDirectoryName(HunterV1())!,
            $"hunter-v1-case-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, source.ToJsonString());
        return path;
    }

    private static string HunterV1() => Path.Combine(
        FindRepoRoot(),
        "arena-bots",
        "arc-relay",
        "tactical-playbook-v1-2026-08-03",
        "playbooks",
        "hunter-v1.json");

    private static string HomeSiegeV3Library() => Path.Combine(
        FindRepoRoot(),
        "arena-bots",
        "arc-relay",
        "tactical-playbook-v1-2026-08-03",
        "playbooks",
        "home-siege-v3-lib.json");

    private static string HomeSiege() => Path.Combine(
        FindRepoRoot(),
        "arena-bots",
        "arc-relay",
        "tactical-playbook-v1-2026-08-03",
        "playbooks",
        "home-siege-v3.json");

    private static string HomeSiegeV3() => Path.Combine(
        FindRepoRoot(),
        "arena-bots",
        "arc-relay",
        "tactical-playbook-v1-2026-08-03",
        "playbooks",
        "home-siege-v3.json");

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
