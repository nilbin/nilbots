using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// The next round's package, pinned: the keel WITHOUT its forward rally (so
/// every arrival walks home and the fabricator's field-placed children are the
/// only forward body delivery left), the 750-tick horizon, and the registered
/// identity the whole package carries in each of its three shapes. The economy
/// half of the same window — the full six-tier board — is pinned beside the
/// ladder it belongs to.
/// </summary>
public sealed class FrontlineLabsNextRoundPackageTests
{
    private const FrontlineLabsPendulumArm Keel =
        FrontlineLabsPendulumArm.StickyFrontline
        | FrontlineLabsPendulumArm.ForwardRally
        | FrontlineLabsPendulumArm.ContestMajority
        | FrontlineLabsPendulumArm.EnemySoleDecay;

    private const FrontlineLabsPendulumArm Hull =
        FrontlineLabsPendulumArm.StickyFrontline
        | FrontlineLabsPendulumArm.ContestMajority
        | FrontlineLabsPendulumArm.EnemySoleDecay;

    private const FrontlineLabsSkillKit WholeKit =
        FrontlineLabsSkillKit.StrikerVolley
        | FrontlineLabsSkillKit.BulwarkAegisShell
        | FrontlineLabsSkillKit.FabricatorFiveSlots;

    /// <summary>The next round's stack, with and without the roster.</summary>
    internal static ActorResolvedMatchDefinition Package(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsRosterArm roster = FrontlineLabsRosterArm.None,
        FrontlineLabsPendulumArm pendulum = Hull,
        FrontlineLabsHorizonArm horizon = FrontlineLabsHorizonArm.Long)
    {
        return Build(teamZero, teamOne, roster, pendulum, horizon);
    }

    private static ActorResolvedMatchDefinition Build(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsRosterArm roster,
        FrontlineLabsPendulumArm pendulum,
        FrontlineLabsHorizonArm horizon)
    {
        bool fabricator =
            teamZero.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots
            || teamOne.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots;
        return FrontlineLabsDefinition.CreatePendulumExperiment(
            pendulum,
            (teamZero, teamOne),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked,
            skills: WholeKit,
            bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
            fiveSlots: fabricator
                ? FrontlineLabsFiveSlotVariant.Wane
                : FrontlineLabsFiveSlotVariant.Full,
            stanceGround: FrontlineLabsStanceGroundArm.Open,
            aim: FrontlineLabsAimArm.Offset,
            cooldown: FrontlineLabsCooldownArm.Ticking,
            volley: FrontlineLabsVolleyArm.Salvo,
            capture: FrontlineLabsCaptureArm.Channel,
            economy: FrontlineLabsEconomyArm.Scrap,
            roster: roster,
            horizon: horizon);
    }

