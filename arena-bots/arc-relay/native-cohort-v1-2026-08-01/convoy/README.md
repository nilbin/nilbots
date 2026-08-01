# ArcConvoy

ArcConvoy is coverage-cell B for the Arc Relay native cohort v1. It is one
`IGenericMindBot` instance that commands all eight live bodies for the whole
match. This is a coverage doctrine, not a claim that convoy play is optimal.

## Doctrine

The first ordered Well in the public Arc Relay binding is the main route. Two
Relays form a leapfrog chain: the current carrier returns toward its reactor,
while the other Relay moves into the nearest homeward catch pocket. When the
carrier's legality mask offers that receiver, the source submits the typed
handoff and the receiver explicitly waits. The former carrier then becomes the
next catch body.

Two Palisades occupy mirrored upper/lower positions ahead of the package and
raise Prism Walls only when nearby enemies or incoming projectiles make the
screen relevant. Patchbay prioritizes damage on the carrier, pickup, screens,
and catcher. Hush remains with the package and opens Null Field only at close
contact or around a visible hostile signature. A threatened low-hull Relay may
use an available Arc Toss target that shortens the remaining reactor route.

The convoy deliberately concedes breadth. Towline contests the first remaining
Well and Lantern the second; each stages homeward of its assigned Well, attempts
the visible or last-seen loose Core, and returns any acquired wing Core directly.
Towline hooks a visible carrier or lane threat, while Lantern flares only when
the Well still owns an unresolved Core that is not visible. Every gun prefers a
visible enemy carrier, so interception supports delivery rather than becoming
reactor camping.

## Declared composition

| Count | Class | Stable job |
| ---: | --- | --- |
| 2 | Relay | main pickup/carrier and leapfrog catcher |
| 2 | Palisade | upper and lower Prism screens |
| 1 | Patchbay | convoy medic |
| 1 | Hush | close suppression escort |
| 1 | Towline | first peripheral Well picket |
| 1 | Lantern | second peripheral Well picket |

This is six distinct classes and respects the public two-copy cap. The exact
slot order is recorded in `sheet.json` under the provisional
`arc-relay-evaluation-sheet-v0` schema.

## Contract discipline

- Well and reactor positions come from the Arc Relay mode binding and named map
  regions. The participant's reactor-facing assignment supplies the mirrored
  forward axis.
- Classes come from each `MindBody`; action identity comes from the rules
  catalog or the class signature catalog; action codes and typed target values
  always come from the body's current legality mask.
- Core ownership, relocation timing, Wells, reactors, and visible signatures
  come from the typed Arc Relay observation. Last-seen Core tiles are retained
  only while the public Well still reports that Core outstanding.
- Movement uses the movement action's legal projectile headings, searches the
  public map, reserves destinations across the whole mind, avoids visible
  bodies/projectiles, and refuses every visible respawn reservation.
- Stable public role tags are `main-carrier`, `relay-catcher`, `main-pickup`,
  `upper-prism-screen`, `lower-prism-screen`, `convoy-medic`,
  `convoy-suppressor`, `first-well-picket`, `second-well-picket`,
  `wing-core-return`, and `convoy-reserve`.

## Mechanical smoke

The only executed author smoke was native in-process self-play at seed
`314159`, using this project and this sheet on both participant sides. It ran
all 600 ticks with both teams eligible and therefore zero runtime faults under
the contract's zero-fault allowance. No source was tuned from its winner,
score, or tactical appearance. Canonical cohort evaluation is coordinator-owned.
