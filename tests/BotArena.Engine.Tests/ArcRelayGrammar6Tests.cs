using System.Collections.Immutable;
using BotArena.Engine;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class ArcRelayGrammar6Tests
{
    private static readonly string[] Composition =
    [
        ArcRelayLaunchClassIds.Minesmith,
        ArcRelayLaunchClassIds.Hush,
        ArcRelayLaunchClassIds.Lantern,
        ArcRelayLaunchClassIds.Mortar,
        ArcRelayLaunchClassIds.Kestrel,
        ArcRelayLaunchClassIds.Palisade,
        ArcRelayLaunchClassIds.Nest,
        ArcRelayLaunchClassIds.Mason,
    ];

    [Fact]
    public void Broadloom6VersionsOnlyMineAndNullFieldRules()
    {
        ActorResolvedMatchDefinition prior = ArcRelayH0Definition.Create(
            Composition, Composition,
            loopProfile: ArcRelayLoopProfile.Broadloom5);
        ActorResolvedMatchDefinition candidate = ArcRelayH0Definition.Create(
            Composition, Composition,
            loopProfile: ArcRelayLoopProfile.Broadloom6);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            candidate.Rules.GameMode);

        Assert.Same(ArcRelayLoopProfile.Broadloom4, ArcRelayLoopProfile.Current);
        Assert.Equal(6, mode.SignatureGrammarVersion);
        ArcRelaySignatureDefinition.TripNode mine = Assert.Single(
            mode.Signatures.OfType<ArcRelaySignatureDefinition.TripNode>());
        Assert.Equal(10, mine.Hull);
        Assert.Equal(20, mine.TriggerDamage);
        Assert.Equal(1, mine.TriggerRadius);
        Assert.Equal(2, mine.MaximumActive);
        Assert.True(mine.HiddenFromNormalVision);
        Assert.Equal(0, mine.RevealRange);
        ArcRelaySignatureDefinition.NullField3 field = Assert.Single(
            mode.Signatures.OfType<ArcRelaySignatureDefinition.NullField3>());
        Assert.Equal(3, field.Radius);
        Assert.Equal(7, field.DurationTicks);
        Assert.True(field.FullSuppression);

        ArcRelaySignatureDefinition[] priorUnchanged = [.. ((
                ArcRelayGameModeDefinition)prior.Rules.GameMode).Signatures
            .Where(value => value.SignatureId is not "trip-node"
                and not "null-field")];
        ArcRelaySignatureDefinition[] candidateUnchanged = [.. mode.Signatures
            .Where(value => value.SignatureId is not "trip-node"
                and not "null-field")];
        Assert.Equal(priorUnchanged, candidateUnchanged);

        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(
                ActorContractManifestSerializer.ToCanonicalJson(candidate));
        var publicMode = Assert.IsType<
            GenericActorRulesContract.ArcRelayGameMode>(
                contract.Rules.GameMode);
        GenericActorRulesContract.ArcRelaySignature publicMine =
            publicMode.Signatures.Single(value => value.Kind == "trip-node");
        Assert.Equal(1, publicMine.TriggerRadius);
        Assert.Equal(2, publicMine.MaximumActive);
        Assert.True(publicMine.HiddenFromNormalVision);
        Assert.True(publicMode.Signatures.Single(value =>
            value.Kind == "null-field").FullSuppression);
    }

    [Fact]
    public void MinesmithMaintainsTwoNodesAndThirdReplacesOldest()
    {
        ArcRelaySignatureRuntime runtime = Runtime(out ActorIdentity mine,
            out _, out _, out _);
        Position source = new(18, 4);
        Position first = new(19, 4);
        Position second = new(18, 5);
        Position third = new(17, 4);
        ArcRelaySignatureRuntime.Life life = Life(mine, source);

        ArcRelaySignatureRuntime.TickResult placed = runtime.Start(
            1, mine, source, "trip-node", [At(first)], [life]);
        Assert.Equal(0, Assert.Single(placed.Events).PrivateTeamId);
        runtime.Start(2, mine, source, "trip-node", [At(second)], [life]);
        Assert.Equal(2, runtime.Project(2).Length);
        ArcRelaySignatureRuntime.TickResult replacement = runtime.Start(
            3, mine, source, "trip-node", [At(third)], [life]);

        Assert.Equal([third, second], runtime.Project(3)
            .SelectMany(value => value.Positions)
            .OrderBy(value => value.Y).ThenBy(value => value.X).ToArray());
        Assert.Contains(replacement.Events, value =>
            Changed(value)?.Reason == "replaced");
    }

    [Fact]
    public void ExactStepTriggersHostileOnlyRadiusOneDamage()
    {
        ArcRelaySignatureRuntime runtime = Runtime(out ActorIdentity mine,
            out _, out _, out _);
        ActorIdentity trigger = ActorIdentity.FromTeamUnitLife(1, 0, 0);
        ActorIdentity adjacent = ActorIdentity.FromTeamUnitLife(1, 1, 0);
        ActorIdentity outside = ActorIdentity.FromTeamUnitLife(1, 2, 0);
        ActorIdentity ally = ActorIdentity.FromTeamUnitLife(0, 4, 0);
        Position node = new(19, 4);
        ArcRelaySignatureRuntime.Life owner = Life(mine, new(18, 4));
        runtime.Start(1, mine, owner.Position, "trip-node", [At(node)], [owner]);

        ArcRelaySignatureRuntime.TickResult result = runtime.ResolvePostMovement(
            2,
            [
                owner,
                Life(trigger, node),
                Life(adjacent, new Position(20, 5)),
                Life(outside, new Position(21, 4)),
                Life(ally, new Position(18, 4)),
            ],
            (_, _) => true);

        ArcRelaySignatureRuntime.Effect.TripNode effect = Assert.IsType<
            ArcRelaySignatureRuntime.Effect.TripNode>(Assert.Single(result.Effects));
        Assert.Equal(20, effect.Damage);
        Assert.True(effect.Targets.SequenceEqual([trigger, adjacent]));
        Assert.Null(Assert.Single(result.Events).PrivateTeamId);
        Assert.Empty(runtime.Project(2));
    }

    [Fact]
    public void HiddenNodeIgnoresBlindFireButFlareAndNullExposeIt()
    {
        ArcRelaySignatureRuntime flareRuntime = Runtime(
            out ActorIdentity mine, out _, out ActorIdentity lantern, out _);
        ActorIdentity enemyLantern = ActorIdentity.FromTeamUnitLife(1, 2, 0);
        Position node = new(19, 4);
        ArcRelaySignatureRuntime.Life owner = Life(mine, new(18, 4));
        ArcRelaySignatureRuntime.Life scout = Life(
            enemyLantern, new Position(17, 4));
        flareRuntime.Start(1, mine, owner.Position, "trip-node", [At(node)],
            [owner, scout]);
        Assert.False(flareRuntime.TryConsumeProjectile(
            node, 1, 1, 10, out _));
        Assert.False(flareRuntime.TryDamageDeployment(
            node, 1, 1, 20, "falling-star", out _));
        flareRuntime.Start(1, enemyLantern, scout.Position, "survey-flare",
            [At(node)], [owner, scout]);
        flareRuntime.Advance(2, [owner, scout]);
        Assert.Contains(flareRuntime.Project(2), value =>
            value.Kind == ArcRelaySignatureDefinition.SignatureKind.SurveyFlare
            && value.Phase == ArcRelaySignatureState.SignaturePhase.Active);
        Assert.True(flareRuntime.IsRevealedForTeam(node, 1, 2));
        Assert.True(flareRuntime.TryConsumeProjectile(
            node, 1, 2, 10, out _));

        ArcRelaySignatureRuntime nullRuntime = Runtime(
            out mine, out ActorIdentity hush, out _, out _);
        ActorIdentity enemyHush = ActorIdentity.FromTeamUnitLife(1, 1, 0);
        owner = Life(mine, new Position(18, 4));
        ArcRelaySignatureRuntime.Life suppressor = Life(
            enemyHush, new Position(20, 4));
        nullRuntime.Start(1, mine, owner.Position, "trip-node", [At(node)],
            [owner, suppressor]);
        nullRuntime.Start(1, enemyHush, suppressor.Position, "null-field", [],
            [owner, suppressor]);
        nullRuntime.Advance(2, [owner, suppressor]);
        nullRuntime.ResolvePostMovement(2, [owner, suppressor], (_, _) => true);
        Assert.True(nullRuntime.Project(2).Single(value =>
            value.Kind == ArcRelaySignatureDefinition.SignatureKind.TripNode)
            .Suppressed);
        Assert.True(nullRuntime.TryDamageDeployment(
            node, 1, 2, 20, "falling-star", out _));
    }

    [Fact]
    public void FullNullFieldCancelsMaturingHostileSignature()
    {
        ArcRelaySignatureRuntime runtime = Runtime(out _,
            out ActorIdentity hush, out _, out ActorIdentity mortar);
        ActorIdentity enemyHush = ActorIdentity.FromTeamUnitLife(1, 1, 0);
        Position impact = new(19, 4);
        ArcRelaySignatureRuntime.Life artillery = Life(
            mortar, new Position(18, 4));
        ArcRelaySignatureRuntime.Life suppressor = Life(enemyHush, impact);
        runtime.Start(1, mortar, artillery.Position, "falling-star",
            [At(impact)], [artillery, suppressor]);
        runtime.Start(1, enemyHush, suppressor.Position, "null-field", [],
            [artillery, suppressor]);
        runtime.Advance(2, [artillery, suppressor]);
        runtime.ResolvePostMovement(2, [artillery, suppressor], (_, _) => true);

        ArcRelaySignatureRuntime.TickResult maturity = runtime.Advance(
            3, [artillery, suppressor]);
        Assert.DoesNotContain(maturity.Effects, value => value is
            ArcRelaySignatureRuntime.Effect.FallingStar);
        Assert.Contains(maturity.Events, value =>
            Changed(value)?.Reason == "cancelled-null-field");
    }

    private static ArcRelaySignatureRuntime Runtime(
        out ActorIdentity mine,
        out ActorIdentity hush,
        out ActorIdentity lantern,
        out ActorIdentity mortar)
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            Composition, Composition,
            loopProfile: ArcRelayLoopProfile.Broadloom6);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(
            definition.Rules.GameMode);
        mine = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        hush = ActorIdentity.FromTeamUnitLife(0, 1, 0);
        lantern = ActorIdentity.FromTeamUnitLife(0, 2, 0);
        mortar = ActorIdentity.FromTeamUnitLife(0, 3, 0);
        return new ArcRelaySignatureRuntime(definition, mode);
    }

    private static ArcRelaySignatureRuntime.Life Life(
        ActorIdentity actor,
        Position position) => new(actor, position, 40, 40);

    private static GenericActorRuntimeActionArgument.PositionTargetArgument At(
        Position position) => new(position);

    private static ArcRelayEvent.SignatureChanged? Changed(
        GenericActorModeEvent value) => value.Payload is
            GenericActorRuntimeObservation.EventPayload.ArcRelay
            { Fact: ArcRelayEvent.SignatureChanged changed }
                ? changed
                : null;
}
