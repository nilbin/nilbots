# Arc Relay Orders v2 pass

Date: 2026-08-12  
Branch: `codex/editor-sweep-followup`  
Worktree: `/Users/sebastian.lind/hobby-projects/nilbots-wt/no-carrier-deadlock`  
Base: `main` at `5de215fa79c0d524ea4ff1261cafb85c7b7d3b99`  
Implementation checkpoint before this report: `2554dc662c8557656d0657541b9e506a9b51064d`  
Status: implementation and evidence complete; not merged

## Outcome

Arc Relay now has one direct authoring language rather than a new surface that
translates back into the old one. An Orders v2 sheet assigns the eight exact
class bodies to roles, gives each role one ordered tree of intent, and exposes
only five actions:

```text
move  fight  use  handoff  drop
```

The shared compiler validates and links that tree as ATL2. The tactical mind
executes ATL2 directly. Doctrine, custody, mode, rung, engagement, formation,
group, cohort, focus, support-policy, and old task-machine records are not
generated as an intermediate language.

There is deliberately no v1 parser or compatibility translation on the active
path. Historical source files needed to explain frozen history may remain in
the repository, but current compilation, templates, admission, runtime, and
authoring all require Orders v2.

This branch is based on `main`; it was not developed in the main checkout.

## Final language

The normative contract is `docs/SPEC-SHEET-ORDERS-V2.md`; the player-facing
contract is `docs/SHEET-AUTHORING.md`.

The orders document has exactly these seven root keys:

```text
schema, sheetId, mapPlan, roles, sharedOrders, capacities, conditions
```

The map-plan document has exactly these six:

```text
schema, mapPlanId, mapId, zones, routes, anchors
```

The important semantics are:

- `roles[].classes` is the composition: flattening it yields exactly eight
  bodies, with the two-copy class cap enforced. There is no second composition
  declaration or assignment policy.
- A role owns an ordered `orders` list. The first executable leaf wins.
- A node may contain at most two branch levels plus a leaf. Authored ids form
  the trace path; generated `rung-*` labels are gone.
- Every node uses the same lifecycle vocabulary: `when`, `until`, `timeout`,
  and `cooldown`.
- Named conditions are finite typed comparisons. `all` and `any` combine
  names explicitly and do not nest.
- Shared orders are exact reuse—no parameters, overrides, inheritance, or
  shared-to-shared references—and require at least two expanded consumers.
- Capacity has one meaning: at most N bodies may concurrently claim the same
  resolved target through leaves sharing that capacity. It applies uniformly
  to Cores, enemies, allies, leaders, Wells, map geometry, placement tiles,
  and carried Cores.
- `move` expresses destination, patrol, or follow intent, optionally through a
  one-time `via` route and with `front`/`line`/`rear` pre-combat posture.
- `fight` owns target preference, `advance`/`hold`/`kite`, and a chase limit.
  Entry, exit, and retry behavior come from the common lifecycle rather than a
  hidden fight block.
- `use` invokes the class's real signature skill with a typed target selector.
- `handoff` and `drop` are explicit. Carrying never implicitly enables combat
  release or a transfer.

Composition, normalized map hash, source hashes, and package hash are derived.
Players do not author or maintain them.

## Core rules boundary

Automatic pickup by walking onto a loose Core and automatic banking by walking
into the owner's reactor remain physical game rules. They are not disguised
action verbs.

Voluntary `drop`, `handoff`, and Arc Toss now share one engine-owned reclaim
invariant keyed by body life and Core id. The releasing body cannot immediately
reclaim that same Core during the existing relocation interval; allies and
enemies still can. Drop chooses a legal adjacent tile toward the owner's
reactor rather than the carrier's own tile. Direct engine tests pin both the
handoff and drop cases.

## Compiler and executor

The old consolidated compiler and runtime machinery was removed rather than
adapted. The implementation commit deleted the task machine, playbook package,
direct-ladder bridge, and the custody/coordination/detachment/escort/formation/
membership primitive files. Its core diff was 3,193 insertions and 15,704
deletions.

ATL2 contains the normalized authored order tree, compiled conditions,
capacity identities, role/class assignments, and normalized team-relative map
geometry. It contains no v1 IR. The runtime selects and acts from that package.

