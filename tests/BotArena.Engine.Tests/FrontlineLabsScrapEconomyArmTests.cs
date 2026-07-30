using BotArena.ActorContracts;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the battlefield economy's contract and identity (DECISIONS #187,
/// <c>docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md</c> §14 as amended by parts
/// 2–3): the whole block round-trips through the canonical mirror, a ruleset
/// without an economy keeps byte-identical fingerprints, the deposit sites are
/// mirror-fair on the unchanged map, the arm refuses to share a cell with the
/// side objective, and every class-pair identity fits the 64-character
/// canonical budget.
/// </summary>
public sealed class FrontlineLabsScrapEconomyArmTests
{
    private const FrontlineLabsPendulumArm Keel =
        FrontlineLabsPendulumArm.StickyFrontline
        | FrontlineLabsPendulumArm.ForwardRally
        | FrontlineLabsPendulumArm.ContestMajority
        | FrontlineLabsPendulumArm.EnemySoleDecay;

    private const FrontlineLabsSkillKit WholeKit =
        FrontlineLabsSkillKit.StrikerVolley
        | FrontlineLabsSkillKit.BulwarkAegisShell
        | FrontlineLabsSkillKit.FabricatorFiveSlots;

    /// <summary>The wave-shaped candidate game, with and without the arm.</summary>
    private static ActorResolvedMatchDefinition FullGame(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsEconomyArm economy,
        FrontlineLabsCaptureArm capture =
            FrontlineLabsCaptureArm.Channel)
    {
        bool fabricator =
            teamZero.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots
            || teamOne.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots;
        return FrontlineLabsDefinition.CreatePendulumExperiment(
            Keel,
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
            capture: capture,
            economy: economy);
    }

    /// <summary>
    /// The additive discipline. A trailing mode block, a new action, a new
    /// parameter kind, and two published collections move no bytes at all for
    /// a ruleset that declares none of them — the immutable hosted v1
    /// included.
    /// </summary>
    [Fact]
    public void RulesetsWithoutAnEconomyKeepByteIdenticalFingerprints()
    {
        foreach (ActorResolvedMatchDefinition definition in new[]
                 {
                     FrontlineLabsDefinition.Create(),
                     FrontlineLabsDefinition.CreateOneBendShotsExperiment(),
                     FrontlineLabsDefinition.CreateClassesExperiment(
                         FrontlineLabsClassDefinition.Bulwark,
                         FrontlineLabsClassDefinition.Striker),
                     FullGame(
                         FrontlineLabsClassDefinition.Bulwark,
                         FrontlineLabsClassDefinition.Striker,
                         FrontlineLabsEconomyArm.None),
                 })
        {
            string canonical =
                ActorContractManifestSerializer.ToCanonicalJson(definition);
            foreach (string fact in new[]
                     {
                         "scrapEconomy",
                         "mode-investment",
                         "upgrade-track",
                         "invest",
                     })
            {
                Assert.DoesNotContain(
                    fact,
                    canonical,
                    StringComparison.Ordinal);
            }
        }

        ActorResolvedMatchDefinition hosted = FrontlineLabsDefinition.Create();
        Assert.Null(
            ((FrontlineGameModeDefinition)hosted.Rules.GameMode).ScrapEconomy);
        Assert.Equal(FrontlineLabsDefinition.RulesetId, hosted.Rules.RulesetId);
    }

