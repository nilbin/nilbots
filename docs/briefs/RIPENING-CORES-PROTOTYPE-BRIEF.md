# Ripening Cores depth prototype — brief (2026-08-05)

Owner direction (in-session): run Ripening as the next single-mechanic
depth experiment, per `DESIGN-ARC-RELAY-DEPTH-IDEAS.md` proposal #1, with
the clean-slate strategy population discipline carried over from the
Threefold campaign. One mechanic in isolation: no Migration, Conduits,
Ledger, or Threefold in this arm. Built beside `-03`; never promoted to
hosted/current. All prior fingerprints, goldens, and frozen artifacts stay
byte-identical.

## Mechanic (exact)

Enabling primitive (registered control): a Core carries a
**`chargeValue`**; banking adds that value to reactor charge; a Pulse
fires at **`chargePerPulse = 6`** (contract field `coresPerPulse`
reinterpreted as charge under the flag). Base value **2**, so the
baseline game keeps its feel: three base Cores per Pulse, nine per win.

Ripening, on top:

1. A **loose** Core's value rises **+1 per 45 uninterrupted loose ticks**
   (2 → 3 → 4), hard cap **4** (memo's over-swinginess brake).
2. **First pickup freezes the value permanently upward**: a carried Core
   never ripens. A killed carrier drops the Core at its frozen value.
3. After a drop, ripening **resumes only after 20 uninterrupted loose
   ticks**, continuing from the frozen value (still capped at 4).
4. Everything else — lifecycle, wells (bank-cleared, as in -03), combat,
   classes, map, victory, timeout ranking — unchanged. The well still
   holds its outstanding Core while it ripens: greed self-prices by
   suppressing that well's production (the memo's key balance property).
5. Two rulesets minted beside -03:
   `arc-relay-charge-value-01` (**control**: primitive only, no
   ripening) and `arc-relay-ripening-01` (primitive + ripening). The
   control must be behaviorally indistinguishable from -03 in stock
   mirrors — if it is not, the primitive is not inert and Wave A stops.

## Exposure

Core `chargeValue` is public and causal at the playhead: observation
(mind + sheet runtime), replay v3, diagnostics, viewer (a value cue on
the Core sphere; reactor charge readout accommodates 0–5). Conditional
emission everywhere: prior rulesets' bytes untouched.

## Stock competence

The stock baseline must understand values: prefer riper Cores among
reachable choices, weigh pickup-now against imminent enemy contest
(never wait passively beside a ripening Core — the felt-degeneracy bars
stay binding and unadjusted), and value-weight carrier interception.
Denial pickups (grab at 2 to stop enemy ripening) are legal and
expected.

## Pre-registered metrics (frozen before any evaluation game)

From the depth memo's registered list, measured on eligible games only:

- **First-score→win conversion** (target: below the -03 landscape's
  observed concentration; report, don't gate).
- **Behind-to-ahead Pulse reversals** (currently ~0 in every prior arm;
  any nonzero rate is the memo's key comeback signal).
- **Charge-source mix**: banked charge from base (2) vs ripened (3–4)
  value, per team per game.
- **Mean Core age at pickup** and the age distribution (the greed
  decision made visible).
- **Denial-pickup rate**: pickups of Cores whose ripening the OWN team
  did not need (value 2, own charge position not urgent) that deny the
  enemy a riper Core.
- **Participation**: the existing pre-registered contribution audit
  (`THREEFOLD-CONTRIBUTION-MEASURE.md` predicates degrade gracefully:
  every Core is required until charge is full) — reported for
  comparability, NOT a primary target of this arm.
- Frozen felt-degeneracy v4 bars binding; never adjusted. A ripening
  Core guarded by presence must not read as formation freeze — if
  guarding trips bars, the guard behavior is repaired or the entrant
  excluded and disclosed.

## Strategy population: clean slate

No prior sheets enter the population. Strategies are authored one at a
time; **strategy #1's sole goal is to beat the competent ripening stock
baseline by margin (≥7/10, 5 seeds × both orientations)**. Subsequent
entrants challenge the incumbent. The depth question this arm must
answer: does greed-vs-tempo produce genuinely different viable
strategies (a patient ripener, a tempo denier, a steal-focused
interceptor), or does one timing policy dominate? The experiment FAILS
the depth objective if take-immediately (or wait-always) dominates every
alternative across seeds and orientations.

## Report

`docs/reports/ARC-RELAY-RIPENING-PROTOTYPE.md`: exact rules, control-arm
inertness proof, surface changes, pre-registered metric results,
strategy ledger, degeneracy results, hash proof, gallery link
(outcome-visible), and an adopt/revise/reject recommendation. Post and
stop for owner review.
