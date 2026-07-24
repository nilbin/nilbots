namespace BotArena.Engine.Tests;

public class ActiveZoneControlTests
{
    private static GameRules ActiveRules(
        int limit = 10,
        int maxTicks = 40,
        int decayInterval = 2) =>
        GameRules.V0_1 with
        {
            RulesVersion = "test-active-control",
            ZoneControl = true,
            ZoneExclusiveAccrual = true,
            ActiveZoneControl = true,
            ZoneDominationTicks = 0,
            ControlPressureLimit = limit,
            ControlPressureGain = 1,
            ControlPressureDecayInterval = decayInterval,
            MaxTicks = maxTicks,
        };

    private static ArenaMap SingleZoneMap() => ArenaMap.Create("test-active-zone", [
        "#######",
        "#.....#",
        "#.....#",
        "#.....#",
        "#######",
    ], [new Spawn(3, 2, Direction.North), new Spawn(5, 2, Direction.West)],
        zone: [new Position(3, 2)]);

    private static ArenaMap SharedZoneMap() => ArenaMap.Create("test-active-shared", [
        "#######",
        "#.....#",
        "#.....#",
        "#.....#",
        "#######",
    ], [new Spawn(2, 2, Direction.North), new Spawn(4, 2, Direction.North)],
        zone: [new Position(2, 2), new Position(3, 2), new Position(4, 2)]);

    [Theory]
    [InlineData(BotAction.MoveForward)]
    [InlineData(BotAction.TurnLeft)]
    [InlineData(BotAction.TurnRight)]
    [InlineData(BotAction.Shoot)]
    public void NonWaitActions_DoNotExertControl(BotAction action)
    {
        var session = new MatchSession(SingleZoneMap(), ActiveRules());

        session.Step([BotDecision.Of(action), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(0, session.State.ControlPressure);
    }

    [Fact]
    public void FaultedOrInvalidatedActions_DoNotBecomeHoldingWaits()
    {
        var faulted = new MatchSession(SingleZoneMap(), ActiveRules());
        faulted.Step([BotDecision.Fault("nope"), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(0, faulted.State.ControlPressure);

        var cooldown = new MatchSession(SingleZoneMap(), ActiveRules());
        cooldown.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        cooldown.Step([BotDecision.Of(BotAction.Shoot), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(0, cooldown.State.ControlPressure);
        Assert.Equal(ActionResult.OnCooldown, cooldown.State.Bots[0].LastActionResult);
    }

    [Fact]
    public void SuccessfulWait_IsTheOnlyHoldingAction_AndCanDominate()
    {
        var session = new MatchSession(SingleZoneMap(), ActiveRules(limit: 3));

        while (!session.IsCompleted)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(3, session.State.ControlPressure);
        Assert.Equal(MatchEndReason.Domination, session.Result!.Reason);
        Assert.Equal(0, session.Result.WinnerSlot);
        Assert.Equal(3, session.Result.ControlPressure);
        Assert.All(session.Result.Bots, bot => Assert.Null(bot.ZoneTicks));
    }

    [Fact]
    public void BothCommittedHolders_FreezeExistingPressure()
    {
        var session = new MatchSession(SharedZoneMap(), ActiveRules());
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.TurnLeft)]);
        Assert.Equal(1, session.State.ControlPressure);

        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(1, session.State.ControlPressure);
    }

    [Fact]
    public void AbandonedLead_DecaysTowardZero_AndCannotBeBanked()
    {
        var session = new MatchSession(SingleZoneMap(), ActiveRules(limit: 20, maxTicks: 8));
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        Assert.Equal(2, session.State.ControlPressure);

        while (!session.IsCompleted)
            session.Step([BotDecision.Of(BotAction.TurnLeft), BotDecision.Of(BotAction.TurnLeft)]);

        Assert.Equal(0, session.State.ControlPressure);
        Assert.Null(session.Result!.WinnerSlot);
        Assert.Equal(0, session.Result.ControlPressure);
    }

    [Fact]
    public void MaxTicks_UsesPressureBeforeHealth_ThenFallsBackAtZero()
    {
        var pressureWins = new MatchSession(
            SingleZoneMap(),
            ActiveRules(limit: 20, maxTicks: 1, decayInterval: 10));
        pressureWins.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Shoot)]);
        Assert.Equal(0, pressureWins.Result!.WinnerSlot);
        Assert.True(pressureWins.Result.Bots[0].FinalHealth < pressureWins.Result.Bots[1].FinalHealth);

        var healthWins = new MatchSession(
            SingleZoneMap(),
            ActiveRules(limit: 20, maxTicks: 1, decayInterval: 10));
        healthWins.Step([BotDecision.Of(BotAction.TurnLeft), BotDecision.Of(BotAction.Shoot)]);
        Assert.Equal(1, healthWins.Result!.WinnerSlot);
        Assert.Equal(0, healthWins.Result.ControlPressure);
    }

