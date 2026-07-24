using Wasmtime;
using WasmtimeEngine = Wasmtime.Engine;

namespace BotArena.Runtime.Wasm;

public sealed record WasmArtifactValidation(
    long SizeBytes,
    long InitialMemoryBytes,
    IReadOnlyList<string> Imports);

/// <summary>
/// Rejects modules outside the narrow nilbots ABI before they enter object
/// storage. Wasmtime remains the execution sandbox; this validator reduces the
/// exposed capability and compatibility surface.
/// </summary>
public static class WasmArtifactValidator
{
    public const long MaxArtifactBytes = 16 * 1024 * 1024;
    public const long MaxInitialMemoryBytes = 64 * 1024 * 1024;
    private const long WasmPageBytes = 64 * 1024;

    private static readonly HashSet<string> AllowedWasiFunctions =
    [
        "args_get",
        "args_sizes_get",
        "clock_time_get",
        "environ_get",
        "environ_sizes_get",
        "fd_close",
        "fd_fdstat_get",
        "fd_seek",
        "fd_write",
        "poll_oneoff",
        "proc_exit",
        "random_get",
        "sched_yield",
    ];

    public static WasmArtifactValidation Validate(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new FileNotFoundException("WASM artifact does not exist.", path);
        if (file.Length is <= 0 or > MaxArtifactBytes)
            throw new InvalidDataException(
                $"WASM artifact must be between 1 byte and {MaxArtifactBytes / 1024 / 1024} MiB.");

        using var engine = new WasmtimeEngine(new Config().WithFuelConsumption(true));
        using var module = Module.FromFile(engine, path);
        return Validate(module, file.Length);
    }

    public static WasmArtifactValidation Validate(Module module, long sizeBytes)
    {
        var imports = new List<string>();
        bool nextObservation = false;
        bool postDecision = false;
        foreach (Import import in module.Imports)
        {
            imports.Add($"{import.ModuleName}::{import.Name}");
            if (import is not FunctionImport function)
                throw new InvalidDataException($"Non-function WASM import is not allowed: {import}.");

            if (import.ModuleName == "botarena")
            {
                if (import.Name == "next_observation" &&
                    Signature(function, [ValueKind.Int32, ValueKind.Int32], [ValueKind.Int32]))
                {
                    nextObservation = true;
                    continue;
                }
                if (import.Name == "post_decision" &&
                    Signature(function, [ValueKind.Int32, ValueKind.Int32], []))
                {
                    postDecision = true;
                    continue;
                }
                throw new InvalidDataException($"Unsupported nilbots ABI import: {import}.");
            }

            if (import.ModuleName != "wasi_snapshot_preview1" ||
                !AllowedWasiFunctions.Contains(import.Name))
            {
                throw new InvalidDataException($"WASM import is not allowed: {import}.");
            }
        }

        if (!nextObservation || !postDecision)
            throw new InvalidDataException("WASM artifact does not import the complete nilbots ABI.");

        FunctionExport? start = module.Exports
            .OfType<FunctionExport>()
            .SingleOrDefault(export => export.Name == "_start");
        if (start is null || start.Parameters.Count != 0 || start.Results.Count != 0)
            throw new InvalidDataException("WASM artifact must export a parameterless _start function.");

        MemoryExport? memory = module.Exports
            .OfType<MemoryExport>()
            .SingleOrDefault(export => export.Name == "memory");
        if (memory is null || memory.Is64Bit)
            throw new InvalidDataException("WASM artifact must export 32-bit memory.");
        long initialMemoryBytes = checked(memory.Minimum * WasmPageBytes);
        if (initialMemoryBytes > MaxInitialMemoryBytes)
            throw new InvalidDataException(
                $"WASM initial memory exceeds {MaxInitialMemoryBytes / 1024 / 1024} MiB.");

        return new WasmArtifactValidation(sizeBytes, initialMemoryBytes, imports);
    }

    private static bool Signature(
        FunctionImport function,
        IReadOnlyList<ValueKind> parameters,
        IReadOnlyList<ValueKind> results) =>
        function.Parameters.SequenceEqual(parameters) &&
        function.Results.SequenceEqual(results);
}
