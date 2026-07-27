using BotArena.Engine;
using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class FrontlineMatchSessionTests
{
    private static readonly FrontlineActorId Team0Life0 = new(0, 0, 0);
    private static readonly FrontlineActorId Team1Life0 = new(1, 0, 0);

    [Fact]
    public void Constructor_RejectsMultiUnitAndNonFrontlineDefinitions()
    {
        GameRules multiUnitRules =
            FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 500) with
            {
                Frontline = new FrontlineRules(),
            };
        ResolvedMatchDefinition multiUnitDefinition =
            MatchDefinitionResolver.Resolve(
                multiUnitRules,
                FrontlineTestDefinitions.Frontline01());
        ResolvedMatchDefinition legacyDefinition =
            MatchDefinitionResolver.Resolve(
                GameRules.V0_1,
                FrontlineTestDefinitions.OpenMapV1());

        Assert.Throws<NotSupportedException>(
            () => new FrontlineMatchSession(multiUnitDefinition));
        Assert.Throws<ArgumentException>(
            () => new FrontlineMatchSession(legacyDefinition));
    }

    [Fact]
    public void ExperimentalMap_RunsWhenDefinitionExplicitlySelectsPrimeOnlyMode()
    {
        FrontlineRules baseline = new();
        GameRules rules =
            FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 3) with
            {
                Frontline = baseline with
                {
                    MaxUnitsPerTeam = 1,
                    FabricationUnlockTicks = [],
                },
            };
        ResolvedMatchDefinition definition = MatchDefinitionResolver.Resolve(
            rules,
            FrontlineTestDefinitions.Frontline01());
        var session = new FrontlineMatchSession(definition);

        while (!session.IsCompleted)
        {
            FrontlineTickStart tickStart = session.PrepareTick();
            session.Step(WaitDecisions(tickStart.ActiveActors));
        }

        Assert.Equal(FrontlineMatchEndReason.MaxTicks, session.Result?.Reason);
        Assert.Null(session.Result?.WinnerTeamId);
        Assert.Equal(2, session.Result?.EndTick);
    }

    [Fact]
    public void Constructor_RevalidatesResolvedDefinitionInputs()
    {
        ResolvedMatchDefinition valid =
            FrontlineTestDefinitions.ResolveOpen();
        ResolvedMatchDefinition forged = valid with
        {
            Map = FrontlineTestDefinitions.OpenMapV1(),
        };

        Assert.Throws<MatchDefinitionValidationException>(
            () => new FrontlineMatchSession(forged));
    }

    [Fact]
    public void Reset_RestoresExactOrderedPrimeLifeZeroState()
    {
        ResolvedMatchDefinition definition =
            FrontlineTestDefinitions.ResolveOpen();
        var session = new FrontlineMatchSession(definition);
        FrontlineTickStart initialTick = session.PrepareTick();
        session.Step(new Dictionary<FrontlineActorId, BotDecision>
        {
            [Team1Life0] = BotDecision.Of(BotAction.TurnRight),
            [Team0Life0] = BotDecision.Of(BotAction.TurnLeft),
        });

        FrontlineResetResult reset = session.Reset();

        Assert.Same(session.State, reset.State);
        Assert.Same(definition.Rules, reset.State.Definition.Rules);
        Assert.Same(definition.Map, reset.State.Definition.Map);
        Assert.Equal(0, reset.State.Tick);
        Assert.Equal(
            FrontlineControlSystem.CreateInitial(definition.FrontlineRules!),
            reset.State.Control);
        Assert.False(reset.State.IsCompleted);
        Assert.Null(reset.State.Result);
        Assert.Empty(reset.State.Projectiles);
        Assert.Equal(0, reset.State.NextProjectileId);
        Assert.Equal(
            [Team0Life0, Team1Life0],
            reset.ActiveActors.ToArray());
        Assert.Equal(
            [0, 1],
            reset.State.Teams.Select(team => team.TeamId).ToArray());

        AssertPrime(
            reset.State.GetTeam(0),
            Team0Life0,
            new Position(1, 2),
            Direction.East,
            definition.FrontlineRules!.PrimeForm.MaxHealth);
        AssertPrime(
            reset.State.GetTeam(1),
            Team1Life0,
            new Position(7, 2),
            Direction.West,
            definition.FrontlineRules.PrimeForm.MaxHealth);

        FrontlineTickStart resetTick = session.PrepareTick();
        Assert.NotSame(initialTick, resetTick);
        Assert.Equal(0, resetTick.Tick);
        Assert.Equal(
            [Team0Life0, Team1Life0],
            resetTick.ActiveActors.ToArray());
        Assert.Empty(resetTick.RespawnedActors);
        Assert.Empty(resetTick.Events);
    }

    [Fact]
    public void PrepareTick_IsIdempotentAndStepRequiresItsStableKeys()
    {
        var session = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveOpen());
        IReadOnlyDictionary<FrontlineActorId, BotDecision> valid =
            WaitDecisions([Team0Life0, Team1Life0]);

        Assert.Throws<InvalidOperationException>(() => session.Step(valid));

        FrontlineTickStart first = session.PrepareTick();
        FrontlineTickStart second = session.PrepareTick();

        Assert.Same(first, second);
        Assert.Equal(0, first.Tick);
        Assert.Equal(
            [Team0Life0, Team1Life0],
            first.ActiveActors.ToArray());
        Assert.Empty(first.RespawnedActors);
        Assert.Empty(first.Events);

        FrontlineStepResult step = session.Step(valid);

        Assert.Equal(0, step.Tick);
        Assert.Same(first, step.TickStart);
        Assert.Equal(
            [Team0Life0, Team1Life0],
            step.ActionResolutions
                .Select(resolution => resolution.ActorId)
                .ToArray());
        Assert.Equal(1, session.State.Tick);
    }

    [Fact]
    public void DecisionDictionaryInsertionOrder_DoesNotAffectResolution()
    {
        var firstSession = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveOpen());
        var secondSession = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveOpen());
        firstSession.PrepareTick();
        secondSession.PrepareTick();

        FrontlineStepResult first = firstSession.Step(
            new Dictionary<FrontlineActorId, BotDecision>
            {
                [Team0Life0] = BotDecision.Of(BotAction.TurnLeft),
                [Team1Life0] = BotDecision.Of(BotAction.TurnRight),
            });
        FrontlineStepResult second = secondSession.Step(
            new Dictionary<FrontlineActorId, BotDecision>
            {
                [Team1Life0] = BotDecision.Of(BotAction.TurnRight),
                [Team0Life0] = BotDecision.Of(BotAction.TurnLeft),
            });

        Assert.Equal(
            first.ActionResolutions.ToArray(),
            second.ActionResolutions.ToArray());
        Assert.Equal(first.Events.ToArray(), second.Events.ToArray());
        Assert.Equal(
            first.ProjectileTraversals.ToArray(),
            second.ProjectileTraversals.ToArray());
        Assert.Equal(first.Control, second.Control);
        Assert.Equal(
            Direction.North,
            firstSession.State.GetActiveLife(Team0Life0).Facing);
        Assert.Equal(
            Direction.North,
            secondSession.State.GetActiveLife(Team0Life0).Facing);
        Assert.Equal(
            Direction.North,
            firstSession.State.GetActiveLife(Team1Life0).Facing);
        Assert.Equal(
            Direction.North,
            secondSession.State.GetActiveLife(Team1Life0).Facing);
    }

    [Fact]
    public void Step_SnapshotsSubmittedEntriesBeforeReadingDecisions()
    {
        var session = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveOpen());
        FrontlineTickStart tickStart = session.PrepareTick();
        var decisions = new EnumerationOnlyDecisionDictionary(
            tickStart.ActiveActors.Select(actorId =>
                new KeyValuePair<FrontlineActorId, BotDecision>(
                    actorId,
                    BotDecision.Of(BotAction.Wait))));

        FrontlineStepResult step = session.Step(decisions);

        Assert.All(
            step.ActionResolutions,
            resolution =>
            {
                Assert.Equal(BotAction.Wait, resolution.ChosenAction);
                Assert.Equal(BotAction.Wait, resolution.ValidatedAction);
                Assert.Equal(ActionResult.Success, resolution.Result);
            });
    }

    [Fact]
    public void InvalidDecisionSets_AreRejectedAtomically()
    {
        var cases = new (
            string Name,
            Func<IReadOnlyList<FrontlineActorId>,
                IReadOnlyDictionary<FrontlineActorId, BotDecision>> Build)[]
        {
            (
                "missing",
                actors => new Dictionary<FrontlineActorId, BotDecision>
                {
                    [actors[0]] = BotDecision.Of(BotAction.Wait),
                }),
            (
                "extra",
                actors => new Dictionary<FrontlineActorId, BotDecision>
                {
                    [actors[0]] = BotDecision.Of(BotAction.Wait),
                    [actors[1]] = BotDecision.Of(BotAction.Wait),
                    [new FrontlineActorId(1, 1, 0)] =
                        BotDecision.Of(BotAction.Wait),
                }),
            (
                "stale life",
                actors => new Dictionary<FrontlineActorId, BotDecision>
                {
                    [actors[0] with { LifeId = actors[0].LifeId + 1 }] =
                        BotDecision.Of(BotAction.Wait),
                    [actors[1]] = BotDecision.Of(BotAction.Wait),
                }),
            (
                "null decision",
                actors => new Dictionary<FrontlineActorId, BotDecision>
                {
                    [actors[0]] = null!,
                    [actors[1]] = BotDecision.Of(BotAction.Wait),
                }),
            (
                "runtime fault",
                actors => new Dictionary<FrontlineActorId, BotDecision>
                {
                    [actors[0]] = BotDecision.Fault("test fault"),
                    [actors[1]] = BotDecision.Of(BotAction.Wait),
                }),
            (
                "unknown action",
                actors => new Dictionary<FrontlineActorId, BotDecision>
                {
                    [actors[0]] = new BotDecision
                    {
                        Action = (BotAction)int.MaxValue,
                    },
                    [actors[1]] = BotDecision.Of(BotAction.Wait),
                }),
            (
                "program on non-shoot action",
                actors => new Dictionary<FrontlineActorId, BotDecision>
                {
                    [actors[0]] = new BotDecision
                    {
                        Action = BotAction.Wait,
                        ShotProgram = ShotProgram.Straight,
                    },
                    [actors[1]] = BotDecision.Of(BotAction.Wait),
                }),
            (
                "out-of-envelope program",
                actors => new Dictionary<FrontlineActorId, BotDecision>
                {
                    [actors[0]] = BotDecision.Shoot(
                        new ShotProgram(2, 0, 0, 1, 0)),
                    [actors[1]] = BotDecision.Of(BotAction.Wait),
                }),
        };

        foreach (var testCase in cases)
        {
            var session = new FrontlineMatchSession(
                FrontlineTestDefinitions.ResolveOpen());
            FrontlineTickStart prepared = session.PrepareTick();
            IReadOnlyDictionary<FrontlineActorId, BotDecision> decisions =
                testCase.Build(prepared.ActiveActors);

            Exception? exception = Record.Exception(
                () => session.Step(decisions));

            Assert.True(
                exception is ArgumentException,
                $"{testCase.Name} returned {exception?.GetType().Name ?? "no exception"}.");
            AssertTickZeroUnchanged(session, prepared);

            session.Step(WaitDecisions(prepared.ActiveActors));
            Assert.Equal(1, session.State.Tick);
        }
    }

    [Fact]
    public void NullDecisionDictionary_IsRejectedWithoutConsumingPreparedTick()
    {
        var session = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveOpen());
        FrontlineTickStart prepared = session.PrepareTick();

        Assert.Throws<ArgumentNullException>(() => session.Step(null!));

        AssertTickZeroUnchanged(session, prepared);
        session.Step(WaitDecisions(prepared.ActiveActors));
        Assert.Equal(1, session.State.Tick);
    }

    private static void AssertPrime(
        FrontlineTeamState team,
        FrontlineActorId actorId,
        Position position,
        Direction facing,
        int maxHealth)
    {
        Assert.Equal(actorId.TeamId, team.TeamId);
        FrontlineUnitState unit = Assert.Single(team.Units);
        Assert.Equal(0, unit.UnitId);
        Assert.Equal("prime-mobile", unit.FormId);
        Assert.Equal(FrontlineLifecycleStatus.Active, unit.LifecycleStatus);
        Assert.Null(unit.RespawnAtTick);
        Assert.Equal(1, unit.NextLifeId);

        FrontlineLifeState life = Assert.IsType<FrontlineLifeState>(
            unit.ActiveLife);
        Assert.Equal(actorId, life.ActorId);
        Assert.Equal(position, life.Position);
        Assert.Equal(facing, life.Facing);
        Assert.Equal(maxHealth, life.Health);
        Assert.Equal(0, life.Cooldown);
        Assert.Equal(0, life.Energy);
        Assert.Equal(0, life.DamageDealt);
        Assert.Equal(ActionResult.None, life.LastActionResult);
        Assert.Equal(0, life.SpawnedAtTick);
    }

    private static void AssertTickZeroUnchanged(
        FrontlineMatchSession session,
        FrontlineTickStart prepared)
    {
        Assert.Equal(0, session.State.Tick);
        Assert.False(session.IsCompleted);
        Assert.Null(session.Result);
        Assert.Empty(session.State.Projectiles);
        Assert.Equal(0, session.State.NextProjectileId);
        Assert.Equal(
            FrontlineControlSystem.CreateInitial(
                session.State.Definition.FrontlineRules!),
            session.State.Control);
        Assert.Equal(new Position(1, 2),
            session.State.GetActiveLife(Team0Life0).Position);
        Assert.Equal(Direction.East,
            session.State.GetActiveLife(Team0Life0).Facing);
        Assert.Equal(ActionResult.None,
            session.State.GetActiveLife(Team0Life0).LastActionResult);
        Assert.Equal(new Position(7, 2),
            session.State.GetActiveLife(Team1Life0).Position);
        Assert.Equal(Direction.West,
            session.State.GetActiveLife(Team1Life0).Facing);
        Assert.Equal(ActionResult.None,
            session.State.GetActiveLife(Team1Life0).LastActionResult);
        Assert.Same(prepared, session.PrepareTick());
    }

    private static IReadOnlyDictionary<FrontlineActorId, BotDecision>
        WaitDecisions(IReadOnlyList<FrontlineActorId> actors) =>
        actors.ToDictionary(
            actor => actor,
            _ => BotDecision.Of(BotAction.Wait));

    private sealed class EnumerationOnlyDecisionDictionary(
        IEnumerable<KeyValuePair<FrontlineActorId, BotDecision>> entries)
        : IReadOnlyDictionary<FrontlineActorId, BotDecision>
    {
        private readonly IReadOnlyList<
            KeyValuePair<FrontlineActorId, BotDecision>> _entries =
                entries.ToArray();

        public int Count => _entries.Count;
        public IEnumerable<FrontlineActorId> Keys =>
            _entries.Select(entry => entry.Key);
        public IEnumerable<BotDecision> Values =>
            _entries.Select(entry => entry.Value);
        public BotDecision this[FrontlineActorId key] =>
            throw new InvalidOperationException(
                "The source dictionary must not be reread after snapshotting.");

        public bool ContainsKey(FrontlineActorId key) =>
            _entries.Any(entry => entry.Key == key);

        public bool TryGetValue(
            FrontlineActorId key,
            out BotDecision value)
        {
            foreach (KeyValuePair<FrontlineActorId, BotDecision> entry in
                     _entries)
            {
                if (entry.Key != key)
                    continue;
                value = entry.Value;
                return true;
            }
            value = null!;
            return false;
        }

        public IEnumerator<KeyValuePair<FrontlineActorId, BotDecision>>
            GetEnumerator() => _entries.GetEnumerator();

        System.Collections.IEnumerator
            System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
