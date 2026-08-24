# Rail Shuttle Eclipse — Broadloom ladder rung 8

`rail-shuttle-eclipse-v1` is admitted above all seven prior Broadloom-7
ladder sheets. It wins every fixed six-game mirrored series: 41–1 across the
42 direct admission games. This is a fixed-set strategy result, not a
population-balance conclusion.

## Doctrine

Rail Shuttle preserves the successful seven-centre / one-home allocation but
changes both the roster and who is allowed to peel for low-value Cores:

- **North battery:** Kestrel, Relay and Mortar close from above. The Mortar
  keeps two plotted Falling Star opportunities.
- **South mine:** Repulsor, Patchbay, Longshot and Minesmith close from below.
  Repulsor and Longshot hunt only Cores worth at least two; Patchbay and
  Minesmith remain opportunistic while staying members of the same formation
  and pickup-phase escort group.
- **Home shuttle:** a second Relay holds the own Well, banks its steady
  one-value supply, and uses ordinary Smart Arc Toss custody.

Roster:
`Kestrel · Relay · Mortar · Repulsor · Patchbay · Longshot · Relay · Minesmith`.
It uses seven classes and the one permitted duplicate wildcard. The duplicate
Relay has a distinct home-custody job rather than duplicating the centre role.

## Admission record

Profile: `broadloom-7`. Cells, always in this order:
`w-9001 w-9002 e-9005 e-9009 w-9012 e-9004`.

| Opponent | Series | Per-cell Rail Shuttle outcomes |
|---|---:|---|
| Loomsplit | **6–0** | W353, W369, W272, W323, W348, W272 |
| Loomshears | **6–0** | W258, W298, W320, W323, W280, W324 |
| Crosscut | **6–0** | W269, W271, W262, W240, W277, W324 |
| Sixfold Pincer | **5–1** | W265, W302, W320, W326, L363, W324 |
| Shrouded Lattice | **6–0** | W320, W268, W268, W323, W265, W324 |
| Eclipse Lattice | **6–0** | W264, W314, W320, W269, W322, W324 |
| Sevenfold Eclipse | **6–0** | W289, W314, W260, W323, W322, W324 |

All 42 replays ended by reactor destruction and passed movement sanity plus
felt-degeneracy eligibility. Tactic conformance produced 1,879 passes, zero
deviations and five notes. The notes are only defensive-leash observations
for the home Relay: no excursion beyond the ordinary Hold allowance, and a
maximum measured excursion of one tile.

## What the iteration proved

The initial unrestricted sheet already beat Sevenfold 5–1. A whole-group
value filter fixed its lone loss but removed Patchbay's useful support peel in
another cell. Moving Patchbay to a different Group restored that behavior but
severed its pickup-phase escort relationship and created a different loss.

The admitted answer does not branch on seed, side, or a hidden timer. Phase
Core intent may optionally select Group members. Repulsor and Longshot receive
the 2+ filter; Patchbay and Minesmith retain the Group's ordinary hunt. The
materializer emits two ordinary per-role Instruction cohorts inside the same
phase wrapper. Formation, posture, custody, executor priority and lifecycle
remain unchanged. This is the smallest vocabulary that expresses the actual
tactical distinction.

Several timing-based drafts were rejected. Counting repeated own-Well banks
through empty carry-forward phases changed pickup-phase custody boundaries and
regressed otherwise winning cells. A value-gated transition also exposed a
tooling error: replay `chargePips` is the Reactor's post-bank partial charge,
not the Core's awarded value. The tactic reader now resolves the authoritative
1/2/4 award from source Well and scoring team and prints both facts explicitly.

## Authoring and depth findings

- Player Tactics now expose only mirrored `own`, `centre`, `enemy` Well choices
  and `own`, `enemy`, `either` Workshop choices. Old west-authored
  `north`/`south` values migrate to `enemy`/`own`; global room ids remain only
  in engine and replay evidence.
- Optional bot selection on a phase Core objective is useful precisely because
  Group membership owns formation and custody. It should stay optional and
  map-driven; it is not a reason to reintroduce visible roles or subgroups.
- The fixed ladder now demonstrates class and signature counterplay, but its
  macro warning remains. The top two rungs both allocate seven bodies near
  centre and one reliable home harvester. Workshops, outer routes and direct
  enemy-Well pressure still have not produced the strongest general-purpose
  answer. The next rung should attempt a genuinely different allocation before
  further polishing this shell.
- Sixfold's `w-9012` win is healthy counterplay. Admission requires winning
  every fixed mirrored series, not erasing every cell.

## Reproducible artifacts

- Tactics source:
  `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/tactics/rail-shuttle-eclipse-v1.json`
- Tactics SHA-256:
  `ec96f4515ee4919f708e5b49a90935ee6275e834bad14ba61f0e122d9ddf058c`
- Materialized Orders SHA-256:
  `71df51128a597bf7c5281e11fc97864f8cfffdee2faf96aa242c56023fbc4bc1`
- Map-plan SHA-256:
  `6c1e0f4270e0fdcd14a1ef4476c1937ccc2e15bd4f896d78812175942d9a26a2`
- Linked package/content hash:
  `516ebf82f7caadfbcae48c3d0f67b11cf89d2f0cc3ad15c963d378480936e21a`

The seven matchup galleries are collected in the public
[Rail Shuttle Eclipse replay gallery](https://blond-auburn-administrators-col.trycloudflare.com).

The sheet is available in the owner's local account database as Tactical
Sheet and paired, non-ranked Arc Relay Entrant
`7169147c-81d9-4f71-aace-4762df2cfe49`.
