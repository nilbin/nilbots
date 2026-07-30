# GateStone — DX findings and freeze record (wave 5)

Wave-5 Frontline Labs entrant, class **bulwark**, revision 2 of the `gate-stone`
lineage (wave-4 revision 1 is its only predecessor). Written before seeing any
other entrant's source, replays, standings, or any aggregate balance report.

## Isolation statement

- **Read:** `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`
  (sha256 `d31b59aa…`), `docs/FRONTLINE-LABS-RULES.md` (`06ff461e…`),
  `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` (`2333bd3c…`, read fully — the
  wave-4 freeze recorded `b91047df…` for the same path, so this file moved
  between waves) — plus `templates/botarena-generic-actor/`, type declarations
  under `src/BotArena.Sdk/`, the CLI help of `sandbox/cli-publish/`, my own
  wave-4 directory (`arena-bots/frontline-labs/classes-wave-4-2026-07-30/gate-stone/`,
  the one other bot source this brief permits), and my own contracts, replays
  and qualification evidence.
- **Not read:** any other entrant's directory, source, replay, standing or
  aggregate report; any Engine or App implementation file; any `docs/` file
  outside the three permitted ones. The wave-5 directory already contained
  three sibling entrants when I arrived; I listed the directory once to learn
  whether `gate-stone/` existed and never descended into a sibling. I also
  never opened `docs/DESIGN-MECHANISM-SLATE-2026-07-29.md` or
  `docs/DECISIONS.md`, which the permitted class doc cites.
- **Private scratch:** `sandbox/gate-stone-w5-scratch-4e7c1ab9/` — uniquely
  named, created by me, never shared. No shared or guessably named scratch path
  was written or read, so there is **no accidental exposure to disclose**.
- **Sparring:** my own wave-4 predecessor, **rebuilt from source** into my
  scratch directory with `nilbots build … --no-cache` (artifact
  `e530682271ab22ac24ee05dac1cfc1814d351d202462c18dca9fa303a0b680d5`), plus my
  own four intermediate revisions. Every recorded number uses that rebuild.
- **Mid-wave correction received and honoured.** The coordinator flagged that
  the class doc's identity paragraph still said the bulwark may "Mobilize back
  once per life". My code never read that sentence: reversibility comes from
  `irreversibleForLife` on the route and the health map from the route's own
  `health.policy`, both dumped from `header.contract` before a line was
  written. The contract says `irreversibleForLife: false` on both mobilize
  routes and `preserve-ratio-floor-minimum-one` with `flatHealthGain: 0` in
  both directions, which is what the brief said and what I built against.
- **Git:** nothing committed, nothing staged. The wave-5 tree is untracked.

## Freeze identity

| field | value |
| --- | --- |
| entrant / class | `gate-stone` / `bulwark` (declared in `botarena.json`) |
| lineage | wave-5 revision 2; predecessor = wave-4 revision 1, same name |
| role / target | `verdict-doctrine`, target T4 (suite 5), achieved T4 |
| doctrine | `capture-arithmetic-gate` |
| author packet | `FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md` sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| the game | `deck` = `--movement facing-locked --pendulum keel --skills kit --bend universal --aim offset --stance-ground open [--five-slots wane]` |
| resolved cells | `frontline-labs-1-bulwark-vs-fabricator-deck-facing-locked`; `…-bulwark-vs-bulwark-sail-open-facing-locked`; `…-bulwark-vs-striker-sail-open-facing-locked` (`deck` names itself only where a fabricator is in the cell) |
| toolchain | nilbots CLI **0.9.21** (the brief names 0.9.20), SDK 0.10.6, build pipeline 4, NativeAOT-LLVM 10.0.0-rc.1.26306.1, wasi-wasm p1 core module, Docker platform-matched builder |
| runtime | actor protocol 1.0, configuration 1.0, contract profile `generic-actor-match-2` |
| build | `nilbots build . --no-cache`, cache key `8722abdb01c9e81aee0bb0e3ebd1f6b92b94b25d9eccfa3b471bb2a840c0919f` (miss/compiled) |
| **bot.wasm sha256** | **`bf975c47588b00984c79fd6be27c7c87deacc2463dc59d2480c25d53e606608d`** |
| qualification | `evidence/t4/qualification.json`, sha256 `46576b521f8591e01b318e6a9548509dc7b80fcab59c1c2fb6fa4e4c76c2da03`, exit code **0**, tier **T4**, `balanceEvidenceEligible: true`, prerequisite T3 PASS, all five T4 probes PASS |
| source-tree digest | `a52543ea1e65797ab46eb21a6ec517b6c473e435913891fef2a193314c4a7c06` (sha256 of the sorted per-file digest list below) |

