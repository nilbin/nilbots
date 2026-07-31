# DX report — iron-root, wave 8 (revision 7, GARRISON)

## Isolation statement

Written from this revision's own forensics, its own qualification report, and
private sparring against this lineage's own rebuilt wave-6 predecessor, against
twelve variants of this revision's own source, and against pre-built opponent
**artifacts** at `sandbox/w8-baseline-0.10.10/*/out/bot.wasm`. **No other
entrant's source, `DX.md`, `README.md`, `botarena.json`, replays, standings, or
any aggregate balance report was opened.** `docs/DECISIONS.md` was not opened.
This revision's private scratch was `sandbox/iron-root-w8-scratch-5c17ae92`, a
uniquely named directory used for nothing else.

Permitted material used, and nothing else: the author packet, the Frontline Labs
v1 rule card, the experimental classes addendum, the labs bot-author packet,
`templates/botarena-generic-actor/`, `src/BotArena.Sdk/` types and XML docs, this
lineage's own frozen wave-6 directory (copied OUT to scratch; the frozen tree was
never written to), replays this session generated, and `sandbox/cli-publish/`
(nilbots 0.9.27, SDK 0.10.10).

```text
d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e  FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md
06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8  FRONTLINE-LABS-RULES.md
e7e1f023ca696faf5d57103eba14b72744bc6fead8e44cfe87929efe8987409c  EXPERIMENTAL-FRONTLINE-CLASSES.md
d3ea99a318bf932a63b9b0231c7e8fbb93cadc265a84cd42d4945befb439fc12  FRONTLINE-LABS-BOT-AUTHOR-PACKET.md
```

The packet and the rule card are **byte-identical to the versions revision 6
recorded**; the classes addendum has changed (revision 6 recorded
`2333bd3c…`), which is the mechanical confirmation that the game moved and this
one did not have to take anyone's word for it.

The frozen wave-6 tree was left untouched and still reproduces its recorded
identity, `8bdb3403c07a8ad0637a9784cd8d43442c57377e8d3a4b5d7ea3fdf18375283a`.

### Two exposures to disclose, as the packet requires

1. **The baseline directory listing.** The permitted opponent artifacts live at
   `sandbox/w8-baseline-0.10.10/<entrant>/out/bot.wasm`, and reaching an artifact
   means listing a directory that also contains that entrant's full source,
   `README.md` and `DX.md`. `ls` on those directories therefore printed **file
   names and sizes** for eight entrants — enough to see, for example, that one
   carries a file named `FabricationRoute.cs`. **No file inside any of those
   directories was opened, read, copied, diffed or executed**, and no
   `botarena.json` was read: the class of each artifact was taken from the
   commission's own text plus the wave-7 directory *name*, and where that was not
   enough I established it by running a match of my own and counting `fabricate`
   in **my own** replay rather than by reading theirs. Worth recording as a
   friction too: the artifacts an author is told to play are stored inside the
   directories the same author is told not to open, and nothing but discipline
   separates them.
2. **A file name in a compile error.** A one-off diagnostic variant compiled
   against the wrong SDK member and the controlled builder echoed the offending
   line from `/workspace/Salvage.cs` — my own file. No exposure; recorded only
   because the builder does echo source lines.

A third, minor, and mine: **sweep stdout was buffered to `/tmp/<config>.txt`**
during the parallel ablation rounds rather than to the private scratch
directory, which is a shared path the packet tells authors to stay out of. The
files contained only my own aggregate lines, no other entrant's data was read
from `/tmp`, and all of them were deleted; recorded because the packet asks for
the disclosure rather than the judgement.

Nothing was committed to git.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `iron-root` |
| Population / wave | Frontline Labs classes, **wave 8** (`classes-wave-8-2026-07-31`) |
| Authoring lineage | `iron-root-v1` |
| Class | `bulwark` (declared in `botarena.json`) |
| Doctrine | FORTRESS ROTATOR — revision 7 codename **GARRISON** |
| Role | `verdict-doctrine` |
| Target tier | cumulative T4 (retain) |
| The game | `--classes <pair> --movement facing-locked --pendulum keel --skills kit --bend universal --aim offset --stance-ground open --cooldown ticking --volley salvo --capture channel --economy scrap [--five-slots wane]` |
| Resolved identity, own mirror | `frontline-labs-1-bulwark-vs-bulwark-smithy-facing-locked` |
| Resolved identity, vs striker | `frontline-labs-1-bulwark-vs-striker-bastion-facing-locked` |
| Predecessor | frozen wave-6 directory, left untouched |
| Source-tree hash | `b3fb6934fd8bee3b849bf74e90b635de7669548daf5adeb0f20b4722007f300a` |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/Guest **0.10.10**, CLI 0.9.27, actor protocol 1.0, WASI p1 core module, platform-matched Docker builder (macOS arm64 host) |
| Build cache key | `4f36f71fbc951fed6a4d80c1d0c9e350f78bbb9c2759f1fb8afc924b7f9eae97` |
| **`out/bot.wasm` sha256** | **`ba7b5adb4a50535316a4340af34d1d8352f49761630223ca1518c949e6b52ff5`** |
| `evidence/t4/qualification.json` sha256 | `e6b74a697df9a7fa238c20014f7c1989504f4c2c0cb4337c4c16535e99149ac6` |
| Cumulative T3 prerequisite report sha256 | `8fe64336de7570f8b9a55ceb5f1be43c5cc37737a03fba3ca1fba26f5379f9fd` (hash-linked inside the T4 report) |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| **Qualification outcome** | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, **exit 0**, **T4 awarded**, `balanceEvidenceEligible: true`, `profileComplete: true`, **first attempt** |

