using System.Collections.Immutable;
using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// The adoption-grade mechanism both stances share: one threshold-triggered
/// automatic return transition. VOLLEY spends a fires-count of one, so a
/// parked striker firing repeated fans — artillery, the bulwark's fantasy in
/// the striker's kit — is impossible by rule rather than by driver etiquette.
/// AEGIS SHELL spends a deflections-count of three, so indefinite deflection
/// stops being an off-switch for ranged combat. Both are owner rulings from
/// <c>docs/DESIGN-MECHANISM-SLATE-2026-07-29.md</c>.
///
/// These tests drive the real arms rather than a synthetic contract wherever
/// the arm can show the fact, because the arm's own windups and thresholds are
/// the pre-registered values under test.
/// </summary>
public sealed class FrontlineLabsAutomaticReturnTests
{
    [Fact]
    public void TheVolleyReturnsOnTheExactTickItsFanLaunches()
    {
        GenericActorMatchChronology chronology = VolleyCast();
        GenericActorMatchTickFrame fan = FirstFan(chronology);

        GenericActorRuntimeObservation.EventPayload.FormTransition start =
            Assert.Single(
                Starts(fan),
                FrontlineLabsSkillArmTestFixture.IsAutomatic);
        Assert.Equal(
            $"unstance-{FrontlineLabsClassDefinition.Striker.Id}-prime",
            start.TransitionId);
        Assert.Equal(
            FrontlineLabsClassDefinition.Striker.PrimeStanceFormId,
            start.FromFormId);
        Assert.Equal(
            FrontlineLabsClassDefinition.Striker.PrimeFormId,
            start.ToFormId);
        Assert.Equal(fan.Tick, start.StartedTick);
        // The automatic return spends the same exit windup the manual early
        // exit spends: one tick, which on this completion clock is due on the
        // tick it started.
        Assert.Equal(
            FrontlineLabsClassDefinition.Striker.StanceExitWindupTicks,
            start.DueTick - start.StartedTick + 1);
        Assert.Contains(
            Completions(fan),
            item => FrontlineLabsSkillArmTestFixture.IsAutomatic(item)
                && item.ActorId == start.ActorId);
        // And the fan is genuinely gone by the end of the tick it fired on.
        Assert.Equal(
            FrontlineLabsClassDefinition.Striker.PrimeFormId,
            fan.PostState.ActiveLives
                .Single(life => life.ActorId == start.ActorId)
                .FormId);
    }

    /// <summary>
    /// The rule the mechanism exists for: one entry buys exactly one fan.
    /// Every later fan by the same life must be preceded by a fresh entry, so
    /// the squatting doctrine has no contract to squat in.
    /// </summary>
    [Fact]
    public void NoStanceEntryEverProducesTwoFans()
    {
        GenericActorMatchChronology chronology = VolleyCast();
        string stance = FrontlineLabsClassDefinition.Striker
            .PrimeStanceFormId;
        int fansSinceEntry = 0;
        int entries = 0;

        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            if (Completions(frame).Any(item => string.Equals(
                    item.ToFormId,
                    stance,
                    StringComparison.Ordinal)))
            {
                entries++;
                fansSinceEntry = 0;
            }
            if (FrontlineLabsSkillArmTestFixture.Attacks(frame).Length > 0)
                fansSinceEntry++;

            Assert.True(
                fansSinceEntry <= FrontlineLabsDefinition.VolleyCastBudget,
                $"tick {frame.Tick} fired {fansSinceEntry} fans from one entry");
        }

