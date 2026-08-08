# DX report — iron-root, wave 5 (revision 5, OPEN ROOT)

## Isolation statement

Written from this revision's own forensics, its own qualification report, and
private sparring against this lineage's own rebuilt wave-4 predecessor and
against variants of this revision's own source. **No other entrant's source,
directory, replays, standings, or aggregate balance report was opened.** This
revision's private scratch was `sandbox/iron-root-w5-scratch-4d91b7e`, a
uniquely named directory used for nothing else.

Permitted material used, and nothing else: the author packet, the Frontline Labs
v1 rule card, the experimental classes addendum (read in full), the
`templates/botarena-generic-actor/` scaffold, `src/BotArena.Sdk/` types, this
lineage's own wave-4 directory, and `sandbox/cli-publish/` (nilbots 0.9.21, SDK
0.10.6). The three briefed documents were hash-verified before use:

```text
d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e  FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md
06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8  FRONTLINE-LABS-RULES.md
2333bd3c9f412e4e9439779ef3d5f2ca6bc8abae6f00973daf54f7e4c892de50  EXPERIMENTAL-FRONTLINE-CLASSES.md
```

The classes addendum hash differs from the one revision 4 recorded
(`b91047df…`); it gained the aim, five-slot-variant and stance-ground sections,
and a stale class-identity line ("Mobilize back once per life") was corrected
mid-wave. That correction did not change anything here — every reversibility
decision in this revision reads `irreversibleForLife` off the resolved route and
was already right, which is checkable in `ContractLens.Reversible`.

The frozen wave-4 tree was left untouched and still reproduces its recorded
identity:

```text
16542cad39c662b5f9b2717b52b235807e3675b7e197259f310cbeb293cf1494  wave-4 source tree
```

**One incidental exposure to disclose, as the packet requires.** The shared
volume ran out of space mid-session (a documented hazard this wave — the
coordinator confirmed another author had filled it). Diagnosing it, I ran
`du -sh *` inside `sandbox/`, which printed other entrants' scratch directory
**names and sizes**. No file inside any of them was opened, listed, read, or
run. Those directory names were already visible in my own environment listing at
session start. Nothing else about any other entrant was observed. My own sweeps
had contributed to the pressure; every sparring script in this revision now
scores a cell and **deletes the replay directory immediately** (a 500-tick
replay v3 plus its self-contained viewer is ~16 MB, and this revision ran ~110
matches), and my scratch finished at 39 MB.

Nothing was committed to git.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `iron-root` |
| Population / wave | Frontline Labs classes, **wave 5** (`classes-wave-5-2026-07-30`) |
| Authoring lineage | `iron-root-v1` |
| Class | `bulwark` (declared in `botarena.json`) |
| Doctrine | FORTRESS ROTATOR — revision 5 codename **OPEN ROOT** |
| Role | `verdict-doctrine` |
| Target tier | cumulative T4 (retain) |
| The game | `--classes <pair> --movement facing-locked --pendulum keel --skills kit --bend universal --aim offset --stance-ground open [--five-slots wane]` |
| Resolved identity, own mirror | `frontline-labs-1-bulwark-vs-bulwark-sail-open-facing-locked` |
| Resolved identity, vs a fabricator | `frontline-labs-1-bulwark-vs-fabricator-deck-facing-locked` (topology `asymmetric-slots-4-3-v1`) |
| Predecessor | wave-4 directory, left untouched |
| Scaffold | `templates/botarena-generic-actor/`, **pruned** — see friction #3 |
| Source-tree hash | `e2d868e794c9e090450758e9a8fce44ec7ce4c9c308544ca33255b4a4c11d7e7` |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/Guest **0.10.6**, actor protocol 1.0, WASI p1 core module, platform-matched Docker builder (macOS arm64 host) |
| Build cache key | `c30f8c18b11c7c4c9874506e5c422c823dcfeaf9a4d53ea5bbff9e502cf2fb19` |
| **`out/bot.wasm` sha256** | **`9f5a7ae3cccc8d5188e7bf6636b974e3f3d1e3f0229c71eb5dafb7a4478f633b`** |
| `evidence/t4/qualification.json` sha256 | `5b7cc2b4c86e054af697b8b6e1a60a406ad9460c2bc9b2a2542f83f15afb80ed` |
| Cumulative T3 prerequisite report sha256 | `8d0033e25fd1065cdb424fa43631721f85b85b872893d6ed9609a91dded33bf1` (hash-linked inside the T4 report) |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| T3 prerequisite contract fingerprint | `4e77075bd13bbe56485eb29b57c8b916fec9dcd8c9ef9fdaa40fc6fad6944e8e` |
| **Qualification outcome** | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, **exit 0**, **T4 awarded**, `balanceEvidenceEligible: true`, `profileComplete: true` |

