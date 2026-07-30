# DX notes — ledger-fly revision 4 (Frontline classes, phase-2 skills + bend)

## Isolation statement

Written from this project's own sources, its own frozen predecessors, its own
qualification report, and matches this entrant played against **its own rebuilt
revision-3 source and its own class-variant copies, and nothing else**. No other
entrant's directory, source, standings, replays, or aggregate balance report was
opened; no scratch directory other than my own was read or written. Permitted
material actually consulted: `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`,
`docs/FRONTLINE-LABS-RULES.md`, `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` (read in
full), `templates/botarena-generic-actor/`, the public SDK types under
`src/BotArena.Sdk/`, my own frozen wave-1 / revision-2 / revision-3 directories
(read only, left byte-untouched), and `sandbox/cli-publish/`. Private scratch for
this pass was `sandbox/ledger-fly-v4-scratch-6e1b93/` — a uniquely named
directory, not a shared or guessable one.

**One incidental exposure, disclosed as the packet requires.** The cohort
directory `arena-bots/frontline-labs/classes-wave-4-2026-07-30/` is shared, and
my final freeze check listed it to verify my own tree. That listing revealed the
*names* of seven sibling entrant directories and their source file names, which
were not there when I started. I opened none of them: no source file, replay,
qualification report, standings table, or aggregate report belonging to another
entrant was read, and every match reported below was played against my own
rebuilt revision-3 source or my own class-variant copies of this revision. Every
strategic decision in this freeze was made before that listing existed. I am
recording it because "I only saw filenames" is exactly the kind of judgement an
author should not get to make privately — and because the fix is structural: a
`find`/`ls` on one's own output directory should not be able to enumerate the
cohort, so per-entrant directories want to be siblings of the cohort root rather
than children of it.

