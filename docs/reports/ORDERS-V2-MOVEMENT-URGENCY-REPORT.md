# Orders v2 movement urgency — design-review report

Date: 2026-08-12  
Branch: `codex/order-urgency`  
Base: `main` at `4aaf80cc`  
Implementation checkpoint: `9b43fe6c`  
Final report commit: `FINAL_SHA`  
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

The candidate Hunter marks `shadow-hunt` routine and the shared Core-return and
recovery branches urgent. All omitted orders—including the unchanged
Wellwright—remain normal. The editor presents four choices: inherit, routine,
normal, urgent, with the movement-only boundary stated next to the control.

## Executor policy

The old hidden carrier/fighter/free-traffic tiers are gone. Every selected
movement plan—including an advancing fight—publishes its intended next tile on
one allied movement plane. Pending strikes are rooted before those requests are
published. A stronger mover may cascade a clearance request through a chain of
weaker allies to the nearest real vacancy; a closed component with no vacancy
still refuses to invent a swap.

Starvation prevention is executor-owned:

- one visible ally on the intended next step adds one block-debt point;
- every 12 points raises effective urgency by one tier;
- strict progress pays four points down;
- at equal effective urgency, greater debt, first blocked tick, activation
  tick, then stable unit id decide the deterministic order;
- a changed order, goal, authored urgency, activation, or life resets the debt.

A routine mover therefore competes as urgent after 24 continuously denied
ticks. It receives an opening without acquiring a permanent tactical
promotion. Route direction hysteresis, bounded detour/rejoin, oscillation
protection, terrain legality, and no-vacancy refusal remain separate policies.

Reasons retain the established grammar and expose both the movement lens and
the decision evidence, for example:

```text
orders:hunter > shadow-hunt:march@23,11:
  urgency-routine+b13=>normal:route-held-next-22-10 via NorthWest
```

The canonical replay parser, broadcast projector, viewer, and route diagnostic
all agree on that shape: `march` remains the action, `(23,11)` is drawn on the
map, and urgency/debt remain readable in the trace.

## Fast deterministic lab

Focused command:

```text
dotnet test tests/BotArena.Cli.Tests/BotArena.Cli.Tests.csproj --no-restore
  --filter FullyQualifiedName~OrdersMovementSceneTests|
           FullyQualifiedName~TacticalMovementGovernorTests|
           FullyQualifiedName~ArcRelayTacticalPlaybookCompilerTests
```

Result: **43/43 passed in about three seconds**.

The pure scenes pin:

- route commitment, impossible-step reset, detour and course rejoin;
- arrival-ring clamping, unreachable waypoints, and correct unavoidable waits;
- front/rear/line posture and 180-degree team rotation;
- vacancy-chain resolution and closed-cycle refusal;
- routine/normal/urgent precedence, block debt, progress repayment, bounded
  starvation, activation/unit-id ties, input-order symmetry, and life/intent
  reset;
- oscillation refusal and bounded clearance/safety loans.

The real scene runs Tapline seed 9001 through the actual compiler, in-process
mind, engine, and greedy stepper. It requires all 16 initial bodies to leave
their deployment tile, both teams to bank, Core return/recovery/combat/make-way
to occur, every command reason to be non-empty, and no single-tile run above
100 ticks. It completes in about two seconds.

Focused web result: **19/19 passed** across the Orders editor and canonical/
broadcast reason parser. `npm run build` also passed. A previous broad web run
had 462/463 passing with one unrelated missing Meshy provenance source file;
the focused relevant tests and production build are green.

## Six-cell exhibit

The standard fixed cells ran on `tapline-1`, candidate Hunter versus unchanged
Wellwright:

| Cell | Observed result |
| --- | --- |
| w-9001 | Hunter win, reactor destroyed at t442 |
| w-9002 | Hunter win, reactor destroyed at t422 |
| e-9005 | Hunter win, reactor destroyed at t423 |
| e-9009 | Wellwright win, reactor destroyed at t476 |
| w-9012 | Wellwright win, reactor destroyed at t401 |
| e-9004 | Hunter win, reactor destroyed at t379 |

That is a **4–2 exhibition record**, not a balance claim.

Across the twelve side-reads:

- every side reached its nearest own Well (`closest = 0`);
- every cell banked 12–17 Cores in total;
- all 16 initial bodies moved by t3 in every cell;
- no delivery-timeout tick occurred;
- blocked traffic was 0.1–7.7% of stationary body-ticks;
- movement-channel visibility was restored at 36.5–74.8%;
- reasons contained routine, normal, urgent, make-way, and cascading clearance;
- all commands had non-empty reasons;
- debt promotion occurred in three cells, reaching a maximum observed debt of
  39 (`routine` therefore reached effective `urgent` in real play).

Ten of twelve side-reads had a longest single-tile run of 62 ticks or less.
The two apparent exceptions in w-9002 were 195 and 204 ticks, but tracing them
showed explicit successful `at-target` work: Hunter's builder held centre and
Wellwright's home hauler held its assigned Well. Their blocked/stationary shares
were 7.7% and 0.1%, respectively. They are intentional sheet positions, not
traffic deadlocks. The route reader should eventually report a separate
"longest blocked stall" so a legitimate hold cannot look like a jam.

The trace-only follow-up from v1 to v2 changed replay bytes but not gameplay:
the repeated w-9001 gameplay-state digest is byte-identical at
`96f84506de0cf6171976f7dde5358bc0f1569c611ccad6d6ea8d76d9e37767fe`.

## Phone delivery

The gallery index exposes six cards with the actual opposing sheet, result,
trigger, tactic, counterplay, fallback, and a concrete watch cue. A real
390×844 Chromium smoke loaded the public index, opened sample 1, mounted the
real replay canvas, began loading all 21 arena assets, and reported no console
or page errors. Evidence screenshot: `/tmp/orders-urgency-phone.png`.

The detached review sessions are:

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
