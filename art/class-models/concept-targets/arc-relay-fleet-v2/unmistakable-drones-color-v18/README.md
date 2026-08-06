# Unmistakable-drones color v18 provenance

- Status: concept review only; no Meshy calls and no runtime changes.
- Accepted built-in image-generation source:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-3df1a645-73c9-44ae-9f64-0f2c0ac1fa51.png`
- Output: `../concept-sheet-unmistakable-drones-color-v18.png`
- Output SHA-256:
  `892c924dc218188a1748aa375c126b06ce67b97795e2b33eed8505a742e47e56`
- Gameplay-scale proof: `../gameplay-scale-unmistakable-drones-color-v18.png`
- Gameplay-scale SHA-256:
  `8145a2993f3e46f238cba74a19a545da7c20fd9ef4ebc85b9dfff714b4ccf9d9`
- Unmistakable-UAV first-pass built-in source:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-ae757f34-10d0-4d2f-9400-27cbf54a9654.png`
- Preserved UAV input: `input-unmistakable-uav-v16.png`
- UAV-input SHA-256:
  `5e13b17d15f3da6d3371bc37e323fffa756806177133df59f5633fa4e10f6f92`
- Sixteen-color second-pass built-in source:
  `019fc27e-33d3-7971-a896-3915521f3a0b/exec-034666ed-3ce0-4521-966d-d43632e0353c.png`
- Preserved color input: `input-sixteen-color-v17.png`
- Color-input SHA-256:
  `8419cf69b70c7996e2e3937b0d47fe5e6c670d2a1298f76b94d0396cafde674a`
- Preserved color gameplay proof: `input-sixteen-color-v17-gameplay.png`
- Color gameplay SHA-256:
  `8b80923f3e406ee12c16320c763a5d01790fde1af915e05d55f11d39e0e787ed`
- Exact v15 edit target: `../concept-sheet-drone-family-v15.png`
- V15 SHA-256:
  `6f0d83fd5e40465b5077bd5f06077f86703fbdc5e9c69be02eb9bbd15b526913`
- Generation route: three built-in `image_gen` precise-object edits, with
  each exact input visible in the conversation context.

## Exact unmistakable-UAV prompt

