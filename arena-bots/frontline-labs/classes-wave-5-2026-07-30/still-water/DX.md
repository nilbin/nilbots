# DX notes — Still Water, revision 5

## Isolation

Written from this entrant's own authoring session, its own sparring replays
against its own rebuilt predecessor and its own scratch variants, and its own
qualification report. No other entrant's source, standings, replays, or
aggregate balance report was opened in this revision. Work was confined to
`arena-bots/frontline-labs/classes-wave-5-2026-07-30/still-water` plus the
private scratch directory `sandbox/still-water-w5-scratch-a2f9d41c`, which is
uniquely named and belongs to this lineage. The wave-4 directory
(`classes-wave-4-2026-07-30/still-water`) was read but not modified; the
predecessor used for sparring is a *copy* of that source inside the private
scratch directory, rebuilt on the current toolchain. Nothing was committed to
git.

Permitted material only: the author packet, the Labs rule card, the class
addendum (read in full, including the new aim, five-slot-variant and
stance-ground sections), `templates/botarena-generic-actor/`,
`src/BotArena.Sdk/` types and XML documentation, this lineage's own wave-4
directory and replays, and `sandbox/cli-publish/`.

### Disclosure: a shared scratchpad, again

**This session created working files in a shared scratchpad directory before
noticing it was shared, and in listing its own files there saw the *names* of
other entrants' wave-5 brief files and of other agents' analysis scripts and
output directories.** The harness advertises a session scratchpad at
`/private/tmp/claude-502/<...>/scratchpad` for temporary files, and the
measurement harness for this revision was written there. That directory turned
out to contain, among other things, `w5-brief-<other-entrant>.md` for the rest of
this wave, earlier `w4-`/`r3-` briefs, and directories and scripts belonging to
other runs.

What was and was not exposed, stated precisely:

- **Seen:** a directory listing. That listing includes other entrants' lineage
  names and the fact that per-entrant brief files exist, plus script and
  directory names such as `p2-gates.py`, `arm_summary.py`, `unblind-review.py`,
  `cross_fab` and `cross_bulwark`.
- **Not seen:** the contents of any of those files. No other entrant's brief,
  source, doctrine, replay, standing, or aggregate report was opened, and
  nothing in the listing influenced any decision in this revision — every design
  choice above was fixed before the listing happened, and the only thing that
  changed afterwards was where my own scripts live.
- **Repaired:** all of this session's tooling was moved to
  `sandbox/still-water-w5-scratch-a2f9d41c/tools/` and every hard-coded path to
  the shared directory was removed. The tooling is the only artifact that was
  ever written there.

The packet's rule and the wave-1 precedent both say to disclose exactly this, so
here it is. The lineage-level lesson is worth more than the incident: **this is
the second exposure in this lineage through a scratchpad whose name was handed
to the agent rather than chosen by it.** The wave-1 case was a guessable name
(`mirror1`); this one is worse, because the harness *recommends* the shared path
in its own instructions while the author packet forbids it, and an author who
follows the harness is out of compliance by default. The fix is not more
discipline — it is that a competitive-isolation experiment should not run in a
harness that advertises a shared temp directory. See friction 3.

**Carried forward from v1, still disclosed:** during the first authoring pass a
shared scratchpad directory name (`mirror1`) collided with another agent's run
and aggregate statistics from one `fabricator-vs-fabricator` replay that was not
mine were read before I noticed. No source, standings, doctrine, or striker
material was seen, and nothing from it influenced any revision. The disclosure
stays with the lineage.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `still-water` |
| Authoring lineage | `still-water-v1` |
| Revision | 5 (one budgeted strategic pass — the launch offsets and the open ground; mechanical/contract repairs free) |
| Class | `striker` (declared in `botarena.json`) |
| Role | `verdict-doctrine` |
| Doctrine | patient interceptor, re-tabulated for the three-lane cone and re-priced for a stance that may rise on the point |
| Primary cell | `--pendulum keel --skills kit --bend universal --aim offset --stance-ground open --movement facing-locked` → `frontline-labs-1-striker-vs-striker-sail-open-facing-locked` (`deck` spells itself as `sail-open` where no fabricator is in the cell) |
| Target tier | cumulative T4 (`frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`) |
| Predecessor | `arena-bots/frontline-labs/classes-wave-4-2026-07-30/still-water` (frozen, untouched) |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` (unchanged since r4) |
| Rule card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` (unchanged since r4) |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `3cb2814b7a853d0547038d2d4d65a498c0d82e06357392957caf4efbdd365e5c` (r4 froze against `b91047df…`; the aim, five-slot-variant and stance-ground sections are new) |
| Starter helper synced | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` (vendored byte-identical; unchanged since r4) |

| Artifact | Value |
| --- | --- |
| `out/bot.wasm` sha256 | `dd2b878bd7595418230d805350332a729a118d7b7694db6777a274b9cda48547` |
| `out/bot.wasm` size | 3,365,952 bytes |
| Deterministic source-tree hash | `4508c97082575b958d9e19f7672f05417c8f2b1f67239bd9c83c50851c15f57d` (sha256 of the sorted per-file sha256 listing of all `.cs` + `.csproj` + `botarena.json`, excluding `bin/` and `obj/`) |
| `evidence/t4/qualification.json` sha256 | `3cfba92ba3a422085508f63cd6eb554dfc2b108b3552b9ba8d05a2e711be9e48` |
| `evidence/t4/prerequisite-t3/qualification.json` sha256 | `9bc132548ce3a5d457f9f821d5c0ab0db4c114c70faf9a48be493a7a4e49018a` |
| `evidence/t4/prerequisite-t3/prerequisite-t2/qualification.json` sha256 | `f3995b57f9a7c24b0037168e2d32043ea6ea4cc66740936ec9064b67923edb88` |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` (unchanged from r4 — the union profile is immutable) |
| `evidence/doctrine-deck/replay.json` replay-v3 hash | `d3e16a50c5c6b1fbdfc8f1458be39f9fe86c63d1b1979583595f43c5d9fb13a3` (`nilbots verify` OK — the primary cell versus the rebuilt predecessor, seed 104729, +30 territory) |
| Verified probe replays | **37 of 37 `nilbots verify` OK** (36 qualification-chain replays plus the cited doctrine replay) |
| Build reproducibility | a second `nilbots build --no-cache` from the frozen source returns the same `dd2b878bd759…` |
| Sparring baseline | wave-4 source rebuilt on the current toolchain, artifact `775bced29ff720cace54ba1143210328700e776e5ab0acb5cc0946eb31a7dc36` |
| Class-variant opponents | the same wave-4 source with only `botarena.json`'s `"class"` changed → **the same artifact hash** `775bced2…`, so the fabricator and bulwark pairings run one brain on three chassis |
| Toolchain | controlled `nilbots build --no-cache`, CLI **0.9.21** (the brief names 0.9.20), SDK 0.10.6, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, game rules 0.5, WASI p1 core module, macOS host via the platform-matched Docker builder |

