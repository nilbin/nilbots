# Season and entrant information-architecture pass

Date: 2026-08-08  
Implementation checkpoint: `4839d16e`  
Scope: hosted App and web product surface. No gameplay, map, sheet grammar,
compiler, rating calculation, canonical replay, broadcast, or mobile-native
feature change.

## Outcome

Arc Relay now has one product hierarchy:

**Season · Entrants · Watch · Docs**

The current Season is the public home. Entrants owns sheets, custom minds and
the sheet workshop. Watch owns match viewing. Arc Relay is the name of the
game, not a second navigation branch. Historical Duel records remain available
through the explicitly labelled legacy archive without being promoted.

Season 1 is a real authoritative competition rather than presentation over the
development ladder. It runs for eight weeks on hosted entrant playlist v8. At
opening, every fielded entrant receives a new 1200 rating and a zero-match
Season record. Existing v6, v7 and pre-Season v8 records remain readable in the
database, but are neither carried forward nor presented as a fake Preseason.

## Season lifecycle and rating book

The persisted lifecycle is:

`Draft -> Scheduled -> Open -> Closing -> Closed`

- Opening runs under one PostgreSQL transaction and advisory lock. It closes
  and unlists previous Arc Relay ladder admission, creates the fresh current
  rating book, and records an opening snapshot for each fielded entrant.
- Equal ratings receive the same competition rank. The stable entrant ID is
  only an ordering tiebreaker; it does not fabricate a different rank.
- An entrant fielded after opening gets an explicit late-entry record, default
  rating and no invented opening rank.
- The wall clock moves an open Season to Closing and closes admission first.
  Closing waits for every non-failed ranked result capable of moving rating to
  settle at the reveal boundary. It then freezes closing rating, rank,
  revision, crest, name and composition. Failed matches cannot strand closure.
- Every transition is re-entrant and serialized. Re-running startup or the
  pairing job does not reseed ratings or duplicate snapshots.

Revisions remain revisions of the same entrant and retain Season rating.
Save-as-copy remains a new entrant. Account-owned scrimmages remain unrated,
and the existing combined limit of three fielded sheet/custom-mind entrants per
account remains server-enforced.

The new `ArcRelaySeasonEntrants` table is the durable opening/closing bookend.
The append-only settled ranked-match rows remain the rating trajectory ledger;
the current value remains in `ArcRelayEntrantRatings`. This avoids duplicating
the rating engine or manufacturing history.

## Reveal-safe projection

`GET /api/arc-relay/seasons/current` is the shared current-Season projection.
It returns the lifecycle, time window, ladder identity, entrant cards, tied
ranks, movement from opening, W-D-L, rating trajectories and recent results.

Only settled ranked rows enter W-D-L, a trajectory, movement, a recent result
or a derived rank. A queued, simulated, completed-but-unrevealed or otherwise
unsettled match changes none of those surfaces. Entrant presentation reuses the
same bounded-query projector as cards and ladder reads, preserving owner-only
and suspension secrecy.

Generated web, mobile and CLI contracts were regenerated from the App endpoint
schema. The mobile diff is generated schema lockstep only; no mobile UI was
added.

## Product hierarchy

| Job | Canonical route | Product responsibility |
| --- | --- | --- |
| Follow competition | `/` | Season clock, fielded entrants, standings, settled results |
| Manage competitors | `/entrants` | Sheet and custom-mind entrant roster |
| Author a sheet | `/entrants/sheets/new`, `/entrants/sheets/:id/edit` | Existing edit -> run -> watch loop |
| Watch matches | `/watch` | Arc Relay match list and viewer |
| Learn the product | `/docs` | Current rules, vocabulary and authoring guidance |

Old `/relay` and `/relay/sheets` bookmarks redirect into the canonical routes;
they do not render a duplicate hierarchy. The desktop and phone shells expose
the same four labels, with visible focus and existing coarse-pointer control
sizes intact.

The entrant overview no longer repeats a ladder, editor and class manual in one
long page. It is now a focused roster. The workshop remains focused and keeps
draft persistence, inline compiler diagnostics, one-tap trial runs, replay
return, and recorded unit-track overlays.

## Browser evidence

The previous product state is retained in the preceding coherence report:

