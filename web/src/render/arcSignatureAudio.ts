import type { ReplayModel } from '../replayModel';
import type {
  ArcSignatureAudioBinding,
  ArcSignaturePresentationDefinition,
} from './arcSignaturePresentation';
import {
  ARC_SIGNATURE_LEGACY_PRESENTATION_ID,
  ARC_SIGNATURE_PREVIOUS_PRESENTATION_ID,
  ARC_SIGNATURE_PRESENTATION_ID,
} from './arcSignaturePresentationId';
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

const legacyAudioBindings = new Map<string, readonly ArcSignatureAudioBinding[]>([
  ['vector-dash', [{ trigger: 'completed', cue: 'signature-motion' }]],
  ['prism-wall', [{ trigger: 'started', cue: 'signature-construct' }]],
  [
    'tractor-hook',
    [
      { trigger: 'started', cue: 'attack-windup' },
      {
        trigger: 'launched',
        cue: 'signature-tether',
        replacesGenericRelease: true,
      },
    ],
  ],
  ['survey-flare', [{ trigger: 'landed', cue: 'signature-survey' }]],
  ['null-field', [{ trigger: 'activated', cue: 'signature-null' }]],
  ['exchange', [{ trigger: 'completed', cue: 'signature-exchange' }]],
  ['hardlight-block', [{ trigger: 'started', cue: 'signature-construct' }]],
  ['target-paint', [{ trigger: 'acquired', cue: 'signature-target' }]],
  ['kinetic-burst', [{ trigger: 'completed', cue: 'signature-kinetic' }]],
  ['smoke-canister', [{ trigger: 'landed', cue: 'signature-smoke' }]],
]);

const previousArcTossAudio: readonly ArcSignatureAudioBinding[] = [
  { trigger: 'core-facts', cue: 'core-family' },
  { trigger: 'started', cue: 'silent' },
  {
    trigger: 'launched',
    cue: 'signature-arc-toss',
    priority: 6,
    tickOffset: 0.2,
  },
];

const projectileAudioDefinitions = new Map<
  string,
  ArcSignaturePresentationDefinition
>();
for (const definition of audioDefinitions.values()) {
  for (const profileId of definition.projectileAudioProfileIds ?? []) {
    if (projectileAudioDefinitions.has(profileId)) {
      throw new Error(
        `Duplicate Arc signature projectile audio profile '${profileId}'.`,
      );
    }
    projectileAudioDefinitions.set(profileId, definition);
  }
}

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
  const trigger = triggerForSignatureReason(reason);
  return trigger
    ? resolveArcSignatureAudioTriggerBinding(replay, signatureId, trigger)
    : null;
}

export function resolveArcSignatureAudioTriggerBinding(
  replay: Pick<ReplayModel, 'signaturePresentationId'>,
  signatureId: string,
  trigger: ArcSignatureAudioBinding['trigger'],
): ArcSignatureResolvedAudioBinding | null {
  const bindings =
    replay.signaturePresentationId === ARC_SIGNATURE_PRESENTATION_ID
      ? audioDefinitions.get(signatureId)?.audio
      : replay.signaturePresentationId ===
          ARC_SIGNATURE_PREVIOUS_PRESENTATION_ID
        ? signatureId === 'arc-toss'
          ? previousArcTossAudio
          : audioDefinitions.get(signatureId)?.audio
      : replay.signaturePresentationId ===
          ARC_SIGNATURE_LEGACY_PRESENTATION_ID
        ? legacyAudioBindings.get(signatureId)
        : null;
  const binding = bindings?.find((candidate) => candidate.trigger === trigger);
  if (!binding || binding.cue === 'core-family') return null;
  return binding as ArcSignatureResolvedAudioBinding;
}

export function resolveArcSignatureProjectileAudioBinding(
  replay: Pick<ReplayModel, 'signaturePresentationId'>,
  attackProfileId: string | undefined,
): ArcSignatureResolvedAudioBinding | null {
  if (
    (replay.signaturePresentationId !== ARC_SIGNATURE_PRESENTATION_ID &&
      replay.signaturePresentationId !==
        ARC_SIGNATURE_PREVIOUS_PRESENTATION_ID) ||
    attackProfileId === undefined
  ) {
    return null;
  }
  const definition = projectileAudioDefinitions.get(attackProfileId);
  if (!definition) return null;
  return resolveArcSignatureAudioTriggerBinding(
    replay,
    definition.signatureId,
    'projectile-launched',
  );
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
    case 'deployed':
    case 'triggered':
      return reason;
    case 'cancelled':
    case 'moved':
    case 'lock-lost':
    case 'owner-destroyed':
    case 'target-destroyed':
    case 'target-out-of-range':
    case 'target-out-of-sight':
    case 'abandoned-move':
    case 'cancelled-damage':
    case 'interrupted-damage':
    case 'interrupted-movement':
    case 'ended-null-field':
    case 'channel-ended':
    case 'replaced':
    case 'expired':
      return 'cancelled';
    default:
      return null;
  }
}