All five T4 components passed (`suppression-choke`, `entry-initiative`,
`prediction-chamber`, `front-rotation`, `map-holdout`) with the cumulative T3 and
T2 prerequisites rerun and hash-linked automatically. The suite runs the
classless, skill-less, aim-less, channel-less, economy-less duel-depth union
profile, and this artifact passes it unchanged on the first attempt — every new
rule is gated on a contract field that profile does not publish, so the whole
wave-8 layer is provably inert there.

`evidence/t4/` keeps the report, the hash-linked prerequisite reports and all
**36 verified probe replays** (one verified with `nilbots verify`:
`suppression-choke/objective-hold-straight-pressure/bot-team-0`, stored hash
`b0b9aa657bc3318efa3a506f888cc9677d2b147a537792c77c29a89caede585f`, OK). The
self-contained `viewer.html` beside each replay was **deleted** — 5.3 MB each,
regenerable, and a full shared volume is a documented hazard for this population.
The replays and every hash are untouched. Directory total: 25 MB.

### Freeze integrity

The frozen tree was rebuilt `--no-cache` **from the freeze location as the last
step**; both hashes are stated at the end of this report. No variant, ablation or
scratch `.cs` file exists anywhere inside the freeze tree — `nilbots build` globs
every `.cs` under the project directory, so an archived variant would make the
frozen tree fail to rebuild with duplicate-member errors, silently, because
nobody rebuilds a freeze. All twelve variants lived in private scratch.

### Per-file source hashes

Recipe (unchanged across the lineage):

```bash
ls *.cs botarena.json IronRoot.csproj | LC_ALL=C sort | xargs shasum -a 256 | shasum -a 256
```

```text
6dfb7d6d55ed0e92e15e5cf47a6dd59add7eeef451ac02eac66d8024f1fb5987  ArenaBasics.cs
dfb1470dd8ad0a288f094d7127ed102b306aab0c5dc9624c4c581ccc67ba40d3  ArenaGeometry.cs
b499786b79b31779fae7fe15a2a48e25c57855ccd9f4b9cd3ae7dedcc4f7cbf6  Channel.cs
5c766f42e7cf4ed8b4e70adcdc1ac55c56136d2ebbe18b2cd1c4bc919de14d22  ContractLens.cs
1e402fb87f26ae3f144ceb068fd95ff75cb8020c081b36d49e70f447689ec213  FortressPlan.cs
c4bfa78d7c249ff0cd66544cef12d95abd92226fcb2a52a9764a46c35b9dac7b  Gunnery.cs
1860b8956509668278915e2faf7dcf02e6839b0a4b14e980759c6404cb0faa3c  IronRoot.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  IronRoot.csproj
4e69aef2560b076e1c114d65385b553b7052a28fcc51c7371887fe882d39032a  Kinematics.cs
89c6df67b1cc98a7f56021125436b904689bd6276cfe85ef838fb8e426ecdd4e  Salvage.cs
548942fdaa738fc1ed8438bdcc20c0d3e5145de507d4de2f002ee0604eed0328  Traffic.cs
4a8979008108922c6a9c4abf230ae484d06264f01f1b841e9df0b6274540120f  botarena.json
```

`Channel.cs` and `Salvage.cs` are new. `ContractLens.cs` gained one reader
(declared sight range) and one dictionary. `IronRoot.cs` carries every decision
point. `ArenaGeometry.cs`, `Kinematics.cs`, `Gunnery.cs`, `FortressPlan.cs`,
`Traffic.cs`, `ArenaBasics.cs` and `IronRoot.csproj` are byte-identical to wave 6
— the whole revision is two new files, one lens reader, and edits to the policy.

## Doctrine delta in one paragraph

