using BotArena.Cli;
using BotArena.Toolchain;

// A help request wins over command dispatch — otherwise `build --help` tries
// to build a project directory literally named "--help". Keep the named
// command so authors get its options and examples instead of only global help.
if (args.Length == 0)
    return Help(exitCode: 1);
if (args[0] is "--help" or "-h" || args is ["help"])
    return Help(exitCode: 0);
// `--version` must answer with a version, not the help text — a bug report needs
// the exact CLI/SDK/rules triple, and `doctor` was the only place it existed.
if (args[0] is "--version" or "-v" or "version")
{
    Console.WriteLine($"nilbots {ToolchainInfo.CliVersion} " +
        $"(SDK {ToolchainInfo.SdkVersion}, game rules {BotArena.Engine.BotArenaVersions.GameRulesVersion}, " +
        $"runtime protocol {BotArena.Engine.BotArenaVersions.RuntimeProtocolVersion})");
    return 0;
}
if (args is ["help", var helpCommand, ..])
    return CommandHelp(helpCommand);
if (args.Skip(1).Any(a => a is "--help" or "-h"))
    return CommandHelp(args[0]);

try
{
    return args switch
    {
        ["new", var name] => NewCommand.Run(name),
        ["register", .. var rest] => ServerCommands.Register(rest),
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
catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException
    or ArgumentException or DirectoryNotFoundException or IOException
    or System.Net.Http.HttpRequestException or TaskCanceledException)
{
    // Expected user-facing failures (bad argument, unreachable server, missing file):
    // one clean line, never a stack trace — those leaked CI build paths to players.
    Console.Error.WriteLine($"error: {Describe(ex)}");
    return 1;
}
catch (Exception ex)
{
    // Last resort: an unexpected fault is still a bug, but a player should get a
    // readable line and a way to produce the full trace for a report.
    Console.Error.WriteLine($"error: {Describe(ex)}");
    Console.Error.WriteLine("This looks like a bug. Set NILBOTS_DEBUG=1 and re-run for the full trace.");
    if (Environment.GetEnvironmentVariable("NILBOTS_DEBUG") is "1" or "true")
        Console.Error.WriteLine(ex);
    return 1;
}

static string Describe(Exception ex) => ex switch
{
    System.Net.Http.HttpRequestException or TaskCanceledException =>
        $"could not reach the server: {ex.Message.TrimEnd('.')}. " +
        "Check the URL (--server) and your connection; `nilbots doctor` shows the configured server.",
    _ => ex.Message,
};

static int Help(int exitCode = 1)
{
    Console.WriteLine("""
        nilbots CLI (prototype)

        Usage:
          nilbots new <Name>                      create a bot project
          nilbots register [--server url]         create an account + sign in via the browser
          nilbots login [--server url]            sign in via the browser (OAuth + PKCE)
          nilbots submit [dir]                    build locally + submit for the canonical
                                                  server build; reports artifact parity
          nilbots whoami | nilbots logout
          nilbots build [dir] [--no-cache]        compile a bot project to WASM (cached;
                                                  also copies the artifact to <dir>/out/bot.wasm)
          nilbots play [--bot <spec>] [--opponent <spec>] [--map <id>]
                        [--seed <n> | --seeds a,b,c] [--swap] [--runtime wasm|in-process]
                        [--rules 0.5|0.4|0.3|0.2|0.1|control|cone-control|cone-active|
                                 cone-active-bolt1|cone-active-bolt2|cone-active-bolt2-overtime|
                                 cone-active-bolt2-overtime-gain|cone-active-bolt2-arcs|
                                 cone-occupancy-bolt2-arcs|
                                 0.5-control|cone|
                                 bolts|conebolts|conebolts1|strafe|hill|hill-shared|slate|energy]
                        [--max-ticks <n>] [--out <dir>]
          nilbots set --bot <spec> --opponent <spec> [--maps a,b,c] [--seeds x,y,z]
                        [--runtime ...] [--out <dir>]
                                                  ranked mirrored set; preserves each game
          nilbots watch [dir] [play options]      rebuild + replay on every change
          nilbots replay <replay.json> [--summary [--no-debug] [--full]] [--out]
                                                  compact match digest, or the visual viewer
          nilbots verify <replay.json>            check a replay's hash
          nilbots doctor                          toolchain and environment status
          nilbots cache [status|clear]            build cache maintenance
          nilbots bots | nilbots maps             list built-ins

        A bot <spec> is a built-in name (hunter, wander, coward, idle), a bot
        project directory, or a path to a .wasm artifact.
        Defaults: --bot hunter --opponent wander --map basic-01 --seed 42
                  --runtime wasm --rules 0.5
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

static int CommandHelp(string command)
{
    string? help = command.ToLowerInvariant() switch
    {
        "new" => """
            Usage: nilbots new <Name>
            Creates <Name>/ with Bot.cs, botarena.json, and a portable SDK reference.
            """,
        "build" => """
            Usage: nilbots build [dir] [--no-cache]
            Compiles a bot to canonical WASM and copies it to <dir>/out/bot.wasm.
            Use --no-cache only to force a verification rebuild.
            """,
        "play" => """
            Usage: nilbots play [--bot <spec>] [--opponent <spec>] [--map <id>]
                   [--seed <n> | --seeds a,b,c] [--swap]
                   [--runtime wasm|in-process] [--rules <name>]
                   [--max-ticks <n>] [--out <dir>]
            Example: nilbots play --bot . --opponent hunter --runtime in-process \
                     --seeds 7,42,1337
            """,
        "set" => """
            Usage: nilbots set --bot <spec> --opponent <spec>
                   [--maps a,b,c] [--seeds x,y,z] [--runtime wasm|in-process]
                   [--rules <name>] [--max-ticks <n>] [--out <dir>]
            Runs every map/seed from both slots. --out preserves each game in a
            gNN-<map>-s<seed>-slot<N>/ subdirectory.
            """,
        "watch" => """
            Usage: nilbots watch [dir] [play options]
            Rebuilds and opens a new replay whenever bot source changes.
            """,
        "replay" => """
            Usage: nilbots replay <replay.json> [--summary [--no-debug] [--full]]
                   [--out <dir>]
            Without --summary, writes a self-contained viewer.html.
            """,
        "verify" => """
            Usage: nilbots verify <replay.json>
            Recomputes and checks the canonical replay hash.
            """,
        "doctor" => """
            Usage: nilbots doctor
            Reports SDK/toolchain versions, build backend, Docker/wasi-sdk, and cache.
            """,
        "cache" => """
            Usage: nilbots cache [status|clear]
            Shows or clears the content-addressed player-WASM build cache.
            """,
        "submit" => """
            Usage: nilbots submit [dir]
            Builds locally, submits source for the canonical server rebuild, and
            reports artifact parity. Creates the remote bot when needed.
            Sign in first with `nilbots register` or `nilbots login`.
            """,
        "register" => """
            Usage: nilbots register [--server <url>]
            Opens secure browser registration, then signs the CLI in with OAuth + PKCE.
            Defaults to https://nilbots.com.
            """,
        "login" => """
            Usage: nilbots login [--server <url>]
            Signs in through the browser with OAuth + PKCE.
            Defaults to https://nilbots.com.
            """,
        "logout" => "Usage: nilbots logout",
        "whoami" => "Usage: nilbots whoami",
        "bots" => "Usage: nilbots bots",
        "maps" => "Usage: nilbots maps",
        _ => null,
    };
    if (help is null)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        return Help();
    }
    Console.WriteLine(help);
    return 0;
}
