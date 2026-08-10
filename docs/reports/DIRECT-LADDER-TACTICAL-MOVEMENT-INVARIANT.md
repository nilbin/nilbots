# Direct-ladder tactical movement invariant

Date: 2026-08-10

Branch: `sheet-bulk`

Starting checkpoint: `bf670286`

Implementation checkpoint: `5ba25659`

## Result

The direct-ladder executor now has one generic, deterministic movement policy
for the failure the owner caught: a wounded Hunter repeatedly alternated
between recovery and threat withdrawal, walked between the same two tiles,
and occupied the north-Well choke while healthier allies tried to pass.

The correction does not name Hunter, a sheet, a route, a class, a map side, or
a coordinate. The sheet continues to choose the body's role, ordered rung,
destination, fight policy, and tactical priority. The executor now owns only
three bounded safety properties:

1. one recovery trigger creates one finite attempt;
2. a stable ordinary intent repeatedly denied by an ally can earn temporary
   right-of-way; and
3. two executor authorities cannot sustain a successful A-B-A-B circuit.

Across the ordinary and coordinated standard six-cell exhibits, the new
successful-motion diagnostic found zero long A-B flap runs for either team in
all twelve cells. This is a mechanism result, not a balance verdict.

## What was actually wrong

The old `w-9001` trace for west unit 0 changed authority almost every tick:

```text
t172       recover-heal
t173       strike-evacuation
t174-t176  withdraw
t177       formation-move@15,10
t178       withdraw
t179       formation-move@15,16
t180       withdraw
...
```

Both moves were individually legal. Recovery re-qualified from the same
unchanged low-health fact after combat moved the body away, while the health
break could re-arm after recovery pulled it back. The body therefore moved
successfully rather than reporting a blocked action. That explains why the
old diagnostic said `chokes 0` while the replay visibly contained a choke.

The updated traffic read separates blocked-move chokes from successful-motion
flaps. On that original replay it reports:

| Side | Moves | Flaps | Reverse moves | Long A-B runs | Worst run |
|---|---:|---:|---:|---:|---:|
| Hunter team | 1,549 | 10.67% | 11.61% | 38 | 18 frames |
| Wellwright team | 1,929 | 7.71% | 12.05% | 21 | 10 frames |

The coordinated stepper could not solve this alone. It arbitrates candidate
paths inside one tick; it had no persistent knowledge that two legitimate
mode authorities had reversed the same life on prior ticks. Both steppers now
consume the same cross-tick movement state before choosing their own path.

## The invariant

### 1. Recovery is an episode, not a standing mode

- An eligible `recover` trigger starts a 24-tick exact-life episode.
- The chosen reachable beacon is latched for that episode; changing heat does
  not make the target oscillate every tick.
- Full health completes and resets the episode.
- An unsafe beacon, the 24-tick timeout, or an actually committed move that
  does not strictly reduce terrain distance to the beacon stops the episode.
- A stopped attempt has a 24-tick retry cooldown for that same injury.
- A fresh health loss increments the injury revision and bypasses the
  remaining cooldown immediately.
- Health-triggered combat break-off uses the same injury revision. An
  unchanged one-health body cannot finish one withdrawal and immediately
  start another forever; new damage is still a new emergency.

This is fixed executor safety behavior, not new sheet configuration. If the
duration ever becomes author-controlled, that requires a separate grammar
design rather than an undocumented extra JSON field.

### 2. Allied traffic gets bounded tickets

For each life the executor records the authored semantic intent, its actual
goal, terrain distance, terrain-shortest next step, and whether an ally owns
that step.

- The identity and goal must remain stable.
- Two ally-blocked observations activate a ticket.
- Tickets are ordered by activation tick and then stable unit id.
- A ticket lasts at most 12 ticks and releases after two strict progress
  steps, even if a hold occurs between them.
- A changed intent, changed goal, or respawn discards the old claim.
- A ticket ranks below a carrier, escort leader, active fight, and repair
  provider, but above ordinary free traffic.
- Recovery, withdrawal, follower, and other yielding channels cannot earn
  hidden seniority. They may clear ground for a ticketed ordinary or custody
  mover. Custody movement may earn a ticket, while a loaded carrier retains
  the stronger carrier rank.

Tickets never invent a destination or participation decision. They answer
only which of two already-moving allied intents temporarily receives a tile.

### 3. Successful oscillation has a circuit breaker

There are two leases with one three-tick window:

- A first safety or explicit make-way displacement softly lends its origin
  against ordinary tactical snap-back. Safety can still reverse immediately
  if the threat picture genuinely changes.
- After two consecutive successful reversals, the just-vacated tile is hard
  blocked for that exact life and every movement authority. Repeated safety,
  carrier, clearance, and tactical reversals therefore all terminate.

Denials produce ordinary explicit holds with causal reasons such as
`movement-lease@290-avoid-19-11` and
`oscillation-yield@359-avoid-12-15`. Recovery traces likewise expose
`stop-movement-preempted`, `cooldown-movement-preempted@...`,
`cooldown-unsafe@...`, and `cooldown-timeout@...`. The per-tick mind trace
also publishes the active traffic tickets and soft/hard motion leases. No
executor action is silent.

## Adversarial regression surface

Thirteen focused tests cover the narrow state machines without constructing a
Hunter match:

- recovery interruption blocks the same injury and fresh damage bypasses it;
- timeout is finite;
- respawn creates a fresh exact-life episode;
- the beacon remains latched;
- two ally blocks activate a ticket and 12 ticks bound it;
- two progress steps release a ticket despite an intervening hold;
- opposite-direction tickets get deterministic, unique precedence;
- recovery and followers cannot age while custody can;
- intent changes and respawns clear ticket debt;
- repeated pair reversal hard-blocks tactical, safety, and clearance moves;
- hard state is exact-life;
- make-way creates a soft lease while allowing a first clearance reversal;
  and
