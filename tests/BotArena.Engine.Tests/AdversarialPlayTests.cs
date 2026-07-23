using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

/// <summary>
/// The §H item 4 suite: the executable plays proven against COMPETENT defense, not
/// passive targets. The defenders here are the strongest scripted policies the
/// mechanics allow — the optimal 4-tick scanner (eyes sweep the full circle with no
/// wasted tick) and a bolt-dodging camper that uses the computable bolt timing the
/// hardened observation exposes. What a shooting, adapting defender adds on top is
/// gen-7's question (pre-registered ship criteria); what these pin is that no
/// SCRIPTABLE defense trivially voids the plays.
/// </summary>
public class AdversarialPlayTests
{
    private static GameRules ConeRules => GameRules.V0_1 with
    {
        RulesVersion = "test-adv-cone",
        VisionCone = true,
        ShotRange = 8,
        MaxTicks = 200,
    };

    /// <summary>Scanner scripted as TurnRight every tick from North: facing at the
    /// observation of tick T is the initial facing rotated T times.</summary>
    private static Direction ScannerFacingAt(int tick) => (Direction)(tick % 4);

    [Fact]
    public void RadarStatue_InTheOpenField_SpotsAnyStraightApproach_WithLatency()
    {
        // The honest strength of the anti-play's target: a 4-tick scanner sweeps
        // faster than a walker crosses the 6..1 approach band (5 ticks > 4-tick
        // cycle), so open-field stealth is impossible — AND the sweep leaves
        // in-range-but-unseen ticks, the latency every timing play builds on.
        var map = ArenaMap.Create("test-adv-open", [
            "###############",
            "#.............#",
            "#.............#",
            "#.............#",
            "#.............#",
            "#.............#",
            "###############",
        ], [new Spawn(2, 3, Direction.East), new Spawn(12, 3, Direction.North)]);
        var session = new MatchSession(map, ConeRules);
        var seenTicks = new List<int>();
        var inRangeUnseen = new List<int>();
        for (int tick = 0; session.State.Bots[0].Position.X < 11; tick++)
        {
            var scannerObs = session.BuildObservation(1);
            bool seen = scannerObs.VisibleEnemies.Any(e => e.Slot == 0);
            int distance = session.State.Bots[0].Position.ChebyshevDistance(new Position(12, 3));
            if (seen)
                seenTicks.Add(tick);
            else if (distance <= ConeRules.VisionRange)
                inRangeUnseen.Add(tick);
            session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.TurnRight)]);
        }
        Assert.NotEmpty(seenTicks);      // detection is inevitable in the open
        Assert.NotEmpty(inRangeUnseen);  // but the sweep has exploitable latency
    }

    [Fact]
    public void RadarStatue_BehindCover_DiesToTheTimedBackstab_WithoutEverSeeingIt()
    {
        // Red-Light Approach vs the optimal scanner: impossible in the open (test
        // above), executable with cover. The attacker plans each step against the
        // scanner's KNOWN rotation using the engine's own visibility — script vs
        // script, both playing optimally within their doctrine.
        // The scanner sits right behind a wall column: everything west of it is in
        // LOS shadow (free approach), and the south flank tiles share ONE exposure
        // phase (the South-facing obs) — a timed dash between two sweeps reaches the
        // ring. The ring tile itself is the commit: entering it is seen, but the
        // point-blank shot lands the same tick.
        var map = ArenaMap.Create("test-adv-cover", [
            "###############",
            "#.............#",
            "#......#......#",
            "#......#......#",
            "#......#......#",
            "#.............#",
            "###############",
        ], [new Spawn(2, 3, Direction.East), new Spawn(8, 3, Direction.North)]);
        var rules = ConeRules;
        var scannerAt = new Position(8, 3);
        Position[] waypoints =
        [
            new(3, 3), new(4, 3), new(5, 3), new(6, 3), new(6, 4),
            new(6, 5), new(7, 5), new(8, 5), new(8, 4),
        ];
        var session = new MatchSession(map, rules);

        bool ExposedAt(Position tile, int obsTick) =>
            Visibility.ComputeVisibleTiles(map, scannerAt, rules.VisionRange, ScannerFacingAt(obsTick))
                .Contains(tile);

        int waypoint = 0;
        BotDecision Attacker(BotObservation obs)
        {
            while (waypoint < waypoints.Length && obs.Position == waypoints[waypoint])
                waypoint++;
            if (waypoint >= waypoints.Length)
            {
                // On the ring tile: face the scanner and keep firing point-blank.
                if (obs.Facing != Direction.North)
                    return BotDecision.Of(TurnToward(obs.Facing, Direction.North));
                return BotDecision.Of(BotAction.Shoot);
            }
            var target = waypoints[waypoint];
            var needed = DirectionFromTo(obs.Position, target);
            bool finalDash = waypoint == waypoints.Length - 1; // the ring tile is a commit
            // The tile before the ring costs TWO observations (arrive + rotate), so
            // stepping onto it needs a two-sweep safety window, not one.
            bool preTurnTile = waypoint == waypoints.Length - 2;
            if (obs.Facing != needed)
                return BotDecision.Of(ExposedAt(obs.Position, obs.Tick + 1)
                    ? BotAction.Wait // never rotate into a sweep; hold this occluded tile
                    : TurnToward(obs.Facing, needed));
            bool targetSafe = !ExposedAt(target, obs.Tick + 1)
                && (!preTurnTile || !ExposedAt(target, obs.Tick + 2));
            if (finalDash || targetSafe)
                return BotDecision.Of(BotAction.MoveForward);
            return BotDecision.Of(BotAction.Wait);
        }

        var seenBeforeContact = new List<int>();
        int? firstDamageTick = null;
        for (int tick = 0; tick < 120 && !session.IsCompleted; tick++)
        {
            var attackerObs = session.BuildObservation(0);
            var scannerObs = session.BuildObservation(1);
            if (firstDamageTick is null && scannerObs.VisibleEnemies.Any(e => e.Slot == 0))
                seenBeforeContact.Add(tick);
            var result = session.Step([Attacker(attackerObs), BotDecision.Of(BotAction.TurnRight)]);
            if (firstDamageTick is null && result.Events.Any(e => e.Type == GameEventType.Damage && e.TargetSlot == 1))
                firstDamageTick = tick;
        }
        Assert.NotNull(firstDamageTick);
        Assert.Equal(BotStatus.Destroyed, session.State.Bots[1].Status);
        // The scanner's first sight of the attacker was AT the first hit, never before:
        Assert.True(seenBeforeContact.All(t => t >= firstDamageTick),
            $"scanner saw the attacker at ticks [{string.Join(",", seenBeforeContact)}] " +
            $"before first damage at {firstDamageTick}.");
    }

    private static GameRules SqueezeRules => GameRules.V0_1 with
    {
        RulesVersion = "test-adv-squeeze",
        ProjectileTicksPerTile = 2,
        ShotRange = 8,
        ZoneControl = true,
        ZoneExclusiveAccrual = true,
        ZoneDominationTicks = 0,
        MaxTicks = 60,
    };

    [Fact]
    public void Squeeze_BoltsPlusBody_DeniesTheCompetentDodgingCamper()
    {
        // §C's executable squeeze vs a camper that dodges bolts PERFECTLY (using the
        // computable TicksUntilAdvance the hardened observation provides — full, not
        // cone, vision: the strongest defender the mechanics allow). The claim under
        // test: bolts alone never kill it, but bolts + the attacker's body deny the
        // zone — a hit, an eviction, or a frozen contested scoreboard.
        var map = ArenaMap.Create("test-adv-squeeze-map", [
            "############",
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "#..........#",
            "############",
        ], [new Spawn(4, 3, Direction.East), new Spawn(7, 3, Direction.South)],
            zone: [new Position(6, 3), new Position(7, 3), new Position(6, 4), new Position(7, 4)]);
        var session = new MatchSession(map, SqueezeRules);

        BotAction[] attackerScript =
        [
            BotAction.Shoot,        // t0: bolt down row 3
            BotAction.TurnRight,    // t1
            BotAction.MoveForward,  // t2: to (4,4)
            BotAction.TurnLeft,     // t3
            BotAction.Shoot,        // t4: bolt down row 4
            BotAction.MoveForward,  // t5: onto own bolt's lane (Vanguard)
            BotAction.MoveForward,  // t6: body onto the zone at (6,4)
        ];
        int camperHits = 0;
        int startZoneTicks = session.State.Bots[1].ZoneTicks;
        const int Window = 16;
        for (int tick = 0; tick < Window && !session.IsCompleted; tick++)
        {
            var camperObs = session.BuildObservation(1);
            var attackerAction = tick < attackerScript.Length
                ? attackerScript[tick]
                : (tick % 3 == 1 ? BotAction.Shoot : BotAction.Wait); // keep the lane hot
            var result = session.Step([BotDecision.Of(attackerAction), CompetentCamper(camperObs)]);
            camperHits += result.Events.Count(e => e.Type == GameEventType.Damage && e.TargetSlot == 1);
        }
        int accrued = session.State.Bots[1].ZoneTicks - startZoneTicks;
        Assert.True(camperHits > 0 || accrued <= Window * 2 / 3,
            $"squeeze denied nothing: camper unhit and accrued {accrued}/{Window} zone ticks.");
        // And the §C honesty clause still holds: bolts alone (the first 5 ticks,
        // before the body arrives) must NOT have killed the perfect dodger.
        Assert.True(session.State.Bots[1].Health >= 2,
            "the dodging camper lost more than one health — bolt dodging with computable timing failed.");
    }

    [Fact]
    public void Vanish_SlippingTheConeSideways_BreaksContact()
    {
        // The Vanish play: at close range, lateral movement leaves a tracker's 90°
        // quadrant entirely — contact breaks without ever leaving vision RANGE.
        var map = ArenaMap.Create("test-adv-vanish", [
            "#########",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#########",
        ], [new Spawn(3, 4, Direction.East), new Spawn(5, 4, Direction.South)]);
        var session = new MatchSession(map, ConeRules);
        Assert.Contains(session.BuildObservation(0).VisibleEnemies, e => e.Slot == 1); // contact
        for (int i = 0; i < 3; i++)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.MoveForward)]);
        var tracker = session.BuildObservation(0);
        Assert.Equal(new Position(5, 7), session.State.Bots[1].Position);
        Assert.DoesNotContain(tracker.VisibleEnemies, e => e.Slot == 1); // vanished
        // Contrast: omnidirectional sight from the same tile WOULD still see it.
        Assert.Contains(new Position(5, 7),
            Visibility.ComputeVisibleTiles(map, new Position(3, 4), ConeRules.VisionRange));
    }

    [Fact]
    public void Fortress_DoorwayBoltsAndVanguardEntry_BreakTheNinetyZeroFreeze()
    {
        // The gen-5 fortress geometry in miniature: a walled keep, one south door, the
        // zone inside, the defender camped on it. Under 0.4 the attacker cannot enter
        // (the doorway is a watched instant-ray lane) and the game freezes 90-0. Under
        // bolts the attacker makes the zone column lethal THROUGH the door, walks in
        // behind its own bolt, and parks on a zone tile: exclusive accrual freezes for
        // the defender — the fortress stops paying.
        var map = ArenaMap.Create("test-adv-keep", [
            "#############",
            "#...........#",
            "#..#######..#",
            "#..#.....#..#",
            "#..#.....#..#",
            "#..#.....#..#",
            "#..###.###..#",
            "#...........#",
            "#############",
        ], [new Spawn(6, 7, Direction.North), new Spawn(7, 4, Direction.West)],
            zone: [new Position(6, 3), new Position(7, 3), new Position(6, 4), new Position(7, 4)]);
        var rules = SqueezeRules with { RulesVersion = "test-adv-keep-rules" };
        var session = new MatchSession(map, rules);

        BotAction[] attackerScript =
        [
            BotAction.Shoot,        // t0: bolt through the door — (6,6)
            BotAction.Wait,         // t1
            BotAction.MoveForward,  // t2: into the doorway behind the bolt
            BotAction.Shoot,        // t3: second bolt up the column
            BotAction.MoveForward,  // t4: to (6,5), still behind its own bolts
            BotAction.Wait,         // t5
            BotAction.MoveForward,  // t6: onto the zone tile (6,4) — contested
        ];
        int defenderHits = 0;
        int startZoneTicks = session.State.Bots[1].ZoneTicks;
        const int Window = 24;
        for (int tick = 0; tick < Window && !session.IsCompleted; tick++)
        {
            var defenderObs = session.BuildObservation(1);
            var attackerAction = tick < attackerScript.Length ? attackerScript[tick] : BotAction.Wait;
            var result = session.Step([BotDecision.Of(attackerAction), CompetentCamper(defenderObs)]);
            defenderHits += result.Events.Count(e => e.Type == GameEventType.Damage && e.TargetSlot == 1);
        }
        // Attacker walked in unharmed (nothing shot at it) and holds zone ground.
        Assert.Equal(rules.MaxHealth, session.State.Bots[0].Health);
        Assert.Contains(session.State.Bots[0].Position,
            new HashSet<Position>(map.EffectiveZone()));
        int accrued = session.State.Bots[1].ZoneTicks - startZoneTicks;
        Assert.True(defenderHits > 0 || accrued < Window / 2,
            $"the fortress still pays: defender unhit and accrued {accrued}/{Window} zone ticks.");
    }

    /// <summary>The strongest scriptable camper: holds zone tiles, dodges bolts by
    /// arithmetic (a tile is lethal if a bolt occupies it — pre-advance check — or
    /// advances onto it this tick), returns to the zone when safe. It does not model
    /// enemy guns or minds; that is gen-7's defender.</summary>
    private static BotDecision CompetentCamper(BotObservation obs)
    {
        var zone = new HashSet<Position>(obs.ZoneTiles ?? []);
        var bolts = obs.VisibleProjectiles ?? [];
        bool Deadly(Position tile) => bolts.Any(b =>
            b.Position == tile
            || (b.TicksUntilAdvance == 1 && Step(b.Position, b.Direction) == tile));
        bool DeadlySoon(Position tile) => bolts.Any(b =>
            b.TicksUntilAdvance == 2 && Step(b.Position, b.Direction) == tile);
        var walls = obs.VisibleTiles.Where(t => t.IsWall).Select(t => t.Position).ToHashSet();
        var floors = obs.VisibleTiles.Where(t => !t.IsWall).Select(t => t.Position).ToHashSet();
        var enemies = obs.VisibleEnemies.Select(e => e.Position).ToHashSet();
        bool Walkable(Position tile) => floors.Contains(tile) && !enemies.Contains(tile);

        var ahead = Step(obs.Position, obs.Facing);
        bool stayLethal = Deadly(obs.Position);
        bool stepSafe = Walkable(ahead) && !Deadly(ahead);

        if (stayLethal)
        {
            if (stepSafe)
                return BotDecision.Of(BotAction.MoveForward);
            // Last resort: rotate toward any safe neighbor (this tick is lost either way).
            foreach (var direction in Enum.GetValues<Direction>())
            {
                var tile = Step(obs.Position, direction);
                if (Walkable(tile) && !Deadly(tile))
                    return BotDecision.Of(TurnToward(obs.Facing, direction));
            }
            return BotDecision.Of(BotAction.Wait);
        }

        if (zone.Contains(obs.Position))
        {
            // Holding the hill. If a bolt lands here in exactly 2 ticks, make sure the
            // 1-move dodge exists: pre-turn toward a safe (ideally zone) neighbor.
            if (DeadlySoon(obs.Position) && !stepSafe)
            {
                foreach (var direction in EscapePreference(zone, obs.Position))
                {
                    var tile = Step(obs.Position, direction);
                    if (Walkable(tile) && !Deadly(tile) && !DeadlySoon(tile))
                        return BotDecision.Of(TurnToward(obs.Facing, direction));
                }
            }
            return BotDecision.Of(BotAction.Wait);
        }

        // Off the hill: walk back. Prefer a safe zone tile ahead; otherwise rotate
        // toward the nearest zone neighbor.
        if (zone.Contains(ahead) && stepSafe && !DeadlySoon(ahead))
            return BotDecision.Of(BotAction.MoveForward);
        foreach (var direction in Enum.GetValues<Direction>())
        {
            var tile = Step(obs.Position, direction);
            if (zone.Contains(tile) && Walkable(tile) && !Deadly(tile) && !DeadlySoon(tile))
                return BotDecision.Of(TurnToward(obs.Facing, direction));
        }
        return BotDecision.Of(BotAction.Wait);

        IEnumerable<Direction> EscapePreference(HashSet<Position> zoneTiles, Position from) =>
            Enum.GetValues<Direction>()
                .OrderByDescending(d => zoneTiles.Contains(Step(from, d)));
    }

    private static Position Step(Position from, Direction direction)
    {
        var (dx, dy) = direction.Vector();
        return from.Offset(dx, dy);
    }

    private static Direction DirectionFromTo(Position from, Position to)
    {
        if (to.X > from.X) return Direction.East;
        if (to.X < from.X) return Direction.West;
        if (to.Y > from.Y) return Direction.South;
        return Direction.North;
    }

    private static BotAction TurnToward(Direction current, Direction desired)
    {
        int delta = ((int)desired - (int)current + 4) % 4;
        return delta == 3 ? BotAction.TurnLeft : BotAction.TurnRight;
    }
}
