using System.Text.Json;
using BotArena.Cli;

namespace BotArena.Cli.Tests;

[Collection("Console")]
public sealed class FrontlineLabsQualificationCommandTests
{
    [Fact]
    public void WaitBot_FailsMirroredEntryWithoutAwardingATier()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-qualification-wait-{Guid.NewGuid():N}");
        try
        {
            string bot = CreateBot(
                temporary,
                "QualificationWait",
                """
                GenericActorActionLegality wait =
                    context.Action("wait")!;
                return GenericActorDecision.WithoutArguments(
                    wait.ActionId,
                    wait.ActionCode);
                """);
            string output = Path.Combine(temporary, "evidence");

            Assert.Equal(3, Run(bot, output));

            string reportPath = Path.Combine(
                output,
                "qualification.json");
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(reportPath));
            JsonElement root = document.RootElement;
            Assert.Equal(
                "frontline-qualification-1",
                root.GetProperty("suiteId").GetString());
            Assert.Equal(
                "entry-initiative",
                root.GetProperty("probeId").GetString());
            Assert.False(root.GetProperty("passed").GetBoolean());
            Assert.Equal(
                JsonValueKind.Null,
                root.GetProperty("tierAwarded").ValueKind);
            JsonElement[] assignments =
            [
                .. root.GetProperty("assignments").EnumerateArray(),
            ];
            Assert.Equal(2, assignments.Length);
            Assert.All(
                assignments,
                assignment =>
                {
                    Assert.False(
                        assignment.GetProperty("passed").GetBoolean());
                    Assert.True(
                        assignment
                            .GetProperty("sentinelAttackCount")
                            .GetInt32()
                        > 0);
                    Assert.Equal(
                        JsonValueKind.Null,
                        assignment
                            .GetProperty("firstLifeObjectiveTick")
                            .ValueKind);
                    AssertReplayVerifies(output, assignment);
                });
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void BreachAndHoldBot_PassesEntryFromBothAssignments()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-qualification-advance-{Guid.NewGuid():N}");
        try
        {
            string bot = CreateBot(
                temporary,
                "QualificationBreachAndHold",
                """
                if (context.Tick >= 2)
                {
                    GenericActorActionLegality wait =
                        context.Action("wait")!;
                    return GenericActorDecision.WithoutArguments(
                        wait.ActionId,
                        wait.ActionCode);
                }
                GenericActorActionLegality move =
                    context.Action("move")!;
                Direction direction =
                    context.Self.ActorId.TeamId == 0
                        ? Direction.East
                        : Direction.West;
                return new GenericActorDecision(
                    move.ActionId,
                    move.ActionCode,
                    [
                        new GenericActorActionArgument
                            .DirectionArgument(direction),
                    ]);
                """);
            string output = Path.Combine(temporary, "evidence");

            Assert.Equal(0, Run(bot, output));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(
                    Path.Combine(output, "qualification.json")));
            JsonElement root = document.RootElement;
            Assert.True(root.GetProperty("passed").GetBoolean());
            Assert.All(
                root.GetProperty("assignments").EnumerateArray(),
                assignment =>
                {
                    Assert.True(
                        assignment.GetProperty("passed").GetBoolean());
                    Assert.NotEqual(
                        JsonValueKind.Null,
                        assignment
                            .GetProperty("firstLifeObjectiveTick")
                            .ValueKind);
                    Assert.Equal(
                        0,
                        assignment
                            .GetProperty("damageTakenBeforeEntry")
                            .GetInt32());
                    Assert.True(
                        assignment
                            .GetProperty(
                                "maxInitialObjectiveCaptureProgress")
                            .GetInt32()
                        >= 5);
                    AssertReplayVerifies(output, assignment);
                });
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void BlindRushBot_TouchesButDoesNotUseTheObjective()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-qualification-rush-{Guid.NewGuid():N}");
        try
        {
            string bot = CreateBot(
                temporary,
                "QualificationBlindRush",
                """
                GenericActorActionLegality move =
                    context.Action("move")!;
                Direction direction =
                    context.Self.ActorId.TeamId == 0
                        ? Direction.East
                        : Direction.West;
                return new GenericActorDecision(
                    move.ActionId,
                    move.ActionCode,
                    [
                        new GenericActorActionArgument
                            .DirectionArgument(direction),
                    ]);
                """);
            string output = Path.Combine(temporary, "evidence");

            Assert.Equal(3, Run(bot, output));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(
                    Path.Combine(output, "qualification.json")));
            Assert.All(
                document.RootElement
                    .GetProperty("assignments")
                    .EnumerateArray(),
                assignment =>
                {
                    Assert.NotEqual(
                        JsonValueKind.Null,
                        assignment
                            .GetProperty("firstLifeObjectiveTick")
                            .ValueKind);
                    Assert.True(
                        assignment
                            .GetProperty(
                                "maxInitialObjectiveCaptureProgress")
                            .GetInt32()
                        < 5);
                    Assert.False(
                        assignment.GetProperty("passed").GetBoolean());
                });
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void MissingBotAndUnknownSuite_FailBeforeRunning()
    {
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsQualificationCommand.Run([]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsQualificationCommand.Run(
                [
                    "--bot",
                    ".",
                    "--suite",
                    "future-suite",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsQualificationCommand.Run(
                [
                    "--bot",
                    ".",
                    "--runtime",
                    "in-process",
                    "--suite",
                    "frontline-qualification-2",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsQualificationCommand.Run(
                [
                    "--bot",
                    ".",
                    "--runtime",
                    "in-process",
                    "--suite",
                    "frontline-qualification-3",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsQualificationCommand.Run(
                [
                    "--bot",
                    ".",
                    "--runtime",
                    "in-process",
                    "--suite",
                    "frontline-qualification-4",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsQualificationCommand.Run(
                [
                    "--bot",
                    ".",
                    "--runtime",
                    "in-process",
                    "--suite",
                    "frontline-qualification-5",
                ]));
    }

    [Fact]
    public void FundamentalsProfile_AwardsT1ButNotT2ForIdleAutomaticChild()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-qualification-fundamentals-{Guid.NewGuid():N}");
        try
        {
            string bot = Path.Combine(
                FindRepoRoot(),
                "arena-bots",
                "frontline-labs",
                "duel-depth-v1-2026-07-28",
                "initiative-planner",
                "bot-balance-lab-v1.wasm");

            Assert.Equal(3, RunFundamentals(bot, temporary));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(
                    Path.Combine(temporary, "qualification.json")));
            JsonElement root = document.RootElement;
            Assert.Equal(3, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(
                "frontline-duel-depth-union-t2-v1",
                root.GetProperty("qualificationProfileId").GetString());
            Assert.False(root.GetProperty("passed").GetBoolean());
            Assert.True(
                root.GetProperty("profileComplete").GetBoolean());
            Assert.Equal(
                "T1",
                root.GetProperty("tierAwarded").GetString());
            Assert.False(
                root.GetProperty("balanceEvidenceEligible").GetBoolean());

            JsonElement[] probes =
            [
                .. root.GetProperty("probes").EnumerateArray(),
            ];
            Assert.Equal(6, probes.Length);
            Assert.Single(
                probes,
                probe =>
                    !probe.GetProperty("passed").GetBoolean()
                    && probe.GetProperty("probeId").GetString()
                        == "automatic-life-cycle");
            foreach (JsonElement assignment in probes.SelectMany(probe =>
                         probe.GetProperty("assignments").EnumerateArray()))
            {
                AssertReplayVerifies(
                    temporary,
                    assignment.GetProperty("primary"));
                JsonElement repeat =
                    assignment.GetProperty("determinismRepeat");
                if (repeat.ValueKind != JsonValueKind.Null)
                {
                    Assert.True(
                        assignment.GetProperty("replayHashMatched")
                            .GetBoolean());
                    AssertReplayVerifies(temporary, repeat);
                }
            }
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void FundamentalsProfile_ArchivedHouseApprenticeQualifiesT2()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-qualification-apprentice-{Guid.NewGuid():N}");
        try
        {
            string bot = Path.Combine(
                FindRepoRoot(),
                "arena-bots",
                "frontline-labs",
                "qualification-instruments-v1-2026-07-28",
                "house-apprentice",
                "bot.wasm");

            Assert.Equal(0, RunFundamentals(bot, temporary));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(
                    Path.Combine(temporary, "qualification.json")));
            JsonElement root = document.RootElement;
            Assert.True(root.GetProperty("passed").GetBoolean());
            Assert.True(root.GetProperty("profileComplete").GetBoolean());
            Assert.Equal(
                "T2",
                root.GetProperty("tierAwarded").GetString());
            Assert.Equal(
                "8aa12a82144e314e0b215a57a60729be68131cc6a3330e68593e178c95f1b873",
                root.GetProperty("artifactHash").GetString());
            Assert.False(
                root.GetProperty("balanceEvidenceEligible").GetBoolean());
            JsonElement[] probes =
            [
                .. root.GetProperty("probes").EnumerateArray(),
            ];
            Assert.Equal(6, probes.Length);
            Assert.All(
                probes,
                probe =>
                    Assert.True(
                        probe.GetProperty("passed").GetBoolean(),
                        probe.GetProperty("probeId").GetString()));

            JsonElement evade = Assert.Single(
                probes,
                probe =>
                    probe.GetProperty("probeId").GetString()
                        == "straight-evade");
            Assert.All(
                evade.GetProperty("assignments").EnumerateArray(),
                assignment =>
                {
                    JsonElement evidence =
                        assignment.GetProperty("primary");
                    Assert.True(
                        evidence.GetProperty("threatenedTurnCount")
                            .GetInt32() > 0);
                    Assert.True(
                        evidence.GetProperty(
                                "successfulThreatMoveCount")
                            .GetInt32() > 0);
                    Assert.Equal(
                        0,
                        evidence.GetProperty("damageTaken").GetInt32());
                });
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void TacticalProfile_PreservesT2WhenApprenticeLacksT3Doctrine()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-qualification-tactical-{Guid.NewGuid():N}");
        try
        {
            string bot = Path.Combine(
                FindRepoRoot(),
                "arena-bots",
                "frontline-labs",
                "qualification-instruments-v1-2026-07-28",
                "house-apprentice",
                "bot.wasm");

            Assert.Equal(3, RunTactical(bot, temporary));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(
                    Path.Combine(temporary, "qualification.json")));
            JsonElement root = document.RootElement;
            Assert.Equal(4, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(
                "frontline-duel-depth-union-t3-v1",
                root.GetProperty("qualificationProfileId").GetString());
            Assert.False(root.GetProperty("passed").GetBoolean());
            Assert.True(root.GetProperty("profileComplete").GetBoolean());
            Assert.Equal(
                "T2",
                root.GetProperty("tierAwarded").GetString());
            Assert.False(
                root.GetProperty("balanceEvidenceEligible").GetBoolean());

            JsonElement prerequisite =
                root.GetProperty("prerequisite");
            Assert.True(
                prerequisite.GetProperty("passed").GetBoolean());
            Assert.Equal(
                "frontline-qualification-3",
                prerequisite.GetProperty("suiteId").GetString());
            Assert.Equal(
                "T2",
                prerequisite.GetProperty("tierAwarded").GetString());
            Assert.Matches(
                "^[0-9a-f]{64}$",
                prerequisite.GetProperty("reportSha256").GetString());

            JsonElement[] probes =
            [
                .. root.GetProperty("probes").EnumerateArray(),
            ];
            Assert.Equal(5, probes.Length);
            Assert.Equal(
                [
                    "cooldown-window",
                    "wall-terminated-bend",
                ],
                probes
                    .Where(probe =>
                        !probe.GetProperty("passed").GetBoolean())
                    .Select(probe =>
                        probe.GetProperty("probeId").GetString()!)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
            Assert.Equal(
                4,
                probes.Single(probe =>
                        probe.GetProperty("probeId").GetString()
                            == "cadence-parity")
                    .GetProperty("cases")
                    .GetArrayLength());
            foreach (JsonElement item in probes.SelectMany(probe =>
                         probe.GetProperty("cases").EnumerateArray()))
            {
                AssertReplayVerifies(
                    temporary,
                    item.GetProperty("run"));
            }
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void TacticalProfile_ArchivedArcApprenticeQualifiesT3()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-qualification-arc-{Guid.NewGuid():N}");
        try
        {
            string bot = Path.Combine(
                FindRepoRoot(),
                "arena-bots",
                "frontline-labs",
                "qualification-instruments-v1-2026-07-28",
                "arc-apprentice",
                "bot.wasm");

            Assert.Equal(0, RunTactical(bot, temporary));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(
                    Path.Combine(temporary, "qualification.json")));
            JsonElement root = document.RootElement;
            Assert.True(root.GetProperty("passed").GetBoolean());
            Assert.True(root.GetProperty("profileComplete").GetBoolean());
            Assert.Equal(
                "T3",
                root.GetProperty("tierAwarded").GetString());
            Assert.Equal(
                "956093006ac9b8f31664b49223de7781670f2bfbec9347509b3dc85f5eabad9a",
                root.GetProperty("artifactHash").GetString());
            Assert.True(
                root.GetProperty("prerequisite")
                    .GetProperty("passed")
                    .GetBoolean());
            Assert.All(
                root.GetProperty("probes").EnumerateArray(),
                probe =>
                    Assert.True(
                        probe.GetProperty("passed").GetBoolean(),
                        probe.GetProperty("probeId").GetString()));

            JsonElement bend = root.GetProperty("probes")
                .EnumerateArray()
                .Single(probe =>
                    probe.GetProperty("probeId").GetString()
                        == "wall-terminated-bend");
            Assert.All(
                bend.GetProperty("cases").EnumerateArray(),
                item =>
                {
                    JsonElement run = item.GetProperty("run");
                    int curvedAttackCount = run
                        .GetProperty("curvedAttackCount")
                        .GetInt32();
                    Assert.True(curvedAttackCount > 0);
                    Assert.Equal(
                        curvedAttackCount,
                        run.GetProperty("curvedProjectileHitCount")
                            .GetInt32());
                    Assert.True(
                        run.GetProperty("curvedDamageDealt")
                            .GetInt32() > 0);
                });
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void PositionalProfile_MeasuresArcApprenticeAsExactT3Boundary()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-qualification-arc-t4-{Guid.NewGuid():N}");
        try
        {
            string bot = Path.Combine(
                FindRepoRoot(),
                "arena-bots",
                "frontline-labs",
                "qualification-instruments-v1-2026-07-28",
                "arc-apprentice",
                "bot.wasm");

            Assert.Equal(3, RunPositional(bot, temporary));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(
                    Path.Combine(temporary, "qualification.json")));
            JsonElement root = document.RootElement;
            Assert.Equal(5, root.GetProperty("schemaVersion").GetInt32());
            Assert.False(root.GetProperty("passed").GetBoolean());
            Assert.Equal(
                "T3",
                root.GetProperty("tierAwarded").GetString());
            Assert.False(
                root.GetProperty("balanceEvidenceEligible").GetBoolean());
            Assert.True(
                root.GetProperty("prerequisite")
                    .GetProperty("passed")
                    .GetBoolean());
            Assert.Equal(
                ["entry-initiative"],
                root.GetProperty("probes")
                    .EnumerateArray()
                    .Where(probe =>
                        !probe.GetProperty("passed").GetBoolean())
                    .Select(probe =>
                        probe.GetProperty("probeId").GetString()!)
                    .ToArray());
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void PositionalProfile_ArchivedBreachApprenticeQualifiesT4()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-qualification-breach-{Guid.NewGuid():N}");
        try
        {
            string bot = Path.Combine(
                FindRepoRoot(),
                "arena-bots",
                "frontline-labs",
                "qualification-instruments-v1-2026-07-28",
                "breach-apprentice",
                "bot.wasm");

            Assert.Equal(0, RunPositional(bot, temporary));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(
                    Path.Combine(temporary, "qualification.json")));
            JsonElement root = document.RootElement;
            Assert.True(root.GetProperty("passed").GetBoolean());
            Assert.Equal(
                "T4",
                root.GetProperty("tierAwarded").GetString());
            Assert.True(
                root.GetProperty("balanceEvidenceEligible").GetBoolean());
            Assert.Equal(
                "2612a2b3a4cea50302877425ae9b9531cce27c7db89ab69bf05d708a0df52002",
                root.GetProperty("artifactHash").GetString());
            Assert.All(
                root.GetProperty("probes").EnumerateArray(),
                probe =>
                    Assert.True(
                        probe.GetProperty("passed").GetBoolean(),
                        probe.GetProperty("probeId").GetString()));
            foreach (JsonElement item in root.GetProperty("probes")
                         .EnumerateArray()
                         .SelectMany(probe =>
                             probe.GetProperty("cases").EnumerateArray()))
            {
                AssertReplayVerifies(
                    temporary,
                    item.GetProperty("run"));
            }
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void FoundationProfile_VerifiesAutomaticLifeAndDeterministicWasm()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-qualification-foundation-{Guid.NewGuid():N}");
        try
        {
            string bot = Path.Combine(
                FindRepoRoot(),
                "arena-bots",
                "frontline-labs",
                "duel-depth-v1-2026-07-28",
                "initiative-planner",
                "bot-balance-lab-v1.wasm");

            Assert.Equal(0, RunFoundation(bot, temporary));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(
                    Path.Combine(temporary, "qualification.json")));
            JsonElement root = document.RootElement;
            Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(
                "frontline-h2h-one-bend-auto-foundation-1",
                root.GetProperty("qualificationProfileId").GetString());
            Assert.True(root.GetProperty("passed").GetBoolean());
            Assert.False(
                root.GetProperty("profileComplete").GetBoolean());
            Assert.Equal(
                JsonValueKind.Null,
                root.GetProperty("tierAwarded").ValueKind);
            Assert.False(
                root.GetProperty("balanceEvidenceEligible").GetBoolean());
            JsonElement probe = root.GetProperty("probes")[0];
            Assert.Equal(
                "contract-auto-determinism",
                probe.GetProperty("probeId").GetString());
            Assert.All(
                probe.GetProperty("assignments").EnumerateArray(),
                assignment =>
                {
                    Assert.True(
                        assignment
                            .GetProperty("replayHashMatched")
                            .GetBoolean());
                    Assert.True(
                        assignment
                            .GetProperty("primary")
                            .GetProperty("automaticLifeStarted")
                            .GetBoolean());
                    Assert.True(
                        assignment.GetProperty("passed").GetBoolean());
                    AssertReplayVerifies(
                        temporary,
                        assignment.GetProperty("primary"));
                    AssertReplayVerifies(
                        temporary,
                        assignment.GetProperty("determinismRepeat"));
                });
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    private static void AssertReplayVerifies(
        string output,
        JsonElement assignment)
    {
        string relativePath = assignment
            .GetProperty("replayPath")
            .GetString()!;
        Assert.Equal(
            0,
            Verify(Path.Combine(output, relativePath)));
    }

    private static int Run(string bot, string output)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            return FrontlineLabsQualificationCommand.Run(
                [
                    "--bot",
                    bot,
                    "--runtime",
                    "in-process",
                    "--out",
                    output,
                ]);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static int RunFoundation(string bot, string output)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            return FrontlineLabsQualificationCommand.Run(
                [
                    "--bot",
                    bot,
                    "--runtime",
                    "wasm",
                    "--suite",
                    "frontline-qualification-2",
                    "--out",
                    output,
                ]);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static int RunFundamentals(string bot, string output)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            return FrontlineLabsQualificationCommand.Run(
                [
                    "--bot",
                    bot,
                    "--runtime",
                    "wasm",
                    "--suite",
                    "frontline-qualification-3",
                    "--out",
                    output,
                ]);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static int RunTactical(string bot, string output)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            return FrontlineLabsQualificationCommand.Run(
                [
                    "--bot",
                    bot,
                    "--runtime",
                    "wasm",
                    "--suite",
                    "frontline-qualification-4",
                    "--out",
                    output,
                ]);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static int RunPositional(string bot, string output)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            return FrontlineLabsQualificationCommand.Run(
                [
                    "--bot",
                    bot,
                    "--runtime",
                    "wasm",
                    "--suite",
                    "frontline-qualification-5",
                    "--out",
                    output,
                ]);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static int Verify(string replayPath)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            return VerifyCommand.Run(replayPath);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static string CreateBot(
        string root,
        string name,
        string tickBody)
    {
        string directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        string sdkProject = Path.Combine(
            FindRepoRoot(),
            "src",
            "BotArena.Sdk",
            "BotArena.Sdk.csproj");
        File.WriteAllText(
            Path.Combine(directory, $"{name}.csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>net10.0</TargetFramework>
                 <Nullable>enable</Nullable>
                 <ImplicitUsings>enable</ImplicitUsings>
               </PropertyGroup>
               <ItemGroup>
                 <ProjectReference Include="{sdkProject}" />
               </ItemGroup>
             </Project>
             """);
        File.WriteAllText(
            Path.Combine(directory, $"{name}.cs"),
            $$"""
              using BotArena.Sdk;

              public sealed class {{name}} : IGenericActorBot
              {
                  public GenericActorDecision Tick(
                      GenericActorContext context)
                  {
              {{Indent(tickBody, 8)}}
                  }
              }
              """);
        File.WriteAllText(
            Path.Combine(directory, "botarena.json"),
            $$"""
              {
                "name": "{{name}}",
                "entryType": "{{name}}",
                "sdkVersion": "0.10.4"
              }
              """);
        return directory;
    }

    private static string Indent(string value, int spaces)
    {
        string prefix = new(' ', spaces);
        return string.Join(
            Environment.NewLine,
            value.Split('\n').Select(line =>
                prefix + line.TrimEnd('\r')));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(
                   directory.FullName,
                   "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }
}
