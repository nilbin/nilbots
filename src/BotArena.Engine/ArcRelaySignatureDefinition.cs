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
        int cooldownTicks,
        ActivationTimingKind activationTiming =
            ActivationTimingKind.BeforeCombat)
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
        ActivationTiming = activationTiming;
    }

    public abstract SignatureKind Kind { get; }
    public string SignatureId { get; }
    public string ClassId { get; }
    public string ActionId { get; }
    public int CooldownTicks { get; }
    public ActivationTimingKind ActivationTiming { get; init; }

    /// <summary>
    /// The joint-tick phase in which an accepted signature action starts.
    /// Most signatures activate after movement and before attacks. A signature
    /// may instead let already-committed combat settle first; this is contract
    /// data, never a kind check hidden in the session.
    /// </summary>
    public enum ActivationTimingKind
    {
        BeforeCombat,
        AfterCombat,
    }

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
            int range, int tellTicks, int travelTilesPerTick, int cooldownTicks,
            ActivationTimingKind activationTiming =
                ActivationTimingKind.BeforeCombat)
            : base(
                signatureId,
                classId,
                actionId,
                cooldownTicks,
                activationTiming)
        {
            RequirePositive(range, nameof(range));
            RequireNonNegative(tellTicks, nameof(tellTicks));
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
            int windupTicks, int range, int damage, int cancelCooldownTicks,
            int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            // A beam that fires the instant it is cast has nowhere to put
            // its telegraph, so rail's windup is positive by construction.
            // Hook and sentinel CAN be instant and author zero for it.
            RequirePositive(windupTicks, nameof(windupTicks));
            RequirePositive(range, nameof(range));
            RequirePositive(damage, nameof(damage));
            RequirePositive(cancelCooldownTicks, nameof(cancelCooldownTicks));
            WindupTicks = windupTicks;
            Range = range;
            Damage = damage;
            CancelCooldownTicks = cancelCooldownTicks;
        }
        public override SignatureKind Kind => SignatureKind.RailLine;
        /// <summary>Ticks between the declare that freezes this line and the
        /// tick it fires. A ruleset balance lever (owner ruling 2026-08-08):
        /// zero authors an instant, untelegraphed cast.</summary>
        public int WindupTicks { get; }
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

    /// <summary>
    /// Grammar-2 sentinel (owner ruling 2026-08-05): the turret launches a
    /// real bolt along an eight-way ray — dodgeable off-axis, blockable by
    /// constructs — and fires faster to compensate. The grammar-1 record
    /// stays untouched so historical rules bytes never move.
    /// </summary>
    public sealed record SentinelSeed2 : ArcRelaySignatureDefinition
    {
        public SentinelSeed2(string signatureId, string classId,
            string actionId, int hull, int range, int damage,
            int fireCooldownTicks, int durationTicks,
            int boltTilesPerAdvance, int cooldownTicks,
            int windupTicks = 0)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequireNonNegative(windupTicks, nameof(windupTicks));
            WindupTicks = windupTicks;
            RequirePositive(hull, nameof(hull));
            RequirePositive(range, nameof(range));
            RequirePositive(damage, nameof(damage));
            RequirePositive(fireCooldownTicks, nameof(fireCooldownTicks));
            RequirePositive(durationTicks, nameof(durationTicks));
            RequirePositive(boltTilesPerAdvance, nameof(boltTilesPerAdvance));
            Hull = hull;
            Range = range;
            Damage = damage;
            FireCooldownTicks = fireCooldownTicks;
            DurationTicks = durationTicks;
            BoltTilesPerAdvance = boltTilesPerAdvance;
        }
        public override SignatureKind Kind => SignatureKind.SentinelSeed2;
        /// <summary>Telegraphed windup before the seed lands (owner ruling
        /// 2026-08-08); zero is the historical instant placement.</summary>
        public int WindupTicks { get; }
        public int Hull { get; }
        public int Range { get; }
        public int Damage { get; }
        public int FireCooldownTicks { get; }
        public int DurationTicks { get; }
        public int BoltTilesPerAdvance { get; }
    }

    /// <summary>
    /// Grammar-2 hook: a grapple bolt that travels its ray and pulls on
    /// contact — dodgeable, and a wall eats it.
    /// </summary>
    public sealed record TractorHook2 : ArcRelaySignatureDefinition
    {
        public TractorHook2(string signatureId, string classId,
            string actionId, int range, int maxPullTiles,
            int boltTilesPerAdvance, int cooldownTicks,
            int windupTicks = 0)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequireNonNegative(windupTicks, nameof(windupTicks));
            WindupTicks = windupTicks;
            RequirePositive(range, nameof(range));
            RequirePositive(maxPullTiles, nameof(maxPullTiles));
            RequirePositive(boltTilesPerAdvance, nameof(boltTilesPerAdvance));
            Range = range;
            MaxPullTiles = maxPullTiles;
            BoltTilesPerAdvance = boltTilesPerAdvance;
        }
        public override SignatureKind Kind => SignatureKind.TractorHook2;
        /// <summary>Telegraphed windup before the grapple bolt leaves (owner
        /// ruling 2026-08-08); zero is the historical instant cast.</summary>
        public int WindupTicks { get; }
        public int Range { get; }
        public int MaxPullTiles { get; }
        public int BoltTilesPerAdvance { get; }
    }

    /// <summary>
    /// Grammar-2 null-field: one tick of visible charge before the
    /// suppression lands, so proximity to a Hush can be answered.
    /// </summary>
    public sealed record NullField2 : ArcRelaySignatureDefinition
    {
        public NullField2(string signatureId, string classId, string actionId,
            int radius, int durationTicks, int tellTicks, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(radius, nameof(radius));
            RequirePositive(durationTicks, nameof(durationTicks));
            RequirePositive(tellTicks, nameof(tellTicks));
            Radius = radius;
            DurationTicks = durationTicks;
            TellTicks = tellTicks;
        }
        public override SignatureKind Kind => SignatureKind.NullField2;
        public int Radius { get; }
        public int DurationTicks { get; }
        public int TellTicks { get; }
    }

    /// <summary>
    /// Grammar-3 null field. Its footprint is still frozen at declaration,
    /// but maturity belongs to the post-movement signature phase so the
    /// published tell is an actual escape window.
    /// </summary>
    public sealed record NullField3 : ArcRelaySignatureDefinition
    {
        public NullField3(string signatureId, string classId, string actionId,
            int radius, int durationTicks, int tellTicks, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(radius, nameof(radius));
            RequirePositive(durationTicks, nameof(durationTicks));
            RequirePositive(tellTicks, nameof(tellTicks));
            Radius = radius;
            DurationTicks = durationTicks;
            TellTicks = tellTicks;
        }
        public override SignatureKind Kind => SignatureKind.NullField3;
        public int Radius { get; }
        public int DurationTicks { get; }
        public int TellTicks { get; }
    }

    /// <summary>
    /// Grammar-3 target paint. The target is acquired after response movement
    /// and a voluntary move abandons the acquisition for its authored partial
    /// cooldown.
    /// </summary>
    public sealed record TargetPaint2 : ArcRelaySignatureDefinition
    {
        public TargetPaint2(string signatureId, string classId,
            string actionId, int range, int acquisitionTicks,
            int durationTicks, int enhancedHitCount, int bonusDamage,
            int cancelCooldownTicks, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(range, nameof(range));
            RequirePositive(acquisitionTicks, nameof(acquisitionTicks));
            RequirePositive(durationTicks, nameof(durationTicks));
            RequirePositive(enhancedHitCount, nameof(enhancedHitCount));
            RequirePositive(bonusDamage, nameof(bonusDamage));
            RequirePositive(cancelCooldownTicks, nameof(cancelCooldownTicks));
            if (cancelCooldownTicks >= cooldownTicks)
                throw new ArgumentOutOfRangeException(nameof(cancelCooldownTicks));
            Range = range;
            AcquisitionTicks = acquisitionTicks;
            DurationTicks = durationTicks;
            EnhancedHitCount = enhancedHitCount;
            BonusDamage = bonusDamage;
            CancelCooldownTicks = cancelCooldownTicks;
        }
        public override SignatureKind Kind => SignatureKind.TargetPaint2;
        public int Range { get; }
        public int AcquisitionTicks { get; }
        public int DurationTicks { get; }
        public int EnhancedHitCount { get; }
        public int BonusDamage { get; }
        public int CancelCooldownTicks { get; }
    }

    /// <summary>
    /// Grammar-3 kinetic burst. The frozen origin resolves after response
    /// movement; leaving the charge spends an authored partial cooldown.
    /// </summary>
    public sealed record KineticBurst2 : ArcRelaySignatureDefinition
    {
        public KineticBurst2(string signatureId, string classId,
            string actionId, int tellTicks, int pushTiles,
            int cancelCooldownTicks, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(tellTicks, nameof(tellTicks));
            RequirePositive(pushTiles, nameof(pushTiles));
            RequirePositive(cancelCooldownTicks, nameof(cancelCooldownTicks));
            if (cancelCooldownTicks >= cooldownTicks)
                throw new ArgumentOutOfRangeException(nameof(cancelCooldownTicks));
            TellTicks = tellTicks;
            PushTiles = pushTiles;
            CancelCooldownTicks = cancelCooldownTicks;
        }
        public override SignatureKind Kind => SignatureKind.KineticBurst2;
        public int TellTicks { get; }
        public int PushTiles { get; }
        public int CancelCooldownTicks { get; }
    }

    /// <summary>
    /// Grammar-3 smoke canister. The selected tile receives a public
    /// ballistic flight before the full-duration smoke field begins.
    /// </summary>
    public sealed record SmokeCanister2 : ArcRelaySignatureDefinition
    {
        public SmokeCanister2(string signatureId, string classId,
            string actionId, int range, int radius, int durationTicks,
            int travelTilesPerTick, int cooldownTicks)
            : base(signatureId, classId, actionId, cooldownTicks)
        {
            RequirePositive(range, nameof(range));
            RequirePositive(radius, nameof(radius));
            RequirePositive(durationTicks, nameof(durationTicks));
            RequirePositive(travelTilesPerTick, nameof(travelTilesPerTick));
            Range = range;
            Radius = radius;
            DurationTicks = durationTicks;
            TravelTilesPerTick = travelTilesPerTick;
        }
        public override SignatureKind Kind => SignatureKind.SmokeCanister2;
        public int Range { get; }
        public int Radius { get; }
        public int DurationTicks { get; }
        public int TravelTilesPerTick { get; }
    }

    private static void RequirePositive(int value, string parameterName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    /// <summary>Windup lengths are non-negative: zero is the authored way to
    /// say "instant, no telegraph".</summary>
    private static void RequireNonNegative(int value, string parameterName)
    {
        if (value < 0)
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
        SentinelSeed2 = 16,
        TractorHook2 = 17,
        NullField2 = 18,
        NullField3 = 19,
        TargetPaint2 = 20,
        KineticBurst2 = 21,
        SmokeCanister2 = 22,
    }

    /// <summary>
    /// Returns the stable player-facing signature ID for a concrete mechanics
    /// kind. Definition-version variants deliberately collapse to the same ID:
    /// the grammar/version selects chronology, while this ID selects the
    /// signature's public meaning and presentation registration.
    /// </summary>
    public static string StableIdFor(SignatureKind kind) => kind switch
    {
        SignatureKind.VectorDash => "vector-dash",
        SignatureKind.PrismWall => "prism-wall",
        SignatureKind.TractorHook or SignatureKind.TractorHook2 =>
            "tractor-hook",
        SignatureKind.RepairBeam => "repair-beam",
        SignatureKind.SurveyFlare => "survey-flare",
        SignatureKind.FallingStar => "falling-star",
        SignatureKind.TripNode => "trip-node",
        SignatureKind.NullField or SignatureKind.NullField2
            or SignatureKind.NullField3 => "null-field",
        SignatureKind.ArcToss => "arc-toss",
        SignatureKind.Exchange => "exchange",
        SignatureKind.RailLine => "rail-line",
        SignatureKind.HardlightBlock => "hardlight-block",
        SignatureKind.TargetPaint or SignatureKind.TargetPaint2 =>
            "target-paint",
        SignatureKind.KineticBurst or SignatureKind.KineticBurst2 =>
            "kinetic-burst",
        SignatureKind.SmokeCanister or SignatureKind.SmokeCanister2 =>
            "smoke-canister",
        SignatureKind.SentinelSeed or SignatureKind.SentinelSeed2 =>
            "sentinel-seed",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// Designed-role metadata, projected into grammar-2 and later contracts so
    /// executors can dispatch generically (category order, argument shape,
    /// sensible engagement distance) instead of hand-maintaining tables.
    /// Grammar-1 contracts never carry it, so their bytes never move.
    /// </summary>
    public readonly record struct SignatureMetadata(
        string Category,
        string ArgumentKind,
        int EngagementRange);

    /// <summary>
    /// Grammar-3 lifecycle metadata shared by every signature. These stable
    /// semantic IDs tell clients what the initial public operation means,
    /// which authoritative chronology boundary may mature it, and where its
    /// full cooldown is anchored. Type-specific numeric fields remain the
    /// source of duration, travel, capacity, and cancellation values.
    /// </summary>
    public readonly record struct SignatureTimingMetadata(
        string InitialPhase,
        string MaturityPhase,
        string CooldownAnchor);

    public static SignatureMetadata MetadataFor(SignatureKind kind) =>
        kind switch
        {
            SignatureKind.RailLine =>
                new SignatureMetadata("damage", "heading", 12),
            SignatureKind.FallingStar =>
                new SignatureMetadata("damage", "position", 8),
            SignatureKind.SentinelSeed or SignatureKind.SentinelSeed2 =>
                new SignatureMetadata("damage", "position", 6),
            SignatureKind.KineticBurst or SignatureKind.KineticBurst2 =>
                new SignatureMetadata("damage", "parameterless", 1),
            SignatureKind.TargetPaint or SignatureKind.TargetPaint2 =>
                new SignatureMetadata("control", "unit", 7),
            SignatureKind.TractorHook or SignatureKind.TractorHook2 =>
                new SignatureMetadata("control", "heading", 6),
            SignatureKind.NullField or SignatureKind.NullField2
                or SignatureKind.NullField3 =>
                new SignatureMetadata("control", "parameterless", 3),
            SignatureKind.TripNode =>
                new SignatureMetadata("control", "position", 4),
            SignatureKind.HardlightBlock =>
                new SignatureMetadata("control", "position", 12),
            SignatureKind.PrismWall =>
                new SignatureMetadata("support", "direction", 6),
            SignatureKind.SmokeCanister or SignatureKind.SmokeCanister2 =>
                new SignatureMetadata("support", "position", 6),
            SignatureKind.SurveyFlare =>
                new SignatureMetadata("support", "position", 8),
            SignatureKind.Exchange =>
                new SignatureMetadata("support", "unit", 6),
            SignatureKind.RepairBeam =>
                new SignatureMetadata("support", "unit", 4),
            SignatureKind.VectorDash =>
                new SignatureMetadata("movement", "heading", 4),
            SignatureKind.ArcToss =>
                new SignatureMetadata("custody", "position", 5),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    public static SignatureTimingMetadata TimingFor(SignatureKind kind) =>
        kind switch
        {
            SignatureKind.VectorDash =>
                new("tell", "tick-start", "completion"),
            SignatureKind.PrismWall =>
                new("active", "immediate", "start"),
            SignatureKind.TractorHook =>
                new("active", "immediate", "start"),
            SignatureKind.RepairBeam =>
                new("channel", "maintained", "completion"),
            SignatureKind.SurveyFlare =>
                new("in-flight", "tick-start", "start"),
            SignatureKind.FallingStar =>
                new("tell", "tick-start", "completion"),
            SignatureKind.TripNode =>
                new("active", "post-movement-contact", "start"),
            SignatureKind.NullField =>
                new("active", "immediate", "start"),
            SignatureKind.ArcToss =>
                new("tell", "tick-start", "launch"),
            SignatureKind.Exchange =>
                new("tell", "tick-start", "completion"),
            SignatureKind.RailLine =>
                new("tell", "post-movement", "completion"),
            SignatureKind.HardlightBlock =>
                new("active", "immediate", "start"),
            SignatureKind.TargetPaint =>
                new("active", "immediate", "start"),
            SignatureKind.KineticBurst =>
                new("tell", "tick-start", "completion"),
            SignatureKind.SmokeCanister =>
                new("active", "immediate", "start"),
            SignatureKind.SentinelSeed =>
                new("active", "immediate", "start"),
            SignatureKind.SentinelSeed2 =>
                new("tell", "tick-start", "start"),
            SignatureKind.TractorHook2 =>
                new("tell", "post-movement", "start"),
            SignatureKind.NullField2 =>
                new("tell", "tick-start", "start"),
            SignatureKind.NullField3 =>
                new("tell", "post-movement", "start"),
            SignatureKind.TargetPaint2 =>
                new("tell", "post-movement", "start"),
            SignatureKind.KineticBurst2 =>
                new("tell", "post-movement", "completion"),
            SignatureKind.SmokeCanister2 =>
                new("in-flight", "tick-start", "start"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    public static SignatureTimingMetadata TimingFor(
        ArcRelaySignatureDefinition definition) => definition switch
        {
            ArcToss { TellTicks: 0 } =>
                new("in-flight", "post-combat", "launch"),
            _ => TimingFor(definition.Kind),
        };
}
