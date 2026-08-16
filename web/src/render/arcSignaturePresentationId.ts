export const ARC_SIGNATURE_PRESENTATION_ID =
  'arc-relay-signatures-v3' as const;

/** The one-tick Arc Toss release package retained by historical replays. */
export const ARC_SIGNATURE_PREVIOUS_PRESENTATION_ID =
  'arc-relay-signatures-v2' as const;

/**
 * V1 remains renderable and keeps its original audio bindings. New replays mint
 * V2 because presentation audio is part of replay identity.
 */
export const ARC_SIGNATURE_LEGACY_PRESENTATION_ID =
  'arc-relay-signatures-v1' as const;

export function isKnownArcSignaturePresentationId(
  value: string | null,
): boolean {
  return (
    value === ARC_SIGNATURE_PRESENTATION_ID ||
    value === ARC_SIGNATURE_PREVIOUS_PRESENTATION_ID ||
    value === ARC_SIGNATURE_LEGACY_PRESENTATION_ID
  );
}
