# Eclipse Lattice — Broadloom ladder rung 6

`eclipse-lattice-v1` is admitted above Loomsplit, Loomshears, Crosscut,
Sixfold and Shrouded Lattice under the promoted `broadloom-7` rules. It wins
all five fixed six-game mirrored series, uses eight different classes, and
keeps meaningful counterplay: both modern predecessors take two cells.

These are fixed-set ladder records, not a population-balance conclusion.

## Doctrine

Eclipse preserves the proven two-arm centre close and two-body home trap, but
changes the north arm from displacement to authored counter-battery fire:

- **North battery:** Kestrel and Relay provide movement/custody tempo while a
  Mortar fires at two exact positions occupied by common opposing centre
  formations.
- **South null:** Repulsor, Patchbay and Hush provide close control, sustain
  and hostile-signature suppression.
- **Home trap:** Nest and Minesmith defend and harvest the own Well with a
  sentry and two hidden Trip Nodes.

The roster has no duplicate class:

`Kestrel · Relay · Mortar · Repulsor · Patchbay · Hush · Nest · Minesmith`.

All six centre bodies close aggressively after the formation is set, use
heal-tile recovery and escort custody, with Relay first in the preferred
carrier list. The Mortar is not a runtime special case: ordinary Program
signature marks compile to ordinary signature Instructions.

## Admission record

Profile: `broadloom-7`. Review cells, always in this order:
`w-9001 w-9002 e-9005 e-9009 w-9012 e-9004`.

| Opponent | Series | Per-cell Eclipse outcomes |
|---|---:|---|
| Loomsplit | **6–0** | W254, W268, W262, W260, W259, W259 |
| Loomshears | **6–0** | W345, W333, W260, W345, W348, W260 |
| Crosscut | **6–0** | W257, W262, W271, W266, W274, W350 |
| Sixfold | **4–2** | W362, L357, W263, W345, L309, W263 |
| Shrouded Lattice | **4–2** | W281, L333, W268, W345, L348, W268 |

All 30 raw replays passed the movement-sanity and felt-degeneracy eligibility
gates. Twenty-five conformance reports contain no deviation. The other five
contain only the known Hold-checker severity seam for the home group: at most
a one-tile combat excursion. Seven zero-deviation reports carry notes from
that same checker. No authored post arrival, phase transition, custody,
delivery or core-rule promise deviated.

## What the iteration proved

The useful edge came from tactical geometry rather than a raw class swap:

1. Replacing Hush with Sunder (`sunspike-lattice-v1`) and replacing Patchbay
   with Sunder (`blackglass-lattice-v1`) each produced a perfectly seat-locked
   1–1 focused pair against Shrouded Lattice, ending at ticks 343 and 342
   respectively. A roster label alone did not break the mirror.
2. Replacing Patchbay with a plotted Mortar (`starless-lattice-v1`) punished
   the known formation and beat Shrouded Lattice 5–1, but lost too much
   sustain and only tied Sixfold 3–3.
3. Keeping Patchbay/Hush while putting Mortar in Towline's slot retained the
   artillery edge and cleared both modern gates 4–2. This is a readable
   tradeoff: formation punishment replaces pull/displacement, rather than
   simply adding another effect.

The admitted replays contain 442 public Falling Star starts from Eclipse's
Mortar. Opposing teams suffer 61 environment-attributed deaths, including 17
against Shrouded Lattice, often on or beside the centre marks. That is
consistent with visible artillery contribution, but the replay currently
does not name the responsible signature on damage/death facts; the numbers
must not be treated as exact Mortar kill attribution.

## Friction, missing depth and improvement potential

### Authoring and evidence

- Exact signature marks are strategically expressive, but authoring the
  counter required manually mirroring an opponent's post coordinates in JSON.
  The map editor should let a player inspect an opponent/sparring sheet and
  adopt or target its projected posts directly.
- `only` is too coarse for deliberate artillery. It repeatedly fires the
  authored point whenever legal, even if the player would rather express
  “fire here when an enemy was recently seen in this area.” Signature marks
  need an optional, ordinary condition gate—not a signature scheduler.
- The conformance reader validates posts, posture, transitions and custody,
  but not signature promises. It should report per mark: attempts, cancelled
  tells, resolutions, targets in footprint, damage and kills.
- Replay damage/death attribution collapses signature damage to
  `environment`. A source operation/signature id would make tactical review
  substantially less inferential.
- The direct CLI materializer still lacks the hosted save gate's real-map
  geometry validation, and the Hold checker still overstates valid one-tile
  responses. Both were already exposed by the previous rung and remain open.

### Gameplay/depth warning

Eclipse discovers real counter-formation play, but it does not solve the
macro-shape problem. The sixth rung is still six bodies converting centre and
two bodies defending/harvesting the own Well. The most successful change was
which signature the centre six carried, not where the team invested.

That is encouraging for roster and spatial counterplay, but it also sharpens
the warning: Workshops, outer routes and enemy-Well pressure still need to be
credible general-purpose investments if the long-term ladder is to branch
rather than becoming successively refined versions of the same 6+2 shell.

## Reproducible artifacts

- Tactics source:
  `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/eclipse-lattice-v1.json`
- Tactics SHA-256:
  `d79789536eba9002659e220241e0d621cd4668df41b5e4d18c9f0a0e61a3aa7c`
- Materialized Orders SHA-256:
  `c16e8e4ee9b2edac7e6b4842a6577650a9953c02d1b284cfb76af101875665ae`
- Map-plan SHA-256:
  `11c2a1ab9b1d99a1b46a6684dbd51c3a9f681172c7cfa37002e976f9b6769882`
- Linked package/content hash:
  `65ca0c904d2f8ecf81327c36975bd3583e05c0d808543765a950d999b87eb082`

The five six-cell galleries are collected in the public
[Eclipse Lattice replay gallery](https://air-dense-asked-easter.trycloudflare.com).

The sheet is also available in the owner's local account database as Tactical
Sheet and paired Arc Relay Entrant `b4037b93-be7d-4f2d-a2f5-5f2b5a5f54fa`.
