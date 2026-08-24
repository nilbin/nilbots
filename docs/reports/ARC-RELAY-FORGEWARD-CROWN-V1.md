# Forgeward Crown — Broadloom ladder rung 10

`forgeward-crown-v1` is admitted above all nine earlier Broadloom-7 ladder
sheets. It wins every fixed six-game mirrored series: 48 wins and six losses
across 54 admission games. Every replay ended by reactor destruction, tripped
no registered felt-degeneracy bar, and contained no unresolved-contact streak.

This rung responds to a weakness in the preceding ladder rather than hiding
it: most of rungs 4–8 are refinements of one centre-concentration family, and
rung 9 still falls back to that shell. Forgeward is the first entrant to use
an observed opening to select between an enemy-return trap and a genuinely
three-theatre economy plan. It is still a fixed-set result, not proof of a
healthy population meta.

## Doctrine

Forgeward fields eight different classes:

`Kestrel · Relay · Longshot · Towline · Patchbay · Nest · Repulsor · Hush`.

Its six-group opening is a public reconnaissance commitment, not an opponent-
id switch.

- Kestrel and Relay take the outer corridor toward the enemy +4 Well.
- Longshot, Towline, Patchbay, Nest and Repulsor assemble distinct posts across
  the enemy-side return throat; Hush takes a plotted forward Null Field mark.
- If two visible enemies defend the enemy-Well ward, the sheet aborts the
  probe into **Barbed Return**. Two bodies pin the +4 Well while six distinct
  jobs form the return trap: Rail overwatch, Hook, sustain, Sentry, Burst and
  Null Field.
- If the enemy Core becomes available without a four-body visible centre
  concentration, it selects **Forgeward Crown**. Towline harvests the own +1,
  Repulsor contests the centre +2, Kestrel and Relay raid the enemy +4 by the
  outer corridor, Patchbay and Nest guard home, Hush controls centre, and
  Longshot trains twice at the enemy Workshop before returning as a level-2
  home overwatch.
- The opening also contains an all-in continuation for an exposed +4 Well
  combined with a four-body centre mass. No fixed admission opponent produced
  that exact public state, so this continuation is retained but classified as
  unproven—not counted as a replay-proven third branch.

All places are authored with mirrored `own`, `centre`, and `enemy` vocabulary.
No sheet id, seat, seed, hidden state, or global north/south alias selects the
response.

## Admission record

Profile: `broadloom-7`. Cells, always in this order:
`w-9001 w-9002 e-9005 e-9009 w-9012 e-9004`.

| Opponent | Series | Per-cell Forgeward Crown outcomes |
|---|---:|---|
| Loomsplit | **5–1** | W242, W269, L290, W224, W227, W261 |
| Loomshears | **6–0** | W234, W328, W245, W328, W309, W391 |
| Crosscut | **5–1** | W311, W434, W179, W179, L251, W305 |
| Sixfold Pincer | **5–1** | W480, W312, W320, W320, W237, L459 |
| Shrouded Lattice | **4–2** | W435, W366, W367, L343, W456, L341 |
| Eclipse Lattice | **6–0** | W378, W370, W379, W387, W443, W361 |
| Sevenfold Eclipse | **6–0** | W330, W306, W484, W449, W185, W426 |
| Rail Shuttle Eclipse | **6–0** | W180, W195, W353, W288, W211, W360 |
| Shadow Dividend | **5–1** | W234, W198, W259, W208, W199, L227 |

The six losses are retained as counterplay. Admission is a winning mirrored
series against every fixed predecessor, not a requirement to sweep every
cell.

## Replay proof of authored behavior

The Loomsplit `w-9001` cell proves the distributed Forgeward branch.

- Empty Throne opens at t0 and aborts on its second public observation row at
  t51: the enemy Core is loose while fewer than four visible enemies occupy
  the authored centre zone.
- Kestrel banks the enemy +4 at t95, Repulsor banks the centre +2 at t116, and
  Towline banks own +1 Cores at t92, t159 and t242.
- Longshot completes two consecutive enemy-Workshop channels at t164 and
  t179. The exact level-2 veterancy condition advances the Tactic at t179 and
  sends Longshot back to its home overwatch.
- Both outer raiders complete patrol laps with zero material per-leg route
  deviation. The team wins 3–1 Pulses at t242.
- The conformance reader records 55 passes and three deviations. Two are home
  guards still fighting away from their posts when the match ends; one is the
  level-2 Longshot taking a legal Hold fight two tiles beyond its nominal
  leash. Phase choice, Workshop channels, veterancy, patrol, custody filters
  and all five banks agree with the document.

The Shrouded Lattice `w-9001` cell proves the defended-Well response.

- The two-enemy ward observation aborts the probe at t33 and immediately
  starts Barbed Return.
- Every one of its eight exact posts is reached during the match. The six
  return-clamp jobs repeatedly fight from the enemy-side throat while the two
  Well pins occupy the +4 approach.
- Five centre +2 conversions plus one own +1 conversion win 3–2 Pulses at
  t435. The result is not a disguised enemy-Well race.
- The four conformance deviations belong only to opening posts pre-empted by
  the deliberate t33 abort. The active Barbed Return has no post, posture,
  custody, bank, branch, or map-binding deviation.

Materialization is byte-identical on a clean rerun. Native replay hashes also
repeat exactly for one replay from each proven branch.

## More than three Groups

