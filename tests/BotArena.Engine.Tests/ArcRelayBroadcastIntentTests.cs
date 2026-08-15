namespace BotArena.Engine.Tests;

public sealed class ArcRelayBroadcastIntentTests
{
    [Fact]
    public void HostedProjectionSeparatesTravelAndFacingWithoutPublishingDebug()
    {
        ArcRelayBroadcastDocument.CompactReason? result =
            ArcRelayBroadcastDocument.ParseReason(
                "orders:guard > hold-gate:march@14,12:at-target:"
                + "face-anchor:threat:face@19,13 via East");

        Assert.NotNull(result);
        Assert.Equal("hold-gate", result.OrderId);
        Assert.Equal("march:at-target:face-anchor:threat:", result.Action);
        Assert.Equal(new ReplayV3.PositionValue(14, 12), result.Destination);
        Assert.Equal(new ReplayV3.PositionValue(19, 13), result.Facing);
    }

    [Fact]
    public void UnknownVocabularyStaysLegibleWithoutInventingMapIntent()
    {
        ArcRelayBroadcastDocument.CompactReason? result =
            ArcRelayBroadcastDocument.ParseReason(
                "custody:committed-delivery-wait");

        Assert.NotNull(result);
        Assert.Null(result.OrderId);
        Assert.Equal("custody:committed-delivery-wait", result.Action);
        Assert.Null(result.Destination);
        Assert.Null(result.Facing);
    }

    [Fact]
    public void HostedProjectionKeepsEvidencePrivateButPublishesItsDecision()
    {
        ArcRelayBroadcastDocument.CompactReason? result =
            ArcRelayBroadcastDocument.ParseReason(
                "orders:guard > answer-centre > hold-gate:"
                + "evidence[surge=4-entries@10..20]:march@14,12:face@19,13");

        Assert.NotNull(result);
        Assert.Equal("hold-gate", result.OrderId);
        Assert.Equal("march:", result.Action);
        Assert.Equal(new ReplayV3.PositionValue(14, 12), result.Destination);
        Assert.Equal(new ReplayV3.PositionValue(19, 13), result.Facing);
    }
}
