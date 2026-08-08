# Season UX and entitlement audit

Date: 2026-08-08  
Scope: hosted web presentation, product wording, and the existing entitlement
catalog. No gameplay, rating, map, sheet grammar, compiler or replay change.

## Presentation

The site keeps the restrained foundry palette, but primary decisions now use a
material-orange fill instead of the same outline treatment as secondary controls.
Panels have a shallow elevation and featured decision panels use a warm gradient and
orange edge. Buttons share one centered flex layout, explicit line height and symmetric
vertical padding; this removes the observed heavy lower padding. The promotional site
footer was removed. The phone navigation remains because it is the primary mobile route
switcher, not a second content footer.

Production-build browser evidence:

- [Season, desktop](assets/season-ux-polish/season-desktop.png) and
  [Season, phone](assets/season-ux-polish/season-phone.png)
- [Entrants, desktop](assets/season-ux-polish/entrants-desktop.png) and
  [Entrants, phone](assets/season-ux-polish/entrants-phone.png)
- [sheet workshop, phone](assets/season-ux-polish/sheet-workshop-phone.png)

The browser smoke found and fixed one iteration-loop race unrelated to the visual
request: practice history was persisted inside a React state updater immediately before
navigation. A fast unmount could lose the recent-practice card. Persistence now happens
synchronously before the replay route opens; the smoke returned to the draft, plotted
all 16 recorded body tracks and completed successfully.

## Ranked and practice

Current ranked admission was already passive. A player fields a sheet once; the
background pairing worker checks every 30 seconds, pairs different accounts by rating,
avoids opponents seen in the preceding 24 hours, and schedules no more than four pairs
per pass or sixteen queued/running ranked matches globally. Playlist v8 pins a baseline
of six ranked matches per entrant in a rolling 24 hours.

The web now explains that behavior where a player makes the decision. There is no
manual ranked queue. `Field for ranked` enrolls a sheet; `Withdraw from ranked` stops
new pairings.

The existing sheet trial is the immediate practice lane. Its product name is now
**Practice & watch**: current draft versus a chosen stock sheet and seed, unranked,
followed by the existing replay-return and actual-track overlay. Internal endpoint and
storage identifiers retain `trial` for compatibility.

## Custom minds

Custom-mind endpoints, controlled build, WASM sandbox, preflight and persistence remain
unchanged on the server. Their submission hooks, source editor, controls and explanatory
copy were removed from the site. Existing custom-mind records are not destroyed; this is
a launch-surface hold, not platform demolition.

## Entitlement inventory

The catalog contained eleven packs which no longer describe the launch web product:

- three generic Duel appearance pairs;
- six Striker/Bulwark/Fabricator appearance pairs from the abandoned three-class line;
- extra daily builds, which applies to the controlled code-build path; and
- extra daily ranked sets, which applies to the retired six-game Duel set endpoint.

All eleven packs are now inactive for new storefront listing and checkout. Their items,
pack definitions, past purchases, grants, 2D manifests and migrations remain intact so
historical appearances and replay/archive reads continue to resolve. No entitlement row
was deleted or reinterpreted.

Achievement grants for the legacy Duel line are likewise retained as historical
contract data. Their evaluators count legacy official `MatchSet`/`BotRating` records,
not Arc Relay entrant matches or Season rating, so Season play does not accidentally
seed those old rewards.

The first-build and controlled-build achievements can still be reached through the
backend paths that remain supported. They are not advertised or sold by the sheet-only
launch web surface.

## Future Season capacity

The v8 six-match value remains the baseline. The existing
`capacity:extra-daily-ranked-sets` key cannot be reused: its unit is a six-game Duel set,
while Arc Relay ranks single matches and pairs passively.

If the shop later sells additional Season volume, the safe shape is:

1. mint a new, explicitly named Season-match entitlement;
2. pin its additive and capped effect in a new immutable matchmaking-policy/playlist
   version rather than silently changing v8;
3. expose baseline, effective allowance, used and remaining counts from the server;
4. increase only the rolling daily match allowance—never fielded slots, worker
   concurrency, pairing priority, rating or in-match power; and
5. keep practice under its existing abuse-protection limit and outside the paid ranked
   allowance.

Whether the purchased increment applies per account or per fielded sheet must be ruled
before a price exists. Per-account is safer against the current three-sheet multiplier;
the API should allocate that pool explicitly rather than hiding it in pairing order.

## Verification

- generated API clients regenerated cleanly after adding the projected daily baseline;
- web tests: 428 passed;
- production web build: passed;
- solution build: passed with zero warnings;
- App tests with the PostgreSQL suite required: 283 passed, 3 unrelated
  container/S3 integration tests intentionally skipped;
- DocDrift tests: 24 passed;
- production browser smoke: passed; hosted practice completed, returned to the saved
  draft and recovered 16 authoritative unit tracks;
- `git diff --check`: passed before the final documentation update and rerun at the
  final checkpoint.
