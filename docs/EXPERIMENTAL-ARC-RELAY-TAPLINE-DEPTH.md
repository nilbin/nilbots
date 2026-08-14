# Arc Relay Tapline depth pack — implementation brief (owner-set 2026-08-14)

Status: **approved for isolated prototyping; not shipped**. Tapline-3, hosted
entrant playlist v21, historical rules, sheets, fingerprints, and replays stay
immutable until a later owner review explicitly promotes a result.

## Goal

Create more durable, visible strategic identities without bringing back a
second top-level play/state-machine grammar. The candidate pack contains exactly
five ideas:

1. team-relative `1 / 2 / 4` Core charge for own / centre / enemy Wells;
2. veteran-centred "ace and escort" strategies through narrow Orders v2
   additions;
3. synchronized public respawn waves;
4. one duplicate-class wildcard in each eight-body roster;
5. persistent deployment control, led by a targetable three-health,
   long-duration Mason barrier that is replaced when its owner deploys another.

These are **candidate changes, not one pre-approved bundle**. Implement each
behind independently selectable, canonically serialized rules/grammar fields.
First prove each arm against unchanged Tapline-3; only then expose a combined
`depth-pack` arm. Do not use a bundled result to claim which individual change
helped.

## Experiment identities

At minimum, retain these distinguishable study cells:

| Cell | Difference from Tapline-3 |
| --- | --- |
| control | none |
| score-124 | origin-valued Core charge and sufficient Well supply only |
| veteran-command | veteran Orders affordances only |
| respawn-wave | synchronized respawns only |
| roster-wildcard | roster diversity rule only |
| durable-deployments | deployment fact and Mason barrier changes only |
| depth-pack | all five, after the isolated cells execute correctly |

Exact registered IDs should follow the existing immutable loop-profile and
ruleset naming conventions. Every replay and receipt must identify the exact
cell; no candidate may masquerade as `tapline-3`.

## 1. Team-relative 1 / 2 / 4 Core charge

### Rule

A Core's bank value is determined by its source Well **relative to the banking
team**, not by an absolute north/south label:

| Source relative to scorer | Charge |
| --- | ---: |
| own/near Well | 1 |
| centre Well | 2 |
| enemy/far Well | 4 |

The same outer Core is therefore worth 1 to the nearby team and 4 if the other
team steals it and completes the long return. Centre is worth 2 to both teams.
The value is settled at bank time and never depends on which team first picked
the Core up.

Keep the existing objective identity:

- 3 charge produces one Pulse;
- 3 Pulses destroy the opposing reactor and win;
- charge above a threshold carries into the next Pulse;
- resolve **every** threshold crossed by one bank immediately and emit one
  ordered Pulse event for each.

Equivalently, for a non-terminal bank:

```text
available = previousCharge + bankValue
pulsesEarned = available / 3
remainingCharge = available % 3
```

Stop when the third Pulse wins; never decrement reactor integrity below zero.
This makes total charge order-independent:

- `4` -> one Pulse plus 1 charge;
- `4 + 4` -> two Pulses plus 2 charge;
- `4 + 4 + 1` -> win;
- `2 + 4` -> two Pulses immediately;
- `2 + 4 + 1 + 1 + 1` -> win;
- `2 + 2 + 2 + 2 + 1` -> win;
- nine 1-value banks -> win.

The current bank path awards at most one Pulse per bank. That is not acceptable
for this arm: for example, a 4-value bank onto 2 stored charge must award two
Pulses rather than leave a full-but-unresolved 3 charge.

### Supply needed for the safe strategy

Tapline-3 currently schedules only seven birth rounds per Well, so nine own-Well
banks cannot occur. The initial score-124 prototype uses:

- first global beat: 25 (unchanged);
- Well cadence: 65 ticks;
- scheduled rounds: 9;
- birth jitter: +/-6 ticks (unchanged);
- match maximum: 600 ticks (unchanged).

The ninth outer-Well beat is nominally tick 570 and remains inside the match at
maximum positive jitter. Thus the nine-safe-Core route is possible but slow,
while centre control and deep raids exchange safety for speed.

### Information and presentation

- The public contract declares the three team-relative values.
- `CoreBanked` evidence exposes the actual charge awarded.
- Post-bank reactor state always has charge `0..2`; Pulse count remains public.
- Add an Orders fact for the team's own scored Pulse count (for example
  `reactor-pulses`); `reactor-charge` retains its existing remainder meaning.
