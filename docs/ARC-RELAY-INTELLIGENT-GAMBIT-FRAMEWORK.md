# Arc Relay intelligent gambit framework

Status: owner-review design proposal, 2026-08-02. This document defines the
player model and deterministic execution semantics that a future sheet pass
should implement. It does **not** change Arc Relay rules, fog, rendering,
canonical replays, the current player-sheet schema, or either frozen stock
mind.

## The new-player explanation

Every body always has an ordinary job from the sheet: where to operate, what
to protect, how aggressively to engage, and what to do with a Core. That is
its **baseline**.

A **gambit** is a saved team play for a recognisable situation. It temporarily
borrows named bodies from their baseline jobs, gives them one coordinated
mission, and releases them when the mission succeeds, becomes unsafe, or runs
out of time. Bodies not borrowed by the gambit keep doing their normal jobs.
When a gambit is unavailable or cooling down, everybody follows the baseline;
cooldown never means standing still.

The opponent can see the composition and everything that happens on the
field, but not the private sheet. A gambit may react only to what its own mind
currently sees, remembers seeing, or can honestly infer from missing
information. It never reads the opponent's hidden plan.

That is the entire mental model:

> **Baseline is how your team normally plays. A gambit is a bounded,
> conditional exception with a purpose, a cost, and a way home.**

## Why the current rear-ambush example is not the desired model

In gallery sample 07, the two rear Towlines had `hold-fire` as their default
position intent. A previous rear collapse had ended, and its 54-tick cooldown
blocked another activation. During that cooldown one Towline personally saw a
passing enemy and the team knew about an enemy carrier in the registered
corridor, but both bodies waited until the gambit became eligible again.

The problem is not that `hold-fire` can never be sensible. Ignoring a screen
to preserve surprise can be exactly right while an ambush is genuinely armed.
The problem is that ambush staging and concealment were smuggled into the
**baseline**, so the bodies remained inert when no ambush owned them.

The desired model puts infiltration, staging, concealment, the strike, and
extraction inside one bounded operation. Outside that operation, the Towlines
resume an ordinary interception baseline and respond normally. The cooldown
blocks another coordinated rear operation; it does not disable judgment.

## 1. The three levels of authorship

### 1.1 Stock tactics: how to perform an intent

The stock mind owns local execution: legal pathfinding, collision avoidance,
aiming, firing, signature legality, short evasion, and recovering from a
temporarily blocked tile. A player does not script individual ticks or eight
headings.

Tactics must respect the selected mission and rules of engagement. The stock
mind may not turn `conceal` into a chase because a target happens to be
visible, nor turn `intercept` into passive waiting because a preferred
signature is cooling down.

### 1.2 Baseline: what each body normally does

Every slot has a complete baseline intent:

- role and operating area;
- outbound, return, and fallback routes;
- objective and carrier priorities;
- rules of engagement and pursuit leash;
- signature budget; and
- partner or formation relationship, if any.

Baseline is continuous and total. A live body can always derive a useful
intent from it. If no gambit currently claims that body, baseline applies on
that tick.

### 1.3 Gambit: when the team temporarily changes the plan

A gambit is a multi-tick operation with one mission. It is appropriate when it
either coordinates multiple bodies or makes one body take a consequential,
multi-tick spatial commitment. It may override only the stated parts of the
baseline; everything else continues to inherit from baseline.

A gambit is not:

- a single shot, hook, turn, or dodge;
- a permanent role such as “always escort the carrier”;
- a list of exact tick-by-tick actions;
- an omniscient reaction to hidden enemy state; or
- a patch for weak pathfinding or fire control.

## 2. What makes a gambit intelligent

Intelligence here means adapting a goal-directed plan to causal evidence,
uncertainty, changing opportunity, and counterplay. It does not mean adding
more conditions or following a longer script. The player authors five
understandable parts, and the editor and preview must prove that they work
together rather than accepting a plausible name.

### Evidence and uncertainty

What honest evidence makes this play worth preparing, and what evidence makes
the team commit?

The evidence block has two distinct conditions when preparation is required:

- **prepare when** starts positioning before the opportunity is certain; and
- **commit when** spends the play once the target or timing is confirmed.

A simple gambit may omit preparation and commit immediately. Conditions name
their information quality explicitly: `visible now`, `last seen within N`,
`unobserved for N`, or a public objective fact. Unknown is never silently
treated as zero.

### Goal and success

