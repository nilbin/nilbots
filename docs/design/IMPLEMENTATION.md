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
   `seasonView` in `LeaderboardPage.tsx`). A placeholder number is worse than a missing
   column, because it survives review.

## Screens

Status is one of **done**, **partial**, **not started**, and names what is missing.

### Season — `/` (currently `/leaderboard`, titled "Standings") — **partial**

The document's home screen. In order:

| part | state | notes |
|---|---|---|
| Nav reads Season · Bots · Watch · Docs | not started | today: Arena · Bots · Leaderboard · Store · Docs |
| Header: `Season 3 · 11 days left · your N ranked bots`, pill `best rank N` | blocked | needs seasons |
| Fleet strip — a card per owned bot: identity chip, rank, movement, rating | partial | rank and rating exist; movement does not |
| Chart: one line per bot, each in that bot's accent | blocked | needs rating history per bot |
| Ladder: `# · movement · bot · sparkline · rating · W–L` | partial | movement, sparkline and W–L missing |
| `tr.mine` marked with a rule in that bot's accent | done | |
| Qualifying band row (`top 8 seed into season 4`) | blocked | the rule is invented in the doc; needs a product decision |
| Sort: rating / movement / mine only | not started | |

**Blocked on:** seasons as a concept, a rank snapshot at season open, and per-bot rating
history. Everything blocked should still be *shaped* — a column that renders nothing is a
column that renders the day the endpoint lands.

### Viewer — **done**

Header is the matchup with provenance behind Verify; bot cards are health segments,
weapon, one plain sentence, objective; the feed is a seeking index; the timeline carries
real causal events with lanes per team. 3D is the renderer and there is no toggle.

Not done: the letterbox full-screen layout. Deliberately — the decision was that chrome
may fade to nothing in full screen, and the restructure needs a device to judge.

### Bot page — `/bots/:key` — **partial**

| part | state | notes |
|---|---|---|
| Hero: identity chip, look · projectile · owner | done | |
| Rating across generations on one axis | not started | needs per-generation rating history |
| Generations as a table | done | |
| Recent matches | partial | exists; not yet the doc's table |
| "Work on this bot" terminal panel | done | |
| Challenge / appearance / submit in a rail | done | |

### First run — **not started**

A signed-in account with no bots. Four numbered steps ending in a live indicator that
turns over when the first artifact arrives, with the actual C# beside it. Today this is
`GaragePage`'s empty state. The document argues this is the highest-leverage empty state
in the product, so it should not stay last.

**Check first:** the steps name `brew install nilbots` and `nilbots new`. Confirm both
exist before building a screen whose whole value is that every command on it works.

### Watch feed — **not started**

`ArenaPage` today. The only place a spectator arrives without already knowing what they
came for.

### Appearance editor — **not started**

Lower priority, but the colour policy rests on it: it should show the pick where it will
be seen — a ladder row, a bot card, the ring on the identity chip — and refuse contrast it
cannot fix.

## Verifying

```bash
npx vite preview --port 4180                  # from web/, serves dist
BASE=http://localhost:4180 node scripts/site-shots.mjs
```

Fixtures are matched by pattern and their shapes come from `web/src/api/schema.d.ts` —
guessing a shape produces a blank page, so the harness reports how much text each page
rendered and shouts BLANK below forty characters. It shoots wide and narrow and reports
horizontal overflow, because a redesign that only works on a laptop is half a redesign.
