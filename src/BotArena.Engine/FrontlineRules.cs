using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Experimental Frontline gameplay configuration. Presence of this subtree
/// selects Frontline definition validation; <see langword="null"/> leaves the
/// shipped duel rules and sessions unchanged.
/// </summary>
public sealed record FrontlineRules
{
    public int TeamCount { get; init; } = 2;
    public int ParticipantsPerTeam { get; init; } = 1;
    public int FrontlinePositionCount { get; init; } = 5;

    public int InitialUnitsPerTeam { get; init; } = 1;
    public int MaxUnitsPerTeam { get; init; } = 3;

    public int CaptureThreshold { get; init; } = 15;
    public int CaptureGainPerSoleTeamTick { get; init; } = 1;
    public int CaptureDecayAmount { get; init; } = 1;
    public int CaptureDecayIntervalTicks { get; init; } = 2;
    public int RedeployPauseTicks { get; init; } = 5;
    public int PushesToBreach { get; init; } = 3;

    public int PrimeRespawnTicks { get; init; } = 18;
    public int ChildRebuildTicks { get; init; } = 30;
    public ImmutableArray<int> FabricationUnlockTicks { get; init; } = [120, 260];

    public UnitFormRules PrimeForm { get; init; } = new(
        FormId: "prime-mobile",
        MaxHealth: 3,
        VisionRange: 6,
        ShootCooldownTicks: 2,
        OmnidirectionalVision: false,
        OmnidirectionalShooting: false,
        ObjectiveWeight: 1,
        CanMove: true,
        CanShoot: true,
        AllowsProgrammedShots: true);

    public UnitFormRules ChildForm { get; init; } = new(
        FormId: "child-mobile",
        MaxHealth: 3,
        VisionRange: 6,
        ShootCooldownTicks: 2,
        OmnidirectionalVision: false,
        OmnidirectionalShooting: false,
        ObjectiveWeight: 1,
        CanMove: true,
        CanShoot: true,
        AllowsProgrammedShots: true);

    public UnitFormRules TurretForm { get; init; } = new(
        FormId: "turret",
        MaxHealth: 5,
        VisionRange: 6,
        ShootCooldownTicks: 1,
        OmnidirectionalVision: true,
        OmnidirectionalShooting: true,
        ObjectiveWeight: 0,
        CanMove: false,
        CanShoot: true,
        AllowsProgrammedShots: false);

    public int AnchorWindupTicks { get; init; } = 1;
    public int AnchorHealthGain { get; init; } = 2;
    public bool AnchorIrreversibleForLife { get; init; } = true;

    public bool FriendlyFireEnabled { get; init; }
    public bool AlliedProjectilesBlock { get; init; }
}