One sentence describes what the operation is trying to achieve, followed by a
machine-checkable success condition. For example: “Force the north carrier to
drop before it clears the homeward fork”; success is `that Core is loose or
ours before the carrier leaves north-return`.

“Play defensively” is not a mission. “Prevent this carrier crossing this line
for 12 ticks” is.

### Participants and feasibility

Which slots or role-qualified bodies are required, how many must be available,
which drawn path/zone/line they use, and whether substitutions are allowed?

Activation is atomic. If a play requires two Towlines and only one is
available, it stays armed while both bodies continue baseline; it does not run
a malformed half-ambush. A gambit may explicitly allow “any two of these
three” or a class-qualified substitute. It may not commandeer an unrelated
body implicitly.

The editor checks **achievability** here: required actors exist, have the
needed capabilities, can plausibly reach the area before the deadline, and do
not depend on mutually impossible assignments.

### Reactions, release, and recovery

Each phase states its rules of engagement, the facts that mean success, the
facts that force an abort, and how borrowed bodies rejoin baseline.

The minimum useful engagement vocabulary is:

| Order | Fire / signature use | Movement consequence |
| --- | --- | --- |
| `conceal` | do not initiate; damage or an authored, observable compromise cue invokes the abort | stay hidden or extract; never chase |
| `defend-in-place` | answer an immediate threat | may reposition inside the mission area; never pursue out |
| `opportunistic` | take legal attacks while pursuing the mission | do not leave the route/zone to chase |
| `carrier-focus` | prioritise the confirmed carrier | pursue only inside the drawn leash |
| `break-contact` | defensive actions only | take the recovery/fallback route |

This separates “may shoot” from “may abandon position,” which the current
`hold-fire` / `normal` switch does not.

Recovery is physical, not a teleport or an immediate role-label swap. A body
finishes an in-progress authoritative action, takes the authored extraction or
nearest safe fallback route, and then resumes its baseline job. A player can
choose an immediate baseline release for plays where location does not matter.

### Timing and adaptation

A gambit declares:

- preparation deadline;
- optional commit lock that prevents routine plan thrashing;
- mission deadline;
- cooldown after release; and
- re-arm rule.

Success, explicit abort, loss of required actors, or an invalid causal target
always overrides a commit lock. A minimum tenure may suppress indecisive plan
switching; it may never force bodies to continue an already impossible play.

The safe default is **edge re-arm**: after a gambit ends, its situation must
become false and later become true again, in addition to cooldown expiring.
“Repeat while this remains true” is an advanced explicit choice, not the
default. This prevents a persistent condition from firing the same play every
cooldown forever.

## 3. Exact execution semantics

### 3.1 Effective intent on a tick

The stock mind first updates every gambit state from the tick's causal team
observation: success and abort conditions, deadlines, and actor validity are
evaluated before new activation or routine preemption. It then resolves each
live body's intent in this order:

1. authoritative action continuation and action legality;
2. a valid claim by the highest-priority active gambit;
3. that body's complete baseline intent; then
4. `Wait` only when the chosen intent genuinely calls for holding or no legal
   progress action exists.

Cooldown is not in this list. It affects whether a gambit may activate, never
what an otherwise unclaimed body does.

### 3.2 Operation phases

A gambit has this deterministic state machine:

```text
DORMANT --prepare condition + actors available--> PREPARE
PREPARE --commit condition----------------------> COMMIT
PREPARE --abort/deadline------------------------> RECOVER
COMMIT  --success/abort/deadline----------------> RECOVER
RECOVER --actors released or deadline----------> DORMANT + COOLDOWN
```

For a no-preparation play, the first transition enters `COMMIT` directly.
Every transition records its causal reason. At most one transition per gambit
occurs in a tick.

If the situation flickers during preparation, the gambit's authored validity
window decides whether preparation continues. There is no implicit one-tick
memory. This gives the player explicit hysteresis instead of accidental
thrashing.

### 3.3 Concurrent and conflicting gambits

Disjoint gambits may run at the same time. A body may be claimed by at most one
gambit. This matters on a three-theater map: a north carrier extraction should
not automatically cancel an unrelated south-Well trap.

For an overlapping scope:

- priority is explicit and deterministic;
- a gambit activates only if it can claim its complete minimum actor set;
- after a commit lock, a higher-priority play may preempt a lower play once the
  lower play's current authoritative action finishes;