All five T4 components passed (`suppression-choke`, `entry-initiative`,
`prediction-chamber`, `front-rotation`, `map-holdout`) with the cumulative T3 and
T2 prerequisites rerun and hash-linked automatically. The suite runs the
duel-depth union profile — no classes, no skills, no aim, no bend envelope, no
coupling — and this artifact passes it unchanged, first attempt. Every rule
added this wave is gated on a declared field that profile does not carry, so on
that contract the artifact is decision-for-decision what revision 4 was, minus
one deleted fallback.

### Per-file source hashes

Recipe (unchanged across the lineage):

```bash
ls *.cs botarena.json IronRoot.csproj | LC_ALL=C sort | xargs shasum -a 256 | shasum -a 256
```

```text
6dfb7d6d55ed0e92e15e5cf47a6dd59add7eeef451ac02eac66d8024f1fb5987  ArenaBasics.cs
dfb1470dd8ad0a288f094d7127ed102b306aab0c5dc9624c4c581ccc67ba40d3  ArenaGeometry.cs
930facf28f6597836739db206d20e4705e76c40225568fd5a1229675ff1a74c6  ContractLens.cs
1e402fb87f26ae3f144ceb068fd95ff75cb8020c081b36d49e70f447689ec213  FortressPlan.cs
c4bfa78d7c249ff0cd66544cef12d95abd92226fcb2a52a9764a46c35b9dac7b  Gunnery.cs
69e65bc4beb295239edc04559b4ea56ab0a5a23950fac4ef4bc2779f77780580  IronRoot.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  IronRoot.csproj
4e69aef2560b076e1c114d65385b553b7052a28fcc51c7371887fe882d39032a  Kinematics.cs
b983bb8cd98ad1702d15f07c119c124bae512e0fca1147a2e80e0fa02ce2339c  botarena.json
```

