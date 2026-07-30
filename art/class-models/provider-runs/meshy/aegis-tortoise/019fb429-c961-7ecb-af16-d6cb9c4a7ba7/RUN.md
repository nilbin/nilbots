# Meshy Bulwark Smart T2 15k oblique comparison

- Provider: Meshy
- Task: `019fb429-c961-7ecb-af16-d6cb9c4a7ba7`
- Status: succeeded
- Credits: 5 (`705` before, `700` after)
- Input: approved elevated front-three-quarter concept
- Purpose: isolate target-polycount from viewpoint

## Verdict

Rejected for texturing, rigging, and runtime.

Raising the same oblique request from 4,000 to 15,000 target triangles improves
local surfaces but does not restore the Bulwark planform or mechanics. The
result contains 14,283 triangles and 141 welded connected components. Its best
unselected quarter-turn top silhouette is `79.49%` IoU, below even the failed
4k probe. Fixed views show incorrect shield/pod layout and an unreliable
underside.

This result proves that the primary failure is viewpoint inference, not merely
triangle budget. Do not retexture or use it as a part donor.

The ignored raw master is 272,988 bytes with SHA-256
`7de16b95389fe963f85f2a5cac14bf9484d45be55292edefd93d24fad9acc8c5`.
The `audit/` directory preserves the decisive fixed views and silhouette
overlay.
