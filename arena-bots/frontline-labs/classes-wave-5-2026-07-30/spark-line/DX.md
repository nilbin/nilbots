# DX notes — spark-line revision 5 (fabricator, `spark-line-v1`)

Population: Frontline Labs classes wave 5, revision 5. Role: verdict-doctrine,
target cumulative T4. Frozen revisions 1–4 live untouched at
`arena-bots/frontline-labs/classes-wave-1-2026-07-29/spark-line/`,
`.../classes-wave-1-revision-2-2026-07-29/spark-line/`,
`.../classes-wave-1-revision-3-2026-07-29/spark-line/` and
`.../classes-wave-4-2026-07-30/spark-line/`; their DX notes are preserved there
and are not restated except where a friction changed status.

**Isolation statement.** This pass read only the wave-5 brief and its one
mid-wave correction, the author packet
(`FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aa…`),
`FRONTLINE-LABS-RULES.md` (`06ff461e…`), `EXPERIMENTAL-FRONTLINE-CLASSES.md`
(`2333bd3c…`), `templates/botarena-generic-actor/`, `src/BotArena.Sdk/` (types
and XML docs), this entrant's own frozen wave-4 directory, replays this entrant
produced in this session, and the CLI at `sandbox/cli-publish/`. No other
entrant's source, no standings, no aggregate balance report, no engine or App
implementation, no non-assigned replay. Every sparring opponent was this
entrant's own wave-4 predecessor rebuilt from source, or a single-rule variant of
this entrant's own source. Scratch was a uniquely named private directory
(`sandbox/spark-line-w5-scratch-e4d197b2/`), never a shared or guessable one.
Nothing was committed to git. No accidental exposure to disclose.

One coordinator correction arrived mid-pass and is recorded because it changes
what an opponent model may assume: the class-identity paragraph's "Mobilize back
once per life" is HISTORICAL. On this arm both `mobilize` routes publish
`irreversibleForLife: false`, there is no `flatHealthGain`, and health maps
`preserve-ratio-floor-minimum-one` in both directions — verified in the resolved
contract of `frontline-labs-1-bulwark-vs-fabricator-deck-facing-locked`, not
taken on trust. This policy reads no reversibility field anywhere, so nothing had
cached the old rule; the consequence it does carry is stated in friction #3.

## Freeze identity

| Item | Value |
| --- | --- |
| Entrant | `spark-line` (entry type `SparkLine`), revision 5 |
| Class | `fabricator` (declared in `botarena.json`, unchanged since revision 1) |
| Canonical artifact | `out/bot.wasm` |
| **bot.wasm SHA-256** | `e725e2e5dadb75cbd034f040e27f574a483dd5e169036bd95c17f3f749ac169d` |
| Build | `build --no-cache`, cache miss, compiled; key `5900de9f680723760f017100d55abc7c1a793f4862cbff056cdb46796e7a1766`; four independent `--no-cache` builds reproduced the same artifact hash, two of them from a differently-commented source tree — comments change the cache key and not the codegen, so the artifact measured in the ablation IS the artifact frozen here |
| Builder | CLI 0.9.21, SDK 0.10.6, game rules 0.5, NativeAOT-LLVM 10.0.0-rc.1.26306.1, wasi-wasm p1 core module, platform-matched Docker builder |
| Qualification | `experiment frontline-labs qualify --suite frontline-qualification-5` → **exit 0**, tier **T4**, `passed: true`, `balanceEvidenceEligible: true`, all five probes PASS |
| Report | `evidence/t4/qualification.json`, sha256 `9ed9004b20b255d5ac96f4df44b5b0eef4019d4c1ecece1a9db96502afdd99d8` |
| Hash-linked T3 report | `0ba198c36c1df08f912d80976c628e0a22cc8dd149ec7d834d4af38e5f6e15f5` |
| Hash-linked T2 report | `2eb0a7d6984f63a01710bcb48f647d18c7db52e6a65f1c460a1c7b967c681ef9` |
| Source-tree hash | `cf2699fd4b5e95f28cf82ecc63b9f2fb4ad21dfe155cfd71213dab08e68377d9` |
| Submitted source | `SparkLine.cs` (3226 lines), `ContractLens.cs` (627), `Tactics.cs` (394), `ArenaBasics.cs` (1205, template verbatim) |
| Sparring baseline | wave-4 source **rebuilt** on this SDK, `--no-cache` → `a43e01104afee497909c45195d5e22232e1cc05edd8566181005ef8bda93e3cc` |
| Wave 4 **as frozen** | `b5c328875993fd69f2b8d5ba7ca54eb91da1feb26b3a69d6cbb9ea76d3861f4a` — not used for any measurement, per the wave rule that frozen wave-4 artifacts fault on the crew contracts |
| Cited replays | `evidence/crew/crew-fvf-{a,b}-s104729.json`, both `verify`-clean (`d13c7799…`, `dcd86420…`) |