`ArenaGeometry.cs`, `Kinematics.cs`, `IronRoot.csproj` and `botarena.json` are
byte-identical to wave 4. `RatchetClock.cs` is **deleted** (friction #3).
`ArenaBasics.cs` is the current scaffold **pruned to the members this doctrine
calls** — deletions only, no edits (friction #3).

## Doctrine in one paragraph

**OPEN ROOT.** Three declarations moved and each of them turned a correct
revision-4 rule backwards. The placement tag stopped binding: every same-life
route declares an empty `forbiddenTileTags` while the map still publishes 112
tiles tagged `transition-placement-forbidden`, so a doctrine that asks the MAP
refuses to armour or root on the entire scoring surface and the central corridor
— a third of the walkable board and exactly the third worth standing on —
while a doctrine that asks the ROUTE gets it back and behaves identically
wherever the tag does bind; that one read is worth **+85.6 points of territory
per cell**, the largest single effect this lineage has ever measured. The mobile
gun got 45 degrees: rotation is cardinal and headings are eight-way, so on a
zero-offset arm half of every ring was unreachable however the body turned, and
this chassis gains most because its vision is OMNIDIRECTIONAL and it has always
seen the diagonal bodies it had to walk onto a lane to answer — worth **+62.6**,
with the same declared envelope widening every "does that muzzle bear on me"
question in the doctrine, which is the one-line fix for the variance revision 4
suspected and could not name. And the turret became a true cycle, which made me
relax the wrong end: reasoning that a rental needs no collateral, I replaced the
tenure gate with the only question a rental can lose on, and the board charged
**47 points a cell** for it — so the gate is unchanged and the correction is
worth more than the rule was, because **reversibility does not make a root
cheaper to take, it makes one cheaper to leave**, and leaving is where a one-use
return was hoarded rather than spent. Shell discipline is the assignment and it
is a count rather than a class name: the arc covers one quadrant and the stance
cannot rotate, so the question is how many hostile sources bear on this tile
inside the frozen window and how many one quadrant answers — with numbers also
read as a declared fact (more slots, rebuilding faster) so it generalises past
the classes that exist — and the refusal is handed to something rather than
thrown away, because **against poke you raise the arc and against numbers you
root the gun**: worth zero against a poking predecessor and **+21.3 against
numbers**, which is precisely the shape the lab measured.

## Measured records vs the rebuilt wave-4 predecessor

Opponent: this lineage's own **wave-4 source rebuilt from the frozen tree with
`--no-cache`** (tree `16542cad…`, rebuilt artifact
`856f3af452413a4c6fb8ca6caa9816ff6941b181349a5d15698d09f2c89bd9e1`). The frozen
wave-4 artifact itself faults on these contracts, as the brief warns.

**WASM runtime (the frozen-cohort standard), the crew game on a bulwark mirror,
both sides, three seeds — 6 cells.** Score is signed territorial progress;
margin is candidate minus opponent.

| pairing | W–L–D | margin / cell | how it ended |
| --- | --- | --- | --- |
| `bulwark-vs-bulwark`, own rebuilt wave-4 | **6–0–0** | **+60.0** | **base-breach in all six** |

The cited cell is `evidence/forensics/cited-crew-cell/` — team 0 breaches at
tick 184, replay hash `96a11608…`, `nilbots verify` OK.

**The seeds buy nothing on this pairing and I would rather say so than count to
six.** Neither artifact consults `context.Random`, so within a side every seed
produces identical decisions and identical per-match counters (visible in
`evidence/forensics/all-cells-wave5.txt`). There are **two** informative cells
here, one per side, and both are wins by breach with the same margin. The
in-process 10-cell sweeps used for attribution have the same property. Weigh the
sides, not the seeds.

**Cross-class probes, WASM, three seeds each.** The isolation rules permit only
this lineage's own material, so the other two classes are played by *this
revision's own artifact* bound to them with `--classes` (the class is a contract
fact, so the WASM is byte-identical — same `9f5a7ae3…`). These are the only
cells in which numbers and a long game exist at all, and they are where two of
the four rules are exercised.

| cell | W–L–D | margin / cell | how it ended |
| --- | --- | --- | --- |
| `bulwark-vs-fabricator` + `wane` (mine is the bulwark) | 3–0–0 | +60.0 | base-breach |
| `bulwark-vs-striker` (mine is the bulwark) | 3–0–0 | +46.0 | max-ticks |

## Skill, cycle and diagonal usage counts

Per match, candidate side, WASM. Ranges are across the three seeds.

| | vs wave-4 (mirror) | vs own fabricator | vs own striker |
| --- | --- | --- | --- |
| volleys cast | **0** | **0** | **0** |
| shells raised (completed) | 15–16 | 15–16 | 19 |
| shells **declined** by discipline (derived) | ~0 | **~17** | not isolated |
| ticks inside the shell | 38–44 | 54–58 | 37 |
| bolts turned by my arcs | 10–11 | 12–13 | 11 |
| shells broken (automatic-threshold return) | 0 | 0 | 0 |
| turret entries (anchor completions) | **0** | **2** | **5** |
| ticks inside the turret | 0 | 56 | **153** |
| **diagonal launches fired** (`initialAimOffset ≠ 0`) | **9–10** | **16–17** | **27** |
| bends fired | 9 | 19–20 | 46 |
| shots fired | 16–17 | 45–47 | 106 |
| deaths | 1 | 7 | 13 |
| unit slots fielded (mine / theirs) | 3 / 3 | **3 / 4** | 3 / 3 |

Four of those numbers need saying out loud rather than hiding in a table.

- **Volleys cast: 0, everywhere, and provably.** Volley is the striker's skill,
  so a bulwark chassis declares no route into a form whose attack profile
  launches more than one projectile, and `TryCastVolley` returns null on the
  first line of every tick of every cell above. Unchanged from wave 4 and still
  unexercised as doctrine.
- **Turret entries: 0 against the predecessor.** Not a bug and not a refusal:
  those matches END, by breach, at tick 184, before the tenure gate ever sees a
  board that wants a root. The cycle is exercised in the two cross-class cells —
  **5 entries and 153 turret ticks over a full 500-tick striker game**, with the
  weight-on-demand exit and the front-rotation exit both firing (traced by
  decision debug text in a kept mirror probe: 4 × `renting the gun`, 1 ×
  `weight wanted: unrenting the gun`, 1 × `front rotated: unrooting`).
- **Slots 3 / 4 against the fabricator, read not assumed.** `--five-slots wane`
  resolves to topology `asymmetric-slots-4-3-v1`: four slots unlocking
  60/180/300, ordinary children rebuilding at **22** and the extra at **30**.
  Nothing here counts in 60/180/300/420 or 15 — the unlock ticks come from the
  observation's own due ticks and the rebuild economies from the lifecycle
  profile each slot is assigned. That read is load-bearing: `EnemyRebuildTicks`
  22 against my own 30 is one of the two terms that makes shell discipline fire
  against numbers.
- **Shells broken: 0.** My own arcs never reach their declared third deflection
  (12 turned across 16 stances is 0.8 per stance), and the permitted opponents
  raise arcs that my discipline mostly declines to poke. Implemented and priced;
  unexercised. Same honest zero as wave 4.

## Attribution: single-rule ablations from the corrected base

Each row is the same artifact with exactly one rule removed, rebuilt through the
controlled toolchain and sparred over the same cells. Raw cells in
`evidence/forensics/all-cells-wave5.txt`. In-process, 10 cells (5 seeds × both
sides) against the rebuilt predecessor; base is **+60.0**.

| rule removed | vs wave-4 | vs own fabricator | worth |
| --- | --- | --- | --- |
| **placement read from the ROUTE** | −25.6 | +60.0 | **+85.6** — the revision |
| **the ±45° aim envelope** | −2.6 | — | **+62.6** |
| **shell discipline (the envelopment count)** | +60.0 | **+38.7** | **0** vs poke, **+21.3** vs numbers |
| the reversible-cycle policy | +60.0 | +60.0 (counters byte-identical) | **0** measurable; +27.3 in a variant duel |
| the on-point site ranking | +60.0 | +60.0 | **0** everywhere measured |

Two of those deserve their own sentences.

**Shell discipline measures zero against the only opponent the isolation rules
supply, and +21.3 against the shape it was written for.** The mechanism is
visible rather than inferred: in the fabricator cell, removing discipline takes
shells raised from 15–16 to **31–34**, ticks frozen inside the stance from 54–58
to **103–108**, deaths from 7 to **13–14**, and turns a breach win into a
max-ticks grind. That is the lab's measured claim reproduced from the inside —
the shell is opponent-shaped, and the trap is standing still in front of numbers.

**The reversible-cycle policy is the weakest claim in this report and I am not
going to dress it up.** With the tenure gate restored it changes no decision at
all against the predecessor or against my own fabricator copy — the ablation's
per-match counters are byte-identical, which is proof of inertness rather than
an estimate. It binds only where a root is actually taken, and there the only
measurement available is a duel between this artifact and a variant of itself
with the cycle removed: **4–0–2 at +27.3 over 6 cells**, with the two draws a
degenerate mirror. Four informative cells, all positive, one opponent that
shares my own blind spots. I report it as suggestive and no more.

### Tried, measured, and turned round

Two rules were shipped backwards and corrected by measurement. Both corrections
are worth more than the rules.

1. **The tenure gate was replaced because reversibility made it look like
   collateral on a loan nobody was taking out.** Every clause of revision 4's
   gate exists because the anchor route was `irreversibleForLife`; under a rental
   the commitment costs two declared windups instead of a life, so demanding a
   guaranteed capture window of relief standing on the surface is asking for
   security against a risk that no longer exists. The argument is airtight and
   the board charged **47.4 points of territory per cell** for it: the relaxed
   gate scored **+12.6** and the restored one **+60.0** over the same ten cells.
   What the arithmetic was missing is that the *bar* and the *exit* are separate
   decisions, and only the exit got cheaper. A root taken on a thin case is still
   a body that is not scoring while the case is thin; being able to undo it later
   does not pay for the ticks in between.

2. **The numbers-answer anchor was written without the tenure term at all**, on
   the reasoning that a crossfire is a local emergency and the site ranking is a
   global preference. Ablated, that version lost **ten cells out of ten by sixty
   points of territory each** — a whole match's worth of front, every time —
   because it converted the body holding the ground into a gun over ground
   nobody held, and it did so exactly when the ground was contested. The turret
   bargain is not suspended by being shot at; being shot at is when a doctrine
   most wants to believe it is. It now carries the same `worth` term as every
   other root and relaxes only the *site floor*, which is the part that really is
   local.

## Timings (Apple Silicon, warm Docker builder)

| Step | Time |
| --- | --- |
| `dotnet build` of the editing project | ~0.6 s |
| `nilbots build --no-cache` (cold, Docker) | **7.7 s** |
| `qualify --suite frontline-qualification-5` (WASM, both assignments) | **5.9 s** |
| one 500-tick crew-game match (in-process or WASM) | ~3.3 s |
| 10-cell sweep (5 seeds × both sides) | ~40 s |
| one single-rule ablation: variant build + 10-cell sweep | ~50 s |

The inner loop is still the best thing about this platform, and this revision is
the clearest case yet: **four of my own confident rules were graded in under
five minutes of wall clock**, and two of them were wrong. The 47-point tenure
error and the 60-point ungated-anchor error were both found by the ablation
harness rather than by reading the code, and neither would have been visible in a
single match.

## Top 3 frictions

### 1. The map keeps the tag; the ROUTE decides whether it binds — and nothing says so

This is worth +85.6 points of territory per cell, the largest measured effect in
this lineage's history, and revision 4 missed it by one reader.

The classes addendum's stance-ground section says, for `free`, exactly the right
thing: "Read your entry route's `placement` from the contract — under this arm
its `forbiddenTileTags` is empty." Two paragraphs later, `open` says "EVERY
transform placement is free, turret anchors included." That reads as a statement
about the *rules*, and the natural implementation of a rules statement is to stop
consulting the thing that used to forbid it — or, if you are already correct
about tags, to assume the tag is gone.

**The tag is not gone.** On this arm the resolved map still publishes **112
tiles** tagged `transition-placement-forbidden` — the whole scoring surface, the
central corridor, both pads — and every same-life route's own
`forbiddenTileTags` is `[]`. Both facts are true at once, and only the second one
binds. A doctrine that tested the map's tag set (which is what revision 4 did,
and it was correct on every earlier arm) declines to armour or root on a third of
the walkable board, silently, with no error and no legality refusal, because it
never asks. The failure mode is invisible: the bot does not do something illegal,
it declines something legal.

