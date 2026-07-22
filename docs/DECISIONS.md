# Decisions log

Implementation decisions made where the plan left choices open, in the order they
were made. Anything here that later proves wrong should be revisited explicitly —
several of these are pinned by versions (game rules / runtime protocol / runtime
configuration) and changing them is a version bump, not an edit.

## Platform

1. **.NET 10 (LTS)** for everything. SDK pinned ≥ 10.0.3xx.
2. **Vite + React 19 + TypeScript + Tailwind 4 + Radix primitives** for the web
   viewer (and later the site frontend). The viewer builds to a single
   self-contained `index.html` so the CLI can ship it next to any replay.

## C# → WASM toolchain (the plan's biggest open risk, §16)

3. Microsoft removed the `wasi-experimental` workload in .NET 9 and did **not**
   bring it back in .NET 10. The viable path is **NativeAOT-LLVM**
   (`Microsoft.DotNet.ILCompiler.LLVM`, experimental feed
   `dnceng/public/dotnet-experimental`), pinned at `10.0.0-rc.1.26306.1`.
4. The net10 toolchain targets `wasm32-unknown-wasip2` and links through
   `wasm-component-ld` into a WASI **P2 component**. We deliberately link into a
   **WASI p1 core module** instead (shim linker drops the component step),
   because the host — **wasmtime-dotnet 44** — hosts core modules with fuel
   metering, memory limits, and typed host functions today. Revisit if/when
   wasmtime-dotnet gains component-model support.
5. **Synthetic wasi-sdk** (`scripts/setup-wasi-sdk.sh`): sandboxed environments
   cannot download the official wasi-sdk from GitHub releases, so we assemble an
   equivalent from Ubuntu packages (clang-18, lld, wasi-libc, wasm32
   compiler-rt/libc++) plus a 17-symbol single-threaded pthread stub archive.
   On unrestricted machines the real wasi-sdk-29 works identically — the
   toolchain that must match between local and server builds is the *compiler*
   (ILC), which is NuGet-pinned either way. Artifact-hash parity across the two
   sysroots is NOT expected; server rebuilds remain canonical (plan §14).
6. **Runtime protocol 0.1** is a line-oriented UTF-8 text protocol over two host
   functions (`botarena::next_observation`, `botarena::post_decision`). Chosen
   over JSON/binary for zero guest dependencies and trivial determinism.
   Debug payloads are base64. Format lives in `WasmProtocol.cs` (host) and
   `GuestProtocol.cs` (guest); the two must stay in sync.
7. **Bot randomness executes guest-side**: the host derives the bot seed
   (`SeedDerivation`) and sends it in the init line; the SDK-owned SplitMix64
   runs inside the sandbox. Determinism holds because the sandbox has no entropy
   imports the bot can reach. A future protocol may move RNG host-side (e.g. for
   per-call metering) — that is a runtime-protocol version bump.
8. **Fuel**: per-tick budget is *reset* at observation delivery (no carry-over).
   Initial calibration from the spike: full runtime startup ≈ 0.4–2 G fuel
   (hence `StartupFuel = 5 G`), a normal built-in bot tick ≪ 200 M
   (`FuelPerTick = 200 M`). These are provisional and part of runtime
   configuration 0.1; re-calibrate before competitive play.
9. **Wall-clock timeout (30 s/tick) is a backstop only** — fuel is the
   deterministic limit. A fuel trap (or any trap) permanently kills the
   instance; every later tick reports a fault and the engine's fault rules
   (3 faults → disqualification) end the match deterministically.

## Game rules 0.1

10. **Vision range 6, Chebyshev distance** (a square, not a circle — simple,
    integer, matches the square grid).
11. **Line of sight: corner-strict integer supercover.** A tile is visible when
    no strictly-intermediate supercover tile is a wall; when the sight line
    passes exactly through a tile corner, *both* adjacent side tiles must be
    clear, so vision never slips diagonally between two walls. Symmetric by
    construction and covered by tests.
12. **PRNG: SplitMix64**, `NextInt` via modulo reduction. Bot seed =
    `Mix(Mix(matchSeed ^ FNV1a64(rulesVersion)) + GOLDEN * (slot + 1))`.
    Golden-value tests pin the exact streams.
13. **Shoot cooldown = 2** with end-of-tick decrement ⇒ a bot can fire every
    3rd tick. Cooldown resets (not accumulates) on the tick the bot fires.
14. **Invalid actions become Wait with a result code** (`OnCooldown`, `Blocked`);
    **faults are only runtime failures** (exceptions, traps, fuel, protocol
    violations). Faulted ticks act as Wait and count toward the fault limit.
15. **Visible events** are previous-tick events whose reference positions are
    inside the observer's *current* field of view.
16. The §4.9 damage-dealt tiebreak is unreachable in symmetric 1v1 (equal
    health ⇒ equal damage received); kept for future modes.

## Replay format 1

17. Canonical JSON: camelCase, enums as strings, nulls omitted, no whitespace,
    property order = C# declaration order. **Replay hash = SHA-256 over the
    canonical JSON of `{header, ticks, result}`** (hash field excluded).
