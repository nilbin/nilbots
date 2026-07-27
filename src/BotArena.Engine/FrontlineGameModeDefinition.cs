namespace BotArena.Engine;

/// <summary>Typed vNext Frontline mode values, independent of match format.</summary>
public sealed record FrontlineGameModeDefinition : GameModeDefinition
{
    public const string Id = "frontline";

    public FrontlineGameModeDefinition(
        FrontlineVictoryDefinition victory,
        int frontlinePositionCount)
        : base(Id, victory)
    {
        if (frontlinePositionCount < 3 || frontlinePositionCount % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frontlinePositionCount),
                "Frontline position count must be odd and at least three.");
        }
        FrontlinePositionCount = frontlinePositionCount;
        FrontlineVictory = victory;
    }

    public override GameModeDefinitionKind Kind =>
        GameModeDefinitionKind.Frontline;
    public FrontlineVictoryDefinition FrontlineVictory { get; }
    public int FrontlinePositionCount { get; }
}