- Player-oriented views label Wells `1`, `2`, and `4`. A neutral spectator view
  labels the outer Wells `Blue 1 / Red 4` and `Blue 4 / Red 1`, with `2 / 2`
  at centre.
- Before a bank, the viewer must make the resulting Pulse count and carried
  remainder understandable, including a possible two-Pulse bank.

### Why this should be deeper and more fun

Safe logistics, centre control, and a defended deep raid become genuinely
different paths to the same victory. Score state changes route value during a
match: a 4-value Core can become a comeback attempt or immediate finishing
threat, while a one-point Core remains a reliable closeout. This creates
defence, feints, escorts, and route switching without forbidding any origin.

### Main risks

- A successful far-Well raid may shorten a match too sharply.
- Centre may remain the reliable default despite the far-Well reward.
- A two-Pulse bank may be exciting or may feel like an unexplained burst.
- Faster supply can create loose-Core clutter if sheets cannot settle Wells.

Do not dilute `1 / 2 / 4` before viewing it. If pacing fails, compare target or
supply tuning in a separately named arm rather than silently changing this one.

## 2. Veteran-centred team strategy

### Preserve the existing veteran economy

Do not buff veterancy numbers in the first arm. Keep:

- level 1 start, 2 XP per level, maximum level 5;
- one ordinary kill XP plus the existing victim-level bounty;
- damage, vision, reach, and vitality upgrade paths;
- a quiet action tick to invest;
- complete reset on death.

The reset and bounty are the built-in counterweight to an ace: protecting a
veteran has value, while killing that veteran both removes the investment and
accelerates the hunter.

### Orders v2 additions

Keep the current role priority tree, shared orders, capacities, branch
lifecycle, and `follow` behavior. Add only these general-purpose primitives:

1. `role-highest-level`: a role-scoped integer fact returning the highest level
   among its live bodies, or 0 when none are live. It is exact for a singleton
   `ace` role and unambiguous for every other role.
2. A friendly unit target `{ "role": "ace" }` for `use`. Resolve the nearest
   legal live body in the role by terrain distance, then stable unit ID. This
   lets Repair Beam and future friendly-target skills deliberately support the
   authored ace instead of merely selecting the globally weakest ally.
3. `{ "selector": "most-experienced-ally" }` for `follow` and compatible
   friendly `use` actions. It is unresolved while every eligible ally is level
   1, otherwise selects highest level, then terrain distance, then stable unit
   ID. Levels change only at explicit kill events, preventing tick-to-tick
   target noise.

Existing syntax remains valid and byte-identical. Do not add `plays`,
formations, calls, phases, operation records, XP transfer, kill reservation,
or a hidden leader state.

### Required strategy demonstrations

- **Authored ace:** one singleton `ace` changes behavior with `self-level`;
  screeners follow it and a Patchbay can repair that role specifically.
- **Emergent ace:** supporters fall back normally at level 1, then rally around
  whichever eligible ally first becomes the most experienced.
- **Bounty counter:** the opponent can prioritize, isolate, and kill the ace;
  support returns to authored fallback after the death/reset.
- **Objective ace:** a veteran may become the carrier or escort for a 4-value
  raid, but carrying still blocks a pending investment exactly as today.

Every selection and fallback must appear in the existing debug/explain trace.

### Why this should be deeper and more fun

One body can acquire a match-specific identity that changes both teams' plans:
feed and protect our ace, pressure it before investment, or collect its bounty.
Because death erases the build, the veteran is a visible strategic asset rather
than permanent score-to-power snowballing. The mechanic also makes meaningful
use of `upgradePath` and `self-level`, which the retained Tapline Orders sheets
currently do not exploit tactically.

### Main risks

- Supports may overcommit to an ace while objectives are unattended.
- Stable tie-breaking can make unit ID look like a hidden preference.
- Repair plus vitality may make a protected veteran too hard to finish.
- Last-hit XP may make an intended funnel unreliable; do not add assist XP
  without a separate design decision.

## 3. Synchronized respawn waves

### Rule

Replace each body's fixed 30-tick respawn delay in this arm with one global,
public wave clock shared by both teams:

