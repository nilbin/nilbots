# DX notes — VectorEdge (striker, vector-edge-v1)

Written after the first source freeze and the T4 qualification pass, before
seeing any opponent, standing, or aggregate balance result.

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
