# DX notes — spark-line revision 6 (fabricator, `spark-line-v1`)

Population: Frontline Labs classes wave 6, revision 6. Role: verdict-doctrine,
target cumulative T4. Frozen revisions 1–5 live untouched at
`arena-bots/frontline-labs/classes-wave-1-2026-07-29/spark-line/`,
`.../classes-wave-1-revision-2-2026-07-29/spark-line/`,
`.../classes-wave-1-revision-3-2026-07-29/spark-line/`,
`.../classes-wave-4-2026-07-30/spark-line/` and
`.../classes-wave-5-2026-07-30/spark-line/`; their DX notes are preserved there
and are not restated except where a friction changed status.

**Isolation statement.** This pass read only the wave-6 brief, the author packet
(`FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aa…`),
`FRONTLINE-LABS-RULES.md` (`06ff461e…`), `EXPERIMENTAL-FRONTLINE-CLASSES.md`
(`2333bd3c…`), `templates/botarena-generic-actor/`, `src/BotArena.Sdk/` (types
and XML docs — `GenericActorContext`'s observation records and `BotTypes`'
direction helpers), this entrant's own frozen wave-5 directory, replays this
entrant produced in this session, and the CLI at `sandbox/cli-publish/`. No
other entrant's source, no standings, no aggregate balance report, no engine or
App implementation, no non-assigned replay. Every sparring opponent was this
entrant's own wave-5 predecessor rebuilt from source, or a single-rule variant
of this entrant's own source. Scratch was a uniquely named private directory
(`sandbox/spark-line-w6-scratch-9f4c1e07/`), never a shared or guessable one.
Nothing was committed to git.

Two exposures to disclose exactly, both incidental and neither a competitor's
material:

- The brief was delivered as one key of a shared JSON file holding all eight
  entrants' briefs. Extracting my own value printed the file's **key list**, so I
  saw the other seven entrants' lineage NAMES. No other key's value was read —
  no brief, no doctrine, no source, no result. A per-entrant file, or a reader
  that projects one key, would have made even that impossible.
- The harness injects the repository's `CLAUDE.md` as ambient context rather than
  as a file I opened. It is project-wide agent guidance describing engine and App
  architecture. Nothing in it concerns another entrant, and nothing in this
  revision derives from it; the policy reads the resolved contract exclusively.
  Recording it because "material I did not choose to read" is still material.

## Freeze identity

| Item | Value |
| --- | --- |
| Entrant | `spark-line` (entry type `SparkLine`), revision 6 |
| Class | `fabricator` (declared in `botarena.json`, unchanged since revision 1) |
| Canonical artifact | `out/bot.wasm` |
| **bot.wasm SHA-256** | `fc397fc5eb6d53be41219a615a30d0a83a9f162f2ba38222ed299748fbe8e2e5` |
| Build | `build --no-cache`, cache miss, compiled; key `7e2ed9b25a0971fa43ae6104eed50a02814d8515f3920edadab53fd68de83d1e`; **four** independent `--no-cache` builds reproduced the artifact hash, one of them from a comment-differing tree whose key is `4a0ca1df…` — comments move the cache key and not the codegen |
| Builder | CLI 0.9.22, SDK 0.10.6, game rules 0.5, NativeAOT-LLVM 10.0.0-rc.1.26306.1, wasi-wasm p1 core module, platform-matched Docker builder |
| Qualification | `experiment frontline-labs qualify --suite frontline-qualification-5` → **exit 0**, tier **T4**, `passed: true`, `profileComplete: true`, `balanceEvidenceEligible: true`, all five probes PASS |
| Report | `evidence/t4/qualification.json`, sha256 `8112a1867ee448796046c83a5581439c27462054dadedb9255d9773ef8abd1ad` |
| Hash-linked T3 report | `bdf5a5c6ce7308588fecaccfe89a31caf4a8e04467bdd2539ee96a4480faf6f0` (`frontline-qualification-4`, profile `frontline-duel-depth-union-t3-v1`) |
| Hash-linked T2 report | `a349ae55568cde101cdbae4405cdaad4a5b66e06248ebf47e337b8485adbfbcd` (`frontline-qualification-3`) |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| Source-tree hash | `390116b0c5b64e4a3d834c11c8737da7b1b54c626cf841c9ecb19cc8d61da4e2` |
| Submitted source | `SparkLine.cs` (3428 lines), `Squad.cs` (576, **new**), `ContractLens.cs` (733), `Tactics.cs` (394), `ArenaBasics.cs` (1205, template verbatim) |
| Sparring baseline | wave-5 source **rebuilt** on this SDK, `--no-cache` → `6fe9dac5eaeb0a0afa64405438066e24c557ef77b53aebf6f4c16542ddaa7fe7` |
| Wave 5 **as frozen** | `e725e2e5dadb75cbd034f040e27f574a483dd5e169036bd95c17f3f749ac169d` — not used for any measurement; the CLI moved 0.9.21 → 0.9.22 under this pass, so the frozen artifact and the rebuilt source are different artifacts and only the rebuild is a fair opponent |
| Cited replays | `evidence/crew/crew-fvf-{a,b}-s104729.json`, both `verify`-clean (`545a352b…`, `a3418418…`) — base breach at tick 440 as team 0 and tick 400 as team 1 |