- [Previous entrant overview, desktop](assets/web-coherence-sheet-iteration/after-relay-desktop.png)
- [Previous entrant overview, phone](assets/web-coherence-sheet-iteration/after-relay-phone.png)
- [Previous sheet workshop, desktop](assets/web-coherence-sheet-iteration/after-sheet-workshop-desktop.png)
- [Previous sheet workshop, phone](assets/web-coherence-sheet-iteration/after-sheet-workshop-phone.png)

Final production-build evidence:

- [Season home, desktop](assets/season-entrant-ia/season-desktop.png)
- [Season home, phone](assets/season-entrant-ia/season-phone.png)
- [Season with a fielded entrant, desktop](assets/season-entrant-ia/season-with-entrant-desktop.png)
- [Season with a fielded entrant, phone](assets/season-entrant-ia/season-with-entrant-phone.png)
- [Entrant roster, desktop](assets/season-entrant-ia/entrants-desktop.png)
- [Entrant roster, phone](assets/season-entrant-ia/entrants-phone.png)
- [Sheet workshop, desktop](assets/season-entrant-ia/sheet-workshop-desktop.png)
- [Sheet workshop, phone](assets/season-entrant-ia/sheet-workshop-phone.png)
- [Completed trial replay](assets/season-entrant-ia/trial-replay-desktop.png)
- [Returned draft with 16 recorded tracks, desktop](assets/season-entrant-ia/trial-tracks-desktop.png)
- [Returned draft with 16 recorded tracks, phone](assets/season-entrant-ia/trial-tracks-phone.png)

The tracked production smoke ran against a real App and a database migrated
from empty PostgreSQL. It registered an account, loaded Season at both widths,
created and fielded a sheet, compiled it with the shared compiler, completed
trial match `f77c1326-d75f-40a0-a955-d86bfb8df509`, opened its broadcast-v2
replay, returned to the same draft and found all 16 recorded unit tracks. It
reported no console error, failing request, lost draft or horizontal overflow
at 390x844.

## Build-size comparison

The baseline was rebuilt from pre-change commit `78d4b6f1` with the same local
toolchain and dependency installation.

| Output | Before | After | Delta |
| --- | ---: | ---: | ---: |
| Main site JS | 1,322.08 KiB | 1,330.09 KiB | +8.01 KiB |
| Main site JS, gzip | 383.39 KiB | 385.28 KiB | +1.89 KiB |
| Main CSS | 63.51 KiB | 65.25 KiB | +1.74 KiB |
| Main CSS, gzip | 11.94 KiB | 12.28 KiB | +0.34 KiB |
| `web/dist` on disk | 53,952 KiB | 53,960 KiB | +8 KiB |
| `web/dist-cli` on disk | 24,108 KiB | 24,144 KiB | +36 KiB |
| 3D viewer JS, gzip | 246.08 KiB | 246.08 KiB | unchanged |

No art, model, audio or broadcast asset was added. The small code/CSS increase
is confined to the Season projection and product shell; the parked 3D viewer
and gameplay payload budgets are unchanged.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build BotArena.sln` | passed; 0 warnings, 0 errors |
| PostgreSQL-gated App suite | 282 passed, 0 failed, 3 unrelated container/S3 integrations skipped |
| Full `dotnet test BotArena.sln --no-build` | 2,138 passed, 0 failed; App PostgreSQL tests intentionally rerun separately above |
| Focused Engine DocDrift suite | 24 passed, 0 failed |
| `cd web && npm test` | 425 passed, 0 failed |
| `cd web && npm run build` | passed; production Vite and all four scoped viewer builds |
| `bash scripts/generate-api-clients.sh` | passed; before/after generated-set hash identical |
| Production browser smoke | passed against migrated PostgreSQL and real hosted trial execution |
| `git diff --check` | passed before the implementation checkpoint |

## Deliberate boundaries

- No history selector or Preseason was added because no real closed public
  Season exists yet. The schema and snapshots can support a future honest
  history surface after Season 1 closes.
- Season dates are authoritative server data, not browser configuration.
- This pass does not change pairing policy, Elo math, gameplay, the Warren,
  sheet grammar, compiler behavior, canonical replays or broadcast content.
- No release, deployment or mobile-native workflow ran.