Per-file source hashes at freeze:

```
04dbf8d8f5b4cd77514e51bf18a6e886da93105256841db96079e085f7da25c6  ActionBook.cs
567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627  ArenaBasics.cs
d1fb75149fe7d46f4b6c6e20edc03f5daef103d23fcf5201adc9ea4efa6b1916  Doctrine.cs
df05fd11c3f1efa2dc032eebc9a11f65478aee5dc61e3447d2115935dedb12a6  Field.cs
4c2ab7b621705f8887d4598d6120f41cae25ae66de8cec5bfc97dd8bc249ca3b  ForkPlanner.cs
4cf2354e43630de4c7edb6f4b7492112020ea57d7f391d4512f31057a7eaafbb  Quarry.cs
a62cce328cfd13051802869d54f0396fa59cea5df26e96b51fff48181653bf3a  Ratchet.cs
068430ebd8315ab9755adb75fc519b96d1102259fa937a1dd5376d83e9354437  Stance.cs
590da1032931dfda6e3377d3505e2d6bbe983250830b7bb2297290a6287fee5d  StillWater.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  StillWater.csproj
90a42a29189ed2c85bc419ad0a11f154943fd2b830cd21cb27635df158c60c28  ThreatField.cs
0ed9bc7973a0d815aad0b89434726c54caa4b3ed9c88ddb812462fca2e087e6f  botarena.json
```

`ActionBook.cs`, `ArenaBasics.cs`, `Field.cs`, `Ratchet.cs`, `StillWater.csproj`
and `botarena.json` are byte-identical to the r4 freeze. `ForkPlanner.cs`,
`Doctrine.cs`, `Quarry.cs`, `Stance.cs`, `StillWater.cs` and `ThreatField.cs`
changed.

### The frozen predecessor did NOT fault, and that is worth recording

The brief warns that frozen wave-4 artifacts fault on this wave's contracts.
**Measured on CLI 0.9.21, the frozen r4 `bot.wasm` (`8ae62751…`) does not
fault**: it plays a full 500-tick match on the primary cell and scores exactly
what the rebuilt source scores (−18 as team 0 on seed 104729, identical to the
rebuilt mirror). Only the replay hash differs, because the artifact hash is
embedded in the replay. Every number below is nonetheless measured against the
**rebuilt** predecessor, because the brief requires it and because a rebuild is
the only way to be sure the two sides share a toolchain — but the warning did not
reproduce here, and an author who takes it on faith will not notice that it has
stopped being true.

## Doctrine in one paragraph

Still Water refuses the closing duel: it stands one bend's reach behind the
contested point, puts the gun across the approach, and makes the other side spend
tiles and tempo coming to it, taking the ground last but never later than the
clock can still pay for. Revision 5 keeps that doctrine and rebuilds the geometry
underneath it, because the restored ±45° launch offset means one facing now owns
three lanes and each of them may still spend the bend — so the reachable set from
a pose is not "the facing lane, or a bend off the dominant axis" but the union of
five families (lane, the aim-only diagonal *slip*, the wave-4 *fork*, the
*flatten* that launches wide and straightens, and the *hook* that turns almost a
quarter circle onto ground more lateral than forward), which on an open field is
124 tiles from one facing where wave-4's table admitted 52. Everything that
consumed that table moved with it: cover stopped being a count and became a
quality, because when every pose covers every body somehow the only thing a
rotation is still choosing is whether the bolt arrives on the shortest path or
spends a bend that a corner can eat; the standoff band moved one tile inward,
because the widest lateral run is now the deepest tile still inside the bend
window rather than one tile past it; the threat map's "already pointed at me"
tier and the shared-cone tax that won wave 4 are computed from the same five
families the gun uses; and a diagonal bolt in flight is no longer treated as
proof that its bend is spent, because it may have launched that way. The other
half of the revision is the open ground: the volley entry route's forbidden-tag
list is now empty while the map still carries the tag on 112 of its 233 open
tiles, so the placement question moved from the map to the route, and the fan —
which keeps objective weight 1 — became a verb that can guard the point it is
standing on instead of a shoulder verb that wave 4 declined 120 matches running.
Re-pricing that decline cuts both ways and both directions are measured: one body
is no longer worth a stance in the open at all, because every ray of the fan is
now a lane the ordinary gun can aim down for one tick of nothing, so the margin
the displaced bolt is owed scales with the number of lanes it could have chosen;
but a fan raised on the point, against a body that has already stopped moving,
seals the tiles an attacker must enter while the capture clock keeps running, and
that one case is worth 30 wins to 9 against the rebuilt predecessor where the
looser version of the same rule managed 25 to 23. Farming completes it: a kill is
priced in the ticks its slot declares before the body can return, and in whether
returning also costs its owner a combat action, so an arm whose ordinary children
rebuild on a slower clock is one where killing children is worth more — read from
the lifecycle rather than told.