**GARRISON.** The doctrine did not move: territory is still the only currency,
placement is still asked of the ROUTE, the turret is still a rental, shell
discipline is still *against poke raise the arc, against numbers root the gun*,
and the wave-6 coordination layer is untouched and still measured. What moved is
the sentence underneath all of it. Under a channelling capture, holding ground is
not standing on it — it is standing **still** on it and not being shot while you
do, because damage to a controlling body on the region reverts that team's whole
run one point per point of health. Revision 6 could not think that thought and,
worse, actively believed the opposite: its only reader of the control policy asks
whether surplus weight scales gain by searching for a substring that names a
*different* policy, so on this arm it concluded "binary control, one body of
positive weight nulls any number" — the exact inverse — and every tenure and
contest decision inherited it. So revision 7 is two new files of arithmetic the
contract already publishes (claim weight, denial weight, the stationary
subtraction, the capped multiplier, the erosion multiple, the interrupt's scope
and granularity, the ladder and its declared effects) and eight rules that
consult them, each a switch, each swept alone. The measured result is **16–0–0
and +14.8 territory a cell** over sixteen bastion mirror cells against a rebuilt
predecessor that the same harness proves the all-switches-off build is
decision-for-decision identical to — a pairing that was **0–0–16, every cell a
draw, zero captures on either side in eight thousand ticks** before. And the
honest headline is that the arm's advertised leverage for this class is
**backwards**: rooting a gun to revert an enemy channel costs 14.5 points a cell
against simply standing there, because a body's denial weight is unconditional
and a bolt is conditional on line, vision, cadence and not being deflected. The
turret is not this class's recapture denial. The body is.

## The rules, with per-rule measured attribution

Every rule is a switch in `Channel.cs`. Each row is the same artifact with
exactly one switch flipped, rebuilt through the controlled toolchain and swept
over the **same sixteen cells** (8 seeds × both sides, WASM-artifact opponents,
in-process host, bastion — `keel` + `kit` + `universal` + `offset` + `open` +
`ticking` + `salvo` + `channel` + `scrap` — against the rebuilt wave-6
predecessor on a bulwark mirror).

**The baseline row is a proof, not an estimate.** With all eight switches off the
artifact is **decision-for-decision identical to the rebuilt wave-6
predecessor** — 698 of 698 team-0 decisions on a checked cell, including every
debug string — so its margin is exactly `+0.0` and every number below is a
difference rather than a comparison.

| configuration | W–L–D | margin / cell | Δ vs shipped | distinct outcomes / 16 |
| --- | --- | --- | --- | --- |
| **all eight OFF** (≡ wave 6) | 0–0–16 | **+0.00** | −14.81 | 2 |
| **all eight ON** (shipped) | **16–0–0** | **+14.81** | — | 9 |
| − G1 stillness lock | 10–4–2 | +4.56 | **+10.25** | 12 |
| − G2b denial fire control | 16–0–0 | +14.31 | +0.50 | — |
| − G3 screen | 16–0–0 | +14.81 | 0.00 | — |
| − G4 cap discipline | 16–0–0 | +14.81 | 0.00 | — |
| − G5 erosion urgency | 16–0–0 | +14.81 | 0.00 | — |
| − G6 interrupt-priced shield | 13–2–1 | +9.00 | **+5.81** | — |
| − G7 invest | 16–0–0 | +14.12 | +0.69 | — |
| − G8 salvage step | 0–0–16 | +0.00 | **+14.81** | 2 |
| **+ G2a root-for-denial** (refuted) | 7–8–1 | +0.31 | −14.50 | 11 |
| **+ G2c root-exit on demand** (refuted) | 8–8–0 | +2.69 | −12.12 | — |

Per-rule verdicts, and five of them are negative results:

- **G2a — root for denial. The arm's advertised leverage for this class, and it
  is backwards.** Two gates were built and measured. The permissive one prices
  the weight I lose against the revert rate a rooted gun declares, and it anchors
  constantly: **487 anchored ticks over sixteen cells bought five points of
  denial** and turned +14.81 into −13.44 in the configuration it was first
  measured in. The strict one demands a live target — a visible body of the
  *claiming* team, standing on the region, on a clear ray inside the rooted gun's
  own declared reach — and relief that keeps weight on the ground; it then fires
  almost never (2 anchored ticks) and still measures 8–7–1 at −0.25 against the
  predecessor's own tenure arithmetic, because a veto costs the anchors the old
  rule would have taken. **SHIPPED OFF**, and the reason generalises past this
  map: a body of positive weight subtracts from the enemy multiplier with no line
  of sight, no vision, no cooldown, nothing to dodge and no arc to turn it, while
  a bolt's revert is the same size and conditional on all four. The gun can never
  beat the body at denial. It can only beat it at killing, which is what the
  wave-6 bargain already roots for.
- **G2c — root-exit on weight demand. Refuted, and it was hiding inside G2b.**
  Bundled, the two halves measured as one rule costing 5.4 a cell. Split, G2b
  *gains* 0.5 and G2c *costs* 12.1: leaving the turret the moment the surface
  could build pulls guns off positions that were still paying, and does it on the
  tick the claim arithmetic first turns positive, which is the tick the gun is
  most useful. Two rules with opposite signs are unpriceable together; this is the
  second wave running in which splitting one was worth more than the rule.
- **G1 — stillness lock. The largest single gain, +10.25 a cell.** It is also the
  rule that makes the others measurable: a body that keeps re-ranking its post
  never accumulates a claim, so before G1 the channel simply did not run.
