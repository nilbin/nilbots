# Arc Relay dynamic-strategy experiment

Status: evaluation-grade player contract. This is not the product sheet editor
schema and does not change Arc Relay rules, classes, map, or scoring.

## Question

Can the existing sheet plus ordered-gambit model express consequential,
opponent-dependent plans without collapsing into per-tick local optimization?

The experiment versions the frozen stock interpreter and its linked sheet data.
Historical stock mind v0, evaluation-sheet v0, match contracts, and canonical
replays remain untouched.

## Sheet model

A sheet remains the only authored strategy object. It contains:

- eight classes and their opening assignments;
- side-relative paths, rectangular regions, rally lines, and policies;
- a default spatial, engagement, and signature intent for each body; and
- an ordered list of named gambits.

Coordinates are authored from the west participant's perspective. The
interpreter mirrors X for the east participant. This makes `enemy-rear` and a
positive home-to-enemy offset mean the same thing for both assignments.

A position intent is one of:

- `base-assignment`: the existing carrier/screen/intercept/reserve behavior;
- `path`: follow a named drawn path, then hold, enter a named region, or resume
  the base assignment;
- `zone`: occupy the closest legal tile in any named authored region; or
- `anchor-offset`: hold a side-relative offset from a causal public anchor.

Public anchors include own/enemy reactor, a named Well, the next Well to birth,
the nearest visible loose Core, own/enemy carrier, visible enemy, partner, or
an ally carrying a named role. Missing anchors use an authored fallback region;
they never consult hidden enemy state.

Each intent can also carry explicit per-body formation offsets. Thus a pincer,
rear staging pair, screen, cutoff, or cross-map reserve is spatially authored
without pretending every position belongs to north/centre/south.

## Gambit execution

One gambit is active at a time. Ascending integer priority is authoritative.
Every gambit declares:

- one or more public-state entry clauses and `rising-edge` or `while-true`
  activation;
- explicit unit IDs and/or base roles in scope;
- minimum and maximum tenure;
- zero or more public-state exit clauses;
- a cooldown measured from exit;
- a coordinated overlay for role, position/formation, carrier/escort/
  interception policy, engagement intent, and signature-use intent.

The interpreter evaluates exit first. Before minimum tenure, nothing can end or
preempt the active gambit. Afterwards an exit clause, maximum tenure, or a
higher-priority eligible gambit may end it. At most one transition occurs per
tick; an exited plan cannot be replaced until the following tick. Entry order
breaks all simultaneous ties. Cooldown prevents immediate re-entry.

Supported clauses compare causal integer facts with `at-least`, `at-most`, or
`equals`: tick, visible own/enemy carried Cores, visible loose Cores, live own
bodies, visible enemies, bodies or carriers in a named region, ticks until the
next Well birth, own/enemy Pulses, and Pulse deficit. Own/enemy Pulse event
clauses are single-tick public edges.

`hold-fire`, `carrier-only`, and `normal` engagement intents separate staging
from contact. `conserve`, `normal`, `aggressive`, and `defensive` signature
intents let a plan reserve or spend its class hooks deliberately. They only
choose among actions already legal under the unchanged match contract.

## Required rear-line demonstration

The `rear-ambush` family assigns two bodies side-relative flank paths into an
`enemy-rear-staging` region outside the protected home pad. They hold fire and
conserve signatures while staged. When a visible enemy carrier enters the
registered return corridor, `rear-collapse` commits them to attack from the
enemy-homeward side of the carrier for a bounded window. It exits after the
Core drops/banks or maximum tenure expires.

The retained read must prove from broadcasts, not names, that at least one
ambusher:

1. reaches the registered rear region before activation;
2. waits there for at least six consecutive ticks without firing;
3. begins its first contact from the enemy-homeward side of the carrier;
4. moves or fights after activation; and
5. leaves the active plan on a declared exit or maximum-tenure boundary.

## Non-goals

- no live commander input or opponent-private sheet inspection;
- no economy, score-to-power, comeback, scoring, class, map, or combat change;
- no claim that the evaluation JSON is a friendly sheet editor;
- no fun claim from simulation metrics; and
- no replacement or mutation of the historical v0 interpreter.

