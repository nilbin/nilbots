using System.Collections.Immutable;
using System.Reflection;
using BotArena.Engine;
using BotArena.Sdk;
using ActorIdentity = BotArena.Sdk.ActorIdentity;
using Direction = BotArena.Sdk.Direction;
using Position = BotArena.Sdk.Position;
using ProjectileHeading = BotArena.Sdk.ProjectileHeading;

/// <summary>
/// Player-plotted Towline rays may spend the Hook rule's published wall
/// allowance. Ordinary locked/Smart rays continue to use a zero allowance.
/// </summary>
public sealed class AuthoredHookRayTests
{
    private static readonly GenericActorResolvedMatchContract Contract =
        ActorCanonicalContractReader.Parse(
            ActorContractManifestSerializer.ToCanonicalJson(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.Broadloom7)));

    [Fact]
    public void BroadloomNestLaneNeedsAndAcceptsExactlyOneWallTile()
    {
        var source = new Position(21, 5);
        var nestPost = new Position(27, 5);

        Assert.False(ArenaBasics.AuthoredHeadingRayIsClear(
            Contract.Map, source, nestPost,
            authoredWallPenetrationTiles: 0));
        Assert.True(ArenaBasics.AuthoredHeadingRayIsClear(
            Contract.Map, source, nestPost,
            authoredWallPenetrationTiles: 1));
    }

    [Fact]
    public void OneWallAllowanceStillRejectsASecondWallTile()
    {
        Assert.False(ArenaBasics.AuthoredHeadingRayIsClear(
            Contract.Map,
            new Position(20, 5),
            new Position(30, 5),
            authoredWallPenetrationTiles: 1));
    }

    [Fact]
    public void TowlineLegalityAcceptsTheAuthoredBreachHeading()
    {
        GenericActorRulesContract.ActionDefinition action = Contract.Rules.Actions
            .Single(value => value.Id == "tractor-hook");
        var legality = new GenericActorActionLegality(
            action.Id,
            action.Code,
            allowedByForm: true,
            available: true,
            [
                new GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint(
                        Enum.GetValues<ProjectileHeading>()),
            ]);
        MindBody body = Body(new Position(21, 5), legality);

        Assert.False(ArenaBasics.CanUseHeadingSignature(
            Contract, body, "tractor-hook", new Position(27, 5)));
        Assert.True(ArenaBasics.CanUseAuthoredHeadingSignature(
            Contract,
            body,
            "tractor-hook",
            new Position(27, 5),
            authoredWallPenetrationTiles: 1));
    }

    private static MindBody Body(
        Position position,
        GenericActorActionLegality legality)
    {
        ConstructorInfo constructor = Assert.Single(typeof(MindBody)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        return (MindBody)constructor.Invoke(
        [
            new ActorIdentity(0, 2, 0),
            0,
            ArcRelayH0Definition.FormPrefix + ArcRelayLaunchClassIds.Towline,
            position,
            Direction.East,
            40,
            0,
            1,
            null,
            null,
            ArcRelayLaunchClassIds.Towline,
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
            ImmutableArray.Create(legality),
            new MindWaitAction("wait", 0),
        ]);
    }
}