18. Per-tick observation summaries (visible tiles as `[x,y]` pairs, visible
    enemies) are stored inline in the replay so the viewer's developer mode can
    show exactly what a bot saw without recomputing FOV client-side. On the
    server these become owner-only (plan §27.2); locally they are always there.
19. The header embeds the map tiles, making every replay self-contained.

## Phase 1 (local developer experience)

20. **The WASI clock and entropy imports are shimmed deterministically by the
    host** (`clock_time_get` = logical clock advancing 1 ms/call; `random_get` =
    SplitMix64 stream derived from the bot seed). Even a bot calling
    `DateTime.UtcNow` or `Random.Shared` replays identically — defense in depth
    below the future analyzers (§6.1). Covered by the `guest-clock` contract test.
21. **`botarena build` mirrors the server submission flow (§14/§15.1)**: only
    `.cs` sources are taken from the player project; the artifact is compiled
    through a generated *controlled build project* wired to `GuestHost.Run`.
    The player's own csproj exists purely for IDE experience.
22. **Build cache key** = SHA-256 over sources + entry type + pinned versions
    (SDK, ILC, guest adapter, runtime protocol, runtime configuration).
    Framework code changes must bump `Toolchain.GuestAdapterVersion` (or use
    `--no-cache` during framework development) — player sources are hashed,
    framework sources are versioned.
23. The protocol init line's bot-name token is optional: multi-bot artifacts
    (built-ins) get a name, single-bot player artifacts don't.
24. SDK/Guest are consumed as project references from the repo for now; NuGet
    packaging + `dotnet new` template packaging happen when there is a registry
    to publish to (the `templates/botarena-bot` folder is already
    template-shaped for that move).

## Pilot platform (phases 2–4 compressed)

25. **Cookie auth only for the pilot**; OpenIddict + PKCE arrive with
    `botarena login/submit`. SameSite=Lax cookies are the CSRF stance.
26. **Server builds run through the same BotArena.Toolchain controlled build as
    the CLI** — plan §14's "one submission path" made literal. Submissions are
    validated (§15.2 first pass: .cs only, ≤16 files, ≤256 KB, no traversal)
    and smoke-tested (§15.4: 5-tick match vs idle; total faulting fails the build).
    NOT yet done: running the compiler in a locked-down container/user (§15.3) —
    required before opening to untrusted strangers; acceptable for a friends pilot.
27. **Auto-activate newest successful build** (plan wants manual activation §35)
    — friendlier for a pilot; revisit with qualification matches.
28. **"Live" matches are completed replays played from tick 0.** Feels live,
    is not synchronized across viewers; SignalR broadcast is the plan's §28
    design and remains open.
29. BotSubmission and BotVersion are merged into one row for the pilot; split
    them when qualification matches arrive.
30. Built-in opponents are seeded as system-owned bots whose versions carry a
    `GuestBotName` selector into the shared catalog artifact.

## Hardening + live viewing

31. **Submission builds run as an unprivileged `botbuild` user** via setpriv with
    CPU/process/file-size ulimits when the host is privileged and provisioned
    (Docker image does this); the controlled workspace is fully self-contained —
    it references prebuilt BotArena.Sdk/Guest *assemblies*, not repo projects,
    so the build user needs no access to the checkout. Falls back to in-process
    user for local dev. Still open for §15.3 completeness: network-less builds
    (NuGet restore needs the experimental feed until packages are vendored) and
    cgroup memory limits.
32. **Rate limits**: 600 req/min/IP global, 10/min auth, 6/10min submissions per
    user, 20/min challenges per user.
33. **Live viewing implements §28's semantics over HTTP polling, not SignalR
    yet**: matches compute instantly, then a server-side presentation clock
    (BroadcastStartedAt + 5 ticks/s) governs what every viewer sees; the replay
    endpoint truncates ticks and withholds the result until the clock passes
    them, so the outcome cannot be peeked. Viewers derive the same tick from the
    same clock → synchronized without a socket. SignalR remains the intended
    transport upgrade; the timeline model won't change.

## Rankings (phase 5)

34. **Ranked sets are 6 games**: three map/seed pairs (basic-01, arena-01 ×2),
    each played from both starting slots (§36 mirrored starts). Ratings move
    only when a whole set completes; a failed game voids the set.
35. **Elo at the bot level** (K=32, initial 1200, set score fraction as the
    game result). Deviation from §36's version-level rankings: bot-level keeps
    the pilot leaderboard stable across resubmissions; MatchSet rows snapshot
    the exact versions and pre-set ratings, so version-level rankings can be
    reconstructed later.
36. **Nothing spoils an unwatched broadcast**: match list/detail and match-set
    endpoints null out winners, outcomes and rating changes until the relevant
    broadcasts complete server-side.

## Deferred decisions

- Numeric limits for submissions (archive size, file counts) — Phase 3.
- Named RNG streams (`context.Random.Stream("...")`) — not in 0.1.
- Whether player artifacts embed one bot per artifact (likely) or use the
  built-in-style multi-bot selector — decide when the project template lands.
- wasmtime-dotnet pinning strategy across OSes for identical fuel accounting —
  verify when a second platform enters CI.
