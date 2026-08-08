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
  render3d/       Lazy WebGL renderer; consumes ReplayModel/presentation, never wire JSON.
  site/           The website: pages, its own components, its API client, auth.
  App.tsx         Standalone viewer mode.
  main.tsx        The mode switch.
  replayWire*.ts Hand-maintained versioned replay mirrors (see root CLAUDE.md).
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

Replay v1, internal Frontline replay v2, and generic actor replay v3 meet only at the
version-neutral `ReplayModel`. Decode and validate each wire format before normalization;
never infer missing lifecycle, form, action-mask, identity, score, or causal data from
presentation state. Replay v3 keeps the canonical generic contract and stable
team/unit/life identity separate, so actor count and future action/form catalogs do not
change viewer identity. Hosted bridge v1 remains the legacy slot-shaped contract and
rejects v2/v3; bridge v2 remains the stable v1/v2 contract; bridge v3 adds generic mode
and scoreboard data. The TypeScript mirror in
`mobile/src/components/arena/protocol.ts` changes in the same commit.

## One camera, two projections

`render/arenaCamera.ts` decides what the arena is looking at — a centre and a span in
tiles, fitted to the active lives, sprung toward, and clamped. **Neither renderer owns
any of that.** `drawArena` turns a frame into a tile size and an origin (`arenaViewport`,
which the canvas hit-test also uses, so a click still lands on the bot under it);
`ArenaCanvas3D` turns the same frame into a distance and a look target. A camera decision
made in one of them is a camera the two viewers disagree about, and a device that loses
its WebGL context swaps between them mid-replay.

