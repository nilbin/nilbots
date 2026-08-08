# Skill-shot forensics — why programmed shots don't matter (2026-07-29)

Agent-produced forensics over all 810 verified replays of
`frontline-classes-wave-2-movement-factorial-v1(-holdout)`: 138,729
projectiles reconstructed launch-to-termination, 49,700 striker bolts
(17,768 bent). Game-theoretic values are solved (double oracle over the
exact engine kinematics), not estimated. Commissioned after the
outcome-blind viewing pass found bends to be the watchability driver
(`BLIND-REVIEW-MOVEMENT-FACTORIAL-2026-07-29.md`) while the owner's
earlier verdict called skill shots "essentially a non-factor".

**Headline: the mechanic works, is used near-optimally by the bots, and
has a structurally low ceiling it already sits at ~95% of.** It is an
*aiming* mechanic (off-axis access for a four-cardinal gun), not the
"hidden trajectory commitment" duel the class identity promises, and
80% of bends are geometrically invisible as curves.

## The load-bearing numbers

- The shipped class envelope is 9 programs (no initial aim offset;
  bend-after 1–4 × direction), not the wider `frontline-labs-1`
  envelope. All nine share `path[0]`, so the bend is structurally
  locked out of the launch tile.
- Hit rates: straight 48.9%, bent 57.6%. Bots pick the uniquely correct
  program 52.4% of the time against an 11.1% blind baseline — **the
  mechanic is not being misused**. Bend-attributable damage ≈ 28.5% of
  striker damage, 11.5% of corpus damage.
- The commitment is genuinely private (`RemainingTiles` does not leak
  committed path length — verified in engine source and replays).
  Privacy is not the problem.
- **Three regimes** (solved V = P(contact) under optimal play):
  d=1 undodgeable (V=1 for every program; 32.7% of all striker hits);
  d=2–3 the mixup (V(straight)=0, V(bend)=1/3 and 1/5); d≥4 dodger
  invulnerable (V=0 for all nine programs; 23.2% of shots are fired
  into this dead band).
- **The ceiling is a covering number and is envelope-invariant**: 9,
  17, and 217 programs all yield V = 0.333 / 0.200 / 0.000 at d=2/3/4,
  because a point projectile on the 8-way lattice produces exactly
  three independent escape classes. "More shot options" is provably
  worthless.
- Half the shipped envelope (k=3,4) is byte-identical to straight
  through the decisive first advance; the effective envelope is 5
  programs.
- Observed play is above equilibrium (targets are hit 46.4% at d=2 vs
  the 33.3% minimax) because bots prioritize objectives over dodging —
  **the measured value of skill shots falls as the population
  improves**. Targets currently dodge *into* bent bolts (dodging is
  net-negative against bends, −4.3%).
- Outcome leverage collapses at the top: 68% of matches are decided
  territorially at the cap; `corr(strikerDamage, win) = +0.066`; the
  46%-bend lineage (still-water) wins slightly *less* than the 25%-bend
  lineage (vector-edge), and the gap concentrates on facing-locked —
  the arm where bend gain is mechanically smallest.
- **Legibility**: 80.1% of bent shots travel ≤1 tile after the bend;
  71.7% of whole flights are ≤3 tiles / ≤2 rendered frames; only 10.4%
  form a legible "L". Mean bent-shot travel 2.94 tiles. There is no arc
  on screen to see, and no numeric lever lengthens flight without
  driving V to zero.
- Kill economics: a kill is ~3 landed hits over ~15 ticks at ~55% of
  max fire cadence; volume dominates placement. The single largest
  damage source in the game is the undodgeable point-blank contact at
  d=1.

## The squeeze (numbers-only verdict)

Every numeric lever lands in one of two absorbing regimes: levers that
make bolts harder to dodge (speed, launch tiles, facing-locked
movement, combat-first resolution, hidden telemetry, tighter maps) push
toward V(straight)=1 where bend gain is zero; levers that give the
dodger time (slower bolts) push toward V=0 everywhere. The mechanic
lives only on a two-tile knife edge whose value is pinned at 1/3.
Best-case numbers-only improvement: bend-attributable share 28.5% →
~33% (projectile speed 4), purchased by making flights even less
visible. **No combination of numbers-only tuning makes skill shots a
real factor.**

Notable solved side-results: `facing-locked` collapses the on-lane
dodge set to {stay, forward, back}, all hit by a straight bolt —
V(straight)→1, bend gain→0 — which mechanically explains DECISIONS
#157's refuted bend hypothesis and means **the balance-preferred
movement arm suppresses the bend mixup**. `tilesPerAdvance` 2→1 and
combat-before-movement each kill the mechanic outright.

## Directional pointers (mechanism design)

Attack the covering number, not the parameter list: area/fan/piercing
threats that cover multiple escape tiles per resolution (and are
visible as shapes); commitment-based movement the dodger cannot revise
mid-flight; telegraphed-but-lethal charged shots that move the
interesting decision to the defender; persistent hazards that convert
the one-shot guess into area control (the thing a territorial objective
actually rewards). Any new mechanism must also price the d=1 walk-up
contact or it will be outcompeted by it, exactly as the bend is today.

Full analysis pipeline and per-projectile digests were produced in the
session scratchpad (`agent-skillshots/`); the corpus and methods are
reproducible from the two registered factorial specs.
