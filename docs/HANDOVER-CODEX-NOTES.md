# Codex class-first-class handoff

Branch: `codex/class-first-class`

Implementation commit: `36cc3b1` (`Add profile-3 class observability contract`)

This branch is complete, but retains the handover's delayed land window: merge
it only in the single SDK-bump window between phase 1 and phase 2.

## Compatibility boundary

- `generic-actor-match-2` / observation profile 2 remains frozen.
- The historical Frontline v1 canonical match fingerprint remains
  `cf10fe4929d8cd11cace95e62b07d9732fbd1549dc2e9fe096f78605028ca837`.
- Newly generated profile-2 Frontline replay bytes are pinned against the
  historical `generic-frontline-replay-v3.json` fixture.
- Typed class identity and the complete observation ledger ship together in
  exact profile 3: class IDs, hold owner/remaining ticks, spawn reservations,
  and projectile cadence/damage.
- Hosted Frontline v1/profile 2 remains registered beside new v2/profile 3;
  the seeder validates both identities.
- No DECISIONS number or CLI version was minted on this branch.

## Verification

- `dotnet build BotArena.sln --no-restore`: clean, zero warnings.
- Full solution tests: Engine 1,074; SDK 51; Guest 16; Wasm 56; App
  167 pass with 74 environment-skipped; Determinism 17; CLI 53.
- `web`: 274/274 tests pass.
- `web`: production and four scoped CLI viewers build successfully.
- `git diff --check`: clean before the implementation commit.

Direct coverage includes a real profile-3 fabrication reservation observation
and a direct chronology rejection for a forged Frontline hold boundary.
