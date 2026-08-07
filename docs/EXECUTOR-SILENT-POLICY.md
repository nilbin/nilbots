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
   Reasons `commit-engage` / `commit-target@N`.
5b. **Contribution test** (`CanContributeToTarget`): the strict pass asks
   "can I fire now"; the lenient pass was supposed to ask "can I
   contribute at all" but answered with `CanAimAtPosition`, which is
   ray-exact. Prey two tiles away and off the eight rays read as
   unreachable, allocation refused it, and the body never got
   close-on-focus, duel-stand or the flush machinery - it walked past.
   FIXED 2026-08-07: the lenient pass now also accepts "a walkable path
   exists", which is what contributing means; the leash still bounds
   how far. Reason `unreachable@N`.
6. **maximumAttackersPerTarget / overkill accounting**: a body denied
   its target because two allies claimed it first - silent.
7. **Participants trap**: an order pointing at an engagement whose
   `participants` excludes the role = no fight policy at all
   (documented in the tuning skill; still the quietest failure here).

### Class A now speaks (2026-08-07)

The whole class above was silent by construction: every one of these
filters drops a target by RETURNING FALSE, so the body's next command
read as ordinary movement and no trace could tell you which gate shut.
`AllocateFocus` now records, per body per tick, WHICH filter dropped its
best candidate, and publishes it on the mind's debug surface as
`declines=<unit>:<reason>,...`. The vocabulary:

| reason | meaning |
|---|---|
| `busy` | carrier or repairer — excluded from allocation entirely |
| `no-scope` | no engagement lists this body's role as a participant |
| `no-target` / `none-visible` | scope has no candidates; the team sees nothing |
| `hold-fire` | `engage.within` filtered every enemy out |
| `returning` | returning-to-formation strip (#2 above) |
| `commit-engage` | `CommitAllowsEngaging` closed the threat gate |
| `cap@N` | `maximumAttackersPerTarget` already met, prey N tiles off |
| `overkill@N` | committed damage already lethal |
| `leash@N` | outside the engagement leash, measured from the POST |
| `commit-target@N` | `CommitAllowsTarget` (catchable / execute-health) |
| `unreachable@N` | `CanContributeToTarget` said it cannot contribute |
| `other@N` | every predicate passed — allocation ran out of capacity |

`scripts/arc-relay-fight-trace.py` prints the reason beside each
ignored-enemy episode, so "why did this unit walk past that fight" is
now one command. It found its first bug immediately: `unreachable@1`
and `unreachable@2` were the modal decline on the committed sheet,
because the LENIENT contribution pass still demanded an exact firing
ray. See the entry below.

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
4. **Idle-break family** — **REMOVED 2026-08-07 by owner ruling.** He
   asked "is it even a good idea" and called it: the no-idle invariant
   was the last big silent mover, and the sheets now own standing
   intent themselves (modes with `while`/`until`, `patienceTicks`,
   the `recover` verb, `duel-stand`). Gone: the `streak` displacement,
   the wedge-shake pacing, and all idle-watch bookkeeping. Standing
   still is sheet policy again — **camping can return, and that is the
   accepted trade**; the `unitParked` bar is now the only thing that
   flags it, so watch that bar rather than trusting the executor to
   prevent it.

   What REMAINS is one narrow check, `lane-relief` (was the supply-lane
   2-tick patience + `PlugsCarrierRoute` backstop). It is not about
   idling but about BLOCKING others: it fires only when a body stands
   beside an own loaded carrier ON an admissible homeward step while
   every such step is taken, and after two ticks of that it displaces
   away from the own reactor. That was a measured fix for real harm
   (the ab51 pocket family, owner-confirmed) and survives the ruling.

   Consequence: `patienceTicks` and `stance`'s idle-limit reading are
   now **accepted but inert** — nothing displaces a patient body, so
   the numbers describe nothing. Both stay in the grammar so frozen
   clean-slate sheets keep compiling byte-identically. `stance` still
   drives concealment micro and flank-approach suppression.
5. **Heal behavior** (`heal-detour`, `heal-channel`): a wounded body
   vector-dashes to heal tiles regardless of orders - the "random
   jump" legibility item.
6. **Opportunistic signatures** (`signature-heading`, `signature-idle`).
7. **Formation micro** (`formation-move`, `turn for ...`, reflow).
8. **The v3 desugar constants**: shrinking. `targetPriorities` is now
   authorable as `fight.targets.prefer` (carrier / weakest / closest /
   strongest-threat / freshest, order = priority, default
   `[carrier, weakest]` so unauthored sheets stay byte-stable), and two
   rankings that used to be executor law are sheet policy:
   `fight.collect` (yield | first - does a loose ball outrank picking a
   new fight) and `fight.heal` (yield | first - does an ARMED recover).
   `fight.breakOff.health` trips the existing timed break-off latch off
   a health threshold, reusing the sticky rally and unconditional
   expiry rather than inventing a second disengager. Still invisible:
   `lockTicks 4, lockPreemption urgent-carrier,
   maximumAttackersPerTarget 2, tieBreakers, release, selfDefense`.
   Two executor-owned floors are deliberate and outrank every knob: hp
   <= 1 arms recover regardless of caution, and a body that broke off
   to heal takes no new fight until whole.

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
