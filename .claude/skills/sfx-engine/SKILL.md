---
name: sfx-engine
description: Generate private sound effects through the SFX Engine text-to-audio API, poll and download completed jobs, standardize review WAVs, and preserve prompt and provider provenance. Use for SFX Engine API integration, prompt-driven sound-pack generation, regenerating individual audio cues, or diagnosing SFX Engine authentication, job, download, and audio-format failures.
---

# SFX Engine

Use SFX Engine as a source generator, then retain auditable prompts and perform
only transparent delivery processing. Never store an API key in the repository.

## Before generating

1. Read `references/openapi.yaml` before changing request or response handling.
2. Inspect the target project's audio brief, current event mappings, review
   artifact, and output validation rules.
3. Confirm that the requested work authorizes generation-credit use.
4. Keep music and approved runtime assets unchanged unless the user explicitly
   asks to replace them.

For nilbots, read `art/audio/README.md` and `docs/AUDIO-DESIGN.md`. Treat regular
attacks as `wind-up -> beam`, `wind-up -> fizzle / interrupt / miss`, or
`wind-up -> cancel`; do not describe the resolved attack as a flying projectile.

## Credentials

Resolve the credential in this order:

1. `SFXENGINE_API_KEY` in the current process environment.
2. On macOS, login Keychain service `com.nilbots.sfxengine.api-key` for the
   current account.

Never print the key, include it in a prompt manifest, pass it as a command-line
argument, or commit it. Use `Authorization: Bearer <key>` only for the API
origin. Do not forward that header to a cross-origin generated-audio URL.

## Prompt and job workflow

- Keep each prompt within the documented 255-character limit.
- Name the event, temporal shape, material or energy language, and intended
  hierarchy. Add compact exclusions for common failure modes such as music,
  speech, retro bleeps, gunshots, explosions, or projectile flybys.
- Request integer durations from 1 through 47 seconds and set `isPublic` to
  `false`.
- Generate sequentially. Save each completed job before starting the next so a
  failed or interrupted run can resume without spending duplicate credits.
- Treat `processing` as asynchronous and poll `GET /sound-effect/{id}` until
  `generated` or `failed`. Do not blindly retry an ambiguous failed POST.
- Preserve the provider ID, generation timestamp, prompt, request duration,
  downloaded content type, source hash, output hash, and measured audio facts.
  Omit the returned URL and user ID from committed provenance.

Run the bundled generator with a JSON job file:

```bash
node .claude/skills/sfx-engine/scripts/generate-pack.mjs \
  --config path/to/private-pack-generation.json
```

Use `--dry-run` to validate prompts, paths, tooling, and credentials without
creating jobs. Use `--force <cue-id>` only when the user has authorized spending
another credit for that specific cue.

## Audio delivery

The generator uses macOS `afconvert` to decode provider output to 48 kHz stereo
PCM, then removes DC, applies short endpoint fades, and peak-normalizes without
adding synthesized layers. Keep the original download hash in provenance even
when only the standardized WAV is retained.

After generation, run the owning project's validators. Inspect duration,
channels, sample rate, peak, RMS, DC offset, clipping, stereo difference, file
size, and cue coverage. These checks establish technical fitness only; never
claim that a sound suits the game until the user listens to the review artifact.

For nilbots candidate packs:

1. Add the pack alongside existing candidates; do not silently replace one.
2. Keep every mapped and proposed cue in the same order as the comparison set.
3. Rebuild and validate the sound lab.
4. Follow the repository `replay-review` skill when delivering a listening URL.

## Reference

`references/openapi.yaml` is the saved SFX Engine OpenAPI 3.0.0 contract. It is
the authority for the base URL, authentication, fields, bounds, and response
shapes. Do not substitute undocumented parameters based on another provider.
