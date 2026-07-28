# Nilbots CLI

Build autonomous C# bots locally, fight them in the official WebAssembly
sandbox, inspect deterministic replays, and submit them to
[nilbots.com](https://nilbots.com).

## Install

```bash
dotnet tool install --global Nilbots
```

The tool requires the .NET 10 SDK. Docker is additionally required for local
WASM compilation on macOS and Linux arm64.

## Create an account and submit a bot

```bash
nilbots register
nilbots new MyBot
cd MyBot
nilbots play --bot . --opponent hunter
nilbots submit .
```

`nilbots register` opens the secure registration page in your browser, completes
OAuth Authorization Code + PKCE, and returns you to the terminal signed in.
(No browser — CI, a container, an agent? Pass `--email` and `--password` and the
same sign-in completes without one.) `nilbots submit` creates the bot on
nilbots.com when necessary, submits its source for the canonical server build,
and compares the local and server WASM artifacts.

The **server** build is the one that plays, and it should be byte-for-byte the
same artifact your machine produced: `submit` prints both hashes and tells you
whether they match. Matches are a pure function of the artifact, the map, the
rules and the seed, so a replay always reproduces exactly. If the hashes ever
disagree, the server's is canonical — and it means your CLI and the server are on
different SDK versions, which `nilbots doctor` will tell you.

Already have an account? Run `nilbots login`. Both commands use
`https://nilbots.com` by default; `--server <url>` is available for local or
self-hosted development.

## Local Frontline experiment

CLI 0.9.0 includes the explicitly separate, unranked Frontline authoring loop:

```bash
nilbots experiment frontline \
  --bot frontline-rusher \
  --opponent frontline-bastion \
  --seed 42
```

One submitted `IActorBot` policy controls a team of independently instantiated
body lives that can fabricate children and Anchor them as turrets. The command
accepts actor built-ins, an actor project directory, or an actor-protocol WASM
artifact. It writes canonical replay-v2 JSON and a self-contained Canvas2D
viewer. Use `--runtime in-process` for fast diagnostic iteration and confirm in
the default isolated WASM runtime.

This is not an alias for `play`: it cannot rank, submit, or enter a server
match, and `frontline-alpha-1` is absent from shipped rules and map catalogs.
See `nilbots help experiment` and the packaged
`docs/EXPERIMENTAL-FRONTLINE.md` contract.

Source and issue tracker: [github.com/nilbin/nilbots](https://github.com/nilbin/nilbots)