Per-file SHA-256:

```text
567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627  ArenaBasics.cs
ee510ed5a67adf61b9f2af1650397ce30f41ddd444e79fe19b5179ceae735fd6  ContractLens.cs
014e0b47b7df4cc259a2134c0a74d26fd2217240d86f963cc096398cfd5eb38a  README.md
bdf7b105507c581dadfebb1f8ac9a3214e01d16b5c8c884d151fa381b7846f2e  SparkLine.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  SparkLine.csproj
4b01cbb79e0d18689d891aa7ef23db26bdd8f3ab73f79af1b535b1264d3a3811  Squad.cs
049b42b422c704c48a825205edcf7cb1c2acf9974a13bee33a6b30e7c74bd4b9  Tactics.cs
340a566bf2a177dd4b1b81f2e74dae80aabb106b1d8e70acf98ef19c362c9c95  botarena.json
```

The same list plus the artifact hash is in `SHA256SUMS.txt`. `Tactics.cs`,
`ArenaBasics.cs`, `botarena.json` and the csproj are **byte-identical to
revision 5**. The revision is one new file (`Squad.cs`), two additions to
`ContractLens.cs` (corridor-run labelling and a cached per-tile walk field), and
about forty lines of call sites in `SparkLine.cs`. No scoring term, no threat
model, no gun code, and no capture arithmetic was touched.

Resolved identities this freeze was measured on — unchanged from wave 5, and
read back out of a replay header rather than inferred from the flags:

| pair | ruleset ID | topology profile |
| --- | --- | --- |
| `fabricator-vs-fabricator` | `frontline-labs-1-fabricator-vs-fabricator-crew-facing-locked` | `…-four-slots-v1` |
| `bulwark-vs-fabricator` | `frontline-labs-1-bulwark-vs-fabricator-deck-facing-locked` | `…-asymmetric-slots-4-3-v1` |
| `fabricator-vs-striker` | `frontline-labs-1-fabricator-vs-striker-deck-facing-locked` | `…-asymmetric-slots-4-3-v1` |
| `bulwark-vs-striker` | `frontline-labs-1-bulwark-vs-striker-sail-open-facing-locked` | `…-three-slots-v1` |
| `bulwark-vs-bulwark` | `frontline-labs-1-bulwark-vs-bulwark-sail-open-facing-locked` | `…-three-slots-v1` |
| `striker-vs-striker` | `frontline-labs-1-striker-vs-striker-sail-open-facing-locked` | `…-three-slots-v1` |

## Doctrine delta, in one paragraph

