# Meshy Bulwark Smart T2 moderate-oblique gate

- Provider: Meshy
- Task: `019fb432-7207-7121-b901-0f5bf705fd34`
- Status: succeeded
- Credits: 5 (`690` before, `685` after)
- Input: moderate-detail simplified three-quarter concept

## Verdict

Rejected for texturing, rigging, part donation, and runtime.

Restoring modest surface detail caused severe Smart fragmentation: 13,072
triangles are split across 345 welded components, with only 11 components
carrying at least one percent of the triangles. Best top IoU is `81.38%`.
This is worse structurally than both the strict-top leader and the more
aggressively simplified oblique.

The controlled comparison confirms that Meshy input detail must be reserved
for texturing after geometry, not placed into the geometry-generation image.

The ignored raw master is 246,468 bytes with SHA-256
`21c44de047a206a60823bbadc4cbc2f5faf60f7f68bb2ce878ecc87af5b30b22`.
