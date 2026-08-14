# Longshot redesign v2

Status: **Railback owner-approved for a Meshy 7 review candidate** as of
2026-08-14. The generated model is not a runtime promotion. The live Longshot
remains the approved Meshy 7 V3 model. Railback's provider and real-gameplay
evidence is ready for review; promotion still requires separate owner approval.

## Why this pass exists

The earlier refresh retained a roughly `2.2:1` rail-barge planform. Normalizing
that long axis to one gameplay tile left too little transverse mass, so the bot
still collapsed into a thin dash at normal camera distance. This pass removes
elongation as an identity requirement. Every candidate instead uses a broad,
substantial planform, meaningful height, exactly three large charge stages,
and a flush full-width beam shutter rather than a gun or barrel.

## Candidates

1. **Railback** — broad shoulders around an exposed three-stage conductor
   spine. This is the strongest current class read and the recommended base.
2. **Hammerhead** — the cleanest compact mass and broadest front read, but it
   risks becoming generic industrial machinery.
3. **Rail Citadel** — the most forceful rejection of the thin silhouette, but
   its near-square mass risks reading as an objective structure rather than a
   mobile bot.

`concept-comparison.png` places the separately generated oblique and inferred
strict-top views on one deterministic review board and includes an approximate
48 px proof. The small proof is a 2D scale check, not real-renderer approval.

## Approval and provider gate

The owner selected Railback on 2026-08-14. Its targeted identity edit pins the
canonical East-facing top silhouette, front/rear asymmetry, exactly three large
charge stages, and the full-width emitter shutter. Meshy receives four separate
camera-consistent images in its documented order: strict front first, then
strict right side, strict rear, and front-right three-quarter. The canonical
strict top remains the same-scale silhouette authority and is not substituted
for one of those four provider views.

The standard beam and Rail Line effects remain renderer-owned. A future model
must not bake a beam, team teal/cyan, team orange, hover glow, particles, or
timing into the asset.

## Provenance

- Generation route: built-in `imagegen`, `stylized-concept`, three independent
  oblique generations followed by three `precise-object-edit` inferred top
  views.
- Exact prompt set: `PROMPTS.md`.
- Review-board generator:
  `scripts/class-models/build-longshot-redesign-v2-concept-review.mjs`.
- Generated originals remain under
  `/Users/sebastian.lind/.codex/generated_images/019fee2d-edd3-7723-8309-6a84e5627578/`;
  the project copies in this folder are the durable sources.

| File | SHA-256 |
| --- | --- |
| `railback-oblique.png` | `41842ccd1b97cf7dd89bbf16817408d06f67ffbfa1ee90043d31370c57679df1` |
| `railback-top.png` | `1334f28f8c2b52fdedccdcd9d621ecbf34f5baa048baf2e5e4ab43ecc6d445f0` |
| `hammerhead-oblique.png` | `2ca94c80c500f69d7c3b37f1211933bfedadb5c8b959dbad1b0e324a99ff6ffe` |
| `hammerhead-top.png` | `a7715a90feb235de258e561cb5f3784e9ca4528de0053bd4d4d18efb95e0ee57` |
| `rail-citadel-oblique.png` | `ef176c40b2d745c3621f6207ef869f83294adb5160c34545dfa9e709c454ef30` |
| `rail-citadel-top.png` | `dc2206dd94a38ab4650b5bf959b1d4b6bcaeacbcfd4c1659cfae3ccfbcc46c15` |
| `concept-comparison.png` | `1d08e3b0bfa71290af359c80f5cda1731a17cb8452704aa1df064170d301aa4d` |
