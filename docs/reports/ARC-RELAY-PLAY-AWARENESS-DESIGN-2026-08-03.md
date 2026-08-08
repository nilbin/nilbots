# Arc Relay play-awareness design

Status: **proposal for owner review; no viewer implementation is authorized by
this document.**

## Decision

Arc Relay needs three levels of tactical readability rather than more always-on
labels:

1. **Glance** — the ordinary broadcast makes possession, a coordinated play,
   its participants, contact, and resolution readable in the world.
2. **Read** — an optional compact tactics lens names the active play and phase
   and adds causal timeline bookmarks.
3. **Learn** — pausing or selecting a play explains its observed trigger,
   branch, participant tasks, counter, resolution, and release to baseline.

This preserves the field as the primary storyteller while admitting a basic
product truth: an arbitrary WASM mind's reason cannot be recovered reliably
from movement alone. Rich explanation must come from bounded deterministic
entrant telemetry and must always be distinguished from authoritative physical
facts.

The existing three-match Meshy gallery remains useful for asset and renderer
review. It is not an acceptable tactics gallery. Future gameplay galleries
should use the retained ten-operation matches, or equally strong replacements,
and bookmark the preparation, commitment, first hostile contact, resolution,
and baseline-release beats.

## The five-second contract

Without pausing, a new viewer should be able to answer:

- Who has each Core, or is it loose?
- What is each team trying to do **right now**?
- Which bodies are involved in that play?
- Where is the opposing response or likely collision?
- Did the play succeed, get countered, abort, or return to ordinary behavior?

After pausing and opening the tactics lens, the viewer should additionally be
able to answer:

- Which observed fact armed the play?
- Why did this branch win over its alternative?
- What task did each claimed body receive?
- What broke the play, if anything?
- When and how did every survivor return to baseline?

The viewer must not claim to know a reason that the entrant did not declare.
"Moved north" is authoritative. "Moved north to bait the screen" is an
entrant-declared explanation unless it came from the frozen sheet interpreter.

## One information model, three truth levels

Every presented fact carries a provenance internally. The UI treatment may be
quiet, but the distinction may never disappear.

| Level | Source | Examples | Treatment |
| --- | --- | --- | --- |
| authoritative | canonical replay or causal broadcast prefix | Core state, position, damage, signature, issued action, death, bank, Pulse | stated as fact |
| interpreted | frozen stock interpreter plus exact sheet revision | operation phase, claimed participants, chosen branch, baseline release | stated as the sheet's actual execution |
| declared | bounded custom-mind presentation telemetry | play name, task labels, claimed intent, declared abort reason | labelled as entrant-declared; never used to adjudicate a match |

The renderer must not infer named tactics from clustering, path shape, or camera
interest. Such inference would confidently mislabel feints, blocked paths, and
coincidental formations. It may derive neutral physical descriptions such as
"three bodies converging" but should not call that a pincer.

### Information release

- A participant may see its own interpreted or declared intent causally during
  a match.
- An opponent-facing or public live prefix exposes only authoritative facts
  already visible under the applicable vision policy. It does not reveal a
  private trigger, unused branch, future waypoint, or unobserved participant.
- A completed replay may expose both sides' **executed** play trace: triggered
  evidence, chosen branch, tasks actually claimed, and resolution. It does not
  publish either complete sheet, untriggered gambits, or custom source.
- Owner/evaluation galleries may opt into the complete evaluation trace and say
  so explicitly.

This answers the sheet-secrecy problem: explaining the play that happened after
the match is not the same as publishing the opponent's entire sheet before it.

## Glance: default broadcast language

The default remains restrained and mostly diegetic. It adds no prose banner.

### Core grammar

- Loose, dropped, and in-flight Cores are neutral energy.
- A carried Core takes the carrier team's accent and glow.
- Birth uses an expanding neutral Well ring and its existing sound.
- Pickup pulls the ring into the carrier and sounds one short rising cue.
- Drop breaks the team tint into a neutral radial shock and sounds a downward
  cue.
- Steal is a two-beat color transfer, not a separate floating label.
- Bank sends the Core's energy into the owning reactor; Pulse adds the existing
  field-wide beat.