```text
Use case: precise-object-edit.
Asset type: unmistakable military-UAV correction for the exact Arc Relay 4x4 conceptual 3D roster.
Input images: Image 1 is the exact drone-family v15 edit target.

Primary request: the current drone attempt reads as armored hover pucks, not drones. Redesign exactly four cells into unmistakable real UAV/drone silhouettes using visible flight hardware: Kestrel row 1 col 1, Lantern row 2 col 1, Sunder row 4 col 1, and Veil row 4 col 3. Preserve all other cells exactly.

Shared hard drone gate: at 63 pixels, each of these four must be immediately identifiable as an unmanned aerial drone before color or labels. Use open circular rotor apertures with clearly visible static fan/propeller blades, slender but credible structural arms/booms, compact unmanned avionics bodies, exposed gimbal sensors, antennae, and absolutely NO cockpit, canopy, seat, pilot window, road-vehicle body, armored hover puck, or solid featureless lift pod. Military/industrial field UAVs, not consumer toys. They remain shown hovering low over a detached neutral shadow, with no underbody glow, but their construction may visibly be capable of flight.

KESTREL — ROW 1 COL 1 — FIXED-WING INTERCEPTOR UAV:
Make a clearly unmanned fixed-wing drone: one extremely narrow golden central fuselage with a single East/right spear nose, long thin swept wings, compact V-tail or notched tail at the rear, one obvious underslung forward sensor gimbal, and one visible protected rear pusher propeller inside an open circular duct. No cockpit/canopy and no second nose. Retain the fast single-point Kestrel identity, but it must resemble a modern fixed-wing interceptor UAV rather than a fantasy spaceship.

LANTERN — ROW 2 COL 1 — TRICOPTER SURVEY UAV:
Replace the solid three-lobed puck with a true tricopter: one compact petrol-teal central sensor/avionics pod, exactly three slim radial arms arranged in a triangle, and exactly three large open circular rotor rings at the arm ends. Each ring visibly contains dark rotor blades and a central motor hub. Preserve the tall bronze survey dish on a narrow mast and add one obvious underslung camera/sensor gimbal. The arms and open air between them create a lightweight aerial silhouette. No solid filled lift discs, no long boat hull, no twin nose.

SUNDER — ROW 4 COL 1 — QUADCOPTER DESIGNATOR UAV:
Replace the thick red X-shaped hover slab with a true quadcopter: one compact oxide-crimson central avionics body, exactly four long narrow structural arms in a clean X, and exactly four large open circular shrouded rotors with visibly exposed dark blades and motor hubs. Preserve one compact raised targeting/designator gimbal or optic turret in the center and one small underslung sensor ball. Large negative spaces must exist between the arms. No thick continuous X armor plate, no weapons, no cockpit.

VEIL — ROW 4 COL 3 — HEAVY HEXACOPTER SMOKE UAV:
Replace the solid purple triangular hover body with a true heavy-lift hexacopter: one compact central royal-purple payload body, exactly six short-to-medium articulated booms in a hexagonal arrangement, and exactly six clearly visible open circular rotor assemblies with dark blades and central hubs. Carry the two existing banks of cylindrical smoke canisters tightly across the central upper payload deck, with short rear smoke outlets. The six-rotor silhouette must be visibly different from Lantern's three and Sunder's four. No paired nose, catamaran, solid wedge, or cockpit.

Counting and distinctness gate:
- Kestrel: fixed wing plus one rear ducted pusher prop.
- Lantern: exactly 3 rotors.
- Sunder: exactly 4 rotors.
- Veil: exactly 6 rotors.
Do not give extra rotors to any of them. The different rotor counts and fixed-wing silhouette must remain countable at gameplay scale.

Hard invariants: preserve all sixteen count and exact 4x4 order; preserve Palisade, Towline, Patchbay, Mortar, Minesmith, Hush, Relay, Switchback, Longshot, Mason, Repulsor, Nest exactly. Preserve Patchbay's large cross/no tail; Towline hook; Minesmith's three Trip Nodes; Relay as the only fork; Mason yellow ring; Nest's two light-taupe Sentinel Seeds and darker olive carrier. Preserve no-underbody-glow across the fleet. All bodies have visible air separation and detached neutral-gray shadows, no colored floor wash. Preserve East/right facing, scale, padding, gray cards, gutters, camera, neutral lighting, clean industrial materials, class colors, and tile occupancy.

No toy quadcopter, consumer camera drone, glossy plastic, micro-drone, cockpit, canopy, humanoid pilot, landing gear, wheels, tracks, legs, high-altitude sky scene, flight trail, motion-blurred rotors, underbody glow, team cyan/red emission, black outline network, dense panel mosaic, extra decals, labels, text, logos, watermark, weapon fire, effects, scenery, fog, additions, omissions, or extra objects. Exactly sixteen machines.
```

## Exact sixteen-color prompt

