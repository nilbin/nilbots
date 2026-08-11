# Meshy 7 Arc Relay fleet assessment

## Outcome

- All 15 remaining multi-image Meshy 7 tasks succeeded.
- Each task consumed 30 credits: 450 total, from 2,180 to 1,730.
- Every raw GLB and texture set was downloaded and provenance-hashed.
- Auto-rigging was disabled; no runtime asset was replaced or promoted.
- The selected Kestrel retexture is included only to complete the 4x4 overview.

## Provider-preview gate

The provider previews are sufficient for a first material/silhouette gate, not final geometry approval. Strong first-pass identity reads include Palisade, Hush, Longshot, Mason, Sunder, Repulsor, and Nest. The following should be checked before any runtime promotion:

| Class | Finding |
| --- | --- |
| Towline | Painted shell skews too close to the orange team color. |
| Patchbay | Mostly pale/ivory instead of the muted sage, coral, and bone separation. |
| Lantern | Purple/gold treatment is more saturated and glossy than the mature industrial target. |
| Mortar | A large pale forward material patch reads too close to the rejected white-nose motif. |
| Minesmith | Terracotta has drifted into bright team-orange territory. |
| Relay | Outer shell reads as team orange rather than oxide brick. |
| Switchback | Provider angle is dominated by the ivory half; verify the charcoal half and center seam in a spin review. |
| Veil | Sage identity reads, but the orange accents conflict with renderer-owned team color. |

No automatic retexture or retry was submitted. Meshy's current standard API documentation treats a resubmission as a new paid task; the account's free retries are a web-plan facility unless an API-created task is shown as retryable in the Meshy UI.

## Raw-master scale

These are deliberately preserved provider masters, not mobile payloads. They range from roughly 64–81 MB and 1.9–2.0 million triangles per class. The user deferred additional optimization, so no fleet review tiers or shipping artifacts were created in this batch.

## Next gate

Owner review of `provider-contact-sheet.png`, followed by one of:

1. accept specific classes for reversible in-game review-tier construction;
2. use free web retries where the Meshy UI exposes them for these tasks; or
3. authorize targeted 10-credit API retextures for material-only misses.
