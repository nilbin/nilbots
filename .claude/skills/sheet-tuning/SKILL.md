# Sheet Tuning

Tuning one unit's behavior in an Arc Relay playbook is a measurement
campaign, not an editing session. This skill encodes the method learned
the hard way in the ghost-doctrine campaign (2026-08), where five
batteries chased three wrong causes and a camping ghost survived a
23/24 bars-clean scorecard until the owner saw it stand still on
replay.

## The loop

1. **Control first.** Before crediting or blaming any change, run the
   PRE-change sheet on the CURRENT binary
   (`git show <commit>:<sheet> > playbooks/<name>-control.json` — it
   must sit in `playbooks/` so its relative `library`/`layout` paths
   resolve; delete it after). If the control does not reproduce the
   old record, the regression is in code, not config, and no amount of
   sheet tuning will find it.
2. **One battery, one change.** `scripts/arc-relay-battery.sh NAME
   SHEET OPPONENT` runs the 24-cell grid and prints record, bars,
   unresolved-engagement streaks, AND the per-unit confinement audit in
   one summary. Never edit `src/` or sheets while it runs.
3. **Audit behavior, not just outcomes.** `arc-relay-unit-audit.py` on
   any raw replay shows what each unit did: dwell, confinement,
   actions, command reasons. A unit CONFINED for 60+ ticks is a bug or
   a deliberate perch — open that replay in the viewer and decide.
   Bars-clean plus winning does NOT mean behaving: shuffle-camping
   defeats the parked bar, and a passive opponent rewards degenerate
   aggression.
4. **Screen cheap, validate expensive.** 12-cell screens (seeds
   9001-9006, both sides, ~45 s on the published binary) rank variants;
   only the winner gets the full 24-cell battery. Sweep one factor at a
   time from a fixed base; when singles plateau, knock out whole
   components — the ghost sweep found the poison (the engageWhen
   acquisition veto) only at component granularity.
5. **Attribute before iterating.** When a config family keeps scoring
   the same, the binding constraint is not the knob you are turning.
   Extract the unit's command reasons (`--verbose`) and read WHY it
   holds/moves before writing the next variant.
6. **Owner verdict on a gallery, not a number.** Win-rate against one
   fixed opponent is a weak signal — the reckless ghost beat wellwright
   BECAUSE wellwright never punished overextension. Behavior quality
   (bars, audit, legibility on replay) is the campaign metric; the
   owner's replay review is the gate. Build with
   `build-review-gallery.py`, serve behind the standing cloudflared
   tunnel, label wins AND losses.

## Known traps

- **Engagement participants**: pointing a doctrine's orders at an
  engagement whose `participants` excludes a role silently strips that
  role of any fight policy under those orders.
- **Terminal routes**: a route that does not loop (`route[0] !=
  route[^1]`) parks its unit at the last waypoint forever; close the
  loop for patrol behavior, and re-pin the layout `sha256` in every
  playbook that binds it.
- **Withdraw destinations**: a withdraw target that resolves to the
  unit's current tile is a no-op that suppresses fighting while going
  nowhere. Withdrawal must move AWAY from the threats.
- **Acquisition vetoes**: gating when a unit may PICK a fight makes it
  passive in any zone with ambient remembered enemies. Gate chases and
  disengagement instead; leave acquisition opportunistic.
- **Sheet paths**: control/variant sheets must live in `playbooks/`
  beside the real ones (relative `library`/`layout` references).
- **Determinism hygiene**: `dotnet run` rebuilds; a battery must run
  one published binary throughout. The runner script does this.

## Scripts

- `scripts/arc-relay-battery.sh` — the whole standard battery + scoring.
- `scripts/arc-relay-unit-audit.py` — per-unit behavior from raw replays.
- `scripts/arc-relay-scorecard.py --bars balance/...v7.json` — bars +
  engagement resolution per broadcast.
- `scripts/arc-relay-pocket-attribution.py` — WHY a carrier is stuck.
- `scripts/build-review-gallery.py` — owner review gallery (gate
  enforced; `--skip-arc-relay-eligibility` only for labeled
  diagnostics).