The first two rules are now implemented; the remaining event grammar belongs
to the future awareness pass.

### Coordinated-play grammar

Only an active coordinated play receives this treatment. Baseline bodies keep
their ordinary presentation.

| Phase | Field treatment | Meaning |
| --- | --- | --- |
| preparing | faint broken team-color brackets beneath claimed bodies; one shared play sigil | assembling; still preemptible |
| committed | brackets become solid for one beat; the shared sigil locks; current one-tick action vectors briefly agree in style | the branch is fixed |
| hostile contact | opposing damage, displacement, reveal, or Core contest produces a neutral clash ring between the actual participants | this is where the response meets the play |
| success | sigil closes toward the achieved physical fact, then fades into recovery | mission condition occurred |
| counter/abort | sigil fractures at the causal contact or failed anchor, never at an unrelated death elsewhere | play ended unsuccessfully |
| recovery | thin outward release ticks replace the shared bracket and fade as each survivor resumes baseline | bodies are no longer held by the play |

The field does **not** draw a complete future route. At most it draws the
already-issued one-tick action vector. This avoids promising a position the
authoritative replay never reaches and avoids revealing private future intent.

When disjoint operations coexist, the default shows the highest-salience play
per team. Bodies in other plays retain a small shared sigil; the tactics lens
can expand all of them. Salience is causal and deterministic: hostile contact,
Core interaction, committed operation, preparation, then recovery.

### Selection

Selecting a body highlights its current playmates, not every ally. The unit
summary becomes:

`Class · current task · play phase`

Examples:

- `Towline · rear interceptor · preparing`
- `Lantern · route probe · committed`
- `Kestrel · baseline recovery`

No `Body X`, opaque role tag, raw internal operation code, or verbose per-tick
decision dump appears in the primary summary. Exact IDs and commands remain in
an expandable diagnostics section.

## Read: the tactics lens

The tactics lens is an explicit toggle, separate from Overview/Director and
available in both camera modes. It is the narrow exception to the earlier
diegetic-only rule that needs owner approval.

Its default form is two compact team rails. Each active play occupies one row:

`[sigil] Rear Hook · COMMIT · Towline ×2`

The row uses the team accent, class icons, and phase symbol. It never expands
the unit panel or obscures the arena. Selecting a row:

- highlights only its current participant lives;
- marks the physical anchor or last-seen target with provenance;
- shows the current branch and each participant task;
- offers a one-click return to the relevant team-vision view; and
- pins the director to the play until released.

The timeline receives small, filterable bookmarks for:

- Core birth, pickup, drop, steal, bank, and Pulse;
- play prepare, commit, success, counter/abort, and release; and
- participant destruction while claimed by a play.

Bookmarks show the event and tick on hover. They reveal outcomes because the
owner explicitly rejected outcome-blind review; they still obey the playhead,
so the standing viewer never displays a future state as current.

## Learn: causal play card

Opening a completed or currently active play produces one concise card, not a
turn-by-turn transcript:

```text
REAR HOOK — countered
Armed: enemy carrier seen on north return
Claimed: Towline 4 (north), Towline 5 (south)
Committed: carrier-strike at 0:26
Contact: Towline 5 destroyed by Longshot at 0:36
Ended: mission deadline at 1:14
Released: survivors baseline at 1:26
```

For a live/current prefix, unavailable future lines are absent rather than
greyed or predicted. A condition sourced from memory says `last seen 2 ticks
ago`; a partially unseen zone says `unknown`, never `clear`.

The card explains one activation. Repeated activations form a vertical list so
the viewer can distinguish "the same card tried again" from one indefinitely
stalled operation.

## Entrant telemetry without game authority

### Sheet entrants

The frozen stock interpreter already knows exact operation state and emits
bounded role tags used by the evaluation proof. The product pass should project
that execution into a small transition-only presentation trace:

- play identity and activation ordinal;
- prepare, commit, recover, and release ticks;
- exact participant life IDs and task IDs;
- public/observed evidence references;
- chosen branch;
- terminal category and causal event handle where one exists; and
- first non-operation command per released survivor.