- **G6 — interrupt-priced shield, +5.81 a cell.** The shell raised over a running
  claim, 92 times across sixteen cells. It is the only mechanism this class has
  for standing on ground it is taking and being shot at for nothing.
- **G8 — salvage step, +14.81, and the number is honest but not the whole story.**
  Without it the mirror is **0–0–16 with zero captures**, byte-for-byte the
  wave-6 stall: three stationary bodies against three stationary bodies is zero
  surplus, nobody gains, and the channel rules are correctly inert because no
  claim is running. What G8 does is make somebody *move* — a body steps off to
  bank an assay — and the resulting asymmetry starts a claim that G1 and G6 then
  convert. **So these rules are not additively separable in this cell**, and the
  leave-one-out table says exactly that: G8 unlocks the game and G1/G6 win it.
  Reporting +14.81 for G8 alone would be true and misleading, so both are stated.
- **G2b — denial fire control, +0.50.** Small, correct, cheap. Kept.
- **G7 — invest, +0.69.** It buys 16 tiers over 16 cells and leaves 93 scrap
  unspent. Kept; see the economy section for what it bought and why.
- **G3, G4, G5 — measured exactly inert, and each for a checkable reason.**
  Decision-for-decision identical to the shipped artifact (760 of 760 on a
  checked cell), so this is inertness rather than a rounding to zero.
  - **G3 (screen)** is a rank term among tiles OFF the surface. The bodies that
    would take one are the bodies G1 has already pinned or that are dead; the
    tie-break never decided a post in sixteen cells.
  - **G4 (cap discipline)** *does* bind arithmetically — on one checked cell the
    surface list would be cut on 113 of the 500 ticks and three bodies are alive
    for 103 of them — and still changes nothing, because **G1 subsumes it**: a
    body the stillness lock has pinned cannot be moved by a change to which post
    it ought to want. That is a genuine interaction and I would not have found it
    without the leave-one-out.
  - **G5 (erosion urgency)** is inert *by construction* once G2a and G2c ship
    off: its only remaining consumer is G4.

## Measured record versus the rebuilt wave-6 predecessor

Opponent: this lineage's own wave-6 source rebuilt from the frozen tree under
0.9.27/0.10.10 (`060ecfa0e846…`; the frozen artifact is `6a62b5c35d27…` — the
drift is the documented SDK-republish effect, not behaviour).

| pairing | cells | distinct outcomes | W–L–D | margin / cell |
| --- | --- | --- | --- | --- |
| **bastion mirror, both sides** | 16 | **9** | **16–0–0** | **+14.81** |
| — as team 0 only | 8 | 1 | 8–0–0 | +16.00 (base-breach every cell) |
| — as team 1 only | 8 | 8 | 8–0–0 | +13.63 |
| **siege mirror (channel, no economy), both sides** | 16 | **2** | 0–0–16 | +0.00 |

**The side asymmetry that dominated wave 6 is gone in this pairing** — 8–0–0 from
both sides where revision 6 measured 8–0–0 and 2–6–0 — and I did not chase it;
it is a consequence of the front actually moving.

**Siege-only is a zero and the zero is exact.** On `--capture channel` without
`--economy scrap`, the mirror is **decision-for-decision identical to the
predecessor, 698 of 698**, and every cell is a draw with zero captures. Every
channel rule is correctly inert there because none of them binds until a claim is
actually running, and on a 3-v-3 stalled surface no claim ever runs. That is the
clean statement of this revision's dependency: the channel rules are the payoff,
the economy is what creates the asymmetry that lets them fire, and **on this map
against this opponent the channel alone cannot break a mirror.**

## Cross-class and cross-doctrine records

All on the bulwark side (team 0), bastion, 8 seeds.

| opponent artifact | pair | revision 7 | rebuilt wave 6 | distinct outcomes |
| --- | --- | --- | --- | --- |
| `vector-edge` | bulwark-vs-striker | 8–0–0, +16.0, breach t69 | 8–0–0, +16.0, breach t69 | **1** |
| `still-water` | bulwark-vs-striker | 8–0–0, +16.0, breach t63 | 8–0–0, +16.0, breach t63 | **1** |
| `ledger-fly` (+`wane`) | bulwark-vs-fabricator | 8–0–0, +16.0, breach t56 | 8–0–0, +16.0, breach t56 | **1** |
| `gate-stone` | bulwark mirror, both sides | 16–0–0, +16.0, mean t86 | 16–0–0, +16.0, mean t86 | 1 |
| `march-wall` | bulwark mirror, both sides | **0–16–0, −16.0**, mean t184 | **0–16–0, −16.0**, mean t266 | 2 / 3 |

**Three of these measure nothing about this revision and I will not pretend
otherwise.** Against every wave-8 baseline striker and fabricator artifact the
match base-breaches between ticks 56 and 69 — before the first scrap deposit at
120, before any of the three slots past the first has unlocked, and before either
side has a claim long enough for the interrupt to bite. Revision 7 and revision 6
play those cells **identically**, to the tick. One informative observation per
pairing, eight seeds spent to get it.

