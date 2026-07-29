namespace BotArena.App.Shared;

/// <summary>
/// The version axes a client must agree with to talk to this server, plus the maps it
/// serves. Named rather than anonymous so the generated OpenAPI schema is usable by
/// typed clients — the CLI and the mobile app both read this before anything else.
/// </summary>
public sealed record MetaResponse(
    string EngineVersion,
    string GameRulesVersion,
    string RuntimeProtocolVersion,
    string SdkVersion,
    string BuildPipelineVersion,
    string CliVersion,
    IReadOnlyList<MetaMapResponse> Maps,
    IReadOnlyList<MetaBotClassResponse> BotClasses);

/// <summary>One class identity accepted at bot creation and legacy assignment.</summary>
public sealed record MetaBotClassResponse(string Id);

/// <summary>
/// One playable arena map as advertised to clients. <see cref="ThemeId"/> is nullable:
/// a map may ship without a theme, in which case the viewer falls back to its default
/// palette. The anonymous type this replaced hid that from every client.
/// </summary>
public sealed record MetaMapResponse(string Id, int Width, int Height, string? ThemeId);
