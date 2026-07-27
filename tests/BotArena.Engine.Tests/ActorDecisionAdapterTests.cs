using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public sealed class ActorDecisionAdapterTests
{
    private static readonly PublicMatchContractManifest Contract =
        PublicRulesManifestFactory.CreateMatchContract(
            FrontlineTestDefinitions.PrimeOnlyRules(),
            FrontlineTestDefinitions.OpenMapV2());

    [Fact]
    public void PrimeAdapter_ResolvesStableIdOrCode()
    {
        BotDecision byId = ActorDecisionAdapter.ToPrimeDecision(
            new ActorDecision { ActionId = PublicActionIds.MoveForward },
            Contract);
        BotDecision byCode = ActorDecisionAdapter.ToPrimeDecision(
            new ActorDecision { ActionCode = (int)BotAction.TurnLeft },
            Contract);

        Assert.Equal(BotAction.MoveForward, byId.Action);
        Assert.Equal(BotAction.TurnLeft, byCode.Action);
    }

    [Fact]
    public void Normalize_CompletesSelectorsAndRemovesAnEmptyPayloadEnvelope()
    {
        ActorDecision canonical = ActorDecisionAdapter.Normalize(
            new ActorDecision
            {
                ActionId = PublicActionIds.MoveForward,
                Payload = new ActorActionPayload(),
                FaultMessage = "ignored stale text",
            },
            Contract);

        Assert.Equal(PublicActionIds.MoveForward, canonical.ActionId);
        Assert.Equal((int)BotAction.MoveForward, canonical.ActionCode);
        Assert.Null(canonical.Payload);
        Assert.False(canonical.Faulted);
        Assert.Null(canonical.FaultMessage);
    }

    [Fact]
    public void PrimeAdapter_PreservesPrivateShotPayloadAndDebug()
    {
        var program = new ShotProgram(1, 0, 0, 1, 0);

        BotDecision adapted = ActorDecisionAdapter.ToPrimeDecision(
            ActorDecision.Shoot(program, "aim right"),
            Contract);

        Assert.Equal(BotAction.Shoot, adapted.Action);
        Assert.Equal(program, adapted.ShotProgram);
        Assert.Equal("aim right", adapted.DebugMessage);
    }

    [Fact]
    public void PrimeAdapter_RejectsShotProgramOutsidePublicContract()
    {
        var invalid = new ShotProgram(
            Contract.Rules.ShotPrograms.MaxInitialAimOctants + 1,
            0,
            0,
            1,
            0);

        Assert.Throws<ArgumentException>(() =>
            ActorDecisionAdapter.Normalize(
                ActorDecision.Shoot(invalid),
                Contract));
    }

    [Fact]
    public void PrimeAdapter_RejectsMismatchedDisabledAndFuturePayloads()
    {
        Assert.Throws<ArgumentException>(() =>
            ActorDecisionAdapter.ToPrimeDecision(
                new ActorDecision
                {
                    ActionId = PublicActionIds.Wait,
                    ActionCode = (int)BotAction.Shoot,
                },
                Contract));
        Assert.Throws<ArgumentException>(() =>
            ActorDecisionAdapter.ToPrimeDecision(
                ActorDecision.Of(
                    PublicActionIds.StrafeLeft,
                    (int)BotAction.StrafeLeft),
                Contract));
        Assert.Throws<ArgumentException>(() =>
            ActorDecisionAdapter.ToPrimeDecision(
                ActorDecision.Of(
                    PublicActionIds.Wait,
                    (int)BotAction.Wait,
                    new ActorActionPayload { FormTargetId = "flight" }),
                Contract));
        Assert.Throws<ArgumentException>(() =>
            ActorDecisionAdapter.ToPrimeDecision(
                ActorDecision.Fault("boom"),
                Contract));
    }
}
