# DX notes — Still Water, revision 3

## Isolation

Written from this entrant's own authoring session, its own sparring replays
against its own rebuilt predecessor, and its own qualification report. No other
entrant's source, standings, replays, or aggregate balance report was opened in
this revision. Work was confined to
`arena-bots/frontline-labs/classes-wave-1-revision-3-2026-07-29/still-water`
plus the private scratch directory `sandbox/still-water-r3-9f4c2a71`, which is
uniquely named and was created by this session. Both frozen predecessor
directories (`classes-wave-1-2026-07-29/still-water` and
`classes-wave-1-revision-2-2026-07-29/still-water`) were read but not modified;
the r2 rebuild used for sparring is a *copy* of that source inside the private
scratch directory. Nothing was committed to git.

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
| Revision | 3 (one budgeted strategic pass; mechanical/contract repairs free) |
| Class | `striker` (declared in `botarena.json`) |
| Role | `verdict-doctrine` |
| Doctrine | patient interceptor, now priced against the pendulum arms |
| Target tier | cumulative T4 (`frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`) |
| Predecessor | `arena-bots/frontline-labs/classes-wave-1-revision-2-2026-07-29/still-water` (frozen, untouched) |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rule card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `fcd6358d30064f38ea00a2ddd88c9dd0c7406a79ab8bd165c938fc44014c36b4` |
| Starter helper synced | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `a05ec4b4ef15753836ee107586fb6442378ed8078a6d4a3cafef9a9a5bd56368` (vendored byte-identical) |

| Artifact | Value |
| --- | --- |
| `out/bot.wasm` sha256 | `fcf6b4a6bf38454bf995d3efb0cd896c71e34d7757e685eb487b1160388e0662` |
| `out/bot.wasm` size | 3,257,588 bytes |
| Deterministic source-tree hash | `801dda9d6c25549e0ed127a32b164ae080e966d56f12ffb3f11bbcd79f05c8ed` (sha256 of the sorted per-file sha256 listing of all `.cs` + `.csproj` + `botarena.json`, excluding `bin/` and `obj/`) |
| `evidence/t4/qualification.json` sha256 | `ca0c40a3c90cb84e2a4e9d313cb6492918562357539576688ed7919f4b0112cd` |
| `evidence/t4/prerequisite-t3/qualification.json` sha256 | `88fc8cfaec8e5b5dac2072b453e046cd81b241fcb3f9b5597e18f2740e066974` |
| `evidence/t4/prerequisite-t3/prerequisite-t2/qualification.json` sha256 | `b39d269ffa7ab2583879bea3a1da4adada3834294aeb99f3e5b685128c83c813` |
| `evidence/wasm-parity` replay-v3 hash | `dce96a9204e0f23aec5ecb78f95e6a22cca1cc0677102e8e604a6d73b9ce73c6` (`nilbots verify` OK) |
| Toolchain | controlled `nilbots build --no-cache`, CLI 0.9.10, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK `0.10.4`, game rules 0.5, WASI p1 core module, macOS host via the platform-matched Docker builder |

Per-file source hashes at freeze:

```
513fc5d6f70403af07e97702a3c65ce8eb1f1a38b37ed8f2eeecc79e970da32f  ActionBook.cs
a05ec4b4ef15753836ee107586fb6442378ed8078a6d4a3cafef9a9a5bd56368  ArenaBasics.cs
2b5884bf994dca99cf42cda68e77fbd604d949b09bcee436edf2c9a3e93ca5bf  Doctrine.cs
df05fd11c3f1efa2dc032eebc9a11f65478aee5dc61e3447d2115935dedb12a6  Field.cs
e0c4c46870c238900ac0b15aac95a1dd511ccf4ee1419c8888eb3d6217218675  ForkPlanner.cs
be10b13da349a9212461d432a9dff0469c90abf5b990df7bb5ce763e8b16f502  Quarry.cs
3a9c4fc7e643e5f363e1ec0734278d3340014b916cb2eea67c5ceb634d87f695  Ratchet.cs
8f26641e4298a2ecefbfb67f7a8e09f7e91f618659caf6f53c98db961c8fd4f7  StillWater.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  StillWater.csproj
c4d2afe4080bff8e0848b8fec79ac40401c72b502455386c608e74e7d56b6e81  ThreatField.cs
bc4e5dd51f3eb957982536925da6640a314b5e4631a870463ed38e1624f92d3c  botarena.json
```

