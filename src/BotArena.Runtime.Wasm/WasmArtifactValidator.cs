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

    /// <summary>
    /// Runtime configuration 2.0's ceiling. The mind is the only instance its
    /// participant owns and holds match-long belief state, so it gets twice the
    /// per-life allowance — and per-participant peak memory still falls,
    /// because one instance replaces one per live body.
    /// </summary>
    public const long MaxMindInitialMemoryBytes = 128L * 1024 * 1024;
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
        byte[] bytes = ReadArtifact(path);
        ValidateBinaryEnvelope(bytes);

        using var engine = new WasmtimeEngine(new Config().WithFuelConsumption(true));
        using var module = Module.FromBytes(
            engine,
            Path.GetFileName(path),
            bytes);
        return Validate(module, bytes);
    }

    public static WasmArtifactValidation Validate(
        Module module,
        long sizeBytes,
        long maxInitialMemoryBytes = MaxInitialMemoryBytes)
    {
        if (sizeBytes is <= 0 or > MaxArtifactBytes)
        {
            throw new InvalidDataException(
                $"WASM artifact must be between 1 byte and {MaxArtifactBytes / 1024 / 1024} MiB.");
        }

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
        if (initialMemoryBytes > maxInitialMemoryBytes)
            throw new InvalidDataException(
                $"WASM initial memory exceeds {maxInitialMemoryBytes / 1024 / 1024} MiB.");

        return new WasmArtifactValidation(sizeBytes, initialMemoryBytes, imports);
    }

    internal static WasmArtifactValidation Validate(
        Module module,
        ReadOnlySpan<byte> bytes,
        long maxInitialMemoryBytes = MaxInitialMemoryBytes)
    {
        ValidateBinaryEnvelope(bytes);
        return Validate(module, bytes.Length, maxInitialMemoryBytes);
    }

    internal static byte[] ReadArtifact(string path)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException("WASM artifact does not exist.", path);
        }

        using (stream)
        {
            if (stream.Length is <= 0 or > MaxArtifactBytes)
            {
                throw new InvalidDataException(
                    $"WASM artifact must be between 1 byte and {MaxArtifactBytes / 1024 / 1024} MiB.");
            }

            byte[] bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1)
            {
                throw new InvalidDataException(
                    "WASM artifact changed while it was being read.");
            }
            return bytes;
        }
    }

    internal static void ValidateBinaryEnvelope(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> header =
        [
            0x00, 0x61, 0x73, 0x6d,
            0x01, 0x00, 0x00, 0x00,
        ];
        if (bytes.Length < header.Length
            || !bytes[..header.Length].SequenceEqual(header))
        {
            throw new InvalidDataException("WASM artifact header is invalid.");
        }

        int offset = header.Length;
        while (offset < bytes.Length)
        {
            byte sectionId = bytes[offset++];
            uint sectionLength = ReadUnsignedLeb32(bytes, ref offset);
            if (sectionLength > bytes.Length - offset)
            {
                throw new InvalidDataException(
                    "WASM artifact contains a truncated section.");
            }
            if (sectionId == 8)
            {
                throw new InvalidDataException(
                    "WASM start sections are not allowed; export _start instead.");
            }
            offset += checked((int)sectionLength);
        }
    }

    private static uint ReadUnsignedLeb32(
        ReadOnlySpan<byte> bytes,
        ref int offset)
    {
        uint value = 0;
        for (int index = 0; index < 5; index++)
        {
            if (offset >= bytes.Length)
            {
                throw new InvalidDataException(
                    "WASM artifact contains a truncated section length.");
            }

            byte current = bytes[offset++];
            if (index == 4 && (current & 0xF0) != 0)
            {
                throw new InvalidDataException(
                    "WASM artifact section length overflows uint32.");
            }
            value |= (uint)(current & 0x7F) << (index * 7);
            if ((current & 0x80) == 0)
                return value;
        }

        throw new InvalidDataException(
            "WASM artifact section length is malformed.");
    }

    private static bool Signature(
        FunctionImport function,
        IReadOnlyList<ValueKind> parameters,
        IReadOnlyList<ValueKind> results) =>
        function.Parameters.SequenceEqual(parameters) &&
        function.Results.SequenceEqual(results);
}
