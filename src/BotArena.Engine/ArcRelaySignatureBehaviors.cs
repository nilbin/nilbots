using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Typed lifecycle handlers for Arc Relay signatures. The runtime remains the
/// chronology coordinator; each kind owns its operation envelope and the
/// phases in which that operation can mature.
/// </summary>
internal sealed partial class ArcRelaySignatureRuntime
{
    private static readonly ImmutableDictionary<
        ArcRelaySignatureDefinition.SignatureKind,
        SignatureBehavior> Behaviors = CreateBehaviors();

    internal static ImmutableArray<(
        ArcRelaySignatureDefinition.SignatureKind Kind,
        Type HandlerType)> RegisteredSignatureBehaviors =>
        [.. Behaviors.OrderBy(entry => entry.Key)
            .Select(entry => (entry.Key, entry.Value.GetType()))];

    private static SignatureBehavior BehaviorFor(
        ArcRelaySignatureDefinition.SignatureKind kind) => Behaviors[kind];

    private static ImmutableDictionary<
        ArcRelaySignatureDefinition.SignatureKind,
        SignatureBehavior> CreateBehaviors()
    {
        SignatureBehavior[] handlers =
        [
            new VectorDashBehavior(),
            new PrismWallBehavior(),
            new TractorHookBehavior(),
            new RepairBeamBehavior(),
            new SurveyFlareBehavior(),
            new FallingStarBehavior(),
            new TripNodeBehavior(),
            new NullFieldBehavior(),
            new ArcTossBehavior(),
            new ExchangeBehavior(),
            new RailLineBehavior(),
            new HardlightBlockBehavior(),
            new TargetPaintBehavior(),
            new KineticBurstBehavior(),
            new SmokeCanisterBehavior(),
            new SentinelSeedBehavior(),
            new SentinelSeed2Behavior(),
            new TractorHook2Behavior(),
            new NullField2Behavior(),
        ];
        var duplicateKinds = handlers.GroupBy(handler => handler.Kind)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .ToArray();
        ArcRelaySignatureDefinition.SignatureKind[] missingKinds = Enum
            .GetValues<ArcRelaySignatureDefinition.SignatureKind>()
            .Except(handlers.Select(handler => handler.Kind))
            .ToArray();
        if (duplicateKinds.Length > 0 || missingKinds.Length > 0)
        {
            throw new InvalidOperationException(
                "Arc Relay signature handlers must cover every kind exactly "
                + $"once; duplicates=[{string.Join(',', duplicateKinds)}], "
                + $"missing=[{string.Join(',', missingKinds)}].");
        }
        return handlers.ToImmutableDictionary(handler => handler.Kind);
    }

    private sealed record SignatureStartContext(
        string OperationId,
        ArcRelaySignatureDefinition Definition,
        ActorIdentity Actor,
        Position Source,
        ActorIdentity? TargetActor,
        Position? TargetPosition,
        ProjectileHeading? Heading,
        Direction? Direction,
        int Tick,
        bool DeclaresLock,
        ImmutableArray<Position> ConeTiles);

    private abstract class SignatureBehavior
    {
        public abstract ArcRelaySignatureDefinition.SignatureKind Kind { get; }
        public virtual bool CooldownStartsAtActivation => false;
        public virtual bool DeclaresLineLock => false;
        public virtual bool RootsTellOnMove => false;

        public Operation Create(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start) => start.DeclaresLock
                ? runtime.CreateLockedOperation(start)
                : CreateUnlocked(runtime, start);

        protected abstract Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start);

        public virtual void OnStarted(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
        }

        public virtual void AdvanceAtTickStart(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyDictionary<ActorIdentity, Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
        }

