---
name: analyze-arc-relay-map
description: Measure and visualize Arc Relay map geometry with pinned route distances, landmark-complete 2D atlases, draggable and rotatable vision/gun-range references, and topology checks. Use for Nilbots Arc Relay map candidates, route-distance or sightline questions, wall-layout changes, landmark audits, and owner-facing 2D map reviews. Do not treat its terrain-only measurements or range envelopes as balance evidence.
---

# Analyze Arc Relay Map

Use the deterministic analyzer for route mathematics and the self-contained
HTML atlas for review. Keep the result tied to the actual map source and exact
scoring landmarks.

## Workflow

1. Inspect the active map builder/profile and the runtime behavior relevant to
   the question. Confirm the exact scoring tile; in Tapline, delivery occurs at
   the reactor rather than a stale scoring-shoulder annotation.
2. Inventory every gameplay landmark from source. For a three-Well map, include
   both reactors, own-side Well, centre Well, enemy-side Well, and both healing
   tiles. Include spawn or delivery regions when they affect the question.
3. State the team perspective so `own` and `enemy` are unambiguous. Inventory
   every requested route family from every relevant origin before measuring.
   Use exact landmark requirements, `requiredLandmarkIds`, and
   `requiredRouteIds` so landmark or path omissions fail validation.
4. Read the active rules for the base, unupgraded vision profile and the three
   ordinary gun profiles. Add one `combatReference` origin and facing that make
   the important lanes easy to compare. Keep signature ranges separate.
5. Read [references/spec.md](references/spec.md), then create or adapt a JSON
   specification in `sandbox/` or a temporary directory. Start from
   [references/tapline-equal-return-candidate.json](references/tapline-equal-return-candidate.json)
   when investigating the current equal-return candidate.
   Owner-facing atlases must set both `atlasRequirements.distances` and
   `atlasRequirements.combatRanges` to `true`.
6. Run the analyzer from the repository root:

   ```bash
   python3 .claude/skills/analyze-arc-relay-map/scripts/map_analysis.py \
     <spec.json> --json-out <analysis.json> --html-out <atlas.html>
   ```

7. Treat every validation error as a blocker. Pin accepted distances with
   `expectedSteps` so later geometry drift fails loudly.
8. Publish the analyzer-generated HTML as the authoritative atlas; do not keep
   a second hand-authored viewer whose route inventory can drift. Open the HTML
   and inspect every route group at desktop and narrow-phone
   widths in light and dark themes. Check that walls, both reactors, all three
   Wells, both healing tiles, routes, badges, table values, and labels remain
   legible. Publish through the repository's normal owner-review path only when
   the user asks for a shareable link.

## Route semantics

- Use `shortest` for exact corner-strict terrain-only breadth-first search.
- Use `via` for a route committed through named gates, corridors, or other
  ordered checkpoints.
- Use `authored` when the exact tile sequence is itself the design contract.
- Never report a cyclic map's unconstrained "longest" route: a unit can loop
  forever. Define the purposeful commitment that makes the route finite.
- Report route distance in terrain steps, not as elapsed ticks. Movement rules
  can throttle relocation—for example, current Core carriers relocate only
  once every two ticks—and traffic, combat, signatures, or hesitation add more.

## Combat reference semantics

- Outline the full base, unupgraded facing cone and separately shade the tiles
  that remain visible after actual corner-strict wall occlusion. Keep both on
  the same reference origin so geometric range and practical sight are legible.
- Show all three ordinary gun profiles and their class memberships. Current
  Tapline values are short-fast 3, medium-steady 4, long-slow 6, with base
  vision 5; verify these in code rather than copying the numbers blindly.
- The three attack contours are Chebyshev distance rulers. They answer “how far
  is this?” but are not firing solutions: facing, shot headings, walls, windup,
  bodies, and signatures still determine whether a shot is legal or useful.
  Render them as squares: a square is the exact radius contour in the engine's
  Chebyshev metric. Do not substitute Euclidean circles unless the game rules
  change to Euclidean distance.
- Place the reference origin deliberately for the question. It is a measuring
  aid, not an extra landmark or gameplay object.
- Generated atlases let the reviewer drag the shared vision/range reference to
  any open tile, rotate it through the four cardinal facings, and reset it.
  Recompute wall-occluded vision after every move. These viewer interactions
  are temporary and must never mutate the source specification or analysis.

## Boundaries

- Analyze only the requested baseline or candidate; do not silently mix both.
- Preserve team rotation and 180-degree symmetry unless asymmetry is explicit.
- Keep analysis read-only unless the user separately asks to implement a map.
- Distinguish topology validation from play balance. Geometry can prove route
  length and connectivity, not whether a route is fun, fair, or tactically safe.
- Base labels and coordinates on code, not on earlier prose or screenshots.

## Verification

After changing the analyzer or its fixture, run:

```bash
python3 .claude/skills/analyze-arc-relay-map/scripts/test_map_analysis.py
```
