# Classes wave 7 (2026-07-30): the striker salvo-integration round

The striker-only re-authoring the owner ordered after the swell read
("the current striker bots were made when it was shit so they barely use
it — do another training round on the striker bots only with the new
updated information"). Three authors, one fan-integration doctrine pass
each on CLI 0.9.25 / SDK 0.10.8 (`swell`, DECISIONS #182/#183), frozen
wave-6 bulwarks and fabricators as opponents. **Three of three T4 on
first attempts, zero friction kills.**

| Entrant | `out/bot.wasm` sha256 | vs its wave-6 self |
| --- | --- | --- |
| vector-edge | `c5cb1f102558c04f7351148e9f62ce83e7521a2e69b5373207cf7000d9d9349b` | mirror 8-0-0 +38.0 (was 0-0-8); cross legs 50-14-0 +16.77 (was 22-34-8 −2.41) |
| still-water | `5f32c2cc40ae72a984f2224e3659fb0341246b91b746b80452e497effa8f816d` | fabricator leg −21.15 → +14.00 (spark-line 0-20 → 20-0); mirror a wash, disclosed |
| arc-light | `7b586b428070388345b322c9f0d5c5b48eec6b88df897ce94013e0144cd5f23e` | mirror 32-0-0 +28.44 (was 0-0-32); cross legs 47-65-0 (was 12-100-0) |

## The balance read (this wave's purpose)

Coarse read, wave-7 strikers vs the frozen wave-6 field, seeds
930011/960017/990037 — directly comparable to the tide/surf/swell reads:

| pair | tide (#180) | swell, stale doctrine (#183) | **wave-7 doctrine** |
| --- | --- | --- | --- |
| bulwark-vs-striker | +1.000 | +0.852 | **+0.333** |
| fabricator-vs-striker | +1.000 | +0.778 | **−0.222** |
| bulwark-vs-fabricator | +0.333 | +0.333 | +0.333 (no striker; unmoved) |

**Every class pair is inside the cycle-magnitude band for the first
time in the campaign.** Fan usage in the read: 368 volley entries
across all 15 pairings (the swell stale-doctrine read had 45).
Disclosed: 14/27 and 7/18 distinct outcome triples — deterministic
bots; read cells, not matches. Caveat that keeps it honest: freshness
is asymmetric (fresh strikers vs stale wave-6 doctrine); wave 8
re-prices the triangle with every lineage adapted.

## Converged findings

1. **The wave-6 strikers were right, not shy** (all three authors,
   independently): every predecessor's cast-refusal logic was CORRECT
   arithmetic on the old arm (arc-light counted 157 correct vetoes per
   match; vector-edge's two-bodies gate rested on "fan bolt = gun bolt";
   still-water's bar quoted the old prices). The pass that worked was
   re-reading the contract's declared numbers — and the two attempts at
   plain aggression both measured WORSE (still-water's 610-entry whole:
   −33; vector-edge's damage multiplier: −16 wins). Corollary recorded
   by vector-edge as an authoring hazard: an arm that re-arms one weapon
   silently re-prices every `max(damagePerHit)`-derived rule in an
   existing bot — arc-light's V5 and vector-edge's R5 were both latent
   safety rules the salvo flipped from inert to binding.
2. **`self.routeCooldowns` gates nothing but schedules something.** Two
   of three measured the clock read at exactly zero for request-gating —
   the legality mask already enforces the clock. arc-light's use of it
   as a CHARGE (schedule the approach so the route opens as the angle
   arrives) is the one live consumption. Friction consensus: the #181
   doc phrasing implies a request cost that mask-driven bots never pay.
   Unresolved inconsistency worth an engineering look: still-water saw a
   held route's `form-target` constraint still listing the stance in
   `allowedFormIds`; vector-edge measured the same constraint EMPTY
   while held (155/155 held ticks). Same contract fact, two shapes.
3. **The fan is an execution tool, not a better bolt** (all three): the
   damage-2 fact enters at kill thresholds — anything at ≤2 health dies
   to one bolt, which includes a fabricator prime at full health — and
   scaling positional scores by damage is a category error (measured
   −16 wins). The 1-tick entry plus decoupled gun makes "fan 2 + gun 1"
   the striker's two-hit collapse.
4. **The bulwark line holds for a reason the fan cannot touch**:
   iron-root beat all three revisions (its leg is the whole remaining
   bvs payoff). Two authors localized it to posture — the striker
   doctrine out-trades but under-holds. That is wave-8 material (or the
   class-numbers lever), not a fan tune.
5. **Platform friction, recurring asks** (full lists in each DX):
   qualify's ~5MB viewer.html per probe despite viewers being opt-in
   everywhere else (three waves running); `--print-candidate-contract`
   prints identity, not the contract, so authors mine replay headers for
   declared numbers; `movement-blocked` still names no blocker/reason;
   `botarena.json`'s `sdkVersion` is unvalidated at build; a replay
   hash is provenance-sensitive and thus not a behavioural identity
   check (rename changes all hashes).
