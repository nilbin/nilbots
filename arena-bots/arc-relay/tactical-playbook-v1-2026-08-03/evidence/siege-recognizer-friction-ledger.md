# Siege-recognizer authoring friction ledger

Recorded while authoring `siege-recognizer-v1` as pure data against the
frozen interpreter and standard-v1 library (goal law: gaps are documented,
never patched mid-goal). Author: the coordinator, running the Stage 2 goal.

## Validator rounds to first successful compile: 8

1. Maneuver track `movement` rejects `arrivalRadius`/`completion` — those
   are assignment-level fields. The track/assignment field boundary is
   learnable only by error.
2. Movement `pace` enum is `slowest|leader|free`; there is no `fastest`.
   The natural authoring instinct ("couriers hurry") has no direct word.
3. Every custody policy requires `safeConversionConditionSetId`; there is
   no permissive default. A "just deliver it" custody needs an explicit
   `[[always]]` condition set.
4. Role `deathPolicy` must pair exactly with group membership `casualty`
   (`hold-vacancy` roles cannot join `promote-role` groups). The
   cross-validation is good; the legal pairing matrix is undocumented.
5. Formations must supply placement slots covering EVERY role's maximum,
   including roles the maneuver never routes through that formation.
6. Placement offsets are one global coordinate space per formation;
   sector labels do not partition them. Overlap across bands is an error.
7. An order's `pace` must equal its formation's cohesion `pace` — the
   same fact is declared twice and must agree.
8. Layouts require at least one anchor and `corridorWidth` on every
   route, even when unused by any maneuver.

## Structural friction (the important ones)

9. Layout bindings key on match-contract fingerprints, which are
   composition-pair-sensitive. A NEW pairing therefore faults the
   OPPONENT at life-start until its (frozen) layout gains bindings for
   the new fingerprint. Frozen sheets cannot meet new opponents without
   a per-pairing "evaluation edition" (layout copy + appended bindings +
   sheet copy re-referencing it). Manageable in the lab via generated
   editions; hostile to any many-entrants product ladder. This is the
   single strongest argument for a future binding mechanism that keys on
   (map, ownReactorSide) rather than the full contract fingerprint.
10. Fingerprints are only resolvable via `--print-contract` on a sheet
    that already compiles, which itself requires format-valid binding
    hashes: authoring bootstraps through placeholder hashes. Workable,
    but nothing documents the dance.
11. The executor's signature layer hardcodes signature ids, and a
    signature it does not know about simply never casts — no error, no
    telemetry, just a class playing gun-only. Writing a match-start
    coverage assertion (every rules-contract signature must be
    categorized, role-handled, or explicitly listed unwired)
    immediately surfaced four ALREADY-dead kits: trip-node (minesmith),
    sentinel-seed (nest), exchange (switchback), smoke-canister (veil).
    Silent kit death is the default failure mode of any hand-maintained
    executor; the assertion converts it to a first-screen-game loud
    failure. The deeper fix is engine-level: signature metadata in the
    rules contract (category, argument kind) so executors can dispatch
    generically instead of maintaining tables per class generation.
12. An unconditional executor behavior change silently rewrote a frozen
    champion's game: the v2 relay forward-pass flipped Home Siege v3's
    east-orientation parity game from a tick-524 win to a tick-599 loss
    (west it merely sped the win, 560→448 — same change, opposite
    signs by orientation). The A/B that caught it: archived pre-v2 mind
    source run as a sandbox bot against the same seed. Lesson now
    encoded as a pattern: executor behavior ADDITIONS ship sheet-gated
    (custody `forwardPass: relay-catcher` opt-in); frozen sheets that
    never declared the field replay byte-faithfully, and only opting
    sheets get the new play. Re-baselining every frozen champion after
    ANY executor edit is not optional — the executor is part of the
    strategy artifact even though it lives outside the sheet.
13. The four dead kits are wired (owner directive 2026-08-05): trip-node
    and sentinel-seed as clamped placements toward the focus target,
    smoke-canister on the threat, exchange as a guarded escape swap with
    a healthier, safer teammate; the idle support path now also lays
    deployables. Single-swap probe games confirm every kit fires
    (trip-node x24, sentinel x12, smoke x14, exchange x5) — and three
    of four swaps BEAT the parity control where current Breakwater
    loses, so the new classes are live tuning material. Sheet-design
    fact learned: deployables cannot be placed on protected pads (the
    engine forbids tagged tiles), so a minesmith anchored inside a pad
    shelter casts nothing — deploy classes belong on approaches, not
    on the pads themselves.

## Positives worth keeping

- The strict validator caught real inconsistencies (pace conflicts, role
  coverage gaps, dangling custody references) before any match ran.
- Template-cloning from home-siege-v3 plus the standard-v1 library made
  a from-scratch archetype expressible in one authoring session: v0
  compiled after 8 rounds and immediately played a competitive,
  fault-free 599-tick match against Home Siege v3.
- The recognition thesis was expressible entirely in existing facts:
  `remembered-enemies-in-zone` over an authored approach zone with
  parameterized thresholds and stable-tick hysteresis needed no new
  interpreter vocabulary.
