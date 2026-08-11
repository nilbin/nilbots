# Arc Relay tactical playbook v1

Status: **hosted source and direct runtime format**. Immutable entrant playlist
v19 admits `arc-relay-tactical-playbook-v1` on the native Tapline map. The exact
authoring and validation contract is [`SHEET-AUTHORING.md`](SHEET-AUTHORING.md);
this page explains the implementation boundary.

## One authored language

A sheet has two exact-bound JSON documents:

```text
ten-key playbook JSON ─┐
                      ├─ strict shared compiler ─ ATL1 direct ladder package
native layout JSON ───┘
```

The playbook contains composition, roles, custody policies, role doctrines,
conditions, and four memory windows. The layout contains Tapline-native zones,
routes, anchors, and side bindings. The playbook pins the layout's exact UTF-8
bytes by SHA-256; geometry is never projected between maps.

Every role owns one ordered ladder. Position is priority, so reading one role
predicts what a body assigned to it will attempt. The six verbs are `patrol`,
`intercept`, `assault`, `recover`, `fetch`, and follower-owned `follow`.
Conditions state when a rung opens and closes. Combat preferences and custody
remain data rather than mind-specific policy.

Coordination has only two narrow shared primitives:

- a **cohort** shares one participation count among multiple compatible rungs;
- a **focus** shares one maximum-attacker cap among multiple rungs.

Neither owns roles, orders, transitions, target preferences, lifecycle, or
formation state. Membership is derived from the authored rung references. A
single-consumer count or cap is inlined instead of named.

There are no groups, formations, engagements, operations, staff rows, escort
calls, muster calls, tempo machines, support-policy plane, authoring wrapper,
or expanded orders in the current language. `home-siege-v2.json` is retained
only as byte-frozen historical evidence and is intentionally not valid current
input.

## Compilation and identity

`nilbots experiment arc-relay-playbook --playbook <path> --out <directory>`
uses the same `BotArena.TacticalPlaybooks` compiler as hosted sheet saves. It
emits:

- `normalized-playbook.json`, the property-canonical authored playbook;
- `normalized-layout.json`, the separately canonicalized layout;
- `playbook.atp`, whose compatibility filename contains an `ATL1` package; and
- `explain.json`, whose entries identify authored `role[position]/verb` rungs.

Arrays retain authored order. Unknown or missing fields, invalid ranges,
unknown references, unconsumed shared names, stale layout hashes, unsupported
maps, and linked payloads above 65,536 bytes are hard errors. The compiler does
not desugar source into the retired exhaustive IR and does not use that IR as an
off-path validation oracle.

The ATL1 package includes the normalized playbook and layout plus their hashes.
The participant-scoped executor reads those role ladders directly. Runtime
trace reasons name the authored rung, not an invented order id. This preserves
the core identity claim: exact sheet bytes, exact mind artifact, exact match
contract, and exact seed select one deterministic action stream.

## Runtime ownership

The sheet decides:

- which classes may fill each role and in what staffing range;
- each role's ordered intents and conditions;
- custody eligibility and delivery/recovery policy;
- combat entry, target preference, chase, break-off, and defense policy;
- route, zone, anchor, posture, cohort, and focus references.

The executor decides only deterministic mechanics needed to carry out those
words: stable role allocation, path steps, target resolution, cohort ranking,
focus admission, and the nearest-live-body resolution of `follow`. Every
executor action carries a reason identifying its selected rung or mechanical
fallback.

The engine remains authoritative for observations, movement legality, combat,
Core custody, Wells, reactor state, respawns, vision, and replay events. The
executor never manufactures hidden facts or game state.

## Hosted use

The App embeds a deliberately small Tapline starter and the frozen Wellwright
opponent as separate native source pairs. The starter has three assignments,
one route, one zone and one anchor; Hunter remains a tracked reference sheet but
is not the player template. The App validates saved sheets through the shared
compiler, snapshots the linked package into admitted matches, and runs the
registered first-party direct-ladder mind. Playlist v19 pins the current
Tapline stock identity, exact frozen mind artifact and Wellwright source pair.
Historical playlist definitions and artifacts remain immutable.

The supported authoring loop is edit → practice → watch → edit. The map editor
draws the engine-exported Tapline map; practice uses the same compiler and runtime
path as hosted admission. Raw JSON and hashes remain portable operator tools,
not the primary player interface.

## Follow-up boundary

The old desugarer, generated ATP1 path, and expanded-source validation oracle
are gone. Some legacy in-memory record types still serve as an adapter between
the direct executor and generic action plumbing. Removing or renaming those
records is a separate cleanup: it must not be confused with changing the
authored DSL or match behavior.