    /// <summary>
    /// The economy's block is real contract data: the canonical writer emits
    /// it trailing on the mode, and the SDK canonical reader — the same
    /// parser the admission validator runs — reproduces the exact match
    /// fingerprint from it.
    /// </summary>
    [Fact]
    public void TheEconomyBlockRoundTripsThroughTheMirror()
    {
        ActorResolvedMatchDefinition scrap = FullGame(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsEconomyArm.Scrap);
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(scrap);

        Assert.Contains(
            "\"scrapEconomy\":{\"veinSites\":[{\"x\":11,\"y\":1},"
            + "{\"x\":11,\"y\":13}],\"veinFirstSpawnTick\":120,"
            + "\"veinSpawnIntervalTicks\":80,\"veinLastSpawnTick\":360,"
            + "\"veinAmount\":6,\"wreckAmount\":1,\"assayAmount\":1,"
            + "\"carryCapacity\":6,\"pileLifetimeTicks\":80,"
            + "\"maxSimultaneousPiles\":16,\"bankRegionIds\":"
            + "[\"team-0-home-pad\",\"team-1-home-pad\"],"
            + "\"upgradeScope\":\"prime-slot-lives-only\","
            + "\"maxTotalTiers\":3,\"purchaseMode\":\"invest-action\","
            + "\"tracks\":[{\"trackId\":\"edge\",\"effect\":"
            + "\"mobile-attack-travel-tiles-delta\",\"perTierMagnitude\":1,"
            + "\"maxTier\":2,\"tierCosts\":[10,10]},{\"trackId\":\"plate\","
            + "\"effect\":\"spawn-max-health-delta\",\"perTierMagnitude\":1,"
            + "\"maxTier\":2,\"tierCosts\":[10,10]},{\"trackId\":\"optic\","
            + "\"effect\":\"vision-range-delta\",\"perTierMagnitude\":1,"
            + "\"maxTier\":2,\"tierCosts\":[10,10]}]}",
            canonical,
            StringComparison.Ordinal);
        Assert.Contains(
            "{\"id\":\"invest\",\"code\":106,\"kind\":\"mode-investment\","
            + "\"parameterKinds\":[\"upgrade-track\"]}",
            canonical,
            StringComparison.Ordinal);

        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);
        var mode = (GenericActorRulesContract.FrontlineGameMode)
            contract.Rules.GameMode;
        GenericActorRulesContract.FrontlineScrapEconomy economy =
            Assert.IsType<GenericActorRulesContract.FrontlineScrapEconomy>(
                mode.ScrapEconomy);
        Assert.Equal(
            [(11, 1), (11, 13)],
            economy.VeinSites.Select(site => (site.X, site.Y)));
        Assert.Equal(120, economy.VeinFirstSpawnTick);
        Assert.Equal(80, economy.VeinSpawnIntervalTicks);
        Assert.Equal(360, economy.VeinLastSpawnTick);
        Assert.Equal(6, economy.VeinAmount);
        Assert.Equal(1, economy.WreckAmount);
        Assert.Equal(1, economy.AssayAmount);
        Assert.Equal(6, economy.CarryCapacity);
        Assert.Equal(80, economy.PileLifetimeTicks);
        Assert.Equal(16, economy.MaxSimultaneousPiles);
        Assert.Equal(
            ["team-0-home-pad", "team-1-home-pad"],
            economy.BankRegionIds.ToArray());
        Assert.Equal("prime-slot-lives-only", economy.UpgradeScope);
        Assert.Equal(3, economy.MaxTotalTiers);
        Assert.Equal("invest-action", economy.PurchaseMode);
        Assert.Equal(
            ["edge", "plate", "optic"],
            economy.Tracks.Select(track => track.TrackId).ToArray());
        Assert.All(
            economy.Tracks,
            track =>
            {
                Assert.Equal(1, track.PerTierMagnitude);
                Assert.Equal(2, track.MaxTier);
                Assert.Equal([10, 10], track.TierCosts.ToArray());
            });
        Assert.Equal(
            GenericActorRulesContract.ActionKind.ModeInvestment,
            contract.Rules.Actions
                .Single(action => action.Id == "invest")
                .Kind);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(scrap),
            contract.MatchContractFingerprint);

        GenericActorCanonicalContractValidation validation =
            GenericActorCanonicalContractValidator.Validate(canonical);
        Assert.Equal(scrap.Rules.RulesetId, validation.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(scrap),
            validation.MatchContractFingerprint);
    }

    /// <summary>
    /// The control level is the same economy with the spend side removed: one
    /// contract key moves, the <c>invest</c> verb disappears from the action
    /// catalog entirely, and the two rulesets are therefore distinct
    /// fingerprints — which is what makes the falsification cell readable.
    /// </summary>
    [Fact]
    public void TheFlatControlDropsTheVerbAndKeepsTheLadder()
    {
        ActorResolvedMatchDefinition arm = FullGame(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsEconomyArm.Scrap);
        ActorResolvedMatchDefinition control = FullGame(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsEconomyArm.ScrapFlat);

        FrontlineScrapEconomyDefinition armEconomy =
            ((FrontlineGameModeDefinition)arm.Rules.GameMode).ScrapEconomy!;
        FrontlineScrapEconomyDefinition controlEconomy =
            ((FrontlineGameModeDefinition)control.Rules.GameMode)
                .ScrapEconomy!;
        Assert.Equal(
            FrontlineScrapEconomyDefinition.PurchaseModeKind.InvestAction,
            armEconomy.PurchaseMode);
        Assert.Equal(
            FrontlineScrapEconomyDefinition.PurchaseModeKind
                .AutomaticGreedyDeclaredOrder,
            controlEconomy.PurchaseMode);
        Assert.True(armEconomy.Tracks.SequenceEqual(controlEconomy.Tracks));
        Assert.True(
            armEconomy.VeinSites.SequenceEqual(controlEconomy.VeinSites));
        Assert.Equal(armEconomy.CarryCapacity, controlEconomy.CarryCapacity);

        Assert.Contains(
            arm.Rules.Actions,
            action => action.Kind == ActorActionKind.ModeInvestment);
        Assert.DoesNotContain(
            control.Rules.Actions,
            action => action.Kind == ActorActionKind.ModeInvestment);
        Assert.DoesNotContain(
            control.Rules.Forms,
            form => form.AllowedActionIds.Contains(
                "invest",
                StringComparer.Ordinal));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(arm),
            ActorContractFingerprint.ComputeMatch(control));
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-siege-flat-facing-locked",
            control.Rules.RulesetId);
        Assert.True(control.Rules.RulesetId.Length <= 64);
    }

    /// <summary>
    /// The deposit addresses are mirror-fair by construction rather than by
    /// assertion: the map's tile rows are palindromic about the centre column,
    /// so a site on <c>x = 11</c> is the same distance from both home pads.
    /// The arm therefore mints NO new map generation, which is what keeps it
    /// fingerprint-comparable to every arm measured to date.
    /// </summary>
    [Fact]
    public void TheDepositSitesAreMirrorFairOnTheUnchangedMap()
    {
        ActorResolvedMatchDefinition plain =
            FrontlineLabsDefinition.CreateClassesExperiment(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker);
        ActorResolvedMatchDefinition scrap = FullGame(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsEconomyArm.Scrap);

        Assert.Equal(plain.Map.Id, scrap.Map.Id);
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(plain.Map),
            ActorContractFingerprint.ComputeMap(scrap.Map));

        foreach (Position site in FrontlineLabsScrapEconomy.VeinSites)
        {
            Assert.False(scrap.Map.IsWall(site));
            // The mirror is the map's own: reflecting a site across the
            // centre column lands on a floor tile too, and reflecting across
            // the centre row lands on the OTHER declared site.
            Assert.False(
                scrap.Map.IsWall(
                    new Position(scrap.Map.Width - 1 - site.X, site.Y)));
            Assert.Contains(
                new Position(site.X, scrap.Map.Height - 1 - site.Y),
                FrontlineLabsScrapEconomy.VeinSites);
        }

        // Both lanes are open corridors, so every site has two approach
        // headings without widening anything.
        foreach (Position site in FrontlineLabsScrapEconomy.VeinSites)
        {
            Assert.False(scrap.Map.IsWall(site.Offset(-1, 0)));
            Assert.False(scrap.Map.IsWall(site.Offset(1, 0)));
        }
    }

    /// <summary>
    /// The two side-lane arms are mutually exclusive, and the guard sits on
    /// the typed mode itself rather than only on the CLI: both claim the side
    /// lanes' attention, so a cell carrying both could attribute neither.
    /// </summary>
    [Fact]
    public void TheEconomyAndTheSideObjectiveCannotShareACell()
    {
        ArgumentException composed = Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                sideObjective: FrontlineLabsSideObjectiveArm.Muster,
                economy: FrontlineLabsEconomyArm.Scrap));
        Assert.Contains(
            "mutually exclusive",
            composed.Message,
            StringComparison.Ordinal);

        FrontlineGameModeDefinition mode =
            (FrontlineGameModeDefinition)FrontlineLabsDefinition
                .Create()
                .Rules
                .GameMode;
        Assert.Throws<ArgumentException>(() =>
            new FrontlineGameModeDefinition(
                mode.FrontlineVictory,
                mode.ScoreCatalog,
                mode.FrontlinePositionCount,
                mode.Capture,
                new FrontlineSecondaryControlDefinition(
                    ["team-0-home-pad"],
                    20,
                    FrontlineSecondaryControlDefinition
                        .SecondaryOwnershipKind
                        .LatchedUntilRecapturedBySoleObjectiveWeight,
                    FrontlineSecondaryControlDefinition.SecondaryEffectKind
                        .Muster,
                    FrontlineSecondaryControlDefinition
                        .SecondaryRallyScopeKind.PrimeAutomaticReturnOnly),
                FrontlineLabsScrapEconomy.For(
                    FrontlineLabsEconomyArm.Scrap)));
    }

    /// <summary>
    /// Every registered identity fits the 64-character canonical budget in
    /// the worst cell — the longest class pair beside <c>facing-locked</c> —
    /// and the arm is never inert-omitted, so each of the six pairs mints a
    /// distinct contract.
    /// </summary>
    [Fact]
    public void EveryEconomyIdentityFitsTheCanonicalBudget()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bulwark-vs-bulwark"] = "smithy",
            ["bulwark-vs-fabricator"] = "redoubt",
            ["bulwark-vs-striker"] = "bastion",
            ["fabricator-vs-fabricator"] = "redoubt",
            ["fabricator-vs-striker"] = "bastion",
            ["striker-vs-striker"] = "bastion",
        };
        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        foreach ((FrontlineLabsClassDefinition zero,
                     FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            string pair = $"{zero.Id}-vs-{one.Id}";
            ActorResolvedMatchDefinition definition = FullGame(
                zero,
                one,
                FrontlineLabsEconomyArm.Scrap);
            string id = definition.Rules.RulesetId;
            Assert.True(id.Length <= 64, $"{id} is {id.Length}");
            Assert.Equal(
                $"frontline-labs-1-{pair}-{expected[pair]}-facing-locked",
                id);
            Assert.True(
                fingerprints.Add(
                    ActorContractFingerprint.ComputeMatch(definition)),
                $"{pair} shares a contract fingerprint");
            Assert.NotEqual(
                ActorContractFingerprint.ComputeMatch(definition),
                ActorContractFingerprint.ComputeMatch(
                    FullGame(zero, one, FrontlineLabsEconomyArm.None)));
        }

        // The economy alone (no channel) mints `forge`, which is the wave-8
        // 2x2's third cell.
        string forge = FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: WholeKit,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
                stanceGround: FrontlineLabsStanceGroundArm.Open,
                aim: FrontlineLabsAimArm.Offset,
                cooldown: FrontlineLabsCooldownArm.Ticking,
                volley: FrontlineLabsVolleyArm.Salvo,
                capture: FrontlineLabsCaptureArm.Frozen,
                economy: FrontlineLabsEconomyArm.Scrap)
            .Rules
            .RulesetId;
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-forge-facing-locked",
            forge);

        // A bare pendulum cell spells the plain token.
        string bare = FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                economy: FrontlineLabsEconomyArm.Scrap)
            .Rules
            .RulesetId;
        Assert.Equal("frontline-labs-1-experiment-keel-scrap", bare);
        Assert.True(bare.Length <= 64);
    }

    /// <summary>
    /// The arm is a real arm on every pair and needs a cell to sit in — the
    /// same rule the side objective and the channel follow, and for the same
    /// reason: it changes the game for both teams whatever chassis they are.
    /// </summary>
    [Fact]
    public void TheEconomyNeedsACellToSitIn()
    {
        ArgumentException failure = Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                economy: FrontlineLabsEconomyArm.Scrap));
        Assert.Contains(
            "needs a cell to sit in",
            failure.Message,
            StringComparison.Ordinal);
    }
}
