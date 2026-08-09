# Direct-ladder semantic intent labels

Date: 2026-08-10

Branch: `sheet-bulk`

Base: `a555c1de`
Status: implementation complete; not merged

## Result

Direct doctrine ladders now keep two deliberately different identities:

- `rung-<role>-<position>-<verb>` is the private exact-life scheduler key;
- a semantic id derived from authored role and intent is the executable and
  spectator identity: `hunter-shadow-hunt`, `hunter-strike-line`,
  `medic-follow-hunter`, `carrier-centre-well`, and so on.

The body caption removes its redundant role prefix, so the field and panel say
`shadow-hunt`, `strike-line`, `follow-hunter`, or `recover` instead of
`rung-hunter-4-patrol`. The full role-qualified id remains in command
provenance and selected-order diagnostics, where global uniqueness matters.
`--explain` shows both ids so a compiler/editor diagnosis can connect authored
priority to the published intent. A compiler regression test compares every
non-fetch explain id with the runtime order ids, preventing the two deterministic
derivations from drifting.

## Hunter behavior finding

No Hunter tuning landed. `hunter-v1.json` is byte-identical to `a555c1de` at
SHA-256 `7277ee5e34d04305e651f6538e74b8861b5253808b08b348306e23e95bb3bf66`.
Its existing fight policy remains leash 16 with 30 persist ticks.

The apparent loss of the old north opening was a side/presentation read, not a
ladder regression. In the previous bulk gallery, Wellwright was the sheet under
review, so the first `w-9001` card put Hunter on east. Team-relative
`rotate-180` correctly mirrors its world-space north route south. Exact raw
state reads for west-side Hunter unit 0 show the legacy and first direct
executors have identical positions and accepted actions through ticks 0–13:
two northeast steps, an east vector dash, a short west/north rejoin, six steps
north to y=1, then east along the north corridor. Against the now-direct
Wellwright, the final exhibit retains that opening through y=1 before visible
combat can pull Hunter under its already-authored long chase.

The semantic-label-only same-cohort `w-9001` control retains gameplay-state
digest
`4015ac280c54f10b9221ff2f0c805d816aea3ffe766acb6040a23902f6d21bb8`
before and after. Full replay hashes change because role tags, command reasons,
artifact provenance, and WASM bytes are recorded facts.

## Artifact and hosted immutability

The controlled macOS Docker build produced:

```text
build input  a63314ed3de6599fd96b3ccaae5a527eb96364e1950f25decec8b4241bcda4ff
artifact     c2b2f0a92f34d2d006bd4e7ecf8d68d0750df89300db7e23f33ef111d793a9a0
bytes        8,432,262
```

Immutable playlist v9 and `ambush-warren-11-stock-v3` remain unchanged.
Playlist v10 pins new frozen `ambush-warren-11-stock-v4`; the App stock seeder
retains v1–v3 inactive and makes only v4 active. The v9 definition fingerprint
is pinned by regression test as
`a2024fd4234b5feb80419a97ada57245d1a8f33b319d34bd04d9aeab9eb24799`.
Because no gameplay fact changed, an open Season 1 copies ratings and ranked
match counts from v9 to v10, then closes v9 to new pairing. Since neither v9
nor v10 has landed on `main`, the seeder also covers the honest first-deploy
case: it may continue directly from open v8 only when no v9 ladder was ever
registered. A registered v9 may not be skipped. PostgreSQL integration runs
both legal predecessor paths.

The final `w-9001` in-process and WASM runs both ended at tick 484 and have
gameplay-state digest
`e77580f1cfde513a530c4b7972442a7626a80cf45a9aff4cb1385108817ac4c9`.

## Exhibit

[Open the six-cell outcome-visible gallery](https://kong-criterion-gulf-boat.trycloudflare.com/).

Hunter is the sheet under review. `w-9001`, `w-9002`, and `w-9012` put it on
west; the first card explicitly calls out the north-corridor opening. The east
cards explain the intentional 180-degree mirror. Every card names the intended
trigger, tactic, counterplay, fallback, and item to watch. This is an exhibit,
not a battery or a balance verdict.

A Playwright phone smoke at 390×844 loaded and played all six WebGL replays,
found the score on every card, and reported zero page, console, request, or
horizontal-overflow errors. Detached server and tunnel sessions run on local
port 8990.

## Validation

Validation results are recorded at the final checkpoint below. `publish-cli`
owes a `cli-v0.9.39` tag before any release or deploy; none was run here.
