using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// ROLE TAGS (DECISIONS #191 P3;
/// <c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §12). A free-vocabulary,
/// non-authoritative, sticky label a mind attaches to a body — published on its
/// own bodies AND on visible enemies, which is what makes deception a real move
/// and the viewer legible.
/// </summary>
public sealed class GenericMindRoleTagTests
{
    [Fact]
    public void ATagIsStickyAndArrivesOnTheTickAfterItIsSet()
    {
        ActorResolvedMatchDefinition definition = Definition();
        var tagged = new List<(int Tick, string? Tag)>();
        using GenericActorMatchSession session = Session(
            definition,
            (participantId, observation) =>
            {
                if (observation.ParticipantId == 0
                    && !observation.Bodies.IsEmpty)
                {
                    tagged.Add((
                        observation.Tick,
                        observation.Bodies[0].RoleTag));
                }
                // Set once, on tick 3 only. Everything after must still
                // publish it: a role assignment costs one call, not one call
                // per tick.
                return observation.Tick == 3 && participantId == 0
                    ? Commands(definition, observation, "channeler")
                    : Commands(definition, observation, null);
            });
        for (int tick = 0; tick < 8; tick++)
            session.Step();

        Assert.Equal(
            [
                (0, null),
                (1, null),
                (2, null),
                (3, null),
                // Set during tick 3, so tick 4 is the first observation that
                // carries it — the same one-tick telegraph grammar a claim, a
                // windup and a purchase already use.
                (4, "channeler"),
                (5, "channeler"),
                (6, "channeler"),
                (7, "channeler"),
            ],
            tagged);
    }

    [Fact]
    public void TheEmptyStringClearsAndNullLeavesItAlone()
    {
        ActorResolvedMatchDefinition definition = Definition();
        var tagged = new List<string?>();
        using GenericActorMatchSession session = Session(
            definition,
            (participantId, observation) =>
            {
                if (observation.ParticipantId == 0
                    && !observation.Bodies.IsEmpty)
                {
                    tagged.Add(observation.Bodies[0].RoleTag);
                }
                if (participantId != 0)
                    return Commands(definition, observation, null);
                return observation.Tick switch
                {
                    1 => Commands(definition, observation, "courier"),
                    4 => Commands(definition, observation, string.Empty),
                    _ => Commands(definition, observation, null),
                };
            });
        for (int tick = 0; tick < 7; tick++)
            session.Step();

        Assert.Equal(
            [null, null, "courier", "courier", "courier", null, null],
            tagged);
    }

    /// <summary>
    /// §12.2, made observable: a visible enemy's declared job is published. It
    /// is a smaller leak than the bank, the tier board, the claim, the hold or
    /// the pile ledger — all of which telegraph with no visibility requirement
    /// at all — and it is what makes a deliberately wrong label a real move.
    /// </summary>
    [Fact]
    public void AVisibleEnemyCarriesTheTagItsOwnMindPublished()
    {
        ActorResolvedMatchDefinition definition = Definition();
        var seen = new List<string>();
        using GenericActorMatchSession session = Session(
            definition,
            (participantId, observation) =>
            {
                foreach (GenericActorRuntimeObservation.ObservedEnemyState
                         enemy in observation.Enemies)
                {
                    if (enemy.RoleTag is not null)
                        seen.Add(enemy.RoleTag);
                }
                return March(
                    definition,
                    observation,
                    observation.Bodies.Any(body => body.RoleTag is null)
                        ? participantId == 0 ? "bait" : "anvil"
                        : null);
            });
        session.Run();

        // Both minds' labels are readable by the other side; neither can be
        // trusted, which is the point.
        Assert.Contains("bait", seen);
        Assert.Contains("anvil", seen);
    }

