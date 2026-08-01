using System.Text.Json;
using BotArena.Engine;

namespace BotArena.Cli;

public static class VerifyCommand
{
    public static int Run(string file)
    {
        string json = ReplayInput.ReadAllText(file);
        using JsonDocument envelope = JsonDocument.Parse(json);
        int replayVersion = envelope.RootElement
            .GetProperty("header")
            .GetProperty("replayVersion")
            .GetInt32();
        if (replayVersion == BotArenaVersions.GenericActorReplayFormatVersion)
            return VerifyGeneric(json, envelope.RootElement);

        ReplayDocument document = ReplaySerializer.FromJson(json);
        var rebuilt = new Replay
        {
            Header = document.Header,
            Ticks = document.Ticks,
            Result = document.Result,
        };
        string actual = ReplaySerializer.ComputeHash(rebuilt);
        Console.WriteLine($"Stored hash:   {document.ReplayHash}");
        Console.WriteLine($"Computed hash: {actual}");
        if (actual == document.ReplayHash)
        {
            Console.WriteLine("OK: replay content matches its hash.");
            return 0;
        }
        Console.Error.WriteLine(
            "MISMATCH: replay content does not match its stored hash.");
        return 1;
    }

    private static int VerifyGeneric(
        string json,
        JsonElement envelope)
    {
        string? stored = envelope
            .GetProperty("replayHash")
            .GetString();
        Console.WriteLine($"Stored hash:   {stored}");
        if (GenericActorReplayDocument.VerifyHash(
                json,
                out string? failure))
        {
            Console.WriteLine(
                "OK: canonical replay v3 content, contract, and hash verify.");
            return 0;
        }

        Console.Error.WriteLine(
            $"MISMATCH: replay v3 verification failed: {failure}");
        return 1;
    }
}
