# Executor policy the sheets cannot see (silent-underminer audit)

Owner call 2026-08-08, after three consecutive replay catches traced to
executor behavior no sheet authors: "do we need a look on other
potential stuff that'll silently undermine the sheets like this?" This
is that look. Ground truth: the complete reason-string vocabulary
extracted from real replays plus a read of every allocation-side path
that acts WITHOUT a reason. First tool: `scripts/arc-relay-fight-trace.py`
answers "why did this unit stop fighting" in one command.

## Class A - acts by ABSENCE (no reason string; the hard class)

These never appear in a trace; the unit's next command just reads like
ordinary movement. When a sheet "mysteriously" underperforms, look here
first.

1. **Focus lock release** (`ReleasePolicy`): a lock silently drops when
   the target is hidden `hiddenTicks` (2 in every v3-generated
   engagement), unreachable 3, outside the leash, or destroyed. In a
   wall warren, hidden-2 ends most chases - the owner's "the enemy ran
   off" scene. NOT sheet-reachable from the v3 fight block.
2. **Returning-to-formation exclusion**: after a self-defense excursion
   (`returnToFormation: true`, hard-coded in v3-generated engagements)
   the body is silently stripped from focus allocation until it is home,
   unless the primary target is a carrier.
3. **HoldFire gate** (`engage.within`): enemies beyond the gate are
   filtered before allocation. Sheet-visible in v3 - but its EFFECT
   (unit watching an enemy, not fighting) has no trace signature.
4. **Isolation filter** (`targets.lone`): same silence - and it carries
   the suspected inversion (DECISIONS #217): the rear-exposure override
   keeps `RearExposedRank == 1` (NOT rear-exposed) targets, the
   opposite of its comment. Baked into every measured battery; fix is
   its own measured change.
5. **Commit gates** (`CommitAllowsEngaging` / `CommitAllowsTarget`):
   a gated body silently skips allocation. The v2 bisect took five
   batteries to find this; the trace tool now flags gated-idle ticks.
6. **maximumAttackersPerTarget / overkill accounting**: a body denied
   its target because two allies claimed it first - silent.
7. **Participants trap**: an order pointing at an engagement whose
   `participants` excludes the role = no fight policy at all
   (documented in the tuning skill; still the quietest failure here).

## Class B - speaks, but the sheet cannot author it

Visible in traces (reason string in parentheses), policy hard-coded:

1. **Between-shots posture** (`duel-stand`, `close-on-focus`): fixed
   2026-08-08 after the owner caught cooldown-tick formation drift
   reading as disengagement. Still executor-owned, not a knob.
2. **Strike-cone evacuation** (`strike-evacuation`) and
   **self-preservation**: threat distance comes from the engagement's
   `selfDefense` block - which the v3 desugar hard-codes to
   `{enabled, threatDistance 2, returnToFormation true}`.
3. **Ambush concealment** (`ambush-conceal`) and **flank approach**
   (`flank-approach`): stance-driven; the v3 desugar emits
   `stance: "ambush"` for every doctrine order unconditionally.
4. **Idle-break family** (`streak`, `wedge-shake`, supply-lane 2-tick
   patience, the carrier-plug test): the no-idle invariant. Patience is
   now partially authorable (`patienceTicks`); the rest is invariant by
   design (camping protection) and should stay.
5. **Heal behavior** (`heal-detour`, `heal-channel`): a wounded body
   vector-dashes to heal tiles regardless of orders - the "random
   jump" legibility item.
6. **Opportunistic signatures** (`signature-heading`, `signature-idle`).
7. **Formation micro** (`formation-move`, `turn for ...`, reflow).
8. **The v3 desugar constants**: every generated engagement fixes
   `lockTicks 4, lockPreemption urgent-carrier,
   maximumAttackersPerTarget 2, targetPriorities
   [enemy-carrier, lowest-health], tieBreakers, release {2,3,true,true},
   selfDefense {true,2,true}` - chosen for ab72 parity, INVISIBLE in
   the sheet. These are the fight plane's remaining dark matter.

## What to do about it

- **Troubleshooting**: `arc-relay-fight-trace.py REPLAY --team N
  [--unit U]` prints the unit's compressed reason timeline plus every
  fight interruption (fighting -> not-fighting with an enemy still
  near), labeling each with its mechanism where one speaks and
  "SILENT (lock release / allocation filter)" where none does. This
  turns the owner's replay catches from an afternoon of bisects into
  one command.
- **Exposure pass (owner design decision, queued)**: the Class-A
  policies that keep mattering earn fight-block homes - leading
  candidates `chase.persistTicks` (hidden-release), and surfacing
  `release`/`selfDefense` under the doctrine grammar. Same
  parity-first method as #216: expose with current values, one change
  per battery after.
- **The isolation inversion** is a measured-change candidate, not a
  drive-by fix.