    [Fact]
    public void ATagOverTheCapIsRefusedBeforeItReachesTheWorld()
    {
        ActorResolvedMatchDefinition definition = Definition();
        using GenericActorMatchSession session = Session(
            definition,
            (_, observation) => Commands(
                definition,
                observation,
                new string('a', 25)));

        // MindValueRules caps it in the SDK, the codec caps it on the wire, and
        // the chronology caps it in the document. This is the engine-side
        // refusal: an over-long tag never becomes recorded evidence.
        Assert.Throws<ArgumentException>(() => session.Step());
    }

    [Fact]
    public void ASlotsNextLifeStartsUnlabelled()
    {
        // A tag is keyed by LIFE, not by slot: a courier run handed to a new
        // body is a decision the mind makes again, not one it inherits.
        Assert.True(GenericMindRoleTag.IsValid("channeler"));
        Assert.True(GenericMindRoleTag.IsValid(string.Empty));
        Assert.False(GenericMindRoleTag.IsValid("Channeler"));
        Assert.False(GenericMindRoleTag.IsValid("channeler-"));
        Assert.False(GenericMindRoleTag.IsValid(new string('a', 25)));
        Assert.Equal(
            "screen",
            GenericMindRoleTag.Apply("channeler", "screen"));
        Assert.Equal(
            "channeler",
            GenericMindRoleTag.Apply("channeler", null));
        Assert.Null(GenericMindRoleTag.Apply("channeler", string.Empty));
    }

    private static ActorResolvedMatchDefinition Definition() =>
        GenericMindSessionTestFixture.OnMindProfile(
            FrontlineLabsDefinition.CreateAutomaticCompanionsExperiment());

    private static GenericActorMatchSession Session(
        ActorResolvedMatchDefinition definition,
        Func<int, GenericMindRuntimeObservation, GenericMindRuntimeDecisions>
            think)
    {
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = definition.Topology.Participants.ToDictionary(
                participant => participant.ParticipantId,
                participant =>
                    new GenericMindSessionTestFixture.RecordingMindFactory(
                        (_, observation) => think(
                            participant.ParticipantId,
                            observation)));
        return new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 6_121);
    }

    /// <summary>
    /// Walk every body at the middle column so the two armies actually see
    /// each other. A tag nobody can see is not evidence that tags are public.
    /// </summary>
    private static GenericMindRuntimeDecisions March(
        ActorResolvedMatchDefinition definition,
        GenericMindRuntimeObservation observation,
        string? tag)
    {
        ActorActionDefinition wait = definition.Rules.Actions
            .First(action => action.Kind == ActorActionKind.Wait);
        ActorActionDefinition? move = definition.Rules.Actions
            .FirstOrDefault(action =>
                action.Kind == ActorActionKind.Movement);
        int centre = definition.Map.Width / 2;
        return new GenericMindRuntimeDecisions(
        [
            .. observation.Bodies.Select(body =>
            {
                if (move is null || body.Position.X == centre)
                {
                    return new GenericMindCommand(
                        body.ActorId.UnitId,
                        body.ActorId.LifeId,
                        wait.Id,
                        wait.Code,
                        ImmutableArray<GenericActorRuntimeActionArgument>
                            .Empty,
                        tag);
                }
                Direction axis = body.Position.X < centre
                    ? Direction.East
                    : Direction.West;
                return new GenericMindCommand(
                    body.ActorId.UnitId,
                    body.ActorId.LifeId,
                    move.Id,
                    move.Code,
                    [
                        new GenericActorRuntimeActionArgument
                            .DirectionArgument(axis),
                    ],
                    tag);
            }),
        ]);
    }

    private static GenericMindRuntimeDecisions Commands(
        ActorResolvedMatchDefinition definition,
        GenericMindRuntimeObservation observation,
        string? tag)
    {
        ActorActionDefinition wait = definition.Rules.Actions
            .First(action => action.Kind == ActorActionKind.Wait);
        return new GenericMindRuntimeDecisions(
        [
            .. observation.Bodies.Select(body => new GenericMindCommand(
                body.ActorId.UnitId,
                body.ActorId.LifeId,
                wait.Id,
                wait.Code,
                ImmutableArray<GenericActorRuntimeActionArgument>.Empty,
                tag)),
        ]);
    }
}
