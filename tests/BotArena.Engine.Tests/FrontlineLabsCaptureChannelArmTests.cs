using BotArena.ActorContracts;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the capture channel's contract and identity (DECISIONS #187,
/// <c>docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md</c> parts 2–3): the three
/// trailing capture facts round-trip through the canonical mirror, a ruleset
/// that does not channel keeps byte-identical fingerprints, the arm carries
/// its own paired threshold, the inert encoding is refused, and every
/// class-pair identity fits the 64-character canonical budget.
/// </summary>
public sealed class FrontlineLabsCaptureChannelArmTests
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

    private const string ChannelControlPolicy =
        "stationary-claim-weight-versus-total-denial-weight-scales-gain-"
        + "capped-opposition-erodes-at-multiple-then-builds";

    /// <summary>The wave-shaped candidate game, with and without the channel.</summary>
    private static ActorResolvedMatchDefinition FullGame(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsCaptureArm capture)
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
            capture: capture);
    }

    /// <summary>
    /// The additive discipline, again: appending a control-policy value and
    /// three trailing capture fields moves no bytes for anyone who does not
    /// declare them. Every existing arm — the immutable hosted v1 included —
    /// keeps its exact rules, map, and aggregate match fingerprints.
    /// </summary>
    [Fact]
    public void RulesetsWithoutTheChannelKeepByteIdenticalFingerprints()
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
                         FrontlineLabsCaptureArm.Frozen),
                 })
        {
            string canonical =
                ActorContractManifestSerializer.ToCanonicalJson(definition);
            foreach (string fact in new[]
                     {
                         "stationaryGainMultiplierCap",
                         "opposingErosionMultiplier",
                         "claimInterrupt",
                     })
            {
                Assert.DoesNotContain(
                    fact,
                    canonical,
                    StringComparison.Ordinal);
            }
        }

        ActorResolvedMatchDefinition hosted = FrontlineLabsDefinition.Create();
        FrontlineCaptureDefinition capture =
            ((FrontlineGameModeDefinition)hosted.Rules.GameMode).Capture;
        Assert.Equal(FrontlineLabsDefinition.RulesetId, hosted.Rules.RulesetId);
        Assert.Equal(
            FrontlineLabsDefinition.DefaultCaptureThreshold,
            capture.Threshold);
        Assert.Equal(0, capture.StationaryGainMultiplierCap);
        Assert.Equal(0, capture.OpposingErosionMultiplier);
        Assert.Null(capture.ClaimInterrupt);
    }

    /// <summary>
    /// The channel's facts are real contract data: the canonical writer emits
    /// them trailing, in one block, and the SDK canonical reader — the same
    /// parser the admission validator runs — reproduces the exact match
    /// fingerprint from them.
    /// </summary>
    [Fact]
    public void TheChannelFactsRoundTripThroughTheMirror()
    {
        ActorResolvedMatchDefinition channel = FullGame(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsCaptureArm.Channel);
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(channel);

        Assert.Contains(
            $"\"controlPolicy\":\"{ChannelControlPolicy}\"",
            canonical,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"redeployTickArithmetic\":\"checked-int64-capture-tick-plus-"
            + "one-plus-pause-require-int32\",\"stationaryGainMultiplierCap\""
            + $":{FrontlineLabsDefinition.ChannelStationaryGainMultiplierCap}"
            + ",\"opposingErosionMultiplier\":"
            + $"{FrontlineLabsDefinition.ChannelOpposingErosionMultiplier}"
            + ",\"claimInterrupt\":{\"kind\":\"damage-to-controller-on-"
            + "objective-reverts-work\",\"revertPerDamagePoint\":1,"
            + "\"scope\":\"controlling-team-bodies-on-active-objective-"
            + "region\",\"granularity\":\"whole-run\"}",
            canonical,
            StringComparison.Ordinal);

        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);
        var mode = (GenericActorRulesContract.FrontlineGameMode)
            contract.Rules.GameMode;
        Assert.Equal(ChannelControlPolicy, mode.Capture.ControlPolicy);
        Assert.Equal(
            FrontlineLabsDefinition.ChannelCaptureThreshold,
            mode.Capture.Threshold);
        Assert.Equal(
            FrontlineLabsDefinition.ChannelStationaryGainMultiplierCap,
            mode.Capture.StationaryGainMultiplierCap);
        Assert.Equal(
            FrontlineLabsDefinition.ChannelOpposingErosionMultiplier,
            mode.Capture.OpposingErosionMultiplier);
        GenericActorRulesContract.FrontlineClaimInterrupt interrupt =
            Assert.IsType<GenericActorRulesContract.FrontlineClaimInterrupt>(
                mode.Capture.ClaimInterrupt);
        Assert.Equal(
            "damage-to-controller-on-objective-reverts-work",
            interrupt.Kind);
        Assert.Equal(1, interrupt.RevertPerDamagePoint);
        Assert.Equal(
            "controlling-team-bodies-on-active-objective-region",
            interrupt.Scope);
        Assert.Equal("whole-run", interrupt.Granularity);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(channel),
            contract.MatchContractFingerprint);

        GenericActorCanonicalContractValidation validation =
            GenericActorCanonicalContractValidator.Validate(canonical);
        Assert.Equal(channel.Rules.RulesetId, validation.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(channel),
            validation.MatchContractFingerprint);
    }

    /// <summary>
    /// The paired factor. Leaving the threshold alone on a channel cell gets
    /// the arm's own 8, and the identity spells nothing extra for it —
    /// naming a different threshold is the channel-speed ablation level and
    /// does spell its number.
    /// </summary>
    [Fact]
    public void TheChannelCarriesItsOwnPairedThreshold()
    {
        ActorResolvedMatchDefinition shipped = FullGame(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsCaptureArm.Channel);
        Assert.Equal(
            FrontlineLabsDefinition.ChannelCaptureThreshold,
            ((FrontlineGameModeDefinition)shipped.Rules.GameMode)
                .Capture.Threshold);
        Assert.DoesNotContain(
            "c8",
            shipped.Rules.RulesetId,
            StringComparison.Ordinal);

        ActorResolvedMatchDefinition level =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                captureThreshold: 12,
                capture: FrontlineLabsCaptureArm.Channel);
        Assert.Equal(
            12,
            ((FrontlineGameModeDefinition)level.Rules.GameMode)
                .Capture.Threshold);
        Assert.Contains(
            "c12",
            level.Rules.RulesetId,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The three settings and the policy travel together. Neither half of the
    /// inert encoding is a second way to spell the same contract.
    /// </summary>
    [Fact]
    public void TheChannelSettingsAndTheChannelPolicyTravelTogether()
    {
        Assert.Throws<ArgumentException>(() => Capture(
            FrontlineCaptureDefinition.ControlPolicyKind
                .StationaryClaimWeightVersusTotalDenialWeightScalesGainCappedOppositionErodesAtMultipleThenBuilds,
            cap: 0,
            erosion: 4,
            FrontlineClaimInterruptDefinition.DamageRevertsWork));
        Assert.Throws<ArgumentException>(() => Capture(
            FrontlineCaptureDefinition.ControlPolicyKind
                .StationaryClaimWeightVersusTotalDenialWeightScalesGainCappedOppositionErodesAtMultipleThenBuilds,
            cap: 2,
            erosion: 4,
            claimInterrupt: null));
        Assert.Throws<ArgumentException>(() => Capture(
            FrontlineCaptureDefinition.ControlPolicyKind
                .NetPositiveObjectiveWeightDifferenceScalesGainNonPositiveAppliesConfiguredDecayOppositionErodesToNeutral,
            cap: 2,
            erosion: 4,
            FrontlineClaimInterruptDefinition.DamageRevertsWork));

        // And the reader refuses an explicitly inert encoding of the same
        // contract, exactly as it does for the ratchet's hold.
        string canonical = ActorContractManifestSerializer.ToCanonicalJson(
            FullGame(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsCaptureArm.Frozen));
        string forged = canonical.Replace(
            "\"redeployTickArithmetic\":\"checked-int64-capture-tick-plus-"
            + "one-plus-pause-require-int32\"",
            "\"redeployTickArithmetic\":\"checked-int64-capture-tick-plus-"
            + "one-plus-pause-require-int32\",\"stationaryGainMultiplierCap\""
            + ":2",
            StringComparison.Ordinal);
        Assert.NotEqual(canonical, forged);
        Assert.Throws<FormatException>(() =>
            ActorCanonicalContractReader.Parse(forged));
    }

    /// <summary>
    /// The identity budget. The channel re-mints the whole game, so every
    /// canonical class pair has to spell the candidate game plus the channel
    /// inside 64 characters — which is exactly why the three registered
    /// composites exist.
    /// </summary>
    [Fact]
    public void EveryChannelIdentityFitsTheCanonicalBudget()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bulwark-vs-bulwark"] = "mantlet",
            ["bulwark-vs-fabricator"] = "sap",
            ["bulwark-vs-striker"] = "siege",
            ["fabricator-vs-fabricator"] = "sap",
            ["fabricator-vs-striker"] = "siege",
            ["striker-vs-striker"] = "siege",
        };
        foreach ((FrontlineLabsClassDefinition zero,
                  FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            string pair = $"{zero.Id}-vs-{one.Id}";
            string id = FullGame(zero, one, FrontlineLabsCaptureArm.Channel)
                .Rules.RulesetId;
            Assert.True(id.Length <= 64, $"{id} is {id.Length}");
            Assert.Equal(
                $"frontline-labs-1-{pair}-{expected[pair]}-facing-locked",
                id);
        }

        // A bare arm still spells itself, and still fits.
        string bare = FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                capture: FrontlineLabsCaptureArm.Channel)
            .Rules.RulesetId;
        Assert.Equal(
            "frontline-labs-1-experiment-keel-channel",
            bare);
    }

    /// <summary>
    /// A capture-core change is a real arm on every pair: it changes capture
    /// for both teams whatever chassis are in the cell, so it is never
    /// inert-omitted, and it needs a cell to sit in.
    /// </summary>
    [Fact]
    public void TheChannelIsARealArmOnEveryPairAndNeedsACell()
    {
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                capture: FrontlineLabsCaptureArm.Channel));

        // Even the fabricator mirror — which inert-omits the salvo — carries
        // a different contract with the channel than without it.
        foreach ((FrontlineLabsClassDefinition zero,
                  FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            Assert.NotEqual(
                ActorContractFingerprint.ComputeMatch(
                    FullGame(zero, one, FrontlineLabsCaptureArm.Frozen)),
                ActorContractFingerprint.ComputeMatch(
                    FullGame(zero, one, FrontlineLabsCaptureArm.Channel)));
        }
    }

    private static FrontlineCaptureDefinition Capture(
        FrontlineCaptureDefinition.ControlPolicyKind controlPolicy,
        int cap,
        int erosion,
        FrontlineClaimInterruptDefinition? claimInterrupt) =>
        new(
            threshold: 8,
            gainPerSoleTeamTick: 1,
            decayAmount: 1,
            decayIntervalTicks: 2,
            redeployPauseTicks: 5,
            gainSchedule: null,
            controlPolicy,
            FrontlineCaptureDefinition.DecayClockKind
                .ConsecutiveEmptyOrContestedTicksResetByAnySoleControl,
            FrontlineCaptureDefinition.RedeployPolicyKind
                .AdvanceImmediatelyResetClaimKeepWorldPauseThroughCapturePlusConfiguredTicksBreachSkipsPause,
            ratchetHoldTicks: 0,
            cap,
            erosion,
            claimInterrupt);
}