- safety creates a soft lease while allowing a first safety reversal.

The existing engine and replay suites continue to cover formation reflow,
cooldown drift, hidden-lock release, waypoint rejoin, stuck detection,
delivery routing, follower filtering, deterministic pathing, and WASM host
parity.

## Exhibit read

The standard cells are fixed in the repository-prescribed order:
`w-9001 w-9002 e-9005 e-9009 w-9012 e-9004`. Hunter is the sheet under
review and Warren-native Wellwright is the actual opponent. Each outcome is
visible on its card; none is interpreted as a balance measurement.

### Ordinary stepping

Public, unauthenticated gallery:
<https://factor-bidding-foods-thumb.trycloudflare.com>

| Side | Moves | Blockades | Chokes | Flaps | Reverse | Long A-B runs |
|---|---:|---:|---:|---:|---:|---:|
| Hunter side totals | 9,049 | 0.84% | 2 | 3.12% | 5.28% | 0 |
| Wellwright side totals | 9,004 | 1.06% | 2 | 3.42% | 5.58% | 0 |

In the original catch cell `w-9001`, the Hunter side is 2.99% flaps, 4.84%
reverse movement, and zero long runs; the Wellwright side also has zero.

### Coordinated stepping

Public, unauthenticated gallery:
<https://submitting-college-federation-calm.trycloudflare.com>

| Side | Moves | Blockades | Chokes | Flaps | Reverse | Long A-B runs |
|---|---:|---:|---:|---:|---:|---:|
| Hunter side totals | 9,100 | 1.15% | 3 | 3.81% | 6.99% | 0 |
| Wellwright side totals | 9,223 | 1.18% | 3 | 3.95% | 7.17% | 0 |

In coordinated `w-9001`, the Hunter side is 2.87% flaps, 4.29% reverse
movement, and zero long runs; the Wellwright side also has zero. The
coordinated path planner remains a selectable pathing implementation, not the
source of the cross-tick guarantee.

Both URLs were loaded after their final card swap in Chromium at
390×844 with touch/mobile capabilities. Each had six outcome-visible cards,
gzip-encoded index and replay payloads, a ready WebGL arena, the score bug,
advancing playback, and zero page, console, or request errors. The servers and
tunnels run in four uniquely named detached `screen` sessions on ports 8993
and 8994.

## Determinism and hosted identity

The final `w-9001` in-process and sandboxed-WASM runs have the same
gameplay-state digest:

```text
d4062e2bdf99a3ef56eae45bd95e10a7c45e659060c31c30eacbd2865f7e0794
```

The controlled macOS Docker build produced:

| Identity | SHA-256 / value |
|---|---|
| Build input | `21247aa1e49b5f46c11c8b93da02245f161d6fb8d3ad4234081a915fe053a9ff` |
| WASM artifact | `50e932fe5a7e79791bfdc9959c868cb58f06925a73c170d4ce105c636dd0a060` |
| WASM bytes | 8,557,005 |
| Pipeline | 5 |
| SDK / guest | 0.10.13 / 0.10.13 |

Current tactical `bot.wasm` and frozen
`ambush-warren-11-stock-v5/bot.wasm` are byte-identical. Hosted entrant
playlist v11 pins that immutable stock. Playlist v10 and frozen stock v4 stay
unchanged at artifact hash
`c2b2f0a92f34d2d006bd4e7ecf8d68d0750df89300db7e23f33ef111d793a9a0`.

No game rule, map, sheet, sheet grammar, endpoint contract, or frozen
historical artifact changed. Canonical replay content intentionally changes
because executor decisions change; rules/map contract fingerprints do not.
The open Season 1 rating book continues from the most recent registered
v8-v10 ladder and refuses to skip any registered intermediate version.

The compatibility bump is CLI 0.9.40. No release or deploy workflow ran;
`publish-cli` owes tag `cli-v0.9.40` before any deployment.

## Validation

| Check | Result |
|---|---|
| `dotnet build BotArena.sln --no-restore` | pass, 0 warnings / 0 errors |
| CLI tests | 270 / 270 pass |
| Engine tests | 1,436 / 1,436 pass |
| Runtime.Wasm tests | 68 / 68 pass |
| Determinism tests | 17 / 17 pass |
| App tests without PostgreSQL opt-in | 202 pass, 85 intentional skips |
| App tests with disposable PostgreSQL | 284 pass, 3 non-PostgreSQL integration skips |
| Generated API clients | regenerated, zero diff |
| Web tests | 437 / 437 pass |
| Web production build | pass |
| Traffic diagnostic Python compile | pass |
| `git diff --check` | pass |
| Phone browser smoke, both public galleries | pass |

## Limits that remain

- This is bounded local arbitration, not globally optimal multi-agent path
  finding. A team can still experience a short hold or reroute under load.
- The final exhibits retain two ordinary and three coordinated blocked-move
  chokes per side total. The invariant removes sustained successful-motion
  circuits; it does not claim that every contested tile clears instantly.
- A ticket deliberately requires a stable semantic intent and goal. A moving
  target that legitimately changes the goal does not carry old seniority into
  the new pursuit.
- Three-tick leases can cost a locally attractive position. That is the
  bounded price of proving the executor will not immediately undo its own
  safety or clearance move.
- Six cells show the policy and its failure bounds. They do not establish
  class dominance, sheet strength, or player-facing balance.
