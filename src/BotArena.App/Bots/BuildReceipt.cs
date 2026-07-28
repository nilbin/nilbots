using BotArena.Toolchain;

namespace BotArena.App.Bots;

public sealed record BuildReceipt(
    Guid BotVersionId,
    string SourceSha256,
    string ArtifactSha256,
    long ArtifactSizeBytes,
    string SdkVersion,
    string GuestAdapterVersion,
    string IlcLlvmVersion,
    string BuildPipelineVersion,
    string GameRulesVersion,
    string RuntimeProtocolVersion,
    string RuntimeConfigurationVersion,
    string CompilerImageReference,
    string GitCommit,
    DateTime BuiltAt,
    string[]? SupportedContractProfiles = null);
