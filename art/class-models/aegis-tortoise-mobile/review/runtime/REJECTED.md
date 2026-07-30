# Rejected runtime-theme evidence

Every `v1` PNG and JSON file in this directory is rejected as visual
approval evidence.

The authoritative source replay used for these captures had
`header.presentation: null`. The viewer therefore rendered its implicit
Control Room fallback, not a Frontline V4 runtime theme. These files do not
prove or approve the Frontline map, ground, walls, spawn zones, capture zones,
or theme fit. Current Ember assets are not being claimed as the approved
Frontline V4 design.

The files remain only as historical diagnostics for Aegis GLB loading,
authored animation phases, deterministic seeking, and the forced SVG-derived
fallback. Do not reuse their map/theme labels or images in an approval packet.

Replacement evidence must wait for the V4 runtime-theme commit. If its
authoritative 23x15 replay still lacks presentation metadata, the review
harness requires an explicitly labelled presentation-only overlay and serves
it as a non-canonical document with `partial: true`, `replayHash: null`, and
`result: null`; the canonical source replay hash remains recorded separately.
