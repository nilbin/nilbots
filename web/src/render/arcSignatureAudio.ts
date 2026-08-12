import type { ReplayModel } from '../replayModel';
import type { ArcSignatureAudioBinding } from './arcSignaturePresentation';
import { ARC_SIGNATURE_PRESENTATION_ID } from './arcSignaturePresentationId';
import arcToss from './signatures/definitions/arcToss';
import exchange from './signatures/definitions/exchange';
import fallingStar from './signatures/definitions/fallingStar';
import hardlightBlock from './signatures/definitions/hardlightBlock';
import kineticBurst from './signatures/definitions/kineticBurst';
import nullField from './signatures/definitions/nullField';
import prismWall from './signatures/definitions/prismWall';
import railLine from './signatures/definitions/railLine';
import repairBeam from './signatures/definitions/repairBeam';
import sentinelSeed from './signatures/definitions/sentinelSeed';
import smokeCanister from './signatures/definitions/smokeCanister';
import surveyFlare from './signatures/definitions/surveyFlare';
import targetPaint from './signatures/definitions/targetPaint';
import tractorHook from './signatures/definitions/tractorHook';
import tripNode from './signatures/definitions/tripNode';
import vectorDash from './signatures/definitions/vectorDash';

const audioDefinitions = new Map(
  [
    arcToss,
    exchange,
    fallingStar,
    hardlightBlock,
    kineticBurst,
    nullField,
    prismWall,
    railLine,
    repairBeam,
    sentinelSeed,
    smokeCanister,
    surveyFlare,
    targetPaint,
    tractorHook,
    tripNode,
    vectorDash,
  ].map((definition) => [definition.signatureId, definition] as const),
);

export type ArcSignatureRuntimeAudioCue = Exclude<
  ArcSignatureAudioBinding['cue'],
  'core-family' | 'silent'
>;

export type ArcSignatureResolvedAudioBinding = ArcSignatureAudioBinding & {
  cue: ArcSignatureRuntimeAudioCue | 'silent';
};

/**
 * Browser-independent audio view of the typed presentation definitions.
 *
 * The visual registry uses `import.meta.glob` for discoverability, while bare-Node
 * replay tests cannot evaluate that Vite primitive. These imports consume the exact
 * same leaf definition objects, so the scheduler stays definition-driven without
 * pulling WebGL/Vite machinery into replay causality.
 */
export function resolveArcSignatureAudioBinding(
  replay: Pick<ReplayModel, 'signaturePresentationId'>,
  signatureId: string,
  reason: string,
): ArcSignatureResolvedAudioBinding | null {
  if (replay.signaturePresentationId !== ARC_SIGNATURE_PRESENTATION_ID) {
    return null;
  }
  const definition = audioDefinitions.get(signatureId);
  const trigger = triggerForSignatureReason(reason);
  if (!definition || !trigger) return null;
  const binding = definition.audio.find(
    (candidate) => candidate.trigger === trigger,
  );
  if (!binding || binding.cue === 'core-family') return null;
  return binding as ArcSignatureResolvedAudioBinding;
}

export function arcSignatureAudioDefinitionIds(): readonly string[] {
  return [...audioDefinitions.keys()].sort();
}

function triggerForSignatureReason(
  reason: string,
): ArcSignatureAudioBinding['trigger'] | null {
  switch (reason) {
    case 'started':
    case 'launched':
    case 'landed':
    case 'activated':
    case 'acquired':
    case 'completed':
      return reason;
    case 'cancelled':
    case 'moved':
    case 'lock-lost':
    case 'owner-destroyed':
    case 'target-destroyed':
    case 'target-out-of-range':
    case 'target-out-of-sight':
      return 'cancelled';
    default:
      return null;
  }
}