    [Fact]
    public void MovingBetweenZoneTiles_EarnsNoPressure()
    {
        var session = new MatchSession(SharedZoneMap(), ActiveRules());

        session.Step([BotDecision.Of(BotAction.TurnRight), BotDecision.Of(BotAction.TurnLeft)]);
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.TurnLeft)]);
        session.Step([BotDecision.Of(BotAction.MoveForward), BotDecision.Of(BotAction.TurnLeft)]);

        Assert.All(session.State.Bots.Take(1), bot => Assert.Contains(bot.Position, session.ZoneTiles));
        Assert.Equal(0, session.State.ControlPressure);
    }

    [Fact]
    public void SuppressedHolder_ChoosesBetweenTheHitAndControl()
    {
        var map = ArenaMap.Create("test-suppression-control", [
            "#########",
            "#.......#",
            "#.......#",
            "#.......#",
            "#.......#",
            "#########",
        ], [new Spawn(1, 3, Direction.East), new Spawn(5, 3, Direction.North)],
            zone: [new Position(5, 2), new Position(5, 3)]);
        var rules = ActiveRules(decayInterval: 100) with
        {
            ProjectileTicksPerTile = 1,
            ProjectileTilesPerAdvance = 2,
            ShotRange = 8,
        };

        var holder = new MatchSession(map, rules);
        var dodger = new MatchSession(map, rules);
        for (int tick = 0; tick < 2; tick++)
        {
            var attackerAction = tick == 0 ? BotAction.Shoot : BotAction.Wait;
            holder.Step([BotDecision.Of(attackerAction), BotDecision.Of(BotAction.Wait)]);
            dodger.Step([BotDecision.Of(attackerAction), BotDecision.Of(BotAction.Wait)]);
        }
        int pressureBeforeChoice = dodger.State.ControlPressure;

        holder.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);
        dodger.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.MoveForward)]);

        Assert.Equal(2, holder.State.Bots[1].Health);
        Assert.Equal(3, dodger.State.Bots[1].Health);
        Assert.True(holder.State.ControlPressure < pressureBeforeChoice);
        Assert.Equal(pressureBeforeChoice, dodger.State.ControlPressure);
    }

    [Fact]
    public void Observation_ExposesSharedPressure_NotLegacyTallies()
    {
        var session = new MatchSession(SingleZoneMap(), ActiveRules());
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        var observation = session.BuildObservation(1);

        Assert.Equal(1, observation.ControlPressure);
        Assert.Equal(10, observation.ControlPressureLimit);
        Assert.Null(observation.MyZoneTicks);
        Assert.Null(observation.EnemyZoneTicks);
    }

    [Fact]
    public void Overtime_ReducesTheLimitAndStopsAbandonedPressureDecay()
    {
        var rules = ActiveRules(limit: 20, maxTicks: 10, decayInterval: 1) with
        {
            ControlOvertimeStartTick = 2,
            ControlOvertimePressureLimit = 2,
            ControlOvertimeStopsDecay = true,
        };
        var session = new MatchSession(SingleZoneMap(), rules);

        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.TurnLeft)]);
        session.Step([BotDecision.Of(BotAction.TurnLeft), BotDecision.Of(BotAction.TurnLeft)]);
        Assert.Equal(0, session.State.ControlPressure);

        Assert.Equal(2, session.BuildObservation(0).ControlPressureLimit);
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.TurnLeft)]);
        session.Step([BotDecision.Of(BotAction.TurnLeft), BotDecision.Of(BotAction.TurnLeft)]);
        Assert.Equal(1, session.State.ControlPressure);

        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.TurnLeft)]);
        Assert.True(session.IsCompleted);
        Assert.Equal(MatchEndReason.Domination, session.Result!.Reason);
        Assert.Equal(0, session.Result.WinnerSlot);
        Assert.Equal(2, session.Result.ControlPressure);
    }

    [Fact]
    public void Overtime_StartResolvesARegulationLeadAtTheReducedLimit()
    {
        var rules = ActiveRules(limit: 20, maxTicks: 10, decayInterval: 100) with
        {
            ControlOvertimeStartTick = 2,
            ControlOvertimePressureLimit = 1,
            ControlOvertimeStopsDecay = true,
        };
        var session = new MatchSession(SharedZoneMap(), rules);

        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.TurnLeft)]);
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.TurnLeft)]);
        Assert.False(session.IsCompleted);
        Assert.Equal(2, session.State.ControlPressure);

        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(MatchEndReason.Domination, session.Result!.Reason);
        Assert.Equal(0, session.Result.WinnerSlot);
        Assert.Equal(1, session.Result.ControlPressure);
    }

    [Fact]
    public void Overtime_CanIncreaseHoldingGainWithoutDisablingDecay()
    {
        var rules = ActiveRules(limit: 20, maxTicks: 10, decayInterval: 1) with
        {
            ControlOvertimeStartTick = 2,
            ControlOvertimePressureLimit = 4,
            ControlOvertimePressureGain = 2,
        };
        var session = new MatchSession(SingleZoneMap(), rules);

        session.Step([BotDecision.Of(BotAction.TurnLeft), BotDecision.Of(BotAction.TurnLeft)]);
        session.Step([BotDecision.Of(BotAction.TurnLeft), BotDecision.Of(BotAction.TurnLeft)]);
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.TurnLeft)]);
        Assert.Equal(2, session.State.ControlPressure);

        session.Step([BotDecision.Of(BotAction.TurnLeft), BotDecision.Of(BotAction.TurnLeft)]);
        Assert.Equal(1, session.State.ControlPressure);

        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.TurnLeft)]);
        session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.TurnLeft)]);

        Assert.Equal(MatchEndReason.Domination, session.Result!.Reason);
        Assert.Equal(0, session.Result.WinnerSlot);
        Assert.Equal(4, session.Result.ControlPressure);
    }
}
