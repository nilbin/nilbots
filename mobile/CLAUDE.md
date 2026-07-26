# CLAUDE.md — mobile

Scoped guide for `mobile/`. The repository root `CLAUDE.md` is canonical and still
applies (contract regeneration, rules-change surfaces, commit conventions); this file
only covers what is specific to the Expo app. Where the two overlap, root wins.

The app is the nilbots site's companion: watch matches, follow the ladder, check your
garage. It is a **spectator client**, not an authoring one — writing C# on a phone is
the CLI's job.

## Stack

Expo SDK 57 · React Native 0.86 · expo-router (file routing) · TanStack Query ·
TypeScript. Deliberately no state-management library, no component kit, no styling
framework — see *Dependencies* below before adding one.

## Folders

```
src/
  api/         Generated schema + the typed fetch client. NOTHING else talks HTTP.
  app/         Routes only. File path = URL path (expo-router).
  auth/        OAuth flow + keychain-backed session. The only module that knows
               how a token is obtained, stored or renewed.
  components/  Reusable, presentational, route-agnostic.
    ui/        Primitives with no domain knowledge (Screen, Card, StateView…).
  hooks/       Data access — one hook per resource, wrapping TanStack Query.
  theme/       Design tokens. No components.
```

Rules that keep this honest:

- **Routes are thin.** A file in `app/` resolves params, calls a hook, and renders
  components. No `fetch`, no `useQuery`, no business logic, no `StyleSheet` blocks
  longer than the JSX. If a route file passes ~120 lines, extract a component.
- **`api/client.ts` is the only module that calls `fetch`.** Screens and components
  never construct URLs.
- **`components/ui/` may not import from `api/` or `hooks/`.** Primitives take props.
  A primitive that knows what a bot is belongs in `components/`, not `components/ui/`.
- **One component per file**, named for the file, matching the root convention.

## Types

Response types come from `src/api/schema.d.ts`, generated from the server. Never
hand-write a response shape; run `bash scripts/generate-api-clients.sh` from the repo
root and commit the result. CI's `contract-drift` job fails on a stale client.

Alias generated types in `api/client.ts` (`export type BotSummary = Schemas[…]`) so
screens import a short domain name rather than indexing `components['schemas']`
everywhere.

## Data access

One hook per resource in `hooks/`, wrapping `useQuery`, owning its own query key and
cadence. Screens never pass a `queryKey` — the hook decides.

Query keys are arrays namespaced by resource: `['bots']`, `['bot', key]`,
`['match', id, 'live']`.

**Polling belongs in the hook.** `/api/matches/{id}/live` is a polling endpoint by
design (there is no socket), so its hook sets `refetchInterval` and stops polling once
the match completes. Do not scatter `setInterval` through components.

## Auth

Authorization Code + PKCE in the **system browser**, against the server's own OpenIddict
as the public client `nilbots-mobile`. Never an embedded WebView — it can read what is
typed into it, and a login form is exactly what that is.

Three things that are easy to get wrong here:

- **`offline_access` is the only scope.** It is the only one the server registers, so
  adding `openid` or `profile` fails the authorize request outright with `invalid_scope`.
- **Tokens go in `expo-secure-store`, never AsyncStorage.** A refresh token mints access
  tokens for 30 days; AsyncStorage is a plain file in the app container.
- **`api/client.ts` does not import `auth/`.** `AuthProvider` registers a token provider
  via `setAccessTokenProvider`, because auth depends on the client and importing back
  would be a cycle. Refreshes are deduplicated — OpenIddict rotates refresh tokens, so
  two concurrent refreshes sign the user out.

`expo-secure-store` and `expo-web-browser` are native modules with config plugins. Their
`app.json` entries do nothing until `npx expo prebuild` and a rebuild.

## Control bars are one line

A search/filter bar never takes a second row. `components/ui/FilterBar` is that line:
a search field plus one button. Anything else — toggles, sorts, ranges — goes behind
that button into a `components/ui/BottomSheet`.

Two reasons this is a rule rather than a preference. A stacked bar eats the top of a
short screen, and on a phone the list rows *are* the content. And a filter that is on
but scrolled out of view silently explains a short list, so `FilterBar` badges its
button with the active count — an active filter must be visible without opening
anything.

Apply the same shape to any screen that grows controls, not just Bots.

## Every screen handles four states

Loading, error, empty, and content — always all four. `components/ui/StateView`
exists so this is one component rather than four ad-hoc branches per screen, and so
"no ranked sets yet" reads like intent rather than a bug.

The API returns real nulls (a deleted bot yields a null name and accent on a match
set). The generated types tell you where. Never `!` past one — render a fallback.

## Styling

`StyleSheet.create` plus tokens from `theme/arena.ts`. Those tokens mirror
`web/src/index.css`; keep them in sync by hand — they are design tokens, not a
contract, and are too small to justify a build step.

No inline colour literals outside `theme/`. If you need a colour that is not a token,
add the token.

Bot accents are **server data, not tokens** — `bot.accent` is a per-bot hex string and
is used directly.

## Dependencies

The current set is small on purpose. Before adding one, note that:

- state management is not needed — TanStack Query owns server state, `useState`
  covers the rest;
- a component kit (Paper, Tamagui, gluestack) will fight the arena aesthetic;
- NativeWind was considered and rejected — its Tailwind-v4 support is unsettled and it
  adds a Babel/Metro layer to debug.

Use `npx expo install`, never bare `npm install`, so versions stay SDK-compatible.

## The arena viewer

Match playback renders in a **WebView** running the site's existing single-file
`viewer.html`, not a reimplemented renderer. `web/src/render/drawArena.ts` is ~550
lines of Canvas2D over ~5.7MB of WebP wall atlases and SVG sprites; porting it to Skia
is a real project and the renderer is still moving.

The cost that matters is the texture decode and sprite bake, which is **per WebView
instance, not per match**. So: mount one WebView, keep it alive, and push replays in
over `postMessage`. Never remount it per match — that pays the whole bake every time
and is exactly the stutter this design avoids.

## Notifications

Not built yet; design in [`../docs/NOTIFICATIONS-PLAN.md`](../docs/NOTIFICATIONS-PLAN.md)
(DECISIONS #108/#118). Two things to know before touching it here.

The app is a **delivery channel, not a second inbox**. Notifications are durable records
on the server, reaching the app over the same SignalR hub the site uses, with unread
reloaded on resume. Do not invent app-local notification state.

In-app toasts are not system banners. A push can be plain; an in-app reward is the moment
the game pays the player back, and it should feel like it. Result toasts reuse
`OutcomeText` and `BotRecord`'s colours — `Arena.ok` for a gain, `Arena.live` for a loss —
rather than inventing a third vocabulary for the same thing. Never toast over the arena
viewer, and never toast a result for the match currently on screen.

## Verifying

`npx tsc --noEmit` must be clean before committing. Run the app on the iOS Simulator
(`npx expo run:ios`, or `npx expo start` then `i`) rather than trusting the web target
— react-native-web silently tolerates things a device does not, and WebView behaviour
differs entirely.

For local development the API defaults to the Metro host on port 8080; override with
`EXPO_PUBLIC_NILBOTS_API`. See `src/api/config.ts`.
