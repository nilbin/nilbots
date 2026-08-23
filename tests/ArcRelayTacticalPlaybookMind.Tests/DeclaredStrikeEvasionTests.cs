using System.Collections.Immutable;
using System.Reflection;
using BotArena.Engine;
using BotArena.Sdk;
using ActorIdentity = BotArena.Sdk.ActorIdentity;
using Direction = BotArena.Sdk.Direction;
using Position = BotArena.Sdk.Position;
using ProjectileHeading = BotArena.Sdk.ProjectileHeading;

public sealed class DeclaredStrikeEvasionTests
{
    private static readonly Position BodyTile = new(12, 13);

    [Fact]
    public void ActiveFightOptInCanLeaveAHostileDeclaredCone()
    {
        MindBody body = Body();
        var strike = new GenericActorContext.ArcRelayPendingStrike(
            new ActorIdentity(1, 0, 0),
            ResolveAtTick: 11,
            new Position(15, 13),
            ProjectileHeading.West,
            body.ActorId,
            [BodyTile, BodyTile.Offset(1, 0)]);
        MindContext mind = Context(body, [strike]);
        GenericActorResolvedMatchContract contract = Contract();

        Assert.Empty(ArenaBasics.SafeEvasionGoals(
            contract, mind, body, includeDeclaredStrikes: false));
        Position[] safe = ArenaBasics.SafeEvasionGoals(
            contract, mind, body, includeDeclaredStrikes: true);

        Assert.NotEmpty(safe);
        Assert.DoesNotContain(BodyTile, safe);
        Assert.DoesNotContain(BodyTile.Offset(1, 0), safe);
    }

    [Fact]
    public void OwnDeclaredConeNeverTriggersEvasion()
    {
        MindBody body = Body();
        var strike = new GenericActorContext.ArcRelayPendingStrike(
            body.ActorId,
            ResolveAtTick: 11,
            BodyTile,
            ProjectileHeading.East,
            new ActorIdentity(1, 0, 0),
            [BodyTile, BodyTile.Offset(1, 0)]);

        Assert.Empty(ArenaBasics.SafeEvasionGoals(
            Contract(), Context(body, [strike]), body,
            includeDeclaredStrikes: true));
    }

    private static GenericActorResolvedMatchContract Contract() =>
        ActorCanonicalContractReader.Parse(
            ActorContractManifestSerializer.ToCanonicalJson(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.Broadloom7)));

    private static MindContext Context(
        MindBody body,
        ImmutableArray<GenericActorContext.ArcRelayPendingStrike> strikes)
    {
        var mode = new GenericActorContext.ModeObservationState.ArcRelay(
            "arc-relay-h0",
            [],
            [],
            [],
            [],
            latestPulseTeamId: null,
            latestPulseTick: null)
        {
            PendingStrikes = strikes,
        };
        return new MindContext(
            MindContext.CurrentSchemaVersion,
            tick: 10,
            new string('a', 64),
            [body],
            slots: [],
            allies: [],
            enemies: [],
            visibleTiles: [],
            visibleProjectiles: [],
            visibleEvents: [],
            heardSounds: [],
            new GenericActorContext.ScoreboardState(
            [
                new GenericActorContext.TeamScoreState(
                    0,
                    eligible: true,
                    [new GenericActorContext.ScoreValue("score", 0)]),
                new GenericActorContext.TeamScoreState(
                    1,
                    eligible: true,
                    [new GenericActorContext.ScoreValue("score", 0)]),
            ]),
            mode,
            participants: []);
    }

    private static MindBody Body()
    {
        ConstructorInfo constructor = Assert.Single(typeof(MindBody)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        return (MindBody)constructor.Invoke(
        [
            new ActorIdentity(0, 0, 0),
            0,
            ArcRelayH0Definition.FormPrefix + ArcRelayLaunchClassIds.Kestrel,
            BodyTile,
            Direction.East,
            30,
            0,
            1,
            null,
            null,
            ArcRelayLaunchClassIds.Kestrel,
            ImmutableArray<GenericActorContext.ObservedRouteCooldown>.Empty,
            0,
            null,
            false,
            0,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Initial,
                0,
                null,
                null,
                null),
            null,
            0UL,
            ImmutableArray<GenericActorActionLegality>.Empty,
            new MindWaitAction("wait", 0),
        ]);
    }
}
