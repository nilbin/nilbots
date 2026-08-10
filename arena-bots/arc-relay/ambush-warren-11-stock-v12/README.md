# Ambush Warren 11 stock v12

This directory freezes the tactical-playbook mind used by hosted entrant
playlist v18. It retains v17's bounded loose-Core memory and authored pickup
selection, then repairs two allocation states that could make a visible drop
look irrelevant to nearby couriers.

An accidental carrier with no committed custody uses an empty runtime sentinel.
That sentinel is now normalized to no custody when the Core is dropped, so any
compatible custody policy may claim it. A body already carrying a Core is also
excluded from pickup reservation: it cannot reserve a second Core while an
available authorized courier passes.

## Frozen identity

- Artifact SHA-256:
  `d63ffb4829a6875573232d3cb8822beb8c77c96708bc2e9bbdd39d0aeb42ca96`
- Build-input SHA-256:
  `1229c727f38e78599dbc228e2f8a86117d64a50fced3d155c7398dcf2a02a6f3`
- Artifact bytes: `8747476`
- Toolchain SDK / guest adapter: `0.10.13`
- Build pipeline: `5`

The registered Wellwright-vs-Wellwright stock cell on
`ambush-warren-11`, seed `9001`, has gameplay-state digest
`b844d14c1eea12b576e06324ae9554bddaca3963aa3de95fa8f4b9bd4cc88296`
in both the in-process and WASM runtimes. Playlist v17's digest was
`e38b5adbd693dffe0200d8481c34bad60205917dbc6c46c93534f33d53abc18f`,
so v18 starts a fresh rating book.

Do not rebuild or replace `bot.wasm`. A later behavior change must mint a new
stock directory and playlist version.
