# Classes wave 1 (2026-07-29)

The first class-briefed population for the DECISIONS #153/#154 slate: two
independently briefed lineages per class, authored under
FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md with the
EXPERIMENTAL-FRONTLINE-CLASSES.md addendum. Role: verdict-doctrine,
target cumulative T4 (suite frontline-qualification-5) on the base
contract plus class-arm mirror play. Implementation budget: one authoring
pass; mechanical contract repairs permitted; one loss-forensics
improvement pass reserved for after the first cross-play screen.
Authors are isolated subagent sessions receiving only the permitted
player-facing material listed in the packet; they never see engine
internals, other entrants' source, or aggregate results.

| Entrant | Class | Lineage | Doctrine |
| --- | --- | --- | --- |
| vector-edge | striker | vector-edge-v1 | Pressure duelist: objective-first advance, bends only in open chambers, straight suppression in corridors |
| still-water | striker | still-water-v1 | Patient interceptor: holds even-range engagements, punishes movement with predicted bends, concedes ground for tempo |
| march-wall | bulwark | march-wall-v1 | Advancing wall: children anchor at won chokes, prime stays mobile, front creeps forward |
| iron-root | bulwark | iron-root-v1 | Fortress rotator: prime anchors forward behind cover through the long windup, mobilizes to rotate the front when flanked |
| spark-line | fabricator | spark-line-v1 | Tempo engine: queues on unlock, bodies forward immediately, wins the objective clock with presence |
| ledger-fly | fabricator | ledger-fly-v1 | Attrition banker: prime stays deep and safe, queues reactively to replace losses, plays the long clock |

## Known viewer/tooling issues (2026-07-29 blind review)

- `nilbots replay <replay.json> --out` cannot export replay-v3 viewers (v1
  deserializer only). Regenerate v3 viewers by injecting the replay at the
  `<!--BOTARENA_REPLAY-->` marker of a built viewer template
  (`ReplayOutput.WriteViewer` semantics); self-contained `web/dist-cli`
  excludes Three.js by design, so serve `web/dist` when review needs WebGL.
- The web validator rejected every automatic-activation replay until the
  availability-pending -> active transition was taught to it (fixed in this
  commit); no auto-companions replay had ever been opened in a browser.
- Mobilize (turret -> mobile) does not visually restore the mobile form in
  the viewer, class forms have no presentation metadata, and teams/classes
  are visually indistinguishable — class chassis looks and team tint are
  open work before any entertainment verdict leans on the viewer.
- Generic-actor movement does not rotate the body (contract behavior, not
  the removed legacy strafe actions) — reads as "strafing" in review.