**Against `march-wall` the revision genuinely differs and genuinely loses.**
Decisions diverge at tick 62 (`channelling: 1 still against 0` where the
predecessor said `holding the scoring surface`), and the split is two-sided:
238 ticks survived against the predecessor's ~170 on one side, 130 against 361 on
the other, for a worse mean. The **outcome** is 0–16 for both revisions, and the
ablation says no rule of mine causes it — removing G1, G6, G8 or G2b leaves it
0–4–0 at −16.0 on the sampled seeds. It is a class-internal matchup this lineage
already lost in wave 6, and I am recording it as the top open item rather than
attributing it to the wave-8 layer.

## Channel and economy usage, shipped artifact

Sixteen bastion mirror cells, candidate side.

| | shipped | all-off (≡ wave 6) |
| --- | --- | --- |
| captures completed | **48** | 0 |
| captures conceded | 20 | 0 |
| ticks the stillness lock pinned a body | **439** | — |
| enemy claims eroded to zero | **117** | 0 |
| ticks with the shell raised over a running claim | **92** | — |
| progress reverted off our own runs by hostile fire | 141 | 0 |
| progress we reverted off theirs | **268** | 0 |
| turret shots (`shoot-direction`) | 243 | 0 |
| turret ticks | 570 | 0 |
| turret-sourced denial damage | **8** | 0 |
| `invest` cast | **26** | 0 |
| — succeeded | **16** (all `plate`) | 0 |
| — `Blocked` | **10** | 0 |
| tiers held at the horn | **16** | 0 |
| scrap left unspent | 93 | **240** |
| assay steps (pile taken under the boot) | **661** | 0 |

Four of those want saying out loud.

- **The predecessor banked 240 scrap over sixteen cells and spent none of it.**
  Fifteen a match, entirely from wrecks falling where it was already standing,
  with no economic code at all — and no verb to spend it. That was the actual
  hole this wave, and it is bigger than any single rule: a whole tier a match
  left on the floor by a doctrine that never read the block.
- **All ten `Blocked` casts are the documented simultaneous-reservation race**,
  and I checked rather than assumed: on exactly ten ticks two of my bodies cast
  `invest` against a bank covering one tier, the canonical
  `(teamId, unitId, lifeId)` order resolved the first and the second came back
  `blocked` with its argument intact. That is the existing grammar working, and
  the cost is zero — the loser's tick was going to be a wait either way, which is
  precisely why the purchase lives in the idle fallback. Worth knowing that a
  team with several idle bodies will "waste" casts at this rate by design.
- **Every successful purchase was the ceiling track**, and that ordering is a
  correction. My first derivation priced a tier by the declared gap it closes,
  which prefers sight (a gun reaching 6 behind eyes reaching 4) over a ceiling
  worth less than one bolt of the heaviest declared hit. Measured on the same
  sixteen cells: ceiling-first **16–0–0 at +14.81**, gap-model sight-first
  **15–0–1 at +13.81**. The shipped rule reads the declared claim interrupt and,
  where one exists, buys the ceiling first — because on such a ruleset the body's
  standing time on the point *is* the claim, and team perception is an immediate
  union that already fills most of a sight gap.
- **Turret-sourced denial damage is 8 across sixteen cells**, against 268 total.
  That single number is the whole case against G2a, and it is why I measured the
  rule instead of shipping the owner's read.

## The engine defect that removed a third of the store

**Buying the gun-travel tier aborts the match.** Any bot that submits
`invest` with the track whose declared effect is
`mobile-attack-travel-tiles-delta` gets:

```text
error: A retained projectile must preserve its exact resolved committed path. (Parameter 'projectiles')
```

and the run ends there — **no result, no standings, and no replay written at
all, not even a partial one**. Isolation, all on the bulwark mirror with
`--economy scrap`:

| variant | seeds 4 / 5 / 7 |
| --- | --- |
| this revision, all rules on | abort / abort / abort |
| `Invest` switch off | 499 / 499 / 499 (clean) |
| `Salvage` switch off, invest on | abort / abort / **499** |
| forced travel-track-only | **abort / abort / abort** |
| forced health-track-only | 499 / 499 / 499 (clean) |
| forced sight-track-only | 499 / 499 / 499 (clean) |

Reproduces identically under `--runtime wasm` and `--runtime in-process`, on the
`forge` arm (`--economy scrap`, no channel) as well as `bastion`, and does **not**
reproduce for the rebuilt wave-6 predecessor or for two sibling artifacts on the
same seeds — because none of them buys anything. Across twelve seeds of my first
build, five aborted.

The mechanism is the obvious one: a tier that rewrites `maxTravelTiles`
invalidates the committed path of a bolt already travelling under the old number.
**There is no observation a bot can gate on, and I measured that rather than
assuming it:** a variant that declines the purchase whenever this body's own
`VisibleProjectiles` is non-empty **still aborts on eight seeds out of eight**,
because the offending bolt is somebody else's and outside a bulwark's four-tile
vision.