**The fit centres the action, not the arena** (DECISIONS #175). A fitted frame is first grown
to the viewport's shape, so on a phone it is far larger than the fight on one axis — and
keeping such a frame inside the map, which is what it used to do, is exactly what put a
spawn-side duel a third of the screen off centre. The frame may now hang over the edge of the
arena; the only thing that overrules the action is an axis the frame already covers whole,
which is centred on the map so the bars are even. Gestures are bounded the same way, because a
hand that can reach somewhere the fit cannot is a camera that jumps when auto-fit comes back.

Two consequences worth knowing. **`drawArena` with no `frame` is the historical whole-map
framing, to the pixel** — that is what the golden frames are recorded at, and what a
caller who does not want a moving camera gets by saying nothing; the default-on camera
lives in `ArenaCanvas`, not in the renderer. And the *override* is a contract, not a
renderer detail (`render/cameraGestures.ts`): wheel and mouse-drag on a pointer device,
two fingers on a touch one, never one finger — which belongs to the page and to
selection. The mobile WebView takes auto-fit and **no** gestures, because the bridge
carries no camera message and a gesture there would have nothing to undo it.

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

**TanStack Query, one hook per resource, in `site/queries.ts` — reads *and* writes.**
Nothing outside `site/api.ts` calls `api.get`/`post`/`put`, and no component owns a fetch:
it calls a hook and renders its states. That includes the session (`useMe`), the unlock
catalog, and the notification inbox; the only `useEffect`s left in `site/` are a form
prop-sync and the SignalR subscription, neither of which is a request.

- **Endpoints are named in `site/api.ts`**, request body *and* response type bound to the
  path in one place. `api.post(url, { … })` states them separately at every call site, so a
  renamed field type-checks perfectly and posts a shape the server ignores — which is
  exactly what had happened: the challenge form omitted a required `seed` and nothing
  noticed until the body was typed.
- **Mutations are `useMutation`, and each one says what it invalidates.** A write that does
  not is how a page ends up showing the value the user just changed away from, and
  `useLogout` clearing the cache is what stops one account's private data being rendered to
  the next. Hand-rolled `busy`/`error` pairs around a bare post are the pattern this
  replaced — there were six, with six different error-message fallbacks (now
  `site/errorMessage.ts`).
- **Polling belongs in the hook**, never a component, and each one stops on its own
  condition: a match when it is finished *and* done broadcasting (status alone stops too
  early — the result is still withheld), a set when it is revealed, a replay when its
  broadcast ends.
- **Query keys are arrays namespaced by resource** — `['bot', key]`, `['match', id, 'live']`.
- **A 4xx is an answer, not a hiccup.** The client-level retry policy does not retry them,
  so a mistyped id says "no such match" instead of spinning. Where a status code is the
  *expected* answer it is handled at the endpoint: `endpoints.me` turns 401 into `null`,
  because "nobody is signed in" would otherwise put an error state on every public page.

`StateView` gives loading, error and empty one implementation. **Every page that fetches
handles all four states** — loading, error, empty, content — for the same reason the
mobile app does: "no ranked sets yet" should read as intent, not as a bug.

This was the site's weak spot, and it took two passes to actually finish. Hand-rolled
`useEffect` fetches each treated `null` as "loading", so a rejected request left the page
on "Loading…" forever and the polling loops died on their first failure with no retry. The
last two survived the first pass for months **while a working hook sat unused beside
them** — `useBotStats` and `endpoints.botStats` both existed, and `BotStatisticsPanel`
still fetched by hand, swallowing errors into `null` and rendering nothing. If you add a
hook, wire it; an unused one reads as done.

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

Playback also holds a **screen wake lock** — `useScreenWakeLock`, beside `useImmersive`, so
`Viewer` (both outputs) and `HostedViewer` share one implementation. It follows *the clock is
running*, not the play button, so a live broadcast counts. The re-acquire on
`visibilitychange` is mandatory rather than defensive: the platform releases the lock whenever
the page is hidden and never hands it back, so without it a viewer who checks a message
watches the rest of the match on a screen free to sleep. And it is silent wherever it cannot
work — iOS Safari, a `file:` viewer that is not a secure context, a low battery — because a
console error there would be one on every replay.

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

## Two builds, one source

`npm run build` produces both, and they exist for different consumers:

| output | config | shape | consumer |
|---|---|---|---|
| `dist/` | `vite.config.ts` | 847 B entry + hashed assets | the site, and the app's WebView |
| `dist-cli/<theme>/` | `vite.cli.config.ts` | one self-contained file per map theme, 3.6–6.7 MB | `nilbots play` |
| `dist-review/` | `vite.review.config.ts` | hashed, `base: './'` | `npm run review` on a phone |

**`viteSingleFile` belongs to `dist-cli` alone.** `nilbots play` writes a `viewer.html` the
player can copy anywhere and open from disk, and a `file:` URL cannot fetch sibling modules
— so that one has to inline everything. It used to be the *only* build: the App served it,
so every visitor and every cold WebView parsed ~15 MB inline before anything rendered,
paying for a constraint neither of them has.

**And it is built once per theme, because a replay draws exactly one.** Themes are
effectively the entire artifact — roughly 14 MB each against the much smaller combined
chassis, projectile-look and approved SFX library — so an unscoped viewer paid for four
and grew with the library.
`ReplayOutput` picks by the replay's `ThemeId`, falling back rather than failing when an
install does not ship the theme a replay names.

The scoping happens in a build-time transform (`scopeToTheme`), not at runtime, and that is
not a stylistic choice: `import.meta.glob` takes a literal pattern and Rollup follows every
match, so filtering the resulting map would inline all four atlases and simply never read
three of them. That transform **throws if the pattern it rewrites has moved**, because the
failure mode it guards is silent — the build would keep succeeding and ship every theme
again.

Two more consequences. `dist` now carries the 1024/2048 atlas variants, so
`preferredAtlasWidth()` can pick per device instead of everyone getting the 4096 master —
`build:cli` runs `atlas:clean` first precisely so those variants are *not* inlined. And the
CLI packs `web/dist-cli/<theme>/index.html`, so moving that path means updating
`BotArena.Cli.csproj`, `ReplayOutput.cs` and `assert-cli-release.sh` together.

The App serves `dist` directly, so a viewer change is not live until `npm run build` has
run — including for the mobile app, which loads it from the server.