**No doctrine moved.** The currency is still surviving objective weight, the
branch is still chosen by the shot envelope — *concentrate what the gun can
defend, spread what it cannot* — a pose is still priced as the three-bearing fan
revision 5 introduced, the weight target is unchanged, and the class's central
bargain is still refused on the same one line. What revision 6 fixes is not what
four bodies want but what they do while wanting it: **concentration routes every
body down the same distance field to the same tile of the same region, and the
movement rules then refuse same-destination moves, swaps, and following a
vacated actor.** Counted on the predecessor's own mirror over twelve matches,
**249 of its 303 blocked moves (82 %) are two of its OWN bodies submitting one
destination**, and **17 of 167 fabrications (10 %)** put a child on a tile its
own prime tried to enter within three ticks. The correction is five rules, all
in `Squad.cs`, all of them pure functions of the frozen shared observation —
which is the only kind of rule a fabricator can have, because every body of it
is a fresh instance with empty private memory and no channel to its siblings, so
the sole thing four lives can agree on is arithmetic all four perform on the
allied union they all receive. Right of way is one written total order (nearer
the active objective, then fabrication-capable, then lower unit slot, then lower
life) so "who yields" is never a negotiation and never a coin flip; on top of it
sit distinct-tile assignment by minimum total walk, a route-claim yield, corridor
precedence with a leave-the-corridor half, a fabrication filter, and a spacing
tie-break. The finding underneath the freeze is that **coordination does not
decompose**: every one of the five rules is a loss or inert on its own, spanning
−67.3 to +1.7 per seed, and the five together are +108.7 and 12-0-0 against the
same predecessor.

## The five coordination rules, and the measured attribution of each

