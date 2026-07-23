namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the redacted hearing model (RULES-0.5-DESIGN §A, §H item 1): the bearing-octant
/// and distance-band math, the radius cutoff, and the no-double-delivery rule. The
/// through-the-blind-arc delivery itself is pinned in ConeVisionTests.
/// </summary>
public class HearingTests
{
    private static readonly Position Listener = new(10, 10);

    [Theory]
    [InlineData(0, -5, 0)]   // due north
    [InlineData(3, -4, 1)]   // north-east diagonal
    [InlineData(5, 0, 2)]    // due east
    [InlineData(4, 4, 3)]    // south-east diagonal
    [InlineData(0, 6, 4)]    // due south
    [InlineData(-3, 3, 5)]   // south-west diagonal
    [InlineData(-7, 1, 6)]   // west dominates 7:1 → cardinal west
    [InlineData(-2, -2, 7)]  // north-west diagonal
    [InlineData(5, 2, 2)]    // 5:2 exceeds 2:1 → cardinal east
    [InlineData(4, 2, 3)]    // exactly 2:1 stays the diagonal (SE)
    [InlineData(4, -2, 1)]   // same boundary in the north hemisphere (NE)
    [InlineData(1, -3, 0)]   // 3:1 vertical dominance → cardinal north
    public void BearingOctant_PartitionsTheCompassAtTwoToOne(int dx, int dy, int expected) =>
        Assert.Equal(expected, Hearing.BearingOctant(Listener, Listener.Offset(dx, dy)));

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(8, 2)]
    public void DistanceBand_BreaksAtNearTwoMediumFive(int distance, int band) =>
        Assert.Equal(band, Hearing.DistanceBand(distance));

    private static ArenaMap Corridor() => ArenaMap.Create("test-hearing-hall", [
        "###############",
        "#.............#",
        "#.............#",
        "#.............#",
        "###############",
    ], [new Spawn(1, 2, Direction.North), new Spawn(13, 2, Direction.East)]);

    [Fact]
    public void SoundsBeyondTheHearingRadius_AreSilence()
    {
        // Radius 4, shooter 12 tiles away, both bots facing away from each other:
        // the shot must arrive as neither an event nor a sound.
        var rules = GameRules.V0_1 with
        {
            RulesVersion = "test-hearing-cutoff",
            VisionCone = true,
            HearingRadius = 4,
        };
        var session = new MatchSession(Corridor(), rules);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        var observation = session.BuildObservation(1);
        Assert.DoesNotContain(observation.VisibleEvents, e => e.Type == GameEventType.Shot);
        Assert.Empty(observation.HeardSounds!);
    }

    [Fact]
    public void ASightedLoudEvent_IsAFullEvent_NeverAlsoASound()
    {
        // Listener faces the shooter: the shot is in plain view, so it must arrive as
        // the authoritative event exactly once — not echoed as a redacted sound too.
        var rules = GameRules.V0_1 with
        {
            RulesVersion = "test-hearing-dedup",
            VisionCone = true,
            HearingRadius = 8,
        };
        var map = ArenaMap.Create("test-hearing-close", [
            "#########",
            "#.......#",
            "#.......#",
            "#.......#",
            "#########",
        ], [new Spawn(2, 2, Direction.North), new Spawn(6, 2, Direction.West)]);
        var session = new MatchSession(map, rules);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        var observation = session.BuildObservation(1);
        Assert.Contains(observation.VisibleEvents, e => e.Type == GameEventType.Shot);
        Assert.Empty(observation.HeardSounds!);
    }

    [Fact]
    public void NoHearingRules_HaveNullSounds()
    {
        var session = new MatchSession(Corridor(), GameRules.V0_4);
        Assert.Null(session.BuildObservation(0).HeardSounds);
    }
}
