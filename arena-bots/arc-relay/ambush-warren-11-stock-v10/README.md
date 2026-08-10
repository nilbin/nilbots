# Ambush Warren 11 stock v10

This directory freezes the tactical-playbook mind used by hosted entrant
playlist v16. It keeps v15's sticky route detours and corner-strict movement,
then adds a once-per-tick carrier delivery plan, deterministic loaded-carrier
precedence, a bounded fresh-life preference for routes that avoid the reactor,
and a bounded reactor-exit clearance handshake.

The launch preference applies only during the first two life ticks and falls
back immediately when avoiding the reactor provides no usable course. It is not
a permanent reactor obstacle and does not alter the map.

## Frozen identity

- Artifact SHA-256:
  `2554509e0af31b18b6ccbaf94efe5b7e6d0ce4b8ec50e4bdd4b47b1a97039f07`
- Build-input SHA-256:
  `e18085e2fbcdcf6d37eb4bf44c71a2dbeb54a2c04f232345ae5069179012ce62`
- Artifact bytes: `8726112`
- Toolchain SDK / guest adapter: `0.10.13`
- Build pipeline: `5`

The registered Wellwright-vs-Wellwright stock cell on
`ambush-warren-11`, seed `9001`, has gameplay-state digest
`5964c536be8598ea9d51f7bcc3b7e4f0a6e25ab67dee04da168da94e90c509c2`
in both the in-process and WASM runtimes. Playlist v15's registered digest was
`c356c5882fcd86c3646365b15147f18d8c8b494b33bc7e5b3a505f6ab91068a6`,
so v16 starts a fresh rating book.

Do not rebuild or replace `bot.wasm`. A later behavior change must mint a new
stock directory and playlist version.
