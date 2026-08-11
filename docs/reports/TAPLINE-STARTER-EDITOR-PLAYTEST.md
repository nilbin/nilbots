# Tapline starter and editor-only authoring playtest

Date: 2026-08-11  
Scope: starter source pair, hosted template catalog, and product-smoke coverage.
No game rule, compiler rule, map, stock opponent, playlist, rating, or replay
contract changed.

## Outcome

Hunter is no longer the new-player template. The App now starts a sheet from a
small, separately authored Tapline source pair:

- two `runner` bodies fetch from any Well and fall back to one home circuit;
- five `line` bodies occupy midfield and intercept a visible enemy carrier;
- one `medic` occupies midfield and repairs nearby allies; and
- the layout contains exactly one authored zone, one route and one anchor, all
  referenced by the playbook.

The eight-body composition uses each currently granted starter class once. The
source is 224 playbook lines plus 83 layout lines. Hunter was 819 lines and its
shared base layout was 1,659 lines with 35 zones, 24 routes and 21 anchors. The
new layout is not a filtered view of that catalog: it is its own tracked source,
so a new author never inherits unrelated or duplicate geometry.

Hunter remains tracked as a strategy/reference sheet. Wellwright and its frozen
Tapline layout remain the practice opponent and hosted stock source. Neither was
rewritten to create this starter.

## Exhibit

The standard six-cell exhibit is available at
[the public phone gallery](https://skins-approx-receiving-hung.trycloudflare.com/).
It uses `tapline-1`, the starter as the exhibited sheet, Wellwright as the actual
opponent, and the registered side/seed set:

| cell | completed | recorded result from starter side |
| --- | ---: | --- |
| `w-9001` | t434 | reactor destroyed, pulses 3–1 |
| `w-9002` | t433 | reactor destroyed, pulses 3–1 |
| `e-9005` | t448 | reactor destroyed, pulses 3–1 |
| `e-9009` | t372 | reactor destroyed, pulses 3–1 |
| `w-9012` | t447 | reactor destroyed, pulses 3–1 |
| `e-9004` | t516 | reactor destroyed, pulses 3–2 |

This is functional exhibition evidence, not a balance sample or claim. The
important observations are that both runners collect, the line moves and
fights, the medic participates, and every cell completes. In the inspected
`w-9001` route read, blocked stationary time was 4.4%, the longest stationary
run was 56 ticks, no body exceeded 100 ticks, and there were no delivery-timeout
ticks.

A phone-sized production-browser smoke opened the public index, found all six
cards, loaded `sample-01` in WebGL, and advanced playback without a page or
request error.

## Editor-only creation test

The tracked `scripts/smoke-arc-relay-product.mjs` now starts with this template
and authors a distinct **Split screen** sheet using visible editor controls only.
It does not upload or mutate JSON behind the UI:

1. rename `line` to `home-guard` and move its target from five bodies to three;
2. duplicate it as `far-raiders` and allocate two bodies;
3. draw a home territory and a far-side territory on the map;
4. bind each assignment's fallback patrol to its own territory and choose rear
   versus front posture;
5. save and compile through the real App against disposable PostgreSQL;
6. run and load a hosted practice against Wellwright;
7. return to the same draft and plot all 16 authoritative body tracks;
8. move the far-side territory by keyboard, rerun with one action, watch again,
   and return with both run cards intact.

The saved revision was read back only after the UI flow as verification. It had
targets `runner:2`, `home-guard:3`, `medic:1`, `far-raiders:2`; the two new zone
rectangles were persisted; both hosted matches completed; both returns exposed
16 tracks; and the browser captured no exception, failing response or phone
horizontal overflow.

## Friction ledger

No issue forced a JSON escape hatch, but the pass found four real UX debts.
They are findings, not silently implemented grammar changes.

1. **Geometry cannot be named at creation.** The two authored ideas became
   `Zone 1` and `Zone 2`. Those generated names then leaked into command cards
   and replay body chips (`zone-1`, `zone-2`). This is the largest
   comprehensibility defect: the map should ask for a semantic name such as
   `home-screen` or `far-raid` and support safe reference-aware rename.
2. **Phone geometry creation requires excessive travel.** The geometry kind,
   object list and Add control sit below the map, while Draw/Select sits above
   it. Creating one zone means add below, scroll up, enter Draw, drag, then leave
   Draw. Adding geometry should enter a compact name-and-draw flow immediately;
   the relevant object controls should remain adjacent to the canvas.
3. **Staffing a split is arithmetic rather than spatial.** Duplicating an
   assignment correctly starts it at zero bodies, but a 5 → 3+2 split requires
   repeated minus/plus actions across two panels. A game-like body-token transfer
   or eight-slot allocation strip would communicate the conservation rule and
   make the move one gesture without changing the DSL.
4. **The phone practice dock covers map space while plotting.** Keeping Practice
   reachable is valuable, but the full dock occupies a meaningful band of the
   small canvas. It should collapse while a pointer is down or while the map is
   in full-screen editing mode, then return when the gesture ends.

Secondary observations: retaining the last selected geometry kind is useful but
can make type-specific controls appear to have disappeared on return; the type
heading needs stronger state. The eligible-chassis grid is informative but very
tall for a five-class assignment, so a compact roster strip would make repeated
assignment work faster without hiding signature-skill help.

## Verification

- focused App template/compiler tests: 3 passed;
- web tests: 464 passed;
- production web and scoped viewer builds: passed;
- production App browser smoke: two hosted practices completed, two draft
  returns, 16 tracks after each, no captured browser errors;
- public 390×844 gallery smoke: six cards, WebGL replay loaded and advanced;
- starter playbook SHA-256:
  `a602a2836aaadc46309a96c38a1db5e179ef531d22082328823cad2c9263f0f1`;
- starter layout SHA-256:
  `f30aa266194bafb2e9ba2109cb7a8bd1da43c7abd8c460ac3b2331a47ad901bf`.