## Qualification outcome

`nilbots experiment frontline-labs qualify --bot out/bot.wasm --suite
frontline-qualification-5 --out evidence/t4` exits **0**. **Tier awarded: T4**,
`passed: true`, `profileComplete: true`, `balanceEvidenceEligible: true`, profile
`frontline-duel-depth-union-t4-v1`.

| Level | Component | v1 | v2 | v3 | v4 | v5 |
| --- | --- | --- | --- | --- | --- | --- |
| T4 | suppression-choke | PASS | PASS | PASS | PASS | PASS |
| T4 | entry-initiative | PASS | PASS | PASS | PASS | PASS |
| T4 | prediction-chamber | PASS | PASS | PASS | PASS | PASS |
| T4 | front-rotation | PASS | PASS | PASS | PASS | PASS |
| T4 | map-holdout (thin-fronts) | PASS | PASS | PASS | PASS | PASS |
| T3 | wall-terminated-bend | PASS | PASS | PASS | PASS | PASS |
| T3 | strict-corner | **FAIL** | PASS | PASS | PASS | PASS |
| T3 | cadence-parity | PASS | PASS | PASS | PASS | PASS |
| T3 | cooldown-window | PASS | PASS | PASS | PASS | PASS |
| T3 | local-form-safety | PASS | PASS | PASS | PASS | PASS |
| T2 | contract-matrix | PASS | PASS | PASS | PASS | PASS |
| T2 | automatic-life-cycle | PASS | PASS | PASS | PASS | PASS |
| T2 | objective-path | PASS | PASS | PASS | PASS | PASS |
| T2 | direct-fire | PASS | PASS | PASS | PASS | PASS |
| T2 | straight-evade | **FAIL** | PASS | PASS | PASS | PASS |
| T2 | manual-fabrication | PASS | PASS | PASS | PASS | PASS |

Every component passed on the first attempt; zero qualification cycles were spent
on repairs, as in r3 and r4. The suite runs the duel-depth union profile — no
skills, no classes, and, measured from its own contract, **no launch offsets
either** (`minInitialAimSteps = maxInitialAimSteps = 0`) — so every new code path
is exercised there in exactly the shape the contract declares rather than the
shape this wave's arm has: no volley profile means no stance route means the cast
ledger never runs, no `projectileGuard` means the deflection branches never run,
and the interception table collapses to lane-plus-fork because the aim range it
reads is zero. That collapse is verified exact, not hoped for (below).

## The interception table is verified, not asserted

The five-family case analysis is an inverse: given a displacement, name the one
program in each family that could reach it. An inverse is exactly the kind of
code that is confidently wrong, so it was checked by brute force against the
engine's own path rule (`ShotPaths.Preview`, replicated) over all four facings and
every displacement in a ±10 box, on an open field, for both aim arms:

| aim range | tiles reachable from one facing | analytic mismatches |
| --- | --- | --- |
| ±0 (`--aim straight`, and every pre-wave-5 arm) | **52** — lane 8, fork 44 | **0** |
| ±1 (this wave, and the union profile) | **124** — lane 8, slip 16, fork 44, flatten 12, hook 44 | **0** |

Two things fall out. Wave-4's table was *exactly right for its arm* — 52 of 52 —
and on this arm it recognises 52 of 124, denying 58% of its own gun's reach. And
every family's path length is the **Chebyshev distance** to the target, so one
`ImpactOffset(chebyshev)` gives the arrival tick of any covering trajectory with
no per-family correction; a doctrine that prices predictions by arrival time gets
that for free. The verifier lives in the private scratch directory
(`tools/verify_table.py`) and is not part of the submitted source.

**Three envelopes, all read, and the table is exact on two of them.** Measured
from the contracts themselves rather than from the documents:

| contract | aim range | bend window | interval | bends | table |
| --- | --- | --- | --- | --- | --- |
| this wave's primary cell (`striker-bolt`) | −1…+1 | 1–4 | 1…1 | 1…1 | **exact** |
| the T4 union profile (`mobile-bolt`) | **0…0** | 1–4 | 1…1 | 1…1 | **exact** (collapses to lane + fork) |
| base hosted `frontline-labs-1` | −1…+1 | 1–4 | **1…3** | **1…3** | conservative |

