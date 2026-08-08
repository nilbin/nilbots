using BotArena.Engine;

namespace BotArena.Engine.Tests;

/// <summary>
/// Robust-play foundations (owner goal 2026-08-05): forward-combat-03 adds
/// seed-derived well-birth jitter, minted BESIDE -02 so historical bytes
/// never move.
/// </summary>
public sealed class ArcRelayFoundations3Tests
{
    [Fact]
    public void Foundations3MintsANewFingerprintAndLeavesPriorRulesAlone()
    {
        string one = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ForwardCombat));
        string two = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ForwardCombat2));
        string three = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ForwardCombat3));
        Assert.NotEqual(one, three);
        Assert.NotEqual(two, three);
        // Re-deriving -01/-02 beside -03 must reproduce their bytes exactly.
        Assert.Equal(
            one,
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.ForwardCombat)));
        Assert.Equal(
            two,
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.ForwardCombat2)));
    }

    [Fact]
    public void JitterIsWrittenCanonicallyOnlyWhenNonZero()
    {
        string two = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ForwardCombat2));
        string three = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.ForwardCombat3));
        Assert.DoesNotContain("wellBirthJitterTicks", two);
        Assert.Contains("\"wellBirthJitterTicks\":6", three);
        Assert.DoesNotContain("alternatingResolutionOrder", two);
        Assert.Contains("\"alternatingResolutionOrder\":true", three);
        // Order-strict contract: the jitter field sits between the grammar
        // version and the wells array.
        Assert.True(
            three.IndexOf("\"signatureGrammarVersion\"", StringComparison.Ordinal)
            < three.IndexOf("\"wellBirthJitterTicks\"", StringComparison.Ordinal));
        Assert.True(
            three.IndexOf("\"wellBirthJitterTicks\"", StringComparison.Ordinal)
            < three.IndexOf("\"wells\"", StringComparison.Ordinal));
    }

    [Fact]
    public void WellBirthDrawIsDeterministicPerSeedWellAndRound()
    {
        ulong a = SeedDerivation.DeriveWellBirthDraw(424242, "north", 0);
        Assert.Equal(
            a,
            SeedDerivation.DeriveWellBirthDraw(424242, "north", 0));
        // The draw separates every axis: seed, well, and round.
        Assert.NotEqual(
            a,
            SeedDerivation.DeriveWellBirthDraw(424243, "north", 0));
        Assert.NotEqual(
            a,
            SeedDerivation.DeriveWellBirthDraw(424242, "south", 0));
        Assert.NotEqual(
            a,
            SeedDerivation.DeriveWellBirthDraw(424242, "north", 1));
    }

    [Fact]
    public void JitterWindowMustStayInsideTheCadence()
    {
        ActorRulesDefinition rules = ArcRelayH0Definition.CreateRules(
            ArcRelayLoopProfile.ForwardCombat3);
        var mode = (ArcRelayGameModeDefinition)rules.GameMode;
        Assert.Equal(6, mode.WellBirthJitterTicks);
        foreach (ArcRelayWellScheduleDefinition well in mode.Wells)
        {
            Assert.True(2 * mode.WellBirthJitterTicks < well.CadenceTicks);
            Assert.True(well.FirstBirthTick - mode.WellBirthJitterTicks >= 1);
        }
    }
}
