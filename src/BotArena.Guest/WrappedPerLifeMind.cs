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
/// <para><b>One documented divergence.</b> A per-life bot's private random
/// stream is seeded host-side in the life domain from the match seed, which a
/// mind is never given — its own seed is a one-way mix in the participant
/// domain. A sub-brain is therefore seeded deterministically from the MIND's
/// seed and its body's identity: still independent per body, still identical on
/// replay, but NOT the same sequence the per-life profile would have produced.
/// A wrapped bot that never draws from <c>context.Random</c> is unaffected;
/// one that does will make different private tie-breaks. Nothing else about the
/// wrap diverges.</para>
/// </summary>
internal sealed class WrappedPerLifeMind : IGenericMindBot
{
    private const ulong Step = 0x9E3779B97F4A7C15UL;

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

            GenericActorDecision decision = brain.Tick(
                Specialize(start, mind, body));
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
    internal static GenericActorContext Specialize(
        MindStart start,
        MindContext mind,
        MindBody body)
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
            Random = mind.Random,
            TeamRandom = mind.TeamRandom,
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
            ActorRandomSeed = DeriveSubBrainSeed(
                start.MindRandomSeed,
                body.ActorId),
            TeamRandomSeed = start.TeamRandomSeed,
            Origin = body.Origin,
            Contract = start.Contract,
        };

    /// <summary>
    /// One independent stream per wrapped body, derived from the MIND's seed
    /// and the body's exact identity so that no two sub-brains — including a
    /// slot's successive lives — ever share randomness. It cannot equal the
    /// per-life profile's seed, which is mixed from the match seed the mind was
    /// never handed; see the class remarks.
    /// </summary>
    internal static ulong DeriveSubBrainSeed(
        ulong mindRandomSeed,
        ActorIdentity actorId)
    {
        unchecked
        {
            ulong x = Mix(mindRandomSeed + Step);
            x = Mix(x + (Step * ((ulong)actorId.TeamId + 1)));
            x = Mix(x + (Step * ((ulong)actorId.UnitId + 1)));
            return Mix(x + (Step * ((ulong)actorId.LifeId + 1)));
        }
    }

    private static ulong Mix(ulong z)
    {
        unchecked
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    private readonly record struct LifeKey(int UnitId, int LifeId);

    private sealed class SubBrain
    {
        private readonly IGenericActorBot _bot;

        private SubBrain(IGenericActorBot bot) => _bot = bot;

        public static SubBrain Create(
            IGenericActorBot bot,
            GenericActorMatchStart start)
        {
            SubBrain brain = new(
                bot
                ?? throw new InvalidOperationException(
                    "Generic actor bot factory returned null."));
            brain._bot.StartLife(start);
            return brain;
        }

        public GenericActorDecision Tick(GenericActorContext context) =>
            _bot.Tick(context)
            ?? throw new InvalidOperationException(
                "Generic actor bot returned null.");
    }
}
