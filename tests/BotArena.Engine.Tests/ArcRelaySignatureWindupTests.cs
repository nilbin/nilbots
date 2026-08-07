using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The telegraphed bolt-class signature (owner ruling 2026-08-08): a line
/// attack that damages or displaces announces itself exactly as a gun does.
/// Rail, hook and sentinel freeze their shape at declare, ride the wire in
/// <c>Tell</c> for their own authored windup, and root the declarer while
/// they do. Utility signatures — smoke above all — stay instant and
/// untelegraphed, because nothing about a smoke canister asks its caster to
/// hold still.
/// <para>
/// The windup is a per-signature RULESET field so retuning rail against hook
/// is a data edit, never an engine edit; it defaults to the ruleset's strike
/// windup, and zero authors an instant cast.
/// </para>
/// </summary>
public sealed class ArcRelaySignatureWindupTests
{
    [Fact]
    public void EveryBoltSignatureTakesTheRulesetsStrikeWindup()
    {
        var mode = (ArcRelayGameModeDefinition)ArcRelayH0Definition
            .CreateRules(ArcRelayLoopProfile.AmbushWarren11).GameMode;

        Assert.Equal(
            1,
            mode.Signatures.OfType<ArcRelaySignatureDefinition.RailLine>()
                .Single().WindupTicks);
        Assert.Equal(
            1,
            mode.Signatures.OfType<ArcRelaySignatureDefinition.TractorHook2>()
                .Single().WindupTicks);
        Assert.Equal(
            1,
            mode.Signatures.OfType<ArcRelaySignatureDefinition.SentinelSeed2>()
                .Single().WindupTicks);
    }

    [Fact]
    public void ARulesetWithoutAStrikeWindupKeepsItsInstantSignatures()
    {
        // ambush-10 is the same grammar-2 warren with instant guns. Hook and
        // sentinel author zero and stay instant; rail keeps the telegraph it
        // has always had, because a beam with no windup has nowhere to put
        // one.
        var mode = (ArcRelayGameModeDefinition)ArcRelayH0Definition
            .CreateRules(ArcRelayLoopProfile.AmbushWarren10).GameMode;

        Assert.Equal(
            0,
            mode.Signatures.OfType<ArcRelaySignatureDefinition.TractorHook2>()
                .Single().WindupTicks);
        Assert.Equal(
            0,
            mode.Signatures.OfType<ArcRelaySignatureDefinition.SentinelSeed2>()
                .Single().WindupTicks);
        Assert.Equal(
            2,
            mode.Signatures.OfType<ArcRelaySignatureDefinition.RailLine>()
                .Single().WindupTicks);
    }

