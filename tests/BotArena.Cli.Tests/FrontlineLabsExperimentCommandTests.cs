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
    }

    private static int Run(
        string bot,
        string opponent,
        string output)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        try
        {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
            return FrontlineLabsExperimentCommand.Run(
                [
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
                "sdkVersion": "0.10.2"
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
