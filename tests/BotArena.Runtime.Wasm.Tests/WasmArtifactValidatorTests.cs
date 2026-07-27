using BotArena.Runtime.Wasm;
using Wasmtime;
using WasmtimeEngine = Wasmtime.Engine;

namespace BotArena.Runtime.Wasm.Tests;

public class WasmArtifactValidatorTests
{
    [Fact]
    public void Validate_AcceptsCanonicalGuestArtifact()
    {
        string artifact = FindArtifact();
        Skip.If(artifact.Length == 0, "WASM guest artifact missing — run scripts/build-wasm-guest.sh");

        WasmArtifactValidation validation = WasmArtifactValidator.Validate(artifact);

        Assert.Contains("botarena::next_observation", validation.Imports);
        Assert.Contains("botarena::post_decision", validation.Imports);
        Assert.InRange(validation.InitialMemoryBytes, 1, WasmArtifactValidator.MaxInitialMemoryBytes);
    }

    [Fact]
    public void Validate_RejectsImportOutsideNilbotsAbi()
    {
        using var engine = new WasmtimeEngine();
        using var module = Module.FromText(
            engine,
            "hostile",
            """
            (module
              (import "botarena" "next_observation" (func (param i32 i32) (result i32)))
              (import "botarena" "post_decision" (func (param i32 i32)))
              (import "host" "connect" (func))
              (memory (export "memory") 1)
              (func (export "_start")))
            """);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => WasmArtifactValidator.Validate(module, 100));

        Assert.Contains("not allowed", error.Message);
    }

    [Fact]
    public void Validate_RejectsWasmStartSection()
    {
        byte[] bytes = Module.ConvertText(
            """
            (module
              (import "botarena" "next_observation"
                (func $next (param i32 i32) (result i32)))
              (import "botarena" "post_decision"
                (func (param i32 i32)))
              (memory (export "memory") 1)
              (func $init
                (drop (call $next (i32.const 0) (i32.const 0))))
              (start $init)
              (func (export "_start")))
            """);
        string path = Path.Combine(
            Path.GetTempPath(),
            $"nilbots-start-section-{Guid.NewGuid():N}.wasm");
        try
        {
            File.WriteAllBytes(path, bytes);

            InvalidDataException error =
                Assert.Throws<InvalidDataException>(
                    () => WasmArtifactValidator.Validate(path));

            Assert.Contains("start sections", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string FindArtifact()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "artifacts",
                "wasm",
                "builtin-bots.wasm");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        return "";
    }
}
