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
human audition.

AAC in M4A is the baseline output because it keeps the broadest practical Safari/iOS and
hardware-decoder path. The manifest and server MIME policy do not hard-code playback to
that choice, so an Ogg/Opus variant can be measured later without redesigning the runtime.
Codec size alone is not approval: transitions, loops and the combined in-game mix require
listening on representative phones and headphones. Neon Protocol remains
`user-supplied-unverified`, `shipApproval: pending`, and `analysis-reviewed` until rights
and human audition are explicitly recorded.

## Deferred decisions

- Numeric limits for submissions (archive size, file counts) — Phase 3.
- Named RNG streams (`context.Random.Stream("...")`) — not in 0.1.
- Whether player artifacts embed one bot per artifact (likely) or use the
  built-in-style multi-bot selector — decide when the project template lands.
- wasmtime-dotnet pinning strategy across OSes for identical fuel accounting —
  verify when a second platform enters CI.