So this revision declines the track, in one switch (`Garrison.TravelTier`),
labelled a defect workaround rather than dressed up as doctrine. Two consequences
worth flagging beyond my own artifact:

- **It is a live hazard for this whole wave's evidence.** The travel track is the
  most obviously attractive of the three for every class in the cell — it is the
  standoff race the class table is built around — so any author who buys it
  produces aborted cells, and an aborted cell leaves no replay to notice it with.
- **The failure mode is the worst available one.** A partial replay, or a
  disqualification with a result, or a `Blocked` on the action, would all be
  diagnosable. An exception out of the runner with no artifact is indistinguishable
  from a harness problem until you bisect your own switches.

## Timings (Apple Silicon, warm Docker builder)

| Step | Time |
| --- | --- |
| `dotnet build` of the editing project | ~2 s |
| `nilbots build` (cache hit) | <1 s |
| `nilbots build --no-cache` (cold, Docker) | ~15 s |
| `qualify --suite frontline-qualification-5` (WASM) | ~11 s |
| one 500-tick bastion match (in-process) | ~2 s |
| one 16-cell sweep | ~35 s uncontended |
| one full 12-config ablation round (build + sweep) | ~12 min |

Three full ablation rounds were run, because the shipped configuration moved
twice (G2 split, then the invest ordering) and an attribution table measured
around a base that then changes is worthless. The structural fix that mattered is
the same one the predecessor found: build variants serially against the one
Docker builder, then run every sweep in parallel.

## Repairs and strategy passes

One doctrine pass; everything else is a decision point wired to it or a repair
forced by a measurement.

1. **Repair — the control-policy reader was answering a different question.**
   `ContractLens.SurplusWeightScalesGain` tests the policy string for
   `net-positive-objective-weight-difference`, which the channelling policy does
   not contain, so the doctrine concluded "binary control" on an arm that scales
   gain by a capped stationary surplus. `ChannelRules` reads the contract's own
   fields instead; the old reader is left alone because it is still correct for
   the policy it names.
2. **New — `Channel.cs`.** The declared arithmetic plus this tick's claim, denial,
   stationary and self-stillness state, derived from the frozen observation and a
   one-tick position memory. There are no new observation facts on this arm, so
   "did that body change tile" has to be remembered; a life born this tick has no
   memory and the rules say that counts as stationary, so the missing entry is an
   answer rather than a gap, and an enemy seen last tick and not this one is
   assumed to have held its tile, which is the conservative direction.
3. **New — `Salvage.cs`.** The declared economy, the store's verb driven off the
   legality mask, the pile detour, and the two refusals (the harvest walk, the
   travel tier).
4. **Strategy — GARRISON.** Eight rules at seven decision points: the step gate,
   the station ranking and its cut, the shield entry, the tenure gate, the
   mobilize exit, the target builder, and the idle fallback.
5. **Repair — split the turret rule in three.** Root-for-denial, denial fire
   control and root-exit were one switch and unpriceable together: measured
   separately their contributions are −14.50, +0.50 and −12.12.
6. **Repair — the turret gate priced a certainty against a hope.** The first
   version compared weight I would certainly lose against a revert rate the gun
   would only earn given a target, a line, a ready cooldown and a bolt nobody
   deflected. Rewritten to demand the target; still negative; shipped off.
7. **Repair — the invest ordering.** Gap-closing preferred sight; the interrupt
   says the ceiling is the claim. Measured, +1.0 a cell.
8. **Repair — decline the travel tier.** See above.

## Top 3 frictions

### 1. The store's first new verb since Split aborts the match on one of its three tracks, silently

Everything about `invest` is well made — the mask prices the ladder so a bot
never does arithmetic, the tier vector is positional against the declared track
order, the purchase is public on the tick it happens through the ordinary
mode-changed fact, and the simultaneous-reservation race resolves in the existing
canonical order. I wrote the whole purchase routine from
`EXPERIMENTAL-FRONTLINE-CLASSES.md` and the SDK doc comments without one wrong
turn.

And then buying one of the three declared tracks throws
`A retained projectile must preserve its exact resolved committed path` out of
the runner and writes **nothing**. Not a partial replay, not a result, not a
disqualification — the cell simply does not exist. Full isolation table above;
the short version is that it is the travel track, on both runtimes, on every seed
tried, and no bot-observable state avoids it.

Three asks, in order of value:

- **Fix the retained-projectile path when a tier changes a projectile envelope**,
  or apply the tier at the next launch rather than to bolts already committed.
  The contract already says a purchase settles *after* every bolt has flown, so
  the intended semantics are unambiguous; this is the implementation disagreeing
  with its own documented ordering.
- **Never lose the replay.** Whatever the failure, a partial replay to the
  offending tick would turn a two-hour bisection into a two-minute read. The
  format already supports `partial: true`.
- **Until then, say so in the brief.** One clause in the `invest` section —
  *the travel track is currently unsafe, do not buy it* — would save every author
  in this wave the same bisection, and would stop the population's evidence from
  quietly acquiring aborted cells.