Submitted sources (sha256):

| file | lines | sha256 |
| --- | --- | --- |
| `GateStone.cs` | 904 | `2e291ddd3b554bd87d1e8517186970b0b41b5ae093727e007560067b252e69c0` |
| `StoneContract.cs` | 610 | `74843cba283856891a0990610784114db7074c7aebc590caf3c0cb359332af6c` |
| `StoneGround.cs` | 944 | `0db619e4b43f884e9291f3d14179222706afc51fa85ad0f14dbf7b93e12550c6` |
| `StoneAim.cs` | 504 | `549ba4016fe3cadbaa3200590d5b10d9ea49a2f9b70ace967ba3f4a0dbe35f38` |
| `StoneMemory.cs` | 212 | `41df9ece7c828d594f8c89a9de74d616a0510a8677d0b19c0809758601b5d52c` |
| `ArenaBasics.cs` | 1205 | `0f6cde2c1ba950ff69d6f41fec436ab762fddabf04306afebfe640dce40c8d74` (scaffold, unmodified — byte-identical to `templates/botarena-generic-actor/ArenaBasics.cs` apart from the bot name in one doc-comment) |
| `GateStone.csproj` | — | `8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573` |
| `botarena.json` | — | `d94f19d152b951c206aab17f23589b59c00c5ab13364b91f592bbe7fca38071d` |

Evidence layout: `evidence/t4/` (suite-5 report + 36 probe replays including the
hash-linked T3 and T2 prerequisites), `evidence/pairs/` (one WASM match per class
pair against the rebuilt predecessor, all three `nilbots verify` OK). Replays are
stored gzipped — the uncompressed suite was 213 MB, the gzipped set is 940 KB and
the same bytes; `gunzip -k` before `verify`. Self-contained `viewer.html` files
were deleted (reproducible from the replay).

## Doctrine, in one paragraph

GateStone keeps one ledger whose unit is **one tick of objective weight**, and
settles every decision in it. Under this arm's net-scaling control policy the
signed capture gain is linear in net weight, so the marginal worth of a body
standing on the active objective is exactly its own weight per tick — and
exactly zero while the published redeploy pause runs or while a completion would
be spent inside an enemy hold, which is what makes those windows free. On the
income side a destroyed enemy body is worth its objective weight multiplied by
the absence its own lifecycle profile declares — eighteen to thirty ticks of
progress its owner never collects, doubled when nothing returns that slot
without an explicit action — and that is the only currency an
objective-weight-zero turret can earn in, so it is collectable only behind
relief: alone on contested ground, fortifying buys a receipt nobody can cash,
and the gate refuses. Because `anchor` ⇄ `mobilize` are reversible for the whole
life, fortifying became a lease rather than a vow: the turret's cooldown-1,
travel-8, eight-headed gun is rented in the ticks weight is worth nothing and
handed back the tick the point starts paying again, taken **on the objective
tile itself** wherever open placement allows, because a turret there scores
nothing but is one tick from scoring again and denies the tile to a body that
would. Health maps proportionally with a floor in both directions, so a full
body round-trips losslessly and a wounded one pays the floor each leg — below
full, the gate reverts to the terms that justified a permanent anchor. How much
relief the gate demands is itself read from the declared body curve: outnumbered
on slots, one spare body is all the relief that will ever exist and the gun has
to be picked up behind it; level or ahead, presence is the cheaper lever and the
two-body margin stands. The shield, finally, is a parry and mostly declined — a
guarding form declares no attack profile, so the cooldown does not advance while
it is up and a shielded tick banks nothing while costing a full tick of fire, so
the arc rises only against a bolt already in the air and already inside it with
nothing leaking round the side, and the restored ±45° launch offsets do the work
the shield used to be asked for, because a bearing a locked facing is not
looking down cannot be shielded and costs a whole tick to answer.

## Measured records vs the rebuilt wave-4 predecessor, on the crew game

All cells are the full flag set above, `--ignore-declared-classes` with an
explicit `--classes` so a single-class author can be measured across pairs.
Mirrored accounting: 3 seeds (104729 / 130363 / 155921) × both bot-to-side
assignments = **6 games per pair**, attributed by
`header.provenance[].artifactHash` against `result.standings.winnerTeamId`
rather than by the CLI's summary column (see friction 3).

