# Gen-9 docs-only arc trial and build findings — 2026-07-24

One isolated bot author received only the generated player README, public SDK
source, and CLI/help/replay-summary output. It did not read engine,
runtime/toolchain, design, repository-agent, or raw replay sources.

## Learnability result

The author produced Helix, an active holder and curved-shot architect that:

- remembers observed terrain and paths to the objective;
- treats successful Wait as the only control commitment;
- enumerates the nullable shot-program limits and previews candidate paths;
- predicts movement/refuge tiles without receiving the opponent's committed
  future path;
- dodges manifested speed-two bolts conservatively; and
- uses redacted hearing as a search cue, not exact radar.

The final WASM artifact had SHA-256
`d4483a859c7c2e6ae992a3aedec5d995304075ffd54cbf3c8b988790751eb987`.
The server rebuilt the same bytes. Ranked-format experimental sets were:

| opponent | Helix score |
| --- | ---: |
| Bastille gen-5 | 4–2 |
| Rampart gen-2 | 4–2 |
| Warden gen-1 | 5–1 |
| Helix mirror | 3–3 |

All six mirror games ended by elimination, in 11–31 ticks. Helix also won
in-process final checks against Hunter 4–2 across basic/arena and had zero
faults. This passes the player-facing learnability check: a fresh author can
build a coherent active-control/arc doctrine without engine knowledge, and
unchanged Bastille is not self-sufficient under v7.

This is not a new champion or an official-rules promotion. It is one bounded
challenger trial against historical rules-blind bots. The full matched 0.4
comparison remains the promotion gate.

## Fixed in this pass

1. **Critical — identical CLI/server WASM builds corrupted one workspace.**
   The in-memory cache-key lock covered threads in one process, not a CLI and
   server sharing `~/.botarena/cache`. Their concurrent NativeAOT publishes
   entered the same `build/` tree and both stalled.
2. **Critical — a timed-out Docker client left its compiler alive.** On Docker
   Desktop, killing `docker run` did not stop the daemon-owned container. The
   orphan kept the shared workspace open, so later builds also stalled with an
   empty log. Builds now have unique names and timeout cleanup removes the
   exact container before killing the client.
3. **High — parallel in-process plays raced in one bot's `bin/obj`.** CLI
   processes now serialize the short MSBuild step per project while matches
   remain independently runnable.
4. **Medium — JSON API examples omitted content type after registration.**
   Every JSON POST in the player docs now sends
   `Content-Type: application/json`.
5. **Medium — the cross-platform cache contract was implicit.**
   `WASM-DEVELOPMENT.md` now documents process-safe reuse, timeout cleanup,
   exact diagnostic commands, and the distinction between compiler host and
   portable WASM output.

## Verification and timing

After cleanup, the previously blocked Helix cold build completed on Apple
Silicon through the cached Linux x64 builder in 16.3 seconds. A fresh
concurrency probe launched two identical cold builds simultaneously:

- compiler: 14.1 seconds, cache miss;
- waiter: 14.2 seconds wall time, cache hit;
- artifact hashes: identical;
- compiler containers left running: zero.

The expected cost remains one mid-teens NativeAOT verification per changed
bot, not per code edit. Strategy iteration stays in-process; unchanged WASM
sources are cache hits.

## Still open

- Command-specific `--help` is still global rather than subcommand-specific.
- `botarena new` has no destination option; case-sensitive agent workspaces
  may need a move after scaffolding.
- Experimental control, hearing, projectile timing, and overtime values are
  distributed across public SDK comments and the experiment brief rather than
  one concise player-facing table.
- `replay --summary` needs an event-only compact mode for long matches.

