# Frontline duel-depth v1 result

Status: retained as a one-pass usability/DX screen, not a balance screen. The
evidence rejects the submitted arm-policy combination, but bot quality is not
controlled strongly enough to select a production shot envelope.

## Frozen screen

- Ruleset: `frontline-labs-1-experiment-one-bend-shots`
- Rules fingerprint:
  `254baa015336bbaa80e1005cb45d6a7a4e245705c147b5bc242ce86b34ecd3fd`
- Engine/source freeze: `5431c5c94851344b9d27dbc912e1420d35479378`
- Runtime: four retained canonical WASM artifacts
- Schedule: 24 verified matches; every pairing mirrored; seed `104729` for
  every pairing and two extra seeds only for pairings containing the
  randomized `AdaptiveMixer`
- Runtime faults and disqualifications: zero

The opening is the real match from tick 0 through 119. Companion 1 unlocks at
tick 120 and companion 2 at tick 260.

## Mechanical result

- Every match contained opening damage, but that does not validate private
  trajectory play.
- The opening contained 354 curved launches and zero curved hits.
- Across complete games, straight shots dealt 343 damage from 1,402 launches;
  curved shots dealt 24 damage from 1,901 launches.
- `AdaptiveMixer`, the only entrant that actually submitted curved opening
  programs, lost all 18 appearances.
- In each six-match pairing against `AdaptiveMixer`, the opponent won 6-0,
  averaged `+15` territorial progress at the first-unlock boundary, and
  `AdaptiveMixer` averaged `-15`.
- `AdaptiveMixer` averaged 1.67 opening damage per appearance. Each straight
  opponent averaged 3.83 in the same pairings.
- Defenders moved on every opening turn where one of `AdaptiveMixer`'s curved
  projectiles was visible and movement was legal. None of those responses
  happened while the defender occupied the active objective, so the forced
  movement produced no measured territorial concession.
- `CurvePredictor` submitted no curved shots. Its prediction rule consistently
  found straight fire preferable on the observed paths.

The pair-normalized values matter because `AdaptiveMixer` had 18 appearances
while every other entrant had 10. Raw cohort totals must not be read as
equal-exposure comparisons.

## Match-shape result

- No match ended during the 1v1 opening.
- Three matches ended after the first companion unlock; 21 reached the phase
  after the second unlock.
- Four matches ended by base breach. Twenty reached the 500-tick limit.
- The evaluator marked 20 matches as containing a repeated recent tactical
  frame sequence and two as stalled. This is a diagnostic flag, not a
  watchability verdict by itself.
- Nineteen fabrications completed across the cohort. Many ready companion
  slots remained unused, so this screen also does not demonstrate the desired
  consistently escalating team tempo.

## Outcome-blind replay review

The reviewer opened two preselected replays without standings, aggregate
results, or entrant source:

- `AdaptiveMixer` versus `GeometricDodger` had a readable 15-tick opening,
  then remained strategically static from tick 44 through 499. Of 162 shots,
  156 ended against wall/path geometry. It is not a viable full replay.
- `CurvePredictor` versus `GeometricDodger` repeated the same straight-fire
  mutual-destruction cycle four times through tick 117. Curved prediction only
  became legible after companion play: ticks 161–168 and especially tick 218
  showed a visible bend catch a geometric movement response.

The bend is visually readable once it manifests, but its private commitment
is not persistently communicated at launch. Normal-speed debug text changes
too quickly to carry that explanation. The review therefore fails the native
1v1 watchability gate while preserving qualified evidence that later
multi-body curve interceptions can be understandable.

## Why this arm failed

The evidence rejects the arm, not the general idea of private programmed
shots.

The bots' first common firing state was aligned horizontally with six tiles
between them, inside the central approach choke. A four-tile bend enters the
wall cluster above or below the corridor and terminates before visibly
turning. A five-tile bend would preserve the private path for one more
decision, but its diagonal is still consumed by the same wall/strict-corner
geometry. Straight suppression is the rational shot family in that state.

`AdaptiveMixer` compounded that mismatch by firing a high volume of random or
weakly adapted bends whenever an enemy was forward, rather than reserving a
curve for an open prediction chamber with a demonstrated intercept path.
Conversely, the nominal
`CurvePredictor` correctly chose straight paths so often that it provided no
independent curve-capable comparison.

This means the cohort is strong enough to reject the submitted policies as
balance references and to reject promoting the exact v1 arm from this
evidence. It is not strong enough to reject the one-bend mechanic, choose a
numeric envelope, or conclude that the existing richer curve language is
strategically empty.

## Bot-quality limitation

