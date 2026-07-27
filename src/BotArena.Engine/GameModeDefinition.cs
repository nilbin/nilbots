namespace BotArena.Engine;

/// <summary>
/// Closed vNext game-mode union. Participant arrangement is deliberately
/// absent: it belongs to <see cref="MatchFormatDefinition"/> and topology.
/// </summary>
public abstract record GameModeDefinition
{
    internal GameModeDefinition(
        string modeId,
        VictoryDefinition victory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modeId);
        ArgumentNullException.ThrowIfNull(victory);
        ModeId = modeId;
        Victory = victory;
    }

    public abstract GameModeDefinitionKind Kind { get; }
    public string ModeId { get; }
    public VictoryDefinition Victory { get; }

    public enum GameModeDefinitionKind
    {
        Frontline = 0,
        Deathmatch = 1,
    }
}
