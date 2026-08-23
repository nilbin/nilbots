# Sixfold Pincer — a roster-diverse answer to the Broadloom ladder

`sixfold-pincer-v1` is the fourth sheet in the Broadloom counter sequence. It
beats `loomsplit-v1`, `loomshears-v1`, and `crosscut-v1` without reusing their
shared eight-class roster. It is a deterministic counter exhibit, not a claim
that the squad is globally balanced.

## Tactical idea

Sixfold Pincer uses seven distinct classes and the one-duplicate wildcard:

| Slot | Class | Build | Tactical reason |
|---:|---|---|---|
| 0 | Kestrel | aggressive | Fast north-arm closer and opportunistic carrier. |
| 1 | Relay | balanced | Flexible short-range arm member and Arc Toss carrier. |
| 2 | Towline | aggressive | Medium-range pressure behind the north pair; Tractor Hook remains a carrier-specific threat. |
| 3 | Repulsor | durable | South-arm front line and Kinetic Burst displacement. |
| 4 | Patchbay | durable | Keeps the slower south arm intact through the centre collision. |
| 5 | Mason | durable | Shapes the south approach with Hardlight Block. |
| 6 | Nest | durable | Sentry-backed own-Well guard and regular home carrier. |
| 7 | Mason | durable | Second home defender and the roster's single duplicate wildcard. |

The operation has two phases:

1. **Set the jaws.** Three mobile bodies take north staging posts, three
   durable bodies take south staging posts, and the two-body home ward sets up
   at the own Well. The phase advances only when the whole formation is ready.
2. **Close the jaws.** Both arms enter centre from different vertical angles
   with aggressive travel and destination posture. They hunt centre Cores and
   use ordinary escort custody. Nest and the second Mason retain the own-Well
   cadence.

This is not an eight-body centre march. Six bodies contest centre, but they
first split into two readable formations and arrive from distinct approaches;
two remain committed to home defence and scoring.

## Why it answers the three predecessors

- **Crosscut** invests one body in training and places five at centre. Sixfold
  gives up training, releases six staged bodies at ticks 23–24, and attacks
  that five-body wedge from two directions while retaining the same two-body
  home allocation.
- **Loomshears** spends two bodies patrolling a Workshop that Sixfold never
  uses and holds centre with four. The pincer converts that allocation cost
  into sustained centre superiority.
- **Loomsplit** divides bodies among training, an outer raid, centre, and home
  screening. Sixfold's home ward repeatedly answers the two raiders while the
  six-body pincer overwhelms the smaller centre screen.

Across the 18-match exhibit, the candidate issued 73 Kinetic Bursts, 202
Repair Beams, 138 Sentinel Seeds, 156 Hardlight Blocks, and 30 Arc Tosses.
Towline did not find a legal Tractor Hook opportunity: Smart Hook targets an
enemy carrier, and none entered its eligible sightline. The class still
contributed its aggressive medium-range body to the north arm, but its
signature is not part of the proven win mechanism.

## Six-cell exhibits

Profile: `broadloom-3`. Standard review cells; `w-*` seats Sixfold west and
`e-*` seats it east. Every match ended by reactor destruction, passed the raw
movement-sanity audit, and passed the felt-eligibility gate.

### Versus `crosscut-v1`

| Cell | Result | End | Pulses | Integrity | Charge |
|---|---:|---:|---:|---:|---:|
| `w-9001` | win | 354 | 3–2 | 1–0 | 0–2 |
| `w-9002` | win | 357 | 3–2 | 1–0 | 0–1 |
| `e-9005` | win | 363 | 3–2 | 1–0 | 0–1 |
| `e-9009` | win | 311 | 3–1 | 2–0 | 1–1 |
| `w-9012` | win | 375 | 3–2 | 1–0 | 0–1 |
| `e-9004` | win | 363 | 3–2 | 1–0 | 0–1 |

### Versus `loomshears-v1`