- success, abort, or impossibility can always release the lower play; and
- a lower-priority play that cannot claim its actors stays armed or expires;
  it never receives a surprising partial group.

A home emergency is not a magic exception with hidden precedence. If it must
break a locked operation, the lower operation names that public emergency as
an abort condition. The editor warns when an overlapping high-priority play
cannot ever obtain its actors because a lower play lacks a compatible release.

This is more precise than one global active gambit, and more understandable
than independently overriding every body on every tick.

### 3.4 Fallback table

| Situation | Required behavior |
| --- | --- |
| Gambit never triggered | Baseline throughout. |
| Trigger true but actors unavailable | Gambit stays armed until its preparation deadline; actors remain baseline. |
| Gambit cooling down | Baseline. No gambit-specific hold, path, role, fire discipline, or signature budget survives. |
| Anchor is unknown | Use an explicitly authored remembered/fallback anchor, or abort; never treat unknown as an empty field. |
| Preferred signature unavailable | Continue the mission with legal ordinary actions; do not wait merely for the signature. |
| Drawn path is temporarily blocked | Repath inside its corridor; after the route-failure limit, take the authored fallback or abort. |
| A required actor dies | Continue only if the declared minimum and substitution policy still hold; otherwise recover and release. |
| A borrowed actor respawns | It starts on baseline; it rejoins an active play only if the play explicitly permits replacement and can still meet its deadline. |
| Success occurs early | Recover/release immediately; minimum tenure does not keep a completed operation alive. |
| Abort occurs during commit | Finish only an already-authoritative action, then recover; no uninterruptible strategic dead time. |
| Higher-priority overlapping play fires | Preempt under the declared lock rule, or leave the new play armed; never merge overlays implicitly. |
| Recovery cannot reach its route | Resume the nearest legal baseline intent after the recovery deadline. |

## 4. Concrete play cards

These are player-language examples, not raw JSON and not commitments to exact
tick values. The preview would compile map travel estimates into suggested
timings and let the player adjust them.

### Example A — Rear Hook (ambush)

**Situation:** Prepare when an outer Well is 20 ticks from birth, at least two
enemies and fewer than two allies are currently observed in its approach zone,
and two rear interceptors are available.
Commit only when an enemy carrier is visible now or was seen within two ticks
inside `north-return`, and both interceptors have reached `rear-pocket`.

**Mission:** Force that Core loose or take possession before its carrier clears
the homeward fork.

**Actors and area:** Two Towlines follow separate drawn infiltration paths.
Both are required; no substitutes. Staging is part of this gambit, not their
baseline assignment.

**Reactions/release:** During preparation they use `conceal`: a harmless screen
may be allowed to pass. Taking damage, a visible enemy entering the staging
zone, a visible sweep effect covering it, or losing either Towline aborts the
ambush and starts `break-contact`. On commit they use `carrier-focus`
inside the return corridor. A loose/owned Core is success. A bank, a carrier
leaving the corridor, or a home emergency is abort. They extract to the
nearest intercept line, then resume their ordinary opportunistic-interceptor
baseline.

**Timing:** Reach staging before the Well birth; give up if no carrier appears
within 18 ticks after birth; commit for at most 14 ticks; re-arm only after the
corridor has been clear and the cooldown has elapsed.

**Cost and counterplay:** Two bodies surrender Well pressure and home defence.
A scout can reveal them, an escort can clear them, or a carrier can switch
routes. A false expectation wastes the whole deployment.

Crucially, a passing enemy ignored during the **conceal** phase is intended and
explainable. A passing enemy ignored while the card is dormant or cooling down
is a baseline/executor defect.

### Example B — Lantern Sweep (expecting an ambush)

The opponent's composition is public; its private route and gambit are not.
This play therefore reacts to risk evidence rather than reading “enemy ambush
active.”

**Situation:** Prepare when we carry a Core toward a registered risk fork and
either (a) two interception-capable enemy bodies have been unobserved for 10
ticks, or (b) one was last seen moving through a rear connector. Commit when
the carrier reaches the safe pre-fork rally.

**Mission:** Clear one return line or expose a threat before committing the
carrier through the fork.

**Actors and area:** The carrier pauses at the rally; a Lantern sweeps three to
four tiles ahead; a Palisade or other declared screen holds the carrier-facing
side. The exact actors and the alternate return line are saved on the sheet.