One clause would have closed it: *the map's tag vocabulary is unchanged; what
changes is that no route forbids it, so ask the route.* And the general lesson
generalises past this arm and is the same one revision 4 learned about spawn
reservations from the other direction: **when a rules change is expressed as "X
is now allowed", the contract expresses it by removing X from a route's own list,
not by removing X from the map.** The map describes the board. The route
describes the deal.

### 2. "The turret is a true cycle" says what a cycle costs and never says what it is for

The addendum is unusually complete about the mechanics: `irreversibleForLife` is
false, read it; health maps by `preserve-ratio-floor-minimum-one` in both
directions with no entry heal; full cycles are lossless and partial health pays
the floor each round trip; the windups are the commitment price. Every one of
those was directly implementable and every one of them is correct — I derived
5/5 ⇄ 7/7 and 4/4 ⇄ 7/7 lossless, and 3/4 → 5/7 → 2/4 as a one-health lap, from
the declared formula before running a single match, and the engine agreed.

What no sentence anywhere addresses is the *strategic* question the change poses,
and the obvious answer to it is the losing one. "This commitment is now
reversible" reads as "commit more freely" — that is what the word means
everywhere else — and committing more freely cost me 47 points of territory a
cell. The correct reading is the opposite end of the same route: the bar for
entry should not move at all, because a root taken on a thin case is a body that
is not scoring for as long as the case is thin, and reversibility does nothing
about those ticks. What reversibility buys is that **leaving is no longer
precious** — a one-use return gets hoarded, and every gate around it is really a
gate around the hoard.