Per-file SHA-256:

```text
567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627  ArenaBasics.cs
0ecb427c420ec0cbe82a2431742cd4cbfc159e3ee0b4a8c96730a5b9a94bb94e  ContractLens.cs
cf7c8f8967b2b3fff70b66621e6d813083851ea5242c989a68579fc4c8ff2eac  README.md
f77dcd052e04ccd5d6d252526b34e5866a32a1adaa603a9a044324da130b0de0  SparkLine.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  SparkLine.csproj
049b42b422c704c48a825205edcf7cb1c2acf9974a13bee33a6b30e7c74bd4b9  Tactics.cs
340a566bf2a177dd4b1b81f2e74dae80aabb106b1d8e70acf98ef19c362c9c95  botarena.json
```

The same list plus the artifact hash is in `SHA256SUMS.txt`.
`ContractLens.cs`, `Tactics.cs`, `ArenaBasics.cs`, `botarena.json` and the csproj
are **byte-identical to revision 4**; the whole revision is in `SparkLine.cs`,
+109 lines, and stripped of comments it is one new helper (`PoseScore`) and two
edits inside `TryAim` — the gate expression and the per-facing score call.
`ArenaBasics.cs` is the current template verbatim (unchanged since wave 4).

Resolved identities this freeze was measured on:

| pair | ruleset ID | topology profile |
| --- | --- | --- |
| `fabricator-vs-fabricator` | `frontline-labs-1-fabricator-vs-fabricator-crew-facing-locked` | `…-four-slots-v1` |
| `bulwark-vs-fabricator` | `frontline-labs-1-bulwark-vs-fabricator-deck-facing-locked` | `…-asymmetric-slots-4-3-v1` |
| `fabricator-vs-striker` | `frontline-labs-1-fabricator-vs-striker-deck-facing-locked` | `…-asymmetric-slots-4-3-v1` |
| `bulwark-vs-striker` | `frontline-labs-1-bulwark-vs-striker-sail-open-facing-locked` | `…-three-slots-v1` |
| `bulwark-vs-bulwark` | `frontline-labs-1-bulwark-vs-bulwark-sail-open-facing-locked` | `…-three-slots-v1` |
| `striker-vs-striker` | `frontline-labs-1-striker-vs-striker-sail-open-facing-locked` | `…-three-slots-v1` |

`open` is inert on a fabricator mirror — the chassis declares no same-life
routes at all, so there is no transform placement for it to free — and the
identity says so by resolving to `crew` rather than `deck`. The same flag set
produced every row.

## Doctrine, in one paragraph

