namespace BotArena.App.Bots;

public static class BotContractProfiles
{
    public const string LegacyDuel = "legacy-duel-0.1";

    public static bool Supports(
        string[]? supportedContractProfiles,
        string requiredContractProfileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredContractProfileId);
        return supportedContractProfiles is null
            ? string.Equals(
                requiredContractProfileId,
                LegacyDuel,
                StringComparison.Ordinal)
            : supportedContractProfiles.Contains(
                requiredContractProfileId,
                StringComparer.Ordinal);
    }

    /// <summary>
    /// During the off-by-default hosted rollout, every newly activated
    /// artifact must remain safe for old Duel admission unless generic actor
    /// hosting has been explicitly enabled after all replicas were upgraded.
    /// </summary>
    public static bool CanActivateCompiledArtifact(
        string[] supportedContractProfiles,
        bool genericActorHostingEnabled)
    {
        ArgumentNullException.ThrowIfNull(supportedContractProfiles);
        return supportedContractProfiles.Length > 0 &&
            (genericActorHostingEnabled ||
             Supports(
                 supportedContractProfiles,
                 LegacyDuel));
    }
}
