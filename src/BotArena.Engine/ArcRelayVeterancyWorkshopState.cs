namespace BotArena.Engine;

/// <summary>
/// Public neutral-objective state of one veterancy Workshop. The signal is
/// deliberately team-and-progress only: it never names the training body, so
/// contesting the objective needs no fog-of-war leak. <see cref="ReadyAtTick"/>
/// is the one effective countdown — the initial unlock or the dormancy after
/// a completion — and is null while the Workshop is available.
/// </summary>
public sealed record ArcRelayVeterancyWorkshopState(
    string WorkshopId,
    Position TrainingTile,
    ArcRelayVeterancyWorkshopPhase Phase,
    int? TrainingTeamId,
    int ProgressTicks,
    int RequiredTicks,
    int? ReadyAtTick);

/// <summary>
/// The Workshop's public phase. Contested means a body stands on the
/// training tile while a living enemy of that body holds the authored
/// contest region, so progress cannot advance.
/// </summary>
public enum ArcRelayVeterancyWorkshopPhase
{
    Ready = 0,
    Training = 1,
    Contested = 2,
    Dormant = 3,
}
