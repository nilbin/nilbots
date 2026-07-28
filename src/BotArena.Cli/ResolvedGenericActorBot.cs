using BotArena.Engine;
using BotArena.Runtime;
using BotArena.Runtime.Wasm;
using BotArena.Toolchain;

namespace BotArena.Cli;

/// <summary>
/// One generic actor policy resolved from a player project or prebuilt WASM
/// artifact. Generic built-ins do not exist: Labs comparisons always name
/// both external entrants explicitly.
/// </summary>
internal sealed class ResolvedGenericActorBot : IDisposable
{
    public required string Name { get; init; }
    public required string Accent { get; init; }
    public required string LookId { get; init; }
    public required string ProjectileLookId { get; init; }
    public required IGenericActorRuntimeFactory RuntimeFactory { get; init; }
    public required string ArtifactHash { get; init; }
    public required string RuntimeKind { get; init; }

    public IReadOnlyList<
        WasmGenericActorRuntimeFactory.RuntimeDiagnostic> WasmDiagnostics =>
        RuntimeFactory is WasmGenericActorRuntimeFactory wasm
            ? wasm.Diagnostics
            : [];

    public GenericActorParticipantConfiguration ToParticipant(
        int participantId,
        int teamId) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = Name,
            RuntimeFactory = RuntimeFactory,
            RuntimeKind = RuntimeKind,
            ArtifactHash = ArtifactHash,
            Accent = Accent,
            LookId = LookId,
            ProjectileLookId = ProjectileLookId,
        };

    public void Dispose() => RuntimeFactory.Dispose();

    public static ResolvedGenericActorBot Resolve(
        string spec,
        string runtimeKind,
        bool quiet = false)
    {
        if (runtimeKind is not ("wasm" or "in-process"))
        {
            throw new InvalidOperationException(
                $"Unknown runtime '{runtimeKind}' " +
                "(use wasm or in-process).");
        }

        if (File.Exists(spec)
            && spec.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
        {
            if (runtimeKind == "in-process" && !quiet)
            {
                Console.WriteLine(
                    $"note: {Path.GetFileName(spec)} is prebuilt WASM and " +
                    "therefore remains sandboxed in this diagnostic batch.");
            }

            string fullPath = Path.GetFullPath(spec);
            var factory = new WasmGenericActorRuntimeFactory(
                new WasmRuntimeOptions
                {
                    ModulePath = fullPath,
                });
            return new ResolvedGenericActorBot
            {
                Name = ArtifactName(fullPath),
                Accent = "#22d3ee",
                LookId = "vanguard",
                ProjectileLookId = "pulse-bolt",
                RuntimeFactory = factory,
                ArtifactHash = factory.ArtifactHash,
                RuntimeKind = "wasm",
            };
        }

        if (Directory.Exists(spec)
            && BotProject.LooksLikeProject(spec))
        {
            BotProject project = BotProject.Load(spec);
            if (runtimeKind == "in-process")
            {
                InProcessProject.LoadedGenericActorFactory loaded =
                    InProcessProject.LoadGenericActorFactory(project, quiet);
                return new ResolvedGenericActorBot
                {
                    Name = project.Manifest.Name,
                    Accent = project.Accent,
                    LookId = project.LookId,
                    ProjectileLookId = project.ProjectileLookId,
                    RuntimeFactory =
                        new InProcessGenericActorRuntimeFactory(
                            loaded.Factory),
                    ArtifactHash = loaded.ProvenanceHash,
                    RuntimeKind = "in-process-generic-actor",
                };
            }

            BuiltBot built = BotBuilder.EnsureBuilt(
                project,
                quiet: quiet);
            if (!quiet)
            {
                Console.WriteLine(
                    $"{project.Manifest.Name}: generic actor WASM artifact " +
                    $"{built.ArtifactHash[..12]}… " +
                    $"({(built.FromCache ? "cache" : "compiled")})");
            }

            var factory = new WasmGenericActorRuntimeFactory(
                new WasmRuntimeOptions
                {
                    ModulePath = built.WasmPath,
                });
            return new ResolvedGenericActorBot
            {
                Name = project.Manifest.Name,
                Accent = project.Accent,
                LookId = project.LookId,
                ProjectileLookId = project.ProjectileLookId,
                RuntimeFactory = factory,
                ArtifactHash = factory.ArtifactHash,
                RuntimeKind = "wasm",
            };
        }

        throw new InvalidOperationException(
            $"Cannot resolve generic actor '{spec}': expected an " +
            "IGenericActorBot project directory or .wasm artifact.");
    }

    private static string ArtifactName(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        if (!stem.Equals("bot", StringComparison.OrdinalIgnoreCase))
            return stem;

        var directory = new DirectoryInfo(
            Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException(
                $"Artifact '{path}' has no parent directory."));
        if (directory.Name == "out" && directory.Parent is not null)
            directory = directory.Parent;
        return directory.Name;
    }
}
