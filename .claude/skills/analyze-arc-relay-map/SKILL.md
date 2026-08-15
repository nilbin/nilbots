---
name: analyze-arc-relay-map
description: Measure and visualize Arc Relay map geometry with corner-strict pathfinding, explicit route families, landmark-complete 2D atlases, and symmetry, connectivity, and topology checks. Use for Nilbots Arc Relay map candidates, route-distance questions, shortest or committed-path comparisons, wall-layout changes, landmark audits, and owner-facing 2D map reviews. Do not treat its terrain-only measurements as balance evidence.
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
3. State the team perspective so `own` and `enemy` are unambiguous. Use exact
   landmark requirements and required IDs so omissions fail validation.
4. Read [references/spec.md](references/spec.md), then create or adapt a JSON
   specification in `sandbox/` or a temporary directory. Start from
   [references/tapline-equal-return-candidate.json](references/tapline-equal-return-candidate.json)
   when investigating the current equal-return candidate.
5. Run the analyzer from the repository root:

   ```bash
   python3 .claude/skills/analyze-arc-relay-map/scripts/map_analysis.py \
     <spec.json> --json-out <analysis.json> --html-out <atlas.html>
   ```

6. Treat every validation error as a blocker. Pin accepted distances with
   `expectedSteps` so later geometry drift fails loudly.
7. Open the HTML and inspect every route group at desktop and narrow-phone
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
- Report route steps as a one-tile-per-tick lower bound before bodies, traffic,
  combat, sight, deployments, or tactical hesitation.

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