Command reasons now speak the sheet, for example:

```text
orders:builder > collect-loose-core:move:core:centre:1 via North
orders:builder > fight-visible-enemy:fight:1:3:2; cover (18,18)
orders:builder > recover-health > reach-healing:move:heal:9:19 via West
orders:builder > score-a-core > return-to-reactor:carry-home via West
```

Every submitted action and deliberate wait retains a non-empty reason.

### Linked payloads

| sheet | source lines | ATL2 bytes | share of 65,536 | headroom | package SHA-256 |
| --- | ---: | ---: | ---: | ---: | --- |
| Starter | 51 | 2,109 | 3.2% | 63,427 | `4f216a66a57dcc691bb6d5717705a7941c8d88e60ccb76a320399a7b34b2a7d2` |
| Hunter | 127 | 3,796 | 5.8% | 61,740 | `b84b7dbab39f2cdcfefb8fd38f45635b96785d508fa984b64c46577e7837330c` |
| Wellwright | 80 | 3,394 | 5.2% | 62,142 | `06c9bcb702bdcd16b9f5a009363997c63eae2d124217f1823d3686f7508af932` |

The previous direct Hunter gate used 14,548 bytes; Orders v2 Hunter uses 3,796.

## Current sheets

Three Tapline-native Orders v2 pairs ship:

- `starter-tapline-v2`: a deliberately small playable starting point;
- `hunter-tapline-v2`: a mixed team with explicit scoring, recovery, combat,
  loose-Core collection, signature use, and fallback positioning;
- `wellwright-tapline-v2`: the stock farming/scoring opponent.

All authored geometry has a consumer and is drawn directly on Tapline. There is
no coordinate projection from another map.

The standard six-cell exhibition completed by reactor destruction in every
cell; neither sheet timed out:

| cell | Hunter side | result | end | pulses | integrity | charge |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| `w-9001` | west | win | 448 | 3–2 | 1–0 | 0–0 |
| `w-9002` | west | win | 470 | 3–2 | 1–0 | 0–2 |
| `e-9005` | east | loss | 433 | 2–3 | 0–1 | 0–0 |
| `e-9009` | east | loss | 407 | 2–3 | 0–1 | 0–0 |
| `w-9012` | west | loss | 415 | 2–3 | 0–1 | 0–0 |
| `e-9004` | east | win | 322 | 3–1 | 2–0 | 0–0 |

This 3–3 record is only an honest disclosure of six hand-picked exhibits. It
is not a battery, a balance verdict, or a fun claim.

The phone gallery is live at:

<https://lessons-hotel-thursday-growth.trycloudflare.com/>

Each broadcast slice is 99–138 KiB, below the 300 KiB per-match budget. The
served production directory is 78 MiB because it also contains the existing
3D fleet and site assets; this pass did not redesign static gallery packaging.

## Determinism

The controlled current and frozen stock artifacts are byte-identical:

```text
WASM SHA-256        d9281f6abee1f5f5e3eeebbdc911dac091138199dea91feb7a8a0812d1549b88
build-input SHA-256 76917d1516cce340d90cb29433b0163a5028b6f53d00e36c35feb9fceb856d81
artifact bytes      5,006,467
```

For Tapline seed 9001 with Wellwright west and Hunter east, both runtimes end
at tick 418 with Hunter winning by reactor destruction. Their gameplay-state
digest is exactly:

```text
0aaccd6ed28a00ecfbc6d401915b0834b99c5ea9bb0116d7cedaf689f9cdc947
```

The reverse-side cell (Hunter west, Wellwright east) ends at tick 448 and has
the same digest in both runtimes:

```text
2a0511929ee3a0a0be0f77d8abebe65b477c80fd4a845559293ec4f4904740fe
```

Outer canonical replay hashes differ between in-process and WASM because the
replay intentionally records runtime/artifact provenance. The dedicated mind
runtime parity test normalizes only those participant provenance fields and
then requires byte-identical replay content. Gameplay identity is equal without
normalization.

## Hosted/App surface