The underlying Tactics grammar, compiler and editor already support one to
eight Groups. A stale wizard sentence still said “up to three”; it now states
the actual eight-Group ceiling and its UI assertion was updated.

Forgeward uses five Groups in Barbed Return and six in each other Tactic. They
are not cosmetic subdivisions: separate Groups own the rail line, Hook,
deployments, Null Field, Workshop channel, each Core source, and outer patrol.
This is the good reason to exceed three—independently governed map intents—not
an invitation to make every bot a hand-configured singleton. Recipes should
still begin with one to three Groups and let players split only when the map
idea calls for it.

## Mechanics and tooling addressed during this rung

### Sentry expiry no longer corrupts replay chronology

A Nest can stand on its non-blocking Sentry and begin an ordinary gun windup
from the same tile. When the Sentry expired, chronology validation could
reclassify the unchanged gun tell from deployment-owned to session-owned and
fault a deterministic match. The lifecycle comparator now accepts raw strike
equality first and otherwise removes only lines attributable to the exact
Rail, Hook or Sentry operation that changed. Focused tests cover both an
unchanged colocated gun tell and removal of the Sentry's own autonomous tell.

### The replay reader made the adaptive claim auditable

The compact tactic reader joins phase markers, authored condition rows,
Workshop events, veterancy, patrols, posts and banks. It exposed that two
branches are proven and the proposed all-in continuation is not. That prevents
a complex source document from being mistaken for observed strategic depth.

## Authoring friction and missing depth

- **Workshop readiness is asymmetric.** It is available to phase transitions
  but not Tactic aborts. Forgeward therefore uses the enemy Core's public
  availability as its decision clock. A player should be able to make the
  same Workshop fact in both places when the observation is public.
- **Two counts of the same condition kind cannot form one row.** The natural
  read “four enemies at centre AND two at their Well” cannot contain two
  `enemiesAt` objects. Staging plus abort expresses the decision, but a small
  multi-instance condition editor would be clearer than hand-built temporal
  choreography.
- **Carry-on is source-verbose.** A `none` job inherits map geometry, but the
  phase still reauthors signature and Core policy where those must remain
  active. The editor should present this as inherited behavior with explicit
  overrides, not make players understand generated leaf Instructions.
- **The direct materializer can accept an invalid wall post.** Runtime then
  faults when it tries to execute it. The map-first editor catches normal
  cases; CLI materialization should apply the same traversability validation.
- **Conformance overstates pre-empted staging deviations.** A post never
  reached because an authored abort fired is useful evidence, but should be
  labelled `PREEMPTED` rather than mixed with a live Tactic's failed promise.
- **WASM is currently a baseline blocker.** The unmodified Loomsplit mirror
  and Forgeward both exhaust the tactical mind's per-decision fuel on
  Broadloom-7 around t16–t30. This is not caused by Forgeward's extra Groups,
  because the old one-sheet mirror fails too. Native execution remains the
  ladder's deterministic evaluation path until the sandbox budget/runtime
  regression is fixed.
- **Guarding and generic confinement metrics need semantic separation.** The
  official parked-unit bar remains clean in all 54 cells. The stricter radius-2
  audit still flags some intentionally posted home defenders after 60 ticks.
  A future audit should distinguish an authored live guard from a bot trapped
  in a movement loop without weakening the official anti-passivity bar.

## Strategic-depth verdict

Forgeward is a meaningful improvement over the visually similar early ladder:
it uses observable state, three Core values, Workshops, veterancy, patrols,
deployment control and two proven spatial allocations. Its clean theater
entropy ranges from 0.645 to 0.997 across the 54 fixed cells; no match collapses
into the registered team-statue, team-dance, sustained-passivity, or parked-
unit failures.

It still does not prove the game has a durable meta. Barbed Return places six
bodies in one enemy-return throat, so concentration remains powerful even when
the objective and geometry differ from the old centre march. The monotonic
“beat everything before it” gate also selects generalists. The next entrant
should be a genuinely different family—mobile interception, deployment
territory, or veterancy tempo—and should be evaluated as a matchup matrix as
well as a new rung. If Forgeward's adaptive generalism crushes every such
specialist, that is a balance finding.

## Validation and reproducible artifacts

- Admission: 48–6 across 54 fixed mirrored matches; every series at least 4–2.
- 54/54 ended by reactor destruction with both teams eligible.
- 54/54 tripped no registered Broadloom-7 felt-degeneracy bar.
- 54/54 contained zero unresolved-contact streaks.
- Engine suite: 1,621/1,621 passed.
- Tactics materializer suite: 95/95 passed.
- Web suite: 656/656 passed.
- Tactics SHA-256:
  `7a7d94aa1ec8f718c090e098ded86aa14cf49f76042fb4a17763af626798295f`
- Materialized Orders SHA-256:
  `0139b8c52ae5dd09d3a02708fd62addab27c69b9d12e8244a279b4cb6d4341ca`
- Map-plan SHA-256:
  `f6b2631a15b2608fbf5054b092e7a9e218f55858d90beba9907756c50c480beb`
- Linked package/content hash:
  `7856d50c3be72ccbfec05bf42f0cf085529a18b14d9ab41b25436494a2997901`
- Tactics source:
  `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/forgeward-crown-v1.json`

The sheet is available in the owner's local account database as paired,
non-ranked Tactical Sheet and Arc Relay Entrant
`875adf14-55cb-49b5-a872-3fec76e8c716`.
