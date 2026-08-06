# Orientation-consistent v20 provenance

- Status: concept review only; no Meshy calls and no runtime changes.
- Canonical orientation reference:
  `../../../../class-look-concepts/arc-relay/premium-roster-v1/concept-sheet.png`
- Canonical reference SHA-256:
  `462af9a32953758100ade6dc8e12455b2484baacdc0410afc4ca35f8c3dc89b1`
- Exact v19 first-pass edit target:
  `../concept-sheet-rotorless-drones-color-v19.png`
- V19 SHA-256:
  `06d503ca0fa2f2746d9d396b82d75bc4eb803c5169fcea5ba352f0c8c46e195a`
- Built-in image-generation first-pass source:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-a971638d-828b-4737-87e5-0012dd0cccb3.png`
- Preserved first-pass output / exact second-pass edit target:
  `input-orientation-pass1.png`
- First-pass SHA-256:
  `06874894d49f8cfa1d3c241466d3bb9ac04634dea60ccfabcb7dbb0fbf659822`
- Built-in image-generation accepted second-pass source:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-b856f1fd-4f3a-4cf3-b2c3-32a263dc6ac9.png`
- Output: `../concept-sheet-orientation-consistent-v20.png`
- Output SHA-256:
  `0ee6b6ed041e5ac8031f6d3a28b2b301768be8f07057e23a8e35aff2e8879f31`
- Gameplay-scale proof:
  `../gameplay-scale-orientation-consistent-v20.png`
- Gameplay-scale SHA-256:
  `fd70a51347e0fd7e98a4b75edbb48a0ae231f91224353fce39650c4bf741e103`
- Generation route: two built-in `image_gen` precise-object edits. The first
  corrected the four ambiguous v19 bodies; the second retained that output and
  localized the remaining Lantern and Veil ambiguity.

## Exact first-pass prompt

```text
Use case: precise object edit for a 4x4 game-unit concept sheet.

IMAGE 1 is the canonical original 2D roster and is the authority for facing direction only.
IMAGE 2 is the exact accepted v19 3D concept sheet and is the image to edit.

PRIMARY REQUEST
Correct the inconsistent yaw/facing presentation in IMAGE 2 while preserving the accepted 3D designs. The entire roster must share one canonical forward direction: EAST / screen-right. Treat the Mortar in row 2 column 2 and Longshot in row 3 column 3 of IMAGE 2 as the in-sheet orientation anchors. Every functional forward axis should visually point parallel to those anchors: nose, primary sensor, gun, rail, hook delivery direction, or launch direction points toward the right side of its card; exhausts, tail structures, and rear hardware belong toward the left.

CHANGE ONLY THESE FOUR CELLS UNLESS ANOTHER CELL IS CLEARLY REVERSED:
1. Kestrel, row 1 column 1, gold flying-wing UAV: re-yaw the same rotorless flying-wing craft so its single sharp spear nose is unmistakably the rightmost point. Its broad trailing delta edge and rectangular vector-thrust exhaust hardware are on the left/rear. Keep the small sensor gimbal under the forward/right third. Do not redesign it and do not add rotors.
2. Lantern, row 2 column 1, petrol-teal twin-boom survey UAV: re-yaw the same rotorless drone so its compact central camera/sensor pod faces screen-right. The two tail booms and joined H-tail extend to the left/rear. Preserve its dish and high straight wing. The rightmost functional end is the sensor nose, never the tail. No rotors or propellers.
3. Sunder, row 4 column 1, crimson solid wedge designator: make its single subtle forward point and sensor aperture unmistakably the rightmost end. Its broad rear corners are left. Preserve the continuous solid red wedge/diamond body, center optic, and levitation.
4. Veil, row 4 column 3, lavender solid rounded-hex smoke unit: make one continuous front edge/nose unmistakably face right. Keep smoke canisters and vent/outlet hardware on the rear-left shoulders. Preserve its one-piece lavender body and avoid creating paired prongs or twin tips.

HARD INVARIANTS
- Preserve the exact 4x4 grid, card framing, orthographic-ish oblique camera, neutral light-gray backgrounds, soft contact shadows, scale, lighting, material quality, and spacing of IMAGE 2.
- Preserve all sixteen accepted, clearly distinct body colors.
- Preserve every accepted silhouette, family identity, equipment, payload, and cell placement apart from the minimal re-yaw needed above.
- Keep all units visibly levitating. No wheels touching the floor.
- No underbody team glow, no colored hover aura, no black cartoon outlines, no labels, no text.
- Preserve Patchbay's white medical cross.
- Preserve Minesmith's carried Trip Nodes and Nest's light warm sandstone-taupe folded Sentinel Seeds.
- Towline remains only crane/hook. Kestrel and Lantern remain rotorless drones. Sunder and Veil remain solid hover bodies.
- Do not mirror or rotate the entire sheet. Do not alter Mortar or Longshot.
- Do not add new accessories, weapons, fins, rotors, propellers, glow, decals, or structural complexity.

SUCCESS GATE
At a glance, a viewer should be able to draw one arrow toward screen-right through the functional front of every one of the 16 units. Kestrel, Lantern, Sunder, and Veil must no longer look left-facing, away-facing, or ambiguous. The result should otherwise look like the same accepted v19 sheet.
```

