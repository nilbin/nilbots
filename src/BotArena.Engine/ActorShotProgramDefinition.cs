using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Complete validation and fallback contract for programmed attacks. All
/// values that can accept, reject, or alter a payload are resolved data.
/// </summary>
public sealed record ActorShotProgramDefinition
{
    public ActorShotProgramDefinition(
        bool enabled,
        int headingSectors,
        ActorShotHeadingModel headingModel,
        int bendStepSectors,
        int minInitialAimSteps,
        int maxInitialAimSteps,
        ActorAimOnlyShotProgramDefinition aimOnlyProgram,
        IEnumerable<int> allowedCurvedBendDirections,
        int minBendAfterTiles,
        int maxBendAfterTiles,
        int minBendEveryTiles,
        int maxBendEveryTiles,
        int minBendCount,
        int maxBendCount,
        int launchTiles,
        bool payloadOptional,
        ActorShotProgramValue defaultProgram,
        ActorActionRejectionResult? invalidPayloadResult,
        ActorActionRejectionResult unsupportedPayloadResult,
        bool diagonalCornersMustBeClear)
    {
        ArgumentNullException.ThrowIfNull(aimOnlyProgram);
        ArgumentNullException.ThrowIfNull(allowedCurvedBendDirections);
        if (headingSectors != 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(headingSectors),
                "Generation 3 programmed shots use exactly eight headings.");
        }
        if (!Enum.IsDefined(headingModel)
            || headingModel
                != ActorShotHeadingModel.EightWayClockwiseModuloV1)
        {
            throw new ArgumentOutOfRangeException(nameof(headingModel));
        }
        if (bendStepSectors != 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bendStepSectors),
                "Generation 3 bends use one 45-degree heading step.");
        }
        if (minInitialAimSteps > maxInitialAimSteps)
            throw new ArgumentException("Initial-aim bounds are reversed.");
        if (minInitialAimSteps < -4 || maxInitialAimSteps > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minInitialAimSteps),
                "Initial-aim bounds must use unique offsets in [-4, 3].");
        }
        ValidatePositiveRange(
            minBendAfterTiles,
            maxBendAfterTiles,
            nameof(minBendAfterTiles),
            nameof(maxBendAfterTiles));
        ValidatePositiveRange(
            minBendEveryTiles,
            maxBendEveryTiles,
            nameof(minBendEveryTiles),
            nameof(maxBendEveryTiles));
        ValidatePositiveRange(
            minBendCount,
            maxBendCount,
            nameof(minBendCount),
            nameof(maxBendCount));
        if (launchTiles <= 0)
            throw new ArgumentOutOfRangeException(nameof(launchTiles));
        if (invalidPayloadResult is { } invalid
            && !Enum.IsDefined(invalid))
        {
            throw new ArgumentOutOfRangeException(nameof(invalidPayloadResult));
        }
        if (!Enum.IsDefined(unsupportedPayloadResult))
        {
            throw new ArgumentOutOfRangeException(
                nameof(unsupportedPayloadResult));
        }
        if (enabled != invalidPayloadResult.HasValue)
        {
            throw new ArgumentException(
                "Enabled shot programs need an invalid-payload result; " +
                "disabled shot programs must leave it null.",
                nameof(invalidPayloadResult));
        }

        ImmutableArray<int> bendDirections = allowedCurvedBendDirections
            .Select(value => value)
            .ToImmutableArray();
        if (bendDirections.IsEmpty
            || bendDirections.Any(direction => direction is not (-1 or 1))
            || bendDirections.Distinct().Count() != bendDirections.Length)
        {
            throw new ArgumentException(
                "Curved bend directions must be a non-empty subset of -1 and 1.",
                nameof(allowedCurvedBendDirections));
        }
        bendDirections = bendDirections.Order().ToImmutableArray();

        if (!IsValidProgram(
                defaultProgram,
                aimOnlyProgram,
                bendDirections,
                minInitialAimSteps,
                maxInitialAimSteps,
                minBendAfterTiles,
                maxBendAfterTiles,
                minBendEveryTiles,
                maxBendEveryTiles,
                minBendCount,
                maxBendCount))
        {
            throw new ArgumentException(
                "The default shot program is outside the declared bounds.",
                nameof(defaultProgram));
        }

        Enabled = enabled;
        HeadingSectors = headingSectors;
        HeadingModel = headingModel;
        BendStepSectors = bendStepSectors;
        MinInitialAimSteps = minInitialAimSteps;
        MaxInitialAimSteps = maxInitialAimSteps;
        AimOnlyProgram = aimOnlyProgram;
        AllowedCurvedBendDirections = bendDirections;
        MinBendAfterTiles = minBendAfterTiles;
        MaxBendAfterTiles = maxBendAfterTiles;
        MinBendEveryTiles = minBendEveryTiles;
        MaxBendEveryTiles = maxBendEveryTiles;
        MinBendCount = minBendCount;
        MaxBendCount = maxBendCount;
        LaunchTiles = launchTiles;
        PayloadOptional = payloadOptional;
        DefaultProgram = defaultProgram;
        InvalidPayloadResult = invalidPayloadResult;
        UnsupportedPayloadResult = unsupportedPayloadResult;
        DiagonalCornersMustBeClear = diagonalCornersMustBeClear;
    }

    public bool Enabled { get; }
    public int HeadingSectors { get; }
    public ActorShotHeadingModel HeadingModel { get; }
    public int BendStepSectors { get; }
    public int MinInitialAimSteps { get; }
    public int MaxInitialAimSteps { get; }
    public ActorAimOnlyShotProgramDefinition AimOnlyProgram { get; }
    public ImmutableArray<int> AllowedCurvedBendDirections { get; }
    public int MinBendAfterTiles { get; }
    public int MaxBendAfterTiles { get; }
    public int MinBendEveryTiles { get; }
    public int MaxBendEveryTiles { get; }
    public int MinBendCount { get; }
    public int MaxBendCount { get; }
    public int LaunchTiles { get; }
    public bool PayloadOptional { get; }
    public ActorShotProgramValue DefaultProgram { get; }
    public ActorActionRejectionResult? InvalidPayloadResult { get; }
    public ActorActionRejectionResult UnsupportedPayloadResult { get; }
    public bool DiagonalCornersMustBeClear { get; }

    internal bool IsValid(ShotProgram program) =>
        IsValidProgram(
            new ActorShotProgramValue(
                program.InitialAimOffset,
                program.BendDirection,
                program.BendAfterTiles,
                program.BendEveryTiles,
                program.BendCount),
            AimOnlyProgram,
            AllowedCurvedBendDirections,
            MinInitialAimSteps,
            MaxInitialAimSteps,
            MinBendAfterTiles,
            MaxBendAfterTiles,
            MinBendEveryTiles,
            MaxBendEveryTiles,
            MinBendCount,
            MaxBendCount);

    private static void ValidatePositiveRange(
        int minimum,
        int maximum,
        string minimumName,
        string maximumName)
    {
        if (minimum <= 0)
            throw new ArgumentOutOfRangeException(minimumName);
        if (maximum < minimum)
            throw new ArgumentOutOfRangeException(maximumName);
    }

    private static bool IsValidProgram(
        ActorShotProgramValue program,
        ActorAimOnlyShotProgramDefinition aimOnlyProgram,
        ImmutableArray<int> allowedBendDirections,
        int minInitialAimSteps,
        int maxInitialAimSteps,
        int minBendAfterTiles,
        int maxBendAfterTiles,
        int minBendEveryTiles,
        int maxBendEveryTiles,
        int minBendCount,
        int maxBendCount)
    {
        if (program.InitialAimOffset < minInitialAimSteps
            || program.InitialAimOffset > maxInitialAimSteps)
        {
            return false;
        }
        if (program.BendCount == 0)
        {
            return program.BendDirection == aimOnlyProgram.BendDirection
                && program.BendAfterTiles == aimOnlyProgram.BendAfterTiles
                && program.BendEveryTiles == aimOnlyProgram.BendEveryTiles;
        }

        return allowedBendDirections.Contains(program.BendDirection)
            && program.BendAfterTiles >= minBendAfterTiles
            && program.BendAfterTiles <= maxBendAfterTiles
            && program.BendEveryTiles >= minBendEveryTiles
            && program.BendEveryTiles <= maxBendEveryTiles
            && program.BendCount >= minBendCount
            && program.BendCount <= maxBendCount;
    }
}