I do not think a doc should tell authors the answer; that is the doctrine's job
and finding it is the experiment. But the addendum's own framing ("the bargain is
the price of fortifying a point", "unlimited per life") invites the wrong half,
and the price it itemises (windups, the health floor) is the small half. The
expensive half is the objective weight, which is stated three sections earlier
under a different heading, in the tense of the old once-per-life rule.

Smaller version of the same shape, worth recording because it was corrected
mid-wave: the class-identity paragraph said the bulwark may "Mobilize back once
per life" while the stance-ground section said unlimited. A contract-driven bot
is immune (I read the flag and never the prose), which is exactly the argument
for reading the flag — but the two lines disagreed for the whole first half of my
session and the wrong one was in the class's own identity paragraph.

### 3. The 256 KB source cap is the binding constraint on contract-driven authorship, and 20% of it is a starter I call ten members of

`nilbots build` refuses sources over 256 KB. Revision 4 froze at **250.6 KB**,
which I did not know was a cliff edge until the first build of this revision
failed at 290.9 KB. Reading three new declarations — route placement legality,
route reversibility, and the health-transfer policy — plus commenting why, does
not fit in 5.4 KB.

Nothing about that cap is unreasonable. What is worth reporting is **what it
charges for**, because it charges for exactly the two behaviours the author
packet demands:

