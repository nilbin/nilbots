using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the phase-2 composite arm identities (DECISIONS #169). Phase 2 is
/// keel + facing-locked on every cell, factored by skill kit (off/on) and bend
/// envelope (striker-only/universal) across six class pairs. Per-factor
/// spelling overflows the 64-character canonical ID budget on the wider cells,
/// so the three combinations the factorial actually runs are registered under
/// one token each — <c>helm</c>, <c>veer</c>, and <c>rig</c> — following the
/// <c>keel</c> precedent: the token is equivalent to the explicit
/// composition, each combination is a distinct content-identified ruleset,
/// and every cell round-trips through the canonical mirror.
/// </summary>
public sealed class FrontlineLabsPhaseTwoArmIdentityTests
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

    private static readonly FrontlineLabsClassDefinition[] Classes =
    [
        FrontlineLabsClassDefinition.Bulwark,
        FrontlineLabsClassDefinition.Fabricator,
        FrontlineLabsClassDefinition.Striker,
    ];

    /// <summary>
    /// The worst class cell is the longest pair (<c>fabricator-vs-fabricator</c>,
    /// 24 characters) beside the longest coupling token, and every registered
    /// composite has to land inside 64 there. Pin all three literally.
    /// </summary>
    public static TheoryData<FrontlineLabsSkillKit,
        FrontlineLabsBendEnvelopeArm, string> WorstCellIdentities() =>
        new()
        {
            {
                WholeKit,
                FrontlineLabsBendEnvelopeArm.StrikerOnly,
                "frontline-labs-1-fabricator-vs-fabricator-helm-facing-locked"
            },
            {
                FrontlineLabsSkillKit.None,
                FrontlineLabsBendEnvelopeArm.Universal,
                "frontline-labs-1-fabricator-vs-fabricator-veer-facing-locked"
            },
            {
                WholeKit,
                FrontlineLabsBendEnvelopeArm.Universal,
                "frontline-labs-1-fabricator-vs-fabricator-rig-facing-locked"
            },
        };

    [Theory]
    [MemberData(nameof(WorstCellIdentities))]
    public void EachRegisteredCompositeFitsTheWorstClassCell(
        FrontlineLabsSkillKit skills,
        FrontlineLabsBendEnvelopeArm bendEnvelope,
        string expectedRulesetId)
    {
        ActorResolvedMatchDefinition worst =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Fabricator,
                    FrontlineLabsClassDefinition.Fabricator),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: skills,
                bendEnvelope: bendEnvelope);

        Assert.Equal(expectedRulesetId, worst.Rules.RulesetId);
        Assert.True(
            worst.Rules.RulesetId.Length <= 64,
            $"{worst.Rules.RulesetId} needs {worst.Rules.RulesetId.Length} "
            + "canonical characters");
    }

    /// <summary>
    /// The registration exists because the spelled forms do not fit. Even the
    /// narrowest phase-2 addition — keel plus the bend envelope, no kit —
    /// needs 65 characters on the worst cell, which is the single character
    /// DECISIONS #169 records.
    /// </summary>
    [Fact]
    public void PerFactorSpellingOverflowsWhereTheRegisteredTokensFit()
    {
        // keel + a PARTIAL kit is unregistered by design (a result on one
        // stance is weak evidence about the other, so a half kit is not a
        // level), and it still spells itself out — under the cap here.
        ActorResolvedMatchDefinition partial =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Fabricator),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: FrontlineLabsSkillKit.FabricatorFiveSlots);
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-fabricator-keel-slot5-facing-locked",
            partial.Rules.RulesetId);

        // ...and over it as soon as the same partial kit gains the bend.
        Assert.Contains(
            "register the combination under a shorter token",
            Assert.Throws<InvalidOperationException>(() =>
                FrontlineLabsDefinition.CreatePendulumExperiment(
                    Keel,
                    (FrontlineLabsClassDefinition.Bulwark,
                        FrontlineLabsClassDefinition.Fabricator),
                    movementCoupling:
                        ActorMovementFacingCoupling.FacingLocked,
                    skills: FrontlineLabsSkillKit.FabricatorFiveSlots,
                    bendEnvelope:
                        FrontlineLabsBendEnvelopeArm.Universal)).Message,
            StringComparison.Ordinal);

        // The one-character overflow DECISIONS #169 records: the narrowest
        // phase-2 addition on the widest pair spells `keel-bend` and needs 65
        // of the 64 canonical characters, where `veer` needs 60.
        ActorResolvedMatchDefinition veer =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Fabricator,
                    FrontlineLabsClassDefinition.Fabricator),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal);
        Assert.Equal(60, veer.Rules.RulesetId.Length);
        Assert.Equal(
            65,
            veer.Rules.RulesetId
                .Replace("veer", "keel-bend", StringComparison.Ordinal)
                .Length);
    }

    /// <summary>
    /// A registered token names a COMBINATION, not a spelling. The kit
    /// resolves per class exactly as <c>--skills kit</c> already does, so on a
    /// single-class cell "the whole kit" and "that cell's one skill" must be
    /// the same ruleset, bytes included.
    /// </summary>
    [Theory]
    [InlineData(
        "fabricator",
        "fabricator",
        FrontlineLabsSkillKit.FabricatorFiveSlots)]
    [InlineData(
        "striker",
        "striker",
        FrontlineLabsSkillKit.StrikerVolley)]
    public void TheRegisteredTokenEqualsItsExplicitPerClassComposition(
        string teamZero,
        string teamOne,
        FrontlineLabsSkillKit cellSkills)
    {
        var pair = (
            FrontlineLabsClassDefinition.Parse(teamZero),
            FrontlineLabsClassDefinition.Parse(teamOne));
        ActorResolvedMatchDefinition asWholeKit =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                pair,
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: WholeKit,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal);
        ActorResolvedMatchDefinition asCellSkills =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                pair,
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: cellSkills,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal);

        Assert.Equal(
            asWholeKit.Rules.RulesetId,
            asCellSkills.Rules.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeRules(asWholeKit.Rules),
            ActorContractFingerprint.ComputeRules(asCellSkills.Rules));
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(asWholeKit),
            ActorContractFingerprint.ComputeMatch(asCellSkills));
        Assert.EndsWith(
            "-rig-facing-locked",
            asWholeKit.Rules.RulesetId,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The four phase-2 factor combinations on one pair must be four rulesets:
    /// a shared name or shared bytes would make the factorial unmeasurable.
    /// The kit-off/striker-only cell is keel itself — the phase-1b
    /// replication anchor — and keeps its existing identity.
    /// </summary>
    [Fact]
    public void TheFourFactorCombinationsAreFourDistinctRulesets()
    {
        var pair = (
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker);
        (FrontlineLabsSkillKit Skills,
            FrontlineLabsBendEnvelopeArm Bend,
            string Suffix)[] cells =
        [
            (FrontlineLabsSkillKit.None,
                FrontlineLabsBendEnvelopeArm.StrikerOnly,
                "keel"),
            (WholeKit, FrontlineLabsBendEnvelopeArm.StrikerOnly, "helm"),
            (FrontlineLabsSkillKit.None,
                FrontlineLabsBendEnvelopeArm.Universal,
                "veer"),
            (WholeKit, FrontlineLabsBendEnvelopeArm.Universal, "rig"),
        ];

        var rulesetIds = new HashSet<string>();
        var rulesFingerprints = new HashSet<string>();
        var matchFingerprints = new HashSet<string>();
        string mapFingerprint = string.Empty;
        foreach (var cell in cells)
        {
            ActorResolvedMatchDefinition definition =
                FrontlineLabsDefinition.CreatePendulumExperiment(
                    Keel,
                    pair,
                    movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                    skills: cell.Skills,
                    bendEnvelope: cell.Bend);

            Assert.Equal(
                $"frontline-labs-1-bulwark-vs-striker-{cell.Suffix}"
                + "-facing-locked",
                definition.Rules.RulesetId);
            Assert.True(rulesetIds.Add(definition.Rules.RulesetId));
            Assert.True(
                rulesFingerprints.Add(
                    ActorContractFingerprint.ComputeRules(definition.Rules)),
                $"duplicate rules bytes for {definition.Rules.RulesetId}");
            Assert.True(
                matchFingerprints.Add(
                    ActorContractFingerprint.ComputeMatch(definition)),
                $"duplicate match bytes for {definition.Rules.RulesetId}");

            // Phase 2 drops the movement factor and holds the map constant,
            // so the only moving parts are the kit and the bend envelope.
            string map = ActorContractFingerprint.ComputeMap(definition.Map);
            if (mapFingerprint.Length == 0)
                mapFingerprint = map;
            Assert.Equal(mapFingerprint, map);
        }
    }

    /// <summary>
    /// The whole registered factorial: 24 cells, every one keel plus
    /// facing-locked, uniquely named and uniquely fingerprinted, each one a
    /// canonical contract its own mirror accepts.
    /// </summary>
    [Fact]
    public void EveryPhaseTwoCellIsADistinctCanonicalRulesetUnderTheCap()
    {
        FrontlineLabsSkillKit[] kitLevels =
            [FrontlineLabsSkillKit.None, WholeKit];
        FrontlineLabsBendEnvelopeArm[] bendLevels =
        [
            FrontlineLabsBendEnvelopeArm.StrikerOnly,
            FrontlineLabsBendEnvelopeArm.Universal,
        ];
        var rulesetIds = new HashSet<string>();
        var matchFingerprints = new HashSet<string>();

        foreach (var pair in CanonicalPairs())
        {
            foreach (FrontlineLabsSkillKit skills in kitLevels)
            {
                foreach (FrontlineLabsBendEnvelopeArm bend in bendLevels)
                {
                    ActorResolvedMatchDefinition definition =
                        FrontlineLabsDefinition.CreatePendulumExperiment(
                            Keel,
                            (pair.TeamZero, pair.TeamOne),
                            movementCoupling:
                                ActorMovementFacingCoupling.FacingLocked,
                            skills: skills,
                            bendEnvelope: bend);

                    Assert.True(
                        definition.Rules.RulesetId.Length <= 64,
                        $"{definition.Rules.RulesetId} exceeds the canonical "
                        + "ID budget");
                    Assert.True(
                        rulesetIds.Add(definition.Rules.RulesetId),
                        $"duplicate ruleset {definition.Rules.RulesetId}");
                    Assert.True(
                        matchFingerprints.Add(
                            ActorContractFingerprint.ComputeMatch(definition)),
                        $"duplicate fingerprint for "
                        + definition.Rules.RulesetId);
                    Assert.Equal(
                        ActorMovementFacingCoupling.FacingLocked,
                        definition.Rules.MovementProfiles.Single()
                            .FacingCoupling);
                    Assert.Equal(
                        FrontlineLabsDefinition.ClassesSeedProfileId,
                        definition.Rules.SeedMechanics.SeedProfileId);

                    GenericActorCanonicalContractValidation validation =
                        GenericActorCanonicalContractValidator.Validate(
                            ActorContractManifestSerializer.ToCanonicalJson(
                                definition));
                    Assert.Equal(
                        definition.Rules.RulesetId,
                        validation.RulesetId);
                    Assert.Equal(
                        ActorContractFingerprint.ComputeMatch(definition),
                        validation.MatchContractFingerprint);
                }
            }
        }

        Assert.Equal(24, rulesetIds.Count);
        Assert.Equal(24, matchFingerprints.Count);
    }

    /// <summary>
    /// A registered token is one token in every position, exactly as
    /// <c>keel</c> is: dropping the coupling must not rename the arm.
    /// </summary>
    [Fact]
    public void TheCompositeTokensDoNotChangeWithoutTheCouplingToken()
    {
        ActorResolvedMatchDefinition rig =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Fabricator),
                skills: WholeKit,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal);

        Assert.Equal(
            "frontline-labs-1-bulwark-vs-fabricator-rig",
            rig.Rules.RulesetId);
        Assert.Equal(
            $"{FrontlineLabsDefinition.MapId}-classes",
            rig.Map.Id);
    }

    /// <summary>
    /// The composites are keel-based by construction: a lesser pendulum level
    /// carrying the same kit and bend is not <c>rig</c>, and says so by
    /// spelling itself out (or overflowing while trying).
    /// </summary>
    [Fact]
    public void ALesserPendulumLevelNeverBorrowsAComposedIdentity()
    {
        ActorResolvedMatchDefinition ratchetKit =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally,
                (FrontlineLabsClassDefinition.Striker,
                    FrontlineLabsClassDefinition.Striker),
                skills: WholeKit,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal);

        Assert.Equal(
            "frontline-labs-1-striker-vs-striker-ratchet-cast-bend",
            ratchetKit.Rules.RulesetId);
        Assert.DoesNotContain(
            "rig",
            ratchetKit.Rules.RulesetId,
            StringComparison.Ordinal);
    }

    private static IEnumerable<(
        FrontlineLabsClassDefinition TeamZero,
        FrontlineLabsClassDefinition TeamOne)> CanonicalPairs()
    {
        for (int first = 0; first < Classes.Length; first++)
        {
            for (int second = first; second < Classes.Length; second++)
                yield return (Classes[first], Classes[second]);
        }
    }
}
