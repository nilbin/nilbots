# Linked Plays authoring report

Date: 2026-08-16

Branch: `main`

Implementation commits: `3ccc558e`, `3efe71ed`, `f91cdd52`

## Result

Arc Relay now has a real, server-backed **Plays** workspace. A Play is an
editor ownership object that updates several existing Orders and map objects as
one tactic. It is not game state: the shared compiler still receives only the
normal Orders v2 document and map plan, and the executor still sees only ATL2.

The full browser flow was exercised against the persistent local PostgreSQL
volume: cast a squad, create both acceptance Plays through visible controls,
place/edit their geometry, inspect their generated Orders, save, run Practice
against Idle Field, return with tracks and selection intact, duplicate the
sheet, detach the Plays, and save the detached ordinary sources through the
server compiler.

## Persistence and identity

`TacticalSheet.WorkshopJson` is nullable PostgreSQL `jsonb`. The API accepts it
only with an explicit `replaceWorkshop` flag and validates a versioned
`arc-relay-workshop-v1` root. Older clients that omit both fields preserve the
current sidecar. The create-copy path copies it.

The sidecar is deliberately absent from `TacticalSheetContentHash`, the shared
compiler, ATL2, match snapshots and replays. Integration coverage changes only
the sidecar across a revision and pins the same content hash. PostgreSQL may
normalize JSON whitespace/key order, so persistence assertions compare JSON
semantically.

The visible browser proof is still in the local persistent volume:

| Sheet | id | revision | Orders | map plan | workshop |
| --- | --- | ---: | ---: | ---: | ---: |
| Linked Plays Fieldbook | `e058bbd2-8443-4e0a-97b6-3338225a5698` | 6 | 18,933 B | 2,564 B | 15,963 B |
| Linked Plays detached proof | `b5dcabcc-37f0-4c40-ab78-3433aa8f0d23` | 3 | 18,938 B | 2,569 B | 48 B |
| Long Corridor Relay | `e11d8345-4b85-4e79-a691-45d507d89137` | 2 | 36,875 B | 7,029 B | 30,470 B |

The detached sheet's 48-byte sidecar is only the empty schema envelope. Its
previously generated Orders and geometry remain present, editable, and passed
the authoritative server compile.

## Authoring model

All recipes use the same deterministic projection and ownership code. The
editor exposes one blank Play plus five guided lenses:

- **Ambush** — exact role or pooled posts, required readiness ranges, watched
  zone, facing, `when`/`until`, `timeout`, `cooldown`, capacity and local
  priority placement.
- **Relay chain** — two role-local projections, alternating `use` of Arc Toss,
  receiver anchors, one logical corridor and derived bounded `move.via` route
  slices.
- **Veteran escort** — level-gated follower and veteran Orders.
- **Safe return** — carrying-gated safe route and handoff/Arc Toss projection.
- **Lane closure** — Mason deployment plus an alternate-lane screen.
- **Blank** — ordinary linked projections without recipe-only concepts.

Editable labels use the Orders vocabulary: `move`, `fight`, `use`, `handoff`,
`drop`, `when`, `until`, `timeout`, `cooldown`, `urgency`, and `capacity`.
Recipe prose may explain assembly or readiness, but those are derived views of
ordinary conditions and Orders lifecycle, not runtime phases.

Stable ids preserve unaffected sources. Every owned root Order records a
fingerprint. Drift blocks ordinary save and offers exactly two recovery paths:
regenerate the owned projections, or detach and preserve the materialized
sources. Delete previews/removes owned projections and can retain geometry.
Priority moves, geometry group moves, participant changes and linked-value
edits are one undoable transaction.

## Browser proof

The acceptance Plays were created with visible controls; neither was imported
or inserted as JSON.

### North corridor ambush

The browser assigned Mason, Longshot, Hush and a two-body reserve role to five
required anchors. The focused Idle Field run temporarily set the existing
northern-traffic threshold to zero, because an idle opponent cannot provide a
hostile observation. Replay tracks show all five role-qualified posts occupied
at tick 40; the return view reports **5/5** and identifies every reached slot.
The intended threshold of one observed northern entry was then restored and
saved.

Match: `f2f85cb0-b76b-47e4-af90-6a1f3c61ae86`, replay hash
`12278dfc20dc011eb47e37043a1fa8ab97280c41a44b4bde9c1ece7c2492058f`.
This proves staging/readiness only. Idle Field cannot prove the hostile
four-enemy engagement or four-destruction release, and this report does not
pretend otherwise. Unit coverage separately pins 4/5 versus 5/5 readiness and
same-pool overlap rejection.

