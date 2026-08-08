# DX notes — Still Water, revision 6 (the coordination wave)

## Isolation

Written from this entrant's own authoring session, its own sparring replays
against its own rebuilt predecessor and its own scratch variants, and its own
qualification report. **No other entrant's source, standings, replays, report, or
brief was opened.** Work was confined to
`arena-bots/frontline-labs/classes-wave-6-2026-07-30/still-water` plus the private
scratch directory `sandbox/still-water-w6-scratch-7b3e94af`, which is uniquely
named and belongs to this lineage. The wave-5 directory
(`classes-wave-5-2026-07-30/still-water`) was read but not modified; the
predecessor sparred against is a *copy* of that source inside the private scratch
directory, rebuilt on the current toolchain. The wave-level
`classes-wave-5-2026-07-30/README.md` was deliberately **not** opened, because a
wave-level README is where standings live. Nothing was committed to git.

Permitted material only: the author packet, the Labs rule card, the class addendum
(read in full on its current hash — see below), the SDK's `GenericActorContext` /
`GenericActorRulesContract` types and their XML documentation,
`templates/botarena-generic-actor/`, this lineage's own wave-4 and wave-5
directories and replays, and `sandbox/cli-publish/`.

### Disclosure: the shared scratchpad, a third time, and now structural

**This session's brief was delivered as one key inside a single JSON file in the
harness's shared scratchpad — `/private/tmp/claude-502/<session>/scratchpad/w6-prompts.json`
— whose other seven keys are the other entrants' wave-6 briefs. To locate my own
key I listed the file's keys, so I saw the eight lineage names.** Stated
precisely:

- **Seen:** the list of top-level keys, i.e. the eight entrant lineage names in
  this wave. Nothing else about any of them.
- **Not seen:** no other key's value. No other entrant's brief, doctrine,
  assignment, source, replay, standing, or report was read, printed, or parsed.
  The only value extracted from that file was `still-water`.
- **Not new information:** those lineage names were already known to this lineage
  from the wave-5 exposure, which the wave-5 `DX.md` discloses. So the incident
  leaked nothing this lineage did not already have on record, and nothing in it
  influenced any decision here.
- **Repaired:** every working file this session created lives under
  `sandbox/still-water-w6-scratch-7b3e94af/`. Nothing was written to the shared
  scratchpad.

The lineage-level point is worth more than the incident, and it has changed shape.
Wave 1 was a guessable scratch name. Wave 5 was a harness that *recommends* a
shared temp directory the packet forbids. **Wave 6 puts every entrant's brief in
one file and hands each author a key into it** — so isolation is no longer
something an author can preserve by being careful about where it writes. Reading
the assignment at all means opening a file that contains all eight assignments,
and finding one's own key in a JSON object means enumerating the others. There is
no careful way to do that. See friction 3.

**Carried forward from v1, still disclosed:** during the first authoring pass a
shared scratchpad directory name (`mirror1`) collided with another agent's run and
aggregate statistics from one `fabricator-vs-fabricator` replay that was not mine
were read before I noticed. No source, standings, doctrine, or striker material
was seen, and nothing from it influenced any revision. The disclosure stays with
the lineage.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `still-water` |
| Authoring lineage | `still-water-v1` |
| Revision | 6 (one budgeted **coordination** pass — the multi-body IQ layer; no doctrine redesign) |
| Class | `striker` (declared in `botarena.json`) |
| Role | `verdict-doctrine` |
| Doctrine | patient interceptor, unchanged; its bodies now get out of each other's way |
| Primary cell | `--classes striker-vs-striker --movement facing-locked --pendulum keel --skills kit --bend universal --aim offset --stance-ground open` → `frontline-labs-1-striker-vs-striker-sail-open-facing-locked` (`deck` spells itself `sail-open` where no fabricator is in the cell) |
| Target tier | cumulative T4 (`frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`) |
| Predecessor | `arena-bots/frontline-labs/classes-wave-5-2026-07-30/still-water` (frozen, untouched) |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` (unchanged since r4) |
| Rule card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` (unchanged since r4) |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `2333bd3c9f412e4e9439779ef3d5f2ca6bc8abae6f00973daf54f7e4c892de50` (**changed** — r5 froze against `3cb2814b…`; re-read in full, and nothing it now says changed a decision here) |
| Starter helper synced | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` (vendored byte-identical; unchanged since r4) |

| Artifact | Value |
| --- | --- |
| `out/bot.wasm` sha256 | `cbdba9c62ba501fe3033074e6b510ea0949a229db7ffa80096340da425d79b01` |
| `out/bot.wasm` size | 3,386,803 bytes |
| Deterministic source-tree hash | `2043e399b1abc76c967da73080f9391fd9c91d02c77bf34bf36dcd404747b9a7` (sha256 of the sorted per-file sha256 listing of all `.cs` + `.csproj` + `botarena.json`, excluding `bin/` and `obj/`) |
| `evidence/t4/qualification.json` sha256 | `c931f07f3f2f2aa9f7ccfe2842fcfa4be570bfb8ece0d3627b6d18cb1cd98972` |
| `evidence/t4/prerequisite-t3/qualification.json` sha256 | `48b84e10a32959bd3d54753763c7cbc49c8c8351b3a669f3c2b0142273416b53` |
| `evidence/t4/prerequisite-t3/prerequisite-t2/qualification.json` sha256 | `8d3536eb0176d3ab46e42980d0ce7c7242a51732b4208be47401a82f5955a78f` |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` (identical to r4 and r5 — the union profile really is immutable) |
| `evidence/doctrine-deck/replay.json` replay-v3 hash | `ae6020ca3fe649721e4c4f024391570f31fc1595701e6ea1b4c23692af048331` (file sha256 `d5cc94368ddca35b436fee92c1c1dfc56a6a29451ee5f1e924136544a010fe56`; `nilbots verify` OK — primary cell versus the rebuilt predecessor, seed 560017, **base breach at tick 432 for +30 territory, on a side the mirror control lost by 11**) |
| Verified probe replays | **37 of 37 `nilbots verify` OK** (36 qualification-chain replays plus the cited doctrine replay), re-verified after the viewer prune below |
| Build reproducibility | a second `nilbots build --no-cache` **from the frozen tree with `bin/` and `obj/` deleted** returns the same `cbdba9c6…` (cache key `c8e182a9…`). Run as the last step of the freeze, deliberately: a freeze nobody rebuilds is a freeze nobody knows is broken. The tree holds exactly 11 `.cs` files and no variant or ablation source anywhere inside it — every variant lives in the private scratch directory. |
| Sparring baseline | wave-5 source rebuilt on the current toolchain, artifact `d6929496f1c6b055d58fbc091b6ac0ab1877f61eb134c11a1db10f8a338c0799`. The wave-5 freeze recorded `dd2b878b…` on CLI 0.9.21, so **the same bytes of source compile to a different artifact on 0.9.22** — which is why the brief's "rebuild anything you spar against from source" is not a formality. |
| Toolchain | controlled `nilbots build --no-cache`, CLI **0.9.22**, SDK 0.10.6, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, game rules 0.5, WASI p1 core module, macOS host via the platform-matched Docker builder |

