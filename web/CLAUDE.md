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

## Data access

**TanStack Query, one hook per resource, in `site/queries.ts`.** Pages never call the API
directly and never own a fetch: they call a hook and render its four states.

- **Endpoints are named in `site/api.ts`.** `endpoints.bot(key)` binds the path and its
  response type together once, so they cannot drift; `api.get<T>(url)` states them
  separately at every call site and a typo type-checks perfectly. The raw verbs remain for
  mutations.
- **Polling belongs in the hook**, never a component, and each one stops on its own
  condition: a match when it is finished *and* done broadcasting (status alone stops too
  early — the result is still withheld), a set when it is revealed, a replay when its
  broadcast ends.
- **Query keys are arrays namespaced by resource** — `['bot', key]`, `['match', id, 'live']`.
- **A 4xx is an answer, not a hiccup.** The client-level retry policy does not retry them,
  so a mistyped id says "no such match" instead of spinning.

`StateView` gives loading, error and empty one implementation. **Every page that fetches
handles all four states** — loading, error, empty, content — for the same reason the
mobile app does: "no ranked sets yet" should read as intent, not as a bug. This used to be
the app's weak spot; nine hand-rolled `useEffect` fetches each treated `null` as "loading",
so a rejected request left the page on "Loading…" forever and four polling loops died on
their first failure with no retry.

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

## Full screen is an orientation, not a button

On a coarse pointer, `(orientation: landscape)` *is* immersive mode — the arena takes the
viewport, portrait gives the page back, and there is no toggle and no exit button, because
either would be overruled by the device the moment it disagreed. `useImmersive` owns that
rule; pointer devices keep the button and real fullscreen.

Do not try to invert it. A "full screen" button that demands landscape cannot work on the
device that matters: **iPhone Safari has no Screen Orientation lock API and no
`requestFullscreen` outside `<video>`**, so nothing the page does can make iOS turn itself.
That is also why immersive mode is CSS (`100dvh`, page chrome hidden) rather than the
Fullscreen API. Android does have both, and gets real fullscreen — but only from a user
gesture, which an orientation change is not, so `promote()` upgrades on the first touch
after landscape engages.

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
what stops a rename from silently rendering nothing — plus `structure.test.ts`.

CI runs both. **`structure.test.ts` enforces the two folder boundaries above** that fail
silently rather than loudly: the viewer half importing site code (compiles, then throws at
runtime for CLI users), and anything but `site/api.ts` importing the generated schema
(spreads a regeneration's breakage across a dozen files instead of one). Prose alone was
not holding them; if you change a boundary here, change that test in the same commit.

The build emits a **single 14 MB `dist/index.html`**, inlined on purpose so the CLI can
ship one file. The App serves it directly, so a viewer change is not live until
`npm run build` has run — including for the mobile app, which loads it from the server.
