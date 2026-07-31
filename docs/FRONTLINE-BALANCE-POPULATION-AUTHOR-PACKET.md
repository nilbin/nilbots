# Frontline Balance Population author packet

Status: current common input for retained Frontline calibration instruments,
verdict-band doctrines, and potential system-owned launch opponents. The
historical first-cohort packet remains frozen separately.

## Assignment supplied by the orchestrator

Every author receives the same packet plus these explicit values:

- population and authoring-lineage IDs;
- one doctrine brief;
- target qualification profile and tier;
- role: `boundary-instrument` or `verdict-doctrine`;
- implementation, mechanical-repair, and improvement budgets;
- permitted player-facing rules/map addenda;
- exact output directory.

Running the same unconstrained prompt several times is not independent
authorship. Doctrine briefs must demand different resource, route, positional,
or opponent-model priorities.

## Product and evidence role

Every submitted revision is retained as source plus compiled WASM. A
mechanically qualified and entertaining revision may later be promoted,
unchanged, as a visibly system-owned playlist opponent. Promotion is not
automatic: Lab-only sentinels, ablations, metric attackers, deliberately
crippled instruments, and confusing policies remain internal.

A `boundary-instrument` should pass Tn and demonstrably fail T(n+1). Its job is
to calibrate the fun floor and adjacent-tier gradient, not to maximize wins.
A `verdict-doctrine` targets T5/T6 and should express its assigned strategic
idea as strongly as the equal authoring budget allows. Tournament standing
never awards a tier.

## Permitted material

Use only:

- this packet and the assigned doctrine/target fields;
- the generated `nilbots new <Name> --profile generic-actor` project and its
  README/helper;
- [`FRONTLINE-LABS-RULES.md`](FRONTLINE-LABS-RULES.md);
- explicitly assigned public experiment addenda, such as
  [`EXPERIMENTAL-FRONTLINE-DUEL-DEPTH.md`](EXPERIMENTAL-FRONTLINE-DUEL-DEPTH.md);
- [`EXPERIMENTAL-FRONTLINE.md`](EXPERIMENTAL-FRONTLINE.md);
- [`WASM-DEVELOPMENT.md`](WASM-DEVELOPMENT.md);
- public SDK types and XML documentation;
- the public qualification summary and the author's own qualification
  report/replays after its first source freeze.

Do not inspect Engine/App implementation, private probes or holdouts, another
entrant's source, standings, aggregate balance reports, or non-assigned
replays. Mechanical probe feedback may repair contract handling; strategic
improvement consumes the declared equal improvement budget.

Work only inside your assigned output directory plus a uniquely named
private scratch directory — never a shared or guessably named scratch path.
A wave-1 author accidentally read another entrant's replay statistics
through a shared scratchpad; competitive independence is the experiment's
evidence, and an accidental exposure must be disclosed in `DX.md` exactly
as that author did.

## Non-negotiable implementation requirements

- Implement `IGenericActorBot` (or `IGenericMindBot` on a mind assignment) and
  treat the delivered contract plus current action legalities as authoritative.
- Never assume participant IDs are `0/1`, that participant ID equals team ID,
  that one participant owns every team body, or that IDs/counts are dense.
- Read participants, teams, unit slots, controller ownership, forms, health,
  actions, transitions, lifecycle, map regions, objectives, projectile values,
  and mode victory data from the contract.
- Resolve numeric action codes and argument domains from current legality.
  Stable semantic IDs may recognize optional capabilities, but the bot must
  fall back safely when one is absent.
- **Memory.** Per-life: expect one fresh bot instance and empty private memory
  for every new body life. **On the mind profile: one instance for the whole
  match, whose fields are your memory, and a runtime fault forgets the match**
  (the Store is discarded and, at this contract's zero allowance, you are
  disqualified). Coordination is no longer a problem to solve — there is one
  decider — so spend the lines on doctrine instead. Additionally, on the mind
  profile: `Think` runs every tick including with no live bodies, every own live
  body defaults to `Wait` so forgetting one costs a tick rather than the match,
  and `SetRole` publishes a free-vocabulary label your opponent can also read.
- Handle both explicit Fabricate and declared automatic activation/return when
  the assigned qualification profile requires their union.
- Use deterministic contract, observation, identity, and `context.Random`
  inputs only. Return one bounded action on every tick and never deliberately
  fault.
- Keep all gameplay logic in ordinary submitted `.cs` source. A prebuilt WASM
  without its exact source/build inputs is not a population revision.

## Current qualification sequence

Build once through the controlled toolchain, then run:

```bash
nilbots experiment frontline-labs qualify \
  --bot out/bot.wasm \
  --suite frontline-qualification-5 \
  --out evidence/t4
```

Suite 5 is the immutable
`frontline-duel-depth-union-t4-v1` profile. It automatically reruns and
hash-links the exact cumulative T3 prerequisite, then checks suppression
versus concession, proactive choke entry, objective-preserving prediction
responses, front rotation, and the thin-fronts map holdout from both
assignments. Exit `0` awards T4 and entrant-level balance eligibility; `3` is
a clean capability failure that retains any prerequisite tier; `2` means
invalid runtime/contract evidence.

Run suite 3 directly for an assigned T2 boundary and suite 4 for T3. T5 and
higher packets must name their immutable suite, profile, prerequisite
qualification report, and holdout policy once implemented. Never copy a tier
label between profiles, rules generations, or seasons.

## Freeze and archive

For every meaningful revision preserve:

- all submitted `.cs` files and project metadata;
- `botarena.json` and player-facing README;
- authoring lineage, doctrine, target tier, role, and exact budget;
- author packet identity/hash;
- deterministic source-tree hash;
- controlled builder/toolchain identity;
- canonical `bot.wasm` and SHA-256;
- qualification JSON, its SHA-256, and every verified probe replay;
- `DX.md`, including documentation gaps, hardcoding temptations, confusing
  terminology, build/qualification time, repairs, and strategy passes.

Write initial DX notes before seeing opponents or aggregate results. Archive
failed and intermediate revisions instead of overwriting them. A later
official-population manifest references the frozen source/WASM/qualification
identities; it never copies an informal “latest” directory.

## Population stopping rule

At T1/T2 and most of T3, author only enough canonical exact-boundary
archetypes to enumerate materially different behavior. A reimplementation
with the same payoff and dynamics signature adds no calibration value.

At T5/T6, continue independently briefed authorship until there are at least
six effective doctrines spanning the preregistered strategy cells and
leave-one-doctrine-out conclusions are stable. Balance Lab's current
payoff/action/form/objective redundancy estimate is diagnostic until
calibrated; it can request more breadth but cannot delete a revision or
promote a candidate automatically.
