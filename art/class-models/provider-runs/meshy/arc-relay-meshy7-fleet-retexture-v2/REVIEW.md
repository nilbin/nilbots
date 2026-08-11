# Meshy 7 restrained fleet retexture V2

Status: owner review only. No game/runtime model was promoted.

Kestrel retains its previously reviewed successful texture byte-for-byte. The
other fifteen classes use Meshy 7 2K PBR retextures with `enable_original_uv`
enabled. The review derivatives transplant those maps back onto the pinned
original meshes, avoiding Meshy's uniform rescale and vertex/index rewrite.
Their optimized geometry fingerprints therefore remain identical to the V1
review tier.

## Initial visual read

The pass reduced the glossy/plastic response, but the prompt's “fine dry
surface grain” and “restrained wear” language was over-interpreted as mottled,
camouflage-like surface noise. Several palettes also converge toward khaki or
gray, while Longshot and Repulsor remain too dark for the arena. Keep the run as
evidence, but do not promote it as a fleet texture direction without owner
approval.

The next free retry should preserve the named palettes while asking for clean,
large, low-frequency color fields and explicitly excluding camouflage,
mottling, stone, marble, grime, chips, scratches, and randomized patchwork.

Review sheet:
`art/reviews/arc-relay-meshy7-fleet-retexture-v2/fixed-orientation-sheet.png`
