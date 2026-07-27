using BotArena.Bots.BuiltIn;
using BotArena.Engine;
using BotArena.Runtime;
using BotArena.Runtime.Wasm;
using BotArena.Toolchain;

namespace BotArena.Cli;

/// <summary>
/// One submitted actor policy resolved from a Frontline built-in, player
/// project, or prebuilt WASM artifact. The factory is match-batch scoped;
/// <see cref="FrontlineActorMatchEngine"/> owns and disposes only the
/// individual life runtimes it creates from that factory.
/// </summary>
internal sealed class ResolvedActorBot : IDisposable
{
    public required string Name { get; init; }
    public required string Accent { get; init; }
    public required string LookId { get; init; }
    public required string ProjectileLookId { get; init; }
    public required IActorRuntimeFactory RuntimeFactory { get; init; }
    public required string ArtifactHash { get; init; }
    public required string RuntimeKind { get; init; }

    public ActorParticipantConfiguration ToParticipant(
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

    public static ResolvedActorBot Resolve(
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

        string normalized = spec.ToLowerInvariant();
        if (BuiltInActorBotCatalog.Names.Contains(normalized))
            return ResolveBuiltIn(normalized, runtimeKind, quiet);

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
            var factory = new WasmActorRuntimeFactory(
                new WasmRuntimeOptions
                {
                    ModulePath = fullPath,
                });
            return new ResolvedActorBot
            {
                Name = ArtifactName(fullPath),
                Accent = "#22d3ee",
                LookId = "vanguard",
                ProjectileLookId = "pulse-bolt",
                RuntimeFactory = factory,
                ArtifactHash = factory.ArtifactHash,
                RuntimeKind = "wasm-actor",
            };
        }

        if (Directory.Exists(spec)
            && BotProject.LooksLikeProject(spec))
        {
            BotProject project = BotProject.Load(spec);
            string accent = project.Accent;
            string lookId = project.LookId;
            string projectileLookId = project.ProjectileLookId;
            if (runtimeKind == "in-process")
            {
                InProcessProject.LoadedActorFactory loaded =
                    InProcessProject.LoadActorFactory(project, quiet);
                return new ResolvedActorBot
                {
                    Name = project.Manifest.Name,
                    Accent = accent,
                    LookId = lookId,
                    ProjectileLookId = projectileLookId,
                    RuntimeFactory = new InProcessActorRuntimeFactory(
                        loaded.Factory),
                    ArtifactHash = loaded.ProvenanceHash,
                    RuntimeKind = "in-process-actor",
                };
            }

            BuiltBot built = BotBuilder.EnsureBuilt(
                project,
                quiet: quiet);
            if (!quiet)
            {
                Console.WriteLine(
                    $"{project.Manifest.Name}: actor WASM artifact " +
                    $"{built.ArtifactHash[..12]}… " +
                    $"({(built.FromCache ? "cache" : "compiled")})");
            }

            var factory = new WasmActorRuntimeFactory(
                new WasmRuntimeOptions
                {
                    ModulePath = built.WasmPath,
                });
            return new ResolvedActorBot
            {
                Name = project.Manifest.Name,
                Accent = accent,
                LookId = lookId,
                ProjectileLookId = projectileLookId,
                RuntimeFactory = factory,
                ArtifactHash = factory.ArtifactHash,
                RuntimeKind = "wasm-actor",
            };
        }

        throw new InvalidOperationException(
            $"Cannot resolve Frontline actor '{spec}': not an actor built-in " +
            $"({string.Join(", ", BuiltInActorBotCatalog.Names)}), bot " +
            "project directory, or .wasm artifact.");
    }

    private static ResolvedActorBot ResolveBuiltIn(
        string name,
        string runtimeKind,
        bool quiet)
    {
        switch (runtimeKind)
        {
            case "wasm":
                string? artifact = CliSupport.FindUpward(
                    Path.Combine(
                        "artifacts",
                        "wasm",
                        "builtin-bots.wasm"));
                if (artifact is null)
                {
                    throw new InvalidOperationException(
                        "Built-in actor WASM artifact not found — run " +
                        "scripts/build-wasm-guest.sh, or use " +
                        "--runtime in-process for a diagnostic run.");
                }
                var factory = new WasmActorRuntimeFactory(
                    new WasmRuntimeOptions
                    {
                        ModulePath = artifact,
                        BotName = name,
                    });
                return new ResolvedActorBot
                {
                    Name = name,
                    Accent = BuiltInActorBotCatalog.Accent(name),
                    LookId = BuiltInActorBotCatalog.Look(name),
                    ProjectileLookId =
                        BuiltInActorBotCatalog.ProjectileLook(name),
                    RuntimeFactory = factory,
                    ArtifactHash = factory.ArtifactHash,
                    RuntimeKind = "wasm-actor",
                };

            case "in-process":
                if (!quiet)
                {
                    Console.WriteLine(
                        "NOTE: in-process Frontline is diagnostic only; " +
                        "fuel and memory limits are not enforced.");
                }
                string assemblyPath =
                    typeof(BuiltInActorBotCatalog).Assembly.Location;
                return new ResolvedActorBot
                {
                    Name = name,
                    Accent = BuiltInActorBotCatalog.Accent(name),
                    LookId = BuiltInActorBotCatalog.Look(name),
                    ProjectileLookId =
                        BuiltInActorBotCatalog.ProjectileLook(name),
                    RuntimeFactory = new InProcessActorRuntimeFactory(
                        () => BuiltInActorBotCatalog.Create(name)),
                    ArtifactHash = BotBuilder.Sha256File(assemblyPath),
                    RuntimeKind = "in-process-actor",
                };

            default:
                throw new InvalidOperationException(
                    $"Unknown runtime '{runtimeKind}' " +
                    "(use wasm or in-process).");
        }
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