Hosted entrant playlist v20 pins Tapline, ATL2, the current rules line,
`wellwright-tapline-v2`, and the frozen artifact above. Playlist v19 remains
immutable at fingerprint:

```text
b16b7d62bd0868371edd39b49fc3b6897ef49c44a033e5b91c46bd4a511463b1
```

Because the executable language, stock sheet, and stock gameplay changed, v20
starts a fresh rating book. Historical playlist readers remain; they are not an
authoring compatibility path.

The App contract now uses `ordersJson` and `mapPlanJson` consistently. Save,
trial, entrant projection, admission, and season lifecycle derive composition
from `roles[].classes` and validate through the shared compiler. A PostgreSQL
integration run caught and fixed the last stale composition projection before
the checkpoint. Generated web, mobile, and CLI clients are regenerated and a
second generation produces no diff.

CLI/package version is 0.9.59. No publish or deploy workflow ran;
`publish-cli` owes `cli-v0.9.59` before any deployment.

## Sheet workshop

The current editor was rebuilt around the actual Orders v2 pair, not patched to
look like it:

- eight visible class/signature cards and exact role assignment;
- ordered, collapsible tactical cards with nested plays in context;
- action cards for the five legal actions instead of generic select boxes;
- class-aware signature controls;
- named condition, shared capacity, and exact shared-order libraries in focused
  modals rather than permanent form columns;
- a Tapline plotter that shows routes, zones, anchors, Wells, heal tiles,
  reactors, delivery zones, and both spawn sides;
- west/east binding preview and last-run tracks;
- one persistent primary Save action, with Practice styled as secondary and
  scenario controls in one place;
- raw JSON import/export moved out of the primary authoring surface;
- phone action dock, 44+ px targets, visible focus, no hover-only operation,
  and no horizontal page overflow.

The browser smoke drives the rendered UI rather than importing editor helpers.
At 1180×900 and 390×844 it creates a sheet, edits a posture, creates and removes
a nested play, edits a named condition, opens the map, reloads and restores the
draft, saves it, launches Practice, reaches the replay viewer, returns through
the editor-specific Back control, and finds the same draft/revision with a
one-tap `Practice again` action.

Evidence is in `/tmp/nilbots-orders-v2-editor-smoke/`, including:

- `orders-v2-squad-{desktop,phone}.png`
- `orders-v2-map-{desktop,phone}.png`
- `orders-v2-restored-{desktop,phone}.png`
- `orders-v2-practice-{desktop,phone}.png`
- `orders-v2-returned-{desktop,phone}.png`

The controlled site-review fixture simulates HTTP persistence and navigation
only. It is not presented as a real match. Real compile/save/trial behavior is
covered through the App/PostgreSQL suite; the public exhibit contains actual
engine matches.

## Verification

| command/surface | result |
| --- | --- |
| `dotnet build BotArena.sln --no-restore` | passed, 0 warnings/errors |
| `dotnet test tests/BotArena.Cli.Tests` | 171 passed |
| `dotnet test tests/BotArena.Engine.Tests` | 1,461 passed |
| `dotnet test tests/BotArena.Determinism.Tests` | 17 passed |
| `dotnet test tests/BotArena.Runtime.Wasm.Tests` | 68 passed |
| App suite with disposable PostgreSQL 16 and required-Postgres gate | 284 passed, 3 skipped external S3/toolchain fixtures |
| `bash scripts/generate-api-clients.sh` | passed; second run clean |
| `cd web && npm test` | 460 passed |
| `cd web && npm run build` | passed |
| desktop and phone browser edit → run → watch → edit smoke | passed |
| controlled in-process/WASM stock cell | same terminal facts and gameplay digest |
| `git diff --check` | passed before report commit |

The disposable PostgreSQL container and the private site-review server were
stopped after validation. The exhibit server and the Cloudflare process started
for this report remain running on local port 8976.

## Deliberate next goal

This pass does not claim to solve tactical traffic. Choke negotiation,
intent-aware right-of-way, lane sharing, combat-space reservation, and richer
coordinated path planning remain a separate next goal. That separation is
intentional: Orders v2 expresses player strategy; tomorrow's work should make
the executor perform the requested strategy intelligently without smuggling
micro-management back into the sheet.

