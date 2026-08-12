import type { ArcSignaturePresentationDefinition } from '../arcSignaturePresentation';
import { ARC_SIGNATURE_PRESENTATION_ID } from '../arcSignaturePresentationId';

export function defineArcSignature(
  definition: Omit<ArcSignaturePresentationDefinition, 'packageId'>,
): ArcSignaturePresentationDefinition {
  return Object.freeze({
    packageId: ARC_SIGNATURE_PRESENTATION_ID,
    ...definition,
  });
}
