# Crosscut Doctrine — an adaptive answer to Loomsplit and Loomshears

`crosscut-v1` is the third sheet in the Broadloom counter sequence. It does
not try to guess the opponent before the match. It begins with a productive
training-and-scoring formation, then uses the public state of the chosen
Workshop to decide whether to keep investing or cut the enemy's live thread.

## Tactical idea

The lower-priority **Cross the idle loom** tactic starts every match:

- **Veteran needle:** the Longshot trains at the seat-relative enemy
  Workshop. That is the Workshop Loomshears leaves vacant while its shears
  patrol Crosscut's side of the map.
- **Centre wedge:** Kestrel, Relay, Mortar, Sunder and Patchbay take five
  aggressive centre posts with Core-hunting intent.
- **Well ward:** Mason and Palisade hold the own Well and preserve the normal
  one-value cadence.

If the Workshop stays safe, four consecutive channels raise the Longshot to
level 3. At tick 86, the tactic advances to **Pierce the Well**: seven bodies
keep their existing scoring jobs while only the veteran Longshot moves to a
post at seat-relative `(22,6)`, within opportunistic pickup range of the enemy
Well, with a Rail line through its room.

If an enemy enters the training room, `workshopContested{enemy}` aborts that
tactic. The higher-priority **Cut live thread** tactic starts on the same
public fact:

- Kestrel and Relay patrol the contested Workshop;
- Longshot, Mortar, Sunder and Patchbay hunt centre;
- Mason and Palisade continue guarding the own Well.

This is ordinary top-to-bottom tactic selection over the existing Instruction
executor. There is no reactive scheduler, reservation, atomic activation or
second parallel priority system.

## Why it answers both predecessors

Against **Loomshears**, the enemy denial pair patrols the wrong Workshop.
Crosscut therefore has seven immediately productive scoring bodies plus a
safe investment, while Loomshears has six productive bodies and a scoreless
pair. In every standard cell, Crosscut completed four training sessions from
ticks 26–85 and advanced on the level-3 condition at tick 86.

Against **Loomsplit**, both Longshots choose the same seat-relative Workshop.
Their arrival makes the room contested, so Crosscut abandons its own sunk cost
at tick 26 in every standard cell and turns into the denial allocation that
already exposed Loomsplit's training risk. The important behavior is the
switch, not merely winning the first Workshop duel.

## Six-cell exhibits

Profile: `broadloom-3`. Standard review cells, with Crosscut on west for
`w-*` and east for `e-*`.

### Versus `loomshears-v1`

| Cell | Result | End | Pulses | Integrity | Charge |
|---|---:|---:|---:|---:|---:|
| `w-9001` | win | 354 | 3–2 | 1–0 | 0–0 |
| `w-9002` | win | 299 | 3–1 | 2–0 | 1–1 |
| `e-9005` | win | 363 | 3–2 | 1–0 | 0–1 |
| `e-9009` | win | 363 | 3–2 | 1–0 | 0–1 |
| `w-9012` | win | 354 | 3–2 | 1–0 | 0–0 |
| `e-9004` | win | 329 | 3–1 | 2–0 | 1–1 |

### Versus `loomsplit-v1`

| Cell | Result | End | Pulses | Integrity | Charge |
|---|---:|---:|---:|---:|---:|
| `w-9001` | win | 266 | 3–0 | 3–0 | 0–2 |
| `w-9002` | win | 268 | 3–0 | 3–0 | 0–2 |
| `e-9005` | win | 471 | 3–2 | 1–0 | 0–1 |
| `e-9009` | win | 266 | 3–0 | 3–0 | 0–2 |
| `w-9012` | win | 276 | 3–0 | 3–0 | 0–2 |
| `e-9004` | win | 266 | 3–0 | 3–0 | 0–2 |

Verdict: **6–0 against each named predecessor in these two six-record
exhibits**, including both seats. All twelve raw replays passed the movement
sanity and felt-eligibility gates. This is deterministic counter evidence for
these authored matchups, not a population-balance claim.

The long `e-9005` Loomsplit cell is useful counter-evidence to a brittle
opening: Loomsplit eventually completed training and banked a charged enemy
Core, but Crosscut's own-Well cadence survived long enough to close at tick
471.

## Authoring and checker findings

No missing gameplay primitive blocked the tactic. Two whole-team tactics,
two phases in the baseline, a tactic-wide abort, one veterancy transition,
ordinary Posts/Patrol/Train jobs and seat-relative Workshop selection express
the whole doctrine.

The compiler deliberately restricts a Train job to one body and a tactic to
three groups. An early draft attempted a two-body training queue alongside a
centre group and a Well ward; representing that would require four groups.
The final one-ace version is easier to read and proved stronger, so neither
limit was widened for this sheet. If future tactics repeatedly need multiple
independent trainees plus two other formations, that would be evidence to
revisit the authoring cap rather than add training-specific runtime behavior.

The conformance reader currently reports “no Workshop channel session began”
as a deviation when the training tactic intentionally aborts at tick 26,
before a legal channel can begin. Those six findings are checker severity,
not failed execution. It also flags a Hold defender in a few cells for one
fight beginning one tile outside its exact post; maximum excursion was one,
within the authored short-leash behavior. The raw phase markers, authoritative
Workshop events and movement audits are consistent with the tactic.

## Reproduction and artifacts

- Source: `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/crosscut-v1.json`
- Source SHA-256: `caafdbae3e8258033a2a9a3654ba79d8eea7f3223d9d0a0f9e24fec9ebba8d9e`
- Normalized Tactics SHA-256: `f24f496131d1eab1a9f8afbd14e6dffec1708acd7a0335f7aec5709d3f0be13e`
- Materialized Orders SHA-256: `3fd28b4b3f53da43ee5a6d68a4ad8928d27c01ceb0aa7b71324fd190443211d7`
- Map-plan SHA-256: `189cddfeff5e9f903f15a497063306c5f92b92cb5aefbe35be4b83d921480fdf`
- Linked package/content hash: `381ac500cc1b1f604ee5573dce39b76622845db0457563f5ae58ecc0d8e34341`
- Loomshears gallery:
  <https://destiny-province-tackle-watson.trycloudflare.com>
- Loomsplit gallery:
  <https://burner-supervision-wax-copied.trycloudflare.com>
- Local account row: `Crosscut Doctrine`, id
  `d275d3fa-f81b-4fcf-a0ea-ce6aadb75842`, revision 1, ladder disabled.

Validation: deterministic Tactics materialization, three focused design
iterations, two mirrored focused pairs, two standard six-cell exhibits,
twelve clean raw-replay movement audits, twelve felt-eligibility passes, and
twelve Tactics conformance reports. Both public gallery indexes and one replay
from each were exercised at a 390×844 browser viewport; the pages had no
horizontal overflow, playback advanced, and the browser reported no warnings
or errors.
