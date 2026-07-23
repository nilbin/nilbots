using BotArena.Cli;
using BotArena.Toolchain;

// A help request anywhere wins over command dispatch — otherwise `build --help`
// tries to build a project directory literally named "--help".
if (args.Length == 0 || args is ["help", ..] || args.Any(a => a is "--help" or "-h"))
    return Help(exitCode: args.Length == 0 ? 1 : 0);

try
{
    return args switch
    {
        ["new", var name] => NewCommand.Run(name),
        ["login", .. var rest] => ServerCommands.Login(rest),
        ["logout"] => ServerCommands.Logout(),
        ["whoami"] => ServerCommands.WhoAmI(),
        ["submit", .. var rest] => ServerCommands.Submit(rest),
        ["build", .. var rest] => BuildCommand.Run(rest),
        ["play", .. var rest] => PlayCommand.Run(rest),
        ["set", .. var rest] => SetCommand.Run(rest),
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
catch (BotBuildException ex)
{
    // The message already carries the extracted compiler diagnostics and the log path.
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}
catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static int Help(int exitCode = 1)
{
    Console.WriteLine("""
        Bot Arena CLI (prototype)

        Usage:
          botarena new <Name>                     create a bot project
          botarena login [--server url]           sign in via the browser (OAuth + PKCE)
          botarena submit [dir]                   build locally + submit for the canonical
                                                  server build; reports artifact parity
          botarena whoami | botarena logout
          botarena build [dir] [--no-cache]       compile a bot project to WASM (cached;
                                                  also copies the artifact to <dir>/out/bot.wasm)
          botarena play [--bot <spec>] [--opponent <spec>] [--map <id>]
                        [--seed <n> | --seeds a,b,c] [--swap] [--runtime wasm|in-process]
                        [--rules 0.4|0.3|0.2|0.1|0.5-control|cone|bolts|conebolts|conebolts1|strafe|hill|hill-shared|slate|energy]
                        [--max-ticks <n>] [--out <dir>]
          botarena set --bot <spec> --opponent <spec> [--maps a,b,c] [--seeds x,y,z]
                        [--runtime ...]           the ranked 6-game mirrored set, locally
          botarena watch [dir] [play options]     rebuild + replay on every change
          botarena replay <replay.json> [--summary [--no-debug] [--full]] [--out]
                                                  compact match digest, or the visual viewer
          botarena verify <replay.json>           check a replay's hash
          botarena doctor                         toolchain and environment status
          botarena cache [status|clear]           build cache maintenance
          botarena bots | botarena maps           list built-ins

        A bot <spec> is a built-in name (hunter, wander, coward, idle), a bot
        project directory, or a path to a .wasm artifact.
        Defaults: --bot hunter --opponent wander --map basic-01 --seed 42 --runtime wasm
        A `"rules"` field in your project's botarena.json pins the default --rules
        for play/set (an explicit flag always wins) — set it while practicing for
        a rules experiment so a dropped flag can't put you on the wrong game.

        `play` runs the match in the official WASM sandbox, writes replay.json plus
        a self-contained viewer.html, and prints the result and replay hash.
        Output defaults to out/<bot>-vs-<opponent>-<map>-s<seed>/ so parallel runs
        never overwrite each other; --out <dir> pins an exact directory.
        Iterate with --runtime in-process (plain build, seconds; not
        submission-equivalent), batch seeds with --seeds, play slot 1 with --swap,
        and use `set` + `replay --summary` for ranked-shape testing and loss forensics.
        """);
    return exitCode;
}