So the qualification profile really does hand the bot a different gun — no launch
offsets at all — and the table degrades to precisely wave-4's 52-tile set there,
verified. The base hosted contract is the one place the table
**under-approximates**: it permits up to three bends at intervals of one to three
tiles, and my inverse enumerates a single bend, so a tile reachable only by a
multi-bend program reads as uncovered. That is the same limitation wave 4 shipped
(it also enumerated one bend), it is conservative in the safe direction — the bot
never believes it has a line it does not have — and it is stated here rather than
discovered later. Closing it properly needs a forward search rather than an
inverse, which is a different piece of work than this revision's assignment.

## Measured records versus the rebuilt predecessor

All figures are **paired deltas**: candidate territory on a given (side, seed)
minus what the identical-bot mirror control scored for the same side on the same
seed. `--swap` on two identical artifacts reproduces a match byte for byte, so the
control has N independent samples rather than 2N, and a bare win/loss column
against a predecessor mostly reports the seed's side bias. Opponent = wave-4
source rebuilt on the current toolchain (`775bced2…`). Runtime WASM. Every cell
is the primary game.

### Primary cell — `striker-vs-striker`, five seed sets, both sides (50 matches)

| Seed set | Record | Mean Δ territory | Ticks | Breaches |
| --- | --- | --- | --- | --- |
| 1 (104729…224737) | 6–1–3 | **+8.50** | 499.0 | 1 |
| 2 (271441…479001) | 7–3–0 | **−0.90** | 499.0 | 0 |
| 3 (520493…700001) | 6–2–2 | **+6.90** | 499.0 | 0 |
| 4 (811081…999983) | 6–1–3 | **+9.40** | 498.8 | 1 |
| 5 (104711…505447) | 5–2–3 | **+0.60** | 497.8 | 1 |
| **all 50** | **30–9–11** | **+4.90** | 498.9 | 3 |
| mirror control (r4 vs r4) | — | 0.0 by construction | 498.9 | — |

Sets 4 and 5 were added *after* the configuration was chosen on sets 1–3 and were
not used to tune anything; they replicate (+9.40, +0.60) inside the spread of the
first three (+8.50, −0.90, +6.90). Five independent sets is the strongest
replication this lineage has reported, and it is deliberately five rather than
three because the effect being measured is smaller than wave 4's.

**The honest headline is that the win column moved much more than the margin.**
Wave 4 beat revision 3 by +20.8 mean territory and 120–0–0; revision 5 beats
wave 4 by +4.9 and 30–9–11. That is not a disappointing version of the same
result, it is a different kind of result: wave 4 found a rule its predecessor
lacked entirely (the shared-cone tax) on a board where that rule decided the
mid-game outright. Revision 5 corrects a *geometry* both artifacts already had —
wave 4 was already firing launch offsets, because its enumerator read them off
`shotProgram` (measured on these same 50 matches: 25.2% of its shots carry an
offset); it simply scored every position with a table that denied those shots
existed. Fixing that moves where the
bot stands and which way it turns, and against a strong predecessor on a nearly
deterministic board that converts losses into wins and draws rather than
multiplying margins.

### Per-pairing records — the other two class match-ups

A declared class binds each bot to its class's canonical team side, so in these
two cells this striker is **always team 1** and the pairing cannot be played from
both sides. The control is therefore not a mirror but a **shared opponent**: the
same artifact (this lineage's own wave-4 brain wearing a different manifest class
— permitted, since it is my own variant) plays first the rebuilt wave-4 striker
and then the candidate, on the same seed, and the delta is candidate territory
minus wave-4 territory for the same side of the same match-up.

| Pairing | Ruleset ID | Seeds | Record (this striker) | Mean Δ vs wave 4 | Ticks |
| --- | --- | --- | --- | --- | --- |
| `striker-vs-striker` | `…-sail-open-facing-locked` | sets 1–5, both sides (50) | **30–9–11** | **+4.90** | 498.9 |
| `fabricator-vs-striker` | `…-deck-facing-locked` | set 1 (5) | 0–5–0 | +3.00 | 405.2 |
| `fabricator-vs-striker` | `…-deck-facing-locked` | set 2 (5) | 0–5–0 | −3.60 | 372.2 |
| `bulwark-vs-striker` | `…-sail-open-facing-locked` | set 1 (5) | 0–5–0 | −1.40 | 481.4 |
| `bulwark-vs-striker` | `…-sail-open-facing-locked` | set 2 (5) | 1–4–0 | +4.60 | 491.2 |

Three things to read out of that, and one of them is not about me.

**Against the other two chassis, revision 5 is level with wave 4** — Δ +3.00 /
−3.60 and −1.40 / +4.60, straddling zero on both. Neither revision's advantage
transfers off the mirror. That is consistent with what the revision actually
changed: the interception table is a *striker* geometry improvement, and the two
cross-class cells are decided long before the standoff band matters.

**Both cross-class cells are lost, 1 win in 20, by both revisions.** The opponent
is this lineage's own wave-4 brain wearing a `fabricator` or `bulwark` manifest
class — a *striker doctrine on another chassis*, which never anchors, never
raises a shell, and never plays its class's verbs (measured: zero `transform`
from either). It still wins 19 of 20. The fabricator ends matches by base breach
at tick 372–405 while fielding **four slots and 52 explicit fabrications**; the
bulwark simply out-durabilities a 3-HP chassis at 5/4. I cannot say whether a
real fabricator or bulwark entrant would do better, and I am not permitted to
find out. But "the same doctrine on a different chassis beats the striker 19 times
in 20 without using its class at all" is a chassis observation the balance record
should have, and it is the strongest statement about the striker's floor that this
isolation permits.

