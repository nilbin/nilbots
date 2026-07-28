using BotArena.App.Competition;
using BotArena.App.Jobs;
using BotArena.Engine;

namespace BotArena.App.Tests;

public sealed class HostedGenericMatchDefinitionRegistryTests
{
    [Fact]
    public void ResolvesEveryRegisteredModeWithoutExecutorBranches()
    {
        FrontlineLabsPlaylistDefinition frontline =
            FrontlineLabsPlaylistDefinition.Create();
        var deathmatch = new FakeDefinition(
            "deathmatch-labs",
            version: 3,
            frontline.Match);
        var registry = new HostedGenericMatchDefinitionRegistry(
            [frontline, deathmatch]);

        Assert.Same(
            frontline,
            registry.Resolve(
                FrontlineLabsPlaylistDefinition.PlaylistKey,
                FrontlineLabsPlaylistDefinition.Version));
        Assert.Same(
            deathmatch,
            registry.Resolve("deathmatch-labs", 3));
        string frontlineJobType =
            GenericActorMatchJobType.ForPlaylist(
                FrontlineLabsPlaylistDefinition.PlaylistKey,
                FrontlineLabsPlaylistDefinition.Version);
        string deathmatchJobType =
            GenericActorMatchJobType.ForPlaylist(
                "deathmatch-labs",
                3);
        Assert.Equal(
            "ExecuteGenericActorMatch:frontline-labs:v1",
            frontlineJobType);
        Assert.Equal(
            new[] { deathmatchJobType, frontlineJobType }
                .Order(StringComparer.Ordinal),
            registry.ExecutionJobTypes);
        Assert.Same(
            deathmatch,
            registry.ResolveJobType(deathmatchJobType));
        Assert.True(registry.SupportsJobType(frontlineJobType));
        Assert.False(
            registry.SupportsJobType(
                GenericActorMatchJobType.ForPlaylist(
                    "unknown-labs",
                    1)));
        Assert.Throws<InvalidOperationException>(
            () => registry.Resolve("unknown-labs", 1));
        Assert.Throws<InvalidOperationException>(
            () => registry.ResolveJobType(
                GenericActorMatchJobType.ForPlaylist(
                    "unknown-labs",
                    1)));
    }

    [Fact]
    public void DuplicateIdentityOrWrongExecutionRouteIsRejected()
    {
        FrontlineLabsPlaylistDefinition frontline =
            FrontlineLabsPlaylistDefinition.Create();
        Assert.Throws<InvalidOperationException>(
            () => new HostedGenericMatchDefinitionRegistry(
                [frontline, frontline]));
        Assert.Throws<InvalidOperationException>(
            () => new HostedGenericMatchDefinitionRegistry(
                [
                    new FakeDefinition(
                        "wrong-route",
                        version: 1,
                        frontline.Match,
                        PlaylistExecutionPolicyIds.LegacyDuel),
                ]));
    }

    private sealed class FakeDefinition(
        string playlistKey,
        int version,
        ActorResolvedMatchDefinition match,
        string executionPolicyId =
            PlaylistExecutionPolicyIds.GenericActor)
        : IHostedGenericMatchDefinition
    {
        public string PlaylistKey { get; } = playlistKey;
        public int Version { get; } = version;
        public string AdmissionPolicyId =>
            Match.CapabilityVersions.ContractProfileId;
        public string ExecutionPolicyId { get; } = executionPolicyId;
        public string ExecutionEngineVersion =>
            BotArenaVersions.GenericActorEngineVersion;
        public ActorResolvedMatchDefinition Match { get; } = match;

        public void Validate(
            Playlist playlist,
            PlaylistVersion version)
        {
        }
    }
}
