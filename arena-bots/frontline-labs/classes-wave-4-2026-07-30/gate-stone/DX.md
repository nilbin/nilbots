# GateStone — DX findings and freeze record

Wave-4 Frontline Labs entrant, class **bulwark**, fresh lineage (no predecessor).
Written before seeing any other entrant's source, replays, standings, or any
aggregate balance report.

## Isolation statement

- **Read:** `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`
  (sha256 `d31b59aa…`), `docs/FRONTLINE-LABS-RULES.md` (`06ff461e…`),
  `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` (`b91047df…`) — all three hashes
  verified against the brief before reading — plus
  `templates/botarena-generic-actor/`, type declarations under
  `src/BotArena.Sdk/`, `cosmetics/catalog.json` (to pick a legal appearance),
  the CLI help of `sandbox/cli-publish/` (nilbots 0.9.15), and my own replays,
  contracts and qualification evidence.
- **Not read:** any other entrant's directory, source, replay, standing or
  aggregate report; any Engine or App implementation file; any `docs/` file
  outside the three permitted ones. The wave-4 directory contains other
  entrants; I listed the directory once to learn whether `gate-stone/` already
  existed and never descended into a sibling.
- **Private scratch:** `sandbox/gate-stone-w4-scratch-b83d5f2/` — uniquely
  named, created by me, never shared. No shared or guessably named scratch
  path was written or read, so there is **no accidental exposure to
  disclose**.
- **Sparring:** the scaffold starter only (`nilbots new StoneStarter --profile
  generic-actor`, unmodified except `"class": "bulwark"`, rebuilt with
  `--no-cache` against SDK 0.10.6), plus seven of my own variants used for the
  A/B decisions recorded below. No frozen pre-0.10.6 artifact was used.
- **Git:** nothing committed, nothing staged.

## Freeze identity

| field | value |
| --- | --- |
| entrant / class | `gate-stone` / `bulwark` (declared in `botarena.json`) |
| lineage | wave-4 fresh lineage, revision 1, no predecessor |
| role / target | `verdict-doctrine`, target T4 (suite 5), achieved T4 |
| author packet | `FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md` sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| primary doctrine cell | `frontline-labs-1-bulwark-vs-bulwark-rig-facing-locked` (`--pendulum keel --skills kit --bend universal --movement facing-locked`) |
| toolchain | nilbots CLI 0.9.15, SDK 0.10.6, build pipeline 4, NativeAOT-LLVM 10.0.0-rc.1.26306.1, wasi-wasm p1 core module, Docker platform-matched builder |
| runtime | actor protocol 1.0, configuration 1.0, contract profile `generic-actor-match-2` |
| build | `nilbots build . --no-cache`, cache key `114a0fa2aa58f11944e2fec0549d4cb2ed35731fecd7122221eb2fc9c7abebc2` (miss/compiled) |
| **bot.wasm sha256** | **`b0d74dafaf6aff9c8dc01876447c913513937e3c3b125e2675d147a1bc09bb8b`** |
| qualification | `evidence/t4/qualification.json`, sha256 `d209cab82ed7cc99e15de40ae01db0525257e016d46a78cf0d4d15c295f1c851`, exit code **0**, tier **T4**, `balanceEvidenceEligible: true`, prerequisite `frontline-qualification-4` PASS |
| source-tree digest | `71f1362f0694f65e14a157688415c8184e99ce0be6d31a6224f15b50c0ce029d` (sha256 of the sorted per-file digest list below) |

Submitted sources (sha256):

| file | lines | sha256 |
| --- | --- | --- |
| `GateStone.cs` | 576 | `20147e909413cd3050b3cc3d0e49371a2fe23fa7814ed7531998438da918ad03` |
| `StoneContract.cs` | 457 | `19412db71e36d3a79ed899f27b50c00a90ecdd0c4385c5b0d15d32bb2f223d07` |
| `StoneGround.cs` | 903 | `54baee08e84c4ad67d469fffc925b8b19693f827fe4c3c40db9bae31a4db8d63` |
| `StoneAim.cs` | 494 | `708bfbdd47647069243b871594f61666e66ed9c49a99a95d842c7f5539cd0a85` |
| `StoneMemory.cs` | 186 | `d909d10832ff8426ec98514262994be2cf8de2fd6b615454da8aae4ec52958d2` |
| `ArenaBasics.cs` | 1205 | `0f6cde2c1ba950ff69d6f41fec436ab762fddabf04306afebfe640dce40c8d74` (scaffold, unmodified) |
| `GateStone.csproj` | — | `8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573` |
| `botarena.json` | — | `d94f19d152b951c206aab17f23589b59c00c5ab13364b91f592bbe7fca38071d` |