**The four-slot economy is visible and read, not assumed.** In the fabricator cell
this striker counts 3 own slots and 4 enemy slots, reads unlocks at 60/180/300 and
rebuild delays of 22 (units 1–2) and 30 (unit 3) off the assigned lifecycle
profiles, and identifies `ready-for-explicit-fabrication` as a policy that costs
its owner an action. Zero faults, zero invalid actions, and no hardcoded slot
count anywhere.

### The retreat-punishing map is still a stalemate

`--duel-map thin-fronts` composed with the primary cell, set 1, both sides:
**0–0–10, every match a 0–0 draw at the tick cap, and the mirror control draws
identically.** Wave 4 measured exactly this and the widened cone does not change
it: neither artifact takes a single position there. So the mirror-cell advantage is
a claim about the shipped map. On the arm designed to raise the positional cost of
retreat, a doctrine built on conceding tiles to restore a band converts its
advantage into a stalemate rather than a loss — the same limitation, unmoved, and
still the most important one in this report.

## Ablations: what each rule is worth

Screened one factor at a time. The screening base is the pre-`steady`
configuration, whose sets 1+2 mean is **+10.60** at 12–8–0 and whose five-set mean
is +6.72 at 25–23–2; the cast gate was then measured against that base over all
five sets and adopted. Δ is against the same mirror control on the same seeds and
sides.

