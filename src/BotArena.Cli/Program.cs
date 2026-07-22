using BotArena.Cli;

try
{
    return args switch
    {
        ["new", var name] => NewCommand.Run(name),
        ["build", .. var rest] => BuildCommand.Run(rest),
        ["play", .. var rest] => PlayCommand.Run(rest),
        ["watch", .. var rest] => WatchCommand.Run(rest),
        ["replay", var file, .. var rest] => ReplayCommand.Run(file, rest),
        ["verify", var file] => VerifyCommand.Run(file),
        ["doctor"] => DoctorCommand.Run(),
        ["cache", .. var rest] => CacheCommand.Run(rest),
        ["bots"] => ListCommand.Bots(),
        ["maps"] => ListCommand.Maps(),
        _ => Help(),
    };
}
catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static int Help()
{
    Console.WriteLine("""
        Bot Arena CLI (prototype)

        Usage:
          botarena new <Name>                     create a bot project
          botarena build [dir] [--no-cache]       compile a bot project to WASM (cached)
          botarena play [--bot <spec>] [--opponent <spec>] [--map <id>] [--seed <n>]
                        [--runtime wasm|in-process] [--max-ticks <n>] [--out <dir>]
          botarena watch [dir] [play options]     rebuild + replay on every change
          botarena replay <replay.json> [--out]   re-emit the visual viewer
          botarena verify <replay.json>           check a replay's hash
          botarena doctor                         toolchain and environment status
          botarena cache [status|clear]           build cache maintenance
          botarena bots | botarena maps           list built-ins

        A bot <spec> is a built-in name (hunter, wander, coward, idle), a bot
        project directory, or a path to a .wasm artifact.
        Defaults: --bot hunter --opponent wander --map basic-01 --seed 42 --runtime wasm

        `play` runs the match in the official WASM sandbox, writes replay.json plus
        a self-contained viewer.html, and prints the result and replay hash.
        """);
    return 1;
}