Every entrant received one authoring pass and no result-informed improvement
pass. That constraint is valuable for testing the player-facing docs and the
T1/T2 authoring floor, but it leaves the strategic ceiling unknown:

- three nominally different entrants converged on effectively identical
  straight-fire opening behavior;
- `CurvePredictor` did not demonstrate its declared curve mechanic in the
  opening;
- `AdaptiveMixer` demonstrated curves, but not competent curve selection;
- companion activation and long-loop avoidance were inconsistent.

The 0-18 record is therefore evidence that `AdaptiveMixer` is not a
balance-grade curve reference. It is not evidence that a T5+ curved-shot
policy would lose 0-18.

## Post-screen positional qualification

The later `frontline-qualification-1` runner starts the Primes directly at
the `(8,7)`/`(14,7)` suppression approach, mirrors the artifact across both
teams, ends before companion unlock, and applies deterministic straight
pressure from a framework controller using only the public SDK. It records
first-life objective entry but intentionally awards no cumulative tier.

All four retained canonical WASM artifacts failed both assignments:

| Artifact | Team-0 first-life entry | Team-1 first-life entry | Sentinel attacks/assignment |
| --- | --- | --- | ---: |
| `AdaptiveMixer` | never | never | 16 / 18 |
| `CurvePredictor` | never | never | 19 / 19 |
| `GeometricDodger` | never | never | 19 / 19 |
| `TerritoryHolder` | never | never | 19 / 19 |

This is the missing quality control: none is a T4 positional reference. Their
full-match trajectory and win table cannot judge whether a qualified policy
can turn the one-bend chamber into useful play.

## Initiative reference and map screen

`InitiativePlanner` is a retained source-complete reference artifact
(`eee65c8fbbab2a35c2755a4e27ade341d0a12216623d7477778df7241cc5a0c4`).
It spends movement when the active objective is within two steps, before
straight suppression reaches its final public state. Under the stricter
mirrored entry component, both assignments entered at tick 2 with zero damage
and reached capture progress 14. This passes that component only; it awards no
cumulative tier.

The exact same WASM then played itself for seeds `104729`, `130363`, and
`155921` on the three content-identified map arms:

| Map arm | Outcomes | Active ticks | Damage / 100t | Destructions | Fabrications | Stalled / looped | Longest no interaction |
| --- | --- | ---: | ---: | ---: | ---: | --- | ---: |
| current | 3 draws | 20.3% | 1.5 | 6 | 0 | 3 / 3 | 415t |
| outer-shoulder bypass | 3 draws | 20.1% | 1.5 | 6 | 0 | 3 / 3 | 415t |
| thin fronts | 2 draws, 1 team-1 edge | 88.8% | 16.7 | 80 | 21 | 0 / 1 | 164t |

All nine matches reached tick 499. This is a diagnostic screen by one
partially qualified policy, not balance-grade population evidence.

Three findings survive that limitation:

1. Objective shape is a strong activity lever for this policy. Thin fronts
   convert equal occupation into movement, combat, deaths, and companion use.
2. The outer-shoulder bypass is strategically invisible to a shortest-path
   policy. It needs a map-holdout reference that deliberately values route
   timing before it can receive a verdict.
3. On current and bypass maps, unlocked companion slots were Ready for 55.4%
   of eligible observations, but no fabrication occurred. The match therefore
   never developed its intended mid/late population. This supports testing the
   already-derived automatic activation policy in the next coherent arm.

Thin fronts are not promoted. Three-tile capacity may over-amplify blocking,
one seed produced a team-1 territorial edge, and none of the games reached a
terminal objective win.

## Decision

Do not promote this ruleset and do not tune Frontline around its win table.
Retain all four bots and the canonical artifacts as negative evidence and a
fast regression cohort.

The exact engine-path analysis supersedes the initial “increase four to five”
idea. In the first-contact central choke, wall and strict-corner geometry
consume both four- and five-tile bends. Changing the numeric limit alone
cannot fix that state.

The current map does contain a theoretically sound one-bend state in its
central objective chamber. At a four-tile engagement, bend-after-3 preserves
an identical public path until the defender's last meaningful decision:
straight hits hold, left hits north, and right hits south. No shot is
unavoidable and no last-moment response is universally safe. Hold and south
retain objective control, north concedes it, and movement gives up firing
tempo.

Before numeric tuning, qualify T4 positional and T5 predictive duel
specialists that can distinguish suppression corridors from open prediction
chambers. Give them a small equal improvement budget, retain every revision,
and freeze holdout seeds before final evaluation. Then run the same qualified
artifacts under both rules arms from both participant assignments. A separate
one-pass T1/T2 cohort remains the accessibility check.
