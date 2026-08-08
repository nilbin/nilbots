# Threefold Pulse depth prototype — owner brief (2026-08-05)

Owner-supplied brief, binding for the Threefold Pulse goal. The /goal text
references this file; where they differ, this file wins.

Continue in /Users/sebastian.lind/hobby-projects/nilbots-wt/arc-strategy-ladder
on branch codex/arc-strategy-ladder.

This is a THREEFOLD PULSE depth prototype. Test one mechanic in isolation: do
not combine it with Ripening, Migration, Conduits, Ledger, or another proposed
depth mechanic. Preserve all accepted profiles, frozen artifacts, evidence, and
canonical hashes. Build the experiment from the latest validated Forward Combat
03 rules foundation without silently promoting it to the hosted/current
profile.

## MECHANIC

1. Every Core has an immutable origin theater: North, Center, or South,
   determined by its Well.
2. Each team's reactor has three corresponding sockets.
3. A team triggers the existing Pulse only after banking one Core from each
   origin, in any order.
4. Banking fills only the matching socket. A Core whose socket is already
   filled cannot be consumed or advance the Pulse; it remains physical and
   contestable.
5. When all three sockets are filled, the existing Pulse occurs and that
   team's sockets reset for its next cycle.
6. Filled sockets are persistent and cannot be stolen or erased. Do not add
   socket sabotage in this experiment.
7. Preserve the existing Core lifecycle, Pulse effect, victory condition,
   combat, classes, movement, map, and respawn rules except where the
   Threefold requirement strictly necessitates a change.
8. Any body may handle any origin Core, and bodies may rotate or cross between
   theaters. Do not encode rigid North/Center/South assignments into the
   rules.
9. Expose Core origin and both teams' socket state deterministically to the
   stock mind, sheet/gambit runtime, replay, diagnostics, and viewer. The
   information must be causal at the playhead.
10. Update stock behavior competently for Threefold. It must understand
    required origins, reject useless duplicate-bank attempts, rotate support,
    and avoid indefinitely holding or abandoning a currently unbankable Core.

## DEPTH QUESTION

The purpose is to determine whether Threefold Pulse makes substantially more
of each eight-body team contribute meaningful objective value while preserving
flexible strategy.

Pre-register an auditable "direct objective contribution" measure before
evaluating results. Count a body only when it performs meaningful work
connected to a required Core, such as carrying/banking it, causing or
preventing a pickup/drop/steal, fighting or using a signature in an active
Core contest, or materially supporting a carrier. Mere proximity, waiting,
formation membership, or walking through a theater must not qualify.

Primary target: the median completed Pulse cycle should involve at least 6 of
8 bodies per team in direct objective work. Report the distribution and the
contribution type for every body; do not hide specialization behind a team
average.

## STRATEGY AUDIT

Author equally competent evaluation-grade sheets/doctrines demonstrating at
least:

- balanced three-theater allocation;
- deliberate overload followed by rotation;
- weak-side denial with a fast cross-map response;
- Core-origin prioritization based on the current socket state;
- protection of two completed sockets while contesting the missing origin;
- interception of an opponent's missing-origin carrier;
- a deceptive commitment or feint that causes an opponent to rotate
  incorrectly;
- a counter to the overload strategy.

These must use the sheet/gambit grammar rather than hard-coded map coordinates
wherever a semantic selector can express the intent. Record any strategy that
the current grammar cannot express cleanly.

Test whether `3-3-2` becomes an automatic solved allocation. Threefold fails
the depth objective if one fixed allocation consistently dominates overloads,
rotations, and adaptive responses. Likewise, no class, composition, movement
pattern, or spawn orientation may explain the result by itself.

## EVALUATION

Run same-cohort, mirrored-orientation comparisons across enough deterministic
campaign seeds to distinguish a repeatable interaction from seed or side luck.
Include:

- the competent Threefold stock baseline;
- each new strategic family against baseline;
- relevant strategy-vs-counter matchups;
- a round-robin among the viable Threefold strategies;
- comparison with the latest validated pre-Threefold cohort for pacing,
  passivity, scoring concentration, and body participation.

Report win records, margins, Pulse timing, per-origin bank timing, socket
completion order, rotations between theaters, body-contribution
distributions, class usage, and side/seed splits.

The frozen felt-degeneracy and cohort-eligibility bars remain binding. Never
adjust a detector to admit Threefold. Explicitly check carrier stalls,
abandoned Cores, duplicate-bank loops, passivity, formation freeze, handoff
ping-pong, uncontested waiting, and games made unwinnable by missing-origin
deadlock. Any entrant that trips a bar is repaired or excluded and disclosed.

## GALLERY

Build a concise review gallery from eligible matches using the actual
Threefold rules and current 3D presentation. Prefer tactically legible replays
rather than random samples.

For every replay, show:

- both entrants and final score/result;
- the strategy being demonstrated;
- its intended trigger and coordinated response;
- the opponent's strategy and expected counterplay;
- which Core origins and socket states matter;
- what the viewer should watch for.

Do not make the gallery index outcome-blind. I want to know whether the
showcased tactic genuinely succeeded and what I am watching.

## VALIDATION AND REPORT

All existing canonical golden hashes and prior profile fingerprints must
remain byte-identical. Run the full relevant engine, tactical-mind, web,
build, DocDrift, and replay verification suites. Record exact runtimes and
distinguish in-process screening from authoritative WASM results.

The goal is met when docs/reports/ARC-RELAY-THREEFOLD-PULSE-PROTOTYPE.md
exists and includes:

- the exact implemented rules;
- observation/replay/viewer changes;
- before/after body-participation evidence;
- strategy and counter-strategy results;
- side, seed, class, and composition checks;
- degeneracy-bar results;
- whether `3-3-2` became solved;
- grammar limitations discovered;
- canonical-hash proof;
- the gallery link and replay descriptions;
- a clear recommendation to adopt, revise, or reject Threefold Pulse.

Do not claim the game is deeper or more fun merely because more bodies moved.
The evidence must show meaningful choices, viable opposing strategies,
readable counterplay, and direct objective contribution from substantially
more of the team. Post the report and stop for owner review.

## Owner amendment (2026-08-05, in-session)

The eight pre-specified strategy families are demoted from an authoring
queue to a coverage checklist. Strategies are authored clean-slate, one at
a time, each against a margin bar; the first sheet's goal is solely to
beat the competent Threefold stock baseline by a decisive margin. The
3-3-2 solved-allocation test runs organically: the best fixed-allocation
sheet is built first, and subsequent challengers try to beat it — if none
can, that is the verdict. The final report audits the checklist: which of
the eight concepts emerged, which proved irrelevant to real Threefold
dynamics, and which the grammar could not express.
