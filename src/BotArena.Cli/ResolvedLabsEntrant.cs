using BotArena.Engine;
using BotArena.Runtime;
using BotArena.Runtime.Wasm;
using BotArena.Toolchain;

namespace BotArena.Cli;

/// <summary>
/// One Frontline Labs entrant resolved from a player project or a prebuilt
/// WASM artifact, on EITHER contract profile. Generic built-ins do not exist:
/// Labs comparisons always name both external entrants explicitly.
///
/// <para>The two profiles resolve through the same code because they are the
/// same entrant playing the same game — only the driver differs. On
/// <c>generic-actor-match-2</c> the entrant owns one runtime per body life; on
/// <c>generic-mind-match-1</c> it owns exactly one runtime for the whole match
/// and commands every body itself. A submitted artifact attests both, so a
/// per-life bot needs no edit to enter a mind match: the guest's wrap adapter
/// hosts it. That is what makes a MIXED match — a native mind against a
/// per-life artifact — an ordinary thing to run rather than a special case.
/// </para>
/// </summary>
internal sealed class ResolvedLabsEntrant : IDisposable
{
    public required string Name { get; init; }
    public required string Accent { get; init; }
    public required string LookId { get; init; }
    public required string ProjectileLookId { get; init; }

    /// <summary>Per-life owner; null on a mind-profile entrant.</summary>
    public IGenericActorRuntimeFactory? RuntimeFactory { get; init; }

    /// <summary>Per-participant owner; null on a per-life entrant.</summary>
    public IGenericMindRuntimeFactory? MindRuntimeFactory { get; init; }

    public required string ArtifactHash { get; init; }
    public required string RuntimeKind { get; init; }

    /// <summary>
    /// How this entrant reaches the profile it was resolved on, for the one
    /// line the CLI prints. A wrapped entrant is not a lesser one — it is the
    /// migration working — but a reader deserves to know which one is playing.
    /// </summary>
    public required string ProgrammingModel { get; init; }

    /// <summary>
    /// Sandbox failures, in one shape for both profiles. A per-life failure
    /// names the LIFE it killed; a mind failure names the participant, because
    /// that is the unit that owns the runtime — and under the mind, losing it
    /// costs the whole match's memory rather than one body's.
    /// </summary>
    public IReadOnlyList<(string Subject, ulong Peak, ulong Budget, string Reason)>
        SandboxFailures =>
        RuntimeFactory is WasmGenericActorRuntimeFactory wasm
            ? [
                .. wasm.Diagnostics
                    .Where(value => value.FailureReason is not null)
                    .Select(value => (
                        value.ActorId?.ToString() ?? "startup",
                        value.MaxFuelUsedPerTick,
                        value.FuelPerTick,
                        value.FailureReason!)),
            ]
            : MindRuntimeFactory is WasmGenericMindRuntimeFactory mind
                ? [
                    .. mind.Diagnostics
                        .Where(value => value.FailureReason is not null)
                        .Select(value => (
                            value.ParticipantId is int id
                                ? $"mind p{id}"
                                : "startup",
                            value.MaxFuelUsedPerTick,
                            value.LastTickFuelBudget,
                            value.FailureReason!)),
                ]
                : [];

