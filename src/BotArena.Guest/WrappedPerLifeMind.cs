using System.Collections.Immutable;
using BotArena.Sdk;

namespace BotArena.Guest;

/// <summary>
/// THE MIGRATION ADAPTER. A mind that hosts an ordinary per-life
/// <see cref="IGenericActorBot"/> — one sub-brain per live body — so an
/// artifact whose author never heard of the mind profile plays on it with ZERO
/// SOURCE EDITS, at the cost of a rebuild.
///
/// <para>It ships in the Guest rather than in player source because
/// <c>GuestHost.RunDetected</c> already selects programming models by static
/// type analysis: a type that implements only <see cref="IGenericActorBot"/>
/// gets this facade automatically and attests BOTH profiles.</para>
///
/// <para><b>Per-life semantics are reproduced exactly, on purpose.</b> A
/// sub-brain is constructed on its body's FIRST tick and discarded the moment
/// that body is no longer live, so a respawn gets a genuinely fresh instance
/// with empty fields — the respawn amnesia the per-life generation has — and a
/// same-life form change keeps the instance, because the life ID did not
/// change. Sub-brains never see each other: there is no shared state, no
/// blackboard, and no way for one to read another's fields. The only thing they
/// share is the frozen observation, which is exactly what they shared
/// before.</para>
///
/// <para><b>The observation is a projection, not a translation.</b> Each
/// sub-brain receives the per-life <c>GenericActorContext</c> the per-life
/// profile would have delivered, field for field: <c>Self</c> from its
/// <see cref="MindBody"/>, <c>TeamUnits</c> from the mind's own slot table,
/// <c>Allies</c> from the OTHER own bodies plus any allied mind's bodies, and
/// every team-shared collection passed through untouched. Because the mind
/// observation reuses the per-life codecs verbatim for every nested type, this
/// specialization moves references rather than rebuilding values.</para>
///
/// <para><b>The random stream is EXACT, and that is P3's fix to a P2 flag.</b>
/// A per-life bot's private stream is seeded host-side in the life domain from
/// the match seed, which a mind's own participant-domain seed cannot
/// reproduce. P2 therefore derived each sub-brain's seed from the mind's seed
/// and the body's identity — independent per body and replay-stable, but NOT
/// the sequence the per-life profile would have produced, so a wrapped bot that
/// drew from <c>context.Random</c> made different private tie-breaks and the
/// §7.2 null pin would have had to explain that away. The engine now publishes
/// each body's own life seed on
/// <see cref="MindBody.BodyRandomSeed"/> and the wrap hands it straight
/// through, so a Random-drawing bot reproduces its per-life behaviour exactly.
/// There is no remaining documented divergence.</para>
/// </summary>
internal sealed class WrappedPerLifeMind : IGenericMindBot
{
    private readonly Func<string, IGenericActorBot> _botFactory;
    private readonly string _botName;
    private readonly Dictionary<LifeKey, SubBrain> _subBrains = [];
    private MindStart? _start;

    public WrappedPerLifeMind(
        Func<string, IGenericActorBot> botFactory,
        string botName)
    {
        _botFactory = botFactory;
        _botName = botName;
    }

    /// <summary>Live sub-brain count, for the lifecycle tests that pin it.</summary>
    internal int SubBrainCount => _subBrains.Count;

    /// <inheritdoc />
    public void StartMatch(MindStart start)
    {
        // Keep the contract; construct nothing. A per-life bot's constructor
        // and StartLife belong to a BODY, and no body exists yet.
        _start = start;
    }

    /// <inheritdoc />
    public void Think(MindContext mind)
    {
        MindStart start = _start
            ?? throw new InvalidOperationException(
                "The wrapped per-life mind was never started.");

        var live = new HashSet<LifeKey>();
        foreach (MindBody body in mind.Bodies)
        {
            var key = new LifeKey(body.ActorId.UnitId, body.ActorId.LifeId);
            live.Add(key);
            if (!_subBrains.TryGetValue(key, out SubBrain? brain))
            {
                brain = SubBrain.Create(
                    _botFactory(_botName),
                    PerLifeStart(start, body));
                _subBrains[key] = brain;
            }

            brain.TeamRandom.BeginTick(mind.Tick);
            GenericActorDecision decision = brain.Tick(
                Specialize(start, mind, body, brain.Random, brain.TeamRandom));
            body.Command(
                decision.ActionId,
                decision.ActionCode,
                decision.Arguments,
                decision.DebugMessage);
        }

        // A body that is no longer live takes its sub-brain with it. Doing this
        // AFTER the loop rather than lazily is what makes "death discards the
        // instance" true even on a tick where nothing new spawned.
        if (_subBrains.Count != live.Count)
        {
            foreach (LifeKey key in _subBrains.Keys.Where(
                         existing => !live.Contains(existing)).ToArray())
            {
                _subBrains.Remove(key);
            }
        }
    }

