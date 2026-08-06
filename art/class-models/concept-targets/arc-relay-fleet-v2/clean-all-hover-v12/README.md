# Clean all-hover v12 provenance

- Status: concept review only; no Meshy calls and no runtime changes.
- Accepted built-in image-generation source:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-0a4040d3-e265-4bbd-ada3-cd322deeecd0.png`
- Output: `../concept-sheet-clean-all-hover-v12.png`
- Output SHA-256:
  `7a8e980576f1b36a24d951b44f2a062e3590f9c19a9db0027c35d16fee827c35`
- Gameplay-scale proof: `../gameplay-scale-clean-all-hover-v12.png`
- Gameplay-scale SHA-256:
  `0889fe51c9093830467c8494a96c04e04baa67cafd9d2dea27f1e911b7133499`
- All-hover first-pass built-in source:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-54a016bc-81cd-4785-a676-70259073506d.png`
- Preserved cleanup target: `input-all-hover-v11.png`
- Cleanup-target SHA-256:
  `caa3bc01a02b00a3b62f7af25616ad0e82880798489ce0c6a74e8312e9f3429f`
- Exact v10 edit target: `../concept-sheet-silhouette-distinct-v10.png`
- V10 SHA-256:
  `0d2ee8c349b1851aab2b8cb42830899f11d6863da3cda78a1a3527e875362714`
- Generation route: two built-in `image_gen` precise-object edits, with the
  input visible in the conversation context.

## Exact all-hover prompt

```text
Use case: precise-object-edit.
Asset type: locomotion correction for the exact Arc Relay 4x4 conceptual 3D roster.
Input images: Image 1 is the exact silhouette-distinct v10 edit target.

Primary request: make ALL SIXTEEN machines visibly levitate. Change ONLY the locomotion bases, ground clearance, underside field hardware, and shadows of the four currently tracked machines: Palisade at row 1 column 2, Mortar at row 2 column 2, Mason at row 3 column 4, and Nest at row 4 column 4. Remove every track, road wheel, tread belt, sprocket, skid, foot, landing leg, and physical ground-contact point from those four. Replace each tracked base with a distinct heavy low-hover platform that preserves its current upper body, rule-bearing hardware, industrial weight, color, scale, and silhouette identity.

Exact four conversions:
- Palisade, row 1 col 2: turn the tracked base into one broad rectangular armored grav-bastion with a deep continuous underbody field skirt and four large recessed corner lift plates. Preserve the massive upright green shield/projector face and rear equipment. Heavy and defensive, not a car, not a fork.
- Mortar, row 2 col 2: turn the tracks into a low wide trapezoidal artillery grav-sled with a stable armored perimeter and six broad recessed underside lift vanes. Preserve the exact large raised mortar tube, recoil mount, olive armor, and forward/right cannon truth. Heavy artillery, no twin nose.
- Mason, row 3 col 4: turn the tracks into a squat octagonal construction hover platform with a thick segmented field skirt and four blocky corner lift modules. Preserve the yellow circular rotary/tool body, central hub, and both articulated crane/tool arms. Industrial workshop weight, not a wheeled vehicle, not a fork.
- Nest, row 4 col 4: turn the tracked base into a broad low hexagonal carrier grav-barge with an enclosed armored perimeter field skirt and six large underside lift plates. Preserve the two folded olive deployable sentry pods and central carrier deck. Heavy payload carrier, one continuous front edge, no twin nose.

Hard hover gate for all sixteen: every complete chassis is suspended at a clear consistent low-hover air gap about 10–14% of body height. Beneath every bot, show a narrow dark technical underside or recessed field skirt plus a tight soft neutral-gray shadow directly below but visibly detached from the chassis. A readable strip of gray background separates solid body from shadow. Restrained warm-white or pale amber underbody light may reveal the lift hardware, without bloom. No component touches the ground.

Silhouette invariants: preserve the current single-point Kestrel; Patchbay's large matte white medical cross and compact cruciform platform; Lantern's single teardrop nose and dish; Relay as the ONLY open twin-prong/twin-tip front; Switchback's closed reversal frame; Veil's single broad smoke wedge; and every other current class silhouette and signature assembly. Do not introduce another fork, catamaran, split nose, road vehicle, or repeated generic hover puck. The four new hover bases must remain distinctly rectangular-bastion, trapezoidal-artillery, octagonal-workshop, and hexagonal-carrier at 63 pixels.

Preserve exact sixteen count, exact 4x4 order and positions, East/right facing, object scale, padding, gray cards, gutters, camera, clean broad mid-value armor treatment, sparse seams, realistic enamel/metal roughness, restrained wear, current fixed class colors, and neutral tiny indicators. No tracks or wheels anywhere in the roster. No high flight, aircraft landing gear, legs, team-colored cyan/red glow, energy bubbles, jets, beams, particles, broad bloom, black outline network, dense plate mosaic, toy simplification, candy gloss, extra decals, labels, words, numbers, logos, watermark, scenery, weapon fire, fog, or extra objects. Exactly sixteen visibly levitating machines.
```

