# Experimental Frontline contract

Status: **implemented internal experiment; not a shipped ruleset**, 2026-07-27.
Official rules 0.5, replay v1, runtime protocol 0.1, and the current ladder are
unchanged. Frontline is not yet selectable through the shipped SDK, CLI, App,
or server match path.

This document is the concise player and bot-author contract for the frozen
Frontline engine arm. The complete machine-readable truth is the
`PublicMatchContractManifest` embedded in replay v2. Numeric defaults below
are experiment inputs, not a balance or ship verdict.

## The game

Two submitted policies contest one moving objective across five ordered
positions. It starts in the centre. Sole presence by one team builds capture
progress; empty or contested control decays existing progress. Completing a
capture advances the active position toward the opponent. Advancing through
the last position breaches the base and wins immediately.

An early breach is valid. Fabrication and turrets are escalation for games
that remain close, not mandatory acts every replay must reach.

The current starting envelope is:

- two teams and one submitted artifact per team;
- one active Prime per team at match start;
- up to three stable unit slots per team;
- binary objective presence, so stacking bodies never accelerates capture;
- Prime respawn after 18 complete absent decision ticks;
- child rebuild after 30 complete absent decision ticks;
- child-slot unlocks at ticks 120 and 260;
- maximum 500 executed ticks.

At timeout, the signed territorial score is the active position's displacement
from centre times the capture threshold, plus current capture progress signed
for the claiming team's advance direction. Health and damage are recorded but
do not break a territorial tie. A base breach on the final allowed tick wins
before timeout resolution.

## Teams, units, and lives

A scoring team, submitted participant, stable unit slot, and runtime life are
different identities:

```text
teamId        scoring side
participantId submitted artifact/policy
unitId        stable team-local Prime or child slot
lifeId        one runtime incarnation of that slot
```

The same submitted artifact is instantiated independently for every active
life. Each life has separate private memory, a deterministic random seed, and
its own runtime invocation and per-tick diagnostic budget. The participant's
match-wide diagnostic cap is shared across its lives, and a host/runtime fault
fails the experimental match rather than becoming gameplay. A form change
keeps the same life and runtime memory. Destruction disposes that runtime;
Prime respawn or child (re)fabrication creates a fresh life with fresh private
memory. A child becoming Ready after its rebuild timer does not create a life.

Collections never use array position as identity. Team, participant, unit, and
life counts are explicit public inputs. Allies, enemies, projectiles, forms,
actions, and objectives are variable collections with presence and legality
masks, so neither scripted bots nor neural policies are structurally fixed to
the current three-slot default.

## Fabrication

Only the Prime may submit `fabricate`, and only while standing on its own
protected spawn pad. The action targets one own child slot whose lifecycle is
`Ready`.

Successful fabrication reserves the first free non-Prime pad tile in canonical
Y-then-X order and creates the child at the next tick start, facing the
authored home direction. Pad capacity is evaluated after movement. A full pad
therefore permits a valid attempt but resolves it as `Blocked`; an ally that
vacates a tile on the same tick can make the attempt succeed.

The authored Prime spawn is permanently reserved against own child movement.
Enemy ground units cannot enter an opposing protected pad, but the pad grants
no health or projectile immunity.

A destroyed child enters `Rebuilding`, later becomes `Ready`, and must be
fabricated explicitly again. The new life always starts in the slot's default
`child-mobile` form, even if the destroyed life was a turret.

## Anchor and turret

An active mobile child may submit:

```text
actionId: transform
actionCode: 101
formTargetId: turret
```

Anchor is illegal on every map-authored `anchorForbiddenTile`, including all
objective and protected-pad tiles. It consumes the tick and is irreversible
for that life.

A transform started on tick `T` completes after that tick's objective phase at
the end of `T + windupTicks - 1`. During the windup the life remains
`child-mobile`, keeps contributing its mobile objective weight, continues to
receive observations, and may only `Wait`. Nonlethal damage does not interrupt
the channel. Lethal damage emits `Destroyed` followed by
`FormTransitionCancelled`; a future-due transition at match end remains
pending without an invented cancellation.

