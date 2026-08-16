public sealed class ForwardRouteProgressTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void APassingPocketProjectsOntoItsAdjacentCourseTile(int distance)
    {
        Assert.Equal(
            0,
            ArcRelayTacticalPlaybookMind.RouteProjectionOffset(distance));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void APositionBeyondTheProjectionBandIsNotRouteProgress(int distance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ArcRelayTacticalPlaybookMind.RouteProjectionOffset(distance));
    }
}