- wave period: 30 ticks, anchored at ticks `30, 60, 90, ...`;
- minimum dead time: 15 complete ticks;
- a destruction on tick `D` is eligible no earlier than `D + 1 + 15`;
- its due tick is the first global wave at or after that eligibility tick;
- every eligible stable slot respawns together and may act on that wave tick.

This yields 15..44 complete dead ticks (mean 29.5), nearly preserving the
current average while grouping reinforcements. Both teams use the same phase;
no seed, side, or first-death offset changes it.

Expose the next wave tick/countdown in the public state and viewer. A body that
misses the eligibility cutoff by one tick visibly waits for the following wave.
Veterancy resets at destruction as today, not at the later wave.

### Why this should be deeper and more fun

Deaths create readable regrouping windows instead of a continuous trickle.
Teams can time a push, retreat for the next wave, exploit a temporary numbers
advantage, or synchronize an escort. Spectators can understand when pressure
will arrive.

### Main risks

- The one-tick cutoff can create a 30-tick difference in downtime.
- Full waves may create repetitive deathballs or overwhelm a reactor defence.
- Burst kills may become more decisive when several opponents share one long
  absence.

Boundary tests must cover destruction immediately before, on, and after every
eligibility cutoff; simultaneous cross-team waves; occupied spawn traffic; and
deterministic replay round-tripping.

## 4. One duplicate-class wildcard

### Rule

An eight-body candidate roster must contain at least seven distinct class IDs:

- exactly eight bodies total;
- no class more than twice;
- at most one class ID may be duplicated;
- equivalently, `distinctClassCount >= 7`.

There is no separate wildcard token. The duplicated class is simply whichever
single pair the author includes. `Mason, Mason` is legal; `Mason, Mason,
Patchbay, Patchbay` and any triple are illegal.

Keep the Orders v2 compiler capable of reading historical max-two sheets.
Enforce the new minimum-diversity rule through the candidate mode/playlist
admission contract and candidate editor validation so existing Tapline sheets
and replay history remain readable. If promoted, mint a new playlist; do not
retroactively invalidate v21.

### Why this should be deeper and more fun

Seven classes force broad tool coverage while one pair still lets a sheet make
a memorable strategic statement such as double Mason fortification. A strict
max-one rule would make the eight-class starter roster a forced composition and
remove that identity; the wildcard preserves one concentration choice and its
opportunity cost.

### Main risks

- One duplicated utility class may still become mandatory.
- Several retained strategies currently use only five or six distinct classes,
  so candidate-native sheets must be adapted rather than treated as balance
  voters unchanged.

Acceptance cases must include eight distinct starters, every possible legal
single pair, two different pairs, a triple, locked-class admission, and exact
historical-profile behavior.

## 5. Deployment activity and Mason's barrier

### What “hardlight” means

“Hardlight” is science-fiction shorthand for projected light or energy made
solid enough to behave like a physical object. In Nilbots it means Mason
projects a temporary blocking tile. Keep the stable IDs `hardlight-block` and
Mason in this prototype, but player-facing copy should explain it plainly:

> Projects a solid 3-health barrier. It blocks movement and shots until
> destroyed, expired, or replaced by Mason's next barrier.

A later owner decision may rename the display label to “Projected Barrier” or
“Barrier Block”; this brief does not require an identity migration.

### Existing behavior to preserve explicitly

The current Hardlight Block already has capacity/hull 3, lasts 12 ticks, has a
14-tick cooldown, consumes hostile projectile contacts, and removes an older
same-owner construct when a replacement is created. The prototype must not
mistake that hidden implementation for a completed player-facing mechanic.

### Candidate Mason rule

- maximum health: 3;
- duration: 30 ticks;
- cooldown: 14 ticks (unchanged);
- blocks ground movement and consumes the shot that destroys it;
- remains until health reaches zero, duration ends, or its stable Mason slot
  successfully deploys a replacement;
- a replacement removes the old barrier atomically with reason `replaced`
  before the new one becomes active;
- one active Hardlight Block per stable Mason slot, including across a death
  and respawn. A surviving old-life barrier therefore occupies the new life's
  deployment slot and is replaced if that Mason casts again.

