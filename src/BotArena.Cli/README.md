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

The **server** build is the one that plays. Its bytes may differ from your local
build — the two compile in different environments — while the bot behaves
identically: matches are a pure function of the artifact, the map, the rules and
the seed, so a replay always reproduces exactly. Byte-for-byte reproducibility
between your machine and the server is a goal we have not reached yet; treat the
server hash as canonical.

Already have an account? Run `nilbots login`. Both commands use
`https://nilbots.com` by default; `--server <url>` is available for local or
self-hosted development.

Source and issue tracker: [github.com/nilbin/nilbots](https://github.com/nilbin/nilbots)
