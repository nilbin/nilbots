# Arc Relay objective concepts v1

Status: **awaiting owner concept approval**. These images are not approved
Meshy inputs, runtime assets, provider-task approval, or spend approval.

## Physical / renderer split

The models are only the neutral physical housings. Replay and renderer state
remain authoritative for all live objective information.

### Core Well

- Physical model: low anchored octagonal housing, open Core bowl, three radial
  anchors, and an unobstructed outer annulus.
- Renderer: north/centre/south source glyph, exact scheduled-birth or rearm
  progress arc, pending/outstanding state, spawned Core, birth bloom, and sound.
- All three Wells share one physical model. Their glyph and state remain
  distinct overlays.

### Team Reactor

- Physical model: low open-throat docking station, unobstructed bank deck,
  exactly three circular charge-counter bezels, and exactly three rectangular
  integrity bezels.
- Renderer: team crest and color, 0–3 Core charge fills, 0–3 integrity fills,
  ownership ring, bank/Pulse response, damage, and destruction.
- Both teams share one physical model, rotated toward the arena. Team identity
  is never baked into the GLB.

The final panel of each sheet is an integration mockup. Its pale-lilac Core,
progress/ownership ring, glyph, crest, and pip lights are demonstrations of the
reserved renderer zones, not model materials or Meshy geometry.

The sheets are concept evidence only. After approval, generate separate clean,
camera-consistent physical-only front, right, rear, top, and gameplay-oblique
files. Do not submit the integration panel or a cropped sheet to Meshy.

`well-top-48px.png` and `reactor-top-48px.png` are deterministic reductions of
the strict top panels for thumbnail-scale silhouette judgment. They are not
repaints.

## Provenance

- Prompt mode: built-in `imagegen` with project references.
- Gameplay reference: `art/reviews/arc-relay-play-awareness/three-theater-overview-webgl.png`, SHA-256 `bee8f3065836f2440cb1c4071f6f3b2535356eb569f22ff05d42dd73ba0c887b`.
- Objective-motion reference: `art/reviews/arc-relay-presentation-batch-v1/03-motion-objective.png`, SHA-256 `14336a9867026736fa54ad2fa7d51ab4d72a44eaecd530a0d4bdc2a4db519be0`.
- Well source: `/Users/sebastian.lind/.codex/generated_images/019fee2d-edd3-7723-8309-6a84e5627578/exec-2ce3187e-c9cc-4d8b-a412-4c94d1281e09.png`; project SHA-256 `cdc5047190b03877230d94b9f2d47478d0337afe3ee395b4326dad347c44ef75`.
- Reactor first pass: `/Users/sebastian.lind/.codex/generated_images/019fee2d-edd3-7723-8309-6a84e5627578/exec-34f8d428-d45d-4edd-83d4-86fe4b2c66db.png`; project SHA-256 `447cc0ee0e082ac1d6112f459deb4af6a0af96a39d164d7da0ea2eed7ba8b3f9`.
- Reactor corrected source: `/Users/sebastian.lind/.codex/generated_images/019fee2d-edd3-7723-8309-6a84e5627578/exec-222aeb4f-e229-49f0-96a8-54ab747b894b.png`; project SHA-256 `4869ab7abccd45a128bfe71d68d2668a4a13fc4e730a3f234834e64d89a0bc42`.
- Exact prompts: `WELL-PROMPT.md` and `REACTOR-PROMPTS.md`.
