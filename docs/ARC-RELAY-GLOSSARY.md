# Arc Relay vocabulary

This is the player-facing vocabulary for the current Arc Relay product. Code and
historical documents may retain older names where compatibility requires them; new
navigation, help, errors, and authoring UI use the terms below.

- **Entrant** — the competitive identity that owns a name, crest, composition,
  revisions, match history, and rating. An entrant is either a sheet or a custom mind.
- **Sheet** — a saved tactical playbook plus its hash-pinned layout, executed by the
  frozen stock mind. Editing it creates a revision of the same entrant; saving a copy
  creates a new entrant.
- **Custom mind** — submitted C# source built through the controlled toolchain and run
  as sandboxed WASM. Its declared composition is part of every revision.
- **Body** — one unit fielded during a match. Bodies are instances, not entrants.
- **Class** — a body's capabilities, signature skill, movement profile, and look.
- **Composition** — the entrant's ordered eight class slots, with no more than two
  copies of one class.
- **Assignment** — a sheet-authored job filled by eligible classes up to its staffing
  target. The compiler stores assignments in its `roles` collection; `role` remains an
  internal schema term, not a second player concept.
- **Doctrine** — one assignment's ordered tactical behavior: custody, combat preferences,
  collection geography, and modes. Every declared assignment has exactly one.
- **Mode** — one conditional behavior inside a doctrine. Modes are evaluated in order;
  the first active mode wins and the last mode is the safe floor.
- **Verb** — the one action family a mode performs: `patrol`, `intercept`, `assault`,
  `recover`, `fetch`, or `follow`.
- **Condition** — a named comparison over an observed fact. Conditions combine with
  `and` and `or` to open or close modes; they do not reveal hidden information.
- **Fight block** — the doctrine or mode settings for target preference, engagement,
  chase, break-off, defense, healing, and loose-Core priority.
- **Follow mode** — follower-owned support intent. The follower names a role and
  predictably selects its nearest live body; the followed role makes no call.
- **Cohort** — one shared participation count for two or more compatible modes.
  Membership is derived from those modes; a cohort does not own roles or behavior.
- **Focus** — one shared attacker cap for two or more modes. It owns no target lock,
  preference, order, or lifecycle.
- **Custody policy** — the shared pickup, delivery, recovery, passing, accidental-pickup,
  and unreachable-route protocol. Each assignment volunteers through its own ordered
  `fetch` mode; the policy does not keep a second carrier roster.
- **Layout** — the map-native zones, routes, anchors, and side bindings used by a sheet.
- **Zone** — an inclusive map rectangle used by conditions, Core collection, and custody.
- **Route** — ordered map waypoints plus a corridor width.
- **Anchor** — one named map tile, commonly used as an interception origin.
- **Trial run** — an unranked run of the current draft against a chosen stock sheet and
  seed. It exists to edit, watch, compare, and edit again; it is not Season evidence.
- **Replay** — the authoritative match record viewed at a playhead. Actual tracks come
  from recorded body positions and never invent movement between ticks.
- **Season** — the public eight-week Arc Relay competition: standings, settled record,
  rating trajectory, and recent ranked matches. Season 1 starts every entrant at 1200.
- **Fielded entrant** — one of at most three entrants an account makes eligible for
  passive Season pairing. Withdrawing it stops new pairings without changing identity.
- **Ladder** — the internal isolated rating population beneath a Season. Product UI says
  Season and fielded entrant; compatibility code and historical records retain ladder.
- **Legacy Duel** — the retired earlier game. Its records remain readable in the archive;
  its bot vocabulary does not describe Arc Relay entrants or bodies.

The exact sheet schema, ranges, facts, and validation rules live in
[`SHEET-AUTHORING.md`](SHEET-AUTHORING.md). Product and compatibility rulings live in
[`DECISIONS.md`](DECISIONS.md), especially decisions 247–251.
