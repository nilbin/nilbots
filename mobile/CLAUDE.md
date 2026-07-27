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

`expo-secure-store`, `expo-web-browser` and `expo-notifications` are native modules with
config plugins. Their `app.json` entries do nothing until `npx expo prebuild` and a rebuild
— `tsc` passing proves nothing about them, because the JavaScript compiles perfectly
against a native module that is not linked. `expo-notifications` also generates the
`aps-environment` entitlement, so a build without the prebuild has no push capability at
all.

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

**Turning the phone is the full-screen control.** The arena is the one screen here that
may rotate — every other one is a list, so the app is locked to portrait from the root
layout and the viewer lifts that lock only while it is showing. Sideways it is the arena
and nothing else, with the transport floating over it and fading out; upright it is the
arena plus the cards, control bar and provenance that explain it. Read the shape from
`useWindowDimensions`, not an orientation listener: it is a layout question, and a split
view is landscape-shaped without the device having turned.

Forcing the rotation instead was tried and reverted. It yanks the phone sideways whether
or not that is what the viewer wanted, and it leaves the bot cards nowhere to go. The
device asking is better than the app insisting — and it means there is no button.

`app.json` declares `"orientation": "default"` on purpose: it is the *native* orientation
list, and **iOS silently refuses to rotate to an orientation the app never declared**.
Narrowing it back to `"portrait"` turns every `lockAsync`/`unlockAsync` here into a no-op
that still resolves — nothing throws, nothing logs, the screen just stays upright.

## Notifications

Design in [`../docs/NOTIFICATIONS-PLAN.md`](../docs/NOTIFICATIONS-PLAN.md) (DECISIONS
#108/#118). Three things to know before touching it here.

The app is a **delivery channel, not a second inbox**. Notifications are durable records
on the server, reaching the app over the same SignalR hub the site uses, with unread
reloaded on resume. Do not invent app-local notification state.

**The simulator cannot test push.** `Device.isDevice` is false there, so registration
returns before ever asking for a token — which is correct, since a simulator has none, but
it means the whole path is unexercised until it runs on hardware. Failures are logged
rather than surfaced, deliberately, so check the console rather than expecting a banner.

**Push registration follows the session, not the app.** `usePushRegistration` registers on
sign-in and deletes on sign-out, because two people sharing a phone must not inherit each
other's results. It re-registers on every launch: Expo rotates push tokens and a stale one
is indistinguishable from a live one until a send fails. Failures are swallowed on purpose
— push is an enhancement, and an error banner about notification plumbing at launch is
noise when the inbox and the hub still work.

In-app toasts are not system banners. A push can be plain; an in-app reward is the moment
the game pays the player back, and it should feel like it. Result toasts reuse
`OutcomeText` and `BotRecord`'s colours — `Arena.ok` for a gain, `Arena.live` for a loss —
rather than inventing a third vocabulary for the same thing. Never toast over the arena
viewer, and never toast a result for the match currently on screen.

## Getting it onto a phone

Three routes, and which one you need is decided by Apple rather than by preference.

| | cost | push | lasts |
|---|---|---|---|
| `npx expo run:ios --device` over USB | free | **no** | 7 days |
| EAS `preview` → iOS | Apple Developer Program, $99/yr | yes | until the profile expires |
| EAS `preview` → Android APK | free | yes | indefinitely |

**A free Apple ID cannot do push.** Xcode gives an unpaid account a "Personal Team", and
personal teams cannot use entitlements at all — including `aps-environment`, which
`expo-notifications` generates. The app installs and everything else works; registration
simply never gets a token. So the cheapest way to actually exercise `ExpoPushTransport` and
`usePushRegistration` is an **Android** build, which needs no paid account.

`eas.json` profiles:

- **development** — a dev client, and an iOS *simulator* build. What `expo run:ios`
  produces locally, but built in the cloud.
- **preview** — internal distribution: an APK on Android, an ad-hoc build on iOS. Pins
  `EXPO_PUBLIC_NILBOTS_API` to `https://nilbots.com`, because a build on someone else's
  phone cannot reach the Metro host that `api/config.ts` falls back to in development.
- **production** — an app bundle, versions auto-incremented by EAS
  (`appVersionSource: remote`, so the number lives with the build rather than in git).

Over USB the API needs no override: `api/config.ts` resolves the Metro host's LAN address,
so a phone on the same Wi-Fi reaches the Mac's `:8080` on its own.

## Verifying

`npx tsc --noEmit` must be clean before committing. Run the app on the iOS Simulator
(`npx expo run:ios`, or `npx expo start` then `i`) rather than trusting the web target
— react-native-web silently tolerates things a device does not, and WebView behaviour
differs entirely.

For local development the API defaults to the Metro host on port 8080; override with
`EXPO_PUBLIC_NILBOTS_API`. See `src/api/config.ts`.