| Variant | Seed sets | Record | Mean Δ | Verdict |
| --- | --- | --- | --- | --- |
| **adopted: on-point cast gated on a steady body** | 1–5 (50) | **30–9–11** | **+4.90** | kept — wins 30 of 50 |
| same, gate relaxed to any body (screening base) | 1–5 (50) | 25–23–2 | +6.72 | more margin, far more losses |
| cast disabled entirely | 1–3 (30) | 19–11–0 | +6.80 | the fan is not free money |
| **wave-4 ledger + open placement** | 1 (10) | 4–6–0 | **−5.90** | casting on the old rule is actively harmful |
| shared-cone tax 1.0 | 1–2 (20) | 8–11–1 | +2.25 | too weak |
| **shared-cone tax 1.5 (adopted)** | 1–2 (20) | 12–8–0 | **+10.60** | kept |
| shared-cone tax 2.0 | 1–2 (20) | 3–17–0 | −13.15 | far worse than no rule |
| shared-cone tax 2.5 | 1–2 (20) | 5–13–2 | −9.50 | far worse than no rule |
| cover quality flattened (a curve counts as a lane) | 1–2 (20) | 7–13–0 | −9.15 | the quality gradient is worth ≈ +20 |
| `AnyLine` requires a *direct* line before repositioning | 1–2 (20) | 5–15–0 | −9.50 | a curve is a line; do not walk for a straighter one |
| pointed-muzzle danger 3.0 → 2.2 | 1–2 (20) | 12–8–0 | +10.30 | inside noise; 3.0 kept |
| standoff band back to wave-4's `maxBendAfter + 1` | 1–2 (20) | 12–8–0 | +10.60 | **outcome-identical**; see below |
| farming value flat (wave-4's 2.0) | 1–2 (20) mirror + fabricator cell (10) | 12–8–0 / 0–10–0 | +10.60 / +3.00, −3.60 | **decision-identical on BOTH**; see below |
| on-point seal ≥ 2 tiles instead of ≥ 1 | 1–3 (30) | 16–13–1 | +7.40 | **outcome-identical**; looser rule kept |

Four of these deserve more than a row.

**The cone tax peak did not move, and that is the useful finding.** Wave 4
reported 1.5 as a knife edge whose response inverts between 1.2 and 1.5, and
warned the peak would move when anything else changed. The cone this tax is
computed over is now *much* wider — it is the same five-family envelope, so two
allied bodies share an enemy's answer far more often — and 1.5 is still the peak,
with 2.0 and 2.5 still catastrophically worse and 1.0 still too weak. So the
coefficient survived a large change in what it multiplies, which is weak evidence
that it is measuring something real about the board rather than fitting one
scramble. It is still non-monotonic and still the most delicate number here.

**The standoff band is outcome-identical at 4 and 5 tiles.** It plays visibly
different matches — different replay hashes, different decisions — and produces the
*same* territorial score on all 20. That is a real property of this board, not a
harness bug: the score saturates. It is kept at the aim-widened reading because
that is the correct reading of `shotProgram` on this arm, and it reverts to
wave-4's number by construction where offsets are absent.

**The farming price is inert for a STRUCTURAL reason, and the reason is the
finding.** I expected it to be inert on a striker mirror, where every child
declares the same 30-tick automatic return and every prime 18 — with one clock
there is nothing to prefer. It is *also* inert on the fabricator cell, where the
clocks genuinely differ (22 for units 1–2, 30 for unit 3, plus an explicit-refield
policy that costs an action): flattening it back to wave-4's constant produced a
**decision-identical** run — same 2,282 decisions, same 291 shots, same 19 casts.
The reason is that the price enters as a **per-body multiplier on that body's own
weight**, so it scales every candidate trajectory aimed at that body by the same
factor and cancels out of the argmax entirely. It can only change a decision when
**two bodies compete for one trajectory**, and on this map, with these doctrines,
two enemy bodies are almost never both finishable and both reachable on the same
tick. Pricing a kill correctly and *acting* on the price are two different
changes; I shipped the first and measured that the second does not follow from it.
An attempt at the second — weighting a pose's cover by the target's worth, so the
standoff points at the more expensive body rather than merely at a body — was
built and measured, and is reported in the next row rather than adopted.

| Variant | Cell | Record | Mean Δ | Verdict |
| --- | --- | --- | --- | --- |
| adopted (price read, cover unweighted) | mirror, sets 1–2 | 13–4–3 | **+3.80** | kept |
| `farmcover`: pose cover weighted by the target's worth | mirror, sets 1–2 | 12–5–3 | **−0.40** | rejected |
| adopted | fabricator, sets 1–2 | 0–10–0 | +3.00 / −3.60 | kept |
| `farmcover` | fabricator, sets 1–2 | 0–10–0 | +0.80 / −3.60 | rejected |

So the honest answer to "exploit the four-slot rebuild economy" is: **the economy
is read correctly and the one behavioural lever I could build on it loses.**
Pointing the standoff at the more expensive body means pointing it away from the
nearer one, and on this map proximity beats price. The price stays in the source
because it is right, it is free, and it is the term any *future* rule about
trading bodies has to be built on — but calling it doctrine when it changes no
decision would be a claim this report cannot support.

**Casting is not free money, and the ledger is what makes it not harmful.** Three
configurations, one factor apart: never cast (+6.80, 19–11–0), cast on wave-4's
ledger with the tile now legal (−5.90, 4–6–0), cast on the re-priced ledger
(+4.90, 30–9–11). The middle row is the one worth publishing — handed a legal tile
for a verb it had never been able to use, the *unrevised* doctrine loses ground
with it. The mechanic did not become good when the map stopped refusing it; it
became available, and the pricing had to be redone from scratch.

## Skill, diagonal and slot usage, counted

Frozen artifact, primary cell, 50 measured matches (five seed sets, both sides),
counted from the replays' own decision records.

| Quantity | Frozen r5 artifact | Rebuilt r4 predecessor, same 50 matches |
| --- | --- | --- |
| Decisions | 31,437 | — |
| Mobile-gun shots | 4,588 | 4,257 |
| … carrying a **launch offset** | **1,363 (29.7%)** | 1,074 (25.2%) |
| … of which **aim-only diagonals** (offset, zero bends) | **314** | 165 |
| … of which offset **plus** bend | 1,049 | 909 |
| Bends fired | 1,691 (36.9%) | 1,531 (36.0%) |
| **Volley stances entered** (`transform`) | **156** | 17 |
| … entered **on an objective tile** | **95** | **0 by construction** — not counted the same way; my own decision messages carry the tag, its do not. But it refuses any tile carrying `transition-placement-forbidden`, and all 22 objective tiles carry it, so its on-point count cannot be anything else |
| **Fans launched** (`shoot-straight` from a stance) | **90** | 7 |
| Left the stance early (`mobilize`) | 64 — 41% of entries | 10 |
| **Shells raised** | 0 — a striker chassis declares no `projectileGuard` form and no shell route | 0 |
| **Shells declined** (bolts refused into a guarded quadrant) | 0 **opportunities** — no opposing form in any measured cell declared `projectileGuard`, so the decline discipline never ran | 0 |
| Turrets anchored / cycled | 0 — no anchor route on this chassis | 0 |
| **Slots fielded** | **3 of 3** (prime plus automatic children at the declared unlocks); 4 of 4 counted on the enemy in the fabricator cell | 3 of 3 |
| `fabricate` actions | 0 — the striker arm declares no fabrication action, read from the mask | 0 |
| Runtime faults | **0** | 0 |
| Non-`success`/`blocked` resolutions | **0** | 0 |

The two rows worth pausing on. **The launch offsets are not new to this
revision's *gun*** — wave 4 already fired them on 25.2% of its shots, because its
enumerator read the aim range off `shotProgram` and used it. What was new is that
wave 4 then *scored every position* with a table that denied those shots existed.
So this revision's gain is not "it can shoot diagonally now"; it is "it finally
stands and turns as though it could". And **the predecessor casts 17 times on this
arm where its own report says zero**, because it is still asking the map: the
shoulder tiles it was restricted to in wave 4 are still legal, so it uses them and
never discovers the 112 tiles the route stopped refusing. 95 of my 156 entries are
on ground it cannot use at all.

## Mechanical repairs (free budget)

1. **Placement is a route question, not a map question.** Wave 4 flattened the
   map's tile tags into one refusal set at read time and asked "is this tile
   tagged `transition-placement-forbidden`?" where it meant "may a stance rise
   here?". On an open-ground arm those answers differ: the tag is still on 112 of
   this map's 233 open tiles and *no route refuses any of them*. Tags are now
   indexed by kind, and only a route's own `placement.ForbiddenTileTags` /
   `RequiredTileTags` decide anything. This is the repair the whole revision turns
   on, and its failure mode is silent — nothing errors, the bot simply declines
   ground the rules just handed it.
2. **A diagonal bolt no longer proves its bend is spent.** Wave 4 refused to
   project further curvature off a diagonal projectile, reasoning it had either
   already turned or come from a turret. A launch offset makes a diagonal bolt with
   an untouched bend perfectly ordinary, and which history produced it is not
   observable — so where the owner's envelope permits both an offset launch and a
   bend, both futures are projected.
3. **The threat map and the gun now share one geometry.** "Already pointed at this
   tile" and the shared-cone test used a dominant-axis alignment helper; both now
   call the same five-family table the shot planner uses. Two models of one
   mechanic is a bug waiting for an arm to expose it.
4. **A kill is priced in time.** Each body's stable slot declares its rebuild
   delay and its destruction policy; the cheapest return on the contract is the
   unit. A slot whose policy is `ready-for-explicit-fabrication` also costs its
   owner a combat action from a body standing in its own fabrication region, which
   is read off the policy string rather than inferred from a class name.
5. **A fortified form that can mobilize again is still a gun that follows you.**
   The standoff band excludes guns that cannot chase. Wave 5 makes the turret a
   true cycle — `irreversibleForLife` false on both mobilize routes, health mapped
   `preserve-ratio-floor-minimum-one` in both directions, no entry heal — so an
   anchored body is no longer spent for the life and its gun belongs in the reach
   you must stand outside of. The band now includes any form that can reach a
   movement-capable form by a reversible route. **Unmeasured where it binds:** on a
   striker mirror the volley stance and the mobile form share a travel of 8, so the
   change is inert; against a bulwark it would move the band from 7 to the fork
   band, because a turret gun outranges my own chassis and the out-range bargain is
   therefore not on offer. That is a contract reading, not a measurement, and it is
   flagged as such.
6. **The standoff band re-derived.** `ForkReach` is the forward distance where the
   coverable lateral run is widest: one tile past the latest legal bend without
   offsets (the fork's budget is what runs out), the deepest tile *inside* the
   window with them (the hook is legal only while `f` is inside it).

## What could not be exercised, stated plainly

**The anti-shell logic is still unexercised in play, for the fourth revision
running.** The guard rules — refuse a bolt whose arrival heading lands inside a
guarded quadrant, prefer a trajectory that swings the last tile outside it, and
deliberately feed the deflection that spends the declared budget — are verified
only by contract reading. The isolation rules permit my own predecessor and my own
variants, my doctrine is a striker's, and a striker owns no shell. The launch
offsets make this *more* interesting than it was, because an aim-only diagonal is a
new and cheap way to arrive on a shell's flank without spending the bend, and that
is precisely the branch I cannot test.

**The turret cycle is read, not played.** Everything in repair 5 comes from the
contract. My own bulwark-classed variant plays a striker's doctrine, so it never
anchors on purpose, and I cannot script an opponent that does.

**The fabricator pairing is single-sided by construction.** Declared classes bind
each bot to its class's canonical side, so `fabricator-vs-striker` cannot be
played from both sides and its record is 5 seeds per set rather than 10.

## Top three frictions this revision

1. **A ground arm empties a route's tag list and leaves every tag on the map, and
   nothing tells you which one is the rule.** This is the single most expensive
   fact in the wave and it is nobody's stated contract. `--stance-ground open`
   sets `placement.forbiddenTileTags: []` on the entry routes; the map keeps
   `transition-placement-forbidden` on 112 of 233 open tiles. Both are true, both
   are readable, and they disagree — so a bot that asks the map (which wave 4 did,
   and which the *rule card's own sentence* about Anchor invites: "Anchor is
   illegal on every contract-tagged transition-forbidden tile") silently declines
   the whole central lane and every objective tile for a verb that is now legal
   there. Nothing fails; the mechanic simply never fires, exactly as it never
   fired in wave 4, and the replay looks fine. The addendum's stance-ground
   section says "Read your entry route's `placement` from the contract — under
   this arm its `forbiddenTileTags` is empty", which is the right instruction, but
   it reads as a detail rather than as *the* invariant. One sentence stating the
   general rule — **"tile tags are map data and carry no legality of their own;
   only a route's own required/forbidden lists decide placement"** — belongs in the
   rule card beside the Anchor sentence that currently implies the opposite.
2. **`--print-candidate-contract` still will not print the contract.** Third
   revision, same complaint, and this wave made it sharper: the entire assignment
   was "rebuild your table around the widened cone and re-price the volley", and
   the four facts that decide both are `shotProgram.minInitialAimSteps` /
   `maxInitialAimSteps`, the entry route's `placement`, the stance form's
   `objectiveWeight`, and the slots' `lifecycle` delays. The flag emits ruleset ID,
   fingerprints, map and topology profile — none of them. The only way to see the
   rules is to run a match and dig `header.contract` out of a 15 MB replay JSON,
   which is what I did four times (base, aim, open, five-slot `wane`). A
   `--print-candidate-contract --full` that dumped the resolved `rules` object
   would have saved an hour per arm for every author in the wave, and the data is
   already assembled — it is embedded in the replay the same command's sibling
   writes.
3. **The harness advertises a shared scratchpad that the author packet forbids.**
   The packet requires "a uniquely named private scratch directory — never a
   shared or guessably named scratch path", and cites a wave-1 exposure as the
   reason. The agent harness's own instructions say, in bold, to use
   `/private/tmp/claude-502/<session>/scratchpad` for *all* temporary files. That
   directory is shared across runs and currently holds every entrant's wave-5
   brief. An author who follows the harness violates the packet on its first
   temporary file, and finds out by seeing a directory listing it should never
   have been able to produce — which is what happened here and is disclosed above.
   This is not a documentation gap, it is two instruction sets in direct
   contradiction, and only one of them is load-bearing for the experiment's
   evidence. Either the brief should name the private scratch path in a way that
   overrides the harness explicitly (it half-does: "Private scratch: a uniquely
   named directory under `sandbox/`" — but as a permission, not as a prohibition on
   the alternative), or the isolated-author waves should run with the shared
   scratchpad unset.

### Still open from earlier revisions and still true

- The published CLI binary in `sandbox/cli-publish/` is named `botarena`, while
  every document, the brief, and its own `--help` output call it `nilbots`.
- Pointing `--bot` at `out/bot.wasm` still silently drops the declared class and
  resolves the base contract. Confirmed again, and this revision made it *useful*:
  my three class variants build to the **same artifact hash**
  (`775bced2…`), because the class lives in the manifest and not in the artifact.
  That is a clean experiment — one brain, three classes — but it is also a
  footgun, because a wasm-path sweep silently measures the wrong ruleset.
- A composed map still does not appear in the ruleset ID: with and without
  `--duel-map thin-fronts`, the primary cell returns the same
  `frontline-labs-1-striker-vs-striker-sail-open-facing-locked`, and only
  `mapId`, `mapFingerprint` and the aggregate match fingerprint differ. Two
  genuinely different games share one ruleset ID.
- The decision `debugMessage` is preserved verbatim per actor turn in replay v3
  and is still documented nowhere. It carried this revision's entire usage
  census: tagging on-point casts in the message is how the 58-of-93 split below
  was counted, without instrumenting the engine.
- **New, and a credit rather than a complaint:** the inert-flag reporting is
  better than I expected and it is worth naming, because I checked it rather than
  assuming. `--skills kit` on a striker mirror prints "requested skills without an
  owning class in this cell change no contract bytes and are dropped", and
  `--five-slots wane` on a non-fabricator pair is a *hard error with the fix in
  it* ("the cell must carry it: pass a class pair containing the fabricator and a
  `--skills` selection that includes five-slots", exit 1). Both are exactly what
  an author wants. What is still silent is the other direction: `--aim offset` and
  `--stance-ground open` are simply absorbed where nothing they touch exists, with
  no line saying so, and the composite name shifts underneath you — the same flag
  set spells `deck` on a fabricator pair and `sail-open` on a striker mirror,
  which is documented but takes a moment to trust the first time an archived
  result comes back with the "wrong" ruleset ID.

### One measurement footgun worth republishing

`--swap` on two **identical** artifacts reproduces the same match byte for byte, so
a mirror control has N independent samples, not 2N, and a sweep that counts both
sides of a control as data reports its side bias as a result. Every number in this
report is a paired delta against that control for the same side and seed. The
addition this revision makes: on this board **the territorial score saturates**, so
two configurations that visibly play different matches can produce identical
scores on every seed (measured twice — the standoff band and the farming price).
An ablation that comes back "identical" has not necessarily done nothing; it has
done nothing *the scoreboard can see*, and those are different claims.

## Timings (macOS, Docker builder, CLI 0.9.21)

| Step | Time |
| --- | --- |
| `dotnet build` (editing loop) | 0.5–1.1 s |
| `nilbots build --no-cache` (cold) | 8–12 s |
| One 500-tick WASM match | ≈1.4 s |
| One 5-seed batch (`--seeds`) | ≈7 s |
| One paired cell (control + both sides, 10 matches + census) | ≈32 s |
| `qualify --suite frontline-qualification-5` (full cumulative chain, WASM) | 6.8 s wall |
| Census parsing (15 MB replay JSON each) | comparable to running the matches |

## Hardcoding temptations resisted

- The initial-aim range is read from `shotProgram.minInitialAimSteps` /
  `maxInitialAimSteps`; ±1 appears nowhere. The aim-only program's inert curvature
  fields come from `shotProgram.aimOnlyProgram` rather than being written as
  zeros, and the bend window, bend interval, bend count and allowed bend
  directions are all read. On a straight-aim arm the table collapses to lane +
  fork by construction, verified by brute force.
- The five families are derived from the declared envelope, not enumerated as
  cases of a known arm: `flatten` and `hook` are simply what "may I offset the
  launch?" plus "may I bend once?" imply, and both disappear when either
  permission is absent.
- 60/180/300, rebuild 22, rebuild 30, 120/260 and 18 appear nowhere. Unlock ticks
  come from lifecycle assignments; rebuild delays and destruction policies come
  from the assigned lifecycle profile; slot counts are counted for both teams.
- The volley's bolt count, spread, cooldown, windup, budget counter and threshold,
  the tile tags that refuse it, the objective weight it keeps, and the return
  route's action are all read. The number three appears nowhere.
- Reversibility is read from `irreversibleForLife` on the exit route, which is the
  whole of repair 5: the same class was once irreversibly fortified and is now a
  cycle, and no form, gun or stat changed when it moved.
- The lane-count margin in the cast ledger is computed from the gun's own aim
  range, so a straight-aim arm reverts to wave 4's 1.15 without a branch.
- Equal-scoring directions are still broken by the contract's own front axis with
  the residual tie randomised per life. An absolute compass preference is a
  measured team-side bias on a mirror-symmetric map.