`ActionBook.cs`, `Field.cs`, `ForkPlanner.cs`, `Quarry.cs`, `ThreatField.cs`,
`StillWater.csproj` and `botarena.json` are byte-identical to the r2 freeze.
`ArenaBasics.cs` is byte-identical to the current template (re-vendored — it
gained the pendulum readers). `Ratchet.cs` is new.

**The r2 artifact had to be rebuilt to spar at all.** The frozen r2
`bot.wasm` (`b0fe1f36…`) faults on the sticky arms because the contract gained
`capture.ratchetHoldTicks`. Rebuilding the untouched r2 source produced
`5c2711a5d542979048e1441b8047428ff319b9c4974fd71803e22bd9c4756b4a`, and that
artifact — same source, current toolchain — is the baseline every number below
is measured against.

## Qualification outcome

`nilbots experiment frontline-labs qualify --suite frontline-qualification-5`
exits **0**. **Tier awarded: T4**, `profileComplete: true`,
`balanceEvidenceEligible: true`. Every component passed on the first attempt —
zero qualification cycles were spent on repairs this revision, against two in r2
and seven in v1.

| Level | Component | v1 | v2 | v3 |
| --- | --- | --- | --- | --- |
| T4 | suppression-choke | PASS | PASS | PASS |
| T4 | entry-initiative | PASS | PASS | PASS |
| T4 | prediction-chamber | PASS | PASS | PASS |
| T4 | front-rotation | PASS | PASS | PASS |
| T4 | map-holdout (thin-fronts) | PASS | PASS | PASS |
| T3 | wall-terminated-bend | PASS | PASS | PASS |
| T3 | strict-corner | **FAIL** | PASS | PASS |
| T3 | cadence-parity | PASS | PASS | PASS |
| T3 | cooldown-window | PASS | PASS | PASS |
| T3 | local-form-safety | PASS | PASS | PASS |
| T2 | contract-matrix | PASS | PASS | PASS |
| T2 | automatic-life-cycle | PASS | PASS | PASS |
| T2 | objective-path | PASS | PASS | PASS |
| T2 | direct-fire | PASS | PASS | PASS |
| T2 | straight-evade | **FAIL** | PASS | PASS |
| T2 | manual-fabrication | PASS | PASS | PASS |

Zero runtime faults and zero invalid actions across the whole qualification
chain and all 40 sparring matches — 47,222 decisions, of which 46,817 resolved
`success` and 405 `blocked`, the latter being the legal joint-resolution
outcome rather than a rejection.

Two design choices exist specifically to keep the new logic inert under the
qualification profile, because that profile runs the duel-depth union contract
with no pendulum and — as r2 learned the hard way — degenerate capture
arithmetic (`captureThreshold: 1000`):

- every pendulum branch keys off a **declared policy value**, never a derived
  ratio. `HoldTicks is > 0`, `SurplusWeightScalesGain`, `ArrivalsRallyForward`
  are all false or null on the union profile, so all of it compiles out at
  runtime. A first draft priced risk as `deathCost / captureTicks`, which with
  a threshold of 1000 evaluates to ~0.03 and would have silently disabled the
  withdrawal posture inside every probe. Booleans over ratios is the rule that
  survives a degenerate scenario.
- the one term that *is* live under the union profile — the binary
  stacking term — is bounded at ±0.8, small enough not to flip a probe, and it
  was verified against the full chain before freeze rather than assumed.

## Doctrine adjustment

