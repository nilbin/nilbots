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

CLI 0.9.6 includes the explicitly separate, unranked Frontline authoring loop:

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

## Local Frontline Labs v1

Create generic actor projects separately from shipped Duel bots:

```bash
nilbots new LabsBot --profile generic-actor
nilbots new Rival --profile generic-actor
nilbots experiment frontline-labs \
  --bot LabsBot \
  --opponent Rival \
  --runtime in-process \
  --seeds 104729,130363,155921
```

This command runs the exact immutable hosted `frontline-labs-1` resolved
contract through `GenericActorMatchSession`, then writes canonical replay v3.
It bypasses App accounts, queues, and pilot quotas and remains local and
unranked. Both entrants are required: a generic spec is an
`IGenericActorBot` project or generic-profile WASM artifact, and no generic
built-in opponent exists. Use `--swap` for the other team assignment.

For a registered local numeric arm, `--capture-threshold <positive-n>` creates
a distinct ruleset such as
`frontline-labs-1-experiment-capture-12`. Its changed rules and match
fingerprints are embedded in replay v3; the option never changes or aliases
hosted `frontline-labs-1`.

For a phased pacing arm, `--capture-gain-phase 300:2` retains gain 1 through
tick 299 and uses gain 2 from tick 300. The complete ordered `gainSchedule` is
embedded in the contract, gets a distinct ruleset/fingerprint identity, and is
available to bots through
`frontlineMode.Capture.GainPhaseAtTick(context.Tick)`.

For the action-contract arm, `--mobilize-turrets` adds the declared
`mobilize` action and a one-way `turret -> child-mobile` same-life transition.
It uses ruleset `frontline-labs-1-experiment-mobilize`; health and combat state
are preserved and capped to the mobile form, while that life cannot Anchor
again after Mobilize.

For the fabrication-transport arm, `--remote-fabrication` keeps Fabricate an
explicit Prime action and keeps child output on the protected home pad, but
allows the Prime to queue a Ready child from any walkable tile. It uses
ruleset `frontline-labs-1-experiment-remote-fabrication` and a distinct
experiment map identity because the resolved map publishes the all-floor
source region. This removes the commute without silently converting an
authored action into a system spawn.

For the objective-control arm, `--net-control` keeps form objective weights,
capture threshold, decay, map, and lifecycle rules fixed. Equal team weight
still contests the objective, while a positive weight surplus applies capture
gain multiplied by that surplus. It uses the distinct ruleset
`frontline-labs-1-experiment-net-control`.

For the duel-depth arm, `--one-bend-shots` keeps the map, topology, objective,
combat cadence, and lifecycle fixed while simplifying mobile programs to
straight or one private 45-degree bend after one to four tiles. Initial aim
offsets and repeated bends are unavailable. It uses ruleset
`frontline-labs-1-experiment-one-bend-shots`; the opening through tick 119 is
the native Prime-versus-Prime isolation window before companion unlocks.

Iterate in-process, then build both projects and repeat in the default WASM
runtime before treating results as evidence. `nilbots verify <replay.json>`
cryptographically verifies replay v3, including its exact embedded contract
fingerprints and payload hash.

The versioned local qualification runner can check the current cumulative
tactical profile:

```bash
nilbots experiment frontline-labs qualify \
  --bot LabsBot/out/bot.wasm \
  --suite frontline-qualification-5 \
  --out out/LabsBot-qualification
```

Suite 5 requires WASM, reruns the exact cumulative T3 prerequisite, then
checks suppression, proactive pressure entry, objective-preserving response,
front rotation, and the thin-fronts map holdout. A complete pass awards T4
and entrant-level balance eligibility. Run suite 3 directly for T2 and suite
4 for T3. Frozen suite 1 remains only the historical `entry-initiative` T4
component, while suite 2 remains an incomplete automatic-life/determinism
foundation.

Source and issue tracker: [github.com/nilbin/nilbots](https://github.com/nilbin/nilbots)