    /// <summary>
    /// The rally, taken away. Under the hull level the lifecycle declares the
    /// permanently reserved assigned spawn for every automatic arrival, and
    /// every other counterweight the keel carries is untouched — so the arm is
    /// exactly one lever.
    /// </summary>
    [Fact]
    public void TheHullLevelSendsEveryAutomaticArrivalHome()
    {
        ActorResolvedMatchDefinition hull = Package(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker);
        // The keel-based v1.1 game it replaces, at its own registered
        // horizon: the comparison is the rally, not the clock.
        ActorResolvedMatchDefinition keel = Package(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            pendulum: Keel,
            horizon: FrontlineLabsHorizonArm.Standard);

        Assert.Equal(
            ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims,
            hull.Rules.Lifecycle.AutomaticReturnPlacement);
        Assert.Equal(
            ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileInTeamAdvanceOrderThenAssignedSpawn,
            keel.Rules.Lifecycle.AutomaticReturnPlacement);

        // Every other counterweight is identical: the ratchet hold, the
        // contest-majority control policy, and the enemy-sole decay clock.
        var hullMode = (FrontlineGameModeDefinition)hull.Rules.GameMode;
        var keelMode = (FrontlineGameModeDefinition)keel.Rules.GameMode;
        Assert.Equal(
            keelMode.Capture.RatchetHoldTicks,
            hullMode.Capture.RatchetHoldTicks);
        Assert.Equal(
            keelMode.Capture.ControlPolicy,
            hullMode.Capture.ControlPolicy);
        Assert.Equal(
            keelMode.Capture.DecayClock,
            hullMode.Capture.DecayClock);
        Assert.Equal(
            keelMode.Capture.RedeployPolicy,
            hullMode.Capture.RedeployPolicy);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(keel.Rules),
            ActorContractFingerprint.ComputeRules(hull.Rules));
    }

    /// <summary>
    /// The horizon is a contract limit, declared per arm. A standard cell
    /// keeps its exact 500, and the long one publishes 750 for anyone who
    /// reads it — which every pacing decision in a bot should.
    /// </summary>
    [Fact]
    public void TheLongHorizonPublishesSevenHundredAndFiftyTicks()
    {
        Assert.Equal(
            750,
            Package(
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker)
                .Rules.Limits.MaxTicks);
        Assert.Equal(
            500,
            Package(
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker,
                    pendulum: Keel,
                    horizon: FrontlineLabsHorizonArm.Standard)
                .Rules.Limits.MaxTicks);
        Assert.Equal(
            500,
            FrontlineLabsDefinition.Create().Rules.Limits.MaxTicks);

        // Every roster activation still lands inside the horizon, with the
        // late tranche owing the whole second act.
        ActorResolvedMatchDefinition legion = Package(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsRosterArm.Legion);
        Assert.All(
            legion.LifecycleAssignments,
            assignment => Assert.True(
                (assignment.UnlockTick ?? 0)
                < legion.Rules.Limits.MaxTicks));
        Assert.Equal(
            450,
            legion.Rules.Limits.MaxTicks
                - FrontlineLabsLegionRoster.LateTrancheUnlockTick);
    }

    /// <summary>
    /// The identity budget for the whole package. Three owner rulings landed
    /// on one game, so one re-mint covers them: the per-factor spelling of
    /// hull + kit + bend + aim + clock + fan + channel + economy + horizon
    /// does not come close to fitting beside the worst class pair, which is
    /// exactly why the package tokens are registered.
    /// </summary>
    [Fact]
    public void EveryPackageIdentityFitsTheCanonicalBudget()
    {
        var expected = new Dictionary<string, (string Bare, string Legion)>(
            StringComparer.Ordinal)
        {
            ["bulwark-vs-bulwark"] = ("bastille", "palisade"),
            ["bulwark-vs-fabricator"] = ("warren", "swarm"),
            ["bulwark-vs-striker"] = ("vigil", "crusade"),
            ["fabricator-vs-fabricator"] = ("warren", "swarm"),
            ["fabricator-vs-striker"] = ("vigil", "crusade"),
            ["striker-vs-striker"] = ("vigil", "crusade"),
        };
        foreach ((FrontlineLabsClassDefinition zero,
                  FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            string pair = $"{zero.Id}-vs-{one.Id}";
            string bare = Package(zero, one).Rules.RulesetId;
            string legion = Package(
                    zero,
                    one,
                    FrontlineLabsRosterArm.Legion)
                .Rules.RulesetId;
            Assert.Equal(
                $"frontline-labs-1-{pair}-{expected[pair].Bare}"
                + "-facing-locked",
                bare);
            Assert.Equal(
                $"frontline-labs-1-{pair}-{expected[pair].Legion}"
                + "-facing-locked",
                legion);
            Assert.True(bare.Length <= 64, $"{bare} is {bare.Length}");
            Assert.True(legion.Length <= 64, $"{legion} is {legion.Length}");
            // The package is a different game from the keel-based one it
            // replaces, and says so.
            Assert.NotEqual(
                ActorContractFingerprint.ComputeMatch(Package(zero, one)),
                ActorContractFingerprint.ComputeMatch(
                    Package(
                        zero,
                        one,
                        pendulum: Keel,
                        horizon: FrontlineLabsHorizonArm.Standard)));
        }

        // A smaller hull cell still spells its factors, and the horizon is an
        // ordinary factor token there.
        Assert.Equal(
            "frontline-labs-1-experiment-hull-long",
            FrontlineLabsDefinition.CreatePendulumExperiment(
                    Hull,
                    horizon: FrontlineLabsHorizonArm.Long)
                .Rules.RulesetId);
        Assert.Equal(
            "frontline-labs-1-experiment-hull",
            FrontlineLabsDefinition.CreatePendulumExperiment(Hull)
                .Rules.RulesetId);
    }

    /// <summary>
    /// The whole package round-trips through the canonical mirror the
    /// admission validator runs — the limits, the topology, the economy block
    /// and the lifecycle placement together.
    /// </summary>
    [Fact]
    public void ThePackageRoundTripsThroughTheCanonicalMirror()
    {
        foreach ((FrontlineLabsClassDefinition zero,
                  FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            ActorResolvedMatchDefinition package = Package(
                zero,
                one,
                FrontlineLabsRosterArm.Legion);
            GenericActorCanonicalContractValidation validation =
                GenericActorCanonicalContractValidator.Validate(
                    ActorContractManifestSerializer.ToCanonicalJson(package));
            Assert.Equal(package.Rules.RulesetId, validation.RulesetId);
            Assert.Equal(
                ActorContractFingerprint.ComputeMatch(package),
                validation.MatchContractFingerprint);
        }
    }

    /// <summary>
    /// The horizon needs a cell to sit in, exactly like the channel and the
    /// economy: it re-prices every pacing gate both teams play against.
    /// </summary>
    [Fact]
    public void TheHorizonIsARealArmThatNeedsACell()
    {
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                horizon: FrontlineLabsHorizonArm.Long));
        // On a cell small enough to spell both, the horizon is exactly one
        // factor: same everything, one number and one token apart.
        ActorResolvedMatchDefinition standard =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Hull,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker));
        ActorResolvedMatchDefinition longer =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Hull,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                horizon: FrontlineLabsHorizonArm.Long);
        Assert.Equal(500, standard.Rules.Limits.MaxTicks);
        Assert.Equal(750, longer.Rules.Limits.MaxTicks);
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-hull-long",
            longer.Rules.RulesetId);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(standard.Rules),
            ActorContractFingerprint.ComputeRules(longer.Rules));
    }
}
