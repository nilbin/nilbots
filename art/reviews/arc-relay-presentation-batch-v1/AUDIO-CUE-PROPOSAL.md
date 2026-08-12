# Signature SFX proposal — restrained orbital machinery

This is a generation plan only. No SFX Engine request has been made for this
artifact.

## Direction

The accepted pack already established the right center of gravity: short,
spacey, low-mid, soft-edged, and sparse. Signatures should extend that language
without becoming a second soundtrack or a collection of arcade stingers.

- No ambience and no loops.
- No laser chirps, bright pings, electrical fizz, alarms, whip cracks, metallic
  screeches, explosions, or long reverb tails.
- No per-class regular-attack sound. All standard heads retain the same quiet
  `projectile` cue, including a matured miss/fizzle.
- Voluntary attack cancel remains silent.
- Events retain the existing arena-horizontal stereo pan. This is left/right
  positioning, not full 3D distance or front/back audio.
- At most two signature voices survive one tick, and each cue class coalesces to
  one voice. Existing projectile stacking remains capped at four staggered
  releases.

## Six private source generations

Approval of the whole batch authorizes exactly the six jobs below with
`isPublic: false`. Each is generated sequentially and recorded immediately. A
failed or disliked result is not automatically rerolled; a seventh call needs
new approval.

| # | Private source | Request | Runtime deliveries |
| --- | --- | --- | --- |
| 1 | Materialize | 1 s | `signature-construct` for Prism Wall and Hardlight Block |
| 2 | Tether | 1 s | `signature-tether` for Tractor Hook |
| 3 | Field family | 2 s | `signature-survey`, `signature-null`, and `signature-smoke`, derived from tonal center / dark center / filtered side air |
| 4 | Transfer | 1 s | short `signature-motion` for Vector Dash and paired `signature-exchange` |
| 5 | Target | 1 s | `signature-target` for Target Paint |
| 6 | Kinetic | 1 s | `signature-kinetic` for Kinetic Burst |

Proposed provider prompts, all within the API's 255-character limit:

1. `Restrained sci-fi construction cue: low ceramic-magnetic panels slot into place, soft mid-low clunk, matte and dry, no sparkle, no alarm, no explosion, no music, clean attack, 1 second.`
2. `Restrained sci-fi grapple launch: compact low mechanical thunk, cable tension and soft magnetic latch, no whip crack, no metal screech, no bright transient, no explosion, no music, 1 second.`
3. `Ambient sci-fi field aperture: warm low-mid tonal center with soft filtered air in the stereo sides, spacious but short, no sparkle, no high ping, no alarm, no music, 2 seconds.`
4. `Restrained sci-fi displacement cue: paired low airy pressure folds and muted servo motion, smooth spatial transfer, no zap, no sparkle, no impact, no music, 1 second.`
5. `Soft sci-fi target confirmation: low rounded optical pulse with a muted mechanical latch, precise but gentle, no beep, no ping, no alarm, no music, 1 second.`
6. `Restrained kinetic repulsion: compact low-mid pressure thump with a short soft air push, controlled and non-explosive, no crack, no sub-bass boom, no music, 1 second.`

## Authoritative cue mapping

| Presentation event | Cue behavior |
| --- | --- |
| Vector Dash terminal `completed` | one `signature-motion` |
| Prism Wall / Hardlight Block `started` active | one shared `signature-construct` |
| Tractor Hook launch | existing quiet wind-up, then `signature-tether` replacing—not layering—the generic release |
| Repair fact | existing `healing`; no channel loop |
| Survey Flare `landed` | one `signature-survey` |
| Falling Star damage resolve | existing `impact`, coalesced to one voice |
| Null Field `activated` | one `signature-null`; no sustained voice |
| Arc Toss | existing Core-family departure and landing outcome cues only |
| Exchange terminal `completed` | one `signature-exchange` |
| Rail Line resolve | existing wind-up and projectile; any signature damage adds one coalesced existing impact |
| Target Paint `started` active | one `signature-target`; segment consumption stays silent |
| Kinetic Burst terminal `completed` | one `signature-kinetic` |
| Smoke Canister `started` active | one `signature-smoke` |
| Level-up fact | existing `level-up` |
| Signature cancel/interruption/expiry/suppression | silent unless an existing concrete outcome fact already owns a cue |

## Delivery and comfort gates

Provider output is source material, not runtime-ready audio. Each approved
source is trimmed, channel-selected where useful, converted to 48 kHz stereo
AAC, faded, low-passed, and normalized before entering the manifest.

New signature cues must meet all of these decoded-delivery gates:

- duration at or below 0.70 s;
- spectral centroid at or below 1.2 kHz;
- energy above 4 kHz at or below 1.0%;
- energy above 8 kHz at or below 0.05%;
- energy above 12 kHz at or below 0.01%;
- effective sample peak at or below -22 dBFS after manifest gain;
- effective RMS at or below -34 dBFS after manifest gain;
- effective maximum high-band window RMS at or below -42 dBFS;
- cue gain no higher than 0.20, voice limit 1, and room send disabled;
- no DC offset, clipped samples, discontinuous cut, or mono phase inversion.

The final audit covers the **entire** Astral Relay pack again, not just the new
files. It includes synthetic crowded Tapline combinations: four staggered
projectiles plus impact, destruction, Core handling, level-up, and two
signature voices. The mixed result must remain below -6 dBTP without relying on
an audible limiter clamp, and a headphone listen is still required after the
numeric gate.

If a generated source cannot be made comfortable without losing its identity,
it is rejected and the mapped signature stays silent or reuses an accepted cue.
It is never shipped merely because generation spent a credit.
