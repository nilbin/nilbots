using System.Collections.Immutable;
using BotArena.Sdk;
using ActorIdentity = BotArena.Sdk.ActorIdentity;
using Direction = BotArena.Sdk.Direction;
using Position = BotArena.Sdk.Position;

namespace BotArena.Guest.Tests;

/// <summary>
/// THE MIGRATION PIN. An existing per-life bot must play the mind profile
/// unchanged, and must play it the same way.
/// </summary>
public sealed class WrappedPerLifeMindTests
{
    [Fact]
    public void TheWrappedBotProducesTheSameAcceptedActionsAsThePerLifeProfile()
    {
        // The pin, at session level: drive ONE wrapped mind and N independent
        // per-life sessions of the SAME bot type over the same world, and
        // require the accepted action sequences to be identical tick for tick
        // and body for body. If the observation specialization is wrong in any
        // field the bot reads, this diverges and localizes the field.
        MindStart start = GenericMindGuestTestFixture.Start();
        var wrapped = new WrappedPerLifeMind(
            _ => new GenericMindGuestTestFixture.PrecedenceBot(),
            "precedence");
        wrapped.StartMatch(start);

        // Keyed by (unit, life): the per-life profile gives a RETURNING body a
        // fresh instance with empty memory, and the control arm has to model
        // that or it would be comparing the wrap against a stronger opponent
        // than the profile it is meant to reproduce.
        Dictionary<(int UnitId, int LifeId),
            GenericMindGuestTestFixture.PrecedenceBot> perLife = [];
        var wrappedActions = new List<string>();
        var perLifeActions = new List<string>();

        for (int tick = 0; tick < 12; tick++)
        {
            MindBody[] bodies = World(tick);
            MindContext mind = GenericMindGuestTestFixture.Context(
                start,
                tick,
                bodies);
            wrapped.Think(mind);
            foreach (MindCommand command in mind.HarvestCommands())
            {
                wrappedActions.Add(Describe(
                    command.UnitId,
                    command.LifeId,
                    command.ActionId,
                    command.Arguments,
                    command.DebugMessage));
            }

            // The per-life arm: an independent bot instance per body, each fed
            // the observation the per-life profile would have built for it.
            MindContext control = GenericMindGuestTestFixture.Context(
                start,
                tick,
                World(tick));
            foreach (MindBody body in control.Bodies)
            {
                var key = (body.UnitId, body.ActorId.LifeId);
                if (!perLife.TryGetValue(key, out var bot))
                {
                    bot = new GenericMindGuestTestFixture.PrecedenceBot();
                    perLife[key] = bot;
                    bot.StartLife(PerLifeStart(start, body));
                }
                GenericActorDecision decision = bot.Tick(
                    PerLifeContext(start, control, body));
                perLifeActions.Add(Describe(
                    body.UnitId,
                    body.ActorId.LifeId,
                    decision.ActionId,
                    decision.Arguments,
                    decision.DebugMessage));
            }
        }

        Assert.NotEmpty(wrappedActions);
        Assert.Equal(perLifeActions, wrappedActions);
    }

