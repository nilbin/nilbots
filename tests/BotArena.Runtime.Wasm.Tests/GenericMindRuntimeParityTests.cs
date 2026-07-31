using BotArena.Runtime;
using BotArena.Runtime.Wasm;
using Engine = BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime.Wasm.Tests;

/// <summary>
/// The in-process mind runtime and the sandboxed one must be the same machine
/// with a different jacket.
///
/// <para>They share nothing but the seam: the in-process twin hands the SDK
/// mind an object graph directly, while the WASM one serializes the same
/// observation onto the wire, ships it into a guest, and parses a reply back.
/// If the mapper, the codec or the protocol lost or reshaped a single fact, the
/// two would disagree — so running the same scripted world through both and
/// requiring identical decisions is the cheapest honest check that the whole
/// P2 seam is lossless.</para>
/// </summary>
public sealed class GenericMindRuntimeParityTests
{
    [Fact]
    public void BothRuntimesProduceIdenticalDecisionsOnAScriptedMatch()
    {
        using GenericMindWasmTestFixture.TemporaryArtifact artifact =
            GenericMindWasmTestFixture.Happy();
        using var factory = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions
            {
                ModulePath = artifact.Path,
                TickTimeoutMs = 5_000,
            });
        Engine.ActorResolvedMatchDefinition contract =
            GenericMindWasmTestFixture.Contract();
        Engine.GenericMindRuntimeStart start =
            GenericMindWasmTestFixture.Start(contract);

        using Engine.IGenericMindRuntime sandboxed = factory.CreateRuntime();
        using Engine.IGenericMindRuntime inProcess =
            new InProcessGenericMindRuntimeFactory(() => new ScriptedMind())
                .CreateRuntime();
        sandboxed.StartMatch(start);
        inProcess.StartMatch(start);

        for (int tick = 0; tick < 8; tick++)
        {
            Engine.GenericMindRuntimeObservation observation =
                GenericMindWasmTestFixture.Observation(contract, tick);

            Engine.GenericMindRuntimeDecisions viaWasm =
                sandboxed.ExecuteTick(observation);
            Engine.GenericMindRuntimeDecisions viaMemory =
                inProcess.ExecuteTick(observation);

            Assert.Equal(
                Describe(viaMemory),
                Describe(viaWasm));
            Assert.Empty(viaWasm.Intents);
            Assert.Empty(viaMemory.Intents);
        }
    }

    private static string Describe(
        Engine.GenericMindRuntimeDecisions decisions) =>
        string.Join(
            "|",
            decisions.Commands.Select(command =>
                $"{command.UnitId}/{command.LifeId}/{command.ActionId}/"
                + $"{command.ActionCode}/{command.RoleTag}/"
                + $"{command.DebugMessage}/{command.Arguments.Length}"));

    /// <summary>
    /// The C# twin of the hand-written WAT: name the point-holder, hold it, and
    /// screen with everything else. On a tick with no bodies the loop simply
    /// does not run — which is the whole "am I alive is a data question"
    /// argument, expressed as the absence of a branch.
    /// </summary>
    private sealed class ScriptedMind : Sdk.IGenericMindBot
    {
        public void Think(Sdk.MindContext mind)
        {
            foreach (Sdk.MindBody body in mind.Bodies)
            {
                if (body.UnitId == 0)
                {
                    body.SetRole("channeler");
                    body.Hold("claim");
                }
                else
                {
                    body.SetRole("screen");
                    body.Hold();
                }
            }
        }
    }
}
