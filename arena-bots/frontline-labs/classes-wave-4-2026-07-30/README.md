# Classes wave 4 (2026-07-30): the skill-doctrine population

The phase-2 population: six lineages revised for the adopted kit on the
keel baseline (#165/#169), plus two fresh skill-native lineages
(arc-light, gate-stone) so adoption failure can never masquerade as
mechanism failure. All cells facing-locked; primary doctrine target the
full game (`rig`). Pre-flight gate ran green (with two gate-metric
fixes it caught: objective-holding is not idleness; `shoot-straight`
counts as shooting). **Eight of eight retained or reached T4, zero
friction kills; both fresh lineages passed on their first authoring
pass.**

| Entrant | Class | `out/bot.wasm` sha256 | Headline |
| --- | --- | --- | --- |
| vector-edge | striker | `16ab20f1785936f3537b6db0629f50b2250c0c372805401f83a8d5fbc031488d` | pose-space solver; declines every cast (priced six ways); 44 breach wins / 0 breach losses |
| still-water | striker | `8ae62751f9f8f6f854d5ed7efd90fad52cdf02d6e0fdcb55ed042a16d8c5547c` | 120-0-0 vs r3; honest thin-fronts null; cone-sharing tax |
| arc-light | striker (fresh) | `cc6ccf624a6e7f934512aaab4233705118817aa32cdc8c9f451dd1b79a753f7e` | casts and wins vs scaffold (16-0-0 @ t160); loses mirrors casting; won cross-class driving other chassis |
| iron-root | bulwark | `ed6e039d407c7eb5ffdf1d4c645699e1f9c3cdfa0461a9dddd6df124a57c22f3` | AEGIS COUNT: shell raised at the muzzle, not the bolt; shell is all of +55.4 on helm |
| march-wall | bulwark | `48e69714fced78b5a1c5a9396b663e72431b642d3928af9b4ff7a0789696eda5` | arc-envelope shield discipline; 15-1-0 rig; shell-vs-shell mutual null found |
| gate-stone | bulwark (fresh) | `b0d74dafaf6aff9c8dc01876447c913513937e3c3b125e2675d147a1bc09bb8b` | capture-arithmetic gate doctrine; 6-0-0 every arm, all breaches |
| spark-line | fabricator | `b5c328875993fd69f2b8d5ba7ca54eb91da1feb26b3a69d6cbb9ea76d3861f4a` | covering-number doctrine ("concentrate what the gun can defend"); 42-6-0 |
| ledger-fly | fabricator | `bdd376cf8dc418316d260e5a4852ebfb097456302188eea720602b85ef2e554f` | contract-shaped stance definition; 24-0-0 veer/rig |

Sparring baselines were r3 sources REBUILT on SDK 0.10.6 (frozen
artifacts fault on the new contracts — expected, #170). Multiple
authors proved gating byte-identically on kit-off arms.

## Converged findings (the tuning-pass agenda)

1. **The `transition-placement-forbidden` tag silently rations the
   entire stance system** (5 independent authors, both classes
   quantified): all 22 objective tiles + the central corridor — 112 of
   233 open tiles — forbid stance entry, so the shell can never stand
   on ground it holds and the volley is castable on ~6% of a standoff
   striker's ticks. The doc's "objective weight stays 1, so it still
   holds ground" is true of the weight and false of the map. Also: an
   unavailable `transform` legality entry carries NO refusing
   constraint — a tag refusal is indistinguishable from any other
   gate.
2. **Shell break budget never fires in competent play** (0 breaks
   across ~350 raises by four bulwark-capable authors) — voluntary
   exits always beat the 3-deflection budget; the punish window is
   near-unreachable for a vision-4 chassis anyway (windup-1 completes
   after combat: a stance is pre-emptive, never reactive — raise at
   the muzzle, not the bolt; proven independently three times).
3. **The volley's value is opponent-shaped**: strikers decline it in
   disciplined mirrors (tempo arithmetic), fresh doctrine sweeps
   weaker opponents with it. Its niche: multi-body answers, sealed
   front ranks, and the near-diagonal the ordinary gun cannot touch
   (aim offset 0 + bend-after >= 1 makes diagonal-adjacent bodies
   unhittable — undocumented). Cross-entrant cells are the test.
4. **Stance cost is spread across five contract fields** (entry
   windup, stance cooldown, automaticReturn threshold, exit windup,
   cooldown continuity) — a worked cost line belongs in the skills
   table. Reversible route pairs need a declared entry direction.
   Three unrelated mechanisms share the `automaticReturn*` prefix.
5. **Tooling traps with votes**: the published binary is `botarena`
   while every doc says `nilbots` (a symlink ends it); a `.wasm` spec
   silently drops the declared class AND `--swap` then swaps chassis
   rather than sides; `--print-candidate-contract` still prints
   identity, not policy values; `SpawnReservation` blocks the team's
   OTHER bodies, never the claimant (cost one author its first T4);
   scaffold `ClassOf` still prefix-parses (superseded by typed
   classId) and no scaffold readers exist for Volley/ProjectileGuard/
   AutomaticReturn.
6. **Bend-envelope inertness by cell**: striker mirrors carry
   identical rules bytes for striker-only vs universal (keel==veer,
   helm==rig) — the factorial analysis must not read those cells as
   independent evidence, and the CLI could say so when resolving.
7. Isolation process: per-entrant directories should be siblings of
   the cohort root (ledger-fly's disclosed listing); mirror controls
   of identical artifacts have N samples, not 2N (`--swap`
   reproduces the same match byte-for-byte).

The observability bump earned its keep in-line: hold fields measured
at +28.7 territory/seed on one lineage's helm cells, and the projectile
fields replaced an inference model decision-for-decision.