    public GenericActorParticipantConfiguration ToParticipant(
        int participantId,
        int teamId,
        byte[]? mindEvaluationData = null,
        string? displayName = null) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = displayName ?? Name,
            RuntimeFactory = RuntimeFactory,
            MindRuntimeFactory = MindRuntimeFactory,
            RuntimeKind = RuntimeKind,
            ArtifactHash = ArtifactHash,
            MindEvaluationData = mindEvaluationData is null
                ? []
                : [.. mindEvaluationData],
            Accent = Accent,
            LookId = LookId,
            ProjectileLookId = ProjectileLookId,
        };

    public void Dispose()
    {
        RuntimeFactory?.Dispose();
        MindRuntimeFactory?.Dispose();
    }

    public static ResolvedLabsEntrant Resolve(
        string spec,
        string runtimeKind,
        bool quiet = false) =>
        Resolve(spec, runtimeKind, mindProfile: false, quiet);

    public static ResolvedLabsEntrant Resolve(
        string spec,
        string runtimeKind,
        bool mindProfile,
        bool quiet)
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
            return FromArtifact(
                ArtifactName(fullPath),
                fullPath,
                "#22d3ee",
                "vanguard",
                "pulse-bolt",
                mindProfile);
        }

        if (Directory.Exists(spec)
            && BotProject.LooksLikeProject(spec))
        {
            BotProject project = BotProject.Load(spec);
            if (runtimeKind == "in-process")
                return FromProjectInProcess(project, mindProfile, quiet);

            BuiltBot built = BotBuilder.EnsureBuilt(
                project,
                quiet: quiet);
            if (!quiet)
            {
                Console.WriteLine(
                    $"{project.Manifest.Name}: generic WASM artifact " +
                    $"{built.ArtifactHash[..12]}… " +
                    $"({(built.FromCache ? "cache" : "compiled")})");
            }

            return FromArtifact(
                project.Manifest.Name,
                built.WasmPath,
                project.Accent,
                project.LookId,
                project.ProjectileLookId,
                mindProfile);
        }

        throw new InvalidOperationException(
            $"Cannot resolve Labs entrant '{spec}': expected an "
            + "IGenericActorBot or IGenericMindBot project directory, or a "
            + ".wasm artifact.");
    }

    private static ResolvedLabsEntrant FromProjectInProcess(
        BotProject project,
        bool mindProfile,
        bool quiet)
    {
        if (!mindProfile)
        {
            InProcessProject.LoadedGenericActorFactory loaded =
                InProcessProject.LoadGenericActorFactory(project, quiet);
            return new ResolvedLabsEntrant
            {
                Name = project.Manifest.Name,
                Accent = project.Accent,
                LookId = project.LookId,
                ProjectileLookId = project.ProjectileLookId,
                RuntimeFactory =
                    new InProcessGenericActorRuntimeFactory(loaded.Factory),
                ArtifactHash = loaded.ProvenanceHash,
                RuntimeKind = "in-process-generic-actor",
                ProgrammingModel = "per-life",
            };
        }

        InProcessProject.LoadedGenericMindFactory mind =
            InProcessProject.LoadGenericMindFactory(project, quiet);
        return new ResolvedLabsEntrant
        {
            Name = project.Manifest.Name,
            Accent = project.Accent,
            LookId = project.LookId,
            ProjectileLookId = project.ProjectileLookId,
            MindRuntimeFactory =
                new InProcessGenericMindRuntimeFactory(mind.Factory),
            ArtifactHash = mind.ProvenanceHash,
            RuntimeKind = "in-process-generic-mind",
            ProgrammingModel = mind.NativeMind
                ? "mind"
                : "per-life (wrapped)",
        };
    }

    /// <summary>
    /// A sandboxed artifact on the requested profile.
    ///
    /// <para>The artifact itself decides how it answers a mind observation —
    /// natively when its author wrote a mind, through the guest's wrap adapter
    /// otherwise — and the host deliberately cannot tell the two apart, which
    /// is the migration working as designed. So the printed model reads
    /// "artifact-declared" rather than inventing a claim the host cannot
    /// check.</para>
    /// </summary>
    private static ResolvedLabsEntrant FromArtifact(
        string name,
        string modulePath,
        string accent,
        string lookId,
        string projectileLookId,
        bool mindProfile)
    {
        if (mindProfile)
        {
            var factory = new WasmGenericMindRuntimeFactory(
                new WasmMindRuntimeOptions
                {
                    ModulePath = modulePath,
                });
            return new ResolvedLabsEntrant
            {
                Name = name,
                Accent = accent,
                LookId = lookId,
                ProjectileLookId = projectileLookId,
                MindRuntimeFactory = factory,
                ArtifactHash = factory.ArtifactHash,
                RuntimeKind = "wasm",
                ProgrammingModel = "artifact-declared",
            };
        }

        var actorFactory = new WasmGenericActorRuntimeFactory(
            new WasmRuntimeOptions
            {
                ModulePath = modulePath,
            });
        return new ResolvedLabsEntrant
        {
            Name = name,
            Accent = accent,
            LookId = lookId,
            ProjectileLookId = projectileLookId,
            RuntimeFactory = actorFactory,
            ArtifactHash = actorFactory.ArtifactHash,
            RuntimeKind = "wasm",
            ProgrammingModel = "per-life",
        };
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