Per-file source hashes at freeze:

```
04dbf8d8f5b4cd77514e51bf18a6e886da93105256841db96079e085f7da25c6  ActionBook.cs
567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627  ArenaBasics.cs
974cd24ddb0a228da6b351c2d5b352a516bdfba07ee33919738fcf1c258986fc  Convoy.cs
d1fb75149fe7d46f4b6c6e20edc03f5daef103d23fcf5201adc9ea4efa6b1916  Doctrine.cs
df05fd11c3f1efa2dc032eebc9a11f65478aee5dc61e3447d2115935dedb12a6  Field.cs
4c2ab7b621705f8887d4598d6120f41cae25ae66de8cec5bfc97dd8bc249ca3b  ForkPlanner.cs
4cf2354e43630de4c7edb6f4b7492112020ea57d7f391d4512f31057a7eaafbb  Quarry.cs
a62cce328cfd13051802869d54f0396fa59cea5df26e96b51fff48181653bf3a  Ratchet.cs
068430ebd8315ab9755adb75fc519b96d1102259fa937a1dd5376d83e9354437  Stance.cs
a7ffb847a7028b9c548e94001794a7b578e042639814ed8b3daa5aba2feab79f  StillWater.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  StillWater.csproj
90a42a29189ed2c85bc419ad0a11f154943fd2b830cd21cb27635df158c60c28  ThreatField.cs
0ed9bc7973a0d815aad0b89434726c54caa4b3ed9c88ddb812462fca2e087e6f  botarena.json
```

**`Convoy.cs` is new and `StillWater.cs` changed. Every other file is byte-identical
to the r5 freeze** — `ActionBook.cs`, `ArenaBasics.cs`, `Doctrine.cs`, `Field.cs`,
`ForkPlanner.cs`, `Quarry.cs`, `Ratchet.cs`, `Stance.cs`, `ThreatField.cs`,
`StillWater.csproj` and `botarena.json` all carry their r5 hashes. That is the
shape a coordination pass ought to have: the interception geometry, the threat
model, the stance ledger and the contract reader were not touched, and the edits to
`StillWater.cs` are confined to the option scorer, the station search and the cover
score.

## Doctrine delta in one paragraph

Revision 6 changes no doctrine. The five-family interception table, the
aim-widened standoff band, the cover-quality gradient, the shared-cone tax and the
re-priced on-point cast ledger are the wave-5 artifacts, untouched, because that is
what wave 5 measured as winning. What revision 6 adds is a coordination layer,
because the doctrine was being executed by bodies that got in each other's way:
measured on the predecessor's own fifty matches, **in the ticks where it had more
than one body on the board, 34% of the time a body's only legal step was onto a
sibling** — and under `facing-locked` that is not a slower body, it is a body with
no movement in its vocabulary until it spends a whole tick pointing somewhere it
did not want to point. The layer rests on one contract fact that turns out to be
sufficient: a life never sees an ally's current action and starts with empty
private memory, but every one of my lives receives the *same* frozen
team-perception union on the same tick, so any pure function of that union
evaluates identically in every sibling — a convention rather than a negotiation.
The convention is a strict total order over my own live bodies, keyed on route cost
to the contested point then stable slot then life id, and a body yields only to
siblings above it, so the leader never yields and a cycle of mutual yielding cannot
form. Two rules ride on it: a **lane claim**, where no body stands on or steps onto
a tile a better-ranked sibling's route needs this tick or next — exact under a
locked coupling, because the legality mask offers precisely one direction, and
degraded to the continuation of an observed step where it is not — and **choke
precedence**, where a one-tile corridor read off the map (24 of this map's 233 open
tiles, four of them the only row-7 approaches to the centre) may be crossed but not
entered behind a better-ranked sibling and never parked in at all, with the station
search refusing corridor tiles outright, because a standoff doctrine parks for
dozens of ticks and a parked body in one of those corridors adds three tiles and up
to five ticks to its own team's route to the point. A third rule completes the
spacing bar by counting an enemy answer wave 5 did not: a guarded form returns the
bolt that hits it back down the bearing it arrived on, so two of my bodies on one
ray out of it are both on the return's lane and the bolt that reaches the second one
is mine — verified against the contract and unexercised in play, because no striker
form declares a projectile guard. Three further coordination rules were built,
measured and **rejected**, which is most of what this report is about.