    [Fact]
    public void TheSpecializedObservationIsThePerLifeOneFieldForField()
    {
        MindStart start = GenericMindGuestTestFixture.Start();
        MindContext mind = GenericMindGuestTestFixture.Context(
            start,
            tick: 4,
            GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2), health: 5),
            GenericMindGuestTestFixture.Body(1, 0, new Position(3, 2), health: 4),
            GenericMindGuestTestFixture.Body(2, 0, new Position(4, 2), health: 3));
        MindBody subject = mind.Bodies[1];

        GenericActorContext context = WrappedPerLifeMind.Specialize(
            start,
            mind,
            subject);

        Assert.Equal(
            GenericActorContext.CurrentSchemaVersion,
            context.SchemaVersion);
        Assert.Equal(mind.Tick, context.Tick);
        Assert.Equal(
            mind.MatchContractFingerprint,
            context.MatchContractFingerprint);

        // Self is the matching body, field for field.
        Assert.Equal(subject.ActorId, context.Self.ActorId);
        Assert.Equal(subject.Generation, context.Self.Generation);
        Assert.Equal(subject.FormId, context.Self.FormId);
        Assert.Equal(subject.Position, context.Self.Position);
        Assert.Equal(subject.Facing, context.Self.Facing);
        Assert.Equal(subject.Health, context.Self.Health);
        Assert.Equal(subject.Cooldown, context.Self.Cooldown);
        Assert.Equal(subject.Energy, context.Self.Energy);
        Assert.Equal(subject.ClassId, context.Self.ClassId);
        Assert.Equal(subject.CarriedScrap, context.Self.CarriedScrap);

        // Allies are the OTHER own bodies — the per-life meaning of the word —
        // and never the subject itself.
        Assert.Equal(
            new[] { 0, 2 },
            context.Allies.Select(ally => ally.ActorId.UnitId));
        Assert.DoesNotContain(
            context.Allies,
            ally => ally.ActorId == subject.ActorId);

        // Team-shared collections pass through untouched, and the legality mask
        // is the SUBJECT's rather than the army's.
        Assert.Equal(mind.Enemies.AsEnumerable(), context.Enemies.AsEnumerable());
        Assert.Equal(
            mind.VisibleTiles.AsEnumerable(),
            context.VisibleTiles.AsEnumerable());
        Assert.Equal(mind.Mode, context.Mode);
        Assert.Equal(mind.Scoreboard, context.Scoreboard);
        Assert.Equal(
            mind.Participants.AsEnumerable(),
            context.Participants.AsEnumerable());
        Assert.Equal(
            subject.ActionLegalities.Select(action => action.ActionId),
            context.ActionLegalities.Select(action => action.ActionId));

        // TeamUnits is the mind's own slot table stamped with its team — exact
        // in every format that puts one participant on a scoring team, which is
        // every format the profile ships with.
        Assert.Equal(
            mind.Slots.Select(slot => slot.UnitId),
            context.TeamUnits.Select(slot => slot.UnitId));
        Assert.All(
            context.TeamUnits,
            slot => Assert.Equal(start.TeamId, slot.TeamId));
    }

    [Fact]
    public void DeathDiscardsTheSubBrainAndRespawnGetsAFreshInstance()
    {
        MindStart start = GenericMindGuestTestFixture.Start();
        var created = new List<GenericMindGuestTestFixture.PrecedenceBot>();
        var wrapped = new WrappedPerLifeMind(
            _ =>
            {
                var bot = new GenericMindGuestTestFixture.PrecedenceBot();
                created.Add(bot);
                return bot;
            },
            "precedence");
        wrapped.StartMatch(start);

        // Tick 0: two bodies alive.
        wrapped.Think(GenericMindGuestTestFixture.Context(
            start,
            0,
            GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2)),
            GenericMindGuestTestFixture.Body(1, 0, new Position(3, 2))));
        Assert.Equal(2, created.Count);
        Assert.Equal(2, wrapped.SubBrainCount);

        // Tick 1: unit 1 died. Its sub-brain goes with it, immediately, on the
        // tick the body stops being live — not lazily on some later tick.
        wrapped.Think(GenericMindGuestTestFixture.Context(
            start,
            1,
            GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2))));
        Assert.Equal(2, created.Count);
        Assert.Equal(1, wrapped.SubBrainCount);

        // Tick 2: unit 1 respawns with a NEW life ID. That is a fresh instance
        // with empty fields, which is exactly the respawn amnesia the per-life
        // generation has — the wrap must not accidentally hand a returning body
        // its predecessor's memory.
        wrapped.Think(GenericMindGuestTestFixture.Context(
            start,
            2,
            GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2)),
            GenericMindGuestTestFixture.Body(1, 1, new Position(9, 9))));
        Assert.Equal(3, created.Count);
        Assert.Equal(2, wrapped.SubBrainCount);
        Assert.Equal(1, created[2].StartLifeCalls);
        Assert.Equal(new ActorIdentity(0, 1, 1), created[2].StartedAs);
    }

    [Fact]
    public void ASameLifeFormChangeKeepsTheSubBrainAndItsMemory()
    {
        MindStart start = GenericMindGuestTestFixture.Start();
        var created = new List<GenericMindGuestTestFixture.PrecedenceBot>();
        var wrapped = new WrappedPerLifeMind(
            _ =>
            {
                var bot = new GenericMindGuestTestFixture.PrecedenceBot();
                created.Add(bot);
                return bot;
            },
            "precedence");
        wrapped.StartMatch(start);

        for (int tick = 0; tick < 3; tick++)
        {
            wrapped.Think(GenericMindGuestTestFixture.Context(
                start,
                tick,
                GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2))));
        }

        // One life, one instance, three ticks: the identity never changed, so
        // neither did the private memory.
        Assert.Single(created);
        Assert.Equal(1, created[0].StartLifeCalls);
    }

    [Fact]
    public void AZeroBodyTickConstructsNothingAndDiscardsEverything()
    {
        MindStart start = GenericMindGuestTestFixture.Start();
        int constructed = 0;
        var wrapped = new WrappedPerLifeMind(
            _ =>
            {
                constructed++;
                return new GenericMindGuestTestFixture.PrecedenceBot();
            },
            "precedence");
        wrapped.StartMatch(start);

        // StartMatch itself constructs nothing: a per-life bot's StartLife
        // belongs to a body, and no body exists before tick 0.
        Assert.Equal(0, constructed);

        wrapped.Think(GenericMindGuestTestFixture.Context(
            start,
            0,
            GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2))));
        Assert.Equal(1, constructed);

        // Total body loss: the mind still ticks, and the wrap has nothing to
        // delegate to. It must not throw and must not keep the dead body's
        // sub-brain alive.
        wrapped.Think(GenericMindGuestTestFixture.Context(start, 1));
        Assert.Equal(1, constructed);
        Assert.Equal(0, wrapped.SubBrainCount);
    }

    /// <summary>
    /// THE SEED FIX (P3). A sub-brain's private stream is the body's OWN
    /// life-domain seed, handed through untouched — not derived from the mind's
    /// participant-domain seed, which is what P2 had to do and flagged as the
    /// null pin's one honest divergence. Independence still holds, because the
    /// host derives one seed per <c>(team, unit, life)</c>; what changes is
    /// that it is now the SAME seed the per-life profile hands that life.
    /// </summary>
    [Fact]
    public void EachSubBrainIsSeededWithItsBodysOwnPerLifeSeed()
    {
        MindStart start = GenericMindGuestTestFixture.Start();
        var seen = new List<ulong>();
        var wrapped = new WrappedPerLifeMind(
            _ => new SeedRecordingBot(seen),
            "seeds");
        wrapped.StartMatch(start);

        wrapped.Think(GenericMindGuestTestFixture.Context(
            start,
            0,
            GenericMindGuestTestFixture.Body(
                0,
                0,
                new Position(2, 2),
                bodyRandomSeed: 0xAAAA_BBBB_CCCC_DDDDUL),
            GenericMindGuestTestFixture.Body(
                1,
                0,
                new Position(3, 2),
                bodyRandomSeed: 0x1111_2222_3333_4444UL)));
        // A slot's next life carries its own seed, so two lives of one slot
        // never share a stream.
        wrapped.Think(GenericMindGuestTestFixture.Context(
            start,
            1,
            GenericMindGuestTestFixture.Body(
                0,
                0,
                new Position(2, 2),
                bodyRandomSeed: 0xAAAA_BBBB_CCCC_DDDDUL),
            GenericMindGuestTestFixture.Body(
                1,
                1,
                new Position(3, 2),
                bodyRandomSeed: 0x9999_8888_7777_6666UL)));

        Assert.Equal(
            [
                0xAAAA_BBBB_CCCC_DDDDUL,
                0x1111_2222_3333_4444UL,
                0x9999_8888_7777_6666UL,
            ],
            seen);
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    /// <summary>
    /// THE RANDOM-DRAWING PIN. A per-life bot whose decision depends on
    /// <c>context.Random</c> must produce the SAME action sequence hosted on
    /// the mind profile as it does on its own — the property P2 could not
    /// offer and the reason the seed moved onto the body.
    /// </summary>
    [Fact]
    public void ARandomDrawingBotReproducesItsPerLifeSequenceExactly()
    {
        MindStart start = GenericMindGuestTestFixture.Start();
        const ulong bodySeed = 0x5EED_5EED_5EED_5EEDUL;

        // Hosted on the mind profile, through the wrap.
        var wrappedDraws = new List<string>();
        var wrapped = new WrappedPerLifeMind(
            _ => new RandomDrawingBot(wrappedDraws),
            "dice");
        wrapped.StartMatch(start);
        for (int tick = 0; tick < 12; tick++)
        {
            wrapped.Think(GenericMindGuestTestFixture.Context(
                start,
                tick,
                GenericMindGuestTestFixture.Body(
                    0,
                    0,
                    new Position(2, 2),
                    bodyRandomSeed: bodySeed)));
        }

        // The same bot, run directly as a per-life bot with the seed the
        // per-life profile would have handed it.
        var directDraws = new List<string>();
        var direct = new RandomDrawingBot(directDraws);
        // The stream the per-life Guest would have built from the life's seed.
        var directRandom = new GuestRandom(bodySeed);
        direct.StartLife(new GenericActorMatchStart
        {
            SchemaVersion =
                GenericActorContractVersions.MatchStartSchemaVersion,
            RuntimeContractVersion =
                GenericActorContractVersions.RuntimeContractVersion,
            ActorId = new ActorIdentity(0, 0, 0),
            ParticipantId = start.ParticipantId,
            ActorRandomSeed = bodySeed,
            TeamRandomSeed = start.TeamRandomSeed,
            Origin = new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Initial,
                Generation: 0,
                ParentActorId: null,
                SourceTransitionId: null,
                SourceOperationId: null),
            Contract = start.Contract,
        });
        for (int tick = 0; tick < 12; tick++)
        {
            direct.Tick(WrappedPerLifeMind.Specialize(
                start,
                GenericMindGuestTestFixture.Context(
                    start,
                    tick,
                    GenericMindGuestTestFixture.Body(
                        0,
                        0,
                        new Position(2, 2),
                        bodyRandomSeed: bodySeed)),
                GenericMindGuestTestFixture.Body(
                    0,
                    0,
                    new Position(2, 2),
                    bodyRandomSeed: bodySeed),
                directRandom));
        }

        Assert.Equal(directDraws, wrappedDraws);
        Assert.Equal(12, wrappedDraws.Count);
        // A bot that never drew would pass this trivially; make sure the draws
        // actually vary.
        Assert.True(wrappedDraws.Distinct().Count() > 1);
    }

    private sealed class SeedRecordingBot : IGenericActorBot
    {
        private readonly List<ulong> _seeds;

        public SeedRecordingBot(List<ulong> seeds) => _seeds = seeds;

        public void StartLife(GenericActorMatchStart start) =>
            _seeds.Add(start.ActorRandomSeed);

        public GenericActorDecision Tick(GenericActorContext context) =>
            GenericActorDecision.WithoutArguments("wait", 0, null);
    }

    private sealed class RandomDrawingBot : IGenericActorBot
    {
        private readonly List<string> _draws;

        public RandomDrawingBot(List<string> draws) => _draws = draws;

        public void StartLife(GenericActorMatchStart start)
        {
        }

        public GenericActorDecision Tick(GenericActorContext context)
        {
            // The private draw IS the decision: an author breaking a tie with
            // the stream is exactly the case the seed fix exists for.
            int draw = context.Random.NextInt(0, 1_000_000);
            _draws.Add(draw.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            return GenericActorDecision.WithoutArguments(
                "wait",
                0,
                $"draw:{draw}");
        }
    }

    private static MindBody[] World(int tick) =>
        tick switch
        {
            // Unit 2 dies at tick 5 and returns at tick 8 with a new life, so
            // the pin covers a mid-match death and respawn rather than a static
            // roster.
            >= 5 and < 8 =>
            [
                GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2), health: 5),
                GenericMindGuestTestFixture.Body(1, 0, new Position(3, 2), health: 4),
            ],
            >= 8 =>
            [
                GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2), health: 5),
                GenericMindGuestTestFixture.Body(1, 0, new Position(3, 2), health: 4),
                GenericMindGuestTestFixture.Body(2, 1, new Position(1, 1), health: 6),
            ],
            _ =>
            [
                GenericMindGuestTestFixture.Body(0, 0, new Position(2, 2), health: 5),
                GenericMindGuestTestFixture.Body(1, 0, new Position(3, 2), health: 4),
                GenericMindGuestTestFixture.Body(2, 0, new Position(4, 2), health: 3),
            ],
        };

    private static GenericActorMatchStart PerLifeStart(
        MindStart start,
        MindBody body) =>
        new()
        {
            SchemaVersion =
                GenericActorContractVersions.MatchStartSchemaVersion,
            RuntimeContractVersion =
                GenericActorContractVersions.RuntimeContractVersion,
            ActorId = body.ActorId,
            ParticipantId = start.ParticipantId,
            ActorRandomSeed = 1,
            TeamRandomSeed = start.TeamRandomSeed,
            Origin = body.Origin,
            Contract = start.Contract,
        };

    private static GenericActorContext PerLifeContext(
        MindStart start,
        MindContext mind,
        MindBody body) =>
        WrappedPerLifeMind.Specialize(start, mind, body);

    private static string Describe(
        int unitId,
        int lifeId,
        string actionId,
        ImmutableArray<GenericActorActionArgument> arguments,
        string? debugMessage) =>
        $"{unitId}:{lifeId}:{actionId}:"
        + string.Join(
            ",",
            arguments.Select(argument => argument switch
            {
                GenericActorActionArgument.DirectionArgument direction =>
                    direction.Value.ToString(),
                _ => argument.Kind.ToString(),
            }))
        + $":{debugMessage}";
}
