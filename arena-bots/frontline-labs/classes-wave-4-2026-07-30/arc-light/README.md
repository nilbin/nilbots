# arc-light

A Frontline Labs **striker** whose doctrine is built around the class-skill kit
rather than retrofitted onto it. Wave-4 entrant, fresh lineage.

Qualified **T4** on `frontline-qualification-5`
(`frontline-duel-depth-union-t4-v1`), `balanceEvidenceEligible: true`.

## How it plays

- **Aiming is interception, not line of sight.** Movement resolves before
  combat, so every candidate arc is scored against where the target can *be* when
  the bolt arrives. Under a facing-locked coupling a body may only step where it
  faces, which turns its reachable set into a ray — arc-light shoots the ray.
- **A curved arc is a commitment.** The whole declared bend envelope is
  enumerated and previewed through the SDK's own bend rule, but a bend is only
  spent on a hard interception: the tile the target stands on, or the one tile it
  can reach exactly as the bolt lands.
- **The volley is priced before it is paid.** The stance is entered only where the
  map's transition tags allow it, only when no bolt can reach the tile during a
  windup that permits nothing but waiting, only when it sheds no objective weight
  that is holding the claim, and only when the fan beats the ordinary gun — which
  it does in the blind spot the class's zero initial-aim envelope creates at close
  diagonal range, and when one fan reaches two bodies.
- **The hold is read, never inferred.** Inside an enemy territory hold a completed
  capture is spent, so the claim is banked short of the threshold and completed on
  the tick the hold lifts. Surplus objective weight scales capture pressure, so
  presence is pressure; only an enemy standing alone erodes a claim, so leaving an
  empty objective is free and leaving a contested one is not.
- **Every projectile it does not own is hostile** — including a bolt of its own
  that an aegis shell returned. Enemy guards are counted from published
  deflection events, so a shield one contact from breaking is fed rather than
  avoided.
- **Bodies envelop.** Independent lives with empty private memory take a rank
  from the shared observation and spread across different bearings, because
  clumping into one lane is what feeds a three-bolt fan.

Nothing above is conditioned on an arm name. The same artifact plays the kit-off
cells (where no stance route exists and the stance code simply declines), both
bend envelopes, and the classless duel-depth qualification profile — where the
same code finds an anchor route and explicit fabrication instead.

## Running it

```bash
# development, class resolved from botarena.json
nilbots experiment frontline-labs --bot . --opponent <other> \
  --pendulum keel --movement facing-locked --skills kit --bend universal \
  --seed 104729

# the frozen artifact carries no class manifest, so name the pair explicitly
nilbots build . --no-cache
nilbots experiment frontline-labs \
  --bot out/bot.wasm --opponent <other>/out/bot.wasm \
  --classes striker-vs-striker \
  --pendulum keel --movement facing-locked --skills kit --bend universal \
  --seed 104729 --runtime wasm

nilbots experiment frontline-labs qualify --bot out/bot.wasm \
  --suite frontline-qualification-5 --out evidence/t4
```

`DX.md` holds the freeze identity, the measured per-arm records, the causal
ablation that prices the volley, and the authoring frictions.

## Source layout

| file | what it owns |
| --- | --- |
| `ArcLight.cs` | the tick priority list — the only place decisions are ordered |
| `ArcFacts.cs` | one-time contract reads: forms, routes, stance budgets, coupling, capture policy, protected terrain |
| `ArcGun.cs` | the bend-envelope search, reach maps, and interception scoring |
| `ArcStance.cs` | entering, aiming, firing, and leaving a stance; finding a legal cast position |
| `ArcKeel.cs` | what a push is worth right now: hold clock, weight arithmetic, push/bank/deny intent, envelopment goals |
| `ArcThreat.cs` | incoming bolts, enemy gun and fan lanes, guard accounting |
| `ArcMove.cs` | routing under a facing coupling, evasion, and in-objective repositioning |
| `ArenaBasics.cs` | the scaffold's contract readers, unmodified |