## Qualification outcome

`nilbots experiment frontline-labs qualify --bot out/bot.wasm --suite
frontline-qualification-5 --out evidence/t4` exits **0**. **Tier awarded: T4**,
`passed: true`, `profileComplete: true`, `balanceEvidenceEligible: true`, profile
`frontline-duel-depth-union-t4-v1`, artifact `cbdba9c62ba5…`, seed 104729.

| Level | Component | v1 | v2 | v3 | v4 | v5 | v6 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| T4 | suppression-choke | PASS | PASS | PASS | PASS | PASS | PASS |
| T4 | entry-initiative | PASS | PASS | PASS | PASS | PASS | PASS |
| T4 | prediction-chamber | PASS | PASS | PASS | PASS | PASS | PASS |
| T4 | front-rotation | PASS | PASS | PASS | PASS | PASS | PASS |
| T4 | map-holdout (thin-fronts) | PASS | PASS | PASS | PASS | PASS | PASS |
| T3 | wall-terminated-bend | PASS | PASS | PASS | PASS | PASS | PASS |
| T3 | strict-corner | **FAIL** | PASS | PASS | PASS | PASS | PASS |
| T3 | cadence-parity | PASS | PASS | PASS | PASS | PASS | PASS |
| T3 | cooldown-window | PASS | PASS | PASS | PASS | PASS | PASS |
| T3 | local-form-safety | PASS | PASS | PASS | PASS | PASS | PASS |
| T2 | contract-matrix | PASS | PASS | PASS | PASS | PASS | PASS |
| T2 | automatic-life-cycle | PASS | PASS | PASS | PASS | PASS | PASS |
| T2 | objective-path | PASS | PASS | PASS | PASS | PASS | PASS |
| T2 | direct-fire | PASS | PASS | PASS | PASS | PASS | PASS |
| T2 | straight-evade | **FAIL** | PASS | PASS | PASS | PASS | PASS |
| T2 | manual-fabrication | PASS | PASS | PASS | PASS | PASS | PASS |

Every component passed on the first attempt; zero qualification cycles were spent
on repairs, as in r3, r4 and r5. **The suite exercises none of this revision's
work.** The union profile fields two bodies per team, so the coordination layer's
own guard — `_bodies.Count < 2` — is what runs there, and it runs to completion:
`Convoy` is constructed on every tick of every probe and every rule returns zero.
That is the correct outcome and it is worth stating plainly, because it means **T4
is evidence that the coordination pass broke nothing, and no evidence at all that
it works.** All of the latter is in the sparring numbers below. The report does
carry a `coordinationGradeAwarded` field; suite 5 leaves it `null` (friction 2).

## The silliness, measured before it was fixed

The owner's complaint — "bots making silly decisions, e.g. blocking an ally's path
in a choke" — is measurable directly out of replay v3, so the first thing built
this revision was the instrument, not the fix. Four counters, all read from the
replay's own authoritative post-state and per-turn observations:

| Counter | What it means |
| --- | --- |
| `ahead_tile_is_sibling` | a decision where, under `facing-locked`, the one tile the movement mask would allow is occupied by a sibling. The exact shape of "blocking an ally's path". |
| `pair_both_in_choke` | a tick where two of my bodies both stand in one-tile corridors. |
| `pair_adjacent` | a tick where two of my bodies are within Chebyshev 1. Stacking. |
| `pair_one_enemy_ray` | a tick where two of my bodies lie on one of the eight rays out of an enemy body — one aimed lane, one fan ray, or one deflection return covers both. |

All four are normalised per **multi-body tick**, because a rule that gets bodies
killed would flatter every raw count. The denominator is ticks in which the
measured side has two or more live bodies.

The rebuilt wave-5 predecessor, measured against itself (the mirror control, 50
side-samples over five seed sets, 8,054 multi-body ticks):

| Counter | Predecessor |
| --- | --- |
| a body's only legal step is onto a sibling | **33.99%** of multi-body ticks |
| two bodies in one-tile corridors | **3.18%** |
| two bodies within Chebyshev 1 | **77.34%** |
| two bodies on one enemy ray | **21.23%** |
| moves the engine refused | 170 |

