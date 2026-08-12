# Orders fight-selection review

Date: 2026-08-12

Branch: `codex/order-urgency`

Status: fast in-process design review; not a hosted promotion or balance read

## Result

The ordinary fight order now answers a body-local question: is a
team-visible enemy within this body's authored engagement distance? Hunter,
Wellwright and the starter use seven tiles. The existing
`visible-enemies-in-zone` fact remains available for deliberately team-wide
contact responses and still drives the sheets' signature-skill orders.

Fight target selectors remain ordered preferences. A remote `carrier` or
`weakest` target no longer masks a later target that the body can shoot or
reach inside its current chase episode. With no actionable target, the fight
leaf yields to the next eligible authored order for the tick. The fight
lifecycle retains its original anchor while yielding, so fall-through cannot
advance the leash one tick at a time.

Reasons expose both decisions:

- `target-fallback-from-<actor>`: a later actionable preference won;
- `after-<fight-id>-no-actionable-target`: the body ran a lower order.

## Motivating G3 scene

Tapline seed 9005, Wellwright west versus Hunter east previously let
Wellwright's `weakest` preference select a remote target while a nearer enemy
was available, then left west bodies holding at the fight leash. The focused
scene test now observes both target and order fallback and zero
`fight-visible-enemy:*:chase-limit` holds.

Across the standard six review cells, every game exercised both paths. Raw
command counts were:

| cell | actionable target fallbacks | lower-order fallbacks | old fight-leash holds |
| --- | ---: | ---: | ---: |
| w-9001 | 72 | 156 | 0 |
| w-9002 | 107 | 184 | 0 |
| e-9005 | 91 | 210 | 0 |
| e-9009 | 115 | 174 | 0 |
| w-9012 | 164 | 175 | 0 |
| e-9004 | 147 | 241 | 0 |

## Exhibit

Public phone gallery:
[Local contact and actionable fight targets](https://performing-holdem-inns-florida.trycloudflare.com)

The six records are four Hunter wins, one loss and one simultaneous reactor
destruction. These are disclosed gallery outcomes, not a balance claim. A
390x844 Chromium smoke loaded the index, the sample-03 replay payload, and the
ready WebGL canvas without console, page, or request errors.

## Validation

- Focused CLI/compiler/product-scene tests: 5 passed.
- Focused web authoring and product-IA tests: 23 passed.
- Web production build: passed.
- Full web test run: 462 passed, one unrelated pre-existing failure because
  this worktree lacks the gitignored Meshy Sentinel provider source referenced
  by `arcRelaySignatureModelAssets3d.test.ts`.
- `git diff --check`: passed.

This review deliberately did not build WASM, alter a frozen artifact, mint a
playlist, move a rating book, or run a release suite. Promotion remains a
separate owner-authorized gate.
