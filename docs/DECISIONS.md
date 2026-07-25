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
    `docs/RULES-0.5-PLAYER-GUIDE.md` becomes the one player-facing
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

78. **Maps own immutable presentation themes; the default world is the
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
    selected from ASCII adjacency; decision #79 supersedes that production
    detail. Mutable viewer/account preferences must never rewrite historical
    playback.

79. **Arena walls use map-authored families and deterministic topology
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

80. **Combat contrast minimally adapts the bot accent to the painted local
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

## Deferred decisions

- Numeric limits for submissions (archive size, file counts) — Phase 3.
- Named RNG streams (`context.Random.Stream("...")`) — not in 0.1.
- Whether player artifacts embed one bot per artifact (likely) or use the
  built-in-style multi-bot selector — decide when the project template lands.
- wasmtime-dotnet pinning strategy across OSes for identical fuel accounting —
  verify when a second platform enters CI.
