# CLAUDE.md — web

Scoped guide for `web/`. The repository root `CLAUDE.md` is canonical and still applies
(contract regeneration, rules-change surfaces, commit conventions); this file covers only
what is specific to the web build. Where the two overlap, root wins.

`mobile/CLAUDE.md` is the sibling guide. The two apps share a server, a design language
and — for match playback — actual code, so a convention that differs between them should
differ for a reason worth writing down.

## One build, two apps

`main.tsx` picks a mode at runtime:

- **the site** — the router, `site/`;
- **the standalone viewer** — `App.tsx`, when `window.__BOTARENA_REPLAY__` is injected
  (the CLI's `<!--BOTARENA_REPLAY-->` marker), on a `file:` URL, or with `?standalone`.

That is why the folder layout splits by *consumer* rather than by kind, which is not
obvious from the names:

```
src/
  api/            Generated schema. Never hand-edit.
  assets/         Bot chassis, projectile looks, map themes.
  components/     VIEWER components only — the arena canvas, bot panel, controls, feed.
  render/         Canvas2D drawing: drawArena, themes, accents, interpolation.
  site/           The website: pages, its own components, its API client, auth.
  App.tsx         Standalone viewer mode.
  main.tsx        The mode switch.
  types.ts        Hand-maintained replay mirror (see root CLAUDE.md).
  playback.ts     Playback clock + live follower.
  replay*.ts      Replay derivation shared by both modes.
```

A site component goes in `site/components/`, not `components/`. `components/` is reachable
from the standalone viewer, which has no router, no auth and no site chrome — anything
there must work without them.

## Playback is shared with the mobile app

`?standalone` also serves the mobile app's arena, where the WebView renders **only the
canvas** and the app draws the transport, control bar and bot cards natively. Two
consequences:

- **`replayPresentation.ts` is the one place per-tick values are derived.** Control
  pressure, overtime limits, zone tallies, hold phrasing — all rules-derived, and all
  consumed by both the site's `BotPanel` and the mobile app's native cards. Deriving any
  of it inline again creates a rules surface that goes stale the moment the rules move
  (root CLAUDE.md, *rules-change surfaces*).
- **`HostedViewer` is the embedded mode** and talks to its host through
  `window.__BOTARENA_LOAD__` / `__BOTARENA_CONTROL__`, posting state back over
  `ReactNativeWebView`. The playback clock stays on this side; a host asks for
  play/pause/seek. Changing that contract means changing `mobile/src/components/arena/`
  in the same commit — it is hand-mirrored, like `types.ts`.

## Types come from the server

Response types are aliases onto `api/schema.d.ts`, generated from the OpenAPI document.
Never hand-write a response shape; run `bash scripts/generate-api-clients.sh` from the
repo root and commit the result. CI's `contract-drift` job fails on a stale client.

Alias generated types in `site/api.ts` so pages import a short domain name rather than
indexing `components['schemas']`.

**Notification payloads are a discriminated union.** Narrow on the payload's own `kind`,
never the notification's — they carry the same string, but TypeScript cannot narrow a
sibling property. The server marks the discriminator required
(`DiscriminatorRequiredTransformer`) precisely so that narrowing works in both branches.

## Data fetching — the known weak spot

Pages fetch with hand-rolled `useEffect` + `useState`, one implementation per page. Prefer
following the existing shape in a page you are already editing over half-migrating the
app, but know what it costs, because the failure is silent:

- `null` doubles as "loading", so a rejected request leaves the page on "Loading…"
  **forever**. Several pages have no error branch at all.
- No caching, no dedupe, no retry, no refetch-on-focus. Navigating away and back refetches
  everything; two components wanting the same resource fetch it twice.

`StateView` (`site/components/StateView.tsx`) exists so loading, error and empty are not
re-invented per page. Use it. **Every page that fetches must handle all four states** —
loading, error, empty, content — which is the same rule the mobile app follows, for the
same reason: "no ranked sets yet" should read as intent, not as a bug.

The mobile app uses TanStack Query and gets caching, retries and the four states for
free. Migrating the site to it is the obvious fix and is not done; do it as a deliberate
piece of work rather than page-by-page drive-bys, which would leave two idioms.

## Styling

Tailwind v4, dark only. The palette lives in `index.css` as `--color-arena-*`; use
`text-arena-dim`, `bg-arena-panel` and friends rather than raw slate values, so the site
and `mobile/src/theme/arena.ts` stay in step.

The house style: near-black field, panels separated by a hairline rather than a shadow,
one cyan accent used sparingly, monospace for anything a machine produced (ratings,
hashes, seeds, tick counts) and sans for prose.

Shadows are for things that float above the page — toasts — and nothing else.

Bot accents are **server data, not tokens**: `bot.accent` is a per-bot hex string, used
directly. Run it through `adjustAccentForBackground` before drawing it on a panel.

## Toasts

`NotificationCenter` owns delivery — SignalR plus an unread poll — and routes by kind to a
toast. `UnlockToast` and `ResultToast` deliberately share a shape: eyebrow, artwork,
headline, a way in. They are both the game telling you something happened to your bot, and
two unrelated designs would read as two unrelated products.

Two rules that are easy to get wrong:

- **A kind with no toast is still acknowledged**, not dropped. Ignoring one silently
  leaves it unread forever and grows an inbox the site can never clear.
- **A loss gets the same size, artwork and prominence as a win** — only the colour
  differs. The ladder already shows the rating, so a shrunken loss reads as concealment
  (DECISIONS #119).

## Verifying

`npm run build` type-checks (`tsc -b`) and builds. `npm test` runs the asset and metadata
suites — they pin that every look manifest resolves to a real shipped sprite, which is
what stops a rename from silently rendering nothing.

The build emits a **single 14 MB `dist/index.html`**, inlined on purpose so the CLI can
ship one file. The App serves it directly, so a viewer change is not live until
`npm run build` has run — including for the mobile app, which loads it from the server.