        // The probe re-enters, so the cycle is really a cycle rather than a
        // one-shot the driver simply never repeated.
        Assert.True(entries > 1, $"only {entries} stance entries observed");
    }

    [Fact]
    public void TheThirdDeflectionShattersTheShellIntoItsReturn()
    {
        GenericActorMatchChronology chronology = ShieldBreak();
        (GenericActorMatchTickFrame Frame, int Running)[] deflections =
            RunningDeflections(chronology);
        Assert.True(
            deflections.Length >= FrontlineLabsDefinition.ShieldBreakBudget,
            $"probe only produced {deflections.Length} deflections");

        // Nothing leaves before the budget is spent: the shell holds through
        // its first two returns.
        foreach ((GenericActorMatchTickFrame frame, int running) in
                 deflections.Where(entry =>
                     entry.Running < FrontlineLabsDefinition
                         .ShieldBreakBudget))
        {
            Assert.DoesNotContain(
                Starts(frame),
                FrontlineLabsSkillArmTestFixture.IsAutomatic);
            Assert.True(running < FrontlineLabsDefinition.ShieldBreakBudget);
        }

        GenericActorMatchTickFrame shatter = deflections
            .First(entry =>
                entry.Running >= FrontlineLabsDefinition.ShieldBreakBudget)
            .Frame;
        GenericActorRuntimeObservation.EventPayload.FormTransition start =
            Assert.Single(
                Starts(shatter),
                FrontlineLabsSkillArmTestFixture.IsAutomatic);
        Assert.Equal(
            $"unstance-{FrontlineLabsClassDefinition.Bulwark.Id}-prime",
            start.TransitionId);
        Assert.Equal(
            FrontlineLabsClassDefinition.Bulwark.PrimeStanceFormId,
            start.FromFormId);
        // The punish window opens the moment the shield goes: the guard is
        // gone from the post-state of the very tick that broke it.
        Assert.Equal(
            FrontlineLabsClassDefinition.Bulwark.PrimeFormId,
            shatter.PostState.ActiveLives
                .Single(life => life.ActorId == start.ActorId)
                .FormId);
    }

    /// <summary>
    /// Leaving early stays the author's decision. The same route serves it,
    /// and the cause it reports is the requested one — which is exactly why
    /// the cause had to become a fact rather than be derived from the route.
    /// </summary>
    [Fact]
    public void AManualExitBeforeTheThresholdIsLegalAndReportsItsOwnCause()
    {
        string stance = FrontlineLabsClassDefinition.Striker
            .PrimeStanceFormId;
        var target = new Position(
            3,
            FrontlineLabsSkillArmTestFixture.StanceRowY);
        GenericActorMatchChronology chronology =
            FrontlineLabsSkillArmTestFixture.Run(
                VolleyArm(),
                (_, observation) =>
                {
                    if (observation.Self.ActorId.TeamId != 0
                        || observation.Self.ActorId.UnitId != 0)
                    {
                        return GenericDeathmatchSessionTestFixture.Wait();
                    }
                    // Enter, then leave without ever firing.
                    if (observation.Self.FormId == stance)
                        return FrontlineLabsSkillArmTestFixture.Mobilize();
                    return FrontlineLabsSkillArmTestFixture.WalkTo(
                            observation,
                            target)
                        ?? (FrontlineLabsSkillArmTestFixture.Allows(
                                observation,
                                "transform")
                            ? GenericDeathmatchSessionTestFixture.Transform(
                                stance)
                            : GenericDeathmatchSessionTestFixture.Wait());
                });

        GenericActorRuntimeObservation.EventPayload.FormTransition[] exits =
        [
            .. chronology.Ticks
                .SelectMany(frame => Starts(frame))
                .Where(item => string.Equals(
                    item.FromFormId,
                    stance,
                    StringComparison.Ordinal)),
        ];
        Assert.NotEmpty(exits);
        Assert.All(
            exits,
            item => Assert.False(
                FrontlineLabsSkillArmTestFixture.IsAutomatic(item)));
        // No fan was ever fired, so no threshold was ever reached.
        Assert.All(
            chronology.Ticks,
            frame => Assert.Empty(
                FrontlineLabsSkillArmTestFixture.Attacks(frame)));
    }

    /// <summary>
    /// The automatic cause is not a privileged one. Its windup obeys the same
    /// lethal-damage policy every requested windup obeys, and the cancellation
    /// still reports the cause of the start it consumes. The arm's own exit
    /// windup is one tick, so this is shown on a synthetic contract whose
    /// return takes long enough to be interrupted — the primitive under test,
    /// not the arm's tuning.
    /// </summary>
    [Fact]
    public void LethalDamageCancelsAnAutomaticReturnMidWindup()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.DefinitionWithSameLifeTransition(
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 16,
                    MaxHealth = 1,
                },
                new GenericDeathmatchSessionTestFixture.SameLifeOptions
                {
                    DurationTicks = 1,
                    IncludeReverseRoute = true,
                    IrreversibleForLife = false,
                    TargetHasAttack = true,
                    TargetMaxHealth = 1,
                    HealthPolicy = ActorSameLifeHealthDefinition
                        .HealthPolicyKind
                        .PreserveCurrentCappedToTargetMaximum,
                    FlatHealthGain = 0,
                    ReverseDurationTicks = 6,
                    ReverseAutomaticReturn = new(
                        ActorAutomaticReturnTriggerDefinition
                            .AutomaticReturnCounterKind
                            .AttacksIssuedSinceEnteringSourceForm,
                        1),
                });

        GenericActorMatchChronology chronology = Run(
            definition,
            (_, observation) =>
                observation.Self.FormId == "anchored"
                    ? GenericDeathmatchSessionTestFixture.Shoot()
                    : FrontlineLabsSkillArmTestFixture.Allows(
                        observation,
                        "transform")
                        ? GenericDeathmatchSessionTestFixture.Transform(
                            "anchored")
                        : GenericDeathmatchSessionTestFixture.Shoot());

        GenericActorRuntimeObservation.EventPayload.FormTransition start =
            chronology.Ticks
                .SelectMany(frame => Starts(frame))
                .First(FrontlineLabsSkillArmTestFixture.IsAutomatic);
        Assert.True(
            start.DueTick > start.StartedTick,
            "the synthetic return must span a windup to be cancellable");

        GenericActorRuntimeObservation.EventPayload.FormTransition[]
            cancellations =
            [
                .. chronology.Ticks
                    .SelectMany(frame =>
                        FrontlineLabsSkillArmTestFixture.Transitions(
                            frame,
                            GenericActorRuntimeObservation.EventKind
                                .FormTransitionCancelled))
                    .Where(item => item.ActorId == start.ActorId
                        && item.StartedTick == start.StartedTick),
            ];
        GenericActorRuntimeObservation.EventPayload.FormTransition cancelled =
            Assert.Single(cancellations);
        // One instance, one cause: the cancellation of an automatic return
        // says so, which is what lets the chronology refuse a mislabelled one.
        Assert.True(
            FrontlineLabsSkillArmTestFixture.IsAutomatic(cancelled));
    }

    /// <summary>
    /// The counter lives on the life and restarts with the form, so neither a
    /// respawn nor a second entry within one life inherits a spent budget —
    /// the failure mode being guarded against is a re-entered stance that
    /// returns on arrival because the previous cycle's count survived.
    /// </summary>
    [Fact]
    public void TheCounterNeverSurvivesAnEntryOrALife()
    {
        GenericActorMatchChronology chronology = VolleyCast();
        string stance = FrontlineLabsClassDefinition.Striker
            .PrimeStanceFormId;
        var entered = new HashSet<ActorIdentity>();

        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            foreach (GenericActorRuntimeObservation.EventPayload.FormTransition
                     item in Completions(frame))
            {
                if (string.Equals(
                        item.ToFormId,
                        stance,
                        StringComparison.Ordinal))
                {
                    entered.Add(item.ActorId);
                }
            }
            // An entry that immediately returned would show as an automatic
            // start on the same tick the entry completed, with no fan.
            foreach (GenericActorRuntimeObservation.EventPayload.FormTransition
                     item in Starts(frame)
                         .Where(FrontlineLabsSkillArmTestFixture.IsAutomatic))
            {
                Assert.Contains(
                    FrontlineLabsSkillArmTestFixture.Attacks(frame),
                    attack => attack.ActorId == item.ActorId);
            }
        }

        Assert.NotEmpty(entered);
    }

    /// <summary>The contract states the rule, so opponents can read it.</summary>
    [Fact]
    public void BothStancesDeclareTheirBudgetAsCanonicalContractData()
    {
        ActorResolvedMatchDefinition arm =
            FrontlineLabsSkillArmTestFixture.Arm(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsSkillKit.StrikerVolley
                    | FrontlineLabsSkillKit.BulwarkAegisShell);
        ActorFormTransitionDefinition[] automatic =
        [
            .. arm.Rules.SameLifeTransitions
                .OfType<ActorFormTransitionDefinition>()
                .Where(route => route.AutomaticReturn is not null)
                .OrderBy(route => route.TransitionId, StringComparer.Ordinal),
        ];

        // Prime and child of each stance-bearing class, and nothing else.
        Assert.Equal(
            [
                "unstance-bulwark-child",
                "unstance-bulwark-prime",
                "unstance-striker-child",
                "unstance-striker-prime",
            ],
            automatic.Select(route => route.TransitionId));
        foreach (ActorFormTransitionDefinition route in automatic)
        {
            bool volley = route.TransitionId.Contains(
                "striker",
                StringComparison.Ordinal);
            Assert.Equal(
                volley
                    ? ActorAutomaticReturnTriggerDefinition
                        .AutomaticReturnCounterKind
                        .AttacksIssuedSinceEnteringSourceForm
                    : ActorAutomaticReturnTriggerDefinition
                        .AutomaticReturnCounterKind
                        .ProjectilesDeflectedSinceEnteringSourceForm,
                route.AutomaticReturn!.Counter);
            Assert.Equal(
                volley
                    ? FrontlineLabsDefinition.VolleyCastBudget
                    : FrontlineLabsDefinition.ShieldBreakBudget,
                route.AutomaticReturn.Threshold);
        }

        // Anchor is untouched: it was never a budgeted stance.
        Assert.All(
            arm.Rules.SameLifeTransitions
                .OfType<ActorFormTransitionDefinition>()
                .Where(route => route.TransitionId.StartsWith(
                    "mobilize",
                    StringComparison.Ordinal)),
            route => Assert.Null(route.AutomaticReturn));
    }

    /// <summary>
    /// A trigger that names a fact its source form can never produce would be
    /// a stance nobody could leave, so the contract validator refuses it
    /// rather than shipping a self-brick.
    /// </summary>
    [Fact]
    public void AnUncountableTriggerIsRefusedAtContractConstruction()
    {
        ActorRulesValidationException error =
            Assert.Throws<ActorRulesValidationException>(() =>
                GenericDeathmatchSessionTestFixture.DefinitionWithSameLifeTransition(
                    transitionOptions:
                    new GenericDeathmatchSessionTestFixture.SameLifeOptions
                    {
                        IncludeReverseRoute = true,
                        IrreversibleForLife = false,
                        // "anchored" declares no projectile guard, so it can
                        // never deflect anything.
                        ReverseAutomaticReturn = new(
                            ActorAutomaticReturnTriggerDefinition
                                .AutomaticReturnCounterKind
                                .ProjectilesDeflectedSinceEnteringSourceForm,
                            3),
                    }));

        Assert.Contains(
            "could never fire",
            error.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The canonical half of the mirror set: the trigger is written only where
    /// it exists, both mirrors read it back, and an explicitly inert encoding
    /// of "no trigger" is refused as a second spelling of the same contract
    /// (DECISIONS #156's discipline, the third field to follow it).
    /// </summary>
    [Fact]
    public void TheTriggerIsWrittenOnlyWhereItExistsAndRoundTripsExactly()
    {
        ActorResolvedMatchDefinition arm =
            FrontlineLabsSkillArmTestFixture.Arm(
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsSkillKit.StrikerVolley);
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsClassDefinition.Striker);
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(arm);
        string plain =
            ActorContractManifestSerializer.ToCanonicalJson(baseline);

        // (The lifecycle profile's unrelated automaticReturnFormId is why this
        // matches the property with its object, not the bare name.)
        Assert.DoesNotContain(
            "\"automaticReturn\":{",
            plain,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"automaticReturn\":{\"counter\":"
            + "\"attacks-issued-since-entering-source-form\","
            + "\"threshold\":1}",
            canonical,
            StringComparison.Ordinal);

        GenericActorCanonicalContractValidation validation =
            GenericActorCanonicalContractValidator.Validate(canonical);
        Assert.Equal(arm.Rules.RulesetId, validation.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(arm),
            validation.MatchContractFingerprint);

        // A zero threshold is the second encoding of "the engine never fires
        // this route", and the reader refuses it.
        string inert = canonical.Replace(
            "\"threshold\":1}",
            "\"threshold\":0}",
            StringComparison.Ordinal);
        Assert.NotEqual(canonical, inert);
        Assert.Contains(
            "automaticReturn",
            Assert.Throws<FormatException>(() =>
                    GenericActorCanonicalContractValidator.Validate(inert))
                .Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AZeroThresholdIsNotASecondEncodingOfNoTrigger() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActorAutomaticReturnTriggerDefinition(
                ActorAutomaticReturnTriggerDefinition
                    .AutomaticReturnCounterKind
                    .AttacksIssuedSinceEnteringSourceForm,
                0));

    private static ActorResolvedMatchDefinition VolleyArm() =>
        FrontlineLabsSkillArmTestFixture.Arm(
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsSkillKit.StrikerVolley);

    /// <summary>
    /// Team 0's prime walks to the stance row and then simply alternates
    /// "enter if mobile, fire if stanced" forever. With the cast rule in
    /// place that IS the whole doctrine — there is no exit to author.
    /// </summary>
    private static GenericActorMatchChronology VolleyCast()
    {
        string stance = FrontlineLabsClassDefinition.Striker
            .PrimeStanceFormId;
        var target = new Position(
            3,
            FrontlineLabsSkillArmTestFixture.StanceRowY);
        return FrontlineLabsSkillArmTestFixture.Run(
            VolleyArm(),
            (_, observation) =>
            {
                if (observation.Self.ActorId.TeamId != 0
                    || observation.Self.ActorId.UnitId != 0)
                {
                    return GenericDeathmatchSessionTestFixture.Wait();
                }
                if (observation.Self.FormId == stance)
                {
                    return FrontlineLabsSkillArmTestFixture.Allows(
                        observation,
                        "shoot-straight")
                        ? FrontlineLabsSkillArmTestFixture.ShootStraight()
                        : GenericDeathmatchSessionTestFixture.Wait();
                }
                return FrontlineLabsSkillArmTestFixture.WalkTo(
                        observation,
                        target)
                    ?? (FrontlineLabsSkillArmTestFixture.Allows(
                            observation,
                            "transform")
                        ? GenericDeathmatchSessionTestFixture.Transform(stance)
                        : GenericDeathmatchSessionTestFixture.Wait());
            });
    }

    /// <summary>
    /// Team 0 pokes a shell head-on from range; team 1 raises the shield and
    /// holds it. Feeding the arc is the only way the shooter buys the window,
    /// which is the counter-play the ruling intends.
    /// </summary>
    private static GenericActorMatchChronology ShieldBreak()
    {
        string shell = FrontlineLabsClassDefinition.Bulwark
            .PrimeStanceFormId;
        var shooterTile = new Position(9, 13);
        var shellTile = new Position(12, 13);
        return FrontlineLabsSkillArmTestFixture.Run(
            FrontlineLabsSkillArmTestFixture.Arm(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsSkillKit.BulwarkAegisShell),
            (_, observation) =>
            {
                if (observation.Self.ActorId.UnitId != 0)
                    return GenericDeathmatchSessionTestFixture.Wait();
                if (observation.Self.ActorId.TeamId == 0)
                {
                    GenericActorRuntimeDecision? walk =
                        FrontlineLabsSkillArmTestFixture.WalkTo(
                            observation,
                            shooterTile);
                    if (walk is not null)
                        return walk;
                    if (observation.Self.Facing != Direction.East)
                    {
                        return GenericDeathmatchSessionTestFixture.Rotate(
                            Direction.East);
                    }
                    return FrontlineLabsSkillArmTestFixture.Allows(
                        observation,
                        "shoot-straight")
                        ? FrontlineLabsSkillArmTestFixture.ShootStraight()
                        : GenericDeathmatchSessionTestFixture.Wait();
                }
                GenericActorRuntimeDecision? approach =
                    FrontlineLabsSkillArmTestFixture.WalkTo(
                        observation,
                        shellTile);
                if (approach is not null)
                    return approach;
                if (observation.Self.FormId == shell)
                    return GenericDeathmatchSessionTestFixture.Wait();
                return FrontlineLabsSkillArmTestFixture.Allows(
                    observation,
                    "transform")
                    ? GenericDeathmatchSessionTestFixture.Transform(shell)
                    : GenericDeathmatchSessionTestFixture.Wait();
            });
    }

    /// <summary>
    /// Every frame carrying a deflection, with the running per-life count the
    /// engine is spending — the exact quantity the threshold is compared to.
    /// </summary>
    private static (GenericActorMatchTickFrame Frame, int Running)[]
        RunningDeflections(GenericActorMatchChronology chronology)
    {
        var running = new Dictionary<ActorIdentity, int>();
        var frames = new List<(GenericActorMatchTickFrame, int)>();
        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            ImmutableArray<
                GenericActorRuntimeObservation.EventPayload
                    .ProjectileDeflected> deflections =
                FrontlineLabsSkillArmTestFixture.Deflections(frame);
            if (deflections.IsEmpty)
                continue;
            int highest = 0;
            foreach (GenericActorRuntimeObservation.EventPayload
                     .ProjectileDeflected item in deflections)
            {
                running.TryGetValue(item.TargetActorId, out int count);
                running[item.TargetActorId] = count + 1;
                highest = Math.Max(highest, count + 1);
            }
            frames.Add((frame, highest));
            foreach (GenericActorRuntimeObservation.EventPayload.FormTransition
                     completed in Completions(frame))
            {
                running.Remove(completed.ActorId);
            }
        }
        return [.. frames];
    }

    private static GenericActorMatchChronology Run(
        ActorResolvedMatchDefinition definition,
        Func<
            GenericActorRuntimeStart,
            GenericActorRuntimeObservation,
            GenericActorRuntimeDecision> decide)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition, decide);
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            FrontlineLabsSkillArmTestFixture.Seed);
        session.Run();
        return session.Chronology;
    }

    private static ImmutableArray<
            GenericActorRuntimeObservation.EventPayload.FormTransition>
        Starts(GenericActorMatchTickFrame frame) =>
        FrontlineLabsSkillArmTestFixture.Transitions(
            frame,
            GenericActorRuntimeObservation.EventKind.FormTransitionStarted);

    private static ImmutableArray<
            GenericActorRuntimeObservation.EventPayload.FormTransition>
        Completions(GenericActorMatchTickFrame frame) =>
        FrontlineLabsSkillArmTestFixture.Transitions(
            frame,
            GenericActorRuntimeObservation.EventKind.FormTransitionCompleted);

    private static GenericActorMatchTickFrame FirstFan(
        GenericActorMatchChronology chronology) =>
        chronology.Ticks.First(frame =>
            FrontlineLabsSkillArmTestFixture.Attacks(frame).Length == 3);
}
