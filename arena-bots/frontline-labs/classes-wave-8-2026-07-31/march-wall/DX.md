# march-wall wave 8 — DX report

Lineage `march-wall-v1`, revision 7. Class BULWARK, role `verdict-doctrine`,
target cumulative T4. Written after the source was frozen.

## Isolation statement

Everything in this directory was derived from the permitted material only:
`docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, `docs/FRONTLINE-LABS-RULES.md`,
`docs/FRONTLINE-LABS-BOT-AUTHOR-PACKET.md`,
`docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, public types and XML
documentation in `src/BotArena.Sdk/`, `templates/botarena-generic-actor/`, this
lineage's own frozen wave-6 directory, replays this pass generated, and the
`sandbox/cli-publish/nilbots` CLI.

- The wave-6 source was **copied out** to a private scratch directory before any
  build; the frozen wave-6 tree was never written to.
- Opponent artifacts were used as **artifacts only**. `iron-root` and
  `gate-stone` are sibling bulwarks: their `out/bot.wasm` was played, and their
  source, DX, README and replay evidence were never opened. No sibling directory
  under `arena-bots/frontline-labs/` other than this lineage's own was read.
- `docs/DECISIONS.md`, aggregate balance reports, and Engine/App/Cli source were
  not opened. `src/BotArena.Sdk` was read for public types and XML docs only.
- Private scratch: `sandbox/march-wall-w8-scratch-6d3a91fe/` (uniquely named,
  not a shared or guessable path). Nothing was written outside that directory
  and this output directory.
- **Two things I read that are worth declaring rather than assuming were
  covered.** First, the `botarena.json` manifest beside each baseline artifact —
  name, declared class, SDK version, accent colour — in order to pass the right
  `--classes` for each opponent. That is not source, DX or replay evidence, and
  both sides' declared classes are public in the resolved contract at match start
  anyway, but "artifacts only" does not literally include the manifest and I
  would rather say so than have it inferred. Second, the repository's own
  `CLAUDE.md` agent guide, which the harness injects automatically rather than on
  request; it describes project structure and invariants and carries no doctrine,
  standings or balance content.
- **No accidental exposure occurred**, and nothing inside any sibling lineage's
  directory was opened.
