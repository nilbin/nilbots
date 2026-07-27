namespace BotArena.Engine;

/// <summary>
/// Health handling for a same-life form transition. Target maximum health is
/// supplied by the referenced target form.
/// </summary>
public sealed record ActorSameLifeHealthDefinition
{
    public ActorSameLifeHealthDefinition(
        HealthPolicyKind policy,
        int flatHealthGain)
    {
        if (!Enum.IsDefined(policy))
            throw new ArgumentOutOfRangeException(nameof(policy));
        if (flatHealthGain < 0)
            throw new ArgumentOutOfRangeException(nameof(flatHealthGain));
        if (policy == HealthPolicyKind.AddFlatCappedToTargetMaximum)
        {
            if (flatHealthGain == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(flatHealthGain),
                    "The additive transition policy needs a positive gain.");
            }
        }
        else if (flatHealthGain != 0)
        {
            throw new ArgumentException(
                "Only the additive transition policy accepts a flat health gain.",
                nameof(flatHealthGain));
        }

        Policy = policy;
        FlatHealthGain = flatHealthGain;
    }

    public HealthPolicyKind Policy { get; }
    public int FlatHealthGain { get; }
    public EvaluationKind Evaluation =>
        EvaluationKind.CompletionTimePreTransitionHealth;
    public ArithmeticKind Arithmetic =>
        ArithmeticKind.CheckedInt64ThenClampToTargetMaximum;
    public PreserveRatioFormulaKind PreserveRatioFormula =>
        PreserveRatioFormulaKind
            .FloorCurrentTimesTargetMaximumDividedBySourceMaximumThenMinimumOne;

    public enum HealthPolicyKind
    {
        PreserveCurrentCappedToTargetMaximum = 0,
        AddFlatCappedToTargetMaximum = 1,
        SetToTargetMaximum = 2,
        PreserveRatioFloorMinimumOne = 3,
    }

    public enum EvaluationKind
    {
        CompletionTimePreTransitionHealth = 0,
    }

    public enum ArithmeticKind
    {
        CheckedInt64ThenClampToTargetMaximum = 0,
    }

    public enum PreserveRatioFormulaKind
    {
        /// <summary>
        /// For PreserveRatio, compute
        /// floor(currentHealth * targetMaximum / sourceMaximum) with the
        /// multiplication performed first in checked signed 64-bit arithmetic,
        /// then clamp to at least one and at most targetMaximum. Source and
        /// target maxima are the completion-time forms' positive maxima.
        /// Other policies ignore this formula tag.
        /// </summary>
        FloorCurrentTimesTargetMaximumDividedBySourceMaximumThenMinimumOne = 0,
    }
}