The projection is derived from the exact sheet revision and interpreter
execution. It is presentation data, excluded from canonical replay hashing.

### Custom-mind entrants

An arbitrary WASM artifact is not introspectable. The practical v1 path is a
small presentation manifest submitted with the entrant revision, mapping its
bounded runtime role tags to safe play, phase, and task IDs. The existing role
tag is already deterministic and replayed; the mapping supplies human meaning
after the match without enabling a new gameplay channel.

Custom declarations are validated for shape, size, safe copy, known class
icons, and bounded churn. They remain self-description, not match truth. A mind
that supplies no valid mapping still gets the complete authoritative Core,
combat, score, action, and timeline language; the UI says `unlabelled
coordination` rather than inventing a tactic.

Do **not** activate the reserved allied-intent wire fields merely to ship this
viewer. That is a contract/SDK decision and would broaden the game channel. If
role-tag mapping proves too weak, a future versioned, output-only presentation
channel should be designed in the normal SDK-bump window and remain invisible
to opposing minds.

## Director relationship

The director consumes the same salience facts but does not define them.

Priority should be:

1. committed play making hostile contact around a Core or reactor;
2. active multi-body fight that can change possession or reactor integrity;
3. counter or preparation denial;
4. contested carrier approaching a bank;
5. imminent Well birth with bodies contesting it;
6. unthreatened carrier movement; and
7. passive preparation or recovery.

The shot contains both the play and its credible response, with enough margin
to see the route/cover relationship. An unthreatened carrier can influence the
frame but should not monopolize it. The tactics lens can pin one play; Overview
remains available at all times.

## Gallery policy

Review galleries get a declared purpose:

- **asset gallery:** fleet coverage, lighting, team colors, mobile/Canvas
  fallback; tactical quality is irrelevant;
- **presentation gallery:** Core events, play phases, camera, sound, and clutter
  under representative matches;
- **gameplay gallery:** successful plays, credible counters, aborts, casualty
  recovery, and baseline return against varied opponents.

The next presentation/gameplay gallery should start from the ten retained
operation-counterplay matches. Each card should name the operation side, teams,
result, and five seek points: prepare, commit, contact, resolution, release.
At least three cards should show overlapping operations or a failed assumption,
not ten clean isolated showcases. Mirror/preflight matches may remain in an
asset gallery but should not dominate a tactics review.

## Prototype order

No implementation begins until the owner approves the information model and
the tactics-lens exception.

1. Prototype Core pickup/drop/steal/bank event grammar and timeline markers
   using existing authoritative events.
2. Generate a noncanonical transition trace for Rear Hook and Lantern Sweep
   from retained evidence; do not change runtime or canonical replay.
3. Prototype the glance brackets/sigils plus the two-row tactics lens on those
   two matches.
4. Add the causal card and team-vision handoff.
5. Conduct a first-viewer comprehension test before scaling to all ten plays.
6. Only after that, define admission-time presentation manifests for custom
   minds and extend all ten evaluation operations.

## Acceptance test for the prototype

Use fresh viewers who have read only a one-screen Arc Relay rules primer. On a
six-match set containing success, committed counter, preparation denial,
casualty, false assumption, and overlapping plays:

- at least five of six viewers identify Core possession correctly at sampled
  beats;
- at least five of six identify the active play's participant group and target;
- at least four of six distinguish prepare, commit, and recovery without
  opening diagnostics;
- after opening the tactics lens, all can state the trigger, chosen branch,
  outcome, and baseline return for the selected activation;
- no viewer reports a future route as already authoritative;
- all timeline markers match exact existing event or presentation-trace ticks;
- team-vision mode exposes no opponent-only anchor or evidence;
- canonical replay hashes remain byte-identical; and
- the transition trace stays comfortably inside the existing 300 KiB
  per-match and 8 MiB per-gallery budgets, reported separately from canonical
  replay bytes.

If the glance layer fails but the tactics card succeeds, do not compensate by
opening more panels by default. Revise the in-world grouping and event grammar.
If custom-mind declarations prove misleading in practice, show only
authoritative facts for that entrant until a safer output-only telemetry
contract exists.

