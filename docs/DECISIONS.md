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

## CLI ↔ server loop

37. **CLI auth is OpenIddict Authorization Code + PKCE, per §13.2** (an earlier
    API-token stand-in was removed the same day). OpenIddict is embedded in the
    monolith with EF stores in the same Postgres DB; the CLI is a seeded public
    client (`botarena-cli`, implicit consent, PKCE required) with four
    registered loopback redirect ports. `botarena login` opens the browser,
    catches the code on 127.0.0.1, exchanges with the verifier, and stores
    access+refresh tokens in the OS secret service when available
    (`secret-tool`), else a 0600 file with a printed warning. Access tokens
    live 1 h and auto-refresh (rotating refresh tokens honored). Signing and
    encryption certificates persist under `BOTARENA_DATA/keys` so tokens
    survive restarts. Note: EF 10's pending-model-changes heuristic
    false-positives on OpenIddict's model — suppressed with an empty-diff
    proof, see Program.cs.
38. **`botarena submit` reports artifact parity** (§46): it builds locally,
    uploads sources, waits for the canonical server build, and compares
    hashes. On a shared toolchain/sysroot the hashes are expected IDENTICAL
    (verified in-container); drift means toolchain/sysroot mismatch and the
    server artifact wins (§14).
39. **Debug lines are public in v0.1 replays.** `Debug.Write` output is part
    of the canonical (hashed) replay document, so the server cannot redact it
    per-viewer without breaking hash verification. The docs used to promise
    "visible only to you" — corrected to state reality (public once the
    broadcast reveals the replay). If strategy-leak becomes a real complaint,
    the fix is a format change: hash the match without debug and ship debug as
    a sidecar keyed by owner. Not worth it for the pilot.
