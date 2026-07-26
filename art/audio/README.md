# nilbots audio candidates

This directory contains review-stage audio, not runtime viewer assets.

Run:

```bash
node scripts/generate-audio-candidates.mjs
node scripts/validate-audio-candidates.mjs
node scripts/build-audio-sound-lab-site.mjs
```

Then open `sound-lab/index.html` or serve `art/audio/sound-lab` from any static
server. Generation is deterministic: the same script revision produces the
same signed 16-bit, 44.1 kHz mono WAV files.

The build command creates the static worker package used for the owner-only
review site under the ignored `sandbox/audio-sound-lab-site` directory. It
reuses `.openai/hosting.json`; never create a second site for this review lab.

The three directions deliberately contain the same ten moments so review is
about sonic character rather than feature coverage:

- **Vector Tactical** — clean, dry, and precise.
- **Foundry Signal** — mechanical, weighted, and industrial.
- **Neon Circuit** — synthetic, musical, and expressive.

All candidates stay under `art/` until a direction is selected. This prevents
thirty review sounds from entering the self-contained replay viewer. The
chosen runtime assets will be copied into a dedicated web audio package,
compressed for delivery, and covered by asset and replay-scheduling tests.
