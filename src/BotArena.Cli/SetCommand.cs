using System.Globalization;
using BotArena.Engine;

namespace BotArena.Cli;

/// <summary>
/// Runs the ranked 6-game mirrored set locally (gen-2 finding #5): 3 map/seed pairs,
/// each played from both starting positions — the exact shape `POST /api/matches/ranked`
/// executes server-side, in one process with one summary table. This is also the
/// balance-harness primitive (docs/GAME-DESIGN.md): same bots, candidate rules, compare.
/// </summary>
public static class SetCommand
{
    // Mirrors RankedEndpoints.SetMaps — the server's ranked pool.
    private static readonly string[] DefaultMaps = ["basic-01", "arena-01", "arena-01"];

    public static int Run(IReadOnlyList<string> args)
    {
        var options = CliSupport.ParseOptions(args);
        string botSpec = options.GetValueOrDefault("bot", ".");
        string opponentSpec = options.TryGetValue("opponent", out string? opp)
            ? opp
            : throw new InvalidOperationException("botarena set requires --opponent <spec>.");
        string runtimeKind = options.GetValueOrDefault("runtime", "wasm");

        string[] maps = options.TryGetValue("maps", out string? mapList)
            ? mapList.Split(',').Select(m => m.Trim()).ToArray()
            : DefaultMaps;
        ulong[] seeds = options.TryGetValue("seeds", out string? seedList)
            ? seedList.Split(',').Select(s => ulong.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray()
            : maps.Select(_ => (ulong)System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue)).ToArray();
        if (seeds.Length != maps.Length)
            throw new InvalidOperationException(
                $"--seeds must provide one seed per map ({maps.Length} map(s), {seeds.Length} seed(s)).");

        var rules = GameRules.V0_1;
        Console.WriteLine($"Local ranked-format set: {maps.Length} map/seed pair(s) x both positions " +
                          $"({runtimeKind}, rules {rules.RulesVersion})");
        Console.WriteLine();

        double myScore = 0;
        string? myName = null, oppName = null;
        int game = 0;
        foreach (var (mapId, seed) in maps.Zip(seeds))
        {
            var map = CliSupport.LoadMap(mapId);
            foreach (bool mirrored in new[] { false, true })
            {
                game++;
                var (first, second) = mirrored ? (opponentSpec, botSpec) : (botSpec, opponentSpec);
                var (run, name0, name1, written) = PlayCommand.RunSingle(
                    first, second, map, seed, runtimeKind, rules, outDirOverride: null, quiet: true);
                int mySlot = mirrored ? 1 : 0;
                (myName, oppName) = mirrored ? (name1, name0) : (name0, name1);
                string verdict;
                if (run.Result.WinnerSlot is int winner)
                {
                    bool iWon = winner == mySlot;
                    myScore += iWon ? 1 : 0;
                    verdict = iWon ? $"WIN  ({myName})" : $"LOSS ({oppName} wins)";
                }
                else
                {
                    myScore += 0.5;
                    verdict = "DRAW";
                }
                Console.WriteLine($"g{game} {mapId,-10} s{seed,-12} slot{mySlot}  " +
                                  $"{verdict,-24} {run.Result.Reason,-16} t{run.Result.EndTick,-4} {written.ReplayPath}");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"Set result: {myName} {myScore.ToString(CultureInfo.InvariantCulture)} - " +
                          $"{(game - myScore).ToString(CultureInfo.InvariantCulture)} {oppName}" +
                          (myScore > game / 2.0 ? $"  →  {myName} takes the set"
                           : myScore < game / 2.0 ? $"  →  {oppName} takes the set" : "  →  set drawn"));
        return 0;
    }
}
