# Tapline equal-return fork — map candidate

Status: **implemented and owner-promoted as Tapline-6 / hosted playlist v24**.

Public 2D review and authoritative candidate route atlas:
[All 14 own-, centre-, and enemy-Well return routes](https://database-asset-dsc-ports.trycloudflare.com/tapline-return-route-atlas.html)

## Design intent

Remove the current 14-step centre-Well-to-reactor shortcut and replace it with
two legible, materially different return choices of equal length. Centre
control should remain valuable without automatically selecting the fastest
scoring plan, while defenders can read, contest, wall, feint, or switch between
two routes rather than camping one mandatory corridor.

Scoring is measured at the exact reactor tile, not the `home-west` /
`home-east` regions sometimes described as scoring shoulders. The current
driver banks only when the loaded body's position equals its team's reactor
position (`ArcRelayActorMatchModeDriver.ApplyObjective`, around lines 696–708).

## Exact geometry delta

Use the existing 31 x 27 Tapline terrain as the baseline. Add six wall tiles
on the west-authored half:

```text
(9, 9)
(10, 9)
(11, 9)
(9, 10)
(5, 10)
(10, 11)
```

Add their exact 180-degree partners, using
`rotate(x, y) = (30 - x, 26 - y)`:

```text
(21, 17)
(20, 17)
(19, 17)
(21, 16)
(25, 16)
(20, 15)
```

The additional `(10,9)` / `(11,9)` pair, and its rotated
`(20,17)` / `(19,17)` pair, fills the complete otherwise-pointless interior
dead-end branches created by the shortcut seal. Filling only each tip merely
moves the dead end inward by one tile.

No existing wall is removed. Wells, reactors, spawn areas, and all other
landmarks stay fixed.

## Pinned route measurements

All figures are corner-strict movement steps with terrain only; body traffic
is ignored. West-team coordinates are shown. East-team results are identical
under the map's 180-degree rotation.

| Route | Current Tapline | Candidate |
|---|---:|---:|
| own Well `(11,6)` -> reactor `(2,5)` | 12 | 12 |
| centre Well `(15,13)` -> reactor | 14 | **19** |
| enemy Well `(19,20)` -> reactor | 21 | **25** |

The intended practical centre returns are both exactly 19 steps. The atlas
also pins the longer centre-origin families so every purposeful return family
is reviewable from every Well:

| Family | North | South | Definition |
|---|---:|---:|---|
| practical equal fork | **19** | **19** | Through the own-Well room or lower wing. |
| middle-gate committed shadow route | **28** | **44** | Enter the outer shadow lane at its middle gate, traverse it to the own-side gate, then reach the reactor. |
| full committed shadow route | **47** | **65** | Enter at the enemy-side far gate, traverse the outer lane to the own-side far gate, then reach the reactor. |

For an enemy-Well Core, the useful route-family measurements are:

| Family | Exact candidate measurement | Definition |
|---|---:|---|
| absolute shortest | **25** | Unconstrained shortest legal path from enemy Well to own reactor. |
| via the exact centre Well and either equal fork | **26** | 7 steps to centre + either 19-step centre return. |
| middle-gate committed shadow route | **35 north / 41 south** | Enter the outer shadow lane at its middle gate, traverse it to the own-side gate, then reach the reactor. |
| full committed shadow route | **52 north / 58 south** | Enter at the enemy-side far gate, traverse the outer lane to the own-side far gate, then reach the reactor. |

“Longest” is otherwise undefined because a unit may loop indefinitely. The
52/58 figures are the longest purposeful simple route family defined by a full
far-gate-to-far-gate perimeter traversal, not a graph-theoretic longest path.

## Validation already performed

The candidate was checked with the same topology rules as
`scripts/arc-relay-tapline-build.py`:

- exact 180-degree terrain symmetry;
- every protected landmark remains open;
- all floor remains connected;
- no visually false diagonal corner-touch passages;
- no meaningful cut vertex under the existing two-tile-stub allowance;
- no candidate-created interior dead-end branch beside either own-side Well;
- all 14 required own-, centre-, and enemy-Well route IDs are present;
- both intended centre returns pin at 19 corner-strict steps.

This is geometry validation, not balance evidence. It does not establish that
19 is the correct live-game distance or that both routes have equal tactical
risk under bodies, deployments, sightlines, and combat.

## Promotion boundary and review gate

The fork ships as the new immutable `arc-relay-tapline-02` map and `tapline-6`
profile. Tapline-5 and hosted v23 remain immutable history. Focused engine tests
pin the wall delta, rotation, topology invariants, and every route measurement;
hosted playlist v24 starts a fresh rating book because the map changes gameplay.

Before a balance or ship verdict, run candidate-native sheets through the
normal trace-first phone exhibit. Specifically inspect congestion at both
19-step branches, Mason barrier leverage, loaded-carrier right-of-way,
interception windows, and whether the 25-step enemy-Well return remains worth
its 4 charge under the 1/2/4 economy.
