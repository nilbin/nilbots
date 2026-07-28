# Frontline Labs baseline cohort DX synthesis

Status: frozen before tournament disclosure. No author had seen an opponent,
match, replay, standing, or balance result when the four reports were written.

Sources:

- `pressure/DX.md`
- `fabricator/DX.md`
- `bastion/DX.md`
- `adapter/DX.md`

## Authoring outcome

| Entrant | Scaffold to valid WASM | Mechanical repairs | Match feedback used |
| --- | ---: | ---: | --- |
| Pressure | 9m 07s, about 6m excluding shared doc pause | 0 | none |
| Fabricator | about 9m 39s, including one compiler pass and a toolchain timeout | 1 | none |
| Bastion | about 7m 02s | 2 | none |
| Adapter | about 7m 26s | 0 | none |

All four authors produced a controlled WASM artifact from the public generic
surface in one strategy pass. The CLI scaffold, contract-delivered topology
and catalogs, typed legality union, deterministic API, and compiler
diagnostics were sufficient to express four distinct doctrines.

## Resolved before source authorship

### High — Labs did not have a standalone mechanics card

The initial packet linked a correct hosted product boundary and local command,
but the full mechanics sections in the same document were explicitly scoped
to the older `frontline-alpha-1`/`IActorBot` contract. Both active authors
paused after scaffolding and before changing source.

The common packet was corrected for every author with
`docs/FRONTLINE-LABS-RULES.md`. It now gives Labs v1 an independent rule card
and explicitly says the nearby alpha sections are not an implicit Labs
specification. The correction was shared equally and did not count as a bot
repair.

## High-priority common findings

### Constraint-safe decisions are too verbose

All four authors found the action model expressive but unnecessarily costly
to use safely. The scaffold resolves `ActionId`/`ActionCode` and checks
`Available`, but accepts arbitrary arguments. Authors must repeatedly inspect
long nested union types to extract legal directions, unit targets, forms,
headings, and shot programs.

Recommended follow-up:

- add public constraint helpers such as `AllowedDirections`,
  `AllowedUnitTargets`, `AllowedFormTargets`, and `AllowedHeadings`;
- add a decision builder that takes a legality entry and refuses arguments
  outside its typed constraints;
- keep the negotiated code and dynamic catalogs visible rather than replacing
  them with fixed Labs constants.

This should be a versioned SDK/DX improvement after the baseline artifacts are
frozen, not an in-place edit to the evaluated cohort.

### Common contract joins need a public index

Every doctrine rebuilt some combination of:

- form ID to form to attack profile;
- Frontline active index to mode binding to objective region;
- transition role to participant-region assignment to home-pad tiles;
- form transition to placement tags to legal map tiles;
- score channel to timeout ranking direction.

Recommended follow-up: provide an immutable contract index/helper layer with
ordinal lookup and typed Frontline extensions. It should derive from the
resolved contract supplied to `StartLife`, so it remains valid for different
maps, team counts, formats, and future catalogs.

### Navigation is repeated player boilerplate

The scaffold's deterministic BFS was useful to every author, but each had to
extend or recreate objective resolution, occupancy filtering, move
constraints, projectile avoidance, and distance queries.

Recommended follow-up: add an SDK example/helper for deterministic static-map
path distance and legal first steps. Keep strategy-specific threat costs in
bot code.

## Medium-priority findings

### The scaffold teaches only part of the generic action surface

It demonstrates direction and projectile-heading arguments, but not
unit-target, form-target, or shot-program constraints. It also assumes
Frontline and throws when the binding/observation differs, while the generic
contract is intended to survive future modes.

Recommended follow-up: make the starter's fallback non-faulting and add a
small cookbook for every current argument kind, without turning the template
into a complete strategy.

### Authoring documentation needs one shorter entry point

The exact commands appear consistently in the experimental contract and WASM
runtime guide, but the latter quickly enters wire/runtime detail. Add a short
generic-bot authoring page that links outward to the complete rule card,
runtime protocol, replay format, and SDK reference.

The page should also contrast:

- team score versus current capture progress;
- active Frontline position versus territorial timeout score;
- stable unit slot versus active life versus participant artifact;
- Fabricate versus Split versus same-life Anchor.

### Controlled-build progress can go silent

Fabricator's first controlled build spent five minutes in toolchain startup
and timed out with an empty advertised log; an unchanged retry completed in
22.51 seconds. The other authors found the final build summary useful.

Recommended follow-up: write phase/progress lines before Docker startup,
restore, publish, validation, and cache copy, and ensure the advertised log is
created immediately.

## Low-priority findings

- Nullable `ImmutableArray<T>` correctly distinguishes unsupported sensing
  from an empty collection, but its nullable value-type ergonomics are easy to
  misuse.
- Long closed-union type names are clear after discovery but noisy in
  ordinary strategy code.
- The current rule card necessarily makes concrete values memorable. Its
  repeated contract-authoritative warning successfully prevented all four
  authors from structurally fixing player count, unit count, map size,
  objective count, form IDs, or numeric action codes.
- The per-entrant manifest requirement did not define one local JSON shape,
  so the four independently produced manifests use different field names.
  The root `cohort.json` normalizes them, but the next scaffold/packet should
  ship a small entrant-manifest schema.

## What worked

- A clean generic scaffold reached controlled WASM in roughly 6–10 minutes.
- Two bots compiled on the first attempt; all compiler-only repairs were
  understandable without match feedback.
- The exact local Labs runner, artifact hash output, and replay-v3 verifier
  made the evaluation boundary legible.
- The standalone rules card made lifecycle memory, lineage, team perception,
  dynamic counts, action legality, and immutable playlist identity clear.
- Authors consistently avoided numeric action codes and fixed
  team/unit/objective/map shapes while still recognizing optional semantic
  capabilities needed by their doctrine.

## Freeze decision

No DX finding changes the four strategies or their artifacts. SDK/template
ergonomics and build-progress improvements are follow-up work. Tournament
evidence begins with the exact hashes recorded in `cohort.json`.
