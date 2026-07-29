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
        ["new", var name, .. var rest] => NewCommand.Run(name, rest),
        ["register", .. var rest] => ServerCommands.Register(rest),
        ["login", .. var rest] => ServerCommands.Login(rest),
        ["logout"] => ServerCommands.Logout(),
        ["whoami"] => ServerCommands.WhoAmI(),
        ["submit", .. var rest] => ServerCommands.Submit(rest),
        ["rank", .. var rest] => ServerCommands.Rank(rest),
        ["spar", .. var rest] => ServerCommands.Spar(rest),
        ["leaderboard", .. var rest] => ServerCommands.Leaderboard(rest),
        ["build", .. var rest] => BuildCommand.Run(rest),
        ["play", .. var rest] => PlayCommand.Run(rest),
        ["experiment", "frontline", .. var rest] =>
            FrontlineExperimentCommand.Run(rest),
        ["experiment", "frontline-labs", "qualify", .. var rest] =>
            FrontlineLabsQualificationCommand.Run(rest),
        ["experiment", "frontline-labs", .. var rest] =>
            FrontlineLabsExperimentCommand.Run(rest),
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
          nilbots new <Name> [--profile duel|generic-actor]
                                                  create a bot project
          nilbots register [--server url]         create an account + sign in via the browser
                        [--email <a@b.c> --password <pw> [--name <display>]]
                                                  ...or headless, with no browser at all
          nilbots login [--server url]            sign in via the browser (OAuth + PKCE)
                        [--email <a@b.c> --password <pw>]   ...or headless
          nilbots submit [dir]                    build locally + submit for the canonical
                                                  server build; reports artifact parity
          nilbots rank <your-bot>                 play a RANKED set; the server
                                                  matchmakes your opponent by rating
          nilbots spar <your-bot> <opponent>      one UNRANKED match against a bot you
                                                  name; never touches the ladder
          nilbots leaderboard [--rules <version>] the ladder (no account needed to look)
          nilbots whoami | nilbots logout
          nilbots build [dir] [--no-cache]        compile a bot project to WASM (cached;
                                                  also copies the artifact to <dir>/out/bot.wasm)
          nilbots play [--bot <spec>] [--opponent <spec>] [--map <id>]
                        [--seed <n> | --seeds a,b,c] [--swap] [--runtime wasm|in-process]
                        [--rules <name>]  the game: 0.5 (current, the default) — or an
                                 older shipped version 0.4|0.3|0.2|0.1 to replay history.
                                 Everything else is a RESEARCH ARM used to evaluate
                                 candidate mechanics; they are not the game and may
                                 change or vanish: control|cone-control|cone-active|
                                 cone-active-bolt1|cone-active-bolt2|cone-active-bolt2-overtime|
                                 cone-active-bolt2-overtime-gain|cone-active-bolt2-arcs|
                                 cone-occupancy-bolt2-arcs|0.5-control|cone|
                                 bolts|conebolts|conebolts1|strafe|hill|hill-shared|slate|energy
                        [--max-ticks <n>] [--out <dir>]
          nilbots experiment frontline
                        [--bot <actor-spec>] [--opponent <actor-spec>]
                        [--map frontline-01] [--rules frontline-alpha-1]
                        [--seed <n> | --seeds a,b,c] [--swap]
                        [--runtime wasm|in-process] [--out <dir>] [--open]
                                                  LOCAL EXPERIMENT: replication,
                                                  Anchor/turrets, replay v2;
                                                  never ranked or server-admitted
          nilbots experiment frontline-labs
                        --bot <generic-spec> --opponent <generic-spec>
                        [--seed <n> | --seeds a,b,c] [--swap]
                        [--runtime wasm|in-process] [--out <dir>] [--open]
                                                  exact hosted Labs v1 contract,
                                                  local/quota-free, replay v3
          nilbots experiment frontline-labs qualify
                        --bot <generic-spec> [--runtime wasm|in-process]
                        [--suite frontline-qualification-1|frontline-qualification-2|frontline-qualification-3|frontline-qualification-4|frontline-qualification-5]
                        [--out <dir>]
                                                  versioned local capability
                                                  probes; never ranked
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
        A Frontline <actor-spec> is an actor built-in (`nilbots help experiment`),
        an IActorBot project directory, or an actor-protocol .wasm artifact.
        A Labs <generic-spec> is an IGenericActorBot project directory or a
        generic-actor-profile .wasm artifact; both entrants must be named.
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
            Usage: nilbots new <Name> [--profile duel|generic-actor]
            Creates <Name>/ with bot source, botarena.json, a portable SDK
            reference, and profile-specific authoring instructions.

            duel is the shipped default. generic-actor creates an experimental
            IGenericActorBot scaffold for `nilbots experiment frontline-labs`.
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
        "experiment" => """
            Usage: nilbots experiment frontline
                   [--bot <actor-spec>] [--opponent <actor-spec>]
                   [--map frontline-01] [--rules frontline-alpha-1]
                   [--seed <n> | --seeds a,b,c] [--swap]
                   [--runtime wasm|in-process] [--out <dir>] [--open]

            Runs the local-only Frontline experiment: one policy controls a team
            whose independent lives may fabricate children and Anchor as turrets.
            It writes canonical replay-v2 JSON plus a self-contained Canvas2D
            viewer. This path is never ranked, submitted, or server-admitted.

            Actor built-ins:
              frontline-rusher       objective-first; never Anchors
              frontline-swarm        fabricates every child; stays mobile
              frontline-bastion      fabricates, Anchors, and fires turrets
              frontline-counterpunch defends its half, then advances on contact
              frontline-probe        protocol/action diagnostic, not a doctrine

            Defaults: frontline-rusher vs frontline-bastion, map frontline-01,
                      seed 42, runtime wasm, rules frontline-alpha-1.
            Use --runtime in-process for fast diagnostic iteration, then confirm
            candidate behavior in the default WASM sandbox.

            Usage: nilbots experiment frontline-labs
                   --bot <generic-spec> --opponent <generic-spec>
                   [--seed <n> | --seeds a,b,c] [--swap]
                   [--capture-threshold <positive-n>]
                   [--capture-gain-phase <start-tick>:<gain>]
                   [--mobilize-turrets]
                   [--remote-fabrication]
                   [--net-control]
                   [--one-bend-shots]
                   [--auto-companions]
                   [--duel-map current|thin-fronts|outer-shoulder-bypass]
                   [--classes <class>-vs-<class>]
                   [--movement preserve-facing|move-sets-facing|facing-locked]
                   [--print-candidate-contract]
                   [--runtime wasm|in-process] [--out <dir>] [--open]

            Runs the exact immutable hosted Frontline Labs v1 resolved contract
            through the generic actor session and writes canonical replay v3,
            without App authentication, queues, or quotas. It is unranked.
            --capture-threshold creates a local-only ruleset with a distinct,
            content-descriptive ruleset ID; it never reinterprets hosted v1.
            --capture-gain-phase does the same while publishing a deterministic
            tick-phase schedule that bots can resolve from context.Tick.
            --mobilize-turrets adds a one-way turret-to-mobile same-life
            transition under its own local-only ruleset identity.
            --remote-fabrication lets a Prime explicitly queue a Ready child
            from any walkable tile while retaining the protected home output.
            --net-control lets surplus objective weight create capture pressure
            instead of treating every two-team presence as a complete contest.
            --one-bend-shots limits mobile fire to straight or one private
            45-degree bend after 1-4 tiles for the duel-depth screen.
            --auto-companions applies those one-bend rules and creates child
            lives automatically at ticks 120 and 260. It may be paired with
            --duel-map; fabrication and Split are absent from this isolated arm.
            --duel-map runs those same one-bend rules on a content-identified
            map arm. thin-fronts raises the positional cost of retreat;
            outer-shoulder-bypass adds an earlier, longer flank without opening
            the last-moment central choke.
            --classes gives each team one pre-registered chassis (bulwark,
            fabricator, or striker) under its own local-only ruleset identity.
            Pairs are canonical in alphabetical order. A project may instead
            declare its class in botarena.json ("class": "striker"): declared
            classes select the arm automatically, always bind each bot to its
            class's canonical team side, and must agree with an explicit
            --classes. --ignore-declared-classes runs classed projects on the
            explicit or base contract instead (the contract qualification
            exercises). --print-candidate-contract honors declared classes
            when bot specs are given. Movement and projectile kinematics stay
            shared; classes differ in durability, vision, fire tempo, shot
            language, anchor play, and fabrication economics. May be paired
            with --duel-map.
            --movement selects the pre-registered movement-kinematics arm.
            preserve-facing is the default measured baseline: a step never
            turns the body, and it adds no ruleset suffix. move-sets-facing
            turns the body to the direction a successful step moved, so
            backpedal-kiting costs the aim it was holding. facing-locked
            offers only the current facing to a movement action, making a
            turn a separate decision. Absolute rotate is unchanged in every
            arm. It may be paired with --classes (and through it --duel-map);
            on its own it is a standalone arm and cannot be combined with the
            other experiment options.
            Both entrants are required; a generic spec is an IGenericActorBot
            project or a generic-actor-profile WASM artifact.
            --print-candidate-contract emits the exact resolved candidate
            identity and exits; bot arguments are not required in this mode.

            Usage: nilbots experiment frontline-labs qualify
                   --bot <generic-spec> [--runtime wasm|in-process]
                   [--seed <n>]
                   [--suite frontline-qualification-1|frontline-qualification-2|frontline-qualification-3|frontline-qualification-4|frontline-qualification-5]
                   [--out <dir>]

            Runs mirrored, versioned capability probes and writes verified
            replay-v3 evidence plus qualification.json. Suite 1 is the frozen
            T4 entry-initiative component. Suite 2 requires WASM and freezes
            the automatic-life foundation component. Suite 3 is the
            cumulative WASM-only T2 duel-depth union profile; suite 4 adds
            cumulative T3 tactical geometry; suite 5 adds cumulative T4
            positional doctrine and is the first balance-eligible tier.
            Probe failure returns exit code 3; runtime or contract failure
            returns 2. It is never ranked.
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
            Usage: nilbots submit [dir] [--allow-toolchain-skew]
            Builds locally, submits source for the canonical server rebuild, and
            reports artifact parity. Creates the remote bot when needed.
            Sign in first with `nilbots register` or `nilbots login`.

            Your CLI and the server must build with the same SDK and build pipeline,
            or the artifact you tested is not the one that ranks you. When they differ
            submit stops before compiling and tells you to run:
              dotnet tool update -g Nilbots
            --allow-toolchain-skew submits anyway; the server build still decides the
            match, you simply give up the local/server parity guarantee.
            """,
        "register" => """
            Usage: nilbots register [--server <url>]
                   nilbots register --email <a@b.c> --password <pw> [--name <display>] [--server <url>]

            With no arguments, opens secure browser registration and signs the CLI in
            with OAuth + PKCE. Defaults to https://nilbots.com.

            HEADLESS (no browser — CI, containers, agents): pass --email and --password
            and the CLI completes the same OAuth + PKCE grant over HTTP itself.
            --name sets the display name (defaults to the local part of the email).
            """,
        "login" => """
            Usage: nilbots login [--server <url>]
                   nilbots login --email <a@b.c> --password <pw> [--server <url>]

            With no arguments, signs in through the browser with OAuth + PKCE.
            Defaults to https://nilbots.com.

            HEADLESS (no browser — CI, containers, agents): pass --email and --password
            to complete the same grant without opening anything.
            """,
        "rank" => """
            Usage: nilbots rank <your-bot> [--rules <name>]

            Queues a RANKED set (6 mirrored games) and moves elo. You do NOT pick the
            opponent: the server matchmakes one of the bots nearest your rating, because
            a ladder where you choose your fights measures who you avoided rather than
            how good your bot is.

            To fight a specific bot, play unranked: `nilbots spar <your-bot> <opponent>`.
            `nilbots set` is different again — a LOCAL simulation that changes nothing on
            the server.

            --rules exists to be explicit, not to choose: only the ruleset the server is
            currently running accepts new sets. Older versions keep their ladders and
            every result on them — `nilbots leaderboard --rules 0.4` still reads — but
            they are closed, because a matchmade opponent never agreed to play a retired
            ruleset.

            Results are withheld until every game has broadcast, so nothing spoils the
            watch; then `nilbots leaderboard` shows the new standings.
            """,
        "spar" => """
            Usage: nilbots spar <your-bot> <opponent> [--map <id>] [--seed <n>]

            One UNRANKED match against a bot you name — yours or anyone's. No elo, no
            ladder, no consequences: this is where you test a matchup as often as you
            like. Both bots need a successfully built active version on the server.

            Omit --map and --seed and the server picks; pass them to reproduce a
            specific fight.
            """,
        "leaderboard" => """
            Usage: nilbots leaderboard [--rules <version>] [--server <url>]

            Prints the ranked ladder. No account required — you can look before you play.
            Each ruleset has its OWN ladder, so an older bot is never invalidated by a
            new ruleset; --rules picks which one to show.
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