## Exact accepted second-pass prompt

```text
Use case: extremely localized orientation correction on a 4x4 game-unit concept sheet.

IMAGE 1 is the canonical original 2D roster and is orientation authority only.
IMAGE 2 is the exact current 3D target to edit.

The first orientation pass in IMAGE 2 successfully corrected Kestrel (row 1 column 1) and Sunder (row 4 column 1). Preserve those exactly. Now correct ONLY Lantern (row 2 column 1) and Veil (row 4 column 3). All other 14 cells must remain visually unchanged.

Canonical forward is EAST / screen-right, matching Mortar row 2 column 2 and Longshot row 3 column 3.

LANTERN — row 2 column 1, petrol-teal rotorless survey drone:
- Reorient the entire same drone so its longitudinal centerline runs left-to-right.
- The compact rounded camera/sensor nose MUST be at the rightmost end and look horizontally toward screen-right, not downward, toward camera, or left.
- Put both slender tail booms and the joined H-tail clearly behind it on the left side.
- Preserve one high straight cross-wing and the gold survey dish over the center.
- It must remain a plausible rotorless UAV, not a quadcopter, airplane with propeller, or symmetric satellite.
- Do not add rotors or propellers.

VEIL — row 4 column 3, lavender solid hover smoke unit:
- Give the same one-piece rounded-hex chassis an unmistakable shallow prow at the rightmost edge, like a restrained armored nose, without making a sharp car.
- Keep the bank of white smoke canisters and all dark vent grilles behind the midpoint, toward the left/rear.
- The right edge should have one centered forward sensor/aperture; the left edge can remain broader and visibly rear-like.
- Do not create two tips, prongs, a car silhouette, or a separate turret.

HARD INVARIANTS:
- Exact same 4x4 grid, cells, light gray background, camera, scale, shadows, lighting, materials, sixteen colors, identities, payloads, and levitation.
- Preserve Kestrel and Sunder exactly as shown in IMAGE 2.
- Preserve Patchbay cross, Minesmith Trip Nodes, Nest light-taupe Sentinel Seeds, Towline hook, and all other equipment.
- No underbody glow, hover aura, black outlines, text, labels, rotors, propellers, new weapons, or redesigns.
- Do not mirror, crop, rotate, or relight the entire sheet.
- Only Lantern and Veil may change, and only enough to make their screen-right facing direction unambiguous.

SUCCESS GATE:
Lantern's camera nose points right with its H-tail left. Veil's centered nose/sensor is right with canisters/rear hardware left. The other 14 cells look identical to IMAGE 2.
```
