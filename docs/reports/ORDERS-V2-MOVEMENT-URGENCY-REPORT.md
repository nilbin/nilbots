# Orders v2 movement urgency — design-review report

Date: 2026-08-12
Branch: `codex/order-urgency`
Base: `main` at `4aaf80cc`
Candidate code checkpoint: `a3252048`
Review gallery: <https://scotland-legislature-define-except.trycloudflare.com>

This is a fast in-process design review, not a hosted promotion or balance
read. Tracked WASM, immutable stock, playlist v20, its rating book, and release
versions were not changed.

## Decision

Orders keep one source of tactical priority: their position in an ordered
tree. A branch or leaf may now state one movement-only weight:

```json
"urgency": "routine" | "normal" | "urgent"
```

The field is optional, inherits through nested orders, uses the nearest
explicit value, and defaults to `normal` at the root. It is consulted only
after an order has been selected and only when allies compete for movement
space. It never selects or reorders an order, changes a condition, target,
capacity, fight, enemy, collision rule, or safety rule.

The candidate Hunter marks shadow patrol and its passive centre hold routine;
shared Core return and recovery are urgent. Active Well work and all other
omitted orders remain normal. Wellwright is unchanged. The editor presents
four choices—inherit, routine, normal, urgent—with the movement-only boundary
stated beside the control.

## Executor policy

The old hidden carrier/fighter/free-traffic tiers are gone. Every selected
movement plan—including an advancing fight—publishes its intended next tile on
one allied movement plane. Pending strikes are rooted before those requests
are published. A stronger mover may cascade a clearance request through a
chain of weaker allies to the nearest real vacancy; a closed component with no
vacancy still refuses to invent a swap.

Starvation prevention is executor-owned:

- one visible ally on the intended next step adds one block-debt point;
- every 12 points raises effective urgency by one tier;
- strict progress pays four points down;
- at equal effective urgency, greater debt, first blocked tick, activation
  tick, then stable unit id decide the deterministic order;
- a changed order, goal, authored urgency, activation, or life resets debt.

A routine mover therefore competes as urgent after 24 continuously denied
ticks without acquiring a permanent tactical promotion. Route direction
hysteresis, bounded detour/rejoin, oscillation protection, terrain legality,
and no-vacancy refusal remain separate policies.

Reasons retain the established grammar and expose both the movement lens and
decision evidence, for example:

```text
orders:hunter > shadow-hunt:march@23,11:
  urgency-routine+b13=>normal:route-held-next-22-10 via NorthWest
```

The canonical replay parser, broadcast projector, viewer, and route diagnostic
all agree on that shape: `march` remains the action, `(23,11)` is drawn on the
map, and urgency/debt remain readable in the trace.

## Replay-found clearance defect

The first complete exhibit exposed a seam that the aggregate metrics hid. In
w-9002, Hunter's normal centre hauler waited 39 ticks behind an at-target
Mason. The traffic plane correctly requested clearance and promoted the
hauler's debt, but the greedy clearance chooser proposed a diagonal step. A
Mason is facing-locked and could perform only a cardinal move, so submission
failed every tick without trying a valid alternative.

The generic fix constrains every direct candidate to the body's actual route
headings before ranking it. The Mason now rotates for the clearance heading,
then takes the cardinal step. A focused real-executor regression pins both
commands and caps this scene's consecutive centre-Well blocking at six ticks;
the observed candidate run is four. No class, role, map, coordinate, or order
name appears in the runtime fix.

That repair revealed a separate authored conflict: an active hauler and the
passive centre holder were both normal and both targeted the exact Well tile,
so equal policy made them alternate. The candidate sheet now tells the truth:
passive hold is routine, active Well work remains normal. This is authored
intent, not another hidden executor precedence.

## Fast deterministic lab

Focused .NET command:

