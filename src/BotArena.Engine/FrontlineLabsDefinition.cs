using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One production-shaped generation-3 Frontline Labs contract. The values are
/// an experimental mechanics arm, not a balance or ranked-play verdict. It is
/// deliberately a new rules/map generation and does not reinterpret or mutate
/// the frozen <c>frontline-alpha-1</c> contract.
/// </summary>
public static class FrontlineLabsDefinition
{
    public const string PlaylistKey = "frontline-labs";
    public const string RulesetId = "frontline-labs-1";
    public const string MapId = "frontline-labs-01";

    /// <summary>
    /// The MUSTER arm's own map generation. A side objective is never an
    /// edit to an existing map: the shipped map goldens stay byte-exact and
    /// this second generation carries the widened centre-column alcoves and
    /// the two mirror-symmetric site regions.
    /// </summary>
    public const string MusterMapId = "frontline-labs-02-muster";
    public const string MatchFormatId =
        HeadToHeadMatchFormatDefinition.Id;
    public const string TopologyProfileId =
        "two-team-one-controller-three-slots-v1";

    /// <summary>
    /// The five-slot arm's topology profile. A fabricator-controlled team
    /// fields five stable unit slots against the opposing class's three, so
    /// the cell is deliberately NOT the same topology on both sides — an
    /// owner-approved amendment of DECISIONS #153's same-topology reading,
    /// registered as the good asymmetry in
    /// <c>docs/DESIGN-MECHANISM-SLATE-2026-07-29.md</c> (the barricade's
    /// per-class wall slot was the bad one). Slot counts are contract data,
    /// so consumers resolve slots by their explicit IDs and nothing reads a
    /// count of three.
    /// </summary>
    public const string AsymmetricSlotsTopologyProfileId =
        "two-team-one-controller-asymmetric-slots-5-3-v1";

    /// <summary>
    /// The five-slot arm's OTHER shape: a fabricator mirror, where both teams
    /// field five. It is symmetric and therefore not the same pre-registration
    /// as the 5-vs-3 cell, even though the same flag produces it — a profile
    /// ID names a topology, not an arm flag.
    /// </summary>
    public const string FiveSlotMirrorTopologyProfileId =
        "two-team-one-controller-five-slots-v1";

    /// <summary>
    /// The Trim tuning variant's shapes (DECISIONS #171): a four-slot
    /// fabricator against a three-slot class, and the four-slot mirror. A
    /// profile ID names a topology, so the trimmed arm mints its own two
    /// rather than reusing the five-slot pair it tunes.
    /// </summary>
    public const string TrimAsymmetricSlotsTopologyProfileId =
        "two-team-one-controller-asymmetric-slots-4-3-v1";

    /// <inheritdoc cref="TrimAsymmetricSlotsTopologyProfileId"/>
    public const string TrimMirrorTopologyProfileId =
        "two-team-one-controller-four-slots-v1";

    /// <summary>
    /// The LEGION roster's three shapes. Every class fields eight stable unit
    /// slots under that arm except the fabricator, which fields nine — so the
    /// arm produces an eight-slot mirror, a nine-versus-eight cell, and a
    /// nine-slot fabricator mirror. A profile ID names a topology rather than
    /// an arm flag, so all three are registered separately: an unregistered
    /// shape faults rather than borrowing a neighbour's pre-registration.
    /// </summary>
    public const string LegionMirrorTopologyProfileId =
        "two-team-one-controller-eight-slots-legion-v1";

    /// <inheritdoc cref="LegionMirrorTopologyProfileId"/>
    public const string LegionAsymmetricSlotsTopologyProfileId =
        "two-team-one-controller-asymmetric-slots-9-8-legion-v1";

    /// <inheritdoc cref="LegionMirrorTopologyProfileId"/>
    public const string LegionFabricatorMirrorTopologyProfileId =
        "two-team-one-controller-nine-slots-legion-v1";

    public const string DuelDepthSeedProfileId =
        "frontline-labs-duel-depth-1";
    public const string ClassesSeedProfileId =
        "frontline-labs-classes-1";

    /// <summary>The hosted contract's capture threshold.</summary>
    public const int DefaultCaptureThreshold = 15;

    /// <summary>The hosted contract's Prime automatic-return delay.</summary>
    public const int DefaultPrimeRespawnTicks = 18;

    /// <summary>
    /// How long a territory ratchet holds a completed advance. The wave-2
    /// corpus measured the advance-reversal latency at 33 ticks — respawn 18
    /// plus transit 12, or capture 15 plus the 5-tick redeploy pause — so a
    /// shorter hold would be undone by the reinforcement wave the capture
    /// itself triggered. Forty ticks is the next round value above that
    /// measurement and still only 8% of a 500-tick match, so a five-position
    /// frontline can change hands repeatedly
    /// (<c>docs/DESIGN-FORENSICS-DYNAMICS-2026-07-29.md</c>).
    /// </summary>
    public const int RatchetHoldTicksDefault = 40;

    /// <summary>
    /// The capture channel's paired speed factor, registered as
    /// <c>channel-speed</c>. Gain per sole stationary team tick stays 1 and
    /// the multiplier arithmetic is untouched; one number moves, because
    /// threshold and gain are not separable claims about the same thing.
    /// <para>Eight is derived from the post-fight window every class shares —
    /// the 18-tick Prime automatic return. A SCREENED solo channeler
    /// completes in 8 and in 9 with one leaked bolt, both of which fit inside
    /// 18 with room for the approach; an unscreened one never completes. At
    /// 10 a single leaked bolt runs into the next reinforcement wave, and at
    /// 6 a screened channel finishes before a defender can rotate onto a
    /// firing heading, which deletes the poke counterplay the whole
    /// mechanism exists to create.</para>
    /// <para>Territorial progress is <c>advance × (index − centre) ×
    /// threshold</c>, so the reported score channel rescales by 8/15 under
    /// this arm. Nothing about ranking changes; historical numbers need the
    /// scale factor applied before comparison.</para>
    /// </summary>
    public const int ChannelCaptureThreshold = 8;

    /// <summary>
    /// The channel's stationary gain-multiplier cap, registered as
    /// <c>channel-stack-cap</c>. Stacking pays — two stationary channelers
    /// against a dead defence capture in 4 ticks rather than 8 — but bodies
    /// three, four, and five buy no additional speed at all, which keeps
    /// "extra bodies buy extra tempo" out of a design that has already
    /// convicted that loop once.
    /// </summary>
    public const int ChannelStationaryGainMultiplierCap = 2;

    /// <summary>
    /// The channel's opposing-erosion multiplier, registered as
    /// <c>recapture-cost</c>. Flipping a standing claim costs
    /// <c>ceil(claim / 8) + threshold</c> ticks against a fresh capture's
    /// threshold: a MAXIMAL standing enemy claim (threshold 8) is erased by a
    /// single controlling tick, so the full flip is 1 erode tick + 8 build
    /// ticks = 9 against a fresh capture's 8 — 1.125×, sliding to 1.0× as the
    /// standing claim shrinks.
    /// <para>Raised 4 → 8 on the owner's wave-8 ruling ("recapture needs to
    /// be faster"). Wave 8 crowned the bulwark on the full game (#188): the
    /// class that holds ground was paying the least for holding it, and the
    /// erosion multiplier is the one number that prices taking ground BACK.
    /// The band the arm was adopted under (1.0–1.25×) still holds — the whole
    /// range now sits in its lower half.</para>
    /// <para>Erosion still stops at neutral and still discards overshoot, so
    /// the documented "no own claim on the crossing tick" invariant is
    /// preserved: erasing a claim and starting one are still two ticks'
    /// work.</para>
    /// </summary>
    public const int ChannelOpposingErosionMultiplier = 8;

    /// <summary>The capture channel's plain arm token.</summary>
    public const string ChannelArmToken = "channel";

    /// <summary>
    /// The declared tick limit for one horizon level. It is a rules LIMIT, so
    /// it travels in the contract like every other pacing number and a bot
    /// reads it rather than assuming 500.
    /// </summary>
    public static int MaxTicks(FrontlineLabsHorizonArm horizon) =>
        horizon switch
        {
            FrontlineLabsHorizonArm.Standard => 500,
            FrontlineLabsHorizonArm.Long => 750,
            _ => throw new ArgumentOutOfRangeException(
                nameof(horizon),
                horizon,
                "Unknown Frontline Labs horizon arm."),
        };

    /// <summary>Canonical IDs are capped at 64 characters.</summary>
    private const int MaxRulesetIdLength = 64;

    /// <summary>
    /// The topology profile label for one resolved cell, read from the
    /// contract's per-team slot counts rather than from the arm flags. A
    /// profile ID is a pre-registration, so an unregistered shape faults here
    /// rather than borrowing a neighbouring label — a five-slot MIRROR is not
    /// the five-versus-three cell, and mislabelling it would carry the wrong
    /// topology into balance registration.
    /// </summary>
    public static string TopologyProfileIdFor(PublicMatchTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        int[] counts =
        [
            .. topology.Teams
                .Select(team => topology.UnitSlots.Count(slot =>
                    slot.TeamId == team.TeamId))
                .OrderByDescending(count => count),
        ];
        return counts switch
        {
            [3, 3] => TopologyProfileId,
            [5, 5] => FiveSlotMirrorTopologyProfileId,
            [5, 3] => AsymmetricSlotsTopologyProfileId,
            [4, 4] => TrimMirrorTopologyProfileId,
            [4, 3] => TrimAsymmetricSlotsTopologyProfileId,
            [8, 8] => LegionMirrorTopologyProfileId,
            [9, 8] => LegionAsymmetricSlotsTopologyProfileId,
            [9, 9] => LegionFabricatorMirrorTopologyProfileId,
            _ => throw new ArgumentOutOfRangeException(
                nameof(topology),
                string.Join("/", counts),
                "No Frontline Labs topology profile is registered for these "
                + "per-team slot counts. Register one before running the "
                + "cell — the profile ID travels into balance evidence."),
        };
    }

    private const string PrimeFormId = "prime-mobile";
    private const string ChildFormId = "child-mobile";
    private const string ReplicaFormId = "replica-mobile";
    private const string TurretFormId = "turret";
    private const string GroundMovementId = "ground";
    private const string MobileVisionId = "mobile-vision";
    private const string TurretVisionId = "turret-vision";
    private const string MobileAttackId = "mobile-bolt";
    private const string TurretAttackId = "turret-bolt";
    private const string MobilizeActionId = "mobilize";
    private const string ShootStraightActionId = "shoot-straight";
    private const int ShootStraightActionCode = 105;
    private const string PrimeLifecycleId = "prime-respawn";
    private const string ChildLifecycleId = "child-ready";
    private const string FabricationSourceRoleId =
        "fabrication-source";
    private const string FabricationOutputRoleId =
        "fabrication-output";
    private const string RemoteFabricationSourceRegionId =
        "fabrication-source-anywhere";

    public static ActorResolvedMatchDefinition Create() =>
        CreateResolved(
            RulesetId,
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false);

    /// <summary>
    /// Creates a local-only, content-identified capture-threshold arm without
    /// reinterpreting the immutable hosted <see cref="RulesetId"/> contract.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateCaptureThresholdExperiment(int captureThreshold)
    {
        if (captureThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(captureThreshold),
                captureThreshold,
                "Capture threshold must be positive.");
        }