Every rule is implemented alone and switchable, built as its own artifact, and
measured on the same matrix: the brief's flag set (`--classes
fabricator-vs-fabricator --movement facing-locked --pendulum keel --skills kit
--bend universal --aim offset --stance-ground open --five-slots wane`), **six
seeds** (104729, 130363, 155921, 181081, 206699, 232391), **both sides**,
controlled WASM runtime, 12 matches per row. The statistic is the **paired edge**
per seed: the sum over both sides of (candidate signed territorial progress −
predecessor's), keyed on `header.provenance.participants[].artifactHash` rather
than on participant ID. Its ceiling is +120.

Fifteen artifact hashes are in `evidence/ablation/ARTIFACT-HASHES.txt`; the
switch block that generates any row is in
`evidence/ablation/SparkLine.switched.cs.txt` with the generator
`evidence/ablation/mkvariant.sh`, and the measurement harness is
`sweep.sh` + `score.py` + `coord.py` beside them. Variant trees were deleted
after their numbers were extracted.

### The control that makes the rest of the table mean anything

| row | artifact | paired edge / seed | W-L-D | completion |
| --- | --- | --- | --- | --- |
| **all five rules OFF** (the whole layer compiled in, inert) | `3cc25ff2` | **0.0** on every seed | 6-6-0 | 12/12 max-ticks |

With every rule off, the candidate's coordination counters are **byte-identical**
to the rebuilt predecessor's — 10,056 body-ticks, 303 blocked, 249 mutual, 4,619
adjacent pairs, 6,728 lane-sharing pairs, 167 fabrications, 17 into its own path,
on both sides. So the new file, the new lens fields and the reshaped score
expressions change **no decision**, and every number below is caused by a rule
rather than by the refactor that carries it. This is the row I would have skipped
if wave 5 had not taught me that a zero can mean two different things.

### Each rule ALONE, against the predecessor

| rule | artifact | paired edge / seed | W-L-D | completion (breach/max) | mutual blocks | fabs into own path |
| --- | --- | --- | --- | --- | --- | --- |
| 1 ENVELOPMENT: distinct objective tiles by min total walk | `772438af` | −1.7 | 6-6-0 | 6/6 | 160 | 5 |
| 2 YIELD: never step onto a better-right-of-way sibling's next tile | `3931d8cc` | **−67.3** | 3-9-0 | 6/6 | **28** | 24 |
| 3 CHOKE: one-tile corridor run admits one body; parked bodies leave | `d326730d` | 0.0 | 6-6-0 | 0/12 | 249 | 17 |
| 4 FORGE: never fabricate into own traffic | `95460c2a` | +1.7 | 6-6-0 | 6/6 | 101 | 12 |
| 5 SPACE: equal-value pose spacing tie-break | `272e127c` | −14.0 | 6-6-0 | 0/12 | 60 | 42 |
| 1+2 only | `98524f9d` | +15.7 | 7-5-0 | 2/10 | 18 | 19 |
| **all five (SHIPPED)** | `35f24ac6` ≡ `fc397fc5` | **+108.7** | **12-0-0** | 10/2 | **28** | **0** |

Read rule 2 and rule 5 carefully, because they are the pass's cautionary rows.
Rule 2 alone is the **best** row in the table on the owner-visible metric — it
removes 89 % of the mutual blocks — and it is also the **worst** row on the
scoreboard at −67.3 and 3-9-0. Fixing the symptom is not the same as fixing the
problem: without distinct destinations both bodies still want one tile, so a
yield rule simply converts a mutual block into a stopped follower, and its body
count falls below the opponent's (7,679 body-ticks against 8,561) because a body
that stops walking dies where it stands. Rule 5 alone reduces mutual blocks
by 76 % and *raises* fabrications-into-own-path from 17 to 42, because pushing
bodies apart moves the prime's route without telling the fabricator about it.

### Each rule's contribution INSIDE the shipped composition (leave-one-out)

This is the attribution the brief asks for, and after the table above it is the
only one that means anything.

| composition | artifact | paired edge / seed | W-L-D | attribution of the removed rule |
| --- | --- | --- | --- | --- |
| all five | `35f24ac6` ≡ `fc397fc5` | **+108.7** | 12-0-0 | — |
| all − 1 ENVELOPMENT | `252d165f` | +43.3 | 10-2-0 | **+65.4** |
| all − 2 YIELD | `712abce2` | −17.0 | 4-6-2 | **+125.7** |
| all − 3 CHOKE | `92f6a122` | +80.0 | 10-2-0 | **+28.7** |
| all − 4 FORGE | `1f4147dc` | −16.0 | 6-6-0 | **+124.7** |
| all − 5 SPACE | `64048f9b` | +18.7 | 6-6-0 | **+90.0** |

Every rule is strongly positive inside the composition and ~zero or negative
outside it, and no single removal improves on the whole. Two of the leave-one-out
rows also carry a mechanism you can read directly off the counters:

- **all − 4 (FORGE)** keeps every routing rule and loses anyway, at −16.0. Its
  corridor-queue counter explodes: `chokeQueue` 8 → **344** and `chokeAdjacent`
  300 → **1,072**. The routing rules successfully push bodies into distinct
  corridors, and the fabricator then materialises children **in** those
  corridors, so the plug the routing removed is reinstalled by the queue. Rule 4
  is not hygiene; it is what stops rules 1–3 sabotaging themselves.
- **all − 3 (CHOKE)** is +80.0 and 10-2-0, and the two matches it loses are the
  two seeds where the primary arm fails to breach. Corridor precedence is the
  cheapest of the five and the one whose absence costs the least — which is
  exactly why it is worth having: +28.7 for a rule that is pure map geometry
  computed once at `StartLife`.

### Reproduction on disjoint seeds

The shipped artifact was re-run on six **disjoint** seeds (7, 4243, 60013, 99991,
314159, 777767): **12-0-0 at +114.3** per seed, 11 of 12 a base breach. Across
the two seed sets that is **24 matches, 24 wins, 24 seeds' worth of positive
paired edge, 0 negative**. Sign test on twelve independent seeds is one-sided
p ≈ 0.0002.

## Measured records — shipped artifact vs rebuilt wave-5 predecessor, the deck game

Same matrix and statistic. The three rows containing a fabricator are the
competitive cells; a fabricator-declared project is bound to its class's
canonical side, so the last three rows are this artifact driving a chassis it
does not own and are robustness probes, not standings.

| pair | ruleset | W-L-D | paired edge / seed | per-seed edges | seeds +/−/0 |
| --- | --- | --- | --- | --- | --- |
| **`fabricator-vs-fabricator`** (my class, primary) | `crew` | **12-0-0** | **+108.7** | 120, 86, 120, 86, 120, 120 | 6 / 0 / 0 |
| **`fabricator-vs-fabricator`**, disjoint seeds | `crew` | **12-0-0** | **+114.3** | 120 ×5, 86 | 6 / 0 / 0 |
| `bulwark-vs-fabricator` | `deck` | **8-2-2** | **+37.0** | 84, 28, 46, 88, −32, 8 | 5 / 1 / 0 |
| `fabricator-vs-striker` | `deck` | 6-6-0 | +2.7 | 38, 0, 0, 12, −4, −30 | 2 / 2 / 2 |
| `striker-vs-striker` (off-class probe) | `sail-open` | 8-0-4 | **+58.3** | 38, 38, 30, 120, 68, 56 | 6 / 0 / 0 |
| `bulwark-vs-striker` (off-class probe) | `sail-open` | 6-6-0 | 0.0 | 0 ×6 | 0 / 0 / 6 |
| `bulwark-vs-bulwark` (off-class probe) | `sail-open` | 0-6-6 | **−43.0** | −54, −54, −54, −30, −54, −12 | 0 / 6 / 0 |

The off-class rows are worth one sentence each because they say something about
how general the layer is. `striker-vs-striker` is **+58.3 and 8-0-4** against the
predecessor — and wave 5's own DX recorded that arm as its worst regression
(−49.7 against ITS predecessor, a different comparison), so the coordination
layer repairs a cell the aim pass hurt. It does that without naming a class: the
rules read forms, objective weights and fabrication routes out of the contract,
so a striker mirror gets the same de-jamming for free, and its mutual blocks fall
to 4 against the predecessor's 38. `bulwark-vs-bulwark` stays negative at −43.0 with six
draws, and the counters say coordination is not the cause: the candidate's
mutual blocks there are **zero** against the predecessor's 10. It is wave 5's
recorded rough edge — the aim gate is this chassis's declared travel and a
range-6 gun on a 3-tick cooldown prices that tick differently — and this pass
did not reopen it.

### Runtime health and the owner-visible counters

All seven rows above were measured on the frozen artifact `fc397fc5` itself.
Across them (84 matches) there were **zero runtime faults and zero rejected
actions** in **49,582 controlled-runtime candidate decisions**, including every
off-class path — volley stances entered and cast, aegis shells raised and
deflecting, turret anchors offered and refused.

The counters the owner would see. The predecessor column is its own 12-match
self-mirror — the control row above, where both sides are `6fe9dac5` — and the
shipped column is `fc397fc5`'s own bodies over the 12 matches it played against
it. Two different runs, because a bot's coordination counters are a property of
the bot and the predecessor's have to be measured somewhere it is not being
beaten by 108 points a seed.

| counter | predecessor (self-mirror) | shipped (vs predecessor) | change |
| --- | --- | --- | --- |
| blocked moves | 303 | **52** | −83 % |
| of which two own bodies wanting one tile | 249 | **28** | −89 % |
| moves blocked into a tile an ally was standing on | 0 | 0 | already fixed in wave 5 |
| own bodies adjacent (body-tick pairs) | 4,619 | 2,512 | −46 % |
| own bodies sharing one bolt lane inside gun reach | 6,728 | 3,631 | −46 % |
| fabrications landing on a tile its own prime needed | 17 of 167 | **0 of 164** | −100 % |
| body-ticks fielded | 10,056 | 7,774 | see note |

The body-tick fall is not a loss of bodies: 10 of 12 matches now end in a base
breach at tick 396–464 instead of grinding to the 499 cap, so there are simply
fewer ticks. On the disjoint set the candidate fields **more** body-ticks than
the predecessor over shorter matches (7,804 against 6,991).

One counter went the other way and is worth stating plainly: `chokeBodyTicks`
rises from 579 to 1,054 and `chokeAdjacent` from 163 to 300. The bodies spend
*more* time in one-tile corridors, not less. That is the rules working rather
than failing — corridors are the approaches to this map's objectives, and a team
that stops jamming its own corridors uses them instead of milling around outside
them. What fell is the thing that hurt: two bodies inside one corridor RUN at
once is `chokeQueue`, and the shipped artifact holds it at 8 while the
`all − FORGE` variant that abandons the fabrication filter shows 344.

## What I did not ship

- **Clearing a lane ahead of the gun.** `TryYieldTheLane` sits after `TryShoot`
  and `TryAim` in the decision order, so a body that could shoot shoots and
  clears next tick. Moving it ahead of the gun is the obvious alternative and it
  is not measured, because the ordering argument is one-sided: the sibling's step
  is worth one tick of walk and the shot is worth a third of a body. It is the
  first thing I would measure with more budget, and it is the honest gap in this
  freeze.
- **Rule 1 as a hard destination filter.** `TryEnterObjective` still accepts any
  region tile and merely *prefers* the assigned one at low order. Making the
  assignment binding was not measured. Presence is the currency and a body two
  tiles from its assigned tile but adjacent to another region tile should take
  the ground, so the soft form is the one that matches the doctrine.
- **A claim on a sibling that is not walking.** The route model claims a tile
  only when the sibling's own facing and its own walk both point at it. A
  sibling that is aiming, evading, or holding gets no forward claim — only the
  corridor run it stands in. Claiming more would freeze half the map; the
  measured cost of claiming less is inside the +125.7 that rule 2 already earns.

## Frictions, in the order they cost me time

### 1. The freeze layout the packet mandates breaks the build that produces it

The packet requires the revision to preserve its ablation evidence beside its
source in one directory. `nilbots build <project>` discovers sources by globbing
the project directory, so the moment I archived the ablation's switchable source
as `evidence/ablation/SparkLine.switched.cs`, the frozen tree **stopped
compiling** — 78 `CS0111` duplicate-member errors, because the archived variant
is a second copy of the same class. The artifact was already built and correct,
so nothing was wrong with the freeze except that nobody could ever reproduce it;
I found this only because I ran two extra `--no-cache` builds to check
determinism, and a freeze that is never rebuilt would have shipped broken.

This is a two-line fix in either direction and it should be made in the tool
rather than in every author's discipline: **exclude a conventional evidence
directory from source discovery** (`evidence/`, or anything the manifest does not
list), or **fail the build loudly when the glob picks up a file outside the
declared source set**. Wave 5 avoided it only by accident — it archived a
`.py` patch generator instead of a `.cs` file. I ended up renaming the archive to
`SparkLine.switched.cs.txt`, which works and is exactly the kind of thing a
reproducer will not think to undo.

### 2. `--viewer` reached `experiment` and not `qualify`, and the evidence bundle is 92 % viewer

The 0.9.22 change is real and it saved this pass a great deal of disk: a
12-match sweep is now 264 MB instead of ~600 MB, and I ran 24 of them (288 matches)
without approaching the wall that wave 5's DX recorded hitting at 9 GB. Thank
you.

It did not reach the qualification suite. `qualify --suite
frontline-qualification-5` writes **36 viewer files totalling 192 MB** beside
**20 MB of replay** — the mandated evidence bundle is 213 MB of which 92 % is
presentation, and it is the one output an author is required to keep forever.
Every frozen revision in this population therefore carries an order of magnitude
more viewer than evidence; wave 5's frozen `evidence/` is 228 MB for the same
reason. I pruned the viewers and kept every `replay.json` — the packet asks for
verified probe replays, and a viewer is not one — which takes this freeze to
21 MB of evidence and 58 MB in total. The fix is the same flag the experiment
command already grew.

### 3. `--print-candidate-contract` still cannot tell me what the cell resolves to

Fourth revision running, unchanged, and still the highest-value first move of a
session that the tool cannot serve. The flag emits identity and fingerprints
only, so establishing the arm means running a throwaway match and dumping
`header.contract` out of a 22 MB replay. This pass needed exactly two facts from
it and neither is in any prose: that `movementProfiles[].facingCoupling` is
`facing-locked` on **every** form of this arm — which is what makes rule 2's
claim exact rather than probabilistic, because the legality mask will offer a
sibling only its own facing — and the map's `tileRows`, from which the corridor
runs are labelled. A `--print-candidate-contract --full` emitting the resolved
rules would delete a step every author repeats, and on this pass it would have
saved half an hour that went into confirming a field I could have read.

Also still true, and still costing minutes: the CLI binary in
`sandbox/cli-publish/` is named `botarena` while every document and the tool's
own help say `nilbots` (third revision running); `ObservedSound.Bearing` remains
unusable because `hearingBearingModel` is a policy-ID string with no published
sector-to-direction mapping (fifth revision running, still a sensor I will not
guess against); and `Available` still reads as "this will work" when it means
"individually legal before the joint step" — which is precisely the gap this
whole revision is about, since a same-destination move is `Available` for both
bodies and refused for both.

## Timing

- Reading the three permitted docs, the SDK's observation records, and the
  wave-5 source and DX: ~35 min.
- Writing the replay-level coordination diagnostic **before** writing any bot
  code: ~25 min, and it is the part I would keep if I could keep only one. The
  "82 % of blocked moves are self-inflicted" number is what turned an
  owner-visible complaint into five falsifiable rules, and the same script is
  what caught rule 2 being simultaneously the best row on the metric and the
  worst on the scoreboard.
- Implementation: ~45 min, most of it in `Squad.cs`.
- Measurement: fifteen artifacts, each a ~9 s `--no-cache` build plus a 12-match
  WASM matrix at ~45 s wall (three sweeps in parallel). ~25 min of machine
  time across 24 sweeps and 288 matches.
- Qualification suite 5 including hash-linked T3 and T2 reruns: **7 s wall**.
- Sweep outputs were deleted as soon as their numbers were extracted; peak
  retained replay was under 1 GB.

## Behaviour of the frozen artifact

Beats the rebuilt wave-5 predecessor **24-0-0 on its own class arm over twelve
disjoint seeds** (+108.7 and +114.3 per seed against a +120 ceiling), 21 of
those 24 wins a base breach, and it breaches from both sides on the same seed.
Positive on both other cells that contain a fabricator (`bulwark-vs-fabricator`
+37.0 and 8-2-2, `fabricator-vs-striker` +2.7). Zero runtime faults and zero
rejected actions in 49,582 controlled-runtime decisions across all six class
pairs.

Known rough edges, recorded rather than fixed:

- **The five rules are only measured as a set.** Every single-rule row loses or
  is inert, and every leave-one-out row loses; I have no evidence about any
  three- or four-rule subset other than 1+2 (+15.7). The composition is a local
  optimum under single removals and I cannot claim more than that.
- **Rule 2's intent model reads rule 1's assignment.** A sibling's claimed tile
  is computed against the tile the assignment gave that sibling, so switching
  rule 1 off does not fully decouple rule 2 — it only stops THIS body routing to
  its own assignment. The `all − 1` row is therefore "envelopment off for my own
  routing", not "no assignment exists", and the +65.4 attributed to rule 1 is
  the smaller of the two possible readings.
- **The corridor definition is straight-only.** A tile with exactly two open
  cardinal neighbours at a right angle is a corner, not a choke, because one body
  can turn in it while another waits beside it. That is defensible and it is also
  the reason `chokeBlock` is zero on every row: the blocks this map produces
  happen at corridor MOUTHS, which are not corridor tiles. A mouth-aware
  definition is untested.
- **Wave 5's rough edges are all still here, unexamined.** The fabricator still
  fields well under its four slots; the aim envelope is still spent in exactly
  one place; the entrant still never anchors and never splits, now against a
  contract where the anchor is cheap, legal on the ground and freely reversible;
  and the volley threshold is still two covered bodies chosen from arithmetic
  rather than measurement. This was a coordination pass and it deliberately
  reopened none of them.
- **`bulwark-vs-bulwark` remains negative** at −43.0, and the counters show it is
  not a coordination failure (zero mutual blocks against the predecessor's 10).
  It is the chassis-specific aim gate wave 5 measured and documented; no
  competitive cell can contain that row for a fabricator-declared entrant.