`blocked_by_sibling` is **zero** across every match measured this revision, for
both artifacts — the predecessor already refused to *submit* a step onto a
sibling's tile. That is the trap: the collision never appears as a blocked action,
it appears as a body that quietly stops moving. An author hunting coordination bugs
in resolution outcomes will find nothing at all.

### What a parked body in a corridor actually costs

Computed on the resolved map contract rather than asserted. This map has **24
one-tile corridor tiles among its 233 open tiles**, and four of them —
`(8,7) (9,7)` and `(13,7) (14,7)` — are the only row-7 approach to the centre
objective from either side.

| Route | Clear | One sibling on `(8,7)` or `(9,7)` |
| --- | --- | --- |
| rank-0 station `(6,7)` → nearest centre tile | 4 tiles | **7 tiles** |
| own home pad `(4,8)` → nearest centre tile | 7 tiles | **10 tiles** |

Three tiles, and under `facing-locked` a detour that changes axis also costs a
rotation, so three tiles is up to five ticks — of a fifteen-tick capture clock. The
flank stations (`(6,5)`, `(6,9)`) are unaffected, which is why this is a
*precedence* problem and not a *station* problem: the bodies that collide are the
ones whose stations are correct.

## Measured records versus the rebuilt predecessor

All figures are **paired deltas**: candidate territory on a given (side, seed)
minus what the identical-artifact mirror control scored for the same side on the
same seed. `--swap` on two identical artifacts reproduces a match byte for byte, so
the control has N independent samples rather than 2N, and a bare win/loss column
against a predecessor mostly reports the seed's side bias. Opponent = wave-5 source
rebuilt on CLI 0.9.22 (`d6929496…`). Runtime WASM. Five seed sets × 5 seeds × both
sides = 50 samples per configuration.

**The seed sets are this revision's own** (primes spanning the range), not wave 5's,
so census counts are not comparable across waves — only across the configurations
below, which all share them exactly.

### Primary cell — `striker-vs-striker`, the deck game, 50 matches

| Seed set | Record | Mean Δ territory |
| --- | --- | --- |
| 1 (104729…224737) | 5–4–1 | **+2.40** |
| 2 (271441…479001) | 5–3–2 | **+7.10** |
| 3 (520493…700001) | 3–6–1 | **+3.30** |
| 4 (811081…999983) | 5–3–2 | **+0.80** |
| 5 (104711…505447) | 5–4–1 | **+4.10** |
| **all 50** | **23–20–7** | **+3.54** (mean 486.7 ticks, 6 breaches) |
| mirror control (r5 vs r5) | — | 0.0 by construction (499.0 ticks, 0 breaches) |

Six of the fifty end in a base breach where the control never breaches once, and
mean match length drops from 499.0 to 486.7. **Coordination makes the board
decisive** — the same doctrine with its traffic untangled converts stalemates into
finishes, in both directions.

**That is also why the variance is brutal, and this report will not hide it.**
Individual paired deltas run from −41 to +60, because a breach is worth ±30 on a
scale whose ordinary matches score ±10, and a coordination change moves breaches
around. Sample standard deviation is ≈17, so the standard error on a 50-sample mean
is ≈2.4 and **+3.54 is roughly 1.5 standard errors from zero.** Read the record
column, not the margin: 23–20–7 on 50 paired samples is a real but modest edge, all
five per-set means are positive, and **no two configurations in the tables below are
separated by more than noise on margin alone.** Every conclusion here that rests on
a margin gap smaller than ≈5 is stated as weak; the ones that rest on the silliness
counters are much stronger, because those counters have thousands of events rather
than fifty matches.

## Coordination-rule attribution: what each rule is worth

Each row is a separate `nilbots build --no-cache` of the frozen source with **one
switch in `Convoy.cs` changed and nothing else**, sparred on the identical 50
samples against the identical control. Silliness columns are percentages of
multi-body ticks.

| Build | Record | Mean Δ | only-step-is-sibling | both-in-corridor | adjacent |
| --- | --- | --- | --- | --- | --- |
| predecessor (control) | 0–0–50 | 0.00 | 33.99% | 3.18% | 77.34% |
| lane claim alone | 19–16–15 | +2.98 | 29.03% | 3.17% | 75.99% |
| choke precedence alone | 14–16–20 | +2.74 | 23.72% | **0.18%** | 68.61% |
| **adopted: lane + choke** | **23–20–7** | **+3.54** | **19.64%** | **0.12%** | **66.54%** |
| + cover complementarity | 21–24–5 | +2.18 | 19.35% | 0.30% | 70.16% |
| + scoring-ground exemption | 20–18–12 | +2.78 | 24.13% | 0.23% | 71.87% |

Twenty-sample rows (sets 1–2 only; the control's own 20-sample baseline is 34.99% /
2.56% / 82.30%), kept at twenty because their direction was decisive enough not to
spend another thirty matches on:

| Build | Record | Mean Δ | only-step-is-sibling | both-in-corridor | adjacent |
| --- | --- | --- | --- | --- | --- |
| rally-pad guard alone | 3–3–14 | **−2.85** | 36.42% | **4.04%** | 83.03% |
| motion-evidence claim | 8–9–3 | +1.75 | 24.80% | 0.17% | 76.48% |
| the first composite (all four, v1) | 7–11–2 | **−0.95** | 21.31% | 0.24% | 77.61% |

### The two rules that shipped are complementary, not redundant

