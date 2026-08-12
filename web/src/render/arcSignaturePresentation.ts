import type { ReplayModel } from '../replayModel';
import { ARC_SIGNATURE_PRESENTATION_ID } from './arcSignaturePresentationId';
export { ARC_SIGNATURE_PRESENTATION_ID } from './arcSignaturePresentationId';

export type ArcSignaturePublicPhase =
  | 'tell'
  | 'active'
  | 'channel'
  | 'in-flight';

export type ArcSignatureTopology =
  | 'self-path'
  | 'construct-tiles'
  | 'owner-target'
  | 'target-area'
  | 'field-footprint'
  | 'persistent-prop'
  | 'objective-flight'
  | 'paired-actors'
  | 'frozen-line'
  | 'self-radius';

export type ArcSignatureCanvasMarker =
  | 'arrow'
  | 'panels'
  | 'tether'
  | 'braid'
  | 'calibration-field'
  | 'cardinal-target'
  | 'prop'
  | 'dark-field'
  | 'objective-arc'
  | 'paired-fold'
  | 'pressure-lane'
  | 'block'
  | 'target-brackets'
  | 'pressure-ring'
  | 'smoke-field';

export type ArcSignatureAudioCueIntent =
  | 'attack-windup'
  | 'projectile'
  | 'impact'
  | 'healing'
  | 'signature-motion'
  | 'signature-construct'
  | 'signature-tether'
  | 'signature-survey'
  | 'signature-null'
  | 'signature-smoke'
  | 'signature-exchange'
  | 'signature-target'
  | 'signature-kinetic'
  | 'core-family'
  | 'silent';

export interface ArcSignatureAudioBinding {
  trigger:
    | 'started'
    | 'launched'
    | 'landed'
    | 'activated'
    | 'acquired'
    | 'completed'
    | 'repair-fact'
    | 'damage-fact'
    | 'core-facts'
    | 'maintained'
    | 'cancelled';
  cue: ArcSignatureAudioCueIntent;
  /** A replacement is used instead of the shared regular release voice. */
  replacesGenericRelease?: boolean;
}

export interface ArcSignaturePrimitiveBudget {
  segments: number;
  panels: number;
  particles: number;
  markers: number;
  transparentLayers: number;
  persistentProps: number;
}

export interface ArcSignaturePresentationDefinition {
  packageId: typeof ARC_SIGNATURE_PRESENTATION_ID;
  signatureId: string;
  topology: ArcSignatureTopology;
  phases: readonly ArcSignaturePublicPhase[];
  canvasMarker: ArcSignatureCanvasMarker;
  webglPainterId: string;
  audio: readonly ArcSignatureAudioBinding[];
  reducedMotion: {
    retain: 'tell-and-footprint' | 'endpoints-and-outcome' | 'prop-and-capacity';
    travelSamples: number;
    particleScale: number;
    decorativeDrift: boolean;
  };
  budget: ArcSignaturePrimitiveBudget;
  fidelity: {
    reference: string;
    dominantSilhouette: string;
  };
}

export type ArcSignaturePresentationResolution =
  | { kind: 'legacy'; definition: null }
  | { kind: 'authored'; definition: ArcSignaturePresentationDefinition }
  | { kind: 'generic'; definition: null };

const definitionModules = import.meta.glob<ArcSignaturePresentationDefinition>(
  './signatures/definitions/*.ts',
  { eager: true, import: 'default' },
);

const definitions = new Map<string, ArcSignaturePresentationDefinition>();
for (const [path, definition] of Object.entries(definitionModules)) {
  if (definition.packageId !== ARC_SIGNATURE_PRESENTATION_ID)
    throw new Error(
      `Arc signature module '${path}' has package '${definition.packageId}'.`,
    );
  if (definition.webglPainterId !== definition.signatureId)
    throw new Error(
      `Arc signature '${definition.signatureId}' must own an equally named WebGL painter.`,
    );
  if (definitions.has(definition.signatureId))
    throw new Error(
      `Duplicate Arc signature presentation '${definition.signatureId}'.`,
    );
  definitions.set(definition.signatureId, definition);
}

export function arcSignaturePresentationDefinitions(): readonly ArcSignaturePresentationDefinition[] {
  return [...definitions.values()].sort((left, right) =>
    left.signatureId.localeCompare(right.signatureId),
  );
}

/**
 * Presence has compatibility meaning: old replay bytes retain their legacy
 * treatment, the known package is exact, and a future unknown package gets a
 * truthful generic marker rather than today's style by accident.
 */
export function resolveArcSignaturePresentation(
  replay: Pick<ReplayModel, 'signaturePresentationId'>,
  signatureId: string,
): ArcSignaturePresentationResolution {
  if (replay.signaturePresentationId == null)
    return { kind: 'legacy', definition: null };
  if (replay.signaturePresentationId !== ARC_SIGNATURE_PRESENTATION_ID)
    return { kind: 'generic', definition: null };
  const definition = definitions.get(signatureId);
  return definition
    ? { kind: 'authored', definition }
    : { kind: 'generic', definition: null };
}
