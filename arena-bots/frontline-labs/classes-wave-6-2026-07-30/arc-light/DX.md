# arc-light — wave-6 DX and freeze record

Wave-6 Frontline Labs entrant. Class **striker**, doctrine
**interception-caster**. Revision of my own wave-5 lineage
(`arena-bots/frontline-labs/classes-wave-5-2026-07-30/arc-light/`), which was the
only other bot source read.

Wave game unchanged: **`deck`** — keel pendulum + the full skill kit + universal
one-bend + the tuned fabricator (`--five-slots wane`) + restored ±45° launch
offsets + open ground, all facing-locked.

```
--classes <pair> --movement facing-locked --pendulum keel --skills kit \
  --bend universal --aim offset --stance-ground open [--five-slots wane]
```

Assignment: an **IQ pass on multi-body coordination among my own units**, not a
doctrine redesign. Keep what wave 5 measured as winning; fix the coordination
layer.

## Isolation statement

Material read while authoring, and nothing else:

- `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`
- `docs/FRONTLINE-LABS-RULES.md`
- `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`
- `templates/botarena-generic-actor/` (the scaffold; `ArenaBasics.cs` is retained
  unmodified and used for its contract readers)
- `src/BotArena.Sdk/` public types and XML documentation (types only)
- my own wave-5 directory, its `DX.md`, and my own replays
- the CLI at `sandbox/cli-publish/` and its resolved contracts

No other entrant's source, standings, replays, DX notes, or aggregate report was
opened. No `docs/DECISIONS.md`, no `docs/BOT-QUALIFICATION-SUITE.md`, no other
`docs/DESIGN-*`/`FORENSICS` file, no Engine or App source. Private scratch was
`sandbox/arc-light-w6-scratch-9f3c2e71/`, a uniquely named directory used by
nothing else. **No accidental exposure to another author's material occurred.**
Nothing was committed to git.

Every sparring partner was built from permitted material only — my own wave-5
source rebuilt, and variants of *this* revision's source differing by exactly one
boolean. All were rebuilt with `build … --no-cache` against the current SDK; no
frozen wave-5 artifact was played.

| partner | what it is |
| --- | --- |
| `ArcW5` | wave-5 predecessor source, rebuilt, `"class": "striker"` |
| `ArcW5Fab` | the same wave-5 source, `"class": "fabricator"` |
| `ArcW5Bul` | the same wave-5 source, `"class": "bulwark"` |
| six ablations | *this* revision with exactly one coordination switch flipped |

All three rebuilt wave-5 partners produce artifact hash
`ea4d9b8a89d48b562bdf5b983962ce6f52fedbc44288a756136d43434e87f60a` — one source,
three chassis, because the class comes from the project manifest and not from the
WASM. Matches were run from project specs so declared classes bound each bot to
its class's canonical team side automatically.

**Freeze-integrity note (acted on, mid-wave, from an orchestrator warning).**
`nilbots build` globs every `.cs` under the project directory, so an archived
variant source anywhere inside the freeze tree would make the frozen tree fail to
rebuild with duplicate-member errors — silently, because a frozen tree is
normally never rebuilt. Every ablation source in this wave lived in the private
scratch directory, never under the freeze. Verified two ways: the freeze tree
contains exactly the twelve `.cs` files listed below and nothing else, and the
freeze was closed with **a fresh `--no-cache` build run from the frozen tree
itself**, which reproduced the shipped hash
`65fec6a5915d2bdcf6e7d517e89af3095cb79e4f3e4bd201fcf08e92d75e81af` and the same
cache key `0c2b4bb203c18507…`.

## Freeze identity

