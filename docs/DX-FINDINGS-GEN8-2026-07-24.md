# Gen-8 docs/CLI findings — 2026-07-24

Four isolated bot authors were restricted to player-facing documentation and
the public CLI while building the revision-v4 aware field. Their independent
friction reports were consistent enough to treat as product findings.

## Fixed in this pass

1. **High — generated projects pinned SDK 0.1.** The current public API is
   0.7.0, so experiment bots had to edit `botarena.json` before projectile or
   control fields compiled. The template now receives
   `ToolchainInfo.SdkVersion` when scaffolded.
2. **High — experimental nullability was undocumented.** One bot treated
   `VisibleProjectiles` as always present and was disqualified in every
   no-bolt comparison, invalidating its first causal result. Section J and the
   generated README now show null-safe `HeardSounds` /
   `VisibleProjectiles` iteration and name every hearing/projectile field.
3. **High — source checkouts had no `botarena` command.** README and generated
   projects invoked a command setup never installed. `scripts/botarena` is a
   fast checkout wrapper; setup prints the one-line PATH export. It executes
   the already-built CLI assembly, avoiding repeated MSBuild startup and the
   child-node failures seen under concurrent author work.
4. **Medium — `new suppressor` generated uncompilable C#.** Lowercase type
   warning CS8981 is an error under repository policy. `new` now preserves the
   requested directory while normalizing the entry type to `Suppressor`.
5. **Medium — generated ProjectReference was checkout-absolute.** Moving or
   cloning the repository elsewhere broke IDE/in-process builds. The scaffold
   now writes a relative SDK project reference.
6. **Medium — projectile hits displayed as zero.** Replay summary counted only
   immediate `Shot.HitSlot`, even when later projectile `Damage` eliminated a
   bot. It now counts dealer-attributed Damage events, covering instant and
   projectile hits.
7. **Medium — unsupported `set --out` was silently ignored.** Set now rejects
   unknown options explicitly instead of pretending the requested output path
   was honored.
8. **Low — balance output hid champion survival.** The harness now prints
   per-bot W-L-D records for every arm, making turnover visible without manual
   replay parsing.
9. **High — the built-in WASM stamp included generated `obj/**/*.cs`.** A
   managed build changed the input set and triggered a needless 17-second
   NativeAOT rebuild despite unchanged guest sources. Generated `bin`/`obj`
   trees are now excluded and the stamp script hashes itself. On Apple
   Silicon the corrected changed build took 17.45 seconds; the immediately
   repeated unchanged check took 0.08 seconds.

## Still open

- Subcommand `--help` returns only global help; authors want command-specific
  examples and option lists.
- Parallel in-process builds of the same bot can race on `obj/Release` locks;
  concurrent `dotnet run` also intermittently produced MSB4166. The checkout
  wrapper removes redundant CLI builds, but bot-project build serialization
  still needs a deliberate solution.
- `new` has no destination option, so agents must change directory or rename
  the generated folder.
- `replay --summary` can remain very long during sustained contact/debug
  output. `--no-debug` helps, but an event-only compact mode would be clearer.
- `play --seeds ... --out <fixed-dir>` intentionally reuses that directory,
  but the repeated printed path is an easy matrix-testing overwrite trap.

## Build timing observed on Apple Silicon

All four changed player bots compiled successfully through the automatic
Docker linux/amd64 backend in 15.6–16.5 seconds. Unchanged artifact cache hits
were sub-second. In-process strategy matches remained sub-second once the
managed project was built. This confirms the expected boundary: a bot source
change pays the NativeAOT verification compile; ordinary engine/docs work and
unchanged bots do not.