**Reactions/release:** If the sweep finds an ambusher, mark it and switch to
the alternate route under `defend-in-place`; do not chase the ambusher. If the
line remains clear for five ticks, release the carrier onto the primary route.
If the scout dies or a visible pursuer closes within three tiles, abort the
pause and take the conservative fallback route. End when the carrier clears
the risk zone, loses the Core, or banks.

**Timing:** At most five ticks of probing and twelve ticks through the risk
zone. Cooldown applies to re-running this inspection at the same fork, not to
the carrier's baseline delivery behavior.

**Cost and counterplay:** A false alarm delays delivery and removes a Lantern
and screen from the next Well. An opponent can exploit that caution without
ever fielding an ambush.

### Example C — Fork Shadow (countering the counter-ambush)

**Situation:** Prepare when an enemy carrier remains in the authored fork-entry
zone for two ticks and a Lantern advances at least three tiles ahead of it.
Commit when the carrier chooses a route after the probe.

**Mission:** Let the probe clear an empty lane, then cut the carrier's chosen
exit rather than revealing on the scout.

**Actors and area:** One interceptor remains outside the primary sweep; the
other rotates through a drawn connector to the alternate cutoff. Both are
required.

**Reactions/release:** Use `conceal` against the scout. Commit on the carrier,
not the probe. Abort if the carrier retains two close escorts, a visible sweep
effect reaches either staging tile, either interceptor takes damage, or the
carrier refuses the fork for eight ticks. Recover to ordinary interception.

**Timing:** The reposition must finish before the carrier crosses the fork;
one bounded strike window, edge re-arm.

**Cost and counterplay:** The ambushers abandon the obvious primary trap and
can be stranded if the carrier waits, double-probes, or takes the now-empty
original line. This creates an actual prediction layer rather than a hard
“ambush counter” button.

The strategic web is therefore:

```text
unscouted fast return -> vulnerable to Rear Hook
Rear Hook             -> vulnerable to Lantern Sweep
automatic Sweep route -> vulnerable to Fork Shadow
Fork Shadow           -> vulnerable to patience, split screening, or double probe
```

Every answer spends bodies and time. None receives private knowledge or a
numeric combat bonus.

### Example D — Birth Rotation (a non-ambush granularity check)

**Situation:** The next Well births in 12 ticks, at least three visible enemies
are committed around the current Core, and two reserve-qualified bodies can
reach the next Well in time.

**Mission:** Establish two-body presence at the next Well before birth without
abandoning the current carrier's minimum screen.

**Actors and area:** Any two of three declared reserve/outer bodies take the
drawn cross-theater line; the current carrier and its last screen cannot be
borrowed.

**Reactions/release:** `opportunistic` on the route, no chase outside a
two-tile leash. Success is both bodies entering the Well zone. Abort if the
current carrier loses its last screen or a home threat crosses the defence
line. After the birth is resolved, they resume their baseline theater roles.

This is a gambit because it makes a bounded cross-theater resource trade. “Go
to the next Well whenever free” would be baseline; “shoot the enemy on the
way” is stock tactics.

## 5. Finding the right granularity

A proposed gambit should pass all seven questions:

1. **Can its mission be said in one sentence?** If not, split unrelated plays.
2. **Does it make a meaningful commitment?** It must coordinate bodies or
   spend position/time in a way the opponent can punish.
3. **Does it have an observable success and an honest abort?** “Behave better”
   is not authorable.
4. **Would it still make sense if local aiming and pathfinding changed?** If
   not, it is a tactic or executor patch.
5. **Is it temporary?** A play expected to own a body for most of the match is
   a baseline assignment.
6. **Can the opponent see a tell and answer before resolution?** A conditional
   free bonus is automation, not strategic depth.
7. **Does it have a way home?** If cooldown or missing targets can leave a body
   inert, the card is invalid.

Useful warning signs:

- a card fires on a nearly permanent condition;
- it commonly activates more often than major objective beats;
- it changes only one local action;
- it has no failure cost;
- `Wait` is its dominant output without a named hold/conceal purpose;
- its title needs “and” to join unrelated missions; or
- its success cannot be distinguished from baseline in a same-seed preview.

## 6. What makes the framework deep rather than merely complicated

More conditions do not create depth. These properties do:

1. **Plans are private; capabilities are public.** A player can anticipate from
   composition and observed behavior, but never knows the opponent's answer.
2. **Information has quality.** Current sight, recent memory, and absence are
   different evidence. A player chooses how much uncertainty to tolerate.
