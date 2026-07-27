namespace BotArena.Engine;

/// <summary>
/// Fully disclosed rules-owned Deathmatch score semantics. Every score is
/// keyed by scoring team, so the same definition applies to head-to-head,
/// free-for-all, and team formats without participant-slot assumptions.
/// </summary>
public sealed record DeathmatchScoringDefinition
{
    public static DeathmatchScoringDefinition RawHostileKillV1 { get; } =
        new(
            DeathIncrementKind
                .OneRawDeathToDestroyedActorTeamPerDamageCausedDestruction,
            KillIncrementKind
                .OneRawKillToExactHostileHealthToZeroDamageSourceTeam,
            AlliedFinalDamageKind.VictimTeamDeathNoKill,
            DamageDealtIncrementKind
                .HostileActualHealthRemovedToExactSourceTeam,
            ActiveHealthSnapshotKind.TerminalSumAcrossActiveTeamLives,
            NonDamageRetirementKind
                .ReplicationRetirementAddsNeitherDeathNorKill,
            EarlyKillLimitResolutionKind
                .CompleteJointTickThenHighestRawKillsWinTiedTopDraw);

    public DeathmatchScoringDefinition(
        DeathIncrementKind deathIncrement,
        KillIncrementKind killIncrement,
        AlliedFinalDamageKind alliedFinalDamage,
        DamageDealtIncrementKind damageDealtIncrement,
        ActiveHealthSnapshotKind activeHealthSnapshot,
        NonDamageRetirementKind nonDamageRetirement,
        EarlyKillLimitResolutionKind earlyKillLimitResolution)
    {
        if (!Enum.IsDefined(deathIncrement))
            throw new ArgumentOutOfRangeException(nameof(deathIncrement));
        if (!Enum.IsDefined(killIncrement))
            throw new ArgumentOutOfRangeException(nameof(killIncrement));
        if (!Enum.IsDefined(alliedFinalDamage))
            throw new ArgumentOutOfRangeException(nameof(alliedFinalDamage));
        if (!Enum.IsDefined(damageDealtIncrement))
        {
            throw new ArgumentOutOfRangeException(
                nameof(damageDealtIncrement));
        }
        if (!Enum.IsDefined(activeHealthSnapshot))
        {
            throw new ArgumentOutOfRangeException(
                nameof(activeHealthSnapshot));
        }
        if (!Enum.IsDefined(nonDamageRetirement))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nonDamageRetirement));
        }
        if (!Enum.IsDefined(earlyKillLimitResolution))
        {
            throw new ArgumentOutOfRangeException(
                nameof(earlyKillLimitResolution));
        }

        DeathIncrement = deathIncrement;
        KillIncrement = killIncrement;
        AlliedFinalDamage = alliedFinalDamage;
        DamageDealtIncrement = damageDealtIncrement;
        ActiveHealthSnapshot = activeHealthSnapshot;
        NonDamageRetirement = nonDamageRetirement;
        EarlyKillLimitResolution = earlyKillLimitResolution;
    }

    public DeathIncrementKind DeathIncrement { get; }
    public KillIncrementKind KillIncrement { get; }
    public AlliedFinalDamageKind AlliedFinalDamage { get; }
    public DamageDealtIncrementKind DamageDealtIncrement { get; }
    public ActiveHealthSnapshotKind ActiveHealthSnapshot { get; }
    public NonDamageRetirementKind NonDamageRetirement { get; }
    public EarlyKillLimitResolutionKind EarlyKillLimitResolution { get; }

    public enum DeathIncrementKind
    {
        /// <summary>
        /// Every damage-caused destruction adds exactly one raw Death to the
        /// destroyed actor's scoring team, including allied or unattributed
        /// final damage. A life can contribute at most one Death.
        /// </summary>
        OneRawDeathToDestroyedActorTeamPerDamageCausedDestruction = 0,
    }

    public enum KillIncrementKind
    {
        /// <summary>
        /// The exact hostile damage instance that reduces remaining health to
        /// zero adds one raw Kill to its source life's scoring team. Joint
        /// damage uses the contract's canonical damage ordering; persistent
        /// projectiles retain their exact firing-life source.
        /// </summary>
        OneRawKillToExactHostileHealthToZeroDamageSourceTeam = 0,
    }

    public enum AlliedFinalDamageKind
    {
        /// <summary>
        /// Allied or self final damage still records the victim team's Death,
        /// but credits no Kill. This prevents friendly-fire kill farming.
        /// </summary>
        VictimTeamDeathNoKill = 0,
    }

    public enum DamageDealtIncrementKind
    {
        /// <summary>
        /// DamageDealt is the exact hostile health removed, capped by the
        /// target's remaining health, credited to the source scoring team.
        /// Allied and unattributed damage are not credited.
        /// </summary>
        HostileActualHealthRemovedToExactSourceTeam = 0,
    }

    public enum ActiveHealthSnapshotKind
    {
        /// <summary>
        /// ActiveHealth is the terminal sum of current health across active
        /// lives on the scoring team; dormant and pending slots add nothing.
        /// </summary>
        TerminalSumAcrossActiveTeamLives = 0,
    }

    public enum NonDamageRetirementKind
    {
        /// <summary>
        /// Replication retirement is a successful lifecycle transition, not
        /// destruction, and increments neither Deaths nor Kills.
        /// </summary>
        ReplicationRetirementAddsNeitherDeathNorKill = 0,
    }

    public enum EarlyKillLimitResolutionKind
    {
        /// <summary>
        /// Apply every destruction and score increment from the joint tick
        /// before checking the optional limit. Only eligible teams are
        /// compared. If their highest raw Kill count meets it, a unique top
        /// team wins; eligible teams tied at top draw. Lower eligible teams
        /// lose and ineligible teams remain tied below them. TimeoutRanking is
        /// not used for this early result.
        /// </summary>
        CompleteJointTickThenHighestRawKillsWinTiedTopDraw = 0,
    }
}
