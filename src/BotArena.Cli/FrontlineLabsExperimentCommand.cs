using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using BotArena.Engine;
using BotArena.Toolchain;

namespace BotArena.Cli;

/// <summary>
/// Local authoring loop for the immutable hosted Frontline Labs v1 definition
/// and explicitly content-identified, local-only numeric experiment arms. It
/// bypasses App authentication and quotas, but uses the same resolved
/// contract, generic session, replay-v3 projection, and WASM runtime.
/// </summary>
public static class FrontlineLabsExperimentCommand
{
    public static int Run(IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(
            options,
            "bot",
            "opponent",
            "seed",
            "seeds",
            "swap",
            "runtime",
            "profile",
            "out",
            "open",
            "viewer",
            "capture-threshold",
            "capture-gain-phase",
            "prime-respawn-ticks",
            "pendulum",
            "mobilize-turrets",
            "remote-fabrication",
            "net-control",
            "one-bend-shots",
            "auto-companions",
            "duel-map",
            "classes",
            "movement",
            "skills",
            "bend",
            "five-slots",
            "stance-ground",
            "aim",
            "cooldown",
            "volley",
            "side-objective",
            "capture",
            "economy",
            "roster",
            "horizon",
            "ignore-declared-classes",
            "print-candidate-contract");
        if (options.ContainsKey("seed") && options.ContainsKey("seeds"))
        {
            throw new InvalidOperationException(
                "Use either --seed or --seeds, not both.");
        }

        string runtimeKind = options
            .GetValueOrDefault("runtime", "wasm")
            .ToLowerInvariant();
        if (runtimeKind is not ("wasm" or "in-process"))
        {
            throw new InvalidOperationException(
                $"Unknown runtime '{runtimeKind}' " +
                "(use wasm or in-process).");
        }

        bool mindProfile = CliSupport.ParseLabsProfile(
            options.GetValueOrDefault("profile"));

        ulong[] seeds = ParseSeeds(options);
        if (options.ContainsKey("open") && seeds.Length != 1)
        {
            throw new InvalidOperationException(
                "--open requires a single --seed.");
        }

        int? captureThreshold = OptionalPositiveInt(
            options,
            "capture-threshold");
        int? primeRespawnTicks = OptionalPositiveInt(
            options,
            "prime-respawn-ticks");
        FrontlineLabsPendulumArm pendulum = OptionalPendulumArm(options);
        FrontlineLabsSkillKit requestedSkills = OptionalSkillKit(options);
        (int StartsAtTick, int Gain)? captureGainPhase =
            OptionalCaptureGainPhase(options);
        bool mobilizeTurrets = OptionalFlag(
            options,
            "mobilize-turrets");
        bool remoteFabrication = OptionalFlag(
            options,
            "remote-fabrication");
        bool netControl = OptionalFlag(
            options,
            "net-control");
        bool oneBendShots = OptionalFlag(
            options,
            "one-bend-shots");
        bool automaticCompanions = OptionalFlag(
            options,
            "auto-companions");
        // THE ONE FLAG AUTHORS ASKED FOR THREE WAVES RUNNING (#184, #188):
        // `identity` (the bare flag, the historical behaviour) prints the ids
        // and fingerprints; `full` prints the COMPLETE resolved canonical
        // contract — the same bytes a bot reads at MatchStart and the same
        // bytes replay.json carries in header.contract, which is what every
        // wave mined a throwaway match for.
        FrontlineLabsContractPrintMode? printCandidateContract =
            OptionalContractPrintMode(options);
        FrontlineLabsDuelMapArm? duelMapArm =
            OptionalDuelMapArm(options);
        ActorMovementFacingCoupling movementCoupling =
            OptionalMovementCoupling(options);
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classPair =
            OptionalClassPair(options);

        // Class is bot identity (DECISIONS #154): a classed project always
        // plays its declared chassis. Declared classes select the arm when
        // --classes is absent, must agree with it when present, and always
        // bind each bot to its class's canonical team side.
        // --ignore-declared-classes runs classed projects on the explicit or
        // base contract instead — the path qualification exercises.
        bool ignoreDeclaredClasses = OptionalFlag(
            options,
            "ignore-declared-classes");
        string? botSpec = null;
        string? opponentSpec = null;
        if (printCandidateContract is not null
            && !ignoreDeclaredClasses
            && classPair is null
            && options.ContainsKey("bot")
            && options.ContainsKey("opponent"))
        {
            // Print mode takes no bots normally, but when specs are given
            // their declared classes resolve the printed identity exactly as
            // a run would — the one command whose job is "show the resolved
            // contract" must not silently show a different one.
            FrontlineLabsClassDefinition? printDeclared0 =
                DeclaredClass(options["bot"]);
            FrontlineLabsClassDefinition? printDeclared1 =
                DeclaredClass(options["opponent"]);
            if (printDeclared0 is not null && printDeclared1 is not null)
            {
                classPair = string.CompareOrdinal(
                        printDeclared0.Id, printDeclared1.Id) <= 0
                    ? (printDeclared0, printDeclared1)
                    : (printDeclared1, printDeclared0);
            }
        }
        if (printCandidateContract is null)
        {
            botSpec = RequiredOption(options, "bot");
            opponentSpec = RequiredOption(options, "opponent");
            if (options.ContainsKey("swap"))
                (botSpec, opponentSpec) = (opponentSpec, botSpec);
            FrontlineLabsClassDefinition? declared0 =
                ignoreDeclaredClasses ? null : DeclaredClass(botSpec);
            FrontlineLabsClassDefinition? declared1 =
                ignoreDeclaredClasses ? null : DeclaredClass(opponentSpec);
            if (classPair is null
                && declared0 is not null
                && declared1 is not null)
            {
                if (string.CompareOrdinal(declared0.Id, declared1.Id) > 0)
                {
                    (botSpec, opponentSpec) = (opponentSpec, botSpec);
                    (declared0, declared1) = (declared1, declared0);
                }
                classPair = (declared0, declared1);
                Console.WriteLine(
                    "Classes resolved from bot manifests: "
                    + $"{declared0.Id}-vs-{declared1.Id}.");
            }
            else if (classPair is { } requested)
            {
                if (declared0 is not null
                    && declared0.Id != requested.TeamZero.Id)
                {
                    throw new InvalidOperationException(
                        $"--bot declares class '{declared0.Id}' but the "
                        + $"requested pair puts '{requested.TeamZero.Id}' "
                        + "on team 0. A classed bot always plays its "
                        + "declared chassis.");
                }
                if (declared1 is not null
                    && declared1.Id != requested.TeamOne.Id)
                {
                    throw new InvalidOperationException(
                        $"--opponent declares class '{declared1.Id}' but "
                        + $"the requested pair puts '{requested.TeamOne.Id}' "
                        + "on team 1. A classed bot always plays its "
                        + "declared chassis.");
                }
            }
            else if ((declared0 is null) != (declared1 is null))
            {
                throw new InvalidOperationException(
                    "One entrant declares a class and the other does not. "
                    + "Declare both (or pass --classes explicitly with "
                    + "class-agnostic bots).");
            }
        }
        // --movement composes with --classes and with --duel-map (a
        // coupling test on the retreat-punishing map is exactly the wanted
        // experiment); it is exclusive only with the unrelated numeric arms.
        bool standaloneMovementArm =
            movementCoupling != ActorMovementFacingCoupling.PreserveFacing
            && classPair is null;
        // A pendulum level, a Prime-respawn retune, or a capture threshold
        // carried alongside a class or movement factor is one cell of the
        // pre-registered phase-1 factorial (DECISIONS #158) rather than a
        // standalone arm, so those factors compose instead of excluding
        // each other.
        // A class-skill kit is the phase-2 factor and lands in the same cell
        // factory: skills are class capabilities, so they only ever appear
        // alongside a class pair (explicit or manifest-declared).
        if (requestedSkills != FrontlineLabsSkillKit.None
            && classPair is null)
        {
            throw new InvalidOperationException(
                "--skills selects per-class stance and slot capabilities, so "
                + "it needs a class pair: pass --classes <a>-vs-<b> or run "
                + "two class-declaring projects.");
        }
        FrontlineLabsBendEnvelopeArm bendEnvelope =
            OptionalBendEnvelope(options);
        if (bendEnvelope != FrontlineLabsBendEnvelopeArm.StrikerOnly
            && classPair is null)
        {
            throw new InvalidOperationException(
                "--bend hands the curve grammar to class chassis, so it "
                + "needs a class pair: pass --classes <a>-vs-<b> or run two "
                + "class-declaring projects.");
        }
        FrontlineLabsSkillKit skills = FrontlineLabsDefinition.EffectiveSkills(
            requestedSkills,
            classPair);
        if (requestedSkills != FrontlineLabsSkillKit.None
            && skills == FrontlineLabsSkillKit.None)
        {
            throw new InvalidOperationException(
                "No class in this cell owns the requested skill. VOLLEY is "
                + "the striker's, AEGIS SHELL the bulwark's, and FIVE SLOTS "
                + "the fabricator's.");
        }
        FrontlineLabsFiveSlotVariant fiveSlots =
            OptionalFiveSlotVariant(options);
        if (fiveSlots != FrontlineLabsFiveSlotVariant.Full
            && !skills.HasFlag(FrontlineLabsSkillKit.FabricatorFiveSlots))
        {
            throw new InvalidOperationException(
                "--five-slots tunes the FIVE SLOTS skill, so the cell must "
                + "carry it: pass a class pair containing the fabricator and "
                + "a --skills selection that includes five-slots.");
        }
        // Inert-omitted where nothing it touches exists (the skills rule):
        // the engine downgrades a ground arm that changes no bytes, so one
        // uniform flag set works across every pair of a wave.
        FrontlineLabsStanceGroundArm stanceGround =
            OptionalStanceGround(options);
        FrontlineLabsAimArm aim = OptionalAimArm(options);
        if (aim != FrontlineLabsAimArm.Straight && classPair is null)
        {
            throw new InvalidOperationException(
                "--aim hands the ±45° launch offsets to class chassis, so "
                + "it needs a class pair: pass --classes <a>-vs-<b> or run "
                + "two class-declaring projects.");
        }
        FrontlineLabsCooldownArm cooldownArm = OptionalCooldownArm(options);
        if (cooldownArm != FrontlineLabsCooldownArm.Frozen
            && classPair is null)
        {
            throw new InvalidOperationException(
                "--cooldown is registered for the class game; pass "
                + "--classes <a>-vs-<b> or run two class-declaring "
                + "projects.");
        }
        // Validated against the REQUESTED kit, not the class-effective one:
        // the engine inert-omits salvo in a strikerless cell, so one
        // uniform flag set works across every pair of a wave.
        FrontlineLabsVolleyArm volleyArm = OptionalVolleyArm(options);
        if (volleyArm != FrontlineLabsVolleyArm.Cast
            && !requestedSkills.HasFlag(FrontlineLabsSkillKit.StrikerVolley))
        {
            throw new InvalidOperationException(
                "--volley tunes the VOLLEY skill, so the cell must carry "
                + "it: pass a --skills selection that includes volley.");
        }
        // A side objective is a real arm on every cell: it changes the map
        // for both teams and declares a typed capability, so it is never
        // inert-omitted the way --volley salvo is in a strikerless cell.
        FrontlineLabsSideObjectiveArm sideObjective =
            OptionalSideObjective(options);
        if (sideObjective != FrontlineLabsSideObjectiveArm.None
            && classPair is null
            && pendulum == FrontlineLabsPendulumArm.None)
        {
            throw new InvalidOperationException(
                "--side-objective adds a contested site to the class game, "
                + "so it needs a cell to sit in: pass --classes "
                + "<a>-vs-<b> (or run two class-declaring projects), or "
                + "compose it with a --pendulum level.");
        }
        // A longer horizon re-prices every pacing gate both teams play
        // against, so it is a real arm on every pair and needs a cell.
        FrontlineLabsHorizonArm horizonArm = OptionalHorizonArm(options);
        if (horizonArm != FrontlineLabsHorizonArm.Standard
            && classPair is null
            && pendulum == FrontlineLabsPendulumArm.None)
        {
            throw new InvalidOperationException(
                "--horizon long re-prices every pacing gate both teams play "
                + "against, so it needs a cell to sit in: pass --classes "
                + "<a>-vs-<b> (or run two class-declaring projects), or "
                + "compose it with a --pendulum level.");
        }
        // The roster states its shape per class, so — like the skills, the
        // aim grammar and the cooldown clock — it needs a class pair. It also
        // mints its own map generation, which is why it cannot share a cell
        // with the side objective.
        FrontlineLabsRosterArm rosterArm = OptionalRosterArm(options);
        if (rosterArm != FrontlineLabsRosterArm.None && classPair is null)
        {
            throw new InvalidOperationException(
                "--roster legion declares three starting bodies per class "
                + "(four slots for the fabricator), so it needs a class "
                + "pair: pass --classes <a>-vs-<b> or run two "
                + "class-declaring projects.");
        }
        if (rosterArm != FrontlineLabsRosterArm.None
            && sideObjective != FrontlineLabsSideObjectiveArm.None)
        {
            throw new InvalidOperationException(
                "--roster and --side-objective each mint their own map "
                + "generation, so they cannot run in the same cell: pick "
                + "one.");
        }
        // The channel reworks capture for BOTH teams whatever chassis they
        // are, so — like the side objective — it is a real arm on every pair
        // rather than an inert-omitted one, and it needs a cell to sit in.
        FrontlineLabsCaptureArm captureArm = OptionalCaptureArm(options);
        if (captureArm != FrontlineLabsCaptureArm.Frozen
            && classPair is null
            && pendulum == FrontlineLabsPendulumArm.None)
        {
            throw new InvalidOperationException(
                "--capture channel reworks the front both teams fight over, "
                + "so it needs a cell to sit in: pass --classes <a>-vs-<b> "
                + "(or run two class-declaring projects), or compose it with "
                + "a --pendulum level.");
        }
        // The economy is a real arm on every pair too: the deposits, the
        // wreckage, and the ladder are the same whatever chassis are in the
        // cell, so it is never inert-omitted and it needs a cell to sit in.
        // It is mutually exclusive with --side-objective, because both claim
        // the side lanes' attention and a cell carrying both could attribute
        // neither.
        FrontlineLabsEconomyArm economyArm = OptionalEconomyArm(options);
        if (economyArm != FrontlineLabsEconomyArm.None
            && sideObjective != FrontlineLabsSideObjectiveArm.None)
        {
            throw new InvalidOperationException(
                "--economy and --side-objective both claim the side lanes' "
                + "attention, so they cannot run in the same cell: pick one.");
        }
        if (economyArm != FrontlineLabsEconomyArm.None
            && classPair is null
            && pendulum == FrontlineLabsPendulumArm.None)
        {
            throw new InvalidOperationException(
                "--economy adds a resource both teams fight over, so it "
                + "needs a cell to sit in: pass --classes <a>-vs-<b> (or run "
                + "two class-declaring projects), or compose it with a "
                + "--pendulum level.");
        }
        bool pendulumCell =
            captureArm != FrontlineLabsCaptureArm.Frozen
            || economyArm != FrontlineLabsEconomyArm.None
            || rosterArm != FrontlineLabsRosterArm.None
            || horizonArm != FrontlineLabsHorizonArm.Standard
            || pendulum != FrontlineLabsPendulumArm.None
            || primeRespawnTicks is not null
            || skills != FrontlineLabsSkillKit.None
            || bendEnvelope != FrontlineLabsBendEnvelopeArm.StrikerOnly
            || sideObjective != FrontlineLabsSideObjectiveArm.None
            || (captureThreshold is not null
                && (classPair is not null || standaloneMovementArm));
        bool duelExperiment = oneBendShots
            || automaticCompanions
            || (duelMapArm is not null
                && classPair is null
                && !standaloneMovementArm
                && !pendulumCell);
        int experimentCount =
            (captureThreshold is null || pendulumCell ? 0 : 1)
            + (captureGainPhase is null ? 0 : 1)
            + (mobilizeTurrets ? 1 : 0)
            + (remoteFabrication ? 1 : 0)
            + (netControl ? 1 : 0)
            + (duelExperiment ? 1 : 0)
            + (classPair is null || pendulumCell ? 0 : 1)
            + (standaloneMovementArm && !pendulumCell ? 1 : 0)
            + (pendulumCell ? 1 : 0);
        if (experimentCount > 1)
        {
            throw new InvalidOperationException(
                "Use one Frontline Labs experiment option at a time.");
        }
        ActorResolvedMatchDefinition definition;
        if (pendulumCell)
        {
            definition = FrontlineLabsDefinition.CreatePendulumExperiment(
                pendulum,
                classPair,
                duelMapArm ?? FrontlineLabsDuelMapArm.Current,
                movementCoupling,
                captureThreshold
                    ?? FrontlineLabsDefinition.DefaultCaptureThreshold,
                primeRespawnTicks
                    ?? FrontlineLabsDefinition.DefaultPrimeRespawnTicks,
                skills,
                bendEnvelope,
                fiveSlots,
                stanceGround,
                aim,
                cooldownArm,
                volleyArm,
                sideObjective,
                captureArm,
                economyArm,
                rosterArm,
                horizonArm);
        }
        else if (captureThreshold is int threshold)
        {
            definition = FrontlineLabsDefinition
                .CreateCaptureThresholdExperiment(threshold);
        }
        else if (captureGainPhase is { } phase)
        {
            definition = FrontlineLabsDefinition
                .CreateCaptureGainPhaseExperiment(
                    phase.StartsAtTick,
                    phase.Gain);
        }
        else if (mobilizeTurrets)
        {
            definition = FrontlineLabsDefinition.CreateMobilizeExperiment();
        }
        else if (remoteFabrication)
        {
            definition =
                FrontlineLabsDefinition.CreateRemoteFabricationExperiment();
        }
        else if (netControl)
        {
            definition = FrontlineLabsDefinition.CreateNetControlExperiment();
        }
        else if (classPair is { } selectedClasses)
        {
            definition = FrontlineLabsDefinition.CreateClassesExperiment(
                selectedClasses.TeamZero,
                selectedClasses.TeamOne,
                duelMapArm ?? FrontlineLabsDuelMapArm.Current,
                movementCoupling);
        }
        else if (standaloneMovementArm)
        {
            definition = FrontlineLabsDefinition
                .CreateMovementCouplingExperiment(
                    movementCoupling,
                    duelMapArm ?? FrontlineLabsDuelMapArm.Current);
        }
        else if (automaticCompanions)
        {
            definition = FrontlineLabsDefinition
                .CreateAutomaticCompanionsExperiment(
                    duelMapArm ?? FrontlineLabsDuelMapArm.Current);
        }
        else if (duelMapArm is { } mapArm)
        {
            definition =
                FrontlineLabsDefinition.CreateOneBendShotsExperiment(mapArm);
        }
        else if (oneBendShots)
        {
            definition =
                FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        }
        else
        {
            definition = FrontlineLabsDefinition.Create();
        }
        // THE MIND (DECISIONS #191). The same match, driven by one runtime per
        // participant instead of one per body life: same rules, same map, same
        // format, same topology, same mode. Only the capability tuple moves,
        // which is exactly the shape the null pin needs — a difference in
        // outcome can then only have come from the driver.
        if (mindProfile)
            definition = definition.OnProfile(ActorMatchCapabilityVersions.Mind);
        if (printCandidateContract is { } printMode)
        {
            PrintCandidateContract(definition, printMode);
            return 0;
        }

        string resolvedBotSpec = botSpec!;
        string resolvedOpponentSpec = opponentSpec!;
        Console.WriteLine(
            experimentCount == 0
                ? "LOCAL LABS: exact hosted Frontline Labs v1 contract; " +
                  "unranked and quota-free."
                : "LOCAL LABS: content-identified experiment; " +
                  "unranked, quota-free, and not the hosted v1 ruleset.");
        Console.WriteLine($"Runtime:           {runtimeKind}");
        Console.WriteLine($"Rules:             {definition.Rules.RulesetId}");
        if (skills != FrontlineLabsSkillKit.None)
        {
            Console.WriteLine(
                "Skills:            "
                + string.Join(
                    ", ",
                    FrontlineLabsDefinition.Skills
                        .Where(skill => skills.HasFlag(skill))
                        .Select(skill => skill.ToString()))
                + (skills == requestedSkills
                    ? string.Empty
                    : " (requested skills without an owning class in this "
                        + "cell change no contract bytes and are dropped)"));
            Console.WriteLine(
                "Topology profile:  "
                + FrontlineLabsDefinition.TopologyProfileIdFor(
                    definition.Topology));
        }
        if (sideObjective != FrontlineLabsSideObjectiveArm.None)
        {
            Console.WriteLine(
                "Side objective:    muster — the owner's PRIME respawns "
                + "rally forward; nobody else does");
        }
        if (economyArm != FrontlineLabsEconomyArm.None)
        {
            Console.WriteLine(
                economyArm == FrontlineLabsEconomyArm.Scrap
                    ? "Economy:           scrap — veins of 8 at (11,1)/"
                        + "(11,13) every 70 ticks from 60 through 620; "
                        + "wrecks drop 2; carry, bank at home, invest in "
                        + "edge/plate/optic up to a full board of 6 tiers"
                    : "Economy:           scrap-flat (CONTROL) — same veins, "
                        + "carrying, and ladder, but the bank buys greedily "
                        + "by itself and no invest verb exists");
        }
        if (horizonArm != FrontlineLabsHorizonArm.Standard)
        {
            Console.WriteLine(
                "Horizon:           long — "
                + $"{FrontlineLabsDefinition.MaxTicks(horizonArm)} ticks "
                + "instead of 500; read limits.maxTicks, do not assume it");
        }
        if (rosterArm != FrontlineLabsRosterArm.None)
        {
            Console.WriteLine(
                "Roster:            legion — three bodies from tick 0 (the "
                + "fabricator stands FOUR; its verb prices the later "
                + "tranches, which it fabricates), +2 at "
                + $"{FrontlineLabsLegionRoster.MidTrancheUnlockTick} and +3 "
                + $"at {FrontlineLabsLegionRoster.LateTrancheUnlockTick}; "
                + "eight at the horn, nine for the fabricator");
            if (skills == FrontlineLabsSkillKit.None)
            {
                Console.WriteLine(
                    "Topology profile:  "
                    + FrontlineLabsDefinition.TopologyProfileIdFor(
                        definition.Topology));
            }
        }
        if (bendEnvelope != FrontlineLabsBendEnvelopeArm.StrikerOnly)
        {
            Console.WriteLine(
                "Bend envelope:     universal (every mobile gun bends; the "
                + "striker keeps the deepest, and specials never curve)");
        }
        Console.WriteLine(
            $"Contract profile:  " +
            $"{definition.CapabilityVersions.ContractProfileId}"
            + (mindProfile
                ? " (one mind per participant, for the whole match)"
                : ""));
        Console.WriteLine(
            $"Map:               {definition.Map.Id} " +
            $"v{definition.Map.Version} " +
            $"({definition.Map.Width}x{definition.Map.Height})");
        Console.WriteLine(
            $"Rules fingerprint: {ActorContractFingerprint.ComputeRules(
                definition.Rules)}");
        Console.WriteLine(
            $"Map fingerprint:   {ActorContractFingerprint.ComputeMap(
                definition.Map)}");
        Console.WriteLine(
            $"Match fingerprint: {ActorContractFingerprint.ComputeMatch(
                definition)}");
        Console.WriteLine();

        int wins = 0;
        int losses = 0;
        int draws = 0;
        for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
        {
            ulong seed = seeds[seedIndex];
            using ResolvedLabsEntrant bot0 =
                ResolvedLabsEntrant.Resolve(
                    resolvedBotSpec,
                    runtimeKind,
                    mindProfile,
                    quiet: seeds.Length > 1);
            using ResolvedLabsEntrant bot1 =
                ResolvedLabsEntrant.Resolve(
                    resolvedOpponentSpec,
                    runtimeKind,
                    mindProfile,
                    quiet: seeds.Length > 1);
            if (seedIndex == 0)
            {
                // On the mind profile the programming model is worth naming:
                // a native mind against a wrapped per-life bot is a legitimate
                // and interesting match, and the reader should be able to see
                // which one they are watching.
                string model0 = mindProfile ? $" {bot0.ProgrammingModel}" : "";
                string model1 = mindProfile ? $" {bot1.ProgrammingModel}" : "";
                Console.WriteLine(
                    $"Participants:      {bot0.Name} " +
                    $"[{bot0.RuntimeKind}{model0}] " +
                    $"vs {bot1.Name} [{bot1.RuntimeKind}{model1}]");
                Console.WriteLine();
            }

            var participants = new[]
            {
                bot0.ToParticipant(participantId: 0, teamId: 0),
                bot1.ToParticipant(participantId: 1, teamId: 1),
            };
            // THE ABORT BOUNDARY (#188's engineering queue). Everything that
            // can refuse the match — a chronology invariant, a replay
            // validation, a runtime the host could not drive — happens inside
            // here, strictly before the replay is written. So an aborted cell
            // leaves no document behind and exits with the abort code rather
            // than looking like a completed one.
            (GenericActorMatchResult result,
                GenericActorReplayDocument replay) = MatchRun.Guard(
                MatchRun.Cell(bot0.Name, bot1.Name, seed),
                () =>
                {
                    using var session = new GenericActorMatchSession(
                        definition,
                        participants,
                        seed);
                    GenericActorMatchResult ran = session.Run();
                    return (
                        ran,
                        GenericActorReplayDocument.Create(
                            session,
                            FrontlineLabsReplayPresentation.Create(
                                definition)));
                });

            string outDir = OutputDirectory(
                options.GetValueOrDefault("out"),
                bot0.Name,
                bot1.Name,
                seed,
                seeds.Length > 1);
            // The self-contained viewer embeds the whole replay into a
            // multi-megabyte theme template — most of a sweep's disk
            // footprint for a file nobody opens (owner ruling: viewers are
            // opt-in for experiments). --open implies one; --viewer forces
            // one without opening it.
            WrittenReplay written = ReplayOutput.WriteJson(
                replay.CanonicalJson,
                outDir,
                withViewer: options.ContainsKey("open")
                    || options.ContainsKey("viewer"));
            _ = result.WinnerTeamId switch
            {
                0 => wins++,
                1 => losses++,
                _ => draws++,
            };

            if (seeds.Length == 1)
            {
                PrintResult(
                    result,
                    replay.ReplayHash,
                    bot0.Name,
                    bot1.Name,
                    seed);
                Console.WriteLine($"Replay:  {written.ReplayPath}");
                Console.WriteLine(
                    written.ViewerPath is not null
                        ? $"Viewer:  {written.ViewerPath}"
                        : options.ContainsKey("open")
                            || options.ContainsKey("viewer")
                        ? "Viewer:  unavailable (build web/dist-cli first)"
                        : "Viewer:  not written (pass --viewer or --open)");
                if (options.ContainsKey("open")
                    && written.ViewerPath is not null)
                {
                    TryOpen(written.ViewerPath);
                }
            }
            else
            {
                string verdict = result.WinnerTeamId switch
                {
                    0 => $"{bot0.Name} wins",
                    1 => $"{bot1.Name} wins",
                    _ => "draw",
                };
                Console.WriteLine(
                    $"seed {seed,-12} {verdict,-28} " +
                    $"{Reason(result),-18} " +
                    $"t{result.EndTick ?? -1,-4} {written.ReplayPath}");
            }

            if (result.EligibleTeamIds.Length != 2)
            {
                Console.Error.WriteLine(
                    "Frontline Labs participant faulted or was " +
                    $"disqualified; preserved replay: {written.ReplayPath}");
                PrintWasmDiagnostics(bot0);
                PrintWasmDiagnostics(bot1);
                return 2;
            }
        }

        if (seeds.Length > 1)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Total ({seeds.Length} seeds, W = slot-0 bot wins): " +
                $"{wins}W {losses}L {draws}D");
        }
        return 0;
    }

    /// <summary>
    /// Reads <c>--print-candidate-contract [identity|full]</c>. The bare flag
    /// keeps its historical meaning, so every existing sweep script and the
    /// preflight gate read the same identity object they always did.
    /// </summary>
    private static FrontlineLabsContractPrintMode? OptionalContractPrintMode(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue(
                "print-candidate-contract",
                out string? value))
        {
            return null;
        }
        return value.ToLowerInvariant() switch
        {
            "true" or "identity" => FrontlineLabsContractPrintMode.Identity,
            "full" or "contract" => FrontlineLabsContractPrintMode.Full,
            _ => throw new InvalidOperationException(
                $"Unknown --print-candidate-contract mode '{value}' "
                + "(use identity — the default — or full, which emits the "
                + "complete resolved canonical contract JSON)."),
        };
    }

    private static void PrintCandidateContract(
        ActorResolvedMatchDefinition definition,
        FrontlineLabsContractPrintMode mode)
    {
        if (mode == FrontlineLabsContractPrintMode.Full)
        {
            // The EXACT bytes: the same canonical document the runtime is
            // handed at MatchStart and the same one a replay-v3 header
            // carries, so a number read here is the number the match plays.
            Console.WriteLine(
                ActorContractManifestSerializer.ToCanonicalJson(definition));
            return;
        }

        var contract = new
        {
            modeId = definition.Rules.GameMode.ModeId,
            rulesetId = definition.Rules.RulesetId,
            rulesFingerprint =
                ActorContractFingerprint.ComputeRules(definition.Rules),
            seedProfileId =
                definition.Rules.SeedMechanics.SeedProfileId,
            mapId = definition.Map.Id,
            mapVersion = definition.Map.Version,
            mapFingerprint =
                ActorContractFingerprint.ComputeMap(definition.Map),
            formatId = definition.Format.FormatId,
            formatFingerprint =
                ActorContractFingerprint.ComputeFormat(definition.Format),
            topologyProfileId =
                FrontlineLabsDefinition.TopologyProfileIdFor(
                    definition.Topology),
            topologyFingerprint =
                ActorContractFingerprint.ComputeTopology(
                    definition.Topology),
            contractProfileId =
                definition.CapabilityVersions.ContractProfileId,
            matchContractFingerprint =
                ActorContractFingerprint.ComputeMatch(definition),
        };
        Console.WriteLine(
            JsonSerializer.Serialize(
                contract,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void PrintWasmDiagnostics(ResolvedLabsEntrant bot)
    {
        foreach ((string subject, ulong peak, ulong budget, string reason)
                 in bot.SandboxFailures)
        {
            Console.Error.WriteLine(
                $"  {bot.Name} {subject}: {reason} " +
                $"(peak completed tick fuel {peak / 1_000_000.0:F1}M/" +
                $"{budget / 1_000_000.0:F1}M)");
        }
    }

    private static string RequiredOption(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out string? value)
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"nilbots experiment frontline-labs requires --{name} " +
                "<project|wasm>.");
        }
        return value;
    }

    private static int? OptionalPositiveInt(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out string? raw))
            return null;
        if (!int.TryParse(
                raw,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int value)
            || value <= 0)
        {
            throw new InvalidOperationException(
                $"--{name} must be a positive integer.");
        }
        return value;
    }

    private static (int StartsAtTick, int Gain)?
        OptionalCaptureGainPhase(
            IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("capture-gain-phase", out string? raw))
            return null;
        string[] parts = raw.Split(
            ':',
            StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(
                parts[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int startsAtTick)
            || !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int gain)
            || startsAtTick <= 0
            || gain <= 0)
        {
            throw new InvalidOperationException(
                "--capture-gain-phase must be <positive-start-tick>:<positive-gain>.");
        }
        return (startsAtTick, gain);
    }

    private static bool OptionalFlag(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out string? value))
            return false;
        if (!string.Equals(
                value,
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"--{name} does not accept a value.");
        }
        return true;
    }

    private static FrontlineLabsDuelMapArm? OptionalDuelMapArm(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("duel-map", out string? value))
            return null;
        return value.ToLowerInvariant() switch
        {
            "current" => FrontlineLabsDuelMapArm.Current,
            "thin-fronts" => FrontlineLabsDuelMapArm.ThinFronts,
            "outer-shoulder-bypass" =>
                FrontlineLabsDuelMapArm.OuterShoulderBypass,
            _ => throw new InvalidOperationException(
                $"Unknown --duel-map '{value}' " +
                "(use current, thin-fronts, or outer-shoulder-bypass)."),
        };
    }

    /// <summary>
    /// Reads the movement-kinematics arm. Omitting the option — or naming
    /// <c>preserve-facing</c> explicitly — selects today's measured baseline
    /// and adds no ruleset suffix, so every existing arm identity stays byte
    /// for byte what it was.
    /// </summary>
    private static ActorMovementFacingCoupling OptionalMovementCoupling(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("movement", out string? value))
            return ActorMovementFacingCoupling.PreserveFacing;
        return value.ToLowerInvariant() switch
        {
            "preserve-facing" =>
                ActorMovementFacingCoupling.PreserveFacing,
            "move-sets-facing" =>
                ActorMovementFacingCoupling.FaceMovementDirection,
            "facing-locked" =>
                ActorMovementFacingCoupling.FacingLocked,
            _ => throw new InvalidOperationException(
                $"Unknown --movement '{value}' " +
                "(use preserve-facing, move-sets-facing, or facing-locked)."),
        };
    }

    /// <summary>
    /// Reads the curve-grammar arm. Omitting the option — or naming
    /// <c>striker-only</c> explicitly — selects today's measured contract and
    /// adds no ruleset suffix, so every existing arm identity stays byte for
    /// byte what it was. <c>universal</c> hands every class's mobile gun the
    /// one-bend grammar at its own declared depth; specials never curve either
    /// way.
    /// </summary>
    private static FrontlineLabsBendEnvelopeArm OptionalBendEnvelope(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("bend", out string? value))
            return FrontlineLabsBendEnvelopeArm.StrikerOnly;
        return value.ToLowerInvariant() switch
        {
            "striker-only" => FrontlineLabsBendEnvelopeArm.StrikerOnly,
            "universal" => FrontlineLabsBendEnvelopeArm.Universal,
            _ => throw new InvalidOperationException(
                $"Unknown --bend '{value}' "
                + "(use striker-only or universal)."),
        };
    }

    /// <summary>
    /// Reads the registered cooldown-clock arm (DECISIONS #180). Omitting
    /// the option — or naming <c>frozen</c> — keeps the historical clock
    /// (an unarmed form freezes the remaining cooldown) and adds no
    /// ruleset suffix. <c>ticking</c> advances the cooldown with time in
    /// every form.
    /// </summary>
    private static FrontlineLabsCooldownArm OptionalCooldownArm(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("cooldown", out string? value))
            return FrontlineLabsCooldownArm.Frozen;
        return value.ToLowerInvariant() switch
        {
            "frozen" => FrontlineLabsCooldownArm.Frozen,
            "ticking" => FrontlineLabsCooldownArm.Ticking,
            _ => throw new InvalidOperationException(
                $"Unknown --cooldown arm '{value}' (use frozen or "
                + "ticking)."),
        };
    }

    /// <summary>
    /// Reads the registered volley arm (DECISIONS #182/#183). Omitting
    /// the option — or naming <c>cast</c> — keeps the measured phase-2
    /// fan and adds no ruleset suffix. <c>salvo</c> re-arms the fan:
    /// every bolt deals 2, the fan stops taxing the mobile gun's counter,
    /// the stance enters on the uniform 1-tick windup, and its frequency
    /// moves to an 8-tick cooldown on the stance entry route.
    /// </summary>
    private static FrontlineLabsVolleyArm OptionalVolleyArm(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("volley", out string? value))
            return FrontlineLabsVolleyArm.Cast;
        return value.ToLowerInvariant() switch
        {
            "cast" => FrontlineLabsVolleyArm.Cast,
            "salvo" => FrontlineLabsVolleyArm.Salvo,
            _ => throw new InvalidOperationException(
                $"Unknown --volley arm '{value}' (use cast or salvo)."),
        };
    }

    /// <summary>
    /// Reads the registered side-objective arm
    /// (<c>docs/DESIGN-SIDE-OBJECTIVES-2026-07-30.md</c>). Omitting the
    /// option — or naming <c>none</c> — keeps today's map and contract and
    /// adds no ruleset suffix. <c>muster</c> adds the rally flag: a
    /// capturable site on the map's centre column whose owner's PRIME
    /// respawns land beside the fight, and which takes the unconditional
    /// forward rally away from both teams to pay for it.
    /// </summary>
    private static FrontlineLabsSideObjectiveArm OptionalSideObjective(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("side-objective", out string? value))
            return FrontlineLabsSideObjectiveArm.None;
        return value.ToLowerInvariant() switch
        {
            "none" => FrontlineLabsSideObjectiveArm.None,
            "muster" => FrontlineLabsSideObjectiveArm.Muster,
            _ => throw new InvalidOperationException(
                $"Unknown --side-objective arm '{value}' (use none or "
                + "muster)."),
        };
    }

    /// <summary>
    /// Reads the registered capture arm (DECISIONS #187,
    /// <c>docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md</c> parts 2–3). Omitting
    /// the option — or naming <c>frozen</c> — keeps today's capture and adds
    /// no ruleset suffix. <c>channel</c> makes taking ground a channel: only
    /// bodies that held their tile this tick add gain (denial still counts
    /// all of them), the multiplier is capped at 2, an opposing claim erodes
    /// at 8× build speed, damage to a controlling body ON the objective
    /// reverts the controller's work on that run, and the paired
    /// <c>channel-speed</c> factor moves the threshold from 15 to 8.
    /// </summary>
    private static FrontlineLabsCaptureArm OptionalCaptureArm(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("capture", out string? value))
            return FrontlineLabsCaptureArm.Frozen;
        return value.ToLowerInvariant() switch
        {
            "frozen" => FrontlineLabsCaptureArm.Frozen,
            "channel" => FrontlineLabsCaptureArm.Channel,
            _ => throw new InvalidOperationException(
                $"Unknown --capture arm '{value}' (use frozen or channel)."),
        };
    }

    /// <summary>
    /// Reads the registered battlefield-economy arm (DECISIONS #187,
    /// <c>docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md</c>). Omitting the option —
    /// or naming <c>none</c> — declares no economy and adds no ruleset
    /// suffix. <c>scrap</c> is the arm: scheduled deposits in both side
    /// lanes, a wreck at every death tile, carried-with-assay banking at your
    /// own home pad, and an <c>invest</c> verb that spends the team bank on
    /// edge, plate, or optic. <c>scrap-flat</c> is its pre-registered
    /// falsification control: the same economy with the bank buying greedily
    /// by itself and no verb at all.
    /// </summary>
    private static FrontlineLabsEconomyArm OptionalEconomyArm(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("economy", out string? value))
            return FrontlineLabsEconomyArm.None;
        return value.ToLowerInvariant() switch
        {
            "none" => FrontlineLabsEconomyArm.None,
            "scrap" => FrontlineLabsEconomyArm.Scrap,
            "scrap-flat" => FrontlineLabsEconomyArm.ScrapFlat,
            _ => throw new InvalidOperationException(
                $"Unknown --economy arm '{value}' (use none, scrap or "
                + "scrap-flat)."),
        };
    }

    /// <summary>
    /// Reads the registered horizon arm (the owner's post-wave-8 ruling,
    /// "longer games at this point is ok"). Omitting the option — or naming
    /// <c>standard</c> — keeps the measured 500-tick limit and adds no ruleset
    /// suffix. <c>long</c> declares 750, which is a contract LIMIT: it travels
    /// in the rules like every other pacing number.
    /// </summary>
    private static FrontlineLabsHorizonArm OptionalHorizonArm(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("horizon", out string? value))
            return FrontlineLabsHorizonArm.Standard;
        return value.ToLowerInvariant() switch
        {
            "standard" => FrontlineLabsHorizonArm.Standard,
            "long" => FrontlineLabsHorizonArm.Long,
            _ => throw new InvalidOperationException(
                $"Unknown --horizon arm '{value}' (use standard or long)."),
        };
    }

    /// <summary>
    /// Reads the registered roster arm (the owner's post-wave-8 ruling).
    /// Omitting the option — or naming <c>none</c> — keeps the measured
    /// prime-plus-two roster on its class cadences and adds no ruleset
    /// suffix. <c>legion</c> starts every team with three live bodies (the
    /// fabricator with a fourth slot it must fabricate), unlocks two more
    /// slots at tick 150 and three more at 300, and runs on its own map
    /// generation, because a slot that returns automatically needs a reserved
    /// spawn anchor and the measured pad has room for two.
    /// </summary>
    private static FrontlineLabsRosterArm OptionalRosterArm(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("roster", out string? value))
            return FrontlineLabsRosterArm.None;
        return value.ToLowerInvariant() switch
        {
            "none" => FrontlineLabsRosterArm.None,
            "legion" => FrontlineLabsRosterArm.Legion,
            _ => throw new InvalidOperationException(
                $"Unknown --roster arm '{value}' (use none or legion)."),
        };
    }

    /// <summary>
    /// Reads the registered aim arm (DECISIONS #173). Omitting the option —
    /// or naming <c>straight</c> — keeps today's facing-only launch and
    /// adds no ruleset suffix. <c>offset</c> restores the ±1-sector (45°)
    /// initial aim on every class's mobile gun.
    /// </summary>
    private static FrontlineLabsAimArm OptionalAimArm(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("aim", out string? value))
            return FrontlineLabsAimArm.Straight;
        return value.ToLowerInvariant() switch
        {
            "straight" => FrontlineLabsAimArm.Straight,
            "offset" => FrontlineLabsAimArm.Offset,
            _ => throw new InvalidOperationException(
                $"Unknown --aim arm '{value}' (use straight or offset)."),
        };
    }

    /// <summary>
    /// Reads the registered stance-ground arm (DECISIONS #171 tuning,
    /// round 3). Omitting the option — or naming <c>strict</c> — keeps
    /// today's placement rule and adds no ruleset suffix. <c>free</c> drops
    /// the forbidden tag kind from the VOLLEY and AEGIS SHELL entry routes
    /// only; turret anchors keep it.
    /// </summary>
    private static FrontlineLabsStanceGroundArm OptionalStanceGround(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("stance-ground", out string? value))
            return FrontlineLabsStanceGroundArm.Strict;
        return value.ToLowerInvariant() switch
        {
            "strict" => FrontlineLabsStanceGroundArm.Strict,
            "free" => FrontlineLabsStanceGroundArm.Free,
            "open" => FrontlineLabsStanceGroundArm.Open,
            _ => throw new InvalidOperationException(
                $"Unknown --stance-ground arm '{value}' (use strict, free, "
                + "or open)."),
        };
    }

    /// <summary>
    /// Reads the registered FIVE SLOTS tuning variant (DECISIONS #171).
    /// Omitting the option — or naming <c>full</c> explicitly — selects the
    /// phase-2 measured arm and adds no ruleset suffix. The variant tunes
    /// one skill, so it is only legal in a cell that carries FIVE SLOTS.
    /// </summary>
    private static FrontlineLabsFiveSlotVariant OptionalFiveSlotVariant(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("five-slots", out string? value))
            return FrontlineLabsFiveSlotVariant.Full;
        return value.ToLowerInvariant() switch
        {
            "full" => FrontlineLabsFiveSlotVariant.Full,
            "trim" => FrontlineLabsFiveSlotVariant.Trim,
            "boom" => FrontlineLabsFiveSlotVariant.Boom,
            "drag" => FrontlineLabsFiveSlotVariant.Drag,
            "moor" => FrontlineLabsFiveSlotVariant.Moor,
            "wane" => FrontlineLabsFiveSlotVariant.Wane,
            _ => throw new InvalidOperationException(
                $"Unknown --five-slots variant '{value}' (use full, trim, "
                + "boom, drag, moor, or wane)."),
        };
    }

    /// <summary>
    /// Reads the pre-registered class-skill kit
    /// (<c>docs/DESIGN-MECHANISM-SLATE-2026-07-29.md</c>). Omitting the option
    /// — or naming <c>none</c> explicitly — selects today's classes and adds
    /// no ruleset suffix. <c>kit</c> requests all three; because each skill is
    /// owned by exactly one class and a cell holds at most two, the resolved
    /// arm carries only the skills whose owning class is present.
    /// </summary>
    private static FrontlineLabsSkillKit OptionalSkillKit(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("skills", out string? value))
            return FrontlineLabsSkillKit.None;

        string[] tokens = value
            .ToLowerInvariant()
            .Split(',', StringSplitOptions.TrimEntries
                | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0
            || tokens.Distinct(StringComparer.Ordinal).Count()
                != tokens.Length)
        {
            throw new InvalidOperationException(
                "--skills takes one or more distinct skill tokens.");
        }
        if (tokens.Contains("none", StringComparer.Ordinal))
        {
            if (tokens.Length != 1)
            {
                throw new InvalidOperationException(
                    "--skills none is the measured baseline and cannot be "
                    + "combined with a skill.");
            }
            return FrontlineLabsSkillKit.None;
        }

        FrontlineLabsSkillKit kit = FrontlineLabsSkillKit.None;
        foreach (string token in tokens)
        {
            kit |= token switch
            {
                "kit" => FrontlineLabsSkillKit.StrikerVolley
                    | FrontlineLabsSkillKit.BulwarkAegisShell
                    | FrontlineLabsSkillKit.FabricatorFiveSlots,
                "volley" => FrontlineLabsSkillKit.StrikerVolley,
                "shell" => FrontlineLabsSkillKit.BulwarkAegisShell,
                "five-slots" => FrontlineLabsSkillKit.FabricatorFiveSlots,
                _ => throw new InvalidOperationException(
                    $"Unknown --skills token '{token}' (use none, kit, "
                    + "volley, shell, or five-slots)."),
            };
        }
        return kit;
    }

    /// <summary>
    /// Reads the pre-registered pendulum level (DECISIONS #158/#166). Omitting
    /// the option — or naming <c>control</c> explicitly — selects today's
    /// measured baseline and adds no ruleset suffix. <c>ratchet</c>,
    /// <c>ratchet-contest</c>, <c>keel</c> (every counterweight at once) and
    /// <c>hull</c> (the keel without its forward rally, so every arrival walks
    /// home) are the registered composite levels; the four single-factor
    /// tokens may
    /// also be combined with commas for an ablation, and a comma spelling that
    /// lands on a registered combination is that same ruleset.
    /// </summary>
    private static FrontlineLabsPendulumArm OptionalPendulumArm(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("pendulum", out string? value))
            return FrontlineLabsPendulumArm.None;

        string[] tokens = value
            .ToLowerInvariant()
            .Split(',', StringSplitOptions.TrimEntries
                | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0
            || tokens.Distinct(StringComparer.Ordinal).Count()
                != tokens.Length)
        {
            throw new InvalidOperationException(
                "--pendulum takes one or more distinct arm tokens.");
        }
        if (tokens.Contains("control", StringComparer.Ordinal))
        {
            if (tokens.Length != 1)
            {
                throw new InvalidOperationException(
                    "--pendulum control is the measured baseline and cannot "
                    + "be combined with another arm.");
            }
            return FrontlineLabsPendulumArm.None;
        }

        FrontlineLabsPendulumArm arm = FrontlineLabsPendulumArm.None;
        foreach (string token in tokens)
        {
            FrontlineLabsPendulumArm selected = token switch
            {
                "ratchet" => FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally,
                "ratchet-contest" =>
                    FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally
                    | FrontlineLabsPendulumArm.ContestMajority,
                "keel" =>
                    FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally
                    | FrontlineLabsPendulumArm.ContestMajority
                    | FrontlineLabsPendulumArm.EnemySoleDecay,
                // The keel without its forward rally (owner ruling): every
                // automatic arrival lands on its reserved home spawn, so the
                // fabricator's field-placed children are the only forward
                // body delivery left in the game.
                "hull" =>
                    FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ContestMajority
                    | FrontlineLabsPendulumArm.EnemySoleDecay,
                "sticky-frontline" =>
                    FrontlineLabsPendulumArm.StickyFrontline,
                "forward-rally" => FrontlineLabsPendulumArm.ForwardRally,
                "contest-majority" =>
                    FrontlineLabsPendulumArm.ContestMajority,
                "enemy-sole-decay" =>
                    FrontlineLabsPendulumArm.EnemySoleDecay,
                _ => throw new InvalidOperationException(
                    $"Unknown --pendulum arm '{token}' (use control, "
                    + "ratchet, ratchet-contest, keel, hull, "
                    + "sticky-frontline, forward-rally, contest-majority, or "
                    + "enemy-sole-decay)."),
            };
            arm |= selected;
        }
        return arm;
    }

    /// <summary>Reads a project spec's declared class from botarena.json.
    /// Raw WASM artifacts declare no class; the Lab's entrant metadata covers
    /// them.</summary>
    private static FrontlineLabsClassDefinition? DeclaredClass(string spec)
    {
        if (!Directory.Exists(spec) || !BotProject.LooksLikeProject(spec))
            return null;
        string? declared = BotProject.Load(spec).Manifest.Class;
        if (declared is null)
            return null;
        try
        {
            return FrontlineLabsClassDefinition.Parse(declared);
        }
        catch (ArgumentException error)
        {
            throw new InvalidOperationException(
                $"{spec}: {error.Message}");
        }
    }

    private static (FrontlineLabsClassDefinition TeamZero,
        FrontlineLabsClassDefinition TeamOne)? OptionalClassPair(
        IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("classes", out string? value))
            return null;

        string[] parts = value.Split("-vs-");
        if (parts.Length != 2
            || parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                "Use --classes <class>-vs-<class>, for example "
                + "--classes bulwark-vs-striker.");
        }

        FrontlineLabsClassDefinition teamZero;
        FrontlineLabsClassDefinition teamOne;
        try
        {
            teamZero = FrontlineLabsClassDefinition.Parse(parts[0]);
            teamOne = FrontlineLabsClassDefinition.Parse(parts[1]);
        }
        catch (ArgumentException error)
        {
            throw new InvalidOperationException(error.Message);
        }

        if (string.CompareOrdinal(teamZero.Id, teamOne.Id) > 0)
        {
            throw new InvalidOperationException(
                $"Class pairs are canonical: use --classes "
                + $"{teamOne.Id}-vs-{teamZero.Id} and swap bot assignments "
                + "with --swap instead of swapping teams.");
        }

        return (teamZero, teamOne);
    }

    private static ulong[] ParseSeeds(
        IReadOnlyDictionary<string, string> options)
    {
        string raw = options.GetValueOrDefault(
            options.ContainsKey("seeds") ? "seeds" : "seed",
            "42");
        string[] values = raw.Split(
            ',',
            StringSplitOptions.TrimEntries
            | StringSplitOptions.RemoveEmptyEntries);
        if (values.Length == 0)
            throw new InvalidOperationException("At least one seed is required.");

        var seeds = new ulong[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            if (!ulong.TryParse(
                    values[index],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out seeds[index]))
            {
                throw new InvalidOperationException(
                    $"Invalid unsigned 64-bit seed '{values[index]}'.");
            }
        }
        if (seeds.Distinct().Count() != seeds.Length)
        {
            throw new InvalidOperationException(
                "--seeds values must be distinct so replay paths do not collide.");
        }
        return seeds;
    }

    private static string OutputDirectory(
        string? overrideDirectory,
        string bot0,
        string bot1,
        ulong seed,
        bool batch)
    {
        if (overrideDirectory is not null)
        {
            return batch
                ? Path.Combine(overrideDirectory, $"s{seed}")
                : overrideDirectory;
        }
        return Path.Combine(
            "out",
            "frontline-labs",
            $"{Slug(bot0)}-vs-{Slug(bot1)}-s{seed}");
    }

    private static void PrintResult(
        GenericActorMatchResult result,
        string replayHash,
        string bot0,
        string bot1,
        ulong seed)
    {
        string verdict = result.WinnerTeamId switch
        {
            0 => $"{bot0} (team 0) wins",
            1 => $"{bot1} (team 1) wins",
            _ => "draw",
        };
        Console.WriteLine($"Seed:    {seed}");
        Console.WriteLine($"Match:   {bot0} vs {bot1}");
        Console.WriteLine(
            $"Result:  {verdict} — {Reason(result)} " +
            $"at tick {result.EndTick?.ToString(
                CultureInfo.InvariantCulture) ?? "pre-tick"}");
        foreach (TeamStanding standing in result.Standings.Standings
                     .OrderBy(standing => standing.TeamId))
        {
            string name = standing.TeamId == 0 ? bot0 : bot1;
            string scores = string.Join(
                ", ",
                standing.Scores.Select(score =>
                    $"{score.Channel}={score.Value}"));
            Console.WriteLine(
                $"  team {standing.TeamId} {name,-22} " +
                $"{standing.Outcome,-5} rank {standing.Rank} {scores}");
        }
        Console.WriteLine(
            $"Ticks:   {(result.EndTick ?? -1) + 1}");
        Console.WriteLine($"Hash:    {replayHash}");
        Console.WriteLine();
    }

    private static string Reason(GenericActorMatchResult result) =>
        result.Mode is GenericActorMatchModeResult.Frontline frontline
            ? frontline.Reason switch
            {
                GenericFrontlineEndReason.BaseBreach => "base-breach",
                GenericFrontlineEndReason.MaxTicks => "max-ticks",
                GenericFrontlineEndReason.FaultEligibility =>
                    "fault-eligibility",
                _ => frontline.Reason.ToString(),
            }
            : result.CompletionReason;

    private static string Slug(string value)
    {
        string slug = new(
            value.ToLowerInvariant()
                .Select(character =>
                    char.IsAsciiLetterOrDigit(character)
                        ? character
                        : '-')
                .ToArray());
        slug = slug.Trim('-');
        return slug.Length == 0 ? "bot" : slug;
    }

    private static void TryOpen(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(
                    new ProcessStartInfo(path)
                    {
                        UseShellExecute = true,
                    });
            }
            else
            {
                Process.Start(
                    OperatingSystem.IsMacOS() ? "open" : "xdg-open",
                    path);
            }
        }
        catch (Exception exception)
            when (exception is InvalidOperationException
                  or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine(
                $"Could not open the viewer automatically: " +
                $"{exception.Message}");
        }
    }
}
