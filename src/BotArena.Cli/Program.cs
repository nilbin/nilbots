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
                        [--runtime wasm|in-process] [--out <dir>] [--viewer] [--open]
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
                   [--pendulum control|ratchet|ratchet-contest|keel|sticky-frontline|forward-rally|contest-majority|enemy-sole-decay]
                   [--skills none|kit|volley|shell|five-slots]
                   [--bend striker-only|universal]
                   [--five-slots full|trim|boom|drag|moor|wane]
                   [--stance-ground strict|free|open]
                   [--aim straight|offset]
                   [--cooldown frozen|ticking]
                   [--volley cast|salvo]
                   [--side-objective none|muster]
                   [--capture frozen|channel]
                   [--prime-respawn-ticks <positive-n>]
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
            arm. It composes with --classes and with --duel-map (declared
            manifest classes compose the same way); it is exclusive only
            with the unrelated numeric arms such as --capture-gain-phase or
            --net-control.
            --pendulum selects one pre-registered structural counterweight to
            the mean-reverting frontline. control is the measured baseline and
            adds no ruleset suffix. sticky-frontline holds a completed advance
            against enemy regression for 40 ticks. forward-rally lands
            respawns and companion arrivals on the own-side objective beside
            the fight instead of at home, on the rear-most free tile of that
            region measured along your own advance direction, so both sides
            arrive at mirrored distances from the fight. contest-majority
            makes surplus
            objective weight create pressure, so a lone body no longer nulls a
            committed force for free. enemy-sole-decay stops empty and
            contested ticks from destroying capture progress. ratchet is
            sticky-frontline plus forward-rally, ratchet-contest adds
            contest-majority, and keel adds enemy-sole-decay on top of that —
            every counterweight at once; those three are the registered factor
            levels, and comma-separated tokens compose any other ablation. A
            comma spelling that lands on a registered combination resolves to
            that same ruleset.
            --skills adds the pre-registered class-skill kit on top of a class
            pair. Each skill belongs to exactly one class, so a cell carries
            only the skills whose owning class is in it and kit is shorthand
            for all three. volley gives the striker a reversible windup-2
            stance whose gun fires three simultaneous bolts down the facing
            lane and both adjacent 45-degree headings, straight only, on a
            slower cadence; the stance is immobile and keeps objective weight
            1. shell gives the bulwark a reversible windup-1 stance that
            DEFLECTS enemy projectiles arriving inside its facing quadrant:
            the incoming bolt dies on the arc and a new bolt launches from the
            shell's tile along the exactly reversed heading, owned by the
            bulwark's team, so a bot that pokes a shell head-on is shot by its
            own fire. The stance cannot move, shoot, or rotate — the protected
            quadrant is chosen before the shield rises — and flank and rear
            hits still land. five-slots gives a fabricator team five unit slots
            against the opponent's three, unlocking at 60/180/300/420 with the
            extra two rebuilding on the slower 30-tick clock; that arm mints
            its own topology profile and fingerprint. Skills compose with
            --classes, --movement, --pendulum, the numbers-only factors, and
            --duel-map, subject to the 64-character canonical ID budget.
            --five-slots selects a registered FIVE SLOTS tuning variant
            (DECISIONS #171) and is only legal in a cell that carries the
            skill: full is the measured arm and adds no suffix; trim drops
            the fifth slot; boom swings the extra schedule late (360/480 on
            the same cadence); drag prices count in tempo by putting the
            ordinary children on the 30-tick baseline rebuild clock instead
            of the fabricator's native 15. Each single-lever variant moves
            exactly one lever; round 2's registered composites carry the two
            levers round 1 measured working on different edges — moor is
            trim + drag, wane is trim + a half-step 22-tick rebuild. Every
            variant mints its own suffixed ruleset identity.
            --stance-ground free drops the transition-placement-forbidden
            tag kind from the VOLLEY and AEGIS SHELL entry routes, so a
            skill stance can rise on objective tiles and in the central
            corridor; turret anchor routes keep the tag. open goes further
            (DECISIONS #176): EVERY transform placement is free — turret
            anchors included — and the turret becomes a true cycle:
            anchor/mobilize unlimited per life, health mapped proportionally
            (floored, minimum one) in both directions, no entry heal. A
            ground arm is inert where nothing it touches exists, so the
            same flags work on every pair. wane + free is registered as
            `berth`; the whole open game (aim + wane + open) as `deck`.
            --aim offset restores the ±1-sector (45°) initial launch offset
            on every class's mobile gun (DECISIONS #173) — the one-bend
            grammar had dropped it by conflation, never by ruling. Specials
            are untouched. Needs a class pair. rig + aim is registered as
            `sail`, and the whole tuned game (rig + aim + wane) as `crew`.
            --cooldown ticking advances a body's attack cooldown with TIME
            in every form (DECISIONS #180): a gunless stance or windup no
            longer freezes gun recovery. General for all classes. The open
            game on the ticking clock is registered as `tide`.
            --volley salvo re-arms the striker's fan (DECISIONS #182/#183):
            every bolt deals 2, the fan stops taxing the mobile gun's
            shared counter, the stance enters on the uniform 1-tick windup
            (the 2-tick fan was the game's only 2-tick public telegraph),
            and its frequency is priced on the stance ENTRY route instead
            — an 8-tick slot-scoped route cooldown (the first consumer of
            the #181 capability; it survives your death). Needs volley in
            the cell's kit; inert-omitted where no striker is present.
            tide + salvo is registered as `swell`.
            --side-objective muster adds MUSTER, the rally flag: a capturable
            site in the two widened alcoves on the map's centre column, held
            by SOLE objective weight for 12 consecutive ticks (any empty or
            contested tick puts the claim back to zero) and then latched
            until the other team completes a claim of its own. While a team
            owns it, that team's PRIME automatic returns land on the forward
            rally tile beside the fight; without it they walk from home. It
            is the ONLY source of a forward rally on this arm — the
            unconditional placement keel hands both teams is taken away and
            becomes the thing they fight over — and it is scoped to the
            Prime, so a fabricator's fourth body gains nothing extra. It
            never pays territorial progress. Read the owner and the running
            claim from the mode observation (secondaryOwnerTeamId,
            secondaryClaimProgress: signed, positive for team 0) and the
            site's regions, threshold, and effect from the contract's
            gameMode.secondaryControl. It runs on its own map generation
            (frontline-labs-02-muster) because the alcoves are widened to two
            approach headings, so it is a real arm on every pair rather than
            an inert-omitted one, and it needs a class pair or a --pendulum
            level to sit in. tide + muster is registered as `ensign`, and
            swell + muster as `banner`.
            --capture channel makes TAKING GROUND a channel (DECISIONS #187).
            Capture gain counts only bodies that did NOT change tile this
            tick; denial counts all of them, so a defender may kite inside
            the region and still subtract while an attacker that stepped
            contributes nothing. Stillness is positional: a blocked move did
            not move, and rotating, shooting, or starting a transform never
            breaks it. Hostile damage to a CONTROLLING body standing ON the
            objective reverts that team's work on the current run by the
            damage amount — never past where the run began, so being shot can
            never complete a capture for the shooter — while damage to a body
            OFF the objective reverts nothing, which is what makes screening
            a channeler the intended play. Gain scales with net stationary
            weight but is CAPPED AT 2, so extra channelers buy screens and
            denial rather than speed. Eroding an enemy claim is a channel
            too, at 4x build speed, which puts a full flip at 10 ticks
            against a fresh capture's 8. The paired `channel-speed` factor
            moves the threshold 15 -> 8. It publishes no new observation
            fact: every rule above moves captureProgress and claimingTeamId
            in their exact published shape — read the policy, the cap, the
            erosion multiple, and the interrupt from the contract's
            gameMode.capture (controlPolicy, stationaryGainMultiplierCap,
            opposingErosionMultiplier, claimInterrupt) instead of assuming
            them. It is a real arm on every pair and needs a class pair or a
            --pendulum level to sit in. swell + channel is registered as
            `siege`.
            Both stances spend a declared budget and then return by
            themselves: the volley returns the tick its fan launches (one cast
            per entry, so a parked striker cannot become artillery), and the
            shell shatters on its third deflection. Leaving earlier is still
            yours to do through the parameterless mobilize; leaving later is
            not. Read the budget from the return route's automaticReturn.
            --bend selects the curve grammar. striker-only (the default) is
            today's contract: only a chassis declaring shot programs bends.
            universal hands every class's mobile gun the one-bend grammar at
            its own depth — the striker keeps 1-4 tiles, the classes that gain
            it here get 1-2 — moving those guns from shoot-straight to shoot
            with an optional payload. Specials never curve: a volley profile
            refuses programmed shots and turret guns stay straight. It needs a
            class pair and composes like the other factors.
            --prime-respawn-ticks retunes the Prime automatic-return delay
            (18 by default); with --capture-threshold it is the numbers-only
            factor level. Both compose with --pendulum, --skills, --classes,
            --movement, and --duel-map, and none of them is compatible with
            --capture-gain-phase, --mobilize-turrets, --remote-fabrication,
            --net-control, --one-bend-shots, or --auto-companions.
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