Completion keeps the same actor identity, runtime, memory, position, facing,
cooldown, energy, and accumulated damage. Health becomes:

```text
min(turret.maxHealth, currentHealth + anchor.healthGain)
```

The default arm uses a 5-HP turret, `+2` Anchor health, cooldown 1, 360-degree
vision and firing, no movement or rotation, and objective weight zero. A
turret cannot capture or contest.

Turrets submit:

```text
actionId: shoot-direction
actionCode: 102
launchHeading: North | NorthEast | East | SouthEast |
               South | SouthWest | West | NorthWest
```

This launches one straight, non-programmed projectile in the absolute heading
without changing body facing. It uses the match's normal damage, energy,
cooldown, range, wall, unit-contact, and strict diagonal-corner rules.
Programmed curves remain unavailable to turrets.

## Observation and action contract

Every active life receives the complete immutable match contract plus a
canonical public observation. The current team-perception arm shares allied
state and the frozen union of what allied sensors can see, with exact
`observedBy` provenance. A runtime never receives an ally's same-tick action;
all observations are frozen before any decision is executed.

The observation includes:

- exact self identity, current form, state, and pending transition;
- every own stable slot and lifecycle timer;
- active allies and visible enemies as variable collections;
- visible tiles, projectiles, and redacted events with provenance;
- the current frontline state;
- the complete action catalog and exact per-tick legality masks.

The public manifest also carries all gameplay variables that may differ by
map, ruleset, or season: topology and counts, form statistics, respawn and
fabrication timings, projectile rules, capture/victory rules, action
parameters, Anchor policy, and map geometry. A future form such as flight is a
new form/action capability, not a reinterpretation of an existing action.
Older artifacts may remain executable while becoming ineligible or
uncompetitive under a newer contract.

## Replay and ML status

Internal replay v2 records, for every exact actor and tick:

- the immutable public match contract and fingerprints;
- tick-start state and lifecycle events;
- the same canonical observation delivered to the runtime;
- runtime reply, accepted decision, and action resolution;
- authoritative ordered events, projectile traversal, and post-state;
- terminal team and stable-unit results.

Default slot form and active-life form are separate replay facts. Pending
transitions and explicit start/change/cancel events make Anchor sequences
trainable without re-simulating historical engine logic. Reward shaping is
not embedded in the canonical replay; raw territory, damage, lifecycle, and
terminal facts are.

The architecture is neural-policy friendly, but it does not promise zero-shot
skill on unseen counts or actions. Models should encode variable entity
collections with masks, consume the public rules vector, maintain recurrent
state per life, and apply the supplied legal-action masks. Four- or five-body
maps remain structurally representable: the executable topology fixture runs
five concurrent lives per team, including fabrication, observation, replay,
transformation, and terminal unit rows. Competitive behavior at those counts
still requires suitable training data.

Dataset export, public replay corpora, model-asset packaging, starter
inference, SDK/protocol vNext, canonical WASM execution, CLI selection, App
admission, and ranked use remain follow-on work in
[`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md) and
[`FRONTLINE-IMPLEMENTATION-PLAN.md`](FRONTLINE-IMPLEMENTATION-PLAN.md).

## Evidence status

Engine, observation, replay, viewer, and mobile-bridge tests establish
determinism and mechanical causality. They do not establish fun, duration, or
balance. The strong-turret defaults remain starting arms.

Before any product verdict, Frontline still requires fixed all-WASM candidate
artifacts, at least four independently authored Frontline-native doctrines
with equal iteration budgets, causal arm comparisons, dynamics analysis, and
at least twelve outcome-blind replay reviews under
[`EVALUATION-METHODOLOGY.md`](EVALUATION-METHODOLOGY.md).
