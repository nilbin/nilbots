using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class ConeVisionTests
{
    private static GameRules ConeRules => GameRules.V0_1 with
    {
        RulesVersion = "test-cone",
        VisionCone = true,
        HearingRadius = 8,
    };

    /// <summary>Open 13x9 room; spawn 0 center-left facing East, spawn 1 far right facing West.</summary>
    private static ArenaMap OpenMap() => ArenaMap.Create("test-cone-map", [
        "#############",
        "#...........#",
        "#...........#",
        "#...........#",
        "#...........#",
        "#...........#",
        "#...........#",
        "#...........#",
        "#############",
    ], [new Spawn(3, 4, Direction.East), new Spawn(11, 4, Direction.West)]);

    [Fact]
    public void Cone_IsTheFacingQuadrantPlusProximityRing()
    {
        var origin = new Position(3, 4);
        // Forward tiles in the East quadrant (|dy| <= dx) are in.
        Assert.True(Visibility.InCone(origin, new Position(6, 4), Direction.East));
        Assert.True(Visibility.InCone(origin, new Position(6, 6), Direction.East));  // dy=2, dx=3
        Assert.True(Visibility.InCone(origin, new Position(6, 7), Direction.East));  // dy=3, dx=3 edge
        // Outside the quadrant: too lateral, behind, or perpendicular beyond the ring.
        Assert.False(Visibility.InCone(origin, new Position(5, 8), Direction.East)); // dy=4 > dx=2
        Assert.False(Visibility.InCone(origin, new Position(1, 4), Direction.East)); // behind
        Assert.False(Visibility.InCone(origin, new Position(3, 6), Direction.East)); // perpendicular, dist 2
        // The proximity ring is omnidirectional: point-blank is never blind.
        Assert.True(Visibility.InCone(origin, new Position(2, 4), Direction.East));  // directly behind, adjacent
        Assert.True(Visibility.InCone(origin, new Position(2, 3), Direction.East));  // diagonal behind, adjacent
    }

    [Fact]
    public void Observation_UnderConeRules_CannotSeeBehind()
    {
        var session = new MatchSession(OpenMap(), ConeRules);
        // Close to distance 6: bot 0 walks two tiles east.
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.Wait)]);
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.Wait)]);
        Assert.Contains(session.BuildObservation(1).VisibleEnemies, e => e.Slot == 0); // ahead: seen

        // Bot 1 turns 180° (West→South→East) while bot 0 keeps closing to distance 4.
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.TurnLeft)]);
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.TurnLeft)]);
        var blind = session.BuildObservation(1);
        Assert.DoesNotContain(blind.VisibleEnemies, e => e.Slot == 0); // 4 tiles directly behind: unseen
        // Sanity: omnidirectional vision WOULD see that tile from here.
        Assert.Contains(new Position(7, 4), Visibility.ComputeVisibleTiles(OpenMap(), new Position(11, 4), 6));
    }

    [Fact]
    public void LoudEvents_ArriveThroughTheBlindArc_AsRedactedSounds()
    {
        var session = new MatchSession(OpenMap(), ConeRules);
        // Bot 0 turns North (its shot will travel away from bot 1's row);
        // bot 1 turns to face East — everything at bot 0 is now fully behind it.
        session.Step([BotDecision.Of(BotAction.TurnLeft), BotDecision.Of(BotAction.TurnLeft)]);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.TurnLeft)]);
        var heard = session.BuildObservation(1); // shot fired last tick, 8 tiles behind
        // Redaction (§H item 1): the unseen shot is a SOUND — type, coarse bearing,
        // coarse band — never the full authoritative event with coordinates.
        Assert.DoesNotContain(heard.VisibleEvents, e => e.Type == GameEventType.Shot);
        var sound = Assert.Single(heard.HeardSounds!);
        Assert.Equal(GameEventType.Shot, sound.Type);
        Assert.Equal(6, sound.Bearing);  // due west of the listener
        Assert.Equal(2, sound.Distance); // Chebyshev 8 = Far

        // A turn (quiet) in the same blind arc is delivered as NOTHING at all.
        session.Step([BotDecision.Of(BotAction.TurnLeft), BotDecision.Of(BotAction.Wait)]);
        var afterTurn = session.BuildObservation(1);
        Assert.DoesNotContain(afterTurn.VisibleEvents, e => e.Type == GameEventType.Turn);
        Assert.Empty(afterTurn.HeardSounds!);
    }

    [Fact]
    public void ASeenImpact_DoesNotRevealTheUnseenShooter()
    {
        // Follow-up review (§I): the shooter is 8 tiles away — beyond vision range —
        // and its ray lands ON the observer. The ray's endpoint is a visible tile
        // (the observer's own), so the any-reference rule would deliver the full Shot
        // event: the unseen shooter's exact tile and slot. Under cone rules the event
        // must be gated on the ACTOR's tile instead — the observer feels the Damage
        // (its own tile, full event) and hears a bearing, nothing more.
        var session = new MatchSession(OpenMap(), ConeRules);
        session.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        var observation = session.BuildObservation(1);
        Assert.DoesNotContain(observation.VisibleEvents, e => e.Type == GameEventType.Shot);
        Assert.Contains(observation.VisibleEvents, e => e.Type == GameEventType.Damage);
        var sound = observation.HeardSounds!.Single(s => s.Type == GameEventType.Shot);
        Assert.Equal(6, sound.Bearing);  // due west: a direction, not a coordinate
        Assert.Equal(2, sound.Distance); // far

        // Where vision is omnidirectional the legacy any-reference rule is unchanged.
        var classic = new MatchSession(OpenMap(), GameRules.V0_1);
        classic.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        Assert.Contains(classic.BuildObservation(1).VisibleEvents, e => e.Type == GameEventType.Shot);
    }

    [Fact]
    public void ClassicRules_AreUntouchedByConeCode()
    {
        var classic = new MatchSession(OpenMap(), GameRules.V0_1);
        var observation = classic.BuildObservation(1);
        // Perpendicular tiles stay visible without a cone.
        Assert.Contains(observation.VisibleTiles, t => t.Position == new Position(11, 8));
        Assert.Null(observation.VisibleProjectiles);
    }
}