| class pair | GateStone w5 vs rebuilt w4 | breach ticks |
| --- | --- | --- |
| `bulwark-vs-bulwark` | **6–0–0** | — (all max-ticks) |
| `bulwark-vs-fabricator` | **6–0–0** | 195 ×3 (as bulwark), 451 ×3 (as fabricator) |
| `bulwark-vs-striker` | **3–3–0** | — |
| **total** | **15–3–0** | |

The `bulwark-vs-striker` 3–3 is a **class fact, not a bot fact**, and the control
says so: predecessor-versus-itself in that cell is won by team 0 (the bulwark) on
all three seeds, so whichever bot holds the bulwark chassis wins. GateStone wins
every one of the three games it plays on its own chassis there and loses the
three it plays as a striker. The same control makes the other two cells
meaningful: predecessor-versus-itself gives the **fabricator** side all three
seeds in `bulwark-vs-fabricator`, and GateStone reverses that cell from the
bulwark side.

The cleanest measurement is a **same-side A/B**: hold the opponent, the side and
the seed fixed and change only my bot. Nine games, team 0 = bulwark, opponent =
the rebuilt predecessor on team 1.

| cell | predecessor on the bulwark side | GateStone w5 on the bulwark side |
| --- | --- | --- |
| vs bulwark | **−13**, max-ticks | **+22**, max-ticks |
| vs fabricator (`deck`) | **−30**, breach *against* at t451 | **+30**, breach *for* at t195 |
| vs striker | **+17**, max-ticks | **+26**, max-ticks |

One WASM confirmation per pair (frozen artifact vs the predecessor's rebuilt
artifact, seed 104729) reproduced the in-process result exactly and verifies:

| pair | result | replay hash |
| --- | --- | --- |
| `bulwark-vs-bulwark` | win, max-ticks, +22 | `d1db8677ed2279c58d226e3f82396a191d233b4a29b95bbd272bc738372976a1` |
| `bulwark-vs-fabricator` | win, base-breach t195, +30 | `b9bfa4cb744b8eea451b514e1fd3014286d214e5a4fc3a1599fae69380a88aed` |
| `bulwark-vs-striker` | win, max-ticks, +26 | `89751eafa575d73bfa03984750c02e035170133d0b990af58e00536e57649086` |

### Skill and diagonal usage — the same nine same-side games, team 0 only

| metric | predecessor | GateStone w5 |
| --- | --- | --- |
| volleys cast | **0 (structural)** | **0 (structural)** |
| shells raised | 372 | **90** |
| — of those, on an objective tile | 270 | 18 |
| ticks spent shielded | 2685 of 7203 body-ticks (**37 %**) | 117 of 5247 (**2.2 %**) |
| deflection events | 75 | 51 |
| turrets anchored | 78 | 72 |
| — of those, **on an objective tile** | 3 | **18** |
| ticks spent anchored | 537 | 243 |
| mobilizes (turret + shell exits) | 393 | **147** |
| all form transitions | 450 | **162** |
| mobile shots fired | 513 | 513 |
| turret shots fired | 54 | **81** |
| **diagonal launches** (aim offset ≠ 0) | 174 | **237** |
| bend programs fired | 285 | 246 |
| kills / deaths | 132 / 108 | **141 / 102** |
| slots fielded | 3 | 3 (4 on the fabricator chassis) |