### Dual Relay corridor chain

Owner review rejected the initial four-post/two-Toss proof as the wrong play.
The corrected visible editor lens pairs two one-body Relay roles across:

- 20 alternating receiver posts along the long lower return corridor;
- 18 bounded same-Relay advance routes, each rejoining two posts ahead;
- 19 alternating Arc Toss Orders; and
- a final receiver bank at the home reactor.

Each hop is one latched ordinary Orders node. The current carrier Tosses only
when its partner occupies the next post, advances empty-handed to its next
same-Relay post, waits there, and releases the hop only after the partner Tosses
possession back. The route avoids the other two Wells so the empty advancing
Relay cannot auto-collect a second Core and poison the chain. There are no
unconditional station waits that can pin a Relay at a future post on spawn.

The corrected Play was created and saved through visible UI controls as
`Long Corridor Relay`, sheet `e11d8345-4b85-4e79-a691-45d507d89137`, revision
2, in the persistent local PostgreSQL volume. A controlled Idle Field exhibit
parks the six non-participants so they cannot steal the proof Core. Its three
complete chains are:

- first Toss at tick 46, nineteenth Toss and bank at tick 191;
- bank at tick 361; and
- bank at tick 531, ending by reactor destruction.

Exhibit replay hash:
`087b33acd55e18c11748571e21dc434cd39a4877cb2c11c8c91d3551e0d7004d`.
Review gallery: `https://advantages-list-sector-cards.trycloudflare.com`.
This is an explicitly labelled mechanics proof against Idle Field, not a
balance read. The existing one-tick Arc Toss tell worked; no rules or
presentation timing change was needed.

## Practice iteration and diagnostics

The active editor section, selected Play, opponent and seed survive the
Practice round-trip in session storage. Returning from a finished Practice
offers **Plot tracks**; in Plays it filters actual replay samples to the
selected roles. Required-slot Plays compute the best observed frame and show a
per-slot reached/missing result rather than inferring readiness from generated
source.

Compiler errors still use the shared server compiler and map to the underlying
Orders/map paths. Play-owned geometry, projection placement, linked values and
readiness validation are shown beside the control that owns them. The UI warns
that target capacity is recomputed and is not a sticky body reservation.

## UX and accessibility

Desktop uses the existing workshop rail, Tapline map and editor panel. Phone
uses the same controls in a vertical game-styled flow with a fixed secondary
Practice action. The four workshop tabs fit at 390 px, touch targets do not
depend on hover, and the measured document width is exactly 390 px with no
horizontal page overflow. The Play cards themselves remain a horizontally
scrollable tactical picker so the map stays wide enough to read.

![Desktop Plays workspace](evidence/linked-plays/plays-desktop.png)

![Phone Plays workspace](evidence/linked-plays/plays-phone-map-390x844.png)

![Phone workshop navigation](evidence/linked-plays/plays-phone-390x844.png)

## Verification

- `cd web && npm test`: 574 passed.
- `cd web && npm run build`: passed; production Vite build completed.
- `dotnet build BotArena.sln --no-restore`: passed (one transient file-copy
  retry while the local App process was running, no error).
- PostgreSQL-gated `BotArena.App.Tests`: 301 passed, 3 skipped after the JSONB
  semantic-comparison correction; the focused persistence test also passed.
- `bash scripts/generate-api-clients.sh`: passed with no generated drift.
- Visible Chrome-extension smoke at 1440×900 and 390×844: create, edit, save,
  reload, Practice return, tracks, copy and detach completed.

## Honest limits

- The browser smoke proves the two requested acceptance strategies and the
  other recipe lenses have compiler-valid projection coverage. It does not
  claim every recipe is strategically strong.
- Ten-minute unfamiliar-player authoring time was not measured with a human
  participant. The full flow is present, but that usability claim needs owner
  or external playtesting.
- Workshop JSON is capped at 192 KiB and currently has root/schema validation;
  the TypeScript generator owns its detailed versioned shape. It is never
  trusted as gameplay input.
- There is no balance claim. Idle Field is intentionally the proof opponent.

No engine rules, map, compiler grammar, executor behavior, frozen artifact,
playlist, rating book, canonical replay content or mobile feature surface
changed in this work.
