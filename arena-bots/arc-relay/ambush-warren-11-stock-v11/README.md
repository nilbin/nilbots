# Ambush Warren 11 stock v11

This directory freezes the tactical-playbook mind used by hosted entrant
playlist v17. It retains v16's stable carrier delivery and bounded launch
clearance, then makes every seen loose Core a bounded last-known objective
through fog and makes `nearest-authorized` friendly-drop recovery genuinely
distance-first.

The sheet grammar also accepts an optional custody `pickupSelection` of
`carrier-preference` (the unchanged default) or `nearest-authorized`. Seeing a
remembered Core's tile empty, observing it carried or banked, or reaching the
sheet's Core-memory limit clears the objective.

## Frozen identity

- Artifact SHA-256:
  `68182fd208eee987898b42069d97c300fd41aec76c90f42f7c58001c40c10348`
- Build-input SHA-256:
  `5fee8282e71be3ece6f50f01c1095e363560e06d23d64a6e2ec80ddaf009c86c`
- Artifact bytes: `8748026`
- Toolchain SDK / guest adapter: `0.10.13`
- Build pipeline: `5`

The registered Wellwright-vs-Wellwright stock cell on
`ambush-warren-11`, seed `9001`, has gameplay-state digest
`e38b5adbd693dffe0200d8481c34bad60205917dbc6c46c93534f33d53abc18f`
in both the in-process and WASM runtimes. Playlist v16's registered digest was
`5964c536be8598ea9d51f7bcc3b7e4f0a6e25ab67dee04da168da94e90c509c2`,
so v17 starts a fresh rating book.

Do not rebuild or replace `bot.wasm`. A later behavior change must mint a new
stock directory and playlist version.