Treat health as damage, not merely an unexplained contact counter. Ordinary
shots deal their declared damage plus the shooter's applicable veterancy damage
upgrade; a barrier has no facing and receives no rear-arc or target-paint
multiplier. A projectile/strike stops on contact even when that contact destroys
the barrier. Route all damaging signatures through an explicit, tested policy
rather than letting some destroy the barrier accidentally and others ignore it
by implementation happenstance.

Visible hostile deployments must be deliberately attackable. Add a narrow
`deployment` fight target that selects a visible hostile deployment with a
legal firing line, then distance and stable operation ID. It may resolve to a
suppressive heading/position rather than pretending a construct is an actor.
The ordinary authored target priority decides whether clearing it outranks an
enemy body.

### Deployment Orders fact

Add body fact `deployment-active`, integer `0 | 1`, for the durable construct
family already governed by replacement semantics: Prism Wall, Trip Node,
Hardlight Block, and Sentinel Seed.

For this fact, active means an extant deployment owned by the body's stable
team/unit slot, including an extant temporarily suppressed deployment. It
becomes 0 on destruction, trigger/removal, expiry, or permanent cancellation.
This definition lets an author distinguish:

- initial placement: `signature-ready` and `deployment-active == 0`;
- deliberate relocation/recast: `signature-ready` and
  `deployment-active == 1`;
- ordinary work while the existing deployment holds.

Do not silently prevent recasting. The sheet chooses whether an active
deployment should be preserved or relocated; the rules guarantee only the
one-per-slot replacement invariant.

Public observation, replay, viewer, and explain/debug output must expose owner,
kind, tile(s), current/max health where applicable, expiry, suppression, and
replacement/destruction reason. A health bar or equally legible three-step
damage read is required for the Mason barrier.

### Why this should be deeper and more fun

A long-lived barrier makes lane ownership and planned fortification persist
long enough for both teams to react. Three health creates real dismantling
counterplay, while replacement on recast prevents accumulation and makes
relocation a meaningful choice. `deployment-active` lets sheets express that
choice without a new play framework.

### Main risks

- A 30-tick barrier may seal a narrow route too cheaply.
- A sheet that recasts every cooldown receives little benefit from the longer
  duration and may churn visual state.
- Veteran damage may make three health trivial; no intentional target may make
  it oppressive. Both cases need replay inspection.
- Stable-slot persistence through death can create confusing ownership unless
  the viewer keeps the team/Mason relationship visible.

## Implementation boundaries

- Version all gameplay-affecting fields in the immutable Engine contract and
  canonical writer/reader. Defaults preserve Tapline-3 byte-for-byte.
- Keep Engine, SDK, Runtime, Guest/WASM, replay-v3, CLI summary, editor, and
  viewer mirrors consistent wherever a new public field or selector crosses
  those boundaries.
- The shared Orders compiler remains the only compiler. New syntax is additive,
  strictly validated, represented in explain output, and inert for old sheets.
- Do not mutate a frozen sheet, profile, playlist, artifact, map, or golden
  replay. Do not promote the experiment to `Current` or hosted admission.
- Do not refresh the tracked tactical WASM mirror merely for local iteration;
  that remains the explicit release checkpoint.
- Every accepted executor command keeps a non-empty debug reason.

## Minimum verification before owner review

1. Focused engine and contract tests for every numerical boundary and all
   team/rotation symmetries.
2. Golden tests proving historical rules, fingerprints, and replays unchanged.
3. Orders compiler/runtime tests for fixed ace, emergent ace, role-targeted
   repair, deployment preservation, and explicit relocation.
4. Deterministic replay round-trip tests for multi-Pulse banks, wave births,
   construct damage/replacement, and new public observations.
5. Candidate-native sheets that deliberately exercise each mechanic. Historical
   sheets are compatibility sentinels, not product-quality voters for the new
   objective and roster rules.
6. Trace-first, six-cell phone exhibits for replay-visible behavior changes.
   Do not run or claim a balance sweep merely because implementation is done;
   use the repository's balance workflow only when the owner requests that
   measurement gate.

The implementation report should be
`docs/reports/ARC-RELAY-TAPLINE-DEPTH-PACK.md` and state what is implemented,
what remains isolated, exact profile/fingerprint identities, focused test
results, exhibit links, known risks, and the next owner decision. It must not
claim that the pack is balanced, deeper, or ready to ship from implementation
evidence alone.