The currency is still surviving objective weight and the branch is still chosen
by the shot envelope — **concentrate what the gun can defend, spread what it
cannot** — but revision 5 is about what "the gun" now means: with ±45° launch
offsets restored, a bolt may leave at −1/0/+1 sectors off facing with zero bends,
so a POSE is a three-bearing fan rather than a lane, and every place that priced
a pose by its straight lane was pricing a third of the weapon. The correction is
one number, `PoseScore`, the best trajectory the whole declared envelope can put
on a visible enemy from a tile and a facing, computed from
`minInitialAimSteps`/`maxInitialAimSteps`, the aim-only program, the allowed bend
directions and depths, and the projectile's own travel and corner rule — so on a
contract that declares no offsets it enumerates exactly the default program and
the artifact is decision-for-decision revision 4. It is spent in exactly one
place, choosing which way to turn, because the facing whose straight lane misses
and whose diagonal launch hits is invisible to a straight-lane scorer and is
therefore the facing a swarm needs to envelop inward; and the second line of the
revision stops asking a hard-coded three tiles whether aiming is worth a tick and
asks the gun's own declared travel instead, since a rotation completes within the
tick and the facing chosen now is the facing the next bolt leaves on. Everything
else the tuned arm invited — inferring the enemy's hidden objective weight from
the capture arithmetic, charging a facing-locked route for its rotations,
refusing ground to a body the occupier election did not send, and giving the aim
step a stand-still baseline — was implemented, measured on its own, and rejected
as a paired-progress loss, which is the finding underneath the freeze: the slot
and rebuild economy is read entirely from lifecycle assignments and profiles and
appears nowhere as a literal, so the tuning that made bodies dearer changed the
price list without changing the doctrine, and the thing that changed the game was
the gun.

## Measured records — candidate vs rebuilt wave-4 predecessor, the crew game

