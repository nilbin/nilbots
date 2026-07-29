# DX notes — VectorEdge (striker, vector-edge-v1)

Revision 2. The sections from **Identity** through **What I could not check**
are the revision-1 notes exactly as frozen, written before seeing any opponent
or aggregate result; the revision-1 identity row is the frozen artifact in
`classes-wave-1-2026-07-29/`. Everything from **Revision 2** onward is new and
was written after the wave-1 cross-play, from this entrant's own replays.

## Identity

| | |
| --- | --- |
| Entrant | `vector-edge` |
| Class | striker (declared in `botarena.json`) |
| Lineage | `vector-edge-v1` |
| Role | verdict-doctrine, target cumulative T4 |
| Budget | one authoring pass; mechanical repairs free |
| Packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md` |
| Addenda used | `FRONTLINE-LABS-RULES.md`, `EXPERIMENTAL-FRONTLINE-CLASSES.md`, `WASM-DEVELOPMENT.md`, generic-actor template, SDK public types |
| Suite | `frontline-qualification-5` / profile `frontline-duel-depth-union-t4-v1` |
| Outcome | exit 0 — **T4 awarded**, `balanceEvidenceEligible: true` |
| `out/bot.wasm` SHA-256 | `c3a7b7bccf52c62a986e22af6356c9191de9d64a59a7d61cedab9be12e736ada` |
| `evidence/t4/qualification.json` SHA-256 | `c08cec16bfba8d2f79ec51e527a9371a8e24447e962ca8f13314ef003e81c991` |
| Artifact size | 3,299,187 bytes |
| Toolchain | NativeAOT-LLVM 10.0.0-rc.1.26306.1, SDK/Guest 0.10.4, Docker builder (macOS arm64 host) |

## Timings

| Step | Wall time |
| --- | --- |
| `dotnet build` of the player project (edit loop) | 0.4–0.5 s |
| One in-process 500-tick match incl. project build | 2.6 s |
| `botarena build --no-cache` (cold key, warm builder image) | 7.4–8.0 s |
| `qualify --suite frontline-qualification-5` (reruns T3 and T2) | 10.6 s wall / ~90 s CPU |

The loop is genuinely fast. The single largest time sink in this assignment was
not any tool — it was reading enough of the contract surface to know which
values were mine to read rather than assume.

## Documentation gaps

1. **Nothing states the within-tick order, and the whole game depends on it.**
   `FRONTLINE-LABS-RULES.md` says damage is simultaneous and that observations
   are frozen before same-tick decisions, but never says movement resolves
   *before* combat. That single fact decides how a shot must be aimed: the
   target you can see has already moved by the time your bolt launches, so a
   shot at a visible enemy's current tile is a lead, not a hit. I recovered the
   ordering from the Anchor description ("starts after movement/fabrication …
   through combat and objective") and confirmed it against replays. A one-line
   phase list in the rule card would have saved an hour and is the difference
   between a bot that fires and a bot that hits.

2. **Nothing says facing survives movement.** The `Movement` event payload
   documents "Actor facing retained during movement", which is the only place
   this appears — and it is in an SDK doc comment on an *event*, not in the
   rules or the actions section. For a class whose shot programs start along
   its facing, this is the central tactical fact: you can strafe while keeping
   the gun on the threat axis, and you must spend a tick to turn otherwise. The
   generated starter never rotates, so a reader who trusts it will never
   discover the mechanic at all.

3. **"Sole presence" is stated but its consequence is not.** The rules say sole
   mobile presence gains progress and that stacking does not accelerate it. The
   consequence — that the *only* way to capture is to remove the other body,
   and that standing on a contested objective is therefore a denial play rather
   than a capture play — is the entire strategic core of the mode and is left
   for the author to derive. Worth one sentence.

4. **The permanent spawn reservation is a rule with no contract pointer.**
   "The authored Prime spawn remains reserved against own child movement"
   appears only in the Fabricate section, and the rules do not say which
   contract field expresses it. It is derivable — `LifecycleAssignment
   .AssignedRespawnSpawnId` joined to `Map.SpawnAnchors` — but nothing tells
   you that, and a bot that misses it will happily plan routes through a tile
   it can never enter. This cost me a real defect (see repairs).

5. **`--print-candidate-contract` ignores declared classes.** With
   `"class": "striker"` in both manifests, an actual match prints "Classes
   resolved from bot manifests: striker-vs-striker" and runs the class arm, but
   `--print-candidate-contract` on the same two specs prints the *base*
   `frontline-labs-1` identity. The one command whose entire job is "show me
   the exact resolved contract for this spec" is the one that does not apply
   manifest class resolution. I only noticed because the forms in the printed
   contract had no class prefix.

6. **No way to run a class-declaring project against the base contract.**
   Once `"class"` is in `botarena.json`, every `experiment frontline-labs` run
   with that project resolves the class arm; `--classes` must agree, and there
   is no `--classes none`. But qualification runs the WASM artifact, which
   carries no manifest, so it exercises the *base* and duel-depth contracts.
   That means the contract you qualify against is one you cannot conveniently
   play locally. My workaround was to keep a scratch copy of the project with
   the `class` key stripped — which is exactly the kind of divergence between
   tested and shipped source that a freeze rule exists to prevent.

7. **Probe semantics are inferable only from replays.** A failing probe reports
   rich counters (`curvedAttackCount`, `apparentThreatTurnCount`,
   `successfulThreatMoveCount`, `usefulAutomaticChildCount`) but no predicate.
   `strict-corner-invalid-intercept` turned out to mean "do not fire the curve",
   while `wall-terminated-bend / off-axis-visible-target` means "do fire the
   curve" — opposite requirements distinguished only by the variant name. Both
   are fair capability checks and I found both by reading my own probe replays
   tick by tick, which is the right tool; but a one-line `expectation` string
   per variant in the report would turn a 20-minute forensic read into a
   30-second one without revealing any threshold.

## Terminology that cost time

- **"Available" vs "AllowedByForm".** The names suggest a subset relationship
  and the doc comments confirm it, but the useful distinction — `AllowedByForm`
  is "this body could ever do this", `Available` is "this body could do it
  right now" — is what lets you ask "am I a fabricator who is simply not home?"
  That question has no other answer in the contract, and the naming does not
  point at it.
- **"Shot program" `BendEveryTiles` when `BendCount == 0`.** The canonical
  straight program requires `BendEveryTiles: 1` with `BendAfterTiles: 0`, which
  reads like an off-by-one until you find `AimOnlyProgram` and realise the
  sentinel values are declared, not derived. The type invites you to construct
  `default` and be rejected.
- **"Redeploy pause" / `ControlResumesAtTick`.** Two names for one clock, in
  the rules and the observation respectively, and neither says the other exists.
- **`ObjectiveWeight`** is the field that decides whether a form can capture at
  all, but nothing in its name or doc comment says "zero means this body cannot
  contest". I only trusted it after cross-reading the turret row of the form
  table.

## Hardcoding temptations

Ranked by how strongly the environment pulled toward the shortcut:

1. **The map.** `FRONTLINE-LABS-RULES.md` prints the 23×15 tile grid, both
   spawns, and all five objective regions as literal coordinates. It is by far
   the fastest path to a working bot and by far the most brittle. The rules text
   does say to use the tags rather than copy the coordinates — good — but it
   prints them anyway, one paragraph earlier. Resisting this took a real
   decision; the payoff arrived immediately, because the qualification probes
   ship their own maps, their own spawn anchors and their own *projectile
   ranges*.
2. **Unlock ticks 120/260.** Printed in the rules, printed again in the class
   addendum with an explicit "do not hard-code 120/260" warning. The warning is
   the reason I looked for `LifecycleAssignment.UnlockTick` — and then found
   that the class arm uses `DormantAutomaticActivationAtTick` where the base
   uses `DormantUnlockAtTick`, which is the actual thing that would have broken.
3. **Form names.** `striker-prime`, `prime-mobile`, `turret` are so readable
   that string comparison feels natural. I never compare a form name; the bot
   asks the contract for `MaxHealth`, `ObjectiveWeight` and `AttackProfileId`
   and decides from those. This is what let one source play four different form
   catalogues, and it is what the class addendum is really asking for when it
   says to condition on stats rather than names.
4. **Shot-program bounds.** The class table says "one private bend"; the base
   rules say "1–3 bends, every 1–3 tiles". Writing `bendCount: 1` would pass the
   class arm and quietly under-use the base envelope. Enumerating from
   `MinBendCount`/`MaxBendCount` cost ten lines and covers both.
5. **Team 0 advances east.** Every printed example has team 0 on the left.
   `FrontlineTeamAdvance.ObjectiveIndexDelta` exists precisely so you do not
   assume this, and `--swap` exists to catch you if you did.

## Repairs

All four were mechanical defects in contract handling or tempo, found from my
own qualification probe replays; none changed the doctrine.

1. **Routes planned through permanently reserved tiles.** T2
   `automatic-life-cycle` failed with `usefulAutomaticChildCount: 1` of 2. A
   child spawned in the home pocket oscillated between two tiles forever: its
   shortest route ran through the Prime's reserved spawn, which it can never
   enter. My path search treated *all* obstacles as transient and only applied
   them to the first step. Fix: split "bodies and projectiles, which move" from
   "walls and other slots' reservations, which do not", and respect the latter
   at every search depth.
2. **A predicted lane outranked a real shot.** T3 `cadence-parity /
   range-4-threatening` failed: at tick 0 the bot slid off a tile the enemy was
   merely *pointing* down, landed behind a wall, and then sat for seven ticks
   with a clear cooldown and a visible enemy. Fix: while standing on the
   objective, a shot outranks shuffling away from a predicted lane — which is
   the doctrine's own "suppression over concession" rule, applied one priority
   level higher than I had it.
3. **Bends fired on free ticks.** T3 `strict-corner-invalid-intercept` failed
   because the bot spent an idle tick on a curve whose path — correctly
   truncated at a strict diagonal corner — threatened nothing. Fix: a free tick
   makes a *straight* shot free; a bend is a commitment and must clear the
   commit threshold regardless of what the tick would otherwise have cost.
4. **Interception priced as a sweep, then priced as certainty.** With (3) in
   place the bot stopped taking the one *legal* curve in
   `wall-terminated-bend`, whose path lands exactly on a visible off-axis body.
   Adding a flat interception bonus fixed that probe and broke two others: the
   bot started trading shots instead of entering the objective. The bonus only
   makes sense against a target that cannot see the gun — a target that is
   watching will simply not be there. Restricting it to unwatched targets and
   halving it passed all three.

The near-miss in (4) is the honest lesson of this pass: each probe is a sharp,
correct statement about one capability, and a scoring change made to satisfy one
of them is a change to the bot's entire tempo. The suite caught it; my own
self-play did not, because a mirror match hides a tempo error that both sides
make.

## Strategy passes

One. The priority list, shot solver, and role assignment were written once; the
only tuning was a single ordering correction (fire-for-value before advancing,
fire-for-free after) made when self-play showed the bot spending 316 of 500
ticks shooting at 3.8% accuracy. Everything after that was probe-driven repair.

## What I could not check

- Any opponent other than the packet-permitted generated starter and my own
  mirror. Both mirrors are lively 500-tick fights with kills on both sides,
  which is what I would want from a duelist, but a mirror cannot tell me whether
  the doctrine is *right* — only that it is self-consistent.
- Whether the class arm's striker-vs-bulwark and striker-vs-fabricator pairings
  expose anything the striker mirror does not. The bot reads the opposing form
  catalogue for health, cooldown, vision shape and transition routes rather
  than for class names, so it should adapt; that is an untested claim.
- Fuel headroom under the WASM limits at the densest tick. No fault occurred in
  any qualification run or WASM match, but I have no direct measurement.

---

# Revision 2

One improvement budget. Written after reading this entrant's own factorial
replays and before seeing any wave-2 result.

## Identity

| | |
| --- | --- |
| Revision | 2 (lineage `vector-edge-v1`, source in `classes-wave-1-revision-2-2026-07-29/`) |
| Predecessor | `classes-wave-1-2026-07-29/vector-edge`, untouched |
| Class | striker (declared in `botarena.json`, unchanged) |
| Budget | one strategic revision; mechanical repairs free |
| Suite | `frontline-qualification-5` / profile `frontline-duel-depth-union-t4-v1` |
| Outcome | exit **0** — **T4 retained**, `balanceEvidenceEligible: true` |
| `out/bot.wasm` SHA-256 | `36cadf4bac048b1f6566b65961bfd4528f07f9bb6367c11d91327d5e66e01493` |
| `evidence/t4/qualification.json` SHA-256 | `e41d28a635c55fd996b8941c5147a2e765c749281375b9d90c25044f4bc3ed22` |
| `evidence/t4/prerequisite-t3/qualification.json` SHA-256 | `df0a5b13d528fa8bd82883d363c2fc67c71a464f280670ea368e06911a7a4cec` |
| Toolchain | NativeAOT-LLVM 10.0.0-rc.1.26306.1, SDK/Guest 0.10.4, Docker builder (macOS arm64 host) |
| Template sync | `ArenaBasics.cs` copied verbatim from `templates/botarena-generic-actor/`, SHA-256 `9cae31d594188f46403887578c2347307cfecd018b2a9d84e06fc82c09afb194` |

## Isolation exposure — disclosed as the packet requires

While writing the loss-forensics script I globbed

```
.../candidates/*/populations/classes-wave-1/matches/*<name>*/attempt-01/replay.json
```

with a pattern intended to select my own striker-mirror matches. The pattern
matched on the *opponent's* name, so the glob also returned 36 replays of
matches that entrant played against three other entrants — replays that are not
mine. Two runs of the script printed one aggregate row per replay before I
noticed: candidate arm, side, win/loss/draw, final territorial score,
completion reason, end tick, shots fired, bends fired, damage dealt and taken,
kills, deaths, and blocked-move count, computed for the other entrant. I did
not open those replays for anything else, and no per-tick behaviour, source, or
standings table was read.

I then hard-wired the filter so the helper cannot return a match directory
without `vector-edge` in its name, and re-ran everything. Nothing in this
revision was chosen because of what those rows showed — the diagnosis and every
change below come from matches this entrant played — but the exposure happened
and the packet says to say so exactly as the wave-1 author did.

## Forensics: what the replays actually said

Numbers below are from the six distinct striker-mirror matchups in my own
factorial replays (three map arms × two sides; the three seeds per cell were
byte-identical, see "frictions").

**The record.** 15 losses and 3 wins in the mirror; every other pairing was
comfortable. In the mirror I was out-damaged 288 to 200 and out-killed 94 deaths
to 64, while the opponent fired 622 bolts to my 521.

**Where the ticks went.** In 1,360 ticks with the gun ready and a body inside
weapon reach, I fired on 521 of them — 38%. 238 went to a rotation, and of those
rotations **105 (42%) never produced a shot at all**: the life ended first.
Meanwhile 110 rotations happened on cooldown ticks, where turning is free.

**Why the tempo was inverted.** `ShotSolver` enumerated only *currently
available* attack actions, so every "what would I hit if I faced there?"
question answered zero while the weapon was on cooldown. Aim could therefore
only ever be bought on a tick that could also fire. That is a mechanical defect
with a strategic invoice.

**Where the damage came from.** Of 288 hits on me, 28% were from bolts that
never appeared in any of my observations, and 53% appeared in exactly one — one
tick of warning, which is the whole reaction window.

**And then the actual mirror pathology**, visible in a single traced match: both
duelists on the objective two tiles apart, both firing a shot my model priced at
1.14, both stepping aside, both stepping back — a three-tick cycle repeated for
hundreds of ticks, capture progress pinned at zero because a contested objective
scores nothing. The model kept valuing a bolt that had never once landed. That
is what "farmed at standoff range" looks like from the inside, and the opponent
did not have to do anything clever to cause it.

## The one strategic revision

**The dodge model is measured instead of assumed** (`DodgeLedger.cs`). Each
tick, the ledger compares where every visible body was with where it is,
counting only bodies that could see this one, and blends the result into
revision 1's assumed stay-put rate with a few observations' worth of prior. The
solver's inertia term reads that estimate.

Against a target that steps off everything, the straight bolt's value collapses
toward the tiles the target will really occupy — so the bend wins the
comparison it used to lose, and when neither clears the bar the tick goes back
to the ground. Against a target that holds its line, the estimate stays at the
prior and nothing changes.

The fire *ledger* is untouched: `PositionalThreshold` is revision 1's exact
table. I built and measured a second idea — a cheaper standoff price for a tick
whose alternative was only an approach step — and **removed it**: it was worth a
large head-to-head margin but failed `entry-initiative` and `map-holdout`
("waiting for a perfectly safe moment fails"), and it was a second strategic
change I had no budget for. Its removal is recorded here rather than silently
dropped.

## Mechanical repairs (free)

1. **Its own bolt was blocking its own corridor.** `Field` treated every
   visible projectile as an obstacle. The contract declares
   `alliedProjectileContact: "pass-through"` and
   `alliedMovementDestinationOverride:
   "pass-through-does-not-block-or-consume-otherwise-use-contact-policy"`.
   Revision 1 therefore fired east down the central corridor, found east
   blocked by its own bolt, stepped west, stepped east, and fired again — a
   three-tick stall on the one lane that matters. The template's
   `TryAdvanceToActiveObjective` already reads the policy; my hand-written
   version did not.
2. **Aim can now be priced on a cooldown tick.** `ShotSolver.Forecast` uses the
   form's declared attack envelope (`AllowedByForm`) instead of this tick's
   availability; only the decision that actually fires is still gated on
   `Available`. `TryLayGun` spends the *last* tick of the cooldown window on
   aim, where the shot it buys is the next one, and demands that a rotation
   taken with the gun ready beat the bolt in hand by 2×.
3. **Three absolute direction orderings removed.** The dodge tie-break, the
   reseat tie-break, and the route search's iteration order all preferred
   north/east/south/west absolutely, which is a measured side bias on a
   mirror-symmetric map. All three now use `ArenaBasics.OrderedDirections`.
   Route search was rewritten as one multi-source BFS returning *every* tied
   shortest first step (`Field.StepsToward`), which is also what lets a
   facing-coupled arm pick the equally short step that lays the gun.
4. **`Reseat` had silently stopped evading.** During the rewrite its "do I have
   a shot?" test became a `Forecast` call, which is true on cooldown ticks too,
   so the firing-seat and off-the-lane branches almost never ran. It now takes
   the straight `ShotPlan` — a bolt firable *this* tick — which is what
   revision 1 meant.
5. **A rotation oscillation.** With the priced turn (`TryLayGun`) and the crude
   bearing (`TryOrient`) both live, a body could alternate between them
   forever. Ordering the priced turn first and leaving the bearing as the last
   resort settles it; I tried gating the bearing away entirely and measured it
   as clearly worse (15-31 versus 10-33 over 48 matches), so it stayed.

## Movement-arm adaptation

Read from `MovementProfile.FacingCoupling` on the form's own profile
(`Doctrine.CouplingFor`); nothing branches on an arm name.

- **`move-sets-facing`.** A step is a turn, so a dodge spends aim and sight
  quadrant with the tile. `ReaimValue` prices every candidate step from the
  tile it lands on *through the facing it leaves behind*, which required
  threading a shot origin through the solver. Consequences: a dodge that would
  cost more re-aim than the bolt in hand is worth is refused outright when the
  body is on the objective and can survive the hit; tied route steps break
  toward the one that leaves the gun looking at something; and reseat candidate
  seats are ranked the same way.
- **`facing-locked`.** Only the current facing is offered to a move, so a
  sideways dodge is a rotation plus a step — two ticks against a bolt that
  lands on the first. The escape set collapses on its own because it is read
  from the mask; route planning uses the *rotation* domain as the travel domain
  and emits the turn that makes next tick's step legal.
- **Both arms** make a target's own sidestep expensive, so both raise the
  ledger's prior stay-put rate rather than multiplying its answer — the arm
  sets the prior, measurement settles it.

## Evidence

- `frontline-qualification-5`: exit 0, T4 retained, all five probes and the
  hash-linked T3 and T2 prerequisites pass. Full report and 36 verified
  replay-v3 probe replays in `evidence/t4/`.
- Head-to-head against my own frozen revision 1 (in-process, three duel maps,
  both sides, eight seeds, 48 matches, `preserve-facing`): **31 W / 13 L / 4 D**.
- Coupled arms cannot be sparred against revision 1 — its frozen artifact
  predates the `facingCoupling` contract field and its SDK's canonical reader
  rejects the unknown property, so it faults at tick 0. Validated instead as
  revision-2 self-play across both coupled arms, three maps, both sides, four
  seeds: 48 matches, **zero faults**, no stalled or degenerate match.
- Tempo, measured over the 48 head-to-head matches: ready ticks spent shooting
  **38% → 48%**, ready ticks spent rotating **18.5% → 7.6%**, cooldown ticks
  spent marching **70% → 85%**.

## Frictions this pass

1. **Deterministic bots make a seed sweep a single sample.** All three seeds in
   every wave-1 factorial cell produced byte-identical replays, because neither
   policy touched `context.Random`. A three-seed cell reads like three
   observations and is one. Now that ties are broken from the per-life stream
   the seeds separate, but nothing in the tooling warns that a seed set is
   inert — a "distinct replay hashes: 1 of 3" line on a multi-seed run would
   have saved me an hour of misreading the factorial table.
2. **A frozen artifact silently rots when the contract gains a field.** The
   revision-1 WASM cannot run either coupled arm: the canonical contract reader
   validates an exact property set, so an added optional field is a hard
   failure rather than an ignorable one. The failure surfaces as "WASM generic
   actor exited before its life ended… peak completed tick fuel 0.0M", which
   reads like a bot bug. A schema-version mismatch deserves its own message,
   and forward-compatible readers would let a frozen population keep competing
   across arm additions.
3. **`--movement` cannot be combined, and the qualification suite runs a
   different contract than the class arm.** `--movement` may pair with
   `--classes` but "on its own it is a standalone arm and cannot be combined
   with the other experiment options"; qualification meanwhile runs the
   duel-depth union profile with no coupling at all. So the thing I am
   qualified on and the thing I am measured on share no movement contract, and
   there is no supported way to qualify *under* a coupled arm. Everything I
   know about my behaviour there is self-play.