        public virtual void ResolvePostMovement(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyCollection<Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
        }
    }

    private Operation CreateLockedOperation(SignatureStartContext start)
    {
        Position aim = start.TargetPosition
            ?? Ray(
                start.Source,
                start.Heading!.Value,
                LineAttackRange(start.Definition))[^1];
        ImmutableArray<Position> line = GenericActorStrikeCone.LineTo(
            _map,
            start.Source,
            aim,
            _diagonalCornersMustBeClear);
        Operation operation = NewOperation(
            start,
            ArcRelaySignatureState.SignaturePhase.Tell,
            checked(start.Tick + LineAttackWindup(start.Definition)),
            endsAtTick: null,
            line.IsEmpty ? [start.Source] : line,
            remainingCapacity: 0);
        operation.DeclaresLock = true;
        operation.ConeTiles = start.ConeTiles;
        return operation;
    }

    private static Operation NewOperation(
        SignatureStartContext start,
        ArcRelaySignatureState.SignaturePhase phase,
        int? completesAtTick,
        int? endsAtTick,
        ImmutableArray<Position> positions,
        int remainingCapacity) =>
        new(
            start.OperationId,
            start.Definition,
            start.Actor,
            start.Source,
            start.TargetActor,
            start.TargetPosition,
            start.Heading,
            phase,
            start.Tick,
            completesAtTick,
            endsAtTick,
            positions,
            remainingCapacity);

    private sealed class VectorDashBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.VectorDash;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.VectorDash)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Tell,
                checked(start.Tick + value.TellTicks),
                endsAtTick: null,
                runtime.Ray(start.Source, start.Heading!.Value, value.MaxTiles),
                remainingCapacity: 0);
        }

        public override void AdvanceAtTickStart(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyDictionary<ActorIdentity, Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            effects.Add(new Effect.VectorDash(
                operation.OperationId,
                operation.OwnerActorId,
                operation.Heading!.Value));
            runtime.Complete(operation, tick, "completed", events);
        }
    }

    private sealed class PrismWallBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.PrismWall;
        public override bool CooldownStartsAtActivation => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.PrismWall)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Active,
                completesAtTick: null,
                checked(start.Tick + value.DurationTicks),
                runtime.WallShape(
                    start.Source,
                    start.Direction!.Value,
                    value.SegmentCount),
                value.ContactCapacity);
        }
    }

    private sealed class TractorHookBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.TractorHook;
        public override bool CooldownStartsAtActivation => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.TractorHook)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Active,
                completesAtTick: null,
                endsAtTick: null,
                runtime.Ray(start.Source, start.Heading!.Value, value.Range),
                remainingCapacity: 0);
        }

        public override void OnStarted(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            effects.Add(new Effect.TractorHook(
                operation.OperationId,
                operation.OwnerActorId,
                operation.Heading!.Value));
            runtime.Complete(operation, tick, "completed", events);
        }
    }

    private sealed class RepairBeamBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.RepairBeam;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start) => NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Channel,
                completesAtTick: null,
                endsAtTick: null,
                Endpoints(start.Source, start.TargetPosition!.Value),
                remainingCapacity: 0);
    }

    private sealed class SurveyFlareBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.SurveyFlare;
        public override bool CooldownStartsAtActivation => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.SurveyFlare)
                start.Definition;
            int travelTicks = Math.Max(
                1,
                (start.Source.ChebyshevDistance(start.TargetPosition!.Value)
                 + value.TravelTilesPerTick - 1)
                / value.TravelTilesPerTick);
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.InFlight,
                checked(start.Tick + travelTicks),
                endsAtTick: null,
                Endpoints(start.Source, start.TargetPosition.Value),
                remainingCapacity: 0);
        }

        public override void AdvanceAtTickStart(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyDictionary<ActorIdentity, Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            var flare = (ArcRelaySignatureDefinition.SurveyFlare)
                operation.Definition;
            operation.Phase = ArcRelaySignatureState.SignaturePhase.Active;
            operation.CompletesAtTick = null;
            operation.EndsAtTick = checked(tick + flare.DurationTicks);
            operation.Positions = runtime.Radius(
                operation.TargetPosition!.Value,
                flare.RevealRadius);
            events.Add(runtime.SignatureEvent(operation, "landed"));
        }
    }

    private sealed class FallingStarBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.FallingStar;
        public override bool CooldownStartsAtActivation => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.FallingStar)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Tell,
                checked(start.Tick + value.TellTicks),
                endsAtTick: null,
                Cross(start.TargetPosition!.Value),
                remainingCapacity: 0);
        }

        public override void AdvanceAtTickStart(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyDictionary<ActorIdentity, Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            effects.Add(new Effect.FallingStar(
                operation.OperationId,
                operation.OwnerActorId,
                operation.TargetPosition!.Value));
            runtime.Complete(operation, tick, "completed", events);
        }
    }

    private sealed class TripNodeBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.TripNode;
        public override bool CooldownStartsAtActivation => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.TripNode)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Active,
                completesAtTick: null,
                endsAtTick: null,
                [start.TargetPosition!.Value],
                value.Hull);
        }

        public override void ResolvePostMovement(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyCollection<Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            if (operation.Suppressed)
                return;
            Life? target = lives.Where(value =>
                    value.ActorId.TeamId != operation.OwnerActorId.TeamId
                    && value.Position == operation.Positions[0])
                .OrderBy(value => value.ActorId)
                .FirstOrDefault();
            if (target is null)
                return;
            int damage = ((ArcRelaySignatureDefinition.TripNode)
                operation.Definition).TriggerDamage;
            effects.Add(new Effect.TripNode(
                operation.OperationId,
                operation.OwnerActorId,
                target.ActorId,
                damage));
            runtime.Remove(operation, "triggered", events);
        }
    }

    private sealed class NullFieldBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.NullField;
        public override bool CooldownStartsAtActivation => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.NullField)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Active,
                completesAtTick: null,
                checked(start.Tick + value.DurationTicks),
                runtime.Radius(start.Source, value.Radius),
                remainingCapacity: 0);
        }
    }

    private sealed class ArcTossBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.ArcToss;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.ArcToss)start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Tell,
                checked(start.Tick + value.TellTicks),
                endsAtTick: null,
                Endpoints(start.Source, start.TargetPosition!.Value),
                remainingCapacity: 0);
        }

        public override void AdvanceAtTickStart(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyDictionary<ActorIdentity, Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            var toss = (ArcRelaySignatureDefinition.ArcToss)
                operation.Definition;
            if (operation.Phase == ArcRelaySignatureState.SignaturePhase.Tell)
            {
                operation.Phase = ArcRelaySignatureState.SignaturePhase.InFlight;
                operation.CompletesAtTick = checked(
                    tick + Math.Max(
                        1,
                        (operation.SourcePosition.ChebyshevDistance(
                            operation.TargetPosition!.Value)
                         + toss.TravelTilesPerTick - 1)
                        / toss.TravelTilesPerTick));
                effects.Add(new Effect.ArcTossLaunch(
                    operation.OperationId,
                    operation.OwnerActorId,
                    operation.TargetPosition.Value,
                    operation.CompletesAtTick.Value));
                runtime._readyAtTick[operation.OwnerActorId] = checked(
                    tick + toss.CooldownTicks);
                events.Add(runtime.SignatureEvent(operation, "launched"));
                return;
            }
            effects.Add(new Effect.ArcTossLand(
                operation.OperationId,
                operation.OwnerActorId,
                operation.TargetPosition!.Value));
            runtime.Complete(operation, tick, "landed", events);
        }
    }

    private sealed class ExchangeBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.Exchange;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.Exchange)start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Tell,
                checked(start.Tick + value.TellTicks),
                endsAtTick: null,
                Endpoints(start.Source, start.TargetPosition!.Value),
                remainingCapacity: 0);
        }

        public override void AdvanceAtTickStart(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyDictionary<ActorIdentity, Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            effects.Add(new Effect.Exchange(
                operation.OperationId,
                operation.OwnerActorId,
                operation.TargetActorId!,
                operation.SourcePosition,
                operation.TargetPosition!.Value));
            runtime.Complete(operation, tick, "completed", events);
        }
    }

    private sealed class RailLineBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.RailLine;
        public override bool DeclaresLineLock => true;
        public override bool RootsTellOnMove => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.RailLine)start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Tell,
                checked(start.Tick + value.WindupTicks),
                endsAtTick: null,
                runtime.Ray(start.Source, start.Heading!.Value, value.Range),
                remainingCapacity: 0);
        }

        public override void AdvanceAtTickStart(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyDictionary<ActorIdentity, Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            effects.Add(new Effect.RailLine(
                operation.OperationId,
                operation.OwnerActorId,
                operation.Heading!.Value,
                []));
            runtime.Complete(operation, tick, "completed", events);
        }
    }

    private sealed class HardlightBlockBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.HardlightBlock;
        public override bool CooldownStartsAtActivation => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.HardlightBlock)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Active,
                completesAtTick: null,
                checked(start.Tick + value.DurationTicks),
                [start.TargetPosition!.Value],
                value.Hull);
        }
    }

    private sealed class TargetPaintBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.TargetPaint;
        public override bool CooldownStartsAtActivation => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.TargetPaint)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Active,
                completesAtTick: null,
                checked(start.Tick + value.DurationTicks),
                [start.TargetPosition!.Value],
                value.EnhancedHitCount);
        }
    }

    private sealed class KineticBurstBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.KineticBurst;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.KineticBurst)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Tell,
                checked(start.Tick + value.TellTicks),
                endsAtTick: null,
                runtime.Radius(start.Source, 1),
                remainingCapacity: 0);
        }

        public override void AdvanceAtTickStart(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyDictionary<ActorIdentity, Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            effects.Add(new Effect.KineticBurst(
                operation.OperationId,
                operation.OwnerActorId,
                operation.SourcePosition));
            runtime.Complete(operation, tick, "completed", events);
        }
    }

    private sealed class SmokeCanisterBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.SmokeCanister;
        public override bool CooldownStartsAtActivation => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.SmokeCanister)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Active,
                completesAtTick: null,
                checked(start.Tick + value.DurationTicks),
                runtime.Radius(start.TargetPosition!.Value, value.Radius),
                remainingCapacity: 0);
        }
    }

    private sealed class SentinelSeedBehavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.SentinelSeed;
        public override bool CooldownStartsAtActivation => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.SentinelSeed)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Active,
                completesAtTick: null,
                checked(start.Tick + value.DurationTicks),
                [start.TargetPosition!.Value],
                value.Hull);
        }
    }

    private sealed class SentinelSeed2Behavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.SentinelSeed2;
        public override bool CooldownStartsAtActivation => true;
        public override bool RootsTellOnMove => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.SentinelSeed2)
                start.Definition;
            bool tells = value.WindupTicks > 0;
            return NewOperation(
                start,
                tells
                    ? ArcRelaySignatureState.SignaturePhase.Tell
                    : ArcRelaySignatureState.SignaturePhase.Active,
                tells ? checked(start.Tick + value.WindupTicks) : null,
                tells ? null : checked(start.Tick + value.DurationTicks),
                [start.TargetPosition!.Value],
                value.Hull);
        }

        public override void AdvanceAtTickStart(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyDictionary<ActorIdentity, Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            var seed = (ArcRelaySignatureDefinition.SentinelSeed2)
                operation.Definition;
            operation.Phase = ArcRelaySignatureState.SignaturePhase.Active;
            operation.CompletesAtTick = null;
            operation.EndsAtTick = checked(tick + seed.DurationTicks);
            events.Add(runtime.SignatureEvent(operation, "deployed"));
        }
    }

    private sealed class TractorHook2Behavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.TractorHook2;
        public override bool CooldownStartsAtActivation => true;
        public override bool DeclaresLineLock => true;
        public override bool RootsTellOnMove => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.TractorHook2)
                start.Definition;
            bool tells = value.WindupTicks > 0;
            return NewOperation(
                start,
                tells
                    ? ArcRelaySignatureState.SignaturePhase.Tell
                    : ArcRelaySignatureState.SignaturePhase.Active,
                tells ? checked(start.Tick + value.WindupTicks) : null,
                endsAtTick: null,
                runtime.Ray(start.Source, start.Heading!.Value, value.Range),
                remainingCapacity: 0);
        }

        public override void OnStarted(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            if (operation.Phase == ArcRelaySignatureState.SignaturePhase.Tell)
                return;
            AddBolt(operation, effects);
            runtime.Complete(operation, tick, "launched", events);
        }

        public override void AdvanceAtTickStart(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyDictionary<ActorIdentity, Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            AddBolt(operation, effects);
            runtime.Complete(operation, tick, "launched", events);
        }

        private static void AddBolt(
            Operation operation,
            ImmutableArray<Effect>.Builder effects)
        {
            var hook = (ArcRelaySignatureDefinition.TractorHook2)
                operation.Definition;
            effects.Add(new Effect.HookBolt(
                operation.OperationId,
                operation.OwnerActorId,
                operation.SourcePosition,
                operation.Heading!.Value,
                hook.Range,
                hook.MaxPullTiles,
                hook.BoltTilesPerAdvance));
        }
    }

    private sealed class NullField2Behavior : SignatureBehavior
    {
        public override ArcRelaySignatureDefinition.SignatureKind Kind =>
            ArcRelaySignatureDefinition.SignatureKind.NullField2;
        public override bool CooldownStartsAtActivation => true;

        protected override Operation CreateUnlocked(
            ArcRelaySignatureRuntime runtime,
            SignatureStartContext start)
        {
            var value = (ArcRelaySignatureDefinition.NullField2)
                start.Definition;
            return NewOperation(
                start,
                ArcRelaySignatureState.SignaturePhase.Tell,
                checked(start.Tick + value.TellTicks),
                endsAtTick: null,
                runtime.Radius(start.Source, value.Radius),
                remainingCapacity: 0);
        }

        public override void AdvanceAtTickStart(
            ArcRelaySignatureRuntime runtime,
            Operation operation,
            int tick,
            IReadOnlyDictionary<ActorIdentity, Life> lives,
            ImmutableArray<Effect>.Builder effects,
            ImmutableArray<GenericActorModeEvent>.Builder events)
        {
            var field = (ArcRelaySignatureDefinition.NullField2)
                operation.Definition;
            operation.Phase = ArcRelaySignatureState.SignaturePhase.Active;
            operation.CompletesAtTick = null;
            operation.EndsAtTick = checked(tick + field.DurationTicks);
            events.Add(runtime.SignatureEvent(operation, "activated"));
        }
    }
}
