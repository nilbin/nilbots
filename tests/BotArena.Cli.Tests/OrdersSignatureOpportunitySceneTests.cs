using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BotArena.Engine;
using BotArena.Runtime;
using BotArena.TacticalPlaybooks;

namespace BotArena.Cli.Tests;

/// <summary>
/// Focused product scenes for signature opportunities. Compiler-shape tests
/// prove the words are accepted; these scenes prove the real direct executor
/// falls through, casts, preserves the engine-owned commitment, and resumes
/// the ordinary continuation it authored.
/// </summary>
public sealed class OrdersSignatureOpportunitySceneTests
{
    [Theory]
    [InlineData("kestrel")]
    [InlineData("palisade")]
    [InlineData("towline")]
    [InlineData("patchbay")]
    [InlineData("lantern")]
    [InlineData("mortar")]
    [InlineData("minesmith")]
    [InlineData("hush")]
    [InlineData("relay")]
    [InlineData("switchback")]
    [InlineData("longshot")]
    [InlineData("mason")]
    [InlineData("sunder")]
    [InlineData("repulsor")]
    [InlineData("veil")]
    [InlineData("nest")]
    public void EveryGeneratedOpportunityEitherActsOrExplainsItsFallthrough(
        string classId)
    {
        TacticalPlaybookCompilation sheet = OpportunitySheet(classId);
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            sheet.Composition,
            sheet.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline7);
        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, sheet), Participant(1, sheet)],
            matchSeed: 9012,
            recordChronology: false);
        ArcRelaySignatureAuthoringDescriptor descriptor =
            ArcRelaySignatureAuthoringCatalog.ForClass(classId);

        string? evidence = null;
        while (!session.IsCompleted && session.Tick < 80 && evidence is null)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindCommandResolution? command = Assert.Single(
                    step.MindTurns, turn => turn.TeamId == 0).Commands
                .FirstOrDefault(value => value.Command.UnitId == 7);
            if (command is null)
                continue;
            Assert.False(string.IsNullOrWhiteSpace(
                command.Command.DebugMessage));
            string reason = command.Command.DebugMessage!;
            if (string.Equals(
                    command.Command.ActionId,
                    descriptor.SkillId,
                    StringComparison.Ordinal)
                || reason.Contains(
                    $"signature-{descriptor.SkillId}",
                    StringComparison.Ordinal)
                || reason.Contains(
                    descriptor.SkillId,
                    StringComparison.Ordinal))
            {
                evidence = $"t{step.Tick}:{command.Command.ActionId}:{reason}";
            }
        }

        Assert.True(evidence is not null,
            $"{classId}/{descriptor.SkillId} neither acted nor left a "
                + "signature-specific fallthrough reason in 80 ticks.");
    }

    [Fact]
    public void KestrelDashesForwardThenResumesTheSameTerminalRoute()
    {
        TacticalPlaybookCompilation sheet = RouteDashSheet();
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            sheet.Composition,
            sheet.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline7);
        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, sheet), Participant(1, sheet)],
            matchSeed: 9001,
            recordChronology: false);

        bool sawNoCourseFallthrough = false;
        int? dashTick = null;
        Position? dashPosition = null;
        Position? previous = null;
        var afterDash = new List<Position>();
        while (!session.IsCompleted && session.Tick < 60)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindCommandResolution? command = Assert.Single(
                    step.MindTurns, turn => turn.TeamId == 0).Commands
                .FirstOrDefault(value => value.Command.UnitId == 7);
            if (command is null)
                continue;
            string reason = command.Command.DebugMessage ?? "";
            GenericActorWorldSnapshot.LifeSnapshot life =
                step.PostState.ActiveLives.Single(value =>
                    value.ActorId.TeamId == 0 && value.ActorId.UnitId == 7);

            sawNoCourseFallthrough |= reason.Contains(
                "after-dash-forward-route-dash-corridor-route-projection-absent",
                StringComparison.Ordinal)
                && reason.Contains("continue-dash-corridor", StringComparison.Ordinal);
            if (string.Equals(
                    command.Command.ActionId,
                    "vector-dash",
                    StringComparison.Ordinal))
            {
                dashTick ??= step.Tick;
                dashPosition ??= life.Position;
            }
            else if (dashTick is not null && step.Tick > dashTick)
            {
                afterDash.Add(life.Position);
                if (previous is Position prior)
                {
                    Assert.True(
                        life.Position.X >= prior.X,
                        $"route continuation reversed from {prior} to "
                            + $"{life.Position} after Dash at t{dashTick}");
                }
                if (life.Position.X >= 7)
                    break;
            }
            previous = life.Position;
        }

        Assert.True(sawNoCourseFallthrough,
            "The first unavailable route-forward use should select its "
                + "ordinary movement sibling in the same tick.");
        Assert.NotNull(dashTick);
        Assert.NotNull(dashPosition);
        Assert.NotEmpty(afterDash);
        Assert.True(afterDash.Max(value => value.X) > dashPosition!.Value.X,
            "Movement should resume forward on the held route after Dash.");
    }

    [Fact]
    public void RootedTellUsesTheEngineCommitmentFactInsteadOfCancellingToMove()
    {
        TacticalPlaybookCompilation sheet = RootedTellSheet();
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            sheet.Composition,
            sheet.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline7);
        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, sheet), Participant(1, sheet)],
            matchSeed: 9001,
            recordChronology: false);

        int? declaredAt = null;
        int? heldAt = null;
        while (!session.IsCompleted && session.Tick < 300)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindCommandResolution? command = Assert.Single(
                    step.MindTurns, turn => turn.TeamId == 0).Commands
                .FirstOrDefault(value => value.Command.UnitId == 7);
            if (command is null)
                continue;
            string reason = command.Command.DebugMessage ?? "";
            if (string.Equals(
                    command.Command.ActionId, "rail-line",
                    StringComparison.Ordinal))
            {
                declaredAt ??= step.Tick;
            }
            if (reason.Contains(
                    "preserve-rail-commitment",
                    StringComparison.Ordinal)
                && reason.Contains(
                    "signature-commitment-hold-active",
                    StringComparison.Ordinal))
            {
                Assert.Equal("wait", command.Command.ActionId);
                heldAt ??= step.Tick;
                break;
            }
        }

        Assert.NotNull(declaredAt);
        Assert.NotNull(heldAt);
        Assert.True(heldAt > declaredAt,
            "The hold must observe a real operation published after declare.");
    }

    [Fact]
    public void RepairBeamChannelsUnderTheSameAuthoritativeCommitmentHold()
    {
        TacticalPlaybookCompilation sheet = RepairChannelSheet();
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            sheet.Composition,
            sheet.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline7);
        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, sheet), Participant(1, sheet)],
            matchSeed: 9005,
            recordChronology: false);

        int? castAt = null;
        int? heldAt = null;
        while (!session.IsCompleted && session.Tick < 400)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindCommandResolution? command = Assert.Single(
                    step.MindTurns, turn => turn.TeamId == 0).Commands
                .FirstOrDefault(value => value.Command.UnitId == 3);
            if (command is null)
                continue;
            string reason = command.Command.DebugMessage ?? "";
            if (string.Equals(
                    command.Command.ActionId, "repair-beam",
                    StringComparison.Ordinal))
            {
                castAt ??= step.Tick;
            }
            if (reason.Contains(
                    "preserve-repair-commitment",
                    StringComparison.Ordinal)
                && reason.Contains(
                    "signature-commitment-hold-active",
                    StringComparison.Ordinal))
            {
                Assert.Equal("wait", command.Command.ActionId);
                heldAt ??= step.Tick;
                break;
            }
        }

        Assert.NotNull(castAt);
        Assert.NotNull(heldAt);
        Assert.True(heldAt > castAt,
            "Repair continuation must wait on the real Channel operation.");
    }

    private static TacticalPlaybookCompilation RouteDashSheet()
    {
        (JsonObject source, JsonObject map) = Starter();
        JsonArray roles = source["roles"]!.AsArray();
        JsonObject far = roles.Single(value =>
            value!["id"]!.GetValue<string>() == "far-team")!.AsObject();
        far["classes"] = new JsonArray("towline", "lantern");
        roles.Add(new JsonObject
        {
            ["id"] = "dash-runner",
            ["classes"] = new JsonArray("kestrel"),
            ["upgradePath"] = new JsonArray("damage", "vision", "vitality"),
            ["orders"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "dash-corridor-plan",
                    ["orders"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "dash-forward",
                            ["use"] = new JsonObject
                            {
                                ["skill"] = "vector-dash",
                                ["target"] = new JsonObject
                                {
                                    ["route"] = "dash-corridor",
                                    ["progress"] = "forward",
                                    ["minimumForwardTiles"] = 2,
                                },
                            },
                        },
                        new JsonObject
                        {
                            ["id"] = "preserve-dash-commitment",
                            ["when"] = "signature-committed",
                            ["move"] = new JsonObject
                            {
                                ["to"] = "current-position",
                            },
                        },
                        new JsonObject
                        {
                            ["id"] = "continue-dash-corridor",
                            ["move"] = new JsonObject
                            {
                                ["patrol"] = "dash-corridor",
                            },
                        },
                    },
                },
            },
        });
        source["conditions"]!["signature-committed"] = new JsonObject
        {
            ["fact"] = "signature-commitment-active",
            ["operator"] = "equals",
            ["value"] = 1,
        };
        map["routes"]!["dash-corridor"] = new JsonObject
        {
            ["waypoints"] = new JsonArray
            {
                new JsonArray(1, 6),
                new JsonArray(8, 6),
            },
        };
        return Compile(source, map, "route-dash-scene");
    }

    private static TacticalPlaybookCompilation RootedTellSheet()
    {
        (JsonObject source, JsonObject map) = Starter();
        JsonArray roles = source["roles"]!.AsArray();
        JsonObject far = roles.Single(value =>
            value!["id"]!.GetValue<string>() == "far-team")!.AsObject();
        far["classes"] = new JsonArray("towline", "lantern");
        roles.Add(new JsonObject
        {
            ["id"] = "rail-runner",
            ["classes"] = new JsonArray("longshot"),
            ["upgradePath"] = new JsonArray("damage", "vision", "vitality"),
            ["orders"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "rail-advance-plan",
                    ["orders"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "rail-nearest-enemy",
                            ["use"] = new JsonObject
                            {
                                ["skill"] = "rail-line",
                                ["target"] = "nearest-enemy",
                            },
                        },
                        new JsonObject
                        {
                            ["id"] = "preserve-rail-commitment",
                            ["when"] = "signature-committed",
                            ["move"] = new JsonObject
                            {
                                ["to"] = "current-position",
                            },
                        },
                        new JsonObject
                        {
                            ["id"] = "continue-to-centre",
                            ["move"] = new JsonObject
                            {
                                ["to"] = new JsonObject { ["well"] = "centre" },
                                ["posture"] = "front",
                            },
                        },
                    },
                },
            },
        });
        source["conditions"]!["signature-committed"] = new JsonObject
        {
            ["fact"] = "signature-commitment-active",
            ["operator"] = "equals",
            ["value"] = 1,
        };
        return Compile(source, map, "rooted-tell-scene");
    }

    private static TacticalPlaybookCompilation RepairChannelSheet()
    {
        (JsonObject source, JsonObject map) = Starter();
        JsonObject centre = source["roles"]!.AsArray().Single(value =>
            value!["id"]!.GetValue<string>() == "centre-team")!.AsObject();
        centre["orders"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "repair-centre-plan",
                ["orders"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "repair-weakest-ally",
                        ["use"] = new JsonObject
                        {
                            ["skill"] = "repair-beam",
                            ["target"] = "weakest-ally",
                        },
                    },
                    new JsonObject
                    {
                        ["id"] = "preserve-repair-commitment",
                        ["when"] = "signature-committed",
                        ["move"] = new JsonObject
                        {
                            ["to"] = "current-position",
                        },
                    },
                    new JsonObject
                    {
                        ["id"] = "fight-at-centre",
                        ["when"] = "enemy-nearby",
                        ["fight"] = new JsonObject
                        {
                            ["targets"] = new JsonArray(
                                "carrier", "weakest", "nearest"),
                            ["style"] = "advance",
                            ["chaseLimit"] = 3,
                        },
                    },
                    new JsonObject
                    {
                        ["id"] = "continue-to-centre",
                        ["move"] = new JsonObject
                        {
                            ["to"] = new JsonObject { ["well"] = "centre" },
                            ["posture"] = "line",
                        },
                    },
                },
            },
        };
        source["conditions"]!["signature-committed"] = new JsonObject
        {
            ["fact"] = "signature-commitment-active",
            ["operator"] = "equals",
            ["value"] = 1,
        };
        return Compile(source, map, "repair-channel-scene");
    }

    private static TacticalPlaybookCompilation OpportunitySheet(
        string classId)
    {
        (JsonObject source, JsonObject map) = Starter();
        ArcRelaySignatureAuthoringDescriptor descriptor =
            ArcRelaySignatureAuthoringCatalog.ForClass(classId);
        JsonObject far = source["roles"]!.AsArray().Single(value =>
            value!["id"]!.GetValue<string>() == "far-team")!.AsObject();
        far["classes"] = new JsonArray("towline", "lantern", classId);

        var use = new JsonObject
        {
            ["skill"] = descriptor.SkillId,
            ["opportunity"] = ArcRelaySignatureAuthoringCatalog.StableId(
                descriptor.Opportunity),
        };
        if (descriptor.Allows(
                ArcRelaySignatureTargetCapability.RouteForward))
        {
            use["target"] = new JsonObject
            {
                ["route"] = "opportunity-route",
                ["progress"] = "forward",
                ["minimumForwardTiles"] = 2,
            };
            map["routes"]!["opportunity-route"] = new JsonObject
            {
                ["waypoints"] = new JsonArray
                {
                    new JsonArray(1, 6),
                    new JsonArray(8, 6),
                },
            };
        }
        else
        {
            ArcRelaySignatureNamedTarget target =
                descriptor.Preset.NamedTarget
                ?? descriptor.AllowedNamedTargets[0];
            use["target"] = ArcRelaySignatureAuthoringCatalog.StableId(target);
            if (descriptor.Allows(
                    ArcRelaySignatureTargetCapability.ExactAnchor))
            {
                map["anchors"]!["opportunity-mark"] = new JsonArray(3, 6);
                use["at"] = "opportunity-mark";
            }
        }
        far["orders"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = $"try-{descriptor.SkillId}",
                ["use"] = use,
            },
            new JsonObject
            {
                ["id"] = $"continue-after-{descriptor.SkillId}",
                ["move"] = new JsonObject
                {
                    ["to"] = "current-position",
                },
            },
        };
        return Compile(source, map, $"{classId}-opportunity-scene");
    }

    private static (JsonObject Source, JsonObject Map) Starter()
    {
        string root = FindRepoRoot();
        string package = Path.Combine(root, "arena-bots", "arc-relay",
            "tactical-playbook-v1-2026-08-03");
        JsonObject source = JsonNode.Parse(File.ReadAllText(Path.Combine(
            package, "playbooks", "starter-tapline-v2.json")))!.AsObject();
        JsonObject map = JsonNode.Parse(File.ReadAllText(Path.Combine(
            package, "map-plans", "tapline-6",
            "starter-tapline-v2.json")))!.AsObject();
        return (source, map);
    }

    private static TacticalPlaybookCompilation Compile(
        JsonObject source,
        JsonObject map,
        string name) => ArcRelayTacticalPlaybookCompiler.Compile(
        Encoding.UTF8.GetBytes(source.ToJsonString()),
        Encoding.UTF8.GetBytes(map.ToJsonString()),
        $"{name}.orders.json",
        $"{name}.map-plan.json");

    private static GenericActorParticipantConfiguration Participant(
        int participantId,
        TacticalPlaybookCompilation playbook) => new()
    {
        ParticipantId = participantId,
        TeamId = participantId,
        Name = $"Signature scene {participantId}",
        MindRuntimeFactory = new InProcessGenericMindRuntimeFactory(
            static () => new global::ArcRelayTacticalPlaybookMind()),
        RuntimeKind = "orders-signature-scene",
        ArtifactHash = "orders-signature-scene",
        MindDataHash = Convert.ToHexStringLower(
            SHA256.HashData(playbook.LinkedData)),
        MindEvaluationData = ImmutableArray.Create(playbook.LinkedData),
        Accent = participantId == 0 ? "#22d3ee" : "#fb5360",
        LookId = "arc-relay-sheet",
        ProjectileLookId = ArcRelayH0ReplayPresentation.ProjectileLookId,
    };

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root is missing.");
    }
}