- *Read the contract, do not assume.* Every read is a method with a doc comment
  explaining which declared field it consults and what an absent field means. The
  contract-reading layer is now 35.5 KB — 14% of the whole budget — and it
  contains no strategy at all.
- *Write your reasoning down.* The packet asks for measured corrections, resisted
  hardcoding temptations, and rules that are provably inert. Those are comments.
  977 of `IronRoot.cs`'s 3,262 lines are comments, and I spent an hour of a budget
  meant for doctrine compressing them.

I paid for it in two places and both are honest but neither is doctrine:

- **`RatchetClock.cs` is deleted.** Revision 4's DX already stated the fallback
  was unreachable on every contract this lineage can run and kept it as a
  contradiction check. It provably never executes and it cost 7.2 KB, so it went.
  That is a budget decision wearing a design decision's clothes, and I would
  rather label it than pretend.
- **The scaffold is pruned from 49.7 KB to 25.4 KB.** `ArenaBasics.cs` ships 37
  members; this doctrine calls **ten** (`LiveHold`, `Capture`,
  `ArrivalsRallyForward`, `ExpectedArrivalTiles`, `ObjectivePresence`, `Threat`,
  `OrderedDirections`, and the `Hold` / `CaptureRules` / `Incoming` records). 26
  members survive the prune, because those ten pull private helpers behind them.
  The deleted 24.3 KB is `TryDodge`, `TryDirectShot`, `TryAdvanceToActiveObjective`,
  `Wait`, `ClassOf` and friends — all of which a developing bot is explicitly
  invited to replace, and every one of which this lineage replaced two revisions
  ago. The prune is deletions only, no edits, so it diffs cleanly against the
  template; but the lineage advertised "scaffold synced verbatim" as a
  reviewability property and that property is now gone. **A helper library inside
  the submission budget makes the starter's convenience a permanent tax on the
  doctrine that outgrew it.** Shipping `ArenaBasics` as an SDK type — or
  excluding a declared helper file from the cap — would return 10% of the budget
  to every author past their first revision.

## Documentation gaps

Beyond the three frictions:

- **`shotProgram.minInitialAimSteps` is the single most consequential number in
  this arm for a straight-only chassis, and the aim section is four sentences.**
  It says the offset is restored and to read the bounds, which is enough to
  implement. It does not say what it *changes*, and the change is categorical
  rather than incremental: `rotate` sets an absolute **cardinal** facing while
  projectile headings are **eight-way**, so at zero offset exactly half of every
  ring is unreachable from a facing-aimed gun no matter how it turns, and a ±1
  envelope hands back all four diagonals. Revision 4 enumerated its whole bend
  envelope and concluded a curve was "worth eighteen off-axis tiles and nothing
  at all as an angle"; the offset is worth the other half of the board and it
  arrives as a tuning flag. It is also the *defensive* half — every enemy gun has
  it too, so every "that muzzle bears on me" test in a doctrine silently gets a
  tick optimistic on the day the flag flips. That is one sentence of prose and it
  would have saved me the enumeration.