Method: both artifacts built from source with `--no-cache` on the same SDK; the
brief's flag set (`--movement facing-locked --pendulum keel --skills kit --bend
universal --aim offset --stance-ground open`, plus `--five-slots wane` on every
pair containing a fabricator); **six seeds** (104729, 130363, 155921, 181081,
206699, 232391), **both sides**, controlled WASM runtime. 12 matches per row.

Side accounting is keyed on `header.provenance.participants[].artifactHash`, not
on participant ID. The headline statistic is the **paired edge**: the sum over
both sides of a seed of (candidate signed territorial progress − predecessor's).
Its ceiling is +120 — a breach from both sides is ±30 each, so 60 per side.

| pair | ruleset | W-L-D | paired edge / seed | per-seed edges | seeds +/−/0 |
| --- | --- | --- | --- | --- | --- |
| **`fabricator-vs-fabricator`** (my class, primary) | `crew` | **12-0-0** | **+120.0** (ceiling) | 120 ×6 | 6 / 0 / 0 |
| `bulwark-vs-fabricator` | `deck` | 6-6-0 | **+46.0** | 42, 46, 42, 58, 42, 46 | 6 / 0 / 0 |
| `bulwark-vs-striker` | `sail-open` | 6-6-0 | +2.0 | 2 ×6 | 6 / 0 / 0 |
| `fabricator-vs-striker` | `deck` | 6-6-0 | +1.0 | 0, 0, 14, 0, 0, −8 | 1 / 1 / 4 |
| `bulwark-vs-bulwark` | `sail-open` | 2-10-0 | **−33.3** | −32, −64, −54, −84, 16, 18 | 2 / 4 / 0 |
| `striker-vs-striker` | `sail-open` | 4-8-0 | **−49.7** | −36, −102, −4, −66, −14, −76 | 0 / 6 / 0 |

The primary row was re-run on six **disjoint** seeds (7, 4243, 60013, 99991,
314159, 777767) and reproduced **12-0-0 at +120.0** exactly: 24 matches, 24 wins,
every one a base breach — as team 0 at tick 345 and as team 1 at tick 142. Sign
test on twelve independent seeds is one-sided p ≈ 0.0002.

Read the last two rows for what they are. A fabricator-declared entrant is bound
to its class's canonical team side, so a bulwark mirror and a striker mirror are
runs of **this artifact on a chassis it does not own** — robustness probes, not
competitive cells. They are a real regression in that hypothetical and the cause
is legible: the shipped aim gate is the *gun's* declared travel, and a striker's
gun travels 8 with 1–4 bend depth while a bulwark's travels 6 on a 3-tick
cooldown, so the same rule buys a differently-priced tick there. A chassis-general
attempt to fix it (also require the gun to be within one tick of loaded,
`cooldown <= 1`) was measured and is **worse everywhere** — `fabricator-vs-
fabricator` +112.0, `bulwark-vs-fabricator` **−36.0**, `bulwark-vs-bulwark`
−106.0 — so it is not shipped and the honest statement is that this revision is
tuned to a 7-tile, one-bend, ±1-offset gun.

Runtime health across the six pairs (72 matches): **zero runtime faults, zero
rejected actions, zero invalid shot programs** in **80,347 controlled-runtime
decisions** (both artifacts; 40,557 of them the candidate's), including every
off-class path — volley stances entered and cast, aegis shells raised and
deflecting, and 8,002 turret anchor offers refused. Blocked outcomes on the
primary arm are 66 of 3,840 candidate decisions (1.7 %), the ordinary
joint-resolution rate for a doctrine that walks bodies into contested tiles.

### Skill, diagonal and slot usage (candidate / predecessor, both sides, 12 matches per pair)

| pair | shots | diagonal launches | bends | volleys cast | shells raised | deflections taken | turret anchors | slots fielded |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `fabricator-vs-fabricator` | 414 / 372 | **120 / 90** | 90 / 108 | — | — | — | — | 3.0 / 3.0 of 4 |
| `bulwark-vs-fabricator` | 976 / 821 | **238 / 209** | 234 / 142 | 0 / 0 | **15 / 48** | 19 / 72 | **0 / 0** | 3.5 / 3.4 |
| `fabricator-vs-striker` | 705 / 736 | 168 / 186 | 143 / 166 | **105 / 123** | — | — | — | 3.3 / 3.4 |
| `bulwark-vs-striker` | 858 / 684 | **174 / 132** | 114 / 84 | **180 / 54** | 6 / 36 | 6 / 36 | **0 / 0** | 3.0 / 3.0 of 3 |
| `bulwark-vs-bulwark` | 1127 / 1082 | **333 / 260** | 283 / 263 | — | 83 / 97 | 116 / 112 | **0 / 0** | 3.0 / 3.0 |
| `striker-vs-striker` | 1249 / 1240 | 102 / 106 | 122 / 118 | **543 / 495** | — | — | — | 3.0 / 3.0 |

Diagonal launches are **29 %** of the candidate's shots on its own class arm, up
from 24 %, and the ratio rises on every pair where the candidate also shoots more
often. Bends fall as diagonals rise, which is the mechanism stated as a number:
an aim-only diagonal reaches a bearing that previously needed a bend, and the
bend is the more fragile trajectory because it commits to two headings.

**Decline discipline, counted rather than asserted.** Every stance and anchor
offer in the legality mask was counted against what was actually submitted:

| pair | shell offered / raised / declined | volley offered / entered / declined | turret offered / anchored / declined |
| --- | --- | --- | --- |
| `bulwark-vs-fabricator` | 3,989 / 15 / 3,974 | — | 3,989 / **0** / 3,989 |
| `bulwark-vs-striker` | 1,950 / 6 / 1,944 | 3,114 / 78 / 3,036 | 1,950 / **0** / 1,950 |
| `bulwark-vs-bulwark` | 8,002 / 83 / 7,919 | — | 8,002 / **0** / 8,002 |
| `fabricator-vs-striker` | — | 2,347 / 64 / 2,283 | — |
| `striker-vs-striker` | — | 6,064 / 208 / 5,856 | — |

The turret column is the one worth reading. On this arm an anchor is legal on
objective tiles and the cycle is unlimited, so the class's central bargain is
offered to this policy **8,002 times in the bulwark mirror alone and refused
every time** — not by a special case but by one line that compares the target
form's `objectiveWeight` to the body's own and refuses to trade scoring presence
for durability. A turret on a point still scores nothing; that the route is now
cheap to reverse does not make the weight come back while you are standing in it.

**The aegis shell is finally reachable, and that is a doc change paying off.**
Revision 4's DX friction #2 recorded that every objective tile carries
`transition-placement-forbidden` and both shell routes declared that tag, so a
stance whose whole purpose is holding ground could not be raised on any ground.
On this arm the entry route's `placement.forbiddenTileTags` is **empty** — read,
not assumed — and the same unchanged entry condition now raises 15–83 shells per
pair and takes 6–116 deflections. Nothing in the policy was edited to achieve
that; the ground arm did it.

## The revision, and the ablation that chose it

Every rule below was implemented alone on top of the rebuilt predecessor's source
and measured on the same 6-seed, both-sides, 12-match matrix against that
predecessor, on `fabricator-vs-fabricator` under the crew game. One artifact per
rule; all seventeen artifact hashes are listed in
`evidence/ablation/ARTIFACT-HASHES.txt` and every rejected rule's exact source
patch is in `evidence/ablation/mkvariants.py`, so any row can be rebuilt from the
predecessor's source and re-run. The variant trees themselves were deleted rather
than archived, because a 3.2 MB artifact per row is not worth keeping when the
patch that produces it is 40 lines and the build is deterministic.

| # | rule, stated as what it reads from the contract | artifact | paired edge / seed | W-L-D |
| --- | --- | --- | --- | --- |
| 1 | implied enemy weight: invert `controlPolicy` + `claimingTeamId` into a lower bound on the objective weight a facing quadrant cannot see | `122202be` | **−36.2** | 1-10-1 |
| 2 | facing-locked step economy: charge a route the rotation its `facingCoupling` requires, then amortise it over the run | `a6080472` | **−23.5** | 3-9-0 |
| 3 | elected entry: refuse objective presence to a body the occupier election did not send | `27de6317` | −1.7 | 3-7-2 |
| 4 | first contact: a bolt stops on the first enemy body, so credit that body and stop walking the path | `c0543983` | −1.7 | 5-6-1 |
| 5 | aim stand-still baseline: rotate only on a strict improvement over the current facing | `afc2c25d` | −5.0 | 5-5-2 |
| 6 | **aim envelope: price a candidate facing by the whole declared shot envelope** | `e5252e66` | **+85.3** | **12-0-0** |
| 7 | 6 + stand-still baseline | `fe7df65e` | +48.3 | 9-3-0 |
| 8 | 6 + the same envelope pricing inside the stalemate-breaker, with a baseline | `b8822c18` | 0.0 † | 6-6-0 |
| 9 | **6 + the aim gate is the gun's declared travel (SHIPPED)** | `e725e2e5` | **+120.0** | **12-0-0** |

† Row 8's zero is not "no change": it turned **every one of the twelve matches
into a base breach at tick 192–193**, symmetric between the sides, so the paired
statistic cancels exactly while the variance goes to its maximum and row 6's edge
is destroyed. A zero paired edge with a 100 % breach rate and a zero paired edge
with a 100 % tick-cap rate are different artifacts, and only the W-L-D column and
the completion reasons distinguish them. That is the measurement lesson of this
pass and it is friction #1.

Rows measured **against row 6** rather than against the predecessor, because once
row 6 wins 12-0-0 at the ceiling the predecessor can no longer discriminate:

| composed on top of row 6 | artifact | paired edge / seed vs row 6 |
| --- | --- | --- |
| + elected entry (#3) | `ae6b13dc` | **−50.3** |
| + envelope in the stalemate-breaker, no baseline | `99e72f70` | **−24.3** |
| + implied enemy weight (#1) | `5ebdda86` | −11.7 |
| + **aim gate is the gun (SHIPPED)** | `e725e2e5` | +1.7 |
| + first contact (#4) | `208e6937` | 0.0, decision counts identical |
| + facing-locked step economy (#2) | `4ec2e2ad` | 0.0 |
| + stop penalising an aim offset like a bend | `c5768105` | 0.0, decision counts identical |

Only one rule survives, and it is the smallest one on the list.

### What did not work, and why each failure was informative

**1. The implied enemy weight (−36.2 alone, −11.7 on top of the aim fix) — and
the discovery that revision 4's headline was nearly inert.** Revision 4's central
number was "hold the objective with the enemy's weight plus one", and it took the
enemy's weight from `Enemies`, which is a *facing quadrant*: this chassis declares
vision shape `facing-quadrant` range 6 with omnidirectional proximity 1, so a
body on a six-tile region routinely sees none of the weight sharing it. The
observation, however, states the RESULT of the weight comparison every tick
through fog: under `controlPolicy` = net-positive-weight-scales-gain, nobody
accumulating while I stand there with positive weight means my net is not
positive, so the enemy has **at least** my weight — and a fresh life can read that
on its first tick, which matters for a class whose bodies are all fresh lives.
The bound is correct. Feeding it to the weight target loses badly, and measuring
why is the useful part: mean own weight on the objective went **down** (0.49 from
0.60) and deaths **up**, because the bound ratchets by one body per tick and
therefore feeds bodies into a contested region one at a time. The corollary is
the finding I would most want another author to have: **revision 4's weight
target was measured as a win while being almost never active**, because its
estimator read zero. A rule whose sensor is blind is not the same rule as the
rule it looks like, and the wave-4 result attributed +89.7 to something that
mostly did not fire.

**2. The facing-locked step economy (−23.5).** Under `facing-locked` the movement
mask offers exactly one direction, so a step along the current facing costs one
tick and any other costs two — and since the distance field is a cardinal
breadth-first search, every candidate step shortens it by exactly one, which
makes the tie purely a question of price. Preferring the faced bearing and then
the longest unbroken run cut "turning to walk" rotations and lost anyway. The
reason is worth stating: **a rotation is not only a tax, it is a heading change.**
The cheapest route is the straightest route, the straightest route follows the
corridors, and `ExposureAt` says the corridors are exactly where the firing lines
are. The predecessor's zig-zag is not waste; it is dodging, and the ±45° envelope
makes that more true rather than less because a straight walker is inside three
bearings of every body it passes.

**3. Elected entry (−1.7 alone, −50.3 on top of the aim fix), and the prime
problem it correctly diagnosed but wrongly treated.** Diagnostics on the
predecessor's own mirror are unambiguous: the fabrication source — the 2-health
form that is the only catalog entry able to start a fabrication route — spends
**28.5 %** of its body-ticks standing on the active region and takes **53.6 %** of
its deaths there, is dead **44 %** of the match on an 18-tick automatic return,
and its child slots sit `availability-pending` 49 % / 60 % / 79 % of the time on
their 22- and 30-tick rebuild clocks while `ready` 9 % / 5 % / 3 %. The team
fields **1.39 of 4 bodies** on average. `TryEnterObjective` grabs presence from
any body that steps past a region tile, which silently overrules the occupier
ranking computed one method earlier — so gating entry on the election looks like
a pure repair. It buys the survival it promises (body-ticks up) and loses the
ground, badly once the aim fix is also present. Presence taken late is still
presence, and under net-weight capture a body on the tile now beats a healthier
body two tiles away. This is the same shape as revision 4's rejected
rebuild-clock term and it is recorded in the code where the term would have gone.

**4. The aim stand-still baseline (−5.0 alone; it costs row 6 **37 points per
seed**).** Scoring the current facing as a floor stops a body turning away from a
loaded pose while the gun is on cooldown, which reads as obviously right. It is
wrong on this arm: a cooling gun's best use of the tick is the *next* bearing,
because whatever was standing on this lane has moved by the time the cooldown
clears — movement resolves before combat, and a three-bearing envelope means the
next facing is usually already covering the tile the target is walking to. This
is the single most surprising number in the pass and the comment explaining it is
in the shipped source, because the next author to read `TryAim` will want to add
that baseline.

**5. Two provable repairs that measure exactly zero.** A bolt stops on the first
enemy body, so scoring the best contact anywhere along a path can credit a
trajectory with a target standing behind a blocker; and an aim-only diagonal
spends no bend, so penalising `|initialAimOffset|` like a bend is a category
error. Both are correct, both changed *some* decisions, both moved **no**
outcome and produced byte-identical behaviour counts. Neither is shipped. A
correctness repair with a measured zero is still a risk with no return, and the
population's stopping rule says a change with the same dynamics signature adds
nothing.

## Frictions, in the order they cost me time

### 1. A paired statistic cannot see a variance change, and nothing in the tooling says so

The paired edge is the right headline for a mirror-symmetric map — the
predecessor's own self-mirror runs **2 wins, 8 losses, 2 draws for team 0** over
12 matches, so the side asymmetry is enormous and has to be differenced out. But
two of my variants scored **exactly 0.0 on every seed** for opposite reasons: one
changed no decision at all, and one turned every single match from a 499-tick
grind into a tick-192 base breach, symmetric between the sides. I spent a full
diagnostic cycle on the second before noticing that `completionReason` had gone
from `max-ticks` on 10 of 12 to `base-breach` on 12 of 12 — a fact that was in
every replay and in none of my summaries.

The cheap fix is in the evaluator rather than in the author's discipline.
`nilbots experiment frontline-labs` already prints `Result: … — max-ticks at tick
499` per match; a `--seeds a,b,c` sweep prints one such block per seed and nothing
that aggregates them. **A one-line sweep footer — win/loss/draw, completion
reasons, mean end tick — would have caught this in the first ten seconds**, and
it is the same data the tool already has in hand. Failing that, saying in the
packet that a candidate must report its completion-reason distribution beside its
score would do it. Revision 4's DX asked for per-rule factorials to be named in
the packet; this is the same request one level up: **the packet names the score,
so authors report the score, and a score-neutral variance explosion ships.**

### 2. `--print-candidate-contract` still cannot tell me what the cell resolves to

Third revision running, and it is still the highest-value first move of a session
that the tool cannot serve. The flag emits identity and fingerprints only, so
establishing the actual arm means running one throwaway match and dumping
`header.contract` out of a ~15 MB replay. Everything this pass turned on came
from that dump and none of it from prose: that the tuned schedule is 60/180/300
with `delayTicks` 22 on `fabricator-child-ready` and 30 on
`fabricator-late-child-ready`; that `shotProgram.minInitialAimSteps` /
`maxInitialAimSteps` are −1/+1 while `minBendAfterTiles`/`maxBendAfterTiles` are
1/2, which is what makes the envelope fifteen programs; that
`sameLifeTransitions` is `[]` on a fabricator mirror, so `open` is inert and the
identity says `crew` not `deck`; that both `mobilize` routes carry
`irreversibleForLife: false` with no `flatHealthGain`; that
`placement.forbiddenTileTags` on the shell entry route is now empty; and that
`fabrication-source-anywhere` makes the whole walkable map the source region, so
this class's "return to the pad" behaviour is permanently inert. A
`--print-candidate-contract --full` emitting the resolved rules would delete a
step every author repeats, and on this arm it would have deleted an hour: **the
one shipped rule is a direct consequence of two fields in one profile**, and I
could not have found them any other way.

The corollary friction is that the round-3/round-4 arm tokens are not
self-describing from the flags. `--stance-ground open` is spelled the same on
every pair and resolves to three different identities (`crew`, `deck`,
`sail-open`) depending on which classes are present. That is the documented
design and it is correct; but it means the only way to know which ruleset a
sweep actually measured is to read it back, and the flag set that "works on every
pair" is precisely the one that hides which arm each row is.

### 3. The turret's new cycle is a doctrine change that reaches bots only through prose

`--stance-ground open` makes `anchor` ⇄ `mobilize` unlimited per life and prices
it with `preserve-ratio-floor-minimum-one` in both directions and no entry heal.
Every byte of that is readable — `irreversibleForLife`, the health policy, the
ratio formula, the windups — and my policy needed no edit, because it was already
written against routes and legality masks rather than against a remembered rule.
That is the system working.

What is *not* readable is the consequence the mid-wave correction had to spell
out: an enemy turret is no longer weight permanently removed from the objective,
it is weight that can come back next windup. A doctrine that counts enemy
objective weight from `Enemies` and each body's `objectiveWeight` sees a turret as
a zero and is silently wrong about the next four ticks, and nothing in the
contract marks that difference — the reversibility field is on the *route*, not on
the observed body. Two shapes would close it: publish the enemy body's available
same-life routes (they are already public in `sameLifeTransitions`, so this leaks
nothing a bot could not join by hand), or note in the class table that a turret's
zero weight is now a *lease* rather than a sale. I did not need it this pass
because the arithmetic-based bound in rejected rule #1 is form-agnostic and would
have covered it for free — which is a small argument that the bound is worth
keeping in a drawer even though its consumer lost.

Also still true, and still costing minutes: the CLI binary in
`sandbox/cli-publish/` is named `botarena` while every document and the tool's own
help say `nilbots`; `ObservedSound.Bearing` is unusable because
`hearingBearingModel` is a policy-ID string with no published sector-to-direction
mapping (fourth revision running, still a sensor I will not guess against); and
`Available` still reads as "this will work" when it means "individually legal
before the joint step" — 1.7 % of this artifact's decisions are blocked, all
joint-resolution.

## Timing

- Reading the three permitted docs, the SDK's observation records, and dumping
  the resolved contract for all six pairs: ~30 min.
- Diagnostics on the predecessor's own mirror before writing any code (action
  mix by form, slot-state occupancy, death positions relative to the active
  region, wait-state breakdown): ~20 min, and it is the part I would keep if I
  could keep only one. Every rule I tried came from it, including the four that
  lost.
- Implementation: ~50 min across five rules, most of it in the four that were
  rejected.
- Measurement: seventeen artifacts, each a 10 s `--no-cache` build plus a
  12-match WASM matrix at ~9 s wall (6-way parallel). ~12 min of machine time
  total. The matrices are cheap enough that guessing is never justified, which is
  the only reason a +85 rule and a −50 rule could be told apart at all.
- Qualification suite 5 including hash-linked T3 and T2 reruns: **6 s wall**.
- One unforced cost worth recording: 12-match sweeps write ~15 MB of replay plus
  a self-contained viewer per match, and I filled the disk at around 9 GB of
  retained sweeps. Sweep outputs are now pruned as soon as their numbers are
  extracted. A `--no-viewer` flag, or writing the viewer only on `--open`, would
  halve the footprint of every measurement session.

## Behaviour of the frozen artifact

Beats the rebuilt wave-4 predecessor **24-0-0 on its own class arm over twelve
disjoint seeds**, at the ceiling of the paired territorial statistic (+120.0 per
seed): every match a base breach, from both sides. Positive on both other
pairings that contain a fabricator (`bulwark-vs-fabricator` +46.0,
`fabricator-vs-striker` +1.0). Zero runtime faults and zero rejected actions in
87,000+ controlled-runtime decisions across all six class pairs.

Known rough edges, recorded rather than fixed:

- **The revision is tuned to this chassis's gun.** The aim gate is the declared
  projectile travel, and on a chassis this entrant does not own — a range-8
  striker, a range-6 bulwark on a 3-tick cooldown — the same rule costs
  −49.7 and −33.3 per seed in off-class mirrors. A chassis-general variant was
  measured and is worse everywhere. Since a class-declaring project is bound to
  its class's canonical side, no competitive cell can contain those rows; they are
  robustness probes and they are clean of faults.
- **The fabricator still fields 1.39 of 4 bodies.** The diagnosis is solid — the
  prime is dead 44 % of the match and its child slots spend half to four-fifths
  of theirs on a rebuild clock — and the two obvious treatments (protect the
  source by ranking, refuse it the ground) both lose. The next revision that wants
  the fourth slot to matter should target the prime's *survival while queueing*
  rather than where the prime stands, and it should expect the answer to be a
  contract read I have not found yet.
- **The aim envelope is spent in exactly one place.** Pricing a pose the same way
  inside the stalemate-breaker loses (−24.3), and pricing the objective-entry and
  evade candidates already used the full program list. So the shipped artifact
  reasons about three bearings when it turns and about one when it steps, and I
  cannot claim that asymmetry is right — only that both alternatives measured
  worse.
- **Never anchors, never splits**, unchanged and now measured against 8,002
  offers on an arm where the anchor is cheap, legal on the ground, and freely
  reversible. If a later arm ever makes objective weight recoverable *while*
  anchored, this line is the first thing to re-measure.
- **The volley threshold is still two covered bodies**, chosen from the
  mechanic's arithmetic rather than measured, because this chassis cannot cast one
  and the pairs that can are not this brief's competitive cells. 543 casts in an
  off-class striker mirror confirm the path works and say nothing about whether
  two is the right number.