## Exact accepted cleanup prompt

```text
Use case: precise-object-edit.
Asset type: restrained hardware and surface cleanup of the exact all-hover Arc Relay 4x4 conceptual 3D roster.
Input images: Image 1 is the exact edit target.

Primary request: make exactly three tightly scoped cleanup changes. Preserve everything else exactly.

1. Patchbay, row 1 column 4: completely remove the entire tall articulated arched/scorpion-tail arm above the medic. Remove all raised arm segments, pivots, pincers, and overhead appendages from base to tip. Leave a clean, low, unobstructed cruciform medical hover platform. Preserve the large matte white medical cross at its current size and position as the dominant identity. Keep only compact flush repair ports or short integrated nozzles inside the existing body silhouette; no arm, tail, crane, hook, antenna, mast, or raised appendage.

2. Mason, row 3 column 4: completely remove both long articulated crane/hook arms and every tall boom segment extending above or beyond the body. Preserve the circular yellow construction/work hub, central round mechanism, current heavy low-hover base, and construction-yellow palette. Replace the removed arms only with two or three SHORT compact fixed radial tool heads or blocky tool ports integrated tightly around the circular hub, never longer than the body radius and never forming a crane, hook, tail, or silhouette appendage. Towline at row 1 column 3 must remain the roster's only crane-and-hook machine.

3. Palisade row 1 col 2, Mortar row 2 col 2, Mason row 3 col 4, and Nest row 4 col 4: simplify ONLY the exterior surfaces of their new heavy hover skirts. Replace the dense small square tile grid, dark checker seams, and track-like repeated rectangular segmentation with four to eight broad uninterrupted armor faces, sparse shallow joints, a few large corner lift housings, and clean bevel shading. The underside may remain dark technical material, but the side skirt must not resemble hidden treads or a belt of track pads. Retain each distinct base outline: Palisade rectangular bastion, Mortar trapezoidal artillery sled, Mason squat octagonal workshop, Nest broad hexagonal carrier.

Hard invariants: all sixteen remain visibly levitating with no tracks, wheels, feet, skids, or ground contact. Preserve every low-hover air gap, recessed field hardware, detached soft shadow, restrained warm-white/amber underside light, exact 4x4 order, East/right facing, scale, padding, gray cards, gutters, camera, lighting, colors, and clean industrial material quality. Preserve every bot except the three edits above exactly. Preserve Kestrel's single lance nose; Patchbay's white cross; Lantern dish; Mortar cannon; Minesmith mine discs; Relay as the ONLY open twin-prong front; Switchback closed frame; Longshot rail; Sunder optic mast; Repulsor ring; Veil smoke canisters; Nest's two folded sentry pods; Towline's single crane and hook.

Do not remove any named signature hardware. Do not add new appendages. No black outline network, small tiled plate mosaic, repeated track-like skirt blocks, toy simplification, candy gloss, team-colored cyan/red glow, broad bloom, extra decals, labels, words, numbers, logos, watermark, scenery, weapon fire, fog, or extra objects. Exactly sixteen levitating machines.
```