        return CreateResolved(
            $"{RulesetId}-experiment-capture-{captureThreshold}",
            captureThreshold,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false);
    }

    /// <summary>
    /// Creates a local-only capture-gain phase arm. Hosted v1 remains static;
    /// the candidate publishes its complete schedule in the resolved contract.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateCaptureGainPhaseExperiment(
            int startsAtTick,
            int gainPerSoleTeamTick)
    {
        if (startsAtTick <= 0 || startsAtTick >= 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startsAtTick),
                startsAtTick,
                "The phase must start after tick zero and before MaxTicks.");
        }
        if (gainPerSoleTeamTick <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gainPerSoleTeamTick),
                gainPerSoleTeamTick,
                "Capture gain must be positive.");
        }

        return CreateResolved(
            $"{RulesetId}-experiment-gain-t{startsAtTick}-{gainPerSoleTeamTick}",
            captureThreshold: 15,
            captureGainSchedule:
            [
                new(
                    "opening",
                    startsAtTick: 0,
                    gainPerSoleTeamTick: 1),
                new(
                    "late-escalation",
                    startsAtTick,
                    gainPerSoleTeamTick),
            ],
            enableMobilize: false,
            remoteFabrication: false);
    }

    /// <summary>
    /// Creates a local-only action-contract arm in which a turret may return
    /// once to child-mobile without allowing an Anchor healing loop.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateMobilizeExperiment() =>
        CreateResolved(
            $"{RulesetId}-experiment-mobilize",
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: true,
            remoteFabrication: false);

    /// <summary>
    /// Creates a local-only fabrication arm in which an explicit Fabricate
    /// action may queue a Ready child from any walkable source position. The
    /// child still appears on the participant's protected output pad and the
    /// action still consumes one Prime decision.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateRemoteFabricationExperiment() =>
        CreateResolved(
            $"{RulesetId}-experiment-remote-fabrication",
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: true);

    /// <summary>
    /// Creates a local-only objective-control arm in which the positive form
    /// weight difference between teams determines capture pressure.
    /// </summary>
    public static ActorResolvedMatchDefinition CreateNetControlExperiment() =>
        CreateResolved(
            $"{RulesetId}-experiment-net-control",
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false,
            controlPolicy:
                FrontlineCaptureDefinition.ControlPolicyKind
                    .NetPositiveObjectiveWeightDifferenceScalesGainNonPositiveAppliesConfiguredDecayOppositionErodesToNeutral);

    /// <summary>
    /// Creates a local-only duel-depth arm. Mobile attacks may remain
    /// straight or commit one private 45-degree bend after one to four tiles;
    /// initial aim offsets and repeated bends are unavailable.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateOneBendShotsExperiment(
            FrontlineLabsDuelMapArm mapArm =
                FrontlineLabsDuelMapArm.Current) =>
        CreateResolved(
            $"{RulesetId}-experiment-one-bend-shots",
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false,
            oneBendShots: true,
            duelMapArm: mapArm,
            seedProfileId: DuelDepthSeedProfileId);

    /// <summary>
    /// Creates a local-only progression arm. Each team's child slots create
    /// their first mobile lives automatically at ticks 120 and 260, then use
    /// ordinary automatic respawn. One-bend shots remain enabled so this arm
    /// can be compared directly with the duel-depth map experiments.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateAutomaticCompanionsExperiment(
            FrontlineLabsDuelMapArm mapArm =
                FrontlineLabsDuelMapArm.Current) =>
        CreateResolved(
            $"{RulesetId}-experiment-one-bend-auto-companions",
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false,
            oneBendShots: true,
            duelMapArm: mapArm,
            automaticCompanions: true,
            seedProfileId: DuelDepthSeedProfileId);

    /// <summary>
    /// Creates a local-only, content-identified class-matchup arm. Each team's
    /// slots carry one pre-registered class chassis; the map, mode, scoring,
    /// and kinematics stay identical to the base contract. Pairs are
    /// canonical in ordinal class-ID order — fairness comes from mirrored bot
    /// assignments, not from a second swapped contract (DECISIONS #153).
    /// </summary>
    public static ActorResolvedMatchDefinition CreateClassesExperiment(
        FrontlineLabsClassDefinition teamZeroClass,
        FrontlineLabsClassDefinition teamOneClass,
        FrontlineLabsDuelMapArm mapArm = FrontlineLabsDuelMapArm.Current,
        ActorMovementFacingCoupling movementCoupling =
            ActorMovementFacingCoupling.PreserveFacing)
    {
        ArgumentNullException.ThrowIfNull(teamZeroClass);
        ArgumentNullException.ThrowIfNull(teamOneClass);
        if (string.CompareOrdinal(teamZeroClass.Id, teamOneClass.Id) > 0)
        {
            throw new ArgumentException(
                "Class pairs are canonical: pass classes in ordinal ID order "
                + "and mirror bot assignments instead of swapping teams.",
                nameof(teamZeroClass));
        }

        return CreateResolved(
            ClassesRulesetId(
                teamZeroClass,
                teamOneClass,
                movementCoupling),
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false,
            duelMapArm: mapArm,
            seedProfileId: ClassesSeedProfileId,
            classes: (teamZeroClass, teamOneClass),
            movementCoupling: movementCoupling);
    }

    /// <summary>
    /// Creates a local-only movement-kinematics arm for the pre-registered
    /// facing-coupling A/B (DECISIONS #155/#156). Everything except the
    /// movement profile's facing coupling — map, mode, scoring, projectile
    /// kinematics, and the free absolute rotate — is held constant against
    /// the base contract, so the arm isolates exactly one mechanic.
    /// <see cref="ActorMovementFacingCoupling.PreserveFacing"/> is the
    /// measured baseline and is not a separate arm.
    /// </summary>
    public static ActorResolvedMatchDefinition
        CreateMovementCouplingExperiment(
            ActorMovementFacingCoupling movementCoupling,
            FrontlineLabsDuelMapArm mapArm = FrontlineLabsDuelMapArm.Current)
    {
        if (movementCoupling == ActorMovementFacingCoupling.PreserveFacing)
        {
            throw new ArgumentOutOfRangeException(
                nameof(movementCoupling),
                movementCoupling,
                "PreserveFacing is the baseline contract, not an arm; call "
                + "Create() for it.");
        }

        return CreateResolved(
            $"{RulesetId}-experiment-{ArmToken(movementCoupling)}",
            captureThreshold: 15,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false,
            duelMapArm: mapArm,
            movementCoupling: movementCoupling);
    }

    /// <summary>
    /// Creates a local-only candidate cell for the pre-registered factorial.
    /// Phase 1's pendulum counterweights (DECISIONS #158) select typed capture
    /// and lifecycle policies, the numbers-only level retunes the capture
    /// threshold and the Prime respawn delay, and phase 2's class skills
    /// (<c>docs/DESIGN-MECHANISM-SLATE-2026-07-29.md</c>) add per-class stance
    /// routes and slot topology. Every factor composes with every other, with
    /// the class slate, with the movement-coupling arm, and with the duel
    /// maps, so one factorial cell is one call.
    /// </summary>
    public static ActorResolvedMatchDefinition CreatePendulumExperiment(
        FrontlineLabsPendulumArm pendulum,
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classes = null,
        FrontlineLabsDuelMapArm mapArm = FrontlineLabsDuelMapArm.Current,
        ActorMovementFacingCoupling movementCoupling =
            ActorMovementFacingCoupling.PreserveFacing,
        int captureThreshold = DefaultCaptureThreshold,
        int primeRespawnTicks = DefaultPrimeRespawnTicks,
        FrontlineLabsSkillKit skills = FrontlineLabsSkillKit.None,
        FrontlineLabsBendEnvelopeArm bendEnvelope =
            FrontlineLabsBendEnvelopeArm.StrikerOnly,
        FrontlineLabsFiveSlotVariant fiveSlots =
            FrontlineLabsFiveSlotVariant.Full,
        FrontlineLabsStanceGroundArm stanceGround =
            FrontlineLabsStanceGroundArm.Strict,
        FrontlineLabsAimArm aim = FrontlineLabsAimArm.Straight,
        FrontlineLabsCooldownArm cooldown = FrontlineLabsCooldownArm.Frozen,
        FrontlineLabsVolleyArm volley = FrontlineLabsVolleyArm.Cast,
        FrontlineLabsSideObjectiveArm sideObjective =
            FrontlineLabsSideObjectiveArm.None,
        FrontlineLabsCaptureArm capture = FrontlineLabsCaptureArm.Frozen,
        FrontlineLabsEconomyArm economy = FrontlineLabsEconomyArm.None,
        FrontlineLabsRosterArm roster = FrontlineLabsRosterArm.None,
        FrontlineLabsHorizonArm horizon = FrontlineLabsHorizonArm.Standard)
    {
        if (!Enum.IsDefined(horizon))
        {
            throw new ArgumentOutOfRangeException(
                nameof(horizon),
                horizon,
                "Unknown Frontline Labs horizon arm.");
        }
        // The horizon is a limits change, which re-prices every pacing gate in
        // the game for both teams — so it is a real arm on every pair and, like
        // the channel and the economy, it needs a cell to sit in.
        if (horizon != FrontlineLabsHorizonArm.Standard
            && classes is null
            && pendulum == FrontlineLabsPendulumArm.None)
        {
            throw new ArgumentException(
                "A longer horizon re-prices every pacing gate both teams "
                + "play against, so it needs a cell to sit in: pass a class "
                + "pair, or compose it with a pendulum level.",
                nameof(horizon));
        }
        if (!Enum.IsDefined(roster))
        {
            throw new ArgumentOutOfRangeException(
                nameof(roster),
                roster,
                "Unknown Frontline Labs roster arm.");
        }
        // The roster states its shape per class — three live bodies for a
        // class that receives companions, a fourth fabricable slot for the
        // one that builds them — so it has no meaning without a class pair,
        // exactly like the skills, the aim grammar, and the cooldown clock.
        if (roster != FrontlineLabsRosterArm.None && classes is null)
        {
            throw new ArgumentException(
                "The roster is declared per class chassis (a class that "
                + "receives companions starts with three bodies; the "
                + "fabricator starts with a fourth slot it fabricates), so it "
                + "needs a class pair; pass one.",
                nameof(roster));
        }
        // Two arms that each mint a map generation cannot share a cell: the
        // combined generation would be a third pre-registration nobody has
        // asked for, and the side objective is dormant (DECISIONS #186).
        if (roster != FrontlineLabsRosterArm.None
            && sideObjective != FrontlineLabsSideObjectiveArm.None)
        {
            throw new ArgumentException(
                "The roster arm and the side objective each mint their own "
                + "map generation, so they are mutually exclusive: a cell "
                + "carrying both would run on an unregistered third map.",
                nameof(roster));
        }
        if (!Enum.IsDefined(capture))
        {
            throw new ArgumentOutOfRangeException(
                nameof(capture),
                capture,
                "Unknown Frontline Labs capture arm.");
        }
        if (!Enum.IsDefined(economy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(economy),
                economy,
                "Unknown Frontline Labs economy arm.");
        }
        if (economy != FrontlineLabsEconomyArm.None
            && sideObjective != FrontlineLabsSideObjectiveArm.None)
        {
            throw new ArgumentException(
                "A battlefield economy and a side objective both claim the "
                + "side lanes' attention, so they are mutually exclusive "
                + "arms: a cell carrying both could attribute neither.",
                nameof(economy));
        }
        if (!Enum.IsDefined(sideObjective))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sideObjective),
                sideObjective,
                "Unknown Frontline Labs side objective arm.");
        }
        if (!Enum.IsDefined(volley))
        {
            throw new ArgumentOutOfRangeException(
                nameof(volley),
                volley,
                "Unknown Frontline Labs volley arm.");
        }
        if (!Enum.IsDefined(cooldown))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cooldown),
                cooldown,
                "Unknown Frontline Labs cooldown arm.");
        }
        if (cooldown != FrontlineLabsCooldownArm.Frozen && classes is null)
        {
            throw new ArgumentException(
                "The cooldown clock is registered for the class game; pass "
                + "a class pair.",
                nameof(cooldown));
        }
        if (!Enum.IsDefined(aim))
        {
            throw new ArgumentOutOfRangeException(
                nameof(aim),
                aim,
                "Unknown Frontline Labs aim arm.");
        }
        if (aim != FrontlineLabsAimArm.Straight && classes is null)
        {
            throw new ArgumentException(
                "The aim grammar is handed to class chassis, so an aim arm "
                + "has no meaning without a class pair; pass one.",
                nameof(aim));
        }
        if (!Enum.IsDefined(stanceGround))
        {
            throw new ArgumentOutOfRangeException(
                nameof(stanceGround),
                stanceGround,
                "Unknown Frontline Labs stance-ground arm.");
        }
        if (!Enum.IsDefined(fiveSlots))
        {
            throw new ArgumentOutOfRangeException(
                nameof(fiveSlots),
                fiveSlots,
                "Unknown Frontline Labs five-slot variant.");
        }
        if (!Enum.IsDefined(bendEnvelope))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bendEnvelope),
                bendEnvelope,
                "Unknown Frontline Labs bend envelope.");
        }
        if (bendEnvelope != FrontlineLabsBendEnvelopeArm.StrikerOnly
            && classes is null)
        {
            throw new ArgumentException(
                "The curve grammar is handed to class chassis, so a bend "
                + "envelope has no meaning without a class pair; pass one.",
                nameof(bendEnvelope));
        }
        if ((pendulum & ~AllPendulumArms) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pendulum),
                pendulum,
                "Unknown Frontline Labs pendulum arm.");
        }
        if ((skills & ~AllSkills) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(skills),
                skills,
                "Unknown Frontline Labs class skill.");
        }
        if (skills != FrontlineLabsSkillKit.None && classes is null)
        {
            throw new ArgumentException(
                "Class skills are class capabilities and have no meaning "
                + "without a class pair; pass one.",
                nameof(skills));
        }
        FrontlineLabsSkillKit effectiveSkills =
            EffectiveSkills(skills, classes);
        if (skills != FrontlineLabsSkillKit.None
            && effectiveSkills == FrontlineLabsSkillKit.None)
        {
            throw new ArgumentException(
                "Every class skill is owned by exactly one class, and no "
                + "class in this cell owns the requested skill: "
                + string.Join(
                    ", ",
                    Skills.Where(skill => skills.HasFlag(skill))
                        .Select(skill =>
                            $"{SkillToken(skill)} belongs to "
                            + $"{OwnerClassId(skill)}"))
                + ". Pick a cell containing the owning class.",
                nameof(skills));
        }
        // The channel carries its own paired threshold. A caller that leaves
        // the threshold alone gets the arm's shipped 8; a caller that names
        // one is running the channel-speed ablation level, which spells its
        // number in the identity exactly as a numbers-only level always has.
        captureThreshold =
            capture == FrontlineLabsCaptureArm.Channel
            && captureThreshold == DefaultCaptureThreshold
                ? ChannelCaptureThreshold
                : captureThreshold;
        if (captureThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(captureThreshold),
                captureThreshold,
                "Capture threshold must be positive.");
        }
        if (primeRespawnTicks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(primeRespawnTicks),
                primeRespawnTicks,
                "The Prime respawn delay cannot be negative.");
        }
        if (classes is { } pair
            && string.CompareOrdinal(pair.TeamZero.Id, pair.TeamOne.Id) > 0)
        {
            throw new ArgumentException(
                "Class pairs are canonical: pass classes in ordinal ID order "
                + "and mirror bot assignments instead of swapping teams.",
                nameof(classes));
        }
        // The channel changes capture for everyone, so it is a real arm on
        // every cell — never inert-omitted the way a class-scoped tuning is
        // — and it needs a cell to sit in for the same reason the side
        // objective does.
        if (capture != FrontlineLabsCaptureArm.Frozen
            && classes is null
            && pendulum == FrontlineLabsPendulumArm.None)
        {
            throw new ArgumentException(
                "The capture channel reworks the front both teams are "
                + "fighting over, so it needs a cell to sit in: pass a class "
                + "pair, or compose it with a pendulum level.",
                nameof(capture));
        }
        // The economy changes the game for every class pair whatever is in
        // the cell — the deposits, the wreckage, and the ladder are the same
        // whatever chassis are present — so it is never inert-omitted, and it
        // needs a cell for exactly the reason the other two arms do.
        if (economy != FrontlineLabsEconomyArm.None
            && classes is null
            && pendulum == FrontlineLabsPendulumArm.None)
        {
            throw new ArgumentException(
                "A battlefield economy adds a resource both teams fight "
                + "over, so it needs a cell to sit in: pass a class pair, or "
                + "compose it with a pendulum level.",
                nameof(economy));
        }
        if (pendulum == FrontlineLabsPendulumArm.None
            && captureThreshold == DefaultCaptureThreshold
            && primeRespawnTicks == DefaultPrimeRespawnTicks
            && classes is null
            && effectiveSkills == FrontlineLabsSkillKit.None
            && bendEnvelope == FrontlineLabsBendEnvelopeArm.StrikerOnly
            && sideObjective == FrontlineLabsSideObjectiveArm.None
            && capture == FrontlineLabsCaptureArm.Frozen
            && economy == FrontlineLabsEconomyArm.None
            && movementCoupling == ActorMovementFacingCoupling.PreserveFacing)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pendulum),
                pendulum,
                "The uncounterweighted contract is the measured control, not "
                + "an arm; call Create() for it.");
        }

        if (fiveSlots != FrontlineLabsFiveSlotVariant.Full
            && !effectiveSkills.HasFlag(
                FrontlineLabsSkillKit.FabricatorFiveSlots))
        {
            throw new ArgumentException(
                "A five-slot variant tunes the FIVE SLOTS skill, so it needs "
                + "that skill active in the cell: pass a class pair "
                + "containing the fabricator and a skill selection that "
                + "includes five-slots.",
                nameof(fiveSlots));
        }
        // The roster authors every slot's unlock tick, so the half of a
        // five-slot variant that moves the extra SCHEDULE changes no contract
        // bytes here and must therefore change no identity — the same
        // inert-omission rule the salvo and the ground arms follow. What
        // survives is the half that moves the rebuild CLOCK.
        FrontlineLabsFiveSlotVariant effectiveFiveSlots =
            EffectiveFiveSlotVariant(fiveSlots, roster);
        // The salvo tunes the striker's fan, so it is inert-omitted where
        // no striker is in the cell — the skills rule again.
        FrontlineLabsVolleyArm effectiveVolley =
            volley == FrontlineLabsVolleyArm.Salvo
            && effectiveSkills.HasFlag(FrontlineLabsSkillKit.StrikerVolley)
                ? FrontlineLabsVolleyArm.Salvo
                : FrontlineLabsVolleyArm.Cast;
        // A ground arm is inert where nothing it touches exists — the
        // skills rule: it changes no contract bytes there, so it changes
        // no identity either. Free touches only the skill stances; Open
        // also touches the turret anchor routes.
        bool touchesStances =
            effectiveSkills.HasFlag(FrontlineLabsSkillKit.StrikerVolley)
            || effectiveSkills.HasFlag(FrontlineLabsSkillKit.BulwarkAegisShell);
        bool touchesAnchors = classes is { } anchorPair
            && (anchorPair.TeamZero.MayAnchor || anchorPair.TeamOne.MayAnchor);
        FrontlineLabsStanceGroundArm effectiveGround = stanceGround switch
        {
            FrontlineLabsStanceGroundArm.Free when !touchesStances =>
                FrontlineLabsStanceGroundArm.Strict,
            FrontlineLabsStanceGroundArm.Open
                when !touchesStances && !touchesAnchors =>
                FrontlineLabsStanceGroundArm.Strict,
            _ => stanceGround,
        };

        return CreateResolved(
            PendulumRulesetId(
                pendulum,
                classes,
                movementCoupling,
                captureThreshold,
                primeRespawnTicks,
                effectiveSkills,
                bendEnvelope,
                effectiveFiveSlots,
                effectiveGround,
                aim,
                cooldown,
                effectiveVolley,
                sideObjective,
                capture,
                economy,
                roster,
                horizon),
            captureThreshold,
            captureGainSchedule: null,
            enableMobilize: false,
            remoteFabrication: false,
            controlPolicy: ControlPolicy(pendulum, capture),
            duelMapArm: mapArm,
            seedProfileId: classes is null ? null : ClassesSeedProfileId,
            classes: classes is { } cell
                ? (ApplyFiveSlotVariant(cell.TeamZero, effectiveFiveSlots),
                    ApplyFiveSlotVariant(cell.TeamOne, effectiveFiveSlots))
                : null,
            movementCoupling: movementCoupling,
            pendulum: pendulum,
            primeRespawnTicks: primeRespawnTicks,
            skills: effectiveSkills,
            bendEnvelope: bendEnvelope,
            stanceGround: effectiveGround,
            aim: aim,
            cooldown: cooldown,
            volley: effectiveVolley,
            sideObjective: sideObjective,
            economy: economy,
            roster: roster,
            horizon: horizon);
    }

    /// <summary>
    /// The five-slot variant a legion cell actually carries. Two of the five
    /// registered variants move only the extra-slot SCHEDULE (Trim drops the
    /// fifth slot, Boom swings both late), and the roster authors that
    /// schedule for every class — so under the roster those two write exactly
    /// the bytes the unmodified skill writes, and an arm that changes no bytes
    /// changes no identity. Moor is Trim + Drag, so it resolves to Drag.
    /// Drag and Wane survive intact: their lever is the fabricator's rebuild
    /// clock, which the roster does not touch.
    /// </summary>
    private static FrontlineLabsFiveSlotVariant EffectiveFiveSlotVariant(
        FrontlineLabsFiveSlotVariant variant,
        FrontlineLabsRosterArm roster) =>
        roster == FrontlineLabsRosterArm.None
            ? variant
            : variant switch
            {
                FrontlineLabsFiveSlotVariant.Trim =>
                    FrontlineLabsFiveSlotVariant.Full,
                FrontlineLabsFiveSlotVariant.Boom =>
                    FrontlineLabsFiveSlotVariant.Full,
                FrontlineLabsFiveSlotVariant.Moor =>
                    FrontlineLabsFiveSlotVariant.Drag,
                _ => variant,
            };

    /// <summary>
    /// Applies a registered FIVE SLOTS tuning variant to the class entry
    /// that owns the skill; every other entry passes through untouched.
    /// Each variant moves exactly one lever (the ablation discipline):
    /// Trim drops the fifth slot, Boom swings the extra schedule late on
    /// the class's own cadence, and Drag prices count in tempo by putting
    /// the ordinary children on the 30-tick baseline rebuild clock.
    /// </summary>
    private static FrontlineLabsClassDefinition ApplyFiveSlotVariant(
        FrontlineLabsClassDefinition entry,
        FrontlineLabsFiveSlotVariant variant)
    {
        if (entry.Skill != FrontlineLabsSkillKit.FabricatorFiveSlots)
            return entry;
        return variant switch
        {
            FrontlineLabsFiveSlotVariant.Full => entry,
            FrontlineLabsFiveSlotVariant.Trim =>
                entry with { ExtraChildUnlockTicks = [300] },
            FrontlineLabsFiveSlotVariant.Boom =>
                entry with { ExtraChildUnlockTicks = [360, 480] },
            FrontlineLabsFiveSlotVariant.Drag =>
                entry with { ChildRebuildDelayTicks = 30 },
            FrontlineLabsFiveSlotVariant.Moor =>
                entry with
                {
                    ExtraChildUnlockTicks = [300],
                    ChildRebuildDelayTicks = 30,
                },
            FrontlineLabsFiveSlotVariant.Wane =>
                entry with
                {
                    ExtraChildUnlockTicks = [300],
                    ChildRebuildDelayTicks = 22,
                },
            _ => throw new ArgumentOutOfRangeException(
                nameof(variant),
                variant,
                "Unknown Frontline Labs five-slot variant."),
        };
    }

    /// <summary>
    /// The identity token for a registered five-slot tuning variant. It
    /// rides AFTER the arm tokens (the variant refines the skill factor),
    /// and the Full arm contributes nothing — phase 2's measured identities
    /// are unchanged.
    /// </summary>
    private static string? FiveSlotToken(
        FrontlineLabsFiveSlotVariant variant) =>
        variant switch
        {
            FrontlineLabsFiveSlotVariant.Full => null,
            FrontlineLabsFiveSlotVariant.Trim => "trim",
            FrontlineLabsFiveSlotVariant.Boom => "boom",
            FrontlineLabsFiveSlotVariant.Drag => "drag",
            FrontlineLabsFiveSlotVariant.Moor => "moor",
            FrontlineLabsFiveSlotVariant.Wane => "wane",
            _ => throw new ArgumentOutOfRangeException(
                nameof(variant),
                variant,
                "Unknown Frontline Labs five-slot variant."),
        };

    private const FrontlineLabsPendulumArm AllPendulumArms =
        FrontlineLabsPendulumArm.StickyFrontline
        | FrontlineLabsPendulumArm.ForwardRally
        | FrontlineLabsPendulumArm.ContestMajority
        | FrontlineLabsPendulumArm.EnemySoleDecay;

    /// <summary>
    /// The keel without its forward rally (owner ruling on the wave-8 read:
    /// "the respawn at capture point may be a bit too strong and it also means
    /// Fab's signature skill is almost useless — let's try the next balancing
    /// round without that"). Sticky frontline, contest majority and enemy-sole
    /// decay are untouched; every automatic arrival lands on its reserved home
    /// spawn.
    /// <para>The consequence the ruling is FOR: with no free forward
    /// placement, the fabricator's field-placed children become the only
    /// forward body delivery in the game. Its class verb stops competing with
    /// a free rally every class already had and starts being the reason to
    /// play the class.</para>
    /// </summary>
    private const FrontlineLabsPendulumArm HullPendulumArms =
        FrontlineLabsPendulumArm.StickyFrontline
        | FrontlineLabsPendulumArm.ContestMajority
        | FrontlineLabsPendulumArm.EnemySoleDecay;

    private const FrontlineLabsSkillKit AllSkills =
        FrontlineLabsSkillKit.StrikerVolley
        | FrontlineLabsSkillKit.BulwarkAegisShell
        | FrontlineLabsSkillKit.FabricatorFiveSlots;

    /// <summary>Every skill in the pre-registered kit, in flag order.</summary>
    public static ImmutableArray<FrontlineLabsSkillKit> Skills { get; } =
    [
        FrontlineLabsSkillKit.StrikerVolley,
        FrontlineLabsSkillKit.BulwarkAegisShell,
        FrontlineLabsSkillKit.FabricatorFiveSlots,
    ];

    /// <summary>
    /// The skills this cell actually carries: a skill is a class capability,
    /// so requesting one whose owning class is absent changes no contract
    /// bytes and must therefore change no arm identity either.
    /// </summary>
    public static FrontlineLabsSkillKit EffectiveSkills(
        FrontlineLabsSkillKit requested,
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classes) =>
        classes is { } pair
            ? requested & (pair.TeamZero.Skill | pair.TeamOne.Skill)
            : FrontlineLabsSkillKit.None;

    private static string OwnerClassId(FrontlineLabsSkillKit skill) =>
        FrontlineLabsClassDefinition.All
            .Single(entry => entry.Skill == skill)
            .Id;

    /// <summary>
    /// The control policy for one cell. The capture channel is itself a
    /// control policy, so it REPLACES whichever presence rule the pendulum
    /// selected — contest-majority's surplus scaling carries over, now
    /// applied to stationary claim weight against total denial weight, and in
    /// the everyone-stationary limit below the cap the two agree exactly.
    /// </summary>
    private static FrontlineCaptureDefinition.ControlPolicyKind ControlPolicy(
        FrontlineLabsPendulumArm pendulum,
        FrontlineLabsCaptureArm capture = FrontlineLabsCaptureArm.Frozen) =>
        capture == FrontlineLabsCaptureArm.Channel
            ? FrontlineCaptureDefinition.ControlPolicyKind
                .StationaryClaimWeightVersusTotalDenialWeightScalesGainCappedOppositionErodesAtMultipleThenBuilds
        : pendulum.HasFlag(FrontlineLabsPendulumArm.ContestMajority)
            ? FrontlineCaptureDefinition.ControlPolicyKind
                .NetPositiveObjectiveWeightDifferenceScalesGainNonPositiveAppliesConfiguredDecayOppositionErodesToNeutral
            : FrontlineCaptureDefinition.ControlPolicyKind
                .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral;

    private static FrontlineCaptureDefinition.DecayClockKind DecayClock(
        FrontlineLabsPendulumArm pendulum) =>
        pendulum.HasFlag(FrontlineLabsPendulumArm.EnemySoleDecay)
            ? FrontlineCaptureDefinition.DecayClockKind
                .EmptyAndContestedTicksPreserveClaimEnemySoleErosionOnly
            : FrontlineCaptureDefinition.DecayClockKind
                .ConsecutiveEmptyOrContestedTicksResetByAnySoleControl;

    private static FrontlineCaptureDefinition.RedeployPolicyKind
        RedeployPolicy(FrontlineLabsPendulumArm pendulum) =>
        pendulum.HasFlag(FrontlineLabsPendulumArm.StickyFrontline)
            ? FrontlineCaptureDefinition.RedeployPolicyKind
                .AdvanceImmediatelyThenDenyEnemyRegressionPastTheHighWaterMarkThroughConfiguredHoldTicks
            : FrontlineCaptureDefinition.RedeployPolicyKind
                .AdvanceImmediatelyResetClaimKeepWorldPauseThroughCapturePlusConfiguredTicksBreachSkipsPause;

    /// <summary>
    /// One channel setting, or its inert zero when the cell does not channel.
    /// </summary>
    private static int ChannelSetting(
        FrontlineCaptureDefinition.ControlPolicyKind controlPolicy,
        int value) =>
        controlPolicy
            == FrontlineCaptureDefinition.ControlPolicyKind
                .StationaryClaimWeightVersusTotalDenialWeightScalesGainCappedOppositionErodesAtMultipleThenBuilds
            ? value
            : 0;

    private static int RatchetHoldTicks(
        FrontlineLabsPendulumArm pendulum) =>
        pendulum.HasFlag(FrontlineLabsPendulumArm.StickyFrontline)
            ? RatchetHoldTicksDefault
            : 0;

    /// <summary>
    /// The rally arms select the team-advance-ordered placement. The
    /// historical map-absolute value stays defined and resolvable for
    /// archived replays, but no arm selects it: one absolute scan handed the
    /// two mirror-image rally regions non-mirrored tiles, which a
    /// facing-locked identical-bot mirror probe measured as a 4/4 side sweep.
    /// </summary>
    private static ActorLifecycleDefinition
        .ActorAutomaticReturnPlacementKind AutomaticReturnPlacement(
            FrontlineLabsPendulumArm pendulum,
            FrontlineLabsSideObjectiveArm sideObjective) =>
        // MUSTER takes the unconditional rally away from BOTH teams and
        // hands it back only to whoever holds the flag — the memo's whole
        // point, that the placement the keel gives away for free becomes the
        // contested asset. So the lifecycle placement reverts to the home
        // spawn here even on a rally pendulum, and the secondary control's
        // effect is the only thing that can move an arrival forward.
        sideObjective == FrontlineLabsSideObjectiveArm.Muster
            ? ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims
        : pendulum.HasFlag(FrontlineLabsPendulumArm.ForwardRally)
            ? ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileInTeamAdvanceOrderThenAssignedSpawn
            : ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims;

    /// <summary>
    /// The declared side objective for one arm, or null for the measured
    /// baseline. Absence writes no canonical bytes at all, so every arm
    /// without a side objective keeps its exact historical fingerprints.
    /// </summary>
    private static FrontlineSecondaryControlDefinition? SecondaryControl(
        FrontlineLabsSideObjectiveArm sideObjective) =>
        sideObjective switch
        {
            FrontlineLabsSideObjectiveArm.None => null,
            FrontlineLabsSideObjectiveArm.Muster =>
                FrontlineLabsMusterSite.Definition,
            _ => throw new ArgumentOutOfRangeException(
                nameof(sideObjective),
                sideObjective,
                "Unknown Frontline Labs side objective arm."),
        };

    /// <summary>
    /// Content-identified ruleset ID for one factorial cell. A class pair
    /// drops the <c>-experiment-classes-</c> segment and every token shortens,
    /// for the reason #156 already recorded: canonical IDs are capped at 64
    /// characters and the longest class pair plus the longest movement token
    /// leaves eight characters for everything else.
    /// </summary>
    private static string PendulumRulesetId(
        FrontlineLabsPendulumArm pendulum,
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classes,
        ActorMovementFacingCoupling movementCoupling,
        int captureThreshold,
        int primeRespawnTicks,
        FrontlineLabsSkillKit skills,
        FrontlineLabsBendEnvelopeArm bendEnvelope,
        FrontlineLabsFiveSlotVariant fiveSlots =
            FrontlineLabsFiveSlotVariant.Full,
        FrontlineLabsStanceGroundArm stanceGround =
            FrontlineLabsStanceGroundArm.Strict,
        FrontlineLabsAimArm aim = FrontlineLabsAimArm.Straight,
        FrontlineLabsCooldownArm cooldown = FrontlineLabsCooldownArm.Frozen,
        FrontlineLabsVolleyArm volley = FrontlineLabsVolleyArm.Cast,
        FrontlineLabsSideObjectiveArm sideObjective =
            FrontlineLabsSideObjectiveArm.None,
        FrontlineLabsCaptureArm capture = FrontlineLabsCaptureArm.Frozen,
        FrontlineLabsEconomyArm economy = FrontlineLabsEconomyArm.None,
        FrontlineLabsRosterArm roster = FrontlineLabsRosterArm.None,
        FrontlineLabsHorizonArm horizon = FrontlineLabsHorizonArm.Standard)
    {
        bool composed = classes is not null
            || movementCoupling != ActorMovementFacingCoupling.PreserveFacing;
        // The tuning tokens ride after the arm tokens. `wane` + `free`
        // overflows the worst class cell by one factor, so the combination
        // is registered under one token, exactly as keel and helm/veer/rig
        // were: `berth` — moored on the ground it holds.
        string[] tuning =
            fiveSlots == FrontlineLabsFiveSlotVariant.Wane
                && stanceGround == FrontlineLabsStanceGroundArm.Free
            ? ["berth"]
            :
            [
                .. FiveSlotToken(fiveSlots) is { Length: > 0 } variant
                    ? new[] { variant }
                    : [],
                .. stanceGround switch
                {
                    FrontlineLabsStanceGroundArm.Free => new[] { "free" },
                    FrontlineLabsStanceGroundArm.Open => new[] { "open" },
                    _ => [],
                },
            ];
        // The aim grammar is an arm-level factor (it rides the gun, not a
        // skill), so its token lands right after the arm tokens. Two
        // combinations are registered under one token because their
        // spellings overflow the worst cells: `sail` = rig + aim, and
        // `crew` = rig + aim + wane — the whole tuned candidate game,
        // which the fabricator mirror cannot spell as sail-wane.
        string[] arms =
            ArmTokens(pendulum, skills, bendEnvelope, classes, composed);
        if (aim == FrontlineLabsAimArm.Offset)
        {
            if (arms is ["rig"]
                && fiveSlots == FrontlineLabsFiveSlotVariant.Wane
                && stanceGround == FrontlineLabsStanceGroundArm.Strict)
            {
                arms = ["crew"];
                tuning = [];
            }
            else if (arms is ["rig"]
                && fiveSlots == FrontlineLabsFiveSlotVariant.Wane
                && stanceGround == FrontlineLabsStanceGroundArm.Open)
            {
                // The whole open game (crew + open ground + the turret
                // cycle, owner ruling #176): the fabricator mirror cannot
                // spell the factors inside the budget.
                arms = ["deck"];
                tuning = [];
            }
            else
            {
                arms = arms is ["rig"]
                    ? ["sail"]
                    : [.. arms, "aim"];
            }
        }
        if (cooldown == FrontlineLabsCooldownArm.Ticking)
        {
            // `tide` = the whole tuned game on the ticking clock — the
            // fabricator mirror cannot spell its factors plus `tick`
            // inside the budget. A mirror resolves the open ground inert
            // (its arms spell `crew`), so both spellings of the same
            // requested game carry the one registered token.
            arms = arms is ["deck"] or ["crew"]
                ? ["tide"]
                : [.. arms, "tick"];
        }
        if (volley == FrontlineLabsVolleyArm.Salvo)
        {
            // `swell` = the whole game with the re-armed fan (#182/#183).
            // `crest` is the plain arm token where the game spells its
            // factors. The first mint of this arm (`surf`/`salvo`, entry
            // windup 2) was re-minted the same day the owner sharpened the
            // entry to the 1-tick grammar — behavior changed, so the
            // tokens changed; surf-id replays stay honest. The striker
            // cells cannot spell their factors plus `crest` inside the
            // budget, so the full-game spellings collapse:
            // fabricator-vs-striker resolves tide (wane present) and the
            // strikerless-wane-free spellings resolve sail-open-tick.
            arms = arms switch
            {
                ["tide"] => ["swell"],
                ["sail", .. ] when stanceGround
                        == FrontlineLabsStanceGroundArm.Open
                    && cooldown == FrontlineLabsCooldownArm.Ticking
                    => ["swell"],
                _ => [.. arms, "crest"],
            };
            if (arms is ["swell"])
                tuning = [];
        }
        if (sideObjective == FrontlineLabsSideObjectiveArm.Muster)
        {
            // The flag re-mints the whole game, exactly as `swell` re-minted
            // `tide` when the fan was re-armed. The worst class cell
            // (`fabricator-vs-fabricator` beside `facing-locked`) leaves 22
            // characters for the suffix and `facing-locked` spends 14 of
            // them, so the candidate game plus `muster` cannot be spelled at
            // all. Three flags for the three shapes the candidate game takes:
            // `pennant` is the tuned open game on the ticking clock,
            // `ensign` adds the fabricator's wane, and `banner` adds the
            // re-armed fan. Everything smaller spells its factors and
            // appends `muster`.
            (string[] Arms, string[] Tuning) flagged =
                (arms, tuning) switch
                {
                    (["swell"], _) => (new[] { "banner" }, []),
                    (["tide"], _) => (new[] { "ensign" }, []),
                    (["sail", "tick"], ["open"]) =>
                        (new[] { "pennant" }, []),
                    _ => (
                        [.. arms, FrontlineLabsMusterSite.ArmToken],
                        tuning),
                };
            arms = flagged.Arms;
            tuning = flagged.Tuning;
        }
        if (capture == FrontlineLabsCaptureArm.Channel)
        {
            // The channel re-mints the whole game, exactly as `swell`
            // re-minted `tide` and the flags re-minted `swell`: it is a
            // capture-CORE change, so a cell carrying it is not the cell it
            // was. The strikerless cells inert-omit the fan and therefore
            // spell a longer game, which does not fit beside the worst class
            // pair and `facing-locked` — the muster arm's exact problem and
            // its exact answer, three registered tokens for the three shapes
            // the candidate game takes.
            // The FIRST mint of this arm was `siege`/`sap`/`mantlet` at
            // erosion 4; the owner's post-wave-8 ruling ("recapture needs to
            // be faster") moved the multiplier to 8, so the tokens re-mint on
            // the surf→swell precedent and the old three keep meaning the
            // measured wave-8 bytes for ever. `storm` is what a siege becomes
            // when the assault is quick, `mine` is the sap driven deeper
            // (undermining, not storming), and `pavise` is the mantlet's
            // bigger cousin — the screen the sapper actually stands behind,
            // which is the open-ground spelling. Everything smaller spells its
            // factors and appends `channel`.
            (string[] Arms, string[] Tuning) channeled =
                (arms, tuning) switch
                {
                    (["swell"], _) => (new[] { "storm" }, []),
                    (["tide"], _) => (new[] { "mine" }, []),
                    (["sail", "tick"], ["open"]) =>
                        (new[] { "pavise" }, []),
                    _ => ([.. arms, ChannelArmToken], tuning),
                };
            arms = channeled.Arms;
            tuning = channeled.Tuning;
        }
        if (economy != FrontlineLabsEconomyArm.None)
        {
            // The economy re-mints the cell for the same reason the channel
            // does — the game it produces is not the game it was — and it hits
            // the same budget wall, because the worst class pair beside
            // `facing-locked` leaves eight canonical characters for the whole
            // arm.
            // The FIRST mint was `forge`/`anvil`/`smelter` (economy alone) and
            // `bastion`/`redoubt`/`smithy` (with the channel), at four deposits
            // of six and a wreck of one. The owner's post-wave-8 ruling ("the
            // new mechanism needs to be stronger and happen earlier") doubled
            // the income, so those six keep meaning the measured wave-8 bytes
            // and v1.1 mints its own: the forge scales up to a `foundry`, the
            // anvil gets `bellows`, the smelter becomes a `furnace` — and with
            // the channel already in the cell the bastion becomes a `citadel`,
            // the redoubt a `rampart`, and the smithy an `armoury`.
            // The control level always spells itself, because a control that
            // shared an identity with the arm it controls would be unreadable
            // in the evidence.
            (string[] Arms, string[] Tuning) traded =
                (arms, tuning, economy) switch
                {
                    (["swell"], _, FrontlineLabsEconomyArm.Scrap) =>
                        (new[] { "foundry" }, []),
                    (["tide"], _, FrontlineLabsEconomyArm.Scrap) =>
                        (new[] { "bellows" }, []),
                    (["sail", "tick"], ["open"],
                        FrontlineLabsEconomyArm.Scrap) =>
                        (new[] { "furnace" }, []),
                    (["storm"], _, FrontlineLabsEconomyArm.Scrap) =>
                        (new[] { "citadel" }, []),
                    (["mine"], _, FrontlineLabsEconomyArm.Scrap) =>
                        (new[] { "rampart" }, []),
                    (["pavise"], _, FrontlineLabsEconomyArm.Scrap) =>
                        (new[] { "armoury" }, []),
                    _ => (
                        [.. arms, EconomyArmToken(economy)],
                        tuning),
                };
            arms = traded.Arms;
            tuning = traded.Tuning;
        }
        if (horizon != FrontlineLabsHorizonArm.Standard)
        {
            // A longer horizon re-prices every pacing gate, so it is a factor
            // like any other and spells itself where the budget allows. Every
            // cell of the next round carries it inside a registered package
            // token, so this spelling is what a SMALLER probe cell gets.
            arms = [.. arms, HorizonArmToken];
        }
        if (roster != FrontlineLabsRosterArm.None)
        {
            // The roster re-mints the cell last, because it is the outermost
            // factor: it changes what every other arm is priced against. The
            // budget wall is the same one, so the same answer — registered
            // composites for the shapes the campaign runs, everything smaller
            // spells its factors and appends `levy`.
            // Two families, because the roster's own read is the 2×2 against
            // the shipped game: on the candidate game alone the formations are
            // `warband`, `retinue` and `vanguard`, and on the full v1.1 game
            // (channel + economy) the quarters that hold them are `brigade`,
            // `column` and `regiment`.
            (string[] Arms, string[] Tuning) mustered =
                (arms, tuning) switch
                {
                    (["swell"], _) => (new[] { "warband" }, []),
                    (["tide"], _) => (new[] { "retinue" }, []),
                    (["sail", "tick"], ["open"]) =>
                        (new[] { "vanguard" }, []),
                    (["citadel"], _) => (new[] { "brigade" }, []),
                    (["rampart"], _) => (new[] { "column" }, []),
                    (["armoury"], _) => (new[] { "regiment" }, []),
                    _ => (
                        [.. arms, FrontlineLabsLegionRoster.ArmToken],
                        tuning),
                };
            arms = mustered.Arms;
            tuning = mustered.Tuning;
        }
        // The next round's whole package — the keel without its rally, the
        // longer horizon, the re-tuned channel and economy on the tuned class
        // game — spells far more factors than the 64-character budget allows
        // in the worst cell, so it carries ONE registered token per shape, the
        // same answer the muster/channel/economy arms each reached. The roster
        // stays a composable flag on top of it.
        if (RegisteredPackageToken(
                pendulum,
                skills,
                bendEnvelope,
                fiveSlots,
                stanceGround,
                aim,
                cooldown,
                volley,
                capture,
                economy,
                horizon,
                roster,
                classes)
            is { Length: > 0 } package)
        {
            arms = [package];
            tuning = [];
        }
        string[] tokens =
        [
            .. arms,
            .. tuning,
            .. NumbersToken(
                    captureThreshold,
                    primeRespawnTicks,
                    composed,
                    capture)
                is { Length: > 0 } numbers
                ? new[] { numbers }
                : [],
            .. movementCoupling == ActorMovementFacingCoupling.PreserveFacing
                ? []
                : new[]
                {
                    composed
                        ? ComposedArmToken(movementCoupling)
                        : ArmToken(movementCoupling),
                },
        ];
        string suffix = string.Join("-", tokens);
        string id = classes is { } pair
            ? $"{RulesetId}-{pair.TeamZero.Id}-vs-{pair.TeamOne.Id}-{suffix}"
            : $"{RulesetId}-experiment-{suffix}";
        if (id.Length > MaxRulesetIdLength)
        {
            throw new InvalidOperationException(
                $"The candidate ID '{id}' needs {id.Length} of the "
                + $"{MaxRulesetIdLength} canonical characters. Drop one "
                + "factor from this cell — the class pair, the movement "
                + "coupling, a pendulum arm, a class skill, or the bend "
                + "envelope — or register the combination under a shorter "
                + "token.");
        }
        return id;
    }

    /// <summary>The longer horizon's plain arm token.</summary>
    private const string HorizonArmToken = "long";

    /// <summary>
    /// The registered identity for the NEXT ROUND'S PACKAGE, or empty.
    /// <para>Three owner rulings landed on the same game at once — no forward
    /// rally, a 750-tick horizon, and an economy that is allowed to decide the
    /// match — on top of the faster recapture and the richer deposits. That is
    /// one new game rather than five composable tunings, and its per-factor
    /// spelling (<c>hull</c> + the kit + the bend + the aim + the clock + the
    /// fan + the channel + the economy + the horizon) does not come close to
    /// fitting beside <c>fabricator-vs-fabricator</c> and
    /// <c>facing-locked</c>. So the package carries one token per shape, in
    /// the siege line the channel and economy tokens already speak:
    /// <list type="bullet">
    /// <item><c>vigil</c> — the striker shapes. Nobody is relieved any more:
    /// every body walks home and walks back, and the front is held by
    /// watching it.</item>
    /// <item><c>warren</c> — the fabricator shapes, where the only forward
    /// delivery left in the game is a fabricated body appearing beside its
    /// prime.</item>
    /// <item><c>bastille</c> — the bulwark mirror, which under home respawns
    /// is a fortress at both ends.</item>
    /// </list>
    /// With the LEGION roster on top they become <c>warpath</c>,
    /// <c>horde</c> and <c>stockade</c>. Every other combination in this
    /// family spells its factors and, in the cells where that overflows,
    /// faults with the usual "register the combination" message — a
    /// pre-registration is a decision, not a fallback.</para>
    /// </summary>
    private static string RegisteredPackageToken(
        FrontlineLabsPendulumArm pendulum,
        FrontlineLabsSkillKit skills,
        FrontlineLabsBendEnvelopeArm bendEnvelope,
        FrontlineLabsFiveSlotVariant fiveSlots,
        FrontlineLabsStanceGroundArm stanceGround,
        FrontlineLabsAimArm aim,
        FrontlineLabsCooldownArm cooldown,
        FrontlineLabsVolleyArm volley,
        FrontlineLabsCaptureArm capture,
        FrontlineLabsEconomyArm economy,
        FrontlineLabsHorizonArm horizon,
        FrontlineLabsRosterArm roster,
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classes)
    {
        if (pendulum != HullPendulumArms
            || classes is not { } pair
            || skills != (pair.TeamZero.Skill | pair.TeamOne.Skill)
            || bendEnvelope != FrontlineLabsBendEnvelopeArm.Universal
            || aim != FrontlineLabsAimArm.Offset
            || cooldown != FrontlineLabsCooldownArm.Ticking
            || stanceGround == FrontlineLabsStanceGroundArm.Free
            || capture != FrontlineLabsCaptureArm.Channel
            || economy != FrontlineLabsEconomyArm.Scrap
            || horizon != FrontlineLabsHorizonArm.Long)
        {
            return string.Empty;
        }
        // The shape resolves exactly as the keel family's does: the fan is
        // inert-omitted without a striker, and the fabricator cells carry the
        // wane variant, so those two facts name the three shapes.
        bool legion = roster == FrontlineLabsRosterArm.Legion;
        if (volley == FrontlineLabsVolleyArm.Salvo)
            return legion ? "warpath" : "vigil";
        if (fiveSlots == FrontlineLabsFiveSlotVariant.Wane)
            return legion ? "horde" : "warren";
        return stanceGround == FrontlineLabsStanceGroundArm.Open
            ? legion ? "stockade" : "bastille"
            : string.Empty;
    }

    /// <summary>
    /// The plain per-factor spelling of one economy level, used wherever the
    /// cell has no registered composite. The control level always spells
    /// itself, because a control that shared an identity with the arm it
    /// controls would be unreadable in the evidence.
    /// </summary>
    private static string EconomyArmToken(
        FrontlineLabsEconomyArm economy) =>
        economy switch
        {
            FrontlineLabsEconomyArm.Scrap =>
                FrontlineLabsScrapEconomy.ArmToken,
            // The control never takes a registered composite: an identity it
            // shared with the arm it controls would be unreadable in the
            // evidence. It spells `flat` rather than the flag's own
            // `scrap-flat` because the composite it appends to already names
            // the economy, and the six extra characters do not fit beside the
            // worst class pair.
            FrontlineLabsEconomyArm.ScrapFlat =>
                FrontlineLabsScrapEconomy.FlatArmToken,
            _ => throw new ArgumentOutOfRangeException(nameof(economy)),
        };

    /// <summary>
    /// Whether this economy level declares the <c>invest</c> verb at all. The
    /// control level's whole point is that it does not: the bank buys by
    /// itself, so the action never enters the catalog and no form ever offers
    /// it.
    /// </summary>
    private static bool DeclaresInvestAction(
        FrontlineLabsEconomyArm economy) =>
        economy == FrontlineLabsEconomyArm.Scrap;

    /// <summary>The <c>invest</c> catalog entry.</summary>
    private static ActorActionDefinition InvestAction() =>
        new(
            PublicActionIds.Invest,
            PublicActionCodes.Invest,
            ActorActionKind.ModeInvestment,
            [ActorActionParameterKind.UpgradeTrack]);

    /// <summary>
    /// The verb appended to every form's allowed actions under the economy
    /// arm, or nothing. Any live body may cast it, from any tile: making it
    /// Prime-only would add a denial vector — freeze their economy by killing
    /// one body — which is a gotcha rather than a decision, and requiring a
    /// forge tile would double-tax an errand that already costs a round trip.
    /// </summary>
    private static string[] InvestActionIds(
        FrontlineLabsEconomyArm economy) =>
        DeclaresInvestAction(economy)
            ? [PublicActionIds.Invest]
            : [];

    /// <summary>
    /// The rules-side arm tokens for one cell: a registered composite
    /// identity when the combination has one, otherwise the per-factor
    /// spelling in the declared order (pendulum, skills, bend).
    /// </summary>
    private static string[] ArmTokens(
        FrontlineLabsPendulumArm pendulum,
        FrontlineLabsSkillKit skills,
        FrontlineLabsBendEnvelopeArm bendEnvelope,
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classes,
        bool composed)
    {
        if (CompositeArmToken(pendulum, skills, bendEnvelope, classes)
            is { Length: > 0 } registered)
        {
            return [registered];
        }
        return
        [
            .. PendulumToken(pendulum, composed) is { Length: > 0 } arm
                ? new[] { arm }
                : [],
            .. SkillsToken(skills) is { Length: > 0 } kit
                ? new[] { kit }
                : [],
            .. bendEnvelope == FrontlineLabsBendEnvelopeArm.StrikerOnly
                ? []
                : new[] { BendEnvelopeToken },
        ];
    }

    /// <summary>
    /// The phase-2 composite identities (DECISIONS #169). Every phase-2 cell
    /// is keel plus some of {the class-skill kit, the universal bend}, and the
    /// per-factor spelling of even the smallest of those overflows the
    /// canonical ID budget in the worst class cell — <c>keel-bend</c> beside
    /// <c>fabricator-vs-fabricator</c> and <c>facing-locked</c> needs 65 of
    /// 64 characters, and the full candidate game needs 74. So the three
    /// combinations the factorial actually runs are registered under one
    /// token each, exactly as <c>keel</c> itself was:
    /// <list type="bullet">
    /// <item><c>helm</c> — keel plus the whole kit. The keel holds the course
    /// and the helm is what the crew steers with, which is what the per-class
    /// verbs are.</item>
    /// <item><c>veer</c> — keel plus the universal bend envelope. Every
    /// class's mobile gun may now bend its bolt, so every bolt may veer.</item>
    /// <item><c>rig</c> — keel plus the kit plus the universal bend: the whole
    /// working rig, and the phase-2 candidate game.</item>
    /// </list>
    /// The kit resolves per class exactly as <c>--skills kit</c> already does:
    /// an arm carries only the skills whose owning class is present, so the
    /// registered token means "every skill this cell can carry", and on
    /// <c>fabricator-vs-fabricator</c> that is <c>slot5</c> alone. The name is
    /// a property of the combination rather than of how it was spelled, so
    /// asking for the whole kit and asking for exactly that cell's skills are
    /// the same content-identified ruleset — which is also why a PARTIAL kit
    /// gets no registered name and keeps spelling itself out.
    /// </summary>
    private static string CompositeArmToken(
        FrontlineLabsPendulumArm pendulum,
        FrontlineLabsSkillKit skills,
        FrontlineLabsBendEnvelopeArm bendEnvelope,
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classes)
    {
        // Every registered composite is keel-based and class-composed: the
        // kit and the bend envelope are both class capabilities, so neither
        // exists without a pair.
        if (pendulum != AllPendulumArms || classes is not { } pair)
            return string.Empty;

        bool wholeKit =
            skills == (pair.TeamZero.Skill | pair.TeamOne.Skill);
        bool universalBend =
            bendEnvelope == FrontlineLabsBendEnvelopeArm.Universal;
        return (wholeKit, skills, universalBend) switch
        {
            (true, _, false) => "helm",
            (false, FrontlineLabsSkillKit.None, true) => "veer",
            (true, _, true) => "rig",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// The registered levels get their pre-registration names; any other
    /// combination joins its per-factor tokens in the declared order. The
    /// name is a property of the combination, not of how it was spelled, so
    /// a registered level and its explicit per-factor spelling are the same
    /// content-identified ruleset.
    /// </summary>
    private static string PendulumToken(
        FrontlineLabsPendulumArm pendulum,
        bool composed) =>
        pendulum switch
        {
            FrontlineLabsPendulumArm.None => string.Empty,
            FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ForwardRally => "ratchet",
            FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ForwardRally
                | FrontlineLabsPendulumArm.ContestMajority =>
                composed ? "contest" : "ratchet-contest",
            // Phase 1b (DECISIONS #166): every built counterweight at once.
            // The keel is what stops a hull swinging, which is the whole
            // claim of the level — and four characters keep the worst class
            // cell (`fabricator-vs-fabricator` plus `facing-locked`) inside
            // the 64-character canonical budget at 60, where the per-factor
            // spelling of the same four needs 83. One token in both
            // positions: a registered name that changed under composition
            // would make the spelled and named forms diverge for no gain.
            AllPendulumArms => "keel",
            // The keel minus its forward rally (owner ruling, next round).
            // A keel is the counterweight that also carries every arrival to
            // the front for free; take that away and what is left holding the
            // same shape is the `hull` — four characters, exactly like keel,
            // so no cell that could spell one can fail to spell the other.
            HullPendulumArms => "hull",
            _ => string.Join(
                "-",
                new[]
                {
                    (FrontlineLabsPendulumArm.StickyFrontline,
                        composed ? "sticky" : "sticky-frontline"),
                    (FrontlineLabsPendulumArm.ForwardRally,
                        composed ? "rally" : "forward-rally"),
                    (FrontlineLabsPendulumArm.ContestMajority,
                        composed ? "majority" : "contest-majority"),
                    (FrontlineLabsPendulumArm.EnemySoleDecay,
                        composed ? "decay" : "enemy-sole-decay"),
                }
                    .Where(entry => pendulum.HasFlag(entry.Item1))
                    .Select(entry => entry.Item2)),
        };

    /// <summary>
    /// Skill tokens are always composed with a class pair (a skill has no
    /// meaning without its owning class), and a pair already spends most of
    /// the 64-character canonical-ID budget — so they are short, exactly as
    /// #156 shortened the coupling token for the same reason. A cell holds at
    /// most two classes and therefore at most two skills.
    /// </summary>
    private static string SkillsToken(FrontlineLabsSkillKit skills) =>
        string.Join(
            "-",
            Skills.Where(skill => skills.HasFlag(skill)).Select(SkillToken));

    /// <summary>
    /// The curve grammar is not a class capability, so it gets its own token
    /// beside the skills rather than joining them, and the striker-only
    /// baseline adds none. It is the codebase's own word for the mechanic.
    /// </summary>
    private const string BendEnvelopeToken = "bend";

    private static string SkillToken(FrontlineLabsSkillKit skill) =>
        skill switch
        {
            // The token names the BEHAVIOUR, not the silhouette, and it is
            // reminted whenever the behaviour changes — the arm that absorbed
            // was `shell`, the arm that returned the bolt was `parry`, and
            // neither is confusable with what stands here now. A prototype
            // volley squatted in its stance and was a `fan`; the adopted one
            // fires once and is automatically returned, which is a `cast`. A
            // prototype shell parried without end; the adopted one shatters on
            // its third deflection, which is a `break`. Both stay inside the
            // five characters #156 measured for the longest cell
            // (bulwark-vs-fabricator + slot5 + facing-locked), so the rename
            // costs no identity budget.
            FrontlineLabsSkillKit.StrikerVolley => "cast",
            FrontlineLabsSkillKit.BulwarkAegisShell => "break",
            FrontlineLabsSkillKit.FabricatorFiveSlots => "slot5",
            _ => throw new ArgumentOutOfRangeException(
                nameof(skill),
                skill,
                "Unknown Frontline Labs class skill."),
        };

    private static string NumbersToken(
        int captureThreshold,
        int primeRespawnTicks,
        bool composed,
        FrontlineLabsCaptureArm capture) =>
        string.Join(
            "-",
            new[]
            {
                // The channel carries its own baseline threshold as a PAIRED
                // factor, so the shipped 8 spells nothing extra and only a
                // channel-speed ablation level (a threshold that is not the
                // arm's own) mints a numbers token.
                captureThreshold == BaselineCaptureThreshold(capture)
                    ? string.Empty
                    : composed
                        ? $"c{captureThreshold}"
                        : $"capture-{captureThreshold}",
                primeRespawnTicks == DefaultPrimeRespawnTicks
                    ? string.Empty
                    : composed
                        ? $"r{primeRespawnTicks}"
                        : $"respawn-{primeRespawnTicks}",
            }.Where(token => token.Length > 0));

    /// <summary>
    /// The capture threshold one capture arm treats as its own baseline. The
    /// channel's is the <c>channel-speed</c> factor's 8; every other arm's is
    /// the hosted contract's 15.
    /// </summary>
    public static int BaselineCaptureThreshold(
        FrontlineLabsCaptureArm capture) =>
        capture switch
        {
            FrontlineLabsCaptureArm.Frozen => DefaultCaptureThreshold,
            FrontlineLabsCaptureArm.Channel => ChannelCaptureThreshold,
            _ => throw new ArgumentOutOfRangeException(
                nameof(capture),
                capture,
                "Unknown Frontline Labs capture arm."),
        };

    /// <summary>
    /// Content-identified ruleset ID for a class pair, optionally composed
    /// with a movement-coupling arm. A PreserveFacing pair keeps the exact
    /// historical <c>-experiment-classes-</c> identity byte for byte. A
    /// coupled pair drops that segment because canonical IDs are capped at 64
    /// characters and the longest pair plus the longest coupling token would
    /// not fit (DECISIONS #156).
    /// </summary>
    private static string ClassesRulesetId(
        FrontlineLabsClassDefinition teamZeroClass,
        FrontlineLabsClassDefinition teamOneClass,
        ActorMovementFacingCoupling movementCoupling) =>
        movementCoupling == ActorMovementFacingCoupling.PreserveFacing
            ? $"{RulesetId}-experiment-classes-"
                + $"{teamZeroClass.Id}-vs-{teamOneClass.Id}"
            : $"{RulesetId}-classes-"
                + $"{teamZeroClass.Id}-vs-{teamOneClass.Id}-"
                + ComposedArmToken(movementCoupling);

    /// <summary>Ruleset-ID token for a standalone coupling arm.</summary>
    private static string ArmToken(
        ActorMovementFacingCoupling movementCoupling) =>
        movementCoupling switch
        {
            ActorMovementFacingCoupling.FaceMovementDirection =>
                "move-sets-facing",
            ActorMovementFacingCoupling.FacingLocked => "facing-locked",
            _ => throw new ArgumentOutOfRangeException(
                nameof(movementCoupling),
                movementCoupling,
                "Unknown movement facing coupling."),
        };

    /// <summary>
    /// Shorter token for composed arms, whose class pair already spends most
    /// of the 64-character canonical-ID budget.
    /// </summary>
    private static string ComposedArmToken(
        ActorMovementFacingCoupling movementCoupling) =>
        movementCoupling switch
        {
            ActorMovementFacingCoupling.FaceMovementDirection =>
                "sets-facing",
            ActorMovementFacingCoupling.FacingLocked => "facing-locked",
            _ => throw new ArgumentOutOfRangeException(
                nameof(movementCoupling),
                movementCoupling,
                "Unknown movement facing coupling."),
        };

    private static ActorResolvedMatchDefinition CreateResolved(
        string rulesetId,
        int captureThreshold,
        IEnumerable<FrontlineCaptureGainPhaseDefinition>?
            captureGainSchedule,
        bool enableMobilize,
        bool remoteFabrication,
        FrontlineCaptureDefinition.ControlPolicyKind controlPolicy =
            FrontlineCaptureDefinition.ControlPolicyKind
                .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral,
        bool oneBendShots = false,
        FrontlineLabsDuelMapArm duelMapArm =
            FrontlineLabsDuelMapArm.Current,
        bool automaticCompanions = false,
        string? seedProfileId = null,
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classes = null,
        ActorMovementFacingCoupling movementCoupling =
            ActorMovementFacingCoupling.PreserveFacing,
        FrontlineLabsPendulumArm pendulum = FrontlineLabsPendulumArm.None,
        int primeRespawnTicks = DefaultPrimeRespawnTicks,
        FrontlineLabsSkillKit skills = FrontlineLabsSkillKit.None,
        FrontlineLabsBendEnvelopeArm bendEnvelope =
            FrontlineLabsBendEnvelopeArm.StrikerOnly,
        FrontlineLabsStanceGroundArm stanceGround =
            FrontlineLabsStanceGroundArm.Strict,
        FrontlineLabsAimArm aim = FrontlineLabsAimArm.Straight,
        FrontlineLabsCooldownArm cooldown = FrontlineLabsCooldownArm.Frozen,
        FrontlineLabsVolleyArm volley = FrontlineLabsVolleyArm.Cast,
        FrontlineLabsSideObjectiveArm sideObjective =
            FrontlineLabsSideObjectiveArm.None,
        FrontlineLabsEconomyArm economy = FrontlineLabsEconomyArm.None,
        FrontlineLabsRosterArm roster = FrontlineLabsRosterArm.None,
        FrontlineLabsHorizonArm horizon = FrontlineLabsHorizonArm.Standard)
    {
        ActorRulesDefinition rules = CreateRules(
            rulesetId,
            captureThreshold,
            captureGainSchedule,
            enableMobilize,
            remoteFabrication,
            controlPolicy,
            oneBendShots,
            automaticCompanions,
            seedProfileId,
            classes,
            movementCoupling,
            pendulum,
            primeRespawnTicks,
            skills,
            bendEnvelope,
            stanceGround,
            aim,
            cooldown,
            volley,
            sideObjective,
            economy,
            horizon);
        ActorMapDefinition map = CreateMap(
            remoteFabrication,
            duelMapArm,
            automaticCompanions,
            classes: classes is not null,
            sideObjective: sideObjective,
            roster: roster);
        PublicMatchTopology topology =
            CreateTopology(classes, skills, roster);
        InitialDeploymentDefinition deployment =
            CreateInitialDeployment(classes, roster);

        return new ActorResolvedMatchDefinition(
            rules,
            map,
            new HeadToHeadMatchFormatDefinition(),
            topology,
            deployment,
            CreateLifecycleAssignments(
                automaticCompanions,
                classes,
                skills,
                roster),
            classes is { } classSelection
                ? classSelection.TeamZero.ExplicitForwardFabrication
                    || classSelection.TeamOne.ExplicitForwardFabrication
                    ? ClassesParticipantRegionAssignments()
                    : []
                : automaticCompanions
                    ? []
                    : CreateParticipantRegionAssignments(remoteFabrication),
            new FrontlineActorModeMapBindingDefinition(
                [
                    "frontline-position-0",
                    "frontline-position-1",
                    "frontline-position-2",
                    "frontline-position-3",
                    "frontline-position-4",
                ],
                [
                    new FrontlineTeamAdvanceDefinition(
                        0,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardHigherIndex),
                    new FrontlineTeamAdvanceDefinition(
                        1,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardLowerIndex),
                ]),
            CreateCapabilityVersions());
    }

    /// <summary>
    /// Every labs ruleset — hosted v1 and every local arm — resolves on the one
    /// generic capability profile. A class arm differs from a class-free arm in
    /// its content, not its generation: the canonical topology gains a
    /// <c>classId</c> only where classes are declared (#156), so a class-free
    /// arm keeps byte-identical topology and match fingerprints.
    /// </summary>
    private static ActorMatchCapabilityVersions CreateCapabilityVersions() =>
        new(
            contractProfileId: "generic-actor-match-2",
            runtimeProtocolVersion: "1.0",
            runtimeConfigurationVersion: "1.0",
            runtimeContractVersion: 2,
            matchStartSchemaVersion: 2,
            observationSchemaVersion: 2,
            decisionSchemaVersion: 2,
            matchContractSchemaVersion: 2);

    private static ActorRulesDefinition CreateRules(
        string rulesetId,
        int captureThreshold,
        IEnumerable<FrontlineCaptureGainPhaseDefinition>?
            captureGainSchedule,
        bool enableMobilize,
        bool remoteFabrication,
        FrontlineCaptureDefinition.ControlPolicyKind controlPolicy,
        bool oneBendShots,
        bool automaticCompanions,
        string? seedProfileId,
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classes,
        ActorMovementFacingCoupling movementCoupling,
        FrontlineLabsPendulumArm pendulum,
        int primeRespawnTicks,
        FrontlineLabsSkillKit skills,
        FrontlineLabsBendEnvelopeArm bendEnvelope,
        FrontlineLabsStanceGroundArm stanceGround =
            FrontlineLabsStanceGroundArm.Strict,
        FrontlineLabsAimArm aim = FrontlineLabsAimArm.Straight,
        FrontlineLabsCooldownArm cooldown = FrontlineLabsCooldownArm.Frozen,
        FrontlineLabsVolleyArm volley = FrontlineLabsVolleyArm.Cast,
        FrontlineLabsSideObjectiveArm sideObjective =
            FrontlineLabsSideObjectiveArm.None,
        FrontlineLabsEconomyArm economy = FrontlineLabsEconomyArm.None,
        FrontlineLabsHorizonArm horizon = FrontlineLabsHorizonArm.Standard)
    {
        var movement = new ActorMovementProfileDefinition(
            GroundMovementId,
            ActorMovementLayer.Ground,
            movementCoupling);
        if (classes is { } classPair)
        {
            return CreateClassesRules(
                rulesetId,
                captureThreshold,
                seedProfileId,
                classPair,
                movement,
                pendulum,
                primeRespawnTicks,
                skills,
                bendEnvelope,
                stanceGround,
                aim,
                cooldown,
                volley,
                sideObjective,
                controlPolicy,
                economy,
                horizon);
        }
        ActorVisionProfileDefinition mobileVision = Vision(
            MobileVisionId,
            ActorVisionShape.FacingQuadrant,
            omnidirectionalProximityRange: 1);
        ActorVisionProfileDefinition turretVision = Vision(
            TurretVisionId,
            ActorVisionShape.Omnidirectional,
            omnidirectionalProximityRange: 6);
        var projectile = new ActorProjectileDefinition(
            ActorProjectileMode.Discrete,
            damagePerHit: 1,
            maxTravelTiles: 8,
            ticksPerAdvance: 1,
            tilesPerAdvance: 2,
            launchTiles: 1,
            advancesOnLaunchTick: false,
            damageAppliedSimultaneously: true,
            diagonalCornersMustBeClear: true);
        var mobileAttack = new ActorAttackProfileDefinition(
            MobileAttackId,
            omnidirectionalAim: false,
            projectile,
            cooldownTicks: 2,
            maxEnergy: 0,
            attackEnergyCost: 0,
            energyRegenerationIntervalTicks: 0,
            energyRegenerationAmount: 0,
            ShotProgram(
                enabled: true,
                oneBendOnly: oneBendShots));
        var turretAttack = new ActorAttackProfileDefinition(
            TurretAttackId,
            omnidirectionalAim: true,
            projectile,
            cooldownTicks: 1,
            maxEnergy: 0,
            attackEnergyCost: 0,
            energyRegenerationIntervalTicks: 0,
            energyRegenerationAmount: 0,
            ShotProgram(
                enabled: false,
                oneBendOnly: false));
        ActorTransitionWindupDefinition anchorWindup = Windup(
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate);
        ActorTransitionWindupDefinition splitWindup = Windup(
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration);
        string[] investActions = InvestActionIds(economy);
        string[] turretActions = enableMobilize
            ?
            [
                "wait",
                PublicActionIds.ShootDirection,
                MobilizeActionId,
                .. investActions,
            ]
            :
            [
                "wait",
                PublicActionIds.ShootDirection,
                .. investActions,
            ];
        var actions = new List<ActorActionDefinition>
        {
            new(
                "wait",
                0,
                ActorActionKind.Wait,
                []),
            new(
                "move",
                1,
                ActorActionKind.Movement,
                [ActorActionParameterKind.Direction]),
            new(
                "rotate",
                2,
                ActorActionKind.Rotation,
                [ActorActionParameterKind.Direction]),
            new(
                "shoot",
                4,
                ActorActionKind.Attack,
                [ActorActionParameterKind.ShotProgram]),
            new(
                "transform",
                PublicActionCodes.Transform,
                ActorActionKind.SameLifeTransition,
                [ActorActionParameterKind.FormTarget]),
            new(
                PublicActionIds.ShootDirection,
                PublicActionCodes.ShootDirection,
                ActorActionKind.Attack,
                [ActorActionParameterKind.ProjectileHeading]),
        };
        if (!automaticCompanions)
        {
            actions.Add(
                new ActorActionDefinition(
                    "fabricate",
                    PublicActionCodes.Fabricate,
                    ActorActionKind.Fabrication,
                    [ActorActionParameterKind.UnitTarget]));
            actions.Add(
                new ActorActionDefinition(
                    "split",
                    103,
                    ActorActionKind.Replication,
                    []));
        }
        if (enableMobilize)
        {
            actions.Add(
                new ActorActionDefinition(
                    MobilizeActionId,
                    104,
                    ActorActionKind.SameLifeTransition,
                    []));
        }
        if (DeclaresInvestAction(economy))
            actions.Add(InvestAction());
        var sameLifeTransitions =
            new List<ActorSameLifeTransitionDefinition>
            {
                new ActorFormTransitionDefinition(
                    "anchor-child",
                    "transform",
                    ChildFormId,
                    TurretFormId,
                    anchorWindup,
                    ActorSameLifeTransitionDefinition.MemoryContinuityKind
                        .PreservePrivateMemory,
                    new ActorSameLifeHealthDefinition(
                        ActorSameLifeHealthDefinition.HealthPolicyKind
                            .AddFlatCappedToTargetMaximum,
                        flatHealthGain: 2),
                    ActorSameLifeCombatStateDefinition
                        .PreserveWithoutRefillV1,
                    new ActorSameLifePlacementDefinition(
                        ActorSameLifePlacementDefinition
                            .PositionContinuityKind.SameOccupiedGroundTile,
                        ActorSameLifePlacementDefinition
                            .LegalityEvaluationKind
                            .QueueAndCompletionTileTags,
                        requiredTileTags: [],
                        forbiddenTileTags:
                        [
                            ActorMapTileTagDefinition.TileTagKind
                                .TransitionPlacementForbidden,
                        ],
                        ActorSameLifePlacementDefinition
                            .FailedCompletionKind
                            .CancelAndRemainInSourceForm),
                    irreversibleForLife: !enableMobilize),
            };
        if (enableMobilize)
        {
            sameLifeTransitions.Add(
                new ActorFormTransitionDefinition(
                    "mobilize-child",
                    MobilizeActionId,
                    TurretFormId,
                    ChildFormId,
                    anchorWindup,
                    ActorSameLifeTransitionDefinition.MemoryContinuityKind
                        .PreservePrivateMemory,
                    new ActorSameLifeHealthDefinition(
                        ActorSameLifeHealthDefinition.HealthPolicyKind
                            .PreserveCurrentCappedToTargetMaximum,
                        flatHealthGain: 0),
                    ActorSameLifeCombatStateDefinition
                        .PreserveWithoutRefillV1,
                    new ActorSameLifePlacementDefinition(
                        ActorSameLifePlacementDefinition
                            .PositionContinuityKind.SameOccupiedGroundTile,
                        ActorSameLifePlacementDefinition
                            .LegalityEvaluationKind
                            .QueueAndCompletionTileTags,
                        requiredTileTags: [],
                        forbiddenTileTags: [],
                        ActorSameLifePlacementDefinition
                            .FailedCompletionKind
                            .CancelAndRemainInSourceForm),
                    irreversibleForLife: true));
        }

        return BuildRules(
            rulesetId,
            captureThreshold,
            captureGainSchedule,
            controlPolicy,
            pendulum,
            seedProfileId,
            new ActorLifecycleDefinition(
                [
                    new ActorLifecycleProfileDefinition(
                        PrimeLifecycleId,
                        ActorLifecycleProfileDefinition
                            .DestructionPolicyKind.AutomaticRespawn,
                        primeRespawnTicks,
                        automaticReturnFormId: PrimeFormId),
                    new ActorLifecycleProfileDefinition(
                        ChildLifecycleId,
                        automaticCompanions
                            ? ActorLifecycleProfileDefinition
                                .DestructionPolicyKind.AutomaticRespawn
                            : ActorLifecycleProfileDefinition
                                .DestructionPolicyKind
                                .ReadyForExplicitFabrication,
                        delayTicks: 30,
                        automaticReturnFormId:
                            automaticCompanions ? ChildFormId : null),
                ],
                AutomaticReturnPlacement(pendulum, sideObjective)),
            [
                new ActorFormDefinition(
                    PrimeFormId,
                    maxHealth: 3,
                    movement.Id,
                    mobileVision.Id,
                    mobileAttack.Id,
                    objectiveWeight: 1,
                    automaticCompanions
                        ?
                        [
                            "wait",
                            "move",
                            "rotate",
                            "shoot",
                            .. investActions,
                        ]
                        :
                        [
                            "wait",
                            "move",
                            "rotate",
                            "shoot",
                            "fabricate",
                            "split",
                            .. investActions,
                        ]),
                new ActorFormDefinition(
                    ChildFormId,
                    maxHealth: 3,
                    movement.Id,
                    mobileVision.Id,
                    mobileAttack.Id,
                    objectiveWeight: 1,
                    [
                        "wait",
                        "move",
                        "rotate",
                        "shoot",
                        "transform",
                        .. investActions,
                    ]),
                new ActorFormDefinition(
                    ReplicaFormId,
                    maxHealth: 3,
                    movement.Id,
                    mobileVision.Id,
                    mobileAttack.Id,
                    objectiveWeight: 1,
                    ["wait", "move", "rotate", "shoot", .. investActions]),
                new ActorFormDefinition(
                    TurretFormId,
                    maxHealth: 5,
                    movement.Id,
                    turretVision.Id,
                    turretAttack.Id,
                    objectiveWeight: 0,
                    turretActions),
            ],
            [movement],
            [mobileVision, turretVision],
            [mobileAttack, turretAttack],
            actions,
            automaticCompanions
                ? []
                : [
                new BoundedChildFabricationDefinition(
                    "fabricate-child",
                    "fabricate",
                    [PrimeFormId],
                    ChildFormId,
                    FabricationSourceRoleId,
                    FabricationOutputRoleId,
                    requiredSourceTileTags:
                    remoteFabrication
                        ? []
                        : [
                            ActorMapTileTagDefinition.TileTagKind
                                .SpawnProtected,
                        ],
                    requiredOutputTileTags:
                    [
                        ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
                    ],
                    forbiddenOutputTileTags: [],
                    candidateOffsets: remoteFabrication
                        ? RemoteFabricationCandidateOffsets()
                        : FabricationCandidateOffsets(),
                    new ActorFabricationDelayDefinition(durationTicks: 1),
                    ActorActionRejectionResult.Blocked),
                ],
            sameLifeTransitions,
            automaticCompanions
                ? []
                : [
                new SplitReplicationTransitionDefinition(
                    "split-prime",
                    "split",
                    [PrimeFormId],
                    ReplicaFormId,
                    descendantCount: 2,
                    maxSourceGeneration: 0,
                    requireNoPriorSameLifeTransition: true,
                    new ActorReplicationHealthDefinition(
                        ActorReplicationHealthDefinition.DistributionKind
                            .DivideCurrentHealthEquallyFloor,
                        minimumHealthPerDescendant: 1,
                        ActorReplicationHealthDefinition.RemainderKind.Discard),
                    candidateOffsets:
                    [
                        new ActorRelativePositionOffset(0, -1),
                        new ActorRelativePositionOffset(0, 1),
                        new ActorRelativePositionOffset(-1, 0),
                        new ActorRelativePositionOffset(1, 0),
                    ],
                    splitWindup),
                ],
            cooldown: FrontlineLabsCooldownArm.Frozen,
            sideObjective: sideObjective);
    }

    /// <summary>
    /// The single assembly point for every Labs arm's rules: limits, seed
    /// mechanics, mode, perception, collision, and tick resolution are
    /// invariant across arms and exist only here, so a new arm cannot drift
    /// them.
    /// </summary>
    private static ActorRulesDefinition BuildRules(
        string rulesetId,
        int captureThreshold,
        IEnumerable<FrontlineCaptureGainPhaseDefinition>?
            captureGainSchedule,
        FrontlineCaptureDefinition.ControlPolicyKind controlPolicy,
        FrontlineLabsPendulumArm pendulum,
        string? seedProfileId,
        ActorLifecycleDefinition lifecycle,
        IEnumerable<ActorFormDefinition> forms,
        IEnumerable<ActorMovementProfileDefinition> movementProfiles,
        IEnumerable<ActorVisionProfileDefinition> visionProfiles,
        IEnumerable<ActorAttackProfileDefinition> attackProfiles,
        IEnumerable<ActorActionDefinition> actions,
        IEnumerable<ActorFabricationTransitionDefinition>
            fabricationTransitions,
        IEnumerable<ActorSameLifeTransitionDefinition> sameLifeTransitions,
        IEnumerable<ActorReplicationTransitionDefinition>
            replicationTransitions,
        FrontlineLabsCooldownArm cooldown =
            FrontlineLabsCooldownArm.Frozen,
        FrontlineLabsSideObjectiveArm sideObjective =
            FrontlineLabsSideObjectiveArm.None,
        FrontlineLabsEconomyArm economy = FrontlineLabsEconomyArm.None,
        FrontlineLabsHorizonArm horizon = FrontlineLabsHorizonArm.Standard) =>
        new(
            rulesetId,
            new ActorRulesLimits(
                MaxTicks(horizon),
                new ActorRuntimeFaultDefinition(
                    faultsAllowedBeforeDisqualification: 0)),
            new ActorSeedMechanicsDefinition(
                seedProfileId ?? rulesetId,
                ActorSeedMechanicsDefinition.SeedDerivationKind
                    .MatchSeedProfileTeamUnitLifeMix64V1,
                ActorSeedMechanicsDefinition.LifeIdentityAssignmentKind
                    .PerStableUnitMonotonicStartingAtZero,
                ActorSeedMechanicsDefinition.RuntimeLifetimeKind
                    .FreshRuntimePerLife,
                ActorSeedMechanicsDefinition.PrivateMemoryKind
                    .IsolatedPerRuntime),
            new FrontlineGameModeDefinition(
                new FrontlineVictoryDefinition(
                    pushesToBreach: 3,
                    [
                        new ScoreRankingDefinition(
                            ScoreChannelDefinition.ChannelKind
                                .TerritorialProgress,
                            ScoreRankingDefinition.SortDirection.HigherWins),
                    ]),
                [
                    new ScoreChannelDefinition(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress),
                ],
                frontlinePositionCount: 5,
                new FrontlineCaptureDefinition(
                    threshold: captureThreshold,
                    gainPerSoleTeamTick: 1,
                    decayAmount: 1,
                    decayIntervalTicks: 2,
                    redeployPauseTicks: 5,
                    gainSchedule: captureGainSchedule,
                    controlPolicy,
                    DecayClock(pendulum),
                    RedeployPolicy(pendulum),
                    RatchetHoldTicks(pendulum),
                    // The channel's three settings travel with the channel
                    // policy and are absent everywhere else, so a ruleset
                    // that does not channel writes no bytes for them.
                    ChannelSetting(
                        controlPolicy,
                        ChannelStationaryGainMultiplierCap),
                    ChannelSetting(
                        controlPolicy,
                        ChannelOpposingErosionMultiplier),
                    controlPolicy
                        == FrontlineCaptureDefinition.ControlPolicyKind
                            .StationaryClaimWeightVersusTotalDenialWeightScalesGainCappedOppositionErodesAtMultipleThenBuilds
                        ? FrontlineClaimInterruptDefinition
                            .DamageRevertsWork
                        : null),
                SecondaryControl(sideObjective),
                FrontlineLabsScrapEconomy.For(economy)),
            lifecycle,
            forms,
            movementProfiles,
            visionProfiles,
            attackProfiles,
            actions,
            fabricationTransitions,
            sameLifeTransitions,
            replicationTransitions,
            new ActorTeamPerceptionDefinition(
                ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion),
            new ActorCollisionDefinition(
                actorsBlockWalls: true,
                actorsBlockActors: true,
                sameDestinationMovesBlockAll: true,
                swapMovesBlocked: true,
                followingVacatedActorAllowed: false,
                projectilesBlockMovement: true,
                movingOntoProjectileCausesHit: true,
                wallsConsumeProjectiles: true,
                projectilesIgnoreFiringLife: true,
                projectilesStopOnFirstEnemyActor: true,
                projectilesCollideWithProjectiles: false,
                ActorCollisionDefinition.AlliedProjectileContactKind
                    .PassThrough),
            new ActorTickResolutionDefinition(
                observationsUsePreTickState: true,
                decisionsResolveAsJointStep: true,
                ActorDamageResolutionDefinition.CanonicalJointV1,
                ActorTickResolutionDefinition.CreateSupportedPhases(),
                cooldown == FrontlineLabsCooldownArm.Ticking
                    ? ActorTickResolutionDefinition.CooldownClockKind
                        .AdvancesWithTime
                    : ActorTickResolutionDefinition.CooldownClockKind
                        .AdvancesOnlyWithAnArmedForm));

    /// <summary>
    /// Expands one or two class chassis into the complete per-class form,
    /// profile, route, and lifecycle catalog. Mirror pairs collapse to one
    /// class so a striker-vs-striker contract contains each catalog entry
    /// exactly once. Kinematics (movement, projectile speed, damage) and the
    /// turret's shared vision/attack stay identical across classes.
    /// </summary>
    private static ActorRulesDefinition CreateClassesRules(
        string rulesetId,
        int captureThreshold,
        string? seedProfileId,
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne) classes,
        ActorMovementProfileDefinition movement,
        FrontlineLabsPendulumArm pendulum,
        int primeRespawnTicks,
        FrontlineLabsSkillKit skills,
        FrontlineLabsBendEnvelopeArm bendEnvelope,
        FrontlineLabsStanceGroundArm stanceGround =
            FrontlineLabsStanceGroundArm.Strict,
        FrontlineLabsAimArm aim = FrontlineLabsAimArm.Straight,
        FrontlineLabsCooldownArm cooldown = FrontlineLabsCooldownArm.Frozen,
        FrontlineLabsVolleyArm volleyArm = FrontlineLabsVolleyArm.Cast,
        FrontlineLabsSideObjectiveArm sideObjective =
            FrontlineLabsSideObjectiveArm.None,
        FrontlineCaptureDefinition.ControlPolicyKind controlPolicy =
            FrontlineCaptureDefinition.ControlPolicyKind
                .BinaryPositiveWeightPerTeamNoStackingNonSoleAppliesConfiguredDecayOppositionErodesToNeutral,
        FrontlineLabsEconomyArm economy = FrontlineLabsEconomyArm.None,
        FrontlineLabsHorizonArm horizon = FrontlineLabsHorizonArm.Standard)
    {
        FrontlineLabsClassDefinition[] distinct =
            classes.TeamZero.Id == classes.TeamOne.Id
                ? [classes.TeamZero]
                : [classes.TeamZero, classes.TeamOne];
        // The curve grammar is a rules-wide factor rather than a class
        // capability, so it reads as one predicate over every chassis: a class
        // bends if it always did, or if this arm hands the grammar to
        // everyone. Depth stays per class either way.
        bool Bends(FrontlineLabsClassDefinition entry) =>
            entry.OneBendShotPrograms
            || bendEnvelope == FrontlineLabsBendEnvelopeArm.Universal;
        // A stance is a class skill selected by the arm, so "this class has a
        // stance here" is the one predicate the catalog builds from.
        bool HasStance(FrontlineLabsClassDefinition entry) =>
            entry.Skill is FrontlineLabsSkillKit.StrikerVolley
                or FrontlineLabsSkillKit.BulwarkAegisShell
            && skills.HasFlag(entry.Skill);
        bool HasVolley(FrontlineLabsClassDefinition entry) =>
            entry.Skill == FrontlineLabsSkillKit.StrikerVolley
            && skills.HasFlag(entry.Skill);
        bool HasShell(FrontlineLabsClassDefinition entry) =>
            entry.Skill == FrontlineLabsSkillKit.BulwarkAegisShell
            && skills.HasFlag(entry.Skill);
        ActorVisionProfileDefinition turretVision = Vision(
            TurretVisionId,
            ActorVisionShape.Omnidirectional,
            omnidirectionalProximityRange: 6);
        var turretAttack = new ActorAttackProfileDefinition(
            TurretAttackId,
            omnidirectionalAim: true,
            ClassProjectile(maxTravelTiles: 8),
            cooldownTicks: 1,
            maxEnergy: 0,
            attackEnergyCost: 0,
            energyRegenerationIntervalTicks: 0,
            energyRegenerationAmount: 0,
            ShotProgram(enabled: false, oneBendOnly: false));

        var actions = new List<ActorActionDefinition>
        {
            new("wait", 0, ActorActionKind.Wait, []),
            new(
                "move",
                1,
                ActorActionKind.Movement,
                [ActorActionParameterKind.Direction]),
            new(
                "rotate",
                2,
                ActorActionKind.Rotation,
                [ActorActionParameterKind.Direction]),
        };
        if (distinct.Any(Bends))
        {
            actions.Add(
                new ActorActionDefinition(
                    "shoot",
                    4,
                    ActorActionKind.Attack,
                    [ActorActionParameterKind.ShotProgram]));
        }
        if (distinct.Any(entry => entry.MayAnchor || HasStance(entry)))
        {
            actions.Add(
                new ActorActionDefinition(
                    "transform",
                    PublicActionCodes.Transform,
                    ActorActionKind.SameLifeTransition,
                    [ActorActionParameterKind.FormTarget]));
            actions.Add(
                new ActorActionDefinition(
                    MobilizeActionId,
                    104,
                    ActorActionKind.SameLifeTransition,
                    []));
        }
        if (distinct.Any(entry => entry.MayAnchor))
        {
            actions.Add(
                new ActorActionDefinition(
                    PublicActionIds.ShootDirection,
                    PublicActionCodes.ShootDirection,
                    ActorActionKind.Attack,
                    [ActorActionParameterKind.ProjectileHeading]));
        }
        if (distinct.Any(entry => entry.ExplicitForwardFabrication))
        {
            actions.Add(
                new ActorActionDefinition(
                    "fabricate",
                    PublicActionCodes.Fabricate,
                    ActorActionKind.Fabrication,
                    [ActorActionParameterKind.UnitTarget]));
        }
        // The volley stance fires straight (no shot programs on a special —
        // the slate's law), so it needs the parameterless action even in a
        // striker mirror where no chassis otherwise carries it.
        if (distinct.Any(entry => !Bends(entry) || HasVolley(entry)))
        {
            actions.Add(
                new ActorActionDefinition(
                    ShootStraightActionId,
                    ShootStraightActionCode,
                    ActorActionKind.Attack,
                    []));
        }
        string[] investActions = InvestActionIds(economy);
        if (DeclaresInvestAction(economy))
            actions.Add(InvestAction());

        var visions = new List<ActorVisionProfileDefinition>();
        var attacks = new List<ActorAttackProfileDefinition>();
        var forms = new List<ActorFormDefinition>();
        var lifecycleProfiles = new List<ActorLifecycleProfileDefinition>();
        var fabrications = new List<BoundedChildFabricationDefinition>();
        var sameLifeTransitions =
            new List<ActorSameLifeTransitionDefinition>();
        foreach (FrontlineLabsClassDefinition entry in distinct)
        {
            string shootActionId = Bends(entry)
                ? "shoot"
                : ShootStraightActionId;
            visions.Add(
                Vision(
                    entry.MobileVisionProfileId,
                    entry.MobileVisionShape,
                    entry.MobileOmnidirectionalProximityRange,
                    entry.MobileVisionRange));
            attacks.Add(
                new ActorAttackProfileDefinition(
                    entry.MobileAttackProfileId,
                    omnidirectionalAim: false,
                    ClassProjectile(entry.MobileMaxTravelTiles),
                    entry.MobileCooldownTicks,
                    maxEnergy: 0,
                    attackEnergyCost: 0,
                    energyRegenerationIntervalTicks: 0,
                    energyRegenerationAmount: 0,
                    ShotProgram(
                        enabled: Bends(entry),
                        oneBendOnly: true,
                        maxBendAfterTiles: entry.MobileMaxBendAfterTiles,
                        aimOffsets: aim == FrontlineLabsAimArm.Offset)));
            bool mayTransform = entry.MayAnchor || HasStance(entry);
            string[] primeActions =
            [
                "wait",
                "move",
                "rotate",
                shootActionId,
                .. mayTransform ? new[] { "transform" } : [],
                .. entry.ExplicitForwardFabrication
                    ? new[] { "fabricate" }
                    : [],
                .. investActions,
            ];
            string[] childActions =
            [
                "wait",
                "move",
                "rotate",
                shootActionId,
                .. mayTransform ? new[] { "transform" } : [],
                .. investActions,
            ];
            forms.Add(
                new ActorFormDefinition(
                    entry.PrimeFormId,
                    entry.PrimeMaxHealth,
                    movement.Id,
                    entry.MobileVisionProfileId,
                    entry.MobileAttackProfileId,
                    objectiveWeight: 1,
                    primeActions));
            forms.Add(
                new ActorFormDefinition(
                    entry.ChildFormId,
                    entry.ChildMaxHealth,
                    movement.Id,
                    entry.MobileVisionProfileId,
                    entry.MobileAttackProfileId,
                    objectiveWeight: 1,
                    childActions));
            if (entry.MayAnchor)
            {
                foreach (string turretFormId in new[]
                         {
                             entry.PrimeTurretFormId,
                             entry.ChildTurretFormId,
                         })
                {
                    forms.Add(
                        new ActorFormDefinition(
                            turretFormId,
                            entry.TurretMaxHealth,
                            movement.Id,
                            turretVision.Id,
                            turretAttack.Id,
                            objectiveWeight: 0,
                            [
                                "wait",
                                PublicActionIds.ShootDirection,
                                MobilizeActionId,
                                .. investActions,
                            ]));
                }
            }
            if (HasVolley(entry))
            {
                // VOLLEY: one gun, three simultaneous bolts down adjacent
                // 45-degree lanes, straight only, on a cadence meaningfully
                // slower than the mobile gun. Everything else — projectile
                // speed, damage, range — is the class's own bolt, so the arm
                // varies exactly width and tempo.
                // SALVO (#182): every bolt deals 2, the fan stops taxing
                // the shared gun counter (floor cadence), and frequency
                // moves to the entry route's slot-scoped cooldown below.
                attacks.Add(
                    new ActorAttackProfileDefinition(
                        entry.StanceAttackProfileId,
                        omnidirectionalAim: false,
                        volleyArm == FrontlineLabsVolleyArm.Salvo
                            ? ClassProjectile(
                                entry.MobileMaxTravelTiles,
                                damagePerHit: 2)
                            : ClassProjectile(entry.MobileMaxTravelTiles),
                        volleyArm == FrontlineLabsVolleyArm.Salvo
                            ? 1
                            : entry.VolleyCooldownTicks,
                        maxEnergy: 0,
                        attackEnergyCost: 0,
                        energyRegenerationIntervalTicks: 0,
                        energyRegenerationAmount: 0,
                        ShotProgram(enabled: false, oneBendOnly: false),
                        new ActorAttackVolleyDefinition(
                            entry.VolleyProjectileCount,
                            ActorAttackVolleyDefinition.VolleySpreadKind
                                .SymmetricAdjacentHeadingFanAscendingSignedSectorOffset)));
            }
            if (HasStance(entry))
            {
                // The stance forfeits mobility and keeps objective weight 1 —
                // the deliberate half of the turret bargain the slate's design
                // guard demands a skill choose explicitly. The volley stance
                // keeps rotation so it stays aimable rather than a second
                // turret; the deflecting shell does NOT, because its arc locks
                // on entry (owner ruling: a weight-1 shield with a tracking
                // arc would be an invincible capturer, and flanking has to
                // stay real even 1v1). The protected quadrant is chosen before
                // the shield rises, in the mobile form.
                bool volley = HasVolley(entry);
                bool lockedArc = HasShell(entry);
                foreach ((string stanceFormId, int maxHealth) in new[]
                         {
                             (entry.PrimeStanceFormId, entry.PrimeMaxHealth),
                             (entry.ChildStanceFormId, entry.ChildMaxHealth),
                         })
                {
                    forms.Add(
                        new ActorFormDefinition(
                            stanceFormId,
                            maxHealth,
                            movement.Id,
                            entry.MobileVisionProfileId,
                            volley ? entry.StanceAttackProfileId : null,
                            objectiveWeight: 1,
                            [
                                "wait",
                                .. lockedArc ? [] : new[] { "rotate" },
                                .. volley
                                    ? new[] { ShootStraightActionId }
                                    : [],
                                MobilizeActionId,
                                .. investActions,
                            ],
                            lockedArc
                                ? ActorFormProjectileGuardKind
                                    .FacingQuadrantContactsDeflected
                                : ActorFormProjectileGuardKind.None));
                }
                bool salvoStance =
                    volleyArm == FrontlineLabsVolleyArm.Salvo
                    && entry.Skill == FrontlineLabsSkillKit.StrikerVolley;
                int entryCooldownTicks =
                    salvoStance ? SalvoEntryCooldownTicks : 0;
                // The salvo also sharpens delivery (owner ruling on top of
                // #182): entry drops to the 1-tick grammar every other
                // stance already uses — the fan's 2-tick public telegraph
                // was the worst telegraph-to-payoff ratio in the game.
                int entryWindupTicks = salvoStance
                    ? SalvoEntryWindupTicks
                    : entry.StanceEntryWindupTicks;
                sameLifeTransitions.Add(
                    StanceRoute(
                        $"{StanceRouteToken(entry)}-{entry.Id}-prime",
                        entry.PrimeFormId,
                        entry.PrimeStanceFormId,
                        entryWindupTicks,
                        stanceGround,
                        entryCooldownTicks));
                sameLifeTransitions.Add(
                    StanceRoute(
                        $"{StanceRouteToken(entry)}-{entry.Id}-child",
                        entry.ChildFormId,
                        entry.ChildStanceFormId,
                        entryWindupTicks,
                        stanceGround,
                        entryCooldownTicks));
                ActorAutomaticReturnTriggerDefinition automaticReturn =
                    StanceAutomaticReturn(entry);
                sameLifeTransitions.Add(
                    StanceReturnRoute(
                        $"unstance-{entry.Id}-prime",
                        entry.PrimeStanceFormId,
                        entry.PrimeFormId,
                        entry.StanceExitWindupTicks,
                        automaticReturn));
                sameLifeTransitions.Add(
                    StanceReturnRoute(
                        $"unstance-{entry.Id}-child",
                        entry.ChildStanceFormId,
                        entry.ChildFormId,
                        entry.StanceExitWindupTicks,
                        automaticReturn));
            }
            if (skills.HasFlag(FrontlineLabsSkillKit.FabricatorFiveSlots)
                && entry.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots)
            {
                lifecycleProfiles.Add(
                    new ActorLifecycleProfileDefinition(
                        entry.ExtraChildLifecycleProfileId,
                        entry.ExplicitForwardFabrication
                            ? ActorLifecycleProfileDefinition
                                .DestructionPolicyKind
                                .ReadyForExplicitFabrication
                            : ActorLifecycleProfileDefinition
                                .DestructionPolicyKind.AutomaticRespawn,
                        entry.ExtraChildRebuildDelayTicks,
                        automaticReturnFormId:
                            entry.ExplicitForwardFabrication
                                ? null
                                : entry.ChildFormId));
            }
            lifecycleProfiles.Add(
                new ActorLifecycleProfileDefinition(
                    entry.PrimeLifecycleProfileId,
                    ActorLifecycleProfileDefinition
                        .DestructionPolicyKind.AutomaticRespawn,
                    primeRespawnTicks,
                    automaticReturnFormId: entry.PrimeFormId));
            lifecycleProfiles.Add(
                new ActorLifecycleProfileDefinition(
                    entry.ChildLifecycleProfileId,
                    entry.ExplicitForwardFabrication
                        ? ActorLifecycleProfileDefinition
                            .DestructionPolicyKind.ReadyForExplicitFabrication
                        : ActorLifecycleProfileDefinition
                            .DestructionPolicyKind.AutomaticRespawn,
                    entry.ChildRebuildDelayTicks,
                    automaticReturnFormId: entry.ExplicitForwardFabrication
                        ? null
                        : entry.ChildFormId));
            if (entry.ExplicitForwardFabrication)
            {
                fabrications.Add(
                    new BoundedChildFabricationDefinition(
                        $"fabricate-{entry.Id}-child",
                        "fabricate",
                        [entry.PrimeFormId],
                        entry.ChildFormId,
                        FabricationSourceRoleId,
                        FabricationOutputRoleId,
                        requiredSourceTileTags: [],
                        requiredOutputTileTags: [],
                        forbiddenOutputTileTags:
                        [
                            ActorMapTileTagDefinition.TileTagKind
                                .SpawnProtected,
                        ],
                        FabricationCandidateOffsets(),
                        new ActorFabricationDelayDefinition(durationTicks: 1),
                        ActorActionRejectionResult.Blocked));
            }
            if (!entry.MayAnchor)
            {
                continue;
            }
            sameLifeTransitions.Add(
                AnchorRoute(
                    $"anchor-{entry.Id}-prime",
                    entry.PrimeFormId,
                    entry.PrimeTurretFormId,
                    entry.PrimeAnchorWindupTicks,
                    stanceGround));
            sameLifeTransitions.Add(
                AnchorRoute(
                    $"anchor-{entry.Id}-child",
                    entry.ChildFormId,
                    entry.ChildTurretFormId,
                    entry.ChildAnchorWindupTicks,
                    stanceGround));
            sameLifeTransitions.Add(
                MobilizeRoute(
                    $"mobilize-{entry.Id}-prime",
                    entry.PrimeTurretFormId,
                    entry.PrimeFormId,
                    stanceGround));
            sameLifeTransitions.Add(
                MobilizeRoute(
                    $"mobilize-{entry.Id}-child",
                    entry.ChildTurretFormId,
                    entry.ChildFormId,
                    stanceGround));
        }
        if (distinct.Any(entry => entry.MayAnchor))
        {
            visions.Add(turretVision);
            attacks.Add(turretAttack);
        }

        return BuildRules(
            rulesetId,
            captureThreshold,
            captureGainSchedule: null,
            controlPolicy,
            pendulum,
            seedProfileId,
            new ActorLifecycleDefinition(
                lifecycleProfiles,
                AutomaticReturnPlacement(pendulum, sideObjective)),
            forms,
            [movement],
            visions,
            attacks,
            actions,
            fabrications,
            sameLifeTransitions,
            replicationTransitions: [],
            cooldown,
            sideObjective,
            economy,
            horizon);
    }

    private static ActorFormTransitionDefinition AnchorRoute(
        string transitionId,
        string sourceFormId,
        string turretFormId,
        int windupTicks,
        FrontlineLabsStanceGroundArm stanceGround =
            FrontlineLabsStanceGroundArm.Strict) =>
        new(
            transitionId,
            "transform",
            sourceFormId,
            turretFormId,
            Windup(
                ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate,
                windupTicks),
            ActorSameLifeTransitionDefinition.MemoryContinuityKind
                .PreservePrivateMemory,
            // Under the open game the turret is a cycle, so the entry heal
            // dies (a healing entry on a repeatable route is a repair loop)
            // and health maps proportionally instead — full stays full,
            // partial pays the floor.
            stanceGround == FrontlineLabsStanceGroundArm.Open
                ? new ActorSameLifeHealthDefinition(
                    ActorSameLifeHealthDefinition.HealthPolicyKind
                        .PreserveRatioFloorMinimumOne,
                    flatHealthGain: 0)
                : new ActorSameLifeHealthDefinition(
                    ActorSameLifeHealthDefinition.HealthPolicyKind
                        .AddFlatCappedToTargetMaximum,
                    flatHealthGain: 2),
            ActorSameLifeCombatStateDefinition.PreserveWithoutRefillV1,
            new ActorSameLifePlacementDefinition(
                ActorSameLifePlacementDefinition
                    .PositionContinuityKind.SameOccupiedGroundTile,
                ActorSameLifePlacementDefinition
                    .LegalityEvaluationKind.QueueAndCompletionTileTags,
                requiredTileTags: [],
                forbiddenTileTags:
                    stanceGround == FrontlineLabsStanceGroundArm.Open
                        ? []
                        :
                        [
                            ActorMapTileTagDefinition.TileTagKind
                                .TransitionPlacementForbidden,
                        ],
                ActorSameLifePlacementDefinition
                    .FailedCompletionKind.CancelAndRemainInSourceForm),
            irreversibleForLife: false);

    private static string StanceRouteToken(
        FrontlineLabsClassDefinition entry) =>
        entry.Skill switch
        {
            FrontlineLabsSkillKit.StrikerVolley => "volley",
            FrontlineLabsSkillKit.BulwarkAegisShell => "shell",
            _ => throw new ArgumentOutOfRangeException(
                nameof(entry),
                entry.Skill,
                "This class owns no same-life stance skill."),
        };

    /// <summary>
    /// Entering a class stance. Unlike Anchor this grants no health: the
    /// stance is a public commitment priced in windup and forfeited mobility,
    /// and a healing entry would turn a reversible cycle into a repair loop.
    /// Reversible for the life, so the cycle really is a cycle.
    /// </summary>
    /// <summary>The salvo's entry price (#182): slot-scoped, death-proof,
    /// the first declared consumer of the route-cooldown capability.</summary>
    private const int SalvoEntryCooldownTicks = 8;

    /// <summary>
    /// The salvo's sharpened delivery (#183): the fan enters on the same
    /// 1-tick grammar as every other stance, ending the volley's status as
    /// the game's only 2-tick public telegraph.
    /// </summary>
    private const int SalvoEntryWindupTicks = 1;

    private static ActorFormTransitionDefinition StanceRoute(
        string transitionId,
        string sourceFormId,
        string stanceFormId,
        int windupTicks,
        FrontlineLabsStanceGroundArm stanceGround =
            FrontlineLabsStanceGroundArm.Strict,
        int cooldownTicks = 0) =>
        new(
            transitionId,
            "transform",
            sourceFormId,
            stanceFormId,
            Windup(
                ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate,
                windupTicks),
            ActorSameLifeTransitionDefinition.MemoryContinuityKind
                .PreservePrivateMemory,
            new ActorSameLifeHealthDefinition(
                ActorSameLifeHealthDefinition.HealthPolicyKind
                    .PreserveCurrentCappedToTargetMaximum,
                flatHealthGain: 0),
            ActorSameLifeCombatStateDefinition.PreserveWithoutRefillV1,
            new ActorSameLifePlacementDefinition(
                ActorSameLifePlacementDefinition
                    .PositionContinuityKind.SameOccupiedGroundTile,
                ActorSameLifePlacementDefinition
                    .LegalityEvaluationKind.QueueAndCompletionTileTags,
                requiredTileTags: [],
                // The free stance-ground arm drops the tag kind from the
                // SKILL stances only; turret anchor routes keep it (the
                // weight-zero fortress-on-point question stays closed).
                forbiddenTileTags:
                    stanceGround is FrontlineLabsStanceGroundArm.Free
                        or FrontlineLabsStanceGroundArm.Open
                        ? []
                        :
                        [
                            ActorMapTileTagDefinition.TileTagKind
                                .TransitionPlacementForbidden,
                        ],
                ActorSameLifePlacementDefinition
                    .FailedCompletionKind.CancelAndRemainInSourceForm),
            irreversibleForLife: false,
            automaticReturn: null,
            cooldownTicks: cooldownTicks);

    /// <summary>
    /// Leaving a class stance through the parameterless Mobilize. Reversible,
    /// which is what separates a stance from Anchor's once-per-life
    /// remobilization: the skill is a cooldown-shaped cycle, and its price is
    /// the two windups plus everything the stance cannot do.
    /// </summary>
    private static ActorFormTransitionDefinition StanceReturnRoute(
        string transitionId,
        string stanceFormId,
        string returnFormId,
        int windupTicks,
        ActorAutomaticReturnTriggerDefinition automaticReturn) =>
        new(
            transitionId,
            MobilizeActionId,
            stanceFormId,
            returnFormId,
            Windup(
                ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate,
                windupTicks),
            ActorSameLifeTransitionDefinition.MemoryContinuityKind
                .PreservePrivateMemory,
            new ActorSameLifeHealthDefinition(
                ActorSameLifeHealthDefinition.HealthPolicyKind
                    .PreserveCurrentCappedToTargetMaximum,
                flatHealthGain: 0),
            ActorSameLifeCombatStateDefinition.PreserveWithoutRefillV1,
            new ActorSameLifePlacementDefinition(
                ActorSameLifePlacementDefinition
                    .PositionContinuityKind.SameOccupiedGroundTile,
                ActorSameLifePlacementDefinition
                    .LegalityEvaluationKind.QueueAndCompletionTileTags,
                requiredTileTags: [],
                forbiddenTileTags: [],
                ActorSameLifePlacementDefinition
                    .FailedCompletionKind.CancelAndRemainInSourceForm),
            irreversibleForLife: false,
            automaticReturn);

    /// <summary>
    /// The adoption-grade half of both stances, and one primitive rather than
    /// two: the same threshold-triggered automatic return serves the striker's
    /// cast (fires-count 1 — the moment the fan launches the return begins, so
    /// artillery squatting is impossible by rule rather than by driver
    /// etiquette) and the bulwark's shield break (deflections-count 3 — the
    /// third bolt shatters the shield into a forced return, so indefinite
    /// deflection stops being an off-switch for ranged combat). Both are owner
    /// rulings; both spend the same exit windup the early manual exit spends.
    /// </summary>
    private static ActorAutomaticReturnTriggerDefinition StanceAutomaticReturn(
        FrontlineLabsClassDefinition entry) =>
        entry.Skill switch
        {
            FrontlineLabsSkillKit.StrikerVolley => new(
                ActorAutomaticReturnTriggerDefinition
                    .AutomaticReturnCounterKind
                    .AttacksIssuedSinceEnteringSourceForm,
                VolleyCastBudget),
            FrontlineLabsSkillKit.BulwarkAegisShell => new(
                ActorAutomaticReturnTriggerDefinition
                    .AutomaticReturnCounterKind
                    .ProjectilesDeflectedSinceEnteringSourceForm,
                ShieldBreakBudget),
            _ => throw new ArgumentOutOfRangeException(
                nameof(entry),
                entry.Skill,
                "This class owns no same-life stance skill."),
        };

    /// <summary>One fan per entry: the volley is a cast, not a stance.</summary>
    public const int VolleyCastBudget = 1;

    /// <summary>
    /// Bolts the aegis shell turns before it shatters. Counter-play is
    /// two-axis by design: go around the locked arc, or feed it three bolts —
    /// each with a return to sidestep — to buy the punish window.
    /// </summary>
    public const int ShieldBreakBudget = 3;

    private static ActorFormTransitionDefinition MobilizeRoute(
        string transitionId,
        string turretFormId,
        string returnFormId,
        FrontlineLabsStanceGroundArm stanceGround =
            FrontlineLabsStanceGroundArm.Strict) =>
        new(
            transitionId,
            MobilizeActionId,
            turretFormId,
            returnFormId,
            Windup(
                ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate),
            ActorSameLifeTransitionDefinition.MemoryContinuityKind
                .PreservePrivateMemory,
            // The ratio policy must hold in BOTH directions of a cycle:
            // preserve-capped on the way down would turn 5/7 into a full
            // 4/4 — a hidden heal every mobilize.
            stanceGround == FrontlineLabsStanceGroundArm.Open
                ? new ActorSameLifeHealthDefinition(
                    ActorSameLifeHealthDefinition.HealthPolicyKind
                        .PreserveRatioFloorMinimumOne,
                    flatHealthGain: 0)
                : new ActorSameLifeHealthDefinition(
                    ActorSameLifeHealthDefinition.HealthPolicyKind
                        .PreserveCurrentCappedToTargetMaximum,
                    flatHealthGain: 0),
            ActorSameLifeCombatStateDefinition.PreserveWithoutRefillV1,
            new ActorSameLifePlacementDefinition(
                ActorSameLifePlacementDefinition
                    .PositionContinuityKind.SameOccupiedGroundTile,
                ActorSameLifePlacementDefinition
                    .LegalityEvaluationKind.QueueAndCompletionTileTags,
                requiredTileTags: [],
                forbiddenTileTags: [],
                ActorSameLifePlacementDefinition
                    .FailedCompletionKind.CancelAndRemainInSourceForm),
            irreversibleForLife:
                stanceGround != FrontlineLabsStanceGroundArm.Open);

    private static ActorProjectileDefinition ClassProjectile(
        int maxTravelTiles,
        int damagePerHit = 1) =>
        new(
            ActorProjectileMode.Discrete,
            damagePerHit,
            maxTravelTiles,
            ticksPerAdvance: 1,
            tilesPerAdvance: 2,
            launchTiles: 1,
            advancesOnLaunchTick: false,
            damageAppliedSimultaneously: true,
            diagonalCornersMustBeClear: true);

    private static ActorVisionProfileDefinition Vision(
        string id,
        ActorVisionShape shape,
        int omnidirectionalProximityRange,
        int range = 6) =>
        new(
            id,
            range,
            ActorVisionDistanceMetric.Chebyshev,
            shape,
            omnidirectionalProximityRange,
            ActorLineOfSightModel.CornerStrictSupercover,
            hearingRadius: 8,
            hearingBearingSectors: 8,
            ActorHearingBearingModel
                .EightOctantsStrictTwoToOneCardinalV1,
            hearingDistanceBandUpperBounds: [2, 5],
            loudEventKinds:
            [
                ActorAudibleEventKind.Destruction,
                ActorAudibleEventKind.Damage,
                ActorAudibleEventKind.Attack,
            ]);

    /// <summary>
    /// The deepest bend distance any arm has ever offered, and the striker's
    /// own envelope. It is the default so every contract authored before the
    /// universal grammar keeps its exact bytes.
    /// </summary>
    private const int DeepestBendAfterTiles = 4;

    private static ActorShotProgramDefinition ShotProgram(
        bool enabled,
        bool oneBendOnly,
        int maxBendAfterTiles = DeepestBendAfterTiles,
        bool aimOffsets = false) =>
        new(
            enabled,
            headingSectors: 8,
            ActorShotHeadingModel.EightWayClockwiseModuloV1,
            bendStepSectors: 1,
            // The historical rich program carried offsets; the one-bend
            // grammar dropped them by conflation, never by ruling. The aim
            // arm (DECISIONS #173) restores them independently of the bend
            // count rule.
            minInitialAimSteps:
                enabled && (aimOffsets || !oneBendOnly) ? -1 : 0,
            maxInitialAimSteps:
                enabled && (aimOffsets || !oneBendOnly) ? 1 : 0,
            new ActorAimOnlyShotProgramDefinition(0, 0, 1, 0),
            allowedCurvedBendDirections: [-1, 1],
            minBendAfterTiles: 1,
            // A disabled program must carry the canonical inert bounds, so the
            // depth only reaches the contract on a gun that actually bends.
            maxBendAfterTiles: enabled ? maxBendAfterTiles : 1,
            minBendEveryTiles: 1,
            maxBendEveryTiles: enabled && !oneBendOnly ? 3 : 1,
            minBendCount: 1,
            maxBendCount: enabled && !oneBendOnly ? 3 : 1,
            launchTiles: 1,
            payloadOptional: enabled,
            defaultProgram: new ActorShotProgramValue(0, 0, 0, 1, 0),
            invalidPayloadResult: enabled
                ? ActorActionRejectionResult.Rejected
                : null,
            unsupportedPayloadResult: ActorActionRejectionResult.Blocked,
            diagonalCornersMustBeClear: true);

    private static ActorTransitionWindupDefinition Windup(
        ActorTransitionWindupDefinition.ActorTransitionCompletionKind
            completion,
        int durationTicks = 1) =>
        new(
            durationTicks,
            ActorTransitionWindupDefinition.PendingActionKind.WaitOnly,
            ActorTransitionWindupDefinition.SourceFormKind.RetainSourceForm,
            ActorTransitionWindupDefinition.TargetabilityKind
                .TargetableAndOccupiesTile,
            ActorTransitionWindupDefinition.LethalDamageKind.CancelTransition,
            completion,
            ActorTransitionWindupDefinition.PlacementReferenceKind
                .QueueTimePose);

    private static ImmutableArray<ActorRelativePositionOffset>
        FabricationCandidateOffsets() =>
        (
            from forward in Enumerable.Range(-2, 5)
            from right in Enumerable.Range(-2, 5)
            where forward != 0 || right != 0
            orderby Math.Max(Math.Abs(forward), Math.Abs(right)),
                Math.Abs(forward) + Math.Abs(right),
                forward,
                right
            select new ActorRelativePositionOffset(forward, right)
        ).ToImmutableArray();

    private static ImmutableArray<ActorRelativePositionOffset>
        RemoteFabricationCandidateOffsets() =>
        (
            from forward in Enumerable.Range(-22, 45)
            from right in Enumerable.Range(-14, 29)
            where forward != 0 || right != 0
            orderby Math.Max(Math.Abs(forward), Math.Abs(right)),
                Math.Abs(forward) + Math.Abs(right),
                forward,
                right
            select new ActorRelativePositionOffset(forward, right)
        ).ToImmutableArray();

    private static ActorMapDefinition CreateMap(
        bool remoteFabrication,
        FrontlineLabsDuelMapArm duelMapArm,
        bool automaticCompanions,
        bool classes = false,
        FrontlineLabsSideObjectiveArm sideObjective =
            FrontlineLabsSideObjectiveArm.None,
        FrontlineLabsRosterArm roster = FrontlineLabsRosterArm.None) =>
        new(
            MapIdFor(
                remoteFabrication,
                duelMapArm,
                automaticCompanions,
                classes,
                sideObjective,
                roster),
            version: 1,
            MapTileRows(duelMapArm, sideObjective),
            [
                Spawn("team-0-prime", 2, 7, Direction.East),
                Spawn("team-1-prime", 20, 7, Direction.West),
                .. AutomaticCompanionSpawns(
                    automaticCompanions || classes,
                    roster),
            ],
            [
                .. ObjectiveRegions(duelMapArm),
                Region("team-0-home-pad", HomePadTiles(0, roster)),
                Region("team-1-home-pad", HomePadTiles(1, roster)),
                .. MusterSiteRegions(sideObjective),
                .. RemoteFabricationRegions(
                    remoteFabrication || classes,
                    sideObjective),
            ],
            [
                new ActorMapTileTagDefinition(
                    "anchor-forbidden",
                    ActorMapTileTagDefinition.TileTagKind
                        .TransitionPlacementForbidden,
                    AnchorForbiddenTiles(duelMapArm)),
                new ActorMapTileTagDefinition(
                    "protected-home-pads",
                    ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
                    [
                        .. Positions(HomePadTiles(0, roster)),
                        .. Positions(HomePadTiles(1, roster)),
                    ]),
            ]);

    /// <summary>
    /// One team's home pad: six tiles on every measured generation, ten on the
    /// legion map, where the extra companion anchors need protecting. The pad
    /// is one thing — reserved spawn anchors, opposing-entry protection, and
    /// (under the economy arm) the banking region — so widening it widens all
    /// three together rather than splitting the concept.
    /// </summary>
    private static IReadOnlyList<(int X, int Y)> HomePadTiles(
        int teamId,
        FrontlineLabsRosterArm roster)
    {
        (int X, int Y)[] teamZero =
            roster == FrontlineLabsRosterArm.Legion
                ? [.. FrontlineLabsLegionRoster.TeamZeroPad]
                : [(1, 6), (2, 6), (1, 7), (2, 7), (1, 8), (2, 8)];
        return teamId == 0
            ? teamZero
            :
            [
                .. teamZero
                    .Select(Mirrored)
                    .OrderBy(tile => tile.Y)
                    .ThenBy(tile => tile.X),
            ];
    }

    /// <summary>
    /// One tile reflected across the map's fairness axis — the vertical centre
    /// line the two spawns face across. It is the construction every mirrored
    /// map fact in this family uses, and the arm tests re-derive it.
    /// </summary>
    private static (int X, int Y) Mirrored((int X, int Y) tile) =>
        (MapWidth - 1 - tile.X, tile.Y);

    private const int MapWidth = 23;

    /// <summary>
    /// The map identity for one arm combination. A side objective mints its
    /// own map generation rather than editing an existing one: historical map
    /// goldens stay byte-exact, and the widened alcoves plus the two site
    /// regions are a new fingerprint on purpose.
    /// </summary>
    private static string MapIdFor(
        bool remoteFabrication,
        FrontlineLabsDuelMapArm duelMapArm,
        bool automaticCompanions,
        bool classes,
        FrontlineLabsSideObjectiveArm sideObjective,
        FrontlineLabsRosterArm roster)
    {
        string baseId = sideObjective == FrontlineLabsSideObjectiveArm.Muster
            ? MusterMapId
            : roster == FrontlineLabsRosterArm.Legion
                ? FrontlineLabsLegionRoster.MapId
                : MapId;
        return remoteFabrication
                ? $"{baseId}-remote-fabrication-experiment"
                : classes
                    ? duelMapArm switch
                    {
                        FrontlineLabsDuelMapArm.Current =>
                            $"{baseId}-classes",
                        FrontlineLabsDuelMapArm.ThinFronts =>
                            $"{baseId}-thin-fronts-classes",
                        FrontlineLabsDuelMapArm.OuterShoulderBypass =>
                            $"{baseId}-outer-shoulder-classes",
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(duelMapArm),
                            duelMapArm,
                            "Unknown Frontline Labs duel map arm."),
                    }
                    : (duelMapArm, automaticCompanions) switch
                    {
                        (FrontlineLabsDuelMapArm.Current, false) => baseId,
                        (FrontlineLabsDuelMapArm.ThinFronts, false) =>
                            $"{baseId}-thin-fronts-experiment",
                        (FrontlineLabsDuelMapArm
                            .OuterShoulderBypass, false) =>
                            $"{baseId}-outer-shoulder-bypass-experiment",
                        (FrontlineLabsDuelMapArm.Current, true) =>
                            $"{baseId}-auto-companions",
                        (FrontlineLabsDuelMapArm.ThinFronts, true) =>
                            $"{baseId}-thin-fronts-auto-companions",
                        (FrontlineLabsDuelMapArm
                            .OuterShoulderBypass, true) =>
                            $"{baseId}-outer-shoulder-auto-companions",
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(duelMapArm),
                            duelMapArm,
                            "Unknown Frontline Labs duel map arm."),
                    };
    }

    /// <summary>
    /// The two mirror-symmetric MUSTER sites, or nothing. Both sit on the
    /// map's centre column, so they are equidistant from the two spawns by
    /// construction, and they are exact reflections of each other across the
    /// centre row.
    /// </summary>
    private static ImmutableArray<ActorMapRegionDefinition>
        MusterSiteRegions(FrontlineLabsSideObjectiveArm sideObjective) =>
        sideObjective == FrontlineLabsSideObjectiveArm.Muster
            ?
            [
                Objective(
                    FrontlineLabsMusterSite.NorthRegionId,
                    [.. FrontlineLabsMusterSite.NorthTiles]),
                Objective(
                    FrontlineLabsMusterSite.SouthRegionId,
                    [.. FrontlineLabsMusterSite.SouthTiles]),
            ]
            : [];

    private static ImmutableArray<string> MapTileRows(
        FrontlineLabsDuelMapArm duelMapArm,
        FrontlineLabsSideObjectiveArm sideObjective =
            FrontlineLabsSideObjectiveArm.None)
    {
        ImmutableArray<string> rows =
        [
            "#######################",
            "#.....................#",
            "#..##.....#.#.....##..#",
            "#.........#.#.........#",
            "#...#......#......#...#",
            "#....#.....#.....#....#",
            "#....#..##...##..#....#",
            "#.....................#",
            "#....#..##...##..#....#",
            "#....#.....#.....#....#",
            "#....#.....#.....#....#",
            "#.........#.#.........#",
            "#..##.....#.#.....##..#",
            "#.....................#",
            "#######################",
        ];
        if (sideObjective == FrontlineLabsSideObjectiveArm.Muster)
        {
            // The MUSTER map opens the two alcove shoulders on rows 3 and 11
            // ((10,3)/(12,3) and their mirrors), turning each 1-wide
            // cul-de-sac into a through-passage. That is a design
            // prerequisite, not a flourish: an AEGIS SHELL parked in a
            // 1-wide dead end deflects every bolt arriving in its facing
            // quadrant and its published counter-play — go around it — does
            // not exist in a corridor. Both rows stay palindromic about the
            // centre column, so the map keeps the mirror fairness the two
            // spawns depend on.
            rows = rows
                .SetItem(3, "#.....................#")
                .SetItem(11, "#.....................#");
        }
        if (duelMapArm != FrontlineLabsDuelMapArm.OuterShoulderBypass)
            return rows;

        return rows
            .SetItem(6, "#....#...#...#...#....#")
            .SetItem(8, "#....#...#...#...#....#");
    }

    private static ImmutableArray<ActorMapRegionDefinition>
        ObjectiveRegions(FrontlineLabsDuelMapArm duelMapArm) =>
        duelMapArm == FrontlineLabsDuelMapArm.ThinFronts
            ?
            [
                Objective(
                    "frontline-position-0",
                    [(4, 8), (4, 9), (4, 10)]),
                Objective(
                    "frontline-position-1",
                    [(7, 4), (7, 5), (7, 6)]),
                Objective(
                    "frontline-position-2",
                    [(11, 6), (11, 7), (11, 8)]),
                Objective(
                    "frontline-position-3",
                    [(15, 4), (15, 5), (15, 6)]),
                Objective(
                    "frontline-position-4",
                    [(18, 8), (18, 9), (18, 10)]),
            ]
            :
            [
                Objective(
                    "frontline-position-0",
                    [(3, 8), (4, 8), (3, 9), (4, 9)]),
                Objective(
                    "frontline-position-1",
                    [(6, 5), (7, 5), (6, 6), (7, 6)]),
                Objective(
                    "frontline-position-2",
                    [
                        (10, 7),
                        (11, 7),
                        (12, 7),
                        (10, 8),
                        (11, 8),
                        (12, 8),
                    ]),
                Objective(
                    "frontline-position-3",
                    [(15, 5), (16, 5), (15, 6), (16, 6)]),
                Objective(
                    "frontline-position-4",
                    [(18, 8), (19, 8), (18, 9), (19, 9)]),
            ];

    private static ImmutableArray<ActorMapRegionDefinition>
        RemoteFabricationRegions(
            bool enabled,
            FrontlineLabsSideObjectiveArm sideObjective) =>
        enabled
            ? [
                Region(
                    RemoteFabricationSourceRegionId,
                    WalkableMapTiles(sideObjective)),
            ]
            : [];

    private static IReadOnlyList<(int X, int Y)> WalkableMapTiles(
        FrontlineLabsSideObjectiveArm sideObjective)
    {
        ImmutableArray<string> rows = MapTileRows(
            FrontlineLabsDuelMapArm.Current,
            sideObjective);
        return (
            from y in Enumerable.Range(0, rows.Length)
            from x in Enumerable.Range(0, rows[y].Length)
            where rows[y][x] != '#'
            select (X: x, Y: y)
        ).ToArray();
    }

    private static ActorMapSpawnAnchorDefinition Spawn(
        string id,
        int x,
        int y,
        Direction facing) =>
        new(
            new InitialSpawnDefinition(
                id,
                new Position(x, y),
                facing),
            [ActorMovementLayer.Ground]);

    /// <summary>
    /// The companion spawn anchors: two per team on every measured
    /// generation, seven per team on the legion map. Team 1's are team 0's
    /// reflected across the fairness axis, and the first two are byte-for-byte
    /// the measured pair, so the legion map's opening geometry is the classes
    /// map's opening geometry.
    /// </summary>
    private static ImmutableArray<ActorMapSpawnAnchorDefinition>
        AutomaticCompanionSpawns(
            bool enabled,
            FrontlineLabsRosterArm roster)
    {
        if (!enabled)
            return [];
        (int X, int Y)[] teamZero =
            roster == FrontlineLabsRosterArm.Legion
                ? [.. FrontlineLabsLegionRoster.TeamZeroCompanionAnchors]
                : [(1, 6), (1, 8)];
        return
        [
            .. teamZero.Select((tile, index) => Spawn(
                FrontlineLabsLegionRoster.CompanionSpawnId(0, index + 1),
                tile.X,
                tile.Y,
                Direction.East)),
            .. teamZero.Select((tile, index) => Spawn(
                FrontlineLabsLegionRoster.CompanionSpawnId(1, index + 1),
                Mirrored(tile).X,
                Mirrored(tile).Y,
                Direction.West)),
        ];
    }

    private static ActorMapRegionDefinition Objective(
        string id,
        IReadOnlyList<(int X, int Y)> tiles) =>
        new(
            id,
            ActorMapRegionDefinition.RegionKind.Objective,
            Positions(tiles));

    private static ActorMapRegionDefinition Region(
        string id,
        IReadOnlyList<(int X, int Y)> tiles) =>
        new(
            id,
            ActorMapRegionDefinition.RegionKind.TransitionPlacement,
            Positions(tiles));

    private static ImmutableArray<Position> Positions(
        IReadOnlyList<(int X, int Y)> tiles) =>
        tiles.Select(tile => new Position(tile.X, tile.Y))
            .ToImmutableArray();

    private static ImmutableArray<Position> AnchorForbiddenTiles(
        FrontlineLabsDuelMapArm duelMapArm) =>
    [
        new(1, 1), new(2, 1), new(20, 1), new(21, 1),
        new(1, 2), new(2, 2), new(20, 2), new(21, 2),
        new(1, 3), new(2, 3), new(3, 3), new(19, 3), new(20, 3), new(21, 3),
        new(1, 4), new(2, 4), new(3, 4), new(19, 4), new(20, 4), new(21, 4),
        new(1, 5), new(2, 5), new(3, 5), new(4, 5), new(6, 5), new(7, 5),
        new(15, 5), new(16, 5), new(18, 5), new(19, 5), new(20, 5), new(21, 5),
        new(1, 6), new(2, 6), new(3, 6), new(4, 6), new(6, 6), new(7, 6),
        new(15, 6), new(16, 6), new(18, 6), new(19, 6), new(20, 6), new(21, 6),
        new(1, 7), new(2, 7), new(3, 7), new(4, 7), new(5, 7), new(6, 7),
        new(7, 7), new(8, 7), new(9, 7), new(10, 7), new(11, 7), new(12, 7),
        new(13, 7), new(14, 7), new(15, 7), new(16, 7), new(17, 7),
        new(18, 7), new(19, 7), new(20, 7), new(21, 7),
        new(1, 8), new(2, 8), new(3, 8), new(4, 8), new(10, 8), new(11, 8),
        new(12, 8), new(18, 8), new(19, 8), new(20, 8), new(21, 8),
        new(1, 9), new(2, 9), new(3, 9), new(4, 9), new(18, 9), new(19, 9),
        new(20, 9), new(21, 9),
        new(1, 10), new(2, 10), new(3, 10), new(4, 10),
        new(18, 10), new(19, 10), new(20, 10), new(21, 10),
        new(1, 11), new(2, 11), new(3, 11), new(4, 11),
        new(18, 11), new(19, 11), new(20, 11), new(21, 11),
        new(1, 12), new(2, 12), new(5, 12), new(17, 12), new(20, 12),
        new(21, 12),
        new(1, 13), new(2, 13), new(6, 13), new(16, 13), new(20, 13),
        new(21, 13),
        .. ShoulderBypassForbiddenTiles(duelMapArm),
    ];

    private static ImmutableArray<Position> ShoulderBypassForbiddenTiles(
        FrontlineLabsDuelMapArm duelMapArm) =>
        duelMapArm == FrontlineLabsDuelMapArm.OuterShoulderBypass
            ?
            [
                new Position(8, 6),
                new Position(14, 6),
                new Position(8, 8),
                new Position(14, 8),
            ]
            : [];

    /// <summary>
    /// How many stable unit slots one team fields. Three everywhere except a
    /// five-slot arm's fabricator side, which fields prime plus four children
    /// (<see cref="AsymmetricSlotsTopologyProfileId"/>), and a legion cell,
    /// where the roster arm authors the whole schedule for both teams — eight
    /// slots, nine for the fabricator — and SUPERSEDES the five-slot skill's
    /// extra slots rather than stacking with them. Two factors that both set
    /// the slot count could not be attributed separately.
    /// </summary>
    private static int TeamSlotCount(
        FrontlineLabsClassDefinition? teamClass,
        FrontlineLabsSkillKit skills,
        FrontlineLabsRosterArm roster)
    {
        if (teamClass is null)
            return 3;
        if (roster == FrontlineLabsRosterArm.Legion)
        {
            return 1
                + FrontlineLabsLegionRoster.CompanionSlots(
                    teamClass.ExplicitForwardFabrication);
        }
        return teamClass.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots
            && skills.HasFlag(FrontlineLabsSkillKit.FabricatorFiveSlots)
                ? 1 + teamClass.ExtraChildUnlockTicks.Length + 2
                : 3;
    }

    private static PublicMatchTopology CreateTopology(
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classes,
        FrontlineLabsSkillKit skills,
        FrontlineLabsRosterArm roster =
            FrontlineLabsRosterArm.None) =>
        new()
        {
            Teams =
            [
                new PublicScoringTeam(0, classes?.TeamZero.Id),
                new PublicScoringTeam(1, classes?.TeamOne.Id),
            ],
            Participants =
            [
                new PublicParticipant(0, 0, classes?.TeamZero.Id),
                new PublicParticipant(1, 1, classes?.TeamOne.Id),
            ],
            UnitSlots =
            [
                .. Enumerable
                    .Range(
                        0,
                        TeamSlotCount(classes?.TeamZero, skills, roster))
                    .Select(unitId => new PublicUnitSlot(0, unitId, 0)),
                .. Enumerable
                    .Range(
                        0,
                        TeamSlotCount(classes?.TeamOne, skills, roster))
                    .Select(unitId => new PublicUnitSlot(1, unitId, 1)),
            ],
            InitialLives =
            [
                new PublicInitialLife(
                    0,
                    0,
                    0,
                    classes?.TeamZero.PrimeFormId ?? PrimeFormId),
                .. OpeningCompanions(classes?.TeamZero, 0, roster)
                    .Select(companion => new PublicInitialLife(
                        0,
                        companion.UnitId,
                        0,
                        companion.FormId)),
                new PublicInitialLife(
                    1,
                    0,
                    0,
                    classes?.TeamOne.PrimeFormId ?? PrimeFormId),
                .. OpeningCompanions(classes?.TeamOne, 1, roster)
                    .Select(companion => new PublicInitialLife(
                        1,
                        companion.UnitId,
                        0,
                        companion.FormId)),
            ],
        };

    /// <summary>
    /// The companion slots that carry a LIVE body at tick zero: none on every
    /// measured generation, and the legion arm's opening tranche for EVERY
    /// class on the levy re-mint. The fabricator's opening was fabricable on
    /// the retired `legion` spelling; the owner ruled it automatic ("the
    /// initial spawn should be automatic for the Fab"), so it stands four
    /// bodies at tick zero and its exclusive verb prices the mid and late
    /// tranches instead.
    /// </summary>
    private static IEnumerable<(int UnitId, string FormId, string SpawnId)>
        OpeningCompanions(
            FrontlineLabsClassDefinition? teamClass,
            int teamId,
            FrontlineLabsRosterArm roster)
    {
        if (roster != FrontlineLabsRosterArm.Legion || teamClass is null)
            yield break;
        int slots = FrontlineLabsLegionRoster.OpeningCompanionSlots
            + (teamClass.ExplicitForwardFabrication
                ? FrontlineLabsLegionRoster.FabricatorExtraOpeningSlots
                : 0);
        for (int index = 0; index < slots; index++)
        {
            int unitId = index + 1;
            yield return (
                unitId,
                teamClass.ChildFormId,
                FrontlineLabsLegionRoster.CompanionSpawnId(teamId, unitId));
        }
    }

    private static InitialDeploymentDefinition CreateInitialDeployment(
        (FrontlineLabsClassDefinition TeamZero,
            FrontlineLabsClassDefinition TeamOne)? classes,
        FrontlineLabsRosterArm roster = FrontlineLabsRosterArm.None)
    {
        (int UnitId, string FormId, string SpawnId)[] teamZero =
            [.. OpeningCompanions(classes?.TeamZero, 0, roster)];
        (int UnitId, string FormId, string SpawnId)[] teamOne =
            [.. OpeningCompanions(classes?.TeamOne, 1, roster)];
        ImmutableArray<ActorMapSpawnAnchorDefinition> anchors =
            AutomaticCompanionSpawns(
                classes is not null,
                roster);
        InitialSpawnDefinition Anchor(string spawnId) =>
            anchors.Single(anchor => anchor.Spawn.SpawnId == spawnId).Spawn;
        return new InitialDeploymentDefinition(
            [
                new InitialSpawnDefinition(
                    "team-0-prime",
                    new Position(2, 7),
                    Direction.East),
                .. teamZero.Select(companion => Anchor(companion.SpawnId)),
                new InitialSpawnDefinition(
                    "team-1-prime",
                    new Position(20, 7),
                    Direction.West),
                .. teamOne.Select(companion => Anchor(companion.SpawnId)),
            ],
            [
                new InitialLifeDeployment(
                    0,
                    0,
                    0,
                    classes?.TeamZero.PrimeFormId ?? PrimeFormId,
                    "team-0-prime"),
                .. teamZero.Select(companion => new InitialLifeDeployment(
                    0,
                    companion.UnitId,
                    0,
                    companion.FormId,
                    companion.SpawnId)),
                new InitialLifeDeployment(
                    1,
                    0,
                    0,
                    classes?.TeamOne.PrimeFormId ?? PrimeFormId,
                    "team-1-prime"),
                .. teamOne.Select(companion => new InitialLifeDeployment(
                    1,
                    companion.UnitId,
                    0,
                    companion.FormId,
                    companion.SpawnId)),
            ]);
    }

    private static ImmutableArray<
        ActorUnitSlotLifecycleAssignmentDefinition>
        CreateLifecycleAssignments(
            bool automaticCompanions,
            (FrontlineLabsClassDefinition TeamZero,
                FrontlineLabsClassDefinition TeamOne)? classes,
            FrontlineLabsSkillKit skills,
            FrontlineLabsRosterArm roster = FrontlineLabsRosterArm.None)
    {
        if (roster == FrontlineLabsRosterArm.Legion && classes is { } legion)
        {
            return
            [
                .. LegionTeamAssignments(0, legion.TeamZero, skills),
                .. LegionTeamAssignments(1, legion.TeamOne, skills),
            ];
        }
        if (classes is { } pair)
        {
            // Non-fabricating classes receive companions automatically at
            // their unlock ticks; the Fabricator's explicit forward
            // fabrication is its class verb (DECISIONS #154).
            string? AutoSpawn(
                FrontlineLabsClassDefinition entry,
                int teamId,
                int unitId) =>
                entry.ExplicitForwardFabrication
                    ? null
                    : $"team-{teamId}-child-{unitId}";
            IEnumerable<ActorUnitSlotLifecycleAssignmentDefinition> Team(
                int teamId,
                FrontlineLabsClassDefinition entry)
            {
                bool stance =
                    entry.Skill is FrontlineLabsSkillKit.StrikerVolley
                        or FrontlineLabsSkillKit.BulwarkAegisShell
                    && skills.HasFlag(entry.Skill);
                return
                [
                    ClassPrimeAssignment(
                        teamId,
                        $"team-{teamId}-prime",
                        entry,
                        stance),
                    ClassChildAssignment(
                        teamId,
                        1,
                        entry,
                        entry.FirstChildUnlockTick,
                        AutoSpawn(entry, teamId, 1),
                        stance),
                    ClassChildAssignment(
                        teamId,
                        2,
                        entry,
                        entry.SecondChildUnlockTick,
                        AutoSpawn(entry, teamId, 2),
                        stance),
                    // The five-slot arm's extra slots run their own later
                    // unlock schedule and their own slower rebuild profile;
                    // they are ordinary slots in every other respect.
                    .. Enumerable
                        .Range(
                            0,
                            TeamSlotCount(
                                entry,
                                skills,
                                FrontlineLabsRosterArm.None) - 3)
                        .Select(index => ClassChildAssignment(
                            teamId,
                            3 + index,
                            entry,
                            entry.ExtraChildUnlockTicks[index],
                            AutoSpawn(entry, teamId, 3 + index),
                            stance,
                            entry.ExtraChildLifecycleProfileId)),
                ];
            }
            return
            [
                .. Team(0, pair.TeamZero),
                .. Team(1, pair.TeamOne),
            ];
        }

        return
        [
        PrimeAssignment(0, "team-0-prime"),
        ChildAssignment(
            0,
            1,
            unlockTick: 120,
            automaticCompanions
                ? "team-0-child-1"
                : null),
        ChildAssignment(
            0,
            2,
            unlockTick: 260,
            automaticCompanions
                ? "team-0-child-2"
                : null),
        PrimeAssignment(1, "team-1-prime"),
        ChildAssignment(
            1,
            1,
            unlockTick: 120,
            automaticCompanions
                ? "team-1-child-1"
                : null),
        ChildAssignment(
            1,
            2,
            unlockTick: 260,
            automaticCompanions
                ? "team-1-child-2"
                : null),
        ];
    }

    /// <summary>
    /// One team's slot lifecycle under the LEGION roster. The prime is
    /// unchanged; every companion slot reads its availability from the roster
    /// table rather than from the class's own cadence:
    /// <list type="bullet">
    /// <item>the opening tranche is LIVE at tick zero for a class that
    /// receives companions (topology initial lives, deployed on its reserved
    /// anchor), and an unlocked-at-zero FABRICABLE slot for the fabricator —
    /// its bodies cost prime actions and arrive in the field, which is the
    /// bargain #154 gave the class;</item>
    /// <item>the mid and late tranches are dormant with a declared automatic
    /// activation tick (the 0.10.4 capability) for an automatic class, and
    /// dormant-unlock-at-tick for the fabricator;</item>
    /// <item>the late tranche keeps FIVE SLOTS' slower rebuild profile where
    /// that skill is in the cell, because those are exactly the extra bodies
    /// the skill was priced on — the arm buys COUNT, never TEMPO.</item>
    /// </list>
    /// </summary>
    private static IEnumerable<ActorUnitSlotLifecycleAssignmentDefinition>
        LegionTeamAssignments(
            int teamId,
            FrontlineLabsClassDefinition entry,
            FrontlineLabsSkillKit skills)
    {
        bool stance =
            entry.Skill is FrontlineLabsSkillKit.StrikerVolley
                or FrontlineLabsSkillKit.BulwarkAegisShell
            && skills.HasFlag(entry.Skill);
        bool fiveSlots =
            entry.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots
            && skills.HasFlag(FrontlineLabsSkillKit.FabricatorFiveSlots);
        bool fabricates = entry.ExplicitForwardFabrication;
        yield return ClassPrimeAssignment(
            teamId,
            $"team-{teamId}-prime",
            entry,
            stance);
        ImmutableArray<int> unlockTicks =
            FrontlineLabsLegionRoster.CompanionUnlockTicks(fabricates);
        for (int index = 0; index < unlockTicks.Length; index++)
        {
            int unitId = index + 1;
            bool late = FrontlineLabsLegionRoster.IsLateTrancheSlot(
                fabricates,
                index);
            // The OPENING tranche is automatic for every class — owner
            // ruling on the levy re-mint ("the initial spawn should be
            // automatic for the Fab"): the fabricator's four bodies stand
            // at tick zero like everyone else's three, and its exclusive
            // verb prices the MID and LATE tranches instead, which stay
            // fabricate-to-field. Its opening slots deploy from anchors
            // but carry NO respawn assignment — a dead opener rebuilds
            // through explicit fabrication, exactly like every other
            // fabricator body.
            bool opening = unlockTicks[index] == 0;
            yield return ClassChildAssignment(
                teamId,
                unitId,
                entry,
                unlockTicks[index],
                fabricates
                    ? null
                    : FrontlineLabsLegionRoster.CompanionSpawnId(
                        teamId,
                        unitId),
                stance,
                late && fiveSlots
                    ? entry.ExtraChildLifecycleProfileId
                    : null,
                activeAtTickZero: opening);
        }
    }

    private static ActorUnitSlotLifecycleAssignmentDefinition
        ClassPrimeAssignment(
            int teamId,
            string spawnId,
            FrontlineLabsClassDefinition entry,
            bool stance) =>
        new(
            teamId,
            unitId: 0,
            entry.PrimeLifecycleProfileId,
            initialGeneration: 0,
            allowedFormIds:
            [
                entry.PrimeFormId,
                .. entry.MayAnchor
                    ? new[] { entry.PrimeTurretFormId }
                    : [],
                .. stance ? new[] { entry.PrimeStanceFormId } : [],
            ],
            ActorUnitSlotLifecycleAssignmentDefinition
                .InitialAvailabilityKind.ActiveAtTickZero,
            unlockTick: null,
            assignedRespawnSpawnId: spawnId);

    private static ActorUnitSlotLifecycleAssignmentDefinition
        ClassChildAssignment(
            int teamId,
            int unitId,
            FrontlineLabsClassDefinition entry,
            int unlockTick,
            string? automaticSpawnId,
            bool stance,
            string? lifecycleProfileId = null,
            bool activeAtTickZero = false) =>
        new(
            teamId,
            unitId,
            lifecycleProfileId ?? entry.ChildLifecycleProfileId,
            initialGeneration:
                activeAtTickZero || automaticSpawnId is not null ? 0 : null,
            allowedFormIds:
            [
                entry.ChildFormId,
                .. entry.MayAnchor
                    ? new[] { entry.ChildTurretFormId }
                    : [],
                .. stance ? new[] { entry.ChildStanceFormId } : [],
            ],
            activeAtTickZero
                ? ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero
                : automaticSpawnId is null
                    ? ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind.DormantUnlockAtTick
                    : ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind
                        .DormantAutomaticActivationAtTick,
            activeAtTickZero ? null : unlockTick,
            assignedRespawnSpawnId: automaticSpawnId);

    private static ActorUnitSlotLifecycleAssignmentDefinition PrimeAssignment(
        int teamId,
        string spawnId) =>
        new(
            teamId,
            unitId: 0,
            PrimeLifecycleId,
            initialGeneration: 0,
            allowedFormIds: [PrimeFormId, ReplicaFormId],
            ActorUnitSlotLifecycleAssignmentDefinition
                .InitialAvailabilityKind.ActiveAtTickZero,
            unlockTick: null,
            assignedRespawnSpawnId: spawnId);

    private static ActorUnitSlotLifecycleAssignmentDefinition ChildAssignment(
        int teamId,
        int unitId,
        int unlockTick,
        string? automaticSpawnId) =>
        new(
            teamId,
            unitId,
            ChildLifecycleId,
            initialGeneration: automaticSpawnId is null ? null : 0,
            allowedFormIds: [ChildFormId, ReplicaFormId, TurretFormId],
            automaticSpawnId is null
                ? ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.DormantUnlockAtTick
                : ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind
                    .DormantAutomaticActivationAtTick,
            unlockTick,
            assignedRespawnSpawnId: automaticSpawnId);

    /// <summary>
    /// Class arms resolve both fabrication roles to the whole walkable map:
    /// the Fabricator's forward fabrication places its child from
    /// source-relative offsets beside the prime, and the SpawnProtected
    /// forbidden-output tag keeps every pad clear. Non-fabricating classes
    /// never exercise these roles.
    /// </summary>
    private static ImmutableArray<
        ActorParticipantRegionAssignmentDefinition>
        ClassesParticipantRegionAssignments() =>
    [
        new ActorParticipantRegionAssignmentDefinition(
            0,
            FabricationSourceRoleId,
            RemoteFabricationSourceRegionId,
            Direction.East),
        new ActorParticipantRegionAssignmentDefinition(
            0,
            FabricationOutputRoleId,
            RemoteFabricationSourceRegionId,
            Direction.East),
        new ActorParticipantRegionAssignmentDefinition(
            1,
            FabricationSourceRoleId,
            RemoteFabricationSourceRegionId,
            Direction.West),
        new ActorParticipantRegionAssignmentDefinition(
            1,
            FabricationOutputRoleId,
            RemoteFabricationSourceRegionId,
            Direction.West),
    ];

    private static ImmutableArray<
        ActorParticipantRegionAssignmentDefinition>
        CreateParticipantRegionAssignments(
            bool remoteFabrication) =>
    [
        new ActorParticipantRegionAssignmentDefinition(
            0,
            FabricationSourceRoleId,
            remoteFabrication
                ? RemoteFabricationSourceRegionId
                : "team-0-home-pad",
            Direction.East),
        new ActorParticipantRegionAssignmentDefinition(
            0,
            FabricationOutputRoleId,
            "team-0-home-pad",
            Direction.East),
        new ActorParticipantRegionAssignmentDefinition(
            1,
            FabricationSourceRoleId,
            remoteFabrication
                ? RemoteFabricationSourceRegionId
                : "team-1-home-pad",
            Direction.West),
        new ActorParticipantRegionAssignmentDefinition(
            1,
            FabricationOutputRoleId,
            "team-1-home-pad",
            Direction.West),
    ];
}
