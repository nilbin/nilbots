# Arc Relay Shadow Scissors v2 exhibit

Date: 2026-08-14  
Branch: `codex/shadow-scissors-exhibit`  
Base: `main` at `4b3d9f91`  
Loop profile: `tapline-3`  
Runtime: trusted in-process tactical mind

## Question

Keep Tapline, the two full outer routes, the eight-class composition, the direct
mid opponent, and the six review cells fixed. Does changing each four-body wing
from four interchangeable bodies into one Core runner plus three denial/support
bodies convert the early rear theft into useful banks?

This is an exhibit, not a battery or a balance read.

## G1 diagnosis: capacity did not mean capacity

The owner caught the decisive executor defect in the v1 gallery at `w-9001`
around tick 150. Two loose Cores sat in midfield among three visible enemies,
yet six control bodies selected `collect-loose-core`; two Shadow bodies joined
them. Four control bodies claimed the same centre Core despite an authored
capacity of two.

The target allocator chose capacity winners, removed them from its open set,
and then forgot their claims while losing bodies walked to their next candidate.
A later fallback pass could therefore fill the same capacity again. The repair
keeps `(capacity scope, resolved target)` claims for the complete allocation
walk. A focused test recreates two already-full Core targets and proves later
losers fall through to their uncapped order; a second test pins unit-id tie
breaking.

In the rebuilt six cells, the maximum number of explicit collectors targeting
one Core in one team/tick is exactly two in every cell. At G1 tick 150, Shadow
has one runner collecting; its screens are staging, recovering, or holding the
enemy exit. No crowd of collectors suppresses their fight/denial work.

## Authored labor

Composition remains:

`Kestrel, Kestrel, Veil, Hush, Towline, Sunder, Patchbay, Relay`.

The roles are:

- north runner: one Kestrel;
- north screen: Kestrel, Veil, Hush;
- south screen: Towline, Sunder, Patchbay; and
- south runner: one Relay.

All eight use the exact v1 perimeter ingress routes. One runner and at least two
screen bodies from both wings must occupy their rear rally zones; the breach
then latches for 80 ticks. During it, each screen follows its own runner with
rear posture, but nearby enemies and class signatures remain higher priority.
The runner owns the enemy Well and its direct return. It neither hands off nor
voluntarily drops: the first draft proved that inherited point-blank dropping
made the specialist surrender its Core in midfield.

After the breach, only the two runners deliberately collect known loose Cores.
The six screen bodies fight and occupy the three legal tiles between the enemy
home pad and enemy-side Well. A screen that physically picks up a Core while
fighting still returns it; the sheet does not discard a free possession.

## Controlled read

Fixing the allocator changed both entrants, so the original v1 gallery is not a
valid baseline. The immutable v1 sheet was rerun on the corrected executor.
That diagnostic remained 0-6, had one horizon finish, and was not review
eligible: `w-9001` tripped formation freeze and `w-9002` tripped handoff
ping-pong. It was not served.

Both variants entered their breach at tick 47 in all six cells. The timing read
is:

| cell | corrected v1 enemy-Well pickup | v2 enemy-Well pickup | corrected v1 first bank | v2 first bank | direct control first bank |
| --- | ---: | ---: | ---: | ---: | ---: |
| `w-9001` | 61 | 56 | 147 | 144 | 49 |
| `w-9002` | 61 | 56 | 132 | 229 | 53 |
| `e-9005` | 77 | 63 | 155 | 133 | 54 |
| `e-9009` | 92 | 63 | 151 | 113 | 53 |
| `w-9012` | 66 | 56 | absent | 133 | 55 |
| `e-9004` | 62 | 63 | 142 | 105 | 54 |

The specialized sheet reaches the enemy-side Core earlier in five cells and
one tick later in the sixth. It banks earlier in four cells, restores a bank in
the v1 no-bank cell, and is 97 ticks later in `w-9002`, where the first runner
is destroyed at tick 78 and the eventual rescue does not bank until tick 229.

That is a concrete improvement in conversion reliability, not a claim that the
strategy is strong. The direct pack still earns the first bank at ticks 49-55;
the flank's first banks remain ticks 105-229.

## Six-cell exhibit

| cell | result for Shadow Scissors v2 | final standing |
| --- | --- | --- |
| `w-9001` | win by horizon ranking at t599 | pulses 2-1, integrity 2-1, charge 1-2 |
| `w-9002` | loss at t334 | pulses 0-3, charge 1-0 |
| `e-9005` | loss at t467 | pulses 1-3, charge 2-0 |
| `e-9009` | loss at t442 | pulses 1-3, charge 0-0 |
| `w-9012` | loss at t367 | pulses 0-3, charge 2-0 |
| `e-9004` | loss at t580 | pulses 1-3, charge 1-0 |

The record is 1-5 in these six hand-picked games. Shadow banks 7, 1, 5, 3, 2,
and 4 Cores respectively. All six pass the frozen felt-degeneracy bars and the
raw movement-sanity gate. There are no closed allied movement cycles, false
move traces, recovery no-progress episodes, route no-path/bypass reports, or
delivery timeouts.

Four cells contain screen bodies holding one exit tile for more than 100 ticks.
Those holds are the authored post-breach denial line, not blocked movement: the
blocked share of stationary ticks is 6.4%-16.8%, and the eligibility bars accept
the surrounding contest/theater activity. Whether that long denial posture is
interesting to watch remains a taste/design question for the owner.

## Read, without a balance verdict

The experiment does not support “outer routes are useless.” The pincer arrives
reliably, steals earlier, can convert repeatedly, and one cell survives ahead to
the horizon. Specializing labor also eliminates the v1 handoff-ping-pong failure.

It also does not refute the travel-cost concern. The direct opponent has roughly
a full Core-return head start in every cell, and the first runner can be killed
before its escort establishes fire. The clean next design question is therefore
geographic value rather than path feasibility: does the outer route need a
nearby objective/reward, a shorter connection, or another reason for denial
occupancy to matter before the mid pack banks?

## Frozen reference

| reference | orders SHA-256 | map-plan SHA-256 | ATL2 SHA-256 | ATL2 bytes |
| --- | --- | --- | --- | ---: |
| Shadow Scissors v2 | `36ef6524e891819a760f9c50cee68e6789f1feb6f3446c40e134f238f810bab7` | `610121a589cc42ab63e43e78a9d952b8459230b198674c0fc13c04c33ec1627c` | `001923e9ebb876170e50905d254b04685055c1482034132ac2083635394057d4` | 7,251 |
| Direct mid control | `2375c7eb7a1970c63e098d2451b01729e766ee0e1aceca2b892baa79ab25e1bb` | `b9232de776338b22509ca923181b45c4f196c436facd370fc86751d16341d4c7` | `f78997f8d63312726449cce746d8417eed6069a549dbe565e362c3abdef4313c` | 2,897 |

## Gallery and validation

Public, unauthenticated gallery:  
https://ties-kurt-zope-historic.trycloudflare.com/

- standard cells: `w-9001`, `w-9002`, `e-9005`, `e-9009`, `w-9012`,
  `e-9004`;
- one max-ticks finish is disclosed; the other five end by reactor destruction;
- six of six pass replay-visible motion sanity and the frozen felt-degeneracy
  eligibility bars;
- a phone-sized Chrome smoke loaded the public index and a real 189,484-byte
  gzip broadcast with no console errors, then advanced playback from tick 000
  to tick 005;
- the unchanged production viewer build was reused, as required by the fast
  sheet-review loop; and
- no battery, sweep, WASM build, playlist mint, rating change, engine-map edit,
  or balance verdict was made.
