using System.Text.Json;
using BotArena.Cli;

namespace BotArena.Cli.Tests;

[Collection("Console")]
public sealed class FrontlineLabsExperimentCommandTests
{
    [Fact]
    public void InProcessRun_WritesDeterministicVerifiableReplayV3()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-cli-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string first = Path.Combine(temporary, "first");
            string second = Path.Combine(temporary, "second");

            Assert.Equal(0, Run(alpha, beta, first));
            Assert.Equal(0, Run(alpha, beta, second));

            string firstPath = Path.Combine(first, "replay.json");
            string firstJson = File.ReadAllText(firstPath);
            string secondJson = File.ReadAllText(
                Path.Combine(second, "replay.json"));
            Assert.Equal(firstJson, secondJson);

            using JsonDocument document = JsonDocument.Parse(firstJson);
            JsonElement root = document.RootElement;
            Assert.False(root.GetProperty("partial").GetBoolean());
            Assert.Equal(
                3,
                root.GetProperty("header")
                    .GetProperty("replayVersion")
                    .GetInt32());
            Assert.Equal(
                "frontline-labs-1",
                root.GetProperty("header")
                    .GetProperty("gameRulesVersion")
                    .GetString());
            Assert.Equal(
                "frontline-labs-01",
                root.GetProperty("header")
                    .GetProperty("contract")
                    .GetProperty("map")
                    .GetProperty("mapId")
                    .GetString());
            Assert.Equal(
                64,
                root.GetProperty("replayHash").GetString()!.Length);
            Assert.Equal(0, Verify(firstPath));

            string tamperedPath = Path.Combine(
                temporary,
                "tampered.json");
            File.WriteAllText(
                tamperedPath,
                firstJson.Replace(
                    "\"gameRulesVersion\":\"frontline-labs-1\"",
                    "\"gameRulesVersion\":\"frontline-labs-2\"",
                    StringComparison.Ordinal));
            Assert.Equal(1, Verify(tamperedPath));
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void MissingEntrantsAndUnknownOptions_FailBeforeRunning()
    {
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run([]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--future-option",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--seeds",
                    "1,2",
                    "--open",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--capture-threshold",
                    "0",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--capture-gain-phase",
                    "300",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--capture-threshold",
                    "12",
                    "--capture-gain-phase",
                    "300:2",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--capture-threshold",
                    "12",
                    "--mobilize-turrets",
                ]));
        Assert.Throws<InvalidOperationException>(
            () => FrontlineLabsExperimentCommand.Run(
                [
                    "--bot",
                    ".",
                    "--opponent",
                    ".",
                    "--mobilize-turrets",
                    "false",
                ]));
    }

    [Fact]
    public void CaptureThresholdArm_WritesDistinctRulesetIdentity()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-arm-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "capture-12");

            Assert.Equal(0, Run(alpha, beta, output, "12"));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "replay.json")));
            JsonElement header = document.RootElement.GetProperty("header");
            Assert.Equal(
                "frontline-labs-1-experiment-capture-12",
                header.GetProperty("gameRulesVersion").GetString());
            Assert.Equal(
                12,
                header.GetProperty("contract")
                    .GetProperty("rules")
                    .GetProperty("gameMode")
                    .GetProperty("capture")
                    .GetProperty("threshold")
                    .GetInt32());
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void CaptureGainPhaseArm_WritesScheduleAndDistinctIdentity()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-gain-arm-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "gain-t300-2");

            Assert.Equal(
                0,
                Run(
                    alpha,
                    beta,
                    output,
                    captureGainPhase: "300:2"));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "replay.json")));
            JsonElement header = document.RootElement.GetProperty("header");
            Assert.Equal(
                "frontline-labs-1-experiment-gain-t300-2",
                header.GetProperty("gameRulesVersion").GetString());
            JsonElement schedule = header.GetProperty("contract")
                .GetProperty("rules")
                .GetProperty("gameMode")
                .GetProperty("capture")
                .GetProperty("gainSchedule");
            Assert.Equal(2, schedule.GetArrayLength());
            Assert.Equal(
                ("late-escalation", 300, 2),
                (
                    schedule[1].GetProperty("phaseId").GetString(),
                    schedule[1].GetProperty("startsAtTick").GetInt32(),
                    schedule[1]
                        .GetProperty("gainPerSoleTeamTick")
                        .GetInt32()));
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void MobilizeArm_WritesActionTransitionAndDistinctIdentity()
    {
        string temporary = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-frontline-labs-mobilize-arm-{Guid.NewGuid():N}");
        try
        {
            string alpha = CreateWaitBot(temporary, "Alpha");
            string beta = CreateWaitBot(temporary, "Beta");
            string output = Path.Combine(temporary, "mobilize");

            Assert.Equal(
                0,
                Run(
                    alpha,
                    beta,
                    output,
                    mobilizeTurrets: true));

            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(output, "replay.json")));
            JsonElement rules = document.RootElement
                .GetProperty("header")
                .GetProperty("contract")
                .GetProperty("rules");
            Assert.Equal(
                "frontline-labs-1-experiment-mobilize",
                rules.GetProperty("rulesetId").GetString());
            Assert.Contains(
                rules.GetProperty("actions").EnumerateArray(),
                action =>
                    action.GetProperty("id").GetString() == "mobilize"
                    && action.GetProperty("code").GetInt32() == 104);
            JsonElement transition = Assert.Single(
                rules.GetProperty("sameLifeTransitions").EnumerateArray(),
                value =>
                    value.GetProperty("transitionId").GetString()
                    == "mobilize-child");
            Assert.Equal(
                "turret",
                transition.GetProperty("sourceFormId").GetString());
            Assert.Equal(
                "child-mobile",
                transition.GetProperty("targetFormId").GetString());
            Assert.True(
                transition.GetProperty("irreversibleForLife").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    private static int Run(
        string bot,
        string opponent,
        string output,
        string? captureThreshold = null,
        string? captureGainPhase = null,
        bool mobilizeTurrets = false)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            var arguments = new List<string>
            {
                "--bot",
                bot,
                "--opponent",
                opponent,
                "--runtime",
                "in-process",
                "--seed",
                "42",
                "--out",
                output,
            };
            if (captureThreshold is not null)
            {
                arguments.Add("--capture-threshold");
                arguments.Add(captureThreshold);
            }
            if (captureGainPhase is not null)
            {
                arguments.Add("--capture-gain-phase");
                arguments.Add(captureGainPhase);
            }
            if (mobilizeTurrets)
                arguments.Add("--mobilize-turrets");
            return FrontlineLabsExperimentCommand.Run(arguments);
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

    private static string CreateWaitBot(
        string root,
        string name)
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
                      GenericActorActionLegality wait =
                          context.Action("wait")!;
                      return GenericActorDecision.WithoutArguments(
                          wait.ActionId,
                          wait.ActionCode);
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
