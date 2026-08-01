# SplitControlMind

Coverage cell A: split-control.

SplitControlMind keeps an independent pair in each Well theater. A fourth
runner rotates from the public Well clocks, and a route-cover body follows an
active friendly carrier or reinforces the next cadence site. Loose Cores are
assigned one-to-one to available runners, so three visible pickups create
three separate jobs instead of one army-wide convergence. A blocked or lost
route therefore leaves the other theater plans intact.

## Composition

The provisional evaluation sheet declares eight distinct classes, all under
the two-copy cap:

1. Kestrel — `north-runner`
2. Palisade — `north-guard`
3. Relay — `centre-runner`
4. Minesmith — `centre-denial`
5. Towline — `south-runner`
6. Hush — `south-denial`
7. Lantern — `cadence-reserve`
8. Veil — `route-cover`

The role tags are stable, lowercase, and public. Class identity is read from
each body observation; positions, Well ordering, reactor location, home-facing
direction, action IDs/codes, movement handling, and all typed constraints are
read from the resolved contract and current legality masks.

## Doctrine details

- Center, north, and south pairs have separate goals and collision claims.
- The reserve selects its destination from `PendingCharge` and
  `NextScheduledBirthTick`, not a private timer.
- Visible loose Cores are assigned to unique runner/reserve units by theater
  ownership and distance. Remembered Core state is discarded when the public
  Well no longer reports that Core outstanding.
- A carrier independently routes to its participant-bound reactor. Core
  relocation recovery is respected through action availability and the
  observed `NextRelocationTick`.
- Guards and denial bodies prioritize a visible enemy carrier, try a legal
  selective signature or shot, and otherwise approach adjacent interception
  tiles rather than occupying the carrier's tile.
- Routing claims every current own tile and every selected destination,
  respects the contract's no-follow rule, avoids visible actors, projectile
  lanes, blocking constructs, and spawn reservations, and replans around a
  previously blocked first step.
- Deliberate handling is discovered from the movement profile and uses
  cardinal routing plus legal rotation; other profiles use the offered
  eight-way movement headings.

The mechanically safe frozen signature is Vector Dash for a distant, clear,
aligned pickup lane. It executed in the required self-play and makes the
distributed pickup intent legible without becoming routine movement. Other authored signature
branches were removed only after they provoked a host-side empty-position
signature-state abort; the exact repair trail is archived under
`repair-history/` and described in `DX.md`.

## Mechanical smoke

The required in-process self-play used this project on both sides, this same
`sheet.json` on both sides, and seed `314159`. It completed at 505 ticks with
both teams eligible. Because the frozen contract allows zero faults before
disqualification, two eligible teams also proves zero runtime faults. No
strategy change was made from the winner, score, summary tactics, or any
cohort result.
