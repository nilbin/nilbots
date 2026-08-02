namespace BotArena.App.Matches;

/// <summary>Product retirement gate. Historical reads and verification remain enabled.</summary>
public sealed record LegacyDuelSettings(bool AdmissionEnabled)
{
    public static LegacyDuelSettings FromConfiguration(IConfiguration configuration) =>
        new(configuration.GetValue<bool>("BOTARENA_LEGACY_DUEL_ENABLED"));
}
