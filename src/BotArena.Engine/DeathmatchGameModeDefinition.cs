namespace BotArena.Engine;

/// <summary>
/// Typed vNext Deathmatch semantics. FFA and team play reuse this same mode
/// through different formats and topologies.
/// </summary>
public sealed record DeathmatchGameModeDefinition : GameModeDefinition
{
    public const string Id = "deathmatch";

    public DeathmatchGameModeDefinition(
        DeathmatchVictoryDefinition victory,
        int respawnDelayTicks)
        : base(Id, victory)
    {
        if (respawnDelayTicks < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(respawnDelayTicks),
                "Deathmatch respawn delay cannot be negative.");
        }
        DeathmatchVictory = victory;
        RespawnDelayTicks = respawnDelayTicks;
    }

    public override GameModeDefinitionKind Kind =>
        GameModeDefinitionKind.Deathmatch;
    public DeathmatchVictoryDefinition DeathmatchVictory { get; }
    public int RespawnDelayTicks { get; }
}