    [Fact]
    public void TheWindupIsOnTheWireOnlyWhenARulesetAuthorsOne()
    {
        string telegraphed = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.AmbushWarren11));
        string instant = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.AmbushWarren10));

        Assert.Contains("\"windupTicks\":1", telegraphed);
        Assert.DoesNotContain("\"windupTicks\"", instant);
        // Retuning is data: the two rulesets differ, and the earlier one is
        // byte-identical to what it always was.
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren11)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren10)));
    }

    [Fact]
    public void BoltSignaturesTelegraphAndUtilitySignaturesDoNot()
    {
        ArcRelayEvent.SignatureChanged[] started = Cast(moveAfterCasting: false)
            .Where(value => value.Reason == "started")
            .ToArray();

        string[] bolts =
            ["rail-line", "tractor-hook", "sentinel-seed"];
        ArcRelayEvent.SignatureChanged[] declared = [.. started
            .Where(value => bolts.Contains(value.SignatureId))];
        Assert.NotEmpty(declared);
        Assert.All(
            declared,
            fact => Assert.Equal(
                ArcRelaySignatureState.SignaturePhase.Tell, fact.Phase));

        // Smoke is untouched: instant, active on the tick it is cast, and it
        // never appears in a telegraphing phase at all.
        Assert.All(
            started.Where(value => value.SignatureId == "smoke-canister"),
            fact => Assert.Equal(
                ArcRelaySignatureState.SignaturePhase.Active, fact.Phase));
    }

    [Fact]
    public void ADeclaredHookWindsUpAndThenLaunchesDownItsFrozenLine()
    {
        // The telegraph is the ray from the declaring tile down the declared
        // heading, and it is a LINE, not a lock: no phase between declare and
        // launch re-aims it, so the tiles published at declare are the tiles
        // that resolve. Stepping off them is the whole dodge; stepping on is
        // the block.
        ArcRelayEvent.SignatureChanged[] hook = Cast(moveAfterCasting: false)
            .Where(value => value.SignatureId == "tractor-hook")
            .ToArray();
        string operation = hook.First(value => value.Reason == "started")
            .OperationId;
        ArcRelayEvent.SignatureChanged[] life = [.. hook
            .Where(value => value.OperationId == operation)];

        Assert.Equal(
            ArcRelaySignatureState.SignaturePhase.Tell,
            life[0].Phase);
        Assert.Equal("started", life[0].Reason);
        Assert.Contains(life, value => value.Reason == "launched");
    }

    [Fact]
    public void CommandingAMoveDuringTheWindupAbandonsTheDeclare()
    {
        // The rooted windup, generalized (DECISIONS #221): the declarer may
        // not walk out of its own beam. The mind spends this deliberately
        // through its disengage latch; the engine only makes the two
        // mutually exclusive.
        // A body on this ruleset's movement cadence cannot act twice in a
        // row, so the abandonment is only REACHABLE for a windup longer than
        // one tick: rail's two on the -10 warren, here. That is itself worth knowing — a
        // one-tick declare is unabandonable by construction, and the latch
        // only starts mattering when a profile buys a longer telegraph.
        ArcRelayEvent.SignatureChanged[] facts = Cast(
            moveAfterCasting: true,
            ArcRelayLoopProfile.AmbushWarren10,
            [
                ArcRelayLaunchClassIds.Longshot,
                ArcRelayLaunchClassIds.Longshot,
                ArcRelayLaunchClassIds.Towline,
                ArcRelayLaunchClassIds.Patchbay,
                ArcRelayLaunchClassIds.Lantern,
                ArcRelayLaunchClassIds.Mortar,
                ArcRelayLaunchClassIds.Minesmith,
                ArcRelayLaunchClassIds.Hush,
            ]);

        Assert.Contains(
            facts,
            value => value.Reason == "abandoned-move"
                && value.SignatureId is "rail-line" or "tractor-hook"
                    or "sentinel-seed");
        // And a utility cast is never abandoned by walking.
        Assert.DoesNotContain(
            facts,
            value => value.Reason == "abandoned-move"
                && value.SignatureId == "smoke-canister");
    }

    /// <summary>
    /// Runs an ambush-11 match in which every body casts its class signature
    /// as soon as the mask offers it, and optionally walks on the very next
    /// tick — which is the abandonment case.
    /// </summary>
    private static ArcRelayEvent.SignatureChanged[] Cast(
        bool moveAfterCasting,
        ArcRelayLoopProfile? profile = null,
        IReadOnlyList<string>? classes = null)
    {
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            teamZeroClasses: classes,
            teamOneClasses: classes,
            loopProfile: profile ?? ArcRelayLoopProfile.AmbushWarren11);
        ActorActionDefinition wait = definition.Rules.Actions.Single(
            value => value.Kind == ActorActionKind.Wait);
        ActorActionDefinition move = definition.Rules.Actions.First(
            value => value.Kind == ActorActionKind.Movement);
        var cast = new HashSet<(int Unit, int Life)>();

        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) => new GenericMindRuntimeDecisions(
                [
                    .. observation.Bodies.Select(body =>
                    {
                        var key = (body.ActorId.UnitId, body.ActorId.LifeId);
                        GenericActorRuntimeActionLegality? signature =
                            cast.Contains(key)
                                ? null
                                : body.ActionLegalities.FirstOrDefault(
                                    value => value.ActionCode
                                        >= ArcRelayActionIds
                                            .FirstSignatureCode
                                    && value.Available);
                        if (signature is not null)
                        {
                            cast.Add(key);
                            return new GenericMindCommand(
                                body.ActorId.UnitId,
                                body.ActorId.LifeId,
                                signature.ActionId,
                                signature.ActionCode,
                                SignatureArguments(signature));
                        }
                        GenericActorRuntimeActionLegality? walk =
                            moveAfterCasting
                                ? body.ActionLegalities.FirstOrDefault(
                                    value => value.ActionId == move.Id)
                                : null;
                        return walk is null
                            ? new GenericMindCommand(
                                body.ActorId.UnitId,
                                body.ActorId.LifeId,
                                wait.Id,
                                wait.Code,
                                [])
                            : new GenericMindCommand(
                                body.ActorId.UnitId,
                                body.ActorId.LifeId,
                                walk.ActionId,
                                walk.ActionCode,
                                SignatureArguments(walk));
                    }),
                ]));

        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(definition, factories),
            matchSeed: 20_260_808UL);
        for (int tick = 0; tick < 12; tick++)
            session.Step();

        return [.. session.Chronology.Ticks
            .SelectMany(value => value.Events.Concat(value.TickStart.Events))
            .Select(value => value.Payload)
            .OfType<GenericActorRuntimeObservation.EventPayload.ArcRelay>()
            .Select(value => value.Fact)
            .OfType<ArcRelayEvent.SignatureChanged>()];
    }

    private static ImmutableArray<GenericActorRuntimeActionArgument>
        SignatureArguments(GenericActorRuntimeActionLegality legality) =>
        [
            .. legality.Constraints.Select(value => value switch
            {
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .DirectionConstraint constraint =>
                    (GenericActorRuntimeActionArgument)new
                        GenericActorRuntimeActionArgument.DirectionArgument(
                            constraint.AllowedValues[0]),
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint constraint =>
                    new GenericActorRuntimeActionArgument
                        .ProjectileHeadingArgument(constraint.AllowedValues[0]),
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .UnitTargetConstraint constraint =>
                    new GenericActorRuntimeActionArgument.UnitTargetArgument(
                        constraint.AllowedValues[0]),
                GenericActorRuntimeActionLegality.ArgumentConstraint
                    .PositionTargetConstraint constraint =>
                    new GenericActorRuntimeActionArgument.PositionTargetArgument(
                        constraint.AllowedValues[0]),
                _ => throw new InvalidOperationException(
                    "Arc signature exposed an unexpected argument constraint."),
            }),
        ];
}