### 2. Eight seeds, one game: the arm has made pairings MORE deterministic, and the packet still asks for seeds

Revision 6's DX corrected its predecessor for claiming seeds were uninformative,
and proved five seeds gave five distinct games. That correction has expired. On
this arm, over the pairings I ran:

| pairing | cells | distinct outcomes |
| --- | --- | --- |
| bastion mirror vs rebuilt wave 6 | 16 | 9 |
| siege mirror vs rebuilt wave 6 | 16 | **2** |
| vs `vector-edge` / `still-water` / `ledger-fly` (one side) | 8 each | **1 each** |
| vs `march-wall` / `gate-stone`, both sides | 16 each | **2** / **1** |

Eight seeds against a fixed cross-class opponent produce **one** observation. The
replay *hashes* all differ, which is the trap: a table of sixteen distinct hashes
looks like sixteen observations and is two. I am disclosing the counts rather
than the cell totals for exactly that reason, and the platform could help in one
line: `--seeds` output that says *n cells, k distinct outcomes*, or a
`replay --summary` field for the decision-stream digest with provenance excluded.
The ingredients exist — the runner already knows every result.

The deeper version: the channel makes the early game more decisive (threshold 8,
three pushes to breach), so cross-class cells now end at tick 56–69, **before the
first scrap deposit at 120 and before the second slot unlocks at 120**. An arm
whose economy starts at 120 and an arm whose games end at 60 do not meet. Two
thirds of the mechanics I was briefed on cannot be exercised in the pairing the
brief is most interested in, and no amount of seeding fixes it.

### 3. "Stillness" is the only load-bearing fact on the arm with no observation behind it

The addendum is explicit and correct — *there are no new observation facts;
`captureProgress` and `claimingTeamId` keep their exact published shape* — and it
is right that no schema needed to change. But the rule that decides who is
capturing is *did this body's tile change this tick*, and that is the one input a
bot must reconstruct from memory rather than read.

The reconstruction is not hard and it is not the friction. The friction is the
three places it is subtly wrong and none of them is signposted:

- **A life born this tick has no previous position**, and the rules say it counts
  as stationary. So the missing entry is an *answer*, not a gap — a bot that
  treats "unknown" as "moving" under-counts its own claim on every spawn tick.
  The brief says this in one clause; the SDK says it nowhere, and the SDK is
  where a bot author is when they write the dictionary.
- **An enemy that left vision and came back cannot be judged at all.** I assume
  it held its tile, which over-counts their claim and is the safe direction — but
  which direction is safe is a doctrine decision I had to derive, and the brief's
  "an enemy claim's movement is partial information exactly as it always was"
  understates it: the *inputs to the arithmetic* are now partial, not just the
  output.
- **A blocked move did not move**, so stillness has to be read from the
  *position*, never from the submitted action. This one is stated plainly in the
  brief and is the single most likely way to get the rule wrong, because the
  natural implementation is "did I submit a move".

One sentence on `ModeObservationState.Frontline` — *under a stationary-claim
policy, whether a body changed tile is derived from consecutive observations; a
life with no previous observation counts as stationary* — would put it where it
is needed. A published per-body `movedThisTick`, or a `claimWeight`/`denialWeight`
pair on the mode state, would remove the class of bug entirely; the engine
computed both authoritatively on the tick it published the progress.

## Documentation gaps

- **`stationaryGainMultiplierCap` is documented as a ceiling on gain and is also
  a floor under a doctrine's body count, and only one of those is said.** The
  brief's own table says the third, fourth and fifth stationary bodies "buy you
  nothing extra in speed" — true — from which an author reasonably concludes they
  are surplus. They are not: they are denial weight, which subtracts from the
  *enemy's* multiplier whether they move or not, and denying is what the extra
  bodies are for. My cap-discipline rule was written from the first reading and
  the second reading is what made it correct (cut the surface list only while MY
  team is the one building). Worth a clause: *the cap limits what your surplus
  buys, not what your presence denies.*
- **The turret's objective weight of zero is stated three times; that it also
  removes the body from the ECONOMY is stated once, in a different section.** A
  form declaring weight zero can neither pick up nor carry, and completing a
  transition into one drops the load on the floor. That is a real interaction
  between two arms — anchoring next to a pile you are carrying is a donation —
  and it lives in the economy section where a class-reading author will not be.
- **Nothing says what a match's expected LENGTH is on this arm, and it is the
  fact that decides which mechanics exist.** Threshold 8 and three pushes to
  breach means a decisive cell ends around tick 60; the economy's first deposit
  is at 120 and the second unit slot unlocks at 120. An author budgeting effort
  across the channel and the economy has no way to know from the brief that the
  economy is a late-game mechanic in a game that frequently has no late.
- **`purchaseMode` is the only way to know whether the verb exists, and the
  legality mask is the only way to know whether it is affordable — and the brief
  tells you to read both, which is right — but nothing says what an unaffordable
  track looks like.** It is absent from the constraint, not present-and-refused.
  Obvious in hindsight; a sentence saying *an empty `UpgradeTrackConstraint` is
  the ordinary state, not an error* would have saved a debugging pass.

