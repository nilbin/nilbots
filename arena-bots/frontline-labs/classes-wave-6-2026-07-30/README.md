# Classes wave 6 (2026-07-30): the coordination cohort

The IQ pass the owner ordered after watching wave 5 ("bots making silly
decisions — e.g. blocking an ally's path in a choke... need the bots to
play smarter to truly tune"). Same deck game as wave 5, zero rules
changes; every author shipped a multi-body coordination layer with
per-rule measured attribution, on CLI 0.9.22. **Eight of eight T4 on
first attempts, zero friction kills, second wave running.**

| Entrant | `out/bot.wasm` sha256 | vs its wave-5 self (deck) |
| --- | --- | --- |
| vector-edge | `3ca784538f34d157de83de2003feb6c471f360627ec0e4902ed8016cfa9075e6` | mirror 39-1-0 (was 20 draws); bvs 13-7 (was 1-19) |
| still-water | `cbdba9c62ba501fe3033074e6b510ea0949a229db7ffa80096340da425d79b01` | mirror 23-20-7 +3.54 (control 0-0-50); honest SE caveat |
| arc-light | `65fec6a5915d2bdcf6e7d517e89af3095cb79e4f3e4bd201fcf08e92d75e81af` | fvs 20-11-1 (was 0-31-1); mirror 27-4-1 |
| iron-root | `6a62b5c35d27914c0729035af39958d8038b79f39998bc7c9b2c83e6b8a684d3` | 10-6-0 +4.8; corridor ticks 382→32 |
| march-wall | `fa364da95eef50bdbd7cc4d008ee20a296fbdde8b678bc16b82754081dc03d2b` | 29-1-2 +700, 20 breaches (control: 0 breaches) |
| gate-stone | `06b4ae21ae0393c220cd675933bfe5e2ff6efdeb37f45e7bba701178872a7d93` | +214 vs +113 baseline; refused-into-sibling 59→11 |
| spark-line | `fc397fc5eb6d53be41219a615a30d0a83a9f162f2ba38222ed299748fbe8e2e5` | 24-0-0 (12 disjoint seeds); mutual blocks 249→28 |
| ledger-fly | `49f452a1e53b6e3297e6bae8a8c2bb3f35dd4cafb0a775a2a1d0ea1c7b29c752` | 48-0-0 +20; self-refusals 12.4→0.8/1000 |

## Converged findings

1. **Coordination does not decompose** (every author, from every
   direction): rules measured alone lose or do nothing; composed they
   dominate. Attribution belongs to leave-one-out from the working
   whole, never build-up. Corollaries measured independently at least
   twice each: **corridors are fixed by routing, not yielding** (a
   doorway-yield makes the doorway worse); **spacing only works as a
   tiebreak** (every priced version costs wins — spread is bought with
   tempo); **predictability beats accuracy** (still-water: being right
   about a sibling's intent measured worse than over-claiming).
2. **The coordination convention is common knowledge from the frozen
   union**: every life receives the same team perception, so a pure
   function of it is a shared total order no channel needs to carry.
   The one thing that breaks it is per-life randomness — and the
   scaffold's own `OrderedDirections` consumes `context.Random`
   (two authors; one had ten sweeps invalidated by a single-decision
   divergence). Ask: a team-scoped stream (`context.TeamRandom`) or a
   deterministic helper.
3. **Wave-5 meta conclusions were conditioned on the defect this wave
   removed**: "objective presence is the scoring channel" came from a
   population that walled itself in and never breached (march-wall:
   coordination-off control has 0 breaches in 32 cells; shipped has
   20). The balance re-read after this wave supersedes wave-5's.
4. **The platform has no coordination instrument** (five authors):
   `coordinationGradeAwarded` ships in qualification schema 6 and no
   suite fills it; suite 5's union profile fields two bodies and cannot
   exercise a single rule this wave wrote; every author hand-built
   ~250 incomparable lines of replay analysis. The suite is now
   demand-justified.
5. **A refusal should name its cause** (third wave, unanimous this
   one): `movement-blocked` carries actor/from/attempted-to but not
   the blocker or reason, so the most common self-inflicted failure is
   the least diagnosable event in the schema. Companion asks: publish
   an observed ally's committed next step (the observation already
   publishes `PendingSameLifeTransition`, proving the pattern), and a
   resolved placement policy's ordered candidate tile list.
6. **Seeds are inert on this arm and every surface agrees with you
   instead of warning you** (four authors): deterministic bots +
   provenance-differing replay hashes make one game look like N.
   Measurement guidance: vary the opponent, not the seed; report
   distinct decision streams as the effective sample.
7. Fixed by 0.9.22 mid-campaign and confirmed working: lean experiment
   sweeps (700-match sessions without disk pressure). Still open, now
   six authors deep: `qualify` writes 36 viewers (~192 MB, 86% of a
   freeze) with no opt-out — every author pruned them by hand.
8. Process notes: the freeze-integrity warning (no variant `.cs` in
   the frozen tree; rebuild from the freeze as the last step) was
   honoured by all eight, prompted by spark-line's catch that the
   build globs everything. Isolation disclosures this wave: shared
   process-table (`ps`) exposure of scratch names/flags (two authors,
   benign, inherent to a shared host) and the brief-delivery file
   letting an author see all eight keys (still-water; next wave's
   briefs go fully inline per agent). One frozen-artifact drift
   observation (vector-edge): a wave-5 freeze re-measured differently
   across the 0.9.21→0.9.22 republish — under investigation in the
   post-wave batch.