```text
Use case: precise-object-edit.
Asset type: sixteen-class color-identity pass on the exact Arc Relay 4x4 conceptual 3D roster.
Input images: Image 1 is the exact unmistakable-UAV roster edit target.

Primary request: change ONLY fixed non-team paint colors and their large-panel coverage so all sixteen machines have clearly distinct color identities at 63 pixels. Preserve every geometry, silhouette, rotor count, payload, material construction, camera, shadow, and no-underbody-glow decision exactly from Image 1.

Color hierarchy: each bot gets one dominant unmistakable class color on roughly 40–55% of its broad external armor, arranged in two or three large contiguous panels rather than stripes or tiny accents. The rest remains warm pale titanium, medium gunmetal, charcoal only in true recesses, and functional unpainted metal. Use satin industrial enamel and credible metal roughness, not plastic. Adjacent bots and repeated mechanical families must be separable by hue AND value at thumbnail size.

Apply this exact palette by fixed grid position:
Row 1 col 1 Kestrel: rich saffron-gold yellow on wings and fuselage, graphite engines.
Row 1 col 2 Palisade: deep saturated emerald-green armor with pale titanium shield frame.
Row 1 col 3 Towline: strong burnt safety-orange armor, gunmetal winch/drum/crane.
Row 1 col 4 Patchbay: dominant warm white ceramic armor with clear jade-green medical panels; preserve the large pure-white medical cross against a darker jade center field.

Row 2 col 1 Lantern: clear petrol-turquoise body and rotor rims with a warm bronze dish; not green or blue.
Row 2 col 2 Mortar: light sandstone/sand-beige armor with a distinctly dark olive cannon mount and gunmetal barrel; tan is the dominant read.
Row 2 col 3 Minesmith: strong terracotta/vermilion-red carrier armor; preserve all three Trip Node mines as dark graphite/bronze deployed-prop hardware, never body-colored.
Row 2 col 4 Hush: deep aubergine/plum-purple armor with cool silver transverse bands.

Row 3 col 1 Relay: vivid medium cobalt-blue fork armor with pale titanium inner rails and neutral central cradle.
Row 3 col 2 Switchback: saturated wine-magenta/raspberry frame with pale inner faces.
Row 3 col 3 Longshot: light cold ice-blue armor with a dark navy rail spine and one narrow ochre rail marker; visibly much lighter than Relay.
Row 3 col 4 Mason: bright industrial chartreuse/yellow-green work ring and compact tool housings over a charcoal base; clearly greener than Kestrel's gold.

Row 4 col 1 Sunder: strong oxide-crimson red central body and arms with gunmetal rotor rings; unmistakably red, not orange or magenta.
Row 4 col 2 Repulsor: dominant aged copper/bronze radial armor with small dark petrol-green secondary panels; metallic copper is the main read.
Row 4 col 3 Veil: clear light lavender/lilac central body with bright ivory smoke canisters and dark graphite rotor hardware; much lighter and cooler than Hush.
Row 4 col 4 Nest: distinctive moss-olive/khaki-green carrier armor, darker and more yellow-olive than Palisade's emerald; preserve both carried Sentinel Seeds as light warm sandstone-taupe ceramic with dark gunmetal bearings/barrels and bronze details, visually separate from the carrier.

Hard distinctness gate: at 63 pixels no two cells may appear to use the same dominant color. Specifically keep gold separate from chartreuse; emerald separate from jade and moss; orange separate from vermilion, crimson, and copper; turquoise separate from cobalt and ice-blue; aubergine separate from magenta and lavender; sandstone separate from white and taupe payloads. Increase or decrease lightness as needed while keeping the exact assigned hue identity.

Hard geometry invariants: preserve Kestrel as fixed-wing UAV with rear ducted pusher prop; Lantern as exactly three visible open rotors; Sunder as exactly four visible open rotors; Veil as exactly six visible open rotors. Preserve visible static blades, thin structural arms, gimbals, no cockpits. Preserve all other silhouettes, exact 4x4 order/count, East/right facing, Patchbay cross/no tail, Towline hook, Mortar cannon, Minesmith Trip Nodes, Relay as only fork, Mason no cranes, Nest Sentinel Seeds, all air gaps and detached neutral shadows.

Team/presentation invariant: these are fixed matte class paints only. No team-colored emissive inlays, cyan/red ownership glow, broad emission, underbody glow, floor wash, colored shadow, bloom, or luminous paint. Preserve tiny neutral body indicators only. No black outlines, panel mosaics, toy plastic, candy gloss, camouflage, decals, labels, text, logos, watermark, weapon fire, effects, scenery, fog, additions, omissions, or extra objects. Exactly sixteen machines; color/material edit only.
```

## Exact accepted Repulsor correction prompt

```text
Use case: precise-object-edit.
Asset type: one-cell class-color collision correction on the exact Arc Relay 4x4 conceptual 3D roster.
Input images: Image 1 is the exact unmistakable-drone sixteen-color edit target.

Primary request: change ONLY Repulsor at row 4 column 2. Replace the broad orange/copper paint on its six large radial outer armor modules with a unique cool high-value burnished nickel/silver satin-metal finish. Keep the inner structural ring dark graphite/gunmetal. Confine warm copper/bronze to the six small functional emitter faces, narrow coil bands, and tiny fasteners only—less than 12% of Repulsor's visible area. Add a few restrained dark petrol-green secondary insets if needed, but the dominant thumbnail read must be COOL SILVER/NICKEL, not orange, bronze, teal, green, or white.

Hard distinctness purpose: Repulsor must no longer collide with Towline's safety orange, Minesmith's terracotta, Sunder's crimson, or Kestrel's gold. Its broad cool metallic silver radial body should be unique among the sixteen while remaining darker and more metallic than Patchbay's warm white ceramic and lighter than ordinary gunmetal mechanisms.

Hard invariants: preserve Repulsor's exact radial ring geometry, six modules, central aperture, proportions, position, East/right orientation, air gap, detached shadow, and all mechanical detail. Preserve every other cell and pixel-level design decision exactly, including the four unmistakable UAVs and their fixed-wing/3/4/6 rotor counts, all fifteen other class colors, Patchbay cross, Minesmith Trip Nodes, Mason chartreuse ring, Nest light-taupe Sentinel Seeds, no-underbody-glow treatment, camera, lighting, gray cards, and shadows.

No geometry change, no team cyan/red glow, no broad emission, no underbody glow, no floor wash, no toy chrome mirror, no new decals, text, labels, logos, watermark, effects, scenery, additions, omissions, or extra objects. Exactly sixteen machines. Change only Repulsor's broad armor material palette.
```
