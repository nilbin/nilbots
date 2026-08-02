# Arc Relay intelligent-gambit adversarial design round

Date: 2026-08-02
Branch: `codex/game-redesign`
Canonical proposal:
[`docs/ARC-RELAY-INTELLIGENT-GAMBIT-FRAMEWORK.md`](../ARC-RELAY-INTELLIGENT-GAMBIT-FRAMEWORK.md)

## DECISION NEEDED

Approve the frozen semantics below as the contract for a narrow stock-mind
prototype. Recommended: approve. Do not build the complete player editor or
freeze a wire schema until Rear Hook and Lantern Sweep survive same-seed
replay review.

## RESULT

The framework is ready for a vertical-slice implementation, not a product
schema freeze. The design round closed participant loss, substitution,
concurrency, preemption, incomplete information, failure recovery, authoring
granularity, and explanation semantics. Eight hostile table scenarios produce
one deterministic response each without reading hidden state, recruiting
unrelated bodies, freezing on cooldown, or changing Arc Relay rules.

The largest simplifications are deliberate:

- substitution is preparation-only;
- commitment chooses at most one of two branches and never switches branch;
- disjoint gambits may coexist, but one body belongs to one gambit;
- committed operations are not routinely preempted;
- `true`, `false`, and `unknown` are distinct; and
- three small gambit cards remain the initial sheet budget.

No fun or balance claim is made. This was a semantic adversarial review, not a
simulation study.

## EVIDENCE

### 1. Frozen rulings

| Surface | Frozen first-implementation ruling | Rejected complexity |
| --- | --- | --- |
| participant resilience | phase-scoped `essential`, preparation-only `replaceable`, and `optional` tasks | implicit whole-team substitution and a generic `expendable` flag |
| activation | complete minimum group claimed atomically | malformed partial formations |
| substitution | declared candidate pool, preparation only, unchanged deadline and travel-feasibility check | mid-fight reinforcement chains and deadline resets |
| respawn | baseline only; never rejoins the current activation | a returned body crossing the map for a stale operation |
| operation states | optional Prepare, one committed branch, physical Recover, then cooldown | nested phases and direct gambit-to-gambit calls |
| same-tick order | authoritative state, success, abort/invalid target, participant minimum, deadline, routine arbitration | accidental ordering based on JSON traversal |
| branches | at most two ordered commit branches; first true branch wins once | per-tick branch flipping or arbitrary scripts |
| concurrency | disjoint cards may run together; one claim per body | one global plan and free overlay merging |
| preemption | Prepare/Recover preemptible; Commit releases only on its own success/abort/impossibility | magic emergency priority and silent commitment cancellation |
| information | conditions are three-valued and cite public, observed, remembered, or deterministic-inference provenance | unseen equals empty, opaque confidence, or opponent-sheet reads |
| condition grammar | per transition: up to four `all-of` clauses and three flat `any-of` alternatives | nested Boolean programs |
| sheet budget | three gambit cards, each one mission with success, abort, deadline, and recovery | speculative extra slots before usability/depth evidence |
| explanation | deterministic sidecar trace of evidence, branch, claims, intent, wait, transition, and baseline release | names such as “ambush” serving as proof of behavior |

The existing observation contract already supplies the relevant causal inputs:
team-union visible bodies and tiles, visible events and projectiles, public
Well/reactor state, visible Cores, and visible signature tells/effects. The
mind can retain observation history deterministically. No fog or renderer
change is required for the strategy prototype.

### 2. Hostile tabletop cases

#### Case 1 — Rear Hook loses a Towline during infiltration

**State:** `PREPARE`; two Towlines are essential; neither has a substitute.
**Hostile event:** one is destroyed before reaching `rear-pocket`.
**Resolution:** mission success is false, the preparation minimum fails, and
the operation enters `RECOVER`. The survivor takes `break-contact` to its
ordinary intercept line. The destroyed slot later respawns on baseline. The
cooldown begins only after survivor release.
**Why this is intelligent:** the mind neither attempts a half-ambush nor
commandeers a defender.

#### Case 2 — Rear Hook loses a Towline on the successful strike

**State:** `COMMIT`; both Towlines attack the carrier.
**Hostile event:** one Towline is destroyed on the same authoritative tick its
attack forces the Core loose.
**Resolution:** success is evaluated before participant feasibility, so the
operation records success and enters its success recovery. The survivor
screens the loose Core for the bounded recovery window, then extracts; an
unclaimed baseline recovery-qualified body may collect it.
**Why this is intelligent:** the achieved objective is not relabelled a failure,
but success does not keep a dead operation attacking indefinitely.

#### Case 3 — Rear Hook loses the carrier under smoke

**State:** `COMMIT`; the enemy carrier was seen in `north-return`.
**Hostile event:** smoke breaks observation before contact.
**Resolution:** the interceptors may continue toward the recorded last-seen
position only for the authored two-tick freshness budget. If the carrier is
not reacquired, the target becomes `unknown`, the operation aborts, and both
extract. They never track the hidden carrier.
**Why this is intelligent:** short causal memory supports prediction without
becoming omniscience.