## Hardcoding temptations

All resisted; the ones this revision created:

- **"The cap is 2, the erosion multiple is 4, the threshold is 8."** They are, on
  this arm. `ChannelRules` reads all three and treats zero as "the field is
  absent, the mechanic does not exist", which is how a ruleset without a channel
  makes every rule provably inert rather than accidentally quiet.
- **"The tracks are edge, plate and optic."** Nothing in the source names a
  track. The choice is made against the declared `effect` policy IDs and the
  declared per-tier magnitude, and the refusal is expressed as *a track whose
  effect changes a projectile envelope*, not as a name.
- **"A screen is a body one tile in front."** It is a body on the clear
  eight-way ray between a live muzzle and a body that is paying into our claim,
  strictly between them, and off the region — every clause of which is a declared
  collision or interrupt-scope fact.
- **"The turret reverts one per tick."** It reverts
  `revertPerDamagePoint × damagePerHit` per `cooldownTicks`, computed as a
  numerator over a period so no rate rounds to zero, and read from the anchor
  route's own *target form* rather than from the class table.
- **"Damage on the point costs progress."** Only damage to the CONTROLLING team's
  bodies ON the region, which is why a screen is free. The two clauses are read
  separately off `claimInterrupt.Scope`, so a ruleset that scopes it differently
  turns the screen rule off by itself.
- **"Buy the biggest number."** The mask decides what is affordable and the
  contract decides what a tier does; the doctrine only ranks, and the ranking
  branches on whether an interrupt is declared.
- **"The veins are at (11,1) and (11,13) on 120/200/280/360."** The doctrine
  never reads the schedule at all — it refuses the errand — and the piles it does
  take come from `mode.ScrapPiles` with their own published expiry.

## Confusing terminology

Carried forward and still true: "Anchor"/"Mobilize" are prose words with no
contract representation; `irreversibleForLife` reads backwards; "Available"
versus "will succeed"; "facing-locked" restricts movement, not rotation; "hold"
is three different things; "deflect" sounds defensive and is an attack; "free"
and "open" are two placement arms and one English word; "choke" is a corridor, a
pressure situation and a map feature; "Blocked" is a legality refusal, a physical
collision and a doctrine's own blacklist.

New this revision:

- **"Claim" is four things.** The running progress (`captureProgress`), the team
  that owns it (`claimingTeamId`), a team's *stationary weight* this tick (the
  new arithmetic), and a "run" — the continuous stretch of control the interrupt
  reverts. The interrupt section needs all four in three sentences and gives them
  one word.
- **"Denial weight" counts bodies that are attacking.** It is the count of *your*
  bodies on the region as seen from the enemy's arithmetic, so the same body is
  claim weight in one sentence and denial weight in the next depending on whose
  turn it is to be described. I named the two fields `Claim` and `Mine` in the
  end, because "my denial weight" reads as something I am doing to myself.
- **"Erosion" is the same verb as decay and a different mechanic.** Decay is the
  clock; erosion is controlling a point while an enemy claim stands, at a
  multiple. The brief is careful to say the damage revert is "a separate erosion
  path that neither consumes nor resets `decayTicksElapsed`" — which is three
  erosions in one sentence.
- **"Assay" appears once, in the economy section, and is the most important word
  in it** — it is what makes ignoring the deposits survivable, and it is the only
  part of the economy a front-line doctrine ever touches.

## What I could not evaluate

- **Whether the turret has any job at all on this arm.** I refuted the assigned
  one with two gates and eleven configurations, all in a bulwark mirror and all
  against one predecessor. The rule that ships is the wave-6 tenure bargain,
  which still roots (570 turret ticks a sixteen-cell sweep) and still wins; what
  I have shown is that *rooting for denial* is worse than standing, not that
  rooting is worthless.
- **Anything about the channel against a live striker or fabricator.** Every
  cross-class cell base-breaches by tick 69, so the interrupt, the cap, the
  erosion multiple and the entire economy are untouched in them. Revision 7 and
  revision 6 are decision-identical there. This is the biggest hole in the
  evidence and it is structural, not budgetary.
- **The `march-wall` matchup.** 0–16 for both revisions, no rule of mine changes
  it, and I did not have the budget to work out what does.
- **G3 and G4 as claims.** Contract-correct, priced, decision-identical to
  shipped on every cell tried. G5 is inert by construction once the two refuted
  rules ship off, which is a stronger statement than a zero.
- **Whether the ceiling track keeps winning past tier 1.** Sixteen cells bought
  sixteen tiers — one each. Nothing in this evidence says anything about tier 2,
  about a bank that reaches 30, or about the third track, because the travel
  track is unbuyable and the horn arrives first.
- **Whether the stillness lock should also refuse a dodge.** It does not: a body
  that would eat a bolt still steps aside, which costs a tick of gain to avoid a
  revert of the same size or larger. That is a trade I reasoned about and never
  measured.
