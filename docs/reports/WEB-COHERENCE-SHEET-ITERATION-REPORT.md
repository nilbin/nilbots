# Web coherence and sheet iteration report

Date: 2026-08-08  
Implementation checkpoint: `b8362a8f`  
Scope: hosted web and one presentation-only match-feed filter; no gameplay, sheet grammar,
compiler-rule, runtime, map, canonical replay, or mobile-app feature change.

## Outcome

The web product now has three clear jobs instead of one accumulating page:

1. **Entrants & ladder** at `/relay` owns competitive identity, crests, custom minds and
   rating.
2. **Sheet workshop** at `/relay/sheets` owns composition, doctrines, conditions, custody,
   map plotting and the change → run → watch → change loop.
3. **Watch** at `/watch` lists Arc Relay matches only. Historical Duel records remain in
   the explicitly labelled, read-only `/archive` surface.

The sheet loop is one continuous operation. A trial first persists the local draft, saves
and compiles it through the existing authoritative server compiler, launches the hosted
trial, opens its replay with a return target, and restores the same draft on return. The
sheet rail keeps recent trials with revision, opponent sheet, seed, content hash and a
coarse authoring-area diff. Selecting a trial draws the exact recorded tiles occupied by
all bodies beneath the authored routes, zones and anchors. Consecutive duplicate samples
are removed; no intermediate positions are inferred.

## Site truth and vocabulary

- Added [`ARC-RELAY-GLOSSARY.md`](../ARC-RELAY-GLOSSARY.md) and used its terms across the
  navigation, workshop, docs and current product copy: entrant, sheet, custom mind, body,
  class, composition, role, group, doctrine, mode, verb, condition, fight block, escort
  call, custody policy, layout, zone, route, anchor, trial run, replay and ladder.
- Rewrote the README and plan summary front matter around shipped Arc Relay: hosted
  playlist v8, `ambush-warren-11`, the deep Warren, sheets and custom minds. Frontline and
  Duel prose is now labelled historical rather than presented as the current product.
- Removed the orphaned Garage, Shop, appearance editor, rankings, Duel admission controls,
  Labs launcher and their dead supporting components. Old bookmarks redirect either to
  the current product or to their corresponding archive record; bot and ranked-set IDs are
  preserved through those redirects.
- Legacy Duel creation and admission remain server-gated as before. The archive still
  exposes records and verification paths, but every archive page and legacy notification
  names that context explicitly.
- Added the optional `arcRelay` query filter to `GET /api/matches`, regenerated the web and
  mobile schema lockstep, and made Watch request `arcRelay=true`. This avoids presenting
  legacy and experimental matches as current Arc Relay without changing match content or
  admission.

## Iteration behavior

- The former all-at-once editor is split into five stable sections: **Composition**,
  **Doctrines**, **Conditions**, **Custody**, and **Map**. Doctrine details are collapsed
  until chosen.
- Desktop keeps saved sheets and recent trials in a sticky side rail. Phone turns both
  lists into horizontal rails and keeps **Run & watch** in a thumb-reachable dock above the
  main navigation.
- A rerun is one tap after an edit. Pending and failure states retain the IndexedDB draft;
  saving explicitly awaits local persistence before navigating away.
- Path-bearing compiler diagnostics are translated into the editor's path model, switch
  to the owning section, scroll to the closest field, focus its first control, and render
  the compiler message inline. Client checks remain guidance; the shared compiler remains
  authoritative.
- The map remains pointer/touch driven and is now keyboard-operable: arrow keys move the
  selected west-side route endpoint, anchor, or zone without creating a separate geometry
  representation. East binding stays a read-only transformed preview.
- Trial tracks reuse the same causal live/replay query path as the viewer. While a broadcast
  is still being released, the overlay contains only positions at or before that prefix
  and grows with it.

## Visual and accessibility system

- The routed site follows the device light/dark preference using the existing semantic
  arena colour roles. The standalone match viewer remains deliberately dark so its world
  lighting does not change.
- Buttons, fields, status text, alerts and selected states now use semantic roles instead
  of drive-by cyan/red/amber utilities. Typography, panel borders, spacing and control
  shapes continue to use the existing house primitives rather than a new framework.
- Links, summaries, map canvases, buttons and fields have visible keyboard focus. Pointer-
  coarse devices get a 44 px control floor. The map has an accessible keyboard
  instruction, and no new behavior depends on hover.
