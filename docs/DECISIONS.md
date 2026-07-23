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
42. **Typed job lanes: one match lane + N compile lanes**
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

## Deferred decisions

- Numeric limits for submissions (archive size, file counts) — Phase 3.
- Named RNG streams (`context.Random.Stream("...")`) — not in 0.1.
- Whether player artifacts embed one bot per artifact (likely) or use the
  built-in-style multi-bot selector — decide when the project template lands.
- wasmtime-dotnet pinning strategy across OSes for identical fuel accounting —
  verify when a second platform enters CI.