The three permitted documents were hash-checked against the brief before use:
`d31b59aa…` (packet), `06ff461e…` (rules card), `b91047df…` (class addendum). All
three matched.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `ledger-fly` |
| Class | `fabricator` (declared in `botarena.json`) |
| Authoring lineage | `ledger-fly-v1` |
| Revision | 4 (wave-4 cohort, phase-2 skills + bend envelope) |
| Role | verdict-doctrine |
| Doctrine | attrition banker |
| Target | cumulative T4 (retain) |
| Budget | **one** strategic revision; mechanical/contract repairs free |
| Predecessor | `arena-bots/frontline-labs/classes-wave-1-revision-3-2026-07-29/ledger-fly` (untouched) |
| Primary doctrine cell | `rig` = `--pendulum keel --skills kit --bend universal`, `--movement facing-locked` |
| Resolved ruleset (primary) | `frontline-labs-1-fabricator-vs-fabricator-rig-facing-locked`, rules fingerprint `7a26de3bc5a2953bd8344b9c419e2f67becf683e23319f4d740caac4b8032d48` |
| Author packet | sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rules card | sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| Class addendum | sha256 `b91047df0c0c3e643fd627f45e9f82a0b60b593f986011107125f6ca28c99518` |
| Template helper | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` (carried **byte-identical**; verified by diff) |
| Source-tree sha256 | `e02c5680987e54be168780bccbd4cc2ea9d280e63a787510072f55fa8c5f7753` |
| Toolchain | nilbots CLI 0.9.15, SDK 0.10.6, game rules 0.5, runtime protocol 0.1 / actor 1.0, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, WASI p1 core module, platform-matched Docker builder (Apple Silicon) |
| Build cache key | `f9be04f5ced20ccc21c5d2167959b8cd8f64fd44da770e2c657d5f3e95fd0a23` |
| **`out/bot.wasm` sha256** | **`bdd376cf8dc418316d260e5a4852ebfb097456302188eea720602b85ef2e554f`** (3,445,365 bytes) |
| Qualification | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, WASM, exit **0**, tier **T4**, `balanceEvidenceEligible: true` |
| `evidence/t4/qualification.json` sha256 | `bd48ce587167f62d879b9476b08d11e374d175d987f99107ec8d56dfb62c8c21` |
| T3 prerequisite report sha256 | `3efbfe4af0082c95c102c51f0b62f6cb0cb8cdd913ac93b19b753327c4747ea4` |
| T2 prerequisite report sha256 | `4e4aadf22e30870031c31ea480ab7e064f661d6a6ff21fb52234e79d0a9a839d` |
| Verified probe replays | 36 under `evidence/t4/` |
| Sparring baseline | revision-3 source rebuilt `--no-cache` against SDK 0.10.6, artifact `018c54fba04bbb90dbe7c1e8cc35af339939d87e98450b042e64aadbafda469f` |

Per-file sha256 of the submitted set (the source-tree hash input, sorted):

```text
567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627  ArenaBasics.cs
e60a25eab645a9d977d847051cc60ab386ffe9e271964bc40283f8c80743873d  Bearings.cs
28bdce1328322aa8457ac7a11d7991d9f4f0e856543bbc81f237a6578c80537e  FabricationRoute.cs
867c9fd603ecce39f54f672d5262abd8306e9eeae1076d98f5b26ffe797bd723  Field.cs
d1119a10b8361b8251547d060a436ae41772396c4efc3abfc02758f239e4c1e4  Gunnery.cs
928e9e177546ece72a956873b60cdb18aa6642150063a6066e9b8bbd125505fc  Kinematics.cs
bcd6a4c64a4ac6fd509f9498aa5c5a929c1940c373574dc368dc63e350496f1d  Ledger.cs
c41e4232417ef55cd46543f494a69256e62be1164c57068a1abe2c4f0e3bc005  LedgerFly.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  LedgerFly.csproj
43a2f7518eaca063c9bb22b936944cf5cd5f6f99351abfc0b06a33dbcf79231e  MatchLens.cs
a0424893079948b7145b3e57b972cfd21f4ceb7519a5b9ed9eb530ee98547a14  Ratchet.cs
670eab4a320c8cfce3ee14355c0f63926335398d80d382a3db705f04e26b4f7d  Stances.cs
ca79e974a421862b5860c82d2cca293cfc8eb6b957d105ae76bcc6e876374e71  botarena.json
```

Source-tree hash is sha256 over the sorted submitted files, each contributed as
name, NUL, big-endian 8-byte length, bytes — the same construction as revisions 2
and 3. `Bearings.cs` and `Stances.cs` are new. `ArenaBasics.cs` is the refreshed
scaffold copy. `FabricationRoute.cs`, `Kinematics.cs`, `Ledger.cs`,
`LedgerFly.csproj`, and `botarena.json` are byte-identical to revision 3. Every
suite-5 probe passed on the first canonical build of this revision.

## Doctrine in one paragraph

The bank is still the slot the contract returns automatically, children are still
the currency, and the unit of account is still the convertible objective-tick
that revision 3 established. What revision 4 adds is that **a tick of presence is
worth only what it can keep standing for**, which makes weight a shape rather
than a scalar: bodies choose contested tiles that do not share a clear firing ray
with an ally inside the opposition's own declared reach, avoid lanes something is
already aimed down, and prefer a bearing onto the objective that no ally covers,
because a bolt is a ray that stops on the first body, a fan is three lanes at
once, and a guard's quadrant never tracks. Where the capture definition says only
an enemy standing *alone* erodes a claim, a body that will survive the incoming
bolt — read from the bolt's own `damagePerHit` and exact arrival tick — and whose
removal would flip control eats it and keeps the tile, because dodging converts
"contested, preserved" into "enemy sole, eroding"; the bank is exempt, being the
lowest-health body and the throughput asset. That throughput is the third
reading: slots that only ever fill through an explicit fabrication are pipelines
the bank feeds by hand, so with a deep roster it stands a tile further back,
queues the slowest-rebuilding Ready slot first, and declines the two
*discretionary* reasons to add its own weight — but only where the front carries
a bend envelope and can be trusted with the exchange it is left to fight. The
skill kit is priced on both sides by one contract-shaped definition of a stance —
a same-life route into a form that keeps its objective weight and adds a fan or a
guard — which admits the volley and the aegis and rejects an Anchor into a
zero-weight turret from the very same `transform` action.

## Mechanical and contract repairs (free per the brief)

1. **The hold is now read, not derived.** `ArenaBasics.LiveHold(context)` gives
   the owner and the expiry from the mode observation. Revision 3's derivation
   survives as a fallback for a contract that declares `ratchetHoldTicks` and
   publishes no clock, and `Ratchet.OwnerRead` reports which channel answered.
   **This closes the friction I ranked #2 at revision 3** — a life born inside
   somebody else's hold now reads it exactly as well as one that watched the
   advance.
2. **Class comes from the topology**, not from splitting a form ID:
   `Topology.Teams[].ClassId`, with the participant list as the fallback, plus
   the per-body `classId` on the observation.
3. **Deflected returns need no special case.** Every dodge, block, threat and
   lane test already keys on `OwnerTeamId`, so a team-flipped bolt is hostile by
   construction. I verified this rather than asserting it (see the probes).
4. **`spawnReservation` is honoured.** A tile carrying one of our own pending
   lifecycle claims joins the blocked set: the engine refuses the step anyway,
   and standing there blocks a replacement we paid a combat action for.
5. **Exact bolt arithmetic.** `ArenaBasics.Threat` replaces the two-advance
   reach approximation wherever a *decision* needs a tick rather than a set.

## The one strategic revision, and how much of it is really one

One sentence — *price the objective-tick by what keeps presence alive on it* —
expressed as three readings: shape, blood, throughput. I count it as one because
the three answer the same question about the same tick and each is inert unless
its field is declared: the crowding terms collapse to zero with one companion,
the eat-the-bolt rung requires `decayClock` to preserve contested ticks, and the
throughput terms require a roster with three or more explicit-fabrication slots.
A reviewer who counts "dispersion" and "stand and bleed" as two revisions would
not be wrong, and following the revision-2/3 precedent I would rather say so than
hide it. What I would defend hardest is that the decision *ladder* is
structurally the revision-3 ladder with two new rungs that are inert without the
kit, and that no arm name appears anywhere in the source.

## What I measured

Candidate versus the **rebuilt revision-3 source**, `--classes
fabricator-vs-fabricator`, `--movement facing-locked` on every cell, both sides,
12 seeds per side in-process (2 × 12 × 4 = 96 matches), confirmed on 6 seeds per
side under the controlled WASM runtime. Records are the candidate's and are
resolved by which artifact played the slot: side *a* runs the candidate as team 0
and side *b* as team 1, because the CLI's own total is slot-relative.

| cell | spelled | in-process (12 seeds × 2 sides) | WASM (6 × 2) |
| --- | --- | --- | --- |
| `keel` | `--pendulum keel` | **12W 12L 0D** | 6W 6L 0D |
| `helm` | `+ --skills kit` | **13W 11L 0D** | 6W 6L 0D |
| `veer` | `+ --bend universal` | **24W 0L 0D** | 12W 0L 0D |
| `rig` | `+ kit + bend` | **24W 0L 0D** | 12W 0L 0D |

The in-process and WASM runs agree exactly where they overlap: for every cell at
seed 42 the **entire accepted-decision stream is identical** (432 / 432 / 1349 /
1988 decisions), as are the standings, completion reasons and end ticks. The
replay *hashes* differ, and only because runtime provenance is inside the hashed
header. `nilbots verify` accepts every WASM replay.

### Why the seed counts overstate the evidence

This is the number I most want an evaluator not to be fooled by. Across 12 seeds
there are only **one to three distinct outcomes per (cell, side)**:

```
keel side a  L -30 breach@194 ×8, L -30 breach@312 ×3, L -25 max-ticks ×1
keel side b  W +19 max-ticks ×12
helm side a  L -30 breach@194 ×8, L -30 breach@310 ×3, W +23 max-ticks ×1
helm side b  W  +4 max-ticks ×7, W +21 max-ticks ×5
veer side a  W +30 breach@429 ×12      veer side b  W +30 breach@188 ×12
rig  side a  W +17 max-ticks ×7, W +26 ×5   rig side b  W +30 breach@379 ×7, W +31 max-ticks ×5
```

The per-life random stream only breaks lateral ties, so a seed is very nearly a
no-op. **The honest unit is the eight (cell, side) games, of which the candidate
wins six**, not the 96 matches. `keel` is a pure side effect — team 1 wins all 24
regardless of which artifact holds it — so that row measures the map, not the
revision. `helm` is near-neutral with a genuine seed split on side b.

### Which sentence earns the wins (ablations, 8 seeds × 2 sides)

I did not want to report two 24-0 rows without knowing what produced them, so I
ablated one reading at a time.

| build | `keel` | `helm` | `veer` | `rig` |
| --- | --- | --- | --- | --- |
| full revision 4 | 8W 8L | 8W 8L | **16W 0L** | **16W 0L** |
| bearing crowding removed | 2W 11L 3D | 2W 12L 2D | 8W 8L | 8W 8L |
| throughput repricing removed | 8W 8L | 8W 8L | 16W 0L | 8W 8L |
| eat-the-bolt removed | 8W 8L | 8W 8L | 16W 0L | 16W 0L |

- **Bearing dispersion is the load-bearing reading.** Remove it and *every* cell
  collapses, including the two the throughput knob does not touch. It is also
  the only change that measurably alters the shape of the team: aggregated over
  the 12-match WASM sweep per cell, shared-lane pairs per body-tick are
  candidate **0.231** vs baseline **0.283** on `keel`, **0.337** vs **0.423** on
  `helm`, **0.350** vs **0.428** on `rig` — and the candidate holds more
  objective-weight-ticks in all four cells (e.g. `veer` 2448 vs 1842). In `veer`
  the lane density is a dead heat (0.299 vs 0.300) and the gain shows up purely
  as presence, so the mechanism is not one clean story in every cell.
- **The throughput knob is a knife edge, and I nearly shipped it wrong.** With
  it ungated it turned `rig` from 8-8 into 16-0 **and `helm` from 8-8 into
  0W 15L 1D** — the same lever, opposite signs, one cell apart. I tried five
  settings (each half alone, a stricter field-body threshold, and a tightened
  weight test); every one of them traded one five-slot cell for the other, and
  the tightened-weight variant produced `rig` 0W 16L. The two cells differ *only*
  in the bend envelope, which is a readable contract fact, so the knob is now
  gated on it: a bank may sit out an exchange only where the bodies it is left
  behind carry a curve, because under `facing-locked` the facing *is* the
  movement lane and a straight-only front answers an off-lane contact a tick
  late. **I found that condition by ablation, not by theory, and it remains a
  two-cell fit.** An evaluator should treat it as a hypothesis with a stated
  mechanism, not as a validated one; the honest alternative was to delete the
  knob entirely, which gives 5 of 8 (cell, side) games instead of 6 and loses the
  primary doctrine cell.
- **Eat-the-bolt is unfalsified.** It changed no outcome in any cell. The
  mechanism requires an enemy on the objective, our weight within one of theirs,
  and a survivable bolt on the same tick — a conjunction a mirror produces
  symmetrically, so both sides take the same trade at the same moment and nothing
  separates. It is correct as far as I can tell and it costs nothing measurable;
  I am keeping it because the brief asks the doctrine to price the fact, and I am
  **not** claiming it as a win. Same posture as revision 3's hold clock.

### Skill usage (candidate only, summed per cell over the 12-match WASM sweep)

| cell | attacks | of which bends | fabricate actions | slot-lives fielded | distinct slots fielded | kills | own deaths |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `keel` | 786 | 0 | 111 | 211 | 36 (3/match) | 194 | 193 |
| `helm` | 930 | 0 | 133 | 229 | 48 (4/match) | 206 | 219 |
| `veer` | 846 | **186** | 90 | 168 | 36 (3/match) | 186 | 138 |
| `rig` | 1286 | **425** | 215 | 300 | 56 (4.7/match) | 308 | 261 |

Volleys cast: **0**. Shells raised: **0**. Shells broken: **0**. Not because the
code is absent but because **a fabricator mirror carries neither skill**: on
`fabricator-vs-fabricator` the whole kit *is* five-slots, and `sameLifeTransitions`
comes back empty. `keel`/`veer` field 3 slots per match and `helm`/`rig` field 4
to 5 — the five-slot roster is doing its work — and `bends` is 0 under
`--bend striker-only` and 22 % (`veer`) to 33 % (`rig`) of all attacks under
`universal`, which is the whole visible effect of the bend factor on my own gun.

### So I exercised the stances against my own class variants

Since the measured cells cannot reach the volley or the shell, I built copies of
**my own source** declaring `bulwark` and `striker` and ran them against the
candidate and each other. These are diagnostics, not records, and no other
entrant's artifact was involved.

| probe (rig cell) | casts | raises | drops | deflections | engine auto-returns |
| --- | --- | --- | --- | --- | --- |
| striker mirror | 4 | — | — | 0 | **4** |
| bulwark mirror | — | 38 | 36 | 0 | 0 |
| fabricator vs bulwark | 0 | 7 | 7 | **3** | 0 |
| bulwark vs striker | 7 | 3 | 3 | 0 | **6** |

Traced tick by tick, a cast is: `transform` into the stance → one Wait-only
windup tick → `shoot-straight` (the fan) → engine return on the same tick with
`reason: automatic-threshold-return`. Three real bugs came out of these probes
and are fixed in the frozen source:

1. **A fan entered on a cold gun.** The first version re-entered the stance the
   tick the previous cast returned it and then stood immobile for three ticks of
   cooldown. Now entry requires `cooldown <= windup`.
2. **A guard that never went up.** Raising was gated on standing *on* the
   objective — and that is unreachable. The shell route declares
   `forbiddenTileTags: [transition-placement-forbidden]`, and I measured that
   **all 22 objective tiles on this map carry that tag** (4/4, 4/4, 6/6, 4/4,
   4/4). The arc is legal only *beside* the ground it protects. The gate is now
   "guards an approach", and the mask decides legality.
3. **A guard that thrashed.** Raising on "a shooter is in my arc and my gun is
   cold" and dropping on "no bolt in flight this tick" produced 168 raises and
   156 drops in one 500-tick bulwark mirror. The hold test is now the weaker one
   (anything inside the arc that can still shoot keeps it up) plus a re-raise
   delay of one full round trip, taken from the route's own declared windup. That
   cut it to 38, which I consider acceptable rather than solved — a bulwark
   mirror is a cell my class never plays, so I stopped there and am saying so.

Zero deflections in the bulwark mirror is not a failure: both bots refuse lanes
whose bolt arrives inside a visible arc, so two of my own doctrines simply never
poke each other's faces. The three deflections in fabricator-vs-bulwark are arcs
raised *after* my bolt launched — which is exactly the case the refusal cannot
cover, and exactly the tempo tax the mechanic is for.

### Two readings I implemented and deleted

- **Punishing a shell's break window.** The break is public — the forced return
  carries its reason and the exit plus a fresh entry windup is the window — and
  `Stances` counts deflections per guarding life against the threshold the return
  route declares. I wrote the "rush the broken shell" rung and deleted it: across
  every probe I am permitted to run, **no shield reached three deflections**,
  because refusing arc lanes is strictly better than feeding them and the only
  bolts that land in an arc are accidents. The counter and the break detection
  stay (they cost nothing and gate the deliberate third bolt); the rung that
  consumed the window is gone rather than shipped untested.
- **Standing the bank down on a barren objective.** Same idea revision 3 deleted,
  retried with five slots on the theory that a deeper roster makes the
  precondition reachable. It fired, and it changed no outcome. Deleted again.

## Time

| Step | Wall time |
| --- | --- |
| `dotnet build` of the editing project | 0.9 s |
| in-process match, 500 ticks | ~2.5 s |
| four-cell sweep, 12 seeds, both sides, in-process (8-way parallel) | 78 s |
| four-cell sweep, 6 seeds, both sides, WASM (8-way parallel) | 23 s |
| cold `nilbots build . --no-cache` (warm Docker builder) | 10.2 s |
| full cumulative suite-5 qualification (T2+T3+T4, both assignments, WASM) | 6.6 s wall / 9.3 s CPU |

The inner loop remains excellent, and the parallel-friendly CLI is most of why:
eight processes on separate `--out` directories saturate the machine with no
interference. The WASM sweep being *faster per match* than in-process is worth
noting — the in-process path pays a per-match compile.

## Documentation gaps, frictions, and hardcoding temptations

**1. The observation upgrades landed and they are excellent.** Everything the
brief promised is there and behaves as documented: `holdOwnerTeamId` /
`holdEndsAtTick` travel together and read exactly like `controlResumesAtTick`;
`ticksPerAdvance` and `damagePerHit` are per projectile; `classId` is on self,
allies, enemies and both topology lists; `spawnReservation` is on the tile. My
revision-3 friction #2 is closed, and `Ratchet.cs` lost more code than it gained.
`ArenaBasics.LiveHold` and `ArenaBasics.Threat` are one-liners at the call site.

**2. The aegis shell is documented as holding ground and cannot be raised on any
ground that scores.** The addendum says "objective weight stays **1**, so it
still holds ground", which is true of the weight and misleading in play: the
shell's route forbids `transition-placement-forbidden` tiles, and every objective
tile on this map carries that tag — the same rule that makes Anchor illegal on
the objective. So the shell is an approach plug, never a capture holder, and the
sentence that reads like the class's central promise ("it still holds ground") is
the one that sent me looking for a bug in my own gate. One clause — "like Anchor,
a stance cannot be raised on transition-forbidden tiles, which includes every
objective tile" — would have saved the cycle. It is also a real balance question:
a bulwark cannot shell-hold the objective, so the shell competes with *presence*
rather than reinforcing it.

**3. The refreshed scaffold still recovers class by parsing a form-ID prefix.**
`ArenaBasics.ClassOf` splits `<class>-<role>` and comments that "a typed classId
replaces this helper's body in a later contract generation" — but the typed
`classId` is already published on `Topology.Teams`, `Topology.Participants`, and
every observed body, and the brief explicitly says never to parse form IDs for
class. An author who copies the scaffold and trusts it ships the parse. Either
rewrite the helper against `ClassId` or delete it; leaving a superseded footgun
in the provided starter is worse than not shipping the helper.

**4. One fact, two names, depending on which surface you read.** The bot learns
that a stance returned by itself from `FormTransition.Automatic`, a bool on
optional wire tag 8. Replay v3 serializes the same fact as
`reason: "automatic-threshold-return"` and carries **no boolean at all**. I wrote
my analysis script against the SDK property, got zero automatic returns out of a
match that plainly had four, and spent a while suspecting the bot. Neither the
addendum nor `REPLAY-FORMAT.md` cross-references the two spellings.

**5. Tooling identity, and a silent arm substitution.** The published CLI is
`sandbox/cli-publish/botarena`; the brief, the help text, and every doc say
`nilbots`, and the binary itself reports `nilbots 0.9.15`. Worse than cosmetic:
a raw `.wasm` spec carries no declared class, so `--skills` and `--bend`
correctly refuse ("needs a class pair"), but `--pendulum keel` alone **succeeds**
and quietly resolves the classless base contract. My first WASM confirmation
sweep produced a plausible `keel` row from the wrong ruleset, and the only tell
was that a straight-only cell had logged 317 bends. `--print-candidate-contract`
would have caught it, so: either make a class-declaring project's class survive
into its built artifact, or refuse a class-arm flag combination that a `.wasm`
spec cannot satisfy instead of falling back.

**6. Self-play still cannot A/B a structural or symmetric reading, and this pass
has two of them.** The eat-the-bolt rung and (at revision 3) the hold clock are
both correct, both active, and both unfalsifiable in a mirror for the same
reason: the opponent experiences the identical state at the identical tick. The
two readings I *can* show are the two that change the shape of my team relative
to a team that does not — dispersion, and where the bank stands. A system-owned
non-strategic calibration opponent per class remains the single biggest
measurement gap for an isolated author, and the phase-2 factorial makes it worse:
four of the six class pairs are the ones where the kit actually resolves to
something, and an isolated fabricator author can reach none of them without
authoring the opposition themselves.

**7. Hardcoding temptations resisted.** New this revision: the fan width (from
`volley.projectileCount`, spread over adjacent headings rather than assumed to be
three), the deflection threshold (from the return route's `automaticReturn`, not
the 3 in the prose), the unlock ticks 60/180/300/420 and the 15/30-tick rebuild
clocks (from each slot's own lifecycle assignment and profile — `Pipelines` counts
profiles without an automatic return rather than counting to five), the bend
depth (`maxBendAfterTiles`, which is 1–2 here and 1–4 for a striker), the guard
arc (a facing quadrant computed from headings, checked at *contact* so a bend is
tested where it lands rather than where it launched), and the objective-tile
transition ban (never enumerated — the legality mask is asked). `Standoff`
remains the only tuned constant in the bot.

**8. A note for the cohort, carried from revision 3 and still true.** A frozen
artifact is only frozen against a frozen contract: the revision-3 `.wasm` cannot
decode a schema that gained `holdOwnerTeamId`, and the rebuild from source is the
whole fix. The source-tree hash is the durable identity; the artifact is a cache.

## Top remaining frictions, ranked

1. **The shell's documented promise contradicts its placement rule.** "Objective
   weight stays 1, so it still holds ground" versus a route that forbids every
   objective tile. This is the one gap in this wave that cost real debugging time
   and that changes what the class *is*.
2. **The provided scaffold still parses form-ID prefixes for class** while the
   brief forbids it and the contract publishes the typed field on four different
   surfaces. A superseded footgun in the starter propagates by copy-paste.
3. **No neutral opponent per class, so a mirror cannot test a symmetric
   reading.** Two of this revision's three readings are unfalsifiable in
   self-play, and the cells where the kit resolves to anything are precisely the
   cross-class ones an isolated author cannot legitimately reach.

Runner-up, and the one an orchestrator can fix cheaply: **the cohort directory is
a shared parent of every entrant's output directory**, so the ordinary act of
listing your own freeze enumerates everybody else's (see the disclosure above).
Competitive independence is this experiment's evidence; it should not depend on
authors declining to read a directory they are told to write into.