    /// <summary>
    /// Rebuilds the per-life observation for one body, field for field.
    /// </summary>
    /// <param name="bodyRandom">
    /// The sub-brain's OWN stream, seeded from
    /// <see cref="MindBody.BodyRandomSeed"/>. A per-life bot's
    /// <c>context.Random</c> is life-scoped, so handing it the mind's shared
    /// stream would make every sub-brain draw from one sequence in body
    /// iteration order — reproducing neither the per-life values nor their
    /// independence. Null falls back to the mind's stream, which is only
    /// correct for a bot that never draws.
    /// </param>
    internal static GenericActorContext Specialize(
        MindStart start,
        MindContext mind,
        MindBody body,
        IBotRandom? bodyRandom = null,
        IBotRandom? bodyTeamRandom = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(mind);
        ArgumentNullException.ThrowIfNull(body);

        // Allies under the per-life profile mean "every other body on my
        // scoring team, whoever controls it": my own siblings, plus an allied
        // mind's bodies when a team format puts one there.
        var allies = ImmutableArray.CreateBuilder<
            GenericActorContext.ObservedAllyState>(
            mind.Bodies.Length - 1 + mind.Allies.Length);
        foreach (MindBody sibling in mind.Bodies)
        {
            if (sibling.ActorId == body.ActorId)
                continue;
            allies.Add(new GenericActorContext.ObservedAllyState(
                sibling.ActorId,
                sibling.Generation,
                sibling.FormId,
                sibling.Position,
                sibling.Facing,
                sibling.Health,
                sibling.Cooldown,
                sibling.Energy,
                sibling.PreviousActionResolution,
                sibling.PendingSameLifeTransition,
                sibling.ClassId,
                sibling.RouteCooldowns,
                sibling.CarriedScrap));
        }
        allies.AddRange(mind.Allies);

        return new GenericActorContext(
            GenericActorContractVersions.ObservationSchemaVersion,
            mind.Tick,
            mind.MatchContractFingerprint,
            new GenericActorContext.ObservedSelfState(
                body.ActorId,
                body.Generation,
                body.FormId,
                body.Position,
                body.Facing,
                body.Health,
                body.Cooldown,
                body.Energy,
                body.PreviousActionResolution,
                body.PendingSameLifeTransition,
                body.ClassId,
                body.RouteCooldowns,
                body.CarriedScrap),
            mind.Slots.Select(slot =>
                new GenericActorContext.ObservedUnitSlot(
                    start.TeamId,
                    slot.UnitId,
                    slot.State)),
            mind.Participants,
            allies.ToImmutable(),
            mind.Enemies,
            mind.VisibleTiles,
            mind.VisibleProjectiles,
            mind.VisibleEvents,
            mind.HeardSounds,
            mind.Scoreboard,
            mind.Mode,
            body.ActionLegalities)
        {
            Random = bodyRandom ?? mind.Random,
            // Each sub-brain's OWN re-derived team stream (see SubBrain
            // .TeamRandom): sharing the mind's instance advanced one stream
            // position across bodies and broke the per-life equivalence.
            TeamRandom = bodyTeamRandom ?? mind.TeamRandom,
            Debug = mind.Debug,
        };
    }

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
            // The body's OWN life-domain seed, published by the host. Not
            // derived, not approximated: the exact value the per-life profile
            // would have handed this life.
            ActorRandomSeed = body.BodyRandomSeed,
            TeamRandomSeed = start.TeamRandomSeed,
            Origin = body.Origin,
            Contract = start.Contract,
        };

    private readonly record struct LifeKey(int UnitId, int LifeId);

    private sealed class SubBrain
    {
        private readonly IGenericActorBot _bot;

        private SubBrain(
            IGenericActorBot bot,
            IBotRandom random,
            GuestTeamRandom teamRandom)
        {
            _bot = bot;
            Random = random;
            TeamRandom = teamRandom;
        }

        /// <summary>
        /// This body's own life-scoped stream, seeded from the seed the host
        /// published for it and advancing across that body's ticks exactly as
        /// the per-life Guest's would.
        /// </summary>
        public IBotRandom Random { get; }

        /// <summary>
        /// This body's OWN per-tick team stream. Under the per-life profile
        /// every life re-derives its team stream each tick, so every life's
        /// Nth draw of a tick yields the same value. Sharing the mind's one
        /// instance advanced a single position across sub-brains in body
        /// iteration order — the first null-pin run diverged in 33 of 63
        /// cells on exactly this. Each sub-brain re-deriving its own stream
        /// restores per-life semantics: identical per draw index, and no
        /// body's draws move another's.
        /// </summary>
        public GuestTeamRandom TeamRandom { get; }

        public static SubBrain Create(
            IGenericActorBot bot,
            GenericActorMatchStart start)
        {
            SubBrain brain = new(
                bot
                ?? throw new InvalidOperationException(
                    "Generic actor bot factory returned null."),
                new GuestRandom(start.ActorRandomSeed),
                new GuestTeamRandom(start.TeamRandomSeed));
            brain._bot.StartLife(start);
            return brain;
        }

        public GenericActorDecision Tick(GenericActorContext context) =>
            _bot.Tick(context)
            ?? throw new InvalidOperationException(
                "Generic actor bot returned null.");
    }
}
