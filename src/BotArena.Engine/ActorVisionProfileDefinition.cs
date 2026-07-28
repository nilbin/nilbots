using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// A named, explicit sight and redacted-hearing profile selected by forms.
/// Hearing distance bands are an ordered partition; loud event kinds are a
/// canonical set.
/// </summary>
public sealed record ActorVisionProfileDefinition
{
    public ActorVisionProfileDefinition(
        string id,
        int range,
        ActorVisionDistanceMetric distanceMetric,
        ActorVisionShape shape,
        int omnidirectionalProximityRange,
        ActorLineOfSightModel lineOfSight,
        int hearingRadius,
        int hearingBearingSectors,
        ActorHearingBearingModel hearingBearingModel,
        IEnumerable<int> hearingDistanceBandUpperBounds,
        IEnumerable<ActorAudibleEventKind> loudEventKinds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(hearingDistanceBandUpperBounds);
        ArgumentNullException.ThrowIfNull(loudEventKinds);
        if (range < 0)
            throw new ArgumentOutOfRangeException(nameof(range));
        if (!Enum.IsDefined(distanceMetric))
            throw new ArgumentOutOfRangeException(nameof(distanceMetric));
        if (!Enum.IsDefined(shape))
            throw new ArgumentOutOfRangeException(nameof(shape));
        if (omnidirectionalProximityRange < 0
            || omnidirectionalProximityRange > range)
        {
            throw new ArgumentOutOfRangeException(
                nameof(omnidirectionalProximityRange));
        }
        if (!Enum.IsDefined(lineOfSight))
            throw new ArgumentOutOfRangeException(nameof(lineOfSight));
        if (hearingRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(hearingRadius));
        if (hearingBearingSectors < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hearingBearingSectors));
        }
        if (!Enum.IsDefined(hearingBearingModel))
            throw new ArgumentOutOfRangeException(nameof(hearingBearingModel));

        ImmutableArray<int> bands = hearingDistanceBandUpperBounds
            .Select(value => value)
            .ToImmutableArray();
        ImmutableArray<ActorAudibleEventKind> loudKinds = loudEventKinds
            .Select(value => value)
            .ToImmutableArray();
        ValidateHearing(
            hearingRadius,
            hearingBearingSectors,
            hearingBearingModel,
            bands,
            loudKinds);

        Id = id;
        Range = range;
        DistanceMetric = distanceMetric;
        Shape = shape;
        OmnidirectionalProximityRange = omnidirectionalProximityRange;
        LineOfSight = lineOfSight;
        HearingRadius = hearingRadius;
        HearingBearingSectors = hearingBearingSectors;
        HearingBearingModel = hearingBearingModel;
        HearingDistanceBandUpperBounds = bands;
        LoudEventKinds = loudKinds
            .OrderBy(kind => kind)
            .ToImmutableArray();
    }

    public string Id { get; }
    public int Range { get; }
    public ActorVisionDistanceMetric DistanceMetric { get; }
    public ActorVisionShape Shape { get; }
    public int OmnidirectionalProximityRange { get; }
    public ActorLineOfSightModel LineOfSight { get; }
    public int HearingRadius { get; }
    public int HearingBearingSectors { get; }
    public ActorHearingBearingModel HearingBearingModel { get; }
    public ImmutableArray<int> HearingDistanceBandUpperBounds { get; }
    public ImmutableArray<ActorAudibleEventKind> LoudEventKinds { get; }
    public HearingDistanceBandModelKind HearingDistanceBandModel =>
        HearingDistanceBandModelKind
            .ChebyshevInclusiveOrderedUpperBoundsThenFinalRadiusBand;

    private static void ValidateHearing(
        int hearingRadius,
        int hearingBearingSectors,
        ActorHearingBearingModel hearingBearingModel,
        ImmutableArray<int> bands,
        ImmutableArray<ActorAudibleEventKind> loudKinds)
    {
        var seenKinds = new HashSet<ActorAudibleEventKind>();
        foreach (ActorAudibleEventKind kind in loudKinds)
        {
            if (!Enum.IsDefined(kind))
                throw new ArgumentOutOfRangeException(nameof(loudKinds));
            if (!seenKinds.Add(kind))
            {
                throw new ArgumentException(
                    "Loud event kinds cannot contain duplicates.",
                    nameof(loudKinds));
            }
        }

        if (hearingRadius == 0)
        {
            if (hearingBearingSectors != 0
                || hearingBearingModel != ActorHearingBearingModel.Disabled
                || !bands.IsEmpty
                || !loudKinds.IsEmpty)
            {
                throw new ArgumentException(
                    "A disabled hearing profile must not declare hearing bands, " +
                    "bearing sectors, or loud event kinds.");
            }
            return;
        }

        if (hearingBearingSectors != 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hearingBearingSectors),
                "Generation 3 hearing uses exactly eight bearing sectors.");
        }
        if (hearingBearingModel
            != ActorHearingBearingModel
                .EightOctantsStrictTwoToOneCardinalV1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hearingBearingModel),
                "Generation 3 hearing uses the strict two-to-one cardinal " +
                "boundary model.");
        }
        if (loudKinds.IsEmpty)
        {
            throw new ArgumentException(
                "An enabled hearing profile needs at least one loud event kind.",
                nameof(loudKinds));
        }

        int previous = 0;
        foreach (int upperBound in bands)
        {
            if (upperBound <= previous || upperBound >= hearingRadius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bands),
                    "Hearing distance-band bounds must be strictly increasing, " +
                    "positive, and below the hearing radius.");
            }
            previous = upperBound;
        }
    }

    public enum HearingDistanceBandModelKind
    {
        /// <summary>
        /// Return the first zero-based band whose inclusive upper bound
        /// contains the Chebyshev distance; distances above every authored
        /// bound and at most HearingRadius use the final band.
        /// </summary>
        ChebyshevInclusiveOrderedUpperBoundsThenFinalRadiusBand = 0,
    }
}
