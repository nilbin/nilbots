# Spec: Fight-plane consolidation (ghost doctrine v3)

Owner-approved shape 2026-08-07 (this document is the contract; the
conversation that produced it is summarized in DECISIONS #216). Status:
EXECUTED 2026-08-08 (owner go; DECISIONS #217/#218). The grammar below
shipped exactly; the target artifact's illustrative VALUES were then
measured one battery at a time and mostly refuted - see "Measured
migration" at the end for the frozen result.

## Why

The job plane (doctrines v1: modes, tasks, while/until, claims,
custody) was consolidated long ago and works. The fight plane never
was: `holdFire`, `isolation`, `stance`, lock arithmetic, and the new
commit block all answer "when do I shoot" at once, with invisible
couplings (removing `stance: ambush` alone collapsed ab73 to 7-17
because it feeds the concealment machinery). `chaseLeash` exists in
three places. `dodgeCoverage` and `posture` are accepted-but-inert
noise. Nobody can predict a one-line change.

## The target artifact

```json
"ghost": {
  "role": "hunter",
  "custody": "well-custody",

  "fight": {
    "targets":  { "lone": 4 },
    "engage":   { "within": 4, "killableTicks": 12,
                  "from": "behind", "positionTicks": 16, "else": "strike" },
    "chase":    { "leash": 6, "onlyCatchable": true, "executeBelowHealth": 1 },
    "breakOff": { "threats": 3, "within": 5, "memoryTicks": 16, "recoverTicks": 24 }
  },

  "modes": [
    { "assault": "strike-line",
      "while": "shadow-op", "until": "shadow-cooled",
      "escort": "medic",
      "fight": { "engage": { "within": 6 }, "chase": { "leash": 14 } } },

    { "intercept": "enemy-carriers", "from": "perch-north",
      "while": "carrier-known or enemy-remembered-deep",
      "until": "carriers-none and backfield-cold",
      "patienceTicks": 12 },

    { "patrol": "shadow-north-long" }
  ]
}
```

## Semantics

**Modes** are an ordered priority list, re-evaluated continuously,
first match wins; the unconditioned patrol at the bottom is the floor.

- `patrol: <route>` — walk the looping route forever; brief built-in
  dwell + scan sweep at waypoints; no patience knob (patrolling IS
  motion). `patrol: "traffic"` selects the computed corridor-shadowing
  waypoints (enemy reactor↔well paths) instead of an authored route.
- `intercept: <object>` (`enemy-carriers`, `inbound`) — predict the
  target's path and move to the CUT-OFF point, wherever that is; the
  `from` perch is only where it lurks when scent exists but no cutoff
  is computable. `patienceTicks` = how long a perch may hold; this is
  the one mode where waiting is the point.
- `assault: <route>` — the committed push: drive the attack route and
  take its fights while the window (`while`/`until`) holds, with the
  mode's `fight` override (typically looser).

**Fight** answers four questions, each exactly once. A mode-level
`fight` object overrides individual keys.

- `targets { lone: N }` — only prey with no ally within N (absorbs
  `isolation.supportRange`).
- `engage { within, killableTicks, from, positionTicks, else }` —
  acquisition: distance gate (absorbs `holdFire.withinDistance`),
  time-to-kill gate (ceil(health/damage) x cadence), and approach
  discipline: `from: "behind"` suppresses front declares and maneuvers
  to the nearest rear-quadrant firing tile (machinery exists:
  `NearestRearFiringTile`), budgeted by `positionTicks`, expiring to
  `else: "strike" | "breakOff"`.
- `chase { leash, onlyCatchable, executeBelowHealth }` — pursuit only;
  anything inside gun range is killable regardless (range-based, never
  aim-based — the orbit bug). Catchable = standing between prey and
  its bank (uniform movement speed).
- `breakOff { threats, within, memoryTicks, recoverTicks }` — the
  threat picture (visible + remembered <= memoryTicks within radius)
  trips a TIMED break (sticky rally, fixed recovery window, expires
  unconditionally — never a while-threats retirement).

**Added 2026-08-07 (owner: combat is sacred, so everything else is
ranked RELATIVE to it, inside this block):**

- `targets { prefer: [...] }` — target priority, order = priority.
  Vocabulary `carrier | weakest | closest | strongest-threat |
  freshest`, mapped onto the executor's existing comparators. Default
  `[carrier, weakest]`.
- `collect { "yield" | "first" }` — does a visible loose Core outrank
  picking a NEW fight. `yield` (default): a body in contact fights or
  dodges and the ball keeps. `first`: break off the uncommitted part of
  a fight and take it — never a strike windup, a cone dodge, or a
  survival layer.
- `heal { "yield" | "first" }` — the same question for an ARMED
  recover. Recover's arming predicate is untouched; this only ranks it
  against fighting. hp <= 1 outranks the knob, and a body that broke
  off to heal takes no new fight until whole.
- `breakOff { health: N }` — trips the EXISTING timed latch off a
  health threshold; composes with `threats` (either trigger, one
  latch), inheriting its sticky rally and unconditional expiry.

**Deleted / not knobs:**

- `whileCarrying` — carriers cannot fight; the engine decides this.
  Drop-to-fight (dropping on a non-current tile) is ruled OUT: the
  carrier's defenselessness is load-bearing design, and drop-juggling
  is execution micro. `arc-toss` to a catcher is the honest fighting-
  courier pattern if ever wanted.
- Backstab is AUTOMATIC engine physics: rear-quadrant strikes do
  double damage and declarations are invisible to victims whose team
  cannot see the shooter's tile. No sheet field enables it.
- `dodgeCoverage`, `posture` — deleted from the schema everywhere.
- `lockTicks`/`lockPreemption`/`aimPreparation`/`tieBreakers`/
  `coordinationScope` — deleted for the ghost (the ENGINE locks
  strikes now); squad engagements keep a trimmed set (see scope).

**Conditions** become readable boolean strings
(`"carrier-known or enemy-remembered-deep"`) compiled against the
existing condition-set primitives; a typo is a compile error, never a
silently-false gate.

## Mapping table (old -> new)

| today | after |
|---|---|
| `stance: "ambush"` | `patienceTicks` (intercept) + `engage.within`; concealment/backstab automatic |
| `holdFire.withinDistance` | `engage.within` |
| `isolation.supportRange` | `targets.lone` |
| `commit.*` | `fight.*` (renamed, flattened) |
| `chaseLeash` (x3 homes) | `fight.chase.leash` |
| `formationId` on ghost modes | gone; `escort: <role>`; solo default |
| lock/aim/tie-break arithmetic | deleted for ghost; trimmed for squads |
| `dodgeCoverage`, `posture` | deleted everywhere |

## Agreed guardrails

1. Translation FIRST: ab72's exact behavior expressed in the new
   grammar, verified by a parity battery. Parity will NOT be perfectly
   clean (stance is a reinterpretation); expect 2-3 batteries with
   per-diff adjudication (load-bearing vs incidental).
2. Scope: ghost's plane only. Squad engagements (well-fight,
   disrupt-fight) keep trimmed old-style records; second pass later.
   One sheet, two dialects, temporarily.
3. New compiler surface (condition strings, verb sugar) desugars to
   the proven machinery; validation as strict as today.
4. Method per `.claude/skills/sheet-tuning/SKILL.md`: one change per
   battery, audit gates (territory/pendulum/lone-ledger/confinement),
   owner gallery as the final gate.

## State at freeze

- hunter-v1 = ab72 config: circuit patrol + scan, commit(chase +
  timed breakOff, no engage gate), stalk keeps ambush stance
  (load-bearing: ab73 measured 7-17 without it).
- ab72: hunter 14-10, 23/24 bars-clean, zero unresolved engagements,
  ghost audit green (enemy-share 0.62-0.67, confinement <= 48t,
  lone conversion 3/7-6/11).
- Known illegibility (viewer pass candidate, not behavior): strike
  duels read as standing between cadence flashes; heal-detour dashes
  read as random jumps; deaths/respawn walks read as confusion.
- Open exhibits: well-scrum staticness on BOTH teams (~10% lone-kill
  conversion, 80-100 confined units per battery) — next campaign
  after this one.

## Measured migration (2026-08-08, post-execution)

The grammar shipped as specified (commit ee5e2970). Parity: ab74
12-12, 23/24 bars-clean, zero unresolved streaks, 7/24 cells
STATE-IDENTICAL to ab72 whole-match; every diff traced to the one-leash
reinterpretation; the two CONFINED ghost cells are the strike-line
assault pocket ab72 already exhibits (its own w-9012 is
state-identical at 117t). Then each target value ran as its own
battery against the translation:

| value | battery | verdict |
|---|---|---|
| `patienceTicks: 12` (intercept) | ab75 11-13, bars 21/24, hunter-side trips | REVERTED — worse on every gate |
| `breakOff.threats: 3` | ab76 9-15, latch pacifism (ab70 family) | REVERTED — 4 stays |
| assault `{within: 6, leash: 14}` inherit | ab77 12-12 but a 233-tick base-camp pocket | REVERTED — gate-free assault is load-bearing |
| `engage.from: "behind"` (16, strike) | ab78 7-17, 2 shots/match where 22 fired | REVERTED — per-target hold windows pacify vs live prey; machinery stays, needs per-engagement budget or unaware-prey gating |
| `engage.killableTicks: 12` | ab79 24/24 state-identical to ab74 | REVERTED — proven no-op at kestrel damage |

FROZEN ghost (hunter-v1, the ab72 translation):

```json
"ghost": {
  "role": "hunter",
  "custody": "well-custody",
  "fight": {
    "targets":  { "lone": 4 },
    "engage":   { "within": 4 },
    "chase":    { "leash": 6, "onlyCatchable": true, "executeBelowHealth": 1 },
    "breakOff": { "threats": 4, "within": 5, "memoryTicks": 16, "recoverTicks": 24 }
  },
  "modes": [
    { "assault": "strike-line", "while": "shadow-op", "until": "shadow-cooled",
      "escort": "medic",
      "fight": { "targets": { "lone": 0 }, "engage": { "within": 0 },
                 "chase": { "leash": 14, "onlyCatchable": false,
                            "executeBelowHealth": 0 },
                 "breakOff": { "threats": 0 } } },
    { "intercept": "enemy-carriers", "from": "perch-north",
      "while": "carrier-known or enemy-remembered-deep",
      "until": "carriers-none and backfield-cold",
      "fight": { "chase": { "leash": 14 } } },
    { "patrol": "shadow-north-long" }
  ]
}
```

Owner gallery: "Ghost doctrine v3 — one grammar, same beast" (ab74
cells, five wins + two honest losses) on the standing tunnel. Noted
for later, untouched to protect parity: the isolation rear-exposure
override in AllocateFocus reads inverted vs its comment (rank==1
keeps NOT-rear-exposed targets); the strike-line assault pocket is
the assault verb's character and predates v3.
