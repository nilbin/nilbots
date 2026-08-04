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
    public void ManeuverCatalogExpandsToExhaustiveRuntimeOrders()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        Assert.Null(source["orders"]);
        Assert.Equal("maneuver-catalog",
            source["authoring"]!["kind"]!.GetValue<string>());
        Assert.Equal(6, source["authoring"]!["maneuvers"]!.AsObject().Count);

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(HomeSiege());
        JsonObject normalized = JsonNode.Parse(compilation.NormalizedPlaybook)!
            .AsObject();
        Assert.Null(normalized["authoring"]);
        Assert.Equal(19, normalized["orders"]!.AsArray().Count);
        Assert.All(normalized["coordination"]!["phases"]!.AsArray(), phase =>
            Assert.Equal(4, phase!["orderIds"]!.AsArray().Count));
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
            JsonElement[] phases = normalized.RootElement
                .GetProperty("coordination").GetProperty("phases")
                .EnumerateArray().ToArray();
            JsonElement assault = phases.Single(phase => phase
                .GetProperty("phaseId").GetString() == "assault");
            JsonElement occupy = phases.Single(phase => phase
                .GetProperty("phaseId").GetString() == "occupy");
            Assert.Equal(4, AttritionThreshold(assault));
            Assert.Equal(2, AttritionThreshold(occupy));
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
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
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
        source["authoring"]!["conditionSets"]![
            "conversion-window-front"]![0]![0] = "missing-predicate";
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains(
                "condition set references unknown predicate "
                + "'missing-predicate'",
                failure.Message);
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
    public void ConditionSetsRequireExplicitConjunctionRows()
    {
        JsonObject source = JsonNode.Parse(File.ReadAllText(HomeSiege()))!
            .AsObject();
        source["authoring"]!["conditionSets"]![
            "secured-conversion-safe"]![0] = new JsonObject();
        string temporary = TemporaryJson(source);
        try
        {
            InvalidDataException failure = Assert.Throws<InvalidDataException>(
                () => ArcRelayTacticalPlaybookCompiler.Compile(temporary));
            Assert.Contains("expected array", failure.Message);
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
        JsonObject harvest = source["coordination"]!["phases"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["phaseId"]!.GetValue<string>()
                == "harvest");
        harvest["orderIds"]!.AsArray().Remove(
            harvest["orderIds"]!.AsArray()
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
        JsonObject harvest = source["coordination"]!["phases"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["phaseId"]!.GetValue<string>()
                == "harvest");
        JsonObject field = source["orders"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["orderId"]!.GetValue<string>()
                == "medics-harvest");
        field["members"] = new JsonObject
        {
            ["kind"] = "take",
            ["roles"] = new JsonArray("medic"),
            ["classes"] = new JsonArray("patchbay"),
            ["count"] = 1,
        };
        JsonObject collection = JsonNode.Parse(field.ToJsonString())!
            .AsObject();
        collection["orderId"] = "medics-harvest-remainder";
        collection["priority"] = 21;
        collection["members"] = new JsonObject
        {
            ["kind"] = "remainder",
        };
        source["orders"]!.AsArray().Add(collection);
        harvest["orderIds"]!.AsArray().Add("medics-harvest-remainder");
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
                == "medics-harvest");
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
            loopProfile: ArcRelayLoopProfile.Current);
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

    private static int AttritionThreshold(JsonElement phase) => phase
        .GetProperty("transitions")[0]
        .GetProperty("when")[0]
        .GetProperty("all")
        .EnumerateArray()
        .Single(condition => condition.GetProperty("fact").GetString()
            == "known-enemies-unavailable")
        .GetProperty("value")
        .GetInt32();

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