```text
dotnet test tests/BotArena.Cli.Tests/BotArena.Cli.Tests.csproj --no-restore
  --filter FullyQualifiedName~OrdersMovementSceneTests|
           FullyQualifiedName~TacticalMovementGovernorTests|
           FullyQualifiedName~ArcRelayTacticalPlaybookCompilerTests
```

Result: **44/44 passed in about five seconds**.

The pure scenes pin route commitment/detour/rejoin, arrival rings,
unreachable waypoints, unavoidable waits, posture and team rotation, vacancy
chains and closed cycles, urgency/debt/progress/ties/resets, and oscillation
loans. The real scenes run Tapline through the actual compiler, in-process
mind, engine, and greedy stepper. They cover deployment, banking, Core return,
recovery, fight approach, make-way, non-empty reasons, and the facing-locked
clearance regression.

Focused web result: **19/19 passed** for Orders authoring and canonical/
broadcast reason parsing. `npm run build` passed. The broad web run remains
**462/463**, with the one unrelated pre-existing failure being a missing Meshy
sentinel-seed provenance source file; this branch neither changes nor repairs
that art input.

## Final six-cell exhibit

The standard fixed cells ran on `tapline-1`, candidate Hunter versus unchanged
Wellwright:

| Cell | Observed result |
| --- | --- |
| w-9001 | Hunter win, reactor destroyed at t390 |
| w-9002 | Wellwright win, reactor destroyed at t480 |
| e-9005 | Hunter win, reactor destroyed at t283 |
| e-9009 | Hunter win, reactor destroyed at t366 |
| w-9012 | Hunter win, reactor destroyed at t396 |
| e-9004 | Hunter win, reactor destroyed at t281 |

That is a **5–1 exhibition record**, not a balance claim.

Across the twelve side-reads:

- all 16 initial bodies left deployment by t3 in every cell;
- both sides reached their nearest own Well (`closest = 0`);
- both teams banked in every cell;
- no delivery-timeout tick occurred;
- blocked traffic was 0.4–6.4% of stationary body-ticks;
- the worst continuous blocked/yield run was 23 ticks;
- all commands had non-empty reasons;
- all three authored urgency words, make-way, and cascading clearance occur;
- debt promotion occurred in four cells, reaching a maximum debt of 17.

The route reader's longest same-tile holds reached 91 ticks, but those are not
continuous traffic stalls; the diagnostic's worst actual blocked sequence is
23. The gallery cards expose actual outcomes and scores derived from their
replays, plus the intended trigger, tactic, counterplay, fallback, and watch
cue.

## Phone delivery

A real headless Chromium smoke at 390×844 loaded the public six-card index,
opened sample 2, mounted the real replay canvas, and reported no page, console,
or request errors. Screenshots are:

```text
/tmp/orders-urgency-v4-phone-index.png
/tmp/orders-urgency-v4-phone-sample.png
```

The detached review sessions remain:

```text
nilbots-orders-urgency-server-9103
nilbots-orders-urgency-tunnel-9103
```

## Promotion boundary and existing drift

No WASM or playlist work was appropriate for this design loop. If the owner
approves the behavior and asks to merge/promote it, the next gate must rebuild
and verify the mind through the controlled path, prove in-process/WASM gameplay
digest equality, mint a new immutable stock/playlist version, and decide rating
continuity from the stock-cell digest.

That gate must also resolve a **pre-existing source-pin drift** rather than hide
it: playlist v20 records Wellwright source SHA-256 `37a85465...`, while main's
current Wellwright source is `fe28c5e0...`. The change arrived in `8176bd92`
before this branch; this branch leaves both the Wellwright file and frozen pin
untouched. The review gallery truthfully uses current unchanged-main
Wellwright, not a claim that its bytes still match v20.

## Verdict

The three-word field earns its place. It replaces action-specific hidden
precedence with author-visible intent, stays below order selection, and is
small enough to explain in one sentence. The deterministic debt plane supplies
the necessary executor intelligence: authors say which movement matters now;
they do not script queues, vacancies, cycles, or starvation recovery.
