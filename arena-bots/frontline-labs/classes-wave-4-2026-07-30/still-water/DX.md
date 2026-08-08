# DX notes — Still Water, revision 4

## Isolation

Written from this entrant's own authoring session, its own sparring replays
against its own rebuilt predecessor and its own scratch variants, and its own
qualification report. No other entrant's source, standings, replays, or
aggregate balance report was opened in this revision. Work was confined to
`arena-bots/frontline-labs/classes-wave-4-2026-07-30/still-water` plus the
private scratch directory `sandbox/still-water-w4-3e7b91d5`, which is uniquely
named and was created by this session. All three frozen predecessor directories
(`classes-wave-1-2026-07-29/still-water`,
`classes-wave-1-revision-2-2026-07-29/still-water`,
`classes-wave-1-revision-3-2026-07-29/still-water`) were read but not modified;
the revision-3 rebuild used for sparring is a *copy* of that source inside the
private scratch directory. Nothing was committed to git.

Permitted material only: the author packet, the Labs rule card, the class
addendum (read in full), `templates/botarena-generic-actor/`,
`src/BotArena.Sdk/` types and XML documentation, this lineage's own directories
and replays, and `sandbox/cli-publish/`. The three doc hashes named in the brief
were verified before reading.

**Carried forward from v1, still disclosed:** during the first authoring pass a
shared scratchpad directory name (`mirror1`) collided with another agent's run
and aggregate statistics from one `fabricator-vs-fabricator` replay that was not
mine were read before I noticed. No source, standings, doctrine, or striker
material was seen, and nothing from it influenced any revision. The disclosure
is repeated here so the record stays with the lineage rather than only with the
frozen v1 directory. No new exposure occurred in this revision.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `still-water` |
| Authoring lineage | `still-water-v1` |
| Revision | 4 (one budgeted strategic pass — the class-skill kit; mechanical/contract repairs free) |
| Class | `striker` (declared in `botarena.json`) |
| Role | `verdict-doctrine` |
| Doctrine | patient interceptor, now pricing the adopted class-skill kit on the keel pendulum |
| Primary cell | `--pendulum keel --skills kit --bend universal --movement facing-locked` → `frontline-labs-1-striker-vs-striker-rig-facing-locked` |
| Target tier | cumulative T4 (`frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`) |
| Predecessor | `arena-bots/frontline-labs/classes-wave-1-revision-3-2026-07-29/still-water` (frozen, untouched) |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rule card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `b91047df0c0c3e643fd627f45e9f82a0b60b593f986011107125f6ca28c99518` (r3 froze against `fcd6358d…`; the skills, hold-observation and phase-2 sections are new) |
| Starter helper synced | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` (vendored byte-identical; it gained `LiveHold` and `Threat`) |

| Artifact | Value |
| --- | --- |
| `out/bot.wasm` sha256 | `8ae62751f9f8f6f854d5ed7efd90fad52cdf02d6e0fdcb55ed042a16d8c5547c` |
| `out/bot.wasm` size | 3,351,535 bytes |
| Deterministic source-tree hash | `cf3a99f80e54a9a522fb6cbd050802e319ea74e56539e91e7677b9572862a0c5` (sha256 of the sorted per-file sha256 listing of all `.cs` + `.csproj` + `botarena.json`, excluding `bin/` and `obj/`) |
| `evidence/t4/qualification.json` sha256 | `87969cdeb0773a1b13ce666e17bc84707ff1834e92231495916768fbf77c68c6` |
| `evidence/t4/prerequisite-t3/qualification.json` sha256 | `2ef3ee53a6d0328515b9c78c0f9d7deaf43ba699fc01880ab6e79adf8fb00302` |
| `evidence/t4/prerequisite-t3/prerequisite-t2/qualification.json` sha256 | `d3cbf9842265fafd7a1474c8a61171ea9e259dbc3dab19c7e08f8d0f903946eb` |
| `evidence/doctrine-rig/replay.json` replay-v3 hash | `dfc6748645ff80ec35c909f440f5c8b90f7e24cd5fbeb113cc4516440879f06a` (`nilbots verify` OK — the primary-cell match, seed 104729) |
| Verified probe replays | 37 of 37 `nilbots verify` OK (36 qualification-chain replays plus the primary-cell doctrine replay) |
| Build reproducibility | a second `nilbots build --no-cache` from the frozen source returns the same `8ae62751f9f8…` |
| Sparring baseline | revision-3 source rebuilt on the current toolchain, artifact `8b5d838abe6876755714058ca0022b61f2a1214d37db196b4787c5410890a027` |
| Toolchain | controlled `nilbots build --no-cache`, CLI 0.9.15, SDK 0.10.6, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, game rules 0.5, WASI p1 core module, macOS host via the platform-matched Docker builder |

Per-file source hashes at freeze:

```
04dbf8d8f5b4cd77514e51bf18a6e886da93105256841db96079e085f7da25c6  ActionBook.cs
567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627  ArenaBasics.cs
f55f52a19b4953ad3a54d03231f5e4ebb9274dcab127ffe61474909185844a85  Doctrine.cs
df05fd11c3f1efa2dc032eebc9a11f65478aee5dc61e3447d2115935dedb12a6  Field.cs
e0c4c46870c238900ac0b15aac95a1dd511ccf4ee1419c8888eb3d6217218675  ForkPlanner.cs
9ca3977cda49df593729d2d78d93c4af0c60ebddfd1e9223817aeb5060f6816e  Quarry.cs
a62cce328cfd13051802869d54f0396fa59cea5df26e96b51fff48181653bf3a  Ratchet.cs
fedcf5362b599f226548bc01b8e54e526aebe90d4f944f97babb46c72d3911db  Stance.cs
5c996e2caf813c198cf9ec1bccd3ca76539049ad9b6968c30d1f81d8d669d8d2  StillWater.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  StillWater.csproj
7f720cc3f565f712a3916fd5d757d8c69b3363b0955163b7e0fd2554c161f2cb  ThreatField.cs
0ed9bc7973a0d815aad0b89434726c54caa4b3ed9c88ddb812462fca2e087e6f  botarena.json
```

`Field.cs`, `ForkPlanner.cs` and `StillWater.csproj` are byte-identical to the r3
freeze. `ArenaBasics.cs` is byte-identical to the current template. `Stance.cs`
is new. `botarena.json` differs from r3 only in `sdkVersion` (0.10.4 → 0.10.6).

**The r3 artifact had to be rebuilt to spar at all**, and this was verified
rather than assumed: the frozen r3 `bot.wasm` (`fcf6b4a6…`) loses at tick 0 on
the primary cell with `fault-eligibility` and
`WASM generic actor exited before its life ended (peak completed tick fuel
0.0M/200.0M)`. Rebuilding the untouched r3 source produced
`8b5d838abe6876755714058ca0022b61f2a1214d37db196b4787c5410890a027`, and that
artifact — same source, current toolchain — is the baseline every number below is
measured against.

## Doctrine in one paragraph

Still Water refuses the closing duel: it stands one bend's reach behind the
contested point, puts the gun across the approach, and makes the other side
spend tiles and tempo coming to it, taking the ground last but never later than
the clock can still pay for. Revision 4 prices the class-skill kit against that
doctrine and reaches an asymmetric verdict. The volley is read from the contract
as what it is — several *simultaneous straight* bolts, so it can reach nothing a
bend could have curved onto and never concentrates damage, its only edge being
breadth at one arrival tick — and it is therefore cast only to answer two
separate bodies with one decision or to seal the front rank of a contested
point, never as a bigger gun, never at one health, never under a gun already
pointed at the tile, and never from the point itself, which the map's
transition-placement tags forbid outright. The other half of the kit's doctrine
— that envelopment from multiple bearings beats fans and shell arcs, while
clumping in a lane feeds them — is where the revision actually won: a candidate
tile is now charged for every ally that shares an enemy cone with it, whether
that cone is a volley fan, a guarded quadrant, or an ordinary aimed lane. Around
those two rules sit the repairs that stopped this bot guessing: the territory
hold is read from `holdOwnerTeamId`/`holdEndsAtTick` instead of reconstructed,
bolt timing and damage come off each bolt's own published cadence, a body's
mobility is read from its form's action mask rather than its objective weight
(a stance holds ground and cannot walk), and both sides' form catalogues come
from their own declared lifecycle assignments instead of a subtraction that a
mirror made meaningless.

## Qualification outcome

`nilbots experiment frontline-labs qualify --bot out/bot.wasm --suite
frontline-qualification-5 --out evidence/t4` exits **0**. **Tier awarded: T4**,
`passed: true`, `profileComplete: true`, `balanceEvidenceEligible: true`, profile
`frontline-duel-depth-union-t4-v1`, contract fingerprint
`2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb`. Every
component passed on the first attempt; zero qualification cycles were spent on
repairs, as in r3.

| Level | Component | v1 | v2 | v3 | v4 |
| --- | --- | --- | --- | --- | --- |
| T4 | suppression-choke | PASS | PASS | PASS | PASS |
| T4 | entry-initiative | PASS | PASS | PASS | PASS |
| T4 | prediction-chamber | PASS | PASS | PASS | PASS |
| T4 | front-rotation | PASS | PASS | PASS | PASS |
| T4 | map-holdout (thin-fronts) | PASS | PASS | PASS | PASS |
| T3 | wall-terminated-bend | PASS | PASS | PASS | PASS |
| T3 | strict-corner | **FAIL** | PASS | PASS | PASS |
| T3 | cadence-parity | PASS | PASS | PASS | PASS |
| T3 | cooldown-window | PASS | PASS | PASS | PASS |
| T3 | local-form-safety | PASS | PASS | PASS | PASS |
| T2 | contract-matrix | PASS | PASS | PASS | PASS |
| T2 | automatic-life-cycle | PASS | PASS | PASS | PASS |
| T2 | objective-path | PASS | PASS | PASS | PASS |
| T2 | direct-fire | PASS | PASS | PASS | PASS |
| T2 | straight-evade | **FAIL** | PASS | PASS | PASS |
| T2 | manual-fabrication | PASS | PASS | PASS | PASS |

Zero runtime faults and zero non-`success`/`blocked` resolutions across the whole
qualification chain and all 120 measured sparring matches. Every new branch keys
off a **declared value** rather than a derived ratio, so all of it compiles out
at runtime on the qualification profile: no volley profile means no stance route
means `BestFan` is null and the cast ledger never runs; no `projectileGuard`
means the deflection branches never run; `holdOwnerTeamId` is null throughout,
which the code treats as the real answer it is.

## Measured per-arm records versus the rebuilt predecessor

Fifteen seeds (104729, 130363, 155921, 179424, 224737, 271441, 314159, 360287,
411083, 479001, 520493, 563623, 601147, 644027, 700001), both sides, WASM
runtime, every cell `--movement facing-locked --pendulum keel`, opponent = r3
source rebuilt on the current toolchain. **120 matches, 120–0–0.**

| Arm | Registered token | Record | Mean territory | Δ vs mirror control | Ticks | Breaches |
| --- | --- | --- | --- | --- | --- | --- |
| `keel` | keel | **30–0–0** | **+20.8** | **+20.8** | 500.0 | 0 |
| `keel --skills kit` | helm | **30–0–0** | **+20.8** | **+20.8** | 500.0 | 0 |
| `keel --bend universal` | veer | **30–0–0** | **+20.8** | **+20.8** | 500.0 | 0 |
| `keel --skills kit --bend universal` | rig | **30–0–0** | **+20.8** | **+20.8** | 500.0 | 0 |
| — | mirror control (r3 vs r3) | 60–60–0 | 0.0 | 0.0 | 497.1 | 2 |

Per-side means on the primary cell: +20.1 as team 0 (fifteen seeds), +21.4 as
team 1. Per-match territory ranges from +13 to +35; the modal result is +18,
which is one completed advance (threshold 15) plus a residual claim held to the
cap.

**Two of the four cells are the same ruleset, and it matters for reading this
table.** In a striker mirror `--bend universal` changes no contract bytes — the
striker already owns the 1–4 bend envelope — so `keel` and `veer` share the rules
fingerprint `4e7c714f…`, and `helm` and `rig` share `3666788559…`. The mirror
control confirms it empirically: r3-vs-r3 produces byte-identical results in all
four cells. So the bend factor is *structurally inert* for this class pair, and
the kit factor is inert *for this artifact* because the cast ledger declines
every opportunity (below). Reporting four independent cells here would be
reporting two cells twice, and this table says so instead.

### The mirror control is the only honest denominator

Running one artifact against **itself** on this arm is side-saturated per seed:
team 0 finishes −22, −30, +7, +26, −22 on the first five seeds, and `--swap`
reproduces each match byte for byte, because swapping two identical artifacts
changes nothing. A raw win/loss column against a predecessor therefore mostly
reports the seed's side bias — r3's own DX said this and it is sharper now that
the control is per-seed. Every number above is the **paired** delta: what this
artifact scored on a given (cell, side, seed) minus what the identical-bot mirror
scored for the same side on the same seed. For a mirror control that delta is
exactly zero by construction, which is why the control row reads 0.0.

## Skill usage, counted

| Quantity | Frozen artifact, 120 measured matches | Notes |
| --- | --- | --- |
| Volleys cast (`transform` into a stance) | **0** | 0 `shoot-straight`, 0 `mobilize`; the four cells are byte-identical as a result |
| Fans launched | 0 | — |
| Shells raised / broken | 0 / 0 | the striker owns no guard; see the fixture probe below |
| Slots fielded | 3 of 3 | prime plus automatic children at ticks 120 and 260; the striker arm declares **no** `fabricate` action, which is read from the mask rather than assumed |
| Bends fired | **526 of 2163 shots (24.3%)** per cell over 30 matches; 1637 straight | the bend envelope is 1–4 tiles, read from `shotProgram` |

The cast machinery is live, not dead code — it is the ledger that declines. The
pre-freeze variant that differed only in the cone weight cast **4 times per ten
`helm` matches**, of which one launched its fan (`automatic-threshold-return`
followed) and three were abandoned early through `mobilize`. Reading the legality
mask straight out of five `helm` replays of the frozen artifact (3539 own
decisions) explains why casting stops entirely once the cone rule disperses the
fight: `transform` is legal on 653 of those decisions (18%), an enemy is visible
on 1498 (42%), and **both
hold together on only 209 (5.9%)** — almost all of them on the two shoulder tiles
directly north of the centre objective. Inside that 5.9% the remaining ledger
conditions (cooldown at most the windup, two anchored bodies in one fan or one
body plus two sealed objective tiles, value above the displaced bolt) are never
all satisfied. Relaxing the two-body rule to one body changes nothing, measured: at the adopted
cone weight `eagercast` is byte-identical to the freeze on all three seed sets
(+21.3, +22.4, +18.6; 20–0–0 each).

## Ablations: every rule had to earn it, and the cone weight is a knife edge

All figures are Δ territory versus the identical-bot mirror on the same seeds and
sides, `keel` / `helm`, 20 matches per seed set.

| Variant | Seed set 1 | Seed set 2 | Seed set 3 | Verdict |
| --- | --- | --- | --- | --- |
| **adopted: shared-cone penalty 1.5** | **+21.3 / +21.3, 20–0–0** | **+22.4 / +22.4, 20–0–0** | **+18.6 / +18.6, 20–0–0** | kept |
| cone penalty 0.9 (first draft) | +3.1 / +0.7, 7–13–0 | −2.8 / −6.5, 6–13–1 | — | too weak to replicate |
| cone penalty 1.2 | +0.0 / −5.7, 6–14–0 | — | — | worse than no rule |
| cone penalty 2.0 | +12.4 / +14.1, 15–5–0 | +12.4 / +14.4, 20–0–0 | — | good, not best |
| cone penalty 2.5 and 3.5 | +12.4 / +14.1 | — | — | identical to 2.0 — saturated |
| no cone rule at all | −2.6 / −8.7, 7–12–1 | — | — | the rule is worth ≈ +24 |
| cast ledger disabled (at cone 0.9) | +3.1 / −1.8 | — | — | casting was worth +2.5 on `helm` |
| fan-station preference off (at cone 0.9) | +3.1 / +3.1 | — | — | the preference cost −4.9 |
| fan-station preference off (at cone 1.5) | identical to freeze | identical | identical | now inert; kept as the only route to a legal cast |
| eager cast: one body instead of two | identical to freeze | identical | identical | no effect; strict rule kept |

### The advantage does not transfer to the retreat-punishing map

`--duel-map thin-fronts` now composes with the whole phase-2 stack, which closes
a question r3 had to leave open. On the primary cell plus thin-fronts, five
seeds, both sides: **0–0–10, every match a 0–0 draw at the tick cap, and the
mirror control draws identically.** Neither this bot nor its predecessor takes a
single position there, and the cast ledger fires zero times. So the +20.8 on the
shipped map is a claim about *that* map: on the arm designed to raise the
positional cost of retreat, a doctrine built on conceding tiles to restore a band
converts its advantage into a stalemate rather than a loss. That is the most
important limitation in this report, and it is exactly the holdout the map arm
exists to expose.
| published hold off, r3 reconstruction on (at cone 0.9) | −0.9 / +2.6, 12–8–0 | — | — | see below |

Three of these deserve more than a row.

**The cone weight is not a law of the game, it is a tuning risk, and the
non-monotonicity proves it.** 0.9 is noise, 1.2 is worse than nothing, 1.5 wins
60 of 60 across three independent seed sets, and 2.0 and above saturate at a
smaller win. A coefficient whose response inverts between 1.2 and 1.5 is
deciding who wins a nearly deterministic mid-game scramble, not expressing a
smooth preference. I adopted 1.5 because it replicated three times, and I am
recording the shape of the response rather than the peak, because the next author
should expect the peak to move when anything else changes.

**The published hold is better on territory and worse on the win column, and
that is the side bias again.** Swapping the read for r3's four-signal
reconstruction gave 12–8 instead of 7–13 but moved mean Δ territory from +1.90 to
+0.85 over the same 20 matches — measured at the old cone weight, where every
number was noise-sized. Territory is the ranking channel at the cap, and the
reconstruction has a defect no measurement excuses: it cannot recover the hold's
*owner* for a life born inside the hold, because private memory is life-scoped.
The read is kept; the reconstruction survives only for a half-populated pair.

**The fan-station preference is the one rule that changed sign under another
rule.** Steering the station toward tiles where the stance is legal cost −4.9 at
cone 0.9 and is exactly inert at cone 1.5. It is kept because without it a cast
is not merely rare but geometrically impossible on this map, and because "inert"
is a measurement rather than a hope.

## Mechanical repairs (free budget)

1. **The hold is read, not reconstructed.** `ArenaBasics.LiveHold(context)`
   replaces r3's four-signal derivation, which is retained only for a contract
   that declares `ratchetHoldTicks` while publishing a half-populated pair. A
   published null pair is treated as "no hold binds this tick", not as "no
   information" — the distinction matters, because the reconstruction would
   otherwise happily invent a clock on top of a definite answer.
2. **Bolt timing and damage come off the bolt.** `ticksPerAdvance` and
   `damagePerHit` are published per projectile, and they have to be: a volley
   bolt, an ordinary bolt, and a bolt a shell has turned around need not agree
   on either. r3 borrowed the cadence of a guessed attack profile and assumed
   one damage. Danger now scales with the published damage, and a bolt's rest
   between advances is projected for the published cadence rather than one tick.
3. **Mobility is an action mask, not an objective weight.** r3 decided whether
   an enemy body could walk by asking whether its form carried objective weight.
   A stance carries weight 1 and cannot move, so r3 would have predicted a
   parked stance as if it were about to stroll away — and an immobile body is the
   most valuable prediction in the game to get right. A body inside a wait-only
   windup is treated the same way, and it is additionally the cheapest target the
   ruleset offers, because lethal damage cancels the transition.
4. **Both form catalogues are declared, so both are read.** r3 derived the
   opposition's forms by subtracting its own from the catalogue; on a mirror the
   remainder was empty and the code fell back to "assume the whole catalogue".
   Each team's lifecycle assignments are in the contract. Reading them is exact
   and is the difference between knowing the opposition has a deflecting stance
   and guessing that somebody might.
5. **Class comes from `ClassId`.** Self, allies, enemies, participants and
   `Topology.Teams` all carry it. Nothing splits a form ID.
6. **Longest *mobile* gun means a gun that can follow you.** The standoff band's
   input previously excluded turrets by objective weight; it now excludes
   anything whose form declares no movement action, which correctly excludes an
   immobile stance too.
7. Re-vendored the current template `ArenaBasics.cs` verbatim; `LiveHold` and
   `Threat` are used rather than reimplemented.

### The 72-tick self-inflicted wound, written down because it is instructive

The first build of the stance layer **lost by base breach at tick 111** with its
prime standing on one tile for 72 consecutive ticks, emitting
`stance aimed East` while wearing `striker-prime` — an ordinary mobile form.

The cause is one line of contract reading. A reversible stance is a *pair* of
`sameLifeTransitions`: `volley-striker-prime` (prime → stance) and
`unstance-striker-prime` (stance → prime). Both are `FormTransition`s, both have
`irreversibleForLife: false`, and each is the other's reverse. My route index
keyed "the stance you are wearing" by target form — so it recorded
`striker-prime` as a stance target too, and the mobile prime took the stance
ladder, which has no feet.

The discriminators that actually work are all indirect: the entry is the route
whose **action declares a form-target parameter**, the return is the route whose
**action is parameterless**, and no life is ever **created** already wearing a
stance. All three are readable, none is labelled. The fix took one build; finding
it took one match, because the replay records each decision's `debugMessage` and
a distribution of 72 `wait`s tagged `stance aimed East` named the bug outright.
That remains the single most useful debugging affordance in this toolchain, and
it is still documented nowhere.

## What could not be exercised, stated plainly

**The anti-shell logic is unexercised in play.** The guard rules — refuse a bolt
whose *arrival* heading lands inside a guarded quadrant, prefer a bend that swings
the last tile outside it, and deliberately feed the deflection that spends a
declared budget — are verified only by contract reading and construction. Nothing
in my permitted sparring set can raise a shell in anger: the isolation rules
allow only my own predecessor and my own variants, and my own doctrine is a
striker's, so it declines a stance with no gun. I built a scratch-only bulwark
fixture that force-raises the shell on its first legal tick; it does raise one
per match, but a fixture that parks its prime in an immobile stance at spawn has
deleted its own scoring presence and loses by breach at tick 108, and across four
such matches the striker fired five bolts into the shell and produced **zero**
`projectile-deflected` events, because the arc was never facing them. That is an
inconclusive probe, and I am reporting it as one rather than claiming the rule
works.

**Five slots was exercised, and it resolves as documented.** Playing my own
fabricator variant against the frozen striker gives topology profile
`two-team-one-controller-asymmetric-slots-5-3-v1`, five slots against three,
unlocks at 60/180/300/420, `fabricator-late-child-ready` on the last two, and 17
successful explicit `fabricate` actions from the mask. Zero faults, zero invalid
actions. The striker side never assumes three slots anywhere; it counts them for
both teams.

## Top three frictions this revision

1. **The map decides where a stance exists, and nothing says so.** The volley is
   the striker's one new verb, and the entry route carries
   `forbiddenTileTags: ["transition-placement-forbidden"]` evaluated at both
   queue time and completion. On `frontline-labs-01-classes` that tag covers
   **112 of 233 open tiles**, including every objective tile, both home regions,
   and the *entire* central lane at y = 7 — which is exactly where a standoff
   striker's station lands. So the volley can never be cast from the point it is
   meant to deny, and on the centre objective it can only be cast from three
   shoulder tiles. Measured on the frozen artifact, the window where the route is
   legal *and* an enemy is visible is 5.9% of ticks. The rule card states the tag
   rule for **Anchor** ("illegal on every contract-tagged transition-forbidden
   tile, including all objective and protected-pad tiles"), and the addendum's
   stance section — which documents the volley's windup, budget, bolt count,
   spread and cooldown in detail — never says the same tags bind a stance, so the
   inference has to be made across two documents and then checked against the
   map's own tag list to learn how much of the board it removes. One sentence in
   the skills table — "a stance obeys the transition-placement tags, so on the
   shipped map it is a shoulder verb and never a point verb" — would have saved an
   entire implementation pass, and it is a fact about how the mechanic is
   *designed*, not just about how it is coded.
2. **A reversible route pair does not say which route enters.** Both directions
   are `FormTransition`s with `irreversibleForLife: false`; "reversible" is
   symmetric and so is the pair. Recovering the direction needs three indirect
   reads (entry action takes a form-target payload, return action is
   parameterless, target is not a creation form), and getting it wrong does not
   fail loudly — it makes an ordinary mobile body believe it is already in a
   stance and stand still until it dies. Either a `role` on the route
   (`stance-entry` / `stance-return`) or one line in the addendum's "Reading it
   from the contract" list ("the entry is the route whose action carries a
   form-target payload; the return is the parameterless one") would close it.
   The addendum currently says "the return is the parameterless `mobilize`",
   which is the answer — but as a description of the return, not as the rule for
   telling the two apart.
3. **A class-owned skill cannot be sparred against inside the isolation rules.**
   Two of the kit's three skills belong to other classes, the shell is the
   largest new mechanic in the slate, and the only permitted opponents are my own
   predecessor and my own variants — all of which are strikers by doctrine.
   `frontline-labs` ships **no** built-in generic opponents at all (while
   `nilbots experiment frontline` ships five), and there is no way to script an
   opponent behaviour, so "make a bulwark hold a shell and walk into it" is not
   expressible. The result is that the most interesting counter-play in the wave
   — flank the arc, bend around it, or break it on purpose — ships untested. Two
   or three trivial built-in generic actors (`labs-shell-holder`,
   `labs-volley-caster`, `labs-five-slot-swarm`) would make the whole kit
   measurable by every author without touching anybody's competitive isolation.

### Still open from earlier revisions and still true

- The published CLI binary in `sandbox/cli-publish/` is named `botarena`, while
  every document, the brief, and its own `--help` output call it `nilbots`.
- `--print-candidate-contract` still emits the resolved *identity* — ruleset ID,
  fingerprints, map, topology profile — and nothing about the rules. When the
  assignment is "price the kit", the resolved `forms`, `attackProfiles` and
  `sameLifeTransitions` are the whole job, and the only way to see them is to run
  a match and dig `header.contract` out of the replay JSON.
- Pointing `--bot` at `out/bot.wasm` still silently drops the declared class and
  resolves the base contract. This bit me again while building the class
  variants: the WASM artifact is byte-identical across all three of my
  differently-classed manifests, because the class lives in the manifest and not
  in the artifact, so only a project path carries it.
- **Fixed since r3:** `--movement` now composes with `--duel-map`, and so does
  the whole phase-2 stack. That is a real improvement and it is what made the
  thin-fronts holdout above measurable at all.
- **New in its place:** a composed map does not appear in the ruleset ID. With
  and without `--duel-map thin-fronts`, `--pendulum keel --skills kit --bend
  universal --movement facing-locked` both return
  `frontline-labs-1-striker-vs-striker-rig-facing-locked`; only `mapId`,
  `mapFingerprint` and the aggregate `matchContractFingerprint` differ. Two
  genuinely different games therefore share one ruleset ID, which is exactly the
  failure mode the addendum's "a level is identified by what it composes, never
  by how you typed it" rule is meant to prevent — the map is composed and is not
  named. An archived result labelled only by ruleset ID is ambiguous.
- The decision `debugMessage` is preserved verbatim per actor turn in replay v3
  and is still documented nowhere. It found this revision's worst bug in one
  match.

### One measurement footgun worth publishing

`--swap` on two **identical** artifacts reproduces the same match byte for byte.
A mirror control therefore has N independent samples, not 2N, and a sweep that
counts both sides of a control as data will report its side bias as a result. The
control's per-seed team-0 territory on the first five seeds is −22, −30, +7, +26,
−22: three of five seeds favour team 1 outright. Any author reporting a bare
win/loss column against a predecessor on this arm is mostly reporting that.

## Timings (macOS, Docker builder, CLI 0.9.15)

| Step | Time |
| --- | --- |
| `dotnet build` (editing loop) | 0.5 s |
| `nilbots build --no-cache` (cold) | 9.5 s |
| One 500-tick WASM match | ≈1.3 s |
| One 15-seed cell (15 matches, batch `--seeds`) | 19.8 s |
| Full 4-cell × 2-side × 15-seed sweep (120 matches) | ≈2.7 min |
| `qualify --suite frontline-qualification-5` (full cumulative chain, WASM) | 5.8 s wall |
| Whole measurement programme (controls, 12 variants, 3 seed sets, the thin-fronts holdout, the class probes, the final sweep) | 798 matches |

## Hardcoding temptations resisted

- The volley's bolt count, spread, cooldown, windup, budget counter and
  threshold, the tile tags that refuse it, and the return route's action are all
  read. The number three appears nowhere: fan heading offsets are derived from
  the declared `projectileCount`, and a spread policy this code does not
  recognise degrades to the single aimed bolt, which is always right about at
  least one bolt.
- "Is this a stance?" is decided by declared parameter schemas and creation
  forms, not by a form-ID substring — see the 72-tick wound for what the lazy
  version costs.
- The cast's tempo comparison is built from `CastTempoTicks` (windup + firing
  tick + exit windup + the stance gun's cooldown) against the mobile gun's own
  cooldown, so it re-prices itself on any arm that retunes either.
- Slot counts are counted for both teams; unlock ticks come from lifecycle
  assignments. 120/260 and 60/180/300/420 are read, never written.
- Every pendulum fact is a declared policy value read through the template's own
  readers. Risk appetite is gated on booleans, never on ratios of contract
  numbers, precisely so a degenerate probe threshold cannot silently invert it.
- The guard test is computed from the bolt's arrival heading against the guard's
  observed facing, in sectors, with the quadrant width taken as the facing
  heading plus its two neighbours; the deflection budget comes from the return
  route's `automaticReturn`, and the count of deflections already made comes from
  observed `projectile-deflected` events.
- Equal-scoring directions are still broken by the contract's own front axis with
  the residual tie randomised per life. An absolute compass preference is a
  measured team-side bias on a mirror-symmetric map.
