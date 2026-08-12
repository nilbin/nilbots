import type { ArcSignaturePresentationDefinition } from '../arcSignaturePresentation';

export function defineArcSignature(
  definition: Omit<ArcSignaturePresentationDefinition, 'packageId'>,
): ArcSignaturePresentationDefinition {
  return Object.freeze({
    // Keep this literal in the leaf helper: importing the registry's runtime
    // constant from a module that the registry eagerly discovers creates an
    // ESM initialization cycle. The return type keeps it exact and checked.
    packageId: 'arc-relay-signatures-v1',
    ...definition,
  });
}