3. **Preparation creates tells.** Repositioning, holding a carrier, probing,
   and preserving signatures are visible and can themselves be read.
4. **Commitments have opportunity cost.** An ambusher is not contesting a
   Well; a cautious carrier loses tempo; a probe can be drawn away.
5. **Counterplay changes the expected payoff, not legality or stats.** No
   gambit grants hidden damage, speed, vision, or score.
6. **Fallback preserves play.** A failed prediction becomes a recoverable bad
   position, not frozen bodies or a scripted collapse.
7. **Finite sheet capacity forces a meta choice.** A player cannot carry a
   bespoke answer to every possible plan; the selected gambits reveal what
   risks the sheet is willing to accept.

The target is not a hard-coded rock-paper-scissors table. It is a web of
costly predictions where each plan has several field-observable answers and
where expecting an answer creates a further exploitable commitment.

## 7. Validation: intelligence must be proved in behavior

### 7.1 Static editor checks

The editor must reject or strongly warn on:

- hidden or ambiguous information (`enemy count = 0` when the area is simply
  unseen);
- no success condition, abort, deadline, or fallback;
- impossible actor/class requirements;
- estimated travel longer than the preparation window;
- a scope conflict with no deterministic priority;
- a permanently true default trigger with automatic repeat;
- a recovery route that cannot reach any baseline area; and
- a phase whose only likely output is unexplained `Wait`.

It should present the completed card back as one sentence:

> “When this evidence appears, borrow these bodies for this goal; react to
> these changes, stop by this deadline, and then resume baseline.”

If that sentence is misleading, the card is not ready.

### 7.2 In-process preview checks

The planned sheet preview playground should expose, at every playhead:

- each gambit's state: dormant, armed/preparing, committed, recovering, or
  cooling down;
- the exact causal facts that passed or failed;
- which bodies are claimed and what baseline job each will return to;
- why a body waited, ignored a target, changed route, or released; and
- a same-seed run with the gambit disabled.

This is both player coaching and a correctness surface. “Why did it do that?”
must have a short answer grounded in the card.

### 7.3 Mechanical gates before cohort evidence

For every retained activation:

- activation conditions and information qualifiers are true in the canonical
  observation history;
- required actors are claimed atomically;
- phase transitions occur on declared reasons;
- success/abort/deadline always reaches recovery and release;
- every released body resumes a valid baseline intent by the next tick after
  recovery;
- cooldown never suppresses baseline action;
- an ignored visible threat is attributable to an active ROE such as
  `conceal`, not an inherited stale overlay; and
- disabling the gambit on the same seed measurably changes the declared
  mission path or resource allocation.

Sample 07 would fail the “cooldown never suppresses baseline action” and stale
overlay checks even though it passed the old staged-wait/contact proof.

### 7.4 Depth evidence

A mechanically correct gambit is not automatically good. Promotion still
requires independently authored sheets, same-composition static controls,
WASM canonical verification, felt-degeneracy bars, and replay review. The
population read should require:

- at least one reproducible matchup reversal caused by a gambit-bearing sheet;
- a counter-sheet that reverses or materially weakens that edge;
- a further adaptation with a real cost, rather than an unconditional upgrade;
- no single gambit family dominating all reasonable baselines; and
- explanations that spectators and new players can identify from field tells
  and the post-match strategy trace.

A directed cycle is useful evidence of a counter-web, not a requirement that
every trio form rock-paper-scissors. Win rate alone cannot establish depth,
and mechanical activation alone cannot establish intelligence.

## 8. Product boundary and implementation consequences

This proposal deliberately does not bless either existing grammar:

- the current product sheet v1 has three event triggers plus duration,
  cooldown, role override, and rally line; it cannot express the complete
  gambit model;
- evaluation sheet v1 has richer clauses and spatial overlays, but one global
  active gambit, uninterruptible minimum tenure, and cooldown-sensitive
  defaults produced the failure above.

The useful evaluation work should be retained—causal clauses, drawn paths and
zones, anchors, sparse overlays, canonical linking, and deterministic traces—
but the next product schema needs the operation phases and baseline guarantees
defined here. This is a product/stock-mind design change, not an Arc Relay
engine-rules change.

No fog or renderer code should be changed as part of this framework. The
pending renderer work should independently make team-vision review accurate;
the strategy interpreter merely consumes the same causal team observation it
already receives.
