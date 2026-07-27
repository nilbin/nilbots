namespace BotArena.App.Competition;

/// <summary>
/// Pure boundary for a versioned ladder rating algorithm. A policy calculates every
/// entrant's transition together; persistence and transactional application stay
/// outside the algorithm.
/// </summary>
public interface IRatingPolicy
{
    string PolicyId { get; }

    IReadOnlyList<RatingUpdate> Calculate(RatingPolicyInput input);
}