Evidence layout: `evidence/t4/` (suite-5 report + 36 probe replays, including the
hash-linked T3 and T2 prerequisites), `evidence/arms/{keel,helm,veer,rig}/` (one
WASM match per arm, all four `nilbots verify` OK). Every `replay.json` is stored
gzipped (`replay.json.gz`) — the uncompressed set was 289 MB, the gzipped set is
1.6 MB and the same bytes; `gunzip -k` before `verify`. Self-contained
`viewer.html` files were deleted (10 MB each, reproducible from the replay).

## Doctrine, in one paragraph

GateStone is a bulwark whose identity is the *gate*, not the shield strapped to
it: it reads the capture policy and spends bodies by arithmetic rather than by
instinct. While its **net objective weight is positive** the claim already
builds, so the marginal body leaves the objective for a *shoulder* tile — one of
the few tiles the contract permits a stance on — where it keeps a firing lane
onto the same ground and raises the aegis arc during the two ticks in three that
a cooldown-3 gun is idle; the moment net weight reaches zero or goes negative,
every point of weight outranks any amount of fire and that body walks back onto
the objective, because at net zero nobody scores and below zero the opponent is
capturing while we shoot at it. It picks the objective tile with the fewest
enemy firing lanes into it, faces the bearing its own gun can actually reach
(with the initial aim offset pinned to zero, the turn *is* the aim), uses the
universal bend only when the arrival tile is certain — a bend that lands inside
an enemy guard's quadrant is a bolt returned with our name on it, so an arc is
fed only when the contact is the third and shatters it — and treats every
deflected bolt as the ordinary hostile projectile it is. It prices the published
hold clock instead of guessing: inside an enemy hold a completed capture is
spent, so with a claim one tick from the threshold and nobody contesting it
steps *off* its own objective (this decay clock preserves a claim on empty
ground) and returns when the hold lapses. It anchors into a turret only behind
two remaining bodies of weight or inside its own live hold, because
objective-weight zero means fortifying subtracts the very pressure it is trying
to protect.

## Measured per-arm records

Sparring baseline: the scaffold starter, class-declared bulwark, rebuilt
`--no-cache`. Every cell is `--movement facing-locked`; six games per arm
(seeds 104729 / 130363 / 155921 × both sides, mirrored accounting).

| arm | registered token | flags added to `--movement facing-locked` | GateStone vs baseline | breach ticks (side A / side B) | mirror self-play |
| --- | --- | --- | --- | --- | --- |
| kit off, bend striker-only | `keel` | `--pendulum keel` | **6–0–0** | 265 / 383 | 3–3–0 |
| kit ON, bend striker-only | `helm` | `--pendulum keel --skills kit` | **6–0–0** | 265 / 472 | 3–3–0 |
| kit off, bend universal | `veer` | `--pendulum keel --bend universal` | **6–0–0** | 177 / 171 | 3–3–0 |
| kit ON, bend universal | `rig` | `--pendulum keel --skills kit --bend universal` | **6–0–0** | 177 / 171 | 3–3–0 |

Every win is a base breach, never a tick-cap decision. The mirror is 3–3 in all
four arms with byte-symmetric per-team statistics, i.e. the residual is pure
side/seed noise, which is what mirrored accounting is supposed to leave.