Revision 2 was authored for a world where ground given up comes back. On the
ratcheted arms it does not, and three declared policies each withdraw part of
that assumption. `Ratchet.cs` reconstructs the one fact that is *not* in the
observation schema — whose advance is currently protected, and for how long —
from four independent observable signals, so a life born at any point in the
match has an answer: the position change it watched happen (exact, with the
owner from the sign against this team's declared index delta),
`controlResumesAtTick` minus the declared redeploy pause (dates the advance to
the tick for a life that opens its eyes inside the pause), a near-threshold
claim that resets while the index does not move (only a spent capture looks
like that, which proves the *other* side holds), and the front's displacement
from the centre of the chain (which names the last advancer whenever the front
is not level). Every branch defaults to unknown, and unknown plays the
baseline exactly.

What the doctrine does with it:

1. **Inside a hold we own**, an opposing capture that completes inside the
   window is spent — letting them buy it is better than contesting it, because
   they pay full presence for a reset. The window becomes the cheapest
   attacking tempo the contract offers, so it is spent on the next position.
2. **But a hold protects the advance, not the scoreboard, and it does not
   delete progress.** A claim still standing when the window drops converts the
   instant it does, so the claim is ignored only while it would *complete*
   inside the hold, and the tick-cap ledger from r2 is evaluated before the
   ratchet and overrides it. Getting this backwards is what a first
   implementation did, and it handed position 0 back twice in a single match.
3. **Inside a hold they own**, our capture is spent, so the only thing presence
   still buys is denial — and whether denial is worth the exposure is
   `capture.controlPolicy`, not a preference. Weight-scaled control makes
   denial quantitative and worth standing in the open for; binary control means
   one body already nulls them whenever the baseline decides a contest is on,
   and forcing it just walks into the gun.
4. **`lifecycle.automaticReturnPlacement`** switches off the withdrawal posture
   when returns rally to the front: conceding ground to save a body that
   reappears in position pays a real price for a saving that is no longer real.
   It deliberately does **not** discount danger — see below.

## Measured effect versus the rebuilt r2

Five seeds (104729, 130363, 155921, 179424, 224737), both sides, WASM runtime,
every cell `--movement facing-locked`, opponent = r2 source rebuilt on the
current toolchain. Overall **28–12–0 over 40 matches, mean territory +6.6**.

| Arm | Record | Mean territory | r2-vs-r2 control |
| --- | --- | --- | --- |
| `control` (unmodified) | **9–1–0** | **+13.3** | 0–0–10, all draws |
| `--capture-threshold 9 --prime-respawn-ticks 9` | **9–1–0** | **+11.3** | 0–0–10, all draws |
| `--pendulum ratchet` | 5–5–0 | −0.5 | 5–5–0, side-saturated |
| `--pendulum ratchet-contest` | 5–5–0 | +2.2 | 5–5–0 |

**The two sticky arms are side-saturated and their win/loss column is not a
measurement.** Running the rebuilt r2 against *itself* on `ratchet` gives team 1
a 5–0 sweep on every seed, breaching at tick 167 each time; two identical bots
cannot differ, so that 5–0 is the arm, not the bot. Reporting 5–5 there as
"even" would be reporting the side assignment. The informative quantity on those
arms is how long each side lasts, measured against the identical-bot mirror:

| Arm | Role | r3 mean ticks | r2 mirror | Delta |
| --- | --- | --- | --- | --- |
| `ratchet` | r3 defends as team 0 | 400 | 167 | **+233** |
| `ratchet` | r3 attacks as team 1 | 303 | 167 | +136 (slower) |
| `ratchet-contest` | r3 defends as team 0 | 493 | 482 | +11 |
| `ratchet-contest` | r3 attacks as team 1 | **247** | 482 | **−235** (faster) |

So on `ratchet` the revision buys a much more durable defence (the losing side
survives 400 ticks instead of 167) at the cost of a slower offence; on
`ratchet-contest` it buys a decisively faster offence (breach at 164 on three
of five seeds, against a mirror that reaches the tick cap) at no defensive
cost. Net territory moves from the mirror's 0.0 to −0.5 and +2.2. I would not
claim more than that from five seeds against one opponent, and the honest
summary is that the ratchet doctrine changes the *shape* of these matches a
great deal and their *outcome* very little, while the mechanical repairs move
the two non-sticky arms from dead-even to 18–2.

### Ablations (same seeds, same opponent)

Every rule that survived had to earn it, and one did not:

| Variant | Effect |
| --- | --- |
| discount danger under forward-rally (`dangerWeight ×0.75`) | **cut** — carrying it pushed the attacker's breach from 173 to 366 ticks on `ratchet` |
| wait out an enemy hold by standing off (`→ Deny`) | **cut** — carrying it cut defensive life from 462/500/347 to 184/184/217 on `ratchet` |
| stand on the point during an enemy hold (`→ Contest`), ungated | best on `ratchet-contest` (500/500/500, mean 500) and worst on `ratchet` (171/171/171 against 436 without) — hence gated on `SurplusWeightScalesGain` |
| own-hold rules (free window + claim suppression) | byte-identical outcomes on `ratchet` (inert), worth ~130 mean defensive ticks on `ratchet-contest` (493 with, 363 without) — kept |

The danger-discount result is the one worth passing on to the next author:
**forward-rally makes a body cheap to replace positionally, and it is very
tempting to conclude it is cheap to spend. It is not.** Capture wants
uninterrupted sole presence, a death interrupts it wherever the replacement
appears, and the return clock is unchanged — so the tempo cost dominates and
the positional saving is nearly worthless.

## Mechanical repairs (free budget)

1. **Facing-locked steering (the big one).** Candidate steps are now enumerated
   from map geometry, and a step the mask refuses becomes an explicitly priced
   rotation toward it — the scaffold's `TryAdvanceToActiveObjective` rule,
   generalised through the option scorer so danger, coverage, survival and band
   all still price the tile. It also works when the movement action is absent
   entirely, which r2's mask-seeded loop did not.
2. **Transient occupants are no longer walls.** The goal cost field is computed
   over walls only; bodies and bolts apply to the one step actually taken. This
   is the scaffold's documented first-step-only rule, and it matters most under
   forward-rally, which deliberately concentrates arrivals on one region.
3. Re-vendored the current template `ArenaBasics.cs` verbatim, and the new
   pendulum readers (`Capture`, `ArrivalsRallyForward`, `ObjectivePresence`)
   are used rather than reimplemented.
4. `TicksToNeutralise` no longer budgets a decay path that an
   `enemy-sole-erosion-only` decay clock would never deliver. That arm is not
   measured in this round; the fix is correctness hygiene.

### The 24-match self-inflicted wound, written down because it is instructive

The first build of the steering rewrite lost **every one of 24 matches,
including on the control arm where the ratchet code is provably inert.** The
cause was one line of arithmetic. I priced a rotation as "the value of the tile
it unlocks, minus one tick of route". But a step that improves the route by one
is worth exactly `goalWeight × 1`, and one tick of route costs exactly
`goalWeight × 1` — the two cancel, the residual tempo penalty tips the balance,
and a facing-locked chassis prefers standing still to turning, forever. The fix
is to score the rotation *where the body actually stands*, wearing the facing it
will have, plus the goal-cost the lane removes: a turn is then correctly worth
more than waiting, because waiting buys nothing at all. That single change took
the three-seed sweep from 0–24 to 17–7, and the control arm from 0–6 to 6–0.

The reason it took one cycle rather than several is that the replay records the
decision's `debugMessage`. Tagging every decision with its posture and reading
the distribution straight out of the replay turned "why is it losing" into a
table in about two minutes. That is the single most useful debugging affordance
in this toolchain and it is not documented anywhere.

## Top frictions this revision

1. **`--print-candidate-contract` does not print the candidate contract.** It
   emits the resolved *identity* — ruleset ID, fingerprints, map, topology
   profile — and nothing about the rules themselves. When the whole assignment
   is "price four capture policies", the one thing needed is the resolved
   `gameMode.capture` and `lifecycle`, and the only way to see them is to run a
   match and dig `header.contract` out of the replay JSON. The flag's name and
   the class addendum's description of it ("emits the exact resolved identity
   for a spec") do not agree, and the packet's advice to be contract-driven is
   materially harder to follow than it needs to be. Printing the resolved
   capture and lifecycle blocks — or accepting `--print-candidate-contract=full`
   — would remove an entire class of guesswork.
2. **The decision `debugMessage` is the best diagnostic in the system and is
   undocumented.** `submittedDecision.debugMessage` is preserved verbatim in
   replay v3 for every actor turn, which makes a bot's internal state
   inspectable at full time resolution without a debugger, an instrumented
   host, or a rebuild of anything but the bot. Neither the rule card, the
   template README, nor the replay-format notes mention it. One sentence in the
   starter README — "whatever you pass as the decision reason is recorded per
   tick in the replay; use it" — would be worth more than several helpers.
3. **A hold is a team-scoped fact delivered to life-scoped memory, and nothing
   bridges the gap.** `capture.ratchetHoldTicks` is declared, but *when the
   current hold started* is not observable; the addendum says to "track when the
   hold started", and private memory is destroyed on every death. Under
   `forward-rally` — which the same arm turns on — bodies die and return
   constantly, so the tracking life is routinely not the surviving life. In a
   traced match my two bodies disagreed about whether a hold was live on the
   same tick, because one had watched the advance and the other was born after
   it, and they correctly played two different doctrines. Team perception shares
   the current observable union, which does not include this. Either
   `holdStartedAtTick`/`holdExpiresAtTick` belongs in the Frontline mode
   observation next to `controlResumesAtTick` (which is exactly this kind of
   fact and *is* published), or the addendum should say plainly that the hold
   clock is only recoverable per-life and expect divergence. As it stands the
   arm asks for a team-level inference through a life-level channel.
4. Still open from r2 and still true: `--movement` refuses to compose with
   `--duel-map` (`Use one Frontline Labs experiment option at a time.`), which
   is now a bigger hole than it was — every measured cell is `facing-locked`,
   so "does my doctrine hold on the map arm designed to punish retreat?" is
   still unanswerable. Pointing `--bot` at `out/bot.wasm` still silently drops
   the declared class and resolves the base contract. And the published CLI
   binary in `sandbox/cli-publish/` is named `botarena`, not `nilbots`, while
   every document and its own `--help` output call it `nilbots`.

## Timings (macOS, Docker builder, CLI 0.9.10)

| Step | Time |
| --- | --- |
| `dotnet build` (editing loop) | 0.5 s |
| `nilbots build --no-cache` (cold) | 8.2 s |
| One 500-tick WASM match | ~1.9 s |
| Full 4-arm × 2-side × 5-seed sweep (40 matches) | ~85 s |
| `qualify --suite frontline-qualification-5` (full cumulative chain, WASM) | 5.9 s wall |

## Hardcoding temptations resisted

- Every pendulum fact is a declared policy value read through the template's
  own readers; nothing branches on a CLI flag name, an arm list, or a ruleset
  ID (the resolved ID `frontline-labs-1-striker-vs-striker-ratchet-facing-locked`
  names all three arms and is never parsed).
- The 40-tick hold, the 18-tick return, threshold 15 and threshold 9 are read,
  never written. The numbers arm needed no code at all.
- Risk appetite is gated on booleans, not on ratios of contract numbers,
  precisely so a degenerate probe threshold cannot silently invert it.
- Hold ownership comes from the sign of the index change against the team's own
  declared `ObjectiveIndexDelta`, and displacement from the chain's own centre —
  no compass, no spawn, no team-ID assumption.
- Objective weight is read per form, so a weight-zero body is correctly counted
  as holding ground for nothing.