| item | value |
| --- | --- |
| artifact | `out/bot.wasm` |
| bot.wasm SHA-256 | `65fec6a5915d2bdcf6e7d517e89af3095cb79e4f3e4bd201fcf08e92d75e81af` |
| build | `nilbots build <project> --no-cache`, cold cache (key `0c2b4bb203c18507…`) |
| toolchain | nilbots **0.9.22**, SDK 0.10.6, game rules 0.5, NativeAOT-LLVM 10.0.0-rc.1.26306.1, Docker builder (macOS arm64 host) |
| entry | `ArcLight` (`botarena.json` `entryType`), declared `"class": "striker"` |
| qualification | `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, seed 104729, runtime wasm |
| qualification result | **T4**, `passed: true`, `balanceEvidenceEligible: true`, **exit code 0** |
| probes | `prerequisite T3`, `suppression-choke`, `entry-initiative`, `prediction-chamber`, `front-rotation`, `map-holdout` — all PASS, first run, no repairs needed |
| qualification report | `evidence/t4/qualification.json`, SHA-256 `bc0ef6b470cf11f859b8fb0cc36da67cfe512575f8bf6c369e4f9293d2053ab2` |
| T3 prerequisite | `frontline-qualification-4` (`frontline-duel-depth-union-t3-v1`) rerun and hash-linked, passed, report SHA-256 `62836edc18031e0d79b529bb4cd11ffc0055ca08dbc8f287f68d805bee371fc8` |
| source-tree hash | `aa2e0a42571b3986697e91aad7e93a7e93d789039ceb432ba098b584d521bfdc` (SHA-256 over the name+digest lines below, name-sorted) |

Per-file SHA-256:

| file | sha256 |
| --- | --- |
| `ArcBoard.cs` | `f6e665c994634a7d281358b8297da0f3c24ca74a2ea82b937a7a2b9978afca94` |
| `ArcFacts.cs` | `b1953aec7fbeb6604cf31489087c8dda9b206b68d7bf29da0c90730892dc7590` |
| `ArcGun.cs` | `b0b68b9fc15d7bf6eb9565ca83f084f1ac7a1fd06a64bc9d9c04c0f8a715e224` |
| `ArcKeel.cs` | `9e8bdf8cb47ee110c6515454666925e4692027591f18afbdff7719c4d9a735ed` |
| `ArcLight.cs` | `520f8f06078d589750d1a58b0c019b8e4ef0606fe6d6249bdbc1665fe44d5b85` |
| `ArcLight.csproj` | `d2288bda995372814943941c1fba7becbfe016f883691052f3f6f5c6d9e17ef6` |
| `ArcMemory.cs` | `8f8338426c6619ccab641ce7f36296fff252c2763c2229dc1c93e876216c5924` |
| `ArcMove.cs` | `12afc06f65c56c734baed8c60fbc93ef597a3a646d63407201274e2e79159f60` |
| `ArcRules.cs` | `8e845366b5cfb11e3ff5441a029dc402fcfcfd889212f7a631b49192c8f8569f` |
| `ArcStance.cs` | `0a2a7cfba7079059f637716058dc541482840c3288b6ffb19f9958a1c848cceb` |
| `ArcThreat.cs` | `bbb244d4c9d0c6ca0f12bc276d856656a18f21ccac3fafabcbbae19a4aa87cb5` |
| `ArcTraffic.cs` | `8b65e5ad262d2b54bd8a37f98b74e3f5a2d23820d2f7247aab5d34ecbc264e5f` |
| `ArenaBasics.cs` | `a198af0a28ace85ed9034a9a93d8e106f21a907681547ac7a65e9e21871ce773` (scaffold, unmodified) |
| `botarena.json` | `ada09877c60994dc6d799ae0b5d0864e10b1d18a07b29fdc6827c8a64805ba98` |

Four files carry the whole wave: `ArcRules.cs` and `ArcTraffic.cs` are new, and
`ArcMove.cs` / `ArcStance.cs` / `ArcKeel.cs` / `ArcLight.cs` are edited to consult
them. `ArcGun.cs`, `ArcThreat.cs`, `ArcBoard.cs` and `ArcMemory.cs` are
byte-identical to wave 5 — the doctrine did not move.

Resolved arm identities this one artifact played (all match the wave-5
fingerprints, so the game really is unchanged):

| pair | flags beyond the common set | rulesetId | rules fingerprint |
| --- | --- | --- | --- |
| `fabricator-vs-striker` | `--five-slots wane` | `frontline-labs-1-fabricator-vs-striker-deck-facing-locked` | `0922aa9320ffaa8e…` |
| `striker-vs-striker` | — | `frontline-labs-1-striker-vs-striker-sail-open-facing-locked` | `218b6f0611bb8ea6…` |
| `bulwark-vs-striker` | — | `frontline-labs-1-bulwark-vs-striker-sail-open-facing-locked` | `dbe453aa44da3228…` |

## Doctrine delta, in one paragraph

The doctrine is wave 5's, unchanged and deliberately so: aiming is still an
interception problem against a facing-locked target's ray, the gun is still
priced over its declared launch-offset envelope, a bend is still spent only on a
hard interception, the cast is still priced in **bodies** against
`ceil(cycle / cadence)` and still sits *below* an available aimed bolt, a loaded
enemy gun still refuses a windup as firmly as a bolt in flight, the live hold is
still read rather than inferred, and the supply form named by the fabrication
catalog is still the priority target. **What is new is that arc-light now prices
its OWN bodies with the same seriousness it already priced the enemy's.** Wave 5
read an enemy's gun envelope, an enemy's fan lanes and an enemy's reachable ray —
and then, on the swarm leg, walked two of its own bodies into the same tile on
the same tick 23.5 times a match and re-walked the same pair into the same tile
19.6 of those times, because the rules resolve a same-destination claim by
blocking *both* movers and nothing in the observation says why. The wave-6 layer
is one class, `ArcTraffic`, built on a single contract fact: a life never sees an
ally's current decision, but **every life receives the same frozen observation**,
so any function of that observation is a shared answer that independent instances
cannot disagree about — which is how a right-of-way rule becomes implementable in
a contract that forbids coordination from being stateful. Under a facing lock a
body that still has ground to cover needs exactly one tile next tick, the one it
faces, so a *committed route* is readable rather than negotiated; a written total
order decides who owns a contested tile; 1-tile corridors are derived from the
published wall grid, grouped into runs, and never used as a destination by
anything — least of all by a stance, which is immobile for its whole declared
cycle and therefore turns a doorway into a sealed reinforcement route. Cast
discipline is untouched: 0.53 casts per match, every one of them still paid for in
forecast bodies, and now also paid for in my own team's routes.

## The coordination rules I shipped, with measured attribution

Five switches, each `static readonly bool` in `ArcRules.cs` so that removing
exactly one is a one-line edit and therefore a real single-lever ablation. Two
measurement legs, both on the wave game, both against my own rebuilt wave-5
predecessor:

- **swarm leg** — `deck` `fabricator-vs-striker`, **32 seeds**, my striker
  measured, fixed opponent `ArcW5Fab` (the wave-5 doctrine on a fabricator);
- **striker mirror** — `sail-open`, 16 seeds × **both assignments** (32 matches),
  opponent `ArcW5`.

`selfTraffic` is blocked own-team ticks per match: a move that blocked because a
sibling claimed the same destination, swapped, followed a vacated tile, or simply
stood there. It is the number the owner was looking at.

### Swarm leg — `deck` `fabricator-vs-striker`, 32 seeds

| striker driver | record | prog | selfTraffic | choke casts | casts |
| --- | --- | --- | --- | --- | --- |
| **wave-5 predecessor** | **0-31-1** | **−22.78** | **23.47** | 0.03 | 0.81 |
| **wave-6 (frozen)** | **20-11-1** | **+6.47** | **0.56** | **0.00** | 0.53 |
| C1 `YieldPrecedence` off | 1-30-1 | −22.09 | 24.34 | 0.00 | 0.78 |
| C2 `ChokePrecedence` off | 19-13-0 | +3.72 | 0.50 | 0.00 | 0.50 |
| C3 `RallyTraffic` off | 20-11-1 | +6.47 | 0.56 | 0.00 | 0.53 |
| C4 `FanSpacing` off | 20-11-1 | +6.47 | 0.56 | 0.00 | 0.53 |
| C5a `CastPricesOwnPaths` off | 20-11-1 | +6.38 | 0.56 | **0.06** | 0.59 |

### Striker mirror — `sail-open`, 32 matches (both assignments)

| build | record | prog |
| --- | --- | --- |
| wave-5 vs wave-5 (baseline) | 0-0-16 on 16 seeds | 0.00 |
| **wave-6 (frozen)** | **27-4-1** | **+14.88** |
| C1 off | **0-0-32** | 0.00 |
| C2 off | 24-7-1 | +11.28 |
| C3 off | 27-4-1 | +14.88 |
| C4 off | 27-4-1 | +14.88 |
| C5a off | 27-4-1 | +14.88 |

### Per-rule verdicts

**C1 — yield precedence. KEPT; it is the wave.** Swarm leg **20-11-1 → 1-30-1**
and **+6.47 → −22.09**; mirror **27-4-1 → 0-0-32**; self-traffic loss **0.56 →
24.34** per match. The rule: no body of mine steps onto, or keeps standing on, a
tile a higher-precedence sibling's committed route needs this tick. Precedence is
a written total order computed from the shared observation — (1) a body frozen in
a transition windup outranks everything, because it cannot move and disputing its
tile is pointless; (2) a body already inside a 1-tile corridor outranks a body
outside one; (3) shorter remaining route to the active objective outranks longer;
(4) lower `(unitId, lifeId)` breaks the rest. Term (4) is what makes the order
total, and totality is the whole safety argument: without it "both yield" is just
a politer deadlock. Two implementation details earned their place. The refusal is
folded into the route search's **first-step blocker set** rather than applied as a
veto afterwards, so the router plans *around* the traffic instead of stalling in
front of it — a yield that becomes a wait is still a lost tick, and the point of
precedence is that only one body loses one. And in `Escape` it is a **penalty**
(40) rather than a refusal, because colliding on an escape tick leaves the body
standing exactly where the bolt is going, but a yield must never be the reason a
body had no exit at all.

*The cleanest single number in this wave:* with C1 off, the mirror returns
**0-0-32 at 500 ticks with 18.00 sibling-ahead ticks, 36.0% fan co-exposure and
91.0 attacks per match — every figure identical to the wave-5-versus-wave-5
baseline.** C1 is the only rule that changes what arc-light *does* in a striker
mirror; the other four only bite in positions C1 creates.

**C2 — choke precedence. KEPT.** Mirror **27-4-1 / +14.88 → 24-7-1 / +11.28**
(three wins and 3.60 progress); swarm leg 20-11-1 / +6.47 → 19-13-0 / +3.72 (one
win, 2.75 progress). The rule: at most one of my bodies occupies or claims one
corridor run at a time, the body already inside goes first, and a choke tile is
never a destination in its own right — not a goal, not a cast tile, not a fortify
tile. Chokes are derived once per life from the published wall grid (an open tile
with at most two open cardinal neighbours which, when there are two, are
collinear) and flood-filled into runs; on this map that finds **16 runs of one or
two tiles**, six of them on the central row that every approach to the middle
objective crosses, including the `(13,7)–(14,7)` pair a team-1 striker walks
through between a forward rally and the centre. My own bodies spend **62.75
body-ticks per match standing on corridor tiles** (8.8% of all body-ticks), so
this is not a theoretical geometry — and across the instrumented set there was
**not one tick with two of my bodies inside the choke set**.

**C3 — rally and fabrication traffic. KEPT, and measured exactly INERT.** Both
legs are byte-identical with and without it: same records, same progress, same end
ticks, same every counter. Reported as inert rather than as a win, because that is
what it measured. The rule is nonetheless correct and I would keep it on a
different map: a pending fabrication or replication publishes
`LifecyclePending.ReservedPosition`, and a forward rally fills the own-side
chain-adjacent objective **rear-most-first along this team's own advance
direction** — so the tile the next arrival will take is derivable from the slot
clocks in `TeamUnits` plus the objective chain, and standing on it does not merely
crowd the arrival: the fill order moves on, and when the region runs out the
contract falls back to the assigned home anchor at the far end of the map. I
verified the derivation against four observed arrivals before writing the rule
(active 2 → `(16,5)`, active 1 → `(12,7)` for team 1; active 1 → `(3,8)` for team
0 — the derivation predicted all four). **Why it is inert here is structural and
worth passing on:** the rally region is the objective one step *behind* the fight,
and a striker whose whole doctrine is presence on the *active* objective is almost
never standing in it. The placement influence is real, published and correctly
read; on this map and this arm it is simply not contested.

**C4 — spacing. KEPT as a last-resort tiebreak, and this is the wave's clearest
lesson about pricing.** As shipped it is inert on both legs. The version I wrote
first was not: co-exposure weighted into lane values (−6 per shared body) in the
kite and unmask searches, −8 in escape scoring, and placed *above* ally clearance
in goal precedence. That form measured **22-10-0 / +11.34** in the mirror against
**27-4-1 / +16.28** for the same build with C4 removed outright — five wins for a
co-exposure rate that only fell from 33.7% to 27.4%. The brief's own wording is
what fixed it: separate two bodies out of one fan pose *"when an equal-value
adjacent pose exists"* — that is a **tiebreak**, and I had built a **cost**.
Demoted to the last sort key before the canonical order, it satisfies the bar and
spends nothing. One honest caveat on the metric: my replay-side co-exposure
counter assumes any enemy could throw a 3-ray fan, while the bot's rule reads the
contract and correctly finds that a *fabricator* has neither a volley profile nor
a route into one — so C4 is structurally inert on the swarm leg for a good reason,
and the 20.8% figure there is an artifact of my measurement, not of the bot.

**C5a — the cast never seals a doorway. KEPT; this is the gap the brief named.**
Swarm leg: choke casts **0.06 → 0.00** per match with records identical
(20-11-1 both ways) and progress +6.38 → +6.47. It removes the owner-visible
silliness without losing, which is exactly the bar. A stance is immobile for its
whole declared cycle, so entering one converts the body into **terrain** for that
many ticks; wave 5 priced the cast against the enemy's bodies (`FanForecast`),
against the enemy's loaded guns (`Bearing`) and against its own tempo
(`RequiredFanHits`), and never once against its own team's routes — and on an
open-ground arm that omission has a specific shape, because the fan may now rise
*anywhere*, doorways included. The same gate is applied to `TryFortify` (a turret
is the longest commitment in the contract) and to `TryGuard` (a shell's budget is
deflections, not ticks, so there is no upper bound on how long it stands there),
and to `CastBearing`, because buying a bearing for a cast the gate will refuse
costs a whole tick under a facing lock.

**C5b — the cast also yields a sibling's route and arrival tile. SHIPPED OFF.**
The obvious generalisation of C5a, and a straight loss: 32 seeds on the swarm leg,
one flag apart, **20-11-1 / +6.47** with C5a alone against **18-13-1 / +3.47**
with C5a and C5b together — two games and three progress — while C5a alone already
drives choke casts to exactly zero, so C5b bought no silliness reduction to trade.
Mirror records were identical either way (27-4-1). The reason, once measured, is
obvious: a cast is rare (0.53/match) and C5b refuses tiles that merely *lie on* a
sibling's two-tile ray, which on a four-tile objective is most of the objective.
C5a refuses tiles that *are* the route. It is left in the source as `false` with
the numbers in its doc comment, so the negative result stays auditable instead of
becoming folklore.

### Records versus the rebuilt wave-5 predecessor, summarised

| leg | wave-5 predecessor | wave-6 (frozen) |
| --- | --- | --- |
| `deck` `fabricator-vs-striker`, 32 seeds | **0-31-1**, prog −22.78 | **20-11-1**, prog +6.47 |
| `sail-open` striker mirror, 32 matches | 0-0-16 per assignment (mirror draw) | **27-4-1**, prog +14.88 |
| `sail-open` `bulwark-vs-striker`, 16 seeds | 16-0-0, prog +30.00 | 16-0-0, prog +30.00 |

Zero runtime faults and zero disqualifications across every match measured for
this freeze — 448 matches across 21 sweeps, verified programmatically.

The swarm leg is the headline: the wave-5 predecessor, measured against this
harder fixed opponent (its own doctrine on a fabricator chassis, rather than
wave 4's), loses 31 of 32 and gets its base breached. The wave-6 build wins 20 of
32 on the same seeds against the same artifact. The striker mirror is the cleanest
A/B available — same class, same contract, one wave apart — and wave 5 against
itself is a perfect 0-0-16 symmetric draw on every one of 16 seeds (identical
artifacts, so the assignment does not matter), which is precisely why 27-4-1 is
attributable.

**The bulwark leg carries no signal and I am reporting it as such.** `ArcW5Bul`
loses 16-0-0 in a mean 68 ticks while issuing 4 attacks a match: my own wave-5
doctrine on a bulwark chassis under this arm **thrashes the aegis shell** — 14
`transform` and 14 `mobilize` in 68 ticks, standing one tile off the objective
while the opponent captures it. `TryGuard` sits above the objective-advance step
and has no presence gate, so any borne-upon tile becomes a shield instead of a
step. That is a real bug in code I inherited, it is single-body rather than
coordination, and it is out of this wave's scope; I am recording it here so the
next wave has it. It also means the bulwark leg cannot measure my coordination
rules — wave 5's own `3-13-0` on this leg was against the *wave-4* doctrine on a
bulwark, which was a functioning opponent.

## Skill, coordination and diagonal usage counts

Instrumented 4-match `deck` `fabricator-vs-striker` set, arc-light's own bodies
(`evidence/self-play/counts-final.txt`):

| quantity | per match |
| --- | --- |
| moves / rotates / waits / attacks | 253.8 / 235.2 / 166.2 / 55.5 |
| volleys cast (stance entries) | **0.5** — cast discipline unchanged from wave 5 |
| casts refused with a priced forecast (`payNsNrNgN`) | 38.0 wait ticks |
| casts refused because a bolt was in flight (`windup-bolt`) | 3.8 |
| wait ticks with no enemy visible at all (`no-route`) | 124.0 |
| shots with a programmed payload | 19.25 of 55.5 (35%) |
| — diagonal launches (initial aim offset ≠ 0) | 17.25 (31% of all attacks) |
| — aim-only diagonals, zero bends | 3.5 |
| — diagonal + one bend | 13.75 |
| straight + one bend | 2.0 |
| own body-ticks standing on a 1-tile corridor | **62.75** (8.8% of body-ticks) |
| ticks with two own bodies inside the choke set | **0** |
| explicit step-aside yields | 0.25 — rare, because the refusal side of C1 does the work |
| blocked own-team ticks (`selfTraffic`) | **0.56**, against **23.47** for wave 5 |

The `Note` field on a held-station wait reads `clear` on every sampled tick, which
is an ordering artifact worth writing down rather than a null result: a body on
station never reaches the traffic gates at all, because `ArcMove.Toward` returns
early when it is already standing on a goal. The yields therefore show up as an
*absence* — 0.56 blocked ticks instead of 23.47 — rather than as a logged event.

## Top 3 frictions

**1. A same-destination collision between two of your OWN bodies is the most
common self-inflicted failure in this contract, and it is the single least
diagnosable event in the whole observation schema.** The rules card is explicit
that "same-destination moves all block" and that "allied actors also block
movement", so the mechanic is documented. What is not available anywhere is *which
of those things happened to you*. A blocked move publishes
`outcome: "blocked"` and nothing else: no cause, no rival actor, no contested
tile, no event. So the three cases a bot must react to differently —
**(a)** a sibling claimed the same tile this tick, **(b)** a sibling was standing
there, **(c)** an enemy or a bolt was there — are indistinguishable at runtime,
and I could only tell them apart by reconstructing every actor's submitted
destination from the replay *offline*. When I did, the answer was that (a) is
essentially all of it: 23.47 blocked ticks per match, of which **19.59 were the
same pair re-attempting the same tile** — in the first replay I dissected, my own
prime and child alternating into `(6,7)` from `(5,7)` and `(6,6)`, and the
opposing fabricator doing the identical thing into `(4,7)` on a three-tick loop
that ran to the end of the match. Both sides of that game were running my
lineage, so I got to watch the bug from both ends. That is the "silly decision" the owner saw, it
cost the wave-5 build 31 losses out of 32 on the swarm leg, and a bot cannot
detect it from inside the game. **The affordance that is missing is one field**:
a blocking reason on `ActionResolution`, or a `move-blocked` observed event naming
the contested tile and the rival actor. This is the third wave in a row my DX has
asked for the same thing in a different costume — wave 4 wanted a legality entry
to name the constraint that refused it, wave 5 wanted it to name the constraint
that *permitted* it, and wave 6 wants a resolution to name the thing that beat
it. **A refusal should always name its cause.**

**2. Every coordination fact this wave needed was already published — but the
three that matter are published as *policy ID strings* whose semantics live in
prose, so a correct bot and a wrong bot look identical in the code.**
`lifecycle.automaticReturnPlacement` is the literal
`"own-side-chain-adjacent-objective-tile-in-team-advance-order-then-assigned-spawn"`.
That string is doing four jobs at once: which objective (own-side chain-adjacent,
i.e. `active − delta`, and note the *minus* — I got the sign wrong in my own
offline analyser first), which order within it (team advance order), what "then"
means (a fallback to the home anchor, which is the actual cost of blocking), and
implicitly that the fill takes the first **free** tile. Only the ordering half
appears in the classes doc, as prose ("the rear-most free tile of that region
measured along your own advance direction"); the fallback appears in neither. A
consumer has to reconstruct a total order over tiles from an English sentence and
hope. There is no way to ask the contract "where will my unit 1 appear if it dies
now" — `ArenaBasics.ExpectedArrivalTiles` returns the whole *region*, which is the
honest limit of what the scaffold can promise, and the exact tile is where the
coordination decision actually is. I verified my derivation against four observed
arrivals and it predicted all four, but that is a test I had to invent, and the
next author will invent it again or will silently ship the sign error I nearly
shipped. **A resolved placement policy should publish its ordered candidate tile
list, not a compound name for one.**

**3. `qualification.json` publishes a field called `coordinationGradeAwarded`,
and on the suite this wave is required to pass it is `null`.** For a wave whose
entire assignment is coordination, that is a uniquely deflating thing to find in
your own evidence file. Nothing in the packet, the rules card or `qualify --help`
mentions the field, which suite populates it, what its values are, or whether a
tier is a prerequisite for one — so I cannot tell whether it is unimplemented,
gated on a suite above T4, or something my bot failed to earn. `passed` and
`profileComplete` are both `true` and `tierAwarded` is `T4`, so nothing is wrong;
but a published contract field that is silently null is indistinguishable from a
capability you missed, which is exactly the failure mode the qualification suite
exists to prevent. **Two smaller ones in the same file, while I am here:** the
suite still writes a `viewer.html` beside all 36 probe replays even though
`experiment` stopped doing that in 0.9.22 (192 MB of the 213 MB my `evidence/t4`
arrived at — stripping them left 21 MB and did not change the report hash), and
the flag that turns it off for `experiment` (`--viewer`/`--open`) has no
counterpart on `qualify`.

**Honourable mentions, because they are repeats and the fix is cheap.** The
published CLI is still `sandbox/cli-publish/botarena` on disk while every
document and brief invokes `nilbots`, and the banner still prints `nilbots` — this
is now the third wave. `ArenaBasics.ClassOf` still recovers a class by splitting
form IDs on `-`, the precise thing the classes doc forbids now that `ClassId` is
published on self, allies, enemies, participants and teams, and its doc-comment
still promises a replacement that shipped two waves ago. And the scaffold still
offers no reader for the facts each wave is actually about: wave 5 needed
`AttackProfile.Volley`, `Form.ProjectileGuard`, `FormTransition.AutomaticReturn`,
`FormTransition.Placement` and `ShotProgramDefinition.MinInitialAimSteps`; wave 6
needed `UnitSlotState.LifecyclePending.ReservedPosition`, the choke geometry of
the wall grid, and the arrival-tile order above. **The good news, and it deserves
saying:** the 2 MB source cap made this the first wave where I did not have to
delete documentation to fit, and it is the reason the negative C5b result could be
shipped as commented-out-but-explained rather than as a deleted experiment.

## Repairs and revisions found from my own measurements

Qualification passed on the first attempt with no repairs, so all of these came
from my own `deck` and mirror replays.

1. **The same-destination sibling deadlock** (friction 1). Found by
   reconstructing every actor's submitted destination from the replay and
   classifying blocked moves. 23.47 blocked ticks/match → 0.56. Fixed by C1.
2. **Yield folded into the route search instead of vetoing its answer.** The
   first version refused a chosen step, which converted a collision into a wait.
   Folding the refusal into `firstStepBlocked` lets the BFS route around.
3. **C4 was a cost pretending to be a tiebreak** (five wins; see above).
   Demoted to the last sort key.
4. **C5 was one rule doing two jobs** — measured separately, one half was worth
   keeping and one was worth two games. Split into C5a and C5b; C5b shipped off.
5. **A body arrived at distance zero has no committed route.** The first version
   treated every sibling's facing as a claim, including bodies standing on the
   objective, which blocked siblings for nothing on a four-tile objective. Claims
   are now empty for a body that has arrived and for a body frozen in a windup
   (whose tile is already an obstacle every consumer knows about).
6. **`ArcRules` switches are `static readonly`, not `const`.** A `const` folds at
   compile time, so an ablation build would differ by dead-code elimination as
   well as by behaviour, and the single-lever claim would be false.
7. **My own offline analyser had the rally-region sign backwards**
   (`active + delta` instead of `active − delta`). Caught by checking the
   derivation against four observed arrival tiles rather than against the prose.

## Build and qualification timings

Cold `--no-cache` WASM build: ~8-15 s (Docker builder, macOS arm64 host). Full
`frontline-qualification-5` including the hash-linked T3 and T2 prerequisites:
~5 s. One 500-tick WASM match: ~1.3 s. A 16-seed sweep: ~20-25 s; a 32-seed
sweep: ~45 s.

**Operational note.** A match still writes ~16-25 MB of replay, so the 32-seed
sweeps in this wave were ~700 MB each. `experiment` no longer writes a viewer
unless asked, which roughly halved the per-match footprint versus wave 5, and
replay writes now fail loudly instead of truncating — both were felt. Every sweep
in this wave was extracted to a text summary and deleted immediately
(`sandbox/arc-light-w6-scratch-9f3c2e71/results/*.json`); this freeze keeps the
qualification evidence with viewers stripped, the text summaries, and one cited
replay.

## Strategy passes

One authoring pass building `ArcTraffic`, then four measured revisions (fold the
yield into the route search; demote C4 from a cost to a tiebreak; split C5 and
retire its losing half; empty the claims of arrived and frozen bodies), then a
six-way single-lever ablation sweep on identical seeds across two legs. Nothing in
the frozen build was tuned by eye: every number in this file was measured against
my own rebuilt predecessor or my own single-flag ablations, and the two rules that
measured inert (C3, C4) are reported as inert rather than as wins.