One WASM confirmation per arm (`--runtime wasm`, seed 104729, the frozen
artifact against the baseline's frozen artifact) reproduced the in-process
result in every arm and verifies:

| arm | result | replay hash |
| --- | --- | --- |
| `keel` | breach, tick 265 | `3682a2c1238fe83c8677357573608752182ae6a26258551fb3aee309c88880fa` |
| `helm` | breach, tick 265 | `3a9a667765fd61d871a00d514b0a3de97c5e7312d7276b0e54b971436d521caa` |
| `veer` | breach, tick 177 | `0ebc0c5b1fd852a75c9b9969b2f324b20d0d062ca54eda4a6e35fdd305514e00` |
| `rig`  | breach, tick 177 | `6dde1a542cb1ad255644a6c02fbff385f7aaad6890f1d34fc86eb8b4be6c17c1` |

### Skill usage, per arm (6 games vs the baseline)

| arm | volleys cast | shells raised | shells broken | turrets anchored | slots fielded | bends fired | kills / deaths |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `keel` | 0 (absent) | 0 (absent) | 0 (absent) | 3 | 3 | 0 (absent) | 48 / 15 |
| `helm` | 0 (never in a bulwark cell) | **54** | 0 | 0 | 3 | 0 (absent) | 60 / 21 |
| `veer` | 0 (absent) | 0 (absent) | 0 (absent) | 0 | 2 | **33** | 30 / 12 |
| `rig`  | 0 (never in a bulwark cell) | 0 | 0 | 0 | 2 | **33** | 30 / 12 |

`rig` raises no shell against the baseline only because the bend ends those
games in ~175 ticks, before a second body is ever contested on a shoulder. In
the contested mirror the same artifact raises them freely: over the 12 mirrored
`helm`+`rig` games, **66 stance entries and 24 deflections** — bolts genuinely
turned and returned at the shooter. **Volleys cast is structurally 0**: the
volley is striker-owned, so `--skills kit` in any bulwark cell resolves to the
aegis shell alone (the CLI says so: "requested skills without an owning class in
this cell change no contract bytes and are dropped").

## Top 3 frictions

**1. The shell is illegal on all the ground the game is played on, and the
class doc implies the opposite.** `EXPERIMENTAL-FRONTLINE-CLASSES.md` sells the
aegis shell with "objective weight stays **1**, so it still holds ground" — which
reads as *park a shield on the objective*. On `frontline-labs-01-classes` that is
unreachable: the `anchor-forbidden` tile tag (`transition-placement-forbidden`)
covers **every tile of all five objectives and the entire central corridor
row y=7, x=1..21**, and `shell-bulwark-*` declares that tag in
`placement.forbiddenTileTags`. So the shield can only be raised on the
*shoulders* beside a gate, never on it. Measured in a fast game: 9 of 204
decisions on stance-legal ground. My first doctrine draft was built on the
documented promise and simply never raised the shield; the whole
holder-plus-shoulder split exists to work around a sentence that the map
contradicts. The prose and the tag set should be reconciled — either the
objective tiles stop forbidding stances, or the doc stops advertising a
ground-holding shield.

**2. A windup-1 stance cannot be a reaction, and nothing says so.** Entry
completes `end-of-started-tick-plus-duration-minus-one-after-mode-update` — after
combat — while a bulwark sees 4 tiles and a bolt crosses 2 tiles per tick after a
1-tile launch. Over 600 mirrored decisions on stance-legal ground, **every single
hostile bolt was observed with exactly one tick to live**, so no shield ever went
up in time to meet a bolt already in the air. The shell is therefore purely
pre-emptive, and its advertised 3-deflection budget is close to unreachable:
across 66 observed stance entries the maximum was **1 deflection**, and **zero**
entries reached the forced break. The kit spends a lot of contract surface —
`automaticReturn`, a `projectiles-deflected-since-entering-source-form` counter,
a documented punish window — on a threshold the class's own vision range makes
almost impossible to hit. A vision-4 chassis needs either a 0-tick stance or a
lower threshold for that budget to mean anything.

**3. A bulwark cannot shoot a diagonally adjacent body, in either bend arm.**
Under `--bend universal` the bulwark envelope is
`minInitialAimSteps = maxInitialAimSteps = 0` with `minBendAfterTiles = 1`, so
the first tile of every bolt is straight ahead and no legal program reaches a
tile at Chebyshev distance 1 off the facing axis. Combined with `facing-locked`
movement — where turning costs the whole tick — a body that steps diagonally
adjacent is briefly immune, which is a strange place for a defensive chassis to
have a hole. The doc's "every class's mobile gun bends once, at its own depth"
gives no hint that the *aim* offset is pinned to zero for the classes that gain
the grammar; I only learned it by dumping `shotProgram` from my own replay's
`header.contract`, which the scaffold README rightly calls the authoritative
move.

### Smaller notes

- **Scaffold drift:** `ArenaBasics.ClassOf` still recovers a class by splitting
  a form ID on `-`, and its doc-comment says a typed `classId` "replaces this
  helper's body in a later contract generation" — but that generation has
  shipped: `classId` is on `Self`/`Allies`/`Enemies`/`Participants` and on
  `Topology.Teams`. A first-time author who follows the helper is doing exactly
  what the brief forbids. I never called it.
- **The invisible wall.** A slot's authored return anchor is reserved against
  its own team's children for the whole match. Nothing in the legality mask says
  so — `move` stays *Available* and the move simply resolves `Blocked` forever.
  My T2 `automatic-life-cycle` failure was precisely this: an automatic child
  spent 19 ticks walking east into the prime's spawn tile. The fix was to read
  `ObservedTile.SpawnReservation` (permanent `AutomaticReturn` claims for
  another slot are impassable) plus a two-strikes rule that promotes any tile
  which refuses this body twice to a wall. The observation carries the fact
  perfectly; it just isn't where an author looks first.
- **Terminology:** "hold" is three things in one cell — the *ratchet hold*
  (`holdEndsAtTick`), a body *holding* an objective, and `--pendulum` tokens
  spelled like nouns. My code says `Push.Hold` for the ratchet and
  `Station` for the tile, and I still misread my own log lines twice.
- **Hardcoding temptations resisted:** the 15-progress / 5-tick-pause /
  3-advance-breach numbers, the 120/260 unlock ticks, three unit slots, the
  literal objective coordinates in the rule card, "children anchor and primes
  do not" (derived instead from `windup.durationTicks > 1`), and "the shell is
  the bulwark skill" (derived from `projectileGuard` on the *target form* of a
  same-life route, so the same code finds any future guard).
- **Times:** in-process build ≈ 0.7 s, controlled `--no-cache` WASM build
  ≈ 3 min (Docker platform-matched builder on macOS), suite-5 qualification in
  WASM ≈ 7 s wall for 36 probe replays across three hash-linked tiers, one
  500-tick in-process match ≈ 3 s, one WASM match ≈ 25 s.

## Repairs and strategy passes (the improvement budget, itemised)

Every row is a measured A/B over the same six mirrored games; the loser was
reverted.

| # | change | measurement | kept |
| --- | --- | --- | --- |
| 1 | anchor behind 1 remaining body vs behind 2 | 1–5 → **6–0** | yes |
| 2 | prefer stance-legal ground in the *router* | 365–494 ticks → **170** without it (near-tied routes flip on the per-life lateral coin and the body dithers) | no |
| 3 | detour around transient occupants vs refuse to move | 326 → **170** | neither: soft cost |
| 4 | occupied tiles as walls vs soft cost 6 | as walls, a holder in the single-file objective mouth blocks its own reinforcement and the team fields 1 weight against 2 → 0–6 in both straight-only arms; soft cost → **6–0** | soft cost |
| 5 | second body always on the objective vs always on a shoulder | shoulder beat boots **3–0–3** head-to-head in the bend arms, lost **0–6** in the straight-only arms | neither: the net-weight test |
| 6 | net-weight test: pile on when net ≤ 0, shoulder when net > 0 | **6–0 in all four arms** | yes |
| 7 | shell reactively (bolt inbound) vs pre-emptively (enemy in arc, gun cold) | reactive never fires (finding 2); pre-emptive without an on-station gate cost tempo (3–3), gated on-station **6–0** | pre-emptive, gated |
| 8 | stationary-enemy confidence + gentler flight-time decay | fixed `suppression-choke` and `prediction-chamber` (T4) | yes |
| 9 | suppress at any live chance when parked | fixed `suppression-choke`; needed a curved-shot floor so a bend is never speculative (fixed `strict-corner`, T3) | yes |
| 10 | presence-preserving dodge (step to another station tile even on ground we hold) | fixed `prediction-chamber` (T4) | yes |

Qualification history: T4 attempt 1 exit 3 (T1) — `suppression-choke`,
`prediction-chamber`, `strict-corner`, `automatic-life-cycle` failing;
attempt 2 exit 3 (T1) — all five T4 probes passing, `strict-corner` and
`automatic-life-cycle` still failing in the prerequisites; attempt 3 exit **0**,
**T4**, `balanceEvidenceEligible: true`.
