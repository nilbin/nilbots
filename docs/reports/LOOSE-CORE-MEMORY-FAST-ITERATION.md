# Loose-Core memory fast iteration

Date: 2026-08-10  
Branch: `codex/traffic-detours-corner-strict`  
Review gallery: <https://means-via-webcams-milton.trycloudflare.com>

This was an exhibit-first correction to an owner-observed Tapline scene, not a
balance run. In west-9002, Hunter transferred a Core near its own north Well;
the drop left vision one tick later, its pickup reservation disappeared, and
the Core remained on the ground for 94 ticks. The map and sheets were left
unchanged.

## Shipped candidate

- Every observed loose Core remains a last-known pickup objective through fog
  for `memory.securedCoreTicks`.
- Visible empty ground, a visible carried Core, an observed pickup or bank, or
  expiry clears the remembered objective and its reservation.
- The causal movement reason is `custody:last-known-pickup`; remembered state
  is not reported as current vision.
- Friendly `dropRecovery: nearest-authorized` ranks eligible bodies by distance,
  then carrier preference, then stable unit id.
- Ordinary Well pickups keep their existing preference-first default. The new
  optional custody `pickupSelection` exposes `carrier-preference` and
  `nearest-authorized` to sheet authors without changing existing sheets.

## Exact-cell and exhibition evidence

The exact `tapline-1`, west-9002 cell was rerun first. Because the new recovery
changes earlier positions, the original tick-317 sequence does not recur
byte-for-byte. Hunter's voluntary drops in the corrected cell are resolved
after 3, 19, 3, and 2 ticks; its repeated near-Well transfer scenes resolve in
2-3 ticks. The trace contains 62 `custody:last-known-pickup` body commands.

The standard six-cell exhibit was then rebuilt with the same Tapline sheets and
seeds. Every cell completed. Aggregate stationary body-tick share is
45.4%-48.2%; the checked west-9002 cell's worst single-tile stall is 23 ticks,
with zero bodies above 100 ticks and zero delivery-timeout ticks. Some contested
or expired-memory drops remain late or unresolved at match end and are visible
in the gallery; they were not filtered out. These are mechanism observations,
not an outcome or balance claim.

## Runtime and artifact check

The registered Wellwright-vs-Wellwright `ambush-warren-11`, seed-9001 cell has
the same gameplay-state digest in-process and in sandboxed WASM:

`e38b5adbd693dffe0200d8481c34bad60205917dbc6c46c93534f33d53abc18f`

The controlled build produced:

- artifact SHA-256:
  `68182fd208eee987898b42069d97c300fd41aec76c90f42f7c58001c40c10348`;
- build-input SHA-256:
  `5fee8282e71be3ece6f50f01c1095e363560e06d23d64a6e2ec80ddaf009c86c`;
- artifact bytes: 8,748,026.

Because the stock digest differs from v16, playlist v17 uses frozen stock v11
and starts a fresh rating book. v16 and stock v10 remain immutable.

## Fast verification

- final targeted custody, movement, compiler, toolchain, playlist-definition,
  and packaged-version tests: 74 passed;
- tactical mind and App builds performed by those tests: passed;
- controlled WASM build: passed;
- in-process/WASM gameplay-state digest equality: passed;
- `git diff --check`: passed.

The full repository suite, PostgreSQL integration half, and any balance battery
were intentionally omitted in this fast iteration. `publish-cli` owes
`cli-v0.9.45` and `cli-v0.9.46` before deployment.
