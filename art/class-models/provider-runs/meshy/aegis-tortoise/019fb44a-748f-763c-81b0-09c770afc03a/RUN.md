# Meshy Bulwark first text-led matte retexture gate

- Provider: Meshy
- Task: `019fb44a-748f-763c-81b0-09c770afc03a`
- Input geometry: `019fb42a-e3ed-7f18-a54e-795edf685815`
- Status: succeeded
- Credits: 10 (`675` before, `665` after)
- Style: exact text prompt recorded in `request-record.json`
- Settings: Meshy 6, original UV, PBR, 2K, lighting removal, GLB

## Verdict

Retain as an alternate/store material source, not the leading default.

The controlled text prompt materially reduces the first pass's chrome finish
and proves that Meshy can supply a flatter game-oriented PBR source on the same
accepted geometry. It also washes too much of the hull into pale ash and loses
the concept's stronger dark structural hierarchy and useful fixed emission.
Those differences make it a plausible pale cosmetic direction but a weaker
default Bulwark read.

Retexture preserved the accepted planform and 122 welded connected components.
Blender imports 12,979 nondegenerate triangles, and the normalized top
silhouette remains `91.65%` IoU against the canonical concept. The output has
one fused material and no semantic team role.

The ignored raw GLB is 9,724,964 bytes with SHA-256
`b5ce7d5217aad14355a0e9804025d06503dead16ededf6065a56010830870bd2`.
Its expiring provider master and lossless maps are backed up in the local
artifact store. The committed `audit/` directory contains the corrected fixed
views, gameplay view, geometry report, and unchanged silhouette overlay.
