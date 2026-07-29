# Striker Meshy 6 multi-view proof

These four images are lossless crops of `../striker-model-sheet-v1.png`.
They are an upload pack, not new concept art:

1. `01-top.png`
2. `02-side.png`
3. `03-front.png`
4. `04-three-quarter.png`

Upload each image separately to **Image to 3D → Meshy 6 → Multi-view**.
Do not upload the four-view sheet as one image.

## First proof settings

- Output quality: **Standard / maximum detail**
- Image enhancement: **Off** (the inputs are already high-quality and approved)
- Textures: **On**
- PBR maps: **On**
- Texture resolution: **4K** (keeps the Meshy 6 emission map; 8K does not)
- Remove lighting: **On** (Nilbots supplies runtime lighting)
- Remesh / Smart Topology / Auto Split: **Off** for the first proof
- Pose: **None**
- Export: **GLB**

If the UI offers a texture prompt, use:

> Preserve the exact graphite-black and weathered-bronze hard-surface vehicle
> design in all references. Preserve fine panel seams, recessed vents, the
> cyan illuminated accent inlays, and warm amber engine/core emission. No
> added weapons, landing gear, text, logos, supports, background, or new
> ornamentation.

## Authority and acceptance

- `../references/trident-wasp-2d.png` remains the exact top-planform authority.
  The generated top view is a textured modeling reference, not permission to
  change that silhouette.
- The generated model is only a base mesh. It must not become a runtime asset
  until Blender cleanup establishes Nilbots scale/orientation, corrects the
  top silhouette, separates the team-color material, repairs UV/PBR maps,
  removes unseen waste, and validates the result in the real web renderer.
- Reject the proof if its oblique render does not read as the approved Striker
  at gameplay distance, even if its close-up texture detail looks impressive.

## Source crop record

All crops use `sips --cropOffset <y> <x>` against the 1536×1024 approved sheet:

| File | Crop (height × width) | Offset (y, x) |
| --- | ---: | ---: |
| `01-top.png` | 480 × 900 | 0, 70 |
| `02-side.png` | 220 × 950 | 475, 30 |
| `03-front.png` | 315 × 650 | 680, 45 |
| `04-three-quarter.png` | 330 × 820 | 665, 690 |