This is the one attribution the numbers make cleanly, because it is a statement
about counters rather than margins. **The lane claim does nothing at all for the
corridor counter** (3.17% against the control's 3.18%), and **choke precedence
leaves a quarter of the blocking in place** (23.72%). Composed, they reach 19.64%
and 0.12%: each fixes a failure the other cannot see, because they answer different
questions — "whose tile is this" and "whose corridor is this". Their margins are
+2.98 and +2.74 alone and +3.54 together, which on this variance is *consistent
with* additivity and proves nothing by itself; the counters are what justify
shipping both, and the record column (23–20–7 against 19–16–15 and 14–16–20) is the
supporting evidence.

Against the owner-visible complaint, the adopted pair delivers:

| Owner-visible symptom | Predecessor | Shipped | Change |
| --- | --- | --- | --- |
| a body's only legal step is onto a sibling | 33.99% | **19.64%** | **−42%** |
| two bodies in one-tile corridors | 3.18% | **0.12%** | **−96%** |
| two bodies stacked within Chebyshev 1 | 77.34% | **66.54%** | −14% |
| two bodies on one enemy ray | 21.23% | **20.13%** | −5% |
| moves the engine refused | 170 | **82** | −52% |

### Four rules built, measured, and rejected

All four remain in the source at their measured-worse setting, as
`static readonly bool` switches. That is deliberate: a rejection nobody can rebuild
is an assertion, and every number above came from flipping exactly one of these and
recompiling.

**1. The rally-pad guard — rejected, and it was the worst rule of the wave (−2.85,
and it made the silliness worse).** Under `forward-rally` an automatic arrival does
not appear at home: the declared placement
(`own-side-chain-adjacent-objective-tile-in-team-advance-order-then-assigned-spawn`)
puts it on the rear-most *free* tile of this team's own-side objective region. So
which tile it lands on depends on where my bodies stand, and that is the only
placement influence this contract gives a striker at all — it declares no
fabrication action, so "do not fabricate into your own traffic" is inert here by
contract. The rule kept the rear-most free tile clear while an arrival was due
inside a window sized from the capture arithmetic. It loses two ways. On this map
the own-side region *is* the standoff band's own ground — the rank-1 station
`(6,5)` is one of its tiles — so vacating the landing tile costs a station worth
more than one tile of arrival exposure. And it pushed bodies into the corridors:
two-bodies-in-one-corridor went **up**, 2.56% → 4.04%. A politeness rule that
displaces bodies without saying where to displaces them into the choke. The honest
conclusion is that the placement influence this contract offers a striker is not
worth using, and I would rather report that than ship a losing rule because a bar
asked for one.

**2. Cover complementarity — rejected for anti-synergy, and it is the most
instructive rejection.** With three launch lanes per facing, two bodies pointed the
same way cover nearly the same ground, so cover a sibling's current pose already
provides was discounted rather than counted twice. **Alone it wins:** 8–4–8 at
+4.55 on sets 1–2, among the best single rules measured. **On top of the traffic
rules it loses:** +3.54 → +2.18, and the record inverts to 21–24–5 — more matches
lost than won. The mechanism is legible in the counters: the traffic rules already
move bodies apart, so a rule that additionally points them apart over-disperses,
putting adjacency back *up* (66.54% → 70.16%) because a body that turns away from
the approach then has to walk further to cover it. Two rules that each win alone
and lose together is exactly the result a per-rule ablation exists to find, and it
is why "measure each rule" and "measure the composition" are different
instructions.

**3. The scoring-ground exemption — rejected against my own prediction, twice
over.** I expected to need it and argued for it in advance: a tile of the contested
objective is the only ground the match is scored on, so a body standing there
should not yield it to a sibling merely walking past, and the engine makes the
handover lossy anyway, because *following a vacated actor blocks* — the sibling
cannot take the tile on the tick it is given up. Wrong, and the specific prediction
I made was also wrong: I attributed the first composite's two breach losses to
yielding the point, and measuring the exemption alone shows it costs 0.76 mean
territory and puts nearly a quarter of the blocking back (19.64% → 24.13%), so the
composite's losses came from the rally guard and the cover discount instead. The
reason the exemption fails is that **the objective is a region, not a tile**: the
yielding body steps to another tile of the same six-tile region, the team's
objective weight never drops, and what the yield buys is that the better-ranked
body stops being stalled on the approach.

**4. Motion evidence — rejected, and it says something general about coordination
rules.** A claim taken from facing alone is exact about what a locked body *may* do
and silent about whether it means to, and this doctrine spends most of its ticks
holding a station with the gun laid across an approach it has no intention of
walking down. So I gated the claim on evidence of travel: the body was last seen
taking exactly this step, or the tile ahead strictly shortens its own route to the
point. It loses — +4.75 → +1.75 on the same twenty samples, with blocking back up
from 18.7% to 24.8%. **Being right about a sibling's intent is worse than
over-claiming**, because a parked muzzle's two forward tiles turn out to be ground a
sibling should not be standing on for other reasons, and the imprecise rule was
quietly doing a second job the precise one gives up. I do not have a clean account
of *which* second job, and I would rather say so than invent one.

### The rule that was verified but could not be measured

The **deflection-return spacing** rule ships, and it changed no decision in any
measured match. Wave 5's shared-cone tax charges for a muzzle's aim-widened
envelope and for a fan; it does not charge for the third answer, whose geometry is
different — a form declaring `projectileGuard:
"facing-quadrant-contacts-deflected"` sends the arriving bolt back **from its own
tile along the exactly reversed heading, owned by its team**, so two of my bodies on
one ray out of it are both on the return's lane and the bolt that reaches the second
one is mine. Read off the contract, the rule is correct. Measured, it is inert: **no
form in any cell this isolation permits declares a projectile guard** — verified
directly from the resolved contract, where all four striker forms report
`projectileGuard: null` — so the guard list is always empty and the test always
returns false. This is the fifth revision running in which this lineage's anti-shell
logic is contract-verified and unplayed, for the same structural reason: my doctrine
is a striker's, a striker owns no shell, and I may only spar against myself.

Proof that it is inert rather than merely small: the frozen artifact and the
`lane + choke` scratch variant that lacks the deflection rule and the rejected
switches entirely produce **identical** results on all 50 samples — 23–20–7, +3.54,
and every one of the eleven census counters equal to the digit. Two different
artifacts, one behaviour.

## Per-pairing records — the other two chassis

A declared class binds each bot to its class's canonical team side, so in these
cells this striker is **always team 1** and the pairing cannot be played from both
sides. The control is therefore not a mirror but a **shared opponent**: the same
predecessor artifact plays first the predecessor and then the candidate, on the same
seed, and the delta is candidate territory minus predecessor territory for the same
side of the same match-up. The opponent is this lineage's own wave-5 brain resolved
onto another chassis by `--classes` — permitted, since it is my own variant.

| Pairing | Ruleset | Seeds | Outcome (this striker) | Mean Δ vs r5 | Δ sign split (up/down/level) | Ticks |
| --- | --- | --- | --- | --- | --- | --- |
| `striker-vs-striker` | `…-sail-open-facing-locked` | sets 1–5, both sides (50) | — | **+3.54** | 23 / 20 / 7 | 486.7 |
| `fabricator-vs-striker` + `--five-slots wane` | `…-deck-facing-locked` | sets 1–5 (25) | **1–24–0** | **−6.88** | 5 / 9 / 11 | 397 |
| `bulwark-vs-striker` | `…-sail-open-facing-locked` | sets 1–2 (10) | 0–10–0 | **+0.80** | 1 / 1 / 8 | 295.3 |
| `striker-vs-striker` + `--duel-map thin-fronts` | `…-sail-open-facing-locked` | set 1 (5) | 0–0–5 | **0.00** | 0 / 0 / 5 | 499.0 |

**Against a fabricator chassis the coordination pass costs ground, and this is the
revision's clearest limitation.** −6.88 mean territory against the predecessor over
25 seeds, negative in four of five sets (−7.40, −8.00, −11.40, 0.00, −7.60). The
sign split is 5 up, 9 down, 11 level, which is not itself significant — the mean is
carried by five breach-timing flips of −34 to −44 — so the *size* is not
established, but the direction is consistent enough that I will not call it noise.
The plausible mechanism is that in the one cell where my side is outnumbered (three
slots against the fabricator's four under `wane`) a yielded tick is worth more than
it is on the mirror, and both shipped rules spend ticks to buy position. **I
deliberately did not fit a knob to it.** The obvious one exists and is cheap —
`Doctrine.OwnSlotCount` and `EnemySlotCount` are already read, so the yield
penalties could be damped under a slot deficit — but fitting a coefficient to a
25-seed cell whose sign test is not significant, in a wave whose assignment is the
mirror's coordination, is how a lineage acquires a rule it cannot defend. It is
reported instead.

**Against a bulwark chassis the pass is level** (+0.80; one seed up, one down, eight
byte-identical) and the cell is decided in 295 ticks by durability, exactly as wave
5 found. **Both cross-class cells remain lost outright** — 1 win in 35 — by both
revisions, against a striker doctrine wearing another chassis that never plays its
class's verbs. That chassis observation is wave 5's and it is unchanged;
coordination does not touch it.

**The retreat-punishing map is still a stalemate, unmoved for a third revision.**
`--duel-map thin-fronts` composed with the primary cell: 0–0–5, every match a 0–0
draw at the tick cap, control identical. Neither artifact takes a single position
there. On the arm designed to raise the positional cost of retreat, a doctrine built
on conceding tiles to restore a band converts its advantage into a stalemate — the
same limitation wave 4 and wave 5 both reported, and coordination was never going to
move it, because a stalemate with one body is not a traffic problem.

## Usage census, counted

Frozen artifact, primary cell, the 50 measured matches, counted from the replays'
own accepted actions and decision messages. Candidate side only.

| Quantity | Frozen r6 |
| --- | --- |
| Decisions | 28,686 |
| `move` submitted | 7,624 → 7,542 succeeded, **82 refused** |
| … refused **by a sibling** | **0** |
| `rotate` | 8,362 |
| `wait` | 8,003 |
| Mobile-gun shots (`shoot`) | 4,406 |
| … carrying a **launch offset** | **1,114 (25.3%)** |
| … of which **aim-only diagonals** (offset, zero bends) | 163 |
| Bends fired | 1,504 (34.1%) |
| **Volley stances entered** (`transform`) | **149** |
| … entered **on an objective tile** | **106 (71%)** |
| **Fans launched** (`shoot-straight` from a stance) | **111** |
| Left the stance early (`mobilize`) | 31 |
| Shells raised / declined | 0 / **0 opportunities** — no opposing form declares `projectileGuard` |
| Turrets anchored | 0 — no anchor route on this chassis |
| Slots fielded | 3 of 3 |
| `fabricate` actions | 0 — the striker arm declares no fabrication action, read from the mask |
| **Runtime faults** | **0** |
| **Non-`success`/`blocked` resolutions** | **0** |
| Multi-body ticks | 7,601 of 24,387 (31%) |

Two rows to read together: the coordination layer has an opinion on only 31% of
ticks, and it is worth +3.54 territory across all of them. The other 69% are one
body playing wave 5's doctrine unchanged.

## Contract facts verified rather than assumed

Each was read out of `header.contract` in a replay, because there is still no way to
print it (see below).

| Fact | Read value | Why the rule needs it |
| --- | --- | --- |
| `movementProfiles[].facingCoupling` | `facing-locked` (one ground profile) | decides whether a sibling's route is exact or a guess |
| `move` legality under that coupling | `allowedValues: ["east"]` for an east-facing body — **exactly one direction** | this is what makes the lane claim exact rather than probabilistic; verified from the mask, not from the addendum's sentence about it |
| map `tileRows` | 24 one-tile corridor tiles of 233 open | the choke set, derived rather than named |
| `lifecycle.automaticReturnPlacement` | `own-side-chain-adjacent-objective-tile-in-team-advance-order-then-assigned-spawn` | the rally-pad rule's whole premise, and its refutation |
| `forms[].projectileGuard` | `null` on all four striker forms | why the deflection rule is unexercised |
| `unitSlots` state + `dueTick` | `availability-pending` / `automatic-return-pending` carry the arrival tick | how "an arrival is due" was asked without hard-coding 120/260 |

## Hardcoding temptations resisted

- **The right-of-way order contains no identity assumption.** It is keyed on route
  cost, then `UnitId`, then `LifeId`, taken from the topology's own slot identities.
  Participant IDs are never assumed to be 0/1, never assumed equal to team IDs, and
  never assumed dense.
- **The choke set is derived from the map contract's tile rows**, not from the
  coordinates in the rule card. `(8,7)`, `(9,7)` and the rest appear in this
  document and nowhere in the source.
- **The claim depth is 2 because "this tick or next" is two ticks**, and the
  corridor span is a bounded search rather than a corridor length.
- **The arrival horizon is derived** from `CaptureTicks(tick)`, so it moves with a
  declared capture threshold or gain phase.
- **A sibling's mobility is its form's declared action mask** plus the absence of a
  pending transition — never its objective weight, which a stance keeps while
  standing perfectly still.
- Everything wave 5 resisted is still resisted: the initial-aim range, the bend
  window, unlock ticks, rebuild delays, destruction policies, slot counts, the
  volley's bolt count and spread, each route's own placement tag lists, and both
  chassis's stats all come from the contract. 60/180/300, rebuild 22, rebuild 30,
  120/260 and 18 appear nowhere.

## Top three frictions this revision

**1. `qualify` writes 36 viewer files nobody asked for — in the release whose
headline change is that runs no longer write viewers.** CLI 0.9.22's stated
improvement is that "experiment runs NO LONGER write viewer.html by default (pass
`--viewer` or `--open`)". That was applied to `experiment frontline-labs` and
**inverted on `experiment frontline-labs qualify`**, which now emits a
self-contained 6 MB `viewer.html` beside every probe replay with no flag to stop it
— `qualify --help` lists only `--bot`, `--runtime`, `--seed`, `--suite`, `--out`.
Measured on this freeze: the qualification tree came out at **224 MB, of which 196
MB (86%) was 36 viewer files**, against 33 MB of replays and reports. The same suite
on the same lineage under 0.9.21 wrote **zero** viewers and totalled 35 MB. So the
release that set out to stop writing viewers made the one output every author is
*required* to archive six times larger, and did it on the path where the files are
least useful — nobody opens a viewer for
`contract-matrix/bot-team-0/determinism-repeat`. I pruned them from the freeze (the
packet requires the qualification JSON and every verified probe replay; a viewer is
neither, and re-running `qualify` regenerates them), re-verified 37 of 37 replays
afterwards, and the archived tree is 32 MB. The fix is one line: give `qualify` the
same `--viewer` opt-in its sibling command just got.

**2. The wave's own subject has no contract surface, and the field that would grade
it exists and is never filled.** This wave asked eight authors to fix multi-body
coordination. The platform offers exactly one coordination primitive and never
describes it as one: **every life of a participant receives the same frozen
team-perception union on the same tick, so any pure function of that union is a
convention every sibling computes identically.** That is the entire foundation of
this revision, and it is derivable only by composing three sentences from two
documents — "observations are frozen before any same-tick decisions execute", "a
life never sees an ally's current action", and "allied perception is an immediate
union". All three are presented as *perception* rules. Nothing anywhere states the
consequence: **shared frozen observation is a coordination channel, and it is the
only one you get.** Two smaller pieces of the same gap. The rule card lists
"following a vacated actor blocks" among six blocking rules as though it were a
corner case, when it is the rule that decides how allied bodies may share a
corridor at all — it means a two-body column can never advance at one tile per tick,
so the correct answer to a choke is never "queue up" but "one at a time with a gap",
and that single sentence would have saved me a design iteration. And
`qualification.json` on 0.9.22 carries a **`coordinationGradeAwarded`** field that
suite 5 leaves `null`. A coordination grade is precisely the instrument this wave
needed — the union profile fields two bodies and therefore cannot exercise a single
rule any of us wrote, so eight authors just spent a wave on behaviour the
qualification suite is structurally unable to see — and the schema already has the
slot.

**3. The isolation harness now delivers every entrant's brief in one file.** The
packet's rule is "never a shared or guessably named scratch path", and it cites a
wave-1 exposure as the reason. Wave 5's friction was that the agent harness
recommends a shared temp directory in bold while the packet forbids it. **Wave 6 is
worse in kind rather than in degree: the assignment itself now arrives as one key
inside `w6-prompts.json`, in that shared directory, alongside the other seven
entrants' briefs.** An author cannot read its own assignment without opening a file
that contains all eight, and cannot find its own key in a JSON object without
enumerating the others — which is what happened here and is disclosed above. There
is no careful way to comply; the delivery mechanism is the leak. Competitive
independence is this experiment's entire evidentiary basis, and right now it is
protected by nothing but each author's willingness not to read a dictionary it
already holds in memory. One brief per file, per author, outside the shared
scratchpad, and the problem is gone.

### Still open from earlier revisions and still true

- **`--print-candidate-contract` still will not print the contract.** Fourth
  revision, same complaint. This wave needed exactly six contract facts to write
  its rules (tabulated above) and got all six by digging `header.contract` out of a
  15 MB replay JSON. The flag emits ruleset ID, fingerprints, map and topology
  profile — none of them. A `--full` that dumped the resolved `rules` object would
  have saved an hour, and the data is already assembled: it is embedded in the
  replay the sibling command writes.
- The published CLI binary in `sandbox/cli-publish/` is still named `botarena`,
  while every document, the brief, and its own `--help` output call it `nilbots`.
- Pointing `--bot` at `out/bot.wasm` still silently drops the declared class and
  resolves from `--classes`. This revision leaned on that deliberately for the
  cross-class cells — one brain, three chassis — but it remains a footgun.
- A composed map still does not appear in the ruleset ID: with and without
  `--duel-map thin-fronts` the primary cell returns the same
  `frontline-labs-1-striker-vs-striker-sail-open-facing-locked`. Two genuinely
  different games share one ruleset ID.
- The decision `debugMessage` is preserved verbatim per actor turn in replay v3 and
  is still documented nowhere. It carried this revision's cast census again.
- **New, and a credit: the 2 MB source cap is the right cap.** Wave 5 froze under
  256 KB and its brief's own instruction was to stop deleting documentation to fit.
  This freeze is 286 KB of source, a substantial fraction of which is the reasoning
  in `Convoy.cs` for why four rules were rejected — and that reasoning is the most
  reusable thing this revision produced. Under the old cap it would have been the
  first thing cut.
- **New, and also a credit: replay writes failing loudly.** Not exercised here (the
  disk never filled), but the 224 MB surprise in friction 1 is exactly the failure
  mode verification exists for, and I would much rather have found it as a `du` than
  as a truncated archive.

### Measurement footguns worth republishing

- `--swap` on two **identical** artifacts reproduces the same match byte for byte,
  so a mirror control has N independent samples, not 2N.
- **On this board the territorial score saturates**, so two configurations that
  visibly play different matches can produce identical scores on every seed. An
  ablation that comes back "identical" has not necessarily done nothing; it has done
  nothing *the scoreboard can see*.
- **New this revision: a coordination change moves breaches, and a breach is worth
  ±30 on a scale whose ordinary matches score ±10.** That inflates the variance of
  every margin far beyond what a positional tweak produces, and it is why this
  report leads with records and counters and treats its own +3.54 as roughly one and
  a half standard errors. An author who measures a traffic rule on twenty samples
  and reads the mean will conclude almost anything — this session's own first
  composite read −0.95 on twenty samples and +3.57 on the next thirty.
- **New: a coordination bug does not show up in resolution outcomes.** Sibling
  collisions never appeared as blocked actions in any of the 1,000-odd matches
  measured here, because the predecessor already refused to submit a step onto an
  ally's tile. They show up as a body that stops moving, which no outcome enum
  records. The instrument has to be built from observations and post-state geometry,
  and it has to be normalised per multi-body tick or a rule that gets bodies killed
  will look like a rule that fixes traffic.

## Timings (macOS, Docker builder, CLI 0.9.22)

| Step | Time |
| --- | --- |
| `dotnet build` (editing loop) | 0.6–1.3 s |
| `nilbots build --no-cache` (cold) | 10–14 s |
| One 500-tick WASM match | ≈1.4 s |
| One 50-sample paired sweep (150 matches + full census) | ≈190 s |
| `qualify --suite frontline-qualification-5` (full cumulative chain, WASM) | 6.1 s wall |
| Census parsing (15 MB replay JSON each) | comparable to running the matches |
| Total measurement for this revision | 11 configurations, ≈840 matches |