| Cell | Result | End | Pulses | Integrity | Charge |
|---|---:|---:|---:|---:|---:|
| `w-9001` | win | 273 | 3–1 | 2–0 | 0–0 |
| `w-9002` | win | 360 | 3–2 | 1–0 | 0–1 |
| `e-9005` | win | 363 | 3–2 | 1–0 | 0–1 |
| `e-9009` | win | 293 | 3–1 | 2–0 | 0–1 |
| `w-9012` | win | 363 | 3–2 | 1–0 | 0–0 |
| `e-9004` | win | 369 | 3–2 | 1–0 | 0–1 |

### Versus `loomsplit-v1`

| Cell | Result | End | Pulses | Integrity | Charge |
|---|---:|---:|---:|---:|---:|
| `w-9001` | win | 261 | 3–1 | 2–0 | 0–0 |
| `w-9002` | win | 273 | 3–1 | 2–0 | 0–0 |
| `e-9005` | win | 338 | 3–2 | 1–0 | 0–2 |
| `e-9009` | win | 366 | 3–2 | 1–0 | 0–0 |
| `w-9012` | win | 268 | 3–1 | 2–0 | 0–0 |
| `e-9004` | win | 383 | 3–2 | 1–0 | 0–2 |

Verdict: **6–0 against each predecessor; 18–0 overall in these named
exhibits**, including both seats. The close Crosscut cells are useful: the
counter wins through repeated objective conversion rather than an immediate
combat snowball.

## Authoring and checker findings

The final tactic required no new Tactics vocabulary: three whole-team groups,
two sequential phases, Posts, `groupsSet`, posture/travel/recovery, Core intent,
custody, and Smart/exact signatures express it cleanly.

The discarded enemy-Well raid exposed one executor-authoring mismatch. An
exact durable deployment previously outranked the job floor from tick zero, so
a Mason could ignore its authored `via` route to approach a distant mark.
Exact durable marks now become eligible only after the bot reaches its job
ground, then use the existing Instruction lifecycle to latch the short
deployment approach until the mark exists or a 40-tick timeout ends it. This is
not a deployment scheduler or another priority system.

The conformance reader also previously demanded literal occupancy of an exact
Post even though runtime formation readiness intentionally accepts
Chebyshev-one tolerance. It now reports exact occupancy as stronger detail but
passes legal readiness-range arrival, including the position at the phase
release boundary. Determinism remained byte-identical. Across the 18 final
replays, remaining conformance deviations are limited to five real Hold-leash
episodes in which a home defender engaged outside the authored range; they are
not phase, movement, custody, or Core attribution failures.

## Reproduction and artifacts

- Source: `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/sixfold-pincer-v1.json`
- Source SHA-256: `81dba4b6618bb2568a23a5b0e404d6a15d8a6af97b203f0865c2c6452921eb46`
- Materialized Orders SHA-256: `9cd6ace5a7635b2fef3211ecf2bb529637dea84c8bc780324633493d81f34583`
- Map-plan SHA-256: `e7cd9ccbe94f89efb781cf7715ef9344d23382ca0fa173805f7034fb37d499bd`
- Linked package/content hash: `9af94552c5ca9a6e011a8a3df0b09b378711dee7ab3c17c2d68bd40613deab77`
- Crosscut gallery:
  <https://retrieved-minds-algebra-begins.trycloudflare.com>
- Loomshears gallery:
  <https://raw-science-catalogs-copyrights.trycloudflare.com>
- Loomsplit gallery:
  <https://belkin-conversation-unified-frequently.trycloudflare.com>
- Local account row: `Sixfold Pincer`, id
  `c4c1f30c-f0ec-42ef-b41e-08418c142b14`, revision 1, ladder disabled.

Validation: multiple discarded raid diagnostics, three mirrored focused
pairs, three standard six-cell exhibits, 18 clean movement audits, 18
felt-eligibility passes, deterministic tactic conformance, 82 focused
materializer tests, and exact executable-hash parity between the reviewed and
installed sheet. All three public gallery indexes were checked at a 390×844
viewport with no horizontal overflow. A Crosscut replay loaded and advanced
from tick 0 to tick 5; the browser reported no warnings or errors.
