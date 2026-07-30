# Fused team-accent extraction

Use this only when a reviewed generated mesh has one fused material but clean,
bounded team-color paint. It is an offline authoring step, not a runtime
shader.

1. Preserve the source model and lossless provider base-color/emission maps.
   Align the maps on an exact common grid; do not classify a resized JPEG or a
   screenshot. Build a candidate from both color and emission, require a
   stronger two-channel seed in every kept component, grow only a few
   deterministic pixels inside the loose base-color candidate, close
   one-pixel holes, and prune small unseeded components. Hue alone admits
   reflections and weathering.
2. Classify faces from several strictly interior UV samples, never only
   vertices or edge midpoints. The validated Striker recipe uses seven
   Dunavant interior samples and a conservative five-of-seven vote. Record the
   threshold configuration, selected faces, selected surface area, boundary
   histogram, estimated purity/coverage, and exact mask/model hashes. Treat its
   numeric cyan thresholds as Striker evidence, not a universal palette rule.
3. Partition the original index stream into hull and accent primitives while
   sharing untouched position, normal, and UV accessors. The triangle multiset,
   scene graph, transforms, facing, and geometry-buffer hashes must remain
   exact.
4. Remove classified color/emission pixels from lossless hull maps. Give the
   accent material neutral white base/emission factors, no fixed base or
   emission texture, the original normal and metallic/roughness textures, and
   `extras.nilbotsRole = "team-accent"` on both primitive and material.
5. Require a deterministic rerun hash, structural validation, close top and
   gameplay-oblique renders in at least two team colors, and real-replay proof.
   The renderer supplies tint, emissive response, and the floor glow; do not
   bake bloom or one team's color into the asset.
