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
                "sdkVersion": "0.10.3"
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