- **Mid-wave platform correction.** An engine defect I hit and reported
  (friction #1) was fixed and republished during this pass. Every number in this
  report was produced by the FIXED build; the pre-fix sweeps were deleted rather
  than merged into it. Spot check on a cell that completed under both builds
  (bastion mirror, seed 104729, side a): identical replay hash
  `ab08393f59df1372…` before and after, so the fix moved only the cells that had
  been aborting.

### Identities

| thing | sha256 |
| --- | --- |
| shipped `out/bot.wasm` | `1b3cff136ce3336ec9d41003ada8ac5d4381f4f3afdf3bb54c9cf95a94984db3` |
| `evidence/t4/qualification.json` | `8ce4f44e8fb99fd19ca4cdf43e450b12e6d674181adea007f656aa00172de6b5` |
| source tree (sha256 of the sorted per-file sha256 list) | `52b465743136b0ac8daf4ed12f0d0dc804f8b83432676ce58db9f91e1be8ca2c` |
| wave-6 self, rebuilt from this lineage's frozen source on SDK 0.10.10 | `033be0a1c3b8eb3edb701f81b7db5d355e384f76a67d19d03ba2fd7364ff9528` |
| wave-6 artifact as frozen in wave 6 (SDK 0.10.4) | `fa364da95eef50bdbd7cc4d008ee20a296fbdde8b678bc16b82754081dc03d2b` |

Per-file source hashes:

```text
a121a6b0959e085ce6daeb343430d7bf5e6e99ec5ee79b633790feefca5d4d50  AnchorPlanner.cs
dfebec45d84c1e7e7a0a71362a3113bfb0bb94e5e61e6124769485618ba3f4b8  ArenaBasics.cs
ed513512a70c066c8846d3db50a610f304e01f87fac3409084fda2f07c47ed40  Channel.cs
6682a546f763bceff29543e3ba33ffaf1283ee2568fc0f1ca490e64738a6af59  Column.cs
361ce6ab61fb68da473bdfbbbeefaba2f797ab48dea1bd856ae67ad1db0ace12  ContractView.cs
974d60836f93f52c598e4c88fd08b60ba583310f4412738f7df777010af01542  Cycle.cs
4aaae221aa46ad4f38645d60d3285d50bbaeae22baaba68d5f2ad774864a0a04  Economy.cs
5497b7c28069d26806cc5e6258e5da52d8f12ac017702a79c9314bf01fe7d87a  FireControl.cs
b4d33b7a1b307a7fa2364bcb3001d123b01d46ed30356f43b89e31ed3098c93f  Geometry.cs
03d5f2c92ddc398e8c547d7e3e991a2cc4cd36d0f196d277bcd375dda543f8cd  Lane.cs
888ba95d4396d7d146f4b112d1fc3ae32dc742be733cb7a66e82165afa99ac2a  MarchWall.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  MarchWall.csproj
82d450631f6993004cb2fecc174519e2ee78f411205af5f5f6a90b80e938962a  Navigation.cs
be9502f662baee0334e730d503e322ec301609ff1df2d61efedb33297d770868  Pendulum.cs
05bd646affab0812ab0e71c4095e518606fdcfe6c54b62120b8223ea19ccad25  Stance.cs
d6b7bcd90d193016b353b669983aaa19048719147ac6428f46f00f47b0158695  Threat.cs
aaa421d25f13adec81dd80c1d5777ba6e32140a16bfca1ba67cbf875a0575c14  botarena.json
```

### Reproduction

Final act, run from the frozen tree in this directory after every source file was
in place:

```bash
nilbots build arena-bots/frontline-labs/classes-wave-8-2026-07-31/march-wall --no-cache
```

`Cache: miss (compiled) · key 4d3ff7ff792392ef71a973a3fb910b10917e4944072221769a4fb211a5969327`.
Both hashes:

- artifact before the verification rebuild:
  `1b3cff136ce3336ec9d41003ada8ac5d4381f4f3afdf3bb54c9cf95a94984db3`
- artifact after the `--no-cache` rebuild:
  `1b3cff136ce3336ec9d41003ada8ac5d4381f4f3afdf3bb54c9cf95a94984db3`

**Identical — the frozen tree reproduces the shipped artifact byte for byte.**

One incidental confirmation worth recording, because it is what lets the
ablation hashes below stand. The last edit to this tree corrected two doc
comments in `Economy.cs` that still described the cut pickup rules. Recompiling
the changed source was a genuine cache MISS and produced a byte-identical
artifact, which demonstrates that comment-only edits do not move the emitted
WASM — so the six ablation artifacts, built before that edit from a source
differing by exactly one property line, are still exactly "the shipped source
with one switch flipped".

Toolchain: `nilbots 0.9.27 (SDK 0.10.10, game rules 0.5, runtime protocol 0.1)`,
compiler NativeAOT-LLVM `10.0.0-rc.1.26306.1`, `--runtime wasm` for every
reported match. `ArenaBasics.cs` is
`templates/botarena-generic-actor/ArenaBasics.cs` synced verbatim
(byte-identical). T4: `frontline-qualification-5`, profile
`frontline-duel-depth-union-t4-v1`, exit 0, every probe PASS, prerequisite T3
rerun and hash-linked.

## Budget ledger

One doctrine pass, as commissioned, plus free mechanical/contract repairs.

| item | spent on |
| --- | --- |
| doctrine pass | six rules: R1 STILLNESS, R2 ESCORT, R3 INTERRUPT FIRE, R4 INVEST, R5 SALVAGE, R6 TEAM DRAW. `Channel.cs` and `Economy.cs` are new files; `MarchWall.cs` gained `HoldTheChannel`, `Escort` and a channel-aware `Prioritized`. |
| repair (free) | `Arguments()` silently dropped an unknown constraint kind and submitted the action anyway — an action missing a required argument rather than a skipped action. `upgrade-track` is the first new parameter kind since that code was written. It now returns null on an unknown constraint. |
| repair (free) | `ContractView` gained `RegionTiles`, `VisionRange`, `OurBestTravel`, `OpposingBestTravel`, `OurVisionRange`; `Geometry` gained `TryBearing`. Pure contract reads. |
| repair (free) | `ArenaBasics.cs` resynced from the template, which is what moves `OrderedDirections` onto `context.TeamRandom`. |
| cut, after measurement | the elected quartermaster and the scheduled deposit run; then the two-tile doorstep detour. R5 no longer moves a body at all. |
| within-pass iteration | six artifacts were built and measured before the shipped one; all are listed below rather than discarded. |

## Per-rule attribution (leave-one-out)

Method: the shipped source with **exactly one** `Rules` property flipped to
`false`, rebuilt through the controlled toolchain, and swept over the same cells
— eight seeds, both team sides. A rule is credited only on cells that COMPLETED
for both artifacts (friction #1), so each figure states its matched-cell count.

Two cells are used because no single cell exercises all six rules. The **siege**
cell (`--capture channel`, no economy) against the wave-6 self isolates the
channel rules; the **bastion** cell (channel + economy) against the `ledger-fly`
fabricator is the only cell measured where matches run long enough for the bank
to reach a tier AND the opponent takes enough ground to be interrupted.

| rule | file | siege vs wave-6 self (16 cells) | bastion vs `ledger-fly` | worth |
| --- | --- | --- | --- | --- |
| **R1 STILLNESS** | `Channel.cs` | **16-0-0 +120 → 8-0-8 ±0** | 10-0-3 → 10-0-3 (13) | **+8 wins, +120** |
| **R2 ESCORT** | `Channel.cs` | 16-0-0 → 16-0-0 | **11-0-4 +88 → 9-0-6 +70** (15) | **+2 wins, +18** |
| **R3 INTERRUPT FIRE** | `Channel.cs` | 16-0-0 → 16-0-0 | 11-0-4 → 11-0-4 (15) | **0** |
| **R4 INVEST** | `Economy.cs` | inert (no economy) | 11-0-4 → 11-0-4 (15) | **0** |
| **R5 SALVAGE** | `Economy.cs` | inert (no economy) | 11-0-4 → 11-0-4 (15) | **0** |
| **R6 TEAM DRAW** | `Navigation.cs` | 16-0-0 → 16-0-0 | 10-0-4 +86 → 9-1-4 +80 (14) | +1 win, +6 |

Ablation artifacts (each is the shipped source with exactly one property flipped):

```text
36eb7261148dd58d038bc993d8d49ddc391a20ca8300a9013e2ac017a15b25a8  Stillness => false
0f804e502f97d547915811c219ef62292bef300b20ff737394fd30c970aefbeb  Escort => false
6bb4f963ebc685e66d84ce947355546f10b49cecef437b2523b3856a1120a6ae  InterruptFire => false
18ee371579e580c5f85d62a2967998eb7e64097e703807434246ffa6c857d5c8  Invest => false
aa0dba0225859bd188ff56ddc0f63c9a807933f7fa0abeabc5415d881c08aebc  Salvage => false
85f51ef9514a33fc9ac420eb41459c052b16e885f9c096c963ad61c4c45fb767  TeamDraw => false
```

**R1 is the revision.** One clause — "while standing on the active objective and
while your not moving is what buys the tick, do not step" — is the whole
difference between beating the wave-6 self 16-0-0 and drawing with it 8-0-8, and
it takes every capture with it: 16 captures to 0 becomes 8 to 8. That is the
right shape for the arm. The channel did not add a capability beside the front,
it changed what standing on the front means, and a doctrine six revisions deep
had four separate well-measured reasons to take exactly the step that is now
fatal.

An earlier candidate measured R1 at **+15 wins and +480 territorial** on the
bastion mirror (15-0-0 against 0-0-15). That number is not in the table because
the shipped artifact cannot be measured on that cell at all (friction #1), and I
would rather quote the 16 cells I can stand behind than the 15 I cannot re-run.

**R3, R4 and R5 measured zero, and each zero says something different.**

- R3 INTERRUPT FIRE reorders fire priority onto the *controlling* enemy's bodies
  on the point, where whole-run revert granularity means one landed bolt undoes a
  capture window. Against the wave-6 self the opponent never completes a claim
  (16 captures to 0), so `TheyControl` is essentially never true. Against
  `ledger-fly` and `spark-line` it does fire — the shipped artifact's own claim
  is reverted 36 and 119 progress in those two sweeps, so the mechanic is
  demonstrably live in both directions — and the outcome does not move. It is
  kept because it is mechanism-true and costs nothing, not because it won
  anything.
- R4 INVEST fires 2 times in 15 cells against `ledger-fly` and 10 times in 16
  against `spark-line`, always choosing `edge`, and always cast by a body with
  nothing else to do with the tick — an anchored turret or a raised shell. That
  is the doctrine working exactly as designed. It buys tier 1 of gun travel
  around ticks 320–400, by which point most cells are decided.
- R5 SALVAGE, after both cuts, is the carrier target-priority ordering and
  nothing else. It cannot move a body, so it cannot cost one; measured, it also
  does not gain one.

**R2 ESCORT and R6 TEAM DRAW are small and real.** Both are worth nothing in the
mirror and something against a fabricator, which is the shape you would expect:
the screen matters when there is a live gun on the point's lane, and a shared
tie-break matters when there are more bodies to disagree about.

## Results

Every match is `--runtime wasm`, `--movement facing-locked`, eight seeds
(`104729, 15485863, 2718281, 31337, 611953, 7919, 86028121, 99991`) plus a
disjoint second set where stated (`1299709, 3141592, 4256233, 5772156, 6180339,
7654321, 8675309, 9999991`), each seed played from **both** team sides. Common
flags: `--pendulum keel --skills kit --bend universal --aim offset
--stance-ground open --cooldown ticking --volley salvo`. `n` is completed cells.

**Distinct outcomes are stated everywhere and they matter.** These bots are
deterministic and the seed feeds only `context.Random` and `context.TeamRandom`;
in a facing-locked mirror neither perturbs the trajectory enough to change a
result. Sixteen cells routinely give sixteen distinct replay hashes and **two**
distinct outcomes, one per side. Read "16-0-0" as two observations replicated
eight times each, not as sixteen.

### Against the wave-6 self

The opponent is this lineage's own frozen wave-6 source rebuilt through the
current toolchain, so this is doctrine against doctrine rather than SDK against
SDK.

| cell | added flags | n | record | territorial | distinct outcomes |
| --- | --- | ---: | --- | ---: | ---: |
| **siege**, seed set A | `--capture channel` | 16 | **16-0-0** | **+120** | 2 |
| **siege**, seed set B (disjoint) | `--capture channel` | 16 | **16-0-0** | **+120** | 2 |
| **forge**, seed set A | `--economy scrap` | 14 | 8-0-6 | +18 | 3 |
| **forge**, seed set B (disjoint) | `--economy scrap` | 13 | 8-0-5 | +23 | 3 |
| **swell** | neither | 16 | 8-0-8 | ±0 | 4 |
| **bastion** | both | **0** | — | — | — |

Read the bottom three rows as the graceful-degradation check and one honest gap.
**swell** carries neither new arm, so every channel- and economy-gated rule is
inert and the only live difference from wave 6 is the team draw: a dead heat that
the team SIDE decides, both artifacts, every cell. That is the intended answer —
on a ruleset without the channel this artifact is revision 6. **forge** carries
the economy without the channel and is likewise side-decided and even, on two
disjoint seed sets; getting it there cost this pass its second cut (below).
**bastion** — the headline cell — could not be measured against the wave-6 self
at all: every cell aborts inside the harness on the remaining engine defect. The
bastion evidence below is against four other artifacts instead.

### Against wave-8 baseline artifacts, bastion cell

MATCHED cells only — the (seed, side) pairs that completed for both artifacts.

| opponent | class | matched | this revision | wave-6 self | delta | distinct outcomes |
| --- | --- | ---: | --- | --- | --- | ---: |
| `ledger-fly` | fabricator | 15 | **11-0-4, +88** | 8-1-6, +55 | **+3 wins, +33** | 8 |
| `spark-line` | fabricator | 16 | 8-0-8, **+23** | 8-0-8, ±0 | +23 territorial | 13 |
| `still-water` | striker | 16 | 8-0-8, ±0 | 8-0-8, ±0 | none — identical cell for cell | 2 |
| `vector-edge` | striker | 16 | 8-0-8, ±0 | 8-0-8, ±0 | none — identical cell for cell | 2 |
| `iron-root` | bulwark (sibling) | 9 | 8-0-1, +120 | 9-0-0, +144 | −1 win, −24 | 3 |
| `gate-stone` | bulwark (sibling) | 15 | 3-0-12, −177 | 2-0-13, −194 | +1 win, +17 | — |

The striker rows are worth stating plainly rather than dressing up: **against
both wave-8-baseline strikers this revision is outcome-identical to its
predecessor, cell for cell.** Those matches end at ticks 66, 92, 98 and 99 —
before the first deposit at 120, and inside a single capture window — and the
side, not the doctrine, decides them. `--capture channel` at threshold 8 makes
bulwark-vs-striker a race that is over before a wall's doctrine engages. The
commission's read that this pairing is an edge race is consistent with what I
see; I could not test it, because the economy never gets to run there.

`gate-stone` beats both artifacts on this cell and beats mine slightly less
badly. `iron-root` is within one cell of even on a nine-cell sample with three
distinct outcomes; I would not read a doctrine claim into it either way.

### Channel and economy usage

From the shipped artifact's own decision debug strings and the replay mode state.

| cell | channel-hold ticks | screen ticks | gain ticks | own claim reverted | captures for/against | invest casts | tiers bought | scrap banked |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| vs wave-6, siege A (16) | 200 | 16 | 80 | 0 | 16 / 0 | — | — | — |
| vs wave-6, forge A (14) | 0 | 0 | 558 | 14 | 22 / 20 | **14** | 14 × edge | 238 |
| vs `ledger-fly` (15) | 609 | 34 | 527 | 9 (−36) | 50 / 39 | **2** | 2 × edge | 88 |
| vs `spark-line` (16) | 1033 | 19 | 1159 | 32 (−119) | 84 / 81 | **10** | 8 × edge | 163 |
| vs `iron-root` (9) | 223 | 32 | 221 | 5 (−10) | 21 / 5 | 1 | 1 × edge | 28 |
| vs `gate-stone` (15) | 209 | 65 | 319 | 1 (−4) | 28 / 47 | 1 | 1 × edge | 66 |

Every investment this artifact has ever made bought `edge`, and that is the
contract-driven ranking working rather than a preference: a fabricator declares
travel 7 and a striker 8 against the bulwark's 6, so the travel track is the only
one closing a gap that decides who opens an exchange. On the forge cell (no
channel, 500-tick matches) the artifact reaches a tier in **every** match; on the
channel cells it reaches one in roughly one match in three, because threshold 8
ends matches sooner. Scrap banked runs 3–15 per cell and is almost entirely the
assay paid at the tile — the wave-6 artifact banks the same order passively by
walking over wrecks it is not looking for, which is the brief's own point.

### Intermediate artifacts

Retained rather than overwritten, in build order. All records are against the
wave-6 self on the cell named.

```text
9a61f0281093a448d8503c25ab2c3faa4036ae6647c636b8e69458a1166e8db5  bastion  7-0-7  (14 cells, pre-fix engine)
a8d41abdb14779d436e7baafd8d8e684a00ad978719f65c4317cdd9a77208697  bastion 16-0-0  (16 cells, pre-fix)
fd02d3dc1fe78a533cf84da68763444061442856f1a15c5ba39b485473b37fe3  bastion  0-0-9  ( 9 cells, pre-fix)
ecebb2b192be7e7afdf81d1677876c4a4ab8426bdd0296946326eb8b5e8f1afb  bastion  0-0-9  ( 9 cells, pre-fix; behaviour-identical control for the above)
202585a84176da8e6d25881c8ec8d6810af66f1996f09a4d9036c7af7824dcf6  bastion 15-0-0  (15 cells, pre-fix)
910d281c7e98d8c9bb333ea5ce06208b86921c259da3d7b2bd7c41e5d17579da  bastion 16-0-0  (16 cells, post-fix) · forge 0-1-13 (14 cells, post-fix)
1b3cff136ce3336ec9d41003ada8ac5d4381f4f3afdf3bb54c9cf95a94984db3  SHIPPED
```

Three of those are the pass's real findings, and all three were my own mistakes.

- **9a61f028 → a8d41abd.** The first integration let any body detour up to two
  tiles for any pile. Under the channel that is a body walking BACKWARDS for one
  scrap: 550 decision ticks across fourteen cells spent on "taking the scrap
  beside us" against 92 scrap banked. Adding one clause — a doorstep pile may not
  be further from the contested objective than we already are — turned 7-0-7 into
  16-0-0. The rule was right; it was pointed the wrong way.
- **a8d41abd → fd02d3dc, then cut.** An elected quartermaster with a prize
  threshold and a committed target is the *correct* reading of a public deposit
  metronome — 48 scrap in a fixed pot, addresses and due ticks readable before
  tick zero — and it went 0-0-9 and lost the fabricator cells it existed to fund.
  The trace is unambiguous: the elected body leaves for a six-scrap vein around
  tick 80–120, the front runs a body light, and under the channel two moving
  defenders hold three still attackers. Cutting it restored 15-0-0.
- **910d281c → 1b3cff13, and this is the one I nearly shipped wrong.** The
  doorstep detour survived the first cut and looked harmless: two tiles, never
  backwards, never from the point's only defender. On the **forge** cell — the
  economy WITHOUT the channel — it went **0-1-13, −390 territorial** against the
  wave-6 self. Removing the detour entirely made the same source **8-0-6, +18**,
  confirmed at 8-0-5, +23 on a disjoint seed set. The mechanism is the
  embarrassing part and is worth writing down for whoever authors next: **on a
  channelling ruleset the stillness gate sits above salvage in the ladder and
  takes the tick before salvage can spend it.** A rule that trades ground for
  currency therefore looked safe for exactly as long as another rule was covering
  for it, and the moment the channel was absent it ran unopposed. I would not
  have found it without a cell that carries one arm and not the other; "do not
  overfit to bastion" turned out to be load-bearing advice.

## Friction

Ordered by what it cost this pass.

**1. Two engine-side replay-validation aborts. One was fixed mid-wave; one is
still live and cost me my headline cell.** Both are fatal to the match and leave
no replay:

```text
error: A retained projectile must preserve its exact resolved committed path. (Parameter 'projectiles')   [FIXED mid-wave]
error: A returned projectile is launched on the deflection tick with a fresh travel budget. (Parameter 'ticks')   [still live]
```

A bot cannot author a projectile path, so these are the harness rejecting a
history the engine itself produced. The first was fixed and republished during
this pass and I re-ran every sweep on the fixed build. The second remains, it is
about DEFLECTION, and this class raises the AEGIS SHELL more than any other — the
shipped artifact aborts **16 of 16** cells of the bastion mirror against the
wave-6 self, on both seed sets, which is precisely the comparison the commission
asked for. Minimal repro:

```bash
nilbots experiment frontline-labs \
  --bot <this>/out/bot.wasm --opponent <wave-6 rebuild>/out/bot.wasm \
  --classes bulwark-vs-bulwark --movement facing-locked \
  --pendulum keel --skills kit --bend universal --aim offset \
  --stance-ground open --cooldown ticking --volley salvo \
  --capture channel --economy scrap --seed 104729 --runtime wasm --out /tmp/repro
```

The same cell with the wave-6 artifact on both sides completes normally, so it is
trajectory-dependent rather than cell-dependent. Across the shipped artifact's
final sweeps 30 cells aborted this way. Two operational notes for whoever fixes
it: the abort is **deterministic per cell** (re-running the same seed reproduces
it exactly), and — as the platform notice separately confirmed — some abort paths
exit 0 with the error on stdout, so a harness that trusts return codes will score
an aborted cell as a completed one. Mine counts replay files, which is why every
table above states `n` separately from the seed count.

**2. Threshold 8 makes whole pairings unmeasurable for a wall.** Every
bulwark-vs-striker bastion cell ended at ticks 66–99, before the first deposit at
120. The economy and the channel are documented as composing into "the shipped
game", but on the pairing where the economy should matter most — edge 6→8 erasing
the striker standoff — the match is over before a bank reaches one tier. No bot
can currently demonstrate that interaction, in either direction.

**3. Seeds are nearly inert, so seed count is not evidence.** Sixteen cells give
sixteen distinct replay hashes and two distinct outcomes against the wave-6 self:
identical end tick, identical winner, identical score, per side. The only
seed-fed inputs are `context.Random` and `context.TeamRandom`. Every table here
states its distinct-outcome count for that reason; a report that says "eight
seeds" without it is reporting one observation eight times. The corollary bit me:
a single guard clause flipped a bastion cell from 16-0-0 to 3-0-10, which is not
"the guard is bad" so much as "this matchup is a knife edge with two
observations on it".

**4. `ArenaBasics.Capture` stops short of the channel.** The shared helper record
`CaptureRules` surfaces threshold, gain, decay, redeploy, hold,
`SurplusWeightScalesGain` and `OnlyEnemySolePresenceDecays` — and none of
`StationaryGainMultiplierCap`, `OpposingErosionMultiplier` or `ClaimInterrupt`.
Those are exactly the three fields the arm's brief tells you to branch on, so
every author on this cell writes the same lines against `FrontlineGameMode.Capture`
directly. The helper is otherwise excellent — it is why the pendulum needed no
re-authoring at all — and this is a gap in it rather than a complaint about it.

**5. The blind-argument fallback pattern is a trap the new verb springs.** The
scaffold-derived "iterate the constraints, take the first allowed value,
`default: break`" fallback silently DROPS a constraint kind it does not know and
submits the action anyway — an action missing a required argument rather than a
skipped action. `upgrade-track` is the first new parameter kind since that code
was written, so it is the first one that could have hit it. Repaired here by
returning null on an unknown constraint, which is the only safe default. Worth
fixing in the template before the next kind lands.

**6. `PublicInitialLife` is a top-level SDK type.** Its siblings in the same
contract — `InitialLifeDeployment`, `LifecycleAssignment`, `InitialSpawn`,
`FrontlineModeMapBinding`, `ParticipantRegionAssignment` — are all nested inside
`GenericActorResolvedMatchContract`. Two compile errors guessing the qualified
name of the one that is not.

**7. No `invest` decision helper.** `Actions`/`ArenaBasics` carry helpers for
waiting, dodging, shooting, fabricating and advancing; the new verb is assembled
by hand from `ActionKind.ModeInvestment` + `UpgradeTrackConstraint` +
`UpgradeTrackArgument`. Contract-driven assembly is the right habit and I would
not want a helper that hides the mask, but the asymmetry is noticeable.

**8. The registered composite names in the brief are striker names.** The
commission and the brief call the four cells swell / siege / forge / bastion.
With no striker in the cell the salvo arm is inert-omitted, so a bulwark mirror
resolves to `sail-open`, `mantlet`, `forge` and `smithy` instead. The CLI does
print "requested skills without an owning class in this cell change no contract
bytes and are dropped", which is a good line — but I still read a replay header
to be sure which ruleset I was measuring. A one-line "resolved identity: …" echo
on the run banner would settle it.

**9. The brief implies an enemy's route cooldowns are readable. They are not.**
`--volley salvo` prices the fan on an 8-tick ENTRY cooldown and says the
stack-versus-screen decision is "a read on published state — `routeCooldowns`
publishes exactly that". `self` and every ally publish theirs;
`ObservedEnemyState` carries none. So "is their fan off cooldown" is not
answerable, and the escort rule has to fall back to "is any hostile gun's
declared travel covering the point". Either the field should appear on visible
enemies or the sentence should not promise it.

**10. One transient host-side hang.** One CLI invocation in roughly 200 produced
no output and no error and had to be killed (`Terminated: 15`); re-running the
identical command completed normally and that replay is in the evidence. Not
reproducible; mentioned only so the count is honest.
