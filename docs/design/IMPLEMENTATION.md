# Implementing the design

`climb.html` is the design. This is the checklist that says whether the product matches
it, screen by screen, so "done" is something you can check rather than something someone
remembers.

**It exists because of a specific failure.** The first pass through the site improved the
pages that were already there — better type, better colour, better tables — instead of
building the screens the document describes. That produces something tidier than before
and still not the design: the ladder came out as a page called *Standings* with a plain
table, when the document's screen is **Season**, carrying a fleet strip, a chart with one
line per bot, and a ladder with movement and a qualifying band. Working from the existing
code makes the existing code the spec.

So: read the row, build the row, shoot it beside the mock.

## How to work

1. **IA before pixels.** If a screen is renamed or re-scoped, do that first — route, nav,
   copy — so nothing gets polished on its way to being replaced.
2. **One screen per commit**, with its before/after pair from `scripts/site-shots.mjs`.
3. **Compare against the mock, not against memory.** `climb.html` is in this directory;
   open the matching section beside the screenshot. Every measurement in it is real —
   `climb.template.html` holds the CSS the mock is built from, and the numbers there are
   the numbers.
4. **Never fake data that does not exist.** Where the design needs something the API
   cannot supply, build the shape and leave the value absent behind a named seam (see
   `seasonView` in `SeasonPage.tsx`). A placeholder number is worse than a missing
   column, because it survives review.

## Screens

Status is one of **done**, **partial**, **not started**, and names what is missing.

### Season — `/` — **partial**

The document's home screen. In order:

| part | state | notes |
|---|---|---|
| Nav reads Season · Bots · Watch · Docs | done | fixed bottom navigation carries the same four destinations on narrow screens |
| Header: `Season 3 · 11 days left · your N ranked bots`, pill `best rank N` | blocked | needs seasons |
| Fleet strip — a card per owned bot: identity chip, rank, movement, rating | partial | rank and rating exist; movement does not |
| Chart: one line per bot, each in that bot's accent | blocked | needs rating history per bot |
| Ladder: `# · movement · bot · sparkline · rating · W–L` | partial | available rank, identity, owner, rating and sets render; unavailable measures stay hidden behind typed seams |
| `tr.mine` marked with a rule in that bot's accent | done | |
| Qualifying band row (`top 8 seed into season 4`) | blocked | the rule is invented in the doc; needs a product decision |
| Sort: rating / movement / mine only | partial | rating and mine work; movement is omitted until its data exists |

**Blocked on:** seasons as a concept, a rank snapshot at season open, and per-bot rating
history. Everything blocked has a named presentation seam, but unavailable columns and
controls stay out of the interface until they can carry a value.

### Viewer — **done**

Header is the matchup with provenance behind Verify; bot cards are health segments,
weapon, one plain sentence, objective; the feed is a seeking index; the timeline carries
real causal events with lanes per team. 3D is the renderer and there is no toggle.

The embedded player flows at phone width and takes a bounded desktop height. In immersive
landscape the arena takes the viewport, chrome floats over its letterbox, fades only when
it has no focus, and reappears for touch or keyboard navigation.

### Bot page — `/bots/:key` — **partial**

| part | state | notes |
|---|---|---|
| Hero: identity chip, look · projectile · owner | done | |
| Rating across generations on one axis | not started | needs per-generation rating history |
| Generations as a table | done | |
| Recent matches | partial | exists; not yet the doc's table |
| "Work on this bot" terminal panel | done | |
| Ranked / Challenge multi-action beside Appearance | done | opens the shared Arena composer; Play is also globally available in the top bar |
| Appearance / submit in the owner area | done | |

### First run — `/garage` empty state — **done**

A signed-in account with no bots. Four numbered steps ending in a live indicator that
turns over when the first artifact arrives, with the actual C# beside it. This is
`GaragePage`'s empty state. Keep its commands synchronized with the CLI's own help and
tests whenever the CLI surface changes.

### Watch feed — `/watch` — **done**

`ArenaPage` is the place a spectator arrives without already knowing what they came for:
live matches first, filters second, completed matches last.

### Appearance editor — `/bots/:botKey/appearance` — **done**

The chosen look, projectile and accent are shown in context rather than as isolated form
fields. Every owned and locked look remains visible with its unlock route; only owned
pairs can be saved. The bot detail links to this picker, `/store` is the commercial
catalogue, and the former `/looks` URL redirects there. Player accents are adjusted at the
presentation boundary when necessary, so a stored colour cannot erase its identity ring
or status rail against the current surface.

### Arena action — global + contextual overlay — **done (current API)**

The primary play journey is a reusable multi-action control rather than a destination.
One provider owns the single native-dialog composer, lazy queries, mutations, error state,
and result navigation. Lightweight launchers reuse it in the global top bar, bot hero,
Garage, Bots directory, and Match/Set continuation without mounting a form per row.
Ranked and one-off play remain separate choices because they are separate server
contracts:

- **Ranked set** explains the six-game mirrored format and removes opponent/map controls
  that the server ignores.
- **One-off challenge** selects an owned challenger or a public opponent plus an optional
  map, and preserves that setup when returning from a result.

The global control first asks which ready owned bot should play. Contextual controls
already know the bot; unranked result controls also preserve the previous opponent and
map. The retired review URL `/bots/:botKey/play` redirects to the bot detail rather than
remaining a second Arena surface.

Signed-out, inactive-build, empty-garage, no-opponent, loading, query-failure, and
admission-failure states all keep an explicit next action. Match and set pages return to
Watch, link their bot identities, and offer another challenge or another matchmade set
when the current account owns a participant.

Remaining allowance is deliberately not shown: the current API does not project it, and
its defaults are server-configurable. The proposed paid scheduler and the projection it
needs are specified in [Automatic Arena](AUTO_ARENA.md); no production fixture or Shop
package is hard-coded before those contracts exist.

## Verifying

```bash
npm run site-review                    # from web/, builds and serves the actual app
npm run site-review:tunnel             # optional public, unauthenticated phone URL
npm run site-shots                     # 1180 px and 390 px captures
```

The review-only Vite configuration serves fixtures whose shapes are checked against
`web/src/api/schema.d.ts`. They are not imported by the normal application bundle and
unmatched API requests fail loudly instead of falling through to a backend. The capture
harness refuses to run against anything except that marked review server, then fails on
blank content, API errors or horizontal page overflow. A redesign that only works on a
laptop is half a redesign.