**Volleys cast is structurally zero**: the volley is striker-owned, so
`--skills kit` in any bulwark cell resolves to the aegis shell alone and the CLI
says so ("requested skills without an owning class in this cell change no
contract bytes and are dropped"). **Shells declined** is the 37 % → 2.2 % row:
both bots run the identical contract, with the guard route legal on every tile,
and GateStone spends seventeen times less of its life behind it while firing the
same number of mobile shots and landing more kills. **Anchors on an objective
tile** is the lever open ground newly grants, and it is now a quarter of all
anchors instead of a rounding error.

## Top 3 frictions

**1. A gunless stance freezes the cooldown, so every exit written as "my gun is
ready" is unreachable from inside it — and the open arm turns that into a lost
match.** The aegis-shell forms declare `attackProfileId: null`, and a cooldown
belongs to an attack profile, so `Self.Cooldown` simply stops advancing while the
shield is up. Measured directly in a replay: a prime enters the shell on tick 9
carrying cooldown 3, decrements once to 2 on the entry tick, and is still
**cooldown 2 at tick 100**. Two consequences, and both bit. First, the wave-4
doctrine's central claim about the shell — "a gun on cooldown three is idle two
ticks in three; those are the ticks the shield costs nothing" — is **false on
this contract**: those ticks stop passing, so a shielded tick costs a full tick
of fire and the shell banks nothing. Second, an exit written as
`if (gunReady && …) drop` can never fire from inside the shell. Under
`--stance-ground strict` that was nearly invisible, because the shield was
illegal on every objective tile and nothing stayed in the quadrant for long; the
predecessor still spends **37 % of all its body-ticks shielded** on the deck arm
and stalls the mirror to a max-ticks ±13. Under `--stance-ground open` it is
fatal, and my own second revision proved it: inheriting that exit, it raised the
arc on the centre objective at tick 9 with an enemy permanently inside the
quadrant, spent **187 of 231 ticks in the shell**, fired six shots all match and
lost a base breach at tick 230. Neither the class doc nor the SDK doc-comments
mention that a form without an attack profile freezes the cooldown, and the
doc's pitch for the stance ("objective weight stays 1, so it still holds
ground") actively encourages the pattern that dead-ends. A one-line note on
`projectileGuard`, or a cooldown that keeps running, would have saved a whole
revision.

**2. `--five-slots` is rejected rather than inert-omitted, so "the same flag set
works on every pair" is not true.** `--stance-ground`, `--aim`, `--skills` and
`--pendulum` all behave as documented — inert factors vanish from the identity
and the same command line runs everywhere, which is genuinely pleasant. But
`--five-slots wane` on `bulwark-vs-bulwark` exits with *"--five-slots tunes the
FIVE SLOTS skill, so the cell must carry it"*. That is a defensible strictness in
isolation, and it is exactly the opposite of the inert-omission rule every
neighbouring flag follows; my first sweep script died on it. Either drop the flag
where the skill is absent (consistent with everything else) or say plainly in the
variant section that this one flag is conditional.

**3. `--swap` mirrors the bots, but the summary's win column still counts
team 0 — and it is labelled as if it counted the bot.** Every run prints
`Total (3 seeds, W = slot-0 bot wins): 3W 0L 0D`. It does not report the slot-0
bot; it reports team 0. With `--swap` the bots exchange sides while the class
binding stays put, so the same label means opposite things in the two halves of a
mirrored set. I recorded a "6–0 mirror" from that line before the control run
made it impossible: predecessor-versus-itself scored `0W 3L` in *both*
configurations, which for two byte-identical artifacts can only mean the column
is side-attributed. The truth in that cell was 3–3. For a wave whose entire
method is mirrored accounting this is the most expensive label in the CLI, and
the only reliable fix is to cross-reference `header.provenance[].artifactHash`
against `result.standings.winnerTeamId` in every replay — which every author will
now write separately. Rename it to `team-0 wins`, or make it follow the artifact.

### Smaller notes

- **"In range" is not "hittable", and it cost a 100-tick mute turret.** An
  absolute eight-way gun fires eight rays with strict diagonal corners, not a
  filled radius. My inherited turret-exit test asked "is an armed enemy within
  range" and answered yes for a body standing one tile diagonally off a blocked
  corner — which no ray of a turret at (10,5) can reach, because (11,5) is a
  wall. The turret held its tile in silence for a hundred ticks with an enemy
  adjacent. Both the entry gate and the exit now ask for a clear lane. The rule
  card states the eight-rays fact plainly; it is just very easy to write a
  distance check anyway.
- **The shell being "a trap vs swarms" is derivable, not just measured.** One arc
  covers three of eight sectors and cannot rotate, so the decline condition is
  exactly "nothing armed outside the arc" — and against a four-slot fabricator
  that is almost never true. Naming the doctrine fact in the brief was useful;
  the contract shape explains it.
- **The ratio floor makes turret durability a full-health privilege.** child 4/4
  → 7/7, 3/4 → 5/7, 2/4 → 3/7, 1/4 → 1/7; back again 7 → 4, 5 → 2, 3 → 1. So a
  full body cycles for free and gains +3 max health, a wounded one gains almost
  no armour and pays a point per round trip. "Anchor full, or anchor to stay" is
  the whole content of that policy, and it is one subtraction from the declared
  formula rather than a table to memorise.
- **The frozen wave-4 artifact did NOT fault for me.** The brief warns that
  frozen wave-4 artifacts fault on the crew contracts. I complied — every
  recorded number uses a `--no-cache` rebuild from source — but out of curiosity
  I also ran the frozen `classes-wave-4/gate-stone/out/bot.wasm` on
  `bulwark-vs-bulwark` under the full flag set, and it played 500 clean ticks and
  produced the same +22 territorial result as its rebuild. Whatever the fault is,
  it did not reproduce for this lineage's artifact on this cell.
- **`transition-placement-forbidden` still tags 112 tiles under `open`; the
  routes just stop citing it.** So a bot that asks the *map* whether it may
  transform gets the wave-4 answer and a bot that asks the *route* gets the
  wave-5 one. Reading `route.Placement.ForbiddenTileTags` is the difference
  between the shield being illegal on every objective and legal on all of them —
  same line of code, opposite plan. Worth advertising more loudly than the
  arm-description bullet does.
- **Hardcoding temptations resisted:** the 60/180/300 unlocks and 22/30 rebuilds
  (read from `lifecycleAssignments` → `lifecycle.profiles`, which is how the
  fabricator's four slots and its two different rebuild clocks reached the kill
  price at all), four slots, the 15-progress / 5-tick-pause / 3-advance numbers,
  the ±1 aim bounds, `maxBendAfterTiles = 2`, "children anchor and primes do
  not" (now a windup-survival calculation, so a prime anchors when it can afford
  three wait-only ticks), "the turret heals on entry" (it does not any more), and
  "anchoring is permanent" (`irreversibleForLife`).