#### Case 4 — Lantern Sweep reacts to a false ambush signal

**State:** two interception-capable enemy bodies have been unobserved for ten
ticks, so the carrier, Lantern, and screen prepare at the risk fork.
**Hostile event:** there is no ambush; the opponent deliberately stayed out of
view to induce caution.
**Resolution:** after five ticks of complete route coverage with no threat,
the ordered `primary-return` branch commits once. The carrier proceeds and the
Lantern releases. Edge re-arm prevents another probe while the original
absence condition remains continuously true.
**Why this is intelligent:** the rule makes a rational but costly decision;
the opponent can exploit its risk tolerance, and the mind does not loop.

#### Case 5 — Lantern dies before either route is justified

**State:** `PREPARE`; carrier and Lantern are essential, the screen came from
a replaceable pool.
**Hostile event:** the Lantern is destroyed after two scan ticks.
**Resolution:** neither commit branch is justified and the essential
preparation task is gone. The play enters `RECOVER`: the carrier immediately
takes the declared conservative return while the screen remains with it. The
Lantern's future respawn uses baseline.
**Why this is intelligent:** the carrier does not wait for a twenty-tick
respawn or proceed as though an unseen route had been cleared.

#### Case 6 — Both Lantern branches appear true on one tick

**State:** the primary route has been fully clear for four ticks.
**Hostile event:** on the fifth tick that route remains fully clear while a
threat is revealed in the adjacent flank risk zone. The primary-clear and
broader threat branches therefore both become true.
**Resolution:** branches are ordered with `alternate-return` first. The first
true branch wins and remains fixed. The trace records why `primary-return` was
not selected.
**Why this is intelligent:** danger wins by authored priority, not incidental
collection order, and the route cannot oscillate afterward.

#### Case 7 — Birth Rotation loses one reserve during preparation

**State:** `PREPARE`; two bodies were selected from a replaceable three-body
pool.
**Hostile event:** one selected body is destroyed.
**Resolution:** the third candidate is selected only if its current shortest
legal travel estimate still reaches the release line before the unchanged
deadline and it is not now carrying a Core or claimed elsewhere. Otherwise the
rotation aborts. Once the two survivors cross the release line and commit,
there is no further substitution.
**Why this is intelligent:** preparation can adapt, while combat cannot produce
an unlimited reinforcement cascade.

#### Case 8 — Emergency Handoff conflicts with Birth Rotation

**State A:** Birth Rotation is preparing and has claimed the only healthy
handoff receiver.
**Event:** the carrier reaches the public low-hull emergency condition.
**Resolution A:** preparation is preemptible. Rotation releases that receiver,
then reselects a feasible pool member or aborts without resetting its deadline.

**State B:** the rotation has already committed.
**Resolution B:** the handoff may claim the receiver only if the rotation card
declares that carrier emergency as an abort. Otherwise it remains armed until
release. The editor flags this overlap before save.
**Why this is intelligent:** commitment matters, and an emergency path is
authored and inspectable rather than magical.

### 3. Additional failure policies frozen by the round

- A blocked route first replans inside its authored corridor. If it cannot
  meet the unchanged deadline after the route-failure budget, it uses the
  named fallback or aborts.
- An unavailable preferred signature falls back to legal mission-preserving
  actions. Waiting solely for cooldown is invalid.
- A body unexpectedly acquiring a Core invokes its task's Core-safe release;
  it is never casually dragged into a non-carrier operation.
- A fully observed empty zone is `true` for “clear”; a partially unseen zone is
  `unknown`. An explicitly authored “body unobserved for N ticks” remains a
  legitimate absence signal.
- Recovery has a deadline. When extraction is impossible, the body resumes
  the nearest legal baseline intent instead of extending the failed operation.

### 4. Implementation boundary

The first prototype should implement only:

1. one complete baseline per body;
2. the frozen operation state machine and strategy trace;
3. Rear Hook with actor loss, last-seen expiry, and physical recovery;
4. Lantern Sweep with a preparation pool and two one-time commit branches;
5. one overlapping emergency-handoff fixture to prove arbitration;
6. deterministic same-seed gambit-enabled/disabled preview runs; and
7. targeted interpreter tests for all eight cases above.

Birth Rotation is the first follow-on card after the state machine passes. A
full editor, API migration, hosted ladder use, balance tournament, and gambit
slot monetisation are explicitly outside the vertical slice.

## NEXT

After owner approval, implement the narrow versioned stock-mind prototype and
trace viewer without changing Arc Relay rules, fog, rendering, or frozen
artifacts. Stop after targeted tests and a small transparent replay comparison;
do not build the complete editor until the owner rules on whether the behavior
looks intelligent.