40. **`botarena play` output defaults to a per-matchup directory**
    (`out/<bot>-vs-<opponent>-<map>-s<seed>/`, tournament DX finding #3):
    parallel runs stopped clobbering each other's replays, while identical
    reruns of a deterministic match still overwrite in place. `--out` pins an
    exact directory; scripts that need a fixed path pass it explicitly.
41. **Broadcast pacing is server configuration, not a request option.**
    `BOTARENA_BROADCAST_TPS` (default 5) and `BOTARENA_BROADCAST_DELAY_SECONDS`
    (default 3) tune the presentation clock so eval deployments (agent-arena)
    aren't rate-limited by spectator pacing — the first tournament spent most
    of its wall clock literally watching matches at 5 ticks/s. Deliberately not
    a per-request bypass: the no-spoiler invariant (§28, nothing revealed
    before `BroadcastComplete`) holds identically at any speed, and a request
    flag would be a spoiler hole. Also fixed: `PresentationTick` now clamps
    instead of overflowing int after weeks (which would have un-revealed old
    matches, sooner at high TPS).
42. **Typed job lanes: one match lane + N compile lanes** (the match-lane
    limit is superseded by the transactionally safe configuration in #110)
    (`BOTARENA_COMPILE_WORKERS`, default 1). Set finalization stays race-free
    because match jobs keep a single consumer (#26 still holds); compiles are
    embarrassingly parallel and were the tournament's other bottleneck — a
    3-minute NativeAOT build blocked every match behind it. BotBuilder is now
    thread-safe for this: same-cache-key builds serialize on a per-key lock
    (shared workspace), distinct keys build concurrently, and the one-time
    Sdk/Guest toolchain-assembly build is double-check locked.
43. **Champions are seeded server bots, and the tournament bar.**
    `ChampionSeeder` registers every `champions/<slug>/` (needs `champion.json`
    + `bot.wasm`) as a system-owned bot on startup, keyed by slug, re-versioned
    if the committed artifact ever changes. The committed .wasm IS the artifact
    — champions are never rebuilt from source, so the crowned bot stays
    bit-identical forever; sources are carried as viewable SourcesJson only.
    agent-arena brackets now include all reigning champions instead of
    hunter-baseline sets, and a new generation is crowned only by finishing
    above every reigning champion (a defended title is a valid outcome).
    Pilot bonus: players can challenge the champion on the site like any bot.
44. **Fast inner loop: `--runtime in-process` runs player bot projects.**
    The CLI builds the project's own csproj as a plain .NET assembly (~2 s,
    incremental) and loads it into the diagnostic in-process runtime via an
    AssemblyLoadContext that shares the host's BotArena.Sdk (type identity).
    Same engine, same deterministic per-bot RNG; fuel/memory limits and WASI
    shims NOT enforced — docs say "iterate in-process, verify in WASM", which
    stays the §3.1 boundary. Measurement that reshaped this work: a cold
    NativeAOT-LLVM WASM compile on an idle box is ~8 s regardless of bot size
    (warm NuGet) — the tournament's "2–4 minutes" was 3-way compile
    contention on 4 cores plus first-boot package downloads, not intrinsic
    cost. Consequently the planned guest trim-flags companion (PLAN-SUMMARY
    pointer #1) is DROPPED: saving ~seconds does not justify invalidating
    every build cache with a GuestAdapterVersion bump.
45. **WASI SDK fallback prefers the system install.** Without WASI_SDK_PATH,
    BotBuilder fell back to `~/.wasi-sdk/wasi-sdk-29.0`; under isolated
    builds the `botbuild` user cannot traverse /root, so clang failed with
    exit 126. Resolution order is now env → `/opt/botarena/wasi-sdk-29.0`
    (world-readable, where setup-wasi-sdk.sh installs) → legacy home dir;
    doctor reports the same resolution.
46. **Gen-2 findings pass: SDK 0.2.0 + batch CLI tooling.** `BotContext.Slot`
    ships as a guest-side change only — the init line always carried the slot,
    so the wire protocol is untouched and 0.1 champion artifacts keep running
    (SdkVersion + GuestAdapterVersion → 0.2.0; builtin artifact rebuilt;
    champions never rebuild by design, #43). Event semantics are now
    documented where they live (SDK XML docs + site rules) instead of adding
    fields — Damage's dealer-slot convention is pinned by replay hashes, so
    clarity beats churn. New CLI surface for the iteration loop the agents
    actually ran: `--seeds` batches, `--swap`, `botarena set` (the ranked
    6-game mirrored format locally — also the GAME-DESIGN balance-harness
    primitive), `replay --summary` (compact digest; states are post-tick by
    convention), `build` drops `<project>/out/bot.wasm`, and `.wasm` opponents
    are labeled by their directory (every champion is a `bot.wasm`). Server:
    `GET /api/bots/{id}/build-status` is the slim polling view.
47. **Game rules 0.2 = seed-spawn variation; energy candidate held back.**
    First balance-harness verdict (scripts/balance-eval.py, champions + gen-2
    bots, fixed seeds, 36 games/arm): spawn variation cut draws 42% → 28% and
    median game length 196 → 151 ticks; adding energy (6 max, 2/shot, +1 per
    3 ticks) cancelled those gains (draws back to 42%) because energy-unaware
    bots run dry mid-attack — it taxes aggression as much as camping until
    bots manage the resource. So `GameRules.Current` → V0_2 (spawns only);
    energy stays fully implemented behind `--rules energy` pending a gen-3
    tournament played under it. Both mechanics are wire-compatible with 0.1
    artifacts (energy is an optional trailing observation field; champions
    never rebuild). Historical rules stay constructible for verification.

48. **Gen-3 verdict: Rampart defends; energy closed as-tuned; experiment
    winners must earn the crown under official rules.** The energy tournament
    (single challenger per the new skill default) produced a dominant
    energy-specialist — Metronome swept Warden 6-0 and drew Rampart 3-3 under
    `0.3-exp-energy` — but under OFFICIAL 0.2 rules it drew Warden and lost
    the Rampart set 2-4: no crown, champion defended. Precedent: a rules
    experiment cannot mint a champion; the title is earned in the game people
    play. Energy's balance case is closed as-tuned (GAME-DESIGN gen-3
    section): unaware bots run dry, aware bots produce 6/6 tick-499 mirror
    stalemates, and fortresses with a health lead become unbreakable. The
    mechanic stays behind its flags as a reference failed candidate.

49. **Rules 0.3 = shot range 8 + lane-safe spawns; strafe and hill held.**
    The basics review (RULES-0.3-DESIGN) built four mechanics behind flags
    and ran the pre-registered 5-arm harness (5 bots, 3 maps incl. new
    crossfire-01, 60 games/arm). Range cap was the only arm passing every
    ship criterion: draws 38% → 22%, median game 153 → 120 ticks,
    eliminations intact — infinite lane suppression was the most indicted
    basic and its removal measured cleanly. Strafe (−6pp draws, +80% game
    length) exhibited the predicted oscillation-dodging and stays behind
    `--rules strafe`. Zone control (−15pp draws, games ×2 with zone-IGNORANT
    bots) stays behind `--rules hill` pending a gen-4 tournament of
    zone-aware bots — its implementation (map-declared zones, Domination,
    zone-first tiebreak, SDK/viewer support) is complete. The combined slate
    underperformed range alone (mechanics dilute each other): ship winners
    individually, not bundles. crossfire-01 joined the ranked pool; the
    champions remain compatible (they simply lose their cross-map lanes).

50. **Hill v2 = exclusive accrual; shared accrual demoted to A/B baseline.**
    The gen-4 trial (single zone-aware challenger, Castellan) exposed shared
    accrual's degenerate equal case: two zone-aware bots co-occupy the 2×2
    zone peacefully and spawn order decides the race — every mirror game was
    a slot-1 Domination at the identical tick (150-146, zero damage dealt).
    Decision: `--rules hill` (0.4-exp-hill2) now accrues only for a SOLE
    active occupant — a contested zone pays nobody, making eviction the
    game; `--rules hill-shared` keeps the old semantics as the harness
    baseline. Aside: the trial also confirmed zone-aware crushes
    zone-ignorant (12-0 server sweep vs both champions, no crown per the
    official-rules ratchet), so the mechanic's real evaluation needs
    aware-vs-aware play — the gen-4 tournament brief now specifies exclusive
    accrual and a bracket of distinct zone doctrines is the recommended
    shape.

51. **Hill v3 adds zone-distance-fair spawns.** Exclusive accrual (#50)
    makes first arrival a real per-game edge, and SpawnVariation had no
    zone-distance term — a seed could legally start one bot beside the hill
    and the other across the map (mirrored sets average it out; individual
    games were still lopsided). Under `ZoneSpawnFairness` (on in
    `--rules hill`, now 0.4-exp-hill3) the two spawns' walking distances to
    the nearest zone tile (4-neighbor BFS, matching orthogonal movement) may
    differ by at most SpawnVariation.ZoneDistanceTolerance = 2 steps.
    Viewer-side, the FOV overlay was also made honest the same day: enemies
    outside the selected bot's sight ghost to 15% instead of rendering at
    full strength (the panel answers "what did this bot know?").

52. **Gen-4 verdict: Talon wins the hill bracket; hill v3 behaves as
    designed; no crown moves.** Two-challenger bracket + the trial's
    Castellan, one improvement iteration, all under 0.4-exp-hill3 —
    experiment rules, so the official-rules ratchet (#43) keeps Rampart
    gen-2 champion regardless of the table. Talon (zone-denial tempo) won
    every head-to-head in both rounds (3.5-2.5, 3.5-2.5 vs Bastille;
    3.5-2.5, 4-2 vs Castellan) and finished top of the ladder at 1258.
    Balance read: aware-vs-aware draws fell 6/18 → 3/18 across the
    iteration — counter-play (Talon's sorties broke all four Castellan
    freeze-draws; Bastille's bait-refusal converted exploit losses to
    trades) fixed what rule-tuning didn't have to. All 72 champion games
    were decisive (zone-ignorant bots lose ~t151-166 by Domination —
    shipping hill officially would obsolete every pre-zone bot; that meta
    reset, not the mechanic, is the open ship question). Emergent depth on
    record: dodge-bait accrual exploits, contested-inference from frozen
    public counters, timed sorties — the strategic variety the mechanic was
    hypothesized to create. Ship decision deferred to a criteria'd harness
    run + owner call (GAME-DESIGN).

53. **Rules 0.4 shipped: zone control is official.** The gen-4 experiment
    graduated on the pre-registered harness run (5 bots: both champions +
    3 zone-aware doctrines, 3 maps, fixed seeds, 60 games/arm):

    | arm  | draw% | decisive% | med tick | notes                          |
    | ---- | ----- | --------- | -------- | ------------------------------ |
    | 0.3  | 37%   | 63%       | 77       | aware bots are strong duelists |
    | hill | 12%   | 88%       | 158      | 0 draws in 36 aware-vs-champ   |

    Draws and decisiveness pass decisively; diversity holds (all three
    doctrines win aware-vs-aware games: 7/4/3). **Median length doubled and
    literal Eliminations fell (Domination replaces them) — recorded as the
    accepted trade**, not hidden: four generations showed draws are the
    product's disease, and a long decided game beats a fast dead one.
    V0_4 = V0_3 + ZoneControl + ZoneDominationTicks 150 + exclusive accrual
    + zone-fair spawns; `Current`/`GameRulesVersion` → 0.4; "hill" stays as
    a Resolve alias; hill-shared remains the A/B baseline. Consequences
    accepted openly: this is a meta reset — every pre-zone bot (both
    champions included) loses to any zone-aware bot, so gen-5 challengers
    can dethrone Rampart legitimately under official rules; pre-launch is
    the cheapest this reset will ever be. Player docs now carry the full
    zone spec (site rules card + template README), enforced by
    DocDriftTests' version stamps.

54. **One elo ladder per rules version.** Owner call after 0.4 shipped: a
    rules era change must not vaporize a bot's standing, and old rulesets
    stay playable. `BotRating(BotId, RulesVersion, Rating, RankedSets)`
    replaces the bot-level rating; a ranked request may pin any
    `GameRules.Resolve` name (default = the server's ruleset) and the set's
    elo moves the ladder of the rules its games actually played —
    experiments therefore rate on their own ladders instead of polluting
    official elo (fixes the era-mixing that put tournament-arm results into
    one number through gen-4). Pre-#54 ratings migrate to the "0.3" ladder
    as the legacy record (closest official era; they were earned across
    0.2/0.3/experiment play); the 0.4 ladder starts fresh. Verified live:
    the same pairing produced Talon 6-0 rampart-gen2 on the 0.4 ladder and
    rampart-gen2 4.5-1.5 Talon pinned to 0.3 — the legacy champion is still
    the better duelist in its own era, which is precisely the point.
    Matchmaking stays a single queue; ladders partition ratings, not
    players.

55. **Gen-5 verdict: the duel era ends — Bastille gen-5 is the first
    zone-control champion.** Season premiere under official 0.4 (new
    challenger Meridian + the three gen-4 veterans as ladder opposition,
    one improvement iteration, 6-map pool with 3-of-6 sampling). Final 0.4
    ladder: Bastille 1279, Talon 1268, Castellan 1244, Meridian 1219,
    Warden gen-1 1101, Rampart gen-2 1089 — every agent finished above
    both duel-era champions (48-0 across two rounds), so per the ratchet
    (#43) the #1 non-champion is crowned: champions/bastille-gen5.
    Asterisk for the record books: Talon beat Bastille head-to-head in
    BOTH rounds (3.5-2.5 twice) but lost the league on its other results —
    crowning follows the final leaderboard, as written. The new challenger
    finishing last among agents is itself a finding: the gen-4 veterans'
    doctrines survived a fresh, well-briefed opponent with an iteration —
    0.4's meta has real depth, not a single dominant trick. Dethroned
    champions remain in champions/ per the ladder-of-history rule.

56. **Rules 0.5 slate implemented behind flags: cone vision + projectiles
    (RULES-0.5-DESIGN).** Arms `cone` (90° facing quadrant + Chebyshev-1
    proximity ring + hearing radius 8 for loud events), `bolts`
    (projectiles: spawn adjacent, advance 1 tile/2 ticks, tile-lethal,
    owner-immune, point-blank instant), `conebolts` (both); all carry
    SpawnAttempts 256 (fixes the gen-5 fairness-fallback finding, rules-
    gated). Zero wire change — trailing `P` observation section; SDK/
    GuestAdapter 0.5.0; rules 0.1-0.4 bit-identical (full suite green,
    110 tests). The executable plays are ENGINE TESTS: Backstab connects
    unseen, a tile-holding camper is hit by a bolt, and the documented
    counter-surf limit holds exactly (PlayAcceptanceTests) — the spec's
    promises are pinned, not prose. Mechanical harness (gen-5 population,
    0.5-blind, 60 games/arm, fixed seeds):

    | arm       | draw% | elims | med tick | avg tick |
    | --------- | ----- | ----- | -------- | -------- |
    | 0.4 ctrl  | 7%    | 13    | 160      | 260      |
    | cone      | 10%   | 19    | 160      | 208      |
    | bolts     | 12%   | 20    | 157      | 217      |
    | conebolts | **3%**| 17    | 159      | 217      |

    Nothing breaks with unaware bots; combat is already deadlier (unseen
    approaches land, old dodge models mistime bolts); the COMBO lowers
    draws below every arm in recorded history while each mechanic alone
    raises them slightly — first evidence for the pairing hypothesis.
    Ship decision awaits the real verdict: a gen-6 tournament of bots
    written FOR 0.5 (owner drives it on a different model), per the
    0.3/0.4 pattern.

57. **Gen-6 verdict (round 1, stopped early): 0.5 conebolts is a
    gameplay success but NOT docs-ready — ships only after a docs + viewer
    pass.** Two challengers written for 0.5 (Nightjar scout-flanker,
    Ballista suppression-zoner), one round on the 0.5-exp-conebolts ladder
    before the owner called it (enough signal). No crown moves (experiment
    rules; Bastille gen-5 defends). Ladder: Nightjar 1233, Ballista 1229,
    Bastille gen-5 1200, Rampart 1170, Warden 1169.
    GAMEPLAY = success: a clean rock-paper-scissors formed (Ballista beats
    Nightjar 4.5-1.5 with bolt-walls; Nightjar beats the camper Bastille
    5-1 with ranged blind-arc eviction; Bastille beats Ballista 4.5-1.5 by
    out-shuffling suppression), both new bots swept the 0.5-blind Warden/
    Rampart 6-0, and every aware-vs-aware head-to-head was a DECISIVE
    elimination with real damage landing (e.g. Ballista kills Nightjar on
    crossfire, hits t41/t47/t50) — versus the identical-bot mirror's 74
    shots / 0 damage, proving directional vision + differing doctrine is
    what makes shots land. Executable plays observed unprompted: Ballista's
    Shepherd (bolt-wall reroute flipping a 36-150 loss to a 20-9 win) and
    5-bolt picket-fence; Nightjar's ranged blind-arc kill (t9/t21/t28,
    zero taken) and closing-enemy interception counter-fire (t28-29).
    DOCS = fail for a docs-only player: BOTH challengers independently
    reverse-engineered the cone predicate, the bolt 2-tick-per-tile
    occupancy, the point-blank-instant vs ranged-slow-bolt split, and the
    split-zone "contested pays nobody (even across pads)" rule — all
    experiment-only — and `replay --summary` shows NEITHER cone contents
    nor bolt state, so decisions can only be debugged via Debug.Write.
    Both said a docs-only player designs the wrong bot first try.
    SHIP DECISION: 0.5 held behind its flags until (a) the player rules
    card is rewritten for point-blank-instant, no-strafe 2-tick dodging,
    the exact cone predicate, bolt occupancy timing, and split-zone
    contest; (b) the viewer already shows cones (done mid-run) and now
    also renders bolts, but `replay --summary` must gain cone/bolt columns.
    Pre-ship task list recorded in GAME-DESIGN + DX-FINDINGS-GEN6.

58. **External review adopted: 0.5 stays experimental until the hardening
    program lands (RULES-0.5-DESIGN §H).** Sol's review of the 0.5 design +
    implementation, point-by-point verified against the code and accepted:
    (1) hearing is currently radar — `IsLoud` delivers full authoritative
    GameEvents within radius 8, a tracking feed; redact to
    HeardSound(Type, octant bearing, near/medium/far band); (2) observed
    bolts hide phase/remaining-range (dodge timing must be computable, not
    measured) and the phase-surfing collision edge is real (occupancy
    checked post-advance only) — fix is check-before-AND-after, under
    which the §C counter-surf conclusion still holds (pinned by test);
    (3) the spawn fix was incomplete (silent unfair fallback survives at
    256 attempts) AND the harness confounded spawn-sampler with mechanics
    — landed now: a spawn-matched **0.5-control** arm (0.4 +
    SpawnAttempts 256); batch: exhaustive deterministic pair enumeration,
    empty set = map rejected loudly; (4) play tests prove mechanics vs
    passive defenders only — adversarial tests (optimal 4-tick scanner et
    al.) required. Ten ship criteria pre-registered in §H, frozen before
    gen-7. All gameplay-affecting fixes ship as ONE hardening batch with
    a single version-string bump per arm (hill v1→v3 precedent). §C
    timing prose corrected (bolt spawns adjacent at the shot tick — the
    t+2 phrasing was a prose error, implementation was always right).
    Sequencing: hardening batch → gen-6 DX docs pass → gen-7 aware
    tournament = the official-0.5 decision.

59. **The 0.5 hardening batch shipped as arm revision v2 — one version
    bump for every gameplay-affecting fix (§H program, one commit).**
    All arms move to `-v2` strings (0.5-exp-control-v2 / cone-v2 /
    bolts-v2 / conebolts-v2) plus the new `conebolts1` arm
    (0.5-exp-conebolts1-v2, bolts at movement speed). In the batch:
    (1) hearing redacted to HeardSound(type, 8-way octant at >2:1
    dominance, near≤2/medium≤5/far bands) — sighted events stay full,
    heard-only events never carry coordinates; (2) bolt observations and
    replays carry ticksUntilAdvance + remainingTiles (P section 4→6
    fields; additive H section) and occupancy is checked before AND
    after each advance, closing the phase-surfing gap while the §C
    counter-surf survives (both pinned by tests); (3) ExhaustiveSpawns
    replaces sampling in every arm — full valid-pair enumeration,
    seed-picked, empty set rejects the map loudly, every shipped map
    gated by test; (4) AdversarialPlayTests prove the plays against the
    strongest SCRIPTED defense (optimal 4-tick scanner, perfect
    timing-aware dodging camper): open-field stealth is impossible
    (detection theorem), cover-timed backstab kills the scanner unseen,
    bolts+body denies the perfect dodger, the gen-5 fortress freeze
    breaks. Also: per-tick zone tallies in replays (ReplayZoneTallies —
    the viewer reads, never re-derives; flag-gated so official 0.4
    bytes are untouched), paired per-game analysis in balance-eval.py,
    SDK/GuestAdapter 0.6.0. The v1 arm strings are retired without a
    compat shim: experiments make no bit-compat promise and gen-6
    artifacts cannot parse the widened P section anyway — their ladders
    and replays stay as history, re-verification is lost, accepted.
    Deliberately NOT pinned by script: shooting/adaptive defenders
    (armed door-watcher, Shepherd follow-up) — that is gen-7's job
    under the frozen §H ship criteria.

60. **Follow-up review adopted: arm revision v3 — shared seed profile,
    de-louded disqualification, actor-gated event visibility under the
    cone (RULES-0.5-DESIGN §I).** The important one: the paired harness
    was not actually paired — spawn seeds (and, found while fixing,
    per-bot RNG streams) derive from RulesVersion, which differs per
    arm, so "the same game under two rule sets" had different spawn
    geometry and different bot dice. New `GameRules.SeedProfile`
    (null = RulesVersion) feeds both derivations; all v3 arms share
    `0.5-exp-shared`, giving the strongest pairing property: a
    mechanics-blind bot plays the bit-identical game under every arm
    (pinned by HardenedArms_ShareSpawnsAndBotStreams across all shipped
    maps). The v2 mechanical-harness paired table is VOID and re-run.
    Also: Disqualified leaves the loud list (no world position, match
    ends same tick — a sound no decision can use); and under VisionCone
    an event is fully visible only when its PRIMARY position (the
    actor) is seen — a ray's endpoint no longer reveals an unseen
    shooter's tile and slot; such events degrade to sounds at the
    muzzle. Omnidirectional rules keep the any-reference rule
    bit-identically. Legacy versions 0.1-0.4 untouched throughout.

61. **Gen-7 aware verdict: HOLD conebolts — cone+hearing validated, bolts do
    NOT solve the camper (RULES-0.5-DESIGN §H criteria; GAME-DESIGN gen-7).**
    Two docs-built 0.5-aware challengers (Bloodhound sound-hunter, Bulwark
    armed door-warden) on the hardened conebolts-v3 arm, one improvement
    iteration each with the anti-camper Double-Lane Squeeze spelled out.
    Final ladder: Bastille gen-5 (0.5-BLIND 0.4 champ) 1241 #1 over Bloodhound
    1236 and Bulwark 1235, both far above the blind controls Warden/Rampart
    1144. Aware bots swept the blind champions 6-0 (cone/bolts/hearing are a
    large real edge over bots that can't perceive them) and produced fast
    decisive aware-vs-aware kills (t17), and hearing behaved as designed
    (Bloodhound never fires from sound alone, converts to a sighting, diagonal
    bearings ambiguous — search not tracking). BUT the 0.5-blind champion beat
    BOTH aware bots even after the counter was handed to them, because on a
    2x2 zone Bastille plays a reactive diagonal-mirror that a single gun under
    no-strafe cannot pin (double-lane physically impossible at cooldown 2; the
    bolt always lands on the tile it just left; no way to force a trade). The
    effective counter was zone-turtle (a 0.4 mechanic — contested pays nobody),
    not the new bolts; it pulled Bastille to a spawn-decided coin-flip
    (2W-3L-1D), every decisive 2x2 game a MaxTicks zone-race not a kill. Split
    1x2 pads (arena-01) are indefensible geometry the mechanics can't fix.
    Scorecard: PASS #4/#5/#7/#9, PARTIAL #1/#3/#6, FAIL #2 (Radar Statue not
    broken in the decisive ranked replay), #8 (fortress/mirror unbreakable),
    #10 (bolts don't individually justify complexity; cone+hearing do). The
    three fails are all the anti-camp promise that motivated bolts. DECISION:
    do not promote conebolts to official 0.5 as-is; hold experimental. Forward
    levers (agent-identified): ship cone+hearing without bolts; or redesign
    bolts to threaten the mirror (second simultaneous lane, longer occupancy,
    or limited strafe — strafe risks the dodge-everything regression 0.5
    removed); or fix zone geometry. No crown (experiment; Bastille defends).
    Direction pending owner call. Tooling note: this tournament survived TWO
    mid-run environment recycles (agents resumed from transcript, work intact),
    validating the babysit protocol; a tournament-drive state.json key
    tolerance fix rode along.

62. **Gen-7 redesign implemented as experimental revision v4: active
    control pressure + ordered fast bolts + ranked-zone cleanup.** The
    rejected premise was not cone/hearing but the reward loop: Bastille
    could spend every tick dodging while permanently banking full zone
    progress. New `ActiveZoneControl` arms require a successful validated
    Wait on-zone to exert control; Move/Turn/Shoot/blocked/faulted actions
    do not. Banked per-bot ticks are replaced by one signed meter
    (slot 0 positive, slot 1 negative): sole holder gains 1, two holders
    freeze it, no holder decays it 1 every 2 ticks, ±100 dominates, and
    MaxTicks uses pressure → health → damage. Projectiles gain
    `ProjectileTilesPerAdvance`; v4 tests 1 and 2 tiles every tick with
    ordered substep wall/bot/range checks, adjacent-only launch on the
    firing tick, and replay traversals for continuous A→B→C viewer
    animation. New paired arms: `control`, `cone-control`, `cone-active`,
    `cone-active-bolt1`, `cone-active-bolt2`, all on seed profile
    `0.5-redesign-shared`; old v3 arms remain reproducible. SDK/
    GuestAdapter 0.7.0 adds the trailing control section and widens P
    6→7 fields. Ranked zones on basic/arena/crossfire/bastion/gallery
    become connected 3×3 or 3×2 regions; narrow `causeway-01` remains an
    adversarial map but leaves ranked. Trails/residue/strafe/spread stay
    deferred so the scoring and speed deltas remain attributable.

63. **WASM developer builds select the compiler host, not the target.**
    The pinned NativeAOT-LLVM release provides only a Linux x64 compiler-host
    package; the emitted WASI p1 module remains architecture-neutral. Linux x64
    with wasi-sdk builds natively. macOS (Intel or Apple Silicon) and Linux
    arm64 automatically publish inside a focused cached `linux/amd64` image
    (`docker/wasm-builder.Dockerfile`) rather than the full application image.
    `scripts/run-wasm-publish.sh` is the single platform boundary used by the
    built-in guest and `BotBuilder`; `doctor` reports the selected backend.
    Docker compiler/NuGet state persists under `~/.cache/nilbots-wasm`, player
    artifacts retain their content-addressed `~/.botarena/cache`, and the
    built-in guest has a source-input stamp so unchanged test runs do not invoke
    NativeAOT. `ToolchainInfo.BuildPipelineVersion` joins the player cache key
    for compiler-invocation changes that alter bytes without changing the SDK.
    A fixed `/src` path map removes checkout-location variance.
    `scripts/test.sh` still calls that builder every time, closing
    the stale tracked-artifact footgun without charging an unchanged rebuild.
    The native in-process runtime remains the recommended strategy inner loop;
    WASM is the required verification/submission boundary.

64. **Gen-8 revision-v4 verdict: HOLD official 0.5; bolt2 wins the
    experimental speed comparison but misses the duration/elimination gate.**
    Four isolated docs/CLI-only authors built an active holder, suppressor,
    sound hunter, and mobile flanker, then received one loss-forensics
    iteration. Their final WASM artifacts fought unchanged Bastille across
    three paired seed profiles: 180 games per arm, 900 final games total.
    Active control fixes the accounting problem, and projectiles finally
    change the champion equilibrium: control leaves Bastille 61-0-11 and
    active/no-bolt leaves it first at 50-7-15, while bolt2 puts ActiveHolder
    first at 51-13-8 over Bastille 45-16-11 and Suppressor 31-35-6. Bolt2
    also beats bolt1 across the combined 180-game speed sample: identical
    15 draws, 97 vs 92 eliminations, median 71 vs 86.5, average 134.9 vs
    139.1, and 291 vs 273 ranged hits. It is the retained flagship.
    However, versus the matched control population, bolt2 lowers draws
    11.7%→8.3% but also lowers elimination share 62.2%→53.9% and lengthens
    median/average games 43.5/101.1→71/134.9; 24/180 reach MaxTicks and
    most finish near zero pressure. The balance-harness gate requires draws
    down, duration down, elimination share up, and diversity retained; two
    of four fail. Therefore 0.4 remains official, no experiment champion is
    crowned, and v4 stays reproducible behind its arm names. The next
    experiment must isolate the near-zero-pressure late-game stalemate
    rather than strengthen bolts, change cone/hearing, or merely lower the
    control limit (which cannot resolve pressure already near zero).

65. **Gen-8 revision-v5 control overtime is retained experimentally; official
    0.5 remains HOLD.** Audit of all 180 bolt2-v4 games found 24 MaxTicks,
    including 15 instances of one exact holder/suppressor cycle: per final
    100 ticks, 40 vs 20 sole holds, 40 defensive no-holder ticks, 20 missed
    launches, and normal decay cancelling the net 20-hold advantage. The
    pre-registered isolation arm freezes every v4 mechanic through tick 199;
    at tick 200 it carries signed pressure into a ±10 overtime and stops
    nobody-holding decay. Same five unchanged WASM bots, maps, seed profile,
    and three seed sets, 180 paired games. Result: MaxTicks 24→5 and average
    134.9→102.0 while draws stay 15, eliminations stay 97, median stays 71,
    and ActiveHolder stays first at 51 wins. Nineteen MaxTicks become
    Domination, seven late Dominations shorten, saving 5,940 total ticks;
    overtime winners split 14 slot 0 / 12 slot 1. Doctrine order is stable.
    One winner flips (Bastille vs Suppressor, Crossfire seed 303) because net
    active holding replaces the old tick-limit tiebreak. RETAIN
    `cone-active-bolt2-overtime`
    (`0.5-exp-cone-active-bolt2-overtime-v5`) as the experimental flagship.
    Do not ship 0.5: versus matched control, median is still 71 vs 43.5 and
    elimination share 53.9% vs 62.2% (average 102.0 vs 101.1, draws 8.3% vs
    11.7%). The next decision is either a design that restores combat tempo
    or an explicit, evidence-backed redefinition of decisive Domination as
    satisfying the tempo gate; the gate is not silently waived.

66. **Revision-v6 doubled overtime gain supersedes v5 stopped decay; official
    0.5 remains HOLD.** Structural review caught that v5 violated the original
    no-banked-lead criterion: once overtime began, abandoning control could
    preserve pressure forever. The pre-registered replacement keeps the
    tick-200 ±10 target but retains normal one-per-two-ticks decay and doubles
    sole-holder gain to 2 during overtime. Same unchanged five-bot, 180-game
    paired field. v6 exactly preserves v5's 15 draws, 97 eliminations, median
    71, five MaxTicks, all five W-L-D records, doctrine order, and 14/12
    overtime winner slot split. Its average is 102.2 versus v5's 102.0;
    22 games change finishing tick, costing only 45 aggregate ticks, with no
    winner flips. A scripted test proves abandoned overtime pressure still
    decays. RETAIN `cone-active-bolt2-overtime-gain`
    (`0.5-exp-cone-active-bolt2-overtime-gain-v6`) as the flagship; keep v5
    resolvable only as the causal reference. Do not promote official 0.5:
    median duration and elimination share still miss matched control.

67. **Privately programmed arcs pass the pre-implementation theory gate;
    select one immediate launch tile.** A reusable finite combat lab modeled
    219 arc parameter combinations (125 distinct open paths), all defender
    Wait/Turn/Move policies, gradual path-prefix revelation, strict diagonal
    corners, range eight, and speed two. Complete-path knowledge makes every
    individual shot dodgeable in all 84 open distance-two-to-four states, but
    private immutable intent removes a universal defence in 53/84: the desired
    prediction game exists without random accuracy or homing. A two-tile
    immediate launch creates 12/84 forced known-path attacks, so use one tile
    instead (zero forced open attacks). The full 10,240-state ranked-zone sweep
    finds 3,552 prediction contests, only 201 forced attacks (2.0%, caused by
    constrained geometry), and valid around-wall paths on every map. PASS the
    theory gate and authorize an in-process engine experiment. Do not promote
    rules or pay the SDK/WASM migration until engine-scripted tests preserve
    these properties and establish a usable player action contract.

68. **Programmed arcs pass the v7 engine/SDK/WASM usability gate; retain them
    as the experimental flagship, while official 0.5 remains HOLD.** The
    `cone-active-bolt2-arcs` arm freezes v6 overtime-gain and adds only a
    private immutable 125-path action family: one launch tile, speed-two
    ordered travel, strict diagonal corners, gradual current-heading reveal.
    SDK/Guest 0.8 adds nullable limits, `Actions.Shoot(ShotProgram)`, exact
    eight-way current headings, and an SDK preview; protocol 0.1 remains
    additive and old artifacts ignore trailing sections. Engine/SDK preview
    parity, no-tunnelling, invalid payload, replay privacy, WASM/in-process
    replay-hash parity, and an all-WASM legacy-artifact set pass. In 180 paired
    v6/v7 games, draws improve 15→11, eliminations 97→106, median 71→61,
    average 102.2→87.2, and MaxTicks 5→3. Suppressor uses 918 programs:
    821 visibly bend, 110 hits land after a bend, 111 curved hits are ranged,
    and five misses cross an active-holder tile just vacated. Suppressor wins
    rise 30→43 while all five doctrines still win. RETAIN v7 as the next
    experimental flagship and v6 as its straight-bolt causal reference. This
    proves usable skill shots, not full 0.5 promotion against official 0.4 or
    completion of the ranked-map geometry gate.

69. **The isolated gen-9 player trial passes v7 learnability and the ranked-map
    geometry gate; official 0.5 remains HOLD for the matched 0.4 comparison.**
    One fresh docs/SDK/CLI-only author built Helix without engine, runtime,
    toolchain, design, agent-guide, or raw-replay access. Its final WASM was
    byte-identical locally/server-side and scored 4–2 over Bastille, 4–2 over
    Rampart, and 5–1 over Warden; a final self mirror drew 3–3 with all six
    games ending by elimination in 11–31 ticks. Helix independently combined
    active Wait holding, terrain memory, redacted-sound search, private
    program enumeration/preview, movement prediction, and conservative
    speed-two bolt evasion with zero faults. This proves the public action and
    observation contract is learnable and unchanged Bastille's mirror is no
    longer self-sufficient. The ranked pool now also has executable geometry
    constraints (connected ≥4-tile regions, horizontal/vertical local movement,
    ≥2 approaches, surrounding attack space), excludes the adversarial 2×2
    causeway, and the trial covered all five ranked maps. No crown: one
    challenger against historical rules-blind bots is a usability gate, not
    the full promotion population. Next run the matched v7-versus-shipped-0.4
    tournament; do not add another combat mechanic first.

70. **Build caches serialize across processes, and timed-out Docker compilers
    are explicitly reaped.** A gen-9 final build exposed that `BuildLocks`
    protected only threads: a CLI build and server submission of identical
    source shared one cache workspace concurrently. On Docker Desktop, the
    five-minute timeout killed the local `docker run` client but left the
    daemon-owned Linux x64 compiler alive, causing subsequent empty-log stalls.
    Each cache key now also holds an OS file lock; waiters consume the completed
    artifact. Docker publishes receive unique `botarena-wasm-*` names and the
    timeout path forcibly removes the exact container before killing the
    client. In-process player MSBuilds likewise serialize per project so seed
    batches cannot race in `bin/obj`. Verification on Apple Silicon: the
    formerly blocked Helix build completed in 16.3s; two simultaneous fresh
    identical builds produced one 14.1s compile plus one cache hit with matching
    hashes and no remaining compiler container.

71. **The 810-game all-WASM promotion run keeps official 0.5 on HOLD; the
    next choice is the product gate, not another mechanic.** Six frozen
    doctrines played 270 games each under shipped 0.4, a shared-seed passive
    control, and programmed-arcs v7, with zero faults. Versus 0.4, v7 cuts
    draws 31→17, average ticks 120.1→99.0, p90 499→200, and leader share
    34.3%→26.5%; damage occurs in more games (70.0% vs 67.8%), all six
    doctrines win, Helix leads 67-21-2, and unchanged Bastille falls from an
    undefeated 82-0-8 to 49-29-12. The skill-shot layer is material: 1,380
    curved launches, 224 ranged curved hits across 95 games, and 36 vacated
    active-holder crossings. But the pre-registered standard gate is binding:
    median rises 31.5→64.5 and elimination share falls 64.8%→55.9%. The exact
    control→v7 rows also create three net draws (14→17), despite five more
    eliminations and a 36.6-tick lower mean. Therefore 0.4 remains official
    and v7 remains the experimental flagship. Do not tune this frozen result.
    If decisive Domination and viewing-time tails are to replace elimination
    label and instant-ray median as promotion criteria, pre-register that as
    an explicit product gate and use fresh holdout seeds while continuing to
    report the original balance table.

72. **Revision-v8 replaces the Wait tax with territorial sole-occupancy
    scoring; the economy works, but its first retention gates remain a HOLD.**
    `cone-occupancy-bolt2-arcs`
    (`0.5-exp-cone-occupancy-bolt2-arcs-v8`) is bit-identical to programmed
    arcs v7 except that exactly one active zone occupant scores with any action
    and contested/empty zones decay. Scripted gates and 810 all-WASM games
    passed with zero faults. The frozen Wait-trained population failed the
    pre-registered v7 table (draws 17→22, average 99.0→119.1, MaxTicks 14→35)
    and used Wait for 84.2% of score ticks. Two docs-only adaptations then
    proved the intended economy: in 270 v8 games, 2,080/3,911 score ticks used
    Move/Shoot/Turn, all 189 contest-to-sole transitions scored immediately,
    162 coincided with damage eviction, damage occurred in 91.1% of games,
    reciprocal damage in 61.5%, and eliminations in 81.1%. Bastille fell to
    30-47-13 and all six doctrines won. The adapted arm still failed its frozen
    duration tail: 30 MaxTicks, all ending at zero pressure after 100-tick
    no-damage physical contests. Retain v8 as a reproducible experiment; do not
    hide those loops with a post-hoc match cap. Its next verdict requires a
    fresh, larger territorial-native cohort.

73. **Substantial rules are judged by native generations, dynamics, and blind
    replay study—not vetoed by old rules-unaware bots or optimized for minimum
    duration.** Frozen historical artifacts remain mandatory compatibility,
    determinism, and exploit sentinels; same-cohort fixed-seed A/B remains the
    mechanic-causality instrument. Primary product evidence now requires at
    least four independently authored/adapted doctrines under the rules they
    were built for, compared as a product generation with the prior native
    cohort under its prior rules. The shared scorecard reports damage tempo,
    reciprocal/multi-tick exchanges, active/stagnant/repeated frames, action
    entropy/runs, objective evictions, outcome diversity, faults, and
    median/p90 as viewing guardrails. At least 12 header-only, map/pair-balanced
    replays are selected before aggregate outcomes and watched at normal speed;
    highlights are separate. `docs/EVALUATION-METHODOLOGY.md`, both shared
    repository skills, `balance-eval.py`, `replay-dynamics-eval.py`, and
    `replay-review-sample.py` encode the policy. Completed historical gates are
    not retroactively rescored; the thresholds apply to fresh holdouts.

74. **Territorial v8 passes its dynamics and blind-viewer gates but remains on
    HOLD because Pincer violates the frozen diversity ceiling.** The
    pre-registered native cohort ran 108 all-WASM games with zero faults and
    reproduced every replay hash. Draws were 1.9%; damage appeared in 100%,
    reciprocal damage in 78.7%, multiple damage ticks in 100%, active-world
    ticks in 100%, and no game stalled or looped. Median action entropy was
    0.728; 1,006/1,030 sole-score ticks used non-Wait actions; 87 contests
    broke to a sole occupant and 51 coincided with damage. The 12-replay blind
    sample passed, averaging 4.33/5 for visible action/counter-action with all
    12 at least 3, and only one repetition score at 2. However, Pincer finished
    45-9-0 and owned 45/106 (42.5%) decided wins, above the pre-registered 35%
    maximum. Do not relax that gate or tune the rules post hoc. Keep v8 and
    Pincer frozen; the next experiment is an equal-budget, fresh-seed
    counterplay adaptation trial pre-registered in RULES-0.5-DESIGN §R.
    `docs/PLAYER-GUIDE.md` becomes the one player-facing
    experiment brief, and the shared skills preserve this native-cohort →
    blind review → strict verdict workflow.

75. **The product owner accepts a 42.5% champion share, sets the future
    diversity ceiling to 45%, promotes frozen territorial v8 as official 0.5,
    and crowns Pincer gen-10.** This does not rewrite decision #74: the
    completed holdout failed its pre-registered 35% gate. It is a subsequent
    product-policy override based on the judgment that 45/106 decided wins is
    strong but healthy in a four-doctrine field where every doctrine won.
    Every safety and watchability gate passed: 108/108 all-WASM games
    reproduced with zero faults; draws were 1.9%; damage, multi-damage-tick,
    and active-world incidence were 100%; reciprocal damage was 78.7%; there
    were no stalled or looped games; median/p90 were 23/41; the blind viewer
    study passed. `GameRules.V0_5` is mechanically identical to
    `0.5-exp-cone-occupancy-bolt2-arcs-v8` except for the official version
    string, retains the shared seed profile, and becomes `Current`.
    `pincer-gen10` preserves the exact
    `0c0271655d25e6b91d520b2f0d55acdefaabd3e205646fff6b98a82b4c1e5abd`
    artifact and source. The §R counterplay holdout is superseded before any
    of its fresh seeds were opened; it remains documented as the road not
    taken. Future substantial-rule native cohorts use a 45% leader ceiling,
    while safety, deterministic verification, and replay integrity remain
    non-overridable hard gates.

76. **Deployment scales by explicit modular-monolith roles, stable object keys,
    and measured promotion gates—not by introducing distributed architecture
    early.** `BotArena.App` now runs as `web`, `compile-worker`,
    `match-worker`, one-shot `migrate`, or local-only `all`; all roles share
    PostgreSQL and the same domain model. The PostgreSQL job queue remains the
    coordinator. Artifacts and replays use stable keys through `IObjectStore`,
    starting on a local persistent volume and requiring an S3-compatible
    backend before workers span VPSs. Production uses Caddy, commit-tagged
    Docker Compose images, provisioned OpenIddict certificates, PostgreSQL
    Data Protection keys, and exactly one global match worker until ranked-set
    finalization is transactionally safe under concurrency. Compiler workers
    may scale first, but public untrusted submissions remain gated on vendored
    build inputs, disabled outbound build networking, and hostile-resource
    tests. No broker, microservices, Kubernetes, shared network filesystem, or
    database cluster is introduced without measurements that justify it.

77. **The first internet launch is a bounded public beta with manual releases
    and a same-VPS, networkless compiler runner.** GitHub Actions runs only
    through `workflow_dispatch`; `verify`, `publish`, and
    `publish-and-deploy` make free-tier usage intentional. Releases produce
    separate runtime and compiler images in GHCR, tagged by the full Git SHA,
    deployed by digest, and accompanied by SBOM and GitHub provenance
    attestations. The networked compile role is a database/object coordinator;
    an unprivileged sidecar consumes a filesystem queue with no network,
    application secrets, or Docker socket and with read-only/cgroup/tmpfs/
    process/file limits. PostgreSQL admission locks make account, hashed
    network, account-queue, and global-queue compiler quotas durable across web
    replicas; network pseudonyms use a server-secret HMAC rather than a
    reversible plain IPv4 hash. Successful output must pass a WASM
    import/export/memory contract check before storage, after which its hash,
    build receipt, and artifact are public while source and logs remain
    private. Registration is public from launch; a dedicated compiler VPS and
    external S3-compatible objects remain measured scaling/defense-in-depth
    promotions, not prerequisites.

78. **Production object storage is a private Garage cluster behind the generic
    S3 contract, bootstrapped at replication factor 3 in Compose.** The first
    VPS runs three storage containers plus one gateway, all in the same real
    HostUp zone. This deliberately does not claim host-level availability; the
    co-located copies preserve Garage's replication factor so future nodes can
    join over a private overlay network and rebalance without the dangerous,
    unsupported replication-factor change from 1 to 3. The application uses
    `AWSSDK.S3` only and remains replaceable across compatible servers.
    Garage is pinned to the v2.3.0 multi-architecture image digest, its S3/RPC/
    admin ports are not published, and bucket-scoped credentials remain in the
    host environment. The old local volume stays mounted during the first
    release as a migration source and immediate rollback aid; verified S3
    materialization, Garage metadata snapshots, HostUp backups, and an
    independent restore rehearsal remain required because co-location is not
    backup or physical redundancy.

*Numbering note: entries 79-84 were originally appended as 70-75, colliding with
the already-numbered 70-78 above; 93 and 94 were likewise first written as 85 and
86, which Codex's visual-asset entries had already taken. Both sets are
renumbered here and every citation of them in the repo was updated with them;
commit messages predating this note may still cite the old numbers. Two agents
appending to one numbered log will keep doing this — check the tail of the file
before picking a number.*

79. **Inert player API is hidden, not deleted (DX-FINDINGS-NUGET-PLAYER).**
    The first evaluation against the PUBLISHED product — an agent with only
    `dotnet tool install --global Nilbots`, nilbots.com/docs, and no repo access
    — built a bot that goes 97W-6L-17D vs `hunter`, and confirmed the shipped
    rules and the public docs agree (no drift; the gen-6 failure is fixed).
    Its sharpest finding reproduced gen-7's independently: `Actions.Strafe*`
    and `BotContext.Energy` are public, undocumented, and inert, degrading to
    Wait/`Blocked` — which players misread as a blocked move. Decision: mark
    them `[Obsolete]` + `[EditorBrowsable(Never)]` with honest doc comments
    rather than delete them. Deleting was considered and rejected because
    strafe is a live design lever (DECISIONS #61 names it as a candidate answer
    to the 2x2 diagonal-mirror camper), the enum values are wire values present
    in historical replays and champion artifacts, and the research arms must
    stay runnable. Also fixed: authenticated commands crashed with CI build
    paths in the trace whenever their server was unreachable (root cause was
    the pre-command token refresh, so it hit `submit` too, not just `whoami`);
    `--version` printed help; the SDK shipped no XML docs, leaving the entire
    player API blank in IntelliSense. SDK 0.8.0 -> 0.8.1 (compile-surface
    change, no wire change). Local<->server artifact parity remains UNVERIFIED
    — registration against production was blocked by tooling boundaries.

80. **Headless onboarding ships; local<->server artifact parity is BROKEN and the
    cause is embedded build paths (DX-FINDINGS-NUGET-PLAYER).** Goal: point a
    friend's agent at the game and have it participate unaided. The blocker was
    that `register`/`login` required a browser, which no container or CI has.
    Fix: both commands accept `--email`/`--password` (+ optional `--name`) and
    complete the SAME Authorization Code + PKCE grant over HTTP — the CLI signs
    in to the JSON API, and `/connect/authorize` (satisfied by that cookie
    session) answers with the redirect carrying the code, read off the Location
    header and exchanged normally. No new grant type, no weakened flow, no
    server change; documented in `--help` and `help register`/`help login`.
    Verified end to end: register -> whoami -> build -> submit -> server build.
    That verification finally MEASURED the headline determinism claim, and it
    fails: local `6fb40191...` vs server `0178dcf8...` on the same machine with
    the same wasi-sdk. Root cause is not toolchain drift (the CLI's guess) but
    absolute build paths embedded in the artifact — `strings` shows
    `/home/user/nilbots/src/BotArena.{Guest,Sdk}`; local builds run from the
    caller's cache dir, server builds isolated as `botbuild` elsewhere, so the
    bytes differ by construction. This also explains gen-7's split result (one
    bot matched, one did not): parity was an accident of build location. Fix is
    deterministic source paths (MSBuild PathMap + DeterministicSourcePaths) in
    the controlled build — deliberately NOT applied here because it changes
    every artifact hash, invalidates the build cache, and rewrites every
    champion artifact: a supervised version-bump batch with goldens re-pinned,
    not a drive-by. Until then `submit` should not guess "toolchain/sysroot
    drift" and the NuGet README should not promise bit-identical artifacts.

81. **Player builds are reproducible across build directories — one of the two
    causes of the local<->server parity failure (DECISIONS #80).** CORRECTION:
    an earlier revision of this entry claimed this fixed the parity promise
    outright. It does not. Measured after the fix, with BOTH sides on the new
    toolchain and identical Sdk/Guest DLLs: local `f4733dfe...` vs server
    `c70b232e...`, still DIFFERENT — and the artifacts differ in SIZE (997,924
    vs 998,445 bytes) with ~92% of bytes differing, which is structurally
    different codegen, not path or timestamp noise. Ruled out so far: the
    source set (bin/obj are correctly excluded, both compile the one file), the
    toolchain assemblies (byte-identical), and the workspace path (now mapped).
    A second cause remains unidentified — likely something differing between
    the isolated `botbuild` build environment and the caller's. Tracked as open
    in DX-FINDINGS-NUGET-PLAYER. What IS proven below stands on its own. The
    controlled build project now sets `PathMap` from `$(MSBuildProjectDirectory)`
    to the fixed virtual root `/nilbots/bot`, plus `Deterministic` and
    `DebugType=none`. Cause being fixed: the workspace lives at a different
    absolute path per host — the caller's cache dir locally,
    `BuildIsolation.WorkRoot/<key>` under server isolation — and those bytes were
    compiled into the artifact, so "local and server produce identical WASM" was
    false by construction rather than by drift. Measured before: local
    `6fb40191...` vs server `0178dcf8...`. Measured after: two cold builds from
    two different cache roots produce the SAME hash
    (`78da86bc9ce7320989bf053abe5a0d78e3bfd48fab48b2e89c896631564001ea`), and
    `strings` finds no `/home`, `/tmp` or `/root` path in the artifact at all.
    Two bonuses: artifacts shrank 2.54 MB -> 998 KB (61%) because debug info was
    dominating them, and player artifacts stop leaking our build directories.
    Fault reporting is unaffected — the guest reports exception type + message,
    not line numbers (verified: `Fault s0: InvalidOperationException: deliberate
    fault for diagnostics`). `BuildPipelineVersion` 1 -> 2 invalidates every
    cached artifact, which is the intended blast radius; committed champion
    artifacts are frozen binaries and keep working unchanged. Guarded by a new
    `scripts/e2e.sh` assertion that builds the same bot under two cache roots and
    fails if the hashes differ, so this cannot silently regress.

82. **Friend's-agent onboarding reaches the ladder, but only past three
    self-inflicted obstacles (DX-FINDINGS-NUGET-PLAYER round 2).** An agent given
    only the CLI and told "a friend sent you this" registered, built a bot
    scoring 184/192 vs the built-ins on unseen seeds, submitted, and reached
    **#2 of 8 on the ranked ladder** — so the path works end to end. Verdict was
    "partly" because it got there by regex-mining docs prose out of the served
    JS bundle and writing a reflection dumper for the SDK API. Fixed here:
    (a) `register`/`login` printed NOTHING when stdout was piped and then blocked
    for five minutes — the fallback URL sat in a buffer, and piping is what
    agents and CI do by default; it now goes to stderr, flushed, alongside the
    headless one-liner. (b) The parity message blamed "toolchain/sysroot drift"
    and cited `docs/DECISIONS.md`, a repo-only file no player can read; it now
    compares the CLI's bundled SDK against the server's `/api/meta` and names the
    version gap with the upgrade command. (c) `web/dist` — what the app actually
    serves — was two days stale and still taught "150 zone-ticks wins", a
    pre-0.5 mechanic, while DocsPage.tsx was correct; rebuilt, and DocDriftTests
    now fails when the bundle is older than the docs source. (d) Site docs still
    said `botarena new`/`botarena set` after the rename. (e) `nilbots bots` now
    describes each opponent; `--rules` separates the game from research arms.
    Harness lesson recorded: the run's `dotnet tool install` silently reused the
    already-installed published 0.4.0 instead of the patched local build, so its
    findings about headless auth, XML docs and `--version` were against stale
    bits — future runs must uninstall first or use a private tool path. Still
    open and ranked in the findings doc: no text/`llms.txt` mirror of the docs
    (the root cause of the bundle-mining), ranked play absent from the CLI
    (`nilbots rank`/`leaderboard`), `doctor` ignoring the signed-in session, and
    the undocumented enemy-cooldown reconstruction.

83. **Cross-platform builds normalised: macOS/arm64 and Linux now agree on one
    virtual source root (corrects #81).** Investigating whether supporting both
    macOS and Linux worsens the artifact divergence turned up a real defect AND
    two errors in #81 that are corrected here.
    THE DEFECT: three build paths reach the generated project — Docker (macOS
    and Linux/arm64, via run-wasm-publish.sh), the isolated setpriv publish, and
    a plain local publish — and only the Docker one passed
    `-p:PathMap=/workspace=/src` and `-p:ContinuousIntegrationBuild=true`. The
    other two got the project's own `PathMap` to `/nilbots/bot` and no CI flag.
    So a Mac build and a Linux build of identical source embedded DIFFERENT
    virtual roots and could never produce equal bytes. Fixed by setting
    `PathMap=$(MSBuildProjectDirectory)=/src` plus `ContinuousIntegrationBuild`
    in the generated project, matching the value the Docker command line already
    uses, so all three paths agree however they are invoked.
    CORRECTION 1 to #81: the csproj `PathMap` added there is OVERRIDDEN on the
    Docker path (command-line `-p:` wins), so it never applied where that entry
    implied it did. CORRECTION 2: #81 credited that change with making builds
    reproducible across build directories, but the "before" state was never
    measured. It is now: with `DebugType=none` removed the build is still
    reproducible, and under isolation the workspace path derives from the cache
    KEY rather than from BOTARENA_HOME, so the two-cache-root check never varied
    the compile path at all. The e2e assertion is relabelled to say what it
    actually proves (determinism), and the honest end-to-end check is a
    submission's local-vs-server comparison. What #81 genuinely delivered stands:
    `DebugType=none` stopped absolute repo paths leaking into every player
    artifact and cut them 2.54 MB -> 998 KB.
    Local<->server divergence remains open and is NOT explained by this: both
    sides here take the isolated path. Behavioural equivalence is measured and
    holds (DX-FINDINGS-NUGET-PLAYER).

84. **Local<->server artifact divergence closed: the staged assembly closure no
    longer depends on which host compiled (closes the item left open by #81/#83).**
    A submission now reports `Parity: IDENTICAL` for the first time. The cause
    was never the compiler or the source paths — it was WHICH assemblies got
    staged into the controlled workspace, and three independent mechanisms made
    that host-dependent:
    (a) `BuildLocked` copied EVERY `*.dll` next to the invoking host into
    `workspace/libs`. From the CLI that is 9 assemblies; from the server it is
    74 — AWSSDK.S3, BotArena.App itself, EF Core, OpenIddict, Humanizer. So
    identical sources compiled against different assembly closures, and the
    server's whole dependency graph was handed to a sandboxed player build for
    no reason. It now stages exactly `BotArena.Sdk.dll` + `BotArena.Guest.dll`
    (Guest -> Sdk is the entire closure; Sdk is a leaf) and throws if either is
    missing rather than silently building something else.
    (b) `EnsureToolchainAssemblies` returns the copies beside the host when BOTH
    are present. BotArena.App referenced Sdk transitively but never Guest, so
    only Sdk.dll sat beside the server — it fell through to building its own
    pair from the repo, in a different configuration and directory than the CLI
    shipped. The App now takes a ProjectReference on BotArena.Guest purely so
    both hosts stage what they shipped with.
    (c) That repo-built fallback cached into `cache/toolchain-<GuestAdapterVersion>`.
    The 0.8.0 -> 0.8.1 SDK change (`[Obsolete]` members, XML docs) did not touch
    GuestAdapterVersion, so the server kept staging a stale Sdk.dll. The
    directory is now keyed by a SHA-256 of the Sdk/Guest sources, so any
    framework edit invalidates it with no version bookkeeping.
    Two hardening changes come with it. `src/ToolchainAssembly.props`, imported
    by Sdk and Guest only, removes every axis along which those two assemblies'
    bytes could vary — `Optimize`, `DefineConstants`, `DebugType`, `PathMap`,
    `Deterministic`, `ContinuousIntegrationBuild`, `AssemblyConfiguration` — so a
    Debug repo build and a Release container build produce the same DLL. The
    cost is no PDBs for those two projects (no line numbers in SDK stack traces).
    And the build-cache key now includes the SHA-256 of each staged assembly.
    That retires the standing footgun that the cache hashed player sources only:
    a framework change now rebuilds by itself, and two hosts holding different
    Sdk builds get DIFFERENT cache keys instead of agreeing on a key while
    producing different bytes.
    MEASURED: a fresh `nilbots submit` against a local server produced
    `ca01ccf5...` on both sides — and the two builds did not even take the same
    path (the CLI ran the isolated setpriv publish under `/var/lib/botbuild/work`,
    the server ran unisolated under its cache dir), so this also demonstrates the
    path independence #83 aimed at. `BuildPipelineVersion` 2 -> 3; every
     pre-existing cache entry is invalid because artifact bytes change.

85. **Maps own immutable presentation themes; the default world is the
    industrial Control Room, and replay viewers do not override it.** ASCII
    `#`/`.` rows remain the authoritative collision layer. A renderer-owned
    standalone JSON theme package supplies floor and wall materials, palette,
    map-stable cosmetic variation, and objective treatment. Each map JSON names
    its theme and the engine snapshots that ID into the replay; the frontend
    never maps map IDs or exposes a viewer skin switch. Bot looks are separate
    JSON packages owned by bots through `botarena.json` / the bot record and
    snapshotted into match participants and replays. The initial set is
    Vanguard, Bulwark, Needle, and Orbiter; slot-based selection remains only
    for old replays without a `lookId`. Movement, turning, recoil, projectiles,
    damage, and destruction animate only from authoritative replay
    states/events. Base materials must be homogeneous and mask-safe; rivers,
    trenches, cable runs, and other map-scale visual features require explicit
    map presentation data rather than being baked into a reusable theme.
    The first wall implementation used theme-owned trim and shadow donors
    selected from ASCII adjacency; decision #86 supersedes that production
    detail. Mutable viewer/account preferences must never rewrite historical
    playback.

86. **Arena walls use map-authored families and deterministic topology
    atlases built from generated material sources.** One repeated wall donor
    cannot make perimeter fortifications and interior cover read as different
    structures. Maps now name `boundaryWall`, `interiorWall`, and optional
    exact-cell family overrides in immutable presentation data, which the
    engine snapshots into replays. A theme supplies art for those semantic
    families. Image generation produces only opaque material sources;
    `build-theme-art.py` makes them periodic, derives albedo/normal/height/
    roughness/AO maps, and bakes matching 256-entry eight-neighbour edge and
    shadow atlases. The viewer only chooses the recorded family, computes its
    adjacency mask, and places the baked sprite. It does not synthesize
    outlines, rounding, bevels, or shadows. This preserves ASCII collision,
    permits handcrafted per-map art direction, keeps playback deterministic,
    and leaves the material bundle ready for a later orthographic 2.5D DCC
    bake without changing map semantics.

87. **Combat contrast minimally adapts the bot accent to the painted local
    background; it does not add universal plaques or outlines.** Themes remain
    free to use light, dark, and locally varied materials. Health and ordnance
    keep their original pips, glow, trails, and projectile silhouettes. The
    renderer samples beneath each indicator and preserves its authored accent
    whenever that color already reaches 3:1 graphical contrast; otherwise it
    makes the smallest one-percent blend toward black or white that does.
    Sampling failure falls back to the authored accent. This is presentation
    only and never mutates bot or replay identity. Non-default rules snapshot
    `maxHealth` into the replay header, while omission preserves canonical
    bytes for historical three-health replays; initial state and both health
    displays use that dynamic value rather than a fixed count.

88. **Theme topology atlases bake at 2× while runtime size remains
    budgeted.** High-DPI canvases can require more than the former 96 source
    pixels for one wall core, so each logical 96 px core / 16 px gutter is now
    baked as 192 / 32 into a 4096×4096 atlas. Edge atlases use quality-95 alpha
    WebP instead of lossless WebP; at gameplay scale this retains the authored
    detail while making the 2× files smaller than the former 1× files. Each
    theme recipe declares a total runtime asset budget and the deterministic
    build fails if it is exceeded. Resolution may not silently turn every
    self-contained replay into a substantially larger download.

89. **Genuine SVG is the recommended default for bot looks, with PNG retained
    as an evidence-based exception.** SVG stays sharp through arena scaling,
    rotation, high-DPI playback, and small telemetry thumbnails while reducing
    bundled bytes. It must use the canonical transparent 512 viewBox and may
    not embed raster payloads. A painterly or organic look may remain PNG only
    when gameplay-scale comparison shows that honest vector authoring
    materially harms its intended surface art; retain a high-resolution master
    in that case. Automatic tracing and SVG-wrapped PNGs are rejected because
    they add complexity without improving scaling.

90. **Map packages are capped at 32×32, but map dimensions are not used as a
    proxy for wall sharpness.** Larger maps produce smaller screen tiles, so
    they do not cause topology-atlas upscaling. The cap instead bounds
    simulation arrays, replay/render work, traversal scale, and the loss of
    per-tile detail caused by mapping one reusable floor material across an
    arena. High-DPI wall sharpness remains owned by the 2× atlas and DPR review.
    The shipped maximum is 24×18, leaving deliberate room for new layouts
    without admitting effectively unbounded maps.

91. **The five shipped mechanical bot looks use genuine SVG redraws.** Lancer,
    Vanguard, Bulwark, Needle, and Orbiter now share a crisp path-based visual
    language and remain distinct at 64 px. The four replaced 512 px PNGs move
    to unbundled `art/bot-looks` references so their art direction is not lost
    and their bytes do not remain in every self-contained viewer. This does not
    ban future PNG looks, but raster is now the documented exception and needs
    gameplay-scale evidence that vector would be dishonest or visually poorer.

92. **Production receives a verified deployment bundle over SSH; it does not
    fetch the source repository.** The manual release workflow archives only
    tracked `deploy/` control files from the exact Git revision that produced
    the immutable GHCR images, hashes the archive, and transfers it with those
    image digests. The VPS verifies the hash and safe archive paths, links the
    candidate to persistent `.env`, certificate, and backup state, deploys it,
    and advances `current` only after health checks pass. `previous` retains
    the prior bundle and image digests for rollback. PostgreSQL, Garage, and
    Caddy continue to live in named Docker volumes. This removes repository
    credentials from the host, keeps private-repository deployment viable, and
    prevents an operator from accidentally deploying an unreviewed `main`
    checkout. Rejected: continuing to `git fetch` an exact SHA on the VPS. It
    was deterministic but coupled production to repository access and made
    tracked configuration rollback less explicit.

93. **Toolchain skew is a hard stop in `submit`, and a server may not be deployed
    ahead of its CLI.** Owner question after #84: force the CLI to match the
    server so we avoid version hell? Yes — but forcing the client alone would
    have made it worse, because the hell does not come from players failing to
    update. `release.yml` offers `publish-cli` as a SEPARATE manual operation
    from `publish-and-deploy`, so a server can be deployed advertising an SDK
    that no published CLI bundles. That is production's state today (server
    SDK 0.8.1, newest published tool 0.4.0): telling players to run
    `dotnet tool update -g Nilbots` would be advice they cannot act on.
    So both halves ship together.
    CLIENT: `/api/meta` now advertises `buildPipelineVersion` and `cliVersion`
    alongside `sdkVersion`, and `submit` refuses BEFORE compiling when the SDK or
    the build-pipeline version differs — the two axes that decide artifact bytes.
    It names both sides, prints the upgrade command and the server's own CLI
    version, and offers `--allow-toolchain-skew` for players who accept losing
    the parity guarantee (the server build still decides the match, so this is a
    guarantee, not a gate on participation). The CLI version is deliberately NOT
    gated on: a CLI-only bugfix release must not force the world to update. A
    server too old to answer, or unreachable, is never treated as mismatched.
    RELEASE: the two operations stay separate manual runs — coupling them would
    publish an irreversible NuGet version as a side effect of a deploy, and a
    deploy that then failed would leave players upgrading INTO skew, which the
    new gate turns into a hard refusal. Instead the ordering is enforced.
    `scripts/assert-cli-release.sh` runs in `verify`: `unpublished` before
    `publish-cli` (so every release takes a fresh version and `--skip-duplicate`
    can no longer silently no-op), `published <sha>` before `publish-and-deploy`.
    CORRECTION to the first version of this entry: that guard only checked that
    `Nilbots <CliVersion>` exists on NuGet, which is vacuous — an untouched
    `CliVersion` is always published, by an older commit carrying a different
    SDK. With CliVersion at 0.4.0 and the server on SDK 0.8.1 it would have
    passed while shipping exactly the breakage it was written to prevent. The
    `publish-cli` job now tags the published commit `cli-v<version>`, and the
    deploy guard originally required that tag to resolve to the exact revision
    being deployed — NuGet says the version exists, the tag says which commit
    made it. FOLLOW-UP: exact-commit coupling forced irreversible no-op NuGet
    releases for auth, migrations, deployment, and site-only changes. The guard
    now accepts a later server revision when its enumerated CLI compatibility
    surface is byte-identical to the tagged release: CLI code, SDK/Guest,
    engine/runtime, controlled compiler inputs, maps, packaged bots/templates,
    player guide, and replay-viewer sources. Any change in that surface still
    fails closed and requires a version bump plus `publish-cli`; server/auth and
    site-only changes can use the existing compatible tool.
    `CliVersion` 0.4.0 -> 0.5.0 accordingly (SDK 0.8.1 + pipeline 3), pinned to
    the csproj `<Version>` by `PackagedCliVersionTests`.
    Rejected: a server-side rejection of skewed submissions. The server rebuilds
    from source and its artifact is canonical, so a submission that compiles is
    valid regardless of which CLI sent it; blocking it would deny participation
    to fix an assurance property. Also considered and deferred: having the CLI
    fetch the Sdk/Guest assemblies from the server it submits to, which would
    decouple the CLI version from the SDK version entirely. It only partly
    works — `BuildPipelineVersion` encodes how `BotBuilder` generates the project,
    which is CLI code and cannot be downloaded — so it reduces rather than
    removes the coupling, and it trades an offline-capable tool for a networked
    one. Not worth it while releases are cheap.

94. **Ratings have a floor of 100, enforced on the pair rather than per bot.** Owner
    call while reviewing how pushed bots are rated. Nothing about a rating is
    client-supplied — `POST /api/matches/ranked` carries only `{BotId,
    OpponentBotId, Rules?}`, and the server picks the three maps, the seeds, both
    slot orders, runs all six games from the stored artifacts, and computes elo in
    `JobWorker.TryFinalizeSet`. The one lever an author has is WHICH opponent to
    challenge, including a second bot they own, so a sacrificial bot could in
    principle be drained to inflate a real one.
    Measured before changing anything: the expectation term already kills that.
    Sweeping a sacrifice pays ~7.5 rating on the tenth set, 0.9 on the hundredth,
    0.18 on the five-hundredth, 0.0004 after 200k — a logarithmic decay no one can
    outrun. So the floor is not the anti-farming mechanism; it is the hard bound
    that keeps ratings out of absurd or negative territory and makes a bot at the
    bottom worth exactly nothing to beat.
    The subtlety is that a naive floor makes farming WORSE. Clamping only the
    loser lets the winner take points the loser never lost, minting rating and
    turning a floored bot into an infinite supply. `EloAdjustment.ForBotA` therefore
    caps the transfer at what the loser can afford, so every set stays zero-sum and
    neither side can cross the floor. Extracted as a pure function to be testable
    without a database; `EloAdjustmentTests` pins the decay, the conservation, and
    the floor from both directions.
    NOT added: a provisional period, or a ban on both bots sharing an owner.
    Same-owner sets are how you test a new version against your own champion, and
    unranked trial fights (`POST /api/matches/challenge`) already exist for
    practice that must not touch the ladder at all — a single match, caller-chosen
    map and seed, no `MatchSetId`, so `TryFinalizeSet` never runs on it.

95. **Ranked opponents are matchmade by rating; you cannot pick your fights.** Owner
    call after #94 established that the one lever an author has over their rating is
    opponent choice. A ladder where you choose who you play measures who you avoided,
    not how good your bot is — so ranked stops accepting an opponent at all.
    `POST /api/matches/ranked` now takes `{BotId, Rules?}`, and the server draws
    uniformly from the five bots nearest the challenger's rating on that ladder.
    Five rather than one: the single nearest would be deterministic, the same two
    bots would play forever, and a rating would say more about who happened to sit
    adjacent than about strength. Bots owned by the challenger are excluded whenever
    anyone else is available — legitimate practice, but the shape of every farming
    scheme — and are the fallback only on a ladder where nobody else has a built bot.
    Unrated bots enter at `BotRating.DefaultRating` so a first set is still sensible.
    Selection is a pure function (`RankedMatchmaking.Choose`) so the rule is testable
    without a database, bots or a match.
    Choosing your opponent remains available where it belongs: unranked matches
    (`POST /api/matches/challenge`), which were previously reachable only from the
    website. `nilbots spar <mine> <theirs> [--map] [--seed]` puts them in the CLI, so
    an agent can practise a matchup, and `nilbots rank <mine>` no longer takes an
    opponent argument. Both surfaced a positional-parsing bug shared with the old
    `rank`: option VALUES were counted as bot names, so `--rules 0.4` had always
    turned into a usage error. `TakeWhile` instead of `Where` fixes it.
    Scripted pairings survive for the evaluation harness, which runs a round robin
    and crowns champions on real ladders. `opponentBotId` is honoured when
    `BOTARENA_ALLOW_PINNED_RANKED` says so, defaulting to true only for the local-only
    `all` role — the role agent-arena runs on. Production refuses it with a message
    pointing at unranked play. Explicit configuration wins over the default in both
    directions, which is also the only way to exercise the refusal on a single-process
    machine.
    Verified against a live server: three `nilbots rank MmBot` runs drew Rampart and
    coward (an unrated bot sits at 1200, and those are the neighbours), a pinned
    opponent under `BOTARENA_ALLOW_PINNED_RANKED=false` returned 400 with the
    redirect to unranked, and `spar` queued matches with and without an explicit
    map and seed.

96. **Public surfaces identify people by display name and bots by name or slug; a
    bot's URL is its slug, not its GUID.** Owner asked for an audit after
    remembering an "id" in a listing. Audited every public payload against a live
    server: `/api/bots`, `/api/leaderboard`, `/api/matches`, `/api/matchsets/{id}`
    and the CLI's `leaderboard`/`bots` all show bot names and owner display names,
    and no email appears in any of them. Emails are returned only by
    `/api/accounts/register`, `/login` and `/me`, each answering the caller with
    their own record. The seeded system account displays as "nilbots", not as
    `system@nilbots.local`. Nothing had to be fixed there, and
    `PublicPayloadPrivacyTests` now pins it: every response type in the app is
    reflected over, and only the three self-scoped ones may name an email. The test
    asserts it actually scanned a meaningful number of types, so it cannot pass by
    scanning nothing.
    What the audit DID find is that ids were reaching people through URLs. Bots have
    carried a unique, immutable slug since the first schema (there is no rename
    endpoint, so a slug cannot go stale), yet every link was a raw GUID —
    `nilbots submit` closed with `Fight: <server>/bots/0c3fafa5-...`. `/api/bots/{key}`
    now resolves a slug or an id, so `/bots/murder-roomba` works and every GUID link
    ever handed out keeps working; the site links by slug from the leaderboard, bots
    and garage pages; and `submit` prints the named URL. The leaderboard payload
    gained `slug` to make that possible.
    Also fixed in passing: the empty-ladder hint still read `nilbots rank <bot>
    <opponent>`, which #95 had made wrong.

97. **Ranked play accepts shipped rules versions only; research arms need the same
    opt-in as pinned opponents.** Owner asked whether the `rules` field on a ranked
    challenge makes sense. Partly. Per-ruleset ladders (#54) are right — every rules
    version keeps its own elo, so an old bot is never invalidated — but the endpoint
    handed whatever arrived straight to `GameRules.Resolve`, which accepts 25 names:
    5 shipped versions and ~20 research arms. Verified live before fixing:
    `{"rules":"energy"}` from an ordinary player account queued a rated set.
    Three things wrong with that, in increasing order of seriousness. Arms are
    mechanics no player-facing doc describes, so a rating exists on a ladder nobody
    can read the rules for. Choosing the ruleset is the same "pick your venue" lever
    that #95 removed for opponents — you could fish for the arm your bot happens to
    suit. And worst, arm ladders are EXPERIMENT DATA: the balance harness reads them
    to make ship/no-ship calls, and player traffic would quietly contaminate a
    measurement the whole rules methodology depends on.
    Ranked now refuses any name outside `GameRules.ShippedNames` unless
    `BOTARENA_ALLOW_PINNED_RANKED` is on — the same gate that admits scripted
    pairings, since both are evaluation-harness needs rather than player-facing
    ones. The refusal distinguishes a known arm from an unknown name, because
    telling someone who typed `wibble` that it is "a research arm" is a lie.
    SUPERSEDED WITHIN THE SAME SESSION by the owner answering that open question:
    freeze them. A ranked set is now only accepted on the ruleset the server is
    actually running (`JobWorker.MatchRules`), which subsumes the arm restriction —
    an arm is playable only where the server itself runs it, which is exactly how an
    eval deployment is configured. Every other ladder keeps its ratings, its history
    and its leaderboard, and takes no new sets. The reasoning that closed retired
    OFFICIAL versions is the same one that closed arms: matchmaking picks the
    opponent, and nobody on the 0.3 ladder chose to play 0.3. Keeping them open would
    also mean N live ladders to balance, moderate and reason about forever, for a
    ruleset nobody is developing against.
    A freeze has to be legible or it is just a confusing 400, so: `/api/leaderboard`
    now returns `activeRulesVersion` beside the ladder being viewed, the CLI prints
    `[closed — 0.5 is live]` in the header, the site says the standings are final and
    names the live ladder, and the refusal itself points at the leaderboard URL where
    the history still lives. An unknown rules name still gets `Resolve`'s own error
    rather than being described as a closed ladder.

98. **A Playwright walk-through of the site is a tracked script, and its first run's
    findings are fixed.** Owner asked whether the UI covers the use cases and has
    filters. It covers them: browse the match feed (30 rows), open a match and watch
    the replay with a tick scrubber, speed control, per-bot state, event feed and a
    field-of-view toggle; read the ladder and switch ruleset; browse bots, open one,
    see its versions and match history; read the full player guide at /docs; sign in;
    and none of it overflows a 390px viewport. Filters it did NOT have.
    Four defects, all fixed here. (a) An unknown bot slug or match id left the page
    on "Loading…" forever — the bot page never caught a rejected fetch, and the match
    page retried a 404 every three seconds indefinitely. Both now say what is wrong
    and link somewhere useful; the match page still retries everything that is not a
    404, since those are transient. (b) /bots listed 34 cards with no way to narrow
    them, which is where a grid stops being browsable; it now filters by bot or owner
    name with a ranked-only toggle and a match count. (c) Every bot card showed a
    truncated artifact hash — an id in a listing, the thing #96 was about. It is a
    verification aid, so it moved to the bot's own page and the card shows the
    version count instead. (d) The leaderboard's ruleset switcher offered research
    arms as equal choices, which is exactly the mistake `GameRules.ShippedNames`
    exists to prevent; the API now offers shipped ladders plus whatever is live, and
    bot cards hide arm ratings the same way. Arms stay queryable by `?rules=` for the
    balance harness.
    Two findings in the first run were the TEST's fault, not the site's, and are
    recorded so nobody re-reports them: an email-shaped regex matched rating badges
    like `1216@0.5`, and the landing feed read as empty because `networkidle` plus an
    immediate navigation aborted its request. Both are false positives; the site was
    fine.
    Not done, and worth its own decision if the arena gets busy: the match feed takes
    a `take` count and nothing else, so it cannot be filtered by bot, map or
    ranked-ness, and there is no pagination anywhere.

99. **Every list the site shows can be narrowed, and the match feed filters
    server-side.** Closes the gap #98 left open. The filters are: the match feed by
    bot, by map and by ranked/unranked, with Load more; /bots by bot or owner name
    with a ranked-only toggle; the ladder by bot or owner.
    The match feed had to move to the SERVER. A browser-side filter can only narrow
    the page it already fetched, so "every match Bastille played" would quietly mean
    "the ones among the latest thirty" — an answer that looks complete and is not.
    `GET /api/matches` therefore takes `bot` (slug or id), `map`, `ranked` and `skip`
    beside `take`. An unknown bot filters to nothing rather than falling back to
    everything, because a filter that silently ignores itself is worse than an empty
    list. /bots and the ladder stay client-side: both fetch their whole collection
    already, so a round trip would buy nothing.
    Feed filters live in the URL (`/?bot=mmbot&ranked=true`), which makes a filtered
    view linkable and reloadable, and gives the bot page somewhere to point — its
    match history is capped, so it now carries an "every match →" link into the
    filtered feed. The ladder keeps each bot's true rank while filtered: #7 is #7
    whatever else is on screen.
    Paging is `skip`/`take` offset, not a cursor. Offset paging can repeat or drop a
    row if the feed changes underneath you, which for a browse feed of finished
    matches is a cosmetic risk, and a (CreatedAt, Id) cursor across Postgres uuid
    ordering is real complexity for that. Revisit if the arena gets busy enough to
    notice.
    Measured with Playwright: 30 rows unfiltered, `bot=warden` narrows to Warden's
    matches, adding `map=arena-01` cuts it to 20, adding `ranked=false` empties it
    with the right message, clear restores 30, a deep link restores both selects, and
    Load more goes 30 -> 54. `scripts/ui-audit.mjs` now checks that filtering works
    and that the URL carries it, and every check it makes passes.
    Its own email regex is fixed too: requiring a letter on both sides of the `@`
    stops it reporting the rating badge `1216@0.5` as a leaked address, which is the
    false positive #98 recorded. A tracked test that cries wolf is worse than no test.

100. **The #99 filters got the three indexes they were missing, measured at 300k
     matches rather than at the 665 we have.** Owner asked whether the indexes are on
     point. At current size every plan is instant and every plan is a lie about what
     happens later, so the queries were re-measured on a throwaway database holding
     300k matches, 600k participants and 4k ratings, shaped like the real ones.
     Before: filtering the feed by an uncommon MAP was a parallel sequential scan
     discarding 149,980 rows per worker (12.5 ms); the LEADERBOARD was a sequential
     scan over every ladder plus a sort, because the only BotRatings index leads with
     BotId and cannot serve `WHERE RulesVersion = ? ORDER BY Rating DESC`; and the BOT
     filter had no index on `MatchParticipants.BotId` at all (35 ms).
     Added, via the FilterIndexes migration: `Matches(MapId, CreatedAt DESC)`,
     `BotRatings(RulesVersion, Rating DESC)`, `MatchParticipants(BotId, MatchId)` —
     MatchId trailing so the probe is index-only. After: the rare-map filter is a
     bitmap index scan at 0.065 ms (190x), the leaderboard 0.10 ms, the bot filter
     10.7 ms (3.3x).
     KNOWN RESIDUAL, measured not guessed: filtering by a bot whose matches are all
     OLD costs 203 ms at 300k matches. Postgres walks the CreatedAt index backwards
     probing each match, because the LIMIT makes early exit look cheap; it is a
     costing choice, not a syntax one — rewriting the EXISTS as an IN produces the
     identical plan (226 ms), so no query-shape change helps. The durable fix is to
     denormalise the match timestamp onto MatchParticipants so `(BotId, CreatedAt DESC)`
     answers it in 30 index rows flat. Not done: it is a schema addition with a
     backfill and a write-path invariant, and at 665 matches — or at a plausible
     year-one 30k, where the same case costs about 20 ms — it buys nothing. Revisit
     when the arena is large enough for that to be felt.

101. **Projectile looks are bot-owned SVG masks; cosmetic entitlement is
    account-owned and enforced when equipping, not when replaying.** Four
    initial looks—Pulse Bolt, Ion Orb, Razor Shard, and Arc Spark—are discovered
    from standalone manifests. Their 256-viewBox East-facing SVGs define only
    the projectile-head silhouette; the renderer tints them with the locally
    contrast-adjusted bot accent and retains one truthful implementation of
    trails, glow, fog, traversal timing, and impact. `appearance.projectile`
    flows through local play and submit, the bot record, match-participant
    snapshot, and optional replay-participant field; missing/legacy IDs fall
    back to Pulse Bolt. A dedicated authenticated appearance endpoint and bot
    UI update accent, chassis, and projectile without a code submission, while
    old matches remain immutable. Future gated cosmetics use a catalog plus
    account grant ledger with achievement/challenge/promotion sources; no
    cosmetic may affect gameplay. Payments are explicitly deferred to a later
    commerce project. Viewing never rechecks ownership, and client-visible art
    is not DRM. A larger catalog must stop embedding every cosmetic into every
    self-contained replay.

102. **The first entitlement slice uses durable product events, not a generic
    achievement engine.** `cosmetics/catalog.json` is embedded into every App
    role and is the authority for stable bot/projectile keys, starter status,
    and unlock hints; tests require it to match the manifest-discovered web
    assets. `EntitlementGrant` is append-oriented and idempotent on account,
    key, source kind, and source ID. A successful first bot build grants Lancer;
    completing a setless authenticated challenge grants Arc Spark. Existing
    accomplishments and pre-entitlement equips are backfilled. System accounts
    may equip the full catalog. Bot creation, appearance updates, and
    submission-synchronized appearance all enforce access server-side, while
    replay rendering never does. The garage exposes locked items and hints.
    Payments, prices, checkout, and provider identifiers remain out of scope.

103. **The first ranked-play prestige reward is a paired, account-wide
    100-match achievement, where one ranked match is one complete mirrored
    set.** Aureate Warden and Regent Lance share the durable
    `achievement/ranked-matches-100` source and are granted together after an
    account completes 100 ranked sets across all owned bots and rulesets. The
    six internal simulations are fairness arms of one user-facing match, not
    six progression events. Evaluation runs after set finalization.
    Same-owner sets count once per account; the grant ledger makes retries
    idempotent; a data migration backfills existing qualifiers. The bot
    manifest recommends Regent Lance as its default companion in the
    appearance UI without coupling or restricting later projectile choice.
    Eclipse Bloom + Null Seed and Redshift Crucible + Crucible Splitter remain
    source-only concepts under `art/`: without runtime manifests or catalog
    entries they are intentionally unavailable until a later unlock is
    designed.

104. **Backend maintenance uses explicit application use cases and invariant
     ownership, not a full DDD rewrite.** The modular monolith remains the
     deployment and codebase shape. Endpoints and workers translate their
     inputs, application use cases own authorization, workflow, and
     transaction boundaries, and small policies or validated values own
     business rules that are repeated or protect ranking, authorization,
     privacy, immutable history, determinism, money, or data integrity. Those
     use cases may use EF Core directly; there will be no generic repository,
     mandatory mediator, global event bus, or speculative microservice split.
     Cross-cutting foundations are an injected `TimeProvider`, explicit actor
     context, typed outcomes and public contracts, documented
     transaction/idempotency behavior, correlated observability, typed
     startup-validated configuration, and mandatory PostgreSQL integration
     tests in CI. Delivery is incremental: establish the database safety net
     and shared primitives, prove the pattern on bot appearance, then extract
     match admission/snapshots, broadcast-safe projections, concurrent ranked
     finalization, and source-owned progression. The phases and invariant
     register live in `docs/BACKEND-MAINTAINABILITY-PLAN.md`.

105. **A revoked equipped cosmetic blocks future submission and match
     admission; it is not silently reset, and historical replay reads never
     reauthorize it.** `BotAppearancePolicy` is the single application owner
     for catalog validation and active entitlement across bot creation,
     appearance updates, version submission, and defense-in-depth match
     admission. A bot whose last active grant disappears keeps its stored
     choice so the owner's intent and the audit record remain truthful, but it
     must equip an owned item before another version or official match is
     accepted. Existing match snapshots and replay rendering remain immutable
     historical truth and do not call the policy. Appearance IDs normalize to
     lowercase kebab case and six-digit accent colors normalize to lowercase;
     the API exposes typed stable error codes through one ProblemDetails mapper.

106. **Bot and projectile catalogs expand independently, while large map themes
     are activated per map instead of entering every viewer at once.** Rift
     Runner and Mossback plus Phase Needle and Cinder Disc are new starter
     choices. Helio Kite, Scrap Jackal, Glass Manta, Helix Dart, Gravity Knot,
     and Prism Fan are independently cataloged entitlement items reserved for
     future achievement, challenge, and competition sources. None of these bot
     manifests recommends a projectile: chassis and projectile releases are not
     one-to-one. The existing Aureate Warden recommendation remains the explicit
     exception described in #103.
     Five generated theme kits retain separate floor, fortified-perimeter, and
     interior-cover sources plus deterministic runtime packages. Ember Forge
     ships on Arena and Frost Relay on Gallery. Drowned Vault, Desert Array, and
     Void Sanctum stay fully baked under `art/themes/*/runtime` until a map
     intentionally activates them. This is availability and packaging, not a
     viewer theme switch: the map JSON remains authoritative. Shipping all five
     immediately raised the self-contained viewer from the 6.7 MB gzip baseline
     to 16.3 MB; shipping two and staging three keeps it to 10.4 MB. The asset
     test pins the active/staged split so a directory copy cannot silently add
     every future theme to every replay.

107. **Public statistics are bot-level and computed from history; account
     statistics and stored aggregates wait for evidence.** Ranked and unranked
     records remain separate. One completed six-game ranked set is one ranked
     match; one authenticated setless challenge is one unranked match. Overall
     W/L/D adds those two user-facing records without treating a ranked set as
     six wins or losses. Combat totals deliberately retain the underlying
     arena-game granularity, currently games played, damage dealt, and faults,
     so useful detail is not discarded. A result contributes only after its
     complete broadcast is public; a ranked set waits for all six games, so
     statistics cannot reveal a delayed outcome. The bot page's latest 50 games
     remain navigation history and no longer masquerade as an all-time record.
     Queries aggregate the authoritative match, set, and participant rows on
     demand. Do not add counters, summary tables, or account-wide statistics
     until product use or measurements show they are interesting and necessary.

108. **User notifications are durable product records; SignalR is a realtime
     delivery channel, not the inbox.** A newly owned entitlement creates one
     account-scoped `UserNotification` in the same PostgreSQL transaction as
     its append-only grants. One source event groups paired rewards into one
     payload, redundant grant sources do not announce an already-owned item,
     and natural dedupe keys make worker retries silent. PostgreSQL `NOTIFY`
     wakes every web process only after commit; each web process forwards the
     named payload to its locally connected authenticated SignalR clients.
     The web app also loads unread records on startup and polls as a recovery
     path, then acknowledges a toast explicitly or after visible presentation.
     Thus an offline user, reconnect, process restart, or missed transient
     event cannot lose the accomplishment. Stable catalog IDs—not image URLs—
     let each client render the reward. Future mobile push and email consume
     the same durable notification through channel-specific delivery records
     and preferences rather than being invoked by achievement code. Match
     viewing's separate SignalR transport remains deferred.

109. **Ranked and unranked creation share participant admission and immutable
     snapshot ownership; all public match outcomes pass through one
     broadcast-safe projection.** `MatchAdmissionService` owns bot existence,
     optional challenger ownership, current appearance entitlement, and the
     active successfully built version. `MatchParticipantSnapshotFactory`
     copies bot/version identity, artifact hash, appearance, and the owner's
     display name for both workflows; a migration backfills historical owner
     names. Ranked games also record the resolved rules version when created.
     `MatchPublicProjection` is the only public result-column reader for match
     feeds, details, ranked sets, bot history, and computed bot statistics.
     Until `BroadcastComplete(now)`, winner, end reason/tick, replay hash,
     participant outcome/health/damage/faults, set score/rating movement, and
     derived history records remain null or neutral. HTTP contract tests pin
     concealment and revelation across every view. The transport remains
     polling for now; this privacy boundary is designed to be reused by the
     later SignalR match stream.

110. **Ranked-set finalization is a locked transactional use case; worker
     concurrency is configuration, not a correctness convention.** Durable-job
     claiming/lease ownership is separate from typed compile and match
     dispatch, and replay persistence is an explicit idempotent stable-key
     handler. Finalization locks the `MatchSet` row first, then both affected
     `Bot` rows in stable ID order. The set lock makes simultaneous last-game
     workers observe one terminal transition; the bot locks serialize
     different sets that touch the same ladder ratings and prevent lost
     updates or duplicate first-rating rows. Score, rating movement, ranked-set
     counters, terminal set status, achievement grants, durable notification,
     and commit-delivered PostgreSQL wake-up share one transaction. Real
     PostgreSQL tests run two finalizers against the same set and against
     separate sets sharing bots, and inject failures after the rating flush and
     immediately before commit. Job and finalization outcome metrics use
     low-cardinality job kind/outcome tags. One match lane remains the default;
     `BOTARENA_MATCH_WORKERS` may raise it only when measured throughput
     warrants more local concurrency.

111. **The second VPS is a web-and-compile node; ingress and the only match
     consumer remain on the primary.** The worker role activates `web` and
     `compile` Compose profiles and refuses stateful, ingress, and match
     services. Kestrel publishes only on the worker's exact provider-private
     address. Primary Caddy balances its local web process and private remote
     web endpoints with active readiness checks and cookie affinity, which
     preserves SignalR connection affinity without introducing Redis.
     PostgreSQL-backed Data Protection, shared OpenIddict certificates, shared
     object storage, durable compiler admission, and PostgreSQL `NOTIFY`
     delivery make the web role safe to repeat. The second process adds
     application capacity and process-level resilience, not primary-host high
     availability: Caddy, PostgreSQL, and Garage remain primary-owned. Older
     worker deployments' match container is explicitly retired. Ranked
     finalization is concurrency-safe under #110, but one match container with
     one lane remains the production default until measured throughput
     justifies increasing `BOTARENA_MATCH_WORKERS` or adding another consumer.

112. **The primary's persistent non-secret worker inventory is the fleet trust
     root and deployment source of truth.** Each tab-separated record contains
     a stable node name, public SSH target, deployment path, provider-private
     application address, and the worker's verified Ed25519 SSH host key.
     Bootstrap streams the primary's minimal shared application settings and
     OpenIddict certificates directly to a hardened worker, never through a
     workstation file, while excluding Garage administration credentials. It
     verifies private PostgreSQL/S3 connectivity, installs a persistent
     `DOCKER-USER` rule that admits Kestrel only from primary ingress, and
     disables root SSH only after operator access succeeds. Caddy derives web
     upstreams from this inventory. The manual release runner trusts the
     already-pinned primary, retrieves and strictly validates the inventory,
     extends known hosts from its recorded keys, and deploys every worker
     sequentially. This replaces per-worker GitHub variables and makes fleet
     growth an authenticated bootstrap plus one inventory entry, without
     adding a scheduler, service discovery system, or repository checkout on
     a VPS.

113. **Generated HTTP contracts are a CLI release surface, while their drift
     check remains manual-only.** Server response records produce the canonical
     OpenAPI document; web, mobile, and CLI DTOs are generated from it. The CLI
     server-command migration changes packaged tool bytes even though SDK 0.8.1
     and build pipeline 3 remain stable, so `CliVersion` and the NuGet package
     advance from 0.5.4 to 0.5.5 before the combined server release. The
     compatibility guard correctly rejects deploying the generated CLI changes
     behind the older tag. Contract drift has its own `workflow_dispatch`
     GitHub job rather than push/PR triggers, preserving the project's
     manual-only Actions budget.

114. **Production image publication and deployment require the complete manual
     CI workflow, not only release E2E.** The release verifier covers the
     managed/WASM pipeline, packaged gameplay assets, Garage recovery, and
     release installer, but intentionally has no PostgreSQL service and does
     not regenerate every API client. The standalone CI workflow is therefore
     also callable as a reusable workflow. `publish` and `publish-and-deploy`
     wait for its contract-drift and mandatory PostgreSQL jobs after release
     E2E succeeds; a failure prevents both image publication and deployment.
     Direct pushes and pull requests still trigger nothing, and an operator can
     still run CI by itself with `workflow_dispatch`, preserving the
     manual-only Actions budget.

115. **Opting into the PostgreSQL suite means running it: a database that cannot be
     created now fails instead of skipping.** Found while verifying the
     client-generation refactor. `dotnet test` reported green with 23 of 100 App tests
     skipped, which is the intended local opt-out — but setting `BOTARENA_TEST_DB` to
     opt IN did not change that. The tests still skipped, silently, and the reason was
     only visible by also setting `BOTARENA_POSTGRES_REQUIRED=true`, which turned the
     skip into a throw: `42501: permission denied to create database`. The fixture
     builds a throwaway database per test and the documented `botarena` role is created
     without CREATEDB, so anyone following the bootstrap and then opting in got a green
     run that exercised nothing — the worst possible answer, because it looks like
     coverage.
     The catch swallowed every exception whenever `BOTARENA_POSTGRES_REQUIRED` was
     unset. It now distinguishes the two intentions: an ABSENT `BOTARENA_TEST_DB` skips
     (the opt-out), a SET one commits, so an unreachable server or an under-privileged
     role throws with the fix in the message. `BOTARENA_POSTGRES_REQUIRED` keeps its
     remaining job — making a missing variable an error, which is how CI catches its
     own misconfiguration. The bootstrap in CLAUDE.md now grants CREATEDB and documents
     the opt-in command.
     CI never saw this because it connects as the `postgres` superuser; only local runs
     were affected, which is precisely where a silent skip does the most damage.
     Verified all three paths: no variable still skips, a variable with a CREATEDB role
     runs 98 of 100, and a variable with a role lacking CREATEDB fails with
     "The role needs CREATEDB — `ALTER ROLE <user> CREATEDB;` as a superuser."

116. **Worker lifecycle is provider-assisted but fleet-safe, reversible, and
     exact-address scoped.** A HostUp wrapper now resolves the stable public VPS
     ID, attaches an unassigned address from the existing private `/24`, waits
     for the provider operation, runs the provider-neutral host bootstrap,
     installs the primary's already-published immutable release, and refreshes
     Caddy. Bootstrap adds exactly one worker `/32` to PostgreSQL
     `pg_hba.conf`, reloads it, and validates PostgreSQL's parsed rules before
     registering the node; raw TCP reachability alone was insufficient and
     allowed a new web container to start unhealthy. Removal reverses the
     dependency order: inventory and Caddy first, worker containers second,
     PostgreSQL access last. It preserves the VPS and application state so a
     PAYG node can be gracefully powered off and later adopted without
     reinstalling the OS or rebuilding images. Resume restores access, starts
     the current release, waits for health, then restores edge traffic. The
     acceptance cycle stopped and restarted a real HostUp Cloud VPS and
     confirmed both its public and private interface assignments remained
     attached. The stable provider VPS ID is still used for power operations
     and current-IP discovery rather than treating address retention as an
     identity guarantee.

117. **Roster and ladder filtering stays client-side until the roster outgrows one
     response.** `GET /api/bots` and `GET /api/leaderboard` take no query parameters and
     return everything; the site, the mobile app, and any future client each filter in
     memory. That is deliberate at eight bots — the site's own note puts the threshold
     past thirty — and it keeps one code path rather than three.
     It is a cliff, not a plateau, and mobile reaches it first: a phone on cellular pays
     to download the whole roster to render ten rows, with no pagination to fall back on.
     When it bites, the fix is `?q=`, `?ranked=`, and cursor pagination on the endpoint,
     after which every client filters server-side and the mobile lists become
     infinite-scroll. Doing it now would add pagination to three clients to solve a
     problem no user has.
     Rank is the counter-example and already went the other way: it is a property of the
     whole ladder, so `/api/bots` serves `currentStanding` rather than letting clients
     join `/api/leaderboard` — a client-side join would cap rank at that endpoint's slice
     and get ties wrong.

118. **A match notification is one record per subject, emitted on the broadcast
     boundary, and the mobile app is another channel rather than another
     inbox.** Extending #108 beyond entitlements, planned in
     [`NOTIFICATIONS-PLAN.md`](NOTIFICATIONS-PLAN.md). Three things are settled
     here because getting them wrong is expensive later. First, a result is
     written when a match finishes *broadcasting* and a set when it is
     *revealed*, never on completion: emitting earlier would push "your bot
     won" to a phone while the replay is still playing out, defeating
     broadcast secrecy at the one moment it matters most. Second, a challenge
     and its result are one row keyed by the subject match or set, rewritten
     and re-announced on settlement rather than appended beside it, so the
     inbox never holds a stale "watch this" next to its own outcome and the
     dedupe key stays natural for silent retries; `ReadAt` clears on rewrite
     because an outcome is new information. A ranked set emits one
     notification, not one per game — six rows would both spam and leak the
     set's shape game by game. Third, mobile consumes the same durable record
     over the same SignalR channel with the same unread-on-resume recovery,
     and push is a separate delivery with its own registration, preferences
     and delivery records, sent from a durable job so match settlement never
     depends on APNs being reachable. The blocking prerequisite is contract
     shaped: `UserNotificationResponse.Payload` is typed as one payload record
     so it reaches every generated client, a second kind compiles fine and
     would serve empty data, and `ToResponse` throws on unknown kinds to force
     the discriminated union to be built first.

119. **Notification policy: announce what others did to you and what moved your
     rating; report losses exactly like wins.** Settles the questions
     [`NOTIFICATIONS-PLAN.md`](NOTIFICATIONS-PLAN.md) left open under #118. A
     challenge notifies only the challenged — the challenger pressed the button
     and is looking at the screen, and an app that echoes your own actions
     teaches you to ignore it. Ranked sets notify both players because rating
     moved for both; unranked results notify only the challenged, since nothing
     moved and the only news is that someone else started it. Losses are
     announced with the same prominence as wins despite the toast being
     designed to feel rewarding: the ladder already shows the rating, so a
     silent loss reads as the app concealing rather than sparing, and a channel
     that only carries good news stops being believed. Push sends through
     Expo's push service rather than direct APNs/FCM — no certificate
     management and one API for both platforms, accepting a third party in the
     delivery path and no per-message priority, and affordable only because
     device registrations, per-channel delivery records and a durable sending
     job already hide the transport; moving to direct APNs/FCM later changes
     one job handler. The site renders the new kinds too rather than staying
     entitlements-only, because both surfaces read the same durable records and
     giving them different news would be a bug in every reading.

120. **Frontline is the active successor experiment; rules 0.5 remains the
     shipped game.** The experiment keeps the deterministic tile-combat core
     and tests one moving five-position frontline, respawns, timed replication,
     and one stationary turret transformation. It is deliberately not a broad
     MOBA economy or free-for-all rules bundle. Early, middle, and late phases
     should emerge from escalation while an outplaying team can still breach
     early. None of the current capture, timing, health, map-size, or turret
     values is a balance verdict: they are isolated-arm starting hypotheses.
     Frontline cannot become the default or a ranked ruleset until its
     deterministic session exists, independently authored Frontline-native
     policies have played the frozen arms, and outcome-blind replay review
     shows that the games are both legible and worth watching.

121. **A submission, a scoring team, and a body are separate identities.**
     Frontline begins with one submitted policy per team, instantiated as
     independent same-artifact runtime lives in the Prime and unlocked unit
     slots. The exact match contract therefore identifies scoring teams,
     submitted participants, stable team-local unit slots, and initial lives;
     it reports actual team, participant, and unit counts explicitly rather
     than asking a bot or model to infer them from currently visible allies.
     Variable allies, enemies, projectiles, objectives, and future player
     counts remain ordered collections with presence/legality masks, so a
     model is not architecturally fixed to three bodies. A form transition
     keeps one runtime's memory; destruction and rebuild create a fresh life.
     Exact team-perception sharing, runtime-budget scope, and later action/form
     eligibility remain separate decisions. The replay-native ML plan stays
     because it supplies the shared canonical-observation, replay-v2, dataset,
     and model-asset path; Frontline must not create a second ML stack.

122. **Frontline lifecycle is a tick-start transaction, while territory reads
     the post-combat world.** The first playable slice is an explicitly
     Prime-only headless session; definitions with fabrication slots are
     rejected rather than partially interpreted. `PrepareTick()` applies every
     due respawn once and freezes the canonically ordered
     `(teamId, unitId, lifeId)` keys whose joint decisions `Step(...)` must
     contain exactly. A Prime destroyed on tick `D` returns at the start of
     `D + 1 + PrimeRespawnTicks`, after exactly that many complete absent
     decision ticks, with a fresh life/runtime identity and authored spawn
     state. Projectiles retain the firing life identity and continue after its
     destruction. Enemy ground movement cannot enter the opposing protected
     pad, but the pad grants no damage immunity and does not block projectiles.
     Turns, movement, existing projectile advances, new shots, simultaneous
     damage, lifecycle queuing, cooldown/energy, objective control, and match
     completion resolve in that order. Actual credited aggregate damage is
     capped at the target's pre-hit health. Only surviving objective-weighted
     bodies count, so a kill can remove a contest and capture on the same tick.
     A final-tick base breach takes precedence over timeout; otherwise the
     signed score is `(active position - centre) × capture threshold`, plus
     team 0 claim progress or minus team 1 claim progress. Runtime fault
     policy, fabrication, Anchor, observations, replay v2, and runtime
     integration remain later contracts rather than guessed behavior in this
     slice.

## 123. Google sign-in through OpenIddict's client, linked on verified email only

External identity is an OpenIddict *client* registration beside the server we already run,
not `Microsoft.AspNetCore.Authentication.Google`. One OAuth library in the process instead
of two, and the next provider — Apple, which the App Store requires alongside any
third-party sign-in — is a registration rather than another handler and another config
shape.

**The callback issues exactly the session cookie the password flow issues**, and that is
what makes this cheap: `/connect/authorize` authenticates from that cookie and bounces to
the SPA login when it is absent, so the mobile app and the CLI inherit Google with no
client change at all.

Linking rules, in order, because the order is the security property:

1. a known `(provider, subject)` signs in — the email is not consulted, so a renamed Google
   account still reaches its own bots;
2. a **verified** email matching a local account links to it;
3. anything else creates an account.

Linking on an *unverified* email would be account takeover: the provider vouches only that
the user controls that provider account, not that inbox. When an unverified address
collides with an existing account the sign-in is **refused** rather than given a second
account — emails are unique, so "create anyway" is not available, and the owner is told to
sign in with their password first.

`User.PasswordHash` is nullable now. A Google-only account has no password, and the login
endpoint refuses null before reaching the verifier — with the same message a wrong password
gets, so it is not an account-enumeration oracle.


## 124. Display names are unique, rejected on a form and suffixed from a provider

Display names identify people everywhere it matters — the ladder, every match row, every
bot card — so they are unique, **case-insensitively**. "Pincer" and "pincer" beside each
other on a ladder are indistinguishable at a glance, which is impersonation rather than a
collision, so the index is on `lower("DisplayName")`.

How a conflict resolves depends on whether anyone is there to ask:

- **Registration rejects** with 409. A name typed into a form is a choice, and quietly
  storing "Pincer2" puts someone on the ladder under a name they did not pick.
- **An external provider suffixes.** There is no form and nobody to prompt; refusing would
  strand a new player on an error page over a name they never typed. Suffixes are applied
  within the 40-character limit by trimming the stem, not by growing past it.

`DisplayNames.FindFreeAsync` is advisory, not a reservation — two simultaneous sign-ups can
both be told the same variant is free. The unique index decides, and
`ExternalSignInService` retries on the violation.

The migration renames existing duplicates before creating the index, keeping whoever held
the name first, and loops until none remain: a suffixed name can collide with a name
already present, and a deploy that renames people and *then* fails on index creation is the
worst of both outcomes.


## 125. Frontline's internal contract is per-life, replay-native, and form-extensible

This completes the later contracts deliberately left open by #122 without rewriting that
historical Prime-only checkpoint. One submitted participant remains one policy/artifact,
but a team owns stable Prime/child slots and the host creates an independent runtime for
every active `(teamId, unitId, lifeId)`. Every new life starts with fresh private memory;
a form transition keeps the exact runtime and memory. Counts, topology, forms, actions,
parameter bounds and all gameplay rules are immutable public match inputs, while variable
allies, enemies, projectiles and objectives remain ordered collections with masks. A later
four- or five-body map therefore changes data volume and training distribution, not the
policy's one-action-per-life interface.

Fabrication is explicit action `fabricate`/100. The Prime must be on its own protected pad
and target one Ready child slot. Capacity is resolved after movement; success reserves the
first free non-Prime pad tile in canonical Y-then-X order and creates the child next tick.
A full pad is a valid attempt that resolves Blocked. Destruction starts the child rebuild
timer; becoming Ready does not auto-spawn it, and explicit refabrication creates a fresh
life in the slot's default mobile form. Old-life projectiles retain exact ownership and
continue crediting actual health removed to the stable firing unit.

Anchor is explicit action `transform`/101 from `child-mobile` to `turret`, irreversible for
that life and illegal on every map-authored Anchor-forbidden tile. A start on tick `T`
completes after objective at `T + windupTicks - 1`; the source form remains Wait-only and
objective-weighted through that tick. Nonlethal damage continues the channel. Lethal
damage emits Destroyed then FormTransitionCancelled; a match ending before a future due
tick leaves the transition pending. Completion preserves actor/runtime/memory, position,
facing, cooldown, energy and damage, and applies
`min(turret.maxHealth, currentHealth + anchor.healthGain)`. The initial turret cannot move,
rotate, capture or contest, has 360-degree perception, and uses separate action
`shoot-direction`/102: one absolute eight-way straight non-programmed projectile with
unchanged body facing and the ordinary global range/resource/collision rules.

One canonical public team observation is frozen for every active life before any runtime
executes; allies share the visible union with exact provenance but never same-tick
decisions. Internal replay v2 stores that exact observation, legality masks, runtime reply,
accepted decision, ordered lifecycle/form events, authoritative post-state and terminal
stable-unit results. Engine and TypeScript validators reject impossible but
self-consistent histories. Web and mobile normalize/present v1 and this internal v2
without changing bridge v1.

This is still an experimental implementation, not a release decision. Official rules
0.1–0.5, replay v1 and protocol 0.1 remain exact; SDK/Guest protocol vNext, canonical WASM
life instances, CLI/App/server admission, datasets/corpus/model assets and ranked use are
later work. [`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md) remains the shared ML
stack because its observation/replay seam is now implemented here while its product
packages are not. No balance value is promoted until Frontline-native all-WASM doctrines,
causal arms, dynamics analysis and outcome-blind review pass the evaluation policy.

## 126. A second renderer, in WebGL, beside the Canvas2D one

`docs/VIEWER-PLAN.md` argued "Canvas2D throughout — no WebGL, no three.js", on the grounds
that rasterisation is not the bottleneck and payload is. That reasoning was about *faking*
depth. It does not survive wanting real depth: offsets and gradients cannot make a shadow
fall across another wall, and no amount of Canvas2D produces a wall you can see the side of.

So there are two renderers. **The Canvas2D one remains the default and is not replaced** —
it is what the CLI ships, what the mobile app loads, and what the golden frames pin. The
WebGL one is opt-in (`?renderer=3d`, or the toggle in the viewer header).

The payload objection turned out to be answerable rather than fatal:

- three.js stays in a **lazy renderer chunk** — currently about 609 KB raw and 162 KB
  gzipped with the arena code — downloaded only if someone asks for the second renderer;
- and it is **stubbed out of the CLI artifact entirely** (`vite.cli.config.ts`), because
  `viteSingleFile` inlines every chunk, so laziness saves nothing there. The current
  theme-scoped artifacts remain roughly 3.6–6.5 MiB with zero three.js in them.

Against 3–7 MB of textures, 131 KB was never the deciding number. What the library actually
buys is the directional shadow map, which is the whole point of building real geometry and
is a night's work to hand-write badly.

**It uses the shipped textures, not new art.** Wall bodies take the 1024² tiling albedo that
previously only filled a flat silhouette; the 16-column topology atlas goes on top as a
transparent cap, in the same order the 2D renderer composites it. Two things had to match
the 2D renderer exactly to stop it looking like a different game: materials are mapped
**once across the arena** in world space rather than tiled per tile (`drawTextureField`
stretches one copy over the whole map and reveals it through geometry — repeating it per
box was the single biggest reason the first attempt looked wrong), and the palette's
`floorTint`/`wallTint` are applied as a colour multiply.

Bots and projectiles started as quads lying **flat on the floor**, on the reasoning that the
sprites are plan views and standing one upright shows a top-down drawing pretending to be a
side view. That reasoning was right about the *sprite* and wrong about the *bot*: a decal on
the ground has no silhouette, casts a shadow the shape of a postage stamp, and vanishes
against a dark floor. See #127.

They are also rasterised through a canvas wherever a sprite is still used as a texture,
because every sprite is an SVG with only a `viewBox` — an unreliable WebGL texture source
that silently yields a fully transparent texture, which `alphaTest` then discards, which is
how the first working build had two active bots and nothing on the floor.

## 127. The 2.5D renderer derives its models from the sprites, and copies the flat renderer's rules

A chassis is not authored as a model. `chassisModel` fetches the look's SVG, extrudes every
filled path, and uses **draw order as height** — a plan-view illustration is layered the way
the object is built, hull then plating then cockpit, so the artist has already described the
relief and the extruder only has to believe them. The alternative was twenty-three
hand-modelled solids to keep in step with twenty-three sprites, obsolete the first time an
artist adjusted one and the first time a cosmetic pack shipped. Now a new look added as a
folder and a manifest gets a 3D form the same way it gets a 2D one.

Walls are traced into outlines and extruded, not stamped as a box per tile: a run of wall is
one chamfered solid rather than a row of cubes with four hidden faces each. Tracing is also
what gives holes, which the boundary ring needs to not pave the floor. Both wall and floor
albedos double as bump maps — the art already contains its relief as painted light and
shade, so reading its luminance as height makes it answer *this* scene's key light.

**Where the two renderers could disagree, the flat one wins**, because they are the same
game and a player switching between them must not see two:

- The owner's accent does **not** tint a chassis. The flat renderer draws these sprites
  untinted; accent reaches the screen as health pips, the facing cone, the selection ring
  and the pool of light on the floor. Emissive accent over hull greys this dark is not a
  trace but the entire colour, and it made every bot a lozenge of team colour.
- A projectile *is* recoloured wholesale, because the flat renderer does exactly that —
  `source-in` over the sprite, keeping the alpha and replacing every pixel.
- Bolt position comes from `boltsAt`, shared with the flat renderer. Facing is eased so a
  bolt banks through an octant change instead of blinking; position never is.

Selection is the other place the two differ on purpose. The flat renderer draws a dashed
ring around the followed bot; here the **bot itself lights up**. A ring wide enough to clear
a chassis became a halo louder than the arena, and tight enough to hug read as drawn across
it — and a marker beside a thing is one you look away to read. The gain is multiplicative,
not additive: hull greys are near-black and barely lit, so *adding* even 0.05 of emission to
one is comparable to everything else it receives, and two attempts at a flat add both came
out as a solid lozenge of team colour. Multiplying leaves near-zero near zero and lifts the
trim the artist already drew bright.

One deliberate divergence: fog darkens the **floor**, not the walls. A horizontal mask plane
can only align at one height, and lifting it above the walls slides it off the floor by most
of a tile at this camera pitch. Walls are static terrain both players have always known
about; the information is in where bots and bolts are, and those are hidden by the actors.

## 128. The 2.5D renderer consumes normalized replay identity and represents forms explicitly

The second renderer does not get a replay-v2 parser. It receives the same version-neutral
`ReplayModel`, `posesAt` interpolation and `replayPresentation` state as Canvas2D and the
hosted bridge. That keeps replay-v1 behavior on the existing normalized duel identities
while allowing Frontline to retain separate participant, stable-unit and exact-life
identity. Bot rigs and selection are keyed by stable unit; damage, destruction and
projectiles remain keyed by exact life, including an old life's projectile after that unit
has respawned.

Every stable unit gets a reusable rig, but it is visible only while the normalized world has
an actor life. Locked, rebuilding and Ready child slots therefore never gain invented bodies
or positions. A lifecycle ring appears only where the replay has an exact reserved
fabrication tile, or at the authored automatic Prime return; queued fabrication gets the
stronger windup. Anchor likewise displays the authoritative pending transition, keeps the
mobile body through the windup, and switches only when the effective form changes. The
turret is a separate circular body with eight radial vanes: stationary and 360-degree at a
glance, without pretending its preserved body facing aims an absolute shot.

Frontline objective geometry renders every authored position and promotes the active one
from presentation state, so three-, five- and later variable-position maps use the same
code. Projectile traversal is one shared normalized derivation with all eight headings and
exact decimal-string identity. Canvas2D remains the default, and the self-contained CLI
build neither includes three.js nor offers its deliberately stubbed renderer as a toggle.

## 129. Frontline uses a bounded tagged binary actor protocol and one isolated WASM instance per life

Protocol/configuration 0.1 remains the exact shipped line-oriented duel path. Frontline
gets separate actor protocol/configuration 1.0 rather than reinterpreting unused legacy
fields. Its dependency-free `NBV2` codec has a fixed 12-byte frame header and
length-delimited tagged fields: unknown fields are skipped, while duplicate, missing
required, malformed, truncated, invalid-UTF-8 and undefined-enum values fail closed. Host
frames are capped at 1 MiB, guest replies at 64 KiB, semantic action/form IDs at 64 UTF-8
bytes, bot selectors and opaque handles at 256 bytes, collection counts at 4,096 and
nesting at 64.

That encoding is an artifact-size decision, not aesthetic preference. A
System.Text.Json NativeAOT spike produced 21.2–21.5 MiB guests and violated the existing
16 MiB artifact ceiling. The custom codec's final tracked built-in guest is 3,341,998
bytes with SHA-256
`9f081e17723a9d155800c258a0613cdba319762dfff598ca35ed82241baff9e4` and input
stamp `52e88112007186066d337ac7e7a6567044b149a7`: 785,134 bytes of growth over the
2,556,864-byte legacy artifact, still below 1 MiB. The shared field codec lives in
BotArena.Sdk so Guest and Runtime.Wasm cannot evolve separate wire definitions.

Negotiation is active. `Hello` identifies actor-capable guests without trusting artifact
metadata; a protocol-0.1 artifact is explicitly executable but Frontline-ineligible.
`Ready` attests the runtime, MatchStart, observation and decision schemas compiled into
the guest rather than echoing the host. Every released host request accepts exactly one
correlated reply. `Fault` terminates a broken negotiation/codec/bot session, while
`Unsupported` names a capability the artifact cannot implement so eligibility never
silently becomes `Wait`.

One submitted-artifact factory owns one Wasmtime Engine and compiled Module. Every active
`(teamId, unitId, lifeId)` owns an independent Store, Instance, guest thread, memory,
globals, deterministic clock/random shims and bot object. A form change keeps that exact
life; destruction disposes it, and respawn or refabrication starts fresh private memory.
Configuration 1.0 pins 64 MiB linear memory, 16,384 table elements, one
memory/table/instance per Store, startup and tick fuel, epoch/wall-clock interruption and
immediate `NOSYS` for `poll_oneoff`; modules with a WebAssembly start section are rejected
so `_start`, ticks and MatchEnd retain an interruption path.

Package 7 coordinates SDK 0.9.0, guest adapter 0.9.0, actor
protocol/configuration 1.0, controlled build-cache provenance, the rebuilt artifact and
CLI package 0.6.0. This is an internal canonical Frontline runtime, not a public shipment:
CLI/App selection, server admission, evaluation and ladders remain Package 8.

## 130. Adaptive music is a compiled, causal presentation graph

A soundtrack is not one long backing file and it is not canonical match state. Local ZIP
archives of aligned PCM stems are untrusted compiler inputs. A reviewed recipe pins their
archive hash, musical grid, stem roles and response curves, phrases, rendered loop seams,
transitions, latency bounds, provenance and approval status. The compiler analyzes the
actual material, omits silent section/stem combinations, preserves relative stem balance,
checks pack headroom and encoded duration, and emits a content-addressed catalog/manifest
graph. A new score is another recipe and catalog entry, not another game/runtime feature.

Runtime adaptation is vertical and horizontal. Sample-aligned stem gains follow immediate
intensity, while sparse, tension, pursuit, combat, climax and resolve phrases navigate the
reviewed graph on musical boundaries. The director consumes only normalized `ReplayModel`
ticks already revealed and derives rules-sensitive objective/health state through the
shared presentation model. Full replays and every live prefix therefore agree on their
shared frames; results cannot leak backwards. Replay-v1 destruction is terminal, whereas
Frontline destruction is only an accent because its exact contract permits respawn.

Music is explicit, website-only media. A user gesture creates one viewer-owned Web Audio
session shared with effects, then the browser fetches only the mutable catalog, one
content-addressed manifest, and the sections needed next. Music has its own bus before the
shared final limiter and cannot close or suspend effects. Hashed pack files are immutable;
the catalog is revalidated. Missing pack URLs remain real 404s rather than SPA fallbacks.
Self-contained CLI viewers stub the score module and do not copy `public/`; HostedViewer
and the mobile bridge remain unchanged. Production release refuses any catalogued pack
until rights are cleared, shipment is approved and every authored loop has completed
human audition. A manually selected public-pilot tier may expose a rights-cleared,
ship-approved pack while loop auditions remain pending; it emits the exact warning and
cannot weaken provenance, manifest-integrity or declared-media checks. Production is the
default tier and remains fully blocked.

AAC in M4A is the baseline output because it keeps the broadest practical Safari/iOS and
hardware-decoder path. The manifest and server MIME policy do not hard-code playback to
that choice, so an Ogg/Opus variant can be measured later without redesigning the runtime.
Codec size alone is not approval: transitions, loops and the combined in-game mix require
listening on representative phones and headphones. Neon Protocol remains
`analysis-reviewed` until every adaptive loop has completed human audition; its source
rights are cleared and shipment is approved.

## 131. Obsidian Foundry is the approved default combat sound-effect pack

Obsidian Foundry is promoted from the four-direction sound lab as the sole compressed
runtime SFX pack. Its lossless 48 kHz stereo masters are deterministically synthesized by
the repository's V2 generator without sampled or third-party material; the runtime
manifest records cleared rights and explicit shipment approval. The other three
directions remain lossless review references under `art/audio` but no longer add bytes or
selection state to a shipped viewer.

The initial runtime contract is deliberately honest and small: authoritative Shot,
Damage, and Destroyed/Disqualified replay events schedule projectile, impact, and
destruction cues. The entitlement-unlock showcase is not relabelled as a match-win sound;
match results and durable reward notifications will receive their own mappings when those
cues are selected.

Effects are opted in by default, with independent persisted mute and volume. Browser
autoplay policy still wins: constructing a viewer does not create an AudioContext, and the
first trusted click or non-modifier key resumes one viewer-owned session shared with the
soundtrack. Enabled music and effects then arm together without a second prompt or replay
restart, establishing the current replay tick as their cursor so activation never
backfills old combat. The full web viewer and self-contained CLI viewer ship this path.
The native HostedViewer remains unchanged until its host bridge owns an explicit audio
activation and control contract. Because the approved cues change every self-contained
replay viewer, the CLI package advances to 0.6.1 under the existing release-order guard.

## 132. PgBouncer is the private application connection boundary, with a separate session alias for notifications

Every application process owns an independent Npgsql pool, so adding otherwise
stateless web and worker nodes can multiply potential PostgreSQL connections
past the primary's fixed budget. PgBouncer runs beside PostgreSQL on the
primary and expose only private port 6432 to exact registered-worker addresses.
Ordinary EF Core, compile, and match traffic uses a bounded transaction-pooled
`botarena` alias. The one PostgreSQL notification listener in each web process
uses a `botarena_session` alias mapped to the same database in session mode,
because transaction pooling cannot preserve `LISTEN`.

Migrations, backups, and administration remain direct inside the primary's
Compose network. A mixed-version deployment keeps exact-address worker access
to 5432 only for the compatibility and rollback window; after every active
release uses PgBouncer, raw PostgreSQL is closed to workers and their direct HBA
rules are removed. This adds connection fan-in and backpressure, not database
high availability. PostgreSQL stays a single primary until measured recovery,
contention, or availability requirements justify moving it.

`pg_stat_statements` is the only PostgreSQL extension adopted with this work.
Audit logging, database cron, partition management, PITR tooling, exporters,
and a full metrics stack remain explicit measured promotions. Backup
correctness and a restore rehearsal outrank those deferred additions; paid
off-site storage remains a separately triggered promotion. The executable
rollout and rollback checklist lives in
`docs/POSTGRESQL-OPERATIONS-PLAN.md`.

## 133. Off-site backups are a value-triggered promotion, with primary-host loss accepted during the hobby phase

Nilbots keeps scheduled, capacity-bounded local PostgreSQL dumps
and rehearse restoring them, but will not yet pay for off-site backup storage.
This protects against bad migrations and accidental logical deletion; it does
not protect against loss or corruption of the primary VPS or disk. PostgreSQL,
the local dumps, and the currently co-located Garage replicas may therefore be
lost together, and that risk is explicitly accepted while production history
is cheap to recreate.

Encrypted backups in a different provider or failure domain become required
before a public competition, payments, valuable user-generated history, a
database move, or whenever losing the primary costs more than recurring backup
storage. Point-in-time recovery through pgBackRest or WAL-G remains a later RPO
decision rather than a prerequisite for the current local-dump baseline.
## 134. Package 8 opens Frontline only as a local experiment and measures dimensions, not “fun”

Frontline enters the CLI through the separate
`nilbots experiment frontline` command, not through historical `play`, ranked
rules resolution, ordinary map catalogs, the App, or server admission. Its
named `frontline-alpha-1` arm stays outside `GameRules.Resolve`,
`KnownNames`, and `ShippedNames`; format-v2 maps are packaged under the
explicit `maps/experimental/` boundary. This makes the mechanics playable and
iterable without accidentally claiming that they are a shipped game mode.
CLI/toolchain version 0.7.0 owns that new surface.

The command accepts actor built-ins, actor projects, or actor-protocol WASM,
creates a distinct participant factory and an isolated runtime for every
life, and emits complete replay v2 plus the self-contained Canvas2D viewer.
In-process execution is a diagnostic convenience; all-WASM remains the
canonical local run. A runtime failure produces a typed non-zero result and a
partial replay instead of silently substituting `Wait`.

Four deterministic reference policies exercise intentionally different
paths—rush, mobile swarm, Anchor/turret bastion, and defensive
counterpunch—but they share one author and count only as calibration/smoke
fixtures. They cannot satisfy the independently authored native-doctrine
cohort required for a product verdict.

Replay-v2 evaluation is likewise separate from the historical slot-based
evaluator. It reports duration and contract-derived phase, fabrication,
Anchor/turret, territorial reversal/comeback, actorless/stagnant, combat, and
action dimensions with per-match rows and fingerprints. It deliberately
defines neither a composite “fun” score nor post-hoc pass thresholds.
Outcome-blind sampling now dispatches between replay versions using header
metadata only.

Adding the reference policies grows the tracked built-in guest to 4,853,279
bytes, SHA-256
`88b6ae1f949dd139fcefcbfc7f144870a27b17a2f98ce58a354738a2908bac5a`,
with controlled input stamp
`f93cd92c9d3985fddaf3abc5d1675c39bdc129d5`. This supersedes Package 7's
artifact as the current development guest without rewriting that package's
historical evidence.

Package 8 therefore establishes a runnable local authoring and measurement
loop, not a balance or ship decision. The remaining gates are an independently
authored cohort, frozen same-cohort/holdout runs, outcome-blind entertainment
review, and a deliberate hosted/admission decision.
## 135. Parallel modes use new typed contract generations, curated playlists, and opaque ladders

Official duel replay v1 and the opened `frontline-alpha-1` rules-schema-2 /
match-contract-schema-1 / replay-v2 evidence remain compatibility generations.
The parallel-mode architecture begins under new experimental rules schema 3,
match contract schema 2, and eventually replay 3; old writers branch on their
stored schema and stay byte-exact. A generic Frontline arm is a new named
experiment, never a reinterpretation of alpha-1.

Product terms are fixed. A **game mode** owns objective/scoring semantics; a
**ruleset** is one immutable mechanic/tuning revision; a **match format** maps
participants onto teams and unit capacity; a **playlist version** pins a
ruleset, format, map pool, scheduler, matchmaking, and admission policy; a
**ladder** is an opaque season/rating population for one playlist version; and
the **match contract** is their fully resolved exact tick-zero input.
FFA Deathmatch is Deathmatch mode plus an FFA format, not another mode.

Forms, actions, same-life form transitions, and one-to-many replication are
closed typed catalogs. Existing typed mechanics are data-tunable; the first
new physical semantic adds one tagged capability, not a rules DSL or
imperative map callback. The initial Split proof retires one surviving source
at the next tick start into bounded fresh descendant lives with isolated
runtimes and explicit lineage. It is not destruction and awards no kill.

Results become canonical tied team standings plus keyed score channels and a
typed mode result. Winner-slot fields remain compatibility projections.
Ratings key by opaque ladder ID rather than rules version. The current duel
ladder first passes through a `DuelEloV1` adapter with exact K=32/floor=100
behavior; an FFA rating policy is a later explicit product decision, not
pairwise Elo in a loop.

The dependency order, schemas, proof fixtures, persistence migration, viewer
boundary, and substantial-change pre-registration are in
[`GAME-MODE-ARCHITECTURE.md`](GAME-MODE-ARCHITECTURE.md). No Split,
Deathmatch, FFA, or numeric starting arm is a balance or ship decision.

## 136. Generic actor contracts negotiate one exact profile and move the controlled toolchain

Actor framing protocol/configuration 1.0 is retained, but its object contract
is no longer inferred from whatever payload arrives next. An absent profile
selects the frozen Frontline-alpha actor generation. New generic matches
require exact profile `generic-actor-match-2`: runtime contract 2, MatchStart
2, observation 2, decision 2, and resolved match contract 2. `HelloAck` and
`Ready` attest the selected profile; unsupported, missing, contradictory, or
late generation switches fail terminally before gameplay. There is no
downgrade or payload sniffing.

The generic SDK boundary mirrors the canonical rules-schema-3,
map-format-3, and match-contract-schema-2 graph, plus variable life-qualified
observations, participant/lifecycle state, nullable sensor capabilities,
generic score/mode state, typed actions and legality, runtime faults, and
event/transition lineage. The Engine remains the full semantic authority.
The dependency-free guest independently enforces canonical syntax,
fingerprints, identity consistency, and explicit byte/node/container limits.
One artifact may implement several bot interfaces, but its negotiated session
invokes exactly one.

This is a player compile-surface and generated-source change. SDK and Guest
adapter move from 0.9.0 to 0.10.0. `BotBuilder` now calls closed-type
`RunDetected<T>` so it can expose all implemented interfaces without creating
a throwaway bot; because that changes generated artifact bytes, controlled
build pipeline moves 3 → 4. The CLI/package moves 0.7.0 → 0.8.0 under the
existing release guard. Historical protocol 0.1, Frontline-alpha contract
objects, replay 1/2, and their fingerprints/hashes remain exact. Generic
gameplay hosting and replay 3 are subsequent packages, not implied shipped
features.

## 137. Generic actor matches execute through one neutral session and replay generation

Schema-3 actor rules and schema-2 resolved contracts now execute through
`GenericActorMatchSession`, not a Deathmatch host with Frontline conditionals.
The session owns shared runtime coordination, movement, combat, lifecycle,
resources, perception, chronology, and participant-fault handling. A closed
typed mode driver owns mode state, score updates, objective completion, and
terminal facts. Deathmatch and Frontline are the first two drivers; a new mode
adds a definition, kernel/driver, and typed result arm without changing actor
identity or the shared world envelope.

Replay 3 is the corresponding generic generation. It records the exact
contract and topology, pre-tick public observation delivered to each life,
submitted and accepted action, authoritative causality, post-state, tied
standings, and a closed typed Deathmatch or Frontline terminal arm. Signed
scores cross JSON as canonical decimal strings. The Engine reader and
web normalizer reject unknown arms, extra fields, impossible chronology, and
contract/result disagreement before the hosted bridge carries a typed result
to mobile. Replay 1 and the opened Frontline-alpha replay 2 remain byte-exact
separate generations. This creates an executable experimental seam; it does
not route a generic mode through public App/server admission or a ladder.

## 138. Fabrication and Split reserve one joint lifecycle claim space

Source-preserving fabrication and source-retiring Split first build
provisional bundles from the same post-movement world. One family-neutral
arbiter then blocks every operation in a connected conflict component sharing
any stable slot, output tile, or operation ID. Neither mechanic receives
priority from family type or collection order. Existing pending claims,
active occupancy, and permanently reserved automatic-return tiles are part of
the same legality boundary.

A queued fabrication captures source lineage/pose, target slot/form, output
tile/facing, and due tick. It survives source movement, destruction, or a later
Split, because it is already source-preserving work; participant
disqualification is the explicit cancellation override. At tick start,
returns/readiness settle first, due fabrication settles before due Split, and
same-life completion follows. Output-tile projectiles are consumed without
damage before a fresh target life/runtime is created. Split still retires its
source and creates fresh isolated descendants. Same-life form changes retain
the exact runtime and private memory.

Static rules, map, topology, and objective facts remain immutable MatchStart
input. Learned private history belongs to one runtime life: respawn,
refabrication, and Split descendants start empty, while team perception shares
only the current frozen observable union. There is no implicit parent,
sibling, or historical team-memory copy. A fabrication configuration whose
unavailable placement result would fault a participant remains rejected until
the host has a causal gameplay-fault API; `Blocked` and `Rejected` execute now.

## 139. Legacy competition rows receive pinned playlist, season, and ladder identity first

The additive competition migration begins by assigning deterministic
legacy-import playlists and immutable playlist versions, explicit seasons,
opaque ladders, and ladder-keyed ratings without changing Duel results or
public response shapes. Ranked and unranked admission dual-write the pinned
identity; workers execute the pinned rules and repair nullable compatibility
rows; finalization repairs identity/rating links while retaining exact legacy
Elo behavior. `Ladder.AwardsAchievements` is authoritative, and
`SeasonOpeningRank` is a nullable opening snapshot rather than current rank
under another name.

The application backfill is advisory-locked, repeatable, and transactionally
isolated. Operators run it after the nullable expand migration and again after
old writers drain, before switching reads or enforcing non-null identities.
This foundation does not claim normalized generic entrants/team results,
reveal-ordered settlement, generic APIs, or an FFA/team rating policy; those
remain the explicit next persistence stages.

## 140. Hosted Frontline Labs stops at one off-by-default, setless, unranked H2H match

The first App consumer of the generic actor architecture is immutable playlist
`frontline-labs` version 1. It pins ruleset `frontline-labs-1`, map
`frontline-labs-01`, head-to-head format, `single-match-v1`,
`direct-challenge-v1`, and exact admission profile
`generic-actor-match-2`. `BOTARENA_FRONTLINE_LABS_ENABLED` defaults false and
controls catalog discovery, new admission, and activation of newly compiled
generic-only artifacts. While it is false, a new artifact must also support
`legacy-duel-0.1`. Turning it off never deactivates an existing artifact or
changes/cancels an already queued match whose playlist identity is pinned.

Admission requires exactly two distinct eligible submitted bots, with the
first entrant owned by the caller and both active versions compile-attested
for the exact generic profile. It creates one setless, unranked `Match`, pins
participant team IDs, and runs the generic WASM session. Canonical
`MatchTeamResult` standings and keyed signed `MatchTeamScore` rows are result
authority. Participant outcome/health and a unique winner-slot may exist only
as compatibility projections.

Hosted generic matches store replay format 3. Before broadcast completion the
existing replay endpoint returns a validated canonical prefix with terminal
result and hash withheld; after reveal it returns the complete document. The
bot-detail Labs panel navigates to the existing direct match page and
version-normalized viewer instead of introducing a parallel viewer or a
ranked-looking mode hub. Labs matches stay out of the legacy Duel feed, bot
history/statistics, achievements, result notifications, ratings, and
leaderboards.

Execution is not inferred from admission or replay format. `PlaylistVersion`
pins `ExecutionPolicyId` and `ExecutionEngineVersion`; Frontline Labs v1 pins
`generic-actor-v1` and generic engine `1.0.0`. A versioned hosted-definition
registry validates the exact canonical definition before execution. Each
immutable hosted playlist version has its own durable queue capability; each
configured generic lane claims any capability registered in its binary. A
legacy worker cannot claim generic work, and an older generic worker leaves a
new playlist version pending instead of exhausting its retries. Adding modes
does not multiply configured concurrency. Unknown/infrastructure failures
reach the normal three-attempt queue retry; the final failed attempt
terminally fails the match without exposing the stored operator exception in
the public match response. A historical definition and its job capability
remain registered while any pending/running job can reference them; removal
requires an explicit queue drain or migration.

Labs admission retains the general unranked ceiling and adds visibility-wide
transactional pilot defaults: 10 starts per account per 24 hours, one active
match per account, and four active matches globally, plus a two-per-minute
account-and-network burst guard. The global and account checks use advisory
locks. Initial bootstrap and routine fleet deployment propagate the disabled
flag and these limits to every web/compiler replica through a narrow,
validated, secret-preserving environment sync; generic workers default to one
lane. Binary rollout occurs everywhere with the flag false before a separate
configuration rollout may enable it. That rollout drains and stops all
compiler roles, propagates one validated configuration to every node, starts
compile workers before exposing any enabled web replica, and then smoke-tests
a generic-only build. After Labs data exists, rollback is limited to binaries
that include the profile-aware compatibility guards and scoped legacy
backfiller.

This slice creates no `MatchSet`, normalized series entrant/result, season,
ladder, rating, or reveal-time series settlement. FFA, 2v2, Deathmatch,
multi-match series, and ranked generic play remain future consumers of the
same typed topology/result envelopes. In playlist v1, Split retires an
eligible untransformed Prime into two fresh mobile replicas with divided
health; replicas cannot Anchor, and only a mobile child created through
Fabricate may transform into a turret.

The map and all numeric mechanics remain `experimental-unvalidated`.
Execution availability is not a balance, entertainment, or ship verdict. The
flag must remain disabled in any deployment until the existing release guard
passes and CLI/package 0.9.0 from the exact compatibility revision has been
published and tagged `cli-v0.9.0`.

## 141. Frontline Labs gets an exact local authoring loop before its balance cohort

The CLI adds `nilbots experiment frontline-labs` as a local, quota-free
authoring boundary around the immutable hosted Frontline Labs v1 definition.
It accepts two explicit external `IGenericActorBot` projects or generic-profile
WASM artifacts, supports deterministic seed batches and side swaps, executes
`FrontlineLabsDefinition.Create()` through `GenericActorMatchSession`, and
writes canonical replay 3. The default WASM path uses the same
`WasmGenericActorRuntimeFactory` as hosted execution; in-process remains an
explicit diagnostic loop.

`nilbots new <Name> --profile generic-actor` provides the missing authoring
surface without changing the shipped Duel default. The scaffold reads variable
topology, map, mode, and action legality from the negotiated contract, and
resolves action codes from the current legality entry rather than copying
numeric selectors. There is no generic built-in opponent: both entrants are
always named, which keeps a cohort from silently testing against the unrelated
Frontline-alpha bot interface. `nilbots verify` now validates replay 3 through
the Engine's canonical serializer, including payload hash and nested contract
fingerprints.

This is tooling around the existing playlist, not a gameplay or product
mutation. Playlist v1, its fingerprints, hosted admission, quotas, and disabled
feature flag remain unchanged. The command creates no App match, submission,
season, ladder, rating, or ranking, and it does not enable Labs. Because the
CLI/package and packaged templates changed, the release target moves from the
unpublished 0.9.0 compatibility revision to 0.9.1 and must be tagged
`cli-v0.9.1` before Labs may be enabled. SDK/Guest remain 0.10.0 and controlled
build pipeline remains 4.

## 142. Labs balance waits for truthful action masks and an Anchor-safe SDK

The first frozen four-doctrine Labs cohort is retained but cannot justify a
numeric rules change. Its 36 verified WASM matches had no participant-slot side
advantage, yet 24 reached MaxTicks, half were stalled and looped, Fabricate
blocked 15,417 times, and Split completed zero times. Outcome-blind review
independently found long inert endings.

The causal defect is in generic action availability, not a capture or combat
number. Fabricate had been advertised whenever a compatible Ready target
existed even when the source was outside its declared region. Split had been
advertised from a matching form even without enough health or Ready compatible
slots. `Available` now includes source-local Fabricate eligibility and
Split source/health/slot prerequisites. Placement, movement, and intersecting
lifecycle claims remain joint-resolution outcomes: predicting them in the
mask could leak hidden occupancy and would reject actions which become possible
after a simultaneous vacancy.

That correction activated Fabricate, Split, and Anchor in unchanged entrant
artifacts. Anchor then exposed two separate public SDK defects. A one-tick
end-of-started-tick form change canonically emits event chronology with
`startedTick == dueTick`; SDK 0.10.0 incorrectly rejected it before player code
ran. Event payloads now permit equal ticks, while in-progress transition state
still requires a future due tick. An enemy transition-created life may also be
visible while privacy policy redacts both its parent and occurrence handle;
SDK 0.10.1 incorrectly required the redacted handle. Transition-spawn events
now accept that canonical redacted shape while retaining the public transition
ID, and still reject a disclosed parent without its operation handle. These
repairs change embedded player bytes, so SDK/Guest move to 0.10.2 and
CLI/package moves to 0.9.3; controlled-build pipeline 4 and every immutable
Labs fingerprint remain unchanged. The required release tag is therefore
`cli-v0.9.3`.

Local Labs also retains sandbox diagnostics after life disposal and prints the
precise local failure plus peak completed-tick fuel when a WASM participant
faults. Public replay faults remain stable/redacted. The next balance evidence
must rebuild probes and entrants against 0.10.2, use a shorter two-seed mirrored
cohort, and demonstrate lifecycle coverage before any one-variable numeric arm.

## 143. Phased Frontline pacing is optional canonical contract data

Static capture tuning did not remove the repetitive late tail in the first
Frontline Labs population. A phased candidate therefore needs to be legible to
hand-authored bots, ML policies, replay tooling, and fingerprints without
changing immutable hosted `frontline-labs-1`.

`FrontlineCaptureDefinition` now supports an ordered, data-defined gain
schedule. A scheduled contract includes every phase ID, start tick, and gain;
the engine and SDK resolve the active phase from the authoritative tick. A
static definition omits the optional property, preserving its exact canonical
bytes and fingerprints. The first candidate keeps gain 1 through tick 299 and
uses gain 2 from tick 300 under its own ruleset ID.

The additive reader and bot-facing helper change embedded SDK/Guest bytes, so
SDK/Guest move to 0.10.3 and CLI/package to 0.9.4. Actor framing,
generic-actor profile/schema numbers, replay v3, and controlled-build pipeline
4 remain unchanged: older artifacts still run unchanged on contracts that omit
the schedule, while schedule-aware rules require a rebuilt artifact.

## 144. Turret remobilization proves the transform architecture but misses its pacing gate

A local-only `frontline-labs-1-experiment-mobilize` definition adds the
declared `mobilize` action and a `turret -> child-mobile` same-life route using
the existing generic action/transition contracts. Actor identity, runtime
memory, position, facing, cooldown, and energy persist; health is preserved
but capped to the mobile maximum. Anchor is reversible only in this candidate,
while Mobilize is irreversible for that life, so an Anchor/Mobilize healing
cycle is impossible. Immutable hosted `frontline-labs-1` and its fingerprints
remain byte-identical.

The pre-registered 12-match WASM screen verified 15 Anchors and 15 Mobilizes
with zero faults, no same-life re-Anchor, exact non-Bastion control
trajectories, and no Bastion drift before its first Mobilize tick. Mobilize
eliminated all dual-turret no-progress ticks, but the candidate policy left all
six Bastion games classified stalled and looped, reduced breaches from seven
to six, and increased MaxTicks from five to six. The action and generic
architecture are retained as an isolated extensibility proof; neither the
mechanic nor this policy is promoted into hosted v1.

Replay-v3 dynamics now classifies same-life routes from contract-declared
source/target objective weights. Weighted-to-zero routes are Anchor,
zero-to-weighted routes are Mobilize, and only Anchor target forms count as
fortified for turret-deadlock detection. This prevents a mobile target of a
future transition from being mislabeled as a turret.

## 145. Balance work becomes a fingerprinted Lab; automatic companions remain an explicit arm

The experiment-wide evidence/holdout details in this historical decision were
superseded by study blocks and commit/reveal in decision 147.

Balance evidence is now organized around an immutable candidate tuple:
`mode + ruleset + map + match format`. A checked-in spec declares the complete
factor product, paired and sealed holdout seeds, exact contract fingerprints,
runtime commands, retained source/WASM hashes, qualification status, and
evidence class. The first mode-independent runner verifies complete mirrored
within-population cross-play, rejects replay/provenance/fault drift, builds a
payoff matrix, adapts replay-v3 Frontline dynamics, and reports a balance
vector plus same-artifact factorial contrasts. Missing exploitability,
equilibrium, tier-gradient, ablation, or human-review evidence is explicitly
`not-measured`; there is no composite score and no champion-only pruning.

The engine/SDK contract adds a third initial slot lifecycle:
`dormant-automatic-activation-at-tick`. On the exact due tick it creates a
fresh generation-declared life at the assigned spawn, before fabrication and
replication, able to act immediately. Its parentless life-origin reason is
`automatic-activation`, distinct from tick-zero deployment and ordinary
post-destruction return. Validation, canonical wire reading, replay projection,
and chronology all preserve that distinction. Hosted `frontline-labs-1`
remains byte-identical and manual.

The first local Frontline arm activates children at ticks 120/260 and uses
automatic 30-absent-tick child returns. It deliberately removes Prime
Fabricate and Split, retaining child Anchor/turret, because already occupied
child slots cannot also be honest capacity for those operations. The
comparison is therefore named a progression-policy bundle, not a single
boolean ablation. Its three map variants and ruleset have separate
fingerprints and are never selected implicitly.

The 12-replay, one-seed Balance Lab run is retained only as
`infrastructure-smoke`: both bots are cumulatively unqualified. Its claimed
causal map-policy effects were superseded after decision 147 found mismatched
private seed profiles between progression arms. It promotes no candidate.
SDK/Guest advance to
0.10.4 for the additive lifecycle/origin contract and CLI/package advances to
0.9.5; actor framing/configuration and controlled-build pipeline 4 remain
unchanged.

## 146. Balance evidence pins topology, evaluation policy, and scoped qualification

The Balance Lab schema advances to slice 2. Each resolved candidate now
declares a descriptive topology profile plus the independently replay-verified
topology fingerprint, in addition to mode/ruleset/map/format and aggregate
match fingerprints. This prevents one-controller three-body H2H, five-body
H2H, true multi-participant teams, and FFA from sharing an ambiguous
experimental identity.

An explicit `evaluationProfileId` owns lineup and payoff semantics. The only
implemented profile is `two-team-zero-sum-v1`; team-lineup and FFA
general-sum evaluation remain separate future profiles rather than
conditionals in the duel matrix.

Population and entrant manifests now carry exact qualification suite/version,
profile, qualification-contract fingerprint, evidence hash, T/C awards, and
balance-evidence eligibility. The driver rejects identity or eligibility
mismatch and emits a hard `balanceVerdictEligible` gate. It may retain
descriptive diagnostics from an unqualified infrastructure population, but
those measurements cannot select a candidate; the separate promotion gate
also remains false while required evaluation layers are unmeasured.

The immutable `frontline-qualification-1` remains the suite-1 T4
entry-initiative component. CLI 0.9.6 adds a distinct WASM-only
`frontline-qualification-2` foundation profile. Its first
`contract-auto-determinism` component runs both participant assignments twice,
requires verified identical replay hashes, zero faults/disqualification, and
the declared automatic child life under an immutable shortened contract. It
awards no tier and is not balance eligible until the remaining T1/T2
identity, path, fire, evade, and fresh-life holdouts exist. SDK/Guest 0.10.4,
actor framing, replay 3, and hosted Labs remain unchanged.

## 147. Freeze Balance Lab pilot architecture and make qualified population the critical path

The first Lab smoke revealed a causal-provenance bug: manual and automatic
progression arms derived private actor streams from different ruleset
identities. Equal numeric seeds therefore did not supply common random
numbers. Both duel-depth arms now declare
`frontline-labs-duel-depth-1` as their shared seed profile, while preserving
their distinct rules and match fingerprints. The old cross-progression smoke
effects are superseded; a corrected 12-match WASM smoke verified all replays
and common initial actor streams, but remains non-voting.

Balance Lab schema 3 replaces one experiment-wide evidence label with
study-scoped roles: compatibility sentinel, mechanic causality,
rules-native product, infrastructure smoke, and adversarial sentinel. Each
block owns candidate/population membership, qualification profile,
self-play policy, and common-randomness declaration. A versioned decision
profile owns voting tier, lineage, coverage, multiplicity, and required
evidence-layer gates. Diagnostic blocks remain visible without poisoning or
promoting voting evidence.

The runner now publishes and hashes one complete executable bundle, rejects
source/toolchain drift during execution or resume, records independent
lineage/doctrine/authoring-budget identities, rejects duplicate source or
artifact populations, and reports finite-population paired contrasts plus
leave-one-lineage-out sensitivity. Pair/seed intervals are explicitly
conditional on the frozen population and are not population-generalization
claims. The internal Frontline pilot floor is four independent cumulative
T4+ lineages; two lineages remain diagnostic only.

Engine-derived candidate blocks replace hand-copied fingerprints through
`--print-candidate-contract` and
`scripts/frontline-balance-candidates.py`. Hidden final seeds use an external
nonce-backed commit/reveal/consume artifact; checked-in seeds are never
described as sealed. Open isolation promises are frozen in
`balance/frontline-ablation-debt-v1.json`.

This is the pilot architecture freeze. T7/T8 qualification, empirical
equilibrium/best-response analysis, and automated candidate search are
deferred until a credible population exists. The next critical path is
cumulative T1–T4 qualification, at least four independent T4+ Frontline
lineages under an equal budget, the registered six-cell
topology-by-progression experiment, and outcome-blind replay/DX review. Duel
conclusions remain provisional for 2v2/3v3, and FFA retains its separate
general-sum evaluation profile and ladder.

## 148. Qualified evaluation bots become explicit launch-population assets

Frontline evaluation cohorts are permanent source/artifact lineages, not
disposable tournament inputs. Lower-tier exact-boundary instruments and every
meaningful passing revision are retained for calibration; independently
authored T5/T6 doctrines supply the eventual verdict population.

A revision may advance from `lab-only` to `official-population` only through
an explicit future promotion manifest that pins its source tree, author
packet, build identity, WASM, qualification evidence, playlist profile, and
entertainment-review result. Promotion references unchanged archived bytes;
the server must never publish every Lab directory implicitly.

Official bots are visibly system-owned launch opponents: T2 for onboarding,
T3/T4 for ordinary population depth, and T5/T6 for aspirational play and
rating anchors. They may backfill sparse playlist queues, have independent
rating/history per playlist, and cannot win human prizes or champion claims.
Restricted variants, metric attackers, and pathological sentinels remain
Lab-only.

## 149. Count effective doctrines, not artifacts, and specialize population by tier

One fixed artifact quota at every tier is rejected. T1/T2 and most T3 bots are
calibration instruments in a small behavior space: keep a canonical set of
distinct archetypes that passes Tn and demonstrably fails T(n+1). Repeated
implementations of the same objective/shoot/dodge policy do not add evidence.

The first directional pilot still requires at least four independently
authored effective T4+ doctrines. Launch-balance evidence concentrates effort
at T5/T6, where Balance Population v1 targets at least six effective doctrines
spanning predeclared strategy cells and continues authoring until
leave-one-doctrine-out conclusions stabilize. T7/T8 shifts to bounded
best-response/search attacks rather than manual enumeration.

The Lab reports artifact count separately from a diagnostic
effective-doctrine estimate. Versioned v1 pair evidence combines normalized
payoff-row, accepted-action, form-occupancy, and objective-residence distances;
its fixed thresholds remain non-gating until calibrated against known
redundant and distinct populations. No clustering score may silently merge
entrants or discard their immutable records. Same-model authoring uses
different doctrine briefs and equal budgets rather than repeated identical
prompts.

Exact-tier instruments and public launch opponents are related but not
identical roles. A natural, entertaining retained revision may be promoted as
a system-owned opponent, while deliberately crippled instruments, ablations,
and sentinels remain Lab-only. Qualification reuse is profile-scoped: new
verbs, topology, or strategically material contract changes may require new
instruments.

## 150. Cumulative T3 is an immutable tactical boundary, not a match-strength label

`frontline-qualification-4` freezes
`frontline-duel-depth-union-t3-v1`. It always reruns and hash-links the exact
suite-3 cumulative T2 prerequisite, then executes mirrored positive-curve,
strict-corner, remaining-range cadence, missed-shot cooldown, and local
transform-safety probes. A clean T3 failure retains the prerequisite tier;
runtime, artifact, contract, controller, or replay invalidity remains exit 2.
T3 is still below the cumulative T4 numeric-balance voting floor.

The negative curve scenario uses a visible target with an invalid
wall-terminated intercept. A proposed unseen-projectile version was rejected:
under the authoritative strict-corner model, the same corner that blocks the
projectile also blocks its visibility, so the scenario could not fairly
require a reaction from public observations.

HouseApprentice and ArcApprentice form the first retained adjacent
qualification pair. HouseApprentice passes T2 and fails the positive-bend and
cooldown components; ArcApprentice adds only contract-driven legal curve
preview and objective-first routine tempo, then passes T3. Both preserve
source, controlled WASM, profile-scoped evidence, and replay-byte manifests.
ArcApprentice is not called an exact T3 boundary until cumulative T4 measures
its upper edge.

## 151. The Docker WASM builder matches the host CPU; emulated builds run single-node

`nilbots build` on Apple Silicon intermittently sat at 0% CPU forever with an
empty quiet-verbosity log. Captured `/proc` evidence from three reproduced
stalls showed the same signature: the entry `dotnet publish` blocked in
`rt_mutex_schedule` after spawning only some of its MSBuild worker nodes, the
spawned workers parked in `futex_wait_queue` mid-handshake, and VBCSCompiler
idle — a multi-node fan-out deadlock under Rosetta's x64 emulation inside the
Docker VM. Measured baseline: 4 stalls in 45 builds (~9%), healthy builds 18 s
serial. `DOTNET_EnableWriteXorExecute=0` alone was tested and refuted (stalled
within 6 builds). Forcing `-maxcpucount:1 -nodeReuse:false
-p:UseSharedCompilation=false` removed every cross-process handshake and every
stall, but serialized the framework-object clang compiles behind
`BuildInParallel` and doubled the build to 36 s.

The structural fix: the pinned NativeAOT-LLVM release also publishes a
`runtime.linux-arm64` compiler host (the historical "Linux x64 only" note was
stale), and it emits **byte-identical** modules — verified by full SHA-256
equality on the same sources. `run-wasm-publish.sh` selects the container
platform matching the host CPU (override: `BOTARENA_WASM_DOCKER_PLATFORM`),
keys the cached builder image per architecture, and the generated workspace
plus `BotArena.WasmGuest` reference the compiler host package conditionally on
the build-process architecture. A native-arm64 `nilbots build` measures 9
seconds end-to-end versus 18 seconds under emulation, and no platform-matched
configuration emulates at all. The single-node, no-build-server, and W^X
guards remain only for an explicitly emulated fallback.
`WasmPublishEmulationGuardTests` pins both command shapes; the emulated
fallback was re-verified against the prior amd64 image with identical hashes.

`BuildPipelineVersion` stays at 1 because player artifact bytes and cache keys
are unchanged. In the composed qualification branch the CLI compatibility
version advances to 0.9.7, after the 0.9.6 qualification release. The
campaigns also surfaced a second, independent fault: deleting and recreating
the workspace directory between builds occasionally raced macOS virtiofs into
a transient empty container view (`MSB1009`, roughly 2% of Docker builds), so
`BotBuilder` now empties the bind-mount root in place instead of replacing its
inode. If an `osx-arm64` compiler host ever ships upstream, the Docker
requirement itself could be revisited.

## 152. Cumulative T4 measures positional commitment and gates entrant evidence

`frontline-qualification-5` freezes
`frontline-duel-depth-union-t4-v1`. It always reruns and hash-links the exact
suite-4 cumulative T3 prerequisite, then executes five mirrored components:
useful-ground suppression, pressure-lane entry, objective-preserving threat
response, rotation after a captured front, and a thin-fronts map holdout. A
clean failure retains the prerequisite tier; artifact, runtime, contract,
controller, or replay invalidity remains exit 2.

T4 is the first tier whose passing artifact may set entrant-level
`balanceEvidenceEligible=true`. That field is necessary but not sufficient:
population lineage breadth, study validity, multiplicity policy, and human
replay review remain independent verdict gates. The holdout is a second
declared map, not evidence that T4 generalizes to every future topology.

ArcApprentice now forms an exact T3 upper boundary: it passes four T4
components but, from one mirrored assignment, repeatedly retreats from the
current-map choke and never enters the objective. BreachApprentice adds one
contract-driven initiative rule, passes cumulative T4 from both assignments,
and preserves its source, WASM, report, and replay-byte manifest. Because
Breach is an adjacent House/Arc revision, all three artifacts remain one
authoring lineage and one effective doctrine for pilot-breadth accounting.
The next balance experiment therefore authors independently briefed T4+
doctrines rather than treating the qualification lineage as a population.

## 153. Classes are data-only chassis on the generic form catalog; kinematics never vary by class

The first class slate — `striker` (one-bend prediction duelist), `bulwark`
(durable short-sighted holder with reversible turret commitment), and
`fabricator` (fragile economy engine with earlier, faster companions) — is
implemented entirely as data: `FrontlineLabsClassDefinition` stat blocks
expand into per-class forms, vision/attack profiles, fabrication/anchor/split
routes, and per-slot lifecycle assignments inside
`frontline-labs-1-experiment-classes-<a>-vs-<b>` arms that compose with the
duel map arms and share the `frontline-labs-classes-1` seed profile.
Class pairs are canonical in ordinal ID order; fairness comes from mirrored
bot assignments, never from a second swapped contract. Mirror pairs collapse
to one catalog so striker-vs-striker contains each entry exactly once.

The axis split is the load-bearing decision: classes may vary durability,
vision shape/range, fire tempo, projectile range, shot language (the
parameterless `shoot-straight` action versus one-bend programs), anchor
reversibility and turret durability, and fabrication economics (unlock
ticks, rebuild delay). They may never vary movement speed, projectile
speed/damage, or the movement layer — those constants define the parity
structure of the exact duel analysis, and varying them would fork that
layer per class pair. Projectile range is the one admitted geometry
variable and is disclosed as such. Classes are conceptually mode-neutral
(the chassis expands into the generic actor catalog and per-slot topology
that any mode consumes); they are deliberately packaged as a Labs
experiment arm first and lift into a mode-neutral catalog when a second
mode schedules its first class experiment, exactly like the evaluation
profiles. No class arm is hosted, ranked, or promoted; the stat values are
pre-registered candidates for the class-pair factorial, and the class
choice mechanism (a player-facing admission input) is explicitly out of
scope for the lab, which needs only pre-registered topology cells.
Pinned by `FrontlineLabsClassesDefinitionTests` and the classes CLI arm
test; the shared `BuildRules` assembly point keeps limits, seed mechanics,
mode, perception, collision, and tick resolution byte-identical across
every arm.

## 154. One exclusive verb per class; companions are automatic unless fabrication is the class skill

The slate revision sharpens #153 into one exclusive verb family per class
and resolves the manual-fabrication chore verdict. **Striker** keeps the
one-bend programs. **Anchor becomes Bulwark-exclusive and class-wide**: the
old child-only restriction guarded against an irreversible prime
self-brick, but Bulwark's Anchor is reversible (Mobilize back, once per
life), so a fortifying prime is a priced commitment rather than a trap.
Each source form anchors into its own turret form
(`bulwark-prime-turret`, `bulwark-child-turret`) so the parameterless
Mobilize resolves a single return target, and the prime's three-tick
windup versus the child's one makes the stronger commitment a visible,
punishable window — pre-registered values, not tuning. **Striker and
Bulwark receive companions automatically** at their unlock ticks
(the earlier finding stands: an explicit queue with no price, alternative,
or placement choice is a dominant chore). **Fabricator is the only class
that fabricates**, and its verb carries exactly the ingredients the
doctrine demands for manual spawning: placement choice (the child
materializes beside the prime in the field, never on a protected pad),
timing risk, and a scarce alternative (the action was a shot or a move) —
at earlier unlocks and faster rebuilds, giving the class the lowest floor
and the highest ceiling. **Split is absent from every class arm** and
reserved as the identity verb of a future swarm class: it was a trap for
the fragile Fabricator, off-identity for Bulwark, and unused in every
retained screen. The composite nature of each chassis is registered as
ablation debt (`classes-composite-chassis`), and class capability remains
fully contract-visible to opponents: routes, windups, reversibility, and
turret stats are all in the resolved contract before tick zero. Class as
bot-level identity (manifest declaration, arm resolution from the
entrants' declared classes) and a typed `classId` in the next contract
generation for ML-stable observation remain the follow-on phases; the
class information is already derivable from the contract and replays, so
no authored population requires re-authoring when the typed field lands —
retained sources rebuild and requalify mechanically.

## 155. Facing-decoupled movement was never decided and is reopened as a candidate

Schema 3 declares movement as one absolute cardinal tile without changing
facing. That choice — functionally universal strafe — carries no numbered
decision, no measurement, and no reference to the duel-game record that
convicted the same mechanic: #49 held strafe for oscillation-dodging and
+80% game length, and the 0.5 record calls it the dodge-everything
regression 0.5 removed. Every generation-3 analysis layer (the exact duel
enumeration's mobile-choice language, the evade probes, the class slate's
kinematics discipline) subsequently built on the undocumented choice as a
given. The wave-1 class factorial's dominant standoff-kiting doctrine and
its 500-tick draws are consistent with the original conviction, and the
product owner independently rejected the visual result on review.

Disposition: the current model stays the measured baseline; facing-coupled
movement enters the pipeline as pre-registered typed arms rather than a
hot rules change. The first candidate is move-sets-facing (a step turns
the body to the movement direction; rotate remains free aim), which kills
backpedal-kiting while preserving one-action dodges at an aim cost; full
tank movement (forward-only plus turns) is the deeper cut requiring a
complete exact-analysis and probe recomputation. Registered as ablation
debt `movement-facing-coupling`, required before the internal pilot's
first movement-touching verdict. Contract-driven bots survive any arm
through the movement legality mask. The blind-review verdict on standoff
watchability decides the arm's priority.

## 156. Facing coupling is a typed movement-profile policy with an inert default

#155 registered facing-coupled movement as pre-registered arms rather than a
hot rules change; this is that capability. `ActorMovementProfileDefinition`
gains one optional `ActorMovementFacingCoupling`, so the policy belongs to a
movement profile — the same place the movement layer already lives — and a
form inherits it by selecting that profile. **PreserveFacing** is today's
behaviour and the default. **FaceMovementDirection** sets the life's facing to
the direction it moved on a *successful* step, before the Movement event is
emitted, so that event's facing payload is the change's evidence; a Blocked
step changes neither position nor facing, which keeps the wall-bump from
becoming a free turn. **FacingLocked** restricts the published Direction
domain of Movement-kind actions to the mover's current facing while Rotation
keeps all four, and resolution defensively Blocks an off-facing movement that
somehow reaches it. Bots read all of this from the legality mask, so a
contract-driven entrant needs no re-authoring to play any arm — the
observable difference is which directions the movement mask offers and what
the Movement event reports.

The load-bearing part is the fingerprint discipline. The canonical writer
emits `facingCoupling` **only when it is not PreserveFacing**, following the
exact precedent of the optional capture-gain schedule, and both mirrors — the
SDK canonical reader and the web replay-v3 normalizer — reject an explicitly
inert value as a second, non-canonical encoding of the same contract. That is
what lets an immutable hosted contract acquire a new capability at all: every
existing ruleset, `frontline-labs-1` included, keeps byte-identical rules,
map, and match fingerprints, and the pinned golden tests passed unmodified.
The same additive discipline runs through the SDK wire mirror (an absent field
means PreserveFacing) and the engine's replay-v3 causality validator, which
previously reconstructed every facing change from rotation evidence and now
accepts a Movement event as the evidence for a coupled step — the second time
this session that a new authoritative fact had to be taught to a validator
that reasonably assumed the old exclusive cause.

**Absolute rotate is deliberately held constant.** Rotation remains a free,
absolute, one-action turn to any cardinal in every arm, so the A/B measures
exactly one mechanic: whether movement spends the aim. Rotation granularity —
relative turns, multi-tick turns, or a turn rate that would make
FacingLocked genuine tank movement rather than a legality mask — is named here
as follow-on debt (`movement-rotation-granularity`) and is a separate
pre-registered arm, not a tuning knob to fold into this one. #155's full tank
movement still requires the complete exact-analysis and probe recomputation it
named.

The arms exist for the pre-registered A/B, not as a ship decision:
`frontline-labs-1-experiment-move-sets-facing` and
`frontline-labs-1-experiment-facing-locked` on the base contract, plus
composition with the class slate so the movement factor can be measured inside
the wave-1 doctrine rather than only against class-free bots. Composed arms
are identified `frontline-labs-1-classes-<a>-vs-<b>-sets-facing` /
`-facing-locked`: the `-experiment-classes-` segment is dropped and the
coupling token shortened because canonical IDs are capped at 64 characters and
the longest class pair leaves no room — a naming compromise, and the
PreserveFacing pair keeps its historical `-experiment-classes-` identity byte
for byte. Each family keeps its existing seed profile. `nilbots experiment
frontline-labs --movement <preserve-facing|move-sets-facing|facing-locked>`
selects the arm and composes with `--classes` and `--duel-map`; the default
adds no ruleset suffix and leaves every existing arm identity unchanged.

## 157. Movement factorial evidence: facing-locked halves class imbalance; the bend prediction failed

The pre-registered classes × movement × maps factorial
(`frontline-classes-wave-2-movement-factorial-v1`, 54 cells, 486/486
verified matches, seeds 180001/210011/240007, unopened sha256
commit-reveal holdout) ran on the `classes-wave-1-r2` population — six
frozen revision-2 lineages, two per class, all cumulative T4 on
`frontline-duel-depth-union-t4-v1`. It is the first run whose cells are
balance-verdict eligible (six voting lineages against a four-lineage
floor, `bonferroni-all-contrasts-v1`); candidate promotion correctly
remains gated on the unmeasured layers. This entry is the measurement
record. **Selecting a hosted movement coupling is a product-owner call
that this entry deliberately does not make**, and the holdout reveal is
appended below once consumed.

Scored against the pre-registered hypothesis:

1. *Coupling shifts payoffs toward fortification and presence, away from
   maneuver; facing-locked further than move-sets-facing.* **Partial.**
   Under facing-locked both anti-striker matchups improve (bulwark
   −0.50 → −0.19; fabricator −0.94 → −0.67 mean payoff) and
   bulwark-vs-fabricator crosses zero toward the fabricator
   (+0.33 → −0.28). But move-sets-facing is not the midpoint the
   hypothesis assumed: the bulwark does *worse* there than at baseline
   (−0.50 → −0.69 vs striker) — half-coupling taxes the wall's rotations
   while leaving the dodger's one-action escapes intact.
2. *The wave-1 counter-cycle direction persists under every arm.*
   **Partial.** Striker-beats-fabricator holds everywhere;
   fabricator-out-bodies-bulwark only materializes under facing-locked;
   bulwark-blunts-striker is refuted in every arm by this population.
3. *Striker bend usage rises under coupling.* **Refuted**, and the
   refutation is the finding: bend share falls 42.4% → 34.1% → 33.3%
   (still-water 56% → 40%; vector-edge flat at 26%). Coupling suppresses
   dodging, which raises the value of the straight bolt, which reduces
   the need for bends. The mechanism the hypothesis named is real; it
   moves the observable the other way.
4. *No arm collapses into fortress stalemates.* **Confirmed.** Bulwark
   mirrors are 13/18 decisive at baseline and 18/18 under facing-locked
   — the historically stalest cell becomes the most decisive.

The cross-cutting numbers. Class payoff spread (best class minus worst,
mean over cross-class cells): preserve-facing 1.36, move-sets-facing
1.28, **facing-locked 0.67** — the deep coupling halves the class
imbalance while the striker stays on top in every arm (+0.72/+0.82/+0.43).
The cost is a rotation tax: rotations are 10.7% of active decisions at
baseline, 17.4% under move-sets-facing, and 37.2% under facing-locked —
over a third of everything bots do on that arm is turning, which is
either tank-game texture or dead air and is exactly what the
outcome-blind viewing pass must judge. Median cell duration stays at the
500-tick cap in most cells on every arm, so no coupling fixes pacing by
itself; the shortest cells (bulwark-vs-striker at 429.5 median under
move-sets-facing) shorten through kills, not captures.

Two harness facts this run established: the revision-2 population fully
restores seed variance (every cell 6/6 or 12/12 distinct replay hashes —
wave-1's three-seeds-one-observation collapse was a population property,
now disclosed per cell by the `seedVariance` block), and the
balance-eligible registration path worked end to end for the first time,
after normalizing the null-vs-"unqualified" coordination-grade mismatch
it had never exercised.

**Holdout replication (appended after the above was committed).** The
commitment was consumed and its two sealed seeds ran the identical
54-cell matrix as the registered derived spec
`frontline-classes-wave-2-movement-factorial-v1-holdout` (324/324
verified). Four of the five pre-registered replication targets hold on
unseen seeds: the class-spread ordering with facing-locked smallest
(1.35 / 1.15 / 0.75 vs main 1.36 / 1.29 / 0.67), bulwark-mirror
decisiveness maximal under facing-locked (12/12), the rotation-tax
ordering nearly digit for digit (10.9% / 17.2% / 37.3%), and the bend
decline (42.6% → 32.0% → 32.2%). The sign-stability target fails in one
column: **bulwark-vs-fabricator flips** (+0.33 bulwark-favored on main
seeds, −0.29 fabricator-favored on holdout at preserve-facing) — that
matchup is the closest to balanced and its per-run sign is seed noise;
no verdict should cite its direction. Every striker matchup's sign and
gradient replicates. Net: the striker-dominance compression, the
mirror-decisiveness gain, and the rotation tax of facing-locked are
robust findings; the bulwark-fabricator boundary is measured as
genuinely contested.

**Outcome-blind viewing pass (appended; full record in
`BLIND-REVIEW-MOVEMENT-FACTORIAL-2026-07-29.md`).** Twelve samples,
four per arm, arm- and outcome-blind. The arms do not separate on
watchability (mean fun 2.75 on all three; clarity 3.75–4.0), so
facing-locked's rotation tax carries no measured viewing penalty, and
both explicit negative reactions targeted preserve-facing strafing.
The watchability driver the pass did detect is the bend: the only two
fun-4 games are the only two featuring the high-bend striker, and the
owner's verdict — recorded blind, then confirmed — is that curved
shots deliver dynamism and that striker-exclusivity of the bend
envelope is itself in question. Dullness is quantified at mean fun
2.75 with no 5s while clarity never fell below 3: presentation is no
longer the bottleneck, the game is. The owner also ruled energy out as
a mechanism candidate, citing #47/#48's closed verdict.

## 158. Dullness diagnosed: a mean-reverting pendulum, a capped skill shot, and a skills-shaped remedy

Three parallel forensic passes over the full 810-replay movement-
factorial corpus (full reports:
`DESIGN-FORENSICS-DYNAMICS-2026-07-29.md`,
`DESIGN-FORENSICS-SKILLSHOTS-2026-07-29.md`,
`DESIGN-MECHANISM-SLATE-2026-07-29.md`) converge with the outcome-blind
viewing pass on one diagnosis. This entry records the measured facts
and the owner's design rulings; pilot selection is deferred to the
owner.

**The pacing fact.** The game is a mean-reverting pendulum:
`P(the leading side pushes further) = 0.350`, manufactured by the
objective walking toward the loser's spawn (reinforcement transit 4
ticks when trailing by 2 vs 20 when leading by 2) while death is free
(automatic full-health return). Kills don't convert (half leave a
contester in the doorway; contest nulls for free — 83% of contested
ticks are 1v1), 48% of capture progress decays away, and 22% of viewing
time is bodies within 3 tiles doing no damage. The corpus contains the
numbers-only disproof: thin-fronts fixed every stall symptom and
produced the *worst* cap share — cheaper captures raise the pendulum's
frequency, not its amplitude. Movement arms relabel the loop
(facing-locked: strafe-dance 16%→0% but stand-and-spin dwell up, worst
cap share 0.804); coupling is a texture knob, not a pacing knob.

**The skill-shot fact.** The bend mechanic is used near-optimally by
the bots (52% correct-program selection vs 11% blind) and contributes
28.5% of striker damage — but its equilibrium value is a covering
number, capped at 1/3 inside a two-tile annulus and **invariant to the
envelope** (9, 17, and 217 programs solve identically), it decays as
opponents dodge better, 80% of bends render too short to read as
curves, and facing-locked mechanically suppresses the mixup
(V(straight)→1 on the lane). Its real function is off-axis access for
a four-cardinal gun. No numeric tuning changes any of this.

**The owner's rulings** (recorded across this session's review): energy
stays closed per #47/#48; curved shots deliver blind-validated
watchable value and their striker-exclusivity is in question; and the
design direction is **"more skills like the turret — not necessarily
static"**: public, telegraphed, cooldown-cycled, visually
state-changing abilities. The engine already carries the machinery
(reversible windup-gated same-life transitions with public
`ObservedFormTransition` start/completion ticks); the turret is today
its only instance, invoked in 0.13% of decisions largely because its
objective-weight-0 bargain is priced in the scoring currency.

**Direction of travel, pending owner pilot selection:** (a) a
structural pendulum counterweight — territory ratchet + lead-independent
reinforcement + contest-costs-something, with the dynamics report's
pass/fail metrics as pre-registration targets; (b) a per-class public
skill kit on the transition machinery (the turret shape generalized:
windup in, visible form, cooldown out), with barricades and
telegraphed charged/area shots as the leading candidates because they
also attack the covering number and the eviction problem; (c) the
numbers-only lethality/respawn arm runs as the control factor in the
same factorial. Split remains parked; energy remains closed.

## 159. Strafe is dead; facing-locked is the presumptive coupling, preserve-facing demoted to experimental control

The owner has ruled free-direction movement out on identity grounds,
twice, the second time blind: "a bot shouldn't be able to move north if
it's facing east" (#155's reopening) and "strafing just doesn't feel
right" written on a preserve-facing sample the owner could not identify
(`BLIND-REVIEW-MOVEMENT-FACTORIAL-2026-07-29.md`). The wave-2 factorial
was therefore always deciding *which* coupling, not *whether*.
`move-sets-facing` is dropped as dominated (worst dullness metrics in
the dynamics forensics; not the assumed midpoint — it taxes the wall's
rotations while leaving one-action dodges intact, #157). That leaves
**facing-locked as the only ship candidate**: best class balance
(spread halved, holdout-replicated), no measured blind-viewing penalty,
and it abolishes the strafe-dance outright.

`preserve-facing` remains in the phase-2 skills factorial strictly as a
**measurement control** — it isolates the skill kit's effect from the
coupling's and anchors the bend-legibility comparison — not as a
candidate. The remaining ship gate is confirmation, not selection:
facing-locked must pass the phase-2 do-no-harm gates (cap share, wait
share, leader-extends) and the watchability gate under the new kit,
whose pieces were chosen compatible with it (a committed charge is
coherent under locked and reads as a super-strafe under preserve; the
dodge tax that halved class spread is unchanged). If facing-locked
fails those gates the question reopens explicitly rather than falling
back to strafe by default.

## 160. Pendulum counterweights exist as typed rules-side arms; measurement may begin

The phase-1 structural interventions from #158 are implemented as
pre-registered candidate arms on the existing capture/lifecycle policy
seams, with zero observation-schema change and the pinned goldens
unmodified (hosted `frontline-labs-1` byte-identical). Composable
`--pendulum` tokens: `sticky-frontline` (a completed advance holds for
`ratchetHoldTicks` = 40 — derived from the corpus's 33-tick reversal
latency — and an enemy capture inside the hold is spent, moving
nothing; breach is never denied), `forward-rally` (automatic returns
and activations place on the own-side chain-adjacent objective region,
derived from the chain, no new map regions), `contest-majority` (the
existing net-objective-weight control policy composed in: surplus
weight scales capture pressure, one body no longer nulls two), and
`enemy-sole-decay` (empty and contested ticks preserve claim; only
enemy sole erosion reduces it). Registered levels: `ratchet` =
sticky + rally; `ratchet-contest` = ratchet + majority; numbers-only
runs on `--capture-threshold` / `--prime-respawn-ticks`. The one new
canonical field (`ratchetHoldTicks`) follows the #156 additive
pattern, so frozen artifacts fault on sticky-carrying arms until
rebuilt — the same accepted consequence facing-locked already
established. The dynamics gates are machine-checkable via
`labs-replay-eval.py --dynamics`, which reproduced all 27 baseline
expectations on the wave-2 corpus before gating anything. A one-seed
smoke ran in the pre-registered direction (control max-ticks; ratchet
breach at 354; ratchet-contest breach at 206) and is recorded here as
direction, not evidence. CLI 0.9.8.

## 161. The prototype skills exist as arms; adoption awaits the watchability screen

The three kit candidates from the slate are implemented as composable
`--skills` arms (CLI 0.9.9), proven firing end-to-end in verified
replays, in-process and WASM behavior byte-identical, pinned goldens
unmodified. VOLLEY: a reversible striker stance (windup 2 in / 1 out,
immobile, objective weight 1) whose gun fires three damage-1 straight
bolts across adjacent headings at cooldown 5 — multi-projectile-per-
attack (`ActorAttackVolleyDefinition`) is the one new engine
capability; bolts are ordinary projectiles with contiguous launch-order
IDs. SHELL: a reversible bulwark stance (windup 1 each way, no attack,
objective weight 1, no health gain so cycling can never heal) whose
form consumes hostile contacts arriving in its facing quadrant,
evaluated on the projectile's approach vector; absorptions publish
`projectile-absorbed`. FIVE SLOTS: fabricator teams field prime + four
children on a continued 120-tick cadence (60/180/300/420) with late
slots on slow 30-tick rebuilds — count without tempo, honoring the
surge critique; mints five-slot and asymmetric 5-3 topology profiles,
the deliberate #153 amendment. Ablation-debt entries registered per
skill (each flag is one factor to the CLI, three to the measurement).

Two probe findings scope the watchability screen: the naive shell
probe produced 121 absorptions and **zero damage events in the entire
match** — the reactive-parry degeneracy named in the slate, observed
on day one (fix ladder if it survives smarter drivers: longer entry
windup, minimum tenure, then cooldown machinery); and the fifth
fabricator body cannot exist before tick 429, while ratchet-arm
matches can end by tick ~206 — the unlock schedule is a data knob if
the numbers-fantasy fails to materialize on screen. Latent three-slot
assumptions in qualification probe trimming and prose are flagged in
the implementation report; unreachable today, they bill to any future
five-slot qualification work. Kit adoption is NOT decided here — it
waits on the owner's outcome-blind prototype gallery.

## 162. The prototype skills get a presentation, and it states the rule

The watchability screen #161 defers to could not be run on the arms as
they rendered. Both stances resolved to the mobile chassis, and the
2.5D renderer — keying on `canMove === false` alone — reared them onto
their noses as spinning omnidirectional turrets, which is a lie about
two forms whose whole content is the direction they face. A volley
arrived as three unrelated bolts; an absorbed bolt vanished. So the
presentation layer now says all three out loud, and every cue is the
rule rather than a flourish:

- **A stance is a third body**, beside mobile and emplaced, resolved in
  `unitPresentation` from the stance token the engine appends to the
  source form ID (`-volley-stance`, `-aegis-shell`) and looked up in
  the replay's own form catalog, never composed from a naming rule. It
  keeps its facing marker; an emplacement still does not. The volley
  grows three barrels at the fan's own −45/0/+45, so the shape predicts
  the shot. The aegis fills the facing quadrant and stops hard at ±45°
  — the *edge* is the counter-play, so the boundary is drawn, and the
  unguarded three quarters are stated rather than left blank.
- **A volley is one wide-arrow glyph**, recovered renderer-side from
  what the contract guarantees and nothing weaker: same owner, same
  launch tick, contiguous ascending IDs (`identityOrder`). A gap in the
  identities means these were not one launch. The outline pushes each
  blade forward along *its own* heading, which is what bows a symmetric
  fan into a crescent — no code knows it is drawing one. A terminated
  blade cuts the run, and survivors either side fly on as separate
  arrows rather than being joined across the gap.
- **An absorption is not a hit.** Nothing expands, nothing is thrown,
  the camera does not move: the guarded arc rings at its own radius on
  the event's own `targetFacing`, and the bolt collapses inward on the
  contact tile. Every absorption restates which quadrant is covered.

Two things this uncovered. The class-form look table gains a stance
slot filled from the shipped catalog (`rift-runner`, `mossback`) — the
same explicitly-temporary stand-in status the mobile and emplaced picks
carry, no new assets, no catalog entries. And every presentation
surface compared event types against the replay-v1/v2 spelling
(`shot`/`destroyed`), so a generation-3 replay played back with no
muzzle flash, no kill flare, no recoil, no death collapse, no camera
knock and no sound cues; `isAttackEvent`/`isDestructionEvent` on the
model now own that equivalence in one place. The model keeps each
document's own vocabulary deliberately — re-labelling `attack` as
`shot` during normalization would invent an equivalence the schemas do
not state.

Two limits the screen has to know about. On
`frontline-labs-01-classes` the volley's flanking lanes almost always
die within a tick or two: the map pinches at columns 10–12 and its one
fully-open row is flush with the boundary, so the widest sustained fan
the map allows is one tick of three lanes. And the naive shell probe is
never flanked (#161's zero-damage finding), so the arc's *cost* is
unproven on screen even though its extent is legible.

The absorption cue is written against the rule as the engine emits it
today, and the slate's later deflection ruling would invert its
sentence: a shell that launches a team-flipped bolt back has not
nullified anything. The return bolt needs no presentation work — it is
an ordinary projectile owned by the guard — but the bolt's inward
collapse would have to become a redirect, or the arena would show one
bolt dying and an unrelated one appearing. The arc flash survives
either ruling; both renderers carry the note at the effect.

## 163. 3D is the viewer, and Canvas2D is a floor rather than a mode

The 2D/2.5D toggle is gone. It asked a player to choose between a fidelity and a
dimension count, which is not a decision anyone wanted to make, and the flat renderer
was only ever the safer default rather than the better one. Extending #127 and #128, the
WebGL renderer under `web/src/render3d/` is simply what the web viewer draws with, and
the name in prose is **3D** — "2.5D" described how it was built, not what it is.

Canvas2D is not deleted. It is demoted from a mode to a floor for the two cases where
3D cannot draw at all, both of which fall back without asking:

- the CLI's self-contained artifact, where `vite.cli.config.ts` stubs the dynamic import
  and sets `__BOTARENA_DIMENSIONAL_RENDERER__` false so Three.js never enters a copied
  replay — verified by every `dist-cli/<theme>` containing zero `WebGLRenderer`;
- a device that yields no WebGL context, which `ArenaCanvas3D`'s `onUnavailable` already
  catches.

So Canvas2D can only be removed once Three.js is allowed into the CLI artifact, which is
a size decision about `nilbots play`, not a rendering one.

Two things this deliberately does not change. `HostedViewer`, the mobile WebView path,
still renders Canvas2D unconditionally: switching it alters mobile rendering with no
device QA behind it, and the web redesign goes first. And the golden frames are not
exposed to any of this — `goldenFrames.test.ts` imports `drawArena` from the SSR harness
and calls it directly, never passing through the viewer's renderer choice, so no harness
pin was needed.

Replacing the WebView with a shared native renderer (expo-gl) is **deferred**: the app
keeps the shared web viewer for now.

## 164. The Arena UI reads one advisory server authority without relabeling legacy Duel

The authenticated `GET /api/arena` projection is the single UI authority for
the current official Duel format, effective rolling allowances, ranked
concurrency, and batch bot ownership/playability. Public roster profile data
may retain a signed-out presentation hint, but it never authorizes a submitted
match. The generated contract drives the shared Arena composer and signed-in
Garage/Bots actions. Successful or refused creation refreshes it.

The projection is advisory. Ranked and unranked POST operations retain their
transactional account locks and re-evaluate quota and participant admission
before creating work. Deliberate Arena refusals use stable application codes
inside one named problem response, including burst-limit rejections produced
before an endpoint runs. Roster and matchmaking admission use bounded batch
queries rather than one admission query chain per global bot. A one-off request
with no map selects the existing fixed `arena-01` default; the frontend no
longer calls that behavior random. Self-challenges are invalid.

`DuelArenaDefinition.Official` and `DuelMirrored6V1` name the already-shipped
default map, ranked pool and six-game mirrored schedule so creation, scoring
and presentation project the same policy. They do not mutate
`LegacyCompetitionDefinition`, register Duel as a hosted generic match, or
change historical playlist/ladder identity.

Automatic Arena remains a separate package. Durable schedules, local-date
occurrences, time-zone/DST policy, worker leases, bounded retries, entitlement
revocation and idempotent creation are not implied by the manual capability
projection. Match-creation idempotency also remains a required follow-up.

## 165. The skill kit is adopted for phase-2 measurement, as rules rather than etiquette

The mechanism screen passed (owner verdict on the prototype gallery:
"definitely more entertaining", correctly scoped as a mechanism screen —
driver bots, not candidates) and every kit question is owner-ruled, so
the kit graduates to adoption-grade mechanics. One primitive serves
both stances: a threshold-triggered automatic return on the same-life
return route (`automaticReturn: {counter, threshold}`, inert-omitted),
with the cause published as a typed `automatic-threshold-return`
form-transition reason following the automatic-activation precedent,
counters life-scoped and blocked-queue-proof, lethal damage cancelling
through the ordinary destruction path, and the chronology validator
re-deriving counts to refuse forged, early, suppressed, or mislabelled
returns. VOLLEY casts (auto-return after one fan — squatting is
impossible by rule); AEGIS SHELL breaks (forced return on the third
deflection); FIVE SLOTS stays 60/180/300/420. The stance arms are
reidentified `cast` and `break` because behaviour changed; the curve
grammar ships as a separable `--bend striker-only|universal` factor
with per-class depth (striker bend-after 1–4, bulwark/fabricator 1–2;
specials never curve). Baseline and every prior arm keep byte-identical
fingerprints; probes verify the cast timeline, the shatter, and a
bulwark bolt genuinely curving on the universal arm.

Adoption is a gate to MEASUREMENT, not a ship decision: the phase-2
factorial still owns the verdict via the pre-registered counter-cycle
sign predictions, the 0.15–0.40 edge band, the do-no-harm pendulum
gates, and a watchability pass on a real T4+ population. Benched
fallbacks (barrage, charge, dash, shell-as-absorption) stay registered.
Two registered ablation debts scope the evidence: one primitive at two
thresholds means a result on one stance is weak evidence about the
other, and gaining the bend grammar also changes which action a gun
uses. Known constraint for the phase-2 pre-registration: the 64-char
canonical ID cap overflows on the widest composed cells
(bulwark-vs-fabricator + break + slot5 + bend + facing-locked), so the
factorial needs a shorter token scheme or a registered combination
identity before every cell can run.

## 166. Phase 1 verdict: the pendulum survived the registered dose; sticky ground without contest cost backfires

`frontline-pendulum-wave-3-v1` ran 216/216 verified matches (24 cells,
four levels × six class pairs, facing-locked, classes-wave-1-r3
population) plus a 144-match sealed-seed holdout, and the pre-registered
gates FAILED — replicated on both seed sets, published as measured.

The instructive failure: **plain ratchet (sticky + rally, no contest
cost) made the game worse** — cap share rose 0.685 → 0.796 (holdout
0.750 → 0.778) and draws exploded 9.3% → 29.6% (8.3% → 25.0%). The
mechanism is legible in its own metrics: the ratchet did its literal
job (reversal rate 0.69 → 0.51), but with one body still nulling any
number at the objective, blocking regression froze the front rather
than freeing it — nobody can lose ground, so nobody gains any.
**Contest-majority is not an enhancement to the ratchet; it is a
precondition.** Ratchet-contest moved every H1/H3 metric the right
direction on both seed sets (leader-extends 0.466/0.474 vs control
0.359/0.346; cap share 0.444/0.472; draws 7.4%/2.8%; the frozen-
scoreboard gate actually passed at 0.111 twice) and missed every other
threshold. H2's transit spread halved (14 → 6-7 ticks), not flattened —
forward rally helps but the placement region still trails the front.
**H4 passed exactly as registered**: numbers-only left the reversal
median at ~0.65-0.67 and leader-extends at 0.29-0.32 on both seed sets
— cheap numbers do not touch the pendulum, so the structural diagnosis
survives its own falsification test. The dose was insufficient, not the
theory.

H5 also failed with a population finding attached: the r3 revisions,
proven decision-identical to their predecessors only on pendulum-free
contracts, shifted cross-class balance — control-cell spread is 1.56-
1.62 (striker sweeping both classes outright) versus wave-2's 0.67 with
the r2 population. Cross-class balance claims from this run are
population-confounded and none are made; the phase-2 population plan
(fresh skill-native lineages) was already the answer.

Disposition: phase 2 does not start on this baseline, per the
registered gate. The next registered dose is **phase-1b**: compose
`enemy-sole-decay` into the winning level (`ratchet-contest` +
enemy-sole-decay — one factor delta, per attribution discipline),
targeting the wasted-sole share that stayed at 45-60% across all
levels while being the one built counterweight the registered levels
never included. Same gates, unchanged thresholds — a failed gate is
answered with a stronger dose, never a softer bar.

## 167. Classes get real skins: internal defaults plus six approved purchase packs

The class-skins branch (Codex; integration notes in
`HANDOVER-CODEX-NOTES.md`) lands the visual identities the class system
has been renting from catalog stand-ins. The **class defaults are
internal form presentation, not account cosmetics** — Trident Wasp +
Trident Spark (striker), Aegis Tortoise + Rebound Diamond (bulwark),
Lattice Loom + Lattice Rivet (fabricator) — and rendering them never
depends on ownership. The owner approved the remaining six concept
pairs as live purchase packs, which **supersedes the historical
invariant that Aureate Warden is the only chassis manifest carrying a
recommended projectile**; that prose is reconciled here rather than on
the branch, which deliberately minted no number. Known gap, explicitly
deferred: alternate manifests expose a presentation-only `classId`,
but account appearance persistence has no class-compatibility
enforcement — purchased looks remain globally equipable until the
class-first-class branch supplies the end-to-end policy. No schema was
added. Art sources and the generation pipeline live under
`art/class-look-concepts/` with `scripts/build-class-look-concepts.py`.

## 168. Phase-1b verdict: keel is the best dose and capture economics is exhausted

`frontline-pendulum-wave-3b-v1` (162/162 mains, 108/108 sealed-seed
holdout, ratchet-contest as replication anchor, keel differing by
exactly enemy-sole-decay) delivers the counterweights' best result and
a diagnosis worth more than a pass. Keel clears the displacement-
efficiency gate on both seed sets (0.457 / 0.429 vs the 0.40 bar — the
first H1 sub-gate ever passed), drives draws to 3.7% / 0.0%, cuts the
reversal rate to 0.40, and drops cap share to 0.46-0.50. The anchor
reproduced its phase-1 values. But **P(leader extends) has plateaued
at ~0.47-0.48 across three escalating doses** (0.36 control → 0.47
ratchet-contest → 0.47 keel, twice replicated), and the wasted-sole
share did not move (0.53-0.56) — enemy-sole-decay relocated erosion
into the opponent's sole windows rather than eliminating it, a
measured null on the mechanism it was added for. With all four
capture-economy counterweights active and the metric unresponsive,
**the remaining mean reversion is not capture economics.** The
never-tried levers from the #158 diagnosis are S4 (overtime/
escalation in place of the flat 500-tick cap — not yet built) and S5
(map geometry: the two-corridor funnel). H2's transit spread holds at
6-8 (halved, not flat); H1 overall therefore still fails and phase 2
does not start on a passed gate.

The balance signal inside the miss: under keel, bulwark-vs-fabricator
sits at exactly 0.00 on both seed sets and bulwark-vs-striker at
−0.17/−0.50 — the counterweights alone nearly balanced two legs of
the class triangle. The outlier is striker-vs-fabricator at −1.00 on
every seed set, which is precisely the cell the adopted phase-2 kit
targets (five slots; the volley softening prediction). Owner fork,
explicitly not decided here: build S4 and run phase-1c before phase 2,
or adopt keel as the phase-2 pendulum baseline — displacement passed,
draws zero, caps down 22 points from control — and carry overtime as a
parallel arm inside phase 2. Gates stay unmoved either way.

## 169. Keel is the phase-2 baseline; the schema window executes with the observability mini-bump

Owner ruling on #168's fork: proceed to phase 2 on the keel baseline —
displacement gate passed twice, draws at zero, cap share down 22
points — rather than chasing the 0.35 cap bar first. S4
(overtime/escalation) is parked as registered follow-up debt, not
abandoned: the leader-extends plateau says the remaining reversion
lives in the flat cap or the map, and either returns to the bench if
phase 2's pacing gates demand it. Phase 2's design consequence: the
movement factor is dropped (all cells keel + facing-locked; #159
demoted preserve-facing to a coupling-measurement control, and phase 2
measures the kit, not the coupling — a deliberate deviation from the
original 48-cell design, disclosed here). The factorial is skill-kit
(off/on) × bend-envelope (striker-only/universal) × six class pairs =
24 cells, with kit-off/striker-only anchoring as a keel replication.

The batched SDK-bump window between phases executes now, scoped to the
measured consensus: the ratchet-hold observability fields (five
authors, two waves — the hold's owner and remaining ticks become
readable instead of inferred) and the ObservedProjectile
timing/damage fields (the wave-2 "should I eat this?" forensics).
Typed classId and cosmetic class-compatibility enforcement stay with
the Codex class-first-class branch (not started at this window; class
remains readable via form prefixes, so nothing in phase 2 blocks on
it). Composite arm identities for the phase-2 cells get the keel
treatment — registered short tokens, since even keel+bend overflows
the worst class cell by one character.

## 170. Class becomes a first-class citizen, additively — and the schema window closes

Three integrations complete #153's Phase B inside the #169 window, all
on contract profile 2 with the pinned hosted fingerprints byte-
identical throughout. (1) **Persistence** (`codex/bot-class-config`):
a nullable persisted `Bot.ClassId` with EF migration, engine-owned
catalog validation, class identity on the bot API contracts with
regenerated mirrors, owner-only immutable atomic first assignment for
legacy bots, Garage UI, and CLI propagation with declaration-mismatch
checks — the persisted identity is authoritative; an omitted manifest
class stays deliberately class-agnostic. (2) **Observability** (the
phase-2 engine prep): readable ratchet-hold facts (`holdOwnerTeamId`,
`holdEndsAtTick`, `controlResumesAtTick` grammar) and
`ObservedProjectile` timing/damage, probe-proven, plus the registered
phase-2 composite identities `helm`/`veer`/`rig` beside `keel`.
(3) **Contract classId + spawn reservations**
(`codex/class-first-class`, resolved): typed `classId` on scoring
teams, participants, and every observed body — copied from the
controlling participant, never parsed from form IDs — and
`spawnReservation` on observed tiles.

The load-bearing resolution: Codex minted a contract profile 3 for
this, and it is **rejected with evidence** — its own branch had to
rewrite the pinned match-fingerprint golden and assert the entire
phase-1 WASM population faulting at tick 0 across all qualification
probes, because the capability tuple rides inside the fingerprinted
bytes. Everything it carried lands additively under the #156 pattern
instead; `observationSchemaVersion` deliberately does not move (the
rule, documented in RUNTIME-PROTOCOL.md: additive unknown-ignored
fields retain the version, since bumping relabels every immutable
ruleset); duplicate hold/projectile encodings are removed with a
reflection test pinning one encoding per fact. `generic-actor-match-2`
remains the one generic lineage. SDK 0.10.6, CLI 0.9.15; frozen
artifacts fault on new contracts until rebuilt — the accepted
consequence the wave-4 rebuild absorbs.

Measurement note for phase 2: class-declaring arms now carry classId
in their canonical bytes, so every phase-2 cell is a fresh
content-identified ruleset relative to the phase-1 cohort's arms. That
is correct, not a problem — phase 2 pre-registers fresh contracts, and
its keel anchor cells double as a robustness replication of #168 under
the new identities. Remaining class work outside this window:
cosmetic class-gating of the six purchase packs (rides the persisted
identity, no schema), and the Meshy 3D model pipeline (presentation
only).

## 171. Phase 2 measured: the kit works as a mechanism and overshoots as a balance

`balance/frontline-skills-wave-4-v1.json` pre-registered before any run
(24 cells: six class pairs × keel/helm/veer/rig; the eight-entrant
wave-4 T4 population; fresh seeds 480013/510007/540041; sealed 2-seed
holdout, commitment `8384c66e…`). Mains 420/420 verified, holdout
280/280, no collapsed seeds, verdict-eligible on both.

**The factorial's first catch was an engine fault, not a balance
fact.** Mains run 1 faulted 9/420 matches — every one arc-light's
volley fan meeting a gate-stone or iron-root shell: a fan bolt
deflected during its own launch traversal minted the return's identity
mid-fan, gapping the volley's projectile identities, and the
chronology validator correctly rejected the engine's own history
against the contract's contiguous-ascending-in-launch-order promise.
Wave-4 authoring could not have seen it — authors spar only their own
class, and volley-into-shell is inherently cross-class. Fixed by
reserving the fan's whole identity block before any bolt flies
(CliVersion 0.9.16); regression test runs a point-blank fan into a
raised shell, red on the old engine with the production error. No
previously valid replay changes (a passing volley match replays
byte-identical; every affected match had faulted, not replayed). Run 1
is preserved beside the rerun with its payoffs unconsulted; the
holdout stayed sealed through the fix.

**Balance verdict: the predicted counter-cycle did not form — FIVE
SLOTS makes the fabricator dominant, not cyclic.** Kit-on pooled
edges, mains and holdout agreeing: fabricator over bulwark −0.833 /
−0.833, fabricator over striker +0.778 / +0.667, bulwark over striker
+0.333 / +0.278. Only the bulwark-vs-striker leg lands in the
registered [0.15, 0.40] band; G4 fails on the two fabricator legs
both runs. Attribution needs no extra ablation — the pair structure
carries it: bulwark-vs-fabricator has no striker (volley irrelevant)
and fabricator-vs-striker has no bulwark (shell irrelevant), and the
kit's other skill sits on the losing side of both swings. Universal
bend alone (veer) already tilts the same way (fabricator −0.278/+0.333
vs keel's +0.056/0.000): more guns curve more. G1a fails as-written
(delta −0.019/−0.056 vs the +0.30 prediction) for a reason worth
recording: the prediction assumed phase-1's striker-favoured anchor,
but keel plus the wave-4 doctrines had already put bulwark ahead
(+0.35 kit-off) — there was no gap left for the shell to close. Its
companion G1b (ends ≥ −0.10) passes both runs.

**Mechanism verdict: adopted, alive, and it improves the game's
pacing.** All three do-no-harm gates pass on both seed sets with the
sign of improvement, not mere non-harm: cap share 0.43→0.24 (mains)
and 0.40→0.29 (holdout), draws to ~0, leader-extends UP 0.51→0.58 /
0.52→0.57 — the first intervention measured here that raises
leader-extends above the #168 plateau. The skill loop operates in
real cross-class play: 348/316 volley casts, 1706/1038 deflections,
and 206/86 shell breaks (mains/holdout kit-on cells). Wave-4's "break
budget never fires" (0 in ~350 self-play raises) was a sparring
artifact — the volley is the natural shell-breaker, and the designed
two-axis counter-play (flank the locked arc or feed the break) is
exercised constantly. Adoption: arc-light casts in 81.5% of kit-on
matches; all three bulwarks raise shells (59–78%); vector-edge and
still-water's 0%/6% are the priced declines already on the wave-4
record (the measured-decline clause applies; striker pooled 29% ≥
25%). Five slots is passive topology, so the usage gate is
inapplicable to it by construction.

Two disclosures resolved: striker-mirror cells keel≡veer and helm≡rig
share identical rules/map/topology/format fingerprints, but the match
provenance differs and seed derivation mixes it, so those cells are
independent samples of one ruleset — honest replications, not
double-counted bytes. And the placement-tag rationing and break
budget were measured AS-IS per the registration; the tuning pass owns
those knobs.

Consequence: the kit stays adopted (mechanism gates passed with
improvement), the balance fails its band as-is, and the first tuning
knob is the five-slot schedule (count and 60/180/300/420 unlocks,
#165) with the volley/shell legs kept — bulwark-vs-striker is the
healthiest cross-class edge yet measured. The owner watchability
gallery (the phase-2 human gate) proceeds on this evidence; the
leave-one-skill-out arms stay registered for the tuning pass.

## 172. The tuning pass: wane adopted; the stance-ground lever measured and returned

Three pre-registered rounds executed the #171 consequence ("the first
tuning knob is the five-slot schedule"), every round with fresh seeds,
a sealed holdout consumed after mains, and a written selection rule so
no knob could be shopped after the numbers.

**Round 1** (`frontline-five-slot-tuning-v1`, arms trim/boom/drag vs
full): a clean lever split, replicated on the holdout. Dropping the
fifth slot moved only the bulwark edge (−0.800 → −0.600); the baseline
30-tick rebuild clock moved only the striker edge (+0.667 → +0.133 —
volley farming finally sticks, the counter-play the fan was built for)
but stalled the fabricator mirror (9/10 capped); the late schedule
moved nothing. Registered outcome: adopt nothing, compose.

**Round 2** (`frontline-five-slot-tuning-2-v1`, composites moor =
trim+drag and wane = trim + a half-step 22-tick rebuild, plus the
kit-minus-five-slots diagnostic column): moor inherited the mirror
stall (7/10 capped, do-no-harm fail); wane passed every health gate
and put fabricator-vs-striker in the band, leaving
bulwark-vs-fabricator at −0.467 against the 0.40 gate on that round's
seeds. The diagnostic column exonerated the shell (shell-alone
−0.067, at the keel anchor) and measured volley-alone at +0.333 —
in band by itself.

**Round 3** (`frontline-stance-ground-v1`, owner-directed): the
`--stance-ground free` arm drops the anchor-forbidden tag kind from
the VOLLEY and AEGIS SHELL entries only (turret anchors keep it; an
objectives-without-corridor level is a map-format question, deferred),
CliVersion 0.9.19, strict pinned byte-identical. Measured pooled with
its holdout: **free backfires on bulwark-vs-fabricator** (−0.300 →
−0.700) — freed placement means more shell raises in worse spots, and
an immobile deflector on an objective is what a swarm envelops; the
shell's value is opponent-shaped. Meanwhile free pulls
bulwark-vs-striker toward the band (+0.644 → +0.533, the striker
exploits freed volley ground), leaves fabricator-vs-striker unmoved
(+0.300, in band; strict-authored doctrine uses the corridor only
opportunistically), and mildly improves bulwark-mirror pacing.
Registered fallback fires: **wane alone is adopted**; the
stance-ground question returns to the owner with these numbers.

**The adopted tuned default for future phases is `rig` + `wane`**
(keel + kit + universal bend + four slots at 60/180/300 + 22-tick
ordinary rebuild). Pooling every post-registration wane measurement
(rounds 2–3 mains + holdouts, n=60 per edge, four fresh seed sets):
bulwark-vs-fabricator **−0.383**, fabricator-vs-striker **+0.383** —
both inside the 0.40 gate and the band, with per-round seed drift
(−0.467/−0.300 and +0.467/+0.300) disclosed rather than averaged
away. The registered arms and their identities stay immutable; `full`
remains the phase-2 measured arm.

Open items recorded, not decided: bulwark-vs-striker rides above the
band on rig cells (+0.644 strict, phase-2 rig-only ~+0.60) — the one
place the freed stance ground measurably helped — so a striker-side
stance-ground revisit or a shell-decline-vs-bulwark doctrine note is
live; the shell-break budget question shifts from "does it fire" to
"is ~3 right when one fan pays most of it"; and the drafted
turret-cycling/cooldown capability work stays parked where #171 left
it.

## 173. Owner rulings: skills are in the game; the 45° aim comes back; reports get a format

Three owner rulings from the post-#172 review (the owner watched the
phase-2 gallery):

1. **The skill kit is in the game — for entertainment and depth, and
   that is non-negotiable.** Blind fun-rating A/Bs stop gating skill
   adoption; this is an explicit product-gate override per the
   balance-harness skill's own rule, recorded here rather than
   laundered into a pass. Balance and match-health gates remain hard.
   The measurement program's job is now to make the skills *land well*
   (tuning, doctrine, presentation), not to decide whether they exist.
2. **The ±45° initial aim is restored.** The owner spotted in watched
   games what three factorial rounds did not surface: `oneBendOnly`
   conflated "one bend per shot" with "no initial aim offset", so
   since the class arms began no mobile gun could fire diagonally at
   all (and a diagonally-adjacent enemy was unhittable — wave-4
   measured the symptom without the cause). That was never a design
   ruling. Restoration ships as a registered arm and is measured
   before adoption, with an adopt-unless-it-breaks selection rule
   (the entertainment ruling flips the default: the arm is adopted
   unless it pushes a fabricator edge out of band, worsens
   bulwark-vs-striker, or regresses pacing). Prediction registered in
   the spec: diagonal aim is flank grammar and should pull the
   over-band bulwark-vs-striker edge (+0.64) down.
3. **Owner reports follow a fixed format** — DECISION NEEDED / RESULT
   / EVIDENCE / NEXT, decisions first, codenames spelled out every
   time (balance-harness skill §6). Prompted by a report that buried
   the ask.

Also commissioned outside the lab: a viewer pass on the follow-camera
auto-fit (action drifting off-center) and a mobile wake-lock so the
screen stays on during replays.

## 174. Fast-iteration mode; crew is the working game; aim numbers provisional

Owner process ruling: until the game is balanced and fun, small changes
are NOT verified standalone — batch, check at mains level, record as
provisional, keep moving (balance-harness skill §6 carries the rule;
the full discipline returns on a stable base). Consequences applied
immediately: the aim-restoration factorial's sealed holdout is
deliberately left unconsumed (its commitment stays valid if ever
wanted), and no per-knob owner review happens.

**`crew` is the working game** — keel + skill kit + universal bend +
four-slot/22-tick-rebuild fabricator + the restored 45° aim (`sail` on
pairs without a fabricator) — per the #173 entertainment ruling. The
aim mains (210/210 verified, fresh seeds, wave-4 population) are
recorded as PROVISIONAL: diagonals swing bulwark-vs-fabricator from
−0.667 to 0.000, but the striker loses ground everywhere
(bulwark-vs-striker +0.93, fabricator-vs-striker +0.78 toward the
fabricator) and two mirrors cap more. Read with the registered
disclosure: wave-4 striker doctrine predates diagonals entirely, while
scaffold-driven chassis exploit them mechanically — the population is
the distortion. Wave 5 authors doctrine the full crew game; balance is
re-read coarsely on their play, and tuning resumes from there.

## 175. The follow-camera centres the action, not the map — and playback holds the screen awake

The viewer pass commissioned in #173, from a phone watching a served
gallery.

**The fit centres the action, and the frame may hang over the edge of
the arena to do it.** `focusFrame` used to clamp the fitted frame so the
whole of it stayed inside the map (`clampCentre`, band
`[-0.2 + span/2, extent + 0.2 - span/2]`), which sounds obviously right
and was the entire off-centre bug: the frame is first *grown* to the
viewport's shape, so on a 2.17:1 phone a small skirmish becomes a
15.6-tile-wide frame, and a 24-wide arena then leaves a band four tiles
wide to be "centred" in. A duel by the right-hand spawn came out **33%
of the viewport width right of centre** with empty floor beside it, and
a portrait fight in the top third landed 20–34% high. Nothing was wrong
with either projection — `arenaViewport` and the 3D look target both put
`frame.x, frame.y` exactly at the middle of the screen; the frame handed
to them was already off the action. The clamp was also inconsistent with
the zoom-out limit it lives beside: `fullArenaFrame` letterboxes
background on the short axis without apology, so "a fit may never show
background" was an invariant the wide shot itself broke.

The new rule is two lines. The centre is the middle of the fitted box,
clamped only so the *point being looked at* stays over the arena — which
never binds on a fit, only on a gesture, so pan and zoom can now reach
everywhere the fit can (a hand that cannot is a camera that jumps when
auto-fit is handed back). The one override: an axis the frame already
covers whole is centred on the arena instead, because sliding that would
push the map off one side and show background on the other for nothing.
The accepted cost is background beside a fight that is hugging a wall,
bounded by the span rules already in place — never wider than the whole
arena, and at least half of each axis is arena, because the centre is
over the board.

**The deadband gained a drift band.** Containment alone let a wipe on
one flank reshape the fitted box without escaping the committed frame,
so the survivors sat a tenth of the screen off-centre for the rest of
the replay. Re-aiming when the fitted centre drifts past 10% of the
committed span is a pan the spring absorbs; it is not the zoom hunt the
deadband exists to stop.

Both are pinned by projected-pixel tests at real phone aspect ratios
(`web/tests/arenaCamera.test.ts`), including the fit-to-selected-team
path — the worst case for the old clamp, since a team is dug in at its
own end of the map and its box is therefore always near an edge.

**Playback holds a screen wake lock** (`useScreenWakeLock`, in
`components/` so both viewer outputs and the hosted WebView share one
implementation), wired to "the clock is running" rather than to the play
button, so a live broadcast counts. The re-acquire on `visibilitychange`
is the load-bearing part: the platform releases the lock whenever the
page is hidden and does not give it back, so without it a viewer who
checks a message watches the rest of the match on a screen free to
sleep. Everything about it is silent by design — the API is absent on
iOS Safari and on a `file:` CLI viewer, and the request rejects on a low
battery. A console error on a device that simply cannot do this teaches
people to ignore the console.

## 176. The open game: tiles unlocked for every skill, and the turret becomes a cycle

Owner rulings during the wave-5 relaunch window, applied as one batch
(fast-iteration mode, #174):

1. **All transform placements open as the starting point — turret
   anchors included.** The wave-5 game is `deck` = crew + `--stance-ground
   open`: shells, volleys, AND turrets may rise on objective tiles and
   the corridor. The turret on a point keeps its weight-zero bargain.
   The owner's standing design direction, recorded for when
   restrictions return: **granular tile classes with per-skill rules,
   never one umbrella tag** — which also turns the map itself into a
   tuning surface (retag a zone, reshape a corridor) with clean
   per-variant fingerprints.
2. **The turret is a true cycle.** Anchor⇄mobilize unlimited per life
   (the once-per-life mobilize was the old rule), the +2 entry heal is
   removed (a healing entry on a repeatable route is a repair loop —
   owner: "turret healing is probably a bad idea to begin with"), and
   health maps by the pre-existing `preserve-ratio-floor-minimum-one`
   policy in BOTH directions, per the owner's relative-floor idea: full
   health cycles losslessly (4/4 ⇄ 7/7, so the turret's high maximum
   finally matters), partial health pays the floor each round trip (a
   natural anti-flicker tax), and preserve-capped on the way down was
   rejected because it silently heals (5/7 → 4/4). Windups remain the
   commitment price; the drafted per-slot cooldown stays parked unless
   cycling proves abusive in play.
3. A ground arm is inert-omitted where nothing it touches exists (the
   skills rule), so one flag set serves every pair of a wave.

Wave 5 was killed pre-freeze and relaunched on `deck` (agents were
still orienting; no work lost). All under CliVersion 0.9.21 with every
existing ruleset pinned byte-identical.

## 177. Wave-5 infrastructure rulings: viewers opt-in, writes verified, source cap 2 MB

Three owner-directed fixes from wave-5 friction, batched under CliVersion
0.9.22 (fast-iteration mode):

1. **The experiment command's self-contained viewer is opt-in**
   (`--viewer`, or implied by `--open`). It embeds the entire replay
   into a multi-megabyte theme template — most of a sweep's footprint,
   duplicated per match for a file nobody opens; two authors filled the
   disk under it ("self contained viewer per replay is stupid" — owner).
   `play`/`replay` keep writing viewers; the balance-lab drive benefits
   automatically.
2. **Replay writes verify and fail loudly.** The full-disk incident
   produced replays that parsed, carried plausible standings, and were
   wrong by five wins with exit code 0. Writes now go through a temp
   file with byte-length verification and an atomic move.
3. **The submission source cap rises 256 KB → 2 MB** (files stay ≤16).
   Wave-5 measured the old cap as the binding constraint on exactly the
   two behaviours the author packet demands — read the contract, write
   the reasoning down — with ~20% of any budget being scaffold
   boilerplate. The scaffold-as-SDK-type idea stays on the bench.

## 178. Wave 5 frozen; the coarse deck read says ladder, not cycle

Wave 5 — the first cohort authored FOR the deck game — froze 8/8 T4 on
first attempts with zero friction kills (cohort README carries the
converged findings; #177 carries the infrastructure fixes it forced).
The coarse balance read (fast-iteration mode: mains only, wave-5 vs
wave-5, one bot-assignment per pairing, 3 seeds with heavy
deterministic collapse disclosed — 8–12 distinct outcomes per pooled
18–27) is directional, and its direction is one ladder:
**bulwark +0.815 over striker, bulwark +0.611 over fabricator,
fabricator +0.667 over striker.** Every edge above the 0.40 band; the
old fabricator dominance is gone (both wave-5 bulwarks' doctrine
out-evolved the swarm); the striker is now the floor of every matchup
despite the wave's most inventive revisions — vector-edge's sight-band
inversion beat the wave-4 bulwark 20-0 and still loses to the wave-5
ones. Match texture is what the campaign wanted: 30+ breaches in 63
matches, one draw, shells holding points, turret leases, diagonal
duels. The owner gallery ships from these replays; the next balance
move is striker-side (candidates already on record: the freed-ground
bvs pull measured in round 3, the shell/turret pricing questions in
#176/#177, lethality numbers) and takes the owner's watch impressions
as input.

## 179. Wave 6: coordination fixed a balance edge; the striker problem is now the class

The coordination cohort froze 8/8 T4 first-attempt (cohort README
carries the converged findings — coordination does not decompose;
corridors are fixed by routing, not yielding; spacing only works as a
tiebreak; the convention is common knowledge from the frozen union and
per-life randomness is what breaks it). Every author beat or matched
its wave-5 self, several overwhelmingly (ledger-fly 48-0; spark-line
24-0; march-wall 29-1-2 with a zero-breach control).

The coarse re-read (same 63-cell sweep as #178, coordinating bots):
**bulwark-vs-fabricator +0.333 — inside the band, fixed by IQ alone**
(was +0.611); bulwark-vs-striker +0.815 unchanged;
fabricator-vs-striker hardened to +1.000. March-wall's caveat
confirmed: wave-5's meta was partly a coordination artifact, and what
survives smart play is one crisp finding — **the striker loses to both
classes and it is the class, not the bots** (three inventive striker
lineages across two waves cannot close it). The striker buff is the
next balance conversation, with candidates already measured on the
record: freed striker-side stance ground (pulled bulwark-vs-striker
toward band in the round-3 factorial), the sight-band/vision numbers,
lethality, and the gunless-stance cooldown-freeze un-nerf.

The owner gallery was refreshed from these matches under the standing
tunnel. The post-wave engineering batch is queued by demand: qualify
viewer opt-out (six authors), refusals naming their cause + observed
ally next-step + resolved placement candidate lists (schema window),
the coordination-grade suite (five authors; T4 is reachable by a bot
that cannot share a corridor with itself), team-scoped randomness, the
evidence/ build-glob exclusion, and the frozen-artifact drift
investigation (a wave-5 freeze re-measured differently across the
0.9.21→0.9.22 republish — determinism is the product; find the moved
input).

## 180. The ticking cooldown clock; tide is the working game

Owner rulings executed as one window. The attack cooldown becomes a
property of TIME instead of the armed form (`--cooldown ticking`,
contract fact `tickResolution.cooldownClock`, inert-omitted): a gunless
stance or windup no longer freezes gun recovery, ending the hidden
stance tax gate-stone discovered in wave 5. Everything else about
cooldowns is unchanged (one public counter per body, declared price per
gun, shared across forms, reset on a new life). **`tide`** — the open
game on the ticking clock — is the working game. Two catches recorded:
the first tide sweep caught a canonical-order bug (the new field was
emitted mid-object; canonical readers demand exact order — moved to
trailing position, reader taught), and the first sweep also
demonstrated the #170 consequence live (frozen pre-0.10.7 artifacts
fault on tide contracts; the cohort was rebuilt from source).

Coarse tide read (rebuilt wave-6 cohort; doctrine authored under the
FROZEN clock, so this is a stale-doctrine lower bound):
bulwark-vs-fabricator **+0.333 — still in band, undisturbed**;
bulwark-vs-striker hardened +0.815 → **+1.000**;
fabricator-vs-striker +1.000. The clock helps every stance user and
helps the shell most (bulwarks now raise and come out with a live
gun), so as a striker buff the hypothesis is REFUTED — it was adopted
as a design-correctness ruling, not a balance lever, and stands. The
striker conversation now has exactly two levers left on the record:
class numbers (HP/vision/lethality) and granular asymmetric stance
ground (the per-skill tile classes). A wave-7 doctrine pass would
re-price everything above; per fast-iteration these numbers ship as
provisional.

## 181. Route cooldowns: the general skill-pricing capability

Owner ruling ("we should add it — will be good to have going
forward"): any same-life route may declare `cooldownTicks`
(`sameLifeTransitions[].cooldownTicks`, inert-omitted). After the
route COMPLETES, requesting it again from the same UNIT SLOT is
refused while `tick < completionTick + cooldownTicks + 1`. The clock
survives the body — die-to-reset, the exploit the original draft
flagged, is closed by construction — and automatic returns are
exempt, so a forced exit is never trapped by its own clock. Session
gates the availability mask and the queue and stamps at completion;
the chronology validator rejects impossible cooldown histories with a
mirrored match-level pass; a live probe pins spacing = cooldown + 1
and no deadlock. SDK 0.10.7 / CLI 0.9.24 carry both of this window's
trailing additive contract facts. No skill declares a cooldown yet:
this is the prerequisite for the higher-power skill tier, and the
observation publication of remaining ticks is bound to the first
skill that uses it (the readability law binds at first use). The
`nilbots` symlink beside `botarena` also finally exists.

## 182. The salvo: the volley re-armed as the first route-cooldown consumer

Owner observation ("the volley skill is a bit weak — don't see it used
a lot") diagnosed, and the "decouple + sharpen" option chosen. The
diagnosis: the aim restoration (#173) cannibalized the fan — its spread
is exactly the mobile gun's three aim options, offered one at a time at
twice the cadence without giving up the step, so the wave-6 cohort's
near-universal volley DECLINE was priced correctly, not timidly.

`--volley salvo` (inert-omitted without a striker; `tide` + salvo is
the registered identity **`surf`**): every fan bolt deals 2 (a
diverging fan lands at most one bolt per body, so per-bolt damage is
not representable and "sharper center" ships as all-2 with identical
duel effect); the fan's profile counter drops to the 1-tick floor so
the stance stops taxing the mobile gun; and frequency moves to the
stance ENTRY routes as `cooldownTicks: 8` — the first consumer of
#181. That bound the readability law: live route-cooldown clocks are
now published (`self`/ally `routeCooldowns` = ordered
`{transitionId, readyAtTick}`, trailing tagged wire field, replay-v3
key present only while live, validators reject lapsed or unordered
clocks; observation schema stays 2). Cast cells stay byte-identical
(pinned), and the salvo values ship untuned per the dump-then-tune
doctrine. SDK 0.10.8 / CLI 0.9.25.

Coarse surf read (same rebuilt wave-6 cohort and seeds as the tide
read, so directly comparable): bulwark-vs-fabricator **+0.333** —
identical to tide, as it must be (salvo inert-omits there; the field
replays mint the tide identity); bulwark-vs-striker **+1.000**;
fabricator-vs-striker **+1.000**. No movement — and the usage count
says why: 57 volley entries across the 45 striker matches, most of
them from still-water (4–5 casts/match), with vector-edge never
entering the stance at all. The cohort's doctrine was authored when
the fan was measured DECLINE, so a stale-doctrine read cannot price
the salvo; it can only confirm nothing regressed. The salvo's real
pricing waits for a doctrine wave briefed on the new fan (and the
route-cooldown observation gives authors the clock to play around).
One CLI catch recorded: the --volley guard first validated the
class-EFFECTIVE kit and rejected the fabricator mirror cells the arm
is designed to inert-omit in — fixed to validate the requested kit,
preserving the one-flag-set-per-wave property.

## 183. The salvo sharpened: 1-tick entry; surf re-mints as swell

Owner ruling on "is salvo enough, or also lower the windup?": fold the
faster entry into the salvo now, one coherent skill change, priced as
a package by the next doctrine wave. Rationale accepted: the fan's
delivery had the worst telegraph-to-payoff ratio in the game — the
only 2-tick public windup (pending transitions are published), spent
stationary for a one-tick payoff, against a shell that deflects
frontal bolts — so damage alone fixes the reward while leaving the
landing problem intact. Under salvo the volley entry drops to the
uniform 1-tick stance grammar (`SalvoEntryWindupTicks`); the measured
cast fan keeps its historical 2-tick entry, pinned. Behavior changed
hours after the first mint, so the identity re-minted: plain token
`salvo` → `crest`, composite `surf` → **`swell`** — the surf-id sweep
replays in #182 stay honest as entry-2 history. On the record
unchanged: striker loses +1.000 in both pairs with near-zero fan
usage, so the chassis is losing, not just the fan; class numbers and
granular asymmetric tiles remain the levers behind this one, and the
salvo package prices at wave 7 [it did — #184].

Coarse swell read (same cohort/seeds as the tide and surf reads;
superseded by the wave-7 read in #184):
**the first striker movement of the campaign, on stale doctrine** —
bulwark-vs-striker +1.000 → **+0.852** (vector-edge beats iron-root
in a seed; arc-light beats march-wall in a seed),
fabricator-vs-striker +1.000 → **+0.778** (still-water beats
ledger-fly in two seeds); bulwark-vs-fabricator **+0.333** unchanged
(inert-omitted, as required). Usage spread from 8 to 11 match-dirs
with casts — vector-edge, which never entered the stance under surf,
now casts. Same deterministic bots, same seeds, doctrine unchanged:
the delta is pure mechanics. Still a stale-doctrine LOWER bound —
wave 7 prices the package properly.

## 184. Wave 7: the triangle closes — every class pair in band

The striker-only salvo-integration round the owner commissioned
("another training round on the striker bots only with the new updated
information"), run while he was AFK under standing autonomy. Three
authors (vector-edge, still-water, arc-light revision 7), wave-6
harness, 3/3 T4 first-attempt, frozen at
arena-bots/frontline-labs/classes-wave-7-strikers-2026-07-30/ with the
full read and converged findings in that README.

The verdict: **bulwark-vs-striker +1.000 → +0.333,
fabricator-vs-striker +1.000 → −0.222, bulwark-vs-fabricator +0.333
unmoved — every class pair inside the cycle-magnitude band for the
first time in the campaign.** Fan usage in the read went 45 → 368
entries. The wave's converged insight: the wave-6 strikers had been
CORRECTLY refusing to cast under the old arithmetic (their refusal
logic priced the old fan right); re-reading the contract's declared
damage numbers was the whole fix, and both attempts at plain
aggression measured worse. The salvo (#182) + sharpened entry (#183)
are hereby priced: adopted, working, tuned enough for now.

Standing caveats: freshness is asymmetric (fresh strikers vs stale
wave-6 bulwark/fabricator doctrine) — wave 8 re-prices the triangle
with every lineage adapted (and with MUSTER + TeamRandom in the game);
iron-root's leg is the entire remaining bvs payoff and two authors
localized it to striker POSTURE (out-trades, under-holds), which is
wave-8 doctrine material, not a fan tune. Distinct-outcome discipline
disclosed in the README. Engineering asks recorded there too — the
recurring ones (qualify's viewer spam, print-candidate-contract
printing identity not contract, movement-blocked naming no blocker)
plus a new inconsistency: two authors observed DIFFERENT
`allowedFormIds` shapes for a cooldown-held route.

## 185. TeamRandom: coordinated unpredictability (SDK 0.10.9 / CLI 0.9.26)

Owner ruling ("go for team random if you can find a decent way to do
it"). The decent way: a team root seed derived host-side in its own
SplitMix64 domain, delivered as a trailing tagged runtime-start field,
with the stream RE-DERIVED PER TICK from (team seed, tick) — never
advanced across ticks — so every life on a team draws identical values
at the same tick, a life born mid-match agrees on its FIRST tick, and
private `context.Random` use cannot desync the shared plan. Teams
cannot derive each other's stream; replays record and re-derive the
seed (C# validator refuses forged/team-swapped documents; TS mirror
bounds-checks). The scaffold's `OrderedDirections` — the wave-6 trap
that invalidated an author's sweeps — now draws from TeamRandom with an
explicit per-life override. Replay-v3 documents grow a trailing
LifeStart key (old documents still verify; new hashes move — the
accepted #169-style consequence). Wave 8's coordination story: teams
can finally flip a coin the enemy cannot predict and all three bodies
see the same coin.

## 186. Side objective, take two: muster ships dormant; SCRAP is the direction

The owner picked MUSTER from the side-objective memo, then pivoted on
seeing what it does: keel's forward rally already IS
respawn-at-the-front, so gating it on a flag is thinner than the memo
sold ("we already have respawn at previous capture"); buff effects were
rejected next ("do better — can we involve RTS aspects? Not
necessarily a hold-the-zone"), landing on two directives: real RTS
macro, and "the side lanes of the map need to matter". The
secondary-control capability + muster arm shipped anyway, DORMANT
(`--side-objective muster`, identities ensign/banner/pennant, new map
generation frontline-labs-02-muster with the alcoves opened into
through-passages, latch factors pre-registered, RELAY the family
control arm) — the flagless game is byte-identical, and any future
site-based effect is an enum value away. The live direction is SCRAP:
a battlefield economy (mirror-scheduled veins in the dead side lanes +
wreckage drops at death sites + team bank + telegraphed tiered
upgrades via an invest action), deep-design memo commissioned with the
owner's constraints as hard requirements (side lanes must measurably
matter; upgrades must not re-open the #184 triangle; body count stays
the fabricator's monopoly). Wave 8 is held until the SCRAP ruling.

## 187. The channel game: capture reworked, SCRAP adopted, ruled under delegation

The part-2 analysis (memo parts 2–3) proved the owner's breach-rush
worry FROM THE CONTRACT: under current capture math, sending any body
to a vein concedes ~one capture, (stay, stay) is the unique
equilibrium, and SCRAP alone measures null — the economy cannot be
fixed by tuning the economy. The owner's own capture-channel proposal
fixes it, and he delegated the ruling ("AFK you decide"). Ruled, per
his stated criteria:

- **`--capture channel` adopted** (composite `siege` = swell +
  channel): capture GAIN counts only bodies that did not change tile
  this tick (denial counts all); damage to a controlling body ON the
  objective reverts the controller's work on this run by the damage
  amount (poke delays, sustained control denies; a screened solo
  channeler completes in 8, unscreened dies; 2 kiting defenders hold
  3); threshold 15 → 8 as the paired `channel-speed` factor; stacking
  scales gain with stationary weight CAPPED AT 2 (fabricator's extras
  buy screens and denial, never speed); whole-run revert (per-body is
  degenerate under the cap); ERODING is also a channel; recapture via
  erosion multiplier N=4 lands the full range in the owner's 1.0–1.25×
  band; ratchet hold NOT re-tuned (diagnostic registered). Zero new
  observation facts — every rule moves captureProgress/claimingTeamId
  in their exact published shape.
- **`--economy scrap` adopted with the significance fixes** (`forge` =
  swell + scrap; `bastion` = the full game): veins 6 scrap at ticks
  120/200/280/360, wreckage 1/death, carried-with-assay (cap 6, pile
  life 80), invest action, edge/plate/optic at a FLAT 10 per tier
  (deep = broad = 30, no volume discounts), prime-only scope in v1
  with per-track scope (plate-to-all-bodies) registered as the v2
  favorite and the all-bodies fabricator amplification (1.27–1.45×)
  on the record.
- **No drone in v1** — three structural blockers (a bolt-absorbing,
  tile-denying free screen is body count by another name under the
  channel; topology-identity fault; unmeasurable in this window).
  Registered `scrap-drone-tier-3` with a build-ready v2 spec.
- **One deviation from the analysis's shipping plan, reasoned**: it
  recommended two sequenced waves (8a siege, 8b bastion). Ruled
  instead: ONE authoring wave briefed on the full bastion game, with
  the balance read run as the pre-registered 2×2 attribution
  (swell/siege/forge/bastion — the arms inert-omit, so one cohort
  plays all four cells). The forced ordering the analysis proved is
  about ADOPTION knowledge, which the 2×2 delivers; the owner
  commissioned one "proper round for all", and a bastion-trained
  cohort still prices siege cells (recorded as a stale-doctrine-style
  confound on the forge cell).
- Pre-registered predictions held to: siege alone moves bvs toward
  +0.05…+0.20 and squeezes the bulwark from both sides — the floor
  watch (any pair below +0.15) is the wave-8 tripwire.

## 188. Wave 8: the channel game works; the bulwark wears the crown

The full-cohort round on the #187 game, run under standing AFK
autonomy. Eight of eight T4 first-attempt, byte-reproducing freezes at
arena-bots/frontline-labs/classes-wave-8-2026-07-31/ (full read and
converged findings in that README). The game itself is a success by
the owner's dynamism criteria: captures are escorted set-pieces, the
stillness doctrine converged in eight independent phrasings, the
striker interrupts, the bulwark holds ground, the fabricator screens
and stacks, and the population found and precisely isolated two engine
defects (both fixed mid-wave, republished, affected measurements
re-run; both reachable only by mask-reading bots — the population is
now the engine's best fuzzer).

The symmetric 2×2 read (every lineage equally fresh — a campaign
first): swell bvs +0.667 / bvf +0.444 / fvs −0.222; siege +0.556 /
+0.500 / −0.222; forge +0.630 / +0.667 / −0.222; bastion **+0.778 /
+0.667 / −0.111**. The #184 triangle did not survive two waves of
bulwark catch-up: the channel alone moved bvs the predicted DIRECTION
at a fraction of the predicted magnitude, the economy as priced is a
bulwark amplifier (+0.223 on bvf), and their interaction (+0.259 bvs)
crowns the bulwark on the full game — the #187 floor tripwire fired
in reverse. Fabricator-vs-striker stays in band on every arm.

Owner-prediction refutations recorded with numbers: the turret is not
the class's recapture denial (a body's unconditional denial weight
beats the conditional revert, twice independently); TeamRandom's
first doctrine verdict is null-to-negative (capability sound, no
doctrine has found where coordinated unpredictability pays).

Next balance levers, in the order the evidence suggests: (1) economy
pricing/scope — the scrap arm favors the class that survives to
collect; the per-track scope (plate-to-all-bodies) and vein/carry
incentives are the registered knobs, and the deep-carry game is
mostly unbought as priced; (2) channel-speed and the interrupt
setting; (3) class numbers. Engineering queue promoted by unanimity:
publish movedThisTick, fix ArenaBasics.Capture's channel misread, fix
abort paths that exit 0. Everything provisional per #174; the owner
rules on the next lever when he returns.

## 189. The legion round: fabricator renaissance, crown dissolved

Six owner rulings shipped as one package after he watched wave 8
("didn't see the new mechanisms"; "initial number of bots higher…
add 2 then 3 so end game is genuinely many"; "recapture needs to be
faster"; "the new mechanism needs to be stronger and happen earlier";
"the respawn at capture point may be too strong and it also means
Fab's signature skill is almost useless — next balancing round
without that"; "longer games ok"; "scraps should decide the game").
Plus the fix for the seeing itself: the viewer now RENDERS both
mechanics (piles, couriers, bank/tier pips, purchase beats, channel
arcs with interrupt-vs-erosion distinction, channeler/screen auras) —
and an adjacent v3 bug meant impact effects had never rendered at
all; restored.

The stack: `--roster legion` (3 bodies at tick 0, fabricator 4 via
dormant-unlock slots its prime field-fabricates — the monopoly becomes
the opening verb; +2 at 150, +3 at 300; endgame 8–9; new map
generation frontline-labs-03-legion), `--pendulum hull` (keel minus
forward-rally: home respawns make fabrication the ONLY forward body
delivery), erosion 8 (flips 1.125×), economy v1.1 (veins 8 scrap from
tick 60 every 70 through 620, wreckage 2/death, six-tier board — the
economy is now allowed to decide), `--horizon long` (750). Wave-8
tokens retired to their measured bytes; the package mints
vigil/crusade. Full-roster observation: 14.1KB, 1.3% of the payload
cap.

Coarse crusade read (wave-8 cohort, stale doctrine on every axis —
the heaviest lower-bound caveat of the campaign):
**bulwark-vs-striker +0.778 → +0.370; bulwark-vs-fabricator +0.667 →
−0.278; fabricator-vs-striker −0.111 → +0.333.** The bulwark crown
is gone and the fabricator is the package's winner exactly as
designed — its restored signature plus the numeric opening. A soft
cycle shows (bulwark > striker > … < fabricator > bulwark). Pacing
note, honest: 44/63 matches reach max-ticks and draws returned (6) —
eight-body defenses under stale doctrine hold hard; whether that is
the doctrine or the numbers is wave-9's question, and
channel-ratchet-retune is now the most-live registered factor (home
respawns lengthened every walk the 40-tick hold was calibrated
against). Ground healing (slow, own-half, stillness-gated) is ruled
and slated as the next-window A/B.

## 190. The mind: one runtime drives all of a participant's bodies

Owner ruling, made knowingly after the feasibility read: "I want the
most drastical one — the 'one mind controls all bots'… I think this
will let the player focus more on the real fun complexity than
ergonomics." The per-life every-bot-for-himself model — with its
common-knowledge coordination, life-scoped memory, and the wave-6/8
friction families it produced — is superseded for this game by a
PARTICIPANT-scoped controller: one submitted artifact, one runtime,
one persistent mind driving every body that participant owns for the
whole match. The owner's own rider fixes the boundary correctly: the
mind is the PARTICIPANT, not the team, so future 2v2/FFA formats
(already expressible in the gen-3 format definitions) are teams of
ALLIED MINDS — and the common-knowledge toolkit (TeamRandom, declared
intents) survives one level up as inter-mind coordination rather than
dissolving. Wave 9 was cancelled minutes in precisely because
authoring more per-life doctrine ergonomics would have been obsolete
work. Design memo commissioned: profile shape beside the per-life
generation (the shipped duel product and the measured cohorts stay
playable and comparable via a wrap adapter), persistent-memory
information game analyzed with eyes open, fuel and payload budgets,
replay-v3 mind turns, qualification and scaffold redesign, migration
plan for the eight lineages. The build ruling follows the memo.

## 191. The mind build: full go, P0-P6, gated at the null pin

Owner ruling on the memo ("Full go: P0-P6"): the mind profile
(generic-mind-match-1, resolved contract schema unchanged so the null
pin means something) builds in the memo's phase order — P0 reserved
field IDs (role tags, allied intents, per-slot chassis, candidate
chassis: the one irreversible step), P1 engine session + contract, P2
SDK/Guest/Runtime + codecs + the WrappedPerLifeMind adapter, P3
replay mind-turns + validators + TS + viewer, P4 CLI/qualification/
scaffold/docs + publish, P5 THE NULL PIN (wrapped cohort
outcome-identical across profiles — explicitly allowed to stop the
project), P6 the port wave with the ported-vs-wrapped A/B and the
pre-registered pacing diagnostic. Compositions (P7) and pacing arms
(P8) stay read-gated behind P6. Fuel 250M + 200M/body, persistent
memory undamped with the fog-effectiveness diagnostic, `mind` is the
product word.

## 192. The null pin holds: the mind is behavior-identical, 63/63

P5 ran twice, and the first run is the story: 33 of 63 cells DIVERGED
— the pin caught a real wrapper defect (one shared TeamRandom stream
position across sub-brains, where per-life semantics re-derive each
life's stream per tick so every life's Nth draw is identical). Fixed
at the source, pinned by a two-body double-draw regression, guest
adapter 0.10.12. Second run: the rebuilt wave-8 cohort's full matrix
on the warpath stack, actor profile vs mind profile, zero tolerances
— winner, end tick, completion reason, scores, and every body's
accepted-action sequence — **63/63 identical. The mind plays the same
game.** Also learned and recorded for the port handover: pre-mind
artifacts fault at startup on the mind profile (expected — their
guests predate the protocol; the standard from-source rebuild fixes
it), and a mind-startup fault currently aborts document recording
instead of producing a clean disqualification (queued in the
pre-friction pass). P6 goes to Codex per the owner's directive, after
the pre-friction check.

## 193. Pre-friction pass done; the Codex handover state is FINAL

The pre-friction check commissioned in #192 landed in full, CLI
0.9.29 (local-window bump, republished to sandbox/cli-publish). What
it fixed, beyond docs: (1) the mind-startup fault recording defect —
`FaultedTurn` stamped the canonically-first body's ActorId on every
stopped body's fault, so recording aborted with "Runtime fault
evidence does not match its actor turn"; now
`GenericMindRuntimeFault.ToActorFault(body)` stamps each body's own
identity and the repro completes as a clean participant
disqualification, exit 2, replay verifies. (2) The generic-mind
template's ArenaBasics released VACATED tiles despite the contract's
`followingVacatedActorAllowed: false` — 16/1357 blocked moves in a
scaffold match; the contract-reading `Vacate` fix measures 0/1512.
(3) Uniform CLI failure mapping: new `CliFailure`/`MatchRun`, abort
exit code 4 distinct from disqualification's 2, stderr guaranteed,
27-case pin. (4) `--print-candidate-contract full` prints the whole
resolved canonical contract (Codex's contract-reading entry point).
(5) `SdkVersionAdvisory` warns on manifest/toolchain mismatch — the
#170/#192 pre-mind-artifact startup fault now has a legible
pre-flight hint. Doc sweep: retired `levy` spelling and the stale
"fabricator must fabricate its opening" claim scrubbed from brief,
CLI help, run banner and BotProject note; enemies-publish
routeCooldowns falsehood fixed; 4 new DocDrift pins; one pre-existing
red test repaired. All suites green. Handover doc carries the two
owner amendments (skip the strict-port stage; THREE bots, one per
class, agents unleashed); wrapped baselines prebuilt at
sandbox/w8-mind-0.10.11. Codex runs P6 whenever the owner says it is
due. Parked for the owner: the fabricator-bootstrap fork
(base-as-root-factory vs elimination) in the mechanism slate.

## 194. Build the game first; the fabricator bootstraps from base

Two owner rulings (2026-07-31). FIRST, the order flips: "we're at the
point where we're designing and building the game before we build
more bots." The Codex mind wave stays parked — the handover is final
but untriggered — and the design window the slate had scheduled
post-wave builds NOW, so Codex's mind natives will be written against
the finished game rather than one shifting under them. The handover
and wrapped baselines get a refresh pass before Codex ever runs.
SECOND, the open fork closes: at total body loss the fabricator's
HOME BASE acts as the root factory — a structure, not a body, can
always seed one body. Comeback preserved, no special body returns,
and a full wipe is a huge tempo win rather than an instant kill.
Total-loss-as-elimination stays registered as the sharper alternative
arm. The build package that follows under #174 delegation: prime
dissolution for all classes (one chassis, one lifecycle per class),
the headless fabricator production network, FOUNDRY in its refined
shape (free chassis choice, scrap buys tempo, home delivery vs field
fabrication), the upgrade-scope re-rule the slate owes, and the
ground-healing A/B riding the same read.

## 195. Commander mode: the passive manager layer, per-sheet only

Mid-window the owner re-scoped #194's package ("remove prime" and
composition are ready; the headless fabricator network is PART of
removing prime; FOUNDRY tempo, home delivery, and ground healing go
back on the design table) and opened the bigger question: can
non-coders play? The direction that emerged and is now ruled:
commander mode, a PASSIVE manager game on the mind architecture. A
non-coder authors a SHEET — composition plans, ordered upgrade
priorities with reserve triggers, economy and capture policies,
roles, FF12-style ordered gambits, and DRAWN spatial plans (paths,
zones, rally lines) — which configures a curated stock mind and
compiles to an ordinary artifact; the ladder plays it while the
player is away, and the morning report (results, decisive replays,
loss attribution, counterfactual re-sims) closes the loop. The owner
ruled drawings PER-SHEET: saved plans executed blind, never live
redrawing — no decision points, no sessions, no pause machinery; the
interactive Mechabellum-style variant is dead. Matchmaking being
blind makes the gambit block carry ALL counter-play, so in-match
conditionals are first-class in the stock-mind config schema. Owner
direction alongside: a much wider class roster (stable of ~5 from a
launch band of ~10-12 mechanically distinct classes — dormant engine
mechanics are the seeds) and a reward loop that unlocks BREADTH,
never power. The map likely grows (spatial authorship needs a
"where"; the mind removed the per-life big-map penalty; map-gen-3
regions are the machinery). Full design:
docs/DESIGN-COMMANDER-MODE-2026-07-31.md. The empirical go/no-go is
the DEPTH AUDIT — fixed stock mind, sheet-space tournament, payoff
matrix read for dominance vs cycles — which, like the map-scale
prototype, waits on stock mind v0 after the one-chassis package
lands. Still open with the owner: the one-shared-ladder question and
who writes stock minds beyond the curated first set.

## 196. The game-redesign campaign hands to Codex; 2D is the renderer

Owner rulings (2026-07-31), driven by quota — the campaign continues
under Codex with the owner at the gates. FIRST, presentation: the
experimental game GOES BACK TO THE 2D RENDERER as its primary and
only REQUIRED surface — way less work per class, and agents can
review a Canvas2D frame autonomously. The 3D viewer is PARKED for the
experiment (kept compiling, no longer extended per new mechanic); the
shipped duel product keeps its 3D stance. The #189 law stands
("viewer must render the mechanics") and 2D is now how it is
satisfied cheaply. SECOND, the commission, in the owner's own frame:
15-20 classes; fun variety; "we are NOT committed to any of the
existing classes"; each class some depth but not too much; a BIGGER
map than current frontline; and core mechanics MORE INTERESTING than
today's — the owner's verdict on the current game is on record:
"frontline feels a bit too dull." Commander mode (#195) is the
overall vision it all serves. The one-chassis package (#194, in
flight) lands as class-agnostic infrastructure — chassis unification
and slot-scoped composition survive any roster. The mind-native bot
wave (the previous Codex handover) is SUPERSEDED IN ORDER: bots are
authored after the game stabilizes, not before. Owner gates are part
of the campaign contract: taste rulings on curated forks, and the
felt-experience gate — galleries watched by the owner — as the only
authority on "fun."

## 197. The commission un-led: figure it out; one skill; fun to watch

Owner refinement of #196, ruled before the handover freezes: do NOT
lead Codex toward existing mechanisms ("don't want to lead it in too
much with muster etc") — the enumerated dormant-mechanics shelf came
out of the handover; the engine's shelved mechanisms are minable but
nothing in the current game is a lead. "We need it to make a good
game and figure it out itself." Three shape rulings ride along: ONE
signature skill per class; human fun value is first-class — the game
MUST BE FUN TO WATCH (under commander mode the product largely is
watching replays, and the owner's gate is watching games); and Codex
may design a DRASTICALLY different game than frontline, keeping the
engine platform (deterministic tile arena, the mind architecture, the
WASM pipeline, the harness) while treating everything frontline
layered on top as disposable.

## 198. The commander-mode player layer stays; gates may live in chat

Owner clarifications on #197. FIRST: "disposable" scopes to the GAME,
not the player layer — sheets, ordered gambits, per-sheet drawn map
tactics, the stable, breadth-only rewards, the morning report, and
passive/blind play "largely stay." Codex may tangent and improve on
those ideas but not discard the layer. SECOND, on gate mechanics: the
gates need no ceremony — the owner may run them as chat check-ins
with Codex's steering, and steer freely mid-run besides. What the
gates actually protect is (a) a consolidated decision artifact at
each fork and (b) no building on unratified foundations — so the
hard rule is only that Codex POSTS the gate report and WAITS at the
two big ratification points (concept/roster; the felt-experience
loop) rather than barreling into build on its own say-so.

## 199. Arc Relay and the sixteen-class launch band are approved

Gate 1 owner ruling (2026-08-01). **Arc Relay is the game.** The
approved concept is the larger-map logistics-combat sport recorded in
`docs/reports/GATE-1-CONCEPT-AND-ROSTER.md`: three separated neutral
Wells produce physical Arc Cores; minds allocate, carry, hand off,
escort, intercept, steal and bank them; deliveries charge visible
reactor Pulses, and Pulses decide the match. The concept's
**no-economy / no-score-to-power** stance is part of the ruling: no
scrap ladder or required in-match upgrade economy, and scoring never
grants stronger combat stats. Commander progression continues to
unlock breadth rather than power.

The recommended sixteen-class launch band is approved unchanged:
Kestrel, Palisade, Towline, Patchbay, Lantern, Mortar, Minesmith,
Hush, Relay, Switchback, Longshot, Mason, Sunder, Repulsor, Veil and
Nest. The report's remaining alternates, holds and rejects stay
registered exactly as recorded; a later season may widen the roster
if the game first proves fun. Phase A is closed and its foundation is
ratified for Phase B. This ruling does not itself begin implementation.

## 200. Threefold and the Arc Relay Phase B mechanics are approved

Gate 2 owner ruling (2026-08-01): **approved unchanged.** Threefold and
the complete H0 mechanics brief in
`docs/reports/GATE-2-MECHANICS-BRIEF.md` are the ratified Phase C
implementation hypothesis: the exact 31x23 map; eight fixed bodies per
side; direct composition from the player's unlocked classes with at
most two copies of one class; the three-Well production schedule and
three-live-Core bound; the Core-owned relocation recovery and committed
handoff; three deliveries per Pulse, three Pulses to win, the 600-tick
horizon, and the 20-tick respawn delay; the statline bands and first
implementation envelopes for all sixteen signatures.

There is no five-class stable in ordinary Arc Relay sheet structure.
That mechanism stays registered only as a scale response if the roster
grows beyond roughly 25 classes, or as an explicit draft/tournament
format lever. Approval makes the H0 package buildable; it does not turn
its provisional numbers into measured balance, establish that the game
is fun, or itself begin Phase C. Rules-native evidence and the owner's
felt-experience gallery remain the authorities for those later gates.

## 201. Counterflow is the hosted map; the failed depth gates stay failed

Owner gallery ruling (2026-08-02). Counterflow's exact rotational fairness,
stronger contested-pickup read, preserved pacing, and better felt lane
character are sufficient to promote it from study arm to the working hosted
Arc Relay map. This is a qualitative product choice over the study's default
HOLD, not a statistical depth pass. The adverse evidence remains on the
record unchanged: 75% first-Pulse conversion, zero behind-to-ahead Pulse
reversals, 14 of 16 mirrored matchup sweeps, and zero directed cycles in four
complete counter-webs. The larger map remains rejected after four entrant
eligibility failures.

The cutover is immutable playlist version 3. Version 2 remains registered so
queued historical matches execute against Home Gates Wide. Saved sheets move
to Counterflow through a deterministic nearest-walkable waypoint migration;
sheet and entrant identity persist. The open v2 rating population is copied
to the v3 ladder before v2 closes. Custom minds retain identity and rating but
must preflight again because a mind may validate the map identity itself. No
EF schema change is needed. This map ruling changes no historical canonical
replay, ruleset, economy, score-to-power stance, or first-party stock artifact.

Depth remains an open product risk. The next structural hypothesis is a sport
of symmetric **Pulse drives**: after either team Pulses, the same neutral
kickoff reset begins a new drive, and a sheet may select a fresh authored play
from public match history at that boundary. Reset and play selection must be
preregistered and tested independently before combination; neither gives the
trailing side power or changes combat stats.

## 202. Home Siege Stage 1 passes as grammar proof and trips the camping alarm

Owner-directed catch-up ruling (2026-08-04) on the Stage 1 report. The 10–0
Home Siege result against the frozen static baseline is accepted for exactly
two simultaneous claims: the evaluation-grade sheet grammar can execute a
long-horizon, casualty-tolerant team strategy, and the registered **Home
camping dominates** alarm fired. The latter is not erased by the former and
the former is not a whole-game balance or fun approval. The unchanged v4 felt-
degeneracy bars remain an admission condition; a siege that trips them fails,
and the detectors are never adjusted to admit the tactic.

The intended first answer to the alarm is a separately authored, evidence-
based recognizer/counter sheet. Pending that test, Stage 1 authorizes no map,
spawn-safety, rules, class, or balance reaction. If a competent counter cannot
answer the frozen siege, geometry becomes a later owner-gated design question.

## 203. Home Siege v3's strict 9–2 correction is accepted

The corrected v3 becomes the canonical Home Siege subject for the next stage.
Against the unchanged stock-v4 coordination-parity control, all 24 fresh,
mirrored, sandboxed-WASM cells ended in a Siege reactor win: 12 at 9–1 and 12
at 9–2, every cell retaining all three integrity segments, reproducing its
canonical hash, passing the unchanged bars, and recording no runtime fault.
The strict lifecycle audit also closes the live 5+1+1+1 schedule, replacement
rejoin, exact-carrier release/retarget, bounded causal interception, and
reachable-Core sanitation requirements.

Acceptance is narrow: v3 is the frozen strategy/opponent identity for the
recognizer goal, not proof that camping is healthy or unbeatable. V2 and all
failed attempts remain immutable evidence; v3 does not rewrite them.

## 204. One strategic archetype is authored per owner-gated goal

Each strategy-ladder goal authors, measures, freezes, reports, and then stops
on **one new archetype**. Home Siege, the recognizer/counter, and the later
adaptive answer are sequential goals; they may not be co-authored or tuned
against one another in one run. Variants within the current archetype are
allowed as declared robustness probes, but they do not smuggle the next
archetype into the same goal.

This sequencing is evidence control as well as worktree hygiene: the accepted
subject is immutable before its answer is written, and the answer is immutable
before adaptation begins. Unseen Home Siege lane and threshold/composition
variants may be hashed before recognizer authoring, but their outcomes may not
be used to write that recognizer.

## 205. Strategy acceptance uses a canonical parity baseline and a decisive margin

A strategy does not pass because it edges a weak or obsolete opponent, wins a
coin-flip set, or merely executes its phase labels. Its final gate freezes a
canonical baseline before outcomes, gives that baseline the same applicable
generic coordination machinery as the candidate, mirrors team assignments,
uses fresh sandboxed-WASM cells and unchanged eligibility bars, and
pre-registers a decisive score/integrity margin in addition to wins.

For accepted Home Siege v3 the baseline is the exact stock-v4 coordination-
parity artifact/sheet, and the hard margin is a reactor win in every one of 24
cells, baseline score at most two, Siege integrity at least two, zero faults,
and both sides eligible. Future archetypes must register their own meaningful
decisive margin before their final block; they do not inherit 9–2 as a universal
balance number. Historical static-baseline results remain useful context but
cannot substitute for the canonical parity-control gate.

## 206. Breakwater is accepted at 7/8 and ships as the Stage 2 deliverable

The Breakwater finals (freeze `a2d4202f`, results `e596dbe3`) passed every
registered pairing at its exact bar — frozen siege 3-2 west / 3-1 east,
parity control 3-1 both ways, all by elimination, WASM tick-identical to the
in-process screens, zero faults, zero false-positive fortify latches — and
split the two unseen holdouts: south-mirror fell both ways, while
four-down-double-relay beat Breakwater's east orientation 2-3. The owner
re-ruled the bar post-outcome to "all registered pairings plus at least one
of two holdouts" and accepted the 7/8 result (2026-08-05, in-session).
Consequences: Breakwater (`breakwater-v1`, nestk-r212hl, sheet
`4496dd39…`, executor artifact `f97792b9…`) is the Stage 2 strategy-ladder
deliverable; the double-relay courier rotation is logged as the next
strategy target; the consumed holdout may be used openly as a development
opponent from here on; and the west/east detection-sensitivity asymmetry
(west margin fixed at 3-2 across the searched space) stands as the concrete
motivation for side-keyed parameter overrides in the format.

## 207. Breakwater v2 supersedes v1 as the Stage 2 deliverable

Owner-ruled 2026-08-05 (in-session "yes"). v2 — release-only memory
freshness (18 ticks) on the siege-release predicate, no side overrides —
passed its pre-outcome freeze (`c7d47647`) at 10/10 evidence cells by
elimination, including the v1-failed double-relay east and a blind
double-kestrel holdout on first contact, with zero faults and zero
false-positive fortify latches. The v1 sheet is superseded; the v2 config
is the artifact all Stage 3 candidates are measured against. The owner
also approved the sentinel-zone promotion run as the next campaign; the
Stage 3 cohort-gate proposal remains open pending owner ratification.

## 208. Stage 3 gate: cohort slice replaces the parity control

Owner-approved 2026-08-05 ("let's proceed" on the recommended protocol).
From the sentinel-zone campaign onward, finals evidence includes the full
depth-map cohort read — all 32 entrants, both orientations, WASM — with a
registered bar of at least 75% of games won and no single entrant sweeping
the candidate 0-2. Entrants that beat the reigning deliverable both ways
are named as explicit counterplay targets for the following rung. The
frozen-champion pairings, blind-holdout discipline, false-positive reads,
and elimination-only scoring are unchanged.

## 209. Sentinel-zone v1 passes the Stage 3 gate and is the Stage 3 deliverable

Owner approved the promotion run in-session (2026-08-05); the pre-outcome
freeze (`52e8a4a1`) registered the champion cells, the ratified cohort
gate, an adversarial blind holdout, and false-positive reads. All bars
were met in 75 WASM games with zero faults: Breakwater v2 beaten 3-1 both
ways, Home Siege v3 3-0/3-1, the mortar-line holdout — authored as the
designed counter to sentinel clusters — shut out 3-0 in both orientations
on first contact, cohort 60/64 with no entrant sweeping the candidate,
and zero false-positive fortify latches. Sentinel-zone v1 is the Stage 3
deliverable; the four east-leaning cohort split-losses are the recorded
signal for the next rung's targets.

## Deferred decisions

- Numeric limits for submissions (archive size, file counts) — Phase 3.
- Named RNG streams (`context.Random.Stream("...")`) — not in 0.1.
- Whether player artifacts embed one bot per artifact (likely) or use the
  built-in-style multi-bot selector — decide when the project template lands.
- wasmtime-dotnet pinning strategy across OSes for identical fuel accounting —
  verify when a second platform enters CI.


## 210. Foundations -03: seed variance and side fairness are rules, not tuning

Owner goal (2026-08-05, in-session): single-game determinism made every
matchup a solved chess position, and counters could only be built with
one-tile scalpels. `arc-relay-forward-combat-03` mints beside -02 with two
foundations. VARIANCE: each scheduled well birth round shifts within a
seed-derived +/-6-tick window (`wellBirthJitterTicks`, drawn from the match
seed per well and round via a domain-prefixed SplitMix64 stream in
`SeedDerivation`); measured 8/8 distinct outcomes across 8 seeds on a fixed
matchup, same-seed replay hash byte-identical. FAIRNESS
(`alternatingResolutionOrder`): the order-dependent slices of resolution —
contested projectile consumption during movement, projectile advancement,
and same-target hook pulls — alternate direction by tick parity instead of
always resolving lowest-ActorId (team 0) first, and sentinel target ties
break toward the shooter's own reactor before ActorId. Both fields are
canonically written only when non-default; -01/-02 fingerprints and goldens
stay byte-exact.

## 211. Executor decisions are expressed in the team's canonical frame

Mirror matches under -03 exposed that the tactical executor's tie-breaks
were absolute: lowest-Y/X goal and placement preferences, heading and
direction enum order, formation reflow and spacing ties. Under the
180-degree side binding these pick opposite relative tiles for the two
sides — measured as 6-7/8 side skews in mirrors. All executor tie-breaks
now route through the mind's canonical frame (`MirroredFrame` /
frame-keyed orderings). The executor also gained a dedicated
opportunistic heading channel (hooks and rails fire at ray-aligned
hostiles without waiting for the gun-gated focus path; 0-2 casts/game
became 51-55, stock parity) and mine avoidance (armed visible hostile
trip-nodes are blocked tiles; forced movement still lands on them).
Residual documented: the hook-heavy hook-control mirror retains a
13/16-west skew whose mechanism has survived every structural engine
audit so far; sentinel-zone and stock-baseline mirrors sit inside the
6/8 bar. Cross-runtime note: tactical-mind games are runtime-deterministic
but not byte-identical between in-process and WASM (same winner observed);
evidence remains WASM-only per the standing house rule.

## 212. Positional combat remake: fights resolve from the board, not from micro

Owner ruling 2026-08-11, after replay review kept finding dances and
Mexican standoffs that every executor patch only relocated: projectile
dodge combat is the wrong model for a deterministic, spectated,
sheet-authored game. Perfect-information bots dodge perfectly, so duels
resolve by tie-break arithmetic that neither the author nor the spectator
can read, while everything that works in the game (pulse race, birth
windows, hunts, promotion, backstabs) is positional. The remake goal is
recorded in docs/EXPERIMENTAL-ARC-RELAY-POSITIONAL-COMBAT.md: combat
becomes declare -> windup cone -> first-body-in-the-way resolve, with
facing/level multipliers; sheets own all judgment (engagement entry and
exit conditions); the mind owns few convergent verbs; standoffs become
unrepresentable rather than discouraged. Minted as arc-relay-ambush-11
behind GameRules flags, frozen-beside; acceptance is a 24-seed battery
with zero felt-degeneracy bar trips, no unresolved-contact streaks,
both existing sheets running untouched, and owner replay review as the
final gate. Ranged area-denial and sheet balance are explicit non-goals
until the melee-beat core proves out.

## 213. Strike cones are the real filled wedge, resolved to the nearest body

Owner rulings 2026-08 during -11 replay review, superseding the
three-spoke fan of #212's first cut: "attacks are not cones..?", then
"I want the real cone this is just weird now". The declared telegraph
is now the filled 90° wedge between the resolved heading's adjacent
sectors — Chebyshev reach, wall-occluded through the canonical strike
line (`GenericActorStrikeCone`), boundary spokes inclusive — and
single-target resolution picks the nearest body anywhere in the wedge
(Chebyshev first, most-central on integer-exact angle ties, canonical
tile order last), delivered along the same 8-connected strike line
through the standard projectile machinery so interposition still works
tile by tile. A lit tile is exactly a hittable tile; three diverging
spokes with unlit gaps between them were neither honest nor readable.
Authored volleys keep frozen per-ray fans; windup stays 1 (one honest
move, owner tuning after verifying movement precedes combat on the
resolve tick). Signature bolts (sentinel/hook) still fly untelegraphed
as historical utility casts — flagged to the owner as the remaining
inconsistency, ruling pending.

## 214. Movement resolves by weight: carrier right-of-way lanes

Owner design direction 2026-08, after watching the diagnostic pocket
gallery and asking the right question ("we have a bunch of units that
want different places - prioritize whose movement weighs most and
adjust accordingly"): tile conflicts are a cooperative pathfinding
problem, not a per-unit discipline problem. Two blocker-side plug tests
(bank-side geometry, then whole-map path existence, then
policy-admissible steps) all failed the same measured scene because the
failure was interactional - the escort on the carrier's route DID try
to move aside every tick, but a dancing teammate earlier in unit order
claimed its escape tiles, and stole the lane tile whenever it was
freed. The fix is priority in the existing reservation system: Claims
carries carrier-owned lane tiles (BlockedNow blocks them for every
other own body), and bodies standing on a lane act immediately after
carriers so their escape choices precede lower-priority claims. This is
windowed cooperative pathfinding in miniature - reservation table plus
priority classes - and it removed the entire (13,7) corridor family in
one battery (ab53 17/24 -> ab54 21/24, zero friendly-blocked runs).
The idle-break's supply-lane plug test (#213-era) remains as a backstop
but is expected dead; removing it is a measured follow-up. Attribution
tooling: scripts/arc-relay-pocket-attribution.py classifies every stuck
run (free / friendly-blocked / enemy-involved, per-unit blocker table)
so the next pocket names itself.

## 215. Strikes lock and follow; the wedge is reach, not a zone

Owner ruling 2026-08, after the tracking-ray measurement (ab54: 38% of
strikes landed on the aimed body, 26% side-swiped a neighbour the
target dodged into, 88% of targets had a one-step wedge exit): the
strike is an AIMED attack. It locks the body the resolution rule picks
when the cone lights, follows it anywhere inside the frozen wedge, and
resolves only against it; it cancels outright - no bolt, the
dead-shooter precedent - when the lock dies, crosses the wedge
boundary, or leaves the shooter's own line of sight (VisibleTilesFor).
Bodyguarding is stepping onto the firing line, never proximity, because
delivery stays a first-body-contact ray. Empty-wedge declares keep the
theatrical centre whiff. Escape is earned geometry: boundary and
max-range tiles have one-step exits, the deep interior is committed.
Measured (ab56): hunter 15-9, 21/24 bars-clean, 5794 engagements, 59%
kill endings, zero unresolved streaks - the every-fight-ends criterion
survives the collateral bucket becoming escapes. The lock rides the
pending-strike wire so the viewer's ray is authoritative.
