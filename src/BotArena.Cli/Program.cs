using BotArena.Cli;

return args switch
{
    ["play", .. var rest] => PlayCommand.Run(rest),
    ["replay", var file, .. var rest] => ReplayCommand.Run(file, rest),
    ["verify", var file] => VerifyCommand.Run(file),
    ["bots"] => ListCommand.Bots(),
    ["maps"] => ListCommand.Maps(),
    _ => Help(),
};

static int Help()
{
    Console.WriteLine("""
        Bot Arena CLI (prototype)

        Usage:
          botarena play [--bot <name>] [--opponent <name>] [--map <id>] [--seed <n>]
                        [--runtime wasm|in-process] [--max-ticks <n>] [--out <dir>]
          botarena replay <replay.json> [--out <dir>]
          botarena verify <replay.json>
          botarena bots
          botarena maps

        Defaults: --bot hunter --opponent wander --map basic-01 --seed 42 --runtime wasm

        `play` runs a full match (WASM runtime by default, per the plan), writes
        replay.json plus a self-contained viewer.html, and prints the result.
        `verify` re-checks a stored replay's hash for integrity.
        """);
    return 1;
}