- Browser assertions found zero horizontal overflow at 390 px for the Relay overview,
  initial workshop, returned track view and Docs in both the relevant dark/light passes.

## Before and after

The old `/relay` page combined every entrant, mind, ladder, editor, class reference and
trial control into one document. Splitting by user job reduced the initial phone document
from 17,453 px tall to 1,027 px for Relay or 2,377 px for the focused workshop; desktop
went from 10,357 px to 1,050 px and 1,523 px respectively.

| Surface | Before | After |
| --- | --- | --- |
| Relay, desktop 1440 px | [10,357 px monolith](assets/web-coherence-sheet-iteration/before-relay-desktop.png) | [1,050 px entrant overview](assets/web-coherence-sheet-iteration/after-relay-desktop.png) |
| Relay, phone 390×844 | [17,453 px monolith](assets/web-coherence-sheet-iteration/before-relay-phone.png) | [1,027 px entrant overview](assets/web-coherence-sheet-iteration/after-relay-phone.png) |
| Sheet workshop, desktop | contained far below the old Relay fold | [focused 1,523 px workshop](assets/web-coherence-sheet-iteration/after-sheet-workshop-desktop.png) |
| Sheet workshop, phone | contained far below the old Relay fold | [focused 2,377 px workshop](assets/web-coherence-sheet-iteration/after-sheet-workshop-phone.png) |
| Docs, desktop | [old dark guide](assets/web-coherence-sheet-iteration/before-docs-desktop.png) | [current light guide and glossary](assets/web-coherence-sheet-iteration/after-docs-desktop-light.png) |
| Docs, phone | [old dark guide](assets/web-coherence-sheet-iteration/before-docs-phone.png) | [current light guide and glossary](assets/web-coherence-sheet-iteration/after-docs-phone-light.png) |

Iteration evidence:

- [Hosted trial replay, desktop](assets/web-coherence-sheet-iteration/after-trial-replay-desktop.png)
- [Returned draft with recorded tracks, desktop](assets/web-coherence-sheet-iteration/after-trial-tracks-desktop.png)
- [Returned draft with recorded tracks, phone](assets/web-coherence-sheet-iteration/after-trial-tracks-phone.png)

## Production browser smoke

The tracked `scripts/smoke-arc-relay-product.mjs` now exercises the current product rather
than the removed monolithic page. Against a real `BotArena.App` and disposable PostgreSQL
database it:

1. registered an account;
2. opened the entrant overview;
3. edited a fresh sheet at desktop and phone widths;
4. compiled and saved through `POST /api/sheets`;
5. launched a trial through the real hosted endpoint;
6. waited for match `377ef16c-ce53-4564-bc13-0ebe42b94985` to complete;
7. loaded its causal broadcast-v2 replay in the 3D viewer;
8. returned through the sheet-specific link and verified the exact draft name survived;
9. selected the recent run and found 16 recorded body tracks on the map; and
10. loaded Docs in the light colour scheme at phone and desktop widths.

The smoke recorded no page exception, console error, failing HTTP response, lost draft,
or horizontal overflow.

## Verification

| Check | Result |
| --- | --- |
| `cd web && npm test` | 422 passed, 0 failed |
| `cd web && npm run build` | passed; production Vite and all four scoped viewer builds |
| `dotnet build BotArena.sln` | passed, 0 warnings, 0 errors |
| PostgreSQL-gated `dotnet test tests/BotArena.App.Tests --no-build` | 282 passed, 0 failed, 3 environment-specific integrations skipped |
| `bash scripts/generate-api-clients.sh` | passed; generated web/mobile contract changes committed |
| `git diff --check` | passed |
| Production browser smoke | passed; hosted match completed and 16 tracks rendered |

The three skipped App tests require their own container-build or S3 integration
environment; the PostgreSQL half was explicitly required and ran against the disposable
server.

## Deliberate limitations

- Recent-trial change snapshots are device-local. Match IDs and replays are durable on the
  server, but the App does not retain historical source documents for every sheet revision,
  so it cannot reconstruct a cross-device source diff without a new persistence contract.
  This pass does not silently invent one.
- The compiler currently returns one free-form, path-bearing diagnostic rather than a
  structured field path. Current grammar diagnostics map inline; a genuinely pathless
  compiler or admission error remains visible at draft level. A typed multi-diagnostic
  contract would be the backend improvement if finer mapping is required later.
- Phone work is web-only as requested. No mobile-app feature surface changed; its generated
  API schema moved only in lockstep with the match-list query parameter.
