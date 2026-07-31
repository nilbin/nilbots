namespace BotArena.Engine;

/// <summary>
/// Closed immutable catalog of Arc Relay's one-signature-per-class launch
/// envelopes. Every number that the runtime consumes is contract data.
/// </summary>
public abstract record ArcRelaySignatureDefinition
{
    private protected ArcRelaySignatureDefinition(
        string signatureId,
        string classId,
        string actionId,
        int cooldownTicks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(classId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (cooldownTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(cooldownTicks));

        SignatureId = signatureId;
        ClassId = classId;
        ActionId = actionId;
        CooldownTicks = cooldownTicks;
    }

    public abstract SignatureKind Kind { get; }
    public string SignatureId { get; }
    public string ClassId { get; }
    public string ActionId { get; }
    public int CooldownTicks { get; }

    public sealed record VectorDash : ArcRelaySignatureDefinition
    {
        public VectorDash(string signatureId, string classId, string actionId,
            int tellTicks, int maxTiles, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(tellTicks, nameof(tellTicks));
            RequirePositive(maxTiles, nameof(maxTiles));
            TellTicks = tellTicks;
            MaxTiles = maxTiles;
        }
        public override SignatureKind Kind => SignatureKind.VectorDash;
        public int TellTicks { get; }
        public int MaxTiles { get; }
    }

    public sealed record PrismWall : ArcRelaySignatureDefinition
    {
        public PrismWall(string signatureId, string classId, string actionId,
            int segmentCount, int durationTicks, int contactCapacity,
            int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(segmentCount, nameof(segmentCount));
            RequirePositive(durationTicks, nameof(durationTicks));
            RequirePositive(contactCapacity, nameof(contactCapacity));
            SegmentCount = segmentCount;
            DurationTicks = durationTicks;
            ContactCapacity = contactCapacity;
        }
        public override SignatureKind Kind => SignatureKind.PrismWall;
        public int SegmentCount { get; }
        public int DurationTicks { get; }
        public int ContactCapacity { get; }
    }

    public sealed record TractorHook : ArcRelaySignatureDefinition
    {
        public TractorHook(string signatureId, string classId, string actionId,
            int range, int maxPullTiles, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(range, nameof(range));
            RequirePositive(maxPullTiles, nameof(maxPullTiles));
            Range = range;
            MaxPullTiles = maxPullTiles;
        }
        public override SignatureKind Kind => SignatureKind.TractorHook;
        public int Range { get; }
        public int MaxPullTiles { get; }
    }

    public sealed record RepairBeam : ArcRelaySignatureDefinition
    {
        public RepairBeam(string signatureId, string classId, string actionId,
            int range, int ticksPerRepair, int hullPerRepair,
            int maxHullPerActivation, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(range, nameof(range));
            RequirePositive(ticksPerRepair, nameof(ticksPerRepair));
            RequirePositive(hullPerRepair, nameof(hullPerRepair));
            RequirePositive(maxHullPerActivation, nameof(maxHullPerActivation));
            Range = range;
            TicksPerRepair = ticksPerRepair;
            HullPerRepair = hullPerRepair;
            MaxHullPerActivation = maxHullPerActivation;
        }
        public override SignatureKind Kind => SignatureKind.RepairBeam;
        public int Range { get; }
        public int TicksPerRepair { get; }
        public int HullPerRepair { get; }
        public int MaxHullPerActivation { get; }
    }

    public sealed record SurveyFlare : ArcRelaySignatureDefinition
    {
        public SurveyFlare(string signatureId, string classId, string actionId,
            int range, int travelTilesPerTick, int revealRadius,
            int durationTicks, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(range, nameof(range));
            RequirePositive(travelTilesPerTick, nameof(travelTilesPerTick));
            RequirePositive(revealRadius, nameof(revealRadius));
            RequirePositive(durationTicks, nameof(durationTicks));
            Range = range;
            TravelTilesPerTick = travelTilesPerTick;
            RevealRadius = revealRadius;
            DurationTicks = durationTicks;
        }
        public override SignatureKind Kind => SignatureKind.SurveyFlare;
        public int Range { get; }
        public int TravelTilesPerTick { get; }
        public int RevealRadius { get; }
        public int DurationTicks { get; }
    }

    public sealed record FallingStar : ArcRelaySignatureDefinition
    {
        public FallingStar(string signatureId, string classId, string actionId,
            int range, int tellTicks, int damage, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(range, nameof(range));
            RequirePositive(tellTicks, nameof(tellTicks));
            RequirePositive(damage, nameof(damage));
            Range = range;
            TellTicks = tellTicks;
            Damage = damage;
        }
        public override SignatureKind Kind => SignatureKind.FallingStar;
        public int Range { get; }
        public int TellTicks { get; }
        public int Damage { get; }
    }

    public sealed record TripNode : ArcRelaySignatureDefinition
    {
        public TripNode(string signatureId, string classId, string actionId,
            int hull, int triggerDamage, int revealRange, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(hull, nameof(hull));
            RequirePositive(triggerDamage, nameof(triggerDamage));
            RequirePositive(revealRange, nameof(revealRange));
            Hull = hull;
            TriggerDamage = triggerDamage;
            RevealRange = revealRange;
        }
        public override SignatureKind Kind => SignatureKind.TripNode;
        public int Hull { get; }
        public int TriggerDamage { get; }
        public int RevealRange { get; }
    }

    public sealed record NullField : ArcRelaySignatureDefinition
    {
        public NullField(string signatureId, string classId, string actionId,
            int radius, int durationTicks, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(radius, nameof(radius));
            RequirePositive(durationTicks, nameof(durationTicks));
            Radius = radius;
            DurationTicks = durationTicks;
        }
        public override SignatureKind Kind => SignatureKind.NullField;
        public int Radius { get; }
        public int DurationTicks { get; }
    }

    public sealed record ArcToss : ArcRelaySignatureDefinition
    {
        public ArcToss(string signatureId, string classId, string actionId,
            int range, int tellTicks, int travelTilesPerTick, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(range, nameof(range));
            RequirePositive(tellTicks, nameof(tellTicks));
            RequirePositive(travelTilesPerTick, nameof(travelTilesPerTick));
            Range = range;
            TellTicks = tellTicks;
            TravelTilesPerTick = travelTilesPerTick;
        }
        public override SignatureKind Kind => SignatureKind.ArcToss;
        public int Range { get; }
        public int TellTicks { get; }
        public int TravelTilesPerTick { get; }
    }

    public sealed record Exchange : ArcRelaySignatureDefinition
    {
        public Exchange(string signatureId, string classId, string actionId,
            int range, int tellTicks, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(range, nameof(range));
            RequirePositive(tellTicks, nameof(tellTicks));
            Range = range;
            TellTicks = tellTicks;
        }
        public override SignatureKind Kind => SignatureKind.Exchange;
        public int Range { get; }
        public int TellTicks { get; }
    }

    public sealed record RailLine : ArcRelaySignatureDefinition
    {
        public RailLine(string signatureId, string classId, string actionId,
            int tellTicks, int range, int damage, int cancelCooldownTicks,
            int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(tellTicks, nameof(tellTicks));
            RequirePositive(range, nameof(range));
            RequirePositive(damage, nameof(damage));
            RequirePositive(cancelCooldownTicks, nameof(cancelCooldownTicks));
            TellTicks = tellTicks;
            Range = range;
            Damage = damage;
            CancelCooldownTicks = cancelCooldownTicks;
        }
        public override SignatureKind Kind => SignatureKind.RailLine;
        public int TellTicks { get; }
        public int Range { get; }
        public int Damage { get; }
        public int CancelCooldownTicks { get; }
    }

    public sealed record HardlightBlock : ArcRelaySignatureDefinition
    {
        public HardlightBlock(string signatureId, string classId,
            string actionId, int hull, int durationTicks, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(hull, nameof(hull));
            RequirePositive(durationTicks, nameof(durationTicks));
            Hull = hull;
            DurationTicks = durationTicks;
        }
        public override SignatureKind Kind => SignatureKind.HardlightBlock;
        public int Hull { get; }
        public int DurationTicks { get; }
    }

    public sealed record TargetPaint : ArcRelaySignatureDefinition
    {
        public TargetPaint(string signatureId, string classId, string actionId,
            int range, int durationTicks, int enhancedHitCount,
            int bonusDamage, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(range, nameof(range));
            RequirePositive(durationTicks, nameof(durationTicks));
            RequirePositive(enhancedHitCount, nameof(enhancedHitCount));
            RequirePositive(bonusDamage, nameof(bonusDamage));
            Range = range;
            DurationTicks = durationTicks;
            EnhancedHitCount = enhancedHitCount;
            BonusDamage = bonusDamage;
        }
        public override SignatureKind Kind => SignatureKind.TargetPaint;
        public int Range { get; }
        public int DurationTicks { get; }
        public int EnhancedHitCount { get; }
        public int BonusDamage { get; }
    }

    public sealed record KineticBurst : ArcRelaySignatureDefinition
    {
        public KineticBurst(string signatureId, string classId, string actionId,
            int tellTicks, int pushTiles, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(tellTicks, nameof(tellTicks));
            RequirePositive(pushTiles, nameof(pushTiles));
            TellTicks = tellTicks;
            PushTiles = pushTiles;
        }
        public override SignatureKind Kind => SignatureKind.KineticBurst;
        public int TellTicks { get; }
        public int PushTiles { get; }
    }

    public sealed record SmokeCanister : ArcRelaySignatureDefinition
    {
        public SmokeCanister(string signatureId, string classId,
            string actionId, int range, int radius, int durationTicks,
            int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(range, nameof(range));
            RequirePositive(radius, nameof(radius));
            RequirePositive(durationTicks, nameof(durationTicks));
            Range = range;
            Radius = radius;
            DurationTicks = durationTicks;
        }
        public override SignatureKind Kind => SignatureKind.SmokeCanister;
        public int Range { get; }
        public int Radius { get; }
        public int DurationTicks { get; }
    }

    public sealed record SentinelSeed : ArcRelaySignatureDefinition
    {
        public SentinelSeed(string signatureId, string classId,
            string actionId, int hull, int range, int damage,
            int fireCooldownTicks, int durationTicks, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(hull, nameof(hull));
            RequirePositive(range, nameof(range));
            RequirePositive(damage, nameof(damage));
            RequirePositive(fireCooldownTicks, nameof(fireCooldownTicks));
            RequirePositive(durationTicks, nameof(durationTicks));
            Hull = hull;
            Range = range;
            Damage = damage;
            FireCooldownTicks = fireCooldownTicks;
            DurationTicks = durationTicks;
        }
        public override SignatureKind Kind => SignatureKind.SentinelSeed;
        public int Hull { get; }
        public int Range { get; }
        public int Damage { get; }
        public int FireCooldownTicks { get; }
        public int DurationTicks { get; }
    }

    private static void RequirePositive(int value, string parameterName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    public enum SignatureKind
    {
        VectorDash = 0,
        PrismWall = 1,
        TractorHook = 2,
        RepairBeam = 3,
        SurveyFlare = 4,
        FallingStar = 5,
        TripNode = 6,
        NullField = 7,
        ArcToss = 8,
        Exchange = 9,
        RailLine = 10,
        HardlightBlock = 11,
        TargetPaint = 12,
        KineticBurst = 13,
        SmokeCanister = 14,
        SentinelSeed = 15,
    }
}
