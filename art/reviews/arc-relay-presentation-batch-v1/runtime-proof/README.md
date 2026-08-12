# Arc Relay presentation runtime proof

This is the retained implementation evidence for the approved Arc Relay
presentation batch. It is intentionally separate from the concept boards:
the boards define the target, while this directory records what the game
actually rendered from real Tapline matches.

## Result

- All **16/16 signatures** use the shared grammar-3 operation envelope and
  have a captured WebGL lifecycle.
- Only Null Field, Target Paint, Kinetic Burst, and Smoke Canister mint new
  concrete mechanic definition versions. The other twelve are still full
  grammar-3 participants; their established mechanics did not need empty
  aliases.
- Both retained matches crossed **two WASM runtimes** and preserve canonical
  replay hashes. Together they cover all 16 signature IDs, all 16 replay-owned
  regular-attack projectile IDs, and 44 real level-up facts.
- Browser proof covers desktop and phone landscape, all 16 start/action/resolve
  rows, four reduced-motion cases, six direct-seek/continuous-history
  comparisons, and one real level-up transient. It reports no page, console,
  or request errors.
- The credible sustained phone proxy uses Playwright WebKit on the host's
  reported **Apple GPU**: 27.92 rendered fps against the 30 fps mobile cap and
  49.96M weighted pixels/s against the 54M budget. The Chromium/SwiftShader
  sample is retained as a software-renderer diagnostic, not a phone claim.
- The signature audio crowd audit is recorded with the audio package; its
  worst tested mix is -7.43 dBTP against the -6 dBTP ceiling. No ambience is
  shipped or played.

## Evidence map

- `index.html` — phone-friendly visual gallery for the complete retained frame
  set, with a link into the live two-replay viewer.
- `proof-manifest.json` — canonical hashes, WASM provenance, exact class,
  projectile, signature, and level-up coverage.
- `replays/*.broadcast.json.gz` — compact spectator projections, each below
  the 300 KiB phone-gallery ceiling and hash-linked to its canonical replay.
- `captures/browser-proof.json` — machine-readable WebGL, viewport,
  reduced-motion, level-up, and seek evidence.
- `captures/signature-lifecycle-contact-sheet.webp` — labelled real-game
  start/action/resolve rows for all sixteen signatures.
- `captures/*-phone.webp` — phone-landscape overview frames.
- `performance/phone-webgl-webkit.json` — Apple-GPU sustained-render sample.
- `performance/phone-webgl-chromium.json` — explicitly non-credible
  SwiftShader performance caveat.
- `sources/` — the Orders-v2 and map-plan source used to exercise the
  remaining signature roster.

The canonical replays themselves are not retained: they include private bot
observations and are much larger than the public spectator projection.

## Reproduction

The retention input directories are scratch artifacts from the two recorded
WASM matches. With those directories available:

```sh
python3 scripts/retain-arc-relay-presentation-proof.py \
  --match core-roster=/path/to/core-wasm-match \
  --match remaining-roster=/path/to/remaining-wasm-match \
  --output art/reviews/arc-relay-presentation-batch-v1/runtime-proof \
  --source-revision "$(git rev-parse HEAD)"

cd web
npm run build:review
cd ..
node scripts/class-models/install-local-replay-gallery.mjs \
  art/reviews/arc-relay-presentation-batch-v1/runtime-proof/gallery-sources.json
node scripts/capture-arc-relay-presentation-proof.mjs
```

The capture tool fingerprints the complete review build, verifies retained
image hashes, and opens only a render profile that still has missing evidence.
Within an incomplete profile it loads one page, then seeks and captures every
missing frame without reloading models. A complete unchanged retry opens no
browser at all; a changed build invalidates the retained browser evidence.
The checked-in index carries `migratedFromCapturedImages: true` because the
fingerprint scope was narrowed after capture to exclude generated gallery
timestamps; the retained frames, lifecycle coordinates, seek verdicts, and
their hashes are unchanged. That one-time recovery path is not part of the
checked-in capture tool, so future build changes must produce fresh frames.

## Proof boundary

Automated evidence can prove identity, coverage, exact timing, visible geometry,
seek reproducibility, renderer budgets, and absence of runtime errors. The
contact sheet has also been visually compared with the approved concept boards
at gameplay scale. Final taste on a physical phone and headphones remains a
human approval step, not something a hash or headless browser can replace.
