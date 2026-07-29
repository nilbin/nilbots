using System.Runtime.InteropServices;
using Wasmtime;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// Best-effort cache of wasmtime's native module translation. Compiling a
/// WASM artifact to native code costs one to two seconds per process, and
/// batch runners spawn one process per match — the same bytes were being
/// retranslated hundreds of times per experiment. The cache key pins the
/// artifact hash, the wasmtime package version, the engine configuration
/// tag, and the host platform; deserialization failure of any kind falls
/// back to a fresh compile and rewrites the entry, so a stale or corrupt
/// cache can cost time but never correctness. Wasmtime's own round-trip
/// guarantee makes a deserialized module semantically identical to a
/// freshly compiled one — replay hashes are unaffected, and the
/// determinism suite is the enforcement. Disable outright with
/// BOTARENA_WASM_MODULE_CACHE=off.
/// </summary>
internal static class WasmModuleCache
{
    internal static Module LoadOrCompile(
        Wasmtime.Engine engine,
        string name,
        byte[] artifactBytes,
        string artifactSha256,
        string configTag)
    {
        string? cachePath = CachePath(artifactSha256, configTag);
        if (cachePath is not null && File.Exists(cachePath))
        {
            try
            {
                return Module.Deserialize(
                    engine,
                    name,
                    File.ReadAllBytes(cachePath));
            }
            catch
            {
                // Version drift, foreign platform, torn write — recompile
                // and replace below.
            }
        }

        Module module = Module.FromBytes(engine, name, artifactBytes);
        if (cachePath is not null)
            TryWrite(cachePath, module);
        return module;
    }

    private static string? CachePath(string artifactSha256, string configTag)
    {
        if (Environment.GetEnvironmentVariable("BOTARENA_WASM_MODULE_CACHE")
            is "off" or "0" or "false")
        {
            return null;
        }
        string home =
            Environment.GetEnvironmentVariable("BOTARENA_HOME")
                is { Length: > 0 } configured
                ? configured
                : Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    ".botarena");
        string wasmtimeVersion =
            typeof(Module).Assembly.GetName().Version?.ToString() ?? "0";
        string platform =
            $"{RuntimeInformation.OSDescription.Split(' ')[0]}" +
            $"-{RuntimeInformation.OSArchitecture}".ToLowerInvariant();
        return Path.Combine(
            home,
            "cache",
            "wasmtime-modules",
            $"{artifactSha256}-w{wasmtimeVersion}-{platform}-{configTag}.cwasm");
    }

    private static void TryWrite(string cachePath, Module module)
    {
        try
        {
            string directory = Path.GetDirectoryName(cachePath)!;
            Directory.CreateDirectory(directory);
            string temp = Path.Combine(
                directory,
                $".{Path.GetFileName(cachePath)}.{Environment.ProcessId}.tmp");
            File.WriteAllBytes(temp, module.Serialize());
            try
            {
                File.Move(temp, cachePath, overwrite: false);
            }
            catch (IOException)
            {
                // A concurrent lane already published the same key.
                File.Delete(temp);
            }
        }
        catch
        {
            // Cache writes are never load-bearing.
        }
    }
}