- **`preserve-ratio-floor-minimum-one` is a policy ID and the formula is a
  second policy ID, and a bot has to parse strings to price them.**
  `SameLifeHealth` carries `Policy`, `FlatHealthGain`, `Evaluation`,
  `Arithmetic`, and `PreserveRatioFormula` — five frozen strings. To answer "what
  will my health be after this route" I match substrings (`"ratio"`,
  `"minimum-one"`) against them, which is exactly the "parse a policy string or
  reimplement the prose" complaint revision 4 raised about the volley's spread.
  The arithmetic is fully declared; it is just not machine-readable. A
  `RoundTripDelta` or an explicit `(numerator, denominator, floor)` triple would
  make the whole class of "is this cycle free" question a subtraction.
- **`irreversibleForLife` reads backwards, and now it reads backwards on the
  route you most need it from.** Revision 4 recorded the collision — turret and
  stance returns share the `mobilize` action ID with different reversibility —
  and keyed everything on the resolved route, which is what saved this revision:
  on this arm the flag is `false` on *all four* transform routes, so a doctrine
  that had special-cased "the turret one is the irreversible one" would now be
  wrong in the expensive direction. Reading `!IrreversibleForLife && a reverse
  route exists` is the correct composition and nothing says the second half is
  needed.
- **Null is still indistinguishable from absent on the published hold.** Carried
  forward from revision 4, and now it costs a file: the contradiction check that
  could settle it is gone, so this revision simply trusts the pair. On every
  contract it can run that is the right answer.

## Hardcoding temptations

All resisted; the ones this revision created:

- **"Open placement means placement is free."** It means no route forbids a tag.
  The tag set is still published and a stricter arm re-binds it, so the test is
  `PlacementAllows(route, tile)` and never a boolean about the arm.
- **"The mobilize route is the reversible one."** Both halves of both cycles
  declare `irreversibleForLife: false` here. Reversibility is
  `!IrreversibleForLife` **and** a route back from the target form, checked on
  the route being taken.
- **"Anchoring heals."** It did, by `min(5, health + 2)`. It now maps by ratio
  with no gain, which happens to *raise absolute health* on the way into a
  tougher form (3/4 → 5/7) and lower it on the way out. Both directions come
  from `HealthAfter`, so a contract that restores a flat heal prices correctly
  without a code change.
- **"±1 is the aim envelope."** It is `Min`/`MaxInitialAimSteps` on the form's
  own profile, clamped, and zero on every special — the volley aims by facing and
  the turret aims absolutely, both of which the same reader gets right because it
  asks `ShotProgram.Enabled` and `Volley is null` first.
- **"Numbers means fabricator."** It is `EnemySlotCount > OwnSlotCount` or
  `EnemyRebuildTicks < OwnRebuildTicks`, both read per side from the topology's
  slot list and the lifecycle profile each slot is assigned. A tuning variant
  that changes the rebuild clock changes this doctrine's behaviour with no code
  change, which is the point.
- **"Four slots unlock at 60/180/300."** They do on this variant. The unlock
  comes from the observation's own due tick and the rebuild from
  `lifecycle.profiles[].delayTicks`; nothing in the source contains any of those
  numbers.

## Confusing terminology

Carried forward and still true: "Anchor"/"Mobilize" are prose words with no
contract representation; `irreversibleForLife` reads backwards; "Available"
versus "will succeed"; "facing-locked" restricts movement, not rotation; "hold"
is three different things; "the kit" is not a fixed set; "deflect" sounds
defensive and is an attack; "facing quadrant" is a vision term reused as a
collision term.

New this revision:

- **"Free" and "open" are two placement arms and one English word.**
  `--stance-ground free` frees the two skill stances; `open` frees everything
  including turret anchors and also changes the turret's reversibility and health
  arithmetic. So `open` is not "more free" — it is a different-sized change
  bundled under the same flag, and the flag's name says nothing about the cycle
  or the health floor, which are the two things in it that can lose you a match.
- **"Unlimited" is about the count and says nothing about the price.** The cycle
  is unlimited per life and each lap costs two windups and, below full health,
  one health that never comes back. "Unlimited" and "free" are one word apart in
  reading and 47 points of territory apart in policy.
- **"Placement" is used for three unrelated things.** A route's completion tile
  legality (`SameLifePlacement`), a fabrication output offset
  (`CandidateOffsets`), and the lifecycle's `automaticReturnPlacement` policy for
  where a respawn appears. Only the first is what `--stance-ground` moves.

## Repairs and strategy passes

One strategic revision plus the assigned discipline; everything else mechanical,
each driven by a measurement or a contract read.

1. **Strategy — OPEN ROOT.** Placement asked of the route (+85.6); the ±45°
   envelope exploited offensively and respected defensively (+62.6); shell
   discipline as an envelopment count with the refusal handed to the anchor
   (+21.3 against numbers); the reversible cycle priced by its declared health
   formula and spent on the exit rather than the entry. Two sub-rules shipped
   backwards and corrected by measurement (−47.4 and −60.0 respectively); one
   sub-rule measured inert everywhere (the on-point site ranking) and kept
   because it is the contract-correct behaviour on an arm where the route allows
   it, and labelled as inert rather than implied to work.
2. **Repair — the muzzle bears wider than its facing.** `MuzzleClock`,
   `ExpectedWindupHits` and `HotTiles` all tested facing equality; they now test
   the enemy's own declared launch envelope. This is the one-line fix for the
   cause revision 4's DX suspected of its `rig` variance and could not name.
3. **Repair — `AlignmentTurn` searches facings, not targets.** With a
   three-heading envelope, facings genuinely differ in how much they cover, so
   the search totals reachable targets per candidate facing and the incumbent
   wins ties. Revision 4 would spend a rotation to gain nothing.
4. **Repair — the anti-flicker clause the shell already needed, applied to the
   cycle.** A reversible one-tick route is cheap enough to thrash; a completed
   leg buys silence for one full declared cycle. Revision 4 paid for this lesson
   once with a windup-one shell that entered its stance 223 times in a match.
5. **Deletion — `RatchetClock.cs`**, provably unreachable and 7.2 KB. Friction
   #3 is the honest reason.
6. **Deletion — 24.3 KB of unused scaffold.** Deletions only. Friction #3.

## What I could not evaluate

- **The turret cycle as a scoring claim.** It is implemented, priced from the
  declared health formula, gated, and exercised (5 entries and 153 turret ticks
  in a 500-tick striker cell, with both exit triggers observed firing). It
  measures *zero* against the only opponent the isolation rules supply, because
  those matches end by breach at tick 184 before the gate sees a board that wants
  a root, and zero against my own fabricator copy with byte-identical counters.
  The only number I have is a duel against a variant of myself (+27.3, four
  informative cells). I would not defend a difference of ten points there.
- **Volley, still.** A bulwark declares no route into a multi-projectile form, so
  two thirds of the kit remain handled-as-contract rather than exercised-as-
  doctrine. Third wave running.
- **Breaking a guard.** My arcs never reach their declared third deflection (0.8
  turns per stance) and discipline mostly declines to poke the arcs my own
  variants raise. Priced and unexercised.
- **Whether the on-point root is ever right.** The route allows it, the ranking
  offers it below an equal-coverage tile beside the point, and it was never
  taken in any cell I measured — the margin test refuses it whenever an enemy is
  on the surface, which on this map is whenever the point matters. Fifteen lines
  of contract-correct code with no measurement behind it.
- **Anything with more than two informative cells.** Neither this artifact nor
  its predecessor consults `context.Random`, so a fixed map plus a fixed opponent
  gives one game per side however many seeds you spend. Every number in this
  report rests on two sides against one lineage, and the honest way to read the
  headline is "wins both sides by breach", not "6–0–0".
