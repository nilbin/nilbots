# Tapline launch and carrier fast iteration

Date: 2026-08-10  
Branch: `codex/traffic-detours-corner-strict`  
Review gallery: <https://means-via-webcams-milton.trycloudflare.com>

This was an exhibit-first correction to two owner-observed scenes, not a
balance run: bodies queued around their own reactor at launch, and loaded
carriers danced or held beside their delivery zone. The Tapline map and both
sheets were left unchanged.

## Rejected broad fix

Treating the friendly reactor as an obstacle for every authored route cleared
the opening queue, but distorted ordinary later pathing and introduced long
stalls. That experiment was removed. The reactor is not a global obstacle in
the shipped candidate.

## Shipped candidate

- During only the first two life ticks, a body prefers a route course that
  avoids its own reactor. When that course is unavailable, the ordinary course
  is used immediately.
- A bounded two-tile reactor-exit handshake prevents an occupant and its queue
  from repeatedly claiming each other's next tile.
- Loaded-carrier destinations and courses are resolved once per tick and shared
  by traffic arbitration and action selection. A blocked carrier holds that
  course rather than falling through into an opposing homeward step.
- Loaded carriers have deterministic precedence by static terrain distance to
  their own bank, with unit id as the tie-break. This resolves head-on
  carrier/carrier traffic without randomness.
- All new movement and hold paths retain non-empty causal `debugMessage`
  reasons.

## Fast evidence

The standard six Tapline cells were generated in parallel through
`scripts/arc-relay-exhibit.sh`; no battery or balance sweep was run.

- all 16 bodies in every cell leave their initial tile by observation tick 7;
- in the inspected cell, 14/16 leave by tick 3, while the two tick-7 bodies are
  facing-locked with one legal initial exit;
- no body has a stationary run above 100 ticks;
- worst single-tile stationary run: 49 ticks;
- `custody:delivery-timeout-*` body-ticks: 0 in all six cells;
- aggregate stationary body-tick share: 43.0%–47.0% across the six cells.

The cells all happen to complete by reactor destruction. That outcome is shown
for honest viewing and is not interpreted as a balance result.

## Runtime and artifact check

The registered Wellwright-vs-Wellwright `ambush-warren-11`, seed-9001 cell has
the same gameplay-state digest in-process and in sandboxed WASM:

`5964c536be8598ea9d51f7bcc3b7e4f0a6e25ab67dee04da168da94e90c509c2`

The controlled build produced:

- artifact SHA-256:
  `2554509e0af31b18b6ccbaf94efe5b7e6d0ce4b8ec50e4bdd4b47b1a97039f07`;
- build-input SHA-256:
  `e18085e2fbcdcf6d37eb4bf44c71a2dbeb54a2c04f232345ae5069179012ce62`;
- artifact bytes: 8,726,112.

Because the stock digest differs from v15, v16 uses frozen stock v10 and starts
a fresh rating book. v15 and stock v9 remain immutable.

## Fast verification

- targeted CLI movement, custody, toolchain, and version tests: 49 passed;
- App playlist-definition tests: 6 passed;
- tactical mind build: passed;
- controlled WASM build: passed;
- `git diff --check`: passed.

The full repository suite and any balance battery were intentionally omitted in
this iteration. `publish-cli` owes `cli-v0.9.45` on the eventual merged revision
before deployment.