- **Times** (measured on this host): in-process build ≈ 0.7 s; controlled
  `--no-cache` WASM build ≈ 2 min (Docker platform-matched builder on macOS);
  suite-5 qualification in WASM **6.2 s** for 36 probe replays across three
  hash-linked tiers; one 500-tick match **3.3 s** in WASM, **3.8 s** in-process
  including the build. Execution is not the bottleneck — the **213 MB of
  uncompressed qualification replays** is, and it is worth knowing before you run
  the suite twice into the same tree.

## Repairs and strategy passes (the improvement budget, itemised)

Every row is measured as a same-side A/B over the same nine games (3 pairs ×
3 seeds, my bot on team 0, the rebuilt predecessor fixed on team 1), reported as
mean signed territorial progress per cell. The loser was reverted.

| # | change | measurement (bulwark / fabricator / striker) | kept |
| --- | --- | --- | --- |
| 0 | inherited wave-4 policy, unchanged, on the deck arm | −13 / −30 breach loss / +17 | baseline |
| 1 | price the anchor as a per-tick lease (kill absence vs weight-tick rent), allies ≥ 1 | −19 / −30 / −25 | no — too permissive alone |
| 2 | decline the shell when anything armed is outside the arc; drop it on a flanker; never shell against an unarmed enemy | +1 / −30 / −30 breach loss | partly — fixed the mirror, exposed the frozen cooldown |
| 3 | shell becomes a pure parry (a bolt in the air, inside the arc, nothing leaking); turret exit and entry both require a clear firing lane, not a nearby body | **+30 breach t431 / +30 breach t195 / +11** | yes — this is the revision |
| 4 | paid anchor behind 2 allies everywhere instead of 1 | +22 / **−26 (cell lost)** / +26 | no — the four-slot cell needs the gun early |
| 5 | relief demanded by the declared **body curve**: 1 when outnumbered on slots, else 2 | **+22 / +30 breach t195 / +26** | yes — best aggregate, and the gate the brief asked for |
| 6 | tie-break fire toward a bearing the target is not facing (worth 4 against a hit's 100) | **not isolated** — shipped inside row 2, so its own A/B was never run; observed effect over the nine games is 174 → 237 diagonal launches with no cell regression | yes |
| 7 | anti-churn: no stance re-entry inside one round trip of windups; dwell tracked in life-scoped memory across the same-life change | form transitions 450 → 162 on the same nine games | yes |

Honest gap against the packet's archive rule: the four intermediate revisions
were edited in place rather than frozen as separate source trees, so what
survives of them is the measurement table above plus their complete replay sets
under my scratch directory (`runs/rev1`, `rev2`, `rev3`, `ab-allies2`, `rev4`, and
the `w4-self` control). Only the final revision is frozen as source, which is
what this brief's single freeze location asks for.

Qualification history: one attempt, exit **0**, **T4**,
`balanceEvidenceEligible: true`, prerequisite T3 PASS. The suite runs the
classless duel-depth union profile, and the revision needed no repair to pass it
— which is the reward for the wave-4 discipline of reading routes, envelopes and
lifecycle clocks instead of the class arm's shape.
